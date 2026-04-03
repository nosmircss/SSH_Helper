## ADDED Requirements

### Requirement: Exists command validation
Script validation SHALL support `exists` as a command-map step with required keys and constrained enum values.

Validation rules:
- `exists` MUST be authored as a mapping.
- `exists.path` is required and must be non-empty.
- `exists.into` is required and must be a valid variable name.
- `exists.type`, when provided, MUST be one of `any`, `file`, `directory`.
- Allowed keys under `exists` are `path`, `into`, `type`, and `on_error`.

#### Scenario: Missing path is rejected
- **WHEN** an `exists` step omits `path`
- **THEN** validation reports a required-key error for `exists.path`

#### Scenario: Missing into is rejected
- **WHEN** an `exists` step omits `into`
- **THEN** validation reports a required-key error for `exists.into`

#### Scenario: Invalid type is rejected
- **WHEN** an `exists` step sets `type` to a value outside `any|file|directory`
- **THEN** validation reports explicit allowed values

#### Scenario: Unknown key under exists is rejected
- **WHEN** an `exists` step contains an unsupported key
- **THEN** validation reports an unsupported-key error for that key
