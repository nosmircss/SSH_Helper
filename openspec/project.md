# Project Context

## Purpose
SSH_Helper is a Windows Forms desktop application that executes preset CLI commands over SSH against multiple hosts simultaneously. It provides a GUI for managing SSH connections to hosts defined in a CSV-style grid, with support for:
- Batch command execution across multiple hosts
- Reusable command presets with per-preset timeout/delay overrides
- Variable substitution using `{{column_name}}` syntax from grid columns
- A scripting engine with control flow (if/while/foreach), file I/O, and webhooks
- Import/export of host lists and presets

## Tech Stack
- **.NET 8.0** - Target framework
- **Windows Forms** - UI framework
- **C# 12** - Primary language with nullable reference types enabled
- **Rebex.SshShell** - SSH client library (primary)
- **Newtonsoft.Json** - JSON serialization for configuration
- **YamlDotNet** - YAML parsing for scripting
- **xUnit** - Unit testing framework
- **FluentAssertions** - Test assertion library
- **Moq** - Mocking framework for tests

## Project Conventions

### Code Style
- **Nullable reference types** enabled project-wide
- **Implicit usings** enabled
- **PascalCase** for public members, types, and methods
- **_camelCase** with underscore prefix for private fields
- **XML documentation comments** on public service methods
- Single responsibility per class; services are stateless where possible
- Use `var` for local variables when type is obvious

### Architecture Patterns
- **Service-oriented architecture** with clear separation:
  - `Models/` - Data transfer objects and configuration models
  - `Services/` - Business logic, SSH execution, configuration persistence
  - `Services/Scripting/` - Script parsing and execution engine with command pattern
  - `Utilities/` - Reusable helper classes (validation, terminal processing)
- **Event-driven communication** between services and UI via C# events
- **Dependency injection** pattern (manual, no DI container) - services passed to Form1
- **Command pattern** for scripting commands (`IScriptCommand` interface)
- UI forms kept thin; delegate business logic to services

### Testing Strategy
- **xUnit** for unit tests in `SSH_Helper.Tests/` project
- **FluentAssertions** for readable assertions
- **Moq** for mocking dependencies
- Test files mirror source structure (e.g., `Services/CsvManagerTests.cs`)
- Focus on testing utilities and services; UI not directly tested

### Git Workflow
- Single `master` branch for main development
- No co-authoring references in commit messages
- Commit messages should be concise and descriptive

## Domain Context
- **SSH Shell Sessions**: The app maintains interactive shell sessions (not just exec commands) to handle prompts and multi-line output
- **Prompt Detection**: Uses regex-based prompt detection to know when commands complete
- **Terminal Output Processing**: Handles ANSI escape sequences, pager artifacts (--More--), and various terminal behaviors
- **Presets**: Saved command configurations that can include delay/timeout overrides
- **Host Grid**: DataGridView with required `Host_IP` column; other columns become variables
- **Scripting**: YAML-based scripts with commands like `send`, `wait`, `extract`, `if`, `foreach`, `log`, `webhook`

## Important Constraints
- **Windows-only**: Uses Windows Forms, targets `net8.0-windows`
- **Local configuration**: Config stored in `%LocalAppData%\SSH_Helper\config.json`
- **Rebex license**: Optional Rebex license key via environment variable or file
- **No async/await in UI thread**: Use events and background execution for SSH operations

## External Dependencies
- **SSH hosts**: Connects to user-specified SSH servers (network devices, Linux hosts, etc.)
- **Webhooks**: Script commands can POST to external URLs
- **GitHub**: Update checking via GitHub releases API
