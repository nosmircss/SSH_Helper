# Feature Landscape

**Domain:** In-app job scheduler for SSH command automation (WinForms desktop)
**Researched:** 2026-03-07

## Table Stakes

Features users expect from any job scheduler. Missing any of these and the feature feels half-baked.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Cron-based recurring schedules | Every scheduler from cron to Rundeck supports this; it is the fundamental scheduling primitive | Medium | Use Cronos library for parsing -- fastest .NET option with full DST/timezone handling |
| One-time (run-once) schedules | Common need for "run this maintenance task tonight at 2am" | Low | Auto-disable after execution; mark as completed in job list |
| Human-readable cron preview | Raw cron expressions are opaque to most users; Rundeck, Cronicle, and every modern scheduler shows "At 09:00, Monday through Friday" | Low | CronExpressionDescriptor NuGet package converts cron to English |
| Next N run times preview | Users need to verify their schedule is correct before saving; standard in cron builder UIs | Low | Cronos.GetNextOccurrence in a loop; show next 5-10 times |
| Visual schedule builder | Not everyone knows cron syntax; tab-based UI (minutes/hours/days/months/weekdays) plus common presets (hourly, daily, weekly) | Medium | Presets dropdown + manual cron input with live preview; no need for full drag-and-drop |
| Run Now (manual trigger) | Every scheduler (Rundeck, Jenkins, Cronicle) has this; users need to test jobs without waiting | Low | Bypass schedule, execute immediately, still record in history |
| Job enable/disable toggle | Users must be able to pause jobs without deleting them | Low | Simple boolean flag on job definition |
| Cancel running job | Long-running SSH jobs need an abort mechanism; CancellationToken already exists in SshExecutionService | Low | Wire existing cancellation infrastructure |
| Per-run history with status | Users expect to see when jobs ran, whether they passed/failed, and how long they took | Medium | Start/end time, duration, success/partial/failure state, host counts |
| Per-run output retention | Without output, users cannot diagnose failures; Rundeck stores stdout/stderr per execution | Medium | Separate output files per run, referenced from history metadata |
| History pruning/cleanup | Unbounded history causes disk and UI performance issues; Rundeck community repeatedly requests this | Low | Dual pruning: max entries AND max age, whichever triggers first |
| Dedicated host list per job | Jobs must be self-contained; if main grid changes, scheduled jobs should not break | Medium | Copy-from-main-grid, import CSV, or manual entry |
| Configurable credentials per job | Different jobs target different environments with different auth requirements | Medium | Stored creds, inherit from preset, or per-host grid column |
| Job persistence across restarts | Jobs must survive app restart; losing schedule definitions is unacceptable | Low | JSON persistence following existing ConfigurationService pattern |
| Status bar/tray integration | User needs to know scheduler is active without opening the scheduler UI | Low | Status text showing next upcoming job and scheduler state |
| In-app notifications on completion/failure | Users need to know when something went wrong without staring at the scheduler | Low | Toast/popup notification, configurable per job |

## Differentiators

Features that set SSH_Helper's scheduler apart. Not expected in a basic scheduler, but valued by power users.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Folder jobs (run all presets in folder) | Batch workflows like "run all backup presets" or "run all health checks" -- unique to SSH_Helper's preset/folder model | Medium | Sequential or parallel execution, configurable per job |
| Variable substitution in schedules | Leverage existing `{{column_name}}` system in scheduled contexts -- host-specific commands without duplicating presets | Low | Already works in execution engine; just needs to flow through scheduler |
| Job chaining / post-run triggers | "After backup completes, run verification" -- common in enterprise schedulers (Rundeck, ActiveBatch), rare in desktop tools | High | Defer to v2; adds significant complexity in error handling and UI |
| Execution concurrency control | Configurable max parallel jobs prevents network saturation on large host lists | Medium | Queue excess jobs; show queue status in UI |
| Missed-run audit trail | Recording skipped runs (app was closed) gives operators visibility into gaps -- most desktop schedulers silently skip | Low | Log entry with "skipped: application not running" timestamp |
| Export/import job definitions | Share scheduled workflows between team members or machines | Low | GZip + Base64 following existing PresetManager export pattern |
| Job duplication | Quick way to create similar jobs with minor variations | Low | Clone existing job, open in editor |
| Dry-run mode | Preview what a job WOULD do without actually executing SSH commands | Medium | Show target hosts, resolved commands, schedule -- no actual connection |
| Run history filtering | Filter by job name, date range, status (success/failure/skipped) -- standard in Rundeck but a differentiator for a desktop app | Low | DataGridView filtering on history view |
| Execution time trending | Show average duration over time so users can spot degrading performance | Medium | Defer to v2; requires charting UI and statistical aggregation |

## Anti-Features

Features to explicitly NOT build. Each adds complexity without proportional value for a desktop SSH tool.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Email/SMTP notifications | External dependency, configuration complexity, SMTP server management -- overkill for a desktop app where the user is at the machine | In-app popup/toast notifications; log to run history |
| Running jobs while app is closed | Requires Windows Service architecture, installer changes, service management -- fundamentally changes the app model | Clearly communicate "scheduler runs while app is open"; missed-run audit trail covers the gap |
| Windows Task Scheduler integration | Coupling to OS scheduler adds fragility, complicates debugging, splits job state across two systems | Self-contained in-app scheduler; simpler mental model |
| Catch-up/auto-execute missed jobs | Dangerous: bulk execution on startup could overwhelm target hosts; unpredictable timing | Always skip + record as skipped; user can manually Run Now |
| REST API / remote triggering | Desktop app, not a server; adds HTTP server dependency, security surface, port management | Keep it local; if remote needed, that is a different product |
| AI-powered scheduling suggestions | Trendy in enterprise tools (RunMyJobs) but massive complexity for minimal value in a preset runner | Simple cron presets dropdown covers 90% of use cases |
| Drag-and-drop workflow designer | Enterprise scheduler feature (ActiveBatch, Stonebranch); overkill for "run preset X on schedule Y" | Simple job editor form with preset/folder picker |
| Multi-user / RBAC | Desktop single-user app; role-based access adds auth complexity with no audience | Single user owns all jobs |
| Calendar view of scheduled jobs | Nice visually but high UI complexity for marginal value when job list with next-run column works fine | Job list with sortable "Next Run" column |
| CPU/memory resource limits per job | Relevant for server schedulers (Cronicle); SSH_Helper jobs run remotely so local resource limits are irrelevant | SSH command timeouts already handle runaway remote commands |

## Feature Dependencies

```
Cron parsing (Cronos library) --> Schedule evaluation --> Job execution
                              --> Human-readable preview
                              --> Next N runs preview

Job persistence (JSON) --> Job editor UI --> Job list UI
                       --> Scheduler service (loads jobs on startup)

Dedicated host list --> Job editor UI (host picker/importer)
                    --> Job execution (provides targets)

SshExecutionService (existing) --> Job execution engine
                               --> Run history recording
                               --> Output retention

Run history --> History UI --> History pruning
           --> Output file management

Folder jobs --> Preset folder enumeration (existing PresetManager)
           --> Sequential/parallel execution mode

Concurrency control --> Job queue --> Execution engine
```

## MVP Recommendation

Prioritize for Phase 1 (minimum viable scheduler):

1. **Job CRUD with persistence** -- Define, save, edit, delete jobs (table stakes foundation)
2. **Cron scheduling with visual builder** -- Schedule presets with human-readable preview and next-run display
3. **One-time schedules** -- Run-once with auto-disable
4. **Dedicated host list per job** -- Self-contained jobs that do not depend on main grid state
5. **Job execution engine** -- Timer-based evaluation, execute due jobs via existing SshExecutionService
6. **Run Now + Cancel** -- Manual trigger and abort controls
7. **Basic run history** -- Per-run metadata (time, duration, status, host counts)
8. **Per-run output retention with pruning** -- Full output stored, bounded by max entries and max age
9. **Status bar integration** -- Scheduler state visible in main window
10. **In-app failure notifications** -- Toast/popup when a job fails

Defer to Phase 2:
- **Folder jobs** (sequential/parallel preset batches) -- adds execution complexity
- **Concurrency control** (job queue with configurable limits) -- Phase 1 can run one job at a time
- **Export/import jobs** -- nice to have after core is solid
- **Dry-run mode** -- useful but not blocking
- **Job chaining** -- significant complexity, wait for user demand

## Sources

- [Rundeck Features](https://www.rundeck.com/features) -- run history, output retention patterns
- [Rundeck Activity Page](https://docs.rundeck.com/docs/manual/08-activity.html) -- history UI filtering
- [Cronos (.NET cron library)](https://github.com/HangfireIO/Cronos) -- cron parsing with DST handling
- [NCrontab](https://github.com/atifaziz/NCrontab) -- alternative .NET cron parser
- [CronExpressionDescriptor](https://bradymholt.github.io/cron-expression-descriptor/) -- human-readable cron descriptions
- [Cronicle](https://cronicle.net/) -- desktop scheduler UI patterns
- [JS7 JobScheduler Features](https://www.sos-berlin.com/en/jobscheduler-features) -- enterprise scheduler feature set
- [SSH Client Comparison](https://armbasedsolutions.com/blog-detail/comparison-of-six-popular-ssh-terminal-tools) -- competitor automation features
- [Best Job Scheduler Software 2026](https://sourceforge.net/software/job-scheduler/) -- market landscape
