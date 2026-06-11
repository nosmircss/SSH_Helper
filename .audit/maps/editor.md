# Subsystem map: Script editor (Scintilla)

Scope: `Services/Editor/*.cs` + `UI/ScintillaScriptEditorControl.cs` + `UI/IScriptEditor.cs`, plus the wiring in `Form1.cs`, `JobEditorDialog.cs`, `SettingsDialog.cs`, and `Utilities/ScintillaNativeBootstrap.cs`.

All paths relative to repo root `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper`.

## File inventory (actual contents, verified by glob)

| File | LOC | Role |
|---|---|---|
| `UI/ScintillaScriptEditorControl.cs` | 2601 | UserControl wrapping ScintillaNET; all visuals, popup completion UI, key handling, folding, indicators, tooltips, theming |
| `UI/IScriptEditor.cs` | 31 | Editor abstraction Form1/JobEditorDialog code against (Text/selection/diagnostics surface) |
| `Services/Editor/ScriptAutocompleteProvider.cs` | 1246 | Context-sensitive completion engine (pure text analysis, no UI) |
| `Services/Editor/EditorTextUtilities.cs` | 672 | Tab/Shift+Tab indentation, Smart Enter, Ctrl+Enter sibling-step insertion (pure functions returning `EditorTextEdit`) |
| `Services/Editor/ScriptEditorValidationService.cs` | 409 | Debounced async validation → `EditorDiagnostic` list via `DiagnosticsUpdated` event |
| `Services/Editor/YamlSshSyntaxHighlighter.cs` | 282 | Regex/line-based highlight span builder with light/dark palettes |
| `Services/Editor/EditorDiagnostic.cs` | 43 | Diagnostic DTO (1-based line/column span, Error/Warning/Info severity) |
| `Utilities/ScintillaNativeBootstrap.cs` | 137 | Native Scintilla/Lexilla DLL resolution/extraction at startup (`Program.cs:58`) |

Two production hosts of the control: the main command editor `txtCommand` in `Form1` (wired at `Form1.cs:656-661`) and the custom-preset editor `_txtCustomPresetCommands` in `JobEditorDialog` (wired at `JobEditorDialog.cs:585-591`, own provider/highlighter/validation-service instances at lines 61-63, 175).

---

## Feature inventory

### 1. Syntax highlighting (YAML + SSH script aware)
- **What**: Per-line regex coloring of: known top-level keys, known step commands, known option keys (incl. nested table column keys `header`/`field`), `${var}` / `{{column}}` interpolations, quoted strings, numbers, booleans/null, and `#` comments (quote-aware comment detection incl. escaped quotes — `YamlSshSyntaxHighlighter.cs:156-215`).
- **Keyword sources**: pulled live from the parser — `ScriptParser.GetKnownTopLevelKeys/GetKnownStepCommands/GetKnownStepOptionKeys` (`YamlSshSyntaxHighlighter.cs:54-60`), so new commands registered in the parser highlight automatically (unlike autocomplete descriptions, see gaps).
- **Palettes**: hardcoded `ColorPalette.Light`/`Dark` (`YamlSshSyntaxHighlighter.cs:257-279`), VS-code-ish colors. Selected at render time by `darkMode` flag.
- **Application**: `ScintillaScriptEditorControl.ApplySyntaxHighlighting` (`UI/ScintillaScriptEditorControl.cs:1700-1739`) resets styling for the whole document then re-applies every span. Styles are allocated per-color on demand (`GetOrCreateStyleForColor`, line 1746) with a fallback to `Style.Default` once index > 255 (line 1754).
- **Settings honored**: `CommandEditorSettings.EnableSyntaxHighlighting` (default true; `Models/AppConfiguration.cs:174`), toggled in SettingsDialog "Command Editor" tab (`SettingsDialog.cs:1808, 2566, 2683`).
- **Reach**: always-on in the main script box and job custom-preset box; refresh coalesced once per message-loop tick via `QueueRefreshAndValidation` (`UI/ScintillaScriptEditorControl.cs:613-639`).

### 2. Inline validation with squiggles (errors + warnings)
- **What**: Debounced background validation of YAML scripts. Runs `ScriptParser.Parse` + `Validate(enforceCanonicalSyntax:true, allowLibraryDefinitions:true)` and maps string error/warning messages to line/column diagnostics (`ScriptEditorValidationService.cs:108-157`). Parser exceptions become a single line-spanning error diagnostic (lines 193-205).
- **Plus YAML hygiene lints** (independent of parser): tab indentation, mixed tabs+spaces, duplicate mapping keys at the same indent level with sequence-item scope reset (`ScriptEditorValidationService.cs:259-354`).
- **Pipeline**: `RequestValidation` cancels the prior CTS (last-edit-wins), `Task.Delay(debounce)`, single in-flight gate via `SemaphoreSlim(1,1)` (lines 26-28, 44-106), raises `DiagnosticsUpdated` on a threadpool thread; the control marshals via `BeginInvoke` (`UI/ScintillaScriptEditorControl.cs:2291-2305`).
- **Rendering**: squiggle indicators — error = `SquiggleLow` index 8, warning = `Squiggle` index 9, themed colors (`ConfigureIndicators` lines 2168-2190); applied in `ApplyDiagnosticsVisuals` (lines 2192-2222) with no-op short-circuit when the diagnostic list is value-equal (`AreDiagnosticsEquivalent`, lines 2260-2289).
- **Gating**: only when `ScriptParser.IsYamlScript(text)` — plain command lists get diagnostics cleared (`UI/ScintillaScriptEditorControl.cs:2238-2258`; `ScriptEditorValidationService.cs:110-113`).
- **Settings honored**: `EnableInlineValidation` (default true), `ValidationDebounceMs` (default 400, clamped 150–2000 via `Min/MaxValidationDebounceMs` consts `Models/AppConfiguration.cs:167-168`), `ShowInlineWarnings`, `EnableYamlHygieneWarnings` (`ApplySettings`, `ScriptEditorValidationService.cs:36-42`). All in SettingsDialog.
- **Manual companion**: right-click → "Validate Script" context menu item (`Form1.Designer.cs:911-916` → `validateScriptToolStripMenuItem_Click` `Form1.cs:5775-5815`) runs the same parser and reports via MessageBox + output append; disabled when a folder is selected (line 5777) and silently does nothing for non-YAML (line 5782).

### 3. Autocomplete (custom popup, context-sensitive)
- **Triggers**: typing (KeyUp filter excludes nav/modifier keys, `UI/ScintillaScriptEditorControl.cs:991-1016`), live re-query while popup is open on TextChanged (lines 603-608), and manual Ctrl+Space (lines 681-687). Gated on `EnableAutocomplete` + `AutocompleteShowOnTyping` settings.
- **Contexts** (`ScriptAutocompleteProvider.GetCompletion`, `ScriptAutocompleteProvider.cs:311-407`):
  - **Interpolation** (`${` or `{{` prefix): merges hardcoded built-ins (`_prompt`, `_output`, `_outputwindow`, `_timestamp`, `_iteration`, `_last_error`, `_host`, `_port`, `_username`, `_password` — lines 92-104), dynamic symbols scraped from the script (vars section, `set:` assignments, `capture:`, `into:` targets + derived `_status/_headers/_count/_avg/_min/_max` suffixes, subroutine params/outputs, `call`'s `out:` mappings — `ExtractDynamicSymbols` lines 640-794), and host-grid column names via the `getHostColumns` callback (Form1 supplies `GetEditorHostColumns` from `dgv_variables`, `Form1.cs:670-677`). Closing `}`/`}}` auto-appended unless already present (`BuildInterpolationInsertText`, `UI/ScintillaScriptEditorControl.cs:1388-1406`).
  - **Step command** after `- ` (line 323-332), or even without the list marker — the provider infers the step indent from prior lines/the `steps:` ancestor and inserts `- ` plus indentation itself (`BuildStepCommandCompletionWithoutListMarker`, lines 470-532); manual Ctrl+Space on a blank column-0 line can "bridge" indent (lines 496-502).
  - **Step option key**: command-scoped option lists from `ScriptParser.GetKnownStepOptionKeysByCommand` / root-level vs nested via `GetKnownStepRootOptionKeysByCommand` (lines 846-872); special-case `respond:` under `send` offers `expect`/`reply` (lines 874-889). Required options are flagged with a "required" detail and sorted first (`RequiredOptionKeysByCommand` lines 180-217, `FilterOptionKeys` lines 1107-1129). Suppressed after a blank-line separator (lines 374-375, 1187-1194).
  - **Option value**: enum-like values, command-scoped first then global (`GetEnumLikeOptionValues[ByCommand]`, lines 346-371).
  - **Top-level key**: at indent 0, with auto-suggest suppressed once `vars:`/`steps:` already exist unless the user types (lines 387-404, 409-448).
- **Popup UI**: a borderless `Panel` + owner-drawn non-focusable `ListBox` hosted *inside* the UserControl (not the native Scintilla AutoC) — `UI/ScintillaScriptEditorControl.cs:176-206`. Kind-colored 8px square glyph + right-aligned grey detail text that hides when space is tight (`CompletionList_DrawItem` lines 1447-1515). Max 8 visible rows, width 350 clamped to client area (lines 67, 1234-1247); flips above the caret when no room below (lines 1249-1275); repositions on scroll, hides on caret leave/viewport exit (lines 715-740, 1277-1295).
- **Keyboard**: Up/Down/PageUp/PageDown navigate; Enter/Tab accept; Esc dismisses; Shift+Tab dismisses and falls through to outdent; Left/Right/Home/End dismiss (lines 846-942, 689-693). Typing `:` accepts the selected key-context item (`Editor_KeyPress` lines 748-758). Accepting a key-context item appends `": "` unless a colon follows (lines 1365-1386). Enter-accept of a StepCommand/TopLevelKey chains a Smart Enter (lines 911-931).
- **Dismissal robustness**: an `Application.AddMessageFilter` watches all mouse-down messages app-wide and dismisses unless the click lands inside the editor hierarchy (`CompletionDismissMessageFilter` lines 2448-2482, WeakReference owner); LostFocus dismissal is deferred and re-checked with `GetFocus()` (lines 760-788).
- **Accept correctness**: re-queries the provider at accept time to get a fresh replace-range, falling back to the cached one (lines 1321-1363); wrapped in a single undo action.

### 4. Smart editing (indent, Smart Enter, comment toggling)
- **Tab / Shift+Tab**: block indent/outdent of all selected lines preserving terminators and recomputing selection (`EditorTextUtilities.ApplyIndentation`, `EditorTextUtilities.cs:41-92`); honors `IndentSize` (clamped 1-8) and `UseSpacesForTab`. Handled before anything else when `AcceptsTab` (`HandleIndentationKeys`, `UI/ScintillaScriptEditorControl.cs:944-963`).
- **Smart Enter** (`ApplySmartEnter`, `EditorTextUtilities.cs:94-155`): continues `- ` lists, indents after a `key:` line (with a hardcoded `NestedStepOptionKeys` allowlist deciding which step options nest, lines 21-39), preserves blank lines between steps (`PreserveBlankLineBetweenSteps` setting), and dedents back to step level after an empty step payload (lines 265-304). Gated on `EnableSmartEnter`.
- **Ctrl+Enter**: inserts a sibling step `- ` at the resolved step indentation (`ApplySiblingStepEnter`, `EditorTextUtilities.cs:339-367`; dispatch `UI/ScintillaScriptEditorControl.cs:965-989`).
- **Comment/Uncomment Selected Lines**: context-menu items (`Form1.Designer.cs:885-897` → `Form1.cs:5827-5835`) → `CommentSelectedLines`/`UncommentSelectedLines` (`UI/ScintillaScriptEditorControl.cs:406-443`), inserting/removing `#` after leading indent via `TransformSelectedLines` (lines 1579-1631, single undo action, selection restored).
- **Edits applied minimally**: full-text edits are diffed into a single prefix/suffix replacement so undo stays granular and the view doesn't jump (`BuildTextReplacement` lines 2531-2566, `ApplyTextEdit` lines 1544-1577).

### 5. Code folding
- Indentation-derived fold levels recomputed on every visual refresh when enabled (`UpdateFoldLevels`, `UI/ScintillaScriptEditorControl.cs:2001-2053`): blank lines inherit the previous indent with `FoldLevelFlags.White`; a line is a header when the next content line is deeper. Tab width for indent columns uses `IndentSize` (line 2073).
- Custom anti-aliased chevron markers drawn into RGBA bitmaps (lines 1935-1991); fold margin (index 2, 14px) click-toggles headers (`Editor_MarginClick` lines 790-808). `AutomaticFold.Show | Click` when enabled (line 1916-1918).
- Setting: `EnableCodeFolding` — **default false** (`Models/AppConfiguration.cs:192`).

### 6. Diagnostics & variable hover tooltips
- Dwell-based (Scintilla `DwellStart`, 250ms hardcoded `MouseDwellTime`, line 135). Priority: diagnostic message at the hovered line/column first (`EnableDiagnosticTooltips`), then variable inspector (`EnableVariableInspectorTooltips`) (`Editor_DwellStart` lines 810-844).
- Variable inspector resolves `${name}` via a Form1-supplied resolver chain: built-ins (placeholders, plus **live host-grid values for `_host`/`_port`/`_username`/`_password`**, `Form1.cs:727-745`), then a full re-parse of the script's `vars:` (`Form1.cs:690-707`), then active environment variables (`Form1.cs:709-713`), then "[declared in script]" for dynamic symbols. `{{column}}` resolves from the selected (or first) host row (`Form1.cs:747-773`). Display formats at `UI/ScintillaScriptEditorControl.cs:2330-2346`; tooltip shown 3000ms (line 2366).

### 7. Visual options & theming
- Current-line highlight (alpha 96), indent guides (`LookBoth`), whitespace markers, long-line edge guide at configurable column (80–200, default 120), brace matching for `{}[]()` at caret with match/mismatch styles (`ConfigureVisualOptions` + helpers, lines 1849-1933, 2107-2166). Each behind its own `CommandEditorSettings` flag; indent guides / whitespace / long-line / folding default **off** (`Models/AppConfiguration.cs:188-192`).
- Line-number margin auto-widens with digit count (`UpdateLineNumberMarginWidth` lines 1836-1847) plus a 10px color spacer margin.
- Full dark/light theme: editor colors, completion popup colors, caret, selection, 1px border in dark mode (`ApplyTheme` lines 547-593); Win32 dark scrollbars via `SetWindowTheme("DarkMode_Explorer")` + undocumented uxtheme ordinal #133 `AllowDarkModeForWindow`, defensively try/caught (lines 2375-2436, 2484-2514). Note this control does **not** use `DialogTheme` — it owns its palette.
- Execution cursor override: while a script runs, the editor cursor is forced to wait via raw `SCI_SETCURSOR` messages and restored after (`ExecutionCursorAwareScintilla` lines 17-48; called from `Form1.cs:13550`).

### 8. Settings plumbing & performance guards
- `CommandEditorSettings` (21 properties, `Models/AppConfiguration.cs:165-207`) is cloned+normalized on apply (`ApplyCommandEditorSettings`, `UI/ScintillaScriptEditorControl.cs:529-545`); SettingsDialog's "Command Editor" tab edits all of them with clamped numeric inputs; Form1 re-applies on settings save (`Form1.cs:5729`).
- Performance guards: per-tick coalescing of refresh+validation (`QueueRefreshAndValidation` lines 613-639), debounce + cancellation + single-flight in the validation service, minimal-diff text replacement, diagnostics-equality short-circuit, completion item-list reuse when unchanged (`AreCompletionItemsEqual` lines 1139-1155).
- A real perf regression test exists: `SSH_Helper.Tests/UI/ScintillaScriptEditorPerformanceTests.cs` asserts p95 keystroke ≤ 50ms, completion ≤ 120ms, EOF-Enter ≤ 100ms on a **500-line** script (lines 41-58).
- Native bootstrap: `ScintillaNativeBootstrap.ConfigureSatelliteDirectory` (called from `Program.cs:58`) prefers `runtimes/<rid>/native` on disk, else extracts embedded win-x64 Scintilla/Lexilla DLLs to `%LocalAppData%\SSH_Helper\scintilla-native\<pkg-version>\win-x64` with temp-dir fallback; throws if no writable root (`Utilities/ScintillaNativeBootstrap.cs:56-87`).

### 9. Misc editor surface
- `IScriptEditor` interface keeps Form1 decoupled (selection, caret line/column for the status bar, clipboard ops, diagnostics injection).
- Ctrl+S in the editor saves the current preset (`Form1.cs:14454-14460`). Word wrap follows `WindowState.CodeEditorWordWrap`.
- Read-only text swap preserves the ReadOnly flag (`SetEditorTextPreservingReadOnly` lines 641-660).
- Test coverage is broad for the service layer: `SSH_Helper.Tests/Editor/` has dedicated suites for provider (~60 cases), validation service, highlighter, subroutine completion; UI behavior in `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs`.

---

## Integration points

- **ScriptParser is the single keyword/metadata source** for highlighter + autocomplete option/value catalogs (`ScriptParser.cs:335-389`; 43 step commands at lines 24-69, 14 top-level keys at lines 70-86). Validation runs the real parser, so editor diagnostics match execution-time errors.
- **Form1 wiring** (`Form1.cs:656-661`): provider with live host-column callback, shared highlighter, owned validation service (disposed at `Form1.cs:338`), tooltip resolvers reading `dgv_variables` + `EnvironmentService.GetActiveEnvironmentVariables()`. Editor↔execution: `SetExecutionCursorOverride` (`Form1.cs:13550`).
- **JobEditorDialog** duplicates the same wiring for custom-preset jobs with its own instances (`JobEditorDialog.cs:61-63, 175, 585-591`; service disposed at 2149).
- **SettingsDialog** ↔ `AppConfiguration.CommandEditor` ↔ `ConfigurationService` persistence; Form1 pushes updated settings into the control on save.
- **Events**: `ScriptEditorValidationService.DiagnosticsUpdated` (threadpool → BeginInvoke marshal in control). The control surfaces standard WinForms `TextChanged`/`KeyDown`/`Click` upward for Form1's preset dirty-tracking.
- **EnvironmentService**: tooltip values come from the active environment, so hover output changes with environment switches.
- **Program startup**: `ScintillaNativeBootstrap` must run before any editor instantiation (the control fails to create otherwise).

---

## Observed gaps & quirks

### Missing affordances (vs. professional editor expectations)
1. **No find/replace inside the script editor.** Ctrl+F (`Form1.cs:14462-14464`) opens `FindDialog`, but it searches **`txtOutput` only** — seed, anchor, match list and highlight all target the output box (`Form1.cs:14484-14619`). There is zero search, replace, or go-to-line for the script being edited. F3/Shift+F3 likewise navigate output matches only.
2. **No error list / problems panel and no margin markers.** Diagnostics exist only as squiggles + hover tooltips; nothing enumerates them, no click-to-jump, no gutter error icons (indicator-only rendering, `UI/ScintillaScriptEditorControl.cs:2192-2222`).
3. **No completion documentation/snippets.** Accepting a command inserts `name: ` only — required options (already known via `RequiredOptionKeysByCommand`, `ScriptAutocompleteProvider.cs:180-217`) are not scaffolded as a snippet body; no signature/hover help for commands (hover is variables+diagnostics only).
4. **Validation silently absent for non-YAML presets** (`ScriptEditorValidationService.cs:110-113`; `UI/ScintillaScriptEditorControl.cs:2247-2257`) and the manual "Validate Script" menu item silently no-ops for them too (`Form1.cs:5782-5783`) — no feedback that nothing was checked.
5. **Multi-line diagnostics unsupported**: `EditorDiagnostic` is a single-line span; `Contains` is exact-line (`EditorDiagnostic.cs:18-24`).

### Drift-prone hand-maintained duplicates
6. **`TopLevelKeyDescriptions` already drifted**: parser knows 14 top-level keys including `preconnect` (`ScriptParser.cs:70-86`) but the description map has 13 — `preconnect` missing (`ScriptAutocompleteProvider.cs:163-178`); it appears in the list with no detail text. `CommandDescriptions` (43) currently matches but is maintained by hand, as are `RequiredOptionKeysByCommand`, `BuiltInSymbols`, and `EditorTextUtilities.NestedStepOptionKeys` (`EditorTextUtilities.cs:21-39`) — adding a new container command requires touching all of them or Smart Enter / required-hints quietly misbehave.
7. **`IntoDerivedSuffixes` over-suggests**: every `into:` target unconditionally spawns `_status/_headers/_count/_avg/_min/_max` symbols (`ScriptAutocompleteProvider.cs:106-114, 782-790`) even for commands that never produce them (e.g. `readfile ... into: x` suggests `x_headers`).

### Security / data exposure
8. **Plaintext password in hover tooltip**: dwelling on `${_password}` or `{{password}}` shows the host grid's password cell verbatim (`Form1.cs:740` + `UI/ScintillaScriptEditorControl.cs:2333-2346`). Same for any sensitive custom column. Matches the audit's "secret-echo" theme; no masking.

### Performance
9. **Full-document re-style on every change**: `ApplySyntaxHighlighting` passes all lines (`Enumerable.Range(0, Lines.Count)`, line 1717) even though `BuildHighlights` accepts changed-line indices — the incremental API exists but is unused. `ConfigureBaseStyles` also runs `StyleClearAll` and rebuilds the per-color style cache each refresh (lines 1707, 1810-1811). Fold levels likewise recomputed over all lines per refresh (line 2001). The perf test only covers 500 lines; multi-thousand-line scripts are unbudgeted.
10. **Hover re-parses the whole script** (`Form1.cs:694-695`) and `ExtractDynamicSymbols` rescans all lines (`Form1.cs:717`) on each dwell — fine for small scripts, O(n) parse per hover otherwise.
11. **Popup-open typing does a full provider re-query synchronously on every keystroke** (`Editor_TextChanged` lines 603-608), each of which can call `GetInterpolationSymbols` → full text scan.

### Heuristic correctness quirks
12. **Inconsistent tab-width assumptions**: hygiene lint and autocomplete count a tab as 2 columns hardcoded (`ScriptEditorValidationService.cs:361`, `ScriptAutocompleteProvider.cs:1226-1230`, `EditorTextUtilities.cs:590-594`) while fold logic uses configurable `IndentSize` (`UI/ScintillaScriptEditorControl.cs:2073`). With `IndentSize` ≠ 2 and tab-indented files, context detection and folding disagree.
13. **Duplicate-key lint is case-insensitive** (`ScriptEditorValidationService.cs:337`) though YAML keys are case-sensitive — `Name:`/`name:` at one level is flagged as a duplicate warning.
14. **Diagnostic line/column extraction is string-scraping**: line numbers regexed from messages ("Line N:" or loose "line N", `ScriptEditorValidationService.cs:9-13, 207-222`); anything without one lands on line 1 (line 164). Token underline targets the **first** case-insensitive occurrence in the line (line 172) — can underline the wrong instance.
15. **Highlighter has no cross-line state**: block scalars (`command: |` bodies) are styled as YAML, so numbers/booleans/quoted strings inside raw CLI text get colored; an unterminated quote on a line makes the rest comment-blind for that line only.
16. **Native Scintilla AutoC config is dead code**: `AutoCSeparator/AutoCIgnoreCase/...` set at `UI/ScintillaScriptEditorControl.cs:141-147` and `AutocompleteList*` theme colors at 556-559/573-576, but `AutoCShow` is never called — the custom popup replaced it and the leftovers remain.
17. **Completion popup is clipped to the control bounds** (Panel child, clamped to `ClientSize`, lines 1242-1244, 1271-1273) — in a very short editor pane the list shrinks toward one row instead of overlaying neighboring UI.
18. **`ValidateNowAsync` is public API used only by tests** (`ScriptEditorValidationService.cs:60-63`); Form1's manual Validate menu re-instantiates its own parser instead of reusing the service (`Form1.cs:5785`), so manual vs. inline validation can diverge in flags (`allowLibraryDefinitions` is true inline, default in manual).
19. **Hardcoded UX constants**: dwell 250ms (line 135), tooltip 3000ms (line 2366), popup width 350 / 8 rows (lines 67, 1238), completion list height 180 (line 180) — none configurable, none DPI-scaled beyond font-based ItemHeight.
20. **`uxtheme.dll` ordinal #133** for dark scrollbars (line 2489) is an undocumented API — guarded by catch blocks but inherently fragile across Windows builds.

### Maturity assessment
The service layer (provider/validator/highlighter/text-utils) is mature, pure, and heavily unit-tested (`SSH_Helper.Tests/Editor/*`, plus a perf budget test and STA UI tests). The control is feature-rich and careful about focus/dismissal/undo edge cases. The weakest spots are the missing in-editor find/replace, the hand-maintained metadata duplicated from the parser, the secret-echoing hover, and the all-lines re-style strategy that relies on scripts staying small.
