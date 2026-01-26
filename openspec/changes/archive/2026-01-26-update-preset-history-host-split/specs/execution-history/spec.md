## ADDED Requirements
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
