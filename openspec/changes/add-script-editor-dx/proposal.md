# Change: Add script editor DX improvements

## Why
The current plain text box makes authoring larger YAML scripts error-prone and slow, especially as step count and syntax surface area continue to grow.

## What Changes
- Replace the current script input textbox with a dedicated code-editor control via a reusable `IScriptEditor` abstraction and WinForms wrapper.
- Keep the editor dependency version flexible in spec scope (implementation uses a compatible package release for `net8.0-windows`; spec does not lock a fixed package version).
- Add syntax highlighting for YAML/script token categories using changed-range updates to avoid full-document rescans on every keystroke.
- Add parser-driven autocomplete (no hard-coded command list in editor code):
  - Command and option vocab must be derived from parser/runtime metadata (`ScriptParser` + script model contracts).
  - Value suggestions must include validated enum-like values (for example method/auth/content-type contracts).
  - Dynamic symbol suggestions must include script-defined variables, capture/into variables, runtime built-ins, and host-grid columns.
- Add debounced asynchronous inline validation with stale-work cancellation:
  - Trailing debounce: `400ms`
  - Maximum one in-flight validation task
  - Last-edit-wins behavior (cancel older validations when new text arrives)
  - Inline diagnostics enabled only for YAML script mode; cleared/disabled for plain command mode.
- Add diagnostics and hover behaviors with column-level precision:
  - `EditorDiagnostic` includes line + column start/end range.
  - Parser errors/warnings are mapped into editor diagnostics with severity and tooltip messages.
  - Variable inspector tooltips support `${var}` and `{{column}}` tokens.
- Integrate editor with existing font/theme configuration and preserve existing script editor keyboard shortcuts and context menu actions.
- Add unit tests for parser-driven completion extraction, debounce/cancellation behavior, diagnostics mapping, and non-YAML behavior; run large-script responsiveness verification (500+ lines).

## Impact
- Affected specs:
  - `script-editor` (new capability)
- Affected code:
  - `UI/IScriptEditor.cs` (new)
  - `UI/ScriptEditorControl.cs` (new)
  - `Services/Editor/EditorDiagnostic.cs` (new)
  - `Services/Editor/YamlSshSyntaxHighlighter.cs` (new)
  - `Services/Editor/ScriptAutocompleteProvider.cs` (new)
  - `Services/Editor/ScriptEditorValidationService.cs` (new)
  - `Form1.Designer.cs`
  - `Form1.cs`
  - `UI/DialogTheme.cs`
  - `SSH_Helper.csproj`
