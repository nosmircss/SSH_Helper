## ADDED Requirements
### Requirement: Scheduler delete integrity and recovery
Preset and folder delete flows SHALL keep scheduler job state consistent for shared-library targets.

If a preset delete removes a preset targeted by a scheduler job, the job SHALL be disabled with a reason identifying the deleted preset.

If a recursive folder delete removes presets targeted by scheduler jobs, those preset-target jobs SHALL also be disabled with a reason identifying the deleted preset.

When an operator undoes a preset or folder delete in the same session, the system SHALL restore the affected scheduler jobs to the exact state captured before the delete.

#### Scenario: Recursive folder delete disables deleted preset jobs
- **WHEN** folder `Network/Legacy` is deleted recursively
- **AND** preset `Network/Legacy/Show Version` removed by that delete is targeted by a scheduler job
- **THEN** that preset-target job is disabled
- **AND** its disabled reason identifies the deleted preset

#### Scenario: Undo delete restores affected scheduler jobs
- **WHEN** deleting a preset or folder disables one or more scheduler jobs because their targets were removed
- **AND** the operator invokes `Undo Delete` in the same app session
- **THEN** the affected jobs are restored to the exact enabled state, target metadata, and disabled reason values they had before the delete
