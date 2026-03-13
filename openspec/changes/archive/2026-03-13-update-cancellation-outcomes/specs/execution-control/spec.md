## ADDED Requirements

### Requirement: Explicit cancelled execution outcome
When an operator stops an in-progress manual execution, the system SHALL treat the run as cancelled after the active execution unwinds and SHALL keep the stop request distinct from ordinary failure.

#### Scenario: Manual stop requests cancellation before unwind
- **WHEN** the operator clicks Stop while a manual preset or folder execution is still running
- **THEN** the UI reports that cancellation has been requested
- **AND THEN** the final run outcome is recorded as cancelled after the active execution path exits

#### Scenario: Cancelled folder run overrides mixed host results
- **WHEN** a folder execution has already observed one or more host failures and the operator then clicks Stop
- **THEN** the overall folder run is recorded as cancelled
- **AND THEN** per-host failure details remain available for the hosts that failed before cancellation
