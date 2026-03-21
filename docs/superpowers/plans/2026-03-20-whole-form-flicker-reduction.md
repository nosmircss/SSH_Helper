# Whole-Form Flicker Reduction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce visible whole-form flicker in two phases: first when SSH Helper regains focus after callback windows close, then during broader day-to-day main-form interactions.

**Architecture:** Introduce buffered/background-erase-aware container controls for the main `Form1` surface and apply them to the top-level split/panel hierarchy that repaints during callback regain-focus. After that activation path is stable, narrow the remaining general-interaction redraw churn in `Form1`, `HostGridRestoreBatcher`, and history/output-related controls by batching updates and replacing broad repaint calls with smaller invalidation/layout scopes.

**Tech Stack:** C# 12, .NET 8 WinForms, xUnit, FluentAssertions, existing browser-callback UI host/tests, existing UI infrastructure in `Form1`

---

### Task 1: Update the task tracker for phased execution

**Files:**
- Modify: `tasks/todo.md`

- [ ] Add a tracked execution item for phase 1 callback regain-focus flicker reduction.
- [ ] Add a tracked execution item for phase 2 general-interaction flicker reduction.
- [ ] Reserve review sections for both phases so verification evidence has a stable home.

### Task 2: Add failing phase-1 infrastructure tests

**Files:**
- Create: `SSH_Helper.Tests/UI/BufferedContainerControlTests.cs`
- Modify: `SSH_Helper.Tests/UI/BorderlessTabControlTests.cs`

- [ ] Write a failing test for a new buffered panel control that expects `OptimizedDoubleBuffer`, `AllPaintingInWmPaint`, and `ResizeRedraw` to be enabled.
- [ ] Write a failing test for a new buffered split-container control that expects the same buffering styles and background-erase suppression behavior when applicable.
- [ ] Keep the tests handle-based only; do not show a visible top-level form.
- [ ] Run the focused control test slice and confirm it fails before implementation.

Run:
`dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BufferedContainerControlTests|FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase1-red\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase1-red\obj\`

Expected:
- FAIL because the new buffered container controls do not exist yet.

### Task 3: Implement phase-1 buffered container infrastructure

**Files:**
- Create: `UI/BufferedPanel.cs`
- Create: `UI/BufferedSplitContainer.cs`
- Modify: `Form1.Designer.cs`

- [ ] Implement `BufferedPanel` as a small WinForms panel wrapper that enables buffered painting styles and suppresses unnecessary background erase only when it fully owns its repaint surface.
- [ ] Implement `BufferedSplitContainer` as the split-container equivalent, keeping splitter behavior unchanged while turning on buffered painting styles.
- [ ] Replace the top-level `Form1` surface seams with buffered types in `Form1.Designer.cs`.
- [ ] Start with the high-coverage activation surfaces:
- [ ] `mainSplitContainer`
- [ ] `topSplitContainer`
- [ ] `commandSplitContainer`
- [ ] `outputSplitContainer`
- [ ] `historySplitContainer`
- [ ] `hostsPanel`
- [ ] `commandPanel`
- [ ] `presetsPanel`
- [ ] `scriptPanel`
- [ ] `outputPanel`
- [ ] `outputRightPanel`
- [ ] `historyPanel`
- [ ] `hostListPanel`
- [ ] Apply the same buffered panel type to header/footer panels only if activation repaint still leaves visible seams after the top-level swap.

### Task 4: Verify phase 1 and lock the callback regain-focus result

**Files:**
- Modify: `tasks/todo.md`

- [ ] Run the focused buffered-control/UI suite.
- [ ] Run the browser-callback host/focus regression slice.
- [ ] Run the broader browser-callback/runtime regression slice.
- [ ] Run `dotnet build .\SSH_Helper.sln -nologo`.
- [ ] Manually run the two-callback preset in SSH Helper and close both callback windows to confirm SSH Helper regains focus without broad client-area flash.
- [ ] Record the phase-1 outcome under the matching task-review section in `tasks/todo.md`.

Run:
`dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BufferedContainerControlTests|FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase1-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase1-ui\obj\`

Run:
`dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase1-callback-focused\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase1-callback-focused\obj\`

Run:
`dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallback|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase1-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase1-regression\obj\`

Run:
`dotnet build .\SSH_Helper.sln -nologo`

### Task 5: Add failing phase-2 interaction-redraw tests

**Files:**
- Modify: `SSH_Helper.Tests/UI/HostGridRestoreBatcherTests.cs`
- Create: `SSH_Helper.Tests/UI/HistoryListBoxTests.cs`
- Modify: `SSH_Helper.Tests/UI/ApplyFontSettingsTests.cs`

- [ ] Extend `HostGridRestoreBatcherTests` to prove repeated host-grid refresh requests during a non-restore UI mutation scope collapse into a single flush.
- [ ] Add `HistoryListBoxTests` proving width-stable size/font changes do not trigger unnecessary full refresh/invalidate loops.
- [ ] Add or adjust a main-form-adjacent UI test only if a new helper seam is introduced for broad repaint suppression.
- [ ] Run the focused phase-2 test slice and confirm it fails before implementation.

Run:
`dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostGridRestoreBatcherTests|FullyQualifiedName~HistoryListBoxTests|FullyQualifiedName~ApplyFontSettingsTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase2-red\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase2-red\obj\`

Expected:
- FAIL because the broader UI-mutation batching and history-list repaint guards are not implemented yet.

### Task 6: Implement phase-2 redraw narrowing

**Files:**
- Modify: `UI/HostGridRestoreBatcher.cs`
- Modify: `UI/HistoryListBox.cs`
- Modify: `Form1.cs`
- Modify: `Form1.Designer.cs` (only if a small helper hook is needed for the main-form surface)

- [ ] Extend `HostGridRestoreBatcher` so it can batch host-grid repaint-related requests during non-startup UI mutation scopes, not just restore scopes.
- [ ] Use that batching in the highest-churn `Form1` host-grid mutation paths before touching less frequent repaint sites.
- [ ] Replace `dgv_variables.Refresh()` in `DeleteSelectedCells()` with a narrower invalidation/update path.
- [ ] Revisit `ApplyTheme()` and remove the unconditional whole-form `Refresh()` if the same visual result can be achieved with buffered layout plus narrower invalidation.
- [ ] Revisit `scriptHeaderPanel.Invalidate(true)` and similar broad invalidations in `Form1.cs`, replacing them with narrower invalidation only where the dirty region is known.
- [ ] Update `HistoryListBox` so width-stable changes avoid redundant full refresh work.
- [ ] Keep this task scoped to main-form redraw churn; do not expand into unrelated dialog/theme cleanup.

### Task 7: Verify phase 2 and close the loop

**Files:**
- Modify: `tasks/todo.md`

- [ ] Run the focused phase-2 UI tests.
- [ ] Rerun the phase-1 buffered/callback slices to confirm no regression.
- [ ] Run `dotnet build .\SSH_Helper.sln -nologo`.
- [ ] Manually verify ordinary interactions:
- [ ] resize the main window
- [ ] switch presets and favorites
- [ ] edit/delete cells in the host grid
- [ ] inspect history/output redraw behavior
- [ ] Record the phase-2 outcome under the matching task-review section in `tasks/todo.md`.

Run:
`dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostGridRestoreBatcherTests|FullyQualifiedName~HistoryListBoxTests|FullyQualifiedName~ApplyFontSettingsTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase2-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase2-ui\obj\`

Run:
`dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BufferedContainerControlTests|FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase2-callback-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase2-callback-regression\obj\`

Run:
`dotnet build .\SSH_Helper.sln -nologo`
