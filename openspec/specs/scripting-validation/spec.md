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
- **WHEN** command text contains metadata words like `name:` or `description:` without strong script indicators
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

