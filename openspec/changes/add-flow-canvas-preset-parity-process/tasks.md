## 1. Implementation
- [x] 1.1 Add parity orchestration loader for `qa_presets.json` (valid vs intentional-invalid classification).
- [x] 1.2 Add front-end test hooks to build graphs through Flow Canvas state/actions without `load-graph`.
- [x] 1.3 Add branch metadata model and editing UX for container branches (`if/elif/else`, `try/catch/finally`, `switch` cases/else, `parallel` branches).
- [x] 1.4 Extend `FlowCanvasBridge` graph-native export generation for `try`, `switch`, `parallel`, and `if` `elif`.
- [x] 1.5 Add Start-panel advanced editors for `vars`, `imports`, and `subroutines`; serialize them in exported preamble.
- [x] 1.6 Add semantic comparator + validator helper used by parity tests.
- [x] 1.7 Add bulk Playwright parity matrix for all valid QA presets plus synthetic `browser_callback`.
- [x] 1.8 Add negative parity suite for intentional-invalid QA presets.
- [x] 1.9 Add gesture smoke suite for drag/connect/property-edit interactions.
- [x] 1.10 Add manual-run command/script and usage docs for parity execution.

## 2. Verification Gates
- [x] 2.1 All valid QA presets pass export + validation + semantic equivalence.
- [x] 2.2 Synthetic `browser_callback` case passes parity checks.
- [x] 2.3 Intentional-invalid presets fail with expected validation diagnostics.
- [x] 2.4 Parity suites do not rely on `load-graph` preset import path.
- [x] 2.5 Focused unit tests for container export and Start advanced-section serialization pass.

## 3. Rollout
- [x] 3.1 Ship as manual-run process only in this phase.
- [x] 3.2 Capture follow-up task for CI gating after runtime/stability data is collected.
