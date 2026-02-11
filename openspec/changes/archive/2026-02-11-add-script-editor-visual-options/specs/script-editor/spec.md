## ADDED Requirements
### Requirement: Configurable script editor visual options
The Command Editor settings experience SHALL expose configurable visual options for script-structure readability and focus guidance.

The persisted `CommandEditorSettings` model SHALL include:
- `EnableCurrentLineHighlight` (default `true`)
- `EnableIndentGuides` (default `false`)
- `ShowWhitespace` (default `false`)
- `EnableLongLineGuide` (default `false`)
- `LongLineColumn` (default `120`, allowed range `80..200`)
- `EnableCodeFolding` (default `false`)
- `EnableBraceMatching` (default `true`)

#### Scenario: Visual options persist across restart
- **WHEN** an operator changes one or more visual option settings and saves
- **THEN** those settings are written to configuration
- **AND** the same values are restored when the app restarts

#### Scenario: Long-line guide column is bounded
- **WHEN** `LongLineColumn` is saved outside the allowed range
- **THEN** configuration normalization clamps the value into `80..200`
