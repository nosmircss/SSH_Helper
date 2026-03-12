# Lessons

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
