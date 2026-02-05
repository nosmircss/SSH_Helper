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

