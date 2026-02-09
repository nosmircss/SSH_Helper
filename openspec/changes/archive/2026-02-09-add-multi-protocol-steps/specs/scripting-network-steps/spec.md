## ADDED Requirements

### Requirement: Multi-protocol step type support
The scripting engine SHALL support first-class `http`, `ping`, `dns`, `portcheck`, and `sftp` steps in script parsing, validation, and runtime execution.

#### Scenario: Mixed-protocol script execution
- **WHEN** a script combines SSH commands with `http`, `ping`, `dns`, `portcheck`, and `sftp` steps
- **THEN** the parser accepts all steps
- **AND** the executor runs each step with its defined semantics

### Requirement: Variable substitution in network step options
The runtime SHALL apply existing variable substitution rules (`${var}` and `{{var}}`) to all string-valued option fields for `http`, `ping`, `dns`, `portcheck`, and `sftp` before execution.

For `http`, this SHALL include at least `url`, `body`, header values, `username`, `password`, and `token`.
For `ping`, `dns`, and `portcheck`, this SHALL include `host`.
For `sftp`, this SHALL include `local_path`, `remote_path`, `host`, `username`, and `password`.

#### Scenario: Substitution in HTTP URL and headers
- **WHEN** an `http` step defines `url` and header values using `${...}` variables
- **THEN** the runtime resolves those variables before sending the request

### Requirement: HTTP step contract and defaults
The `http` step SHALL support absolute `http://` or `https://` URL execution with method, headers, body, timeout, redirect handling, auth modes, failure control, and response capture.

The `http` step SHALL enforce the following defaults and accepted values:
- `method`: default `GET`; accepted values `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS` (case-insensitive)
- `timeout`: default `30` seconds
- `follow_redirects`: default `true`
- `allow_failure`: default `false`
- `verify_tls`: default `true`
- `auth`: default `none`; accepted values `none`, `basic`, `bearer` (case-insensitive)
- `content_type`: optional shorthand; accepted values `json`, `form`, `text`, `xml` (case-insensitive)

When `content_type` is provided, the runtime SHALL map shorthand values as follows:
- `json` -> `application/json`
- `form` -> `application/x-www-form-urlencoded`
- `text` -> `text/plain`
- `xml` -> `application/xml`

If both `content_type` and an explicit `Content-Type` header are provided, the explicit `Content-Type` header SHALL take precedence.

#### Scenario: HTTP defaults are applied
- **WHEN** an `http` step only specifies `url`
- **THEN** the runtime uses `method=GET`
- **AND** uses `timeout=30`
- **AND** uses `follow_redirects=true`
- **AND** uses `allow_failure=false`
- **AND** uses `verify_tls=true`
- **AND** treats `auth` as `none`

#### Scenario: HTTP auth mode validation
- **WHEN** `auth: basic` is specified without `username` or `password`
- **OR** `auth: bearer` is specified without `token`
- **THEN** validation reports the missing required auth field with line context

#### Scenario: HTTP content_type header precedence
- **WHEN** an `http` step specifies both `content_type: json` and `headers.Content-Type: text/plain`
- **THEN** the request uses `text/plain` as the effective content type

#### Scenario: Case-insensitive HTTP method and auth
- **WHEN** an `http` step uses `method: post` and `auth: BEARER`
- **THEN** validation accepts the values and runtime executes them as `POST` and `bearer`

### Requirement: HTTP failure semantics and on_error interoperability
The `http` step SHALL treat non-2xx HTTP responses as step failures unless `allow_failure: true` is specified.

Transport/runtime failures (for example timeout, connection, TLS, or DNS resolution failures) SHALL follow existing step-level `on_error` behavior and SHALL NOT be reclassified by `allow_failure`.

#### Scenario: Allow failure for non-2xx responses
- **WHEN** an `http` step receives a non-2xx response and `allow_failure: true`
- **THEN** the step does not terminate script execution

#### Scenario: Timeout respects on_error continue
- **WHEN** an `http` step times out and the step has `on_error: continue`
- **THEN** script execution continues
- **AND** the step failure is reported as suppressed under existing runtime error semantics

### Requirement: HTTP TLS certificate validation
The `http` step SHALL validate TLS certificates by default (`verify_tls: true`) and SHALL allow explicit per-step opt-out via `verify_tls: false`.

#### Scenario: Invalid certificate fails by default
- **WHEN** an `http` step targets an HTTPS endpoint with an invalid certificate and `verify_tls` is omitted
- **THEN** the step fails due to TLS validation

#### Scenario: Explicit TLS verification opt-out
- **WHEN** an `http` step targets an HTTPS endpoint with an invalid certificate and sets `verify_tls: false`
- **THEN** the request is allowed to proceed without certificate validation

### Requirement: HTTP capture variables
The `http` step SHALL support `into` response capture with these variables:
- `${into}`: response body
- `${into}_status`: HTTP status code
- `${into}_headers`: response headers serialized as JSON

When `into` is provided and no HTTP response is received (for example timeout or transport failure), `${into}`, `${into}_status`, and `${into}_headers` SHALL be set to empty strings for that step execution to avoid stale values from prior steps.

#### Scenario: HTTP capture variables
- **WHEN** an `http` step defines `into: result`
- **THEN** the runtime stores response body in `${result}`
- **AND** stores status code in `${result_status}`
- **AND** stores response headers in `${result_headers}`

#### Scenario: HTTP capture variables on transport failure
- **WHEN** an `http` step with `into: result` fails before receiving any HTTP response
- **THEN** `${result}` is set to an empty string
- **AND** `${result_status}` is set to an empty string
- **AND** `${result_headers}` is set to an empty string

### Requirement: Ping step behavior and defaults
The `ping` step SHALL support host reachability checks with normalized status outputs and metrics usable by downstream expressions.

The `ping` step SHALL use `count=4` and `timeout=3000` milliseconds when those fields are not provided.

When `into` is specified, each ping execution SHALL set `${into}`, `${into}_avg`, and `${into}_loss`.
On complete ping failure (no successful replies), `${into}` SHALL be `failure`, `${into}_avg` SHALL be empty, and `${into}_loss` SHALL be `100`.

#### Scenario: Ping result normalization
- **WHEN** a `ping` step executes with `into: ping_result`
- **THEN** `${ping_result}` is set to success or failure
- **AND** latency/loss metrics are exposed via derived variables

#### Scenario: Ping complete failure capture
- **WHEN** a `ping` step executes with `into: ping_result` and all probes fail
- **THEN** `${ping_result}` is `failure`
- **AND** `${ping_result_avg}` is empty
- **AND** `${ping_result_loss}` is `100`

### Requirement: DNS step behavior and defaults
The `dns` step SHALL support `A`, `AAAA`, and `PTR` lookup types with `type=A` and `timeout=10` seconds defaults.

When `into` is specified, the runtime SHALL set `${into}` to a `List<string>` of resolved values and SHALL set `${into}_count` to the number of resolved values.

When a DNS lookup completes with no records, the step SHALL succeed with `${into}` as an empty list and `${into}_count` as `0`.
When a DNS lookup fails due to timeout or resolver/runtime error, the step SHALL follow `on_error` behavior and, if `into` is specified, SHALL set `${into}` to an empty list and `${into}_count` to `0` for that execution.

#### Scenario: DNS A lookup capture
- **WHEN** a `dns` step executes with `type: A` and `into: resolved`
- **THEN** `${resolved}` contains a list of resolved IP address strings
- **AND** `${resolved_count}` contains the number of resolved addresses

#### Scenario: DNS PTR lookup capture
- **WHEN** a `dns` step executes with `type: PTR` and `into: resolved`
- **THEN** `${resolved}` contains a list of resolved host name strings
- **AND** `${resolved_count}` contains the number of resolved names

#### Scenario: DNS lookup returns no records
- **WHEN** a `dns` step executes with `into: resolved` and lookup returns no records
- **THEN** `${resolved}` is an empty list
- **AND** `${resolved_count}` is `0`

### Requirement: Portcheck step behavior and defaults
The `portcheck` step SHALL perform TCP connection checks with defaults `port=22` and `timeout=5` seconds when those fields are not provided.

When `into` is specified, the runtime SHALL set `${into}` for every execution and SHALL set `${into}_latency` to measured milliseconds when available, or empty when unavailable.

#### Scenario: Portcheck result normalization
- **WHEN** a `portcheck` step executes with `into: check`
- **THEN** `${check}` is set to open, closed, or timeout
- **AND** `${check_latency}` records measured connection latency when available

#### Scenario: Portcheck timeout capture
- **WHEN** a `portcheck` step times out with `into: check`
- **THEN** `${check}` is set to `timeout`
- **AND** `${check_latency}` is set to an empty value

### Requirement: SFTP transfer step behavior and defaults
The `sftp` step SHALL support `upload` and `download` actions with overridable endpoint and credential fields and transfer result capture.

The `sftp` step SHALL use `overwrite=true` and `timeout=120` seconds defaults when those fields are not provided.

When `host`, `port`, `username`, or `password` are omitted, the runtime SHALL reuse the current host execution context values and default port `22` when no active port is available.

When `into` is specified, the runtime SHALL set `${into}` to `success` or `failure` and SHALL set `${into}_bytes` to transferred byte count on success or `0` on failure.

When `overwrite: false` is set and destination file already exists, the step SHALL fail with a clear destination-exists error.

#### Scenario: Download succeeds
- **WHEN** an `sftp` step with `action: download` completes successfully and `into: transfer`
- **THEN** `${transfer}` is set to success
- **AND** `${transfer_bytes}` contains the transferred byte count

#### Scenario: SFTP overwrite disabled with existing destination
- **WHEN** an `sftp` step sets `overwrite: false` and the destination path already exists
- **THEN** the step fails with a destination-exists error
- **AND** if `into` is set, `${into}` is `failure` and `${into}_bytes` is `0`

### Requirement: Backward compatibility for webhook step
Existing scripts using `webhook` SHALL remain valid and behave as before after introducing `http`.

#### Scenario: Legacy webhook script
- **WHEN** a previously valid script uses only `webhook` for HTTP notification
- **THEN** parsing and runtime behavior remain unchanged

### Requirement: Required-field validation for new steps
Validation SHALL report errors with line context when required fields for `http`, `ping`, `dns`, `portcheck`, or `sftp` are missing.

Validation SHALL also report errors with line context when enum-like fields contain unsupported values (`http` method/auth/content_type, `dns` type, `sftp` action) or when `verify_tls` is not a boolean value.

Enum-like fields SHALL be validated case-insensitively.

#### Scenario: Missing required HTTP URL
- **WHEN** a script defines `http` without `url`
- **THEN** validation reports an error indicating the missing required field and its line

#### Scenario: Invalid DNS type
- **WHEN** a script defines `dns` with `type: TXT`
- **THEN** validation reports an error indicating `type` must be one of `A`, `AAAA`, or `PTR` and includes line context

#### Scenario: Lowercase enum values are accepted
- **WHEN** a script defines `http` with `method: post` and `dns` with `type: aaaa`
- **THEN** validation accepts both values as valid case-insensitive options
