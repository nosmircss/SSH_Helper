## ADDED Requirements

### Requirement: Dedicated script editor control
The script authoring surface SHALL use a code-editor control instead of a plain textbox while preserving core edit operations used by the existing UI flow.

#### Scenario: Replace textbox without losing baseline editing
- **WHEN** an operator edits scripts in the main window
- **THEN** copy, cut, paste, select-all, selection tracking, and keyboard editing behave consistently with the prior workflow

### Requirement: Syntax highlighting for YAML scripting
The editor SHALL apply syntax highlighting for YAML structure and scripting tokens including step commands, options, variables, strings, numbers, and comments.

#### Scenario: Highlight step command and variable tokens
- **WHEN** a script line contains a step key and `${variable}` references
- **THEN** the command key and variable tokens are visually distinguished from plain text

### Requirement: Context-aware autocomplete
The editor SHALL provide context-aware completion for top-level keys, step commands, step options, valid option values, script variables, and host-grid column placeholders.

#### Scenario: Step completion inside steps block
- **WHEN** the caret is at the start of a step item in the `steps` list
- **THEN** completion suggestions include valid step command names

#### Scenario: Variable completion after interpolation start
- **WHEN** the operator types `${`
- **THEN** completion suggestions include known variables from script definitions and runtime built-ins

### Requirement: Inline validation diagnostics
The editor SHALL perform debounced script validation and render diagnostics with line-level visual markers and message tooltips.

#### Scenario: Validation error marker
- **WHEN** script validation detects a syntax or semantic error
- **THEN** the affected line is marked in the editor
- **AND** hovering the marker shows the validation message

### Requirement: Theme and font setting integration
The editor SHALL apply configured code-font settings and respond to dark/light theme changes without requiring an application restart.

#### Scenario: Theme switch updates editor colors
- **WHEN** the operator toggles application theme
- **THEN** the editor updates foreground/background and token colors to the active theme palette

### Requirement: Large script responsiveness
The editor SHALL remain responsive for scripts of at least 500 lines by limiting expensive syntax and validation work to changed content and debounced refresh cycles.

#### Scenario: Rapid typing in large script
- **WHEN** an operator types continuously in a script with 500 or more lines
- **THEN** keystroke handling remains responsive
- **AND** diagnostics update asynchronously after debounce intervals
