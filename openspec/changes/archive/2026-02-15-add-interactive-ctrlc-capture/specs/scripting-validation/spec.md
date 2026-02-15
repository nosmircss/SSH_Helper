## ADDED Requirements
### Requirement: Interactive capture option validation
Script validation SHALL enforce interactive capture option contracts.

Validation rules:
- `interactive` remains map-only.
- Allowed keys under `interactive` include `session`, `command`, `capture`, `max_seconds`, `mirror_output`, and `on_error`.
- When `interactive.command` is present, `interactive.session` MUST be `separate`.
- When provided, `interactive.max_seconds` MUST be a positive integer (`> 0`).

#### Scenario: Shared session with command is rejected
- **WHEN** a script sets `interactive.command` and `interactive.session: shared`
- **THEN** validation reports that command mode requires `session: separate`

#### Scenario: Non-positive max_seconds is rejected
- **WHEN** a script sets `interactive.max_seconds` to `0` or a negative value
- **THEN** validation reports that `interactive.max_seconds` must be greater than zero

#### Scenario: Map-only interactive contract remains enforced
- **WHEN** a script authors `interactive` as a scalar
- **THEN** validation reports that `interactive` must be a mapping with supported keys
