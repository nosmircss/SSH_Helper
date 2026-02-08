# Tasks: Add script editor DX improvements

## 1. Editor foundation
- [ ] 1.1 Add editor control dependency in `SSH_Helper.csproj` using a compatible package version (do not encode a spec-level fixed version).
- [ ] 1.2 Create `UI/IScriptEditor.cs` abstraction with text, selection, focus, clipboard, diagnostics, and line/column APIs needed by `Form1`.
- [ ] 1.3 Create `UI/ScriptEditorControl.cs` wrapper implementation and map existing `txtCommand` behaviors.
- [ ] 1.4 Replace script textbox wiring in `Form1.Designer.cs` and `Form1.cs` while preserving:
  - cursor-position updates (`Ln/Col`)
  - context-menu actions (cut/copy/paste/select-all/pretty-format/validate)
  - `Ctrl+S` preset-save behavior gated by script editor focus.

## 2. Parser-driven syntax and completion
- [ ] 2.1 Implement `YamlSshSyntaxHighlighter.cs` for YAML/script token categories using changed-range updates.
- [ ] 2.2 Implement `ScriptAutocompleteProvider.cs` so command/option vocabulary is parser-driven (no hard-coded step-command arrays in editor code).
- [ ] 2.3 Add context-aware completion triggers for:
  - top-level keys
  - step command keys in `steps`
  - option keys and enum-like values
  - `${...}` script variables/built-ins
  - `{{...}}` host grid columns.
- [ ] 2.4 Add dynamic completion extraction from current script (`vars`, `set`, `capture`, `into`) and runtime built-ins.

## 3. Validation and diagnostics
- [ ] 3.1 Create `EditorDiagnostic` model and severity enum including `LineNumber`, `ColumnStart`, and `ColumnEnd`.
- [ ] 3.2 Implement `ScriptEditorValidationService.cs` with trailing `400ms` debounce, single in-flight validation, and cancellation of stale requests.
- [ ] 3.3 Run inline validation only when `ScriptParser.IsYamlScript(text)` is true; clear diagnostics in non-YAML mode.
- [ ] 3.4 Map parser validation errors and warnings into column-level diagnostics (token-span mapping with line-span fallback when token localization is unavailable).
- [ ] 3.5 Render inline markers and hover tooltips from mapped diagnostics.

## 4. UX integration
- [ ] 4.1 Integrate dark/light theme switching and color palettes
- [ ] 4.2 Apply existing code-editor font family and size settings
- [ ] 4.3 Preserve existing keyboard shortcuts and context menu behavior
- [ ] 4.4 Implement variable inspector tooltips:
  - `${var}` resolves from script/default/runtime symbol maps
  - `{{column}}` previews from selected grid row, falling back to first non-new row.

## 5. Verification
- [ ] 5.1 Add tests for parser-driven completion extraction and context triggering.
- [ ] 5.2 Add tests for validation debounce/cancellation (last-edit-wins) and non-YAML diagnostics clearing.
- [ ] 5.3 Add tests for diagnostic column-span mapping from parser messages/warnings.
- [ ] 5.4 Run manual smoke tests for highlighting, completions, inline errors, tooltips, and theme/font switching.
- [ ] 5.5 Run large-script responsiveness verification (500+ lines) and tune changed-range + debounce behavior to keep typing responsive.
