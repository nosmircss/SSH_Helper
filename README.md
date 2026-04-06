# SSH Helper

SSH Helper is a Windows Forms desktop app for executing SSH commands and YAML automation across many hosts, with presets/folders, scheduler jobs, Flow Canvas visual authoring, and persistent execution history.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

- **Multi-host execution**: Run against checked hosts or the currently selected host, with live progress and cancellation.
- **Preset library**: Save commands/scripts as presets, organize into folders, favorite frequently used items, and reorder manually.
- **Folder execution dialog**: Run folder presets sequentially or in parallel with host parallelism, stop-on-error, and output separator controls.
- **Scheduler**: Create on-demand, one-time, or recurring (cron) jobs targeting a preset, folder, or scheduler-local custom content.
- **Flow Canvas**: Visual script builder with apply/run/test-step integration and runtime/debug signaling back to the host app.
- **YAML scripting engine**: 39 script step types (SSH, local command execution, HTTP/network checks, parsing, control flow, file I/O, prompts, and more).
- **Expressions and functions**: 55 built-in function helpers plus JSON/list expression utilities for data transformation and conditions.
- **Script editor (Scintilla)**: Syntax highlighting, context-aware autocomplete, inline diagnostics, variable inspector tooltips, and smart indentation.
- **Environment management**: Named environments (for example dev/staging/prod) with isolated host grids, variables, and color labels.
- **History and details**: Persisted run history with per-host payloads, details dialogs, and separate scheduler history retention.
- **Credential/security options**: Windows Credential Manager support, SSH agent preference, and build-flavor credential isolation.
- **Auto-updates**: GitHub release checks with verified download/update flow.

## Getting Started

### Prerequisites

- Windows 10 or later
- .NET 8 Runtime
- Microsoft Edge WebView2 Runtime (used by embedded browser features)

### Installation

1. Download the latest release from [Releases](https://github.com/nosmircss/SSH_Helper/releases).
2. Extract to a folder of your choice.
3. Launch one of:
   - `SSH_Helper.exe` (standard)
   - `SSH_Helper_Portable.exe` (portable)

### Release Flavors

- **Standard (`SSH_Helper.exe`)**: App data stored under `%LocalAppData%\SSH_Helper`.
- **Portable (`SSH_Helper_Portable.exe`)**: App data stored beside the executable.
  - Portable mode requires write access to the executable folder.
- **Credential isolation**:
  - Standard build uses `SSH_Helper:*` Windows Credential Manager targets.
  - Portable build uses `SSH_Helper_Portable:*` targets.

## Usage

### Load Hosts

1. Use **File > Open CSV** (or drag and drop a CSV onto the app).
2. CSV must include `Host_IP`.
3. Additional columns become variables (`${column_name}` and `{{column_name}}`).
4. You can also add/edit rows manually in the host grid.

### Run Commands or Presets

1. Enter credentials in the toolbar.
2. Select a preset (or type commands/script content).
3. Check one or more hosts and click **Run Selected**.
4. If no hosts are checked, the run targets the currently selected host.
5. Use **Stop** to request cancellation for the active run.

### Presets and Folders

- Save and rename presets from the editor area.
- Right-click presets/folders for favorites, import/export, move, duplicate, rename, and delete actions.
- Use folder execution to run all direct child presets with advanced controls.
- Undo latest preset/folder delete is available (when pending) via `Ctrl+Z`.

### Scheduler Jobs

Open the scheduler from the top menu (`Scheduler`) or status bar link.

Scheduler supports:
- Preset, folder, or custom scheduler-local content targets
- Run now, one-time, or recurring cron schedules
- Per-job host grids and credential mode selection
- Optional per-job timeout overrides
- Persisted run history and retention controls

### Flow Canvas

Open **Edit > Flow Canvas...** (or `Ctrl+Shift+F`) to author scripts visually.

Flow Canvas supports:
- Block graph editing with start block metadata
- Apply-to-YAML back into the main editor
- Unified run/test-step execution from the canvas
- Variable/host/debug state synchronization with the main app

### YAML Scripting

Use YAML for advanced workflows:
- Control flow: `if`, `switch`, `try/catch/finally`, `foreach`, `while`, `parallel`, `call/return`
- Execution steps: `send`, `interactive`, `localcmd`, `http`, `ping`, `dns`, `portcheck`, `sftp`
- Data operations: `set`, `extract`, `readfile`, `writefile`, `parse`, `table`, assertions, prompts, and logging
- Environment/grid updates: `updateenvironment`, `updatecolumn`

See:
- [SCRIPTING.md](SCRIPTING.md) for full command reference
- [ScriptSamples/README.md](ScriptSamples/README.md) and `ScriptSamples/` for examples

### Script Editor

The built-in editor includes:
- YAML-aware syntax highlighting
- Context-sensitive autocomplete (`Ctrl+Space`)
- Inline errors/warnings and hover diagnostics
- Variable inspector hover previews
- Smart Enter/indent behavior (`Tab` / `Shift+Tab`)
- Optional YAML hygiene checks (tabs, mixed indentation, duplicate keys)

### Custom Columns and Connection Testing

- Right-click host grid column headers to add/rename/delete custom columns.
- Custom columns are available to scripts and CSV export.
- Host grid context menu includes connection testing actions for quick validation.

## Settings

Open **File > Settings**.

### General
- Remember state on exit
- Default command timeout and SSH connection timeout
- Maximum manual history entries
- Dark mode and host-grid auto-resize
- SSH config integration
- SSH connection pooling
- Credential Manager and SSH agent preferences

### Updates
- Check for updates on startup
- Update log toggle

### Command Editor
- Syntax highlighting/autocomplete toggles
- Inline validation and debounce
- Diagnostic and variable-inspector tooltips
- YAML hygiene warnings
- Indentation, smart Enter, and blank-line behavior

### Appearance
- Separate UI/code font families
- Per-surface font sizes
- Global scaling
- Word wrap and row-height controls
- Accent color

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open CSV |
| Ctrl+S | Save preset (editor focus) or save CSV |
| Ctrl+Shift+S | Save CSV As |
| Ctrl+F | Open Find |
| F3 | Find next |
| Shift+F3 | Find previous |
| Ctrl+Shift+V | Validate script |
| Ctrl+Shift+F | Open Flow Canvas |
| Ctrl+Z | Undo latest preset/folder delete (when available) |
| Alt+F4 | Exit app |

Host grid (when focused):
- `Ctrl+A` select all cells
- `Ctrl+C` copy
- `Ctrl+V` paste
- `Delete` / `Backspace` clear selected cells

Script editor:
- `Ctrl+Space` autocomplete
- `Tab` indent selection
- `Shift+Tab` outdent selection

Find dialog:
- `Alt+C` toggle case-sensitive find

## Configuration and Data Storage

Application data location:
- Standard build: `%LocalAppData%\SSH_Helper\`
- Portable build: executable directory (same folder as `SSH_Helper_Portable.exe`)

Key files/folders include:
- `config.json` (application settings and UI state)
- `jobs.json` (scheduler jobs)
- `history.index.json` and `history/<run-id>.json` (manual run history)
- `job-history/<job-id>/index.json` plus run payload files (scheduler history)
- Preset/folder/environment state and appearance/editor preferences

## Building from Source

```bash
# Clone
git clone https://github.com/nosmircss/SSH_Helper.git
cd SSH_Helper

# Install Flow Canvas dependencies (first time)
cd FlowCanvas
npm install
cd ..

# Build (runs Flow Canvas build target by default)
dotnet build

# Run
dotnet run
```

Optional: skip Flow Canvas build during .NET build with:

```bash
dotnet build -p:SkipFlowCanvasBuild=true
```

### Source Build Requirements

- Windows 10+ and .NET 8 SDK
- Node.js and npm (for Flow Canvas assets)
- Visual Studio 2022+ recommended

Major runtime dependencies are restored automatically (`Rebex.SshShell`, `SSH.NET`, `Scintilla5.NET`, `YamlDotNet`, `Newtonsoft.Json`, `Microsoft.Web.WebView2`, `Cronos`, `NAudio`).

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).

## Contributing

1. Fork the repository.
2. Create a feature branch.
3. Submit a pull request.

## Support

For issues and feature requests, use [GitHub Issues](https://github.com/nosmircss/SSH_Helper/issues).
