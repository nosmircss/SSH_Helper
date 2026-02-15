## ADDED Requirements
### Requirement: Interactive capture close reasons in execution details
Execution details SHALL preserve interactive capture completion outcomes and transcript audit data at step completion time.

Audit contract:
- Interactive capture sessions SHALL record close reasons including `ctrl_c_continue`, `timeout_continue`, `early_close_partial`, and `natural_complete`.
- Session transcript SHALL be persisted when the step completes, even if a detached read-only window remains open for review.

#### Scenario: Ctrl+C continuation reason is persisted
- **WHEN** capture mode is stopped by `Ctrl+C`
- **THEN** interactive session details store close reason `ctrl_c_continue`
- **AND** transcript is available in execution history details

#### Scenario: Detached window does not block history completeness
- **WHEN** capture mode reaches timeout or natural completion and the window stays open detached
- **THEN** execution details already contain finalized transcript and close reason for that step

#### Scenario: Early close stores partial reason
- **WHEN** operator closes capture window before interrupt/completion
- **THEN** close reason `early_close_partial` is stored
- **AND** partial transcript is preserved in execution details
