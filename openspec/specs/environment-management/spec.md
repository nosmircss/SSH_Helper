# environment-management Specification

## Purpose
TBD - created by archiving change add-environment-dialog-layout-persistence. Update Purpose after archive.
## Requirements
### Requirement: Environment dialog layout persistence
The system SHALL persist Manage Environments dialog layout changes so user-adjusted size and split layout are restored on subsequent openings.

#### Scenario: Reopen dialog after resize
- **WHEN** an operator resizes the Manage Environments dialog and closes it
- **THEN** reopening the dialog restores the prior width and height

#### Scenario: Reopen dialog after splitter adjustment
- **WHEN** an operator adjusts the Manage Environments left-panel splitter and closes the dialog
- **THEN** reopening the dialog restores the prior splitter distance

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

### Requirement: Named environment profiles
The system SHALL support multiple named environments, where each environment stores an independent host-grid snapshot including host columns, host rows, selected host indices, and last imported CSV path.

#### Scenario: Create environment from current grid
- **WHEN** an operator creates a new environment from the current workspace state
- **THEN** the new environment contains the current columns, hosts, selected rows, and CSV reference

#### Scenario: Environment data isolation
- **WHEN** an operator edits host rows in one environment
- **THEN** host rows in other environments remain unchanged

### Requirement: Active environment switching
The system SHALL provide an active environment selector in the main UI and SHALL load the selected environment state into the host grid when changed.

#### Scenario: Switch environment with unsaved changes
- **WHEN** an operator selects a different environment while the current grid has unsaved edits
- **THEN** the UI prompts whether to save current environment changes before switching

#### Scenario: Switch environment updates context
- **WHEN** an operator switches to another environment
- **THEN** the host grid reflects that environment's stored state
- **AND** the window title displays the active environment name

### Requirement: Environment variable injection precedence
The system SHALL merge per-environment variables into per-host runtime variables using this precedence order: host grid column values first, then environment variables, then script `vars` defaults.

#### Scenario: Environment variable used as fallback
- **WHEN** a host row does not provide a value for a variable key and the environment defines that key
- **THEN** command/script substitution uses the environment value

#### Scenario: Host column overrides environment variable
- **WHEN** both a host row and the active environment define the same variable key
- **THEN** the host row value is used during execution

### Requirement: Backward-compatible legacy migration
The system SHALL remain compatible with existing single-environment configuration files that do not define explicit environments.

#### Scenario: Load legacy config without environments
- **WHEN** configuration is loaded and `Environments` is absent or empty
- **THEN** the application behaves exactly as current single-environment mode

#### Scenario: First-time environment adoption
- **WHEN** an operator creates the first explicit environment in a legacy profile
- **THEN** the current workspace is snapshotted into a `Default` environment without data loss

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

