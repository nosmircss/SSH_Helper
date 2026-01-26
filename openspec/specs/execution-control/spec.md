# execution-control Specification

## Purpose
TBD - created by archiving change add-execution-safety-and-ux. Update Purpose after archive.
## Requirements
### Requirement: Per-run cancellation disposal
Each execution run SHALL create and dispose its own cancellation token source after completion.

#### Scenario: Sequential executions
- **WHEN** an execution completes and a new execution starts
- **THEN** the previous cancellation token source is disposed and a new one is created for the new run

### Requirement: Stop-on-first-error determinism
When Stop-on-first-error is enabled for folder execution, the system SHALL cancel outstanding work after the first failure is observed.

#### Scenario: Parallel preset failure
- **WHEN** a preset fails during parallel folder execution and Stop-on-first-error is enabled
- **THEN** the execution cancels remaining presets and reports the failure deterministically

