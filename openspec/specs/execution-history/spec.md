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
The system SHALL persist retained execution history using an external history store located beside `config.json`, with metadata in `history.index.json` and full per-run payloads in `history/<entry-id>.json`. Startup SHALL restore history metadata without deserializing all run payload files.

#### Scenario: Startup restores history metadata only
- **WHEN** the application starts and external history files exist
- **THEN** the history list is populated from index metadata (`Id`, label, and flags)
- **AND THEN** run payload JSON files are not deserialized until a specific run is selected or requested for details/export

#### Scenario: Retained history entry keeps details after restart
- **WHEN** a retained history entry includes execution details and the application restarts
- **THEN** the entry metadata indicates details are available in the history list
- **AND THEN** opening "View Details..." lazy-loads that run payload and shows the saved details

#### Scenario: Legacy entry without details remains unavailable
- **WHEN** a restored history entry has no persisted execution details
- **THEN** the "View Details" action remains unavailable for that entry

### Requirement: Interactive capture close reasons in execution details
Execution details SHALL preserve interactive capture completion outcomes and transcript audit data at step completion time.

Audit contract:
- Interactive capture sessions SHALL record close reasons including `ctrl_c_continue`, `timeout_continue`, `early_close_partial`, and `natural_complete`.
- Session transcript SHALL be persisted when the step completes, even if a detached read-only window remains open for review.

#### Scenario: Ctrl+C continuation reason is persisted
- **WHEN** capture mode is stopped by `Ctrl+C`
- **THEN** interactive session details store close reason `ctrl_c_continue`
- **AND** transcript is available in execution history details

#### Scenario: Detached window does not block history completeness
- **WHEN** capture mode reaches timeout or natural completion and the window stays open detached
- **THEN** execution details already contain finalized transcript and close reason for that step

#### Scenario: Early close stores partial reason
- **WHEN** operator closes capture window before interrupt/completion
- **THEN** close reason `early_close_partial` is stored
- **AND** partial transcript is preserved in execution details

### Requirement: Interactive terminal session audit capture
When a script launches an interactive terminal session, the system SHALL capture that launched session in execution details with lifecycle metadata and transcript output.

Captured fields per session SHALL include:
- Session number
- Host address
- Session mode
- Emulation mode
- Start and end timestamps
- Close reason (`user_closed`, `disconnected`, `cancelled`, or `error`)
- Completion flag
- Terminal transcript text

Only sessions that are actually launched SHALL be recorded.

#### Scenario: One or more interactive sessions are launched
- **WHEN** a script execution launches one or more interactive terminal windows
- **THEN** each launched interactive session is stored in `ExecutionDetails.InteractiveSessions`
- **AND THEN** session transcript and close metadata are preserved for history inspection

#### Scenario: Interactive launch is rejected before window opens
- **WHEN** an `interactive` step fails pre-launch (for example shared session unavailable)
- **THEN** no interactive session record is added for that step

### Requirement: Interactive session details viewer
The execution details dialog SHALL provide an `Interactive` tab that lists captured interactive sessions and displays the transcript for the selected session.

#### Scenario: Execution details contains interactive sessions
- **WHEN** a user opens "View Details" for a history entry with captured interactive sessions
- **THEN** the dialog shows an `Interactive` tab
- **AND THEN** the tab lists one row per session with metadata
- **AND THEN** selecting a session shows its transcript text

#### Scenario: Execution details has no interactive sessions
- **WHEN** a user opens "View Details" for an entry with no captured interactive sessions
- **THEN** the `Interactive` tab shows an explicit empty-state message

### Requirement: Interactive session export and persistence
Interactive session audit data SHALL be included in execution-details export and SHALL persist across application restart with history state.

#### Scenario: Save execution details includes interactive sessions
- **WHEN** a user saves execution details to a file
- **THEN** the output includes an `Interactive Terminal Sessions` section with session metadata and transcript content

#### Scenario: Restart restores interactive session details
- **WHEN** a history entry with interactive sessions is saved in application state and later restored
- **THEN** the interactive sessions remain available in `ExecutionDetails`
- **AND THEN** the `Interactive` tab can display the restored sessions

### Requirement: Full-fidelity persisted history payload
The system SHALL persist retained history payload content without application-introduced truncation or cropping of output, host output, command snapshots, interactive transcripts, or detail variable values.

#### Scenario: Large output is persisted intact
- **WHEN** an execution produces large output and interactive transcript data
- **THEN** the saved run payload contains the full captured content for retained entries
- **AND THEN** selecting that history entry displays the full persisted output

### Requirement: Legacy history migration to external store
If external history index metadata is absent and legacy `SavedState.History` entries exist, the system SHALL import those legacy entries into the external history store and clear the legacy field after successful migration.

#### Scenario: One-time migration from in-config history
- **WHEN** startup finds no `history.index.json` entries and `SavedState.History` contains entries
- **THEN** those entries are imported into external run payload files and indexed metadata
- **AND THEN** `SavedState.History` is cleared and config persistence no longer uses it for normal history storage

