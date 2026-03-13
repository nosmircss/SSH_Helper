## ADDED Requirements

### Requirement: Cancelled run history retention
The system SHALL retain cancelled manual and folder executions in history with partial output, execution details, and per-host outcomes preserved.

#### Scenario: Cancelled preset run remains inspectable
- **WHEN** an operator stops a manual preset execution before completion
- **THEN** the saved history entry is marked cancelled
- **AND THEN** the captured partial output and execution details remain available after restart

#### Scenario: Cancelled folder run preserves partial host output
- **WHEN** an operator stops a folder execution after some host or preset work has already produced output
- **THEN** the saved history entry stores the partial transcript shown to the operator
- **AND THEN** per-host entries distinguish cancelled hosts from completed or failed hosts
