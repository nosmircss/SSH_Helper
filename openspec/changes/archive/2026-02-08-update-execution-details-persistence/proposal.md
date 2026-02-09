# Change: Persist execution details with saved history

## Why
Execution history survives restart, but `View Details` metadata currently does not. Users lose the execution context they explicitly asked to retain.

## What Changes
- Persist `ExecutionDetails` per history entry in `SavedState.History`.
- Restore persisted details into the runtime history-details store on startup.
- Keep backward compatibility for legacy history entries that have no details.
- Add tests validating round-trip persistence for nested execution detail data.

## Impact
- Affected specs: `execution-history`
- Affected code:
  - `Models/AppConfiguration.cs`
  - `Form1.cs`
  - `SSH_Helper.Tests/Services/`
