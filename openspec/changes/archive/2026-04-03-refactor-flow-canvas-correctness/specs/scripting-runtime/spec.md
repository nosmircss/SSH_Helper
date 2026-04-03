## ADDED Requirements
### Requirement: Scoped Step Identity for Runtime Events
The scripting runtime SHALL emit a scope-aware `StepPath` identity for step lifecycle and debug pause/resume events.

`StepPath` SHALL uniquely identify steps across nested control-flow scopes.

#### Scenario: Nested step emits unique path
- **WHEN** execution enters a nested step inside control-flow containers
- **THEN** emitted step events include a `StepPath` distinct from top-level siblings

#### Scenario: Debug pause includes scoped identity
- **WHEN** debug pause state changes for a nested step
- **THEN** the pause/resume event includes `StepPath`
- **AND** callers can resolve the corresponding canvas node without relying on flat local step index
