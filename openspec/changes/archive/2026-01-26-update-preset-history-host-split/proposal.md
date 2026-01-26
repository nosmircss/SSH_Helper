# Change: Add per-host history split for preset executions

## Why
Preset executions against multiple hosts currently store a single combined history output, while folder executions store per-host results and show a host selector. Users need the same per-host history view for preset runs. When multiple hosts are selected for a preset run, the same execution options dialog used for folder runs should appear so users can review targets and execution settings before starting.

## What Changes
- Store per-host results for preset executions (non-folder), regardless of host count.
- Show the per-host host selector for any history entry that has per-host results.
- Present the execution options dialog for preset runs that target multiple hosts.

## Impact
- Affected specs: execution-history, ssh-execution
- Affected code: Form1.cs, FolderExecutionDialog.cs (reuse), Models/AppConfiguration.cs, Services/HistoryResultStore.cs
