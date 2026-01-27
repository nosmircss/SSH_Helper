## ADDED Requirements

### Requirement: Pooled session encoding parity
Pooled SSH sessions SHALL use the same terminal encoding settings as non-pooled sessions (UTF-8).

#### Scenario: Pooled session start
- **WHEN** a pooled SSH session is created
- **THEN** the terminal encoding is set to UTF-8 before script execution

### Requirement: Health-check resource cleanup
Connection pool health checks SHALL dispose or release any temporary scripting resources they create.

#### Scenario: Health check completion
- **WHEN** a pooled connection health check completes
- **THEN** any temporary scripting resources are disposed or released
