# Jobs & Scheduling — Feature Map

Audit briefing for the scheduler subsystem of SSH_Helper. All paths relative to repo root
`C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper`.

Core files:

| File | LOC | Role |
|---|---|---|
| `Services/SchedulingService.cs` | 194 | Pure cron logic (validate, describe, next-run, missed-run detection) |
| `Services/JobStorageService.cs` | 323 | jobs.json CRUD + CSV host import |
| `Services/JobExecutionService.cs` | 1119 | Timer scheduler, concurrency gate, queue, execution pipeline |
| `Services/JobHistoryService.cs` | 638 | Per-job run history (index + payload files), retention, search |
| `Services/JobExportService.cs` | 186 | .sshjobs file / clipboard export-import |
| `Services/SchedulerHistoryPolicyResolver.cs` | 38 | Effective retention policy (job override → config → constants) |
| `Models/JobDefinition.cs` | 274 | Job DTO + 5 enums (CredentialMode, ScheduleType, FolderExecutionMode, JobExecutionState, JobTargetType) |
| `Models/JobRun{Result,Record,Payload,Filter}.cs`, `Models/JobHostOutput.cs`, `Models/JobHistoryRetentionOptions.cs`, `Models/JobExportDocument.cs`, `Models/QueuedJob.cs`, `Models/RunningJobState.cs`, `Models/SkippedRun{Entry,SummaryEntry}.cs` | — | Run/history/export DTOs |
| `JobListDialog.cs` | 1560 | Scheduler management UI (modeless) |
| `JobEditorDialog.cs` | 2185 | Job create/edit dialog (5 tabs) |
| `ImportPreviewDialog.cs` | 309 | Import conflict preview |
| `RunOutputViewerDialog.cs` | 406 | Per-host run output viewer with search |
| `UI/CronBuilderControl.cs` | 851 | Visual cron builder |
| `Utilities/SchedulerInstanceLock.cs` | 92 | Named-mutex single-scheduler-owner lock |
| `Utilities/SchedulerNotificationFormatter.cs` | 115 | Pure notification/status-bar text formatting |
| `Utilities/SchedulerJobIntegrityUtilities.cs` | ~30 | Missing-target import state, stored-credential note text |
| `Utilities/JobEditorValidator.cs` | 261 | All job editor validation rules |
| `Form1.cs` `#region Scheduler` (~15048–15349) | — | Service wiring, status bar, missed-run recording, Run Now tracking |

Maturity: this is one of the most polished subsystems in the app — heavily unit-tested
(`SSH_Helper.Tests/Services/JobExecutionServiceTests.cs` 1500+ lines, plus storage/history/export/
scheduling/dialog test suites), with deliberate "locked decision" comments from a phased spec
(openspec archives under `openspec/changes/archive/2026-03-13-*`). The gaps are at the seams:
timezone semantics, notification dead-ends, and persistence-convention violations.

---

## Feature inventory

### 1. Cron scheduling (recurring)
- **What**: 5-field cron expressions (Cronos parse; CronExpressionDescriptor for human text). Validation: `Utilities/InputValidator.cs:152-161`. Description (12-hour format): `Services/SchedulingService.cs:32-45`. Next run local: `SchedulingService.cs:53-63` (passes `TimeZoneInfo.Local` to Cronos — cron fields interpreted in LOCAL tz). Next run UTC: `:71-80` (no tz — UTC interpretation). Missed occurrences in a window (exclusive bounds, UTC interpretation): `:89-100`.
- **Due check at runtime**: `JobExecutionService.IsJobDue` (`Services/JobExecutionService.cs:1023-1037`) — Recurring = any occurrence between `_lastEvaluationUtc` and now (`GetMissedOccurrences`, i.e. **UTC interpretation**); multiple missed occurrences in a window collapse to one run.
- **UI**: `UI/CronBuilderControl.cs` — 10 preset chips (`:20-32`, "Every 5 min" … "Quarterly"), 5 per-field dropdowns, raw expression textbox with bidirectional sync, live description + next-run preview + inline validation label. Embedded in JobEditorDialog General tab (`JobEditorDialog.cs:462-477`).

### 2. One-time scheduling
- `ScheduleType.OneTime` + `OneTimeScheduleUtc` (`Models/JobDefinition.cs:191`). Editor picker converts local→UTC (`JobEditorDialog.cs:2024-2025`); must be future at save time (`Utilities/JobEditorValidator.cs:82-94`).
- Due when `OneTimeScheduleUtc <= now` (`JobExecutionService.cs:1030-1031`). After a **successful** run the job is auto-disabled with reason "One-time schedule completed" (`SchedulingService.MarkOneTimeCompleted`, `:188-192`; called from `JobExecutionService.HandlePostExecution`, `:875-898`). A **failed scheduled** (not run-now, not cancelled) one-time run also auto-disables with "One-time schedule failed" (`:883-889`). Cancelled one-time jobs stay enabled (will re-fire).

### 3. Scheduler engine (timer, concurrency, queue)
- **File**: `Services/JobExecutionService.cs`.
- 30-second `System.Threading.Timer` (`Start()`, `:173-182`), first tick immediate; `Interlocked` reentrancy guard (`TimerCallback`, `:292-302`); evaluation loop snapshots jobs and dispatches (`EvaluateAndExecuteDueJobsAsync`, `:308-335`; per-job try/catch with stage tags `EvaluateJobForDueExecution`, `:978-1021`).
- **Concurrency gate**: `SemaphoreSlim` sized from `AppConfiguration.MaxConcurrentJobs` (default 3; `<=0` coerced to 3) read **once at construction** (`:140-142`). Overflow goes to a FIFO `ConcurrentQueue<QueuedJob>` with `_queuedJobIds` dedupe (`TryQueueJob`, `:791-807`); drained when slots free (`DrainQueue`, `:838-860`). Folder jobs count as 1 slot ("locked decision", `:712`).
- **Run Now**: `RunNowAsync` (`:222-268`) **bypasses the gate entirely**; blocks only if the same job is already running (raises `Skipped` state "Already running"). Doesn't check `IsEnabled` — disabled jobs are manually runnable.
- **Cancellation**: per-run CTS; `CancelJob` (`:274-282`); token registered to call `sshService.Stop()` (`:412-414`).
- **Crash recovery**: `RunningJobState` persisted into jobs.json while executing (`TryStartJob`, `:768-774`; cleared in `CompleteJob`, `:827-832`). `Initialize()` (`:153-168`) scans for orphaned `RunningState` at startup, raises `JobStateChanged(Failed, "Application crashed during execution")`, clears state. Must run before `Start()` (Form1.cs:15097-15099 honors this).
- **Single scheduler across instances**: `Utilities/SchedulerInstanceLock.cs` — named mutex `Local\SSH_Helper_Scheduler_v1` plus in-process ownership set; the losing instance never starts the timer and shows a "paused by lock" status (`Form1.cs:15090-15104`, `:15214-15217`).
- **Disposal**: cancels all CTSes, waits max **1000 ms** for tracked executions (`WaitForTrackedScheduledExecutions`, `:1096-1114`), then disposes timer/gate.
- **Testability**: internal ctor takes `jobExecutionOverride` + `jobEvaluationFaultInjector` (`:123-138`).

### 4. Job target types
- `JobTargetType` (`Models/JobDefinition.cs:84-100`): **Preset** (single named preset), **Folder** (all direct children of a preset folder — no recursion, "locked decision" `JobExecutionService.cs:711,722`), **CustomPreset** (commands/YAML stored on the job itself, `CustomPresetCommands` normalized to CRLF, `JobDefinition.cs:138-142,261-272`).
- Resolution: `ResolvePresetForExecution` (`JobExecutionService.cs:690-707`) — CustomPreset wraps content in a transient `PresetInfo`; missing named preset throws → job Fails.
- Folder execution: `ExecuteFolderJobAsync` (`:714-745`) builds `FolderExecutionOptions` with `RunPresetsInParallel` (= `FolderExecutionMode.Parallel`) and `StopOnFirstError` (= `JobDefinition.StopOnError`, default false = continue through all presets); delegates to `SshExecutionService.ExecuteFolderAsync`. Empty folder throws → Fail.
- Editor: target type radios + combo populated with presets or folders (`JobEditorDialog.cs:563-660`); custom preset gets a full Scintilla editor with YAML syntax highlighting, validation service and host-column-aware autocomplete (`JobEditorDialog.cs:62-64,583-624`).

### 5. Per-job host list
- Jobs carry their own host grid snapshot: `Hosts` (list of column→value dicts) + ordered `HostColumns` (`JobDefinition.cs:159-164`). Populated by: hand-editing in the editor Hosts tab (add/rename/delete columns, add/remove rows, `JobEditorDialog.cs:702-1055`), **CSV import** (`JobStorageService.ImportHostsFromCsv`, `:258-290` — requires `Host_IP` column header), or **Copy from main grid** (`BtnCopyFromMain_Click` `JobEditorDialog.cs:1890`, snapshot via `HostGridUtilities.BuildSchedulerCopySnapshot`, `Form1.cs:15181-15190`).
- At run time `BuildHostConnections` (`JobExecutionService.cs:487-544`): `Host_IP` parsed (supports `host:port`); `port` column honored unless host value has explicit port (`:500-506`); `vault_path` column resolves per-row Vault creds (`:508-523`); `username`/`password` columns override; **all** columns copied into `HostConnection.Variables` for `{{var}}` substitution (`:534-538`). Rows lacking Host_IP silently skipped; zero valid hosts throws.

### 6. Credential modes
- `CredentialMode` (`JobDefinition.cs:8-29`): **InheritFromApp** (app default username + Credential Manager default password), **Stored** (per-job secret at `CredentialTargets.JobPasswordTarget(jobId)` in Windows Credential Manager), **PerHostColumn** (username/password columns in host rows), **Vault** (`VaultCredentialPath` + optional `VaultProfileName` override).
- Resolution: `ResolveCredentials` (`JobExecutionService.cs:592-645`). All failure paths return empty strings + `Debug.WriteLine` warnings; `ExecuteJobCoreAsync` rejects only when **both** username and password are empty for non-PerHostColumn modes (`:387-393`).
- Vault profile precedence: job `VaultProfileName` → `EnvironmentVaultProfile` (kept in sync on environment switch, `Form1.cs:1902-1903`) → null (`ResolveJobDefaultVaultProfile`, `:647-655`).
- Editor: Credentials tab with mode panels, stored-credential note ("leave blank to keep current secret", `SchedulerJobIntegrityUtilities.FormatStoredCredentialNote`), Vault path + profile-override combo (`JobEditorDialog.cs:1056-1463`). Validation: `JobEditorValidator.ValidatePerHostCredentials` requires username+password columns and per-non-empty-row values (`:137-172`); Vault requires a path (`:177-186`).
- Lifecycle: deleting a Stored-mode job best-effort deletes its Credential Manager entry (`JobStorageService.Delete`, `:147-169`); duplicating a job copies the stored secret to the new ID and rolls back the duplicate on failure (`JobListDialog.cs:1295-1332`).

### 7. Job persistence (jobs.json)
- `Services/JobStorageService.cs` — versioned wrapper `{Version:1, Jobs:[]}` at `%LocalAppData%\SSH_Helper\jobs.json` (path injectable for tests). Load is corruption-resilient: parse failure → `JsonFileWriter.TryBackupCorrupt` (`jobs.json.corrupt`) + `LoadError` message + empty list (`:74-98`).
- `Save` validates/normalizes name (trim, ≤100 chars, case-insensitive unique, `:113-139`), stamps `ModifiedUtc`, persists, raises `JobsChanged`. `PersistToDisk` (`:227-244`) makes a best-effort `.bak` copy then **plain `File.WriteAllText`** (see gaps).
- `RestoreSnapshots` (internal, `:203-222`) re-inserts deep-cloned job snapshots — used by `PresetDeleteUndoService` so undoing a preset delete also restores the auto-disabled jobs.

### 8. Run history
- `Services/JobHistoryService.cs` — layout `job-history/{jobId}/index.json` + `{runId}.json` payloads (newest-first index). Auto-wired: `SubscribeTo(executionService, retentionResolver)` hooks `JobCompleted` (`:36-47`; Form1.cs:15071).
- **Retention** (`EnforceRetention`, `:445-474`): age-based then count-based pruning, applied on every save. Policy = per-job overrides (`MaxHistoryRuns`/`HistoryRetentionDays`) → config (`DefaultMaxHistoryRuns`/`DefaultHistoryRetentionDays`/`MaxJobOutputCharsPerHost`, `Models/AppConfiguration.cs:140-158`) → constants 50 runs / 30 days / 1 MiB per host (`SchedulerHistoryPolicyResolver.cs`, `JobHistoryRetentionOptions.cs`).
- **Output truncation** with an explicit `[... output truncated: N characters removed ...]` marker (`:384-396`).
- **Consecutive-failure collapse** (`TryCollapseLatestFailure`, `:237-312`): an identical failure (same host counts + normalized error message + per-host failure signature) updates the latest record in place and bumps `ConsecutiveFailureCount` instead of appending — prevents history flooding from a repeatedly failing cron job.
- **Skipped-run records**: `SaveSkippedRun` (single) and `SaveSkippedRunSummary` (aggregated window with `SkippedRunCount`, `SkippedWindowStart/EndUtc`) (`:99-151`). Production uses only the summary variant.
- **Query/search**: `GetRunsForJob` with `JobRunFilter` (success/from/to/max default 50, `:499-518`); `LoadRunPayload` (null on corrupt, `:524-540`); `SearchRunOutput` case-insensitive across output + host address (`:546-558`). Deletion: per-run, per-job (whole directory), `GetJobIds` enumerates directories (`:568-618`). Corrupt index → timestamped `.corrupt_yyyyMMddHHmmss` rename + empty (`:409-439`).
- **Viewer**: `RunOutputViewerDialog.cs` — per-host output, search box, Copy All. Skipped-summary entries have no payload to view (guard in `JobListDialog.cs:1399-1406`).
- `sethistorylabel` script command surfaces as `JobHostOutput.Label`/`LabelReplacesAddress` (`Models/JobHostOutput.cs:33-43`).

### 9. Missed-run (downtime) detection
- `Form1.cs:3946` records `LastAppShutdownUtc` to config on shutdown. On startup (only when the instance owns the scheduler lock), `RecordMissedSchedulerRunsOnStartup` (`Form1.cs:15258-15283`) calls `SchedulingService.DetectMissedRunSummaries` (`SchedulingService.cs:148-181` — enabled + Recurring + non-empty cron only) and writes one aggregated skipped-summary history entry per affected job. Missed runs are **never auto-executed** (by design, `Models/SkippedRunEntry.cs:5-7`).
- History rows render skipped entries orange with "Skipped (N runs)" text (`JobListDialog.cs:690-693, 914-921`).

### 10. Job management UI
- **Entry points**: "Scheduler" menu item inserted before Help + clickable status-bar segment (`Form1.cs:15110-15147`); status bar shows "Scheduler: N active -- Next: <job> in <countdown>" refreshed every 5 s (`UpdateSchedulerStatusBar`, `Form1.cs:15200-15245`; formatting in `SchedulerNotificationFormatter.FormatStatusBar/FormatTimeRemaining`). Hidden when zero active jobs.
- **JobListDialog** (modeless single-instance via `ModelessDialogManager`): jobs grid (Name, Enabled checkbox — directly toggleable, Schedule description, Next Run, Last Result, Target with `[F]`/`[Custom]` markers; running jobs green, disabled jobs dimmed; `RefreshJobList`, `:491-564`) + history grid (Started, Duration, Result color-coded, Error; `RefreshHistory`, `:639-718`). Toolbar/context menu/keyboard: New, Edit (double-click), Run Now, Cancel, Enable/Disable, Delete (confirm; also deletes history, `:1369-1385`), Duplicate (`(copy)` suffix + credential copy, `:1109-1138`), Export to file/clipboard (multi-select), Import from file/clipboard, View Output, Clear History. Live refresh on `JobsChanged`/`JobStateChanged`/`JobCompleted` (`:415-489`).
- **JobEditorDialog** tabs: General (name, target type, target combo, schedule type, cron builder, one-time picker), Content (custom-preset Scintilla editor), Hosts (grid with column management + CSV import + copy-from-main + count label), Credentials (4 modes), Advanced (folder Sequential/Parallel + StopOnError, command timeout override 1–300 s, connection timeout override 5–120 s with inherited-source guidance `:1496-1551`, history overrides). Save path: `ValidateAndSave` (`:2012-2138`) delegates everything to `JobEditorValidator.ValidateAll`, persists stored credentials, captures `TargetContentHash`/`FolderPresetHashes` (legacy drift bookkeeping).

### 11. Import/export
- `Services/JobExportService.cs`: `.sshjobs` JSON file (`ExportToFile`/`ImportFromFile`) and GZip+Base64 clipboard string (`ExportToString`/`ImportFromString` via `GZipBase64Utility`). `CloneForExport` (`:141-148`) resets `CredentialMode` → InheritFromApp, clears `RunningState` and `HasDriftWarning`.
- `PrepareImport` (`:114-136`): new GUID per job, deterministic conflict renames `"Name (imported)"`, `"Name (imported 2)"`….
- UI flow (`JobListDialog.ProcessImportedJobs`, `:1420-1463`): missing preset/folder targets flagged; `ImportPreviewDialog` lets the user check/uncheck entries and shows conflict/missing-target columns; `CommitImportedEntries` (`:1465-1495`) auto-disables missing-target jobs with reasons via `SchedulerJobIntegrityUtilities.ApplyMissingTargetImportState`, collects per-job failures into a summary message.

### 12. Failure handling & reference integrity
- Per-job execution exceptions → `JobStateChanged(Failed)` + synthetic `JobCompleted` result with 0/0 host counts (`OnJobFailed`, `JobExecutionService.cs:909-927`); cancellation → `Cancelled` + "Cancelled by user." result (`OnJobCancelled`, `:929-946`). Partial host failure → overall `Failed` with up to **3** host error messages joined (`:447-453`).
- Preset/folder lifecycle integrity (`Services/PresetManager.cs`): renaming a preset rewrites `TargetName` on referencing jobs (`:187-196`); deleting a preset/folder auto-disables referencing jobs with `DisabledReason` "Preset/Folder '…' was deleted" (`:715-733`, `:1089-1101`); `PresetDeleteUndoService` restores job snapshots on undo. Re-enabling a job in the UI clears `DisabledReason` (`JobListDialog.cs:1279-1281`).

### 13. Notifications hookup
- `JobExecutionService.NotificationService` (`:70-75`) is threaded into the per-run `SshExecutionService` (`:410`) → `ScriptContext` (`Services/SshExecutionService.cs:1773`) — i.e. it only powers the **`notify` YAML script command** inside job scripts. Wired in `Form1.cs:15067` and kept in sync when notification settings change (`Form1.cs:1432-1451`).
- `SchedulerNotificationFormatter.FormatCompletion`/`FormatStateChange` (`Utilities/SchedulerNotificationFormatter.cs:16-65`) define `[Scheduled:]`/`[Run Now:]`/`[Skipped:]` message formats — **only referenced by tests** (see gaps).

---

## Integration points

- **Events out**: `JobExecutionService.JobStateChanged` (Queued/Started/Completed/Failed/Cancelled/Skipped + message) and `JobCompleted` (`JobRunResult`) → Form1 status bar (`Form1.cs:15250-15304`), JobListDialog refresh, `JobHistoryService.SaveRun`. `JobStorageService.JobsChanged` → status bar + dialog refresh. Events are raised on ThreadPool threads; UI handlers marshal via `InvokeRequired`/`BeginInvoke`.
- **ConfigurationService**: `MaxConcurrentJobs`, `DefaultMaxHistoryRuns`, `DefaultHistoryRetentionDays`, `MaxJobOutputCharsPerHost`, `LastAppShutdownUtc` (`Models/AppConfiguration.cs:134-158`); retention resolved per job via `SchedulerHistoryPolicyResolver` in `Form1.ResolveSchedulerHistoryRetention` (`:15285-15294`).
- **PresetManager** ↔ **JobStorageService**: circular dependency broken by `SetJobStorageService` setter (`PresetManager.cs:41-44`; wired `Form1.cs:15085`); rename/delete integrity above; `PresetDeleteUndoService` ↔ `RestoreSnapshots`.
- **SshExecutionService**: a fresh instance per job run with `UseConnectionPooling=false` to avoid clashing with the UI's pool (`JobExecutionService.cs:402-404`); `allowFileSelectionDialogs:false` blocks interactive file dialogs in headless runs (`:687,744`). Same execution engine as manual runs → YAML scripting engine, per-host variables, `sethistorylabel`.
- **Vault**: `VaultCredentialProvider` injected post-construction; reset to null on Vault disable and recreated on environment/Vault changes (`Form1.cs:1384-1424`); per-row `vault_path` and job-level `VaultCredentialPath` both honored; environment switch updates `EnvironmentVaultProfile` (`Form1.cs:1902-1903`).
- **NotificationService** (`Services/Notifications/`): scheduler passes through to script `notify` command only.
- **Credential Manager**: `CredentialTargets.JobPasswordTarget(jobId)` per-job secrets; `DefaultPasswordTarget` for InheritFromApp.
- **Cross-instance**: `SchedulerInstanceLock` named mutex; second instance keeps full CRUD UI but no timer.
- **Tests**: extensive suites under `SSH_Helper.Tests/Services/` (execution, storage, history, export, scheduling, policy resolver) and `SSH_Helper.Tests/UI/` (job editor variants, run-now, scheduler notification formats).

---

## Observed gaps & quirks

### Correctness
1. **Cron timezone inconsistency (display vs execution)** — `GetNextRunLocal` interprets the cron in **local** time (`SchedulingService.cs:61`, passes `TimeZoneInfo.Local` to Cronos) and feeds the status bar countdown (`Form1.cs:15230`) and Job List "Next Run" column (`JobListDialog.cs:741`). But the actual due-check uses `GetMissedOccurrences` (`SchedulingService.cs:97`) with the zone-less Cronos overload — **UTC** interpretation — via `IsJobDue` (`JobExecutionService.cs:1028`). Missed-run detection is also UTC. On any machine not running UTC, a "Daily 9 AM" job displays next-run 9 AM local but fires at 9 AM UTC. The two paths disagree by the UTC offset.
2. **jobs.json written non-atomically** — `JobStorageService.PersistToDisk` (`:243`) uses plain `File.WriteAllText` after a best-effort `.bak` copy, violating the project's own `JsonFileWriter.WriteJsonAtomic` convention (CLAUDE.md) that the history service follows. Exposure is amplified because jobs.json is rewritten **twice per run** for `RunningState` (`JobExecutionService.cs:772-773`, `:830-831`).
3. **Thread-safety of `_jobs` dictionary** — `JobStorageService` is not synchronized: `Save` mutates `Dictionary<string,JobDefinition>` from ThreadPool threads (TryStartJob/CompleteJob/HandlePostExecution) while the UI thread enumerates `Jobs.Values` (status-bar 5 s timer `Form1.cs:15205,15224`; dialog refresh). Single-writer/concurrent-reader `Dictionary` access is not safe in .NET.
4. **Plaintext per-host passwords persist and export** — PerHostColumn mode stores `password` column values in cleartext inside jobs.json (`JobDefinition.Hosts`), and `CloneForExport` (`JobExportService.cs:141-148`) only resets `CredentialMode`; it does **not** strip `password`/`username` values from `Hosts` rows, despite the "Credentials are stripped" doc comments (`:32-34,50-52`). A shared `.sshjobs` file or clipboard blob can leak host credentials.
5. **Overdue one-time jobs silently fire at startup** — missed-run detection covers Recurring only (`SchedulingService.cs:120,159`), while `IsJobDue` treats any past `OneTimeScheduleUtc` as due (`JobExecutionService.cs:1030-1031`). A one-time job whose moment passed during downtime executes immediately on next launch with no skip record or confirmation — inconsistent with the "missed runs are never auto-executed" policy for recurring jobs.
6. **Crashed runs leave no history** — `Initialize()` raises only a transient `JobStateChanged(Failed)` for orphaned `RunningState` (`JobExecutionService.cs:153-168`); no `JobCompleted` fires, so nothing is written to run history and the Job List "Last Result" never shows the crash.

### Notification / UX dead-ends
7. **Scheduler completion notifications are unwired** — `SchedulerNotificationFormatter.FormatCompletion`/`FormatStateChange` are referenced **only by tests** (grep: production callers limited to `FormatStatusBar`/`FormatTimeRemaining`). Form1's `OnSchedulerJobCompleted`/`OnSchedulerJobStateChanged` (`:15250-15304`) merely refresh the status bar, despite the wiring comment "Subscribe to execution events for output panel notifications" (`Form1.cs:15073`). Consequently: no output-panel line, no toast, no SMTP/Teams/webhook when a scheduled job fails — the entire `NotificationSettings` machinery is bypassed for job outcomes. The `_runNowJobIds` set (`Form1.cs:167,15309`) exists solely to choose the `[Run Now:]` prefix that is never rendered.
8. **No catch-up affordance for missed runs** — downtime summaries are written to history only (`Form1.cs:15276-15282`); the user is never prompted ("N runs missed — run now?") nor shown a startup banner.
9. **Generic import failure messaging** — `ImportFromFile`/`ImportFromString` swallow all exceptions and return an empty list (`JobExportService.cs:82-86,103-106`), so a corrupt/wrong-format file surfaces as "No valid jobs found in the import data" (`JobListDialog.cs:1424`) — indistinguishable from a genuinely empty export.
10. **Single-job operations on a multi-select grid** — export supports multi-select (`GetSelectedJobs`, `JobListDialog.cs:783`) but Delete/Enable-Disable/Run Now operate only on the single active job (`:1096-1107,1369-1385`). Deleting a job silently destroys its entire history with one generic confirm (`:1382-1383`); no undo (unlike preset deletion, which has `PresetDeleteUndoService`).

### Configuration / policy quirks
11. **Hidden config knobs** — `MaxConcurrentJobs`, `DefaultMaxHistoryRuns`, `DefaultHistoryRetentionDays`, `MaxJobOutputCharsPerHost` have no SettingsDialog UI (grep of `SettingsDialog.cs` returns nothing); config.json hand-edit only. `MaxConcurrentJobs` is additionally read once at service construction (`JobExecutionService.cs:140-142`) — changes require an app restart.
12. **Run Now is unbounded** — by locked decision it bypasses the semaphore (`:236`), so N parallel Run Now invocations can exceed `MaxConcurrentJobs` entirely.
13. **Folder jobs ignore per-preset timeout at the job level** — `BuildTimeouts` (`:660-672`) resolves `_presetManager.Get(job.TargetName)`, which is a folder path for Folder targets → null → `config.Timeout`. Whether `ExecuteFolderAsync` re-applies per-preset `Timeout` internally is not visible from this service; per-job override is the only reliable knob.
14. **Failure collapse rewrites StartedUtc** — `TryCollapseLatestFailure` overwrites the collapsed record's `StartedUtc/CompletedUtc` with the latest attempt (`JobHistoryService.cs:263-264`), losing the first-failure timestamp; only `ConsecutiveFailureCount` hints at streak length.
15. **`JobRunFilter` doc vs behavior** — comments say "runs that **started** at or after" (`Models/JobRunFilter.cs:14-19`) but filtering compares `CompletedUtc` (`JobHistoryService.cs:509-513`).

### Dead/legacy weight
16. **Legacy drift machinery still being written** — `HasDriftWarning` is documented as legacy (`JobDefinition.cs:199-201`), yet `JobEditorDialog.ValidateAndSave` still computes `TargetContentHash`/`FolderPresetHashes` on every save (`:2076-2099`) and resets `HasDriftWarning` (`:2133`); nothing consumes the hashes at execution time.
17. **Stale phase-plan comments** — "placeholder for Phase 3" on `CronExpression`/`OneTimeScheduleUtc` (`JobDefinition.cs:184-190`), "Does NOT run a timer (that is Phase 3)" (`SchedulingService.cs:9-11`), "Plan 03-03 will preserve this call" (`JobExecutionService.cs:868`) — all describe long-completed work.
18. **`SaveSkippedRun` (singular) unused in production** — only the summary variant is called (`Form1.cs:15279`); the per-occurrence API (`JobHistoryService.cs:99-123`) and `SkippedRunEntry.DetectedUtc` are test-only.

### Minor
19. **Per-refresh disk I/O** — `GetLastResultText` reads each job's index.json on every job-list refresh (`JobListDialog.cs:755-763`), and refreshes fire on every `JobStateChanged`; O(jobs) file reads per event.
20. **CSV parsing is line-based** — `ImportHostsFromCsv` uses `File.ReadAllLines` + per-line `CsvManager.ParseCsvLine` (`JobStorageService.cs:263-285`); quoted multi-line CSV fields will mis-parse. Rows with more values than headers silently drop extras.
21. **Hardcoded values** — 30 s evaluation period (`JobExecutionService.cs:181`), 5 s status-bar refresh (`Form1.cs:15143`), 1000 ms disposal wait (`:1104`), 3-error-message cap in result summary (`:452`), 50-result query default (`JobRunFilter.cs:27`).
22. **Credential validation loophole** — `ExecuteJobCoreAsync` only rejects when username AND password are *both* empty (`:388-389`); a Stored-mode job whose secret was deleted from Credential Manager but whose username field survives elsewhere can proceed and fail per-host instead of failing fast with a clear message.
