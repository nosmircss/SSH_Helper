# M3: Script Editor DX

**Status**: NOT STARTED

**Why**: The current plain `TextBox` is the weakest point of the developer experience. Users writing YAML scripts with 20+ command types need syntax highlighting, autocomplete, and inline error detection.

---

## Progress Checklist

- [ ] Add `FastColoredTextBox` NuGet package to `SSH_Helper.csproj`
- [ ] Create `Services/Editor/EditorDiagnostic.cs` model
- [ ] Create `Services/Editor/YamlSshSyntaxHighlighter.cs` with dark/light palettes
- [ ] Create `Services/Editor/ScriptAutocompleteProvider.cs` with static + dynamic completions
- [ ] Create `Services/Editor/ScriptEditorValidationService.cs` with debounced validation
- [ ] Create `UI/IScriptEditor.cs` interface
- [ ] Create `UI/ScriptEditorControl.cs` FCTB wrapper
- [ ] Replace `txtCommand` TextBox with FCTB control in `Form1.Designer.cs`
- [ ] Wire autocomplete callbacks in Form1 (grid columns, preset names, variables)
- [ ] Wire inline validation to `ScriptValidationFormatter`
- [ ] Wire variable inspector (hover tooltips)
- [ ] Integrate with theme system (dark/light switching)
- [ ] Apply `FontSettings.CodeEditorFontSize` and `CodeFontFamily` to FCTB
- [ ] Test with large scripts (500+ lines) for performance
- [ ] Manual smoke test: highlighting, autocomplete, inline errors, dark↔light theme

---

## Editor Control Choice

**Recommendation: FastColoredTextBox (FCTB)**

| Control | Pros | Cons | Verdict |
|---------|------|------|---------|
| **FastColoredTextBox** | Pure WinForms, .NET 8 compatible, built-in syntax highlighting API, autocomplete, line numbers, text markers, folding, undo/redo. Lightweight (~300KB). | Smaller community than Scintilla. | **Recommended** |
| ScintillaNET | Feature-rich, battle-tested. | Requires native Scintilla DLL (x86/x64/ARM). Packaging complexity. | Good but complex |
| AvalonEdit via ElementHost | Excellent WPF editor. | Requires WPF hosting in WinForms. Airspace issues, DPI mismatches. | Rejected |
| Custom RichTextBox | No dependencies. | No line numbers, no autocomplete infra. Enormous effort. | Rejected |

### NuGet Package

```xml
<PackageReference Include="FastColoredTextBox" Version="2.16.24" />
```

---

## Syntax Highlighting

### New: `Services/Editor/YamlSshSyntaxHighlighter.cs`

Listens to FCTB `TextChanged` event. Processes only the changed range (`e.ChangedRange`) for efficiency.

### Color Scheme

| Token Category | Light Mode | Dark Mode | Regex Pattern |
|---|---|---|---|
| **Top-level YAML keys** (`name:`, `steps:`, `vars:`) | Bold `#0000FF` | Bold `#569CD6` | `^\s*(name\|description\|version\|debug\|nobanner\|vars\|steps)\s*:` |
| **Step commands** (`send:`, `print:`, `if:`, etc.) | Bold `#800080` | Bold `#C586C0` | `^\s*-\s*(send\|print\|wait\|set\|exit\|extract\|if\|foreach\|while\|...)\s*:` |
| **Step options** (`capture:`, `timeout:`, etc.) | `#008080` | `#4EC9B0` | `^\s*(capture\|suppress\|expect\|timeout\|on_error\|then\|else\|...)\s*:` |
| **Strings** (single/double quoted) | `#A31515` | `#CE9178` | `("[^"]*"\|'[^']*')` |
| **Numbers** | `#098658` | `#B5CEA8` | `\b\d+(\.\d+)?\b` |
| **Comments** (`#`) | `#008000` | `#6A9955` | `#.*$` |
| **Variables** (`${var}`, `{{col}}`) | `#FF8C00` | `#D7BA7D` | `\$\{[^}]+\}` and `\{\{[^}]+\}\}` |
| **Boolean/null** | `#0000FF` | `#569CD6` | `\b(true\|false\|yes\|no\|null)\b` |
| **YAML structure** (`---`, `-`) | `#808080` | `#808080` | `^---` and `^\s*-\s` |

Reads `AppConfiguration.DarkMode` and switches palettes on theme change.

---

## Autocomplete

### New: `Services/Editor/ScriptAutocompleteProvider.cs`

Uses FCTB's built-in `AutocompleteMenu` control.

### Trigger Conditions

| Context | What to Suggest |
|---------|----------------|
| After `- ` at step start (inside `steps:`) | Step command names: `send`, `print`, `wait`, `set`, `exit`, `extract`, `if`, `foreach`, `while`, `http`, `ping`, `dns`, `portcheck`, `sftp`, etc. |
| After a known key + `: ` | Valid values: `on_error:` → `continue`/`stop`; `level:` → `info`/`debug`/`warning`/`error`; `method:` → `GET`/`POST`/`PUT`/etc.; `format:` → `text`/`json`/`jsonl`/`csv` |
| After `${` | Known variable names (from `vars:` section + `set:` assignments + built-in: `_output`, `_timestamp`, `_last_error`, `_host`, `_port`, `_username`) |
| After `{{` | Grid column names from current DataGridView |
| At top-level position | `name`, `description`, `version`, `debug`, `nobanner`, `vars`, `steps` |

### Dynamic Variable Extraction

Parses current script text to find:
- Keys in `vars:` section → variable defaults
- `set:` assignments → variable names
- `capture:` values → captured output variables
- `into:` values → result variables (+ `_status`, `_headers`, `_avg`, etc.)

### Configuration

```csharp
var autocomplete = new ScriptAutocompleteProvider(
    getColumnNames: () => GetGridColumnNames(),   // From Form1 DataGridView
    getPresetNames: () => _presetManager.GetNames() // From PresetManager
);
autocomplete.AttachTo(fctbEditor);
```

---

## Inline Validation

### New: `Services/Editor/ScriptEditorValidationService.cs`

### Flow

```
User types → TextChanged event → Debounce (500ms timer) → ValidateAsync()
    → ScriptParser.IsYamlScript(text)
    → If yes: ScriptParser.Parse(text) + Validate(script, text)
    → Returns List<EditorDiagnostic>
    → Apply visual markers to FCTB
```

### Diagnostic Model: `Services/Editor/EditorDiagnostic.cs`

```csharp
public class EditorDiagnostic
{
    public int LineNumber { get; set; }        // 1-based
    public DiagnosticSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ColumnStart { get; set; }       // For underlines (optional)
    public int ColumnEnd { get; set; }
}

public enum DiagnosticSeverity { Error, Warning, Info }
```

### Visual Markers

- **Errors**: Red wavy underline via FCTB's `WavyLineStyle`, red circle in line number gutter
- **Warnings**: Yellow wavy underline, yellow triangle in gutter
- **Hover tooltip**: FCTB `ToolTipNeeded` event → show diagnostic message

### Variable Inspector (Hover)

Also via `ToolTipNeeded`:
- Hover over `${varname}` → show default value from `vars:` section
- Hover over `{{column}}` → show value from first grid row (or "[NOT FOUND]" if column missing)

---

## Migration: Replacing `txtCommand`

### Interface: `UI/IScriptEditor.cs`

```csharp
public interface IScriptEditor
{
    string Text { get; set; }
    int SelectionStart { get; set; }
    int SelectionLength { get; set; }
    string SelectedText { get; }
    bool ReadOnly { get; set; }
    bool WordWrap { get; set; }
    Font Font { get; set; }
    Color BackColor { get; set; }
    Color ForeColor { get; set; }
    event EventHandler TextChanged;
    event KeyEventHandler KeyDown;
    void SelectAll();
    void Copy();
    void Cut();
    void Paste();
    void Focus();
    void SetDiagnostics(IReadOnlyList<EditorDiagnostic> diagnostics);
    void ClearDiagnostics();
    void GoToLine(int lineNumber);
    int GetCurrentLine();
    Control AsControl();
}
```

### Wrapper: `UI/ScriptEditorControl.cs`

Wraps `FastColoredTextBox` and implements `IScriptEditor`. Instantiates the highlighter, autocomplete, and validation internally.

### Key Differences from TextBox

| TextBox Property | FCTB Equivalent | Notes |
|-----------------|----------------|-------|
| `Multiline` | Always multiline | Remove property set |
| `ScrollBars` | Built-in scrollbars | Remove property set |
| `AcceptsTab` | Native tab handling | Remove property set |
| `Lines` (string[]) | `Lines` (List\<string\>) | Minor adaptation |
| `ContextMenuStrip` | Same | Reassign |
| `SelectionChanged` | `SelectionChanged` | Use `Selection.Start.iLine`/`iChar` for position |

FCTB exposes `.Text`, `.SelectionStart`, `.SelectionLength`, `.SelectedText`, `.Copy()`, `.Cut()`, `.Paste()`, `.SelectAll()` with the same signatures — most Form1 references compile without changes.

### Font Integration

Apply existing `FontSettings.CodeEditorFontSize` and `CodeFontFamily` to FCTB's `Font` property. Wire up to `ApplyFontSettings` in Form1.

### Theme Integration

On dark/light mode switch, call highlighter's `SetTheme(isDark)` to swap color palettes. Apply FCTB background/foreground colors. May need `NativeMethods.SetWindowTheme` for scrollbar theming.

---

## Key Files

| File | Action |
|------|--------|
| `UI/IScriptEditor.cs` | CREATE |
| `UI/ScriptEditorControl.cs` | CREATE |
| `Services/Editor/YamlSshSyntaxHighlighter.cs` | CREATE |
| `Services/Editor/ScriptAutocompleteProvider.cs` | CREATE |
| `Services/Editor/ScriptEditorValidationService.cs` | CREATE |
| `Services/Editor/EditorDiagnostic.cs` | CREATE |
| `SSH_Helper.csproj` | MODIFY — add FastColoredTextBox NuGet |
| `Form1.Designer.cs` | MODIFY — replace txtCommand with FCTB |
| `Form1.cs` | MODIFY — wire autocomplete, validation, theme |
| `UI/DialogTheme.cs` | MODIFY — add FCTB color constants |
