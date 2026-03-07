# Domain Pitfalls

**Domain:** In-app job scheduler for WinForms SSH automation tool
**Researched:** 2026-03-07

## Critical Pitfalls

Mistakes that cause rewrites or major issues.

### Pitfall 1: Timer Tick on Wrong Thread Freezes or Crashes UI

**What goes wrong:** The scheduler timer fires a callback that directly touches WinForms controls (updating status bar, progress indicators, job list) from a non-UI thread, causing cross-thread exceptions or silent corruption. Alternatively, using `System.Windows.Forms.Timer` for the scheduler tick means the callback runs on the UI thread, and any non-trivial work (calculating next run times, checking job states, acquiring locks) blocks the UI.

**Why it happens:** .NET has four timer types with different threading behaviors. Developers pick one without understanding which thread the callback runs on. The existing SSH_Helper codebase already uses `BeginInvoke` for SSH output events, but a scheduler introduces a second always-running timer alongside the connection pool's `System.Threading.Timer`, and the patterns must be consistent.

**Consequences:** UI freezes during scheduler ticks (if Forms timer with heavy work), or `InvalidOperationException` / silent data corruption (if Threading timer touching controls). In extreme cases, suppress-flag state in Form1 (30+ booleans) gets corrupted when a scheduler callback races with user interaction.

**Prevention:**
- Use `System.Threading.Timer` or `System.Timers.Timer` for the scheduler heartbeat -- never `System.Windows.Forms.Timer`.
- All UI updates from the scheduler must go through `BeginInvoke` (not `Invoke`, to avoid deadlocks if the UI thread is waiting on a scheduler lock).
- Keep the timer callback extremely lightweight: check wall clock against next-run times, enqueue jobs to a `ConcurrentQueue<ScheduledJob>`, return immediately. A separate background task processes the queue.
- Hold a strong reference to the timer object to prevent GC collection during app lifetime.

**Detection:** UI becomes sluggish on a regular cadence (every N seconds matching the timer interval). Cross-thread exceptions in debug builds. Status bar updates lag behind actual job state.

**Phase:** Core scheduler engine (Phase 1). Must be right from the start -- retrofitting threading model is a rewrite.

---

### Pitfall 2: Concurrent Jobs Exhaust SSH Connections and Starve Target Hosts

**What goes wrong:** Multiple scheduled jobs fire simultaneously (or a single job targets many hosts), overwhelming the `SshConnectionPool` and target SSH daemons. SSH servers have `MaxStartups` (default 10:30:100) and `MaxSessions` (default 10) limits. Exceeding these causes connection resets, authentication failures, and cascading timeouts that look like network issues rather than resource exhaustion.

**Why it happens:** The existing `SshConnectionPool` has a single `SemaphoreSlim(1,1)` creation lock (noted in CONCERNS.md as a bottleneck) and no maximum pool size. The current sequential host execution in `SshExecutionService` masks this problem because only one connection is active at a time. A scheduler with bounded concurrency creates parallel connections for the first time, exposing the pool's lack of limits.

**Consequences:** Connection failures cascade. Jobs that should take 30 seconds timeout after 5 minutes. Target infrastructure (network switches, firewalls) may rate-limit or block the source IP. Windows client-side socket exhaustion at ~512 concurrent SSH sessions (Win32-OpenSSH hard limit, relevant if using system SSH for tunneling).

**Prevention:**
- Implement a two-level concurrency model: (1) job-level concurrency (max N jobs running simultaneously), and (2) per-job host concurrency (max M hosts per job executing in parallel). Use `SemaphoreSlim` for both.
- Add `MaxPoolSize` to `SshConnectionPool` with LRU eviction (already identified in CONCERNS.md scaling limits).
- Replace the global `_creationLock` with per-host locks via `ConcurrentDictionary<string, SemaphoreSlim>` (already identified in CONCERNS.md).
- Default the global concurrency limit conservatively (e.g., 3 concurrent jobs, 5 concurrent hosts per job) and let users raise it.
- Implement backpressure: if a job cannot acquire a connection within a timeout, fail that host attempt cleanly rather than blocking indefinitely.

**Detection:** Jobs report many hosts as "connection timeout" or "connection refused" when the same hosts work fine in manual execution. Pool connection count grows monotonically during scheduler activity.

**Phase:** Connection pool hardening should happen before scheduler goes live. The pool changes are a prerequisite phase; the concurrency model belongs in the core scheduler engine phase.

---

### Pitfall 3: Scheduler State Corrupts on Ungraceful App Exit

**What goes wrong:** The app crashes or the user force-closes it while jobs are running. The scheduler state file records a job as "running" with no end time. On next launch, the scheduler sees an orphaned "running" job and either: (a) tries to resume it (dangerous -- partial execution), (b) marks it failed but loses output, or (c) leaves it in "running" state forever, confusing the user and possibly blocking the concurrency slot.

**Why it happens:** Desktop apps lack the graceful shutdown guarantees of hosted services. `FormClosing` can be bypassed by task manager kills, power loss, or unhandled exceptions. The existing `ConfigurationService` writes JSON synchronously, which can produce truncated files on crash.

**Consequences:** Orphaned job records, corrupted job state file, lost run history, permanently "stuck" jobs consuming concurrency slots.

**Prevention:**
- On startup, scan for any jobs in "running" state and transition them to "interrupted" with a clear status message and whatever partial output was flushed.
- Write job state transitions atomically: write to temp file, then rename (atomic on NTFS). Never update state in-place.
- Flush per-run output to disk incrementally (not just at completion) so partial output survives crashes.
- Use `FormClosing` with a cancellation grace period: request cancellation of running jobs, wait up to N seconds, then force-close. But never depend on this -- the orphan-recovery path must work independently.
- Keep a heartbeat timestamp in the state file. On startup, if a "running" job's heartbeat is stale (older than 2x the expected interval), mark it interrupted.

**Detection:** After a crash, the job list shows a job permanently stuck in "Running" state. History shows runs with no end time.

**Phase:** Persistence layer design (early phase). The atomic write pattern and orphan recovery must be designed into the storage format from day one.

---

### Pitfall 4: Cron Expression Drift and Missed-Run Confusion

**What goes wrong:** The scheduler calculates next-run times using `DateTime.Now` rather than tracking the last-fired time relative to the cron expression. Over time, timer resolution (~15.6ms on Windows) and processing delays cause the scheduler to either: (a) fire slightly late and calculate the next occurrence from "now" (skipping the intended fire time), or (b) fire twice for the same cron occurrence when the timer callback re-enters before the job is marked as dispatched.

**Why it happens:** Cron schedule evaluation is wall-clock-based. The Windows timer has ~15.6ms resolution, and DST transitions or system clock adjustments (NTP sync) can shift wall-clock time forward or backward. A naive "check every second" approach accumulates error. The PROJECT.md explicitly states "always skip" for missed runs, but the mechanism to detect and record skipped runs needs careful implementation.

**Consequences:** Jobs fire at wrong times. Users see "last run: 2:00:01 AM" instead of "2:00:00 AM" and lose trust. DST transitions cause a job to fire twice (fall-back) or not at all (spring-forward). The "missed run" log fills with false positives if the detection window is too tight.

**Prevention:**
- Use a proven cron expression library (Cronos is lightweight, handles DST correctly, MIT licensed, and is what Hangfire uses internally). Do not hand-roll cron parsing.
- Track `LastFiredUtc` per job in UTC, not local time. Calculate `NextRunUtc` from the cron expression relative to `LastFiredUtc`, not from "now".
- Use a tolerance window for "is it time to fire?" -- e.g., if `NextRunUtc` is within the past 60 seconds, fire it. If older than 60 seconds, record as skipped.
- Protect against double-firing with a compare-and-swap on the job's `NextRunUtc`: only fire if the current `NextRunUtc` matches what you calculated. After firing, advance `NextRunUtc` before releasing any locks.
- Store all times in UTC internally. Convert to local only for display.

**Detection:** Jobs fire at inconsistent times. DST transition days show missing or duplicate runs. The "next run" preview in the UI doesn't match when the job actually fires.

**Phase:** Core scheduler engine. The cron evaluation and fire/skip logic is the heart of the scheduler and must be correct from the start.

---

### Pitfall 5: Scheduler Blocks Manual Execution (and Vice Versa)

**What goes wrong:** A scheduled job and a manual "Run Now" from the main UI both try to use `SshExecutionService` simultaneously. The existing service was designed for single-execution-at-a-time from the UI. Events like `ProgressChanged` and `OutputReceived` are wired to Form1's output pane, so a background scheduled job's output bleeds into the user's manual session, or the service rejects concurrent execution because of internal state (e.g., a shared `CancellationTokenSource`).

**Why it happens:** `SshExecutionService` appears to use instance-level state for execution tracking. The event-driven communication model (`ProgressChanged`, `OutputReceived`, `ColumnUpdateRequested`) routes all output to Form1's single output pane. There is no concept of execution "channels" or isolated execution contexts. The existing `_isExecuting` flag in Form1 (line ~120-198 state flags) was designed for mutual exclusion with manual execution.

**Consequences:** Output from scheduled jobs corrupts the user's manual session display. Or: the scheduler waits for manual execution to finish before firing, causing missed schedules. Or: two executions race on the connection pool's global creation lock, causing deadlocks.

**Prevention:**
- Create a new `SchedulerExecutionService` (or execution context abstraction) that wraps `SshExecutionService` but routes events to a separate output sink (the job's run history), not to Form1's output pane.
- Each scheduled job execution must have its own `CancellationTokenSource` and output buffer, independent of the manual execution path.
- The scheduler should use its own `SshConnectionPool` instance, or the pool must be made truly concurrent (per-host locks instead of global lock).
- Manual execution and scheduled execution should be fully independent -- neither blocks the other. The UI shows a "scheduler active" indicator but does not couple execution flows.
- If the same preset is running manually and fires on schedule, the scheduler should either skip that firing (with a log entry) or run it independently -- make this a design decision, not an accident.

**Detection:** User runs a command manually and sees unexpected output from a scheduled job interleaved. Or: scheduled jobs show as "skipped" whenever the user is doing manual work.

**Phase:** Architecture design phase (before implementation). This determines whether `SshExecutionService` needs refactoring or whether a wrapper/adapter pattern suffices. Must be decided before coding the scheduler engine.

---

## Moderate Pitfalls

### Pitfall 6: Unbounded Output Accumulation from Background Jobs

**What goes wrong:** Scheduled jobs run unattended, potentially against many hosts with verbose output. Unlike manual execution where the user watches and can stop, scheduled jobs silently accumulate megabytes of output in memory. The existing `_outputBuffer` StringBuilder in Form1 already has scaling concerns (CONCERNS.md: thresholds at 500K and 10M chars). Background jobs without the UI's trim logic will accumulate without limit.

**Prevention:**
- Stream job output directly to disk (per-run output file) with a capped in-memory buffer (e.g., last 100KB for status display).
- Set a per-job maximum output size. Truncate with a "[output truncated at X MB]" marker.
- Do NOT route scheduled job output through Form1's StringBuilder pipeline. The scheduler's output path must be independent.
- Implement the dual pruning (max entries AND time-based) from PROJECT.md requirements early in the history storage design.

**Detection:** Memory usage grows steadily while scheduled jobs run. `GC.Collect()` calls increase (the app already has explicit GC calls, per CONCERNS.md).

**Phase:** Output/history storage design. Must be addressed before the scheduler can run unattended jobs.

---

### Pitfall 7: Job Credential Lifecycle Mismanagement

**What goes wrong:** A scheduled job stores a reference to credentials (password, SSH key path) at job creation time. Later, the user changes their password in Credential Manager, rotates an SSH key, or the per-host grid column values change. The scheduled job either: (a) uses stale credentials and fails silently, or (b) reads credentials at execution time but the credential source has been deleted.

**Prevention:**
- Resolve credentials at execution time, not at job definition time. The job definition stores a credential *strategy* ("use stored cred X", "inherit from preset", "read from host grid column Y"), not the credential itself.
- Validate credential availability during the pre-flight check before job execution. If credentials cannot be resolved, fail the job immediately with a clear error, not after connecting to 49 of 50 hosts.
- For "inherit from preset" mode, resolve the preset's current credential configuration at fire time.
- Log which credential strategy was used in the run history for debugging.

**Detection:** Jobs that worked for weeks suddenly fail with "authentication failed" after a credential rotation. The error appears on all hosts simultaneously.

**Phase:** Job editor / credential configuration phase.

---

### Pitfall 8: Host List Desynchronization

**What goes wrong:** A job has a "dedicated host list" (per PROJECT.md requirements), but the user expects it to stay in sync with the main grid or a CSV file. They add a new host to the main grid, assume the scheduled job picks it up, and the new host is never checked. Or: they delete a host from the grid but the job still targets it, causing connection failures to a decommissioned device.

**Prevention:**
- Make the job host list source explicit and visible in the UI: "Static list (copied at creation)" vs "CSV file reference (re-read at execution time)".
- For static lists, show a "last synced" indicator and provide a "re-import from grid/CSV" action.
- For CSV file references, validate the file exists and is readable during pre-flight. Handle the file being moved/deleted gracefully.
- Consider a "diff" view that shows what changed between the job's host list and the current main grid.

**Detection:** Users report "the job didn't run against the new host I added." Job targets hosts that no longer exist and logs repeated connection failures.

**Phase:** Job editor UI phase. The host list management UX is where this pitfall is prevented.

---

### Pitfall 9: Re-Entrant Timer Callbacks Cause Double-Firing

**What goes wrong:** The scheduler's heartbeat timer fires while the previous callback is still processing (evaluating cron expressions, dispatching jobs). Two callbacks both see the same job as "ready to fire" and dispatch it twice.

**Prevention:**
- Use `System.Threading.Timer` with `Timeout.Infinite` for the period, and manually re-arm the timer at the end of each callback (the "one-shot + re-arm" pattern). This guarantees no re-entrancy.
- Alternatively, use an `int` flag with `Interlocked.CompareExchange` at the top of the callback to bail out if already executing.
- The existing `SshConnectionPool` uses a similar pattern with `_idleKeepAliveSweepRunning` and `Interlocked.CompareExchange` -- follow that pattern.

**Detection:** Job run history shows two runs with start times within milliseconds of each other. Run count per day is double what the cron schedule implies.

**Phase:** Core scheduler engine.

---

## Minor Pitfalls

### Pitfall 10: Cron Expression UX Leads to User Mistakes

**What goes wrong:** Users create a cron expression meaning "every minute" when they meant "every hour", or use `*/5` thinking it means "every 5 hours" when it means "every 5 minutes". The job fires 60x more often than intended, overwhelming target hosts.

**Prevention:**
- Show a human-readable translation of the cron expression in the job editor (e.g., "Every 5 minutes" or "At 2:00 AM on weekdays").
- Show the next 5-10 calculated fire times so users can verify.
- Provide preset templates for common schedules (hourly, daily at midnight, weekly, etc.) instead of requiring raw cron syntax.
- Set a minimum interval guard (e.g., warn if the schedule fires more often than every 5 minutes).

**Phase:** Job editor UI.

---

### Pitfall 11: History Pruning Happens During Job Execution

**What goes wrong:** The pruning task (delete old history entries) runs while a job is completing and writing its output. File locks or concurrent JSON modifications cause the run's output to be partially written or the pruning to skip the corrupt entry, leaving orphaned files.

**Prevention:**
- Never prune while a job is actively writing output. Use a reader-writer lock or defer pruning to idle periods.
- Prune by deleting complete run directories/files, not by modifying shared index files while writers are active.
- Use the atomic write pattern (write temp file, rename) for both history entries and the history index.

**Phase:** History/output storage phase.

---

### Pitfall 12: Form1 God Object Absorbs Scheduler UI Logic

**What goes wrong:** Following the path of least resistance, scheduler UI code (job list panel, status bar updates, notification popups) gets added directly to Form1.cs, pushing it from 10,471 lines to 12,000+. This accelerates the existing god-object tech debt (CONCERNS.md) and makes future extraction harder.

**Prevention:**
- Create a `SchedulerController` or `SchedulerPresenter` class that owns all scheduler-related UI logic.
- The scheduler management UI should be a separate dialog/form (`SchedulerDialog`), not embedded in Form1.
- Form1's only scheduler touchpoint should be: (1) a status bar label bound to scheduler state, and (2) a menu item to open the scheduler dialog.
- Follow the extraction pattern already recommended in CONCERNS.md for other Form1 regions.

**Detection:** Form1.cs line count increases by more than ~100 lines for scheduler integration.

**Phase:** UI integration phase. Enforce as an architectural constraint from the start.

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Scheduler engine design | Timer threading model wrong (Pitfall 1) | Use `System.Threading.Timer` with one-shot + re-arm pattern |
| Scheduler engine design | Double-firing from re-entrant callbacks (Pitfall 9) | Follow `SshConnectionPool._idleKeepAliveSweepRunning` pattern |
| Scheduler engine design | Cron drift and DST bugs (Pitfall 4) | Use Cronos library, UTC internally, tolerance window |
| Connection pool hardening | SSH connection exhaustion (Pitfall 2) | Per-host locks, max pool size, two-level concurrency |
| Persistence layer | Crash-corrupted state (Pitfall 3) | Atomic writes, orphan recovery on startup, heartbeat timestamps |
| Execution isolation | Manual vs scheduled execution collision (Pitfall 5) | Separate execution contexts, independent output sinks |
| Output/history storage | Unbounded memory from background jobs (Pitfall 6) | Stream to disk, capped in-memory buffer, per-job output limits |
| Output/history storage | Pruning races with active writes (Pitfall 11) | Reader-writer locks, prune only during idle |
| Job editor UI | Credential staleness (Pitfall 7) | Resolve at execution time, store strategy not value |
| Job editor UI | Host list desync (Pitfall 8) | Explicit source type, re-import action, diff view |
| Job editor UI | Cron expression mistakes (Pitfall 10) | Human-readable preview, next-N fire times, templates |
| UI integration | Form1 bloat (Pitfall 12) | Separate SchedulerController/Dialog, max ~100 lines in Form1 |

## Sources

- [Quartz.NET Best Practices](https://www.quartz-scheduler.net/documentation/best-practices.html) -- misfire handling, thread pool sizing, exception handling
- [Quartz.NET FAQ](https://www.quartz-scheduler.net/documentation/faq.html) -- DST behavior, in-memory vs persistent stores
- [Complete Guide to Quartz.NET in .NET 8](https://quartznetpro.com/posts/2025/11/complete-guide-quartznet-job-scheduling-dotnet-8/) -- integration patterns
- [Microsoft: Cross-thread operations in WinForms](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls) -- Invoke/BeginInvoke patterns
- [Microsoft: MaxStartups/MaxSessions troubleshooting](https://learn.microsoft.com/en-us/troubleshoot/windows-server/system-management-components/troubleshoot-openssh-connection-issues-maxstartups-maxsessions) -- SSH server connection limits
- [Win32-OpenSSH 512 session limit](https://github.com/PowerShell/Win32-OpenSSH/issues/2045) -- Windows-specific hard limit
- [Cronitor: Preventing duplicate cron executions](https://cronitor.io/guides/how-to-prevent-duplicate-cron-executions) -- overlap prevention patterns
- [5 Ways Cron Jobs Fail Silently](https://dev.to/deadping/5-ways-your-cron-jobs-are-failing-silently-and-how-to-catch-them-2njp) -- silent failure patterns
- [WinForms async patterns](https://grantwinney.com/using-async-await-and-task-to-keep-the-winforms-ui-more-responsive/) -- Task.Run and UI thread marshalling
- [C# Timer Best Practices](https://xafmarin.com/best-practices-for-using-timers-in-c/) -- timer type selection, GC collection, re-entrancy

---

*Pitfalls analysis: 2026-03-07*
