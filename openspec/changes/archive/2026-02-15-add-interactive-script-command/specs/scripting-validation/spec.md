## ADDED Requirements
### Requirement: Interactive command shape and option validation
Script validation SHALL enforce strict shape and option contracts for the `interactive` command.

Validation rules:
- `interactive` MUST be authored as a mapping value (`- interactive: { ... }` or multi-line map).
- Scalar shorthand (`- interactive: ...`) is invalid.
- Allowed keys under `interactive` are `session`, `emulation`, and `on_error`.
- Unknown keys under `interactive` are validation errors.
- `session` MUST be `separate` or `shared`.
- `emulation` MUST be `full`.
- If omitted, defaults are `session=separate` and `emulation=full`.

#### Scenario: Defaults are applied for empty interactive map
- **WHEN** script author uses `- interactive: {}`
- **THEN** validation succeeds
- **AND** runtime options default to `session=separate` and `emulation=full`

#### Scenario: Scalar shorthand is rejected
- **WHEN** script author uses `- interactive: true`
- **THEN** validation reports that `interactive` must be a mapping

#### Scenario: Invalid enum values are rejected
- **WHEN** script author sets `session` or `emulation` to unsupported values
- **THEN** validation reports explicit allowed values for each option

#### Scenario: Unknown key under interactive is rejected
- **WHEN** script author includes an unrecognized key under `interactive`
- **THEN** validation reports an unsupported-key error for that key
