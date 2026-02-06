# Changelog

## Changes Since `cc99f52` (0.50.18)

### JSON Scripting Engine

A comprehensive JSON manipulation library has been added to the scripting engine, providing 20+ functions for working with structured data:

- **Object & Array Construction** — `json()` creates objects from key-value pairs or arrays from lists
- **Path-Based Access** — `json.get()`, `json.set()`, `json.delete()` operate on nested structures using dot-path notation (e.g., `data.items[0].name`)
- **Deep Merge** — `json.merge()` combines multiple objects with recursive merging
- **Introspection** — `json.type()`, `json.exists()`, `json.len()`, `json.keys()`, `json.values()`, `json.items()` for querying structure
- **Array Operations** — `json.push()`, `json.pop()`, `json.unshift()`, `json.shift()`, `json.slice()`, `json.concat()`, `json.indexOf()` for array manipulation
- **Formatting** — `json.format()` for pretty-printing or compacting JSON output

Nested dot-path assignment is now supported in `set:` commands (e.g., `obj.key.subkey = value`), with intermediate objects created automatically.

### WriteFile Format Support

`writefile:` now supports four output formats:

| Format | Description |
|--------|-------------|
| **json** | Valid JSON output with smart append-mode merging (arrays concatenate, objects deep-merge) |
| **jsonl** | JSON Lines format, one object per line with proper boundary handling on append |
| **csv** | CSV with automatic header extraction from JSON arrays of objects, proper escaping, and nested array flattening |
| **text** | Plain text (existing behavior) |

### Pre-Execution Column Validation

A new static analysis system inspects scripts before execution to identify which grid columns are referenced. If a script references columns that don't exist in the grid, a warning dialog lists the missing columns and allows the user to proceed or cancel. This prevents silent failures where column variables would resolve to empty strings.

### Command Editor Context Menu

The command text box now has a right-click context menu with:

- Standard editing operations (Cut, Copy, Paste, Select All)
- **Pretty Format** — Reformats YAML scripts while preserving user-placed comments, blank lines, and document markers
- **Validate Script** — Checks script syntax before execution

### Terminal Output Improvements

- **Trailing prompt stripping** — Command output now automatically strips trailing shell prompt lines, including metadata lines from modern prompts like Starship (timestamps, context info)
- **Cleaner captured data** — Prevents prompt artifacts from appearing in variables set from command output

### Variable Syntax

`{{variable_name}}` syntax is now supported everywhere alongside the existing `${variable_name}` syntax, including in SSH session variable substitution.

### Environment Variable Expansion

File paths in `readfile:` commands now expand Windows environment variables (`%TEMP%`, `%APPDATA%`, `%USERPROFILE%`, etc.) after script variable substitution.

### Command Normalization

All preset command text is automatically normalized to Windows line endings (CRLF), regardless of source. This prevents inconsistencies when importing presets or pasting commands from different platforms.

### Host Grid Context Menu

Separators in the host grid context menu are now shown/hidden dynamically based on which actions are available, preventing empty separator lines when menu items aren't visible.

### Documentation

New "Quoting and Escaping" section added to SCRIPTING.md, documenting YAML string literal rules — when to use double quotes (for escape sequences like `\n`, `\t`) vs. single quotes (for literal backslashes and regex patterns).

### Test Coverage

New unit tests added across the scripting subsystem covering:

- PresetInfo command normalization
- Expression evaluation with parenthesized grouping
- ExtractCommand with multiple capture groups
- ReadFileCommand with environment variable expansion
- ScriptContext dynamic array indexing and nested interpolation
- ScriptPrettyFormatter comment and whitespace preservation
- SetCommand JSON construction, list operations, and interpolation
- WriteFileCommand JSONL, CSV, and append-mode behavior
- TerminalOutputProcessor ANSI handling, cursor operations, and pager artifacts
