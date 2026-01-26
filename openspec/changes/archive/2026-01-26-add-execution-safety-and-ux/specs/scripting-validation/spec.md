## ADDED Requirements

### Requirement: Script validation action
The system SHALL provide a script validation action that parses and validates YAML scripts without executing commands.

#### Scenario: Validation errors
- **WHEN** the user validates a script containing syntax or semantic errors
- **THEN** the validation errors are reported to the user with line numbers and messages

### Requirement: Validation success reporting
Successful script validation SHALL report a clear success message without executing any script steps.

#### Scenario: Valid script
- **WHEN** the user validates a well-formed script
- **THEN** the application reports validation success and performs no SSH actions
