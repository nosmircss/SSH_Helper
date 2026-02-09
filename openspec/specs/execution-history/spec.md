# execution-history Specification

## Purpose
TBD - created by archiving change add-execution-safety-and-ux. Update Purpose after archive.
## Requirements
### Requirement: Unique history identifiers
History entries SHALL include a stable unique identifier that is independent of the display label.

#### Scenario: Two runs in the same second
- **WHEN** two executions complete within the same second
- **THEN** each history entry has a distinct identifier and associated per-host data is stored against that identifier

### Requirement: History deletion cleanup
Deleting a history entry or clearing history SHALL remove any associated per-host results.

#### Scenario: Delete history entry
- **WHEN** the user deletes a history entry
- **THEN** any stored per-host results for that entry are removed and cannot be displayed

### Requirement: Per-host history for preset executions
The system SHALL store per-host execution results for preset executions (non-folder), regardless of host count, and associate them with the history entry for that run.

#### Scenario: Preset run across multiple hosts
- **WHEN** a user runs a preset against multiple hosts
- **THEN** the saved history entry includes per-host results for each host
- **AND THEN** the history entry is eligible for per-host display

#### Scenario: Preset run against a single host
- **WHEN** a user runs a preset against a single host
- **THEN** the saved history entry includes one per-host result and can be displayed via the host list

### Requirement: History host split display
The history view SHALL display the per-host selector for any history entry that has stored per-host results.

#### Scenario: History entry with per-host results
- **WHEN** a user selects a history entry that has per-host results
- **THEN** the host list is shown and selecting a host displays that host's output

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

### Requirement: Execution details persistence across restart
The system SHALL persist captured `ExecutionDetails` alongside each retained history entry in saved application state and SHALL restore those details on startup.

#### Scenario: Retained history entry keeps details after restart
- **WHEN** a history entry includes execution details and the application state is saved
- **AND WHEN** the application restarts and restores that history entry
- **THEN** the entry still has execution details available
- **AND THEN** the "View Details..." action is available for that entry

#### Scenario: Legacy entry without details remains unavailable
- **WHEN** a restored history entry has no persisted execution details
- **THEN** the "View Details" action remains unavailable for that entry

