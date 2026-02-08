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

