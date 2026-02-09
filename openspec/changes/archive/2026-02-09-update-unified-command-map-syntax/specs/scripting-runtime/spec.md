## ADDED Requirements
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
