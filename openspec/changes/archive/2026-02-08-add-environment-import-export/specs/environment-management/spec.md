## ADDED Requirements

### Requirement: Environment transfer files
The system SHALL allow an operator to export a named environment to a portable file and import that file into another profile.

#### Scenario: Export selected environment
- **WHEN** an operator exports the currently selected environment
- **THEN** the system writes a JSON file containing the environment name, host snapshot, variables, and metadata

#### Scenario: Import environment with non-conflicting name
- **WHEN** an operator imports a valid environment file whose environment name does not already exist
- **THEN** the system persists the imported environment and makes it available in environment selection

#### Scenario: Import environment with conflicting name
- **WHEN** an operator imports a valid environment file whose environment name already exists
- **THEN** the system prompts to overwrite or rename before persisting the import
