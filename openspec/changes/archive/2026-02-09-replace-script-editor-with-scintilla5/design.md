## Context
The current editor implementation uses a custom RichTextBox-based control with many compensating behaviors for syntax colors, completion UI, diagnostics overlays, smart-enter, and scroll/caret preservation. Despite recent fixes, users continue to observe editor jank and completion friction.

We need a full replacement that improves editing ergonomics while preserving parser-driven script semantics and keeping deployment portable.

## Goals / Non-Goals
- Goals:
  - Replace the current editor engine with Scintilla5.NET in a way that preserves existing script workflow integration.
  - Keep parser/validation/completion authority in C# and map into Scintilla APIs.
  - Improve responsiveness and interaction stability for 500+ line scripts.
  - Make core interactions feel familiar to users of modern code editors (cursor, selection, completion, reveal-on-edit, undo/redo).
  - Keep portable release packaging simple. User just distributes a exe (`.exe`).
- Non-Goals:
  - Introduce full IDE features unrelated to script authoring.
  - Change scripting runtime semantics or parser rules.
  - Rebuild parser logic in editor-specific code.

## Decisions
- Decision: Scintilla-backed control behind `IScriptEditor`
  - Add `ScintillaScriptEditorControl` implementing `IScriptEditor`.
  - Keep `Form1` and calling code dependent on `IScriptEditor` contract rather than concrete editor API.

- Decision: Preserve parser-driven semantics
  - Continue using existing `ScriptAutocompleteProvider`, `ScriptEditorValidationService`, and syntax/diagnostic models as source of truth.
  - Bridge data into Scintilla autocomplete/annotation/indicator primitives.

- Decision: Built-in editor behavior first, custom glue second
  - Prefer Scintilla native features for scrolling, caret reveal, completion list, and syntax styling.
  - Keep custom logic only where SSH Helper behavior must remain specific (smart-enter, parser-driven symbols, diagnostics mapping).

- Decision: Explicit interaction contract
  - Capture required editing semantics as a concrete contract for:
    - caret and selection navigation (`Arrow`, `Ctrl+Arrow`, `Shift+Arrow`, `Home`, `End`, mouse click)
    - completion lifecycle (trigger, incremental filtering, commit, dismiss, post-commit caret position)
    - newline behavior at file end and viewport reveal
    - undo/redo grouping for smart-enter and indent/outdent edits.
  - Rationale: avoids subjective "feels right" outcomes and prevents regressions.

- Decision: Performance budget for familiar feel
  - Treat responsiveness as a first-class acceptance gate with practical thresholds in a reference environment:
    - rapid typing remains interactive with no dropped input
    - keystroke-to-visible-text latency p95 <= 50 ms for 500-line scripts
    - completion popup update latency p95 <= 120 ms after qualifying keystrokes
    - end-of-file `Enter` reveal keeps caret visible within <= 100 ms
    - large-script editing (500+ lines) does not require visible UI stalls.
  - Rationale: familiar editor feel is strongly tied to latency, not just feature parity.

- Decision: Portable packaging
  - Use only NuGet-managed native assets shipped with app output.
  - Do not require machine-level runtimes outside normal .NET app dependencies.

## Risks / Trade-offs
- Scintilla API integration complexity.
  - Mitigation: implement adapter incrementally behind `IScriptEditor` and keep behavior tests focused on contract.

- Behavior drift from current editor semantics.
  - Mitigation: preserve existing settings and parser-driven contracts, plus explicit parity checks for key workflows.

- Dependency migration churn.
  - Mitigation: migrate first, then remove obsolete packages only after compile/test pass confirms no references remain.

## Migration Plan
1. Add Scintilla5.NET dependency and create `ScintillaScriptEditorControl`.
2. Implement baseline `IScriptEditor` API surface (text/selection/focus/clipboard/events/line-col).
3. Implement interaction contract: navigation/selection/completion/commit-dismiss/newline-reveal/undo semantics.
4. Port syntax highlight, completion, diagnostics, and tooltip integrations.
5. Port indentation and smart-enter behavior with settings parity.
6. Replace existing editor construction/wiring in `Form1`.
7. Validate UX parity and performance on large scripts using explicit acceptance checklist.
8. Remove obsolete editor-specific code/dependency leftovers.

## Interaction Matrix
| Input / Action | Completion Closed | Completion Open | Expected Familiar Behavior |
|---|---|---|---|
| Type alphanumeric | Insert text at caret | Insert text at caret and refilter list | Typing is never blocked by completion UI |
| `Backspace` / `Delete` | Delete text and keep caret stable | Delete text and refilter/dismiss as needed | Editing remains uninterrupted while list is visible |
| `Enter` | Apply smart-enter/newline behavior based on settings | Commit selected completion item | `Enter` only inserts newline when completion is not active |
| `Tab` | Apply indent behavior (`UseSpacesForTab`, `IndentSize`) | Commit selected completion item | `Tab` is context-aware between indentation and completion commit |
| `Shift+Tab` | Outdent selected lines by one indent level | Outdent selected lines by one indent level (completion closes first) | Outdent behavior remains deterministic |
| `Escape` | No text mutation | Dismiss completion popup without text mutation | Dismiss is immediate and side-effect free |
| `Up` / `Down` | Move caret line | Move completion selection | Navigation keys prioritize completion list when active |
| `Left` / `Right` | Move caret char | Move caret char and close completion if context invalidates | Horizontal caret movement remains predictable |
| `Ctrl+Left` / `Ctrl+Right` | Word-wise caret movement | Word-wise caret movement and close completion if context invalidates | Word navigation behaves like mainstream editors |
| `Shift+Arrow` | Expand/shrink selection | Expand/shrink selection and close completion if needed | Selection behavior is unaffected by popup presence |
| `Home` / `End` | Move caret to line start/end | Same, with completion dismiss/refilter as needed | Line navigation is stable and predictable |
| Mouse click in editor | Reposition caret | Reposition caret and dismiss completion | Click-to-move always closes suggestion list |
| Mouse wheel scroll | Scroll viewport | Scroll viewport and keep completion anchored or dismiss on overflow | Scrolling never traps input focus |
| `Ctrl+Z` / `Ctrl+Y` | Undo/redo by logical edit units | Same; completion UI closes before applying undo/redo | Undo/redo order remains intuitive |
| `Ctrl+S` | Trigger preset save workflow | Trigger preset save workflow (after dismissing completion) | Save shortcut parity with existing workflow |
| EOF `Enter` (last line) | Insert newline and reveal caret within viewport | Commit completion if open; next `Enter` follows closed-state behavior | New line is visible immediately without manual scroll |

Reference notes:
- "Completion Open" means the parser-driven suggestion list is visible and a selected item exists.
- If no selected completion item exists, `Enter`/`Tab` fall back to closed-state behavior.
- Completion dismissal conditions must avoid mutating document content unless commit is explicit.

## Open Questions
- None for proposal phase.
