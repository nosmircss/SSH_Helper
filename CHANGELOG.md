# Changelog

## Changes Since `86f4dc2` (0.51.3)

### Interactive Scripting Commands

Three new scripting commands let scripts prompt users for input during execution:

**`choose` — Single-Select from List**

Presents a dialog where the user picks one option from a list. Options can be simple strings or label/value pairs with a different display label from the stored value. Supports a `default` pre-selection and variable substitution in prompts and option text.

```yaml
- choose:
    prompt: "Select management protocol:"
    into: mgmt_port
    options:
      - label: "SSH (22)"
        value: "22"
      - label: "HTTPS (443)"
        value: "443"
    default: "22"
```

**`multiselect` — Multiple-Select from Checklist**

Presents a checkbox list for selecting multiple items. Stores the result as a list accessible via `${var[0]}`, `${var.length}`, and `foreach` iteration. Also sets `${var}_count`. Supports optional `min`/`max` selection constraints with inline validation.

```yaml
- multiselect:
    prompt: "Select interfaces to configure:"
    into: selected_ifaces
    options:
      - GigabitEthernet0/0
      - GigabitEthernet0/1
      - Loopback0
    min: 1
    max: 3
```

**`confirm` — Yes/No Confirmation**

Presents a simple yes/no dialog. Stores `"true"` or `"false"` as a string. Unlike `input`, confirm never fails — it always stores a value regardless of which button is pressed. The `default` field controls which button is pre-focused.

```yaml
- confirm:
    prompt: "Apply configuration changes?"
    into: confirmed
    default: false
```

All three commands:
- Support variable substitution in prompts and option text
- Respect `on_error: continue` for error handling
- Auto-detect dark mode and render themed dialogs
- Integrate with the dependency analyzer for column reference tracking

### Local Script Execution

Scripts that don't require an SSH session are now detected and executed locally without establishing an SSH connection. A static analyzer walks the parsed script tree and checks whether any `send` or default-host `sftp` steps are present.

When a script contains only local commands (e.g., `set`, `print`, `choose`, `http`, `dns`, `readfile`, `writefile`, control flow), it runs in a local execution path that:
- Skips SSH connection setup entirely
- Skips invalid host validation when no SSH session is needed
- Shows a `LOCAL SCRIPT` banner instead of the SSH connection header
- Still receives host context variables (IP, columns, environment variables)

This means scripts that only do local work (file processing, HTTP calls, user prompts, variable manipulation) no longer require valid SSH credentials or reachable hosts.

### List Variable Rendering

`ScriptContext.GetVariableString` now joins `List<string>` values with `, ` when interpolated via `${var}`. This makes multiselect results and DNS result lists readable in `print` and `log` output without manual iteration.

### QA Presets

Three new QA presets added under `QA/Interactive`:
- **QA Choose Basic** — Tests simple options, label/value pairs, default selection, and conditional branching
- **QA Multiselect Basic** — Tests min/max constraints, count variable, foreach iteration, and index access
- **QA Confirm Basic** — Tests default values, conditional branching, and value validation

### Documentation

`SCRIPTING.md` updated with full reference sections for `choose`, `multiselect`, and `confirm` including syntax, parameter tables, feature notes, and usage examples.

---

## Changes Since `f34fb7c` (0.51.0)

### Environment Management

A full environment system allows managing multiple named profiles (e.g., dev, staging, prod), each with independent host grids, variables, and visual identity.

- **Environment profiles** — Each environment stores its own host grid columns, host entries, selected host indices, last CSV path, and a set of key-value variables
- **Toolbar integration** — A dropdown button on the toolbar shows the active environment name with an optional color swatch; switching environments swaps the entire host grid and variable context
- **Management dialog** — A dedicated resizable dialog provides CRUD operations: create, duplicate, rename, delete, and edit description, label color, and variables per environment
- **Import/Export** — Environments serialize to `.sshenv.json` files for sharing across machines or teams, with conflict resolution on import (overwrite or rename)
- **Variable scoping** — Each environment has its own variable dictionary; active environment variables are injected into SSH execution context and script runtime
- **Script integration** — A new `updateenvironment:` command allows YAML scripts to persist variable updates back to the active environment during execution, with the updated value immediately available to subsequent steps
- **Label colors** — Optional ARGB color per environment provides at-a-glance identification in the toolbar dropdown and management dialog list
- **Window title** — The application title bar now shows the active environment name
- **Default environment** — A reserved "Default" environment is always present and cannot be renamed or deleted; legacy state is automatically captured into Default on first use

### Multi-Protocol Network Commands

Six new scripting commands extend workflow capabilities beyond SSH:

| Command | Protocol | Captures | Key Capabilities |
|---------|----------|----------|------------------|
| `http:` | HTTP/HTTPS | body, status code, headers | GET/POST/PUT/PATCH/DELETE/HEAD/OPTIONS, Basic/Bearer auth, custom headers, TLS control, redirect following |
| `dns:` | DNS | record list, count | A/AAAA/PTR lookups, returns empty list (not error) when no records found |
| `ping:` | ICMP | status, avg latency, packet loss % | Multi-probe aggregation with per-probe timeout |
| `portcheck:` | TCP | status (open/closed/timeout), latency | Connection timing with configurable timeout |
| `sftp:` | SFTP over SSH | status, bytes transferred | Upload/download with endpoint override, environment variable expansion in paths |
| `updateenvironment:` | N/A | N/A | Persists a variable to the active environment and updates the running script context |

All network commands support:
- **Variable capture** via `into:` with command-specific suffixed derivatives (e.g., `${result}_status`, `${result}_count`, `${result}_avg`, `${result}_loss`)
- **Error handling** via step-level `on_error: continue` to suppress failures
- **Variable substitution** in all user-provided fields (`${var}` and `{{var}}`)
- **Cancellation** through linked cancellation tokens respecting both script-level and per-command timeouts

### SFTP Backend: SSH.NET

The SFTP runtime backend has been switched from Rebex SFTP to SSH.NET (`Renci.SshNet`). SFTP operations no longer depend on the Rebex SFTP package or its licensing. Endpoint resolution follows a priority chain: explicit `host`/`port`/`username`/`password` options, then host context variables from the grid, then toolbar defaults.

### Scintilla5.NET Script Editor

The command editor has been replaced with a Scintilla5.NET-powered control, providing a code-editor-grade authoring experience for YAML scripts.

**Syntax highlighting** — Eight token types with dual color palettes for light and dark themes: top-level keys, step commands, step options, variables (`${...}` / `{{...}}`), string literals, numbers, booleans/null, and comments. Highlighting is scoped to known parser keywords and re-paints only changed lines for performance.

**Context-aware autocomplete** — Suggestions adapt to structural position in the YAML document:

| Context | Trigger | Suggestions |
|---------|---------|-------------|
| Root level | Typing at indent 0 | `steps`, `vars`, `description`, `timeout`, etc. |
| Step command | After `- ` at step indent | `send`, `capture`, `set`, `http`, `ping`, `dns`, etc. |
| Step option | Indented under a command | Command-specific options (e.g., `capture`, `timeout`, `on_error` for `send`) |
| Option value | After `key: ` | Enum-like values (e.g., `continue`/`stop` for `on_error`) |
| Interpolation | Inside `${...}` or `{{...}}` | Built-in symbols, script-declared variables, grid column names |

Autocomplete commits with Enter/Tab and auto-appends `: ` after key completions. The popup is non-activating so typing is never interrupted.

**Inline diagnostics** — Real-time validation with debounced re-parsing surfaces errors (red squiggle underlines) and warnings (yellow squiggles) directly in the editor. Hover tooltips show the diagnostic message. Optional YAML hygiene warnings flag tab indentation, mixed indent styles, and duplicate keys within the same scope.

**Variable inspector tooltips** — Hovering over `${var}` or `{{column}}` tokens shows a tooltip with the resolved value from vars, environment variables, or grid preview data.

**Smart editing** — Tab/Shift+Tab indent/outdent selected lines by configurable spaces. Enter inserts context-aware indentation based on YAML structure (deeper after `:`, sibling after `-`). Blank-line preservation between steps is supported.

**Theme support** — Full dark and light mode theming for the editor, autocomplete popup, diagnostic indicators, and native scrollbars via Windows UX theme APIs.

### Command Editor Settings

A new "Command Editor" tab in Settings provides granular control over the script editor:

- **Features** — Toggle syntax highlighting, autocomplete, and auto-show-on-typing
- **Validation & Diagnostics** — Toggle inline validation, adjust debounce timing (150–2000ms), control warning visibility, enable/disable diagnostic and variable inspector tooltips, toggle YAML hygiene warnings
- **Indentation** — Choose spaces vs. tabs, set indent size (2–8), toggle smart-enter and blank-line preservation between steps

All settings persist in `config.json` under `CommandEditor` and apply immediately.

### Unified Command Map Syntax

All script commands now use a canonical map syntax where the command name is a YAML key and its options are nested underneath:

```yaml
# Canonical syntax (new default)
- send:
    command: show version
    capture: version_output
    on_error: continue

# Inline shorthand still accepted
- send: show version
```

The parser accepts both forms. All 26 bundled script samples and QA presets have been migrated to the canonical format.

### Context-Aware Preset Operations

Preset actions (duplicate, rename, delete, export) now resolve the target preset based on invocation context. Actions triggered from the context menu operate on the right-clicked item; toolbar actions operate on the active tab or tree selection. This prevents stale tree selection from causing operations to target the wrong preset.

After deleting a preset, the nearest item above the deleted entry is selected instead of clearing context.

### Execution Details Persistence

View Details metadata attached to history entries is now persisted in the configuration and restored into the history store at startup. Execution details survive application restart.

### Dialog Theming Improvements

- **Tab control styling** — Owner-drawn tab rendering with accent lines for dark and light modes
- **Themed message dialogs** — `DialogTheme.Confirm()` and `DialogTheme.ShowMessage()` provide dark-mode-aware confirmation and message dialogs with consistent fonts
- **Native scrollbar theming** — Recursive Windows UX theme application for scrollbars, checkboxes, radio buttons, combo boxes, and other native controls in dialogs
- **Dialog font propagation** — `DialogTheme.SetDialogFont()` applies fonts without triggering auto-scale relayout

### Font Settings

The Semibold font family resolution has been improved. `ResolveSemiboldFontFamily()` properly handles font names that already end with "Semibold" to prevent double-suffixing. A dedicated dialog font is now created and managed alongside other UI fonts.

### Pretty Format Removal

The Pretty Format feature (YAML reformatting via `ScriptPrettyFormatter`) has been removed along with its associated tests. The Scintilla-based editor with inline validation and smart editing replaces the need for bulk reformatting.

### Dependency Changes

| Package | Version | Purpose |
|---------|---------|---------|
| **Scintilla5.NET** | 6.1.1 | Script editor control (new) |
| **SSH.NET** | 2024.1.0 | SFTP backend, replacing Rebex for file transfers (new) |

### Script Samples

All 26 bundled script samples across bash, Cisco, Check Point, FortiGate, and generic categories have been migrated to the canonical command map syntax.

### Documentation

SCRIPTING.md has been substantially expanded with documentation for the new network commands (`http`, `dns`, `ping`, `portcheck`, `sftp`, `updateenvironment`), unified command map syntax, and updated examples throughout.

### License

An MIT license has been added to the repository.

### Test Coverage

New test suites added:

- **Editor** — `EditorTextUtilitiesTests`, `ScriptAutocompleteProviderTests`, `ScriptEditorValidationServiceTests`, `YamlSshSyntaxHighlighterTests`, `ScintillaScriptEditorControlTests`, `ScintillaScriptEditorPerformanceTests`
- **Scripting** — `CanonicalCommandMapSyntaxTests`, `ExitCommandTests`, `NetworkCommandTests`, `NetworkStepParserTests`, `ScriptDependencyAnalyzerTests`, `UpdateEnvironmentCommandTests`
- **Services** — `ConfigurationServiceCommandEditorSettingsTests`, `ConfigurationServiceExecutionDetailsTests`, `ConfigurationServiceWindowStateTests`, `EnvironmentServiceTests`
- **UI** — `SettingsDialogAppearanceTests` (expanded)

---

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
- SetCommand JSON construction, list operations, and interpolation
- WriteFileCommand JSONL, CSV, and append-mode behavior
- TerminalOutputProcessor ANSI handling, cursor operations, and pager artifacts
