## ADDED Requirements
### Requirement: Scintilla visual aids are settings-driven
The Scintilla script editor SHALL apply visual aids from persisted Command Editor settings and SHALL allow operators to disable them.

Visual aids in scope:
- current-line highlight
- indentation guides
- whitespace markers
- long-line edge ruler
- fold margin and fold markers
- brace matching highlight

#### Scenario: Visual aids toggle at runtime
- **WHEN** an operator changes visual option settings and returns to the editor
- **THEN** Scintilla visual aid rendering updates to match saved settings
- **AND** editor restart is not required

#### Scenario: Folding can be disabled
- **WHEN** `EnableCodeFolding` is false
- **THEN** the fold margin is hidden
- **AND** fold markers are not shown in the script editor gutter
