## ADDED Requirements

### Requirement: Scheduler missed-run lifecycle recording
The scheduler SHALL persist the last application shutdown timestamp and, on startup, detect recurring jobs missed during downtime and record one skipped scheduler summary event per affected job/startup window without executing those jobs automatically.

#### Scenario: Startup records a compact skipped summary from downtime
- **WHEN** the application restarts after downtime and a recurring scheduler job should have run one or more times while the app was closed
- **THEN** the scheduler does not execute the missed run immediately
- **AND** one skipped scheduler history event is recorded for that job summarizing the missed run count and downtime range

#### Scenario: First launch does not fabricate missed runs
- **WHEN** the application starts without any previously persisted shutdown timestamp
- **THEN** the scheduler does not create skipped events for historical time windows it cannot verify

### Requirement: Scheduler history retention policy enforcement
Scheduler run persistence SHALL honor per-job max-runs and retention-day overrides when present and SHALL otherwise use the configured global scheduler history defaults and per-host output-size cap.

#### Scenario: Per-job retention override prunes that job's history
- **WHEN** a scheduler job defines a smaller max-runs or retention-days override than the global defaults
- **THEN** history pruning for that job uses the per-job override values

#### Scenario: Global defaults apply when no override is set
- **WHEN** a scheduler job leaves retention overrides unset
- **THEN** the scheduler history store uses the configured global defaults for run count, retention days, and per-host output size

### Requirement: Scheduler history timestamp accuracy
The scheduler history list SHALL display each run's actual start time and duration derived from the persisted run start and completion timestamps.

#### Scenario: History row uses the stored start timestamp
- **WHEN** a scheduler run is shown in the scheduler history list
- **THEN** the `Started` column reflects the persisted run start time rather than the completion time
- **AND** the duration matches the difference between the stored start and completion timestamps
