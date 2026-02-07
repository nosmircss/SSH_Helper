# Change: Add execution details view to history context menu

## Why
After SSH execution, history entries store output and per-host results but discard execution parameters (preset name, commands, timeouts, username, host variables, run mode). Users have no way to review what was executed, against which hosts, and with what settings after the fact.

## What Changes
- Capture full execution metadata (settings, host context, timing) at execution time alongside existing history data
- Add "View Details..." context menu item to the history list
- New tabbed modal dialog displaying: Summary, Hosts, Settings, and Context tabs
- Export capability (copy to clipboard, save to file) for execution details
- Graceful degradation for history entries created before this feature

## Impact
- Affected specs: `execution-history`
- Affected code:
  - `Models/ExecutionDetails.cs` (new) — execution metadata model
  - `ExecutionDetailsDialog.cs` (new) — tabbed detail viewer dialog
  - `Services/HistoryResultStore.cs` — extended with details storage
  - `Form1.cs` — capture details at execution time, context menu wiring
  - `Form1.Designer.cs` — new menu item in `contextHistoryLst`
  - `SSH_Helper.Tests/Services/HistoryResultStoreTests.cs` — new tests
