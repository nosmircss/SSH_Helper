## ADDED Requirements
### Requirement: Throttled UI output rendering
The system SHALL throttle UI output rendering to no more than once every 50 milliseconds while preserving full output capture for history.

#### Scenario: High-volume output
- **WHEN** a command produces rapid output
- **THEN** the output display updates at most once every 50 milliseconds
- **AND THEN** all output is still captured for history

### Requirement: Command-end flush
The system SHALL flush any buffered UI output when a command completes.

#### Scenario: Command completion
- **WHEN** a command finishes and the prompt returns
- **THEN** any buffered output is flushed immediately to the UI

### Requirement: Debug output bypass
The system SHALL render debug output without throttling.

#### Scenario: Debug output
- **WHEN** output is prefixed with [DEBUG] or [SSH DEBUG]
- **THEN** it is rendered immediately and not delayed by throttling
