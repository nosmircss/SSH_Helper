# scripting-control-flow Specification

## Purpose
TBD - created by archiving change add-scripting-control-flow-primitives. Update Purpose after archive.
## Requirements
### Requirement: Explicit loop control steps
The scripting language SHALL provide explicit `break` and `continue` steps for loop control.

#### Scenario: break exits current loop
- **WHEN** a `break` step executes inside a loop body
- **THEN** the current loop terminates immediately
- **AND** execution resumes after the loop

#### Scenario: continue skips to next iteration
- **WHEN** a `continue` step executes inside a loop body
- **THEN** remaining steps in the current iteration are skipped
- **AND** the next loop iteration begins

### Requirement: Multi-branch if with elif
If steps SHALL support ordered `elif` branches between `then` and `else`.

#### Scenario: first matching elif branch executes
- **WHEN** the `if` condition is false and multiple `elif` branches are present
- **THEN** only the first `elif` branch with a true condition executes
- **AND** later branches and `else` are skipped

### Requirement: Structured try/catch/finally
Scripts SHALL support `try`/`catch`/`finally` step blocks.

#### Scenario: catch executes on failure in try block
- **WHEN** a step in `try` fails without terminating the script
- **THEN** the `catch` block executes
- **AND** `_last_error` remains available inside `catch`

#### Scenario: finally executes regardless of try outcome
- **WHEN** `try` block succeeds or fails
- **THEN** `finally` executes exactly once after `try` and optional `catch`

