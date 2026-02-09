## ADDED Requirements
### Requirement: Smart-enter supports command-map option authoring
When editing command-map payloads, the editor SHALL prioritize option-level continuation on `Enter`.

#### Scenario: Enter after send.command line
- **WHEN** caret is at the end of `send.command` line inside a `send` map
- **THEN** pressing `Enter` inserts a new line at the same option indentation level
- **AND** does not immediately insert a new sibling `- ` step prefix

### Requirement: Explicit next-step insertion shortcut
The editor SHALL provide an explicit shortcut for quickly creating the next sibling step.

#### Scenario: Ctrl+Enter inserts sibling step
- **WHEN** caret is inside a command-map payload block
- **THEN** pressing `Ctrl+Enter` inserts a sibling step line beginning with `- ` at step indentation
- **AND** caret lands after the inserted step prefix
