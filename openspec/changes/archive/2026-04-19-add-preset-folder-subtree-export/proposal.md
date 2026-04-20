# Change: Add preset folder subtree export

## Why

Operators can currently export a single preset to the clipboard or export the entire preset library to JSON, but there is no way to export one folder and all of its nested presets/folders. That makes it awkward to share or archive a logically grouped preset library without either hand-copying presets or exporting unrelated data.

## What Changes

- Add a folder-scoped `Export Folder...` action to the preset-folder context menu.
- Export only the selected folder subtree to a JSON file.
- Reuse the existing bulk preset export JSON shape so the current import flow remains compatible.
- Rebase exported paths so the selected folder becomes the bundle root and can be imported cleanly at root or under a chosen destination folder.
- Keep existing single-preset clipboard export and full-library export behavior unchanged.

## Impact

- Affected specs:
  - `preset-organization`
- Affected code:
  - `Services/PresetManager.cs`
  - `Form1.cs`
  - `Form1.Designer.cs`
  - `SSH_Helper.Tests/Services/PresetManagerTests.cs`
  - `SSH_Helper.Tests/UI/*` (new folder export coverage)
  - `tasks/todo.md`
