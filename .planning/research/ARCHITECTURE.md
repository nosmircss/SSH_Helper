# Architecture Patterns

**Domain:** In-app job scheduler for SSH_Helper WinForms application
**Researched:** 2026-03-07

## Recommended Architecture

The job scheduler integrates as a new service sub-system alongside existing services, following the established pattern: services own business logic, Form1 handles UI, events bridge the gap. The scheduler introduces one genuinely new architectural element -- a long-running background timer loop -- but everything else maps cleanly onto existing patterns.

### High-Level Component Map

```
Form1 (UI Layer)
  |
  |-- subscribes to events from:
  |     SchedulerService (tick, run-start, run-complete, run-failed, state-change)
  |
  |-- opens dialogs:
  |     JobEditorDialog (create/edit job definitions)
  |     JobManagerDialog (list jobs, view run history, run-now, enable/disable)
  |
  |-- status bar integration:
        Shows scheduler state (running/paused/idle), next-run countdown

Services Layer (new)
  |
  SchedulerService          -- Timer loop, job evaluation, concurrency gating
  |   |
  |   +-- JobDefinitionStore   -- CRUD for job definitions, persistence
  |   +-- CronEvaluator        -- Cron expression parsing, next-run calculation
  |   +-- JobRunner             -- Executes a single job (builds hosts, calls SshExecutionService)
  |   +-- JobHistoryStore       -- Per-job run history persistence with pruning
  |
  (reuses existing)
  |   +-- SshExecutionService   -- Actual SSH execution (no changes needed)
  |   +-- SshConnectionPool     -- Connection reuse (no changes needed)
  |   +-- PresetManager         -- Preset/folder resolution (read-only access)
  |   +-- ConfigurationService  -- Global settings access
  |   +-- ICredentialProvider   -- Credential retrieval for job-configured creds

Models Layer (new)
  |
  +-- JobDefinition        -- What to run, when, against which hosts, with what creds
  +-- JobSchedule          -- Cron expression + one-time flag + enabled state
  +-- JobHostList          -- Dedicated host list per job (self-contained)
  +-- JobCredentialConfig  -- Credential source selection per job
  +-- JobRunRecord         -- Single run outcome (start, end, duration, success, host counts)
  +-- SchedulerState       -- Enum: Running, Paused, Stopped
```

### Component Boundaries

| Component | Responsibility | Communicates With | Owns Data |
|-----------|---------------|-------------------|-----------|
| **SchedulerService** | Timer tick, job due evaluation, concurrency semaphore, start/stop lifecycle | JobDefinitionStore (reads), CronEvaluator (reads), JobRunner (delegates), Form1 (events) | Scheduler state (running/paused), active job tracking |
| **JobDefinitionStore** | CRUD for job definitions, persistence to JSON | ConfigurationService (storage path pattern) | `scheduler.jobs.json` |
| **CronEvaluator** | Parse cron expressions, calculate next-run times, validate cron strings | None (pure function) | None |
| **JobRunner** | Build HostConnection list from JobHostList, resolve preset/folder, delegate to SshExecutionService, capture results | SshExecutionService, PresetManager, ICredentialProvider, JobHistoryStore | None (stateless orchestrator) |
| **JobHistoryStore** | Per-job run history persistence, dual pruning (max entries + age) | File system | `scheduler/history/` directory |
| **JobEditorDialog** | Create/edit job definitions with cron preview and host management | JobDefinitionStore (save), CronEvaluator (preview), PresetManager (preset picker) | None |
| **JobManagerDialog** | List jobs, view status/next-run, run-now, enable/disable, view run history | SchedulerService (actions), JobDefinitionStore (reads), JobHistoryStore (reads) | None |
| **Form1 (scheduler integration)** | Status bar updates, menu entry for scheduler, event subscriptions | SchedulerService (events + commands) | None |

### Data Flow

**Scheduler Tick Loop (core loop):**

```
1. System.Threading.Timer fires every 30-60 seconds
2. SchedulerService.OnTick() runs on ThreadPool thread
3. For each enabled job in JobDefinitionStore:
   a. CronEvaluator.GetNextRun(job.Schedule, job.LastRunUtc) -> nextRun
   b. If nextRun <= DateTime.UtcNow AND concurrency semaphore available:
      - Acquire semaphore slot
      - Fire JobStarting event (UI updates status bar)
      - Launch JobRunner.ExecuteAsync(job) on background task
4. When JobRunner completes:
   a. Save JobRunRecord to JobHistoryStore
   b. Update job.LastRunUtc in JobDefinitionStore
   c. If one-time job: set job.Enabled = false
   d. Release semaphore slot
   e. Fire JobCompleted/JobFailed event (UI shows notification)
```

**Job Execution (single job run):**

```
1. JobRunner receives JobDefinition
2. Resolves target:
   - Single preset: PresetManager.GetPreset(job.PresetName)
   - Folder: PresetManager.GetPresetsInFolder(job.FolderName)
3. Builds HostConnection list from job.HostList (self-contained, not main grid)
4. Resolves credentials based on JobCredentialConfig:
   - Stored: ICredentialProvider.TryGetPassword(job.CredentialTarget)
   - Inherit: Use username/password from job definition
   - Per-host: Use host grid columns
5. For each preset (sequential or parallel per job config):
   a. Creates new SshExecutionService instance (or uses shared with isolation)
   b. Calls ExecutePresetAsync(hosts, preset, username, password, timeouts)
   c. Collects List<ExecutionResult>
6. Aggregates results into JobRunRecord
7. Returns to SchedulerService for history storage and event firing
```

**User Creates a Job:**

```
1. User opens JobManagerDialog from menu/toolbar
2. Clicks "New Job" -> JobEditorDialog opens
3. User configures:
   - Name, description
   - Target: picks preset or folder from tree (PresetManager provides data)
   - Schedule: enters cron expression, sees next 5 runs preview (CronEvaluator)
   - OR one-time: picks date/time
   - Hosts: imports from CSV, copies from main grid, or manual entry
   - Credentials: stored / inherit / per-host column
   - Folder mode: sequential or parallel (if folder target)
4. Saves -> JobDefinitionStore.Save(jobDefinition)
5. SchedulerService picks it up on next tick
```

**Run-Now (manual trigger):**

```
1. User selects job in JobManagerDialog, clicks "Run Now"
2. JobManagerDialog calls SchedulerService.RunNow(jobId)
3. SchedulerService bypasses schedule check, submits to JobRunner immediately
4. Same execution flow as scheduled run
5. Does NOT update LastRunUtc (so next scheduled run still fires on time)
```

## Patterns to Follow

### Pattern 1: Event-Driven UI Communication (existing pattern)
**What:** Services raise events, Form1 subscribes and marshals to UI thread.
**When:** All scheduler status updates, notifications, progress.
**Why:** This is the established pattern throughout SSH_Helper. Breaking it creates inconsistency.
**Example:**
```csharp
// In SchedulerService
public event EventHandler<JobStartedEventArgs>? JobStarted;
public event EventHandler<JobCompletedEventArgs>? JobCompleted;
public event EventHandler<JobFailedEventArgs>? JobFailed;
public event EventHandler<SchedulerStateChangedEventArgs>? StateChanged;

// In Form1
_schedulerService.JobCompleted += (s, e) =>
    BeginInvoke(() => ShowJobNotification(e));
_schedulerService.StateChanged += (s, e) =>
    BeginInvoke(() => UpdateSchedulerStatusBar(e));
```

### Pattern 2: Separate Persistence File (follows HistoryStorageService)
**What:** Scheduler data persisted to its own JSON file, not crammed into config.json.
**When:** Job definitions and run history.
**Why:** config.json is already large with compressed state. Scheduler data grows independently and should not bloat the main config. HistoryStorageService already established this precedent with `history.index.json` + per-run files.
**Example:**
```
%LocalAppData%\SSH_Helper\
  config.json                    (existing -- add only SchedulerSettings)
  history.index.json             (existing)
  history/                       (existing)
  scheduler.jobs.json            (NEW -- job definitions)
  scheduler/                     (NEW)
    history/                     (NEW -- per-job run history files)
      {jobId}.runs.json          (run records for each job)
```

### Pattern 3: Self-Contained Job Data
**What:** Each job carries its own host list, credentials config, and preset reference by name -- not by reference to mutable UI state.
**When:** Always. Jobs must survive grid changes, environment switches, preset renames.
**Why:** The main grid changes constantly (environment switches, CSV imports). If jobs reference "current grid rows," scheduled runs would break silently when the user switches context.
**Example:**
```csharp
public class JobDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? PresetName { get; set; }      // null if folder job
    public string? FolderName { get; set; }       // null if preset job
    public JobSchedule Schedule { get; set; }
    public JobHostList HostList { get; set; }     // self-contained
    public JobCredentialConfig Credentials { get; set; }
    public FolderExecutionMode FolderMode { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? LastRunUtc { get; set; }
    public int TimeoutSeconds { get; set; }
    public int ConnectionTimeoutSeconds { get; set; }
}

public class JobHostList
{
    public List<Dictionary<string, string>> Hosts { get; set; } = new();
    public List<string> Columns { get; set; } = new();
}
```

### Pattern 4: Concurrency via SemaphoreSlim
**What:** Bounded concurrent job execution using SemaphoreSlim.
**When:** SchedulerService tick evaluates multiple due jobs.
**Why:** Simple, well-understood, built into .NET. No need for external libraries. User configures max concurrent jobs (default: 1).
**Example:**
```csharp
private SemaphoreSlim _concurrencySemaphore;

public void SetConcurrencyLimit(int max)
{
    _concurrencySemaphore = new SemaphoreSlim(max, max);
}

private async Task TryRunJobAsync(JobDefinition job, CancellationToken ct)
{
    if (!await _concurrencySemaphore.WaitAsync(0, ct))
        return; // all slots full, try next tick
    try
    {
        await _jobRunner.ExecuteAsync(job, ct);
    }
    finally
    {
        _concurrencySemaphore.Release();
    }
}
```

### Pattern 5: Timer on ThreadPool, Marshal to UI
**What:** Use `System.Threading.Timer` for the scheduler tick, not `System.Windows.Forms.Timer`.
**When:** Scheduler tick evaluation.
**Why:** WinForms Timer runs on the UI thread -- a blocked UI (user dragging a dialog, modal open) would delay job evaluation. `System.Threading.Timer` fires on ThreadPool regardless of UI state. Events marshal results back to UI thread via `BeginInvoke`.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Sharing SshExecutionService State with Manual Execution
**What:** Reusing the same `SshExecutionService` instance that Form1 uses for manual execution.
**Why bad:** `SshExecutionService` has `IsRunning` state, progress events wired to the main output window. Scheduler jobs firing `ProgressChanged` would corrupt the main UI output during manual execution.
**Instead:** JobRunner creates dedicated `SshExecutionService` instances per job run, or uses a separate instance pool. Scheduler output goes to job history, not the main output window.

### Anti-Pattern 2: Storing Job Hosts as Grid Row Indices
**What:** Referencing hosts by index into the current DataGridView.
**Why bad:** Grid indices change with sorting, filtering, environment switches, CSV reloads. A job referencing "rows 0, 3, 7" would silently target wrong hosts.
**Instead:** Store full host data (IP, port, columns) in `JobHostList` as a self-contained snapshot.

### Anti-Pattern 3: Polling Cron in a Tight Loop
**What:** Checking every second whether a job is due.
**Why bad:** Wasteful CPU. Desktop app should be efficient.
**Instead:** Calculate the soonest next-run across all jobs, set the timer interval to wake at that time (with a maximum cap of 60 seconds for safety). Recalculate after each job completes or after job edits.

### Anti-Pattern 4: Putting Scheduler Logic in Form1
**What:** Adding timer logic, cron evaluation, or job execution orchestration directly to Form1.
**Why bad:** Form1 is already ~10,500 lines. The project explicitly follows service-oriented architecture. Scheduler logic in Form1 violates the established pattern and makes testing impossible.
**Instead:** All logic in `SchedulerService`, `JobRunner`, etc. Form1 only subscribes to events and provides menu/button handlers.

### Anti-Pattern 5: Blocking the UI Thread for Job Operations
**What:** Synchronous job save/load/delete on the UI thread.
**Why bad:** File I/O can stall the UI, especially with many jobs or large history files.
**Instead:** All persistence operations are async. Job editing dialogs use async save patterns.

## Component Build Order (Dependencies)

The scheduler system has clear internal dependencies that dictate build order:

```
Phase 1: Foundation (no dependencies on each other)
  Models: JobDefinition, JobSchedule, JobHostList, JobCredentialConfig, JobRunRecord
  CronEvaluator (pure logic, no dependencies)
  JobDefinitionStore (depends on Models only)

Phase 2: Execution (depends on Phase 1)
  JobRunner (depends on Models + existing SshExecutionService + PresetManager)
  JobHistoryStore (depends on Models, follows HistoryStorageService pattern)

Phase 3: Orchestration (depends on Phase 2)
  SchedulerService (depends on JobDefinitionStore + CronEvaluator + JobRunner + JobHistoryStore)

Phase 4: UI (depends on Phase 3)
  JobEditorDialog (depends on JobDefinitionStore + CronEvaluator + PresetManager)
  JobManagerDialog (depends on SchedulerService + JobDefinitionStore + JobHistoryStore)
  Form1 integration (depends on SchedulerService -- status bar, menu, events)
```

Each phase is testable independently:
- Phase 1: Unit tests with no mocking needed
- Phase 2: Unit tests with mocked SshExecutionService
- Phase 3: Integration tests with mocked JobRunner
- Phase 4: Manual UI testing + WinForms test harness

## File Placement

Following existing conventions from STRUCTURE.md:

```
Models/
  JobDefinition.cs
  JobSchedule.cs
  JobHostList.cs
  JobCredentialConfig.cs
  JobRunRecord.cs
  SchedulerSettings.cs          (added to AppConfiguration)

Services/
  Scheduler/                    (new sub-directory, like Scripting/ and Editor/)
    SchedulerService.cs         (main orchestrator)
    JobDefinitionStore.cs       (job CRUD + persistence)
    JobRunner.cs                (single job execution)
    JobHistoryStore.cs          (run history persistence)
    CronEvaluator.cs            (cron parsing + next-run)

UI/
  (JobEditorDialog.cs or at root level following existing dialog placement)
  (JobManagerDialog.cs or at root level)

Root level:
  JobEditorDialog.cs            (follows SettingsDialog.cs, EnvironmentDialog.cs placement)
  JobManagerDialog.cs           (follows same pattern)
```

## Cron Library Decision

Use **Cronos** (https://github.com/HangfireIO/Cronos) for cron expression parsing.

**Why Cronos:**
- Lightweight (single file, no dependencies)
- .NET Standard 1.0+ compatible, works with .NET 8
- Handles 5-field cron expressions (minute, hour, day, month, weekday)
- `CronExpression.Parse()` + `GetNextOccurrence()` is the entire API surface needed
- MIT license
- Maintained by the Hangfire team (established .NET scheduling ecosystem)
- No need for a full scheduler framework (Quartz.NET, Hangfire) -- this is an in-app timer, not a distributed job system

**CronEvaluator wraps Cronos** to provide:
- Validation with user-friendly error messages
- Next N occurrences preview for the job editor
- Timezone handling (local time, since this is a desktop app)

## Scalability Considerations

| Concern | At 5 jobs | At 50 jobs | At 200 jobs |
|---------|-----------|------------|-------------|
| Timer evaluation | Negligible | Negligible | Sort by next-run, evaluate top N only |
| Job definition file | Single JSON file fine | Single JSON file fine | Still fine (200 jobs is ~200KB JSON) |
| Run history storage | Per-job files, no issue | Per-job files, index file grows | Pruning critical; consider splitting index |
| Concurrent execution | Default 1-2 | User sets 5-10 | SemaphoreSlim handles; connection pool is the bottleneck |
| Memory | Minimal | Moderate (job definitions in memory) | Still fine (~50MB for 200 jobs with host lists) |

For a desktop app, 200 scheduled jobs is an extreme upper bound. The architecture handles it without any exotic patterns.

## Integration Points with Existing Code

| Existing Component | Integration Type | Changes Needed |
|-------------------|------------------|----------------|
| `SshExecutionService` | Used by JobRunner | None -- instantiate separately for scheduler |
| `SshConnectionPool` | Shared or separate | Consider separate pool for scheduler to avoid contention with manual execution |
| `PresetManager` | Read-only by JobRunner | None -- just reads preset definitions |
| `ConfigurationService` | Stores SchedulerSettings | Add `SchedulerSettings` class to AppConfiguration |
| `ICredentialProvider` | Used by JobRunner | None -- reads credentials |
| `Form1` constructor | Instantiate SchedulerService | Add ~15 lines: create service, wire events |
| `Form1` status bar | Show scheduler state | Add scheduler label to existing status strip |
| `DialogTheme` | Apply to new dialogs | None -- call `DialogTheme.ApplyTo()` as all other dialogs do |
| `HistoryStorageService` | Pattern reference only | None -- JobHistoryStore follows same approach |

## Sources

- Existing codebase analysis: `Services/ExecutionCoordinator.cs`, `Services/HistoryStorageService.cs`, `Services/SshExecutionService.cs`, `Models/AppConfiguration.cs`
- Existing architectural patterns: `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/STRUCTURE.md`
- Project requirements: `.planning/PROJECT.md`
- Cronos library: https://github.com/HangfireIO/Cronos (HIGH confidence -- well-established, used by Hangfire ecosystem)
- .NET `System.Threading.Timer`: Official .NET documentation (HIGH confidence)
- `SemaphoreSlim` for async concurrency: Official .NET documentation (HIGH confidence)

---

*Architecture analysis: 2026-03-07*
