## 1. Data model and persistence
- [x] 1.1 Add interactive session model and `ExecutionDetails.InteractiveSessions`.
- [x] 1.2 Add deep-copy support for interactive sessions in execution-details cloning paths.
- [x] 1.3 Ensure configuration round-trip preserves interactive session details.

## 2. Runtime capture
- [x] 2.1 Add interactive session audit storage API to `ScriptContext`.
- [x] 2.2 Capture interactive transcript + lifecycle reason in `InteractiveTerminalService`.
- [x] 2.3 Persist launched-session audits on success, disconnect, cancel, and error close paths.

## 3. Execution aggregation
- [x] 3.1 Add per-host interactive sessions to `ExecutionResult`.
- [x] 3.2 Propagate script-context interactive sessions into execution results for pooled/non-pooled/local script execution.
- [x] 3.3 Aggregate execution-result interactive sessions into `ExecutionDetails`.

## 4. Details UI and export
- [x] 4.1 Add `Interactive` tab with session grid and transcript viewer in `ExecutionDetailsDialog`.
- [x] 4.2 Add interactive session count in summary tab.
- [x] 4.3 Include interactive sessions in copy/save text formatting output.

## 5. Validation coverage
- [x] 5.1 Extend execution-details configuration persistence tests for interactive sessions.
- [x] 5.2 Add script-context tests for interactive session ordering/numbering and snapshot copy safety.
- [x] 5.3 Add interactive command integration test for context audit retention.
- [x] 5.4 Extend history-result-store tests for details carrying interactive sessions.
- [x] 5.5 Add UI test coverage for interactive tab populated and empty states.
