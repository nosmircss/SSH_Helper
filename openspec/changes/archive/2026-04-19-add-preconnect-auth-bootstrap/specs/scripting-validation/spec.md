## ADDED Requirements

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
