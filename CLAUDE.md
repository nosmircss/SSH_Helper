# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build the project (runs the Flow Canvas React build first via MSBuild target)
dotnet build SSH_Helper.sln
dotnet build SSH_Helper.sln -c Release

# Skip the Node/Vite build (build .NET only, no Node toolchain required)
dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true

# Run the application
dotnet run --project SSH_Helper.csproj

# Run the .NET test suite
dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj

# Flow Canvas (React/Vite) — from FlowCanvas/
npm run build      # tsc && vite build
npm test           # vitest run (unit suite)
npm run test:e2e   # Playwright e2e
```

The solution (`SSH_Helper.sln`) holds two projects: `SSH_Helper` (WinExe) and `SSH_Helper.Tests`.

## Project Overview

SSH_Helper is a Windows Forms application (.NET 8.0) for executing SSH commands and YAML scripts against multiple hosts. It features a visual Flow Canvas editor, a full YAML scripting engine (30+ commands), cron-based job scheduling, interactive terminals, environment management, and connection pooling.

## Architecture

The repo root holds `Form1.cs` (main UI — keep it thin) and the top-level dialogs (`SettingsDialog`, `JobListDialog`, `JobEditorDialog`, `EnvironmentDialog`, etc.). The folders below are the map; **browse a folder (Glob) for its current contents rather than trusting a catalog here** — most of these directories are large and grow often.

```
Models/                # Config / job / history / environment DTOs
Services/              # Business logic (see breakdown below)
  Scripting/           # YAML engine: executor, parser, context, Commands/, Functions/
  Editor/              # Scintilla editor services (validation, autocomplete, highlighting)
  Credentials/         # Windows Credential Manager integration
  Terminal/            # Interactive terminal sessions
  Vault/               # HashiCorp Vault credential retrieval + OIDC login
  Notifications/       # Multi-channel dispatch (SMTP, Teams, Toast, Webhook)
UI/                    # Reusable WinForms controls/forms (incl. FlowCanvasForm.cs)
Forms/                 # Additional forms (InteractiveTerminalForm.cs)
Utilities/             # Cross-cutting helpers
FlowCanvas/src/        # React/TypeScript visual editor (Vite + @xyflow/react)
  nodes/ panels/ stores/ blockDefs/
ScriptSamples/         # Example YAML scripts
```

### Models

DTOs for config, jobs, history, and environments — read the folder. The non-obvious invariants:

- **AppConfiguration.cs** is the root, persisted to `config.json`. It aggregates `FontSettings`, `CommandEditorSettings` (20+ Scintilla options), `SshConfigSettings`, `CredentialSettings`, `UpdateSettings`, `VaultSettings`, `NotificationSettings`, plus `WindowState` (geometry, splitter positions, Flow Canvas dimensions) and the compressed `ApplicationState` blob.
- **PresetInfo** — `PresetType` is `Simple` or `YamlScript`, and `Type` is **auto-detected** from content (`ScriptParser.IsYamlScript(Commands)`), never stored. The only per-preset override is `int? Timeout` (there is **no** per-preset `Delay`). `Commands` is always normalized to CRLF line endings (same for `JobDefinition.CustomPresetCommands`) — this matters for content-hash comparison and diffing.
- **JobDefinition** — `JobTargetType` has three shapes: `Preset` (named), `Folder` (carries per-preset `FolderPresetHashes` for drift tracking), and `CustomPreset` (command/YAML content lives directly on the job, so it runs without any saved preset). `CredentialMode` includes a `Vault` member (`VaultCredentialPath` / `VaultProfileName`).

### Services

`Services/` (root) covers SSH execution + pooling, config persistence, environments, history, presets, and scheduling/jobs; subfolders add Vault, Notifications, Credentials, Terminal, Editor, and Scripting (below). Filenames are self-describing — read the folder. Non-obvious wiring:

- **SshConnectionPool** is built on **Rebex** (not SSH.NET) for pooled/interactive sessions.
- **ConfigurationService** GZip-compresses the `ApplicationState` blob into `SavedStateCompressed` (`gz64:` prefix) on save and re-inflates on load; legacy uncompressed `SavedState` and legacy string presets are migrated on first load (write-back). Load is corruption-resilient: a parse failure backs the file up to `config.json.corrupt`, sets `ConfigLoadError` for the UI to surface, and falls back to defaults. Every save also keeps a `.bak`.
- **EnvironmentService** raises `EnvironmentChanged` (previous/current environment + config).
- **HistoryStorageService** derives its base dir from the config **file's** directory, so a test that redirects the config path also redirects history.

### Scripting engine (`Services/Scripting/`)

The executor, parser, `ScriptContext`, `ExpressionEvaluator`/`ExpressionParser`, `FunctionRegistry`, and `ValueResolver` live here — read the folder. Wiring a fresh agent can't infer:

- **Commands/** holds one class per command implementing `IScriptCommand` (`ExecuteAsync(ScriptStep, ScriptContext, CancellationToken)` → `CommandResult`); there are ~43 handlers. **Registration is manual and enum-keyed**: dispatch is a `Dictionary<StepType, IScriptCommand>` built in `ScriptExecutor`'s ctor (~line 104), so adding a command means adding a `StepType` value **and** a dictionary entry, not just dropping a file in the folder.
- **Container commands** (If/Foreach/While/Repeat/Try/Switch/Parallel/Call) are constructed with `this` so they re-enter `ScriptExecutor` to run nested steps; leaf commands take no executor. A block command that forgets its own recursion silently skips all nested execution/validation.
- `CommandResult` carries the control-flow + canvas contract: `ShouldExit/Break/Continue/Return`, `IterationCount`, and `BranchTaken`. The `BranchTaken` vocabulary (`then` | `else` | `elif/{i}/then` | `cases/{i}/do` | `default`) **must** match the Flow Canvas `edge.data.branchPath` or path highlighting breaks.
- **Functions/** — ~70 built-in functions across 8 `IFunctionCategory` classes (`void Register(FunctionRegistry)`), wired in `FunctionRegistry`. Note: `JsonFunctions.cs` lives in `Commands/` but is a plain static helper, **not** a command or category.
- **ScriptExecutor** fires `StepStarting`, `StepCompleted`, `DebugPauseStateChanged` and supports breakpoints, step mode, and disabled nodes.

### Flow Canvas (`FlowCanvas/`)

A React/TypeScript visual script editor (Vite + `@xyflow/react`) hosted in a WebView2 window. The C# `UI/FlowCanvasForm.cs` hosts the React app and exchanges JSON via `PostWebMessage`/`WebMessageReceived`.

React side: the Zustand store is composed from slices in `stores/slices/` (read the folder for the current set); `MessageBus.ts` is the typed WebView2 protocol (`on`/`send`/`sendReady`, singleton `messageBus`); custom node types and UI panels live in `nodes/` and `panels/`.

**C#↔React bridge flow:**
1. YAML text → `FlowCanvasBridge.TextToGraph()` → graph JSON (nodes + edges).
2. `FlowCanvasForm.LoadGraph()` sends a `load-graph` message. Its `hasUserLayout` flag tells the canvas whether positions are a saved user arrangement (keep) or algorithmic defaults (run hierarchical auto-layout).
3. User edits the graph visually in React Flow.
4. Export: React posts `apply-yaml` → `FlowCanvasBridge.ExportGraphToYaml()`.
5. Debug: `ScriptExecutor` events → `FlowCanvasForm` → React messages for step highlighting, breakpoints, variable updates.

**Bridge design:** each graph node stores the original YAML snippet verbatim for its step; export reassembles snippets, preserving all properties, comments, and formatting (snippet round-trip, not lossy re-serialization). This is why edits to untouched blocks survive export.

**Handshake:** `FlowCanvasForm` queues all outbound `SendMessage` payloads in `_pendingMessages` until React posts `{type:'ready'}`, then drains them. Sending `load-graph` before `ready` would be dropped.

**C# event surface** (grep for these handlers; all are wired in `Form1`): `OnApplyYaml`, `OnDebugAction`, `OnTestStep`, `OnExecuteCanvas`, `OnBreakpointToggle`, `OnRunRequest`, `OnDisableBlock`, `OnTestDataBlock`, plus `OnLayoutAutosave` and `OnBrowsePath`. The React-side `run-request` message is deprecated in favor of `run` (both route to `OnRunRequest`). Display-settings (panel sizes, heatmap, block width, density, branch bands, reduced motion) persist to `WindowState` via internal `layout-save`/`pref-save` messages.

Per Development Guideline 8, Flow Canvas changes require both React (`FlowCanvas/src/`) and C# (`FlowCanvasBridge.cs`, `FlowCanvasForm.cs`) updates.

### UI and Utilities

Reusable WinForms controls/forms live in `UI/`; cross-cutting helpers live in `Utilities/` — browse the folders. Conventions to honor:

- Theme dialogs through **`DialogTheme`** (the single static class driving dark/light app-wide).
- Use **`BufferedPanel` / `BufferedSplitContainer`** (double-buffered subclasses) for repaint-heavy surfaces instead of stock `Panel`/`SplitContainer` to avoid flicker.
- Build `%LocalAppData%\SSH_Helper\` paths via **`AppDataPaths`** (single source of truth for the storage root).
- Persist JSON via **`JsonFileWriter.WriteJsonAtomic`** (temp file + `File.Replace`, optional `.bak`); `TryBackupCorrupt` salvages bad files on load. Hand-rolled `File.WriteAllText` drops the crash-safety guarantee.
- Show modeless windows via **`ModelessDialogManager<TForm>.ShowOrActivate`** (single-instance, owner-centered).
- Throttle high-rate output via **`OutputThrottler`**.
- **`ScintillaNativeBootstrap`** extracts the native Scintilla/Lexilla DLLs at startup; the editor control fails to load without it.

### Event-Driven Communication

Services talk to the UI via events. The event **names** below are real; payload fields differ from a naive guess, so check the EventArgs:

```csharp
// SSH execution — SshProgressEventArgs has Host/Message/IsError/IsConnected (no Current/Total)
service.ProgressChanged += (s, e) => UpdateProgressBar(e.Host, e.Message);
service.OutputReceived  += (s, e) => AppendOutput(e.Output);

// Script execution — StepExecutionEventArgs carries StepIndex, StepPath ("steps/2/then/0"),
// StepType, StepName, Success, Output, DurationMs (long? ms), IterationCount, BranchTaken
executor.StepStarting          += (s, e) => HighlightStep(e.StepPath);
executor.StepCompleted         += (s, e) => UpdateStepStatus(e.StepType, e.DurationMs);
executor.DebugPauseStateChanged += (s, e) => UpdateDebugUI(e.IsPaused);

// Environment
envService.EnvironmentChanged += (s, e) => ReloadHostGrid(e.CurrentEnvironment);

// Job scheduler — JobCompleted carries a JobRunResult
jobService.JobStateChanged += (s, e) => UpdateJobStatus(e.Job);
jobService.JobCompleted    += (s, e) => RecordJobHistory(e);
```

The `StepPath` / `BranchTaken` / `IterationCount` fields are the contract the Flow Canvas debug bridge depends on.

## Configuration

Default storage root is `%LocalAppData%\SSH_Helper\` (a `PORTABLE_BUILD` compile flavor redirects it to the exe directory — see `AppDataPaths`):

- `config.json` — main config (see `ConfigurationService` notes above).
- `history/` — execution history (index + per-run payload files).
- `jobs.json` — job **definitions** (file, not a folder); `job-history/` — job run history (per-job subdirs).

Window state, splitter positions, and Flow Canvas dimensions are persisted into config.

## CSV Grid Columns

`Host_IP` is the only column **enforced in code** (`CsvManager.HostColumnName` — required, non-deletable first column). `port`, `username`, `password`, and `vault_path` are convention-level columns wired in `Form1` as per-host SSH overrides. Any other column becomes a `{{column_name}}` variable usable in commands/scripts.

## Dependencies

Package versions are the source of truth in `SSH_Helper.csproj` `<PackageReference>` entries — read the csproj rather than trusting a copied list. The key libraries and their reason for being:

- **SSH.NET** — SSH client library.
- **Rebex.SshShell** — SSH shell with terminal emulation (connection pooling, interactive terminal). Plus three conditional manual `Reference` DLLs (`Rebex.Castle`/`Curve25519`/`Ed25519`) gated on `libs\RebexElliptic` existing.
- **Newtonsoft.Json** — JSON serialization.
- **Scintilla5.NET** — advanced code editor control.
- **Microsoft.Web.WebView2** — Chromium control (Flow Canvas, browser callback).
- **YamlDotNet** — YAML serialization (script parser, Flow Canvas bridge).
- **Cronos** / **CronExpressionDescriptor** — cron calculation / human-readable descriptions.
- **Microsoft.Toolkit.Uwp.Notifications**, **NAudio** — toast notifications and sound playback.

**Rebex licensing:** `RebexLicenseKey` is injected from the `REBEX_LICENSE_KEY` env var into an assembly metadata attribute at build (`rebex.key` is copied to output if present). Rebex SSH won't function without it.

**Build-time pipeline:**
- `BuildFlowCanvas` MSBuild target runs `npm run build` in `FlowCanvas/` before the .NET build (gated on `FlowCanvas\package.json` existing; skip with `-p:SkipFlowCanvasBuild=true`).
- Single-file publish settings (`PublishSingleFile`/`SelfContained`/`win-x64`) apply **only in Release** — which is why the Flow Canvas `dist/` and the native Scintilla/Lexilla DLLs are embedded as assembly resources. Debug builds resolve them from disk.
- Resolution is layered: `FlowCanvasDistLocator` prefers `exe-dir/FlowCanvas/dist` → `project-root/FlowCanvas/dist` → embedded-extracted-to-AppData (extraction dir versioned by build-time `BuildTimestamp` metadata for cache-busting). `ScintillaNativeBootstrap` similarly prefers `runtimes/<rid>/native` on disk before extracting embedded DLLs.
- A `PortableBuild` flavor (`-p:PortableBuild=true`) defines `PORTABLE_BUILD` and renames the assembly to `SSH_Helper_Portable`.

## Test Infrastructure

- Test project `SSH_Helper.Tests/` uses xUnit 2.7.0, FluentAssertions 6.12.0, Moq 4.20.70 (FluentAssertions 6.x vs 8.x APIs differ — don't accidentally upgrade).
- WinForms tests require **`Xunit.StaFact` 1.1.11** (`[WinFormsFact]`/`[WinFormsTheory]`) to run on an STA thread.
- `InternalsVisibleTo("SSH_Helper.Tests")` is in `SSH_Helper.csproj` (a second entry exposes internals to `FlowCanvasParityCli`).
- `ConfigurationService(string? configFilePath = null)` enables test isolation via temp dirs; because `HistoryStorageService` derives its base dir from the config file's directory, redirecting the config path also redirects history.

## Development Guidelines

1. **Add new features** through services, not directly in Form1.
2. **New script commands** go in `Services/Scripting/Commands/` implementing `IScriptCommand` — and remember to register the `StepType` + dictionary entry in `ScriptExecutor`.
3. **New built-in functions** go in `Services/Scripting/Functions/` implementing `IFunctionCategory`.
4. **Use InputValidator** for all user input validation.
5. **Use TerminalOutputProcessor** for any terminal output handling.
6. **Follow existing patterns** — events for service-to-UI communication.
7. **Keep Form1 thin** — UI logic only, delegate to services.
8. **Flow Canvas changes** require both React (`FlowCanvas/src/`) and C# (`FlowCanvasBridge.cs`, `FlowCanvasForm.cs`) updates.
9. **Use DialogTheme** for consistent dark/light mode theming in new dialogs.
10. **Use AppDataPaths** for any file paths under the storage root.
11. **Track rejected feature ideas** in `rejected_ideas.md` so declined proposals are documented and not repeatedly reintroduced.
