## 1. Implementation
- [x] 1.1 Add `flow-canvas` spec deltas and runtime deltas for scoped step identity.
- [x] 1.2 Introduce structured graph export result with diagnostics and guardrail messaging.
- [x] 1.3 Ensure non-container executable blocks export from canonical props path and reject invalid blocks explicitly.
- [x] 1.4 Add `execute-canvas` host message and route toolbar/keyboard run+test-step through a single pipeline.
- [x] 1.5 Add scoped `StepPath` to script step/debug events and host-side node resolution.
- [x] 1.6 Implement true single-host test-step entrypoint and remove button-proxy behavior.
- [x] 1.7 Fix interaction regressions (drag undo timing, breakpoint visuals, context-menu conflicts, comments rendering, selection sync).
- [x] 1.8 Add focused automated tests and run verification suites.

## 2. Verification Gates
- [x] 2.1 Export gate: edited imported blocks persist, unsupported/invalid export paths fail loudly with diagnostics.
- [x] 2.2 Mapping gate: nested runtime/debug events resolve to correct canvas node via `StepPath`.
- [x] 2.3 Execute parity gate: keyboard and toolbar run/test-step use identical host pipeline.
- [x] 2.4 Interaction gate: undo/comment/context-menu/breakpoint/selection regressions are covered and passing.

## 3. Follow-up Phase: Browser Test Harness
- [x] 3.1 Add a browser automation harness for `FlowCanvas` with deterministic graph fixtures and host-bridge stubs/mocks.
- [x] 3.2 Add end-to-end browser specs for run/test-step trigger parity (toolbar and keyboard paths reach the same host payload contract).
- [x] 3.3 Add end-to-end browser specs for key interaction correctness (drag undo, breakpoint/context-menu gesture separation, comment node persistence, box-select state sync).
- [x] 3.4 Wire harness into CI with artifact capture (screenshots/video/logs) and add local run instructions.
