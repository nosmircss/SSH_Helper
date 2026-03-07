# Phase 3: Execution Pipeline - Context

**Gathered:** 2026-03-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Scheduled jobs execute automatically at their due times, with manual run-now, cancellation, concurrency control, and folder job support. This phase delivers the scheduler timer, job execution engine, concurrency management, folder job execution, crash recovery, and run-now/cancel controls. History recording, output retention, pruning, and the full management UI are separate phases.

</domain>

<decisions>
## Implementation Decisions

### Concurrency & queueing
- Default max concurrent jobs: 3 (user-configurable via settings)
- Concurrency is per-job (one job = one slot, regardless of how many hosts it targets)
- When a job is already running and its next cron trigger fires, skip the trigger and log it — no queuing of duplicate runs
- When multiple jobs are queued waiting for a slot, execute in FIFO order (first due, first run)

### Folder job execution
- Default execution mode: sequential (configurable per-job to parallel)
- Only direct children of the target folder are executed — no recursive subfolder inclusion
- Per-job configurable stop-on-error toggle: continue through all presets (default) or stop on first failure
- A folder job counts as 1 concurrency slot regardless of how many presets it contains or whether running in parallel mode

### Run-now & cancellation
- Run-now bypasses the concurrency queue — fires immediately regardless of how many jobs are already running
- Block run-now while the same job is already running (disable button, show "Already running" status)
- Immediate cancellation via CancellationToken — same behavior as existing manual execution cancel in SshExecutionService
- Drift detection blocks both scheduled execution and run-now (consistent with Phase 1 decision: user must re-acknowledge before any execution)

### Crash recovery
- Track running state in the jobs file via a RunningState field on JobDefinition (null when idle; set with start time when running)
- On startup, any job with RunningState set is treated as orphaned — mark as failed
- Best-effort partial results: if any output was captured before the crash, preserve it in the run record with per-host completion status
- No auto-retry of orphaned jobs — consistent with "always skip missed runs" policy; user can run-now to retry
- Orphaned run notification: log entry only (no startup popup); Phase 5 UI will surface the failed status

### Scheduler timer
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

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SshExecutionService.ExecutePresetAsync()`: Core SSH execution — reuse directly for job execution against host lists
- `ExecutionCoordinator`: Thin orchestrator for preparing execution inputs — extend or parallel for job execution
- `SshConnectionPool`: Connection pooling with health checks — shared across manual and scheduled execution
- `SchedulingService`: Stateless scheduling logic — `GetNextRunUtc()`, `DetectMissedRuns()`, `MarkOneTimeCompleted()` ready to consume
- `JobStorageService`: Job CRUD and persistence — add RunningState field, consume for job lookup during evaluation
- `JobDefinition`: Model has all needed fields (schedule, hosts, credentials, target type, drift detection)
- `PresetManager`: Preset lookup by name and folder listing — needed for resolving job targets and folder contents
- `ICredentialProvider` + `CredentialTargets`: Credential resolution for all three modes (Stored, InheritFromApp, PerHostColumn)
- `CancellationTokenSource`: Existing pattern in SshExecutionService for cooperative cancellation

### Established Patterns
- `System.Threading.Timer` for background work (no existing usage but fits ThreadPool model)
- Event-driven service-to-UI communication: `EventHandler<T>` for progress, output, state changes
- `volatile` flags + `lock` blocks for thread-safe state (see SshExecutionService._isRunning)
- `sealed` services with constructor injection via manual DI in Form1
- `CancellationToken` as last async parameter throughout the call chain

### Integration Points
- `Form1` constructor: New `JobExecutionService` (or similar) wired here alongside existing services
- `SchedulingService`: Consumed for due-job evaluation — `GetNextRunUtc()` called on each timer tick
- `JobStorageService`: RunningState persistence on job start/complete/fail
- `PresetManager`: Resolve `TargetName` to `PresetInfo` for single presets; list folder contents for folder jobs
- `ConfigurationService`: Read concurrency limit and other scheduler settings from AppConfiguration
- Future Phase 4: Execution results handed off to history recording service

</code_context>

<specifics>
## Specific Ideas

- The scheduler should feel invisible during normal app usage — no UI freezes, no blocking during modal dialogs, just jobs running in the background
- Run-now should be instant feedback — the user clicks and the job starts, no waiting for queue slots
- Crash recovery should be quiet — mark failed, log it, move on. User discovers it when they check history, not via intrusive popups
- Folder jobs with stop-on-error off behave like "run everything and report" — matching the common SSH operational pattern of health checks across a folder of presets

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 03-execution-pipeline*
*Context gathered: 2026-03-07*
