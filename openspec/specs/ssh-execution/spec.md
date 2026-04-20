# ssh-execution Specification

## Purpose
TBD - created by archiving change refactor-execution-and-io. Update Purpose after archive.
## Requirements
### Requirement: Unified execution orchestration
The system SHALL execute presets through a single orchestration flow for all host selection modes (all, selected, checked), applying the same timeout resolution, preset creation, and error handling logic.

#### Scenario: Execute selected hosts
- **WHEN** a user runs a preset against selected hosts
- **THEN** the same orchestration flow is used as for running against all hosts

### Requirement: Pooled execution parity
When connection pooling is enabled, the system SHALL apply the same SSH algorithm settings and timeouts as non-pooled execution.

#### Scenario: Pooled connection uses algorithms
- **WHEN** pooling is enabled and a host specifies SSH algorithms
- **THEN** the pooled connection applies the specified algorithms before connecting

### Requirement: Connection pooling toggle
The system SHALL allow connection pooling to be enabled or disabled via configuration or UI settings.

#### Scenario: Disable pooling
- **WHEN** a user disables pooling
- **THEN** new executions use non-pooled connections

### Requirement: Multi-host preset execution confirmation
When a preset execution targets multiple hosts, the system SHALL present an execution options dialog that lists the selected hosts and allows the user to confirm execution settings before starting.

#### Scenario: Preset run with multiple hosts selected
- **WHEN** a user runs a preset with more than one host selected
- **THEN** the execution options dialog is shown with the selected hosts and execution settings

### Requirement: Throttled UI output rendering
The system SHALL throttle UI output rendering to no more than once every 50 milliseconds while preserving full output capture for history.

#### Scenario: High-volume output
- **WHEN** a command produces rapid output
- **THEN** the output display updates at most once every 50 milliseconds
- **AND THEN** all output is still captured for history

### Requirement: Command-end flush
The system SHALL flush any buffered UI output when a command completes.

#### Scenario: Command completion
- **WHEN** a command finishes and the prompt returns
- **THEN** any buffered output is flushed immediately to the UI

### Requirement: Debug output bypass
The system SHALL render debug output without throttling.

#### Scenario: Debug output
- **WHEN** output is prefixed with [DEBUG] or [SSH DEBUG]
- **THEN** it is rendered immediately and not delayed by throttling

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

### Requirement: Preconnect-aware SSH authentication ordering
When a script defines `preconnect` and requires an SSH session, the SSH execution pipeline SHALL complete the host-scoped preconnect phase before attempting SSH connection authentication for that host.

#### Scenario: Preconnect failure prevents SSH login
- **WHEN** preconnect fails for a host in a script that requires SSH
- **THEN** SSH login is not attempted for that host
- **AND** the host execution result reports the preconnect failure

### Requirement: Effective auth parity across pooled and non-pooled paths
The SSH execution pipeline SHALL apply the same resolved effective auth inputs (including preconnect overrides) in both pooled and non-pooled script execution.

#### Scenario: Dynamic identity file with pooling enabled
- **WHEN** connection pooling is enabled and preconnect resolves `_ssh_identity_file`
- **THEN** pooled session creation/authentication uses that effective identity file for the host
- **AND** it does not reuse an incompatible session authenticated with different effective credentials

#### Scenario: Dynamic password path with pooling disabled
- **WHEN** pooling is disabled and preconnect resolves `_ssh_password`
- **THEN** non-pooled login uses that effective password for the host

