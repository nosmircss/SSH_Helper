# M4: Job Scheduler

**Status**: NOT STARTED

**Why**: Transforms the app from "run when I click" to "run on schedule." Health checks, config backups, compliance audits — all automated while the app is open.

---

## Progress Checklist

- [ ] Add `Cronos` NuGet package to `SSH_Helper.csproj`
- [ ] Create `Models/ScheduledJob.cs` (job definition + `ScheduledJobHost`)
- [ ] Create `Models/JobHistoryEntry.cs` (per-run tracking)
- [ ] Add `ScheduledJobs`, `JobHistory`, `MaxJobHistoryEntries` to `AppConfiguration.cs`
- [ ] Create `Services/JobSchedulerService.cs` (timer, cron evaluation, execution, concurrency)
- [ ] Create `Services/JobNotificationService.cs` (desktop notifications)
- [ ] Create `JobEditorDialog.cs` (add/edit job with cron preview)
- [ ] Create `JobSchedulerDialog.cs` (job list + history view)
- [ ] Add "Job Scheduler..." menu item to Form1
- [ ] Wire scheduler service in Form1 (instantiate, events, status bar)
- [ ] Handle missed jobs on app startup (log as skipped)
- [ ] Implement job output storage in separate files
- [ ] Write tests for `JobSchedulerService` and cron evaluation
- [ ] Manual smoke test: create cron job, verify execution, test notifications

---

## Data Models

### `Models/ScheduledJob.cs`

```csharp
public enum JobScheduleType { Cron, OneTime }
public enum JobStatus { Enabled, Disabled, Running }

public class ScheduledJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public string? FolderName { get; set; }              // Execute all presets in folder
    public string? EnvironmentName { get; set; }         // Target environment (M1 integration)
    public List<ScheduledJobHost> Hosts { get; set; } = new();
    public string Username { get; set; } = string.Empty;
    public bool UseStoredCredentials { get; set; } = true;
    public JobScheduleType ScheduleType { get; set; } = JobScheduleType.Cron;
    public string? CronExpression { get; set; }          // "0 */6 * * *"
    public DateTime? OneTimeUtc { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Enabled;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunUtc { get; set; }
    [JsonIgnore] public DateTime? NextRunUtc { get; set; } // Computed at runtime
    public int? TimeoutSeconds { get; set; }
    public int? ConnectionTimeoutSeconds { get; set; }
    public bool NotifyOnCompletion { get; set; } = true;
    public bool NotifyOnFailureOnly { get; set; } = false;
}

public class ScheduledJobHost
{
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
}
```

### `Models/JobHistoryEntry.cs`

```csharp
public class JobHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string JobId { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public DateTime StartedUtc { get; set; }
    public DateTime CompletedUtc { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public int HostCount { get; set; }
    public int SuccessfulHosts { get; set; }
    public int FailedHosts { get; set; }
    public string? ErrorSummary { get; set; }
    public string OutputSummary { get; set; } = string.Empty; // First N chars
}
```

---

## NuGet Package

```xml
<PackageReference Include="Cronos" Version="0.8.4" />
```

`Cronos` by HangfireIO — zero-dependency, supports standard 5-field cron expressions, handles DST transitions.

---

## JobSchedulerService

### `Services/JobSchedulerService.cs`

```csharp
public class JobSchedulerService : IDisposable
{
    // Events
    public event EventHandler<JobStartedEventArgs>? JobStarted;
    public event EventHandler<JobCompletedEventArgs>? JobCompleted;
    public event EventHandler<JobProgressEventArgs>? JobProgress;
    public event EventHandler? ScheduleChanged;

    // Constructor
    public JobSchedulerService(
        ConfigurationService configService,
        PresetManager presetManager,
        SshConnectionPool connectionPool,
        int maxConcurrentJobs = 2);

    // Lifecycle
    public void Start();   // Start timer, called from Form1 constructor
    public void Stop();    // Stop timer, cancel all running jobs

    // CRUD
    public ScheduledJob AddJob(ScheduledJob job);
    public void UpdateJob(ScheduledJob job);
    public void RemoveJob(string jobId);
    public void SetJobEnabled(string jobId, bool enabled);
    public IReadOnlyList<ScheduledJob> GetJobs();

    // Execution
    public Task RunJobNowAsync(string jobId);  // Manual trigger
    public void CancelJob(string jobId);
    public bool IsJobRunning(string jobId);

    // History
    public IReadOnlyList<JobHistoryEntry> GetHistory(string? jobId = null);
    public void ClearHistory(string? jobId = null);
}
```

### Timer Design

- `System.Threading.Timer` fires every **30 seconds**
- On each tick:
  1. Iterate all enabled jobs
  2. Compute `NextRunUtc` via `Cronos.CronExpression.Parse(cron).GetNextOccurrence(lastRunOrNow, TimeZoneInfo.Local)`
  3. If `NextRunUtc <= DateTime.UtcNow` → job is due, enqueue for execution
  4. For one-time jobs: if `OneTimeUtc <= UtcNow` and never run → execute, then auto-disable
- `SemaphoreSlim(maxConcurrentJobs)` gates concurrency (default 2)

### Job Execution Flow

1. Set `job.Status = Running`, persist
2. Create dedicated `SshExecutionService` instance (shares connection pool)
3. Resolve preset via `PresetManager`
4. Build `HostConnection` list from `job.Hosts`
5. Resolve credentials via `ICredentialProvider` (Credential Manager)
6. Execute via `sshService.ExecutePresetAsync()`
7. Capture output via `OutputReceived` event
8. On completion: create `JobHistoryEntry`, save full output to file, fire `JobCompleted`
9. Reset `job.Status = Enabled`, update `LastRunUtc`

### Edge Cases

- **App startup**: Scan for overdue jobs, log as skipped (NOT auto-executed per user decision)
- **Concurrent jobs**: Semaphore prevents overload
- **Long-running jobs**: Per-job `CancellationTokenSource` with 2-hour overall timeout
- **Manual execution**: "Run Now" button bypasses schedule
- **Isolation**: Scheduler uses own `SshExecutionService` instances — independent from Form1 manual execution
- **Thread safety**: Timer callbacks on thread pool. All UI updates via `SynchronizationContext` or `Control.Invoke`
- **Config contention**: Use `ConfigurationService.Update()` pattern (atomic load-modify-save)

---

## Notification Service

### `Services/JobNotificationService.cs`

Uses WinForms `NotifyIcon` with `BalloonTipText` (works on Windows 10/11).

```csharp
public class JobNotificationService : IDisposable
{
    public void ShowJobCompleted(string jobName, bool success, int hostCount, int failedHosts, TimeSpan duration);
    public void ShowJobFailed(string jobName, string errorSummary);
    public void ShowMissedJobs(int count);
}
```

- `BalloonTipIcon.Info` for success, `BalloonTipIcon.Error` for failure
- Clicking the balloon opens Job History in `JobSchedulerDialog`

---

## Job Management UI

### `JobSchedulerDialog.cs` (Modeless Dialog)

Follows `SettingsDialog` pattern. Opened from File menu.

```
+--------------------------------------------------+
| Job Scheduler                               [X]  |
+--------------------------------------------------+
| [+ Add Job]  [Edit]  [Delete]  [Run Now]         |
|                                                   |
| +-----------------------------------------------+|
| | Name      | Preset    | Schedule  | Next Run  ||
| | Backup    | backup-fw | 0 2 * * * | 02:00 AM  ||
| | Health    | health-chk| */30 * * *| 12:30 PM  ||
| | Audit     | sec-audit | 0 8 * * 1 | Mon 8AM   ||
| +-----------------------------------------------+|
|                                                   |
| [Job History]                                     |
| +-----------------------------------------------+|
| | Time       | Job     | Result  | Duration     ||
| | 2026-02-07 | Backup  | Success | 2m 34s       ||
| | 2026-02-06 | Health  | Failed  | 0m 12s       ||
| +-----------------------------------------------+|
+--------------------------------------------------+
```

### `JobEditorDialog.cs` (Modal Sub-dialog)

```
+----------------------------------------------+
| Add/Edit Scheduled Job                  [X]  |
+----------------------------------------------+
| Name:        [___________________________]   |
| Preset:      [dropdown_________________ v]   |
|                                              |
| Schedule:    (*) Cron  ( ) One-time          |
|   Cron expr: [0 2 * * *____________]         |
|   Preview:   "Every day at 2:00 AM"          |
|   Next 5:    Feb 8 02:00, Feb 9 02:00, ...   |
|  -or-                                        |
|   Date/Time: [2026-02-08] [14:00]            |
|                                              |
| Environment: [dropdown_________________ v]   |
| Hosts:       [v] Use current grid hosts      |
|              [ ] Custom host list             |
|                                              |
| Username:    [___________________________]   |
| Credentials: [v] Use stored credentials      |
|                                              |
| Notifications:                               |
|   [v] Notify on completion                   |
|   [ ] Only on failure                        |
|                                              |
| [Save]                          [Cancel]     |
+----------------------------------------------+
```

Cron preview: calls `Cronos.CronExpression.GetNextOccurrence()` 5 times in a loop.

---

## Status Bar Integration

Add to Form1's existing `StatusStrip`:
- `statusSchedulerLabel` → "Scheduler: 3 jobs | Next: Backup at 02:00"
- When a job runs: "Job: Backup running (2/5 hosts)..."
- When scheduler is off (no enabled jobs): "Scheduler: Off"

---

## Output Storage

- **Summary**: `JobHistoryEntry` stored in `AppConfiguration.JobHistory` (capped at `MaxJobHistoryEntries`, default 100)
- **Full output**: Stored as separate JSON files in `%LocalAppData%\SSH_Helper\job_history\{entry_id}.json`
- Cleanup: Old files pruned when history entries are removed

---

## Key Files

| File | Action |
|------|--------|
| `Models/ScheduledJob.cs` | CREATE |
| `Models/JobHistoryEntry.cs` | CREATE |
| `Services/JobSchedulerService.cs` | CREATE |
| `Services/JobNotificationService.cs` | CREATE |
| `JobSchedulerDialog.cs` | CREATE |
| `JobEditorDialog.cs` | CREATE |
| `Models/AppConfiguration.cs` | MODIFY — add scheduler properties |
| `SSH_Helper.csproj` | MODIFY — add Cronos NuGet |
| `Form1.cs` | MODIFY — instantiate service, menu, events, status bar |
| `Form1.Designer.cs` | MODIFY — add menu item |
