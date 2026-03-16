# Change: Replace scheduler drift with save-time warning

## Why
The current scheduler drift flow hides the real cause of skipped jobs behind a passive `[DRIFT]` indicator and a separate recovery workflow in the job editor. Users need the impact explained when they save a referenced preset, not after the scheduler silently stops running it.

## What Changes
- Replace scheduler drift indicators and execution blocking with a single preset save confirmation when an existing preset used by scheduled jobs is being updated
- Keep the existing unsaved preset diff visible inside that save confirmation while also surfacing the scheduled-job impact summary and affected job list
- Show affected scheduled job names in that save confirmation and explain that future scheduled and Run Now executions will use the updated preset
- Remove preset/folder mutation drift reevaluation and ignore legacy `HasDriftWarning` state during scheduler execution
- Keep legacy drift snapshot fields in serialized job files for compatibility without using them as active runtime behavior

## Impact
- Affected specs:
  - `job-scheduler`
- Affected code:
  - `Form1.cs`
  - `JobEditorDialog.cs`
  - `JobListDialog.cs`
  - `Services/JobExecutionService.cs`
  - `Services/PresetManager.cs`
  - `Utilities/SchedulerJobIntegrityUtilities.cs`
