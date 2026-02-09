# Tasks: Replace script editor with Scintilla5.NET

## 1. Dependency and host control
- [x] 1.1 Add `Scintilla5.NET` to `SSH_Helper.csproj`.
- [x] 1.2 Add `UI/ScintillaScriptEditorControl.cs` implementing `IScriptEditor`.
- [x] 1.3 Ensure editor control supports text, selection, caret, focus, clipboard, line/column, and scroll APIs required by `Form1`.

## 2. Behavior migration
- [x] 2.1 Port parser-driven autocomplete integration.
- [x] 2.2 Port diagnostics rendering and hover tooltip integration.
- [x] 2.3 Port syntax highlighting integration and theme-aware token colors.
- [x] 2.4 Port YAML indentation (`Tab`/`Shift+Tab`) and smart-enter behaviors with existing settings.
- [x] 2.5 Ensure autocomplete closes on caret-relocation clicks.
- [x] 2.6 Implement completion commit/dismiss contract:
  - `Enter`/`Tab` commit selected completion item when completion is open
  - `Escape` closes completion without text mutation
  - typing with completion open continues normal text insertion and list filtering.
- [x] 2.7 Ensure undo/redo grouping remains intuitive for smart-enter and indent/outdent edits.

## 3. UX and performance parity
- [x] 3.1 Implement scroll-past-end so the last line can be positioned near top of viewport.
- [x] 3.2 Ensure `Enter` at end-of-file reveals the new caret line without viewport jank.
- [x] 3.3 Ensure autocomplete never blocks normal typing.
- [x] 3.4 Validate smooth editing on 500+ line scripts.
- [x] 3.5 Validate cursor/selection navigation parity for key flows (`Arrow`, `Ctrl+Arrow`, `Shift+Arrow`, `Home`, `End`, mouse click reposition).
- [x] 3.6 Define reference performance profile for measurements (machine + build configuration + script size profile).
- [x] 3.7 Record latency observations and compare to target budgets:
  - keystroke-to-visible-text p95 <= 50 ms on 500-line script
  - completion update latency p95 <= 120 ms
  - EOF `Enter` caret reveal <= 100 ms.

## 4. App integration
- [x] 4.1 Replace current script editor instantiation/wiring in `Form1.Designer.cs` and `Form1.cs`.
- [x] 4.2 Preserve existing context menu actions, line/column status updates, and `Ctrl+S` save behavior.
- [x] 4.3 Preserve `CommandEditorSettings` runtime behavior toggles and defaults.

## 5. Cleanup
- [x] 5.1 Remove old editor-engine-specific code paths no longer used after migration.
- [x] 5.2 Remove obsolete editor package references only after successful migration verification.

## 6. Verification
- [x] 6.1 Update/add tests for editor text utilities and completion/diagnostic behavior affected by engine swap.
- [x] 6.2 Run editor-focused test slice and build validation.
- [x] 6.3 Run manual smoke tests for typing, scrolling, completion, diagnostics, theme/font, and settings.
- [x] 6.4 Add a parity checklist artifact documenting pass/fail for each "familiar editor" interaction contract item.
