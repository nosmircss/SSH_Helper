## 1. Implementation
- [x] 1.1 Add external history storage models (`HistoryIndexDocument`, `HistoryIndexEntry`, `HistoryRunPayload`) and `HistoryStorageService`.
- [x] 1.2 Persist history metadata to `history.index.json` and per-run payloads to `history/<entry-id>.json` beside `config.json`.
- [x] 1.3 Add atomic write behavior (`.tmp` write then replace/move) and best-effort `.bak` backup for the index file.
- [x] 1.4 Enforce `MaxHistoryEntries` retention at save time by deleting oldest index entries and corresponding run files.
- [x] 1.5 Implement one-time legacy migration from `SavedState.History` into the external history store.
- [x] 1.6 Refactor startup history load to metadata-only and lazy-load payloads when an entry is selected/exported/viewed.
- [x] 1.7 Refactor history save/read/delete paths in `Form1.cs` to use external storage APIs.
- [x] 1.8 Stop persisting history data in `ApplicationState` (retain legacy field for migration only).
- [x] 1.9 Remove persisted-history trimming/cropping from save paths; keep memory-pressure trimming scoped to transient UI buffers only.
- [x] 1.10 Update user-facing docs to describe external history files.

## 2. Validation
- [x] 2.1 Add automated tests for `HistoryStorageService` covering large payload roundtrip, lazy index load behavior, retention, delete, and legacy import.
- [x] 2.2 Build solution and run full test suite to verify no regressions.
