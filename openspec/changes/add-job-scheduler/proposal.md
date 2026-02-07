# Change: Add in-app job scheduler

## Why
Execution is currently manual, which limits repeatable operational checks and backups that should run on defined schedules while the app is open.

## What Changes
- Add persisted scheduled jobs with cron and one-time schedule modes
- Add scheduler service for due-job detection, execution orchestration, and bounded concurrency
- Add scheduler management UI with job list, run-now actions, and per-run history
- Add job notifications, status bar integration, and full output retention
- Define startup behavior for missed jobs (recorded as skipped, not auto-run)

## Impact
- Affected specs:
  - `job-scheduler` (new capability)
- Affected code:
  - `Models/ScheduledJob.cs` (new)
  - `Models/JobHistoryEntry.cs` (new)
  - `Models/AppConfiguration.cs`
  - `Services/JobSchedulerService.cs` (new)
  - `Services/JobNotificationService.cs` (new)
  - `JobSchedulerDialog.cs` (new)
  - `JobEditorDialog.cs` (new)
  - `Form1.Designer.cs`
  - `Form1.cs`
  - `SSH_Helper.csproj`
