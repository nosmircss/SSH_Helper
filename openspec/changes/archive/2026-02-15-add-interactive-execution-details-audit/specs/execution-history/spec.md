## ADDED Requirements
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
