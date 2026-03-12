## 1. Implementation
- [x] 1.1 Extend the scheduler job model and persistence with a `CustomPreset` target type and stored custom preset content.
- [x] 1.2 Update scheduler execution, timeout resolution, and target-display/import flows to support custom preset jobs without using `PresetManager`.
- [x] 1.3 Add scheduler job editor support for selecting `Custom Preset`, authoring job-local content in a dedicated tab, and validating non-empty content.
- [x] 1.4 Add focused tests for job model/storage/export, job editor validation and UI, and custom preset execution behavior.

## 2. Verification
- [x] 2.1 Validate the OpenSpec change with `openspec validate add-scheduler-custom-presets --strict --no-interactive`.
- [x] 2.2 Run focused scheduler/editor test coverage for custom preset behavior.
- [x] 2.3 Run a normal `dotnet build` for the solution.
