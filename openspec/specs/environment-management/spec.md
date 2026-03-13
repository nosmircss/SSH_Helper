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

### Requirement: Environment CSV freshness awareness
The system SHALL persist enough metadata with each environment's remembered CSV reference to determine whether that environment's stored host snapshot still matches the file on disk when the environment becomes active.

#### Scenario: Switching to an environment whose CSV changed on disk
- **WHEN** environment `Lab A` remembers `fortigate.csv`
- **AND** `fortigate.csv` has changed on disk since `Lab A` last captured its host snapshot
- **AND** an operator switches to `Lab A`
- **THEN** the system detects that the remembered host snapshot is stale before loading it into the grid
- **AND** the operator is offered a reload-from-disk choice

#### Scenario: Switching to an environment whose CSV is missing on disk
- **WHEN** an environment remembers `fortigate.csv`
- **AND** that file no longer exists on disk
- **AND** an operator switches to the environment
- **THEN** the environment's stored host snapshot remains available
- **AND** the hosts header indicates that the remembered file is missing on disk

#### Scenario: Switching to an environment whose CSV still matches disk
- **WHEN** an environment remembers `fortigate.csv`
- **AND** the file on disk still matches the environment's remembered host snapshot
- **AND** an operator switches to the environment
- **THEN** the host grid loads without any stale-file warning
- **AND** the hosts header shows the file reference without a disk-drift warning

### Requirement: Folder-level base environment inheritance
The system SHALL allow preset folders to declare an optional base environment override. When resolving the environment context for a preset or folder, the system SHALL use the operator-selected global base environment unless the selected preset's folder or one of its ancestor folders declares a nearer base environment override.

#### Scenario: Folder base overrides the global base
- **WHEN** the global base environment is `Default`
- **AND** folder `Network/Prod` declares folder base environment `prod`
- **AND** an operator loads a preset in `Network/Prod` that does not declare its own environment
- **THEN** the active environment switches to `prod`

#### Scenario: Child folder inherits nearest ancestor base
- **WHEN** folder `Network` declares folder base environment `lab`
- **AND** folder `Network/Switches` does not declare its own base environment
- **AND** an operator loads a preset in `Network/Switches`
- **THEN** the active environment resolves to `lab`

#### Scenario: Preset-declared environment still wins
- **WHEN** folder `Network/Prod` declares folder base environment `prod`
- **AND** a preset in that folder declares top-level script environment `staging`
- **THEN** the active environment switches to `staging`
- **AND** the folder base remains available as the lower-precedence fallback for presets in that folder

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

