# Architecture

**Analysis Date:** 2026-03-07

## Pattern Overview

**Overall:** Service-oriented WinForms application with event-driven UI communication

**Key Characteristics:**
- Thick client: single WinForms window (`Form1`) orchestrates all UI; services handle business logic
- Manual dependency wiring (no DI container) -- services instantiated in `Form1` constructor
- Event-driven: services raise events (`ProgressChanged`, `OutputReceived`, `ColumnUpdateRequested`) that `Form1` subscribes to for UI updates
- YAML-based scripting engine with command pattern for extensible script steps (37 command files)
- Async SSH execution with cancellation support via `CancellationTokenSource`
- Dual SSH library strategy: Rebex (primary, scripting API / terminal emulation) + SSH.NET (legacy/fallback)

## Layers

**UI Layer (Forms & Dialogs):**
- Purpose: All WinForms UI code, event handlers, visual theming
- Location: Root-level dialog files + `Forms/` + `UI/`
- Key files:
  - `Form1.cs` (~10,471 lines) -- main window, organized into #region blocks
  - `Form1.Designer.cs` (~2,315 lines) -- auto-generated layout
  - `SettingsDialog.cs` (~1,363 lines) -- application settings
  - `EnvironmentDialog.cs` (~869 lines) -- environment profile management
  - `ExecutionDetailsDialog.cs` -- execution history details viewer
  - `FindDialog.cs` -- modeless text search
  - `FolderExecutionDialog.cs` -- batch folder execution options
  - `AboutDialog.cs` -- version info
  - `UpdateDialog.cs` -- auto-update UI
  - `Forms/InteractiveTerminalForm.cs` -- interactive SSH terminal window
  - `UI/ScintillaScriptEditorControl.cs` -- Scintilla-based YAML editor with syntax highlighting
  - `UI/InteractiveTerminalViewportControl.cs` -- terminal emulation viewport
  - `UI/DialogTheme.cs` -- centralized dark/light theme application
  - `UI/UnsavedPresetDiffDialog.cs` -- visual diff for unsaved changes
  - `UI/MemoryDebuggerDialog.cs` -- diagnostic memory viewer
- Contains: Form classes, custom controls, theme application, event wiring
- Depends on: Services, Models, Utilities
- Used by: `Program.cs` (entry point)

**Service Layer:**
- Purpose: Core business logic, external I/O (SSH, file system, GitHub API)
- Location: `Services/`
- Contains: SSH execution, configuration persistence, preset management, CSV handling, environment profiles, history storage, auto-update, credential management, scripting engine, editor intelligence
- Depends on: Models, Utilities, SSH.NET, Rebex, Newtonsoft.Json, YamlDotNet
- Used by: UI Layer
- Key files:
  - `Services/SshExecutionService.cs` (~1,834 lines) -- core SSH execution engine
  - `Services/SshConnectionPool.cs` -- connection pooling with health checks
  - `Services/SshShellSession.cs` -- individual Rebex shell session management
  - `Services/ConfigurationService.cs` (~468 lines) -- JSON config persistence with compression
  - `Services/PresetManager.cs` (~876 lines) -- preset CRUD, folders, import/export
  - `Services/EnvironmentService.cs` -- named environment profile switching
  - `Services/ExecutionCoordinator.cs` (57 lines) -- thin orchestrator for execution prep
  - `Services/CsvManager.cs` -- CSV import/export for host grid
  - `Services/HistoryStorageService.cs` -- external per-run history file management
  - `Services/HistoryResultStore.cs`, `Services/HistoryIdGenerator.cs` -- history helpers
  - `Services/UpdateService.cs` -- GitHub release API auto-update
  - `Services/SshConfigService.cs` -- `~/.ssh/config` file integration
  - `Services/SshTerminalOptionsFactory.cs` -- terminal options builder
  - `Services/SshTimeoutOptions.cs` -- timeout configuration
  - `Services/StopOnFirstErrorTracker.cs` -- multi-host error abort logic

**Scripting Engine (Sub-layer of Services):**
- Purpose: YAML script parsing, execution, and command dispatch
- Location: `Services/Scripting/`
- Contains: Parser, executor, context, 35 command implementations (37 files including interface and helpers), expression evaluator, config parsers, validation formatter
- Depends on: Models, `IScriptCommand` interface, `ScriptContext`
- Used by: `SshExecutionService`
- Key files:
  - `Services/Scripting/ScriptParser.cs` -- YAML to `Script` object using YamlDotNet
  - `Services/Scripting/ScriptExecutor.cs` (336 lines) -- dispatches to commands via `Dictionary<StepType, IScriptCommand>`
  - `Services/Scripting/ScriptContext.cs` -- runtime variable store, session reference, events
  - `Services/Scripting/ExpressionEvaluator.cs` -- conditional expression evaluation
  - `Services/Scripting/ValueResolver.cs` -- property resolution (`.length`)
  - `Services/Scripting/JsonPathNavigator.cs`, `Services/Scripting/JsonUtilities.cs` -- JSON handling
  - `Services/Scripting/ScriptDependencyAnalyzer.cs` -- variable dependency analysis
  - `Services/Scripting/ScriptFileAccessValidator.cs` -- file access safety
  - `Services/Scripting/ScriptValidationFormatter.cs` -- human-readable validation output
  - `Services/Scripting/ScriptRegexDefaults.cs` -- shared regex patterns

**Editor Intelligence (Sub-layer of Services):**
- Purpose: IDE-like features for the script editor
- Location: `Services/Editor/`
- Contains: Autocomplete, inline validation, syntax highlighting, diagnostics
- Key files:
  - `Services/Editor/ScriptAutocompleteProvider.cs` -- command/variable completion
  - `Services/Editor/ScriptEditorValidationService.cs` -- real-time YAML diagnostics
  - `Services/Editor/YamlSshSyntaxHighlighter.cs` -- Scintilla syntax coloring
  - `Services/Editor/EditorDiagnostic.cs` -- diagnostic model
  - `Services/Editor/EditorTextUtilities.cs` -- text manipulation helpers

**Credential Layer (Sub-layer of Services):**
- Purpose: Secure credential storage abstraction
- Location: `Services/Credentials/`
- Key files:
  - `Services/Credentials/ICredentialProvider.cs` -- interface
  - `Services/Credentials/CredentialManagerProvider.cs` -- Windows Credential Manager via P/Invoke
  - `Services/Credentials/CredentialTargets.cs` -- target name generation

**Model Layer:**
- Purpose: Data transfer objects, configuration shape, domain models
- Location: `Models/`
- Contains: `AppConfiguration`, `PresetInfo`, `HostConnection`, `ExecutionResult`, `EnvironmentConfig`, history models, SSH config models, folder/execution models
- Depends on: Nothing (pure data, except `PresetInfo.Type` references `ScriptParser` for auto-detection)
- Used by: All other layers

**Utility Layer:**
- Purpose: Stateless helper functions, formatters, validators
- Location: `Utilities/`
- Contains: Terminal output processing, prompt detection, input validation, CSV sync evaluation, diff building, indicator formatters, SSH config parsing
- Depends on: Models (some formatters reference model types)
- Used by: Services, UI

## Data Flow

**SSH Command Execution (Simple Preset):**

1. User selects hosts in DataGridView and enters commands in script editor
2. `Form1` builds `HostConnection` list from grid rows, calls `ExecutionCoordinator.PrepareExecution()`
3. `ExecutionCoordinator` creates `PresetInfo` + `SshTimeoutOptions`, returns `ExecutionPreparation`
4. `Form1` calls `ExecutionCoordinator.ExecutePresetAsync()` which delegates to `SshExecutionService.ExecutePresetAsync()`
5. `SshExecutionService` iterates hosts sequentially, connects via `SshConnectionPool` (Rebex), sends commands through `SshShellSession`
6. Progress/output events fire back to `Form1` via `ProgressChanged` and `OutputReceived` events
7. `Form1` appends output to RichTextBox on UI thread via `BeginInvoke`, throttled by `OutputThrottler`
8. Results stored as `List<ExecutionResult>`, added to history via `HistoryStorageService`

**YAML Script Execution:**

1. `SshExecutionService` detects YAML script via `ScriptParser.IsYamlScript()` (checks for `steps:` header)
2. `ScriptParser.Parse()` converts YAML text to `Script` object (list of `ScriptStep`) using YamlDotNet
3. `ScriptExecutor.ExecuteAsync()` iterates steps, dispatching to registered `IScriptCommand` handlers
4. `ScriptContext` holds variables, SSH session reference, output callbacks, column update events
5. Commands can:
   - Send SSH commands (`SendCommand`)
   - Extract output via regex (`ExtractCommand`)
   - Control flow (`IfCommand`, `ForeachCommand`, `WhileCommand`, `SwitchCommand`)
   - Interact with user (`ChooseCommand`, `InputCommand`, `ConfirmCommand`, `MultiselectCommand`)
   - Modify grid data (`UpdateColumnCommand`, `UpdateEnvironmentCommand`)
   - Network operations (`PingCommand`, `DnsCommand`, `HttpCommand`, `PortcheckCommand`, `WebhookCommand`)
   - File I/O (`ReadFileCommand`, `WriteFileCommand`, `SftpCommand`)
   - Parse device output (`ParseCommand` + `FortiGateParser`)
   - Parallel execution (`ParallelCommand`)
   - Tabular output (`TableCommand`)
   - Assertions (`AssertCommand`)

**Environment Switching:**

1. User selects environment from dropdown or script declares `environment:` header
2. `EnvironmentService.SwitchEnvironment()` saves current grid state to `EnvironmentConfig`, loads target environment
3. `EnvironmentChanged` event fires, `Form1` repopulates DataGridView with new host data
4. Presets can declare an environment via `Script.Environment` property; `PresetBaseEnvironmentResolver` and `PresetEnvironmentLoadPlanner` determine which environment to load
5. Folders can have a `BaseEnvironment` that child presets inherit from

**Configuration Persistence:**

1. `ConfigurationService` manages `%LocalAppData%\SSH_Helper\config.json`
2. `AppConfiguration` is the root object containing presets, environments, window state, font settings, editor settings, theme, credential settings, SSH config settings, recent files, update settings
3. `SavedState` captures full grid state; compressed to `SavedStateCompressed` (GZip + Base64, prefixed with `gz64:`) for size
4. History stored externally: `history.index.json` + per-run payload files in `history/` subfolder via `HistoryStorageService`
5. Config is cached in `_cachedConfig` for performance; `GetCurrent()` returns cache, `Load()` reads from disk
6. Legacy config migration: string presets auto-migrated to `PresetInfo` objects

**State Management:**
- Application state persisted to `%LocalAppData%\SSH_Helper\config.json` via `ConfigurationService`
- State includes: presets, environments, window layout, font settings, editor settings, theme, credential preferences, SSH config integration, recent files
- `SavedState`/`SavedStateCompressed` captures full grid state for session restore
- History stored externally: `history.index.json` + per-run payload files in `history/` subfolder
- `EnvironmentService` manages named environment profiles (different host grids for dev/staging/prod)
- Presets organized into nested folders (`PresetInfo.Folder` supports `"Network/Cisco/Switches"` paths)

## Key Abstractions

**IScriptCommand:**
- Purpose: Uniform interface for all script step handlers
- Examples: `Services/Scripting/Commands/SendCommand.cs`, `Services/Scripting/Commands/ExtractCommand.cs`, `Services/Scripting/Commands/IfCommand.cs`
- Pattern: Command pattern -- `ScriptExecutor` maintains `Dictionary<StepType, IScriptCommand>` and dispatches by step type
- Contract: `Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken ct)`
- `CommandResult` carries: `Success`, `Message`, `ShouldExit`, `ShouldBreak`, `ShouldContinue`, `SuppressedError`
- Factory methods: `CommandResult.Ok()`, `CommandResult.Fail()`, `CommandResult.Exit()`, `CommandResult.Break()`, `CommandResult.Continue()`, `CommandResult.Suppressed()`

**ScriptContext:**
- Purpose: Shared execution state passed through script commands
- Location: `Services/Scripting/ScriptContext.cs`
- Contains: Variable store, SSH session reference, output event, column/environment update events, debug state, cancel token, host connection info
- Pattern: Mutable context bag threaded through command chain
- Events: `OutputReceived`, `ColumnUpdateRequested`, `EnvironmentVariableUpdateRequested`

**IScriptEditor:**
- Purpose: Abstraction over the script editor control for testability
- Location: `UI/IScriptEditor.cs`
- Implementation: `UI/ScintillaScriptEditorControl.cs` (Scintilla-based rich editor with syntax highlighting, autocomplete, inline diagnostics)
- Pattern: Interface segregation -- `Form1` programs against the interface
- Methods: `Text`, `SetDiagnostics()`, `ClearDiagnostics()`, `GetCaretPosition()`, `AsControl()`

**ICredentialProvider:**
- Purpose: Abstraction for credential storage backends
- Location: `Services/Credentials/ICredentialProvider.cs`
- Implementation: `Services/Credentials/CredentialManagerProvider.cs` (Windows Credential Manager via P/Invoke)
- Pattern: Strategy pattern for swappable credential backends
- Methods: `TryGetPassword()`, `SavePassword()`, `DeletePassword()`, `IsAvailable`

**SshConnectionPool:**
- Purpose: Reusable SSH connection management with health checks
- Location: `Services/SshConnectionPool.cs`
- Pattern: Object pool with idle keepalive timer (5s sweep interval), automatic stale connection cleanup, lease tracking via `ConcurrentDictionary`
- Uses Rebex `Ssh` and `TerminalEmulation` for connections

**SshShellSession:**
- Purpose: Individual interactive shell session wrapping Rebex Scripting API
- Location: `Services/SshShellSession.cs`
- Pattern: Encapsulates Rebex terminal scripting with pattern-based prompt matching, automatic pager handling, real-time output streaming via events (`ShellOutputEventArgs`, `ShellCommandCompletedEventArgs`)

**IConfigParser:**
- Purpose: Pluggable network device output parsers
- Location: `Services/Scripting/Parsers/IConfigParser.cs`
- Implementation: `Services/Scripting/Parsers/FortiGateParser.cs`
- Factory: `Services/Scripting/Parsers/ParserFactory.cs`
- Pattern: Strategy pattern selected via `parse` script command

**PresetInfo / PresetType:**
- Purpose: Represents a saved command preset with auto-detected type
- Location: `Models/PresetInfo.cs`
- Pattern: `PresetInfo.Type` auto-detects `Simple` vs `YamlScript` via `ScriptParser.IsYamlScript()`
- Supports nested folders via `Folder` property (e.g., `"Network/Cisco/Switches"`)

## Entry Points

**Application Entry:**
- Location: `Program.cs`
- Triggers: Windows application launch
- Responsibilities: Rebex license initialization (assembly metadata > local `rebex.key` file > env var `REBEX_LICENSE_KEY`), Scintilla native DLL bootstrap via `ScintillaNativeBootstrap.ConfigureSatelliteDirectory()`, `Application.Run(new Form1())`

**Form1 Constructor:**
- Location: `Form1.cs` (`#region Constructor`, line ~217)
- Triggers: `Program.Main()` creates `new Form1()`
- Responsibilities: Instantiates all services (`ConfigurationService`, `SshExecutionService`, `PresetManager`, `CsvManager`, `EnvironmentService`, `ExecutionCoordinator`, `UpdateService`, `SshConfigService`, `HistoryStorageService`), wires event handlers, loads saved state, applies theme

**SSH Execution Entry:**
- Location: `Form1.cs` `#region SSH Execution` (line ~8491)
- Triggers: Execute button click or folder execution dialog
- Responsibilities: Validates inputs, builds host list from DataGridView, delegates to `ExecutionCoordinator`

**Script Execution Chain:**
- `SshExecutionService.ExecutePresetAsync()` -> detects YAML -> `ScriptParser.Parse()` -> `ScriptExecutor.ExecuteAsync()` -> `IScriptCommand.ExecuteAsync()` per step

## Error Handling

**Strategy:** Exception-based with UI-level catch-and-display

**Patterns:**
- Services throw exceptions for unrecoverable errors; `Form1` catches and shows `MessageBox`
- `ConfigurationService.Load()` catches corrupt config, creates backup `.corrupt` file, returns default config, sets `ConfigLoadError` for UI warning
- `SshExecutionService` catches per-host SSH errors, wraps in `ExecutionResult.ErrorMessage`, continues to next host
- Script execution uses `CommandResult.Fail()` for recoverable failures; `TryCommand` provides script-level try/catch error suppression
- `StopOnFirstErrorTracker` (`Services/StopOnFirstErrorTracker.cs`) tracks whether to abort multi-host execution on first failure
- `ScriptStep.ParseErrors` collects parser-level validation issues per step
- `CommandResult.Suppressed()` carries error info while allowing execution to continue (e.g., `on_error: continue`)

## Cross-Cutting Concerns

**Logging:** No formal logging framework. Script `log` command writes to file via `LogCommand`. `UpdateService` has optional file-based logging (`EnableUpdateLog`). Otherwise debug output via events.

**Validation:**
- `Utilities/InputValidator.cs` -- centralized validation for IPs, hostnames, ports, timeouts, column names
- `Services/Editor/ScriptEditorValidationService.cs` -- validates YAML scripts in the editor with inline diagnostics
- `Services/Scripting/ScriptFileAccessValidator.cs` -- validates file access paths in scripts
- `Services/Scripting/ScriptDependencyAnalyzer.cs` -- analyzes variable dependencies

**Authentication:** SSH authentication via password or private key (`HostConnection.IdentityFile`). Optional Windows Credential Manager integration (`ICredentialProvider`). Optional SSH agent (`CredentialSettings.PreferSshAgent`). SSH config file parsing for per-host settings (`SshConfigService` + `Utilities/SshConfigParser.cs`). Host key algorithms and ciphers configurable per-host via `HostConnection.HostKeyAlgorithms` and `HostConnection.Ciphers`.

**Theming:** Dark mode support via `NativeMethods` P/Invoke for Windows dark scrollbars and title bar (`DwmSetWindowAttribute`, `SetPreferredAppMode`). `UI/DialogTheme.cs` applies consistent theming across all dialogs recursively. `AppConfiguration.DarkMode` controls toggle. VS Code-inspired dark color palette. Custom accent color support via `FontSettings.CustomAccentColor`.

**Output Processing:** `Utilities/TerminalOutputProcessor.cs` normalizes raw terminal output (ANSI escape codes, CR/LF, backspace, TAB, CSI commands, pager artifacts). `Utilities/PromptDetector.cs` detects shell prompts via adaptive regex for command completion detection.

**Output Throttling:** `Utilities/OutputThrottler.cs` rate-limits UI updates during high-throughput SSH output (50ms throttle constant `UiOutputThrottleMs`) to prevent UI thread saturation.

---

*Architecture analysis: 2026-03-07*
