# Codebase Concerns

**Analysis Date:** 2026-03-06

## Tech Debt

**Form1.cs God Object (10,059 lines):**
- Issue: `Form1.cs` is a 10,059-line monolith containing UI logic, SSH execution orchestration, history management, clipboard operations, CSV operations, preset management, theme management, find/replace, column operations, and update checking. Despite region organization, it is far too large for a single class.
- Files: `Form1.cs`
- Impact: Extremely difficult to test (no unit tests for Form1 itself), high risk of regressions when modifying any feature area, merge conflicts when multiple features are in-flight, and slow IDE navigation.
- Fix approach: Extract logical regions into dedicated controller/presenter classes (e.g., `HistoryController`, `PresetViewController`, `ExecutionController`, `ThemeManager`). The existing `#region` sections map directly to extraction boundaries. Use the existing event-driven pattern to communicate between components.

**ScriptParser.cs Complexity (3,543 lines):**
- Issue: The YAML script parser is a single 3,543-line class with complex manual YAML parsing logic alongside the YamlDotNet deserializer. Handles 25+ command types inline.
- Files: `Services/Scripting/ScriptParser.cs`
- Impact: Adding new script commands requires modifying this large file. Hard to reason about parsing edge cases. High regression risk.
- Fix approach: Extract per-command parsing into individual parser classes (following the existing `IScriptCommand` pattern) or use a registry/visitor approach for step type parsing.

**ScriptStep.cs Data Model (1,205 lines):**
- Issue: `ScriptStep` is a massive data class with nullable properties for every possible command type. Every step carries fields for all 25+ command types regardless of which one is active.
- Files: `Models/ScriptStep.cs`
- Impact: Memory waste for complex scripts, confusing API surface, no compile-time enforcement of which fields apply to which command.
- Fix approach: Consider a discriminated union pattern or separate step-type classes implementing a common interface, rather than one flat class.

**Empty Event Handler:**
- Issue: `tsbPassword_Click` at line 10050 is an empty event handler that does nothing.
- Files: `Form1.cs:10050-10053`
- Impact: Dead code, minor clutter.
- Fix approach: Remove the empty handler and unwire the event in the designer.

**Manual GC.Collect() Calls:**
- Issue: Multiple explicit `GC.Collect()` and `GC.WaitForPendingFinalizers()` calls with `LargeObjectHeapCompactionMode.CompactOnce` and `EmptyWorkingSet` P/Invoke. Used in history compaction and output trimming.
- Files: `Form1.cs:1029-1041`, `Form1.cs:4868-4870`, `Form1.cs:4906-4918`
- Impact: Can cause UI stalls during garbage collection. Indicates the app accumulates large string allocations (output buffers, history payloads) that should be managed more efficiently. May mask underlying memory management issues.
- Fix approach: Investigate streaming output to disk instead of accumulating in `StringBuilder`. Use memory-mapped files or paged views for large history payloads. Remove explicit GC calls once root cause is addressed.

**Dual JSON Serializers:**
- Issue: The codebase uses both `Newtonsoft.Json` (13.0.3) and `System.Text.Json` in different places. `ConfigurationService`, `HistoryStorageService`, and `PresetManager` use Newtonsoft. `HttpCommand` and `WriteFileCommand` use `System.Text.Json`.
- Files: `Services/ConfigurationService.cs`, `Services/HistoryStorageService.cs`, `Services/Scripting/Commands/HttpCommand.cs`, `Services/Scripting/Commands/WriteFileCommand.cs`
- Impact: Increased binary size, potential behavior differences between serializers, confusing which to use for new code.
- Fix approach: Standardize on one serializer. `System.Text.Json` is preferred for new .NET code, but migration would need careful testing of all serialization edge cases.

## Known Bugs

No explicit bugs found via TODO/FIXME markers. The codebase is clean of diagnostic comments.

## Security Considerations

**TLS Certificate Validation Bypass:**
- Risk: `HttpCommand` allows scripts to disable TLS verification via `verify_tls: false`, which calls `DangerousAcceptAnyServerCertificateValidator`.
- Files: `Services/Scripting/Commands/HttpCommand.cs:260-262`
- Current mitigation: This is a per-request opt-in via script YAML (`verify_tls` defaults to true).
- Recommendations: Log a warning when TLS verification is bypassed. Consider requiring an explicit confirmation or environment flag for production use.

**ScriptFileAccessValidator Hardcoded to C: Drive:**
- Risk: The file access validator hardcodes `C:\Windows`, `C:\Program Files`, etc. and `C:\Users` paths. On systems with Windows installed on a different drive or non-standard user profile paths, the validator could allow access to protected directories or incorrectly block legitimate paths.
- Files: `Services/Scripting/ScriptFileAccessValidator.cs:12-20`, `Services/Scripting/ScriptFileAccessValidator.cs:54-55`
- Current mitigation: Write operations are restricted to user directories (Documents, Desktop, AppData, Temp). Read operations block system directories and other users' profiles.
- Recommendations: Use `Environment.GetFolderPath()` and `Environment.SystemDirectory` instead of hardcoded paths. Use `Path.GetPathRoot(Environment.SystemDirectory)` to determine the system drive letter.

**Password in ToolStripTextBox:**
- Risk: The default password is displayed in `tsbPassword` (a `ToolStripTextBox`), which does not support `UseSystemPasswordChar`. A hidden `txtPassword` TextBox exists as a workaround for password masking, but the toolbar field itself may expose passwords via screen sharing or screenshots.
- Files: `Form1.cs:1076-1077`, `Form1.Designer.cs:1364-1366`
- Current mitigation: Password masking is wired up via `InitializePasswordMasking()`. Credential Manager integration stores passwords securely.
- Recommendations: Verify that `tsbPassword` actually masks the input characters. Consider replacing with a custom control that renders dots/asterisks.

**SSH Password in Memory:**
- Risk: SSH passwords are held as plain `string` objects in memory throughout the application lifetime (via toolbar text, host connection objects, and credential resolution).
- Files: `Form1.cs:1069-1078`, `Services/SshConnectionPool.cs:124-128`, `Services/SshExecutionService.cs:200-205`
- Current mitigation: Windows Credential Manager is used for persistent storage (`Services/Credentials/CredentialManagerProvider.cs`). Passwords are not written to config.json.
- Recommendations: Use `SecureString` or memory-pinned buffers where feasible. Zero out password strings after use. This is a defense-in-depth concern; the Windows Credential Manager integration is good.

## Performance Bottlenecks

**Sequential Host Execution:**
- Problem: SSH commands execute against hosts sequentially in a `foreach` loop, not in parallel.
- Files: `Services/SshExecutionService.cs:259-276`
- Cause: Each host is processed one at a time via `await Task.Run(() => ExecuteSingleHost(...))`. For large host lists, this means total execution time = sum of all individual host times.
- Improvement path: Use `Task.WhenAll` with configurable concurrency (e.g., `SemaphoreSlim` throttle) to execute against multiple hosts simultaneously. The connection pool already supports concurrent connections.

**StringBuilder Output Accumulation:**
- Problem: All SSH output accumulates in `_outputBuffer` (a `StringBuilder`) in memory, with periodic flushing to a `TextBox`. For large output volumes, this causes high memory usage and eventually triggers explicit GC calls.
- Files: `Form1.cs:170-171` (buffer declaration), `Form1.cs:4868-4918` (aggressive trim logic)
- Cause: No upper bound on output size until manual "aggressive trim" is triggered. History payloads can exceed 10MB (see `LargeHistoryPayloadCharThreshold = 10_000_000`).
- Improvement path: Implement a ring buffer or write output to temp files with a windowed view. Cap in-memory output at a reasonable threshold and page from disk.

**Single SemaphoreSlim for Connection Pool Creation:**
- Problem: `SshConnectionPool` uses a single `SemaphoreSlim(1,1)` (`_creationLock`) for all new connections, serializing connection creation across all hosts.
- Files: `Services/SshConnectionPool.cs:52`, `Services/SshConnectionPool.cs:155`
- Cause: Global lock prevents concurrent connection creation, even to different hosts.
- Improvement path: Use per-host locks (e.g., `ConcurrentDictionary<string, SemaphoreSlim>`) to allow parallel connection creation to different hosts while still preventing duplicate connections to the same host.

## Fragile Areas

**Form1 State Flags:**
- Files: `Form1.cs:128-168`
- Why fragile: Over 30 boolean and state-tracking fields (`_suppressPresetSelectionChange`, `_suppressEnvironmentSelectionChange`, `_suppressExpandCollapseEvents`, `_suppressHistorySelectionChanged`, `_suppressHostSelectionChanged`, `_historySelectionHandlingEnabled`, `_historySelectionArmPending`, etc.) create complex state machine behavior. Event suppression flags are easy to leave in wrong state if an exception interrupts a multi-step UI operation.
- Safe modification: Always wrap suppress flag changes in try/finally blocks. Test UI interactions manually for event ordering issues. Consider replacing suppress flags with a state machine enum.
- Test coverage: No automated tests for Form1 UI behavior. All state management is untested.

**History Selection Arming:**
- Files: `Form1.cs:159-168`
- Why fragile: History selection uses a complex arming/debounce mechanism with `_historySelectionArmedAtUtc`, `_historySelectionArmPending`, `_lastHistorySelectionGcEntryId`, and `Application.Idle` event hooks. Race conditions between idle handler arming and user interaction could cause missed or double history loads.
- Safe modification: Add comprehensive logging to trace arming state transitions. Consider simplifying to a standard debounce timer.
- Test coverage: No automated tests.

**Preset TreeView Drag-and-Drop:**
- Files: `Form1.cs:148-149` (state), `Form1.cs:3477` region
- Why fragile: TreeView drag-drop with `_draggedNode` and `_lastHighlightedNode` state depends on precise event ordering (DragEnter, DragOver, DragDrop, DragLeave). Missing cleanup of highlight state can leave visual artifacts.
- Safe modification: Test drag-drop manually with edge cases (drag to self, drag across folders, cancel mid-drag).
- Test coverage: No automated tests.

## Scaling Limits

**In-Memory Output Buffer:**
- Current capacity: Configurable thresholds at 500KB (`SmallHistoryPayloadCharThreshold`) and 10MB (`LargeHistoryPayloadCharThreshold`) character counts.
- Limit: Output beyond 500K chars triggers automatic compaction. Beyond 10MB, aggressive trimming is needed. Very large outputs (100+ hosts, verbose commands) can exhaust available memory.
- Scaling path: Stream output to disk with windowed in-memory view.

**History Storage:**
- Current capacity: JSON files on disk, one per run, stored in `%LocalAppData%\SSH_Helper\history\`.
- Limit: No automatic cleanup of old history files. Over time, hundreds of large JSON files can accumulate. Index file (`history.index.json`) is loaded entirely into memory.
- Scaling path: Add automatic history rotation (delete runs older than N days). Consider SQLite for history storage.

**Connection Pool:**
- Current capacity: Unbounded number of connections (one per unique host+username pair).
- Limit: No maximum pool size. Large host lists could exhaust system sockets or SSH connection limits on target infrastructure.
- Scaling path: Add configurable `MaxPoolSize` with LRU eviction.

## Dependencies at Risk

**SSH.NET (2024.1.0):**
- Risk: The project now primarily uses Rebex.SshShell for SSH operations, but SSH.NET remains as a dependency. It is unclear how much SSH.NET is still actively used versus Rebex.
- Impact: Unnecessary binary size and potential confusion about which SSH library to use for new features.
- Migration plan: Audit usage of SSH.NET imports. If fully superseded by Rebex, remove the dependency. If still used for specific operations, document which library handles what.

**Newtonsoft.Json (13.0.3):**
- Risk: .NET 8 ships with `System.Text.Json`. Maintaining both serializers increases complexity.
- Impact: Larger binary, two serialization patterns to maintain.
- Migration plan: Gradually migrate to `System.Text.Json` starting with new code, then migrate existing serialization.

## Missing Critical Features

**No Dependency Injection Container:**
- Problem: Services are instantiated directly in `Form1` constructor with `new`. No DI container or service locator.
- Blocks: Unit testing Form1, swapping implementations, lifecycle management.

**No Logging Framework:**
- Problem: No structured logging. Debug output goes through SSH service events with `[DEBUG]` prefix. Errors are shown via `DialogTheme.Show()` message boxes.
- Blocks: Post-mortem debugging, remote diagnostics, log-level filtering.

## Test Coverage Gaps

**Form1.cs (10,059 lines - 0% coverage):**
- What's not tested: All UI logic, event handlers, state management, execution orchestration, history management, CSV grid operations, preset selection, theme application, find/replace, clipboard operations.
- Files: `Form1.cs`
- Risk: Any refactoring of Form1 has zero safety net. State flag bugs, event ordering issues, and edge cases in UI workflows are all undetected.
- Priority: High - this is the largest file and the application entry point.

**SshConnectionPool.cs (674 lines - 0% unit test coverage):**
- What's not tested: Connection creation, health checking, lease/release, stale cleanup, keep-alive sweep, concurrent access patterns.
- Files: `Services/SshConnectionPool.cs`
- Risk: Connection leaks, deadlocks, or stale connections could cause silent failures in production.
- Priority: High - core infrastructure.

**SshShellSession.cs (1,347 lines - 0% unit test coverage):**
- What's not tested: Shell initialization, command sending, output reading, prompt detection, pager handling.
- Files: `Services/SshShellSession.cs`
- Risk: Terminal interaction bugs are hard to reproduce and debug without tests.
- Priority: High - core SSH execution path.

**InteractiveTerminalService.cs (3,433 lines - minimal coverage):**
- What's not tested: Most of the interactive terminal logic, only transcript filtering has tests.
- Files: `Services/Terminal/InteractiveTerminalService.cs`
- Risk: Interactive terminal feature is complex and largely untested.
- Priority: Medium.

**SettingsDialog.cs (1,363 lines - minimal coverage):**
- What's not tested: Most settings UI logic. Only appearance tests exist (`SettingsDialogAppearanceTests`).
- Files: `SettingsDialog.cs`
- Risk: Settings changes could break configuration without detection.
- Priority: Low - settings rarely change once set up.

**Dialog Forms (EnvironmentDialog, FolderExecutionDialog, ExecutionDetailsDialog, etc.):**
- What's not tested: Most dialog interaction logic.
- Files: `EnvironmentDialog.cs` (869 lines), `FolderExecutionDialog.cs`, `ExecutionDetailsDialog.cs` (789 lines)
- Risk: Dialog bugs affect user workflows but are lower risk than core execution.
- Priority: Low.

---

*Concerns audit: 2026-03-06*
