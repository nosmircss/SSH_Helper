using System.Collections.Concurrent;
using System.Diagnostics;
using SSH_Helper.Models;
using SSH_Helper.Services.Notifications;

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
        private readonly Func<JobDefinition, bool, CancellationToken, Task>? _jobExecutionOverride;
        private readonly Action<JobDefinition>? _jobEvaluationFaultInjector;

        /// <summary>
        /// Optional Vault credential provider. Set after construction when VaultService becomes available.
        /// </summary>
        public VaultCredentialProvider? VaultCredentialProvider { get; set; }

        /// <summary>
        /// Optional notification service for dispatching notify-command notifications during scheduled job runs.
        /// </summary>
        public NotificationService? NotificationService { get; set; }

        /// <summary>
        /// Optional active-environment Vault profile name used as a scheduler default when jobs do not specify one.
        /// </summary>
        public string? EnvironmentVaultProfile { get; set; }

        private System.Threading.Timer? _timer;
        private int _evaluating; // 0 = idle, 1 = evaluating (Interlocked guard)
        private readonly SemaphoreSlim _concurrencyGate;
        private readonly ConcurrentDictionary<string, RunningJobInfo> _runningJobs = new();
        private readonly ConcurrentDictionary<long, Task> _scheduledExecutions = new();
        private readonly ConcurrentQueue<QueuedJob> _jobQueue = new();
        private readonly ConcurrentDictionary<string, byte> _queuedJobIds = new();
        private readonly CancellationTokenSource _disposalCts = new();
        private long _nextScheduledExecutionId;
        private volatile bool _shutdownRequested;
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
            : this(jobStorage, schedulingService, configService, presetManager, credentialProvider, null)
        {
        }

        internal JobExecutionService(
            JobStorageService jobStorage,
            SchedulingService schedulingService,
            ConfigurationService configService,
            PresetManager presetManager,
            ICredentialProvider credentialProvider,
            Func<JobDefinition, bool, CancellationToken, Task>? jobExecutionOverride,
            Action<JobDefinition>? jobEvaluationFaultInjector = null)
        {
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
            _schedulingService = schedulingService ?? throw new ArgumentNullException(nameof(schedulingService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _presetManager = presetManager ?? throw new ArgumentNullException(nameof(presetManager));
            _credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
            _jobExecutionOverride = jobExecutionOverride;
            _jobEvaluationFaultInjector = jobEvaluationFaultInjector;

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
        /// Blocks only if the same job is already running.
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

            // NO semaphore acquisition — run-now bypasses concurrency gate entirely
            if (!TryStartJob(jobId))
                return false;

            // Mark this as a run-now execution
            if (_runningJobs.TryGetValue(jobId, out var info))
                info.IsRunNow = true;

            try
            {
                await ExecuteTrackedJobAsync(job, isRunNow: true);
                return true;
            }
            catch (OperationCanceledException)
            {
                HandlePostExecution(job, success: false, isRunNow: true, wasCancelled: true);
                OnJobStateChanged(jobId, job.Name, JobExecutionState.Cancelled);
                OnJobCancelled(job);
                return false;
            }
            catch (Exception ex)
            {
                HandlePostExecution(job, success: false, isRunNow: true, wasCancelled: false);
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
            if (_shutdownRequested)
                return;

            if (Interlocked.CompareExchange(ref _evaluating, 1, 0) != 0)
                return;

            // Fire-and-forget the Task-returning evaluation (NOT async void)
            _ = EvaluateAndExecuteDueJobsAsync();
        }

        /// <summary>
        /// Core evaluation loop: iterates all jobs, determines which are due, and dispatches them
        /// through the concurrency gate or into the overflow queue.
        /// </summary>
        private Task EvaluateAndExecuteDueJobsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var jobs = _jobStorage.Jobs.Values.ToList();

                foreach (var job in jobs)
                {
                    if (_shutdownRequested)
                        break;

                    EvaluateJobForDueExecution(job, now);
                }

                _lastEvaluationUtc = now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Scheduler evaluation failed: {ex}");
            }
            finally
            {
                Interlocked.Exchange(ref _evaluating, 0);
            }

            return Task.CompletedTask;
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
                ReleaseConcurrencySlot();
                return;
            }

            try
            {
                await ExecuteTrackedJobAsync(job, isRunNow: false);
            }
            catch (OperationCanceledException)
            {
                HandlePostExecution(job, success: false, isRunNow: false, wasCancelled: true);
                OnJobStateChanged(job.Id, job.Name, JobExecutionState.Cancelled);
                OnJobCancelled(job);
            }
            catch (Exception ex)
            {
                HandlePostExecution(job, success: false, isRunNow: false, wasCancelled: false);
                OnJobFailed(job, ex.Message);
            }
            finally
            {
                CompleteJob(job.Id);
                ReleaseConcurrencySlot();
                if (!_shutdownRequested)
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
            var defaultVaultProfileOverride = ResolveJobDefaultVaultProfile(job);

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
            if (VaultCredentialProvider != null)
            {
                sshService.VaultService = VaultCredentialProvider.VaultService;
                sshService.EnvironmentVaultProfile = defaultVaultProfileOverride;
            }
            sshService.NotificationService = NotificationService;

            // Scheduler cancellation for both single-preset and folder jobs flows through
            // the per-run SSH service via Stop(), which cancels its internal execution token.
            using var reg = ct.Register(() => sshService.Stop());

            if (job.TargetType == JobTargetType.Folder)
            {
                results = await ExecuteFolderJobAsync(job, sshService, username, password, hosts, timeouts);
            }
            else
            {
                results = await ExecuteSinglePresetAsync(job, sshService, username, password, hosts, timeouts);
            }

            // Build result
            int succeeded = 0, cancelled = 0, failed = 0;
            foreach (var r in results)
            {
                if (r.Success) succeeded++;
                else if (r.WasCancelled) cancelled++;
                else failed++;
            }
            var unsuccessful = cancelled + failed;
            var wasCancelled = ct.IsCancellationRequested || cancelled > 0;
            var overallSuccess = unsuccessful == 0;

            var runResult = new JobRunResult
            {
                JobId = job.Id,
                JobName = job.Name,
                StartedUtc = _runningJobs.TryGetValue(job.Id, out var info) ? info.StartedUtc : DateTime.UtcNow,
                CompletedUtc = DateTime.UtcNow,
                Success = overallSuccess,
                WasCancelled = wasCancelled,
                HostsSucceeded = succeeded,
                HostsFailed = unsuccessful,
                ErrorMessage = wasCancelled
                    ? "Cancelled by user."
                    : overallSuccess ? null : string.Join("; ",
                    results.Where(r => !r.Success && !r.WasCancelled)
                           .Select(r => r.ErrorMessage)
                           .Where(m => m != null)
                           .Take(3)),
                HostOutputs = results.Select(r => new JobHostOutput
                {
                    HostAddress = r.Host?.IpAddress ?? "unknown",
                    Output = r.Output ?? string.Empty,
                    Success = r.Success,
                    WasCancelled = r.WasCancelled,
                    ErrorMessage = r.ErrorMessage,
                    Label = r.HistoryLabel,
                    LabelReplacesAddress = r.HistoryLabelReplacesAddress
                }).ToList()
            };

            JobCompleted?.Invoke(this, runResult);
            OnJobStateChanged(job.Id, job.Name,
                wasCancelled ? JobExecutionState.Cancelled
                    : overallSuccess ? JobExecutionState.Completed : JobExecutionState.Failed,
                wasCancelled ? null : overallSuccess ? null : $"{failed} host(s) failed");

            HandlePostExecution(job, overallSuccess, isRunNow, wasCancelled);
        }

        private Task ExecuteTrackedJobAsync(JobDefinition job, bool isRunNow)
        {
            var cancellationToken = GetTrackedJobCancellationToken(job.Id);
            if (_jobExecutionOverride != null)
                return _jobExecutionOverride(job, isRunNow, cancellationToken);

            return ExecuteJobCoreAsync(job, isRunNow, cancellationToken);
        }

        /// <summary>
        /// Builds HostConnection objects from the job's persisted host rows.
        /// </summary>
        private List<HostConnection> BuildHostConnections(JobDefinition job)
        {
            var hosts = new List<HostConnection>();
            var defaultVaultProfileOverride = ResolveJobDefaultVaultProfile(job);

            foreach (var row in job.Hosts)
            {
                if (!TryGetRowValue(row, "Host_IP", out var hostIp) || string.IsNullOrWhiteSpace(hostIp))
                    continue;

                var host = HostConnection.Parse(hostIp);

                // Apply per-row overrides from columns
                if (!TryGetExplicitPortFromHostValue(hostIp, out _) &&
                    TryGetRowValue(row, "port", out var portStr)
                    && int.TryParse(portStr, out var port)
                    && port > 0 && port <= 65535)
                {
                    host.Port = port;
                }

                var rowHasVaultPath = TryGetRowValue(row, "vault_path", out var vaultPath)
                    && !string.IsNullOrWhiteSpace(vaultPath);
                var vaultResolved = false;

                if (rowHasVaultPath &&
                    VaultCredentialProvider != null &&
                    VaultCredentialProvider.TryGetPassword(
                        vaultPath,
                        out var vaultUser,
                        out var vaultPassword,
                        defaultVaultProfileOverride))
                {
                    host.Username = vaultUser;
                    host.Password = vaultPassword;
                    vaultResolved = true;
                }

                if (!vaultResolved)
                {
                    if (TryGetRowValue(row, "username", out var user) && !string.IsNullOrEmpty(user))
                        host.Username = user;

                    if (TryGetRowValue(row, "password", out var pass) && !string.IsNullOrEmpty(pass))
                        host.Password = pass;
                }

                // Copy all row columns as variables for {{variable}} substitution
                foreach (var kvp in row)
                {
                    host.Variables[kvp.Key] = kvp.Value;
                }

                hosts.Add(host);
            }

            return hosts;
        }

        private static bool TryGetRowValue(
            IReadOnlyDictionary<string, string> row,
            string key,
            out string value)
        {
            foreach (var kvp in row)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = kvp.Value;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private static bool TryGetExplicitPortFromHostValue(string hostValue, out int port)
        {
            port = 0;
            if (string.IsNullOrWhiteSpace(hostValue))
            {
                return false;
            }

            var trimmed = hostValue.Trim();
            var colonIndex = trimmed.LastIndexOf(':');
            if (colonIndex <= 0 || colonIndex >= trimmed.Length - 1)
            {
                return false;
            }

            var portPart = trimmed[(colonIndex + 1)..];
            if (!int.TryParse(portPart, out var parsedPort) || parsedPort <= 0 || parsedPort > 65535)
            {
                return false;
            }

            port = parsedPort;
            return true;
        }

        /// <summary>
        /// Resolves credentials based on the job's credential mode.
        /// </summary>
        private (string username, string password) ResolveCredentials(JobDefinition job)
        {
            var defaultVaultProfileOverride = ResolveJobDefaultVaultProfile(job);

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

                case CredentialMode.Vault:
                    if (VaultCredentialProvider != null &&
                        !string.IsNullOrEmpty(job.VaultCredentialPath) &&
                        VaultCredentialProvider.TryGetPassword(
                            job.VaultCredentialPath,
                            out var vaultUser,
                            out var vaultPass,
                            defaultVaultProfileOverride))
                    {
                        return (vaultUser, vaultPass);
                    }
                    Debug.WriteLine($"Warning: Vault credential resolution failed for job '{job.Name}' at path '{job.VaultCredentialPath}'");
                    return (string.Empty, string.Empty);

                default:
                    return (string.Empty, string.Empty);
            }
        }

        private string? ResolveJobDefaultVaultProfile(JobDefinition job)
        {
            if (!string.IsNullOrWhiteSpace(job.VaultProfileName))
                return job.VaultProfileName;

            return string.IsNullOrWhiteSpace(EnvironmentVaultProfile)
                ? null
                : EnvironmentVaultProfile;
        }

        /// <summary>
        /// Builds SSH timeout options from preset and config defaults.
        /// </summary>
        private SshTimeoutOptions BuildTimeouts(JobDefinition job)
        {
            var config = _configService.GetCurrent();
            var preset = _presetManager.Get(job.TargetName);

            var commandTimeout = job.CommandTimeoutOverrideSeconds
                ?? (job.TargetType == JobTargetType.CustomPreset
                    ? config.Timeout
                    : preset?.Timeout ?? config.Timeout);
            var connectionTimeout = job.ConnectionTimeoutOverrideSeconds ?? config.ConnectionTimeout;

            return SshTimeoutOptions.Create(commandTimeout, connectionTimeout);
        }

        /// <summary>
        /// Executes a single preset or custom preset against the job's hosts.
        /// </summary>
        private async Task<List<ExecutionResult>> ExecuteSinglePresetAsync(
            JobDefinition job,
            SshExecutionService sshService,
            string username,
            string password,
            List<HostConnection> hosts,
            SshTimeoutOptions timeouts)
        {
            var preset = ResolvePresetForExecution(job);

            return await sshService.ExecutePresetAsync(hosts, preset, username, password, timeouts, allowFileSelectionDialogs: false);
        }

        private PresetInfo ResolvePresetForExecution(JobDefinition job)
        {
            if (job.TargetType == JobTargetType.CustomPreset)
            {
                if (string.IsNullOrWhiteSpace(job.CustomPresetCommands))
                {
                    throw new InvalidOperationException($"Job '{job.Name}' has no custom preset content");
                }

                return new PresetInfo
                {
                    Commands = job.CustomPresetCommands
                };
            }

            return _presetManager.Get(job.TargetName)
                ?? throw new InvalidOperationException($"Preset '{job.TargetName}' not found");
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
            SshTimeoutOptions timeouts)
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

            return await sshService.ExecuteFolderAsync(hosts, presets, username, password, timeouts, options, allowFileSelectionDialogs: false);
        }

        #endregion

        #region Job Lifecycle Tracking

        /// <summary>
        /// Atomically starts tracking a job execution. Returns false if the job is already running.
        /// </summary>
        private bool TryStartJob(string jobId)
        {
            if (_shutdownRequested)
                return false;

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

        private CancellationToken GetTrackedJobCancellationToken(string jobId)
        {
            if (_runningJobs.TryGetValue(jobId, out var info))
                return info.Cts.Token;

            return _disposalCts.Token;
        }

        /// <summary>
        /// Adds a job to the overflow queue once per pending execution.
        /// </summary>
        private bool TryQueueJob(JobDefinition job)
        {
            if (_shutdownRequested)
                return false;

            if (!_queuedJobIds.TryAdd(job.Id, 0))
            {
                Debug.WriteLine($"Skipping queue add for job '{job.Name}': already queued");
                return false;
            }

            _jobQueue.Enqueue(new QueuedJob(job.Id, DateTime.UtcNow));
            OnJobStateChanged(job.Id, job.Name, JobExecutionState.Queued,
                "Waiting for concurrency slot");
            Debug.WriteLine($"Job '{job.Name}' queued: all concurrency slots in use");
            return true;
        }

        /// <summary>
        /// Clears queued tracking for a job once its queued entry is consumed or discarded.
        /// </summary>
        private void ClearQueuedJob(string jobId)
        {
            _queuedJobIds.TryRemove(jobId, out _);
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
            while (!_shutdownRequested && _jobQueue.TryPeek(out var queued) && TryAcquireConcurrencySlot())
            {
                if (_jobQueue.TryDequeue(out queued))
                {
                    ClearQueuedJob(queued.JobId);
                    var job = _jobStorage.Get(queued.JobId);
                    if (job != null && job.IsEnabled && !_runningJobs.ContainsKey(job.Id))
                    {
                        StartTrackedScheduledExecution(job);
                    }
                    else
                    {
                        ReleaseConcurrencySlot();
                    }
                }
                else
                {
                    ReleaseConcurrencySlot();
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
        private void HandlePostExecution(JobDefinition job, bool success, bool isRunNow, bool wasCancelled)
        {
            if (success && job.ScheduleType == ScheduleType.OneTime)
            {
                _schedulingService.MarkOneTimeCompleted(job);
                _jobStorage.Save(job);
                Debug.WriteLine($"One-time job '{job.Name}' auto-disabled after successful execution");
            }
            else if (!success && !wasCancelled && job.ScheduleType == ScheduleType.OneTime && !isRunNow)
            {
                job.IsEnabled = false;
                job.DisabledReason = "One-time schedule failed";
                _jobStorage.Save(job);
                Debug.WriteLine($"One-time job '{job.Name}' auto-disabled after failed scheduled execution");
            }
            else if (wasCancelled)
            {
                Debug.WriteLine($"Job '{job.Name}' was cancelled");
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
                WasCancelled = false,
                HostsSucceeded = 0,
                HostsFailed = 0,
                ErrorMessage = errorMessage
            };

            JobCompleted?.Invoke(this, result);
        }

        private void OnJobCancelled(JobDefinition job)
        {
            var result = new JobRunResult
            {
                JobId = job.Id,
                JobName = job.Name,
                StartedUtc = _runningJobs.TryGetValue(job.Id, out var info) ? info.StartedUtc : DateTime.UtcNow,
                CompletedUtc = DateTime.UtcNow,
                Success = false,
                WasCancelled = true,
                HostsSucceeded = 0,
                HostsFailed = 0,
                ErrorMessage = "Cancelled by user.",
                HostOutputs = new List<JobHostOutput>()
            };

            JobCompleted?.Invoke(this, result);
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _shutdownRequested = true;
            Stop();
            _disposalCts.Cancel();

            // Cancel all running jobs
            foreach (var info in _runningJobs.Values)
            {
                info.Cts.Cancel();
            }

            WaitForTrackedScheduledExecutions();
            _timer?.Dispose();
            _concurrencyGate.Dispose();
            _disposalCts.Dispose();
        }

        #endregion

        #region Internal Helpers

        private void EvaluateJobForDueExecution(JobDefinition job, DateTime now)
        {
            var stage = "pre-check";

            try
            {
                _jobEvaluationFaultInjector?.Invoke(job);

                if (_shutdownRequested || !job.IsEnabled)
                    return;

                if (_runningJobs.ContainsKey(job.Id))
                {
                    Debug.WriteLine($"Skipping job '{job.Name}': already running (duplicate trigger)");
                    return;
                }

                if (_queuedJobIds.ContainsKey(job.Id))
                {
                    Debug.WriteLine($"Skipping job '{job.Name}': already queued");
                    return;
                }

                stage = "due-check";
                if (!IsJobDue(job, now))
                    return;

                stage = "semaphore acquisition";
                if (TryAcquireConcurrencySlot())
                {
                    stage = "dispatch";
                    StartTrackedScheduledExecution(job);
                }
                else if (!_shutdownRequested)
                {
                    stage = "queueing";
                    TryQueueJob(job);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Scheduler evaluation failed for job '{job.Name}' ({job.Id}) during {stage}: {ex}");
            }
        }

        private bool IsJobDue(JobDefinition job, DateTime now)
        {
            switch (job.ScheduleType)
            {
                case ScheduleType.Recurring:
                    return _schedulingService.GetMissedOccurrences(job.CronExpression, _lastEvaluationUtc).Count > 0;

                case ScheduleType.OneTime:
                    return job.OneTimeScheduleUtc.HasValue && job.OneTimeScheduleUtc.Value <= now;

                case ScheduleType.None:
                default:
                    return false;
            }
        }

        private bool TryAcquireConcurrencySlot()
        {
            if (_shutdownRequested)
                return false;

            try
            {
                return _concurrencyGate.Wait(0);
            }
            catch (ObjectDisposedException) when (_shutdownRequested || _disposed)
            {
                return false;
            }
        }

        private void ReleaseConcurrencySlot()
        {
            if (_shutdownRequested)
                return;

            try
            {
                _concurrencyGate.Release();
            }
            catch (ObjectDisposedException) when (_shutdownRequested || _disposed)
            {
                // Shutdown has already retired the gate; nothing else should be queued.
            }
        }

        private void StartTrackedScheduledExecution(JobDefinition job)
        {
            if (_shutdownRequested)
            {
                ReleaseConcurrencySlot();
                return;
            }

            var executionId = Interlocked.Increment(ref _nextScheduledExecutionId);
            var scheduledTask = ExecuteScheduledJobAsync(job);
            _scheduledExecutions[executionId] = scheduledTask;

            scheduledTask.ContinueWith(
                task => OnScheduledExecutionCompleted(executionId, task),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void OnScheduledExecutionCompleted(long executionId, Task task)
        {
            _scheduledExecutions.TryRemove(executionId, out _);

            if (task.IsFaulted)
                Debug.WriteLine($"Tracked scheduled execution faulted: {task.Exception}");
        }

        private void WaitForTrackedScheduledExecutions()
        {
            var scheduledTasks = _scheduledExecutions.Values.ToArray();
            if (scheduledTasks.Length == 0)
                return;

            try
            {
                if (!Task.WaitAll(scheduledTasks, millisecondsTimeout: 1000))
                {
                    Debug.WriteLine(
                        $"Scheduler shutdown timed out waiting for {scheduledTasks.Length} tracked scheduled execution(s).");
                }
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine($"Scheduler shutdown observed tracked task fault(s): {ex}");
            }
        }

        #endregion
    }
}

