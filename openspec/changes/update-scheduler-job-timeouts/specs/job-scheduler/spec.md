## ADDED Requirements

### Requirement: Scheduler per-job timeout overrides
The scheduler SHALL support optional per-job command and connection timeout overrides that apply to scheduled execution and `Run Now` without requiring preset changes.

#### Scenario: Preset-backed job overrides inherited timeouts
- **WHEN** an operator enables per-job command or connection timeout overrides for a scheduled job targeting a preset or folder and saves the job
- **THEN** the saved job persists only those override values that were enabled
- **AND** later scheduled or `Run Now` execution uses the per-job override instead of the inherited timeout for that dimension

#### Scenario: Unset override keeps inherited timeout behavior
- **WHEN** a scheduled job leaves one or both timeout overrides disabled
- **THEN** execution continues to inherit command timeout from the preset timeout or application default and connection timeout from the application default
- **AND** existing jobs without the new fields continue to behave as they did before the feature was added

#### Scenario: Custom preset job shows app default as inherited source
- **WHEN** an operator edits a scheduled job targeting `Custom Preset` and does not enable a command timeout override
- **THEN** the editor indicates that the inherited command timeout source is the application default
- **AND** execution uses that application default command timeout until the operator enables a per-job override
