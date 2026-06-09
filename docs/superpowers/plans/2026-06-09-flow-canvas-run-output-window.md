# Detachable Run Output Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the in-canvas pop-out overlay with a real, native, top-level window (draggable to any monitor) that hosts a second WebView2 rendering the same `RunOutputView` console, fed by Form1's output stream.

**Architecture:** A new `RunOutputWindowForm` (WinForms, owned by Form1) hosts its own WebView2 loading `https://flowcanvas.local/index.html?panel=runoutput`; React branches on that flag to render only `<RunOutputView/>` via a minimal bridge. Form1 fans the existing run-output stream + run-state into the window. The canvas "Pop out" button now opens the window (via a message to the host) and switches the dock to the Block Output tab.

**Tech Stack:** C# .NET 8 WinForms + WebView2 (xUnit + Xunit.StaFact + FluentAssertions), React 19 + Zustand + TypeScript (Vitest + @testing-library/react + jsdom).

**Spec:** `docs/superpowers/specs/2026-06-09-flow-canvas-run-output-window-design.md`

---

## Conventions (read once)

- **React test:** `cd FlowCanvas && npx vitest run <spec>` (full: `npm test`). Type-check: `npx tsc --noEmit`. Prod bundle: `npm run build`.
- **C# test:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~<Class>"`. Build (no Node): `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`.
- **Zustand:** vanilla, no immer; `set((s) => ({...}))`.
- **C# message send idiom:** `SendMessage(new { type = "...", ... })`, queued behind the `ready` handshake into `ConcurrentQueue<string> _pendingMessages`.
- **C# test seam:** in tests `_reactReady` is false → `SendMessage` enqueues onto `_pendingMessages`; drain + parse to assert.
- **Every C# UI test class** carries `[Collection(CallbackUiSerialCollection.Name)]` + `[WinFormsFact]`.
- **The detached console is dark-only** (the canvas ignores `theme-sync`; `RunOutputView` doesn't read `theme`) — no theme plumbing in the window.
- **Commit trailer:** end every commit message with `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

## File Structure

**Phase 1 — Standalone React window**
- Create `FlowCanvas/src/panelMode.ts` — pure `panelFromSearch(search)` helper.
- Create `FlowCanvas/src/RunOutputWindowApp.tsx` — standalone console host.
- Create `FlowCanvas/src/stores/runOutputWindowBridge.ts` — minimal bridge.
- Modify `FlowCanvas/src/main.tsx` — branch on `?panel=runoutput`.
- Modify `FlowCanvas/src/stores/slices/uiSlice.ts` — add `setRunOutputPoppedOut`.
- Tests: `panelMode.test.ts`, `RunOutputWindowApp.test.tsx`, `runOutputWindowBridge.test.ts`.

**Phase 2 — Remove the in-canvas overlay**
- Delete `FlowCanvas/src/panels/RunOutputPopOut.tsx` + `__tests__/RunOutputPopOut.test.tsx`.
- Modify `FlowCanvas/src/App.tsx` — remove the import + mount.

**Phase 3 — Canvas-side protocol**
- Modify `FlowCanvas/src/communication-message-types.ts` — new message keys.
- Modify `FlowCanvas/src/stores/slices/uiSlice.ts` — `openRunOutputWindow`/`closeRunOutputWindow`, remove `toggleRunOutputPoppedOut`.
- Modify `FlowCanvas/src/panels/RunOutputView.tsx` — Pop-out button → `openRunOutputWindow`.
- Modify `FlowCanvas/src/panels/OutputPreview.tsx` — Run-tab dock-back when popped out; drop the placeholder branch.
- Modify `FlowCanvas/src/stores/messageBridge.ts` — `run-output-window-closed` handler; gate auto-focus/unread on `!poppedOut`.
- Tests: `uiSliceRunOutput.test.ts` (update), `messageBridge`-level via existing patterns, `OutputPreviewTabs.test.tsx` (update), `RunOutputView` (update).

**Phase 4 — C# window form**
- Create `UI/RunOutputWindowForm.cs`.
- Modify `Models/AppConfiguration.cs` — window geometry fields.
- Test `SSH_Helper.Tests/UI/RunOutputWindowFormTests.cs`.

**Phase 5 — C# wiring**
- Modify `Form1.cs` — own/open/feed the window + run-state.
- Modify `UI/FlowCanvasForm.cs` — open/close-window inbound messages + events.
- Test `SSH_Helper.Tests/UI/FlowCanvasFormRunOutputWindowTests.cs` (the two new events).

**Ordering rationale:** Phases 1→2→3 keep the React build green at each step (overlay removed before `toggleRunOutputPoppedOut` is dropped). Phases 4–5 add the C# side; full end-to-end works after Phase 5.

**Spec deviation (noted):** the spec mentioned `ModelessDialogManager`; this plan uses the proven `_flowCanvasForm`-style ownership (nullable field + reuse-if-open + `FormClosed` nulling) since the window needs Form1 ownership + event wiring + its own geometry persistence — same single-instance/owner intent.

---

## Phase 1 — Standalone React window

### Task 1.1: `panelFromSearch` helper

**Files:** Create `FlowCanvas/src/panelMode.ts`; Test `FlowCanvas/src/__tests__/panelMode.test.ts`

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/__tests__/panelMode.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { panelFromSearch } from '../panelMode';

describe('panelFromSearch', () => {
  it('returns "runoutput" when panel=runoutput', () => {
    expect(panelFromSearch('?panel=runoutput')).toBe('runoutput');
    expect(panelFromSearch('?foo=bar&panel=runoutput')).toBe('runoutput');
  });
  it('returns "main" otherwise', () => {
    expect(panelFromSearch('')).toBe('main');
    expect(panelFromSearch('?panel=other')).toBe('main');
    expect(panelFromSearch('?x=1')).toBe('main');
  });
});
```

- [ ] **Step 2: Run to verify fail**

Run: `cd FlowCanvas && npx vitest run src/__tests__/panelMode.test.ts`
Expected: FAIL — cannot find module `../panelMode`.

- [ ] **Step 3: Implement**

Create `FlowCanvas/src/panelMode.ts`:

```ts
export type PanelMode = 'main' | 'runoutput';

/** Decides which React entry to render based on the URL query (?panel=runoutput). */
export function panelFromSearch(search: string): PanelMode {
  return new URLSearchParams(search).get('panel') === 'runoutput' ? 'runoutput' : 'main';
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd FlowCanvas && npx vitest run src/__tests__/panelMode.test.ts`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/panelMode.ts FlowCanvas/src/__tests__/panelMode.test.ts
git commit -m "feat(flow-canvas): panelFromSearch helper for the run-output window entry

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.2: `setRunOutputPoppedOut` setter in uiSlice

**Files:** Modify `FlowCanvas/src/stores/slices/uiSlice.ts`; Test `FlowCanvas/src/stores/slices/__tests__/uiSliceRunOutput.test.ts` (extend)

- [ ] **Step 1: Add failing assertion**

Append inside the `describe` block in `uiSliceRunOutput.test.ts`:

```ts
  it('setRunOutputPoppedOut sets the flag directly', () => {
    useFlowStore.getState().setRunOutputPoppedOut(true);
    expect(useFlowStore.getState().runOutputPoppedOut).toBe(true);
    useFlowStore.getState().setRunOutputPoppedOut(false);
    expect(useFlowStore.getState().runOutputPoppedOut).toBe(false);
  });
```

- [ ] **Step 2: Run to verify fail**

Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/uiSliceRunOutput.test.ts`
Expected: FAIL — `setRunOutputPoppedOut is not a function`.

- [ ] **Step 3: Implement**

In `uiSlice.ts`, add to the `UISlice` interface (next to `toggleRunOutputPoppedOut`):

```ts
  setRunOutputPoppedOut: (v: boolean) => void;
```

Add the implementation (next to `toggleRunOutputPoppedOut`):

```ts
  setRunOutputPoppedOut: (v) => set({ runOutputPoppedOut: v }),
```

- [ ] **Step 4: Run to verify pass**

Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/uiSliceRunOutput.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/slices/uiSlice.ts FlowCanvas/src/stores/slices/__tests__/uiSliceRunOutput.test.ts
git commit -m "feat(flow-canvas): setRunOutputPoppedOut direct setter

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.3: Minimal window bridge

**Files:** Create `FlowCanvas/src/stores/runOutputWindowBridge.ts`; Test `FlowCanvas/src/stores/__tests__/runOutputWindowBridge.test.ts`

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/stores/__tests__/runOutputWindowBridge.test.ts`:

```ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../MessageBus', () => {
  const handlers = new Map<string, Set<(m: any) => void>>();
  return {
    messageBus: {
      on: (t: string, h: (m: any) => void) => {
        if (!handlers.has(t)) handlers.set(t, new Set());
        handlers.get(t)!.add(h);
        return () => handlers.get(t)?.delete(h);
      },
      send: vi.fn(),
      sendReady: vi.fn(),
      __emit: (m: any) => handlers.get(m.type)?.forEach((h) => h(m)),
    },
    CANVAS_HOST_MESSAGES: {
      incoming: { runOutput: 'run-output', runOutputClear: 'run-output-clear', executionStarted: 'execution-started', executionFinished: 'execution-finished', layoutRestore: 'layout-restore' },
      outgoing: { layoutSave: 'layout-save' },
    },
  };
});
import { useFlowStore } from '../useFlowStore';
import { messageBus } from '../../MessageBus';
import { initRunOutputWindowBridge } from '../runOutputWindowBridge';

const emit = (m: any) => (messageBus as any).__emit(m);

describe('runOutputWindowBridge', () => {
  let cleanup: () => void;
  beforeEach(() => {
    useFlowStore.getState().clearRunOutput();
    useFlowStore.getState().setRunning(false);
    cleanup = initRunOutputWindowBridge();
  });

  it('sends ready on init', () => {
    expect(messageBus.sendReady).toHaveBeenCalled();
  });
  it('appends run-output and clears on run-output-clear', () => {
    emit({ type: 'run-output', chunk: 'hello\n' });
    expect(useFlowStore.getState().runOutput).toBe('hello\n');
    emit({ type: 'run-output-clear' });
    expect(useFlowStore.getState().runOutput).toBe('');
  });
  it('drives isRunning from execution-started/finished', () => {
    emit({ type: 'execution-started' });
    expect(useFlowStore.getState().isRunning).toBe(true);
    emit({ type: 'execution-finished' });
    expect(useFlowStore.getState().isRunning).toBe(false);
  });
  it('restores prefs from layout-restore', () => {
    useFlowStore.setState({ runOutputColor: true, runOutputWrap: false });
    emit({ type: 'layout-restore', runOutputColor: false, runOutputWrap: true });
    expect(useFlowStore.getState().runOutputColor).toBe(false);
    expect(useFlowStore.getState().runOutputWrap).toBe(true);
    cleanup();
  });
});
```

- [ ] **Step 2: Run to verify fail**

Run: `cd FlowCanvas && npx vitest run src/stores/__tests__/runOutputWindowBridge.test.ts`
Expected: FAIL — cannot find module `../runOutputWindowBridge`.

- [ ] **Step 3: Implement**

Create `FlowCanvas/src/stores/runOutputWindowBridge.ts`:

```ts
/**
 * Minimal bridge for the standalone Run Output window (?panel=runoutput). Unlike the full
 * messageBridge.ts (which drives the whole canvas), this only feeds the RunOutputView console:
 * the run-output stream, run-state for the LIVE dot, and the view-pref seed. Dark-only.
 */
import { messageBus } from '../MessageBus';
import { useFlowStore } from './useFlowStore';
import { CANVAS_HOST_MESSAGES } from '../communication-message-types';

export function initRunOutputWindowBridge(): () => void {
  const store = useFlowStore;
  const unsubs = [
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutput, (msg) => {
      if (typeof msg.chunk === 'string' && msg.chunk.length > 0) {
        store.getState().appendRunOutput(msg.chunk);
      }
    }),
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutputClear, () => {
      store.getState().clearRunOutput();
    }),
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.executionStarted, () => {
      store.getState().setRunning(true);
    }),
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.executionFinished, () => {
      store.getState().setRunning(false);
    }),
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.layoutRestore, (msg) => {
      store.getState().restoreRunOutputPrefs({
        runOutputColor: typeof msg.runOutputColor === 'boolean' ? msg.runOutputColor : undefined,
        runOutputWrap: typeof msg.runOutputWrap === 'boolean' ? msg.runOutputWrap : undefined,
        runOutputFollow: typeof msg.runOutputFollow === 'boolean' ? msg.runOutputFollow : undefined,
      });
    }),
  ];
  messageBus.sendReady();
  return () => unsubs.forEach((u) => u());
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd FlowCanvas && npx vitest run src/stores/__tests__/runOutputWindowBridge.test.ts`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/runOutputWindowBridge.ts FlowCanvas/src/stores/__tests__/runOutputWindowBridge.test.ts
git commit -m "feat(flow-canvas): minimal bridge for the standalone Run Output window

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.4: `RunOutputWindowApp` + main.tsx branch

**Files:** Create `FlowCanvas/src/RunOutputWindowApp.tsx`; Modify `FlowCanvas/src/main.tsx`; Test `FlowCanvas/src/__tests__/RunOutputWindowApp.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/__tests__/RunOutputWindowApp.test.tsx`:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';

const bridgeCleanup = vi.fn();
const initBridge = vi.fn(() => bridgeCleanup);
vi.mock('../stores/runOutputWindowBridge', () => ({ initRunOutputWindowBridge: initBridge }));

const mock = vi.hoisted(() => ({
  state: {
    runOutput: '', isRunning: false, runOutputColor: true, runOutputWrap: false, runOutputFollow: true,
    runOutputPoppedOut: false,
    setRunOutputPoppedOut: vi.fn((v: boolean) => { mock.state.runOutputPoppedOut = v; }),
    toggleRunOutputColor: vi.fn(), toggleRunOutputWrap: vi.fn(), toggleRunOutputFollow: vi.fn(),
    openRunOutputWindow: vi.fn(),
  } as any,
}));
vi.mock('../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

import RunOutputWindowApp from '../RunOutputWindowApp';

describe('RunOutputWindowApp', () => {
  beforeEach(() => { mock.state.runOutputPoppedOut = false; vi.clearAllMocks(); });

  it('renders the console and inits the window bridge', () => {
    render(<RunOutputWindowApp />);
    expect(screen.getByTestId('run-output-view')).toBeInTheDocument();
    expect(initBridge).toHaveBeenCalledTimes(1);
  });

  it('marks itself popped-out so the console hides its own Pop out button', () => {
    render(<RunOutputWindowApp />);
    expect(mock.state.setRunOutputPoppedOut).toHaveBeenCalledWith(true);
  });
});
```

- [ ] **Step 2: Run to verify fail**

Run: `cd FlowCanvas && npx vitest run src/__tests__/RunOutputWindowApp.test.tsx`
Expected: FAIL — cannot find module `../RunOutputWindowApp`.

- [ ] **Step 3: Implement the app**

Create `FlowCanvas/src/RunOutputWindowApp.tsx`:

```tsx
/** Standalone entry for the detached Run Output window (?panel=runoutput). Renders only the
 *  console and wires the minimal window bridge. */
import { useEffect } from 'react';
import { useFlowStore } from './stores/useFlowStore';
import RunOutputView from './panels/RunOutputView';
import { initRunOutputWindowBridge } from './stores/runOutputWindowBridge';

export default function RunOutputWindowApp() {
  const setPoppedOut = useFlowStore((s) => s.setRunOutputPoppedOut);
  useEffect(() => {
    // This window IS the popped-out console, so hide the console's own "Pop out" button.
    setPoppedOut(true);
    const cleanup = initRunOutputWindowBridge();
    return cleanup;
  }, [setPoppedOut]);

  return (
    <div style={{ height: '100vh', display: 'flex', flexDirection: 'column', background: 'var(--fc-term-bg)' }}>
      <RunOutputView />
    </div>
  );
}
```

- [ ] **Step 4: Wire main.tsx**

Replace `FlowCanvas/src/main.tsx` with:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './styles/tokens.css';
import './styles/reducedMotion.css';
import './styles/justPlaced.css';
import App from './App';
import RunOutputWindowApp from './RunOutputWindowApp';
import { panelFromSearch } from './panelMode';

const isRunOutputWindow = panelFromSearch(window.location.search) === 'runoutput';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {isRunOutputWindow ? <RunOutputWindowApp /> : <App />}
  </StrictMode>,
);
```

- [ ] **Step 5: Run test + type-check**

Run: `cd FlowCanvas && npx vitest run src/__tests__/RunOutputWindowApp.test.tsx && npx tsc --noEmit`
Expected: PASS (2 tests), tsc clean.

- [ ] **Step 6: Commit**

```bash
git add FlowCanvas/src/RunOutputWindowApp.tsx FlowCanvas/src/main.tsx FlowCanvas/src/__tests__/RunOutputWindowApp.test.tsx
git commit -m "feat(flow-canvas): standalone RunOutputWindowApp + ?panel=runoutput entry branch

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> **CHECKPOINT — Phase 1.** The `?panel=runoutput` React app exists and is tested. Run `cd FlowCanvas && npm test` to confirm no regressions. Stop for review.

---

## Phase 2 — Remove the in-canvas overlay

### Task 2.1: Delete the overlay and unmount it

**Files:** Delete `FlowCanvas/src/panels/RunOutputPopOut.tsx` + `FlowCanvas/src/panels/__tests__/RunOutputPopOut.test.tsx`; Modify `FlowCanvas/src/App.tsx`

- [ ] **Step 1: Remove the App.tsx import + mount**

In `FlowCanvas/src/App.tsx`, delete the import line:

```tsx
import RunOutputPopOut from './panels/RunOutputPopOut';
```

and delete the mount line (in the JSX near `<DebugPanel />`):

```tsx
        <RunOutputPopOut />
```

- [ ] **Step 2: Delete the overlay files**

```bash
git rm FlowCanvas/src/panels/RunOutputPopOut.tsx FlowCanvas/src/panels/__tests__/RunOutputPopOut.test.tsx
```

- [ ] **Step 3: Type-check + full suite (no dangling references)**

Run: `cd FlowCanvas && npx tsc --noEmit && npm test`
Expected: PASS — no references to `RunOutputPopOut` remain; `toggleRunOutputPoppedOut` is still defined (used by `RunOutputView`'s button until Phase 3), so nothing breaks.

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/App.tsx
git commit -m "refactor(flow-canvas): remove in-canvas pop-out overlay (replaced by OS window)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> **CHECKPOINT — Phase 2.** Overlay gone, build green. Stop for review.

---

## Phase 3 — Canvas-side protocol

### Task 3.1: New message keys

**Files:** Modify `FlowCanvas/src/communication-message-types.ts`

- [ ] **Step 1: Add keys**

In `CANVAS_HOST_MESSAGES.incoming`, after `runOutputClear: 'run-output-clear',` add:

```ts
    runOutputWindowClosed: 'run-output-window-closed',
```

In `CANVAS_HOST_MESSAGES.outgoing`, after `setLayoutMode: 'set-layout-mode',` add:

```ts
    openRunOutputWindow: 'open-run-output-window',
    closeRunOutputWindow: 'close-run-output-window',
```

- [ ] **Step 2: Type-check**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/communication-message-types.ts
git commit -m "feat(flow-canvas): open/close-run-output-window + run-output-window-closed messages

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 3.2: open/close window actions in uiSlice

**Files:** Modify `FlowCanvas/src/stores/slices/uiSlice.ts`; Test `FlowCanvas/src/stores/slices/__tests__/uiSliceRunOutput.test.ts`

- [ ] **Step 1: Write the failing tests**

In `uiSliceRunOutput.test.ts`, replace the existing `toggleRunOutputPoppedOut` test with:

```ts
  it('openRunOutputWindow sends open + sets popped-out and the Block tab', () => {
    useFlowStore.getState().openRunOutputWindow();
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'open-run-output-window' }));
    expect(useFlowStore.getState().runOutputPoppedOut).toBe(true);
    expect(useFlowStore.getState().outputTab).toBe('block');
  });

  it('closeRunOutputWindow sends close + clears popped-out and shows the Run tab', () => {
    useFlowStore.setState({ runOutputPoppedOut: true, outputTab: 'block' });
    useFlowStore.getState().closeRunOutputWindow();
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'close-run-output-window' }));
    expect(useFlowStore.getState().runOutputPoppedOut).toBe(false);
    expect(useFlowStore.getState().outputTab).toBe('run');
  });
```

(Also remove `runOutputPoppedOut: false` reset usages of the old toggle if present; the `beforeEach` already resets `runOutputPoppedOut: false`.)

- [ ] **Step 2: Run to verify fail**

Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/uiSliceRunOutput.test.ts`
Expected: FAIL — `openRunOutputWindow is not a function`.

- [ ] **Step 3: Implement**

In `uiSlice.ts`, in the `UISlice` interface, REPLACE `toggleRunOutputPoppedOut: () => void;` with:

```ts
  openRunOutputWindow: () => void;
  closeRunOutputWindow: () => void;
```

(Keep `setRunOutputPoppedOut` from Phase 1.)

In the implementation, REPLACE the `toggleRunOutputPoppedOut: () => set(...)` line with:

```ts
  openRunOutputWindow: () => {
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.openRunOutputWindow });
    set({ runOutputPoppedOut: true, outputTab: 'block' });
  },
  closeRunOutputWindow: () => {
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.closeRunOutputWindow });
    set({ runOutputPoppedOut: false, outputTab: 'run' });
  },
```

- [ ] **Step 4: Run to verify pass**

Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/uiSliceRunOutput.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/slices/uiSlice.ts FlowCanvas/src/stores/slices/__tests__/uiSliceRunOutput.test.ts
git commit -m "feat(flow-canvas): open/closeRunOutputWindow actions replace the pop-out toggle

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 3.3: RunOutputView Pop-out button → open window

**Files:** Modify `FlowCanvas/src/panels/RunOutputView.tsx`; Test `FlowCanvas/src/panels/__tests__/RunOutputView.test.tsx`

- [ ] **Step 1: Update the test**

In `RunOutputView.test.tsx`, the mock currently has `toggleRunOutputPoppedOut: vi.fn()`. Replace that key with `openRunOutputWindow: vi.fn()`. Then update the pop-out visibility test's click assertion — replace the existing `'shows the Pop out button when docked, hides it when popped out'` test's body to also verify the click:

```ts
  it('shows the Pop out button when docked (calls openRunOutputWindow), hides it when popped out', () => {
    const { rerender } = render(<RunOutputView />);
    const btn = screen.getByTestId('run-output-btn-popout');
    btn.click();
    expect(mock.state.openRunOutputWindow).toHaveBeenCalledTimes(1);
    mock.state.runOutputPoppedOut = true;
    rerender(<RunOutputView />);
    expect(screen.queryByTestId('run-output-btn-popout')).toBeNull();
  });
```

- [ ] **Step 2: Run to verify fail**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/RunOutputView.test.tsx`
Expected: FAIL — `openRunOutputWindow` undefined / not called.

- [ ] **Step 3: Implement**

In `RunOutputView.tsx`, REPLACE the selector:

```tsx
  const togglePoppedOut = useFlowStore((s) => s.toggleRunOutputPoppedOut);
```

with:

```tsx
  const openWindow = useFlowStore((s) => s.openRunOutputWindow);
```

and update the Pop-out button's `onClick`:

```tsx
        {!poppedOut && (
          <ToolbarButton testid="run-output-btn-popout" active={false} onClick={openWindow} title="Pop out to a window">⤢ Pop out</ToolbarButton>
        )}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/RunOutputView.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/panels/RunOutputView.tsx FlowCanvas/src/panels/__tests__/RunOutputView.test.tsx
git commit -m "feat(flow-canvas): Pop out button opens the detached window

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 3.4: OutputPreview Run-tab docks back when popped out

**Files:** Modify `FlowCanvas/src/panels/OutputPreview.tsx`; Test `FlowCanvas/src/panels/__tests__/OutputPreviewTabs.test.tsx`

- [ ] **Step 1: Update the test**

In `OutputPreviewTabs.test.tsx`, add `closeRunOutputWindow: vi.fn()` and `runOutputPoppedOut: false` to the mock state (if not present). Add a test:

```ts
  it('clicking the Run tab while popped out docks back (closeRunOutputWindow)', () => {
    mock.state.runOutputPoppedOut = true;
    mock.state.outputTab = 'block';
    renderPanel();
    screen.getByTestId('output-tab-run').click();
    expect(mock.state.closeRunOutputWindow).toHaveBeenCalledTimes(1);
  });
```

- [ ] **Step 2: Run to verify fail**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/OutputPreviewTabs.test.tsx`
Expected: FAIL — `closeRunOutputWindow` not called.

- [ ] **Step 3: Implement**

In `OutputPreview.tsx`, add selectors near the others:

```tsx
  const closeWindow = useFlowStore((s) => s.closeRunOutputWindow);
```

(`poppedOut` selector already exists from the prior feature.) Update the Run Output `TabButton`'s `onClick` to dock-back when popped out:

```tsx
        <TabButton testid="output-tab-run" active={outputTab === 'run'} onClick={() => (poppedOut ? closeWindow() : setOutputTab('run'))}>
```

And REPLACE the run-tab body branch (the Phase-6 placeholder) so it just renders the console (when popped out, `outputTab` is `'block'`, so this branch isn't shown):

```tsx
      ) : (
        <RunOutputView />
      )}
```

(Remove the `poppedOut ? <placeholder> : <RunOutputView/>` conditional that was there.)

- [ ] **Step 4: Run to verify pass**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/OutputPreviewTabs.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/panels/OutputPreview.tsx FlowCanvas/src/panels/__tests__/OutputPreviewTabs.test.tsx
git commit -m "feat(flow-canvas): Run tab docks the window back when it's popped out

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 3.5: messageBridge — window-closed handler + gate auto-focus/unread

**Files:** Modify `FlowCanvas/src/stores/messageBridge.ts`

- [ ] **Step 1: Gate the auto-focus on run start**

In the `executionStarted` handler, REPLACE `store.getState().setOutputTab('run');` with:

```ts
      if (!store.getState().runOutputPoppedOut) store.getState().setOutputTab('run');
```

- [ ] **Step 2: Gate the unread dot**

In the `runOutput` handler, REPLACE the unread guard with:

```ts
        if (state.outputTab !== 'run' && !state.runOutputPoppedOut) {
          state.setRunOutputUnread(true);
        }
```

- [ ] **Step 3: Add the window-closed handler**

Inside the `unsubs` array (next to the run-output handlers), add:

```ts
    // The detached Run Output window was closed (by its own X) — dock the console back.
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutputWindowClosed, () => {
      const state = store.getState();
      state.setRunOutputPoppedOut(false);
      state.setOutputTab('run');
    }),
```

- [ ] **Step 4: Type-check + full suite**

Run: `cd FlowCanvas && npx tsc --noEmit && npm test`
Expected: PASS (no regressions).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/messageBridge.ts
git commit -m "feat(flow-canvas): dock console back on window close; skip auto-focus/unread when popped out

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 6: Production bundle check**

Run: `cd FlowCanvas && npm run build`
Expected: succeeds (tsc + vite). This is the bundle the C# windows load.

> **CHECKPOINT — Phase 3.** React side complete: the canvas opens/closes the window via messages, the dock switches tabs, the standalone window renders. C# handlers don't exist yet (messages are no-ops until Phase 5). Stop for review.

---

## Phase 4 — C# window form

### Task 4.1: WindowState geometry fields

**Files:** Modify `Models/AppConfiguration.cs`

- [ ] **Step 1: Add fields**

In `Models/AppConfiguration.cs`, in `WindowState`, after the `FlowCanvasRunOutputFollow` field (~line 524), add:

```csharp
        // Detached Run Output window geometry (persisted across sessions; null = not yet set)
        public int? FlowCanvasRunOutputWindowLeft { get; set; }
        public int? FlowCanvasRunOutputWindowTop { get; set; }
        public int? FlowCanvasRunOutputWindowWidth { get; set; }
        public int? FlowCanvasRunOutputWindowHeight { get; set; }
```

- [ ] **Step 2: Build**

Run: `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Models/AppConfiguration.cs
git commit -m "feat(flow-canvas): WindowState geometry fields for the Run Output window

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 4.2: `RunOutputWindowForm`

**Files:** Create `UI/RunOutputWindowForm.cs`; Test `SSH_Helper.Tests/UI/RunOutputWindowFormTests.cs`

- [ ] **Step 1: Write the failing test**

Create `SSH_Helper.Tests/UI/RunOutputWindowFormTests.cs`:

```csharp
using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class RunOutputWindowFormTests
{
    [WinFormsFact]
    public void SendRunOutputAppend_QueuesRunOutputMessage()
    {
        using var win = new RunOutputWindowForm(darkMode: true, configService: null);
        win.SendRunOutputAppend("hello\n");
        var q = GetField<ConcurrentQueue<string>>(win, "_pendingMessages");
        var msg = ReadMessageOfType(q, "run-output");
        msg.Should().NotBeNull();
        msg!["chunk"]?.ToString().Should().Be("hello\n");
    }

    [WinFormsFact]
    public void SendRunState_QueuesExecutionStartedOrFinished()
    {
        using var win = new RunOutputWindowForm(darkMode: true, configService: null);
        win.SendRunState(true);
        win.SendRunState(false);
        var q = GetField<ConcurrentQueue<string>>(win, "_pendingMessages");
        ReadMessageOfType(q, "execution-started").Should().NotBeNull();
        ReadMessageOfType(q, "execution-finished").Should().NotBeNull();
    }

    [WinFormsFact]
    public void SaveRunOutputPrefs_PersistsToWindowState_AndSeedReplaysThem()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"RunOutWin_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new ConfigurationService(Path.Combine(dir, "config.json"));
            using var win = new RunOutputWindowForm(darkMode: true, configService: cfg);
            InvokePrivate(win, "SaveRunOutputPrefs", JObject.FromObject(new { runOutputColor = false, runOutputWrap = true }));
            cfg.GetCurrent().WindowState!.FlowCanvasRunOutputColor.Should().BeFalse();
            cfg.GetCurrent().WindowState!.FlowCanvasRunOutputWrap.Should().BeTrue();

            var q = GetField<ConcurrentQueue<string>>(win, "_pendingMessages");
            InvokePrivate(win, "SendPersistedPrefs");
            var restore = ReadMessageOfType(q, "layout-restore");
            restore.Should().NotBeNull();
            restore!["runOutputColor"]?.Value<bool>().Should().BeFalse();
            restore["runOutputWrap"]?.Value<bool>().Should().BeTrue();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static JObject? ReadMessageOfType(ConcurrentQueue<string> q, string type)
    {
        foreach (var json in q.ToArray())
        {
            var p = JObject.Parse(json);
            if (string.Equals(p["type"]?.ToString(), type, StringComparison.Ordinal)) return p;
        }
        return null;
    }
    private static void InvokePrivate(object o, string m, params object?[] a)
    {
        var mi = o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.NonPublic);
        mi.Should().NotBeNull($"{m} should exist");
        mi!.Invoke(o, a);
    }
    private static T GetField<T>(object o, string f) where T : class
    {
        var fi = o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic);
        fi.Should().NotBeNull();
        return (fi!.GetValue(o) as T)!;
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~RunOutputWindowFormTests"`
Expected: FAIL — compile error: `RunOutputWindowForm` not defined.

- [ ] **Step 3: Implement the form**

Create `UI/RunOutputWindowForm.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.Utilities;

namespace SSH_Helper.UI
{
    /// <summary>
    /// Detachable top-level window mirroring the run output. Hosts its own WebView2 loading the
    /// same dist with ?panel=runoutput (renders only RunOutputView). Owned and fed by Form1;
    /// independent of the Flow Canvas window. Dark-only (matches the console).
    /// </summary>
    internal sealed class RunOutputWindowForm : Form
    {
        private readonly WebView2 _webView;
        private readonly Label _statusLabel;
        private readonly bool _darkMode;
        private readonly ConfigurationService? _configService;
        private readonly ConcurrentQueue<string> _pendingMessages = new();
        private bool _reactReady;
        private bool _initStarted;

        private static Point? _lastLocation;
        private static Size? _lastSize;

        public RunOutputWindowForm(bool darkMode, ConfigurationService? configService = null)
        {
            _darkMode = darkMode;
            _configService = configService;
            _webView = new WebView2();

            var initialSize = _lastSize ?? GetPersistedSize() ?? new Size(900, 520);

            Text = "Run Output";
            Size = initialSize;
            MinimumSize = new Size(420, 260);

            var persistedLocation = _lastLocation ?? GetPersistedLocation();
            StartPosition = persistedLocation.HasValue ? FormStartPosition.Manual : FormStartPosition.CenterParent;
            if (persistedLocation.HasValue) Location = persistedLocation.Value;
            ShowInTaskbar = true;
            KeyPreview = true;

            _statusLabel = new Label
            {
                Text = "Initializing Run Output...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12F),
                ForeColor = Color.FromArgb(136, 136, 136),
                BackColor = _darkMode ? DialogTheme.DarkBackground : SystemColors.Control,
            };
            Controls.Add(_statusLabel);

            ((System.ComponentModel.ISupportInitialize)_webView).BeginInit();
            _webView.Dock = DockStyle.Fill;
            _webView.Visible = false;
            Controls.Add(_webView);
            ((System.ComponentModel.ISupportInitialize)_webView).EndInit();

            if (_darkMode) BackColor = DialogTheme.DarkBackground;
            DialogTheme.SetDarkTitleBar(this, _darkMode);

            LocationChanged += (_, _) => { if (WindowState == FormWindowState.Normal) _lastLocation = Location; };
            SizeChanged += (_, _) => { if (WindowState == FormWindowState.Normal) _lastSize = Size; };
            FormClosing += (_, _) => SavePersistedGeometry();

            Shown += OnFormShown;
        }

        private async void OnFormShown(object? sender, EventArgs e)
        {
            if (_initStarted) return;
            _initStarted = true;
            try { await InitializeWebView2Async(); }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
                _statusLabel.ForeColor = Color.FromArgb(231, 76, 60);
                System.Diagnostics.Debug.WriteLine($"[RunOutputWindow] Init error: {ex}");
            }
        }

        private async System.Threading.Tasks.Task InitializeWebView2Async()
        {
            _statusLabel.Text = "Loading Run Output...";
            if (!_webView.IsHandleCreated) _webView.CreateControl();

            // Dedicated user-data folder: a separate browser process from the Flow Canvas WebView2,
            // which avoids any user-data-folder lock contention between the two windows.
            var userDataDir = Path.Combine(AppDataPaths.GetAppFolder(), "WebView2", "RunOutputWindow");
            Directory.CreateDirectory(userDataDir);

            var environment = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: userDataDir);
            await _webView.EnsureCoreWebView2Async(environment);

            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            if (_darkMode) _webView.DefaultBackgroundColor = DialogTheme.DarkBackground;

            _webView.CoreWebView2.NavigationCompleted += (s, ev) =>
            {
                if (ev.IsSuccess) { _webView.Visible = true; _statusLabel.Visible = false; }
                else { _statusLabel.Text = $"Navigation error: {ev.WebErrorStatus}"; _statusLabel.ForeColor = Color.FromArgb(231, 76, 60); }
            };
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            var distPath = FlowCanvasDistLocator.ResolveDistPath().DistPath;
            if (distPath != null)
            {
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping("flowcanvas.local", distPath, CoreWebView2HostResourceAccessKind.Allow);
                _webView.CoreWebView2.Navigate("https://flowcanvas.local/index.html?panel=runoutput");
            }
            else
            {
                _statusLabel.Text = "Flow Canvas assets not found.";
                _statusLabel.ForeColor = Color.FromArgb(231, 76, 60);
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try { HandleHostMessage(JObject.Parse(e.WebMessageAsJson)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RunOutputWindow] Message error: {ex.Message}"); }
        }

        internal void HandleHostMessage(JObject msg)
        {
            switch (msg["type"]?.ToString())
            {
                case "ready":
                    _reactReady = true;
                    while (_pendingMessages.TryDequeue(out var pending))
                        _webView.CoreWebView2.PostWebMessageAsJson(pending);
                    SendPersistedPrefs();
                    break;
                case "layout-save":
                    SaveRunOutputPrefs(msg);
                    break;
            }
        }

        public void SendMessage(object message)
        {
            var json = JsonConvert.SerializeObject(message);
            if (InvokeRequired) { BeginInvoke(() => PostOrQueue(json)); return; }
            PostOrQueue(json);
        }

        private void PostOrQueue(string json)
        {
            if (_reactReady && !IsDisposed && _webView.CoreWebView2 != null)
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            else
                _pendingMessages.Enqueue(json);
        }

        public void SendRunOutputAppend(string chunk)
        {
            if (string.IsNullOrEmpty(chunk)) return;
            SendMessage(new { type = "run-output", chunk });
        }

        public void SendRunOutputClear() => SendMessage(new { type = "run-output-clear" });

        /// <summary>Drives the console's LIVE indicator (reuses the canvas run-lifecycle messages).</summary>
        public void SendRunState(bool running) => SendMessage(new { type = running ? "execution-started" : "execution-finished" });

        private void SendPersistedPrefs()
        {
            var ws = _configService?.GetCurrent().WindowState;
            if (ws == null) return;
            SendMessage(new
            {
                type = "layout-restore",
                runOutputColor = ws.FlowCanvasRunOutputColor,
                runOutputWrap = ws.FlowCanvasRunOutputWrap,
                runOutputFollow = ws.FlowCanvasRunOutputFollow,
            });
        }

        private void SaveRunOutputPrefs(JObject msg)
        {
            if (_configService == null) return;
            var color = msg["runOutputColor"]?.Value<bool>();
            var wrap = msg["runOutputWrap"]?.Value<bool>();
            var follow = msg["runOutputFollow"]?.Value<bool>();
            if (color == null && wrap == null && follow == null) return;
            _configService.Update(c =>
            {
                c.WindowState ??= new Models.WindowState();
                if (color.HasValue) c.WindowState.FlowCanvasRunOutputColor = color.Value;
                if (wrap.HasValue) c.WindowState.FlowCanvasRunOutputWrap = wrap.Value;
                if (follow.HasValue) c.WindowState.FlowCanvasRunOutputFollow = follow.Value;
            });
        }

        private Size? GetPersistedSize()
        {
            var ws = _configService?.GetCurrent().WindowState;
            if (ws?.FlowCanvasRunOutputWindowWidth > 0 && ws?.FlowCanvasRunOutputWindowHeight > 0)
                return new Size(ws.FlowCanvasRunOutputWindowWidth.Value, ws.FlowCanvasRunOutputWindowHeight.Value);
            return null;
        }

        private Point? GetPersistedLocation()
        {
            var ws = _configService?.GetCurrent().WindowState;
            if (ws?.FlowCanvasRunOutputWindowLeft is int l && ws?.FlowCanvasRunOutputWindowTop is int t)
                return new Point(l, t);
            return null;
        }

        private void SavePersistedGeometry()
        {
            if (_configService == null || WindowState != FormWindowState.Normal) return;
            _configService.Update(c =>
            {
                c.WindowState ??= new Models.WindowState();
                c.WindowState.FlowCanvasRunOutputWindowLeft = Location.X;
                c.WindowState.FlowCanvasRunOutputWindowTop = Location.Y;
                c.WindowState.FlowCanvasRunOutputWindowWidth = Size.Width;
                c.WindowState.FlowCanvasRunOutputWindowHeight = Size.Height;
            });
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~RunOutputWindowFormTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add UI/RunOutputWindowForm.cs SSH_Helper.Tests/UI/RunOutputWindowFormTests.cs
git commit -m "feat(flow-canvas): RunOutputWindowForm — 2nd WebView2 host for the detached console

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> **CHECKPOINT — Phase 4.** The window form exists and is unit-tested but nothing opens it yet. Stop for review.

---

## Phase 5 — C# wiring

### Task 5.1: FlowCanvasForm open/close-window events

**Files:** Modify `UI/FlowCanvasForm.cs`; Test `SSH_Helper.Tests/UI/FlowCanvasFormRunOutputWindowTests.cs`

- [ ] **Step 1: Write the failing test**

Create `SSH_Helper.Tests/UI/FlowCanvasFormRunOutputWindowTests.cs`:

```csharp
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class FlowCanvasFormRunOutputWindowTests
{
    [WinFormsFact]
    public void OpenAndCloseWindowMessages_RaiseTheirEvents()
    {
        using var form = new FlowCanvasForm(darkMode: false, configService: null);
        var opened = false;
        var closed = false;
        form.OnOpenRunOutputWindow += _ => opened = true;
        form.OnCloseRunOutputWindow += _ => closed = true;

        form.HandleHostMessage(JObject.FromObject(new { type = "open-run-output-window" }));
        form.HandleHostMessage(JObject.FromObject(new { type = "close-run-output-window" }));

        opened.Should().BeTrue();
        closed.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasFormRunOutputWindowTests"`
Expected: FAIL — `OnOpenRunOutputWindow` not defined.

- [ ] **Step 3: Implement**

In `UI/FlowCanvasForm.cs`, add to the events region (next to `OnBrowsePath`):

```csharp
        public event Action<JObject>? OnOpenRunOutputWindow;
        public event Action<JObject>? OnCloseRunOutputWindow;
```

In `HandleHostMessage`'s switch, add two cases (next to `browse-path`):

```csharp
                    case "open-run-output-window":
                        OnOpenRunOutputWindow?.Invoke(msg);
                        break;

                    case "close-run-output-window":
                        OnCloseRunOutputWindow?.Invoke(msg);
                        break;
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasFormRunOutputWindowTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UI/FlowCanvasForm.cs SSH_Helper.Tests/UI/FlowCanvasFormRunOutputWindowTests.cs
git commit -m "feat(flow-canvas): FlowCanvasForm raises open/close-run-output-window events

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 5.2: Form1 owns, opens, feeds the window

**Files:** Modify `Form1.cs` (5 sites). Build-verified + manual (no unit test — needs a Form1 instance).

- [ ] **Step 1: Add the field**

In `Form1.cs`, after the `_flowCanvasForm` field (line ~175), add:

```csharp
        private RunOutputWindowForm? _runOutputWindow;
```

- [ ] **Step 2: Add `OpenRunOutputWindow()`**

Add this method near `OpenFlowCanvas` (mirrors its reuse/create/seed pattern):

```csharp
        private void OpenRunOutputWindow()
        {
            if (_runOutputWindow != null && !_runOutputWindow.IsDisposed)
            {
                _runOutputWindow.BringToFront();
                _runOutputWindow.Activate();
                return;
            }

            var config = _configService.GetCurrent();
            _runOutputWindow = new RunOutputWindowForm(config.DarkMode, _configService);
            _runOutputWindow.FormClosed += (_, _) =>
            {
                _runOutputWindow = null;
                // Tell the canvas to dock the console back into its bottom panel.
                _flowCanvasForm?.SendMessage(new { type = "run-output-window-closed" });
            };
            _runOutputWindow.Show(this);

            // Seed with whatever the main output box currently holds.
            _runOutputWindow.SendRunOutputClear();
            _runOutputWindow.SendRunOutputAppend(GetBufferedOutputSnapshot());
        }
```

- [ ] **Step 3: Wire the canvas open/close events**

In `OpenFlowCanvas()`, in the event-wiring block (after the `OnBrowsePath` wiring, ~line 6772), add:

```csharp
            _flowCanvasForm.OnOpenRunOutputWindow += (_) =>
            {
                BeginInvoke(() => OpenRunOutputWindow());
            };

            _flowCanvasForm.OnCloseRunOutputWindow += (_) =>
            {
                BeginInvoke(() => _runOutputWindow?.Close());
            };
```

- [ ] **Step 4: Fan output + clear into the window**

In `AppendOutputToUi` (line ~13933), after `_flowCanvasForm?.SendRunOutputAppend(output);` add:

```csharp
            _runOutputWindow?.SendRunOutputAppend(output);
```

In `ClearOutput` (line ~14020), after `_flowCanvasForm?.SendRunOutputClear();` add:

```csharp
            _runOutputWindow?.SendRunOutputClear();
```

- [ ] **Step 5: Forward run state (LIVE dot)**

In `ExecutePresetOnRowsAsync`, immediately AFTER the `if (_flowCanvasForm != null) { ... SendMessage(new { type = "execution-started" }); }` block closes (i.e., unconditionally, before `ClearOutput()` at ~line 12326), add:

```csharp
            _runOutputWindow?.SendRunState(true);
```

In `SshService_ExecutionCompleted` (line ~13551), after the `execution-finished` send, add:

```csharp
            _runOutputWindow?.SendRunState(false);
```

In `ExecuteCanvasRun`'s no-host early-return (line ~12655, after the `execution-finished, success=false` send), add:

```csharp
                _runOutputWindow?.SendRunState(false);
```

- [ ] **Step 6: Build**

Run: `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add Form1.cs
git commit -m "feat(flow-canvas): Form1 owns/opens/feeds the detachable Run Output window

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 5.3: Full build + manual verification

- [ ] **Step 1: Full solution build (embeds the React bundle)**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded (runs `npm run build` then .NET). 0 errors. (Ensure the running app is closed first to avoid a DLL lock.)

- [ ] **Step 2: Manual end-to-end**

Run: `dotnet run --project SSH_Helper.csproj`. Verify:
1. Open the canvas, click **Pop out** on the Run Output console → a separate **Run Output** window opens; the dock switches to **Block Output**.
2. Drag the window to a **second monitor** — it moves freely (it's a real OS window).
3. Run a script → output streams into the window (banners, errors, blank lines); the LIVE dot shows during the run; Color/Find/Follow/Wrap all work.
4. Toggle Color in the window → reopen later → the pref persisted.
5. Click the **Run Output** tab in the dock → the window closes and the console returns to the dock.
6. Re-pop, then close the window via its **X** → the dock shows the Run tab again.
7. Close the canvas while the window is open → the window stays and keeps receiving output.
8. Resize the window + move it, close & reopen → geometry restored.

- [ ] **Step 3: Note any defects, loop back to the relevant task. When clean, the feature is done.**

> **CHECKPOINT — Phase 5.** Feature complete end-to-end.

---

## Final verification

- [ ] `cd FlowCanvas && npm test` — full React suite green.
- [ ] `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~RunOutputWindow"` and `--filter "FullyQualifiedName~FlowCanvasFormRunOutputWindow"` — C# window tests green.
- [ ] `dotnet build SSH_Helper.sln` — full build succeeds.
- [ ] Manual checklist (Task 5.3) passes, including the second-monitor drag.

## Risks / trade-offs
- **Separate WebView2 user-data folder** (`WebView2/RunOutputWindow`) → a separate browser process from the canvas: simplest and avoids lock contention, at the cost of more memory than sharing one environment. Acceptable for a single optional window.
- **Two stores (canvas + window)** each hold their own `runOutput`; both are fed the same stream from Form1 and the window seeds on open, so they stay in sync.
- **Run-state forwarding** to the window is best-effort (drives the LIVE dot); if a run is already in progress when the window opens, the dot syncs on the next run-lifecycle event. Acceptable.
- **Pref last-writer-wins** across the dock and the window (both write the same `WindowState` booleans) — fine for booleans.
