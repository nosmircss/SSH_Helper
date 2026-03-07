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
            public bool IsRunNow { get; }

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
            ConfigurationService configService)
        {
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
            _schedulingService = schedulingService ?? throw new ArgumentNullException(nameof(schedulingService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));

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
        /// Placeholder for real SSH execution logic.
        /// TODO: Plan 03-03 replaces this stub with real SSH execution
        /// </summary>
        private async Task ExecuteJobCoreAsync(JobDefinition job, bool isRunNow, CancellationToken ct)
        {
            await Task.Delay(100, ct);

            HandlePostExecution(job, success: true);

            var result = new JobRunResult
            {
                JobId = job.Id,
                JobName = job.Name,
                StartedUtc = _runningJobs.TryGetValue(job.Id, out var info) ? info.StartedUtc : DateTime.UtcNow,
                CompletedUtc = DateTime.UtcNow,
                Success = true,
                HostsSucceeded = 0,
                HostsFailed = 0
            };

            JobCompleted?.Invoke(this, result);
            OnJobStateChanged(job.Id, job.Name, JobExecutionState.Completed);
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
