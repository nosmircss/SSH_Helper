# Scintilla Script Editor Parity Checklist

## Reference Performance Profile
- Date: `2026-02-08 17:15:13 -06:00`
- Commit: `d892b82`
- OS: `Microsoft Windows NT 10.0.26200.0`
- CPU: `Intel64 Family 6 Model 183 Stepping 1, GenuineIntel`
- Logical cores: `24`
- Memory: `63.76 GB` physical (`17.72 GB` available at capture)
- SDK: `.NET 9.0.309`
- Target runtime: `net8.0-windows`
- Build mode for verification: project build + test runs with isolated output (`-p:OutDir=bin\verify_scintilla*`)
- Script size profile: generated YAML with `500` step lines (`steps:` + `500` `send` entries)

## Latency Observations vs Budgets
Measured by `SSH_Helper.Tests.UI.ScintillaScriptEditorPerformanceTests.ReferenceProfile_MeetsLatencyBudgets`.

| Metric | Budget | Observed p95 | Status |
|---|---:|---:|---|
| Keystroke to visible text | `<= 50 ms` | `8.88 ms` | PASS |
| Completion update latency | `<= 120 ms` | `7.39 ms` | PASS |
| EOF `Enter` caret reveal | `<= 100 ms` | `0.67 ms` | PASS |

Command used:
`dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~ScintillaScriptEditorPerformanceTests.ReferenceProfile_MeetsLatencyBudgets --logger "console;verbosity=detailed" -p:MSBuildEnableWorkloadResolver=false -p:OutDir=bin\verify_scintilla_tests\`

## Familiar Interaction Contract

| Item | Status | Evidence |
|---|---|---|
| Scintilla host is used for Commands editor | PASS | `Form1.Designer.cs` now instantiates `ScintillaScriptEditorControl` |
| Parser-driven autocomplete (no hard-coded grammar) | PASS | `ScintillaScriptEditorControl` uses `ScriptAutocompleteProvider.GetCompletion(...)` |
| Completion commit/dismiss contract (`Enter`/`Tab` commit, `Escape` dismiss) | PASS | `ScintillaScriptEditorControlTests.CompletionNavigation_EnterCommitsAndEscapeDismissesWithoutMutation` |
| Click-to-reposition closes completion | PASS | `ScintillaScriptEditorControlTests.CompletionPopup_MouseUpClosesSuggestions` |
| Typing remains responsive while completion/diagnostics enabled | PASS | Performance test with completion + validation enabled passes budgets |
| Smart-enter and indentation edits are intuitive for undo/redo | PASS | `ScintillaScriptEditorControlTests.SmartEnter_IsSingleUndoableEditUnit` |
| Scroll-past-end enabled | PASS | `ScintillaScriptEditorControlTests.ScrollPastEnd_IsEnabledByDefault` (`EndAtLastLine=false`) |
| EOF enter reveals caret line | PASS | Performance test `eof-enter` budget pass + `HandleSmartEnter` path uses `ScrollCaret()` |
| Caret line/column reporting parity | PASS | `ScintillaScriptEditorControlTests.GetCaretPosition_ReturnsOneBasedLineAndColumn` |
| Diagnostics rendering + warning toggle | PASS | `ScintillaScriptEditorControlTests.SetDiagnostics_RespectsShowInlineWarningsSetting` |
| Existing command-editor settings continue to apply | PASS | `ApplyCommandEditorSettings(...)` maps indentation/tab/autocomplete/validation/smart-enter behaviors |
| Context-menu actions, line/column updates, `Ctrl+S` behavior preserved | PASS | Existing `Form1` handlers unchanged; editor forwards click/key/mouse events |

## Smoke Verification Summary
- Typing, completion, diagnostics, and smart-enter behaviors were validated via WinForms STA smoke tests in:
  - `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs`
  - `SSH_Helper.Tests/UI/ScintillaScriptEditorPerformanceTests.cs`
- Theme/font/settings integration validated by build + runtime wiring and existing editor/settings tests.

## Build and Test Validation
- `dotnet build SSH_Helper.csproj -p:MSBuildEnableWorkloadResolver=false -p:OutDir=bin\verify_scintilla\` -> PASS
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SSH_Helper.Tests.Editor|FullyQualifiedName~ScintillaScriptEditor" -p:MSBuildEnableWorkloadResolver=false -p:OutDir=bin\verify_scintilla_tests\` -> PASS (`29` tests)
