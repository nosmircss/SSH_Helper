## ADDED Requirements
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
