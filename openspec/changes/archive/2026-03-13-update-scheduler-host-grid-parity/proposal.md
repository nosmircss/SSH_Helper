# Change: Update scheduler host-grid parity

## Why
The scheduler Hosts tab currently behaves like a simplified mini-grid instead of matching the main host grid workflow and presentation. That makes manual host authoring, per-host credential columns, clipboard-heavy editing, and visual affordances inconsistent with the rest of the application.

## What Changes
- Add scheduler host-column operations that match the main grid's add, rename, delete, and reorder behavior
- Reuse the main grid's keyboard and clipboard editing workflow for selection, copy, paste, delete, keypress-to-edit, and double-click editing
- Align scheduler CSV import and main-grid copy behavior with the same parsing and row-selection semantics
- Refresh scheduler host-count feedback whenever inline edits change whether a row has a usable `Host_IP`
- Align scheduler host-grid visual treatment with the main hosts grid, including row sizing, row-header/row-number affordances, themed scroll behavior, and selection/checkbox styling where applicable

## Impact
- Affected specs:
  - `job-scheduler`
- Affected code:
  - `JobEditorDialog.cs`
  - `Form1.cs`
  - `Services/CsvManager.cs`
  - `UI/DialogTheme.cs`
  - Shared host-grid helper code extracted from the main form, if needed
