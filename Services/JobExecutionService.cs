using System.Collections.Concurrent;
using System.Diagnostics;
using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Timer-driven scheduler with concurrency control, FIFO queue, and crash recovery.
    /// Evaluates due jobs every 30 seconds on a ThreadPool thread, gates concurrent execution
    /// via SemaphoreSlim, and queues overflow with FIFO ordering.
    /// </summary>
    public sealed class JobExecutionService : IDisposable
    {
        #region Nested Types

        /// <summary>
        /// Event data raised when a job transitions between execution states.
        /// </summary>
        public class JobStateChangedEventArgs : EventArgs
        {
            public string JobId { get; }
            public string JobName { get; }
            public JobExecutionState State { get; }
            public string? Message { get; }

            public JobStateChangedEventArgs(string jobId, string jobName, JobExecutionState state, string? message = null)
            {
                JobId = jobId;
                JobName = jobName;
                State = state;
                Message = message;
            }
        }

        /// <summary>
        /// Tracks an in-progress job execution (in-memory only, NOT the persisted RunningJobState).
        /// </summary>
        private sealed class RunningJobInfo
        {
            public string JobId { get; }
            public DateTime StartedUtc { get; }
            public CancellationTokenSource Cts { get; }
            public bool IsRunNow { get; set; }

            public RunningJobInfo(string jobId, DateTime startedUtc, CancellationTokenSource cts, bool isRunNow)
            {
                JobId = jobId;
                StartedUtc = startedUtc;
                Cts = cts;
                IsRunNow = isRunNow;
            }
        }

        #endregion

        #region Fields

        private readonly JobStorageService _jobStorage;
        private readonly SchedulingService _schedulingService;
        private readonly ConfigurationService _configService;
        private readonly PresetManager _presetManager;
        private readonly ICredentialProvider _credentialProvider;

        private System.Threading.Timer? _timer;
        private int _evaluating; // 0 = idle, 1 = evaluating (Interlocked guard)
        private readonly SemaphoreSlim _concurrencyGate;
        private readonly ConcurrentDictionary<string, RunningJobInfo> _runningJobs = new();
        private readonly ConcurrentQueue<QueuedJob> _jobQueue = new();
        private readonly CancellationTokenSource _disposalCts = new();
        private bool _disposed;
        private DateTime _lastEvaluationUtc = DateTime.UtcNow;

        #endregion

        #region Events

        /// <summary>
        /// Raised when a job transitions between execution states (Queued, Started, Completed, Failed, Cancelled).
        /// </summary>
        public event EventHandler<JobStateChangedEventArgs>? JobStateChanged;

        /// <summary>
        /// Raised when a job execution completes (success or failure) with the run result.
        /// </summary>
        public event EventHandler<JobRunResult>? JobCompleted;

        #endregion

        #region Constructor

        public JobExecutionService(
            JobStorageService jobStorage,
            SchedulingService schedulingService,
            ConfigurationService configService,
            PresetManager presetManager,
            ICredentialProvider credentialProvider)
        {
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
            _schedulingService = schedulingService ?? throw new ArgumentNullException(nameof(schedulingService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _presetManager = presetManager ?? throw new ArgumentNullException(nameof(presetManager));
            _credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));

            var maxConcurrent = configService.GetCurrent().MaxConcurrentJobs;
            if (maxConcurrent <= 0) maxConcurrent = 3;
            _concurrencyGate = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Crash recovery: scans for orphaned jobs that were running when the app last crashed.
        /// Must be called BEFORE Start().
        /// </summary>
        public void Initialize()
        {
            foreach (var job in _jobStorage.Jobs.Values.ToList())
            {
                if (job.RunningState != null)
                {
                    Debug.WriteLine($"Orphaned job detected: '{job.Name}' (started {job.RunningState.StartedUtc:u})");

                    OnJobStateChanged(job.Id, job.Name, JobExecutionState.Failed,
                        "Application crashed during execution");

                    job.RunningState = null;
                    _jobStorage.Save(job);
                }
            }
        }

        /// <summary>
        /// Starts the 30-second evaluation timer. Fires immediately for the first evaluation.
        /// </summary>
        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _timer = new System.Threading.Timer(
                TimerCallback,
                state: null,
                dueTime: TimeSpan.Zero,
                period: TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Pauses the evaluation timer without disposing the service.
        /// </summary>
        public void Stop()
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Checks whether a specific job is currently executing.
        /// </summary>
        public bool IsJobRunning(string jobId)
        {
            return _runningJobs.ContainsKey(jobId);
        }

        /// <summary>
        /// Returns the IDs of all currently executing jobs.
        /// </summary>
        public IReadOnlyList<string> GetRunningJobIds()
        {
            return _runningJobs.Keys.ToList();
        }

        /// <summary>
        /// Number of jobs currently waiting in the overflow queue.
        /// </summary>
        public int QueuedJobCount => _jobQueue.Count;

        /// <summary>
        /// Number of jobs currently executing.
        /// </summary>
        public int RunningJobCount => _runningJobs.Count;

        /// <summary>
        /// Immediately executes a job, bypassing the concurrency gate entirely.
        /// Blocks if the same job is already running or has a drift warning.
        /// </summary>
        public async Task<bool> RunNowAsync(string jobId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(JobExecutionService));

            var job = _jobStorage.Get(jobId);
            if (job == null) return false;

            // Block if already running (per locked decision)
            if (_runningJobs.ContainsKey(jobId))
            {
                OnJobStateChanged(jobId, job.Name, JobExecutionState.Skipped, "Already running");
                return false;
            }

            // Block if drift warning (per locked decision: consistent blocking for both scheduled and run-now)
            if (job.HasDriftWarning)
            {
                OnJobStateChanged(jobId, job.Name, JobExecutionState.Skipped,
                    "Drift warning — re-acknowledge before execution");
                return false;
            }

            // NO semaphore acquisition — run-now bypasses concurrency gate entirely
            if (!TryStartJob(jobId))
                return false;

            // Mark this as a run-now execution
            if (_runningJobs.TryGetValue(jobId, out var info))
                info.IsRunNow = true;

            try
            {
                await ExecuteJobCoreAsync(job, isRunNow: true, _disposalCts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                OnJobStateChanged(jobId, job.Name, JobExecutionState.Cancelled);
                return false;
            }
            catch (Exception ex)
            {
                OnJobFailed(job, ex.Message);
                return false;
            }
            finally
            {
                CompleteJob(jobId);
                // No semaphore release — run-now never acquired one
                // No DrainQueue — run-now doesn't affect the scheduled queue
            }
        }

        /// <summary>
        /// Cancels a running job via its CancellationTokenSource.
        /// Returns true if the job was found and cancellation was requested.
        /// </summary>
        public bool CancelJob(string jobId)
        {
            if (_runningJobs.TryGetValue(jobId, out var info))
            {
                info.Cts.Cancel();
                return true;
            }
            return false;
        }

        #endregion

        #region Timer and Evaluation

        /// <summary>
        /// Timer callback on ThreadPool thread. Uses reentrancy guard to prevent overlapping evaluations.
        /// The guard release is in the async method's finally block, NOT here.
        /// </summary>
        private void TimerCallback(object? state)
        {
            if (Interlocked.CompareExchange(ref _evaluating, 1, 0) != 0)
                return;

            // Fire-and-forget the async evaluation (NOT async void)
            _ = EvaluateAndExecuteDueJobsAsync();
        }

        /// <summary>
        /// Core evaluation loop: iterates all jobs, determines which are due, and dispatches them
        /// through the concurrency gate or into the overflow queue.
        /// </summary>
        private async Task EvaluateAndExecuteDueJobsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;

                foreach (var job in _jobStorage.Jobs.Values.ToList())
                {
                    if (_disposalCts.IsCancellationRequested)
                        break;

                    if (!job.IsEnabled)
                        continue;

                    if (job.HasDriftWarning)
                    {
                        Debug.WriteLine($"Skipping job '{job.Name}': drift warning active");
                        continue;
                    }

                    if (_runningJobs.ContainsKey(job.Id))
                    {
                        Debug.WriteLine($"Skipping job '{job.Name}': already running (duplicate trigger)");
                        continue;
                    }

                    bool isDue = false;

                    switch (job.ScheduleType)
                    {
                        case ScheduleType.Recurring:
                            var missed = _schedulingService.GetMissedOccurrences(job.CronExpression, _lastEvaluationUtc);
                            if (missed.Count > 0)
                                isDue = true;
                            break;

                        case ScheduleType.OneTime:
                            if (job.OneTimeScheduleUtc.HasValue && job.OneTimeScheduleUtc.Value <= now)
                                isDue = true;
                            break;

                        case ScheduleType.None:
                            // Manual-only jobs are never triggered by the timer
                            break;
                    }

                    if (!isDue)
                        continue;

                    // Try to acquire a concurrency slot (non-blocking)
                    if (_concurrencyGate.Wait(0))
                    {
                        _ = ExecuteScheduledJobAsync(job);
                    }
                    else
                    {
                        _jobQueue.Enqueue(new QueuedJob(job.Id, DateTime.UtcNow));
                        OnJobStateChanged(job.Id, job.Name, JobExecutionState.Queued,
                            "Waiting for concurrency slot");
                        Debug.WriteLine($"Job '{job.Name}' queued: all concurrency slots in use");
                    }
                }

                _lastEvaluationUtc = now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Evaluation loop error: {ex}");
            }
            finally
            {
                Interlocked.Exchange(ref _evaluating, 0);
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Job Execution

        /// <summary>
        /// Executes a job with the semaphore already acquired. Releases the semaphore and
        /// drains the queue in the finally block.
        /// </summary>
        private async Task ExecuteScheduledJobAsync(JobDefinition job)
        {
            if (!TryStartJob(job.Id))
            {
                _concurrencyGate.Release();
                return;
            }

            try
            {
                await ExecuteJobCoreAsync(job, false, _disposalCts.Token);
            }
            catch (OperationCanceledException)
            {
                OnJobStateChanged(job.Id, job.Name, JobExecutionState.Cancelled);
            }
            catch (Exception ex)
            {
                HandlePostExecution(job, success: false);
                OnJobFailed(job, ex.Message);
            }
            finally
            {
                CompleteJob(job.Id);
                _concurrencyGate.Release();
                DrainQueue();
            }
        }

        /// <summary>
        /// Core execution pipeline: resolves credentials, builds host connections,
        /// creates a dedicated SshExecutionService instance, and dispatches to the
        /// appropriate execution path (single preset or folder).
        /// </summary>
        private async Task ExecuteJobCoreAsync(JobDefinition job, bool isRunNow, CancellationToken ct)
        {
            // Validate credential availability before attempting execution
            var (username, password) = ResolveCredentials(job);
            if (job.CredentialMode != CredentialMode.PerHostColumn
                && string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    $"Cannot resolve credentials for job '{job.Name}' (mode: {job.CredentialMode})");
            }

            var hosts = BuildHostConnections(job);
            if (hosts.Count == 0)
                throw new InvalidOperationException($"Job '{job.Name}' has no valid hosts");

            var timeouts = BuildTimeouts(job);
            List<ExecutionResult> results;

            // Create a new SshExecutionService per job run (NOT shared with UI)
            using var sshService = new SshExecutionService();
            sshService.UseConnectionPooling = false; // Avoid pool conflicts with UI instance

            // Link cancellation: when our CTS fires, stop the SSH service
            using var reg = ct.Register(() => sshService.Stop());

            if (job.TargetType == JobTargetType.Folder)
            {
                results = await ExecuteFolderJobAsync(job, sshService, username, password, hosts, timeouts, ct);
            }
            else
            {
                results = await ExecuteSinglePresetAsync(job, sshService, username, password, hosts, timeouts);
            }

            // Build result
            var succeeded = results.Count(r => r.Success);
            var failed = results.Count(r => !r.Success);
            var overallSuccess = failed == 0;

            var runResult = new JobRunResult
            {
                JobId = job.Id,
                JobName = job.Name,
                StartedUtc = _runningJobs.TryGetValue(job.Id, out var info) ? info.StartedUtc : DateTime.UtcNow,
                CompletedUtc = DateTime.UtcNow,
                Success = overallSuccess,
                HostsSucceeded = succeeded,
                HostsFailed = failed,
                ErrorMessage = overallSuccess ? null : string.Join("; ",
                    results.Where(r => !r.Success)
                           .Select(r => r.ErrorMessage)
                           .Where(m => m != null)
                           .Take(3)),
                HostOutputs = results.Select(r => new JobHostOutput
                {
                    HostAddress = r.Host?.IpAddress ?? "unknown",
                    Output = r.Output ?? string.Empty,
                    Success = r.Success,
                    ErrorMessage = r.ErrorMessage
                }).ToList()
            };

            JobCompleted?.Invoke(this, runResult);
            OnJobStateChanged(job.Id, job.Name,
                overallSuccess ? JobExecutionState.Completed : JobExecutionState.Failed,
                overallSuccess ? null : $"{failed} host(s) failed");

            HandlePostExecution(job, overallSuccess);
        }

        /// <summary>
        /// Builds HostConnection objects from the job's persisted host rows.
        /// </summary>
        private static List<HostConnection> BuildHostConnections(JobDefinition job)
        {
            var hosts = new List<HostConnection>();

            foreach (var row in job.Hosts)
            {
                if (!row.TryGetValue("Host_IP", out var hostIp) || string.IsNullOrWhiteSpace(hostIp))
                    continue;

                var host = HostConnection.Parse(hostIp);

                // Apply per-row overrides from columns
                if (row.TryGetValue("port", out var portStr)
                    && int.TryParse(portStr, out var port)
                    && port > 0 && port <= 65535)
                {
                    host.Port = port;
                }

                if (row.TryGetValue("username", out var user) && !string.IsNullOrEmpty(user))
                    host.Username = user;

                if (row.TryGetValue("password", out var pass) && !string.IsNullOrEmpty(pass))
                    host.Password = pass;

                // Copy all row columns as variables for {{variable}} substitution
                foreach (var kvp in row)
                {
                    host.Variables[kvp.Key] = kvp.Value;
                }

                hosts.Add(host);
            }

            return hosts;
        }

        /// <summary>
        /// Resolves credentials based on the job's credential mode.
        /// </summary>
        private (string username, string password) ResolveCredentials(JobDefinition job)
        {
            switch (job.CredentialMode)
            {
                case CredentialMode.Stored:
                    if (_credentialProvider.TryGetPassword(
                            CredentialTargets.JobPasswordTarget(job.Id),
                            out var storedUser, out var storedPass))
                    {
                        return (storedUser, storedPass);
                    }
                    Debug.WriteLine($"Warning: No stored credentials found for job '{job.Name}'");
                    return (string.Empty, string.Empty);

                case CredentialMode.InheritFromApp:
                    var config = _configService.GetCurrent();
                    var appUser = config.Username ?? string.Empty;
                    var appPass = string.Empty;
                    if (_credentialProvider.TryGetPassword(
                            CredentialTargets.DefaultPasswordTarget,
                            out _, out var defaultPass))
                    {
                        appPass = defaultPass;
                    }
                    else
                    {
                        Debug.WriteLine($"Warning: No app-level password found for job '{job.Name}' (InheritFromApp)");
                    }
                    return (appUser, appPass);

                case CredentialMode.PerHostColumn:
                    // Per-host credentials are already embedded in HostConnection from BuildHostConnections
                    return (string.Empty, string.Empty);

                default:
                    return (string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// Builds SSH timeout options from preset and config defaults.
        /// </summary>
        private SshTimeoutOptions BuildTimeouts(JobDefinition job)
        {
            var config = _configService.GetCurrent();
            var preset = _presetManager.Get(job.TargetName);

            // Use preset's Timeout override if set, otherwise config default
            var timeout = preset?.Timeout ?? config.Timeout;
            var connectionTimeout = config.ConnectionTimeout;

            return SshTimeoutOptions.Create(timeout, connectionTimeout);
        }

        /// <summary>
        /// Executes a single preset against the job's hosts.
        /// </summary>
        private async Task<List<ExecutionResult>> ExecuteSinglePresetAsync(
            JobDefinition job,
            SshExecutionService sshService,
            string username,
            string password,
            List<HostConnection> hosts,
            SshTimeoutOptions timeouts)
        {
            var preset = _presetManager.Get(job.TargetName)
                ?? throw new InvalidOperationException($"Preset '{job.TargetName}' not found");

            return await sshService.ExecutePresetAsync(hosts, preset, username, password, timeouts);
        }

        /// <summary>
        /// Executes all presets in a folder against the job's hosts.
        /// Per locked decision: direct children only (no recursive subfolder inclusion).
        /// Per locked decision: folder job counts as 1 concurrency slot regardless of preset count.
        /// </summary>
        private async Task<List<ExecutionResult>> ExecuteFolderJobAsync(
            JobDefinition job,
            SshExecutionService sshService,
            string username,
            string password,
            List<HostConnection> hosts,
            SshTimeoutOptions timeouts,
            CancellationToken ct)
        {
            // Get direct children only (per locked decision: no recursive subfolder inclusion)
            var presetNames = _presetManager.GetPresetsInFolder(job.TargetName).ToList();

            // Build preset dictionary, skip null entries
            var presets = new Dictionary<string, PresetInfo>();
            foreach (var name in presetNames)
            {
                var preset = _presetManager.Get(name);
                if (preset != null)
                    presets[name] = preset;
            }

            if (presets.Count == 0)
                throw new InvalidOperationException($"Folder '{job.TargetName}' contains no presets");

            var options = new FolderExecutionOptions
            {
                SelectedPresets = presets.Keys.ToList(),
                RunPresetsInParallel = job.FolderExecutionMode == FolderExecutionMode.Parallel,
                StopOnFirstError = job.StopOnError
            };

            return await sshService.ExecuteFolderAsync(hosts, presets, username, password, timeouts, options);
        }

        #endregion

        #region Job Lifecycle Tracking

        /// <summary>
        /// Atomically starts tracking a job execution. Returns false if the job is already running.
        /// </summary>
        private bool TryStartJob(string jobId)
        {
            var cts = new CancellationTokenSource();
            var info = new RunningJobInfo(jobId, DateTime.UtcNow, cts, isRunNow: false);

            if (!_runningJobs.TryAdd(jobId, info))
            {
                cts.Dispose();
                return false;
            }

            // Persist RunningState for crash recovery
            var job = _jobStorage.Get(jobId);
            if (job != null)
            {
                job.RunningState = new RunningJobState { StartedUtc = info.StartedUtc };
                _jobStorage.Save(job);
            }

            OnJobStateChanged(jobId, job?.Name ?? jobId, JobExecutionState.Started);
            return true;
        }

        /// <summary>
        /// Clears job tracking and persisted RunningState after execution completes.
        /// </summary>
        private void CompleteJob(string jobId)
        {
            if (_runningJobs.TryRemove(jobId, out var info))
            {
                info.Cts.Dispose();
            }

            var job = _jobStorage.Get(jobId);
            if (job != null)
            {
                job.RunningState = null;
                _jobStorage.Save(job);
            }
        }

        /// <summary>
        /// Drains the overflow queue when concurrency slots become available.
        /// </summary>
        private void DrainQueue()
        {
            while (_jobQueue.TryPeek(out var queued) && _concurrencyGate.Wait(0))
            {
                if (_jobQueue.TryDequeue(out queued))
                {
                    var job = _jobStorage.Get(queued.JobId);
                    if (job != null && job.IsEnabled && !_runningJobs.ContainsKey(job.Id))
                    {
                        _ = ExecuteScheduledJobAsync(job);
                    }
                    else
                    {
                        _concurrencyGate.Release();
                    }
                }
                else
                {
                    _concurrencyGate.Release();
                }
            }
        }

        #endregion

        #region Post-Execution and One-Time Handling

        /// <summary>
        /// Handles post-execution logic including one-time job auto-disable.
        /// Called after ExecuteJobCoreAsync completes. Plan 03-03 will preserve this call
        /// when replacing the stub with real SSH execution.
        ///
        /// Re-trigger protection for one-time jobs:
        /// After MarkOneTimeCompleted sets IsEnabled=false, the evaluation loop's
        /// early "if (!job.IsEnabled) continue" check prevents re-triggering on the next cycle.
        /// </summary>
        private void HandlePostExecution(JobDefinition job, bool success)
        {
            if (success && job.ScheduleType == ScheduleType.OneTime)
            {
                _schedulingService.MarkOneTimeCompleted(job);
                _jobStorage.Save(job);
                Debug.WriteLine($"One-time job '{job.Name}' auto-disabled after successful execution");
            }
            else if (!success && job.ScheduleType == ScheduleType.OneTime)
            {
                // Failed one-time jobs remain enabled so the user can retry or reschedule.
                // They will not re-trigger automatically because OneTimeScheduleUtc <= now
                // only fires once per evaluation window, and after failure the job stays
                // in the _runningJobs dictionary until CompleteJob clears it.
                Debug.WriteLine($"One-time job '{job.Name}' failed; remains enabled for retry");
            }
            else if (success)
            {
                Debug.WriteLine($"Job '{job.Name}' completed successfully");
            }
        }

        #endregion

        #region Event Helpers

        private void OnJobStateChanged(string jobId, string jobName, JobExecutionState state, string? message = null)
        {
            JobStateChanged?.Invoke(this, new JobStateChangedEventArgs(jobId, jobName, state, message));
        }

        private void OnJobFailed(JobDefinition job, string errorMessage)
        {
            OnJobStateChanged(job.Id, job.Name, JobExecutionState.Failed, errorMessage);

            var result = new JobRunResult
            {
                JobId = job.Id,
                JobName = job.Name,
                StartedUtc = _runningJobs.TryGetValue(job.Id, out var info) ? info.StartedUtc : DateTime.UtcNow,
                CompletedUtc = DateTime.UtcNow,
                Success = false,
                HostsSucceeded = 0,
                HostsFailed = 0,
                ErrorMessage = errorMessage
            };

            JobCompleted?.Invoke(this, result);
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposalCts.Cancel();
            _timer?.Dispose();

            // Cancel all running jobs
            foreach (var info in _runningJobs.Values)
            {
                info.Cts.Cancel();
            }

            _concurrencyGate.Dispose();
            _disposalCts.Dispose();
            _disposed = true;
        }

        #endregion
    }
}
