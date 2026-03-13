## 1. Preset save UX
- [x] 1.1 Add preset save impact resolution for scheduled jobs that reference the saved preset directly or via its current folder.
- [x] 1.2 Add a single save confirmation dialog for referenced preset saves, including rename-existing vs create-new choices without stacked popups.
- [x] 1.3 Keep the unsaved preset diff visible in that save confirmation and collapse the affected-job list behind an explicit expand action.

## 2. Scheduler drift removal
- [x] 2.1 Remove drift reevaluation from preset and folder mutation paths.
- [x] 2.2 Remove drift indicators from scheduler and job editor surfaces.
- [x] 2.3 Ignore legacy `HasDriftWarning` state during scheduled and run-now execution.

## 3. Verification
- [x] 3.1 Add focused tests for save impact resolution, referenced-save dialog flows, and legacy drift-flag execution behavior.
- [x] 3.2 Extend dialog coverage for the combined diff-plus-impact prompt, collapsed affected-job list, and rename-choice button variants.
- [x] 3.3 Run focused `dotnet test`, clean `dotnet build`, and `openspec validate replace-scheduler-drift-with-save-warning --strict --no-interactive`.
