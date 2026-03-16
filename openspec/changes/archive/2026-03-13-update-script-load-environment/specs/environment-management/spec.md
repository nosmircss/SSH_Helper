## ADDED Requirements

### Requirement: Script-declared environment affinity on preset load
The system SHALL allow a YAML script preset to declare a preferred environment at the script root, SHALL preserve a persisted base environment chosen through manual environment changes, and SHALL restore the active environment to that base when a later preset is loaded without a top-level `environment`.

#### Scenario: Manual environment change rebases the base environment
- **WHEN** an operator manually switches the environment through the UI
- **THEN** the selected environment becomes both the active environment and the persisted base environment

#### Scenario: Loading a preset switches to its declared environment
- **WHEN** an operator loads a YAML script preset with a top-level `environment` value that matches an existing environment
- **THEN** the active environment switches to that environment
- **AND** the host grid updates using the existing environment-switch behavior
- **AND** the base environment remains unchanged

#### Scenario: Loading a later preset without environment restores the base environment
- **WHEN** an operator has an active environment that differs from the base environment and loads a preset without a top-level `environment`
- **THEN** the active environment switches back to the base environment
- **AND** the host grid updates using the existing environment-switch behavior

#### Scenario: Loading a preset with a missing declared environment
- **WHEN** an operator loads a YAML script preset whose top-level `environment` value does not match any existing environment
- **THEN** the active environment remains unchanged
- **AND** the system reports the missing environment with a non-blocking status message

### Requirement: Base environment mismatch indicator
The system SHALL show the base environment in the main toolbar only while the active environment differs from the persisted base environment.

#### Scenario: Active environment matches base environment
- **WHEN** the active environment and base environment are the same
- **THEN** the toolbar does not show the base-environment indicator

#### Scenario: Active environment differs from base environment
- **WHEN** the active environment differs from the base environment
- **THEN** the toolbar shows `Base: <name>` next to the environment controls
