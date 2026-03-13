## ADDED Requirements

### Requirement: Scheduler-local custom presets
The scheduler SHALL support a job-local custom preset target whose command or YAML script content is stored in the job definition instead of the shared preset library.

#### Scenario: Save a custom preset job
- **WHEN** an operator selects `Custom Preset` in the scheduler job editor, enters job-local content, and saves
- **THEN** the job definition is persisted with that custom preset content
- **AND** the job does not require a shared preset or folder target name

#### Scenario: Execute a custom YAML preset job
- **WHEN** a scheduler job with `Custom Preset` content contains a valid YAML script and the job runs
- **THEN** the scheduler executes it through the same script-capable preset execution pipeline used by shared presets
- **AND** the job uses the application default timeout when no shared preset is involved

#### Scenario: Import or export a custom preset job
- **WHEN** a scheduler job with a custom preset is exported and later imported
- **THEN** the job-local custom preset content round-trips with the job definition
- **AND** the imported job is not treated as missing a preset or folder target
