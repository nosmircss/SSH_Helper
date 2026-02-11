## 1. Specification
- [x] 1.1 Add script-editor requirement deltas for visual options settings and persistence.
- [x] 1.2 Add script-editor-scintilla-host requirement deltas for Scintilla visual aid behavior.
- [x] 1.3 Validate change with `openspec validate add-script-editor-visual-options --strict --no-interactive`.

## 2. Implementation
- [x] 2.1 Extend `CommandEditorSettings` with visual option toggles and numeric bounds.
- [x] 2.2 Add Command Editor UI controls to load/save visual options.
- [x] 2.3 Apply visual options in `ScintillaScriptEditorControl` (current line, guides, whitespace, ruler, folding, brace highlight).

## 3. Verification
- [x] 3.1 Extend configuration round-trip/default tests for new settings.
- [x] 3.2 Add/extend Scintilla control tests for visual option behavior.
- [x] 3.3 Run targeted test slices for updated editor/config tests.
