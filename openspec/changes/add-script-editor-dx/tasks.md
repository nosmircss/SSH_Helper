# Tasks: Add script editor DX improvements

## 1. Editor foundation
- [ ] 1.1 Add editor control dependency in `SSH_Helper.csproj`
- [ ] 1.2 Create `UI/IScriptEditor.cs` abstraction
- [ ] 1.3 Create `UI/ScriptEditorControl.cs` wrapper implementation
- [ ] 1.4 Replace script textbox wiring in `Form1.Designer.cs` and `Form1.cs`

## 2. Syntax and completion
- [ ] 2.1 Implement `YamlSshSyntaxHighlighter.cs` for YAML/script token categories
- [ ] 2.2 Implement `ScriptAutocompleteProvider.cs` for contextual command/key/value completion
- [ ] 2.3 Add dynamic completion sources for variables, captures, and grid columns

## 3. Validation and diagnostics
- [ ] 3.1 Create `EditorDiagnostic` model and severity enum
- [ ] 3.2 Implement debounced `ScriptEditorValidationService.cs`
- [ ] 3.3 Render inline markers and hover tooltips from parser validation output

## 4. UX integration
- [ ] 4.1 Integrate dark/light theme switching and color palettes
- [ ] 4.2 Apply existing code-editor font family and size settings
- [ ] 4.3 Preserve existing keyboard shortcuts and context menu behavior

## 5. Verification
- [ ] 5.1 Add tests for autocomplete context extraction and validation debounce behavior
- [ ] 5.2 Run manual smoke tests for highlighting, completions, and inline errors
- [ ] 5.3 Run large-script performance test (500+ lines) and tune changed-range processing
