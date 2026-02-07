## ADDED Requirements

### Requirement: Execution details capture
The system SHALL capture a snapshot of execution metadata at the time of each execution, including: preset name, commands, preset type, start time, end time, username, command timeout, connection timeout, connection pooling setting, run mode, and per-host variable context (excluding passwords).

#### Scenario: Preset execution captures details
- **WHEN** a user executes a preset against one or more hosts
- **THEN** the system stores an `ExecutionDetails` record associated with the history entry ID
- **AND THEN** the details include the preset name, commands, timing, settings, and per-host variables (without passwords)

#### Scenario: Folder execution captures details
- **WHEN** a user executes a folder of presets against hosts
- **THEN** the system stores an `ExecutionDetails` record with `IsFolderExecution` set to true, the folder name, and the list of preset names executed

### Requirement: Execution details viewer
The system SHALL provide a "View Details" option in the history list context menu that opens a modal dialog displaying the captured execution metadata in organized tabs.

#### Scenario: View details for a recent execution
- **WHEN** the user right-clicks a history entry that has stored execution details
- **AND WHEN** the user selects "View Details"
- **THEN** a modal dialog opens with tabs for Summary, Hosts, Settings, and Context

#### Scenario: View details unavailable for older entries
- **WHEN** the user right-clicks a history entry created before this feature was added
- **THEN** the "View Details" menu item is disabled with "(not available)" text

### Requirement: Execution details export
The "View Details" dialog SHALL provide the ability to copy all execution details to the clipboard or save them to a text file.

#### Scenario: Copy details to clipboard
- **WHEN** the user clicks "Copy to Clipboard" in the details dialog
- **THEN** a formatted plain-text representation of all details is placed on the clipboard

#### Scenario: Save details to file
- **WHEN** the user clicks "Save to File" in the details dialog
- **THEN** a save dialog appears and the formatted details are written to the selected file

### Requirement: Execution details cleanup
Deleting a history entry or clearing all history SHALL also remove any associated execution details.

#### Scenario: Delete entry removes details
- **WHEN** the user deletes a history entry that has execution details
- **THEN** the associated execution details are also removed from storage
