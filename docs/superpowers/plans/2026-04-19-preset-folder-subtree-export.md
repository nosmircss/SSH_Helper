# Preset Folder Subtree Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a folder-only preset-tree action that exports the selected folder and all nested presets/folders to a JSON file.

**Architecture:** Extend `PresetManager` with a subtree export path that filters and rebases the existing bulk preset JSON shape. Keep the UI change scoped to `Form1` and the preset context menu, using a save-dialog helper seam so WinForms tests can exercise the action without opening a real dialog.

**Tech Stack:** C#, WinForms, Newtonsoft.Json, xUnit, FluentAssertions, OpenSpec

---

### Task 1: Update tracking and spec artifacts

**Files:**
- Modify: `tasks/todo.md`
- Modify: `openspec/changes/add-preset-folder-subtree-export/proposal.md`
- Modify: `openspec/changes/add-preset-folder-subtree-export/tasks.md`
- Modify: `openspec/changes/add-preset-folder-subtree-export/specs/preset-organization/spec.md`
- Modify: `docs/superpowers/specs/2026-04-19-preset-folder-subtree-export-design.md`

- [ ] Confirm `tasks/todo.md` task 258 reflects the approved subtree-root behavior.
- [ ] Validate the OpenSpec proposal/tasks/spec delta against the approved design before touching code.

### Task 2: Add failing `PresetManager` coverage

**Files:**
- Modify: `SSH_Helper.Tests/Services/PresetManagerTests.cs`
- Modify: `Services/PresetManager.cs`

- [ ] Write a test that exports a selected folder subtree and proves unrelated presets/folders are excluded.
- [ ] Write a test that exports a nested folder and proves the file rebases to the selected folder name (`Prod`, `Prod/Core`, etc.).
- [ ] Write a test that imports the exported subtree with `ImportAllFromFile(..., "Archive")` and proves the subtree lands under `Archive/<selected-folder-name>`.
- [ ] Run: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetManagerTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/preset-folder-export-red/bin/ -p:BaseIntermediateOutputPath=artifacts/preset-folder-export-red/obj/ -v minimal`
- [ ] Confirm the new tests fail for the missing folder-subtree export path before implementation.

### Task 3: Add failing WinForms coverage for the folder export UI

**Files:**
- Create: `SSH_Helper.Tests/UI/Form1FolderExportTests.cs`
- Modify: `Form1.cs`
- Modify: `Form1.Designer.cs`

- [ ] Write a WinForms test that opens the preset-tree context menu on a folder and asserts `ctxExportFolder` is visible while `ctxExportPreset` is hidden.
- [ ] Write a WinForms test that selects a folder, injects a test save-path override, triggers the new folder export action, and asserts the JSON file is created.
- [ ] Run: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FolderExportTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/form1-folder-export-red/bin/ -p:BaseIntermediateOutputPath=artifacts/form1-folder-export-red/obj/ -v minimal`
- [ ] Confirm the tests fail before the UI implementation exists.

### Task 4: Implement subtree export in `PresetManager`

**Files:**
- Modify: `Services/PresetManager.cs`
- Modify: `Utilities/FolderPathUtility.cs` (only if a small helper is truly needed)

- [ ] Add a public subtree export method that validates the folder path and file path.
- [ ] Filter folders to the selected folder plus descendants.
- [ ] Filter presets to entries assigned to the selected folder or descendant folders.
- [ ] Rebase nested folder keys and preset `Folder` values so the selected folder becomes the bundle root.
- [ ] Serialize the existing JSON shape and write it to disk with indented formatting.
- [ ] Re-run the targeted `PresetManager` tests and confirm they pass.

### Task 5: Implement the folder export action in `Form1`

**Files:**
- Modify: `Form1.Designer.cs`
- Modify: `Form1.cs`
- Modify: `SSH_Helper.Tests/UI/Form1FolderExportTests.cs`

- [ ] Add `ctxExportFolder` to the preset context menu near the existing export/import actions.
- [ ] Update `ContextPresetLst_Opening(...)` so the new item is visible only for preset-tree folder nodes and hidden on favorites.
- [ ] Add a folder resolver/helper for context-based actions.
- [ ] Add a save-dialog helper with a test override seam instead of opening a raw dialog directly in the handler.
- [ ] Implement `ExportFolder(...)` to choose the save path, call `PresetManager`, and show success/error feedback.
- [ ] Re-run the targeted WinForms tests and confirm they pass.

### Task 6: Final verification and closeout

**Files:**
- Modify: `tasks/todo.md`

- [ ] Run: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetManagerTests|FullyQualifiedName~Form1FolderExportTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/preset-folder-export-green/bin/ -p:BaseIntermediateOutputPath=artifacts/preset-folder-export-green/obj/ -v minimal`
- [ ] Run: `dotnet build SSH_Helper.csproj -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/preset-folder-export-build/bin/ -p:BaseIntermediateOutputPath=artifacts/preset-folder-export-build/obj/ -v minimal`
- [ ] Run: `cmd /c openspec validate add-preset-folder-subtree-export --strict --no-interactive`
- [ ] Capture the implementation summary, verification results, and any residual warnings under task 258 in `tasks/todo.md`.
