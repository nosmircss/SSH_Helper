# Flow Canvas Browser Harness

## Purpose
Browser-level harness for Flow Canvas behavior that cannot be validated reliably with host/runtime unit tests alone.

Current coverage focuses on execution trigger parity:
- Toolbar `Run` and keyboard `F5` emit equivalent `execute-canvas` payloads.
- Toolbar `Test Step` and keyboard `Ctrl+Enter` target the same selected node and payload.
- QA preset parity matrix rebuilds presets through Flow Canvas state/actions (without `load-graph`) and verifies bridge export semantic parity.
- Intentional-invalid QA presets run in a dedicated negative suite and are expected to fail validation.
- Gesture smoke coverage validates drag/connect/property-edit export validity through real UI interactions.

## Location
- Playwright config: `FlowCanvas/playwright.config.ts`
- Harness helpers: `FlowCanvas/e2e/support/harness.ts`
- QA parity loader/comparator helpers: `FlowCanvas/e2e/support/qaPresetLoader.ts`, `FlowCanvas/e2e/support/parityCli.ts`
- Graph fixtures: `FlowCanvas/e2e/fixtures/graphs.ts`
- Specs:
  - `FlowCanvas/e2e/flow-canvas-parity.spec.ts`
  - `FlowCanvas/e2e/flow-canvas-preset-parity.spec.ts`
  - `FlowCanvas/e2e/flow-canvas-preset-negative.spec.ts`
  - `FlowCanvas/e2e/flow-canvas-gesture-smoke.spec.ts`
- Parity CLI helper: `FlowCanvas/tools/FlowCanvasParityCli/`

## Run Locally
From repo root:

```powershell
cd FlowCanvas
npm install
npm run test:e2e:install
npm run test:e2e
```

Optional:

```powershell
npm run test:e2e:headed
npm run test:e2e:ui
```

Manual parity run only:

```powershell
cd FlowCanvas
npm run test:e2e:parity
# or
.\scripts\run-preset-parity.ps1
```

## How It Works
- A test-only hook (`window.__FLOWCANVAS_TEST_HOOKS__`) captures outbound MessageBus messages in non-WebView2 mode.
- Fixtures are injected through the same inbound transport path used by host messages (`window.postMessage({ type: 'load-graph', ... })`).
- Parity suites rebuild graph state through test hooks (`setGraphViaActions`) so reconstruction does not depend on `load-graph`.
- Assertions validate emitted host contracts (`execute-canvas` payload shape and parity behavior).

## Artifacts
- HTML report: `FlowCanvas/playwright-report/`
- Failure videos/screenshots/traces: `FlowCanvas/test-results/`

## CI Integration
- Workflow: `.github/workflows/build-release.yml`
- Job: `flowcanvas-browser-tests`
- Gate: existing browser harness job remains in CI.
- Preset parity suites are manual-run in this phase (no CI gate changes yet).
- Uploaded artifacts (always): `FlowCanvas/playwright-report/`, `FlowCanvas/test-results/`

## Follow-Up
- After collecting runtime/stability data from manual parity runs, add a follow-up task to decide CI gating for:
  - `flow-canvas-preset-parity.spec.ts`
  - `flow-canvas-preset-negative.spec.ts`
  - `flow-canvas-gesture-smoke.spec.ts`

## Troubleshooting
- `npm run test:e2e` fails with missing browser:
  - Run `npm run test:e2e:install`.
- Timeouts on startup:
  - Verify `npm run dev` can start locally and port `4174` is free.
- Selector failures after UI changes:
  - Update fixture labels/locators in `FlowCanvas/e2e/*.spec.ts` and keep assertions focused on message contracts and behavior, not visual styling details.
