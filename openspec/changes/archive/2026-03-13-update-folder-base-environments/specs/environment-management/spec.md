## ADDED Requirements

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
