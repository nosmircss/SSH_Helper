# Tasks: Update unified command map syntax

## 1. Parser and runtime contract
- [x] 1.1 Update parser contracts so canonicalized commands consume nested maps with explicit primary keys (`command`, `message`, `seconds`, `expression`, etc.).
- [x] 1.2 Implement nested-map parsing for control-flow command headers (`if`, `foreach`, `while`, `try`) while preserving current runtime semantics.
- [x] 1.3 Align `on_error` handling placement to command maps for commands that support continue/stop behavior.
- [x] 1.4 Update runtime command handlers and models as needed to consume canonicalized payload shape.

## 2. Validation behavior
- [x] 2.1 Keep canonical key diagnostics while allowing supported shorthand aliases for mapped commands.
- [x] 2.2 Ensure unknown-key diagnostics use command-map context (for example `send.command` missing vs legacy `send: echo hi`).

## 3. Editor authoring UX
- [x] 3.1 Update smart-enter behavior so `Enter` on command-map payload lines keeps option-level indentation by default.
- [x] 3.2 Add `Ctrl+Enter` behavior to insert sibling `- ` step lines at step indentation.
- [x] 3.3 Update autocomplete options to suggest canonical map keys for converted commands.

## 4. Content and presets
- [x] 4.1 Update `SCRIPTING.md` to document canonical map syntax and supported shorthand aliases.
- [x] 4.2 Update `qa_presets.json` command blocks to canonical map syntax.
- [x] 4.3 Update `ScriptSamples/**/*.yaml` for canonical command map shape.

## 5. Verification
- [x] 5.1 Add parser/runtime tests for canonical map syntax success paths.
- [x] 5.2 Add validation/parser tests proving supported shorthand aliases are accepted and mapped correctly.
- [x] 5.3 Add editor tests for `Enter` option-level continuation and `Ctrl+Enter` next-step insertion.
- [x] 5.4 Run targeted regression suites for scripting runtime, editor utilities, and Scintilla editor control behavior.

## 6. Shorthand mapping implementation
- [x] 6.1 Implement parser shorthand mappings for `send`, `print`, `wait`, `set`, `log`, `if`, `foreach`, `while`, and `exit`.
- [x] 6.2 Keep multi-field commands map-only (no shorthand expansion for `http`, `webhook`, `dns`, `ping`, `portcheck`, `sftp`, `readfile`, `writefile`, `input`, `updatecolumn`, `updateenvironment`, `parse`, `extract`, `try`).
