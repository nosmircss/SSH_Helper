## ADDED Requirements
### Requirement: Interactive command editor vocabulary
The script editor autocomplete and syntax-highlighting vocabulary SHALL include the `interactive` command and its option/value tokens.

The editor SHALL surface:
- step command: `interactive`
- option keys inside `interactive` map: `session`, `emulation`, `on_error`
- enum-like value suggestions:
  - `session`: `separate`, `shared`
  - `emulation`: `full`

#### Scenario: Step command completion includes interactive
- **WHEN** caret is at step-command completion position
- **THEN** autocomplete suggestions include `interactive`

#### Scenario: Interactive option completion and values
- **WHEN** caret is inside an `interactive` command map
- **THEN** autocomplete suggests `session` and `emulation`
- **AND** value completion for `session` and `emulation` suggests their allowed enum values
