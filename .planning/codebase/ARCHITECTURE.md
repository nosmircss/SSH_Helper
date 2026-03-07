# Architecture

**Analysis Date:** 2026-03-06

## Pattern Overview

**Overall:** Service-oriented WinForms application with event-driven UI communication

**Key Characteristics:**
- Thick client: single WinForms window (`Form1`) orchestrates all UI; services handle business logic
- Manual dependency wiring (no DI container) -- services instantiated in `Form1` constructor
- Event-driven: services raise events (`ProgressChanged`, `OutputReceived`, `ColumnUpdateRequested`) that Form1 subscribes to for UI updates
- YAML-based scripting engine with command pattern for extensible script steps
- Async SSH execution with cancellation support via `CancellationTokenSource`

## Layers

**UI Layer (Forms & Dialogs):**
- Purpose: All WinForms UI code, event handlers, visual theming
- Location: `Form1.cs`, `FindDialog.cs`, `AboutDialog.cs`, `SettingsDialog.cs`, `EnvironmentDialog.cs`, `ExecutionDetailsDialog.cs`, `FolderExecutionDialog.cs`, `UpdateDialog.cs`
- Location: `Forms/InteractiveTerminalForm.cs`
- Location: `UI/ScintillaScriptEditorControl.cs`, `UI/InteractiveTerminalViewportControl.cs`, `UI/DialogTheme.cs`, `UI/UnsavedPresetDiffDialog.cs`, `UI/MemoryDebuggerDialog.cs`
- Contains: Form classes, custom controls, theme application, event wiring
- Depends on: Services, Models, Utilities
- Used by: `Program.cs` (entry point)

**Service Layer:**
- Purpose: Core business logic, external I/O (SSH, file system, GitHub API)
- Location: `Services/`
- Contains: SSH execution, configuration persistence, preset management, CSV handling, environment profiles, history storage, auto-update, credential management, scripting engine
- Depends on: Models, Utilities, SSH.NET, Rebex, Newtonsoft.Json, YamlDotNet
- Used by: UI Layer

**Scripting Engine (Sub-layer of Services):**
- Purpose: YAML script parsing, execution, and command dispatch
- Location: `Services/Scripting/`
- Contains: Parser (`ScriptParser.cs`), executor (`ScriptExecutor.cs`), context (`ScriptContext.cs`), 30+ command implementations, expression evaluator, config parsers
- Depends on: Models, `IScriptCommand` interface, `ScriptContext`
- Used by: `SshExecutionService`

**Model Layer:**
- Purpose: Data transfer objects, configuration shape, domain models
- Location: `Models/`
- Contains: `AppConfiguration`, `PresetInfo`, `HostConnection`, `ExecutionResult`, `EnvironmentConfig`, `ScriptStep`, `Script`, history models
- Depends on: Nothing (pure data)
- Used by: All other layers

**Utility Layer:**
- Purpose: Stateless helper functions, formatters, validators
- Location: `Utilities/`
- Contains: Terminal output processing, prompt detection, input validation, CSV sync evaluation, diff building, indicator formatters
- Depends on: Models (some formatters reference model types)
- Used by: Services, UI

## Data Flow

**SSH Command Execution (Simple Preset):**

1. User selects hosts in DataGridView and enters commands in script editor
2. `Form1` builds `HostConnection` list from grid rows, calls `ExecutionCoordinator.PrepareExecution()`
3. `ExecutionCoordinator` creates `PresetInfo` + `SshTimeoutOptions`, returns `ExecutionPreparation`
4. `Form1` calls `ExecutionCoordinator.ExecutePresetAsync()` which delegates to `SshExecutionService.ExecutePresetAsync()`
5. `SshExecutionService` iterates hosts sequentially, connects via `SshConnectionPool`, sends commands
6. Progress/output events fire back to `Form1` via `ProgressChanged` and `OutputReceived` events
7. `Form1` appends output to RichTextBox on UI thread via `BeginInvoke`
8. Results stored as `List<ExecutionResult>`, added to history via `HistoryStorageService`

**YAML Script Execution:**

1. `SshExecutionService` detects YAML script via `ScriptParser.IsYamlScript()`
2. `ScriptParser.Parse()` converts YAML text to `Script` object (list of `ScriptStep`)
3. `ScriptExecutor.ExecuteAsync()` iterates steps, dispatching to registered `IScriptCommand` handlers
4. `ScriptContext` holds variables, SSH session reference, output callbacks, column update events
5. Commands can: send SSH commands (`SendCommand`), extract output (`ExtractCommand`), control flow (`IfCommand`, `ForeachCommand`, `WhileCommand`), interact with user (`ChooseCommand`, `InputCommand`, `ConfirmCommand`), modify grid data (`UpdateColumnCommand`), do network ops (`PingCommand`, `DnsCommand`, `HttpCommand`)

**State Management:**
- Application state persisted to `%LocalAppData%\SSH_Helper\config.json` via `ConfigurationService`
- State includes: presets, environments, window layout, font settings, editor settings, theme
- `SavedState`/`SavedStateCompressed` captures full grid state for session restore
- History stored externally: `history.index.json` + per-run payload files in `history/` subfolder
- `EnvironmentService` manages named environment profiles (different host grids for dev/staging/prod)

## Key Abstractions

**IScriptCommand:**
- Purpose: Uniform interface for all script step handlers
- Examples: `Services/Scripting/Commands/SendCommand.cs`, `Services/Scripting/Commands/ExtractCommand.cs`, `Services/Scripting/Commands/IfCommand.cs`
- Pattern: Command pattern -- `ScriptExecutor` maintains `Dictionary<StepType, IScriptCommand>` and dispatches by step type
- Contract: `Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken ct)`

**ScriptContext:**
- Purpose: Shared execution state passed through script commands
- Location: `Services/Scripting/ScriptContext.cs`
- Contains: Variable store, SSH session reference, output event, column/environment update events, debug state, cancel token
- Pattern: Mutable context bag threaded through command chain

**IScriptEditor:**
- Purpose: Abstraction over the script editor control for testability
- Location: `UI/IScriptEditor.cs`
- Implementations: `UI/ScintillaScriptEditorControl.cs` (Scintilla-based rich editor)
- Pattern: Interface segregation -- Form1 programs against the interface

**ICredentialProvider:**
- Purpose: Abstraction for credential storage backends
- Location: `Services/Credentials/ICredentialProvider.cs`
- Implementation: `Services/Credentials/CredentialManagerProvider.cs` (Windows Credential Manager)
- Pattern: Strategy pattern for swappable credential backends

**SshConnectionPool:**
- Purpose: Reusable SSH connection management with health checks
- Location: `Services/SshConnectionPool.cs`
- Pattern: Object pool with idle keepalive and automatic stale connection cleanup

## Entry Points

**Application Entry:**
- Location: `Program.cs`
- Triggers: Windows application launch
- Responsibilities: Rebex license initialization, Scintilla native DLL bootstrap, `Application.Run(new Form1())`

**Form1 Constructor:**
- Location: `Form1.cs` (line ~201, `#region Constructor`)
- Triggers: `Program.Main()` creates `new Form1()`
- Responsibilities: Instantiates all services (`ConfigurationService`, `SshExecutionService`, `PresetManager`, `CsvManager`, `EnvironmentService`, `ExecutionCoordinator`, `UpdateService`, `SshConfigService`, `HistoryStorageService`), wires event handlers, loads saved state

**SSH Execution Entry:**
- Location: `Form1.cs` `#region SSH Execution` (line ~8079)
- Triggers: Execute button click
- Responsibilities: Validates inputs, builds host list, delegates to `ExecutionCoordinator`

## Error Handling

**Strategy:** Exception-based with UI-level catch-and-display

**Patterns:**
- Services throw exceptions for unrecoverable errors; `Form1` catches and shows `MessageBox`
- `ConfigurationService.Load()` catches corrupt config, creates backup `.corrupt` file, returns default config, sets `ConfigLoadError` for UI warning
- `SshExecutionService` catches per-host SSH errors, wraps in `ExecutionResult.ErrorMessage`, continues to next host
- Script execution uses `CommandResult.Fail()` for recoverable failures; `try/catch` commands for error suppression
- `StopOnFirstErrorTracker` (`Services/StopOnFirstErrorTracker.cs`) tracks whether to abort multi-host execution on first failure

## Cross-Cutting Concerns

**Logging:** No formal logging framework. Script `log` command writes to file. `UpdateService` has optional file-based logging (`EnableUpdateLog`). Otherwise debug output via events.

**Validation:** `Utilities/InputValidator.cs` provides centralized validation for IPs, ports, timeouts, column names. `Services/Editor/ScriptEditorValidationService.cs` validates YAML scripts in the editor with inline diagnostics.

**Authentication:** SSH authentication via password or private key (`HostConnection.IdentityFile`). Optional Windows Credential Manager integration (`ICredentialProvider`). Optional SSH agent (`CredentialSettings.PreferSshAgent`). SSH config file parsing for per-host settings (`SshConfigService`, `Utilities/SshConfigParser.cs`).

**Theming:** Dark mode support via `NativeMethods` P/Invoke for Windows dark scrollbars. `UI/DialogTheme.cs` applies consistent theming across all dialogs. `AppConfiguration.DarkMode` controls toggle.

**Output Processing:** `Utilities/TerminalOutputProcessor.cs` normalizes raw terminal output (ANSI codes, CR/LF, pager artifacts). `Utilities/PromptDetector.cs` detects shell prompts for command completion.

---

*Architecture analysis: 2026-03-06*
