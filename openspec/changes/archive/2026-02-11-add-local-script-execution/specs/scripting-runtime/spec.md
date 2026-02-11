## ADDED Requirements

### Requirement: Static SSH requirement analysis
The scripting engine SHALL provide static analysis of parsed scripts to determine SSH connection requirements before execution.

The analysis SHALL inspect `StepType` of every step in the script's step tree, including all nested step lists (`Then`, `Else`, `Elif[].Then`, `Do`, `Try`, `Catch`, `Finally`), and SHALL report:
- Whether any `send` command exists (requires SSH shell session) — `RequiresSshSession`
- Whether any `sftp` command exists (needs credentials but not SSH shell) — `UsesSftp`
- Whether any `sftp` step omits `host:` and will fall back to `Host_IP` at runtime — `SftpUsesDefaultHost`
- Whether any `sftp` step omits `username:` or `password:` and will fall back to context defaults at runtime — `SftpUsesDefaultCredentials`

The analysis SHALL be performed once after script parsing and validation, before the execution host loop begins. The result SHALL be passed through the execution call chain to avoid redundant analysis.

The analysis SHALL short-circuit (stop walking) once all detectable flags are set (`RequiresSshSession == true && UsesSftp == true`).

#### Scenario: Analysis detects send in deeply nested control flow
- **WHEN** a script contains a `send` command nested inside a `foreach.do` block inside a `try` block
- **THEN** the analysis reports `RequiresSshSession = true`

#### Scenario: Analysis reports no SSH needed for local-only script
- **WHEN** a script contains only `http`, `print`, `set`, `dns`, `ping`, `portcheck`, `webhook`, `log`, `wait`, `readfile`, `writefile`, and control-flow commands
- **THEN** the analysis reports `RequiresSshSession = false`

#### Scenario: Analysis detects sftp without send
- **WHEN** a script contains `sftp` steps but no `send` steps
- **THEN** the analysis reports `RequiresSshSession = false` and `UsesSftp = true`

#### Scenario: Analysis detects both send and sftp
- **WHEN** a script contains both `send` and `sftp` steps
- **THEN** the analysis reports `RequiresSshSession = true` and `UsesSftp = true`

#### Scenario: Empty script requires nothing
- **WHEN** a script has an empty `steps` list
- **THEN** the analysis reports `RequiresSshSession = false` and `UsesSftp = false`

#### Scenario: Analysis checks all control-flow nesting paths
- **WHEN** a script contains `send` in any of: `if.then`, `if.else`, `elif[].then`, `foreach.do`, `while.do`, `try`, `catch`, `finally`
- **THEN** the analysis detects it and reports `RequiresSshSession = true`

#### Scenario: Analysis detects sftp step using default host
- **WHEN** a script contains an `sftp` step that does not specify `host:` (the `Host` property is null or whitespace)
- **THEN** the analysis reports `SftpUsesDefaultHost = true` (the step will fall back to `Host_IP` from context at runtime)

#### Scenario: Analysis detects sftp step with explicit host
- **WHEN** a script contains an `sftp` step with `host: "10.0.0.5"` explicitly specified
- **THEN** the analysis reports `SftpUsesDefaultHost = false` (if no other sftp steps use the default host)

#### Scenario: Analysis detects sftp step using default credentials
- **WHEN** a script contains an `sftp` step that does not specify `username:` or `password:` (either property is null or whitespace)
- **THEN** the analysis reports `SftpUsesDefaultCredentials = true`

#### Scenario: Analysis detects sftp step with explicit credentials
- **WHEN** a script contains an `sftp` step with both `username:` and `password:` specified
- **THEN** the analysis reports `SftpUsesDefaultCredentials = false` (if no other sftp steps omit credentials)

#### Scenario: Multiple sftp steps — any using defaults sets the flag
- **WHEN** a script contains two `sftp` steps, one with explicit host and one without
- **THEN** the analysis reports `SftpUsesDefaultHost = true` (because any step using defaults sets the flag)
