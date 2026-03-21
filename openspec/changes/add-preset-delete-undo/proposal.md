# Change: Add session-scoped preset and folder delete undo

## Why
Deleting a preset or folder is currently immediate and irreversible from the operator's point of view. That is risky in a preset library that supports nested folders, favorites, manual ordering, and scheduler references. The current delete flow also has two correctness gaps: deleting a folder with "move children to parent" flattens descendants instead of preserving the subtree shape, and recursive folder delete does not disable preset-target scheduler jobs for presets removed by that delete.

## What Changes
- Add a session-scoped, multi-level undo stack for preset and folder deletes.
- Surface delete undo through a single `Edit > Undo Delete` command and guarded `Ctrl+Z`.
- Capture and restore preset-library snapshots for deleted presets/folders, including ordering and favorites metadata.
- Capture and restore affected scheduler job state for delete and undo flows.
- Preserve descendant folder structure when deleting a folder with the "move children to parent" option.
- Disable preset-target scheduler jobs when a recursive folder delete removes their referenced presets.

## Impact
- Affected specs:
  - `preset-organization`
  - `job-scheduler`
- Affected code:
  - `Form1.cs`
  - `Form1.Designer.cs`
  - `Services/PresetManager.cs`
  - new preset delete undo support types under `Services/` or `UI/`
  - preset manager and form/UI tests
