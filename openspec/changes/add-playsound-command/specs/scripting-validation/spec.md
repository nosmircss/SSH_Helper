## ADDED Requirements

### Requirement: Playsound command validation
Script validation SHALL support `playsound` as a command-map step with required keys and constrained option values.

Validation rules:
- `playsound` MUST be authored as a mapping.
- `playsound.path` is required and must be non-empty.
- Allowed keys under `playsound` are `path`, `wait`, `volume`, `max_seconds`, `into`, and `on_error`.
- `playsound.wait`, when provided, MUST be a boolean-like token (`true|false|yes|no|1|0`).
- `playsound.volume`, when provided, MUST parse to an integer in range `0..100`.
- `playsound.max_seconds`, when provided, MUST parse to a positive integer (`> 0`).

#### Scenario: Missing path is rejected
- **WHEN** a `playsound` step omits `path`
- **THEN** validation reports a required-key error for `playsound.path`

#### Scenario: Invalid volume is rejected
- **WHEN** a `playsound` step sets `volume` outside `0..100`
- **THEN** validation reports a range error for `playsound.volume`

#### Scenario: Invalid max_seconds is rejected
- **WHEN** a `playsound` step sets `max_seconds` to `0`, negative, or non-numeric text
- **THEN** validation reports that `playsound.max_seconds` must be a positive integer

#### Scenario: Unknown key under playsound is rejected
- **WHEN** a `playsound` step contains an unsupported key
- **THEN** validation reports an unsupported-key error for that key
