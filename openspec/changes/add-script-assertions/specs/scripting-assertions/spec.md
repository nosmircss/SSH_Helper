## ADDED Requirements

### Requirement: Assertion step syntax
The scripting language SHALL support an `assert` step in both shorthand and mapping forms for expression-based checks.

#### Scenario: Shorthand assertion
- **WHEN** a script contains `assert: "${status} == 'OK'"`
- **THEN** the expression is evaluated as a boolean assertion condition

#### Scenario: Mapped assertion with message
- **WHEN** a script defines an assertion with `condition` and `message`
- **THEN** a failed assertion result includes the configured message in reporting

### Requirement: Expect alias parity
The scripting language SHALL support `expect` as an alias for `assert` with identical parsing and runtime semantics.

#### Scenario: Expect step execution
- **WHEN** a script uses `expect` instead of `assert`
- **THEN** execution and result reporting are equivalent to `assert`

### Requirement: Default soft-fail behavior
Assertion failures SHALL be recorded without terminating script execution by default.

#### Scenario: Failed assertion continues script
- **WHEN** an assertion condition evaluates to false and `fail_fast` is not enabled
- **THEN** the failure is recorded
- **AND** subsequent steps still execute

### Requirement: Optional fail-fast assertion mode
Assertions SHALL support a `fail_fast` option that stops execution immediately on the first failed assertion.

#### Scenario: Fail-fast terminates run
- **WHEN** an assertion with `fail_fast: true` evaluates to false
- **THEN** script execution terminates at that step with a failure outcome

### Requirement: Structured assertion result persistence
The system SHALL store per-host structured assertion results with each execution record, including condition text, pass/fail state, optional message, and step index/context.

#### Scenario: Multi-host assertion storage
- **WHEN** a preset runs against multiple hosts and includes assertions
- **THEN** each host record includes its own assertion results for that run

### Requirement: Assertion summary reporting
Execution history and scheduled-job reporting SHALL include assertion pass/fail summary counts.

#### Scenario: Scheduled run notification with assertion summary
- **WHEN** a scheduled workflow with assertions completes
- **THEN** the result summary includes total assertions passed and failed across targeted hosts
