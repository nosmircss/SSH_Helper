## ADDED Requirements
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
