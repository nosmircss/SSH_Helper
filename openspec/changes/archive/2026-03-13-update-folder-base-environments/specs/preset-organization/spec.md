## ADDED Requirements

### Requirement: Folder base-environment assignment
The system SHALL allow an operator to assign or clear a folder-specific base environment from the preset-folder context menu, and that assignment SHALL persist in folder metadata.

#### Scenario: Assign folder base environment
- **WHEN** an operator right-clicks folder `Network/Prod` in the Presets tree
- **AND** assigns base environment `prod`
- **THEN** the folder metadata persists `prod` as that folder's base environment override

#### Scenario: Clear folder base environment to inherit
- **WHEN** an operator clears the base environment override on folder `Network/Prod`
- **THEN** the folder no longer stores its own base environment
- **AND** presets in that folder inherit the nearest ancestor folder base or the global base environment

#### Scenario: Environment rename repairs folder assignments
- **WHEN** folder `Network/Prod` stores base environment `prod`
- **AND** the operator renames environment `prod` to `production`
- **THEN** the folder metadata is updated to `production`

#### Scenario: Environment delete clears folder assignments
- **WHEN** folder `Network/Prod` stores base environment `prod`
- **AND** the operator deletes environment `prod`
- **THEN** the folder base-environment override is cleared
