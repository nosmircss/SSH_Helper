# SSH Helper

A Windows Forms application for executing SSH commands across multiple hosts with YAML-based scripting, environment management, multi-protocol network commands, and a code-editor-grade script editor.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

- **Multi-Host Execution**: Run commands on multiple hosts simultaneously with configurable parallelism
- **CSV Host Import**: Load host lists from CSV files with custom columns that become script variables
- **Command Presets**: Save, organize into folders, and favorite frequently used commands
- **YAML Scripting**: 24+ commands for complex automation workflows with control flow, error handling, and expressions
- **Multi-Protocol Commands**: HTTP/HTTPS, DNS, ICMP Ping, TCP Port Check, and SFTP beyond SSH
- **Environment Management**: Named profiles (dev, staging, prod) with independent host grids, variables, and color labels
- **Script Editor**: Scintilla-based editor with syntax highlighting, context-aware autocomplete, inline diagnostics, and smart indentation
- **Dark Mode**: Full application-wide dark and light theme support
- **Expression Engine**: 40+ built-in functions for string, array, JSON, and math operations
- **History Management**: Track execution history with output preservation and per-host details that persist across restarts
- **SSH Config Integration**: Read `~/.ssh/config` for host configurations
- **Device Config Parsing**: Parse FortiGate configurations into structured data for scripting
- **State Persistence**: Remember hosts, presets, environments, history, and window layout between sessions
- **Auto-Updates**: Check for updates from GitHub releases with SHA256 checksum verification

## Getting Started

### Prerequisites

- Windows 10 or later
- .NET 8.0 Runtime

### Installation

1. Download the latest release from the [Releases](https://github.com/nosmircss/SSH_Helper/releases) page
2. Extract to your preferred location
3. Run `SSH_Helper.exe`

## Usage

### Loading Hosts

1. **From CSV**: File > Open CSV or drag-and-drop a CSV file
   - CSV must contain a `Host_IP` column
   - Additional columns become variables for scripts (`${column_name}` or `{{column_name}}`)
2. **Manual Entry**: Add hosts directly in the grid

### Running Commands

1. Enter credentials in the toolbar (Username/Password)
2. Select a preset or type commands in the editor
3. Click **Execute All** to run on all hosts, or **Execute Selected** for selected hosts

### Command Presets

- **Save**: Enter a name and click Save to store the current commands
- **Rename**: Changing the preset name then saving prompts to rename existing or create new
- **Favorites**: Right-click a preset to mark as favorite (shown with star)
  - Access all favorites quickly via the Favorites tab
  - Both presets and folders can be marked as favorites
- **Sorting**: Use the sort button to organize presets
  - Ascending, descending, or manual drag-and-drop reordering
- **Folders**: Organize presets into folders for better management
  - Right-click to create/rename/delete folders
  - Drag presets into folders or use "Move to Folder" menu
  - Folders can be expanded/collapsed (state is remembered)
  - Selecting a folder displays a summary showing preset count and contents
  - Execute all presets in a folder at once via right-click menu

### Folder Execution

Execute multiple presets from a folder with advanced options:
- **Preset Selection**: Choose which presets to run from a checklist
- **Execution Mode**: Run presets sequentially or in parallel
- **Stop on Error**: Optionally stop execution if any preset fails
- **Parallel Hosts**: Configure how many hosts to run simultaneously (1-N)
- **Suppress Separators**: Hide preset name separators from output
- Per-host results are tracked in history for later review

### Environment Management

Environments let you maintain separate host configurations and variables for different contexts (development, staging, production, etc.).

- **Create/Switch**: Use the toolbar dropdown to switch between environments; the entire host grid and variable context swaps on switch
- **Variables**: Each environment has its own key-value variable dictionary, injected into execution context
- **Host Grids**: Each environment stores independent host columns, entries, and selections
- **Color Labels**: Assign optional colors to environments for visual identification in the toolbar
- **Import/Export**: Share environments via `.sshenv.json` files with conflict resolution on import
- **Script Integration**: Use `updateenvironment:` in YAML scripts to persist variable updates back to the active environment
- **Default Environment**: A reserved "Default" environment is always present; legacy state is automatically captured on first use
- **Window Title**: The title bar shows the active environment name

Manage environments via **Edit > Environments** or the toolbar dropdown button.

### YAML Scripts

For complex automation, use YAML scripts. Scripts support:
- Variables and expressions with 40+ built-in functions
- Conditional logic (if/elif/else)
- Loops (foreach with filters, while with max iterations)
- Output capture and regex extraction
- File operations (read/write with JSON, JSONL, CSV, text formats)
- User input prompts with validation
- Try/catch/finally error handling
- Multi-protocol network commands (HTTP, DNS, Ping, Port Check, SFTP)
- Grid column updates and environment variable persistence
- Device configuration parsing (FortiGate)
- Logging with levels (debug, info, warning, error, success)
- Built-in variables: `${_output}`, `${_timestamp}`, `${_iteration}`, `${_last_error}`

See [Scripting Documentation](SCRIPTING.md) for full details.

**Example Script:**
```yaml
---
name: Quick Status Check
steps:
  - send: show version
    capture: output
  - extract:
      from: output
      pattern: 'Version (.+?)$'
      into: version
  - print: "Device version: ${version}"
```

**Example: HTTP Health Check:**
```yaml
---
name: API Health Check
steps:
  - http:
      url: "https://${Host_IP}/api/health"
      method: GET
      timeout: 10
      into: health
      on_error: continue
  - if: ${health_status} == 200
    then:
      - log:
          message: "${Host_IP} healthy"
          level: success
    else:
      - log:
          message: "${Host_IP} unhealthy (status: ${health_status})"
          level: warning
```

**Example: Block IPs from File:**
```yaml
---
name: Block IPs from File
steps:
  - readfile:
      path: "C:\\blocklist.txt"
      into: blocked_ips
  - foreach: ip in blocked_ips
    do:
      - send: iptables -A INPUT -s ${ip} -j DROP
      - print: "Blocked ${ip}"
```

### Script Editor

The built-in script editor provides a code-editor-grade experience for writing YAML scripts:

- **Syntax Highlighting**: 8 token types (keys, commands, options, variables, strings, numbers, booleans, comments) with light and dark theme palettes
- **Context-Aware Autocomplete**: Suggestions adapt to your position in the YAML document -- root keys, step commands, command-specific options, enum values, and variable interpolation. Triggered by typing or Ctrl+Space
- **Inline Diagnostics**: Real-time validation with red (error) and yellow (warning) squiggle underlines and hover tooltips
- **Variable Inspector**: Hover over `${var}` or `{{column}}` tokens to see resolved values
- **Smart Editing**: Context-aware Enter key (deeper indent after `:`, sibling indent after `-`), Tab/Shift+Tab for block indent/outdent
- **YAML Hygiene**: Optional warnings for tab indentation, mixed indent styles, and duplicate keys

All editor features are configurable via **Edit > Settings > Command Editor**.

### Custom Columns

Right-click column headers to add, rename, or delete custom columns. Custom columns:
- Become available as variables in scripts using `${column_name}` or `{{column_name}}` syntax
- Can be updated by scripts using the `updatecolumn` command
- Are saved when exporting to CSV
- Are scoped per environment

## Settings

Access via **Edit > Settings**:

### General
- **Remember State**: Save hosts, presets, environments, and history on exit
- **Max History Entries**: Limit stored history items (default: 30)
- **Default Timeout**: Command timeout in seconds
- **Connection Timeout**: SSH connection timeout
- **Dark Mode**: Toggle application-wide dark/light theme
- **Auto-Resize Columns**: Automatically fit host grid columns
- **SSH Config**: Integrate with `~/.ssh/config` for host lookups
- **Connection Pooling**: Reuse SSH connections across commands
- **Credential Manager**: Use Windows Credential Manager for stored credentials
- **SSH Agent**: Prefer SSH agent for authentication

### Updates
- **Check for Updates**: Automatic update checks on startup
- **Update Log**: Enable detailed logging for troubleshooting updates

### Command Editor
- **Syntax Highlighting**: Toggle on/off
- **Autocomplete**: Enable autocomplete, auto-show on typing
- **Validation**: Inline validation toggle, debounce timing (150-2000ms), show warnings
- **Tooltips**: Diagnostic tooltips, variable inspector tooltips
- **YAML Hygiene**: Flag tabs, mixed indents, duplicate keys
- **Indentation**: Spaces vs. tabs, indent size (2-8), smart Enter, blank line preservation

### Appearance
- **Font Families**: Separate UI and code font families
- **Font Sizes**: 12 individually configurable sizes (section titles, tree view, code editor, output, tabs, buttons, host list, menu, status bar, dialogs, and more)
- **Global Scale**: Scale factor from 0.8x to 1.5x
- **Layout**: Word wrap for code editor and output, row heights for tree view and host list
- **Accent Color**: Custom accent color with picker

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open CSV file |
| Ctrl+S | Save CSV file / Save preset |
| Ctrl+F | Find in output |
| F3 | Find next match |
| Shift+F3 | Find previous match |
| Alt+C | Toggle case sensitivity (Find) |
| Alt+W | Toggle whole word match (Find) |
| Alt+R | Toggle regex mode (Find) |
| F5 | Execute on all hosts |
| F6 | Execute on selected hosts |
| Escape | Stop execution / Close Find |
| Ctrl+A | Select all cells |
| Ctrl+C | Copy selected cells |
| Ctrl+V | Paste to cells |
| Delete | Clear selected cells |
| Ctrl+Space | Trigger autocomplete (Script Editor) |
| Tab | Indent selected lines (Script Editor) |
| Shift+Tab | Outdent selected lines (Script Editor) |

## Configuration

Settings are stored in `config.json` in the application directory (`%LocalAppData%\SSH_Helper\`):
- Window position, size, and splitter positions
- Presets, folders, favorites, and manual sort order
- Folder expand/collapse states
- Environment profiles with variables and active environment
- Command editor preferences
- Font and appearance settings
- Dark mode preference
- Execution history with per-host details metadata
- Update settings

## Building from Source

```bash
# Clone the repository
git clone https://github.com/nosmircss/SSH_Helper.git

# Navigate to project
cd SSH_Helper

# Build
dotnet build

# Run
dotnet run
```

### Requirements

- Visual Studio 2022 or later (recommended)
- .NET 8.0 SDK
- NuGet packages (restored automatically):
  - Rebex.SshShell - SSH terminal sessions
  - SSH.NET - SFTP file transfers
  - Scintilla5.NET - Script editor control
  - YamlDotNet - YAML parsing
  - Newtonsoft.Json - JSON serialization

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Submit a pull request

## Support

For issues and feature requests, please use the [GitHub Issues](https://github.com/nosmircss/SSH_Helper/issues) page.
