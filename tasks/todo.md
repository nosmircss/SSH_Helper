# TODO

## 45. Autosave Dirty Grid On Environment Switch
- [x] 45.1 Replace the dirty host-grid environment-switch prompt with automatic save-to-environment behavior.
- [x] 45.2 Verify all environment-switch entry points still complete cleanly after autosave.
- [x] 45.3 Capture the implementation and verification notes in the review section below.

### 45 Review
- Removed the dirty host-grid confirmation from the environment-switch path and kept the existing save-to-environment snapshot behavior unconditional inside `TrySwitchEnvironment(...)`.
- Simplified the related folder-selection and preset-driven switch callers by dropping the now-unused `promptIfDirty` plumbing from `TrySwitchEnvironment(...)` and `TryApplyFolderEnvironment(...)`.
- Verified the remaining switch entry points still compile and route through the same shared switch helper: toolbar environment changes, Manage Environments selection changes, folder base-environment application, folder selection, and preset-driven environment restore/switch.
- Verification: `dotnet build .\\SSH_Helper.csproj -p:BaseOutputPath=artifacts\\env-switch-autosave-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\env-switch-autosave-build\\obj\\` passed with 0 warnings and 0 errors.

## 43. Investigate CSV Save Exit Hang
- [x] 43.1 Trace the normal exit path in `Form1` and identify all conditions that can cancel shutdown.
- [x] 43.2 Trace CSV save/save-as flows and any dialog interactions that can leave the form in a state where exit requests are ignored.
- [x] 43.3 Verify the most plausible failure mode against related event handlers/background work and capture the findings below.

### 43 Review
- Both `File -> Exit` and the window close button funnel through `ConfirmExitWorkflow()` (`Form1_FormClosing` for X, `ExitMenuItem_Click` for the menu). That method cancels shutdown whenever execution is running and the user declines to stop, whenever the dirty-CSV prompt returns `Cancel`, whenever dirty-CSV save returns `false`, or whenever dirty-preset resolution returns `false`.
- The most plausible “exit does nothing but app stays responsive” path is the dirty-CSV save branch: `ConfirmExitWorkflow()` calls `SaveCurrentCsv(promptIfNoPath: true)`, which returns `false` if the user answers `Yes` to save but then cancels `Save As`, or if saving throws and the error path returns `false`. In that case `ConfirmExitWorkflow()` returns `false`, `FormClosing` sets `e.Cancel = true`, and both exit routes appear to do nothing.
- `SaveCurrentCsv(...)` makes that behavior easy to hit because the no-path branch calls `SaveCsvAs()` and infers success only from whether `_loadedFilePath` ended up non-empty after the dialog. There is no follow-up status explaining that the close was canceled because the save dialog was canceled.
- A second close-cancel path still exists even after CSV save succeeds: if `IsPresetDirty()` is true, `TryResolvePendingPresetChanges()` can also veto shutdown. That means a user can associate the issue with the CSV prompt even though the actual final cancellation came from unsaved preset changes.
- I did not find a stronger hard-lock path in the main-form shutdown flow. This looks like repeated close cancellation rather than the app getting stuck in an unresponsive state.
- Verification: source review only; no code changes or UI automation run for this investigation.

## 44. Patch CSV Exit Cancellation UX
- [x] 44.1 Refactor the CSV save/save-as path so close handling can distinguish save success, save cancellation, and save failure.
- [x] 44.2 Update the exit workflow to offer exit-without-saving when the CSV save attempt is canceled or fails, instead of silently canceling shutdown.
- [x] 44.3 Verify the patch builds cleanly and capture the review below.

### 44 Review
- Added a small `CsvSaveAttemptResult` flow in `Form1` so CSV save/save-as now distinguishes successful save, canceled save dialog, and failed save instead of collapsing everything to `true`/`false`.
- `SaveCsvAs()` now uses an owned `SaveFileDialog` (`ShowDialog(this)`) and both save paths share one `TrySaveCsvToPath(...)` method that updates `_loadedFilePath`, fingerprint, status bar, and save-error messaging consistently.
- `ConfirmExitWorkflow()` now routes CSV handling through `TryResolvePendingCsvChangesForExit()`. If the user says `Yes` to save but then cancels `Save As`, or if saving fails, the app now asks whether to exit without saving instead of silently canceling the close.
- Verification: `dotnet build .\\SSH_Helper.csproj -p:BaseOutputPath=artifacts\\csv-exit-fix\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\csv-exit-fix\\obj\\` passed with 0 warnings and 0 errors.
- Automated tests were not added for this patch because the affected behavior is inside the WinForms main-form dialog workflow; verification here was compile-only.

## 42. Rebase Branch Onto origin/master
- [x] 42.1 Confirm the current branch/worktree state before rebasing.
- [x] 42.2 Fetch the latest `origin/master`.
- [x] 42.3 Rebase the current branch onto `origin/master` and capture whether conflicts were encountered.

### 42 Review
- Confirmed the current branch was `0.51.8` and the worktree was clean before I wrote the task plan.
- Fetched the latest `origin/master`.
- Temporarily stashed the local `tasks/todo.md` planning edit, rebased `0.51.8` onto `origin/master`, and restored the stash afterward.
- The rebase completed successfully with no conflicts.

## 41. Review Connection Pooling Feature
- [x] 41.1 Trace the UI/config toggle and runtime execution paths that enable or bypass SSH connection pooling.
- [x] 41.2 Inspect the pool lifecycle, health-check, keep-alive, and session-leasing behavior plus any focused specs/tests.
- [x] 41.3 Deliver a concise review of concrete benefits, drawbacks, and implementation-specific risks below.

### 41 Review
- The settings/UI wiring is straightforward: the checkbox in `SettingsDialog` persists `UseConnectionPooling`, `Form1` keeps a long-lived `SshExecutionService` with an internal pool, and manual runs switch between pooled and non-pooled execution by checking `UseConnectionPooling`.
- Real benefits in this implementation are limited to repeated manual UI runs against the same `host:port:username` within one app session: pooled execution skips reconnect/login work, preserves timeout/algorithm/UTF-8 parity with non-pooled execution, and leases a host key so one pooled connection is not shared concurrently.
- The feature is narrower than the label suggests: scheduler jobs create a fresh `SshExecutionService` and force `UseConnectionPooling = false`, so scheduled runs and `Run Now` job execution do not benefit from this toggle at all.
- Operational drawbacks: pooled connections stay alive via a background timer/SSH keepalive sweep, active reuse can issue a real `echo 1` shell command as a health check, and disabling the setting only stops future reuse; it does not immediately clear already pooled connections.
- Implementation risk: when a same-host pooled connection is already leased, `CreateSessionAsync(...)` falls back to a standalone SSH client, but the pooled execution callers only dispose the `SshShellSession` and release the lease. I do not see an explicit `client.Dispose()`/`Disconnect()` path for that fallback client, so concurrent same-host pooled runs appear capable of leaking standalone SSH connections.
- Coverage gap: I did not find direct unit/integration tests for `SshConnectionPool` behavior or the pooled execution branches. Current tests only cover persisting the `UseConnectionPooling` flag inside execution-details/history metadata.
- Verification: source review only; no build or test run was needed for this analysis task.

## 40. Fix Scheduler Retry, Import Naming, and Per-Host Validation
- [x] 40.1 De-duplicate queued scheduled jobs and correct one-time failure handling so scheduled one-time jobs do not requeue or auto-retry after a failed scheduled attempt.
- [x] 40.2 Implement deterministic import conflict naming with `(imported)`, `(imported 2)`, etc., and surface partial import save failures in the completion message.
- [x] 40.3 Tighten per-host credential validation so every populated host row requires non-blank `username` and `password` values in per-host mode.
- [x] 40.4 Add focused regression coverage for scheduler queueing/one-time behavior, import naming and failure reporting, and per-host validation.
- [x] 40.5 Run focused verification and capture the review outcome below.

### 40 Review
- `JobExecutionService` now tracks queued job IDs to prevent duplicate pending entries, skips re-queueing jobs that are already waiting, clears that tracking on dequeue, and auto-disables failed scheduled one-time jobs with `DisabledReason = "One-time schedule failed"` while preserving manual `Run Now` behavior.
- `JobExportService.PrepareImport(...)` now reserves names across the full import batch and resolves conflicts deterministically as `Name (imported)`, `Name (imported 2)`, `Name (imported 3)`, etc. `JobListDialog` now records per-entry save failures and reports them in the import completion message instead of silently swallowing them.
- `JobEditorValidator.ValidateAll(...)` now accepts host-column input, enforces per-host `username` and `password` columns case-insensitively, and blocks save on the first populated row missing either value. `JobExecutionService.BuildHostConnections(...)` now reads those per-host credential fields case-insensitively at runtime so validation and execution match.
- Added focused regression coverage in `JobExecutionServiceTests`, `JobExportServiceTests`, `JobEditorValidationTests`, and `JobListDialogRunNowTests` for the new scheduler, import, and per-host validation behavior.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj -p:BaseOutputPath=artifacts\\test-output\\ -p:BaseIntermediateOutputPath=artifacts\\test-obj\\ --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobExportServiceTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~JobListDialogRunNowTests"` passed (101/101).
- Verification: `dotnet build .\\SSH_Helper.csproj` passed.

## 39. Review UI Diff Since 3937c252
- [x] 39.1 Collect the UI/interaction diff for the requested dialogs, control, and related UI utilities since `3937c2522f7b2eb12931594746d1bd7754da48ed`.
- [x] 39.2 Inspect the changed behavior in `JobEditorDialog`, `JobListDialog`, `ImportPreviewDialog`, `RunOutputViewerDialog`, `UI/CronBuilderControl`, `UI/UnsavedPresetDiffDialog`, and any directly related UI helpers.
- [x] 39.3 Consult targeted tests only if needed to confirm expected behavior, then record concrete bugs, regressions, and worthwhile enhancements below.

### 39 Review
- Review scope stayed limited to the requested UI/interaction files plus directly related helpers: `JobEditorDialog`, `JobListDialog`, `ImportPreviewDialog`, `RunOutputViewerDialog`, `UI/CronBuilderControl`, `UI/UnsavedPresetDiffDialog`, `Utilities/JobEditorValidator`, `Utilities/HostGridUtilities`, `Utilities/ModelessDialogManager`, `Utilities/PresetSaveImpactResolver`, and `Utilities/SchedulerNotificationFormatter`.
- Confirmed four concrete issues worth raising: stored-credential duplication produces a new job with no matching saved secret, per-host credential mode is not validated despite the UI promising required columns, clear-history leaves the jobs list's `Last Result` stale until a later refresh, and import save failures are silently swallowed after the preview step.
- Reviewed targeted WinForms/unit coverage only where it clarified intent (`JobListDialogRunNowTests`, `JobEditorValidationTests`, `JobEditorDialogStoredCredentialTests`, `UnsavedPresetDiffDialogTests`, `CronBuilderControl*Tests`, `HostGridUtilitiesTests`, `ModelessDialogManagerTests`). Those tests do not currently cover the four issues above.

## 38. Review Scoped Storage Export Integrity Diff
- [ ] 38.1 Inspect the scoped git diff for the targeted storage, export, preset-integrity, model, and credential-target files since `3937c2522f7b2eb12931594746d1bd7754da48ed`.
- [ ] 38.2 Check only relevant tests as supporting evidence for the reviewed behaviors.
- [ ] 38.3 Deliver prioritized findings with concrete file/line references, plus up to two worthwhile enhancements, and capture the review below.

## 39. Review Scheduler Runtime Diff
- [x] 39.1 Inspect the git diff since `3937c2522f7b2eb12931594746d1bd7754da48ed` for `SchedulingService`, `JobExecutionService`, `JobHistoryService`, `HistoryStorageService`, `SchedulerHistoryPolicyResolver`, and `Form1` scheduler wiring.
- [x] 39.2 Verify related models/utilities only where needed to confirm behavior, edge cases, and line-accurate findings.
- [x] 39.3 Deliver concrete review findings with severity ordering, file/line references, and up to two worthwhile enhancements.

### 39 Review
- Reviewed the scoped diff in the scheduler runtime/history path plus directly implicated supporting types (`JobDefinition`, run-history models, `JsonFileWriter`, `JobStorageService`, status-bar wiring, and cron UI consumption points).
- Main findings: recurring cron execution currently evaluates against UTC overloads while the UI surfaces local next-run times; failed/cancelled one-time jobs are left eligible and will re-trigger every evaluation cycle; startup missed-run handling can both over-count downtime after crashes and double-handle occurrences that land between service construction and the first timer tick.
- Additional execution risks: concurrent scheduler threads persist `RunningState` through unsynchronized `JobStorageService.Save(...)` calls, shutdown disposal can race with background semaphore release, and the per-job cancellation token created in `TryStartJob(...)` is never passed into execution so `CancelJob(...)` does not stop a running job.
- No material regression stood out in the `HistoryStorageService` refactor itself; the risky behavior in this range is concentrated in scheduling/execution startup and concurrency handling rather than the extracted atomic JSON writer.
- Verification: source review only; no tests were run for this review task.

## 37. Restore Unified Preset Save Diff
- [x] 37.1 Refactor the preset save confirmation UI so the diff dialog can also show optional scheduled-job impact details and rename/create-new actions.
- [x] 37.2 Route `Form1` preset-save confirmation flows through the unified dialog while preserving no-op saves and non-impact save behavior.
- [x] 37.3 Update OpenSpec/task artifacts and focused WinForms coverage for combined diff-plus-impact behavior, collapsed affected-job listing, and rename-choice flows.
- [x] 37.4 Run focused verification, clean build, OpenSpec validation, and capture the review outcome below.

### 37 Review
- `UnsavedPresetDiffDialog` now serves as the single preset-save confirmation surface: it preserves the existing diff-first review layout, adds an optional scheduled-impact header, and keeps the affected-job list behind a collapsed toggle so the diff remains dominant.
- `Form1.ShowPresetSavePrompt(...)` now routes referenced preset saves, rename-vs-create decisions, and unsaved-change confirmations for existing presets through that unified dialog instead of splitting between the old diff dialog, the impact-only dialog, and a rename message box.
- Referenced rename flows keep the one-dialog behavior while clarifying that `Rename Existing` carries scheduled jobs forward and `Create New` saves a separate preset; non-impacted dirty saves still retain the diff prompt without showing scheduler impact controls.
- Retired the dedicated `PresetSaveImpactDialog` implementation and replaced its coverage with unified-dialog WinForms tests for impact summary visibility, collapsed/expanded affected-job lists, rename-choice buttons, and the non-impacted diff regression.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~UnsavedPresetDiffDialogTests|FullyQualifiedName~PresetSaveImpactResolverTests"` passed (7/7).
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~UnsavedPresetDiffDialogTests|FullyQualifiedName~PresetSaveImpactResolverTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~PresetManagerJobReferenceTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~JobEditorDialogStoredCredentialTests" -p:BaseOutputPath=artifacts\\preset-save-unified-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\preset-save-unified-tests\\obj\\` passed (75/75).
- Verification: `dotnet build .\\SSH_Helper.sln` passed.
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\preset-save-unified-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\preset-save-unified-build\\obj\\` passed.
- Verification: `openspec validate replace-scheduler-drift-with-save-warning --strict --no-interactive` passed.

## 36. Replace Scheduler Drift With Save-Time Warning
- [x] 36.1 Add OpenSpec change artifacts for replacing scheduler drift blocking with a preset save-time warning.
- [x] 36.2 Add preset save impact resolution and a single save confirmation dialog for referenced preset saves, including rename-vs-create-new handling without stacked popups.
- [x] 36.3 Remove drift reevaluation, UI indicators, and execution blocking while keeping legacy drift fields file-compatible.
- [x] 36.4 Add focused tests for preset save impact resolution, referenced-save dialog flows, and legacy `HasDriftWarning` execution behavior.
- [x] 36.5 Run focused verification, clean build, OpenSpec validation, and capture the review outcome below.

### 36 Review
- `Form1` now routes referenced preset saves through `PresetSaveImpactResolver` plus the new `PresetSaveImpactDialog`, so users see one save-time confirmation with affected scheduled job names instead of discovering drift later in the scheduler UI.
- Referenced-save prompts cover direct preset jobs and folder jobs targeting the preset's current folder, sort those jobs by name, and de-duplicate by job ID before display.
- Direct save, unsaved-change save, and referenced rename flows now share the same warning surface without a follow-up drift acknowledgement step; unreferenced saves continue using the existing lightweight flows.
- `PresetManager` no longer reevaluates or writes drift state when presets or folders change, `JobListDialog` no longer renders `[DRIFT]` or drift-colored rows, and `JobExecutionService` no longer blocks scheduled or Run Now execution on legacy `HasDriftWarning`.
- Legacy scheduler compatibility stays intact: job JSON still carries `TargetContentHash`, `FolderPresetHashes`, and `HasDriftWarning`, and job save/export paths normalize `HasDriftWarning` to `false` without using it as active runtime behavior.
- Added focused coverage for preset save impact resolution, the new save confirmation dialog modes, `PresetManager` no-longer-recomputes behavior, `SchedulerJobIntegrityUtilities` remaining helpers, and legacy-drift execution through both Run Now and scheduler evaluation.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetSaveImpactResolverTests|FullyQualifiedName~PresetSaveImpactDialogTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~PresetManagerJobReferenceTests|FullyQualifiedName~SchedulerJobIntegrityUtilitiesTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~JobEditorDialogStoredCredentialTests"` passed (77/77).
- Verification: `dotnet build .\\SSH_Helper.sln` passed.
- Verification: `openspec validate replace-scheduler-drift-with-save-warning --strict --no-interactive` passed.

## 35. Audit Scheduler Drift Touchpoints
- [x] 35.1 Identify production code paths and symbols for scheduler drift state, target-hash drift detection, UI banners/indicators, and save/run blocking.
- [x] 35.2 Identify scheduler drift test coverage and relevant OpenSpec references.
- [x] 35.3 Summarize dependencies that remain if drift indicators/blocking are removed but preset-save warnings are introduced.
- [x] 35.4 Capture the audit review below.

### 35 Review
- Drift state is modeled on `JobDefinition` via `TargetContentHash`, `FolderPresetHashes`, and `HasDriftWarning`; preset/folder mutations flow through `PresetManager.ReevaluateAffectedJobDriftStates(...)`, which delegates comparison to `SchedulerJobIntegrityUtilities.IsDrifted(...)` and persists changed flags through `JobStorageService`.
- UI drift touchpoints are limited to `JobEditorDialog` (banner visibility, acknowledge action, save-time snapshot recompute and drift clear) and `JobListDialog` (name suffix/color indicator plus generic Run Now warning when service-level blocking returns false).
- Execution blocking lives only in `JobExecutionService`: `RunNowAsync(...)` returns false and emits `Skipped` when `HasDriftWarning` is set, and the recurring evaluation loop silently skips drifted jobs.
- Export/import integrity touchpoints are `JobExportService.CloneForExport(...)` clearing `HasDriftWarning` while preserving target hashes and `SchedulerJobIntegrityUtilities.ApplyMissingTargetImportState(...)` disabling missing-target imports with explicit reasons.
- Test coverage exists for model fields/defaults, hash utility behavior, reference lookups, drift activation in `PresetManager`, service-level run blocking, and export stripping. No direct automated coverage exists for the `JobEditorDialog` drift banner/acknowledge flow or the `JobListDialog` `[DRIFT]` indicator/warning dialog.
- If drift indicators/blocking are removed and preset-save warnings are added, the minimal surviving backend is the preset-save entry point plus reference lookup (`Form1.SaveCurrentPreset`, `PresetManager.GetJobsReferencingPreset/GetJobsReferencingFolder`, `JobStorageService` queries). Saved hashes and `SchedulerJobIntegrityUtilities.IsDrifted(...)` remain necessary only if the new warning should be content-aware or limited to actual snapshot changes rather than warning on every referenced preset save.

## 34. Collapse Consecutive Identical Scheduler Failures
- [x] 34.1 Extend job-history persistence so the newest matching failed run for a job is updated with an incrementing repeat counter instead of adding another row.
- [x] 34.2 Surface collapsed failure counts in the scheduler history UI and last-result column without changing success or skipped-run behavior.
- [x] 34.3 Add focused service and WinForms regression coverage for repeated-failure collapse and reset behavior.
- [x] 34.4 Run focused and full verification, then capture the review outcome below.

### 34 Review
- `JobHistoryService` now collapses only the newest consecutive identical failure for a job: same failure counts, same top-level error text, same per-host success/error signature, not skipped, and still failure-only.
- Collapsed failures keep a single history row/payload file, overwrite that payload with the latest run details, and increment a persisted `ConsecutiveFailureCount` on both the index record and payload so the count survives refresh and restart.
- `JobListDialog` now renders collapsed failures as `FAIL xN (...)` in both the run-history grid and the jobs list `Last Result` column while leaving success and skipped summary formatting unchanged.
- Added service coverage for collapse, no-collapse on different failures, and no-collapse after a success resets the streak, plus a WinForms regression that verifies two identical failures render as one `FAIL x2` history row.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobListDialogRunNowTests"` passed (40/40).
- Verification: `dotnet build .\\SSH_Helper.sln` passed.
- Manual interactive UI verification was not run from this CLI environment.

## 33. Fix Cron Builder Dialog Clipping
- [x] 33.1 Replace fixed cron-builder height assumptions with measured responsive layout inside `CronBuilderControl`.
- [x] 33.2 Make `JobEditorDialog` size the recurring schedule host panel from the cron builder's computed height.
- [x] 33.3 Add WinForms regression coverage for cron control layout and New Job recurring-panel visibility.
- [x] 33.4 Run focused and full verification, then capture the review outcome below.

### 33 Review
- `CronBuilderControl` now remeasures its preset flow panel, dropdown row, raw expression row, and status labels whenever content, width, or font-related layout changes occur, then updates its own `Height`, `MinimumSize`, and `AutoScrollMinSize` from the actual visible content bottom instead of fixed constants.
- The preset button area no longer assumes a fixed two-row `64` px slot, so narrower widths or larger fonts can wrap buttons without hiding the fields and expression controls below.
- `JobEditorDialog` now syncs `_panelCron.Height` to the embedded cron builder's computed height and refreshes that sizing on dialog/tab resize, cron-builder size changes, schedule-mode switches, prepopulation, and post-theme initialization.
- Added WinForms regressions covering both the cron control's wrapped preset layout and the New Job dialog's recurring schedule section at the current default window size.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CronBuilderControl|FullyQualifiedName~JobEditorDialog"` passed (41/41).
- Verification: `dotnet build .\\SSH_Helper.sln` passed.
- Manual interactive UI verification was not run from this CLI environment.


## 32. Collapse Scheduler Downtime Misses Into One Summary Entry
- [x] 32.1 Add a scheduling summary model/path that groups missed recurring runs by job for a startup downtime window.
- [x] 32.2 Persist one skipped summary history entry per affected job, including skipped-count and downtime-window metadata.
- [x] 32.3 Update the scheduler history UI to render summarized skipped rows compactly and block output viewing for new skipped-summary entries.
- [x] 32.4 Add focused service and WinForms regression coverage for skipped-run aggregation, rendering, and history-slot compression.
- [x] 32.5 Update the scheduler spec text and capture verification results in the review section.

### 32 Review
- `SchedulingService` now exposes `DetectMissedRunSummaries(...)`, which collapses all missed recurring occurrences for a job into one `SkippedRunSummaryEntry` with count plus first/last scheduled timestamps.
- `Form1.RecordMissedSchedulerRunsOnStartup()` now persists one skipped history summary per affected job/startup window instead of one history row per missed cron slot.
- `JobHistoryService` now persists skipped-summary metadata (`SkippedRunCount`, `SkippedWindowStartUtc`, `SkippedWindowEndUtc`) on both the index record and payload while keeping legacy single skipped rows compatible through the old `SaveSkippedRun(...)` path.
- `JobListDialog` now renders summarized skipped rows as `SKIPPED (N)`, keeps the `Started` column on the most recent missed time, shows compact downtime messages in `Error`, and disables `View Output` for the new skipped-summary entries so they do not open an empty viewer.
- Added focused coverage for summary detection, summary persistence, single-summary and multi-summary UI rendering, legacy skipped-row rendering, and the regression that a long downtime window now compresses into one history slot per job.
- Verification: `dotnet build .\\SSH_Helper.sln` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 81020).
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobListDialogRunNowTests"` failed for the same locked default `obj\\Debug\\net8.0-windows\\SSH_Helper.dll` path.
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\downtime-summary-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\downtime-summary-build\\obj\\` passed.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobListDialogRunNowTests" -p:BaseOutputPath=artifacts\\downtime-summary-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\downtime-summary-tests\\obj\\` passed (82/82).
- Verification: `openspec validate update-scheduler-runtime-history --strict --no-interactive` passed.

## 31. Scheduler Notification Output Suppression
- [x] 31.1 Confirm which scheduler event paths append lifecycle messages into the main output pane.
- [x] 31.2 Stop appending scheduler start/completion/skipped messages into the shared output pane while preserving scheduler history and status updates.
- [x] 31.3 Run focused verification and capture the review results.

### 31 Review
- Root cause: `Form1` appended scheduler lifecycle lines directly into the same output buffer used for live host command output from `OnSchedulerJobCompleted(...)`, `OnSchedulerJobStateChanged(...)`, and startup skipped-run reporting, which merged scheduler metadata into normal terminal output.
- `Form1` now keeps scheduler lifecycle updates out of the shared output pane while still persisting skipped runs and refreshing scheduler status-bar state.
- Focused verification used the existing scheduler/history/dialog test suite plus a clean solution build; there is not yet a dedicated `Form1` output-routing test harness that asserts against the live output textbox directly.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulerNotificationTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests" -p:BaseOutputPath=artifacts\\scheduler-output-suppression-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-output-suppression-tests\\obj\\` passed (61/61).
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\scheduler-output-suppression-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-output-suppression-build\\obj\\` passed.

## 30. Scheduler History Row Selection Stability
- [x] 30.1 Confirm why the run-history grid falls back to the first row after the scheduler dialog refresh timer ticks.
- [x] 30.2 Preserve the active history run selection across timer-driven and event-driven history refreshes.
- [x] 30.3 Add focused WinForms regression coverage for selecting a non-first history row before refresh.
- [x] 30.4 Run focused verification and capture the review results.

### 30 Review
- Root cause: `JobListDialog` runs a 5-second `_refreshTimer` that calls `RefreshJobList()`, which in turn rebuilds `_gridHistory` via `RefreshHistory(...)`; the old code cleared and repopulated the history rows without restoring the selected run, so WinForms fell back to the first row.
- `JobListDialog` now tracks the active history `RunFileName`, suppresses history selection churn while the grid is rebuilt, and reapplies the matching history row after timer-driven and event-driven refreshes.
- `ViewSelectedOutput()` now resolves the active history run through the preserved selection state instead of depending only on the transient current `SelectedRows` collection.
- Added a focused WinForms regression test that selects the second history row, invokes `RefreshJobList()`, and verifies the same run remains selected afterward.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialogRunNowTests" -p:BaseOutputPath=artifacts\\history-row-selection-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\history-row-selection-tests\\obj\\` passed (5/5).
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\history-row-selection-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\history-row-selection-build\\obj\\` passed.

## 23. Scheduler History Dialog Selection Stability
- [x] 23.1 Make scheduler job selection deterministic on dialog load and refresh.
- [x] 23.2 Keep history rendering bound to a stable active job ID instead of transient grid selection state.
- [x] 23.3 Add WinForms regression coverage for initial history population and post-refresh stability.
- [x] 23.4 Run focused verification with isolated build output paths and capture review results.

### 23 Review
- `JobListDialog` now uses a stable `_selectedJobId` plus deterministic fallback selection order (`previous active job -> current row -> first available row`) so the Run History pane populates immediately on dialog load and survives job-grid rebuilds.
- The jobs grid now runs as single-select, suppresses selection-change handling while rows are rebuilt, and refreshes the history pane explicitly after the active job row is restored.
- Job actions and history actions now resolve the active job through the stabilized selection path instead of depending on transient `SelectedRows` state during refresh timing.
- Added WinForms regression coverage for first-load history population without manual clicking and for preserving the active job/history after a completion-driven refresh.
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\scheduler-history-dialog-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-history-dialog-build\\obj\\` passed.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~JobHistoryServiceTests" -p:BaseOutputPath=artifacts\\scheduler-history-dialog-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-history-dialog-tests\\obj\\` passed (31/31).

## 22. Scheduler Runtime History Correctness
- [x] 22.1 Read `openspec/changes/update-scheduler-runtime-history/proposal.md`, `tasks.md`, and related scheduler/runtime code paths to confirm scope.
- [x] 22.2 Wire persisted shutdown timestamps into scheduler startup so missed recurring runs are recorded as skipped without auto-running them.
- [x] 22.3 Apply per-job scheduler history retention overrides with fallback to global defaults and output caps.
- [x] 22.4 Correct scheduler history presentation to show persisted run start time and derived duration.
- [x] 22.5 Add focused regression tests for missed-run recording, retention selection, and history timestamp display.
- [x] 22.6 Run verification, update OpenSpec task checkboxes, and capture review results.

### 22 Review
- Scheduler startup now reads `LastAppShutdownUtc`, detects recurring runs missed while the app was closed, appends skipped scheduler notifications, and persists skipped history rows without auto-executing those jobs.
- Scheduler shutdown now stops the execution timer during form close and persists a fresh `LastAppShutdownUtc` anchor before configuration save.
- Scheduler history persistence now resolves per-job `MaxHistoryRuns` and `HistoryRetentionDays` overrides with fallback to global config defaults and the global per-host output cap.
- Skipped startup runs are persisted with an explicit `WasSkipped` flag so the history list can render `SKIPPED` instead of misclassifying them as failures.
- Scheduler history rows now display `StartedUtc` in the `Started` column and derive duration from the stored start/completion timestamps, clamping invalid negative durations to zero.
- Added focused regression coverage for skipped-run persistence, retention policy resolution, and the scheduler history grid timestamp/duration display.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~SchedulerHistoryPolicyResolverTests|FullyQualifiedName~JobListDialogRunNowTests" -p:BaseOutputPath=artifacts\\runtime-history-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\runtime-history-tests\\obj\\` passed (45/45).
- Verification: `dotnet build SSH_Helper.sln -p:BaseOutputPath=artifacts\\runtime-history-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\runtime-history-build\\obj\\` passed.
- Verification: `openspec validate update-scheduler-runtime-history --strict --no-interactive` passed.

## 21. Scheduler OpenSpec Follow-Up Proposals
- [x] 21.1 Create a scheduler integrity proposal covering stored credentials, drift activation, safe import disabling, run-now attribution, and single-instance dialog behavior.
- [x] 21.2 Create a scheduler host-grid parity proposal covering column operations, keyboard/clipboard behavior, CSV import parity, and host-count refresh rules.
- [x] 21.3 Create a scheduler runtime/history proposal covering missed-run recording, retention-policy enforcement, and history timestamp correctness.
- [x] 21.4 Validate all new OpenSpec changes with strict validation and capture results.
- [x] 21.5 Amend the scheduler host-grid parity proposal to include visual/styling parity with the main hosts grid.

### 21 Review
- Added standalone OpenSpec change `update-scheduler-job-integrity` with proposal, tasks, design, and `job-scheduler` spec deltas for stored credentials, drift activation, safe missing-target imports, run-now attribution, and single-instance scheduler dialog behavior.
- Added standalone OpenSpec change `update-scheduler-host-grid-parity` with proposal, tasks, and `job-scheduler` spec deltas for host-grid column parity, keyboard/clipboard parity, CSV/copy parity, live host-count refresh, and visual/styling parity with the main hosts grid.
- Added standalone OpenSpec change `update-scheduler-runtime-history` with proposal, tasks, and `job-scheduler` spec deltas for missed-run recording, retention policy enforcement, and correct history timestamps.
- Validation: `openspec validate update-scheduler-job-integrity --strict --no-interactive` passed.
- Validation: `openspec validate update-scheduler-host-grid-parity --strict --no-interactive` passed.
- Validation: `openspec validate update-scheduler-runtime-history --strict --no-interactive` passed.
- Validation: `openspec validate update-scheduler-host-grid-parity --strict --no-interactive` passed again after adding visual parity requirements.

## 20. Scheduler Implementation Review
- [x] 20.1 Cross-check `.planning/phases` scheduler requirements, plans, and validation notes against the implemented code paths.
- [x] 20.2 Review scheduler UI behavior with explicit comparison between the scheduler hosts grid and the main form hosts grid.
- [x] 20.3 Review scheduler persistence, execution, history, import/export, and notification flows for functional gaps or regressions.
- [x] 20.4 Run targeted verification and capture concrete review results.

### 20 Review
- Stored-credential jobs are not actually persisted or reloaded: the editor collects username/password text but save logic only stores `CredentialMode`, while execution expects credentials to already exist in Credential Manager.
- Missed-run recording is not wired into startup/shutdown flow: `SchedulingService.DetectMissedRuns(...)` and `AppConfiguration.LastAppShutdownUtc` exist, but the scheduler initialization path never uses them.
- Drift detection is incomplete: the editor saves target hashes and can clear `HasDriftWarning`, but no reviewed code path marks jobs drifted after preset or folder content changes.
- Scheduler host-grid parity is materially incomplete versus the main hosts grid: no column add/rename/delete flow, no copy/paste/delete keyboard behavior, no checked-row copy semantics, and no immediate host-count refresh on inline `Host_IP` edits.
- Import preview warns that missing-target jobs will be disabled, but the import save path persists them without disabling them.
- Run-now notifications are misclassified because Form1 only labels them as run-now when `TrackRunNow(...)` is called, and the current Job List run-now action never calls it.
- Per-job history retention overrides are captured in the editor but not used by `JobHistoryService`, which always applies hard-coded defaults on `JobCompleted`.
- Job history UI labels completion time as the run start time in the history grid.
- Verification: `dotnet build SSH_Helper.sln -p:BaseOutputPath=artifacts\\review-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\review-build\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobStorageServiceTests|FullyQualifiedName~SchedulingService|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobExportServiceTests|FullyQualifiedName~SchedulerNotificationTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~PresetManagerJobReferenceTests" -p:BaseOutputPath=artifacts\\review-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\review-tests\\obj\\` passed (217/217).

## 5. Base Environment Rebase and Restore
- [x] 5.1 Extend environment persistence with a separate base-environment value and normalization rules.
- [x] 5.2 Update environment service operations so base environment survives rename/delete and can be manually rebased.
- [x] 5.3 Update preset/manual environment switching in `Form1` to preserve base on preset loads and restore it on no-environment presets.
- [x] 5.4 Add the conditional toolbar base-environment indicator and refresh/status behavior.
- [x] 5.5 Amend OpenSpec/docs for persisted base-environment semantics.
- [x] 5.6 Add focused regression tests for base-environment persistence, preset-load decisions, and indicator visibility.
- [x] 5.7 Run verification and capture outcomes.

## 4. Script Load Environment Switching
- [x] 4.1 Add OpenSpec change artifacts for script-declared environment switching.
- [x] 4.2 Extend the script model/parser/editor metadata with the new top-level `environment` key.
- [x] 4.3 Consolidate preset editor loading in `Form1` and apply script-declared environment switching on load.
- [x] 4.4 Document the new root option and load-time behavior in `SCRIPTING.md`.
- [x] 4.5 Add focused parser/editor regression tests.
- [x] 4.6 Run focused verification and capture outcomes.

## 3. Missing Column Warning Script Suppression
- [x] 3.1 Add a top-level YAML script option to suppress the missing-column warning.
- [x] 3.2 Respect the new option during single-preset and folder execution preflight checks.
- [x] 3.3 Document the new option in `SCRIPTING.md`.
- [x] 3.4 Add parser/dependency-analysis regression tests.
- [x] 3.5 Run focused tests and capture outcome.

## 2. Prompt Spacing Bug (zsh PROMPT_SP Chunk Split)
- [x] 2.1 Confirm and document root cause in the live shell streaming path.
- [x] 2.2 Implement boundary-safe cleanup for split zsh prompt redraw artifacts before UI/history emission.
- [x] 2.3 Add regression tests for `%` + clear-sequence + prompt split across chunks.
- [x] 2.4 Run focused tests and capture outcome.

## 1. Space Loss Bug (Chunked Output)
- [x] 1.1 Confirm and document root cause in output normalization pipeline.
- [x] 1.2 Add targeted normalization option to preserve trailing spaces on unfinished chunk lines.
- [x] 1.3 Use the new option in live chunk UI emission path.
- [x] 1.4 Add regression tests for split chunks (`set ` + `resource ...`).
- [x] 1.5 Run focused tests and capture outcome.

## 6. Folder Base Environment Inheritance
- [x] 6.1 Add OpenSpec change artifacts for folder-level base-environment overrides.
- [x] 6.2 Persist folder base-environment metadata and normalize invalid values.
- [x] 6.3 Add preset-folder context-menu assignment UI with inherited fallback behavior.
- [x] 6.4 Apply resolved folder-base environments when loading presets and selecting/executing folders.
- [x] 6.5 Keep folder base-environment references valid across folder rename/delete and environment rename/delete flows.
- [x] 6.6 Add focused regression tests for folder base resolution and persistence.
- [x] 6.7 Run verification and capture outcomes.

## 7. Folder Base Menu Click Regression
- [x] 7.1 Confirm why the folder base environment context-menu entry does not open.
- [x] 7.2 Patch the menu item so a normal click opens its dropdown.
- [x] 7.3 Run verification and capture outcome.

## 8. Folder Base Menu Interaction Rework
- [x] 8.1 Confirm the click-to-open submenu patch still does not work in the real UI flow.
- [x] 8.2 Replace the nested submenu interaction with a direct chooser launched from the context-menu command.
- [x] 8.3 Run verification and capture outcome.

## 9. Folder Base Chooser Crash
- [x] 9.1 Confirm the secondary chooser menu is crashing in the WinForms context-menu disposal path.
- [x] 9.2 Replace the secondary chooser menu with a stable dialog-based selection flow.
- [x] 9.3 Run verification and capture outcome.

## 10. Folder Summary Base Environment Refresh
- [x] 10.1 Confirm the folder details pane can be ambiguous or stale when switching folders with different base-environment sources.
- [x] 10.2 Make the folder summary explicitly show inherited source folders and refresh selected-folder details when environment state changes.
- [x] 10.3 Run verification and capture outcome.

## 11. Folder Click Summary Refresh
- [x] 11.1 Confirm folder-to-folder clicks can leave the first folder summary in the command pane.
- [x] 11.2 Make folder click handling refresh the folder summary even when `AfterSelect` does not deliver the expected update.
- [x] 11.3 Run verification and capture outcome.

## 12. Read-Only Folder Summary Refresh
- [x] 12.1 Confirm the command editor can block programmatic folder-summary updates once the first folder leaves it read-only.
- [x] 12.2 Patch the editor control so programmatic text updates still work while preserving read-only mode for user edits.
- [x] 12.3 Add focused regression tests for read-only programmatic updates.
- [x] 12.4 Run verification and capture outcome.

## 13. Manual Environment Switch Folder Refresh
- [x] 13.1 Confirm folder details refresh too early during manual environment/base switches, leaving the global base label stale.
- [x] 13.2 Refresh selected-folder details after the final base environment is applied in manual environment-switch flows.
- [x] 13.3 Run verification and capture outcome.

## 14. Preset Environment Switch Status Message
- [x] 14.1 Confirm preset-load environment handling only reports base restores and missing environments, not successful declared-environment switches.
- [x] 14.2 Add a shared formatter/helper for preset-load environment status messages and use it for restore/switch/missing cases.
- [x] 14.3 Add focused regression tests for preset-load environment status text.
- [x] 14.4 Run focused verification and capture outcome.

## 15. Hosts File Header Indicator
- [x] 15.1 Confirm the current hosts header and CSV state transitions that should drive a filename/unsaved indicator.
- [x] 15.2 Add a hosts-file indicator that shows the current filename and whether the grid is unsaved/new.
- [x] 15.3 Add focused regression tests for the indicator formatting.
- [x] 15.4 Run verification and capture outcome.

## 16. Environment CSV Drift Detection
- [x] 16.1 Add OpenSpec change artifacts for environment CSV freshness tracking and stale-snapshot handling.
- [x] 16.2 Persist CSV fingerprint metadata with environment and saved-state host snapshots.
- [x] 16.3 Detect backing-file drift when switching environments and offer a safe reload path from disk.
- [x] 16.4 Show active hosts-file drift state in the hosts header and status messaging.
- [x] 16.5 Add focused regression tests for fingerprint persistence, drift evaluation, and indicator text.
- [x] 16.6 Run verification and capture outcome.

## Review
- Added OpenSpec change `update-script-load-environment` with proposal, implementation checklist, and spec deltas for load-time script environment selection.
- Added a top-level YAML `environment` key to the script model/parser/editor metadata without changing YAML auto-detection semantics for metadata-only text.
- Consolidated preset loading into a shared `Form1` helper and applied script-declared environment switching across tree selection, favorites, import/duplicate, and fallback load flows.
- Missing script-declared environments now leave the current environment unchanged and emit a non-blocking status-bar message.
- Documented the new root option in `SCRIPTING.md` and added parser/autocomplete/highlighter regression coverage.
- Hardened [SSH_Helper.csproj] against repo-local generated source leakage by excluding `artifacts/**` from default compile items, preventing duplicate assembly-attribute build failures after local verification runs.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-build --filter "FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~YamlSshSyntaxHighlighterTests"` passed (152/152).
- Verification: `openspec validate update-script-load-environment --strict --no-interactive` passed.
- Added top-level YAML flag `suppress_missing_column_warning: true` to the script model/parser and exposed it through dependency analysis.
- Updated `ValidateColumnDependencies(...)` to analyze presets individually so suppressed scripts skip the dialog while unsuppressed presets in the same run still trigger it.
- Documented the new header option in `SCRIPTING.md` with an optional-column example.
- Added parser/dependency-analysis regression tests for the new flag and metadata detection behavior.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptDependencyAnalyzerTests"` passed (150/150). Build emitted copy warnings because `SSH_Helper.exe` was running, but tests completed successfully.
- Prompt spacing bug root cause confirmed: zsh `PROMPT_SP` redraw artifacts were being stripped per chunk, so a split `%` + spaces/CR clear sequence leaked into the live output buffer.
- Implemented `StripZshPromptSpStreaming(..., ref carry)` and applied it in `SshShellSession` so ambiguous prompt-redraw suffixes are held across chunk boundaries and flushed safely at command end.
- Added regression tests for whole-sequence cleanup, split-chunk cleanup, legitimate mid-line percent preservation, and end-of-stream flushing.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~TerminalOutputProcessorTests"` passed (51/51). Build emitted copy warnings because `SSH_Helper.exe` was running, but tests completed successfully.
- Root cause confirmed: chunk-level normalization trimmed trailing spaces on unfinished chunk lines.
- Implemented `Normalize(..., preserveTrailingSpacesOnFinalLine: true)` for live chunk rendering in `SshShellSession`.
- Added regression tests in `TerminalOutputProcessorTests` for trailing-space preservation and split-chunk word join prevention.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~TerminalOutputProcessorTests"` passed (47/47).
- Added persisted `BaseEnvironment` configuration state and taught environment normalization to default/fix it alongside `ActiveEnvironment`.
- Updated `EnvironmentService` so manual rebases can persist a base environment and rename/delete/import flows keep that base valid.
- Updated `Form1` preset-load behavior so `environment:` presets switch only the active environment, while presets without `environment` restore the active environment back to the base environment.
- Added a conditional toolbar indicator that shows `Base: <name>` only while the active environment differs from the base environment.
- Added focused regression coverage for base-environment persistence plus utility tests for preset-load decisions and indicator visibility.
- Hardened both project files against generated-source leakage from repo-local `bin/**`, `obj/**`, and `artifacts/**` verification outputs.
- Verification: `dotnet build SSH_Helper.csproj` was attempted but failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 15128).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~EnvironmentServiceTests|FullyQualifiedName~PresetEnvironmentLoadPlannerTests|FullyQualifiedName~BaseEnvironmentIndicatorFormatterTests" -p:BaseOutputPath=artifacts\\verify-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-tests\\obj\\` passed (22/22).
- Verification: `openspec validate update-script-load-environment --strict --no-interactive` passed.
- Added OpenSpec change `update-folder-base-environments` with environment-management and preset-organization deltas for folder-level base-environment overrides.
- Extended `FolderInfo`/`PresetManager` with persisted folder base-environment metadata, invalid-reference cleanup on load, and repair helpers for environment rename/delete flows.
- Added a `Folder Base Environment` preset-folder context-menu submenu with inherited fallback labeling and immediate folder summary/environment refresh behavior.
- Preset loads now resolve environment precedence as global base -> nearest folder base -> script-declared preset environment, and folder selection/execution now applies the resolved folder base before use.
- Added focused regression coverage for pure folder-base resolution and temp-config preset-manager persistence/repair flows.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests"` passed (9/9).
- Verification: `openspec validate update-folder-base-environments --strict --no-interactive` passed.
- Patched the `Folder Base Environment` context-menu entry so clicking it explicitly opens the dropdown instead of relying on implicit submenu behavior.
- Replaced the fragile nested `Folder Base Environment` submenu interaction with a direct chooser context menu launched after the parent menu closes.
- Verification: `dotnet build SSH_Helper.csproj` passed after the chooser rework.
- Confirmed the second-stage chooser `ContextMenuStrip` could be disposed while WinForms was still closing the parent context menu, causing the reported `ObjectDisposedException`.
- Replaced the folder base chooser with a modal selection dialog built on the existing `ScriptChooseDialog` path, keeping the interaction outside the context-menu disposal lifecycle.
- Verification: `dotnet build SSH_Helper.csproj` passed after the dialog-based crash fix.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests"` passed (9/9).
- Updated folder-detail base-environment text to include the inherited source folder path, so switching between folders shows which ancestor is supplying the effective base.
- Added selected-folder summary refresh on environment changes so the details pane stays synchronized while folder-driven environment switching occurs.
- Added focused formatter regression tests for folder summary and inherit-choice labels.
- Verification: `dotnet build SSH_Helper.csproj` passed with one retry warning because `SSH_Helper.dll` was in use during the copy step.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~FolderBaseEnvironmentSummaryFormatterTests"` passed (14/14).
- Confirmed folder-to-folder clicks could leave the first folder summary visible because the custom TreeView click flow could miss the expected `AfterSelect`-driven refresh.
- Added a shared folder-selection handler plus click-path fallback refresh in both preset and favorites trees so folder clicks update the command pane even when WinForms selection events are inconsistent.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~FolderBaseEnvironmentSummaryFormatterTests"` initially failed because `obj\\Debug\\net8.0-windows\\SSH_Helper.dll` was locked by another process.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-build --filter "FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~FolderBaseEnvironmentSummaryFormatterTests"` passed (14/14).
- Confirmed the real blocker was the Scintilla-based command editor staying read-only after the first folder summary, which prevented later programmatic text replacements from taking effect.
- Patched `ScintillaScriptEditorControl` so `Text` and `Clear()` temporarily disable read-only during programmatic updates and then restore the prior read-only state.
- Added focused UI regression tests covering programmatic `Text` replacement and `Clear()` while the editor remains read-only.
- Verification: `dotnet build SSH_Helper.csproj` passed with apphost copy retry warnings because `SSH_Helper.exe` was running.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests|FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~FolderBaseEnvironmentSummaryFormatterTests"` passed (40/40) with the same running-exe copy warnings.
- Confirmed manual environment switches could refresh folder details too early from the environment-changed event, before the new base environment was persisted, leaving the folder summary on the old global-base label.
- Refreshed selected-folder details after manual environment/base-switch completion and after environment-management flows that keep a folder summary visible.
- Verification: `dotnet build SSH_Helper.csproj` failed because `obj\\Debug\\net8.0-windows\\SSH_Helper.dll` and `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` were locked by a running `SSH_Helper` process (PID 11172).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-env-refresh\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-env-refresh\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests|FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~FolderBaseEnvironmentSummaryFormatterTests" -p:BaseOutputPath=artifacts\\verify-env-refresh-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-env-refresh-tests\\obj\\` passed (40/40).
- Extracted preset-load environment status text into `PresetEnvironmentStatusFormatter` so restore, successful switch, and missing-environment notifications stay consistent.
- Added the missing success message for preset-declared environment switches, emitted only after `TrySwitchEnvironment(...)` succeeds.
- Added focused formatter regression tests for global-base restore, folder-base restore, successful environment switch, and missing-environment messaging.
- Verification: `dotnet build SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 56684).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-preset-switch\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-preset-switch\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetEnvironmentLoadPlannerTests|FullyQualifiedName~PresetEnvironmentStatusFormatterTests" -p:BaseOutputPath=artifacts\\verify-preset-switch-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-preset-switch-tests\\obj\\` passed (8/8).
- Added `HostsFileIndicatorFormatter` and wired the hosts header label to show `Hosts: <file>` or `Hosts: <file> (unsaved)` with `Unsaved` fallback when no backing CSV path exists.
- Refreshed the hosts header through the shared host-count/selection paths and the remaining save, column-edit, delete-cell, and restore-state transitions that change CSV identity or dirty state without changing host counts.
- Adjusted the hosts header title label to fill available space with ellipsis so longer filenames do not crowd out the host count on the right.
- Added focused regression tests for missing-path, clean-file, and dirty-file indicator formatting.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostsFileIndicatorFormatterTests"` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 59064).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-hosts-header\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-hosts-header\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostsFileIndicatorFormatterTests" -p:BaseOutputPath=artifacts\\verify-hosts-header-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-hosts-header-tests\\obj\\` passed (3/3).
- Added OpenSpec change `update-environment-csv-sync` covering persisted CSV fingerprints, stale-snapshot detection on environment activation, reload prompting, and hosts-header drift indicators.
- Extended environment snapshots and remembered application state with `LastCsvFingerprint`, then persisted that metadata through environment save/load/import flows and current-grid saves.
- Added `CsvFileSyncEvaluator` plus switch-time stale-file handling in `Form1` so activating an environment now detects changed or missing backing CSVs, prompts to reload when the file changed, and can refresh the environment snapshot directly from disk.
- Expanded the hosts header indicator to show `disk changed` and `missing on disk` states in addition to `unsaved`, and report reload/stale outcomes through manual environment-switch status messages.
- Added focused regression coverage for environment fingerprint persistence, stale-file evaluation, and expanded hosts-file indicator text.
- Verification: `dotnet build SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 59064).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-env-csv-sync\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-env-csv-sync\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~EnvironmentServiceTests|FullyQualifiedName~HostsFileIndicatorFormatterTests|FullyQualifiedName~CsvFileSyncEvaluatorTests" -p:BaseOutputPath=artifacts\\verify-env-csv-sync-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-env-csv-sync-tests\\obj\\` passed (26/26).
- Verification: `openspec validate update-environment-csv-sync --strict --no-interactive` passed.
- Root cause confirmed: blank top-level lines were treated as an empty identifier, so the provider returned every root key whenever the popup was refreshed after header edits or other non-manual caret moves.
- Split autocomplete invocation into automatic vs manual blank-line behavior so `Ctrl+Space` can still offer root keys on an empty top-level line, while normal typing/refresh paths suppress that noisy popup.
- Added focused regression tests for provider-level blank-line root completion behavior and the Scintilla editor's auto-vs-manual popup integration.
- Verification: `dotnet build SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 48888).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-autocomplete\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-autocomplete\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -p:BaseOutputPath=artifacts\\verify-autocomplete-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-autocomplete-tests\\obj\\` passed (56/56).
- Refined the blank-line root autocomplete rule after user feedback: automatic root-key suggestions now still appear in the top-level metadata/header area, but only until the first top-level `vars:` or `steps:` section is reached.
- Kept the post-section suppression for blank-line auto-popup behavior and preserved explicit `Ctrl+Space` root-key suggestions anywhere at the top level.
- Added regression coverage for provider and Scintilla popup behavior before `vars:` / `steps:` and after those sections.
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-header-autocomplete\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-header-autocomplete\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -p:BaseOutputPath=artifacts\\verify-header-autocomplete-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-header-autocomplete-tests\\obj\\` passed (58/58).
- Confirmed the preset header had no selection/dirty indicator yet, while `IsPresetDirty()` already defined the exact unsaved-state rules to reuse.
- Added `PresetHeaderIndicatorFormatter` plus a shared `Form1` header refresh path so the presets header now shows the active preset or folder and appends `(unsaved)` during editor drift.
- Wired the preset header refresh to command/name/timeout edits and to preset save/load/rename/folder-summary transitions, and let the header label auto-ellipsis long names.
- Added focused regression tests for clean default, clean preset, dirty preset, folder selection, and unnamed dirty-editor formatter cases.
- Verification: `dotnet build SSH_Helper.csproj` failed because `obj\\Debug\\net8.0-windows\\SSH_Helper.dll` was locked by another process.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetHeaderIndicatorFormatterTests"` passed (5/5).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-preset-header\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-preset-header\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetHeaderIndicatorFormatterTests" -p:BaseOutputPath=artifacts\\verify-preset-header-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-preset-header-tests\\obj\\` passed (5/5).
- User follow-up confirmed the first preset indicator landed in the presets pane header, not in the active editor header where edits are made.
- Mirrored the dirty indicator into the visible script editor header by switching the section label to `Commands (unsaved)` and the button text to `Save*` while `IsPresetDirty()` is true.
- Extended the formatter coverage for the visible command-header and save-button labels.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetHeaderIndicatorFormatterTests"` passed (9/9).
- Root cause confirmed for the autocomplete follow-up: when a completion popup was already open, caret movement only repositioned it and never re-ran completion for the new caret context, so header/root suggestions could visually follow the caret below `vars:` / `steps:`.
- Updated `ScintillaScriptEditorControl` to remember the active blank-line completion mode and refresh the visible popup on selection changes, which hides stale root suggestions once the caret moves into a suppressed context.
- Added a focused WinForms regression test covering a root popup opened in the header and then moved to a blank line after `steps:`.
- Verification: `dotnet build SSH_Helper.csproj` passed with apphost copy retry warnings because `SSH_Helper.exe` was running (PID 60432).
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests|FullyQualifiedName~ScriptAutocompleteProviderTests"` passed (59/59).
- User correction narrowed the requirement further: root-level autocomplete must stay suppressed below top-level `vars:` / `steps:` even when completion is triggered manually with `Ctrl+Space`.
- Removed the provider/editor blank-line manual override so manual completion now follows the same post-section suppression rule as automatic popup refresh.
- Updated focused regression coverage so provider/editor tests now assert that a blank top-level line after `steps:` stays hidden for both auto-popup and `Ctrl+Space`.
- Verification: `dotnet build SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 73144).
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests|FullyQualifiedName~ScriptAutocompleteProviderTests"` failed because `obj\\Debug\\net8.0-windows\\SSH_Helper.dll` was locked by the same running process.
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-autocomplete-manual\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-autocomplete-manual\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests|FullyQualifiedName~ScriptAutocompleteProviderTests" -p:BaseOutputPath=artifacts\\verify-autocomplete-manual-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-autocomplete-manual-tests\\obj\\` passed (59/59).

## 17. YAML Root Autocomplete Noise
- [x] 17.1 Confirm why top-level/root key suggestions appear on blank lines below the metadata header and around step editing.
- [x] 17.2 Limit blank-line root suggestions to explicit/manual completion while keeping typed-prefix and step-scope suggestions intact.
- [x] 17.3 Add focused regression tests for auto vs manual blank-line root completions.
- [x] 17.4 Run focused verification and capture outcome.

## 18. Header Region Root Autocomplete
- [x] 18.1 Refine blank-line root autocomplete so the metadata/header area still auto-suggests top-level keys before `vars:` or `steps:`.
- [x] 18.2 Keep blank-line auto suggestions suppressed once the script is at or below top-level `vars:` / `steps:` sections, while preserving manual `Ctrl+Space`.
- [x] 18.3 Add focused regression tests for header-region vs post-section blank-line completion behavior.
- [x] 18.4 Run focused verification and capture outcome.

## 19. Preset Dirty Header Indicator
- [x] 19.1 Confirm the preset header states and reuse the existing preset dirty rules for indicator behavior.
- [x] 19.2 Add a preset header indicator that shows the active preset or folder and appends an unsaved marker when the editor is dirty.
- [x] 19.3 Add focused regression tests for the preset indicator formatting.
- [x] 19.4 Run focused verification and capture outcome.

## 20. Visible Preset Dirty Indicator
- [x] 20.1 Correct the preset dirty indicator placement so it appears in the active editor header while editing.
- [x] 20.2 Reuse the existing dirty-state rules in the visible editor header text without regressing the presets-pane label.
- [x] 20.3 Extend focused regression tests for the visible editor indicator text.
- [x] 20.4 Run focused verification and capture outcome.

## 21. Root Autocomplete Popup Follow-Up
- [x] 21.1 Confirm why root-level completion items still appear when the caret moves below top-level `vars:` / `steps:` content.
- [x] 21.2 Patch the popup refresh/hide behavior so stale root suggestions do not persist in suppressed contexts.
- [x] 21.3 Add focused regression coverage for caret-move/update flows after a root popup is already visible.
- [x] 21.4 Run focused verification and capture outcome.

## 22. Post-Section Manual Root Autocomplete Suppression
- [x] 22.1 Confirm the remaining root autocomplete path below `vars:` / `steps:` is the explicit/manual blank-line request flow.
- [x] 22.2 Remove blank-line root suggestions after `vars:` / `steps:` for both automatic and manual popup requests while preserving valid scoped completions.
- [x] 22.3 Update focused provider/editor regression coverage for the corrected manual behavior.
- [x] 22.4 Run focused verification and capture outcome.

## 23. Trailing Blank Line Tab Indent
- [x] 23.1 Confirm why pressing `Tab` on a trailing blank line indents the previous line instead of the current blank line.
- [x] 23.2 Patch indentation line targeting so a final blank line after a newline is treated as its own editable line.
- [x] 23.3 Add focused regression coverage for utility/control Tab behavior on a trailing blank line.
- [x] 23.4 Run focused verification and capture outcome.

## 24. Table Column Highlight Consistency
- [x] 24.1 Confirm the current syntax-highlighting gap for nested `table.columns` keys and keep the fix scoped to editor coloring only.
- [x] 24.2 Patch YAML highlighting so nested table-column keys render consistently with other recognized option keys.
- [x] 24.3 Add focused regression coverage for nested table-column key highlighting.
- [x] 24.4 Run focused verification and capture outcome.

## 25. Scheduler Code Map
- [x] 25.1 Inspect scheduler UI entry points in `JobListDialog.cs`, `JobEditorDialog.cs`, and `Form1.cs`.
- [x] 25.2 Inspect implemented scheduler models, services, and utilities without reading planning docs.
- [x] 25.3 Inspect scheduler-focused tests and note covered versus uncovered behaviors.
- [x] 25.4 Produce a concise architecture/code map with file references and likely weak spots.

## 26. Scheduler Planning Artifact Review
- [x] 26.1 Read `.planning/REQUIREMENTS.md` and scheduler phase artifacts `01-job-definitions-persistence` through `05-scheduler-ui-integration` only.
- [x] 26.2 Extract required scheduler behaviors, validations, and explicit UX/functional details from those planning documents.
- [x] 26.3 Deliver a concise referenced summary for the user and capture the review result below.

## 27. Implement update-scheduler-host-grid-parity
- [x] 27.1 Read the approved OpenSpec change artifacts for `update-scheduler-host-grid-parity` and map the main-grid behaviors that must be mirrored in `JobEditorDialog`.
- [x] 27.2 Add scheduler Hosts-tab column, keyboard/clipboard, import/copy, and host-count parity with minimal shared helper logic.
- [x] 27.3 Align the scheduler host-grid visual treatment with the main hosts grid, including row sizing, row numbers, selection styling, and themed scroll handling.
- [x] 27.4 Add focused automated coverage for scheduler host-grid parity helpers and dialog behaviors.
- [x] 27.5 Run verification, update the OpenSpec checklist, and capture the review outcome below.

## 28. Implement update-scheduler-job-integrity
- [x] 28.1 Read the approved OpenSpec change artifacts for `update-scheduler-job-integrity` and map the affected credential, drift, import, and Form1 integration paths.
- [x] 28.2 Add secure stored-credential round-trip support for scheduler jobs without persisting plaintext to `jobs.json`.
- [x] 28.3 Recompute scheduler drift state when referenced preset or folder snapshots change, and normalize missing-target imports into disabled jobs with explicit reasons.
- [x] 28.4 Fix Run Now attribution and modeless scheduler single-instance reuse from Form1/job-list entry points.
- [x] 28.5 Add focused automated coverage and run verification, then update the OpenSpec checklist and capture the outcome below.

## 29. Inspect update-scheduler-runtime-history
- [x] 29.1 Read the approved OpenSpec change artifacts for `update-scheduler-runtime-history` and confirm the required behavior deltas.
- [x] 29.2 Trace the current shutdown timestamp persistence/read paths and startup missed-run detection entry points.
- [x] 29.3 Trace scheduler event/history recording plus history UI bindings for started/duration values.
- [x] 29.4 Return the concrete files, methods, behavior gaps, and smallest likely edit points.

### 29 Review
- `LastAppShutdownUtc` exists on `AppConfiguration` and round-trips through `ConfigurationService`, but no production path sets it on shutdown or reads it during scheduler startup.
- Startup missed-run detection logic exists only as pure helpers in `SchedulingService`; production scheduler startup goes through `Form1.InitializeSchedulerServices()` and `JobExecutionService.Initialize()` without calling `DetectMissedRuns(...)`.
- Scheduler history persistence is driven solely by `JobHistoryService.SubscribeTo(JobExecutionService)` -> `OnJobCompleted(...)`, which always saves with hard-coded retention/output defaults and has no skipped-run write path.
- Scheduler history UI binds `Started` from `CompletedUtc` in `JobListDialog.RefreshHistory()`, while duration correctly uses `CompletedUtc - StartedUtc`; result rendering also only supports `OK`/`FAIL`, not a skipped state.

### 29 Review
- OpenSpec change `update-scheduler-runtime-history` requires a persisted shutdown anchor plus startup-time missed recurring runs to be recorded as skipped without auto-execution; see `openspec/changes/update-scheduler-runtime-history/proposal.md` and `openspec/changes/update-scheduler-runtime-history/specs/job-scheduler/spec.md`.
- `AppConfiguration.LastAppShutdownUtc` exists in the config model, and `ConfigurationService` will serialize/deserialize it generically, but production runtime code does not currently set or read that property anywhere.
- Actual startup wiring in `Form1.InitializeSchedulerServices()` loads jobs, creates scheduler services, runs `JobExecutionService.Initialize()` crash recovery, and starts the timer immediately; no startup path calls `SchedulingService.DetectMissedRuns(...)`.
- `JobExecutionService` does call `SchedulingService.GetMissedOccurrences(...)`, but only inside the live 30-second evaluation loop using `_lastEvaluationUtc`, which is initialized to `DateTime.UtcNow`; that covers only in-process gaps between timer evaluations, not downtime between app shutdown and restart.
- There is also no production consumer for `SkippedRunEntry`: `JobHistoryService` only persists `JobRunResult` instances received from the `JobCompleted` event, so startup-detected missed occurrences currently have no path into persisted scheduler history.
- Smallest likely edit points are `Form1_FormClosing()` for writing a dedicated shutdown anchor, `Form1.InitializeSchedulerServices()` for reading it and invoking missed-run detection before `_jobExecutionService.Start()`, and a narrow bridge in `JobHistoryService` (or adjacent startup wiring) to persist/report each `SkippedRunEntry`.
- Source inspection only; no code changes or test runs were performed for this task.

## Review Addendum
- Reviewed scheduler implementation only from code and tests: `Form1`, `JobListDialog`, `JobEditorDialog`, scheduler-related models/services/utilities, and scheduler-focused tests. No planning docs were read for this task.
- Confirmed the implemented scheduler stack is split into UI wiring (`Form1`/dialogs), pure cron helpers (`SchedulingService`, `CronBuilderControl`, validators/formatters), persistence (`JobStorageService`, `JobHistoryService`, `JobExportService`), and timer-driven execution (`JobExecutionService`).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobStorageServiceTests|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobExportServiceTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~SchedulerNotificationTests|FullyQualifiedName~CronBuilderControlTests|FullyQualifiedName~JobDefinitionTests|FullyQualifiedName~MaxConcurrentJobsTests|FullyQualifiedName~ExecutionPipelineModelTests|FullyQualifiedName~PresetManagerJobReferenceTests" -p:BaseOutputPath=artifacts\\verify-scheduler-map\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-scheduler-map\\obj\\` passed (292/292). Build emitted two existing warnings about unused `_schedulerStatusDirty` and `_loaded` fields.
- Main implementation risks found: missed-run detection exists but is not wired into production flow; stored job credentials UI appears to validate input without persisting it; per-job/global history retention settings are modeled in UI/config but not applied by the event-driven history writer; cancellation and run-now notification paths have disconnected plumbing; drift metadata is saved/checked but no production path sets `HasDriftWarning = true`.
- Reviewed only `.planning/REQUIREMENTS.md` plus scheduler phase artifacts `01-job-definitions-persistence` through `05-scheduler-ui-integration`; implementation code was intentionally not inspected.
- Consolidated the planned scheduler contract across job persistence, scheduling, execution, history, export/import, and Form1/UI integration with file/line references for user review.
- Noted one planning nuance for follow-up: `.planning/REQUIREMENTS.md` still marks `UI-03` notifications as pending even though Phase 5 planning specifies the intended notification/status-bar behavior in detail.
- Focused scheduler hosts-grid parity review completed against the phase note that calls for the Hosts tab mini-grid to use the same column structure as the main grid.
- Findings from the comparison: the scheduler grid lacks manual column add/rename/delete/reorder flows, its host count label does not refresh on inline `Host_IP` edits, its CSV import path diverges from the main grid's `CsvManager` behavior, keyboard clipboard/selection workflows are not carried over, and visual parity is only partial because the main grid adds custom scrollbars and painting on top of shared theme colors.
- Verification: source review only for this parity check; no tests were run.
- Root cause confirmed for the table-column highlighting inconsistency: the editor only colored top-level keys, step commands, and global step-option keys, so nested `table.columns` keys like `header` and `field` were left white.
- Extended the YAML highlighter's option-key set with nested table-column keys and taught list-item mappings like `- header:` to render as option keys when they are not actual step commands.
- Added focused regression tests for both `- header:` and `field:` under `table.columns`.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~YamlSshSyntaxHighlighterTests"` passed (5/5).
- Root cause confirmed for the trailing blank-line Tab bug: `EditorTextUtilities.GetLineStartIndices(...)` did not create a line-start entry for a final newline, so a caret on the trailing blank line was mapped back to the previous content line during indentation.
- Patched trailing line-start enumeration so a final blank line is treated as its own line target for indentation edits.
- Added focused regression coverage at both the utility layer and the Scintilla control layer for pressing `Tab` on a trailing blank line.
- Verification: `dotnet build SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 9196, plus .NET Host child processes).
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~EditorTextUtilitiesTests|FullyQualifiedName~ScintillaScriptEditorControlTests"` failed for the same locked-output reason while rebuilding the app project.
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-trailing-tab\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-trailing-tab\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~EditorTextUtilitiesTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -p:BaseOutputPath=artifacts\\verify-trailing-tab-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-trailing-tab-tests\\obj\\` passed (41/41).
- Implemented scheduler host-grid parity in `JobEditorDialog` with add/rename/delete/reorder support, main-grid-style keyboard/clipboard editing, shared CSV import semantics, and immediate host-count refresh on inline `Host_IP` edits.
- Added shared `HostGridUtilities` coverage for scheduler copy-source selection, DataTable snapshot conversion, and paste expansion, plus WinForms dialog tests for grid parity properties, copy-from-main behavior, host-count refresh, and persisted display-order extraction.
- Implemented scheduler job-integrity fixes across `JobEditorDialog`, `JobListDialog`, `Form1`, `PresetManager`, and supporting utilities so stored credentials round-trip through Credential Manager, missing-target imports save disabled, preset/folder mutations activate drift warnings, and the scheduler window/run-now flows reuse Form1-owned integration seams.
- Added focused coverage for stored-credential save/reopen behavior, preset/folder drift activation, missing-target import normalization helpers, run-now callback routing, and modeless dialog reuse.
- Verification: `dotnet build SSH_Helper.sln -p:BaseOutputPath=artifacts\\job-integrity-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\job-integrity-build\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetManagerJobReferenceTests|FullyQualifiedName~SchedulerJobIntegrityUtilitiesTests|FullyQualifiedName~JobEditorDialogStoredCredentialTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~ModelessDialogManagerTests" -p:BaseOutputPath=artifacts\\job-integrity-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\job-integrity-tests\\obj\\` passed (28/28).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobStorageServiceTests|FullyQualifiedName~PresetManagerJobReferenceTests|FullyQualifiedName~JobExportServiceTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~JobEditorDialogStoredCredentialTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~ModelessDialogManagerTests|FullyQualifiedName~SchedulerJobIntegrityUtilitiesTests" -p:BaseOutputPath=artifacts\\job-integrity-regression-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\job-integrity-regression-tests\\obj\\` passed (144/144).
- Verification: `openspec validate update-scheduler-job-integrity --strict --no-interactive` passed.
- Updated the main-form scheduler handoff to copy checked rows first when any host rows are checked, otherwise all eligible host rows, while excluding the select-checkbox column.
- Updated `DialogTheme.ApplyNativeTheme(...)` to theme `DataGridView` scrollbars so the scheduler grid inherits themed scroll treatment in dark/light modes.
- Verification: `dotnet build SSH_Helper.sln -p:BaseOutputPath=artifacts\\host-grid-parity-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\host-grid-parity-build\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostGridUtilitiesTests|FullyQualifiedName~JobEditorDialogHostGridParityTests" -p:BaseOutputPath=artifacts\\host-grid-parity-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\host-grid-parity-tests\\obj\\` passed (7/7).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CsvManagerTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~HostGridUtilitiesTests|FullyQualifiedName~JobEditorDialogHostGridParityTests" -p:BaseOutputPath=artifacts\\host-grid-parity-tests2\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\host-grid-parity-tests2\\obj\\` passed (50/50).
- Verification: `openspec validate update-scheduler-host-grid-parity --strict --no-interactive` passed.
- Manual interactive UI verification was not run from this CLI environment; OpenSpec task `5.2` remains unchecked pending a live click-through.
