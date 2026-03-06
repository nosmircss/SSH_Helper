# Lessons

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
