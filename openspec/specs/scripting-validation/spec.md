# scripting-validation Specification

## Purpose
TBD - created by archiving change add-execution-safety-and-ux. Update Purpose after archive.
## Requirements
### Requirement: Script validation action
The system SHALL provide a script validation action that parses and validates YAML scripts without executing commands.

#### Scenario: Validation errors
- **WHEN** the user validates a script containing syntax or semantic errors
- **THEN** the validation errors are reported to the user with line numbers and messages

### Requirement: Validation success reporting
Successful script validation SHALL report a clear success message without executing any script steps.

#### Scenario: Valid script
- **WHEN** the user validates a well-formed script
- **THEN** the application reports validation success and performs no SSH actions

### Requirement: Non-fatal parser warnings
Script validation SHALL report unknown YAML keys as warnings with line context while preserving parse success.

#### Scenario: unknown step key warning
- **WHEN** a script includes an unrecognized key in a step mapping
- **THEN** validation reports a warning containing the key name and line number
- **AND** the script remains parseable for execution

### Requirement: YAML detection guidance alignment
Operator-facing scripting documentation SHALL match runtime YAML detection behavior.

#### Scenario: metadata-only command text
- **WHEN** command text contains metadata words like `name:`, `description:`, or `environment:` without strong script indicators
- **THEN** documentation states that metadata words alone do not guarantee YAML script detection

### Requirement: Supported shorthand syntax acceptance
Script validation SHALL accept supported shorthand aliases for converted commands and evaluate them using canonical command semantics.

#### Scenario: Inline send shorthand
- **WHEN** a script uses `- send: echo hi`
- **THEN** validation accepts the step when other send constraints are satisfied
- **AND** execution semantics match `send.command: echo hi`

#### Scenario: Inline while shorthand with do block
- **WHEN** a script uses `while: condition` with sibling `do`
- **THEN** validation accepts the step when `do` is present
- **AND** validation still enforces loop-structure requirements

### Requirement: Canonical payload key diagnostics
Validation SHALL report missing required primary payload keys for converted commands.

#### Scenario: Missing send.command
- **WHEN** a `send` map omits `command`
- **THEN** validation reports a required-key error for `send.command`
- **AND** script execution does not proceed with that invalid step

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

### Requirement: Readfile picker-aware validation
Script validation SHALL treat `readfile.path` as conditionally required based on `readfile.select_file`, and SHALL accept picker-specific customization keys on `readfile` steps.

#### Scenario: Standard readfile still requires path
- **WHEN** a `readfile` step omits `path`
- **AND** `select_file` is not `true`
- **THEN** validation reports that `readfile` requires `path`

#### Scenario: Picker mode allows omitted path
- **WHEN** a `readfile` step sets `select_file: true`
- **AND** omits `path`
- **THEN** validation accepts the step if `into` is present

#### Scenario: Picker customization keys are accepted
- **WHEN** a `readfile` step includes `message` and `fileext`
- **THEN** validation accepts those keys as part of the supported `readfile` contract

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

### Requirement: Preconnect section validation
Script validation SHALL support an optional top-level `preconnect` section authored as a YAML sequence of steps.

#### Scenario: Invalid preconnect shape is rejected
- **WHEN** a script defines `preconnect` as a non-sequence value
- **THEN** validation reports that `preconnect` must be a sequence of steps

### Requirement: Preconnect step compatibility checks
Validation SHALL reject `preconnect` steps that require an active SSH shell session.

#### Scenario: Interactive shared/send not allowed in preconnect
- **WHEN** `preconnect` contains `send` or other SSH-session-dependent commands
- **THEN** validation reports that the command is not allowed in `preconnect`
- **AND** validation includes line context

### Requirement: Reserved override variable diagnostics
Validation and parser diagnostics SHALL preserve reserved override variable names exactly as authored so operators can diagnose auth bootstrap scripts.

#### Scenario: Typo in reserved override variable
- **WHEN** a script sets `_ssh_identitty_file` (typo)
- **THEN** validation does not silently map it to `_ssh_identity_file`
- **AND** downstream execution behaves according to normal variable resolution semantics

