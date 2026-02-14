# Change: Add interactive terminal audit trail in execution details

## Why
Interactive terminal sessions currently appear live during execution, but there is no persistent per-session audit trail in history details once execution completes.

## What Changes
- Extend `ExecutionDetails` to persist interactive terminal sessions (metadata + transcript).
- Capture interactive session lifecycle and transcript data inside interactive runtime flow.
- Propagate captured sessions through script execution results into history details storage.
- Add a dedicated `Interactive` tab in `ExecutionDetailsDialog` for per-session inspection.
- Include interactive session logs in execution-details copy/export text output.
- Preserve backward compatibility for legacy entries without interactive session data.

## Impact
- Affected specs:
  - `execution-history`
- Affected code:
  - `Models/ExecutionDetails.cs`
  - `Models/ExecutionResult.cs`
  - `Services/Scripting/ScriptContext.cs`
  - `Services/Terminal/InteractiveTerminalService.cs`
  - `Services/SshExecutionService.cs`
  - `Form1.cs`
  - `ExecutionDetailsDialog.cs`
  - `SSH_Helper.Tests/Services/ConfigurationServiceExecutionDetailsTests.cs`
  - `SSH_Helper.Tests/Scripting/ScriptContextTests.cs`
  - `SSH_Helper.Tests/Scripting/InteractiveCommandTests.cs`
  - `SSH_Helper.Tests/Services/HistoryResultStoreTests.cs`
  - `SSH_Helper.Tests/UI/ExecutionDetailsDialogTests.cs`
