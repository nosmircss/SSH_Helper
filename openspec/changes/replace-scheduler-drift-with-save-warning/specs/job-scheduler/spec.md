## REMOVED Requirements

### Requirement: Scheduler drift activation on target change
**Reason**: Replace hidden drift state and blocked execution with an explicit save-time warning.
**Migration**: Scheduler jobs now keep running the latest preset or folder contents. Legacy drift fields remain file-compatible but are ignored by runtime execution and scheduler UI.

## ADDED Requirements

### Requirement: Preset save warning for referenced scheduled jobs
When an operator saves changes to an existing preset that is used by scheduled jobs, the system SHALL present a single save confirmation that identifies the affected jobs and explains that future scheduled and Run Now executions will use the updated preset contents.

#### Scenario: Save an existing referenced preset
- **WHEN** an operator saves changes to an existing preset that is referenced by one or more scheduled jobs
- **THEN** the save flow shows one confirmation dialog before saving
- **AND** the dialog preserves the unsaved preset diff as the primary review surface
- **AND** the dialog includes the affected job count and job names
- **AND** the dialog explains that future scheduled and Run Now executions will use the updated preset

#### Scenario: Review impacted scheduled jobs without hiding the diff
- **WHEN** the save confirmation warns that scheduled jobs are affected
- **THEN** the impacted-job count and warning text remain visible immediately
- **AND** the affected job list is available from a collapsed section without replacing the diff view

#### Scenario: Save a referenced preset after changing its name
- **WHEN** an operator edits an existing referenced preset and changes its name before saving
- **THEN** the save flow uses one dialog that offers `Rename Existing`, `Create New`, and `Cancel`
- **AND** the dialog explains that `Rename Existing` will carry affected scheduled jobs forward to the renamed preset

#### Scenario: Save a new unreferenced preset
- **WHEN** an operator saves a brand-new preset that does not mutate an existing preset already used by scheduled jobs
- **THEN** the system does not need to show the scheduled-job impact warning

### Requirement: Scheduler execution ignores legacy drift state
The system SHALL NOT block scheduled execution, Run Now execution, or scheduler list presentation based on legacy drift state persisted in job files.

#### Scenario: Legacy job retains a drift flag
- **WHEN** a job file still contains `HasDriftWarning = true`
- **THEN** scheduled evaluation still considers the job eligible based on its real enablement and schedule
- **AND** Run Now is not blocked by the legacy drift flag
- **AND** the scheduler UI does not show a drift-specific warning or name indicator for that job
