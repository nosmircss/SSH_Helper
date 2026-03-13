# Change: Add per-job timeout overrides for scheduled jobs

## Why
Scheduled jobs currently inherit command and connection timeout behavior from presets and the global app settings. Operators cannot tune a specific scheduled task when it needs different timeout behavior from the shared preset or the global defaults.

## What Changes
- Add optional per-job command and connection timeout override fields to scheduler job definitions.
- Surface timeout override controls in the scheduler job editor with inherited-value guidance when overrides are not enabled.
- Apply the resolved per-job timeout values to both scheduled execution and Job List `Run Now`.
- Preserve backward-compatible `jobs.json` and `.sshjobs` import/export behavior.

## Impact
- Affected specs:
  - `job-scheduler`
- Affected code:
  - `Models/JobDefinition.cs`
  - `Services/JobExecutionService.cs`
  - `JobEditorDialog.cs`
  - scheduler storage/export services and focused scheduler tests
