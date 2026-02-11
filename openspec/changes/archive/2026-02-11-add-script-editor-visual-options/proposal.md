# Change: Add Script Editor Visual Options

## Why
Operators need stronger visual structure in the script editor (folding, guides, current-line focus, braces, and long-line/whitespace cues), but these behaviors must remain user-configurable instead of being hard-coded.

## What Changes
- Add configurable Scintilla visual aids for script authoring:
- current-line highlight
- indentation guides
- whitespace visibility
- long-line edge ruler with adjustable column
- code folding margin with markers
- brace matching highlight
- Extend `CommandEditorSettings` with persisted toggles and numeric bounds for visual aids.
- Extend `SettingsDialog` Command Editor tab so operators can enable/disable and tune these features.
- Apply settings live in `ScintillaScriptEditorControl` without restart.
- Add/extend tests for defaults, config round-trip, and Scintilla behavior mapping.

## Impact
- Affected specs:
- `script-editor`
- `script-editor-scintilla-host`
- Affected code:
- `Models/AppConfiguration.cs`
- `SettingsDialog.cs`
- `UI/ScintillaScriptEditorControl.cs`
- `SSH_Helper.Tests/Services/ConfigurationServiceCommandEditorSettingsTests.cs`
- `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs`
