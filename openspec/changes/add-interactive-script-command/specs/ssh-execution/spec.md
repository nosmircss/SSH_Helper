## ADDED Requirements
### Requirement: Interactive run-context preflight restrictions
Scripts that use `interactive` SHALL be restricted to single-host current-host execution mode.

Preflight rules:
- Multi-host script runs containing `interactive` MUST fail before execution.
- Folder/preset-batch runs containing `interactive` MUST fail before execution.
- Failure messages MUST clearly state that `interactive` is single-host only.

#### Scenario: Multi-host run with interactive script
- **WHEN** an operator executes a script containing `interactive` against more than one host
- **THEN** execution is rejected in preflight
- **AND** each targeted host result indicates an interactive single-host restriction error

#### Scenario: Folder run with interactive script preset
- **WHEN** a folder execution includes a YAML script preset containing `interactive`
- **THEN** that preset execution is rejected before SSH connection attempts
- **AND** result output indicates interactive commands are not supported in folder runs
