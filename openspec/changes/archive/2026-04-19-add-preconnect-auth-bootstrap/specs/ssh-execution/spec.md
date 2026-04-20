## ADDED Requirements

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
