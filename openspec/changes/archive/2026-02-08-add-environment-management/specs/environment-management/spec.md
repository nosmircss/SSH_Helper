## ADDED Requirements

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
