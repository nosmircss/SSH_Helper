## MODIFIED Requirements
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

## ADDED Requirements
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
