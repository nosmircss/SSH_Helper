# Phase 5: Scheduler UI & Integration - Context

**Gathered:** 2026-03-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can manage the full job lifecycle through dedicated management dialogs with in-app notifications and job portability via export/import. This phase delivers the job list dialog, job editor dialog, run history viewing, output panel notifications, status bar integration, and job export/import. The underlying services (storage, scheduling, execution, history) are complete from Phases 1-4.

</domain>

<decisions>
## Implementation Decisions

### Job list dialog
- Separate modeless dialog, opened via menu item or toolbar button (follows SettingsDialog/EnvironmentDialog pattern)
- Dense table layout: Name, enabled status, schedule description (cron human-readable or one-time date), next run time, last run result (success/fail + timestamp), target preset name
- Context menu + toolbar for actions: Edit, Run Now, Enable/Disable, Delete, View History, Duplicate
- Live refresh via timer while dialog is open — updates next-run countdown and running status using JobExecutionService events
- Split panel layout: top half is job list grid, bottom half shows run history for the selected job
- Run history panel shows: started time, duration, result (success/fail with host counts)
- "View Output" button in history panel opens output viewer dialog
- "Clear History" button per job for manual history cleanup

### Job editor dialog
- Tabbed structure with tabs: General, Hosts, Credentials, Advanced
- **General tab**: Job name, target type (Preset/Folder radio buttons), target dropdown, schedule type (ComboBox: None/Recurring/One-time), embedded CronBuilderControl for recurring, DateTimePicker for one-time, human-readable cron description inline, next-run preview
- **Hosts tab**: Full embedded DataGridView (mini-grid) with same column structure as main grid. Toolbar buttons: Import CSV, Copy from Main Grid, Add Row, Remove Selected. Inline cell editing. Styled via DialogTheme.StyleDataGridView. Host count shown below grid
- **Credentials tab**: CredentialMode radio buttons (InheritFromApp, Stored, PerHostColumn) with context-appropriate controls for each mode
- **Advanced tab**: Folder execution mode (Sequential/Parallel), stop-on-error toggle, per-job history retention overrides (max runs, retention days)
- Drift warning: yellow/orange banner at top of General tab when preset has changed since job was saved. "Review & Acknowledge" button shows diff via existing UnsavedPresetDiffDialog pattern. Banner text: "Target preset has changed since this job was saved. Execution blocked until reviewed."
- Save button validates all fields, Cancel discards changes

### Notification behavior
- Output panel log entries in Form1's existing output area for all scheduler activity
- Differentiated prefixes by trigger type:
  - `[Scheduled: JobName]` for automatic cron/one-time runs
  - `[Run Now: JobName]` for manual run-now triggers
  - `[Skipped: JobName]` for missed runs detected on startup
- Format: `[HH:mm:ss] [Prefix: Name] Completed -- X/Y hosts succeeded (duration)` or `Failed -- X/Y hosts failed (duration)`
- Status bar integration: persistent text showing scheduler state — "Scheduler: N jobs active -- Next: JobName in Xh Ym". Clicking the status bar text opens the job list dialog
- Status bar updates via timer, same interval as job list live refresh

### Run output viewer
- Separate dialog for viewing full per-host output from a historical run
- Host selector dropdown at top to switch between hosts in the run
- Read-only RichTextBox for output display
- Find button launches existing FindDialog pattern for in-output search
- Copy All button for clipboard
- Themed via DialogTheme (dark/light)

### Export/import flow
- **Two export formats**:
  - `.sshjobs` JSON file (human-readable, version-controllable) via Save File dialog
  - GZip + Base64 string copied to clipboard via "Copy as String" for quick sharing
- File format: `{ "Version": 1, "ExportedUtc": "...", "Jobs": [...] }` wrapper for forward compatibility
- **Export scope**: selected job(s) from job list — multi-select enables bulk export. Toolbar buttons: "Export Selected" (file) and "Copy Selected as String" (clipboard)
- **Export content**: full job definition + host list, credentials stripped (CredentialMode reset to InheritFromApp on import). Safe for sharing
- **Import**: supports both formats — file import via Open File dialog, paste import for Base64 strings
- **Conflict handling**: import preview dialog listing jobs to be imported. Duplicate names get " (imported)" suffix. Missing presets flagged with warning ("Target preset not found -- job will be disabled until linked"). User confirms before importing
- Import always supports multiple jobs in one file

### Claude's Discretion
- Dialog dimensions and control sizing
- Exact toolbar icons/images (or text-only buttons)
- Timer intervals for live refresh and status bar updates
- How "Copy from Main Grid" determines which rows to copy (checked rows, selected rows, or all)
- Run history grid column widths and sorting defaults
- Status bar text truncation for long job names
- How the job list handles the "currently running" visual state (bold text, color, icon)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `DialogTheme`: Full dark/light theme system with `ApplyTo()`, `StyleButton()`, `StyleDataGridView()`, `SetDarkTitleBar()`, `ApplyNativeTheme()`, `StyleTabControl()`, `SetDialogFont()`, `Confirm()`, `Show()` — use for all new dialogs
- `BorderlessTabControl`: Custom TabControl used in SettingsDialog — reuse for editor dialog tabs
- `CronBuilderControl`: Phase 2 visual cron builder UserControl — embed directly in editor General tab
- `UnsavedPresetDiffDialog`: Visual diff dialog — reuse pattern for drift warning "Review & Acknowledge"
- `FindDialog`: Modeless find dialog — reuse for output search in run output viewer
- `ExecutionDetailsDialog`: Existing execution details viewer — reference pattern for run output viewer
- `SettingsDialog`: ~1363 lines, tabbed dialog with theme support — structural reference for job editor
- `EnvironmentDialog`: ~869 lines, modeless management dialog — structural reference for job list dialog
- `PresetManager`: Export/import with GZip + Base64 — reuse compression pattern for string export
- `CsvManager`: CSV import for host grids — reuse for job host list CSV import
- `JobStorageService`: Job CRUD and persistence — consume for all job operations
- `JobExecutionService`: Events (JobStarted, JobCompleted, JobQueued) — subscribe for live refresh and notifications
- `JobHistoryService`: Query API (GetRunsForJob, LoadRunPayload, SearchRunOutput) — consume for history panel
- `SchedulingService`: GetNextRunUtc(), GetHumanReadableDescription() — consume for schedule display
- `InputValidator`: Centralized validation — extend for job editor validation

### Established Patterns
- Modeless dialogs: `EnvironmentDialog` pattern — `Show()` not `ShowDialog()`, single instance tracking
- Code-only layout: No Designer.cs files for new dialogs — matches SettingsDialog, EnvironmentDialog, CronBuilderControl
- Event-driven updates: Services raise events, UI subscribes — use for live refresh
- Manual DI: Services passed via constructor — job list dialog receives all needed services
- `sealed` dialog classes: Match SettingsDialog pattern
- Form1 `#region` organization: Add Scheduler region for menu items, status bar, event handlers

### Integration Points
- `Form1` menu bar: Add "Scheduler" menu or "Jobs" menu item to open job list dialog
- `Form1` status bar: Add scheduler status label (StatusStrip already exists)
- `Form1` output panel: Append scheduler log entries via existing output mechanism
- `Form1` constructor: Wire job list dialog creation with all service dependencies
- `JobExecutionService` events: Subscribe for notifications and live refresh
- `JobStorageService` events: Subscribe for job list refresh on external changes
- `PresetManager`: Resolve target names for display and drift checking

</code_context>

<specifics>
## Specific Ideas

- The job list dialog should feel like an operations dashboard — split panel with job list on top and run history on bottom gives immediate visibility into both current state and recent activity
- Status bar integration ("Scheduler: 3 active -- Next: Backups in 2h") makes the scheduler feel integrated into the app rather than a hidden feature
- Differentiated notification prefixes (`[Scheduled:]`, `[Run Now:]`, `[Skipped:]`) turn the output panel into a proper audit log of all scheduler activity
- Import preview dialog with conflict resolution (auto-rename, missing preset warnings) prevents silent data issues when sharing jobs between instances
- Drift warning as a banner in the editor (not a blocking modal) lets users explore the job before deciding to acknowledge — less disruptive workflow

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope

</deferred>

---

*Phase: 05-scheduler-ui-integration*
*Context gathered: 2026-03-07*
