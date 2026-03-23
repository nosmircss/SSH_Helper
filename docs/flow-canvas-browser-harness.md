# Flow Canvas Browser Harness

## Purpose
Browser-level harness for Flow Canvas behavior that cannot be validated reliably with host/runtime unit tests alone.

Current coverage focuses on execution trigger parity:
- Toolbar `Run` and keyboard `F5` emit equivalent `execute-canvas` payloads.
- Toolbar `Test Step` and keyboard `Ctrl+Enter` target the same selected node and payload.

## Location
- Playwright config: `FlowCanvas/playwright.config.ts`
- Harness helpers: `FlowCanvas/e2e/support/harness.ts`
- Graph fixtures: `FlowCanvas/e2e/fixtures/graphs.ts`
- Specs: `FlowCanvas/e2e/flow-canvas-parity.spec.ts`

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

## How It Works
- A test-only hook (`window.__FLOWCANVAS_TEST_HOOKS__`) captures outbound MessageBus messages in non-WebView2 mode.
- Fixtures are injected through the same inbound transport path used by host messages (`window.postMessage({ type: 'load-graph', ... })`).
- Assertions validate emitted host contracts (`execute-canvas` payload shape and parity behavior).

## Artifacts
- HTML report: `FlowCanvas/playwright-report/`
- Failure videos/screenshots/traces: `FlowCanvas/test-results/`

## CI Integration
- Workflow: `.github/workflows/build-release.yml`
- Job: `flowcanvas-browser-tests`
- Gate: Windows build/release path now waits for browser harness pass.
- Uploaded artifacts (always): `FlowCanvas/playwright-report/`, `FlowCanvas/test-results/`

## Troubleshooting
- `npm run test:e2e` fails with missing browser:
  - Run `npm run test:e2e:install`.
- Timeouts on startup:
  - Verify `npm run dev` can start locally and port `4174` is free.
- Selector failures after UI changes:
  - Update fixture labels/locators in `FlowCanvas/e2e/*.spec.ts` and keep assertions focused on message contracts and behavior, not visual styling details.
