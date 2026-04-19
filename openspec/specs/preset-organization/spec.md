# preset-organization Specification

## Purpose
TBD - created by archiving change add-nested-preset-folders. Update Purpose after archive.
## Requirements
### Requirement: Nested folder paths
The system SHALL support folder paths using forward-slash notation to represent hierarchy (e.g., "Network/Cisco/Switches").

#### Scenario: Create nested folder
- **WHEN** user creates a folder with path "Network/Cisco/Switches"
- **THEN** folders "Network", "Network/Cisco", and "Network/Cisco/Switches" all exist in PresetFolders
- **AND** the TreeView displays the hierarchy with Network containing Cisco containing Switches

#### Scenario: Display nested hierarchy
- **WHEN** folders exist at paths "Network", "Network/Cisco", "Network/Juniper"
- **THEN** the TreeView displays "Network" as a parent node
- **AND** "Cisco" and "Juniper" appear as children under "Network"

#### Scenario: Root-level folder unchanged
- **WHEN** user creates a folder "Scripts" with no slash
- **THEN** it appears at the root level of the TreeView
- **AND** existing single-level folders continue to work

### Requirement: Preset placement flexibility
The system SHALL allow presets to be placed at any folder level, including the root level and intermediate folders.

#### Scenario: Preset in intermediate folder
- **WHEN** preset "show version" has folder "Network/Cisco"
- **AND** folder "Network/Cisco/Switches" also exists
- **THEN** "show version" appears under "Network/Cisco" node alongside the "Switches" subfolder node

#### Scenario: Preset at root level
- **WHEN** preset "quick test" has no folder assigned (Folder is null)
- **THEN** "quick test" appears at the root level of the TreeView

### Requirement: Folder rename propagation
The system SHALL update all descendant folder paths and preset assignments when a folder is renamed.

#### Scenario: Rename intermediate folder
- **WHEN** user renames folder "Network/Cisco" to "Network/CiscoSystems"
- **THEN** subfolder "Network/Cisco/Switches" becomes "Network/CiscoSystems/Switches"
- **AND** all presets with folder "Network/Cisco" move to "Network/CiscoSystems"
- **AND** all presets with folder "Network/Cisco/Switches" move to "Network/CiscoSystems/Switches"

#### Scenario: Rename root folder
- **WHEN** user renames folder "Network" to "Infrastructure"
- **THEN** all descendant folders update their path prefix from "Network/" to "Infrastructure/"

### Requirement: Folder deletion options
The system SHALL provide options when deleting a folder that contains subfolders or presets.

#### Scenario: Delete folder with children - move to parent
- **WHEN** user deletes folder "Network/Cisco" which contains presets and subfolder "Switches"
- **AND** user chooses to move children to parent
- **THEN** presets in "Network/Cisco" move to "Network"
- **AND** subfolder "Network/Cisco/Switches" becomes "Network/Switches"

#### Scenario: Delete folder with children - recursive delete
- **WHEN** user deletes folder "Network/Cisco" which contains presets and subfolders
- **AND** user chooses to delete recursively
- **THEN** all presets in "Network/Cisco" and its subfolders are deleted
- **AND** all subfolders under "Network/Cisco" are deleted

#### Scenario: Delete empty folder
- **WHEN** user deletes folder "Network/Legacy" which has no presets or subfolders
- **THEN** the folder is deleted without prompting

### Requirement: Implicit parent folder creation
The system SHALL automatically create parent folders when a nested folder path is created.

#### Scenario: Create deeply nested folder
- **WHEN** user creates folder "Production/Database/MySQL/Backups"
- **AND** none of the parent folders exist
- **THEN** folders "Production", "Production/Database", "Production/Database/MySQL", and "Production/Database/MySQL/Backups" are all created
- **AND** all parent folders have default FolderInfo (IsExpanded=true, IsFavorite=false)

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

### Requirement: Folder subtree export
The system SHALL allow an operator to export a selected preset folder and all descendant folders and presets to a JSON file from the preset-folder context menu.

#### Scenario: Export selected folder subtree only
- **WHEN** an operator right-clicks preset folder `Switches` and chooses `Export Folder...`
- **THEN** the system prompts for a JSON save path
- **AND** the exported document includes folder `Switches`, its descendant folders, and presets within that subtree
- **AND** the exported document excludes presets and folders outside the selected subtree

#### Scenario: Export nested folder rebases to bundle root
- **WHEN** an operator exports nested preset folder `Network/Prod`
- **THEN** the exported document represents `Prod` as the bundle root
- **AND** descendant folder `Network/Prod/Core` is exported as `Prod/Core`
- **AND** presets in the subtree keep their relative placement beneath `Prod`

#### Scenario: Exported subtree remains import-compatible
- **WHEN** an operator imports a previously exported folder subtree document into destination folder `Archive`
- **THEN** the selected folder and descendants are recreated under `Archive/<selected-folder-name>`
- **AND** preset-name collision handling continues to follow the existing import rules

