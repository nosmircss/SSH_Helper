## ADDED Requirements
### Requirement: Script-driven environment variable persistence
The system SHALL persist environment-variable updates requested by script execution into the active environment profile.

#### Scenario: Script update persists into active environment
- **WHEN** script execution emits an environment-variable update request for the active environment
- **THEN** the requested key/value pair is saved in the active environment variable set
- **AND** subsequent executions can resolve that environment variable from the persisted value

#### Scenario: Script update in legacy profile creates default environment state
- **WHEN** an environment-variable update is requested while no explicit environment profiles exist
- **THEN** the system first captures legacy state into `Default`
- **AND** saves the updated variable under `Default`
