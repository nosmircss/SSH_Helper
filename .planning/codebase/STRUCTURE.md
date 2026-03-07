# Codebase Structure

**Analysis Date:** 2026-03-06

## Directory Layout

```
SSH_Helper/
├── Models/                          # Data transfer objects and config models
├── Services/                        # Business logic layer
│   ├── Credentials/                 # Credential storage abstraction
│   ├── Editor/                      # Script editor services (autocomplete, validation, highlighting)
│   ├── Scripting/                   # YAML scripting engine
│   │   ├── Commands/                # Individual script command implementations
│   │   ├── Models/                  # Script, ScriptStep, DebugState
│   │   └── Parsers/                 # Config output parsers (FortiGate, etc.)
│   └── Terminal/                    # Interactive terminal service
├── Forms/                           # Secondary form classes
├── UI/                              # Custom controls, interfaces, theming
├── Utilities/                       # Stateless helpers and formatters
├── Properties/                      # .NET Settings.Designer.cs
├── SSH_Helper.Tests/                # xUnit test project (separate csproj)
├── artifacts/                       # Build/test artifacts (gitignored)
├── .planning/                       # Planning documents
├── Form1.cs                         # Main application form (10,059 lines)
├── Form1.Designer.cs                # WinForms designer code (2,315 lines)
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
- Contains: Configuration shape (`AppConfiguration.cs`), domain models (`HostConnection.cs`, `PresetInfo.cs`, `ExecutionResult.cs`), environment profiles (`EnvironmentConfig.cs`), history models (`HistoryIndex.cs`, `HistoryRunPayload.cs`), SSH config models (`SshConfigFile.cs`, `SshHostConfig.cs`), folder/execution models
- Key files: `AppConfiguration.cs` (root config with all settings classes), `PresetInfo.cs` (auto-detects Simple vs YamlScript type)

**`Services/`:**
- Purpose: All business logic, I/O operations, external integrations
- Contains: SSH execution, configuration persistence, preset CRUD, CSV import/export, environment management, history storage, auto-update
- Key files: `SshExecutionService.cs` (1,796 lines -- core SSH engine), `ConfigurationService.cs` (468 lines -- JSON persistence), `PresetManager.cs` (876 lines), `ExecutionCoordinator.cs` (57 lines -- thin orchestrator)

**`Services/Scripting/`:**
- Purpose: YAML-based scripting engine
- Contains: Parser, executor, expression evaluator, context, dependency analyzer, validation formatter
- Key files: `ScriptParser.cs` (3,543 lines -- YAML to Script), `ScriptExecutor.cs` (336 lines -- dispatches to commands), `ScriptContext.cs` (587 lines -- runtime state), `ExpressionEvaluator.cs` (482 lines)

**`Services/Scripting/Commands/`:**
- Purpose: Individual script command implementations (one class per command)
- Contains: 30+ command handlers implementing `IScriptCommand`
- Key files: `SendCommand.cs` (SSH send), `SetCommand.cs` (825 lines -- variable ops), `ExtractCommand.cs` (regex capture), `IfCommand.cs`, `ForeachCommand.cs`, `WhileCommand.cs` (control flow), `HttpCommand.cs`, `PingCommand.cs`, `DnsCommand.cs` (network ops), `InteractiveCommand.cs` (terminal handoff), `TableCommand.cs` (tabular output)

**`Services/Scripting/Parsers/`:**
- Purpose: Network device output parsers
- Contains: `IConfigParser.cs` (interface), `FortiGateParser.cs`, `ParserFactory.cs`

**`Services/Editor/`:**
- Purpose: Script editor intelligence (IDE-like features)
- Contains: Autocomplete provider, validation service, syntax highlighter, diagnostics, text utilities
- Key files: `ScriptAutocompleteProvider.cs` (707 lines), `ScriptEditorValidationService.cs`, `YamlSshSyntaxHighlighter.cs`, `EditorDiagnostic.cs`, `EditorTextUtilities.cs`

**`Services/Credentials/`:**
- Purpose: Windows Credential Manager integration
- Contains: `ICredentialProvider.cs` (interface), `CredentialManagerProvider.cs` (Win32 implementation), `CredentialTargets.cs` (target name generation)

**`Services/Terminal/`:**
- Purpose: Interactive terminal session management
- Contains: `InteractiveTerminalService.cs` (3,433 lines -- Rebex terminal emulation)

**`Forms/`:**
- Purpose: Secondary form windows
- Contains: `InteractiveTerminalForm.cs` (749 lines -- interactive SSH terminal UI)

**`UI/`:**
- Purpose: Custom controls, abstractions, theming
- Contains: Script editor control, terminal viewport, dialog theme helper, diff dialog, memory debugger
- Key files: `ScintillaScriptEditorControl.cs` (2,081 lines -- Scintilla wrapper), `IScriptEditor.cs` (interface), `DialogTheme.cs` (790 lines -- dark/light theme application), `InteractiveTerminalViewportControl.cs` (681 lines)

**`Utilities/`:**
- Purpose: Stateless helper functions, formatters, validators
- Contains: Terminal output processing, prompt detection, input validation, CSV sync evaluation, diff building, indicator formatters
- Key files: `TerminalOutputProcessor.cs` (520 lines -- ANSI/terminal normalization), `PromptDetector.cs` (shell prompt regex), `InputValidator.cs` (IP/port/name validation), `OutputThrottler.cs`, `InlineDiffBuilder.cs`

**`SSH_Helper.Tests/`:**
- Purpose: xUnit test project (separate .csproj)
- Contains: Tests mirroring source structure: `Editor/`, `Models/`, `Scripting/`, `Services/`, `UI/`, `Utilities/`

## Key File Locations

**Entry Points:**
- `Program.cs`: Application bootstrap (Rebex license, Scintilla native DLL, `Application.Run`)
- `Form1.cs`: Main window -- constructor wires all services (line ~201)

**Configuration:**
- `SSH_Helper.csproj`: Build config, dependencies, single-file publish settings
- `App.config`: .NET app config (minimal)
- Runtime config: `%LocalAppData%\SSH_Helper\config.json` (managed by `ConfigurationService`)

**Core Logic:**
- `Services/SshExecutionService.cs`: SSH command execution engine
- `Services/SshConnectionPool.cs`: Connection pooling with Rebex
- `Services/SshShellSession.cs`: Individual shell session management (1,347 lines)
- `Services/Scripting/ScriptParser.cs`: YAML script parser
- `Services/Scripting/ScriptExecutor.cs`: Script step dispatcher
- `Services/ConfigurationService.cs`: JSON config load/save with compression and migration

**Testing:**
- `SSH_Helper.Tests/SSH_Helper.Tests.csproj`: Test project
- Tests follow `SSH_Helper.Tests/{Layer}/{ServiceName}Tests.cs` pattern

## Naming Conventions

**Files:**
- PascalCase for all C# files: `SshExecutionService.cs`, `HostConnection.cs`
- One primary class per file, named to match the file
- Designer files: `Form1.Designer.cs` (WinForms auto-generated)
- Test files: `{ClassName}Tests.cs` (e.g., `CsvManagerTests.cs`)

**Directories:**
- PascalCase: `Models/`, `Services/`, `Utilities/`, `Forms/`, `UI/`
- Nested by domain: `Services/Scripting/Commands/`, `Services/Credentials/`

**Namespaces:**
- Root: `SSH_Helper`
- Follow directory: `SSH_Helper.Models`, `SSH_Helper.Services`, `SSH_Helper.Services.Scripting`, `SSH_Helper.Services.Scripting.Commands`, `SSH_Helper.Utilities`, `SSH_Helper.UI`, `SSH_Helper.Forms`

## Where to Add New Code

**New SSH Feature:**
- If it extends execution: add to `Services/SshExecutionService.cs` or create a new service in `Services/`
- If it needs UI: add event to service, subscribe in `Form1.cs`
- Tests: `SSH_Helper.Tests/Services/{ServiceName}Tests.cs`

**New Script Command:**
- Create: `Services/Scripting/Commands/{CommandName}Command.cs` implementing `IScriptCommand`
- Add `StepType` enum value in `Services/Scripting/Models/ScriptStep.cs`
- Add properties to `ScriptStep` class for the command's parameters
- Register in `ScriptExecutor` constructor dictionary
- Add parsing logic in `ScriptParser.cs`
- Add to `KnownStepKeys` array in `ScriptParser.cs`
- Add autocomplete in `Services/Editor/ScriptAutocompleteProvider.cs`
- Tests: `SSH_Helper.Tests/Scripting/{CommandName}CommandTests.cs`

**New Configuration Setting:**
- Add property to appropriate class in `Models/AppConfiguration.cs`
- Access via `ConfigurationService.Load()` / `ConfigurationService.GetCurrent()`
- UI: add to `SettingsDialog.cs` if user-facing

**New Dialog:**
- Create at root level (e.g., `MyDialog.cs`) for simple dialogs
- Create in `Forms/` for complex forms with their own services
- Apply theming via `UI/DialogTheme.cs`

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

## Special Directories

**`artifacts/`:**
- Purpose: Build and test output artifacts
- Generated: Yes
- Committed: No (gitignored)

**`.planning/`:**
- Purpose: Planning and analysis documents
- Generated: By tooling
- Committed: Yes

**`RebexPOC/`:**
- Purpose: Proof-of-concept code for Rebex SSH library
- Generated: No (manual)
- Committed: Yes but excluded from main build via `DefaultItemExcludes`

**`.claude/`:**
- Purpose: Claude AI assistant configuration and commands
- Contains: Custom commands (`commands/`), roadmap docs (`roadmap/`)
- Committed: Yes

---

*Structure analysis: 2026-03-06*
