# Tasks: Add Ctrl+C capture mode for interactive

## 1. Model and parser contracts
- [x] 1.1 Extend `InteractiveOptions` with `command`, `capture`, `max_seconds`, and `mirror_output`.
- [x] 1.2 Update parser metadata and parsing for the new keys.
- [x] 1.3 Enforce validation rules (`interactive.command` requires `session: separate`; `max_seconds > 0` when provided).

## 2. Runtime behavior
- [x] 2.1 Resolve runtime variable substitution for `interactive.command` and `interactive.capture`.
- [x] 2.2 Add capture-mode interactive flow with auto-send command and completion triggers (Ctrl+C, timeout, natural completion, early close).
- [x] 2.3 Keep window open in detached read-only mode for Ctrl+C/timeout/natural completion while script continues.
- [x] 2.4 Capture transcript, set completion reason, and keep `on_error` behavior unchanged.
- [x] 2.5 Store transcript to capture variable and `_output` only when `capture` is configured.

## 3. UI and history/audit updates
- [x] 3.1 Add detached read-only mode in `InteractiveTerminalForm` (disable host input/paste/reset/clear; keep copy/selection/scroll).
- [x] 3.2 Record capture close reasons (`ctrl_c_continue`, `timeout_continue`, `early_close_partial`, `natural_complete`) in interactive session details.
- [x] 3.3 Ensure execution details remain complete even when detached window stays open.

## 4. Editor/docs/QA and tests
- [x] 4.1 Extend autocomplete option keys and enum-like values for interactive capture mode.
- [x] 4.2 Update scripting docs with capture-mode syntax and behavior.
- [x] 4.3 Add QA preset for sniffer/tcpdump style interactive capture workflow.
- [x] 4.4 Add parser/editor/command/service regression tests for new contracts.
