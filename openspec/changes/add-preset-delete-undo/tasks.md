# Tasks: Add session-scoped preset and folder delete undo

## 1. Spec and tests
- [ ] 1.1 Add `preset-organization` and `job-scheduler` deltas for delete undo and delete-side scheduler integrity
- [ ] 1.2 Add failing preset-manager coverage for folder move-to-parent subtree preservation
- [ ] 1.3 Add failing preset-manager coverage for recursive folder delete disabling preset-target jobs
- [ ] 1.4 Add failing undo-service and form/UI coverage for multi-level delete undo, undo invalidation, and guarded `Ctrl+Z`

## 2. Delete/undo behavior
- [ ] 2.1 Add internal snapshot types for preset-library and affected-job state
- [ ] 2.2 Add a session-scoped delete undo service with bounded stack depth and restore semantics
- [ ] 2.3 Route preset and folder delete entry points through shared capture/delete/push helpers
- [ ] 2.4 Restore deleted preset/folder state and affected scheduler jobs through a single undo command

## 3. Folder and scheduler correctness fixes
- [ ] 3.1 Preserve descendant folders when deleting a folder with "move children to parent"
- [ ] 3.2 Disable preset-target scheduler jobs when recursive folder delete removes their target preset

## 4. UI wiring
- [ ] 4.1 Add `Edit > Undo Delete` with dynamic enablement and pending-action text
- [ ] 4.2 Guard `Ctrl+Z` so editor/textbox/grid edit undo keeps existing behavior
- [ ] 4.3 Clear the delete-undo stack on later non-delete preset-library mutations

## 5. Verification
- [ ] 5.1 Run focused preset-manager and undo/UI tests
- [ ] 5.2 Run broader preset/scheduler regression tests
- [ ] 5.3 Run `dotnet build .\\SSH_Helper.sln -nologo`
- [ ] 5.4 Run `openspec validate add-preset-delete-undo --strict --no-interactive`
