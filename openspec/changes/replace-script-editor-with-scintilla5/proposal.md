# Change: Replace script editor with Scintilla5.NET

## Why
The current custom RichTextBox-based editor still has interaction and performance edge cases (caret/scroll behavior, autocomplete stability, and typing smoothness). The project also prefers portable deployment without external runtime prerequisites.

Scintilla5.NET provides a mature native WinForms code-editing surface with strong text-editing performance and built-in behaviors needed for a VS Code-like authoring feel, while keeping deployment portable.

## What Changes
- Replace the current script editor control implementation with a Scintilla5.NET-backed control.
- Keep `IScriptEditor` as the abstraction boundary so `Form1` integration remains stable.
- Preserve parser-driven completion and diagnostics from existing C# services (`ScriptParser`, validation and symbol providers).
- Keep existing command-editor settings model and behavior semantics (indentation, smart-enter, autocomplete, inline validation, warnings, tooltips).
- Implement explicit "familiar editor" interaction expectations:
  - typing, caret movement, selection growth/shrink, and undo/redo remain uninterrupted while completion is visible
  - completion list updates as text changes and supports expected commit/dismiss keys (`Enter`, `Tab`, `Escape`, click)
  - scroll-past-end behavior allows the final line to sit near the top of the viewport
  - pressing `Enter` at end-of-file reveals caret on the new line without manual scrolling
  - clicking to relocate caret always dismisses completion.
- Add measurable verification gates for interaction and performance:
  - editor must remain responsive for at least 500-line scripts
  - no dropped keystrokes during rapid typing with completion + inline diagnostics enabled
  - suggestion popup latency and newline reveal behavior verified in smoke/perf checklist.
- Maintain portable packaging (no external runtime like WebView2 required).
- Remove obsolete editor dependency baggage if no longer needed after migration.

## Impact
- Affected specs:
  - `script-editor-scintilla-host` (new capability)
- Affected code:
  - `UI/ScintillaScriptEditorControl.cs` (new)
  - `UI/IScriptEditor.cs`
  - `Form1.cs`
  - `Form1.Designer.cs`
  - `SettingsDialog.cs`
  - `Models/AppConfiguration.cs`
  - `Services/Editor/*` (adapter integration updates)
  - `SSH_Helper.csproj`
