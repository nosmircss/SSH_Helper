# Change: Update scheduler runtime and history correctness

## Why
The scheduler runtime and history pipeline still misses several correctness behaviors promised by the scheduler roadmap: recurring jobs missed during downtime are not recorded, per-job retention overrides are ignored, and the history UI does not consistently present the stored timestamps as intended.

## What Changes
- Record missed recurring runs as skipped using persisted app shutdown timestamps during scheduler startup
- Apply per-job and global scheduler retention/output policies when persisting scheduler history
- Correct scheduler history presentation so run rows display the actual start time and derived duration from persisted timestamps

## Impact
- Affected specs:
  - `job-scheduler`
- Affected code:
  - `Form1.cs`
  - `Models/AppConfiguration.cs`
  - `Services/SchedulingService.cs`
  - `Services/JobExecutionService.cs`
  - `Services/JobHistoryService.cs`
  - `JobListDialog.cs`
