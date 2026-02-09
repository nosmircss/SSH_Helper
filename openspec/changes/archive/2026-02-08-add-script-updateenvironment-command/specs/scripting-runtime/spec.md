## ADDED Requirements
### Requirement: Update environment variables from script
The scripting runtime SHALL support an `updateenvironment` step that updates a named environment variable value.

#### Scenario: Updateenvironment writes value for remaining steps
- **WHEN** a script executes `updateenvironment` with both `variable` and `value`
- **THEN** the runtime requests persistence of that variable/value pair
- **AND** the script context exposes the updated value for later substitutions in the same execution

#### Scenario: Updateenvironment validates required fields
- **WHEN** an `updateenvironment` step omits `variable` or `value`
- **THEN** script validation reports an error and execution does not proceed with an incomplete updateenvironment step
