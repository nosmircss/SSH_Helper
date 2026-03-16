# Change: Normalize cancellation outcomes and expose scheduler cancel UI

## Why
Manual, folder, and scheduled executions currently stop cooperatively, but the resulting runs are flattened into generic failures or completions. That makes operator feedback, history retention, and scheduler behavior diverge from the stop semantics the UI implies.

## What Changes
- Normalize cancellation as a first-class outcome across execution, history, and scheduler models.
- Preserve partial output and execution details for cancelled runs instead of dropping or relabeling them as failures.
- Add a Job List `Cancel` action for running scheduled jobs without changing the scope of the main-form Stop button.
- Distinguish cancelled scheduler runs from failed and skipped runs in persisted history and notifications.

## Impact
- Affected specs: `execution-control`, `execution-history`, `job-scheduler`
- Affected code: `Form1.cs`, `JobListDialog.cs`, `ExecutionDetailsDialog.cs`, execution/history/job models, `Services/SshExecutionService.cs`, `Services/JobExecutionService.cs`, `Services/JobHistoryService.cs`, `Services/HistoryStorageService.cs`, scheduler notification helpers, and focused tests
