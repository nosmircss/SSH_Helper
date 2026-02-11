## ADDED Requirements

### Requirement: Local script execution when SSH is not required
The system SHALL execute YAML scripts without establishing an SSH connection when static analysis determines that no commands in the script require an SSH shell session.

The analysis SHALL recursively inspect all steps in the parsed script, including steps nested within control-flow blocks (`if`/`elif`/`else`, `foreach`, `while`, `try`/`catch`/`finally`), and SHALL classify the script as SSH-required if any `send` command is found at any nesting depth.

Scripts containing only `sftp` (without `send`) SHALL NOT require an SSH shell session, since `sftp` creates its own independent connection using credentials from context variables.

When executing locally, the system SHALL set `ScriptContext.Session` to `null`, skip SSH connection/login/session initialization, and SHALL display a `LOCAL SCRIPT:` header prefix in output to distinguish from SSH-connected execution.

All existing execution infrastructure (event-driven progress/output reporting, cancellation, execution history recording, execution details metadata, debug mode output) SHALL function identically for local execution.

Simple (non-YAML) presets SHALL always require SSH and SHALL NOT be eligible for local execution.

Host rows in the grid SHALL still be required for local execution. The `Host_IP` column MAY contain any value (URL, reference ID, hostname) for tracking and variable substitution purposes. When a script does not require SSH AND no sftp step falls back to the default host, the `Host_IP` validation (hostname/IP format check) SHALL be skipped to allow arbitrary values.

When an `sftp` step does not specify its own `host:` option, it falls back to `Host_IP` from the grid at runtime (`SftpCommand.ResolveEndpoint()`). In this case, `Host_IP` validation SHALL remain active to ensure a valid hostname/IP for the SFTP connection. Similarly, when an `sftp` step does not specify its own `username:` or `password:`, the existing runtime validation in `SftpCommand` (which reports "requires username/password") SHALL continue to apply.

#### Scenario: Script with only local commands skips SSH
- **WHEN** a YAML script contains only `http`, `webhook`, `dns`, `ping`, `portcheck`, `print`, `set`, or other non-SSH commands
- **AND** the host grid contains rows
- **THEN** the system executes the script without establishing an SSH connection
- **AND** script output displays a `LOCAL SCRIPT:` header prefix
- **AND** grid column values are available as variables via `${column}` and `{{column}}` syntax

#### Scenario: Script with send command in nested control flow requires SSH
- **WHEN** a YAML script contains a `send` command inside any nested control-flow block (`if.then`, `if.else`, `elif[].then`, `foreach.do`, `while.do`, `try`, `catch`, `finally`)
- **THEN** the system establishes an SSH connection before executing the script

#### Scenario: Script with only sftp does not require SSH shell
- **WHEN** a YAML script contains `sftp` steps but no `send` commands
- **THEN** the system does not establish an SSH shell session
- **AND** the `sftp` command creates its own connection using credentials from context variables

#### Scenario: Mixed script with send and local commands requires SSH
- **WHEN** a YAML script contains both `send` and local commands (e.g., `http`, `print`)
- **THEN** the system establishes an SSH connection and all commands execute normally

#### Scenario: Simple preset always requires SSH
- **WHEN** a non-YAML simple preset is executed
- **THEN** the system always establishes an SSH connection regardless of command content

#### Scenario: Local execution records history and execution details
- **WHEN** a local script execution completes
- **THEN** the execution is recorded in history with output and execution details
- **AND** the host value from the grid is preserved in execution details

#### Scenario: Local execution supports cancellation
- **WHEN** a user cancels a running local script
- **THEN** the execution stops and the cancellation is handled identically to SSH execution cancellation

#### Scenario: Local execution in folder context
- **WHEN** a folder containing only local-only presets is executed
- **THEN** each preset executes locally without SSH connections
- **AND** folder execution options (parallel/sequential, stop-on-first-error) apply normally

#### Scenario: Host_IP accepts arbitrary values for local scripts without sftp fallback
- **WHEN** a user enters a URL or reference ID (e.g., `https://api.example.com` or `batch-001`) in the `Host_IP` column
- **AND** the script does not require SSH
- **AND** no sftp step falls back to the default host (either no sftp, or all sftp steps specify their own `host:`)
- **THEN** the value is accepted without hostname/IP validation
- **AND** the value is available as `${Host_IP}` in the script

#### Scenario: Host_IP validation still applies for SSH scripts
- **WHEN** a user enters an invalid hostname in `Host_IP`
- **AND** the script requires SSH (contains `send` commands)
- **THEN** the host is skipped with existing invalid-host behavior

#### Scenario: Host_IP validation applies when sftp uses default host
- **WHEN** a YAML script contains an `sftp` step that does not specify `host:` (falls back to Host_IP)
- **AND** the user enters a URL or non-hostname value in `Host_IP`
- **THEN** the host is skipped with existing invalid-host behavior (because SFTP needs a valid hostname to connect to)

#### Scenario: Sftp with explicit host allows arbitrary Host_IP
- **WHEN** a YAML script contains an `sftp` step with `host: "10.0.0.5"` explicitly specified
- **AND** the script has no `send` commands
- **AND** the user enters an arbitrary value in `Host_IP`
- **THEN** the value is accepted without validation
- **AND** the sftp command connects to the explicitly specified host, not Host_IP

#### Scenario: Sftp without explicit credentials uses context defaults
- **WHEN** a YAML script contains an `sftp` step that does not specify `username:` or `password:`
- **AND** the host grid's `username`/`password` columns (or global defaults) are empty
- **THEN** the sftp command reports "requires username" or "requires password" at runtime (existing `SftpCommand` validation)
