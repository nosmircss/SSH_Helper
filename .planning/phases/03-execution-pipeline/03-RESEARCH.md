# Phase 3: Execution Pipeline - Research

**Researched:** 2026-03-07
**Domain:** Background job execution, concurrency control, crash recovery in .NET 8 WinForms
**Confidence:** HIGH

## Summary

Phase 3 builds the runtime engine that connects the scheduling infrastructure (Phase 2) to the existing SSH execution service. The core challenge is bridging a `System.Threading.Timer` callback (ThreadPool, synchronous signature) to the async `SshExecutionService.ExecutePresetAsync` / `ExecuteFolderAsync` methods, while managing concurrency slots, duplicate-run prevention, crash recovery, and run-now bypasses -- all without freezing the WinForms UI thread.

The existing codebase provides strong foundations: `SshExecutionService` already handles async multi-host execution with `CancellationTokenSource`, `SchedulingService` provides stateless `GetNextRunUtc()` for due-job evaluation, `JobStorageService` persists job definitions, and `PresetManager` resolves preset names and folder contents. The new `JobExecutionService` orchestrates these existing services under a timer-driven evaluation loop.

**Primary recommendation:** Build a single `sealed class JobExecutionService : IDisposable` that owns the `System.Threading.Timer`, a `SemaphoreSlim` for concurrency limiting, a `ConcurrentDictionary<string, RunningJobState>` for tracking active runs, and a `ConcurrentQueue<QueuedJob>` for overflow. Each job run gets its own `SshExecutionService` instance (they are lightweight, support independent `CancellationTokenSource` per execution) to avoid contention with the manual UI execution path.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Default max concurrent jobs: 3 (user-configurable via settings)
- Concurrency is per-job (one job = one slot, regardless of how many hosts it targets)
- When a job is already running and its next cron trigger fires, skip the trigger and log it -- no queuing of duplicate runs
- When multiple jobs are queued waiting for a slot, execute in FIFO order (first due, first run)
- Default folder job execution mode: sequential (configurable per-job to parallel)
- Only direct children of the target folder are executed -- no recursive subfolder inclusion
- Per-job configurable stop-on-error toggle: continue through all presets (default) or stop on first failure
- A folder job counts as 1 concurrency slot regardless of how many presets it contains or whether running in parallel mode
- Run-now bypasses the concurrency queue -- fires immediately regardless of how many jobs are already running
- Block run-now while the same job is already running (disable button, show "Already running" status)
- Immediate cancellation via CancellationToken -- same behavior as existing manual execution cancel in SshExecutionService
- Drift detection blocks both scheduled execution and run-now (consistent with Phase 1 decision: user must re-acknowledge before any execution)
- Track running state in the jobs file via a RunningState field on JobDefinition (null when idle; set with start time when running)
- On startup, any job with RunningState set is treated as orphaned -- mark as failed
- Best-effort partial results: if any output was captured before the crash, preserve it in the run record with per-host completion status
- No auto-retry of orphaned jobs -- consistent with "always skip missed runs" policy; user can run-now to retry
- Orphaned run notification: log entry only (no startup popup); Phase 5 UI will surface the failed status
- System.Threading.Timer on ThreadPool for UI-independent scheduling (RELY-03)
- 30-second evaluation interval (research recommendation from STATE.md)
- Timer callback evaluates due jobs, queues or executes them, independent of UI thread and modal dialogs

### Claude's Discretion
- JobExecutionService internal design and method signatures
- Scheduler timer lifecycle management (start/stop/dispose)
- How execution results are structured before Phase 4 adds full history
- Event patterns for job state changes (started, completed, failed, queued)
- Queue data structure choice (ConcurrentQueue, Channel, etc.)
- How run-now interacts with the scheduler timer's evaluation cycle
- Thread synchronization strategy for job state transitions

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| EXEC-01 | Scheduler evaluates due jobs and executes them automatically while the application is running | System.Threading.Timer with 30s interval, SchedulingService.GetNextRunUtc() for due evaluation, reentrancy guard to prevent overlapping timer callbacks |
| EXEC-02 | User can trigger a job immediately via run-now action | RunNowAsync method on JobExecutionService that bypasses SemaphoreSlim concurrency gate, checks duplicate-run and drift blocking |
| EXEC-03 | User can cancel a running job mid-execution | CancellationTokenSource per running job tracked in ConcurrentDictionary, Cancel() propagated to SshExecutionService |
| EXEC-04 | User can configure the maximum number of concurrent jobs | MaxConcurrentJobs int property on AppConfiguration, SemaphoreSlim initialized with this value, configurable via settings UI |
| EXEC-05 | Excess due jobs queue until execution slots become available | ConcurrentQueue<QueuedJob> for FIFO overflow, dequeue attempted after each job completion releases semaphore |
| EXEC-06 | Folder jobs execute all presets in the target folder | PresetManager.GetPresetsInFolder() to resolve direct children, SshExecutionService.ExecuteFolderAsync() or per-preset sequential calls |
| EXEC-07 | User can configure folder job execution order per job (sequential or parallel) | New FolderExecutionMode enum/property on JobDefinition, maps to FolderExecutionOptions.RunPresetsInParallel |
| RELY-02 | Jobs orphaned by application crash are detected and marked as failed on next startup | RunningState field on JobDefinition, startup scan in JobExecutionService.Initialize(), orphaned jobs logged and marked failed |
| RELY-03 | Scheduler timer operates independently of UI thread | System.Threading.Timer runs on ThreadPool, no Control.Invoke needed for evaluation logic, events marshaled to UI thread only for display |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Threading.Timer | .NET 8 BCL | 30s periodic evaluation of due jobs | Locked decision; ThreadPool-based, UI-independent |
| SemaphoreSlim | .NET 8 BCL | Concurrency slot limiting (default 3) | Async-friendly, lightweight, supports WaitAsync with CancellationToken |
| ConcurrentQueue<T> | .NET 8 BCL | FIFO overflow queue for excess due jobs | Lock-free, thread-safe enqueue/dequeue |
| ConcurrentDictionary<string, RunningJobState> | .NET 8 BCL | Track active job runs by job ID | Thread-safe lookup for duplicate detection and cancellation |
| CancellationTokenSource | .NET 8 BCL | Per-job cancellation | Existing pattern in SshExecutionService |

### Supporting (already in project)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Cronos | 0.11.1 | Cron next-occurrence calculation | Via SchedulingService.GetNextRunUtc() -- already consumed |
| Newtonsoft.Json | 13.0.3 | RunningState serialization in jobs.json | Via JobStorageService persistence -- already consumed |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.Threading.Timer | PeriodicTimer (.NET 6+) | PeriodicTimer is naturally non-reentrant and async-native, but the user locked System.Threading.Timer. Use reentrancy guard instead. |
| ConcurrentQueue | Channel<T> | Channel provides bounded capacity and backpressure. For this use case (small queue, simple FIFO, no backpressure needed), ConcurrentQueue is simpler and sufficient. |
| SemaphoreSlim | Custom lock-based slot counter | SemaphoreSlim is the standard .NET primitive for this exact problem. No reason to hand-roll. |

## Architecture Patterns

### Recommended Project Structure
```
Services/
  JobExecutionService.cs      # Timer, concurrency, orchestration (new)
Models/
  JobDefinition.cs            # Add RunningState, FolderExecutionMode (modify)
  JobRunResult.cs             # Lightweight execution result for Phase 4 handoff (new)
  RunningJobState.cs          # Start time + CTS reference for tracking (new)
  QueuedJob.cs                # Job ID + queue time for FIFO ordering (new)
```

### Pattern 1: Timer with Reentrancy Guard
**What:** System.Threading.Timer fires every 30 seconds. The callback must not overlap with itself (if evaluation takes > 30s due to many jobs). Use an `int` flag with `Interlocked.CompareExchange` to ensure only one evaluation runs at a time.
**When to use:** Every timer tick.
**Example:**
```csharp
// Source: .NET BCL best practice for System.Threading.Timer reentrancy
private int _evaluating;
private System.Threading.Timer? _timer;

private void TimerCallback(object? state)
{
    // Reentrancy guard: skip if previous evaluation is still running
    if (Interlocked.CompareExchange(ref _evaluating, 1, 0) != 0)
        return;

    try
    {
        // Fire-and-forget the async evaluation, but catch all exceptions
        _ = EvaluateAndExecuteDueJobsAsync();
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Timer evaluation error: {ex.Message}");
    }
    finally
    {
        Interlocked.Exchange(ref _evaluating, 0);
    }
}
```

**CRITICAL NOTE:** The `async void` pitfall. The timer callback is `void`. Calling an async method from it requires careful exception handling. The pattern above uses fire-and-forget with the async method wrapping its entire body in try/catch. The `finally` block in the callback releases the reentrancy guard after the fire-and-forget is *initiated*, not *completed*. For true non-overlap, move the `Interlocked.Exchange` release inside the async method's own finally:

```csharp
private void TimerCallback(object? state)
{
    if (Interlocked.CompareExchange(ref _evaluating, 1, 0) != 0)
        return;

    // The async method itself handles releasing the guard
    _ = EvaluateAndExecuteDueJobsAsync();
}

private async Task EvaluateAndExecuteDueJobsAsync()
{
    try
    {
        // ... evaluate due jobs, queue or execute ...
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Scheduler evaluation error: {ex.Message}");
    }
    finally
    {
        Interlocked.Exchange(ref _evaluating, 0);
    }
}
```

### Pattern 2: SemaphoreSlim Concurrency Gate
**What:** A SemaphoreSlim initialized to MaxConcurrentJobs (default 3) controls how many jobs execute simultaneously. Each scheduled job awaits `WaitAsync()` before executing. Run-now bypasses this gate entirely.
**When to use:** All scheduled job executions. Run-now skips it.
**Example:**
```csharp
private SemaphoreSlim _concurrencyGate = new(3); // configurable

// Scheduled execution
private async Task ExecuteScheduledJobAsync(JobDefinition job)
{
    await _concurrencyGate.WaitAsync(_disposalCts.Token);
    try
    {
        await ExecuteJobCoreAsync(job);
    }
    finally
    {
        _concurrencyGate.Release();
        DrainQueue(); // Try to dequeue and execute waiting jobs
    }
}

// Run-now bypasses the gate
public async Task RunNowAsync(string jobId)
{
    var job = _jobStorage.Get(jobId);
    // Validation: not already running, no drift warning, is enabled
    await ExecuteJobCoreAsync(job);
}
```

### Pattern 3: Running Job State Tracking
**What:** A `ConcurrentDictionary<string, RunningJobState>` maps job IDs to their active execution state (CancellationTokenSource, start time, partial results). This enables duplicate-run detection, cancellation, and crash recovery.
**When to use:** Every job start/stop/cancel.
**Example:**
```csharp
private readonly ConcurrentDictionary<string, RunningJobState> _runningJobs = new();

private bool TryStartJob(string jobId, out CancellationTokenSource cts)
{
    cts = new CancellationTokenSource();
    var state = new RunningJobState
    {
        JobId = jobId,
        StartedUtc = DateTime.UtcNow,
        Cts = cts
    };

    if (!_runningJobs.TryAdd(jobId, state))
    {
        cts.Dispose();
        cts = null!;
        return false; // Already running -- duplicate blocked
    }

    // Persist RunningState to disk for crash recovery
    var job = _jobStorage.Get(jobId);
    job.RunningState = new JobRunningState { StartedUtc = state.StartedUtc };
    _jobStorage.Save(job);
    return true;
}

private void CompleteJob(string jobId)
{
    if (_runningJobs.TryRemove(jobId, out var state))
    {
        state.Cts.Dispose();
        var job = _jobStorage.Get(jobId);
        job.RunningState = null; // Clear crash recovery marker
        _jobStorage.Save(job);
    }
}
```

### Pattern 4: Crash Recovery on Startup
**What:** During initialization, scan all jobs for non-null `RunningState`. These were running when the app crashed. Mark them failed, log the orphaned run, clear RunningState.
**When to use:** Once, at application startup, before the timer starts.
**Example:**
```csharp
public void Initialize()
{
    _jobStorage.Load();

    foreach (var job in _jobStorage.Jobs.Values.ToList())
    {
        if (job.RunningState != null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Orphaned job detected: '{job.Name}' (started {job.RunningState.StartedUtc:u})");

            // Create a failed run record (Phase 4 will store full history)
            OnJobFailed(job.Id, "Application crashed during execution");

            job.RunningState = null;
            _jobStorage.Save(job);
        }
    }
}
```

### Pattern 5: Folder Job Execution
**What:** Resolve folder contents via `PresetManager.GetPresetsInFolder()`, then execute using the existing `SshExecutionService.ExecuteFolderAsync()` which already supports sequential/parallel modes, stop-on-error, and progress reporting.
**When to use:** When `job.TargetType == JobTargetType.Folder`.
**Example:**
```csharp
private async Task<List<ExecutionResult>> ExecuteFolderJobAsync(
    JobDefinition job,
    SshExecutionService sshService,
    CancellationToken ct)
{
    // Get direct children only (no recursive)
    var presetNames = _presetManager.GetPresetsInFolder(job.TargetName).ToList();

    // Build preset dictionary
    var presets = new Dictionary<string, PresetInfo>();
    foreach (var name in presetNames)
    {
        var preset = _presetManager.Get(name);
        if (preset != null) presets[name] = preset;
    }

    var options = new FolderExecutionOptions
    {
        SelectedPresets = presets.Keys.ToList(),
        RunPresetsInParallel = job.FolderExecutionMode == FolderExecutionMode.Parallel,
        StopOnFirstError = job.StopOnError,
        ParallelHostCount = 1 // Jobs process hosts sequentially
    };

    var hosts = BuildHostConnections(job);
    var (username, password) = ResolveCredentials(job);
    var timeouts = BuildTimeouts(job);

    return await sshService.ExecuteFolderAsync(
        hosts, presets, username, password, timeouts, options);
}
```

### Pattern 6: Event-Driven State Changes (Service-to-UI)
**What:** The JobExecutionService raises events for state transitions. Form1 subscribes and marshals to UI thread via `BeginInvoke`. This matches the existing `SshExecutionService.ProgressChanged` / `OutputReceived` pattern.
**When to use:** All job state transitions.
**Example:**
```csharp
// In JobExecutionService
public event EventHandler<JobStateChangedEventArgs>? JobStateChanged;

public class JobStateChangedEventArgs : EventArgs
{
    public string JobId { get; init; } = string.Empty;
    public string JobName { get; init; } = string.Empty;
    public JobExecutionState State { get; init; }
    public string? Message { get; init; }
}

public enum JobExecutionState
{
    Queued,
    Started,
    Completed,
    Failed,
    Cancelled,
    Skipped  // duplicate trigger or drift blocked
}
```

### Anti-Patterns to Avoid
- **Direct UI access from timer callback:** The timer fires on a ThreadPool thread. Never touch WinForms controls directly. Use events + `BeginInvoke` in the subscriber.
- **Sharing SshExecutionService instance with UI:** The existing `_sshService` in Form1 manages manual execution with its own `_isRunning` / `_cts` state. Job execution must use separate SshExecutionService instances per job to avoid state conflicts.
- **Blocking the timer callback:** Never `await` inside the timer callback method itself (it has a `void` signature). Use fire-and-forget with proper exception handling and reentrancy guard.
- **Saving RunningState on every host completion:** This would hammer the disk. Save RunningState only at job start (set) and job completion (clear). Partial results are best-effort from memory if crash occurs.
- **Ignoring CancellationToken in queue drain:** When the app is shutting down, queued jobs should not start. Pass `_disposalCts.Token` through all queue operations.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Concurrency limiting | Custom lock-based slot counter | SemaphoreSlim | Battle-tested, supports async WaitAsync, handles edge cases (release on different thread) |
| Thread-safe job tracking | Dictionary + manual locking | ConcurrentDictionary<string, RunningJobState> | Lock-free reads, atomic TryAdd/TryRemove, designed for this exact scenario |
| FIFO job queue | List + lock + manual ordering | ConcurrentQueue<QueuedJob> | Lock-free FIFO, thread-safe enqueue/dequeue, no ordering bugs |
| Reentrancy guard | bool flag + lock | Interlocked.CompareExchange on int | Atomic, no lock contention, proven pattern for timer reentrancy |
| SSH execution against hosts | Custom connection management | SshExecutionService.ExecutePresetAsync / ExecuteFolderAsync | Already handles connection pooling, prompt detection, ANSI processing, cancellation |
| Credential resolution | Manual credential lookup | ICredentialProvider + CredentialTargets.JobPasswordTarget | Already handles all three credential modes, Windows Credential Manager integration |
| Cron next-run calculation | Custom cron parser | SchedulingService.GetNextRunUtc() | Already wraps Cronos, handles 5-field validation, tested |

**Key insight:** Almost all the hard SSH execution logic already exists. Phase 3's job is orchestration, not execution. The new code wraps existing services with timer-driven scheduling, concurrency control, and state management.

## Common Pitfalls

### Pitfall 1: Timer Callback Reentrancy
**What goes wrong:** If evaluation takes > 30 seconds (many jobs, slow disk I/O for state persistence), the timer fires again while the previous evaluation is still running. Two evaluations could try to start the same job simultaneously.
**Why it happens:** `System.Threading.Timer` callbacks are dispatched to ThreadPool threads independently. There is no built-in reentrancy prevention.
**How to avoid:** Use `Interlocked.CompareExchange` as a reentrancy guard at the start of the callback. Release it inside the async method's finally block, not in the sync callback.
**Warning signs:** Duplicate job executions, "already running" logs appearing during normal scheduled runs.

### Pitfall 2: SshExecutionService Instance Sharing
**What goes wrong:** If the job executor shares the same `SshExecutionService` instance used by Form1 for manual execution, the `_isRunning` flag and `_cts` field conflict. Starting a manual execution while a job is running (or vice versa) would cancel the other.
**Why it happens:** `SshExecutionService` uses a single `_cts` field and `_isRunning` volatile bool. It was designed for one execution at a time from the UI.
**How to avoid:** Create a new `SshExecutionService` instance per job execution (or per JobExecutionService). They are lightweight -- the expensive resource (connection pool) can be shared or omitted for job execution.
**Warning signs:** Manual "Execute" button randomly cancels a scheduled job, or a scheduled job prevents manual execution.

### Pitfall 3: Race Between RunningState Persistence and Crash
**What goes wrong:** If the app crashes between the in-memory state update and the disk write, RunningState won't be persisted. On restart, the orphaned job won't be detected.
**Why it happens:** `JobStorageService.Save()` writes to disk, but the crash could occur before `Save()` completes.
**How to avoid:** Write RunningState to disk immediately when a job starts (before any SSH execution begins). This is a "write-ahead" pattern. The window between `TryAdd` and `Save` is small but exists.
**Warning signs:** Rare edge case; not practically testable but worth documenting.

### Pitfall 4: Async Void Exception Swallowing
**What goes wrong:** If the timer callback uses `async void` and an exception escapes the try/catch, the exception is thrown on the ThreadPool's SynchronizationContext and crashes the entire application.
**Why it happens:** `System.Threading.Timer` requires a `void` callback delegate. Any async work requires async void, which has different exception semantics than async Task.
**How to avoid:** Wrap the entire async method body in try/catch. Never let exceptions escape. Log and continue.
**Warning signs:** Application crashes with no visible error. Debug output shows unhandled exception on ThreadPool thread.

### Pitfall 5: SemaphoreSlim Leak on Exception
**What goes wrong:** If `ExecuteJobCoreAsync` throws an exception after acquiring the semaphore but before the `finally` block runs (unlikely but possible), the semaphore count is permanently decremented. After enough failures, no jobs can execute.
**Why it happens:** Improper try/finally structure around `WaitAsync` / `Release`.
**How to avoid:** Always use `try { ... } finally { semaphore.Release(); }` immediately after `WaitAsync` returns. Never put logic between `WaitAsync` and `try`.
**Warning signs:** Jobs stop executing after several failures. `_concurrencyGate.CurrentCount` shows 0 when no jobs are running.

### Pitfall 6: Disposing SemaphoreSlim While Jobs Are Waiting
**What goes wrong:** On application shutdown, if the timer is stopped but queued jobs are still waiting on `WaitAsync`, disposing the SemaphoreSlim throws `ObjectDisposedException`.
**Why it happens:** Shutdown sequence doesn't cancel waiting operations before disposal.
**How to avoid:** Use a `CancellationTokenSource` for disposal. Cancel it before disposing the SemaphoreSlim. Pass `_disposalCts.Token` to all `WaitAsync` calls. Handle `OperationCanceledException` gracefully.
**Warning signs:** `ObjectDisposedException` during application shutdown.

### Pitfall 7: Credential Resolution for Unattended Execution
**What goes wrong:** Jobs with `CredentialMode.InheritFromApp` need the app-level username/password. If the user hasn't entered credentials yet (or they're stored in Credential Manager but the provider isn't available), the job fails silently.
**Why it happens:** Manual execution always has the user present to enter credentials. Scheduled execution is unattended.
**How to avoid:** Validate credential availability at job evaluation time. If credentials can't be resolved, skip the job and log a warning rather than attempting execution with empty credentials.
**Warning signs:** Jobs fail with SSH authentication errors despite working fine when executed manually.

## Code Examples

### Building HostConnection List from JobDefinition
```csharp
// Source: Existing patterns in Form1.cs and JobStorageService
private static List<HostConnection> BuildHostConnections(JobDefinition job)
{
    var hosts = new List<HostConnection>();
    foreach (var row in job.Hosts)
    {
        if (!row.TryGetValue("Host_IP", out var hostIp) || string.IsNullOrWhiteSpace(hostIp))
            continue;

        var host = HostConnection.Parse(hostIp);

        // Apply per-row overrides
        if (row.TryGetValue("port", out var portStr) && int.TryParse(portStr, out var port))
            host.Port = port;
        if (row.TryGetValue("username", out var username) && !string.IsNullOrEmpty(username))
            host.Username = username;
        if (row.TryGetValue("password", out var password) && !string.IsNullOrEmpty(password))
            host.Password = password;

        // Copy all columns as variables for {{variable}} substitution
        foreach (var kvp in row)
            host.Variables[kvp.Key] = kvp.Value;

        hosts.Add(host);
    }
    return hosts;
}
```

### Resolving Credentials per CredentialMode
```csharp
// Source: Existing ICredentialProvider + CredentialTargets patterns
private (string username, string password) ResolveCredentials(JobDefinition job)
{
    switch (job.CredentialMode)
    {
        case CredentialMode.Stored:
            var target = CredentialTargets.JobPasswordTarget(job.Id);
            if (_credentialProvider.TryGetPassword(target, out var storedUser, out var storedPass))
                return (storedUser, storedPass);
            return (string.Empty, string.Empty); // Will fail at SSH level

        case CredentialMode.InheritFromApp:
            var config = _configService.GetCurrent();
            var appUser = config.Username;
            // App password from Credential Manager
            _credentialProvider.TryGetPassword(
                CredentialTargets.DefaultPasswordTarget, out _, out var appPass);
            return (appUser, appPass ?? string.Empty);

        case CredentialMode.PerHostColumn:
            // Per-host credentials are already in host.Username/Password from BuildHostConnections
            return (string.Empty, string.Empty);

        default:
            return (string.Empty, string.Empty);
    }
}
```

### Timer Lifecycle Management
```csharp
// Source: .NET BCL System.Threading.Timer API
public sealed class JobExecutionService : IDisposable
{
    private System.Threading.Timer? _timer;
    private readonly CancellationTokenSource _disposalCts = new();
    private bool _disposed;

    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JobExecutionService));

        // dueTime: 0 = fire immediately for first evaluation
        // period: 30_000 ms = 30 second interval
        _timer = new System.Threading.Timer(
            TimerCallback, null,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromSeconds(30));
    }

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _disposalCts.Cancel();
        _timer?.Dispose();

        // Wait briefly for running jobs to acknowledge cancellation
        // Do NOT block indefinitely -- app is closing
        foreach (var state in _runningJobs.Values)
        {
            state.Cts.Cancel();
        }

        _concurrencyGate.Dispose();
        _disposalCts.Dispose();
    }
}
```

### Due Job Evaluation Logic
```csharp
// Source: SchedulingService.GetNextRunUtc() + Cronos cron evaluation
private async Task EvaluateAndExecuteDueJobsAsync()
{
    var now = DateTime.UtcNow;

    foreach (var job in _jobStorage.Jobs.Values.ToList())
    {
        if (_disposalCts.IsCancellationRequested) break;

        // Skip disabled, drift-blocked, or already-running jobs
        if (!job.IsEnabled) continue;
        if (job.HasDriftWarning) continue;
        if (_runningJobs.ContainsKey(job.Id)) continue;

        DateTime? nextRun = null;

        if (job.ScheduleType == ScheduleType.Recurring)
        {
            nextRun = _schedulingService.GetNextRunUtc(job.CronExpression);
            // Check if we're past the due time (within the 30s window)
            // GetNextRunUtc returns the NEXT occurrence from now, so we need
            // to check if any occurrence fell within the last evaluation window
            var missed = _schedulingService.GetMissedOccurrences(
                job.CronExpression, _lastEvaluationUtc);
            if (missed.Count == 0) continue;
        }
        else if (job.ScheduleType == ScheduleType.OneTime)
        {
            if (!job.OneTimeScheduleUtc.HasValue) continue;
            if (job.OneTimeScheduleUtc.Value > now) continue;
            // Due now
        }
        else continue; // ScheduleType.None -- manual only

        // Try to execute or queue
        if (_concurrencyGate.CurrentCount > 0)
        {
            _ = ExecuteScheduledJobAsync(job);
        }
        else
        {
            _jobQueue.Enqueue(new QueuedJob(job.Id, now));
            OnJobStateChanged(job.Id, job.Name, JobExecutionState.Queued);
        }
    }

    _lastEvaluationUtc = now;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| System.Timers.Timer | System.Threading.Timer or PeriodicTimer | .NET 6+ | PeriodicTimer is modern async-native; System.Threading.Timer is still appropriate for void callbacks |
| Manual thread management | Task + async/await + SemaphoreSlim | .NET 4.5+ | No need for manual Thread objects or ManualResetEvents |
| lock + bool flags | Interlocked + ConcurrentDictionary | .NET 4+ | Better performance, no deadlock risk |

**Deprecated/outdated:**
- `System.Timers.Timer`: Still works but `System.Threading.Timer` is preferred for ThreadPool callbacks in services. The existing codebase uses neither yet, and the locked decision is `System.Threading.Timer`.
- `BackgroundService` / `IHostedService`: These are ASP.NET Core patterns, not applicable to WinForms desktop apps.

## Open Questions

1. **SshExecutionService instance per job vs shared pool**
   - What we know: Each SshExecutionService has its own `_isRunning` / `_cts` state. Sharing one with the UI would conflict.
   - What's unclear: Whether creating one per job run (and disposing after) is wasteful if connection pooling is involved.
   - Recommendation: Create one SshExecutionService per active job (reuse across runs of the same job). Pass `enablePooling: false` to avoid pool conflicts with the UI instance, or share a single SshConnectionPool. The SshExecutionService constructor shows it accepts an optional pool; creating without pooling is fine for scheduled execution where connections are short-lived.

2. **MaxConcurrentJobs configuration change while jobs are running**
   - What we know: User can change max concurrent jobs in settings. SemaphoreSlim cannot be resized after creation.
   - What's unclear: Whether to recreate the semaphore (requires draining running jobs) or defer the change until next app restart.
   - Recommendation: Apply changes on next timer restart (e.g., after settings dialog closes). Stop timer, dispose old semaphore (after all running jobs complete), create new one, restart timer. Simpler: just require app restart for this setting change.

3. **Phase 4 handoff: What execution result data to capture now**
   - What we know: Phase 4 adds full history (HIST-01 through HIST-04). Phase 3 needs to produce some result even without Phase 4.
   - What's unclear: Exactly what structure Phase 4 expects.
   - Recommendation: Capture a minimal `JobRunResult` (job ID, start/end UTC, success/failure, per-host success count, error message) and raise it as an event. Phase 4 can subscribe to the event and persist detailed history. For now, log the result to Debug output.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.7.0 + FluentAssertions 6.12.0 + Moq 4.20.70 |
| Config file | `SSH_Helper.Tests/SSH_Helper.Tests.csproj` |
| Quick run command | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecution" --no-build -q` |
| Full suite command | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EXEC-01 | Timer evaluates due jobs and starts execution | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~JobExecutionServiceTests" --no-build -q` | Wave 0 |
| EXEC-02 | RunNow starts immediately, bypasses queue | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~RunNow" --no-build -q` | Wave 0 |
| EXEC-03 | Cancel stops running job via CancellationToken | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~Cancel" --no-build -q` | Wave 0 |
| EXEC-04 | MaxConcurrentJobs configurable | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~Concurrency" --no-build -q` | Wave 0 |
| EXEC-05 | Excess jobs queue in FIFO order | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~Queue" --no-build -q` | Wave 0 |
| EXEC-06 | Folder jobs execute all presets in folder | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~FolderJob" --no-build -q` | Wave 0 |
| EXEC-07 | Folder execution mode (sequential/parallel) | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~FolderExecution" --no-build -q` | Wave 0 |
| RELY-02 | Orphaned jobs detected on startup | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~CrashRecovery" --no-build -q` | Wave 0 |
| RELY-03 | Timer runs independently of UI thread | unit/integration | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~TimerIndependent" --no-build -q` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~JobExecution" --no-build -q`
- **Per wave merge:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `SSH_Helper.Tests/Services/JobExecutionServiceTests.cs` -- core execution service tests (EXEC-01 through EXEC-07, RELY-02, RELY-03)
- [ ] Test infrastructure: Mock/stub for SshExecutionService (avoid real SSH connections in unit tests) -- Moq already available
- [ ] Test infrastructure: Mock for ICredentialProvider (already used in JobStorageServiceTests)

*(Testing strategy: Unit-test the orchestration logic -- timer evaluation, concurrency gating, queue FIFO, duplicate detection, crash recovery, credential resolution -- using mocked SshExecutionService and JobStorageService. No real SSH connections needed.)*

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection: SshExecutionService.cs, JobStorageService.cs, SchedulingService.cs, JobDefinition.cs, ExecutionCoordinator.cs, PresetManager.cs, AppConfiguration.cs
- [Microsoft .NET Timers documentation](https://learn.microsoft.com/en-us/dotnet/standard/threading/timers) -- Timer patterns, PeriodicTimer comparison
- [Microsoft SemaphoreSlim documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim?view=net-10.0) -- Async concurrency limiting

### Secondary (MEDIUM confidence)
- [The Dangers of Async Void](https://sergeyteplyakov.github.io/Blog/csharp/2025/01/28/The_Dangers_Of_Async_Void.html) -- Exception handling in timer callbacks
- [DEV.to: SemaphoreSlim Practical Guide](https://dev.to/stevsharp/semaphoreslim-in-net-a-practical-guide-with-the-rest-of-the-toolbox-1mh7) -- Concurrency patterns
- [ConcurrentQueue vs Channels in .NET 2025](https://medium.com/@mahmednisar/concurrentqueue-vs-channels-in-net-2025-the-performance-battle-you-need-to-see-e9949ec106e2) -- Queue choice rationale

### Tertiary (LOW confidence)
- None -- all findings verified against official docs or codebase inspection

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all components are .NET BCL primitives, no external dependencies needed
- Architecture: HIGH -- patterns derived from direct codebase inspection of existing services
- Pitfalls: HIGH -- timer reentrancy, async void, SemaphoreSlim lifecycle are well-documented .NET concerns
- Code examples: HIGH -- built from actual existing method signatures in the codebase

**Research date:** 2026-03-07
**Valid until:** 2026-04-07 (stable .NET 8 BCL, no moving targets)
