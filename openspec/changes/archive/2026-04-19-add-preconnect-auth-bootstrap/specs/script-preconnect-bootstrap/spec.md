## ADDED Requirements

### Requirement: Preconnect script phase
The system SHALL support an optional top-level `preconnect` step list in YAML scripts.

`preconnect` SHALL execute once per target host before any SSH connection/login attempt for that host when the script requires an SSH session.

If the script does not require SSH and runs in local-only mode, `preconnect` SHALL still execute before main `steps` for that host.

#### Scenario: Preconnect runs before SSH login for mixed script
- **WHEN** a script defines `preconnect` and also contains at least one `send` command
- **THEN** the runtime executes `preconnect` for each target host before calling SSH login
- **AND** only after preconnect completes successfully does it establish the SSH session and run main steps

#### Scenario: Script without preconnect preserves behavior
- **WHEN** a script omits `preconnect`
- **THEN** execution order remains unchanged from existing behavior

### Requirement: Auth override contract from preconnect
The runtime SHALL support reserved variables set during `preconnect` to override host auth inputs for the current host execution.

Reserved variables:
- `_ssh_identity_file`
- `_ssh_identity_passphrase`
- `_ssh_username`
- `_ssh_password`

Override values SHALL apply only to the current host execution and SHALL NOT persist across different hosts.

#### Scenario: Preconnect sets identity file and passphrase
- **WHEN** `preconnect` sets `_ssh_identity_file` and `_ssh_identity_passphrase`
- **THEN** SSH login for that host uses key-based authentication with those resolved values

#### Scenario: Preconnect sets username/password fallback
- **WHEN** `preconnect` sets `_ssh_username` and `_ssh_password` and no identity override is provided
- **THEN** SSH login uses those credentials for that host

### Requirement: Preconnect command safety constraints
The `preconnect` phase SHALL reject commands that require an active SSH shell session.

#### Scenario: Send command inside preconnect is invalid
- **WHEN** a script includes `send` within `preconnect`
- **THEN** validation reports a preconnect unsupported-command error with line context
- **AND** execution does not start for that script
