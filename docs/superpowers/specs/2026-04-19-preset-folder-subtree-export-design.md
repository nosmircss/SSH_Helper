# Preset Folder Subtree Export Design

**Date:** 2026-04-19
**Status:** Approved
**Feature:** Export a selected preset folder subtree to JSON

---

## Summary

Add a folder-scoped export path to the preset tree so an operator can right-click a folder, choose `Export Folder...`, and save that folder plus every descendant folder and preset to a `.json` file. The export reuses the existing bulk preset JSON shape (`version`, `exportDate`, `presets`, `folders`) so the current import flow can consume it without a new importer. The selected folder is rebased to the bundle root, which lets nested exports import cleanly either at root or under a chosen destination folder.

## Goals

- Let operators export one folder subtree without exporting the entire preset library.
- Preserve folder metadata and preset content for the selected folder and everything beneath it.
- Keep the exported file compatible with the existing `Import All Presets` JSON flow.
- Keep the UI surface minimal by adding the action to the existing preset-folder context menu.

## Non-Goals

- Changing single-preset clipboard export/import.
- Adding a new import format or a dedicated folder-import dialog.
- Exporting jobs, history, environments, or other non-preset data.
- Refactoring the preset tree or import pipeline beyond what this feature needs.

---

## Behavior

### UI

- Add `Export Folder...` to the preset-tree context menu.
- Show it only when a folder is selected on the `Presets` tab.
- Hide it for preset nodes and for the `Favorites` tab, matching the current limited favorites context menu.
- Clicking the action opens a `SaveFileDialog` filtered to `*.json`.
- The default filename uses the selected folder's display name, not the full slash-delimited path.

### Exported content

- The JSON includes only the selected folder and descendants.
- Unrelated root folders, sibling folders, and presets outside the subtree are excluded.
- Folder metadata is preserved through the existing `FolderInfo` serialization.
- Preset entries keep their existing `PresetInfo` serialization, including folder assignment, favorite flag, timeout, and canvas layout.

### Path rebasing

- If the selected folder is root-level, exported paths remain unchanged.
- If the selected folder is nested, the export rebases paths so the selected folder becomes the root of the bundle.
- Example:
  - source folder: `Network/Prod`
  - descendant folder: `Network/Prod/Core`
  - exported folder keys: `Prod`, `Prod/Core`
  - presets formerly assigned to `Network/Prod/Core` export with folder `Prod/Core`
- This keeps ancestors outside the selection out of the file while still allowing import to recreate the subtree.

### Import compatibility

- Existing `Import All Presets` behavior remains unchanged.
- Importing an exported subtree at root recreates the selected folder as a root folder.
- Importing an exported subtree into destination folder `Archive` recreates it under `Archive/<selected-folder-name>`.
- Existing preset-name collision handling (`_imported`) continues to apply.

---

## Implementation

### `PresetManager`

- Add a folder-subtree export method that:
  - validates the requested folder exists,
  - gathers the selected folder and descendant folders,
  - gathers presets assigned to the selected folder or descendant folders,
  - rebases folder keys and preset `Folder` values to the bundle root,
  - writes the existing JSON export shape to disk.
- Keep the existing `ExportAllToFile` path intact.

### `Form1`

- Add a folder-only context-menu item and click handler.
- Resolve the selected folder from the same context-source rules used for preset actions.
- Add a small save-dialog helper seam so WinForms tests can drive the export path without opening a real dialog.
- Show success/error dialogs aligned with the existing preset export messaging.

### Tests

- Add `PresetManager` regression coverage for:
  - exporting only the selected subtree,
  - rebasing nested folder paths to the selected folder root,
  - preserving import compatibility through the existing importer.
- Add WinForms coverage for:
  - folder-only visibility of the new context-menu item,
  - invoking folder export through the form with a test-controlled save path,
  - avoiding real dialogs during tests.

---

## Risks and tradeoffs

- Reusing the existing JSON schema avoids importer churn, but the meaning of folder keys becomes "bundle-root-relative" for subtree exports. That is acceptable because the current importer already treats the file's folder keys as the source structure to recreate.
- Adding a save-dialog seam in `Form1` slightly increases test-only surface area, but it keeps the production behavior unchanged and matches existing prompt/file-picker override patterns in the form.

## Verification plan

- Focused `PresetManager` tests for subtree filtering and rebasing.
- Focused WinForms tests for context-menu visibility and folder export invocation.
- `dotnet build SSH_Helper.csproj -p:SkipFlowCanvasBuild=true -p:UseAppHost=false ...`
- `openspec validate add-preset-folder-subtree-export --strict --no-interactive`
