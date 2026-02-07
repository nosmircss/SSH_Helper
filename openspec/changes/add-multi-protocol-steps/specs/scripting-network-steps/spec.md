## ADDED Requirements

### Requirement: Multi-protocol step type support
The scripting engine SHALL support first-class `http`, `ping`, `dns`, `portcheck`, and `sftp` steps in script parsing, validation, and runtime execution.

#### Scenario: Mixed-protocol script execution
- **WHEN** a script combines SSH commands with `http`, `ping`, `dns`, `portcheck`, and `sftp` steps
- **THEN** the parser accepts all steps
- **AND** the executor runs each step with its defined semantics

### Requirement: HTTP step behavior
The `http` step SHALL support URL, method, headers, body, timeout, redirect handling, auth modes, failure control, and response capture into script variables.

#### Scenario: HTTP capture variables
- **WHEN** an `http` step defines `into: result`
- **THEN** the runtime stores response body in `${result}`
- **AND** stores status code in `${result_status}`
- **AND** stores response headers in `${result_headers}`

#### Scenario: Allow failure for non-2xx responses
- **WHEN** an `http` step receives a non-2xx response and `allow_failure: true`
- **THEN** the step does not terminate script execution

### Requirement: Reachability and lookup steps
The `ping`, `dns`, and `portcheck` steps SHALL provide normalized status outputs and metrics usable by downstream expressions.

#### Scenario: Ping result normalization
- **WHEN** a `ping` step executes with `into: ping_result`
- **THEN** `${ping_result}` is set to success or failure
- **AND** latency/loss metrics are exposed via derived variables

#### Scenario: Portcheck result normalization
- **WHEN** a `portcheck` step executes with `into: check`
- **THEN** `${check}` is set to open, closed, or timeout
- **AND** `${check_latency}` records measured connection latency when available

### Requirement: SFTP transfer step
The `sftp` step SHALL support upload and download actions with overridable endpoint/credential fields and transfer result capture.

#### Scenario: Download succeeds
- **WHEN** an `sftp` step with `action: download` completes successfully and `into: transfer`
- **THEN** `${transfer}` is set to success
- **AND** `${transfer_bytes}` contains the transferred byte count

### Requirement: Backward compatibility for webhook step
Existing scripts using `webhook` SHALL remain valid and behave as before after introducing `http`.

#### Scenario: Legacy webhook script
- **WHEN** a previously valid script uses only `webhook` for HTTP notification
- **THEN** parsing and runtime behavior remain unchanged

### Requirement: Required-field validation for new steps
Validation SHALL report errors with line context when required fields for `http`, `ping`, `dns`, `portcheck`, or `sftp` are missing.

#### Scenario: Missing required HTTP URL
- **WHEN** a script defines `http` without `url`
- **THEN** validation reports an error indicating the missing required field and its line
