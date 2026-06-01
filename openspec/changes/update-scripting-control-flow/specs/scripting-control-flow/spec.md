## ADDED Requirements

### Requirement: Universal step-level when guard
Every step SHALL support an optional `when:` guard that conditionally skips the step at dispatch time.

#### Scenario: Step skipped when guard is false
- **WHEN** any step defines `when:` and the condition evaluates to false
- **THEN** the step is skipped and reported as skipped
- **AND** execution continues with the next step

#### Scenario: Step runs when guard is true
- **WHEN** a step defines `when:` and the condition evaluates to true
- **THEN** the step executes normally

#### Scenario: Foreach retains per-item filter semantics
- **WHEN** a `foreach` step defines `when:`
- **THEN** `when:` continues to filter individual iterations rather than skipping the entire loop

### Requirement: Repeat-until loop
The scripting language SHALL provide a `repeat`/`until` loop that executes its body once before evaluating the exit condition.

#### Scenario: Body runs at least once
- **WHEN** a `repeat` step has an `until` condition that is already true before the first iteration
- **THEN** the body executes exactly once
- **AND** the loop then exits

#### Scenario: Repeats until condition becomes true
- **WHEN** a `repeat` step's `until` condition becomes true after several iterations
- **THEN** the body executes each iteration until the condition is true, bounded by `max_iterations`

#### Scenario: Break and continue inside repeat
- **WHEN** a `break` or `continue` step executes inside a `repeat` body
- **THEN** loop control behaves identically to `while` and `foreach`

### Requirement: Soft-assert run summary
Soft assertions (`assert` with `severity: warning`) SHALL be aggregated into an end-of-run summary.

#### Scenario: Summary reports soft-assert outcomes
- **WHEN** a script executes one or more soft assertions
- **THEN** the run reports an aggregate count of passed and failed soft assertions at completion

#### Scenario: Soft-assert failures do not terminate the script
- **WHEN** a soft assertion fails
- **THEN** the failure is recorded in the summary
- **AND** the script continues executing subsequent steps
