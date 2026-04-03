## ADDED Requirements
### Requirement: Canonical Flow Canvas Export with Diagnostics
The system SHALL export executable canvas nodes through a canonical graph-to-YAML path and SHALL return structured diagnostics for warnings and errors.

Executable nodes that cannot be exported validly SHALL produce explicit export errors and SHALL NOT be silently omitted.

#### Scenario: Invalid executable block fails export
- **WHEN** a user applies or executes a canvas graph containing an invalid executable block
- **THEN** export returns `success=false` with actionable error diagnostics
- **AND** no silent node omission occurs

#### Scenario: Edited imported block persists in exported YAML
- **WHEN** a user edits properties of an imported executable block in Flow Canvas
- **THEN** exported YAML reflects the edited properties
- **AND** the result no longer depends on stale pre-edit serialized snippet data for that block

### Requirement: Unified Canvas Execution Trigger
Flow Canvas SHALL execute run and test-step actions through a single host message contract (`execute-canvas`) that atomically applies the graph export and starts execution.

#### Scenario: Keyboard and toolbar run parity
- **WHEN** an operator triggers run from keyboard or toolbar
- **THEN** both paths use the same `execute-canvas` host pipeline
- **AND** execution behavior is identical

#### Scenario: Test-step uses unified execution path
- **WHEN** an operator triggers test-step from keyboard or toolbar
- **THEN** both paths use the same `execute-canvas` host pipeline
- **AND** selected node context is passed in the same request shape

### Requirement: Flow Canvas Interaction Correctness
The Flow Canvas editor SHALL preserve expected interaction behaviors for node movement undo, breakpoint visuals, context menu invocation, comment visibility, and selection synchronization.

#### Scenario: Move undo restores pre-drag position
- **WHEN** an operator drags a node and performs undo
- **THEN** node position reverts to the pre-drag state

#### Scenario: Comment added from context menu appears on canvas
- **WHEN** an operator chooses Add Comment from a node context menu
- **THEN** a comment node is created and visible on the canvas
- **AND** it participates in undo/redo behavior

#### Scenario: Box selection updates active selection state
- **WHEN** an operator uses drag-box selection
- **THEN** selected-node state is synchronized with current React Flow selection
- **AND** downstream actions operate on that synchronized selection
