## 1. Implementation
- [x] 1.1 Inventory existing execution paths and extract a single orchestration flow (host selection, timeouts, preset creation, status updates).
- [x] 1.2 Introduce an execution coordinator service and update Form1 to delegate non-UI logic.
- [x] 1.3 Align pooled and non-pooled execution behavior (algorithm settings, error handling, timeouts) and add tests.
- [x] 1.4 Add update package integrity verification before launching the updater.
- [x] 1.5 Document updater behavior and ExecutionPolicy usage in user-facing docs.
- [x] 1.6 Replace CSV parsing with multiline-safe parsing and add unit tests for quoted newlines and escapes.
- [x] 1.7 Remove unused configuration dependencies/appsettings.json or wire them into the configuration flow.
