# Codebase Structure

**Analysis Date:** 2026-03-07

## Directory Layout

```
SSH_Helper/
├── Models/                          # Data transfer objects and config models
│   ├── AppConfiguration.cs          # Root config with all settings classes (477 lines)
│   ├── ConnectionTestResult.cs      # Connection test outcome
│   ├── CsvFileFingerprint.cs        # File identity for freshness detection
│   ├── EnvironmentConfig.cs         # Named environment profile (dev/staging/prod)
│   ├── ExecutionDetails.cs          # Execution metadata for history viewer
│   ├── ExecutionResult.cs           # Per-host execution result
│   ├── FolderExecutionOptions.cs    # Batch folder execution config
│   ├── FolderExecutionProgress.cs   # Folder execution progress tracking
│   ├── FolderInfo.cs                # Preset folder metadata
│   ├── HistoryIndex.cs              # History index entry
│   ├── HistoryListItem.cs           # History list display item
│   ├── HistoryRunPayload.cs         # Per-run history data
│   ├── HostConnection.cs            # SSH host connection details
│   ├── PresetInfo.cs                # Command preset with auto-type detection
│   ├── SshConfigFile.cs             # SSH config file model
│   └── SshHostConfig.cs             # Per-host SSH config
├── Services/                        # Business logic layer
│   ├── ConfigurationService.cs      # JSON config persistence (~468 lines)
│   ├── CsvManager.cs                # CSV import/export for host grid
│   ├── EnvironmentService.cs        # Environment profile switching
│   ├── ExecutionCoordinator.cs      # Thin execution orchestrator (57 lines)
│   ├── HistoryIdGenerator.cs        # Unique history ID generation
│   ├── HistoryResultStore.cs        # History result caching
│   ├── HistoryStorageService.cs     # External history file management
│   ├── PresetManager.cs             # Preset CRUD, folders, import/export (~876 lines)
│   ├── SshConfigService.cs          # ~/.ssh/config integration
│   ├── SshConnectionPool.cs         # Connection pooling with health checks
│   ├── SshExecutionService.cs       # Core SSH execution engine (~1,834 lines)
│   ├── SshShellSession.cs           # Rebex shell session wrapper
│   ├── SshTerminalOptionsFactory.cs # Terminal options builder
│   ├── SshTimeoutOptions.cs         # Timeout configuration record
│   ├── StopOnFirstErrorTracker.cs   # Multi-host error abort logic
│   ├── UpdateService.cs             # GitHub release auto-update
│   ├── Credentials/                 # Credential storage abstraction
│   │   ├── ICredentialProvider.cs   # Interface
│   │   ├── CredentialManagerProvider.cs # Windows Credential Manager
│   │   └── CredentialTargets.cs     # Target name generation
│   ├── Editor/                      # Script editor intelligence
│   │   ├── EditorDiagnostic.cs      # Diagnostic model
│   │   ├── EditorTextUtilities.cs   # Text manipulation helpers
│   │   ├── ScriptAutocompleteProvider.cs # Command/variable completion
│   │   ├── ScriptEditorValidationService.cs # Real-time YAML diagnostics
│   │   └── YamlSshSyntaxHighlighter.cs # Scintilla syntax coloring
│   ├── Scripting/                   # YAML scripting engine
│   │   ├── ExpressionEvaluator.cs   # Conditional expression evaluation
│   │   ├── JsonPathNavigator.cs     # JSON path traversal
│   │   ├── JsonUtilities.cs         # JSON helpers
│   │   ├── ScriptContext.cs         # Runtime variable store and events
│   │   ├── ScriptDependencyAnalyzer.cs # Variable dependency analysis
│   │   ├── ScriptExecutor.cs        # Step dispatcher (336 lines)
│   │   ├── ScriptFileAccessValidator.cs # File access safety
│   │   ├── ScriptParser.cs          # YAML to Script object
│   │   ├── ScriptRegexDefaults.cs   # Shared regex patterns
│   │   ├── ScriptValidationFormatter.cs # Validation output formatting
│   │   ├── ValueResolver.cs         # Property resolution (.length)
│   │   ├── Commands/                # 37 files: IScriptCommand + implementations
│   │   │   ├── IScriptCommand.cs    # Command interface + CommandResult
│   │   │   ├── SendCommand.cs       # SSH command execution
│   │   │   ├── SetCommand.cs        # Variable operations
│   │   │   ├── ExtractCommand.cs    # Regex capture
│   │   │   ├── IfCommand.cs         # Conditional branching
│   │   │   ├── ForeachCommand.cs    # Collection iteration
│   │   │   ├── WhileCommand.cs      # While loops
│   │   │   ├── SwitchCommand.cs     # Switch/case
│   │   │   ├── TryCommand.cs        # Error handling
│   │   │   ├── ParallelCommand.cs   # Parallel execution
│   │   │   ├── InteractiveCommand.cs # Terminal handoff
│   │   │   ├── HttpCommand.cs       # HTTP requests
│   │   │   ├── PingCommand.cs       # ICMP ping
│   │   │   ├── DnsCommand.cs        # DNS lookup
│   │   │   ├── PortcheckCommand.cs  # TCP port check
│   │   │   ├── SftpCommand.cs       # SFTP file transfer
│   │   │   ├── WebhookCommand.cs    # Webhook dispatch
│   │   │   ├── ParseCommand.cs      # Device output parsing
│   │   │   ├── TableCommand.cs      # Tabular output
│   │   │   ├── AssertCommand.cs     # Assertions
│   │   │   ├── LogCommand.cs        # File logging
│   │   │   ├── PrintCommand.cs      # Output messages
│   │   │   ├── WaitCommand.cs       # Delay
│   │   │   ├── ExitCommand.cs       # Script termination
│   │   │   ├── BreakCommand.cs      # Loop break
│   │   │   ├── ContinueCommand.cs   # Loop continue
│   │   │   ├── InputCommand.cs      # User text input
│   │   │   ├── ChooseCommand.cs     # Single-choice dialog
│   │   │   ├── MultiselectCommand.cs # Multi-choice dialog
│   │   │   ├── ConfirmCommand.cs    # Yes/no confirmation
│   │   │   ├── ReadFileCommand.cs   # Local file read
│   │   │   ├── WriteFileCommand.cs  # Local file write
│   │   │   ├── UpdateColumnCommand.cs # Grid column update
│   │   │   ├── UpdateEnvironmentCommand.cs # Environment variable update
│   │   │   ├── ChoiceOptionResolver.cs # Choice option parsing helper
│   │   │   ├── ScriptPromptDialogRunner.cs # Dialog runner helper
│   │   │   └── JsonFunctions.cs     # JSON helper functions
│   │   ├── Models/                  # Script data models
│   │   │   ├── Script.cs            # Parsed script document
│   │   │   ├── ScriptStep.cs        # Individual step with StepType enum
│   │   │   └── DebugState.cs        # Debug execution state
│   │   └── Parsers/                 # Network device output parsers
│   │       ├── IConfigParser.cs     # Parser interface
│   │       ├── FortiGateParser.cs   # FortiGate config parser
│   │       └── ParserFactory.cs     # Parser selection factory
│   └── Terminal/                    # Interactive terminal
│       └── InteractiveTerminalService.cs # Rebex terminal emulation
├── Forms/                           # Secondary form windows
│   └── InteractiveTerminalForm.cs   # Interactive SSH terminal UI
├── UI/                              # Custom controls, interfaces, theming
│   ├── IScriptEditor.cs             # Script editor interface
│   ├── ScintillaScriptEditorControl.cs # Scintilla editor implementation
│   ├── InteractiveTerminalViewportControl.cs # Terminal viewport
│   ├── DialogTheme.cs               # Dark/light theme application
│   ├── UnsavedPresetDiffDialog.cs   # Visual diff dialog
│   └── MemoryDebuggerDialog.cs      # Memory diagnostic viewer
├── Utilities/                       # Stateless helpers and formatters
│   ├── BaseEnvironmentIndicatorFormatter.cs
│   ├── CsvFileSyncEvaluator.cs      # CSV freshness detection
│   ├── ExecutionDialogPolicy.cs     # Execution dialog display rules
│   ├── FolderBaseEnvironmentSummaryFormatter.cs
│   ├── FolderPathUtility.cs         # Folder path manipulation
│   ├── HostsFileIndicatorFormatter.cs
│   ├── InlineDiffBuilder.cs         # Text diff computation
│   ├── InputValidator.cs            # IP/port/name validation
│   ├── OutputThrottler.cs           # UI update rate limiting
│   ├── PresetBaseEnvironmentResolver.cs # Preset environment resolution
│   ├── PresetEnvironmentLoadPlanner.cs # Environment load planning
│   ├── PresetEnvironmentStatusFormatter.cs
│   ├── PresetHeaderIndicatorFormatter.cs
│   ├── PromptDetector.cs            # Shell prompt regex detection
│   ├── ScintillaNativeBootstrap.cs  # Scintilla DLL extraction
│   ├── SshConfigParser.cs           # SSH config file parser
│   └── TerminalOutputProcessor.cs   # ANSI/terminal normalization
├── Properties/                      # .NET settings
│   └── Settings.Designer.cs
├── SSH_Helper.Tests/                # xUnit test project (separate csproj)
│   ├── SSH_Helper.Tests.csproj
│   ├── Editor/                      # Editor service tests
│   ├── Models/                      # Model tests
│   ├── Scripting/                   # Script command tests
│   │   └── Parsers/                 # Config parser tests
│   ├── Services/                    # Service tests
│   ├── UI/                          # UI control tests
│   └── Utilities/                   # Utility tests
├── ScriptSamples/                   # Example YAML scripts
│   ├── bash/                        # Bash-targeted scripts
│   ├── checkpoint/                  # Check Point device scripts
│   ├── cisco/                       # Cisco device scripts
│   ├── fortigate/                   # FortiGate device scripts
│   ├── generic/                     # Generic/multi-vendor scripts
│   └── generic_health_check.yaml
├── RebexPOC/                        # Rebex SSH proof-of-concept
│   ├── Program.cs
│   └── RebexPOC.csproj
├── artifacts/                       # Build/test artifacts (gitignored)
├── .planning/                       # Planning and analysis documents
├── .claude/                         # Claude AI configuration
│   ├── commands/                    # Custom Claude commands
│   └── roadmap/                     # Roadmap documents
├── .github/                         # GitHub configuration
│   ├── prompts/                     # GitHub Copilot prompts
│   └── workflows/                   # CI/CD workflows
├── Form1.cs                         # Main application form (~10,471 lines)
├── Form1.Designer.cs                # WinForms designer code (~2,315 lines)
├── FindDialog.cs                    # Modeless find dialog
├── AboutDialog.cs                   # About/version dialog
├── SettingsDialog.cs                # Application settings (~1,363 lines)
├── EnvironmentDialog.cs             # Environment management (~869 lines)
├── ExecutionDetailsDialog.cs        # Execution details viewer
├── FolderExecutionDialog.cs         # Folder batch execution options
├── UpdateDialog.cs                  # Auto-update dialog
├── Program.cs                       # Application entry point
├── SSH_Helper.csproj                # Project file (.NET 8.0 WinForms)
├── SSH_Helper.sln                   # Solution file
├── CLAUDE.md                        # AI assistant instructions
├── SCRIPTING.md                     # Scripting language documentation
└── qa_presets.json                  # QA test preset definitions
```

## Directory Purposes

**`Models/`:**
- Purpose: Pure data classes with no business logic
- Contains: Configuration shape (`AppConfiguration.cs`), domain models (`HostConnection.cs`, `PresetInfo.cs`, `ExecutionResult.cs`), environment profiles (`EnvironmentConfig.cs`), history models (`HistoryIndex.cs`, `HistoryRunPayload.cs`, `HistoryListItem.cs`), SSH config models (`SshConfigFile.cs`, `SshHostConfig.cs`), folder/execution models (`FolderInfo.cs`, `FolderExecutionOptions.cs`, `FolderExecutionProgress.cs`), connection testing (`ConnectionTestResult.cs`), CSV tracking (`CsvFileFingerprint.cs`)
- Key files: `AppConfiguration.cs` (root config with nested classes: `FontSettings`, `CommandEditorSettings`, `WindowState`, `UpdateSettings`, `SshConfigSettings`, `CredentialSettings`, `ApplicationState`, `HistoryEntry`), `PresetInfo.cs` (auto-detects `Simple` vs `YamlScript` type)

**`Services/`:**
- Purpose: All business logic, I/O operations, external integrations
- Contains: SSH execution, configuration persistence, preset CRUD, CSV import/export, environment management, history storage, auto-update
- Key files: `SshExecutionService.cs` (~1,834 lines -- core SSH engine), `ConfigurationService.cs` (~468 lines -- JSON persistence with caching and compression), `PresetManager.cs` (~876 lines -- preset/folder CRUD with import/export), `ExecutionCoordinator.cs` (57 lines -- thin orchestrator)

**`Services/Scripting/`:**
- Purpose: YAML-based scripting engine
- Contains: Parser, executor, expression evaluator, context, dependency analyzer, validation formatter, JSON utilities
- Key files: `ScriptParser.cs` (YAML to Script using YamlDotNet), `ScriptExecutor.cs` (336 lines -- dispatches to commands), `ScriptContext.cs` (runtime state with variable store and events), `ExpressionEvaluator.cs` (conditional expressions)

**`Services/Scripting/Commands/`:**
- Purpose: Individual script command implementations (one class per command)
- Contains: 37 files total: `IScriptCommand.cs` interface, 35 command implementations, plus helper classes (`ChoiceOptionResolver.cs`, `ScriptPromptDialogRunner.cs`, `JsonFunctions.cs`)
- Key files: `SendCommand.cs` (SSH send), `SetCommand.cs` (variable ops -- largest command), `ExtractCommand.cs` (regex capture), `IfCommand.cs`, `ForeachCommand.cs`, `WhileCommand.cs` (control flow), `HttpCommand.cs`, `PingCommand.cs`, `DnsCommand.cs` (network ops), `InteractiveCommand.cs` (terminal handoff), `TableCommand.cs` (tabular output), `ParallelCommand.cs` (concurrent execution)

**`Services/Scripting/Parsers/`:**
- Purpose: Network device output parsers for the `parse` script command
- Contains: `IConfigParser.cs` (interface), `FortiGateParser.cs` (FortiGate config parser), `ParserFactory.cs` (parser selection by name)

**`Services/Editor/`:**
- Purpose: Script editor intelligence (IDE-like features)
- Contains: Autocomplete provider, validation service, syntax highlighter, diagnostics, text utilities
- Key files: `ScriptAutocompleteProvider.cs` (command/variable completion), `ScriptEditorValidationService.cs` (real-time inline diagnostics), `YamlSshSyntaxHighlighter.cs` (Scintilla coloring), `EditorDiagnostic.cs` (diagnostic model), `EditorTextUtilities.cs` (text helpers)

**`Services/Credentials/`:**
- Purpose: Windows Credential Manager integration
- Contains: `ICredentialProvider.cs` (interface), `CredentialManagerProvider.cs` (Win32 P/Invoke implementation), `CredentialTargets.cs` (target name generation)

**`Services/Terminal/`:**
- Purpose: Interactive terminal session management
- Contains: `InteractiveTerminalService.cs` (Rebex terminal emulation for interactive SSH sessions)

**`Forms/`:**
- Purpose: Secondary form windows (complex forms with their own service dependencies)
- Contains: `InteractiveTerminalForm.cs` (interactive SSH terminal UI window)

**`UI/`:**
- Purpose: Custom controls, abstractions, theming
- Contains: Script editor control, terminal viewport, dialog theme helper, diff dialog, memory debugger
- Key files: `ScintillaScriptEditorControl.cs` (Scintilla wrapper with syntax highlighting, autocomplete, diagnostics), `IScriptEditor.cs` (editor abstraction interface), `DialogTheme.cs` (recursive dark/light theme application with VS Code-inspired palette), `InteractiveTerminalViewportControl.cs` (terminal rendering)

**`Utilities/`:**
- Purpose: Stateless helper functions, formatters, validators
- Contains: 17 files covering terminal output processing, prompt detection, input validation, CSV sync evaluation, diff building, environment/preset indicator formatters, folder path utilities, SSH config parsing, Scintilla bootstrapping, output throttling
- Key files: `TerminalOutputProcessor.cs` (ANSI/terminal normalization), `PromptDetector.cs` (shell prompt regex), `InputValidator.cs` (IP/port/name validation), `OutputThrottler.cs` (UI rate limiting), `InlineDiffBuilder.cs` (text diff), `SshConfigParser.cs` (SSH config file parsing), `ScintillaNativeBootstrap.cs` (native DLL extraction from embedded resources)

**`SSH_Helper.Tests/`:**
- Purpose: xUnit test project (separate .csproj)
- Contains: Tests mirroring source structure: `Editor/`, `Models/`, `Scripting/`, `Scripting/Parsers/`, `Services/`, `UI/`, `Utilities/`

**`ScriptSamples/`:**
- Purpose: Example YAML scripts organized by target platform
- Contains: Subdirectories for `bash/`, `checkpoint/`, `cisco/`, `fortigate/`, `generic/`
- Reference for users building custom scripts

## Key File Locations

**Entry Points:**
- `Program.cs`: Application bootstrap (Rebex license, Scintilla native DLL, `Application.Run`)
- `Form1.cs`: Main window -- constructor wires all services (`#region Constructor`, line ~217)

**Configuration:**
- `SSH_Helper.csproj`: Build config, dependencies, single-file publish settings, `DefaultItemExcludes`
- Runtime config: `%LocalAppData%\SSH_Helper\config.json` (managed by `ConfigurationService`)
- History index: `%LocalAppData%\SSH_Helper\history.index.json` (managed by `HistoryStorageService`)
- History runs: `%LocalAppData%\SSH_Helper\history/` (per-run JSON payload files)

**Core Logic:**
- `Services/SshExecutionService.cs`: SSH command execution engine with event-driven progress
- `Services/SshConnectionPool.cs`: Connection pooling with Rebex, health checks, lease tracking
- `Services/SshShellSession.cs`: Individual shell session management with Rebex Scripting API
- `Services/Scripting/ScriptParser.cs`: YAML script parser (YamlDotNet)
- `Services/Scripting/ScriptExecutor.cs`: Script step dispatcher (command pattern)
- `Services/ConfigurationService.cs`: JSON config load/save with compression and legacy migration

**Testing:**
- `SSH_Helper.Tests/SSH_Helper.Tests.csproj`: Test project
- Tests follow `SSH_Helper.Tests/{Layer}/{ServiceName}Tests.cs` pattern

## Naming Conventions

**Files:**
- PascalCase for all C# files: `SshExecutionService.cs`, `HostConnection.cs`
- One primary class per file, named to match the file
- Designer files: `Form1.Designer.cs` (WinForms auto-generated)
- Test files: `{ClassName}Tests.cs` (e.g., `CsvManagerTests.cs`)
- Commands: `{CommandName}Command.cs` (e.g., `SendCommand.cs`, `ForeachCommand.cs`)

**Directories:**
- PascalCase: `Models/`, `Services/`, `Utilities/`, `Forms/`, `UI/`
- Nested by domain: `Services/Scripting/Commands/`, `Services/Credentials/`, `Services/Editor/`

**Namespaces:**
- Root: `SSH_Helper`
- Follow directory structure:
  - `SSH_Helper.Models`
  - `SSH_Helper.Services`
  - `SSH_Helper.Services.Scripting`
  - `SSH_Helper.Services.Scripting.Commands`
  - `SSH_Helper.Services.Scripting.Models`
  - `SSH_Helper.Services.Scripting.Parsers`
  - `SSH_Helper.Services.Editor`
  - `SSH_Helper.Utilities`
  - `SSH_Helper.UI`
  - `SSH_Helper.Forms`

## Where to Add New Code

**New SSH Feature:**
- If it extends execution: add to `Services/SshExecutionService.cs` or create a new service in `Services/`
- If it needs UI: add event to service, subscribe in `Form1.cs`
- Tests: `SSH_Helper.Tests/Services/{ServiceName}Tests.cs`

**New Script Command:**
1. Create: `Services/Scripting/Commands/{CommandName}Command.cs` implementing `IScriptCommand`
2. Add `StepType` enum value in `Services/Scripting/Models/ScriptStep.cs`
3. Add properties to `ScriptStep` class for the command's parameters
4. Register in `ScriptExecutor` constructor dictionary
5. Add parsing logic in `Services/Scripting/ScriptParser.cs`
6. Add to `KnownStepKeys` array in `Services/Scripting/ScriptParser.cs`
7. Add autocomplete in `Services/Editor/ScriptAutocompleteProvider.cs`
8. Tests: `SSH_Helper.Tests/Scripting/{CommandName}CommandTests.cs`

**New Configuration Setting:**
- Add property to appropriate class in `Models/AppConfiguration.cs` (pick the right nested class: `FontSettings`, `CommandEditorSettings`, `WindowState`, `UpdateSettings`, `SshConfigSettings`, `CredentialSettings`, or root `AppConfiguration`)
- Access via `ConfigurationService.Load()` / `ConfigurationService.GetCurrent()`
- UI: add to `SettingsDialog.cs` if user-facing

**New Dialog:**
- Create at root level (e.g., `MyDialog.cs`) for simple dialogs
- Create in `Forms/` for complex forms with their own services
- Apply theming via `UI/DialogTheme.cs` -- call `DialogTheme.ApplyTo(this, darkMode)` in constructor

**New Utility:**
- Create in `Utilities/` for stateless helpers
- Tests: `SSH_Helper.Tests/Utilities/{UtilityName}Tests.cs`

**New Model:**
- Create in `Models/` for data classes
- Tests: `SSH_Helper.Tests/Models/{ModelName}Tests.cs`

**New Editor Feature:**
- Create in `Services/Editor/` for editor intelligence
- Tests: `SSH_Helper.Tests/Editor/{FeatureName}Tests.cs`

**New Credential Backend:**
- Implement `ICredentialProvider` in `Services/Credentials/`
- Tests: `SSH_Helper.Tests/Services/{ProviderName}Tests.cs`

**New Device Parser:**
- Implement `IConfigParser` in `Services/Scripting/Parsers/`
- Register in `Services/Scripting/Parsers/ParserFactory.cs`
- Tests: `SSH_Helper.Tests/Scripting/Parsers/{ParserName}Tests.cs`

**New Script Sample:**
- Add YAML file to `ScriptSamples/{platform}/` directory

## Special Directories

**`artifacts/`:**
- Purpose: Build and test output artifacts (verification builds, temp data)
- Generated: Yes
- Committed: No (gitignored)

**`.planning/`:**
- Purpose: Planning and analysis documents
- Generated: By tooling
- Committed: Yes

**`RebexPOC/`:**
- Purpose: Proof-of-concept code for Rebex SSH library
- Generated: No (manual)
- Committed: Yes but excluded from main build via `DefaultItemExcludes` in `SSH_Helper.csproj`

**`.claude/`:**
- Purpose: Claude AI assistant configuration and commands
- Contains: Custom commands (`commands/`), roadmap docs (`roadmap/`)
- Committed: Yes

**`.github/`:**
- Purpose: GitHub configuration
- Contains: Copilot prompts (`prompts/`), CI/CD workflows (`workflows/`)
- Committed: Yes

**`ScriptSamples/`:**
- Purpose: Example YAML scripts for different platforms/vendors
- Contains: Organized by target (`bash/`, `cisco/`, `fortigate/`, `checkpoint/`, `generic/`)
- Committed: Yes

---

*Structure analysis: 2026-03-07*
