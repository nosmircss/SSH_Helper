## ADDED Requirements
### Requirement: Interactive capture autocomplete vocabulary
The script editor autocomplete vocabulary SHALL include interactive capture-mode keys and values.

Editor completion contract for `interactive`:
- option keys include `session`, `command`, `capture`, `max_seconds`, `mirror_output`, and `on_error`
- value suggestions include `mirror_output: true|false`

#### Scenario: Interactive option completion includes capture keys
- **WHEN** caret is inside an `interactive` map
- **THEN** option completion includes `command`, `capture`, `max_seconds`, and `mirror_output`

#### Scenario: Mirror output value completion suggests booleans
- **WHEN** caret is at `interactive.mirror_output` value position
- **THEN** completion suggestions include `true` and `false`
