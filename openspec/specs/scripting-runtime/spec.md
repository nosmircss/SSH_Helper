# scripting-runtime Specification

## Purpose
TBD - created by archiving change update-scripting-correctness-and-diagnostics. Update Purpose after archive.
## Requirements
### Requirement: Expression and loop correctness
The scripting runtime SHALL evaluate conditional expressions and arithmetic deterministically without pre-substitution side effects.

#### Scenario: If condition with spaced variable value
- **WHEN** an `if` condition references a variable containing spaces via `${var}` syntax
- **THEN** the expression evaluator receives the original expression text
- **AND** the condition is evaluated correctly

#### Scenario: Arithmetic with mixed operators
- **WHEN** a `set` expression contains mixed operators such as `a + b * c`
- **THEN** the runtime evaluates using precedence rules
- **AND** supports parentheses for explicit grouping

### Requirement: Suppressed error observability
The scripting runtime SHALL retain suppressed error details for downstream logic.

#### Scenario: on_error continue captures last error
- **WHEN** a step fails and `on_error: continue` is configured
- **THEN** execution continues
- **AND** `_last_error` is set to the failure message

#### Scenario: successful step clears last error
- **WHEN** a subsequent step completes successfully
- **THEN** `_last_error` is cleared

### Requirement: Dynamic built-in variables
Built-in timestamp variables SHALL be resolved dynamically at substitution time.

#### Scenario: timestamp changes during long script
- **WHEN** `${_timestamp}` is substituted in two different steps at different times
- **THEN** the values reflect current execution time at each substitution point

### Requirement: While iteration controls
While loops SHALL support per-step iteration caps.

#### Scenario: custom max iterations on while step
- **WHEN** a while step defines `max_iterations`
- **THEN** that value overrides the default safety cap for that step

### Requirement: Foreach JSON scalar conversion
Foreach iteration over JSON arrays SHALL expose scalar items as plain string values.

#### Scenario: foreach over JSON string array
- **WHEN** a foreach loop iterates a JSON array of strings
- **THEN** each item variable is set to the scalar text value without extra JSON quotes

### Requirement: Update environment variables from script
The scripting runtime SHALL support an `updateenvironment` step that updates a named environment variable value.

#### Scenario: Updateenvironment writes value for remaining steps
- **WHEN** a script executes `updateenvironment` with both `variable` and `value`
- **THEN** the runtime requests persistence of that variable/value pair
- **AND** the script context exposes the updated value for later substitutions in the same execution

#### Scenario: Updateenvironment validates required fields
- **WHEN** an `updateenvironment` step omits `variable` or `value`
- **THEN** script validation reports an error and execution does not proceed with an incomplete updateenvironment step

### Requirement: Canonical command-map payload shape
The scripting runtime SHALL parse converted step commands from nested command-map payloads with explicit keys as the canonical contract.

Converted commands and required primary keys:
- `send.command`
- `print.message`
- `wait.seconds`
- `set.expression`
- `if.condition`
- `foreach.iterator`
- `while.condition`
- `try.do`

Optional/related keys remain command-specific (for example `send.capture`, `while.max_iterations`, `if.then`).

#### Scenario: Send command uses nested payload
- **WHEN** a step is authored as `send:` with nested `command` and optional keys
- **THEN** runtime executes the command text from `send.command`
- **AND** applies optional keys (`capture`, `suppress`, `expect`, `timeout`, `on_error`) from the same `send` map

#### Scenario: While command uses nested condition and do block
- **WHEN** a step is authored as `while:` with nested `condition`, `do`, and optional `max_iterations`
- **THEN** runtime evaluates `while.condition` each iteration
- **AND** executes nested `while.do` steps with existing loop semantics

### Requirement: Shorthand aliases for single-primary-field commands
The scripting runtime SHALL accept shorthand scalar forms for commands that have one clear primary payload field and map them to the same runtime behavior as canonical map forms.

Supported shorthand:
- `send: <command>` -> `send.command`
- `print: <message>` -> `print.message`
- `wait: <seconds>` -> `wait.seconds`
- `set: <expression>` -> `set.expression`
- `log: <message>` -> `log.message`
- `if: <condition>` -> `if.condition`
- `foreach: <iterator>` -> `foreach.iterator`
- `while: <condition>` -> `while.condition`
- `exit: <message>` -> `exit.status=success` + `exit.message`

#### Scenario: If shorthand with then block
- **WHEN** a step is authored as `if: status == "up"` with sibling `then`/`else` blocks
- **THEN** runtime evaluates the condition exactly as if authored under `if.condition`
- **AND** executes `then`/`else` blocks with unchanged control-flow semantics

#### Scenario: Exit shorthand defaults to success status
- **WHEN** a step is authored as `exit: "All checks passed"`
- **THEN** runtime terminates with success status
- **AND** uses the scalar text as the exit message

### Requirement: Canonical exit payload parsing
The runtime SHALL parse `exit` from a nested map with explicit fields.

#### Scenario: Exit status and message fields
- **WHEN** a step is authored with `exit.status` and `exit.message`
- **THEN** runtime emits the same status/message outcome currently produced by `exit` execution semantics
- **AND** script termination behavior remains unchanged

### Requirement: On-error placement within command maps
For commands that support continue/stop failure behavior, `on_error` SHALL be parsed from that command's nested map payload.

#### Scenario: Nested on_error on send
- **WHEN** `send.on_error` is set to `continue`
- **THEN** send failures are treated as suppressed failures
- **AND** execution continues according to existing suppressed-error runtime behavior

### Requirement: License-free SFTP runtime backend
The scripting runtime SHALL execute `sftp` steps using a backend that does not require Rebex SFTP licensing.

The implementation SHALL use `SSH.NET` (`Renci.SshNet`) for SFTP transfer operations while preserving the existing `sftp` step contract and failure semantics.

#### Scenario: SFTP step runs without Rebex SFTP package
- **WHEN** an operator runs a script with an `sftp` step in a build that does not include `Rebex.Sftp`
- **THEN** the runtime can still execute the transfer using `SSH.NET`
- **AND** the step continues to populate `${into}` and `${into}_bytes` according to existing behavior

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

