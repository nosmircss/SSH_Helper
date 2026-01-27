# ssh-execution Specification

## Purpose
TBD - created by archiving change refactor-execution-and-io. Update Purpose after archive.
## Requirements
### Requirement: Unified execution orchestration
The system SHALL execute presets through a single orchestration flow for all host selection modes (all, selected, checked), applying the same timeout resolution, preset creation, and error handling logic.

#### Scenario: Execute selected hosts
- **WHEN** a user runs a preset against selected hosts
- **THEN** the same orchestration flow is used as for running against all hosts

### Requirement: Pooled execution parity
When connection pooling is enabled, the system SHALL apply the same SSH algorithm settings and timeouts as non-pooled execution.

#### Scenario: Pooled connection uses algorithms
- **WHEN** pooling is enabled and a host specifies SSH algorithms
- **THEN** the pooled connection applies the specified algorithms before connecting

### Requirement: Connection pooling toggle
The system SHALL allow connection pooling to be enabled or disabled via configuration or UI settings.

#### Scenario: Disable pooling
- **WHEN** a user disables pooling
- **THEN** new executions use non-pooled connections

### Requirement: Multi-host preset execution confirmation
When a preset execution targets multiple hosts, the system SHALL present an execution options dialog that lists the selected hosts and allows the user to confirm execution settings before starting.

#### Scenario: Preset run with multiple hosts selected
- **WHEN** a user runs a preset with more than one host selected
- **THEN** the execution options dialog is shown with the selected hosts and execution settings

