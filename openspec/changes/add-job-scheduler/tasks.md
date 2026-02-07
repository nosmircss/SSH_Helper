# Tasks: Add in-app job scheduler

## 1. Models and configuration
- [ ] 1.1 Add scheduler dependency package to `SSH_Helper.csproj`
- [ ] 1.2 Create `Models/ScheduledJob.cs` and `ScheduledJobHost`
- [ ] 1.3 Create `Models/JobHistoryEntry.cs`
- [ ] 1.4 Add scheduler and job-history persistence fields to `Models/AppConfiguration.cs`

## 2. Scheduler services
- [ ] 2.1 Implement `JobSchedulerService.cs` lifecycle, CRUD, and due-job evaluation
- [ ] 2.2 Add bounded concurrency and cancellation handling for job execution
- [ ] 2.3 Add startup missed-job detection that marks overdue runs as skipped
- [ ] 2.4 Implement `JobNotificationService.cs` for completion/failure notifications

## 3. UI integration
- [ ] 3.1 Create `JobSchedulerDialog.cs` for job list and run history
- [ ] 3.2 Create `JobEditorDialog.cs` with cron preview and host/credential options
- [ ] 3.3 Add entry point/menu wiring in `Form1.Designer.cs`
- [ ] 3.4 Integrate scheduler status and progress text in `Form1.cs`

## 4. Output and history handling
- [ ] 4.1 Persist history summaries in configuration with max-entry retention
- [ ] 4.2 Save full job output to per-entry files under local app data
- [ ] 4.3 Prune stale output files when history entries are removed

## 5. Verification
- [ ] 5.1 Add unit tests for cron evaluation, due detection, and missed-job handling
- [ ] 5.2 Add service tests for concurrency, cancellation, and run-now behavior
- [ ] 5.3 Run manual smoke tests for scheduled run execution and notifications
