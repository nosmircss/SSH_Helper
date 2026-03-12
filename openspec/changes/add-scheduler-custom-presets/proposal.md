# Change: Add scheduler-local custom presets

## Why
Scheduler jobs can currently target only a saved preset or a preset folder. Users need a scheduler-only preset that stores its own command or YAML script content in the job itself so they can automate one-off or job-specific behavior without creating a shared preset in the main preset library.

## What Changes
- Add a scheduler-local `Custom Preset` job target that persists job-owned command/script content.
- Reuse the existing preset execution pipeline by materializing custom job content as a transient `PresetInfo` at runtime.
- Add a dedicated content authoring tab to the scheduler job editor using the existing script editor stack.
- Preserve backward compatibility for existing preset and folder jobs, export/import, and scheduler integrity flows.

## Impact
- Affected specs: `job-scheduler`
- Affected code: `Models/JobDefinition.cs`, `JobEditorDialog.cs`, `Services/JobExecutionService.cs`, scheduler import/export/list flows, and scheduler-focused tests
