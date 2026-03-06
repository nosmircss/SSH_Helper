# Change: Add folder-level base environment inheritance

## Why
Preset folders can already organize related scripts, but they cannot currently carry their own environment baseline. That forces operators to keep switching the global base environment manually when moving between folders, even when an entire subtree should default to a different environment.

## What Changes
- Add an optional folder-level base environment override in preset folder metadata
- Let operators assign or clear that override from the preset-folder context menu
- Resolve preset environment context with this precedence: global base environment, then nearest folder base environment, then preset-declared script environment
- Apply folder base resolution when loading presets and when selecting or running a folder
- Keep folder base references valid when folders or environments are renamed or deleted

## Impact
- Affected specs:
  - `environment-management`
  - `preset-organization`
- Affected code:
  - `Models/FolderInfo.cs`
  - `Services/PresetManager.cs`
  - `EnvironmentDialog.cs`
  - `Form1.cs`
  - `Utilities/PresetEnvironmentLoadPlanner.cs`
  - `tasks/todo.md`
  - `SSH_Helper.Tests/Services/PresetManagerTests.cs`
  - `SSH_Helper.Tests/Utilities/PresetEnvironmentLoadPlannerTests.cs`
