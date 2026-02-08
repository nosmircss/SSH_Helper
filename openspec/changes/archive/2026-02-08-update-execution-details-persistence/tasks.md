## 1. Implementation
- [x] 1.1 Add persisted execution-details field to history entry model.
- [x] 1.2 Save execution details into `SavedState.History` when configuration is persisted.
- [x] 1.3 Restore persisted execution details into runtime history storage on startup.
- [x] 1.4 Preserve execution details when merging environment grid state with saved app state.

## 2. Validation
- [x] 2.1 Add/update tests for configuration round-trip persistence of execution details.
- [x] 2.2 Run targeted test(s) covering the new persistence behavior.
- [x] 2.3 Validate OpenSpec change with strict mode.
