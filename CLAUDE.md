<!-- OPENSPEC:START -->
# OpenSpec Instructions

These instructions are for AI assistants working in this project.

Always open `@/openspec/AGENTS.md` when the request:
- Mentions planning or proposals (words like proposal, spec, change, plan)
- Introduces new capabilities, breaking changes, architecture shifts, or big performance/security work
- Sounds ambiguous and you need the authoritative spec before coding

Use `@/openspec/AGENTS.md` to learn:
- How to create and apply change proposals
- Spec format and conventions
- Project structure and guidelines

Keep this managed block so 'openspec update' can refresh the instructions.

<!-- OPENSPEC:END -->

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build the project (includes Flow Canvas React build via MSBuild target)
dotnet build SSH_Helper.sln

# Build in release mode
dotnet build SSH_Helper.sln -c Release

# Run the application
dotnet run --project SSH_Helper.csproj

# Run tests
dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj

# Build Flow Canvas separately (React/Vite)
cd FlowCanvas && npm run build
```

## Project Overview

SSH_Helper is a Windows Forms application (.NET 8.0) for executing SSH commands and YAML scripts against multiple hosts. It features a visual Flow Canvas editor, a full YAML scripting engine with 30+ commands, cron-based job scheduling, interactive terminals, environment management, and connection pooling.

## Architecture

```
SSH_Helper/
├── Models/                    # Data transfer objects and configuration models
├── Services/                  # Business logic and external integrations
│   ├── Scripting/             # YAML script engine (executor, parser, context)
│   │   ├── Commands/          # 30+ command handlers (send, if, foreach, try, etc.)
│   │   ├── Functions/         # Built-in function registry (string, math, collection, etc.)
│   │   ├── Models/            # Script, ScriptStep, DebugState
│   │   └── Parsers/           # Config parsers (e.g., FortiGate)
│   ├── Editor/                # Scintilla editor services (validation, autocomplete, highlighting)
│   ├── Credentials/           # Windows Credential Manager integration
│   └── Terminal/              # Interactive terminal session management
├── UI/                        # Reusable WinForms controls and forms
│   └── FlowCanvasForm.cs      # WebView2 host for the React Flow Canvas
├── Forms/                     # Additional forms
│   └── InteractiveTerminalForm.cs
├── FlowCanvas/                # React/TypeScript visual script editor (Vite + React Flow)
│   └── src/
│       ├── nodes/             # Custom node types (BaseBlock, StartNode, CommentNode)
│       ├── panels/            # UI panels (Toolbar, Palette, Properties, Debug, Output)
│       ├── stores/            # Zustand store with 9 slices
│       └── blockDefs/         # Block type metadata registry
├── Utilities/                 # Reusable helper classes
├── ScriptSamples/             # Example YAML scripts
├── Form1.cs                   # Main UI
├── SettingsDialog.cs          # Multi-tab settings (editor, fonts, SSH, credentials, updates)
├── EnvironmentDialog.cs       # Named environment profile management
├── JobListDialog.cs           # Job scheduler dashboard
├── JobEditorDialog.cs         # Job creation/editing
├── ExecutionDetailsDialog.cs  # Execution history viewer
├── FolderExecutionDialog.cs   # Batch folder execution config
├── RunOutputViewerDialog.cs   # Per-host output viewer
├── ImportPreviewDialog.cs     # Import conflict resolution
├── FindDialog.cs              # Modeless find dialog
├── AboutDialog.cs             # Application info
└── UpdateDialog.cs            # Auto-update from GitHub Releases
```

### Models

- **AppConfiguration.cs** - Root configuration persisted to config.json. Includes:
  - `Presets`: Dictionary of saved presets (commands or YAML scripts)
  - `Username`, `Delay`, `Timeout`: Default SSH settings
  - `ConnectionTimeout`, `UseConnectionPooling`: Connection pool settings
  - `DarkMode`, `AutoResizeHostColumns`, `RememberState`, `PresetSortMode`
  - `FontSettings`: Per-element font family/size with `GlobalScaleFactor`
  - `CommandEditorSettings`: 20+ Scintilla editor settings (highlighting, autocomplete, folding, validation)
  - `SshConfigSettings`, `CredentialSettings`, `UpdateSettings`
  - `MaxConcurrentJobs`, `DefaultMaxHistoryRuns`, `DefaultHistoryRetentionDays`
  - `WindowState`: Window geometry, splitter positions, Flow Canvas dimensions
  - `ApplicationState`, `HistoryEntry`, `HostHistoryEntry`

- **PresetInfo.cs** - Command preset with `PresetType` enum (Script vs. legacy commands), optional per-preset delay/timeout overrides

- **HostConnection.cs** - SSH host connection details with IP validation, port parsing, variable storage

- **ExecutionResult.cs** - Result of SSH command execution on a single host

- **EnvironmentConfig.cs** - Named environment profile (host columns, hosts, variables, CSV fingerprint)

- **JobDefinition.cs** - Scheduled job model with cron expression, target hosts/presets, credentials, execution mode. Related enums: `CredentialMode`, `ScheduleType`, `FolderExecutionMode`, `JobExecutionState`, `JobTargetType`

- **JobRunRecord.cs / JobRunIndexDocument.cs / JobRunPayload.cs / JobRunResult.cs** - Job execution history persistence

- **FolderExecutionOptions.cs / FolderInfo.cs** - Folder-level batch execution configuration

- **SshConfigFile.cs / SshConfigEntry / SshHostConfig.cs** - OpenSSH config file models

- **CsvFileFingerprint.cs** - Hash/size fingerprint for CSV file change detection

### Services

**Core SSH:**
- **SshExecutionService.cs** - Core SSH execution engine with async execution, cancellation, event-driven progress
- **SshConnectionPool.cs** - Connection pooling using Rebex SSH; reuses connections across executions
- **SshShellSession.cs** - Stateful interactive SSH shell using Rebex scripting API with prompt detection
- **SshConfigService.cs** - Reads `~/.ssh/config` and applies host-specific settings
- **ExecutionCoordinator.cs** - Facade that decouples Form1 from direct SSH service coupling

**Scripting Engine (`Services/Scripting/`):**
- **ScriptExecutor.cs** - Interprets YAML scripts; dispatches to 30+ command handlers; fires `StepStarting`, `StepCompleted`, `DebugPauseStateChanged` events; supports breakpoints, step mode, disabled nodes
- **ScriptParser.cs** - Parses YAML text into `Script`/`ScriptStep` models
- **ScriptContext.cs** - Runtime state: variables, SSH session ref, output events, exit status
- **ExpressionEvaluator.cs** - Boolean expressions (==, !=, >, <, contains, matches, startswith, is empty, is defined, and/or/not)
- **ExpressionParser.cs** - Function-call expression parsing from YAML values
- **FunctionRegistry.cs** - 40+ built-in functions across categories (string, math, collection, datetime, encoding, type)
- **LambdaExpression.cs** - Arrow-style lambdas for higher-order collection functions (map, filter, reduce)
- **ScriptDependencyAnalyzer.cs** - Analyzes which host columns and SSH capabilities a script requires
- **ValueResolver.cs** - Resolves `{{variable}}` references in script values

**Scripting Commands (`Services/Scripting/Commands/`):**
30+ command handlers including: `SendCommand`, `PrintCommand`, `SetCommand`, `WaitCommand`, `ExtractCommand`, `IfCommand`, `ForeachCommand`, `WhileCommand`, `TryCommand`, `SwitchCommand`, `ParallelCommand`, `BreakCommand`, `ContinueCommand`, `ExitCommand`, `ReturnCommand`, `CallCommand`, `AssertCommand`, `LogCommand`, `InputCommand`, `ConfirmCommand`, `ChooseCommand`, `MultiselectCommand`, `ReadFileCommand`, `WriteFileCommand`, `HttpCommand`, `WebhookCommand`, `PingCommand`, `DnsCommand`, `PortcheckCommand`, `SftpCommand`, `ParseCommand`, `TableCommand`, `UpdateColumnCommand`, `UpdateEnvironmentCommand`, `InteractiveCommand`, `BrowserCallbackCaptureCommand`

**Editor Services (`Services/Editor/`):**
- **ScriptEditorValidationService.cs** - Debounced async YAML validation with diagnostics
- **ScriptAutocompleteProvider.cs** - Context-aware autocomplete for YAML script editing
- **YamlSshSyntaxHighlighter.cs** - Syntax highlighting rules for Scintilla

**Scheduling:**
- **SchedulingService.cs** - Cron-based job scheduler with timer management and missed-run detection
- **JobExecutionService.cs** - Runs scheduled/on-demand jobs with queue, cancellation, concurrency limits
- **JobStorageService.cs** - CRUD persistence for job definitions
- **JobHistoryService.cs** - Job execution history with pruning and retention policies
- **JobExportService.cs** - Export/import job definitions with conflict detection

**Other Services:**
- **ConfigurationService.cs** - Configuration persistence to `%LocalAppData%\SSH_Helper\config.json` with legacy migration
- **PresetManager.cs** - Preset CRUD with export/import (GZip + Base64)
- **CsvManager.cs** - CSV import/export with `Host_IP` column requirement
- **EnvironmentService.cs** - Named environment profiles with active switching and `EnvironmentChanged` event
- **HistoryStorageService.cs** - Execution history persistence (index + per-run payloads)
- **FlowCanvasBridge.cs** - Bidirectional translator between YAML scripts and Flow Canvas graph JSON (import/export with layout calculation and branch coloring)
- **UpdateService.cs** - GitHub Releases auto-update (check, download, verify, install)
- **PresetDeleteUndoService.cs** - Undo support for preset deletion
- **CredentialManagerProvider.cs** - Windows Credential Manager integration via DPAPI

### UI Components (`UI/`)

- **FlowCanvasForm.cs** - Modeless WinForms window hosting the React Flow Canvas via WebView2. Bidirectional JSON messaging (PostWebMessage/WebMessageReceived). Events: `OnApplyYaml`, `OnDebugAction`, `OnTestStep`, `OnExecuteCanvas`, `OnBreakpointToggle`, `OnRunRequest`, `OnDisableBlock`, `OnTestDataBlock`
- **ScintillaScriptEditorControl.cs** - Full-featured YAML script editor: syntax highlighting, autocomplete, inline diagnostics, code folding, brace matching, smart Enter, variable tooltips
- **UnsavedPresetDiffDialog.cs** - Inline diff of unsaved preset changes (LCS algorithm)
- **CronBuilderControl.cs** - UserControl for building/previewing cron expressions
- **BrowserCallbackWebViewDialog.cs** - Embedded browser for OAuth/SSO flows during script execution
- **MemoryDebuggerDialog.cs** - Live process memory diagnostics
- **HistoryListBox.cs** - Custom-drawn history list with status icons
- **InteractiveTerminalViewportControl.cs** - Terminal viewport renderer
- **DialogTheme.cs** - Centralized dark/light mode theming
- **BufferedPanel.cs / BufferedSplitContainer.cs** - Double-buffered controls to eliminate flicker

### Flow Canvas (`FlowCanvas/`)

A React/TypeScript visual script editor built with Vite and @xyflow/react (React Flow), hosted in a WebView2 window.

**Key components:**
- **App.tsx** - Root ReactFlow canvas with MiniMap, Controls, Background
- **MessageBus.ts** - Typed C#-to-React message protocol over WebView2
- **stores/useFlowStore.ts** - Zustand store with 9 slices: graph, debug, execution, ui, variable, host, timeline, comment, undo
- **nodes/BaseBlock.tsx** - Universal block node rendering all step types with breakpoint/disable toggles
- **panels/** - Toolbar, Palette (drag-and-drop blocks), Properties editor, OutputPreview, VariableInspector, DebugPanel, TimelinePanel, SearchOverlay, HostBar

**C#-to-React bridge flow:**
1. YAML script in editor -> `FlowCanvasBridge.ImportToCanvas()` -> graph JSON (nodes + edges)
2. Graph JSON sent to React via `PostWebMessage`
3. User edits graph visually in React Flow
4. Export: React sends graph JSON back -> `FlowCanvasBridge.ExportToYaml()` -> YAML script
5. Debug: `ScriptExecutor` events -> `FlowCanvasForm` -> React messages for step highlighting, breakpoints, variable updates

### Utilities

- **TerminalOutputProcessor.cs** - ANSI escape sequence handling (Normalize, Sanitize, StripPagerArtifacts)
- **PromptDetector.cs** - Shell prompt detection with adaptive regex
- **InputValidator.cs** - Centralized input validation
- **FlowCanvasDistLocator.cs** - Locates built Flow Canvas `dist/` folder (alongside exe or embedded resources)
- **InlineDiffBuilder.cs** - LCS-based inline diff algorithm for preset change visualization
- **ModelessDialogManager.cs** - Generic show-or-activate pattern for modeless dialogs
- **OutputThrottler.cs** - Batches rapid output events to prevent UI thread flooding
- **SshConfigParser.cs** - Parses OpenSSH `~/.ssh/config` files
- **PresetSaveImpactResolver.cs** - Analyzes what saving a preset affects (jobs, environments)
- **PresetEnvironmentLoadPlanner.cs** - Decides environment switching when loading presets
- **CsvFileSyncEvaluator.cs** - Evaluates CSV file sync status with loaded grid
- **JsonFileWriter.cs** - Atomic JSON write (temp file + rename)
- **AppDataPaths.cs** - Centralizes `%LocalAppData%\SSH_Helper\` path construction
- **ScintillaNativeBootstrap.cs** - Extracts Scintilla/Lexilla native DLLs from embedded resources at startup

### Event-Driven Communication

Services communicate with the UI via events:
```csharp
// SSH execution events
service.ProgressChanged += (s, e) => UpdateProgressBar(e.Current, e.Total);
service.OutputReceived += (s, e) => AppendOutput(e.Output);

// Script execution events
executor.StepStarting += (s, e) => HighlightCurrentStep(e.Step);
executor.StepCompleted += (s, e) => UpdateStepStatus(e.Step, e.Duration);
executor.DebugPauseStateChanged += (s, e) => UpdateDebugUI(e.IsPaused);

// Environment events
envService.EnvironmentChanged += (s, e) => ReloadHostGrid(e.Environment);

// Job scheduler events
jobService.JobStateChanged += (s, e) => UpdateJobStatus(e.Job);
jobService.JobCompleted += (s, e) => RecordJobHistory(e.Result);
```

## Configuration

- Config file: `%LocalAppData%\SSH_Helper\config.json`
- Execution history: `%LocalAppData%\SSH_Helper\history/`
- Job definitions and history: `%LocalAppData%\SSH_Helper\jobs/`
- Presets can override global Delay and Timeout values
- Legacy configs (string presets) are auto-migrated on load
- Window state, splitter positions, and Flow Canvas dimensions are persisted

## CSV Grid Columns

The DataGridView supports these predefined columns:
- `Host_IP` (required, cannot be deleted)
- `port`, `username`, `password`, `vault_path`

Custom columns can be added and used as variables in commands/scripts via `{{column_name}}` syntax.

## Dependencies

- **SSH.NET** (2024.1.0) - SSH client library
- **Rebex.SshShell** (7.0.9448) - SSH shell with terminal emulation (connection pooling, interactive terminal)
- **Newtonsoft.Json** (13.0.3) - JSON serialization
- **Scintilla5.NET** (6.1.1) - Advanced code editor control
- **Microsoft.Web.WebView2** (1.0.3124.44) - Chromium browser control (Flow Canvas, browser callback)
- **YamlDotNet** (16.3.0) - YAML serialization (script parser, Flow Canvas bridge)
- **Cronos** (0.11.1) - Cron schedule calculation
- **CronExpressionDescriptor** (2.45.0) - Human-readable cron descriptions

**Build-time:**
- `BuildFlowCanvas` MSBuild target runs `npm run build` in `FlowCanvas/` before .NET build
- Flow Canvas `dist/` is embedded as assembly resources for single-file publish
- Scintilla native DLLs embedded and extracted at startup via `ScintillaNativeBootstrap`

## Test Infrastructure

- Test project: `SSH_Helper.Tests/` using xUnit 2.7.0, FluentAssertions 6.12.0, Moq 4.20.70
- WinForms tests require `Xunit.StaFact` 1.1.11 (`[WinFormsFact]`/`[WinFormsTheory]`)
- `InternalsVisibleTo("SSH_Helper.Tests")` is in SSH_Helper.csproj
- `ConfigurationService` accepts optional `configFilePath` for test isolation

## Development Guidelines

1. **Add new features** through services, not directly in Form1
2. **New script commands** go in `Services/Scripting/Commands/` implementing the command handler pattern
3. **New built-in functions** go in `Services/Scripting/Functions/` implementing `IFunctionCategory`
4. **Use InputValidator** for all user input validation
5. **Use TerminalOutputProcessor** for any terminal output handling
6. **Follow existing patterns** - events for service-to-UI communication
7. **Keep Form1 thin** - UI logic only, delegate to services
8. **Flow Canvas changes** require both React (`FlowCanvas/src/`) and C# (`FlowCanvasBridge.cs`, `FlowCanvasForm.cs`) updates
9. **Use DialogTheme** for consistent dark/light mode theming in new dialogs
10. **Use AppDataPaths** for any file paths under `%LocalAppData%\SSH_Helper\`
11. **Track rejected feature ideas** in `rejected_ideas.md` so declined proposals are documented and not repeatedly reintroduced
