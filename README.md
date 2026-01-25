# SSH Helper

A Windows Forms application for executing SSH commands across multiple hosts with support for YAML-based scripting automation.

## Features

- **Multi-Host Execution**: Run commands on multiple hosts simultaneously
- **CSV Host Import**: Load host lists from CSV files with custom columns
- **Command Presets**: Save and organize frequently used commands
- **YAML Scripting**: Powerful scripting language for complex automation workflows
- **History Management**: Track execution history with output preservation
- **State Persistence**: Remember hosts, presets, and history between sessions
- **Auto-Updates**: Check for updates from GitHub releases

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
   - Additional columns become variables for scripts
2. **Manual Entry**: Add hosts directly in the grid

### Running Commands

1. Enter credentials in the toolbar (Username/Password)
2. Select a preset or type commands in the editor
3. Click **Execute All** to run on all hosts, or **Execute Selected** for selected hosts

### Command Presets

- **Save**: Enter a name and click Save to store the current commands
- **Favorites**: Right-click a preset to mark as favorite (shown with star)
  - Access all favorites quickly via the Favorites tab
  - Both presets and folders can be marked as favorites
- **Presets**: Use the sort button to organize presets
  - drag-and-drop reordering
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

### YAML Scripts

For complex automation, use YAML scripts. Scripts support:
- Variables and expressions
- Conditional logic (if/else)
- Loops (foreach, while)
- Output capture and regex extraction
- File operations (read/write text files)
- User input prompts with validation
- Error handling

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

## Settings

Access via Edit > Settings:

- **Remember State**: Save hosts, presets, and history on exit
- **Max History Entries**: Limit stored history items
- **Default Timeout**: Command timeout in seconds
- **Connection Timeout**: SSH connection timeout
- **Check for Updates**: Automatic update checks on startup

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open CSV file |
| Ctrl+S | Save CSV file / Save preset |
| Ctrl+F | Find in output |
| F3 | Find next match |
| Shift+F3 | Find previous match |
| F5 | Execute on all hosts |
| F6 | Execute on selected hosts |
| Escape | Stop execution |
| Ctrl+A | Select all cells |
| Ctrl+C | Copy selected cells |
| Ctrl+V | Paste to cells |
| Delete | Clear selected cells |

## Configuration

Settings are stored in `config.json` in the application directory (`%LocalAppData%\SSH_Helper\`):
- Window position and size
- Splitter positions
- Presets, folders, and favorites
- Manual sort order for presets and folders
- Folder expand/collapse states
- Update settings

### Custom Columns

Right-click column headers to add, rename, or delete custom columns. Custom columns:
- Become available as variables in scripts using `${column_name}` syntax
- Can be updated by scripts using the `updatecolumn` command
- Are saved when exporting to CSV

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
- NuGet packages:
  - Rebex.SshShell
  - Newtonsoft.Json
  - YamlDotNet
  - Microsoft.Extensions.Configuration.Json
  - Microsoft.Extensions.Configuration.Binder

## License

This project is provided as-is for personal and internal use.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Submit a pull request

## Support

For issues and feature requests, please use the [GitHub Issues](https://github.com/nosmircss/SSH_Helper/issues) page.
