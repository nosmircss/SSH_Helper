## ADDED Requirements
### Requirement: Preset and folder delete undo
The system SHALL provide a session-scoped, multi-level undo stack for preset deletes and folder deletes.

Undo SHALL restore the exact preset-library state captured immediately before the delete, including presets, folders, favorite state, and manual ordering metadata.

Undo history SHALL be cleared when a later preset-library mutation other than another delete or an undo changes the library state.

#### Scenario: Undo the most recent preset delete
- **WHEN** an operator deletes preset `Show Version`
- **AND** then invokes `Undo Delete` in the same app session before another non-delete library mutation
- **THEN** preset `Show Version` is restored with its original content, folder assignment, and favorite state
- **AND** the preset tree selection returns to the restored preset

#### Scenario: Undo multiple delete operations in reverse order
- **WHEN** an operator deletes preset `A` and then deletes folder `Network/Legacy`
- **AND** then invokes `Undo Delete` twice in the same app session
- **THEN** the first undo restores folder `Network/Legacy` with its deleted contents
- **AND** the second undo restores preset `A`

#### Scenario: Later library mutation clears delete undo history
- **WHEN** an operator deletes preset `A`
- **AND** then renames preset `B`
- **THEN** `Undo Delete` is no longer available for preset `A`

## MODIFIED Requirements
### Requirement: Folder deletion options
The system SHALL provide options when deleting a folder that contains subfolders or presets.

#### Scenario: Delete folder with children - move to parent
- **WHEN** user deletes folder "Network/Cisco" which contains presets and subfolder "Switches"
- **AND** user chooses to move children to parent
- **THEN** presets in "Network/Cisco" move to "Network"
- **AND** subfolder "Network/Cisco/Switches" becomes "Network/Switches"
- **AND** presets previously in "Network/Cisco/Switches" remain in "Network/Switches"

#### Scenario: Delete folder with children - recursive delete
- **WHEN** user deletes folder "Network/Cisco" which contains presets and subfolders
- **AND** user chooses to delete recursively
- **THEN** all presets in "Network/Cisco" and its subfolders are deleted
- **AND** all subfolders under "Network/Cisco" are deleted

#### Scenario: Delete empty folder
- **WHEN** user deletes folder "Network/Legacy" which has no presets or subfolders
- **THEN** the folder is deleted without prompting
