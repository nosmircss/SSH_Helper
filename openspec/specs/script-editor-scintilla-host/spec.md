# script-editor-scintilla-host Specification

## Purpose
TBD - created by archiving change replace-script-editor-with-scintilla5. Update Purpose after archive.
## Requirements
### Requirement: Scintilla-based script editor engine
The application SHALL provide a Scintilla5.NET-backed script editor implementation for the Commands editor surface.

The Scintilla implementation SHALL integrate through the existing `IScriptEditor` abstraction so script editing workflows in `Form1` remain functional.

#### Scenario: Scintilla editor is used for command authoring
- **WHEN** the operator edits script content in the Commands pane
- **THEN** editing is performed by the Scintilla-based editor implementation
- **AND** existing script authoring actions remain available through `IScriptEditor`

### Requirement: Familiar editor interaction model
The Scintilla editor SHALL provide a modern code-editor interaction baseline for caret movement, selection, and editing operations.

The baseline SHALL include:
- predictable caret navigation with keyboard and mouse
- standard selection growth/shrink interactions
- consistent undo/redo behavior for text edits, smart-enter edits, and indentation edits.

#### Scenario: Cursor movement remains predictable
- **WHEN** an operator navigates using arrow keys, word-navigation keys, and mouse clicks
- **THEN** the caret lands at the expected target position without unexpected jumps

#### Scenario: Undo and redo restore edits in expected order
- **WHEN** an operator performs typing, indentation, and smart-enter actions
- **THEN** undo and redo return the document/caret to prior states in intuitive step order

### Requirement: Parser-driven behavior parity
Autocomplete and diagnostics in the Scintilla editor SHALL remain parser-driven from existing C# services and SHALL NOT use an independent hard-coded command grammar.

#### Scenario: Parser metadata drives suggestions
- **WHEN** parser/runtime metadata defines valid command and option contracts
- **THEN** Scintilla completion suggestions reflect those contracts without separate static command arrays

### Requirement: Scroll and caret visibility parity
The Scintilla editor SHALL support scroll-past-end behavior and deterministic caret reveal when adding new lines at end-of-file.

`ScrollPastEnd` behavior SHALL be enabled by default so the final content line can be positioned away from the viewport bottom.

#### Scenario: Last line can be positioned near top
- **WHEN** the operator scrolls to the end of a script
- **THEN** the last content line can be positioned near the top of the visible viewport

#### Scenario: Enter on final line reveals new line
- **WHEN** the caret is on the final line and the operator presses `Enter`
- **THEN** the editor updates viewport position so the caret remains visible on the inserted line

### Requirement: Completion interaction stability
Autocomplete suggestions in the Scintilla editor SHALL not block normal typing and SHALL close when the user explicitly relocates the caret by mouse click.

Completion interaction SHALL follow this commit/dismiss contract:
- `Enter` or `Tab` commits the selected suggestion when completion is open
- `Escape` dismisses completion without mutating text
- when completion is closed, `Enter` follows smart-enter/newline behavior and `Tab` follows indentation behavior.

#### Scenario: Typing continues while suggestions are open
- **WHEN** suggestions are visible and the operator continues typing
- **THEN** typed input is inserted in the editor normally
- **AND** suggestion filtering updates without stealing input focus

#### Scenario: Click-to-reposition closes suggestions
- **WHEN** suggestions are visible and the operator clicks to move the caret
- **THEN** the suggestion popup closes immediately

#### Scenario: Enter commits suggestion only when completion is active
- **WHEN** completion is open with a selected suggestion and the operator presses `Enter`
- **THEN** the selected suggestion is committed
- **AND** a newline is not inserted by that keypress

### Requirement: Command editor settings compatibility
Scintilla editor behavior SHALL continue to honor existing persisted command editor settings, including syntax highlighting, autocomplete, inline validation, tooltips, indentation, and smart-enter toggles.

#### Scenario: Existing settings continue to apply
- **WHEN** an operator has non-default Command Editor settings saved
- **THEN** the Scintilla editor applies those settings without requiring reset/reconfiguration

### Requirement: Familiar responsiveness under script load
The Scintilla editor SHALL remain responsive for large scripts and active completion/diagnostics workflows used during authoring.

At minimum, responsiveness verification SHALL include:
- rapid typing in scripts with at least 500 lines
- completion popup updates under active typing
- newline insertion at end-of-file with caret reveal.

In the defined reference performance profile, the implementation SHALL meet these budgets:
- keystroke-to-visible-text latency p95 <= 50 ms
- completion update latency p95 <= 120 ms after qualifying keystrokes
- end-of-file `Enter` caret reveal <= 100 ms.

#### Scenario: Rapid typing with diagnostics enabled
- **WHEN** inline diagnostics and autocomplete are enabled for a script of at least 500 lines
- **THEN** typed characters continue to appear without dropped input
- **AND** the editor remains interactively usable without visible blocking stalls

#### Scenario: Reference profile latency budget check
- **WHEN** responsiveness is measured against the project-defined reference performance profile
- **THEN** editor latency measurements meet the specified keystroke, completion, and EOF-reveal budgets

### Requirement: Portable deployment compatibility
The Scintilla editor integration SHALL remain compatible with portable application distribution and SHALL NOT require external machine-level editor runtimes.

#### Scenario: Portable zip deployment
- **WHEN** the app is launched from a portable extracted folder on a target machine with .NET prerequisites
- **THEN** the Scintilla editor loads and functions without separate editor runtime installation steps

