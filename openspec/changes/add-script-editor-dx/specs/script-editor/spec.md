## ADDED Requirements

### Requirement: Dedicated script editor control with workflow parity
The script authoring surface SHALL use a dedicated code-editor control via the `IScriptEditor` abstraction instead of a plain `TextBox`.

The replacement SHALL preserve existing authoring workflow behaviors used in `Form1`, including copy/cut/paste/select-all, selection tracking, line/column display, context menu actions, and keyboard save flow.

#### Scenario: Replace textbox without losing baseline editing
- **WHEN** an operator edits scripts in the main window
- **THEN** copy, cut, paste, select-all, selection tracking, and keyboard editing behave consistently with the prior workflow

#### Scenario: Save shortcut parity
- **WHEN** the script editor has focus and the operator presses `Ctrl+S`
- **THEN** preset-save behavior executes with the same gating used by the existing script editor workflow

### Requirement: YAML/script syntax highlighting
The editor SHALL apply syntax highlighting for YAML structure and scripting tokens, including top-level keys, step commands, step options, variables (`${...}` and `{{...}}`), strings, numbers, and comments.

Highlighting updates SHALL be scoped to changed ranges instead of forcing full-document recolor on every keystroke.

#### Scenario: Highlight step command and variable tokens
- **WHEN** a script line contains a step key and `${variable}` references
- **THEN** the command key and variable tokens are visually distinguished from plain text

### Requirement: Parser-driven completion vocabulary
Autocomplete command and option vocabularies SHALL be derived from scripting parser/model metadata rather than maintained as hard-coded command arrays in editor code.

#### Scenario: Completion source tracks parser contracts
- **WHEN** parser/model metadata defines a valid step command and option key
- **THEN** the editor can include those values in completion suggestions without requiring a separate hard-coded editor command list

### Requirement: Context-aware autocomplete
The editor SHALL provide context-aware completion for:
- top-level script keys
- step command keys at step-start positions
- step option keys and enum-like option values
- `${...}` script variables and runtime built-ins (`_output`, `_timestamp`, `_iteration`, `_last_error`)
- `{{...}}` host-grid column placeholders.

Dynamic variable suggestions SHALL include names discovered from current script text (`vars`, `set`, `capture`, and `into` outputs).

#### Scenario: Step completion inside steps block
- **WHEN** the caret is at the start of a step item in the `steps` list
- **THEN** completion suggestions include valid step command names

#### Scenario: Variable completion after interpolation start
- **WHEN** the operator types `${`
- **THEN** completion suggestions include known script variables and runtime built-ins

#### Scenario: Grid placeholder completion
- **WHEN** the operator types `{{`
- **THEN** completion suggestions include current host-grid column names

### Requirement: Debounced inline validation pipeline
The editor SHALL run inline validation asynchronously with trailing debounce and stale-work cancellation.

Validation pipeline contract:
- Debounce interval: `400ms` trailing
- Maximum one in-flight validation task
- New edits cancel older pending/running validation (last-edit-wins)
- YAML parsing/validation for inline diagnostics runs only when `ScriptParser.IsYamlScript(text)` is true.

#### Scenario: Rapid typing cancels stale validation
- **WHEN** an operator types continuously
- **THEN** older pending/running validations are canceled
- **AND** only the latest text snapshot publishes diagnostics

### Requirement: Plain command mode behavior
For non-YAML command text, inline diagnostics SHALL be cleared and not continuously recomputed by the inline validator.

#### Scenario: Non-YAML text disables inline diagnostics
- **WHEN** editor content is not detected as YAML script text
- **THEN** inline markers are removed
- **AND** no parser-driven inline diagnostics are shown

### Requirement: Column-level diagnostics and hover details
Inline diagnostics SHALL be represented as structured editor diagnostics with line and column spans.

Each diagnostic SHALL include:
- `LineNumber` (1-based)
- `ColumnStart` (1-based)
- `ColumnEnd` (inclusive end column)
- severity (`Error`, `Warning`, or `Info`)
- message text for hover tooltip.

Parser validation output and parser warning output SHALL be mapped into these diagnostics.
If token localization is unavailable for a diagnostic, the editor SHALL use a line-span fallback (`ColumnStart=1`, `ColumnEnd=line length`) so diagnostics remain column-addressable.

#### Scenario: Validation error marker with column span
- **WHEN** script validation reports an error for a known option token on a line
- **THEN** the editor marks the token range with an error diagnostic
- **AND** hover displays the mapped message

#### Scenario: Warning marker mapping
- **WHEN** parser warnings report unknown keys with line context
- **THEN** the editor renders warning diagnostics with mapped line/column ranges and hover text

### Requirement: Variable inspector hover contract
Hover tooltips for variable tokens SHALL resolve values from deterministic sources:
- `${var}`: script/default/runtime symbol maps
- `{{column}}`: selected grid row value, with fallback to first non-new row when no row is selected.

Missing variables or columns SHALL be shown as unresolved in tooltip content.

#### Scenario: Column hover preview from selected row
- **WHEN** the operator hovers `{{column}}` and a host row is selected
- **THEN** tooltip preview uses that selected row value

### Requirement: Theme and font integration
The editor SHALL apply configured code font settings and respond to dark/light theme changes without requiring restart.

#### Scenario: Theme switch updates editor colors
- **WHEN** the operator toggles application theme
- **THEN** editor background/foreground and token colors update to the active palette

#### Scenario: Font settings update editor font
- **WHEN** code font family or size settings change
- **THEN** the editor applies updated code font settings consistently with other UI font updates

### Requirement: Large-script responsiveness
The editor SHALL remain responsive for scripts of at least 500 lines by combining changed-range highlighting with debounced asynchronous validation.

During sustained typing, inline validation SHALL not start more than once per debounce interval.

#### Scenario: Rapid typing in large script
- **WHEN** an operator types continuously in a script with 500 or more lines
- **THEN** typing remains interactive without UI-thread blocking waits for validation completion
- **AND** diagnostics update asynchronously after debounce intervals
