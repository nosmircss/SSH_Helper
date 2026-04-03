## ADDED Requirements

### Requirement: Front-End-Only Preset Reconstruction for Parity
Flow Canvas parity automation SHALL reconstruct presets through front-end graph construction actions and SHALL NOT rely on YAML preset graph-loading paths.

#### Scenario: Parity run builds a preset without load-graph
- **WHEN** a parity test reconstructs a preset from `qa_presets.json`
- **THEN** graph state is built via front-end construction hooks/actions
- **AND** no `load-graph` preset import message is used for that reconstruction

### Requirement: Graph-Native Container Export for Advanced Branching
Flow Canvas export SHALL generate canonical YAML for graph-authored container branches for `try`, `switch`, `parallel`, and `if` `elif` without requiring stored `_yamlSnippet` content.

#### Scenario: Try block exports do/catch/finally from graph topology
- **WHEN** a canvas graph contains a `try` block with connected `do`, `catch`, and `finally` branches
- **THEN** export emits valid YAML with `try.do`, `try.catch`, and `try.finally` structure
- **AND** export diagnostics remain empty for branch-shape-valid graphs

#### Scenario: Switch block exports cases and else from graph topology
- **WHEN** a canvas graph contains a `switch` block with multiple case branches and an else branch
- **THEN** export emits valid YAML with `switch.cases[].value/do` and `switch.else`
- **AND** case ordering follows graph branch ordering

#### Scenario: Parallel block exports branch steps from graph topology
- **WHEN** a canvas graph contains a `parallel` block with multiple connected branches
- **THEN** export emits valid YAML with `parallel.steps`
- **AND** each branch step remains semantically equivalent to the authored node configuration

### Requirement: Start-Node Advanced Section Authoring
Flow Canvas SHALL provide Start-node authoring support for `vars`, `imports`, and `subroutines`, and SHALL include those sections in exported YAML preamble.

#### Scenario: Start advanced sections persist through export
- **WHEN** an operator configures `vars`, `imports`, and `subroutines` in Start settings
- **THEN** exported YAML contains those sections with valid structure
- **AND** downstream parsing/validation accepts the exported script

### Requirement: QA Catalog Semantic Parity Matrix
The system SHALL provide automated parity tests that rebuild every valid YAML preset in `qa_presets.json` and verify exported YAML semantic equivalence.

#### Scenario: Valid QA preset roundtrip via front-end construction
- **WHEN** a valid QA preset is reconstructed in Flow Canvas and exported
- **THEN** exported YAML parses and validates with canonical validation enabled
- **AND** exported script semantics are equivalent to the source preset semantics

### Requirement: Intentional-Invalid Preset Negative Coverage
The system SHALL keep intentional-invalid QA presets in a dedicated negative parity suite.

#### Scenario: Intentional-invalid preset remains invalid after reconstruction
- **WHEN** an intentional-invalid QA preset is reconstructed and exported through Flow Canvas
- **THEN** validation fails with diagnostics
- **AND** the negative suite reports the case as expected-failure rather than success

### Requirement: Synthetic Coverage for QA-Catalog Gaps
Parity automation SHALL include synthetic reconstruction cases for block types missing from the QA preset catalog.

#### Scenario: Browser callback synthetic parity case
- **WHEN** parity automation runs synthetic coverage
- **THEN** it includes a `browser_callback` reconstruction case
- **AND** exported YAML for that case passes validation and semantic expectations

### Requirement: Gesture-Path Smoke Verification
Flow Canvas parity validation SHALL include a gesture-driven smoke suite covering real drag/connect/edit interactions.

#### Scenario: Gesture smoke suite validates real UI path
- **WHEN** gesture smoke tests run
- **THEN** blocks can be created/connected/edited through visible UI gestures
- **AND** resulting export remains valid
