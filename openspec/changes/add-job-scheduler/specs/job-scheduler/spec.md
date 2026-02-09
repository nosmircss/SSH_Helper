## ADDED Requirements

### Requirement: Scheduled job definitions
The system SHALL support persisted scheduled job definitions that include target preset or preset folder, schedule type, schedule expression/time, host targets, credentials mode, and notification preferences.

#### Scenario: Save scheduled job
- **WHEN** an operator creates a scheduled job in the scheduler UI and clicks save
- **THEN** the job definition is persisted and appears in the scheduler list after restart

### Requirement: Cron and one-time schedule evaluation
The scheduler SHALL support both cron-based recurring schedules and one-time schedules, computing next run times in local time and exposing them in the scheduler UI.

#### Scenario: Cron next-run preview
- **WHEN** an operator configures a valid cron expression
- **THEN** the scheduler computes and displays upcoming run occurrences

#### Scenario: One-time job auto-disable
- **WHEN** a one-time job executes successfully
- **THEN** the job is marked disabled and does not run again automatically

### Requirement: Missed-run startup policy
The scheduler SHALL NOT auto-execute jobs missed while the application was closed and SHALL record missed runs as skipped entries.

#### Scenario: Overdue cron job on startup
- **WHEN** the application starts and a recurring job should have run during downtime
- **THEN** the job is not executed immediately
- **AND** a skipped event is recorded in scheduler history/logging

### Requirement: Bounded concurrent execution
The scheduler SHALL execute due jobs with configurable concurrency limits and SHALL support run-now and cancellation controls.

#### Scenario: Concurrency cap enforcement
- **WHEN** more jobs become due than the configured concurrency limit
- **THEN** excess jobs wait in queue until execution slots become available

#### Scenario: Run now bypasses schedule wait
- **WHEN** an operator selects Run Now for an enabled job
- **THEN** execution starts immediately without waiting for the next scheduled occurrence

### Requirement: Job run history and output retention
The system SHALL record per-run scheduler history entries and SHALL persist full run output separately from summary metadata.

#### Scenario: Completed job history entry
- **WHEN** a scheduled run completes
- **THEN** history includes start/end time, duration, success state, and host success/failure counts
- **AND** full output is written to a dedicated output file referenceable from the history entry

### Requirement: Scheduler notifications and status visibility
The system SHALL surface scheduler state and run outcomes through in-app status text and desktop notifications.

#### Scenario: Failure notification
- **WHEN** a scheduled job completes with failures and notifications are enabled
- **THEN** the operator receives a failure notification summarizing the run result
