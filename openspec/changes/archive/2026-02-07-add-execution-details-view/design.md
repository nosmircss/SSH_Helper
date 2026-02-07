## Context
Execution history currently stores only output text and per-host success/fail status. The execution parameters (what commands, what settings, which hosts with what variables) are discarded. This design captures that metadata at execution time and provides a browsable dialog.

## Goals / Non-Goals
- Goals: Capture and display full execution metadata; export details; work with both preset and folder executions
- Non-Goals: Persisting details across app restarts (in-memory only, same as current `HistoryResultStore`); editing or re-running from details

## Decisions

### Store details in existing `HistoryResultStore`
- **Why**: Already manages per-entry data keyed by the same history IDs with identical lifecycle (add, remove, clear). Adding a second dictionary avoids creating a redundant store class.
- **Alternative**: Separate `ExecutionDetailsStore` — rejected because it duplicates the same keying/cleanup logic.

### Exclude passwords from `HostExecutionContext.Variables`
- **Why**: `GetHostConnections()` copies `password` into `host.Variables` (Form1.cs:6142). Storing passwords in details would be a security concern. Filter out `password` key when building `HostExecutionContext`.

### Modal dialog with `BorderlessTabControl`
- **Why**: Matches existing `SettingsDialog` pattern. Tabs keep the information organized without overwhelming the user.

### In-memory only (no persistence)
- **Why**: Details are only available for the current session, same as the existing `HistoryResultStore` pattern. Persisting execution details would require schema changes to config.json and increase file size. Can be added later if needed.

## Risks / Trade-offs
- **Memory**: `HostExecutionContext` stores all variable values per host. For large grids (500+ hosts), this could be significant. Mitigated by the existing `MaxHistoryEntries` trimming.
- **Thread safety**: `BuildExecutionDetails()` reads UI controls. Must be called on UI thread (within the existing `Invoke()` block in `StoreExecutionHistory`).

## Open Questions
- None — all decisions resolved.
