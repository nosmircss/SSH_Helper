# Change: Add script editor DX improvements

## Why
The current plain text box makes authoring larger YAML scripts error-prone and slow, especially as step count and syntax surface area continue to grow.

## What Changes
- Replace the current script input textbox with a dedicated code-editor control
- Add syntax highlighting for YAML and scripting tokens
- Add context-aware autocomplete for commands, keys, variables, and grid columns
- Add inline diagnostics with line-level feedback and hover help
- Integrate editor behavior with existing font and theme settings

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
