# Change: Refactor Flow Canvas Correctness

## Why
Flow Canvas currently has correctness gaps that violate user expectations: edited imported blocks can export stale YAML, execution/debug highlighting can mis-map nested steps, keyboard and toolbar execution paths diverge, and several interaction features regress under normal usage.

## What Changes
- Add a canonical Flow Canvas export contract with structured diagnostics and explicit failure behavior (no silent block drops).
- Add a unified host message for atomic canvas execution (`execute-canvas`) across run and test-step flows.
- Add scoped step identity (`StepPath`) for runtime/debug event correlation.
- Add guardrails and interaction fixes for undo, comments, breakpoint visuals, context menus, and selection sync.
- Add focused automated tests for bridge/export, mapping, execution messaging parity, and interaction correctness.
- Add a follow-up browser test harness phase for end-to-end Flow Canvas parity/interaction coverage.

## Impact
- Affected specs: `flow-canvas` (new), `scripting-runtime` (modified).
- Affected code: `FlowCanvas/src/*`, `Services/FlowCanvasBridge.cs`, `UI/FlowCanvasForm.cs`, `Form1.cs`, `Services/Scripting/*`, test project.
