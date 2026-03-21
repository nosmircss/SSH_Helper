# Lessons

## 2026-03-20
- When a user points out that a cell-level WinForms status cue disappears under selection, I must look for a non-selected-owned surface such as the row header instead of continuing to fight the selected-cell paint path.
- When I smooth a WinForms add-preset tree mutation, I must verify the new selection is fully visible in the actual viewport instead of only preserving `TopNode`; a preserved anchor is not enough if the inserted row ends up clipped below the fold.
- When I create a new preset in `Form1`, I must route the post-create load through the same preset-loading path as ordinary selection changes; hand-populating the editor can skip required environment-restore logic.
- When I fix full-visibility for one preset-tree insertion path in WinForms, I must audit the matching undo/restore path in the same pass; undelete uses the same viewport-sensitive selection pattern and can regress independently if I only patch add.
- When a WinForms tab-strip flicker survives buffered overlays and `WM_ERASEBKGND` fixes, I must remove the native tab header from the visible surface entirely; as long as native `TabControl` chrome is still what the user sees, repaint flashes can survive any seam patch around it.
- When a user still sees WinForms flicker specifically in the tab-header gap beside the last tab, I must inspect `WM_ERASEBKGND` for the tab control itself; buffering nearby panels is not enough if the header gap stays unpainted until the post-paint seam patch.
- When a WinForms dark-mode flicker survives earlier header buffering, I must inspect runtime-created panels and `UseVisualStyleBackColor` on the tab pages themselves; raw child panels and themed tab-page background erase can still flash even when the tab strip overlay looks fixed.
- When a WinForms tab control looks correct in an isolated managed-paint test but still shows native seams at runtime, I must treat that as a paint-order bug and look for a buffered post-`WM_PAINT` overlay path instead of continuing to tune only the managed `Paint` rectangles.
- When a user still sees WinForms tab-strip flicker after I remove an extra managed handler, I must inspect any remaining direct `Graphics.FromHwnd` or post-`WM_PAINT` overdraw next; buffered event wiring alone does not eliminate flicker if a control still patches pixels after native paint.
- When a custom WinForms tab control already owns its border/header overlay in `WndProc`, I must not also layer a form-level `Paint` handler onto that same control; the duplicate overlay can survive broader buffering fixes as a small residual flicker around the tab strip.
- When I verify WinForms repaint batching, I should not assert absolute `Invalidated` counts across a font change unless I control the framework noise; the safer contract is to compare the counts before and after the explicit follow-up call.
- When I add WinForms handle/paint regression tests, I must avoid showing a real top-level `Form` unless visibility is part of the behavior under test; otherwise the test can leak a blank desktop window during runs.
- When a user reports multi-window callback focus still falls through to another app, I must verify the modeless close path explicitly restores activation to the main SSH Helper form; fixing owner selection and modal flicker alone is not enough.
- When I keep a modal browser surface open after success, I must also update its affordances and completion styling; leaving the footer button as `Cancel` and the embedded HTML unthemed creates a misleading, half-finished UX.
- When a user names an option by the visible behavior they expect, I must verify the entire user-facing surface, not just one internal layer; `auto_close_browser: false` was incomplete when the page stayed open but the host WebView2 window still auto-closed.
- When I hand off a self-contained `browser_callback_capture` preset, I must state explicitly that the command itself starts the temporary localhost listener before opening the browser; users should not have to infer that from the `start_url` alone.
- When I change WinForms/browser callback focus restoration, I must verify the real interactive foreground result, not just native-call ordering in a unit test; Windows activation behavior can still regress even when the API sequence looks stronger on paper.
- When I wrap Windows P/Invoke calls in helper names like `NativeIsIconic`, I must explicitly set `EntryPoint` (or keep the extern name exact) and add a test for the import mapping; mocked focus tests will not catch missing exports and the bug will surface only at runtime.
- When I add a new script option that changes live browser-launch behavior, I must verify the end-to-end preset execution path in the actual app, not just parser/command unit tests; otherwise the UI can still behave like the old path and I will miss it until the user tries it manually.
- When I add a buffered WinForms container wrapper, I must make background-erase suppression conditional on a clearly opaque, fully-owned surface; unconditional `WM_ERASEBKGND` suppression is too broad for a reusable control.
- When I verify WinForms/browser-callback repaint behavior, I must not run multiple UI-heavy `dotnet test` processes in parallel; shared activation state and visible-form cleanup can create false failures that disappear on a serial rerun.
- When I keep a browser callback window modeless to avoid modal close flicker, I must not disable the entire owner form as a substitute lock; that broad disabled-state repaint can blank labels and reintroduce whole-form flicker during launch.

## 2026-03-15
- When a user narrows a popup-ownership cleanup to allow some ownerless dialogs, I must preserve explicit exceptions for startup/global flows instead of force-owning every modal call site.
- When I add or adjust a modeless dialog launched from `Form1`, I must verify the close path explicitly restores activation to the main window instead of assuming WinForms ownership will do it automatically.
- When I restore owner activation from a modeless dialog close path, I must verify the timing on the UI thread; an unnecessary deferred `BeginInvoke` can cause visible focus flicker by letting another app activate briefly first.
- When I reason about WinForms `TreeView` display order, I must not use `TreeNode.IsVisible` as a proxy for logical tree visibility; it is viewport-dependent and can break adjacent-node selection rules.
- When I preserve tree expansion state during WinForms reselection, I must gate on collapsed ancestors, not on `TreeNode.IsVisible`; off-screen root nodes are still valid selection targets.
- When I rebuild a WinForms `TreeView` to preserve selection, I must also preserve `TopNode` while redraw is suspended or users will see a jump-to-top/jump-back flicker.
- When deleting a single item from a WinForms `TreeView`, I should prefer in-place node removal over clearing and rebuilding the whole tree; full rebuilds are prone to scroll-state regressions and visible flicker.

## 2026-03-13
- When a user narrows status-bar progress behavior, I must encode the exact simplification they asked for instead of preserving extra host/preset detail from the earlier plan.
- When I drive a WinForms status bar from `Progress<T>`, I must guard late UI-thread callbacks with a run token and confirm the exact visibility threshold so 1x1 runs do not show a pointless progress bar.

## 2026-03-12
- When a user asks for clearer QA preset prerequisites or execution expectations, I must encode those details directly in each preset `description` instead of assuming the preset name or folder is enough context.
- When I expand the scripting runtime to accept new expression forms, I must update `ScriptDependencyAnalyzer` in the same pass or the missing-column preflight will drift and flag expression text as fake grid columns.
- When a scripting surface accepts plain scalar text as a valid literal value, I must align missing-column analysis with the runtime resolver and avoid treating arbitrary words in that scalar as expression tokens.
- When a user cancels a script-driven file picker and asks for the script to stop, I must model that as a real script cancellation or exit path, not a suppressible step error routed through `on_error`.

## 2026-03-09
- When a user narrows a follow-up implementation to a specific subset of review findings, I must implement only that approved scope and drop adjacent enhancements I suggested on my own.
- When I swap a single-row WinForms input for a multi-line help label, I must reflow the rows beneath it and add a layout regression test instead of assuming the old fixed `y` offsets still hold.

## 2026-03-08
- When scheduler lifecycle notifications share the same pane as live host output, I should remove or relocate them instead of mixing them into the merged output stream.
- When a user reports scheduler history flooding with the same repeated failure, I should collapse consecutive identical failures in persisted history instead of only tweaking the grid presentation.
- When a user says a scheduler safeguard is confusing, I should prefer a save-time explanation over a hidden blocked-state flag or secondary recovery workflow.
- When I add a new save-time warning to an existing preset workflow, I must preserve the existing diff/context the user relies on instead of replacing it with a narrower confirmation dialog.

## 2026-03-06
- If I run verification with custom output paths inside the repo, I must either clean those generated folders or exclude them from compile globs before handing off.
- Before saying testing is complete, I must run at least one normal `dotnet build` for the touched project, not only a workaround-based test command.
- If verification required special build flags, I must say that explicitly and explain whether the normal build path also passes.
- When a user corrects UI indicator behavior, I must capture the exact visibility rule instead of assuming the indicator should always be visible.
- When I add a nested context-menu command in WinForms, I should verify primary click behavior explicitly instead of assuming submenu expansion works acceptably by default.
- When a user reports a WinForms menu still does nothing after a UI patch, I should replace the fragile interaction model instead of iterating on the same submenu assumption.
- When I launch follow-up UI from a WinForms context-menu command, I should use a regular dialog or another non-menu surface instead of opening a second `ContextMenuStrip` inside the active menu lifecycle.
- When I show inherited configuration in a details pane, I should include the source scope and refresh the current selection when related environment state changes, otherwise different folders can look unchanged.
- When a WinForms TreeView uses custom click handling for full-row selection, I should not rely on `AfterSelect` alone to refresh detail panes; I need a click-path fallback for folder nodes.
- When a read-only custom editor is reused as a details pane, I must ensure programmatic `Text` and `Clear()` operations temporarily bypass read-only or subsequent detail refreshes will silently fail.
- When a manual switch updates both the active environment and the base environment, I must refresh folder-detail UI after the final base-environment write, not only from the earlier environment-changed event.
- When a user corrects autocomplete scope, I should encode the exact context boundary they asked for, not flatten it into a broader suppression rule; header-area and post-section behavior may need different completion rules.
- When a user says autocomplete still leaks after a scope fix, I must verify popup lifecycle on caret movement as well as provider filtering; stale visible suggestions can survive even when fresh completion results are already correct.
- When a user corrects autocomplete scope again, I must update the manual invocation rules too; preserving `Ctrl+Space` behavior from an earlier assumption can still violate the real boundary they want.
- When a user reports Tab behavior on a blank editor line, I must verify trailing-newline indexing specifically; helpers that enumerate line starts often collapse the final blank line back onto the previous content line.
- When adding a preset dirty indicator, I must place it in the active editor header the user sees while typing, not only in the presets tree pane.
- When drafting new OpenSpec changes around already-implemented work, I should not anchor the proposal set to an older active change unless the user explicitly wants to keep that dependency; if the user wants fresh proposals, I should frame them as standalone changes.
- When a user says parity should include the look-and-feel as well as behavior, I should extend the spec/tasks to include visual parity explicitly instead of assuming behavioral parity is enough.
