## ADDED Requirements

### Requirement: Scheduled job cancellation outcome and controls
The scheduler SHALL provide a Job List cancel action for running jobs and SHALL persist cancelled scheduled runs distinctly from failed or skipped runs.

#### Scenario: Operator cancels a running scheduled job from the Job List
- **WHEN** a scheduled job is running and the operator clicks `Cancel` in the scheduler Job List toolbar or context menu
- **THEN** the scheduler requests cancellation for that job without affecting unrelated runs
- **AND THEN** the job's final state is recorded as cancelled when execution unwinds

#### Scenario: Cancelled scheduled run is shown distinctly in history
- **WHEN** a scheduled run is cancelled after partial host output has been produced
- **THEN** scheduler history persists the run with a cancelled outcome and retained partial per-host output
- **AND THEN** scheduler notifications and result text distinguish the run from failed and skipped entries
