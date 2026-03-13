# Change: Update scheduler job integrity

## Why
The implemented scheduler UI currently exposes stored credentials, drift warnings, import preview safety, run-now actions, and modeless window affordances that are not wired end-to-end. Those gaps leave several scheduler paths inconsistent with the behavior the application now needs to rely on.

## What Changes
- Persist job-specific stored credentials through Windows Credential Manager without writing plaintext to `jobs.json`
- Recompute scheduler drift warnings when referenced presets or folders change after a job is saved
- Import missing-target jobs in a disabled state with an explicit disabled reason
- Preserve run-now notification attribution from job-list actions and reuse the existing modeless scheduler dialog instance instead of opening duplicates

## Impact
- Affected specs:
  - `job-scheduler`
- Affected code:
  - `JobEditorDialog.cs`
  - `JobListDialog.cs`
  - `Form1.cs`
  - `Services/JobExecutionService.cs`
  - `Services/JobExportService.cs`
  - `Services/PresetManager.cs`
  - `Services/JobStorageService.cs`
  - `Services/Credentials/*`
