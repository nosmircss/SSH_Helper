# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build the project
dotnet build SSH_Helper.sln

# Build in release mode
dotnet build SSH_Helper.sln -c Release

# Run the application
dotnet run --project SSH_Helper.csproj
```

## Project Overview

SSH_Helper is a Windows Forms application (.NET 8.0) that executes preset CLI commands over SSH against multiple hosts. It provides a GUI for managing SSH connections to multiple hosts defined in a CSV-style grid.

## Architecture

The project follows a service-oriented architecture with clear separation of concerns:

```
SSH_Helper/
├── Models/           # Data transfer objects and configuration models
├── Services/         # Business logic and external integrations
├── Utilities/        # Reusable helper classes
├── Form1.cs          # Main UI (uses services via dependency injection)
├── FindDialog.cs     # Modeless find dialog
└── AboutDialog.cs    # Application info dialog
```

### Models

- **AppConfiguration.cs** - Root configuration object persisted to config.json
  - `Presets`: Dictionary of saved command presets
  - `Username`: Default SSH username
  - `Delay`: Default delay between commands (ms)
  - `Timeout`: Default command timeout (seconds)

- **PresetInfo.cs** - Command preset with optional per-preset overrides
  - `Commands`: The command string(s) to execute
  - `Delay`: Optional delay override
  - `Timeout`: Optional timeout override

- **HostConnection.cs** - SSH host connection details
  - Includes IP validation, port parsing, and variable storage
  - `Parse()` method handles "host:port" format

- **ExecutionResult.cs** - Result of SSH command execution on a single host

### Services

- **SshExecutionService.cs** - Core SSH execution engine
  - Async execution with cancellation support
  - Event-driven progress reporting (`ProgressChanged`, `OutputReceived`)
  - Uses SSH.NET library for connections
  - Handles shell stream management and prompt detection

- **ConfigurationService.cs** - Configuration persistence
  - Loads/saves to `%LocalAppData%\SSH_Helper\config.json`
  - Handles legacy format migration (string presets to PresetInfo objects)
  - Provides caching for performance

- **PresetManager.cs** - Preset CRUD operations
  - Save, load, rename, delete, duplicate presets
  - Export/Import with GZip compression + Base64 encoding
  - Uses ConfigurationService for persistence

- **CsvManager.cs** - CSV file operations
  - Import/export DataTable to CSV files
  - Proper quoting and escaping
  - `Host_IP` column is required and auto-created

### Utilities

- **TerminalOutputProcessor.cs** - ANSI escape sequence handling
  - `Normalize()`: Processes raw terminal output (CR, LF, TAB, BS, CSI commands)
  - `Sanitize()`: Removes all ANSI codes for plain text
  - `StripPagerArtifacts()`: Removes --More-- and similar pager prompts

- **PromptDetector.cs** - Shell prompt detection
  - `BuildPromptRegex()`: Creates adaptive regex for prompt matching
  - `TryDetectPrompt()`: Finds prompts in output buffer
  - Handles mode changes (e.g., "hostname#" vs "hostname (config)#")

- **InputValidator.cs** - Centralized input validation
  - IP address, port, and timeout validation
  - Column name sanitization
  - Safe integer parsing

### Form1.cs (Main UI)

The main form is organized into logical regions:
- **Constants** - Column names, special fields
- **Services** - Injected service instances
- **State** - UI state tracking (current preset, execution state, etc.)
- **Initialization** - Control setup, event wiring
- **Event Handlers** - DataGridView, preset, menu, button events
- **Operations** - CSV, column, clipboard, preset, SSH execution
- **Helpers** - Utility methods

### Event-Driven Communication

Services communicate with the UI via events:
```csharp
// SshExecutionService events
service.ProgressChanged += (s, e) => UpdateProgressBar(e.Current, e.Total);
service.OutputReceived += (s, e) => AppendOutput(e.Output);
```

## Configuration

- Config file: `%LocalAppData%\SSH_Helper\config.json`
- Presets can override global Delay and Timeout values
- Legacy configs (string presets) are auto-migrated on load

## CSV Grid Columns

The DataGridView supports these predefined columns:
- `Host_IP` (required, cannot be deleted)
- `dudescript`, `port`, `delay`, `timeout`
- `transport`, `username`, `password`, `personality`

Custom columns can be added and used as variables in commands via `{{column_name}}` syntax.

## Dudescript

The `dudescript/` folder contains documentation for a custom scripting language for complex SSH automation. Commands include: `send`, `grab`, `check`, `wait`, `do`, `print`, `write`, `set`, `goto`, `include`, `return`, `interactive`, `exit`.

## Dependencies

- **SSH.NET** (2024.0.0) - SSH client library
- **Newtonsoft.Json** (13.0.3) - JSON serialization
- **Microsoft.Extensions.Configuration.Json** - Configuration support

## Development Guidelines

1. **Add new features** through services, not directly in Form1
2. **Use InputValidator** for all user input validation
3. **Use TerminalOutputProcessor** for any terminal output handling
4. **Follow existing patterns** - events for service-to-UI communication
5. **Keep Form1 thin** - UI logic only, delegate to services
