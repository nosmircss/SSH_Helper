# Flow Canvas Execution Path Highlight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After (and during) a run in the Flow Canvas, persistently highlight the connectors the execution traversed — the taken branch glows, untaken branches fade — with a toolbar "Clear Path" control.

**Architecture:** Pure React/presentation change. A memoized selector classifies each edge as `on-path` / `untaken` / `idle` from store state that already persists after a run (`blockStates`, `branchTaken`, `loopIterations`). One new boolean flag (`pathVisible`) gates the selector so "Clear Path" can hide the trail without disturbing node badges. `AnimatedEdge` (the single renderer for all edges) applies the style, decoupled from `isRunning`. No C# changes; nothing leaks into YAML/graph export.

**Tech Stack:** React 19, TypeScript, Zustand 5, @xyflow/react 12, Vite 8, Vitest 4 (jsdom), Playwright 1.58. Token layer is OKLCH CSS custom properties in `src/styles/tokens.css`.

**Reference (read before starting):** `docs/superpowers/specs/2026-05-31-flow-canvas-execution-path-highlight-design.md`

**Working directory for all `npm` commands:** `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper\FlowCanvas`

---

## Key facts the engineer needs

- **The store already has the data.** `messageBridge.ts` handles the `execution-update` host message and calls `setBlockState`, `setBranchTaken(stepId, branchTaken)`, `setLoopIteration`. These maps are **not** cleared on `execution-finished` — only on a new run (`execution-started` → `clearExecution()`) or graph load. So the path persists for free; the only reason edges currently revert is that their style is gated on `isRunning`.
- **`branchTaken` map values are the raw C# scope keys**: `'then'`, `'else'`, `'elif/0/then'`, `'cases/2/do'`, `'default'`. These match an edge's `edge.data.branchPath` exactly (set by `getBranchVisual`/`inferDefaultBranchMetadata` in `graphSlice.ts`). The `else` branch additionally carries `sourceHandle === 'false'`.
- **`BaseEdge` forwards `className`** onto the path: it renders `<path className={cc(['react-flow__edge-path', props.className])} …>` (verified in installed `@xyflow/react`). So a `className` prop becomes a class on the `.react-flow__edge-path` element — assertable in both jsdom and Playwright.
- **`START_NODE_ID`** is exported from `src/stores/slices/graphSlice.ts` (`'__start__'`). The start node never gets an exec state.
- **Tests:** `npm test` runs Vitest (`vitest run`). `npm run test:e2e` runs Playwright. jsdom cannot compute `color-mix`/`var()`, so unit/component tests assert at the **string / class** level; the e2e (real Chromium) can assert computed `opacity`.

---

## File structure

| File | Responsibility | Action |
|------|----------------|--------|
| `src/stores/selectors/edgePath.ts` | Pure `selectEdgePathStatus(state, edgeId)` classifier | **Create** |
| `src/stores/selectors/__tests__/edgePath.test.ts` | Unit tests for the classifier | **Create** |
| `src/stores/slices/executionSlice.ts` | Add `pathVisible` flag + `setPathVisible`/`clearPath`; reset on run start | **Modify** |
| `src/stores/slices/__tests__/executionSlice.pathVisible.test.ts` | Unit tests for the flag | **Create** |
| `src/styles/tokens.css` | `--fc-edge-traversed` + glow token | **Modify** |
| `src/nodes/animatededge.css` | `.fc-edge-onpath` / `.fc-edge-untaken` styles | **Modify** |
| `src/nodes/AnimatedEdge.tsx` | Apply path status styling, decoupled from `isRunning` | **Modify** |
| `src/nodes/__tests__/AnimatedEdge.test.tsx` | Component integration test | **Create** |
| `src/panels/Toolbar.tsx` | "Clear Path" button | **Modify** |
| `e2e/fixtures/graphs.ts` | `createBranchPathFixture()` (if + then + else) | **Modify** |
| `e2e/flow-canvas-execution-path.spec.ts` | End-to-end path highlight + clear + parity | **Create** |

---

## Task 1: Edge path-status selector

**Files:**
- Create: `src/stores/selectors/edgePath.ts`
- Test: `src/stores/selectors/__tests__/edgePath.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/stores/selectors/__tests__/edgePath.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import type { Edge, Node } from '@xyflow/react';
import { selectEdgePathStatus } from '../edgePath';
import { START_NODE_ID } from '../../slices/graphSlice';

// Minimal stand-in for the FlowStore: the selector only reads these fields.
function makeState(overrides: {
  pathVisible?: boolean;
  edges?: Edge[];
  nodes?: Node[];
  blockStates?: Map<string, string>;
  branchTaken?: Map<string, string>;
  loopIterations?: Map<string, number>;
}): any {
  return {
    pathVisible: overrides.pathVisible ?? true,
    edges: overrides.edges ?? [],
    nodes: overrides.nodes ?? [],
    blockStates: overrides.blockStates ?? new Map(),
    branchTaken: overrides.branchTaken ?? new Map(),
    loopIterations: overrides.loopIterations ?? new Map(),
  };
}

const ifNode: Node = { id: 'if-1', position: { x: 0, y: 0 }, data: { blockType: 'if' } } as Node;
const loopNode: Node = { id: 'loop-1', position: { x: 0, y: 0 }, data: { blockType: 'foreach' } } as Node;
const parallelNode: Node = { id: 'par-1', position: { x: 0, y: 0 }, data: { blockType: 'parallel' } } as Node;

describe('selectEdgePathStatus', () => {
  it('returns idle for every edge when pathVisible is false', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    const state = makeState({ pathVisible: false, edges, blockStates: new Map([['a', 'success']]) });
    expect(selectEdgePathStatus(state, 'e1')).toBe('idle');
  });

  it('returns idle when the edge id is unknown', () => {
    expect(selectEdgePathStatus(makeState({}), 'nope')).toBe('idle');
  });

  it('returns idle when the source never ran', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    expect(selectEdgePathStatus(makeState({ edges }), 'e1')).toBe('idle');
  });

  it('marks a plain successor on-path when the source completed', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    const state = makeState({ edges, blockStates: new Map([['a', 'success']]) });
    expect(selectEdgePathStatus(state, 'e1')).toBe('on-path');
  });

  it('does NOT mark a successor on-path while the source is still running', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    const state = makeState({ edges, blockStates: new Map([['a', 'running']]) });
    expect(selectEdgePathStatus(state, 'e1')).toBe('idle');
  });

  it('does NOT mark a successor on-path when the source errored (trail halts)', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    const state = makeState({ edges, blockStates: new Map([['a', 'error']]) });
    expect(selectEdgePathStatus(state, 'e1')).toBe('idle');
  });

  it('treats a skipped (disabled) source as pass-through', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    const state = makeState({ edges, blockStates: new Map([['a', 'skipped']]) });
    expect(selectEdgePathStatus(state, 'e1')).toBe('on-path');
  });

  it('lights the taken if-branch and fades the untaken sibling', () => {
    const edges: Edge[] = [
      { id: 'then', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge,
      { id: 'else', source: 'if-1', target: 'e', sourceHandle: 'false', data: { branchPath: 'else' } } as Edge,
    ];
    const state = makeState({
      edges, nodes: [ifNode],
      blockStates: new Map([['if-1', 'success']]),
      branchTaken: new Map([['if-1', 'then']]),
    });
    expect(selectEdgePathStatus(state, 'then')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'else')).toBe('untaken');
  });

  it('matches the else branch by its branchPath value', () => {
    const edges: Edge[] = [
      { id: 'then', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge,
      { id: 'else', source: 'if-1', target: 'e', sourceHandle: 'false', data: { branchPath: 'else' } } as Edge,
    ];
    const state = makeState({
      edges, nodes: [ifNode],
      blockStates: new Map([['if-1', 'success']]),
      branchTaken: new Map([['if-1', 'else']]),
    });
    expect(selectEdgePathStatus(state, 'else')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'then')).toBe('untaken');
  });

  it('matches an indexed switch case', () => {
    const edges: Edge[] = [
      { id: 'c0', source: 'if-1', target: 'a', data: { branchPath: 'cases/0/do' } } as Edge,
      { id: 'c2', source: 'if-1', target: 'b', data: { branchPath: 'cases/2/do' } } as Edge,
    ];
    const state = makeState({
      edges, nodes: [{ id: 'if-1', position: { x: 0, y: 0 }, data: { blockType: 'switch' } } as Node],
      blockStates: new Map([['if-1', 'success']]),
      branchTaken: new Map([['if-1', 'cases/2/do']]),
    });
    expect(selectEdgePathStatus(state, 'c2')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'c0')).toBe('untaken');
  });

  it('returns idle for a branch edge when no branchTaken was recorded (does not guess)', () => {
    const edges: Edge[] = [{ id: 'then', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge];
    const state = makeState({ edges, nodes: [ifNode], blockStates: new Map([['if-1', 'success']]) });
    expect(selectEdgePathStatus(state, 'then')).toBe('idle');
  });

  it('returns idle for branch edges when the conditional itself errored', () => {
    const edges: Edge[] = [{ id: 'then', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge];
    const state = makeState({
      edges, nodes: [ifNode],
      blockStates: new Map([['if-1', 'error']]),
      branchTaken: new Map([['if-1', 'then']]),
    });
    expect(selectEdgePathStatus(state, 'then')).toBe('idle');
  });

  it('lights the loop body once the loop iterated, fades it otherwise', () => {
    const edges: Edge[] = [{ id: 'body', source: 'loop-1', target: 'x', data: { branchPath: 'do' } } as Edge];
    const ran = makeState({
      edges, nodes: [loopNode],
      blockStates: new Map([['loop-1', 'success']]),
      loopIterations: new Map([['loop-1', 3]]),
    });
    const zero = makeState({
      edges, nodes: [loopNode],
      blockStates: new Map([['loop-1', 'success']]),
      loopIterations: new Map([['loop-1', 0]]),
    });
    expect(selectEdgePathStatus(ran, 'body')).toBe('on-path');
    expect(selectEdgePathStatus(zero, 'body')).toBe('untaken');
  });

  it('lights every parallel branch (no untaken among them)', () => {
    const edges: Edge[] = [
      { id: 'p0', source: 'par-1', target: 'a', data: { branchPath: 'parallel/0' } } as Edge,
      { id: 'p1', source: 'par-1', target: 'b', data: { branchPath: 'parallel/1' } } as Edge,
    ];
    const state = makeState({ edges, nodes: [parallelNode], blockStates: new Map([['par-1', 'success']]) });
    expect(selectEdgePathStatus(state, 'p0')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'p1')).toBe('on-path');
  });

  it('treats a container continuation edge as a plain successor', () => {
    const edges: Edge[] = [{ id: 'cont', source: 'if-1', target: 'after', sourceHandle: 'continue' } as Edge];
    const state = makeState({ edges, nodes: [ifNode], blockStates: new Map([['if-1', 'success']]) });
    expect(selectEdgePathStatus(state, 'cont')).toBe('on-path');
  });

  it('lights the start edge once its target block has run', () => {
    const edges: Edge[] = [{ id: 's', source: START_NODE_ID, target: 'first' } as Edge];
    const ran = makeState({ edges, blockStates: new Map([['first', 'running']]) });
    const notRun = makeState({ edges });
    expect(selectEdgePathStatus(ran, 's')).toBe('on-path');
    expect(selectEdgePathStatus(notRun, 's')).toBe('idle');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- edgePath`
Expected: FAIL — `Cannot find module '../edgePath'` (file does not exist yet).

- [ ] **Step 3: Write the selector**

Create `src/stores/selectors/edgePath.ts`:

```ts
import type { Edge } from '@xyflow/react';
import type { FlowStore } from '../useFlowStore';
import { START_NODE_ID } from '../slices/graphSlice';

export type EdgePathStatus = 'on-path' | 'untaken' | 'idle';

// Source states from which control flows onward to a plain successor.
// 'error' halts the trail; 'running' has not completed yet.
const PASS_THROUGH = new Set(['success', 'skipped', 'disabled']);

function branchPathOf(edge: Edge): string | undefined {
  const data = (edge.data ?? {}) as Record<string, unknown>;
  const value = data.branchPath;
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

/**
 * Classify an edge against the last/current run: 'on-path' (traversed), 'untaken'
 * (a sibling branch that did not fire — faded), or 'idle' (never reached / hidden).
 *
 * Derived from state that already persists after a run, so the result survives
 * `execution-finished`. `pathVisible` is the only gate the "Clear Path" control flips.
 * Reads only transient exec maps — never node/edge persisted data — so export is unaffected.
 */
export function selectEdgePathStatus(state: FlowStore, edgeId: string): EdgePathStatus {
  if (!state.pathVisible) return 'idle';

  const edge = state.edges.find((e) => e.id === edgeId);
  if (!edge) return 'idle';

  // The Start node never receives an exec state; its outgoing edge is traversed once
  // the block it points at has entered execution.
  if (edge.source === START_NODE_ID) {
    const targetState = state.blockStates.get(edge.target);
    return targetState && targetState !== 'idle' ? 'on-path' : 'idle';
  }

  const sourceState = state.blockStates.get(edge.source);
  if (!sourceState || sourceState === 'idle' || sourceState === 'running') return 'idle';

  const branchPath = branchPathOf(edge);
  const isBranch = !!branchPath && edge.sourceHandle !== 'continue';

  if (!isBranch) {
    // Plain successor / container continuation: traversed only if the source completed
    // cleanly (or was skipped/disabled). A failed source halts the trail here.
    return PASS_THROUGH.has(sourceState) ? 'on-path' : 'idle';
  }

  // Branch edge of a container block.
  if (sourceState === 'error') return 'idle'; // conditional failed before it branched

  const sourceNode = state.nodes.find((n) => n.id === edge.source);
  const sourceData = (sourceNode?.data ?? {}) as Record<string, unknown>;
  const blockType = typeof sourceData.blockType === 'string' ? sourceData.blockType : undefined;

  // Parallel fans out to every branch — all of them are on the path.
  if (blockType === 'parallel') return 'on-path';

  // A loop body ('do') is on-path once the loop iterated at least once.
  if ((blockType === 'foreach' || blockType === 'while') && branchPath === 'do') {
    return (state.loopIterations.get(edge.source) ?? 0) > 0 ? 'on-path' : 'untaken';
  }

  // Conditional (if / switch / try): compare against the recorded taken branch.
  const taken = state.branchTaken.get(edge.source);
  if (!taken) return 'idle'; // no branch signal — don't guess
  const matches = branchPath === taken || edge.sourceHandle === taken;
  return matches ? 'on-path' : 'untaken';
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- edgePath`
Expected: PASS — all `selectEdgePathStatus` cases green.

- [ ] **Step 5: Commit**

```bash
git add src/stores/selectors/edgePath.ts src/stores/selectors/__tests__/edgePath.test.ts
git commit -m "feat(flow-canvas): add execution edge path-status selector"
```

---

## Task 2: `pathVisible` flag + clear/reset

**Files:**
- Modify: `src/stores/slices/executionSlice.ts`
- Test: `src/stores/slices/__tests__/executionSlice.pathVisible.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/stores/slices/__tests__/executionSlice.pathVisible.test.ts`:

```ts
import { describe, it, expect, beforeEach } from 'vitest';
import { useFlowStore } from '../../useFlowStore';

describe('execution path visibility', () => {
  beforeEach(() => {
    useFlowStore.setState({ pathVisible: true });
    useFlowStore.getState().clearExecution();
  });

  it('defaults to visible', () => {
    expect(useFlowStore.getState().pathVisible).toBe(true);
  });

  it('clearPath hides the path', () => {
    useFlowStore.getState().clearPath();
    expect(useFlowStore.getState().pathVisible).toBe(false);
  });

  it('setPathVisible toggles the flag', () => {
    useFlowStore.getState().setPathVisible(false);
    expect(useFlowStore.getState().pathVisible).toBe(false);
    useFlowStore.getState().setPathVisible(true);
    expect(useFlowStore.getState().pathVisible).toBe(true);
  });

  it('clearExecution (a fresh run) re-shows the path', () => {
    useFlowStore.getState().clearPath();
    expect(useFlowStore.getState().pathVisible).toBe(false);
    useFlowStore.getState().clearExecution();
    expect(useFlowStore.getState().pathVisible).toBe(true);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- executionSlice.pathVisible`
Expected: FAIL — `clearPath is not a function` / `pathVisible` is `undefined`.

- [ ] **Step 3: Add the flag and actions**

In `src/stores/slices/executionSlice.ts`:

Add to the `ExecutionSlice` interface — after the `dataBlockTestResults: Map<...>;` state line (around line 27):

```ts
  /** When false, the "Clear Path" control hides the edge highlight without touching node badges. */
  pathVisible: boolean;
```

Add to the `ExecutionSlice` interface actions — after `setBranchTaken: (id: string, key: string) => void;` (around line 34):

```ts
  setPathVisible: (visible: boolean) => void;
  clearPath: () => void;
```

In the slice creator, add the initial value — after `isRunning: false,` (around line 42):

```ts
  pathVisible: true,
```

Add the action implementations — after the `setBranchTaken` implementation closes (after line 98, before `clearExecution`):

```ts
  setPathVisible: (visible) => set({ pathVisible: visible }),

  // Clear Path: hide the edge highlight only. Node blockStates/badges are untouched.
  clearPath: () => set({ pathVisible: false }),
```

In `clearExecution`, add `pathVisible: true` to the first `set({...})` call so each fresh run re-shows the path. Change:

```ts
  clearExecution: () => {
    set({
      blockStates: new Map(),
      blockOutputs: new Map(),
      blockTimings: new Map(),
      loopIterations: new Map(),
      branchTaken: new Map(),
      dataBlockTestResults: new Map(),
    });
```

to:

```ts
  clearExecution: () => {
    set({
      blockStates: new Map(),
      blockOutputs: new Map(),
      blockTimings: new Map(),
      loopIterations: new Map(),
      branchTaken: new Map(),
      dataBlockTestResults: new Map(),
      pathVisible: true,
    });
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- executionSlice.pathVisible`
Expected: PASS — all four cases green.

- [ ] **Step 5: Commit**

```bash
git add src/stores/slices/executionSlice.ts src/stores/slices/__tests__/executionSlice.pathVisible.test.ts
git commit -m "feat(flow-canvas): add pathVisible flag with clearPath and run-start reset"
```

---

## Task 3: Tokens + edge CSS classes

**Files:**
- Modify: `src/styles/tokens.css`
- Modify: `src/nodes/animatededge.css`

No unit test — these are pure presentation values. They are exercised by the component test (Task 4, class names) and the e2e (Task 6, computed `opacity`). jsdom cannot compute `var()`/`color-mix`/`drop-shadow`, so there is nothing meaningful to assert here in Vitest.

- [ ] **Step 1: Add the token**

In `src/styles/tokens.css`, immediately after the `--fc-edge-packet` line (around line 169):

```css
  --fc-edge-packet: oklch(95% 0.03 200);
  /* ── Execution path highlight (persists after a run; see execution-path design doc) ──
     Cyan trail (hue 200, harmonizing with the bright packet above), distinct from idle grey
     and from success-green so a traversed wire reads as "traveled", not "succeeded". */
  --fc-edge-traversed: oklch(70% 0.13 200);
  --fc-edge-traversed-glow: oklch(70% 0.13 200 / 0.5);
```

- [ ] **Step 2: Add the edge classes**

Append to `src/nodes/animatededge.css`:

```css

/* Execution-path overlay (Wave 2b → execution path). Static styling only — no animation — so
 * it is safe under reduced motion and persists after the run finishes. on-path = soft glow on
 * top of the (full-strength) stroke; untaken = a branch that did not fire, faded back. */
.fc-edge-onpath {
  filter: drop-shadow(0 0 2px var(--fc-edge-traversed-glow));
}
.fc-edge-untaken {
  opacity: 0.35;
}
```

- [ ] **Step 3: Verify the build still type-checks / bundles**

Run: `npm run build`
Expected: PASS — `tsc` and `vite build` complete with no errors (CSS is valid; tokens resolve).

- [ ] **Step 4: Commit**

```bash
git add src/styles/tokens.css src/nodes/animatededge.css
git commit -m "feat(flow-canvas): add traversed edge token and path overlay CSS classes"
```

---

## Task 4: Wire path styling into AnimatedEdge

**Files:**
- Modify: `src/nodes/AnimatedEdge.tsx`
- Test: `src/nodes/__tests__/AnimatedEdge.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/nodes/__tests__/AnimatedEdge.test.tsx`:

```tsx
import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import React from 'react';
import type { Edge, Node } from '@xyflow/react';
import AnimatedEdge from '../AnimatedEdge';
import { useFlowStore } from '../../stores/useFlowStore';

// AnimatedEdge reads the real store and renders the real BaseEdge (a pure path — no
// ReactFlow context needed). We drive state via setState and read the rendered path.
// NOTE: jsdom does not reliably store `var(...)`/`color-mix(...)` on the SVG `stroke`
// property, so we assert the CLASS attribute (a plain DOM attribute — 100% reliable) here
// and verify the resolved stroke COLOR in the Playwright e2e (real Chromium).
const baseProps = {
  id: 'e1',
  sourceX: 0, sourceY: 0, targetX: 0, targetY: 100,
  sourcePosition: 'bottom', targetPosition: 'top',
  source: 'node-1', target: 'node-2',
  data: {},
  interactionWidth: 20,
} as any;

function setStore(partial: Record<string, unknown>) {
  useFlowStore.setState({
    pathVisible: true,
    isRunning: false,
    reducedMotion: false,
    blockStates: new Map(),
    branchTaken: new Map(),
    loopIterations: new Map(),
    nodes: [],
    edges: [],
    ...partial,
  } as any);
}

function renderEdge(props: any) {
  return render(React.createElement('svg', {}, React.createElement(AnimatedEdge, props)));
}

function pathClass(container: HTMLElement): string {
  return container.querySelector('.react-flow__edge-path')?.getAttribute('class') ?? '';
}

describe('AnimatedEdge path overlay', () => {
  beforeEach(() => setStore({}));

  it('marks a traversed successor edge on-path', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, blockStates: new Map([['node-1', 'success']]) });

    const { container } = renderEdge({ ...baseProps, style: { stroke: 'var(--fc-edge-idle)' } });
    expect(pathClass(container)).toContain('fc-edge-onpath');
  });

  it('marks an on-path branch edge', () => {
    const edges: Edge[] = [
      { id: 'e1', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge,
    ];
    const nodes: Node[] = [{ id: 'if-1', position: { x: 0, y: 0 }, data: { blockType: 'if' } } as Node];
    setStore({ edges, nodes, blockStates: new Map([['if-1', 'success']]), branchTaken: new Map([['if-1', 'then']]) });

    const { container } = renderEdge({ ...baseProps, source: 'if-1', target: 't', style: { stroke: 'var(--fc-branch-then)' } });
    expect(pathClass(container)).toContain('fc-edge-onpath');
  });

  it('fades an untaken branch edge', () => {
    const edges: Edge[] = [
      { id: 'e1', source: 'if-1', target: 'e', sourceHandle: 'false', data: { branchPath: 'else' } } as Edge,
    ];
    const nodes: Node[] = [{ id: 'if-1', position: { x: 0, y: 0 }, data: { blockType: 'if' } } as Node];
    setStore({ edges, nodes, blockStates: new Map([['if-1', 'success']]), branchTaken: new Map([['if-1', 'then']]) });

    const { container } = renderEdge({ ...baseProps, source: 'if-1', target: 'e', style: { stroke: 'var(--fc-branch-else)' } });
    expect(pathClass(container)).toContain('fc-edge-untaken');
  });

  it('leaves an idle edge unstyled when its source has not run', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, blockStates: new Map() });

    const { container } = renderEdge({ ...baseProps });
    expect(pathClass(container)).not.toContain('fc-edge-onpath');
    expect(pathClass(container)).not.toContain('fc-edge-untaken');
  });

  it('hides the path styling when pathVisible is false', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, blockStates: new Map([['node-1', 'success']]), pathVisible: false });

    const { container } = renderEdge({ ...baseProps, style: { stroke: 'var(--fc-edge-idle)' } });
    expect(pathClass(container)).not.toContain('fc-edge-onpath');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- AnimatedEdge`
Expected: FAIL — the rendered path has no `fc-edge-onpath`/`fc-edge-untaken` class (current behavior renders only the gradient, with no path overlay class).

- [ ] **Step 3: Apply the path styling**

Replace the entire contents of `src/nodes/AnimatedEdge.tsx` with:

```tsx
import { memo } from 'react';
import { BaseEdge, getSmoothStepPath, getStraightPath, type EdgeProps } from '@xyflow/react';
import { mix } from '../utils/tokens';
import { markerIdForStroke } from './EdgeMarkers';
import { useFlowStore } from '../stores/useFlowStore';
import { selectEdgePathStatus } from '../stores/selectors/edgePath';
import './animatededge.css';

function AnimatedEdge(props: EdgeProps) {
  const { id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, source, style } = props;

  const isRunning = useFlowStore((s) => s.isRunning);
  const blockStates = useFlowStore((s) => s.blockStates);
  const reducedMotion = useFlowStore((s) => s.reducedMotion);
  // Path overlay status. Returns a string → referentially stable, no extra renders.
  const pathStatus = useFlowStore((s) => selectEdgePathStatus(s, id));

  // Geometry (not data.branchPath / sourceHandle) is the discriminator: imported branch edges
  // carry no branchPath, so metadata would misclassify them. Aligned, downward edges (the
  // continuation spine) get a literal straight line so the run packet glides cleanly; X-offset
  // edges (branch/loop corridors — IF "false", container "continue", branch-first) keep
  // smoothstep so they route orthogonally around child blocks. See design doc.
  // ALIGN_EPS: flow coords are integers, so centered equal-width handles compute dx==0 exactly;
  // 0.5 absorbs sub-pixel float drift and never catches a real corridor (smallest offset ~70px).
  const ALIGN_EPS = 0.5;
  const isSpine = Math.abs(sourceX - targetX) < ALIGN_EPS && targetY > sourceY;
  const [edgePath] = isSpine
    ? getStraightPath({ sourceX, sourceY, targetX, targetY })
    : getSmoothStepPath({
        sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, borderRadius: 8,
      });

  // Color comes from the edge's style.stroke (set by getBranchVisual / defaultEdgeOptions /
  // selection). Branch edges = --fc-branch-*, continuation = --fc-accent, plain = --fc-edge-idle.
  const color = (typeof style?.stroke === 'string' ? style.stroke : undefined) ?? 'var(--fc-edge-idle)';
  const markerId = markerIdForStroke(color);

  const sourceState = blockStates.get(source);
  const active = isRunning && (sourceState === 'success' || sourceState === 'running');

  const gradientId = `fc-grad-${id}`;

  // ── Execution-path overlay (persists after the run; decoupled from isRunning) ──
  // on-path: full-strength stroke + soft glow. Idle-grey spine edges promote to the traversed
  //   token so a traveled wire actually reads as lit; branch edges keep their branch color.
  // untaken: a branch that did not fire — faded via the .fc-edge-untaken class.
  const onPath = pathStatus === 'on-path';
  const untaken = pathStatus === 'untaken';
  const onPathStroke = color === 'var(--fc-edge-idle)' ? 'var(--fc-edge-traversed)' : color;

  let stroke: string;
  let strokeWidth: number;
  let edgeClass: string | undefined;
  if (onPath) {
    stroke = onPathStroke;
    strokeWidth = typeof style?.strokeWidth === 'number' ? style.strokeWidth : 3;
    edgeClass = 'fc-edge-onpath';
  } else if (untaken) {
    stroke = color;
    strokeWidth = typeof style?.strokeWidth === 'number' ? style.strokeWidth : 1.5;
    edgeClass = 'fc-edge-untaken';
  } else {
    // Idle: existing behavior — dim→full gradient, widening while the source is active.
    stroke = `url(#${gradientId})`;
    strokeWidth = typeof style?.strokeWidth === 'number' ? style.strokeWidth : active ? 2.5 : 2;
    edgeClass = undefined;
  }

  return (
    <>
      <defs>
        {/* userSpaceOnUse so the gradient orients along the actual edge; dim→full toward target. */}
        <linearGradient id={gradientId} gradientUnits="userSpaceOnUse" x1={sourceX} y1={sourceY} x2={targetX} y2={targetY}>
          <stop offset="0%" stopColor={mix(color, 30)} />
          <stop offset="100%" stopColor={color} />
        </linearGradient>
      </defs>
      <BaseEdge
        id={id}
        className={edgeClass}
        path={edgePath}
        markerEnd={`url(#${markerId})`}
        style={{ ...style, stroke, strokeWidth }}
      />
      {active && !reducedMotion && (
        <circle
          className="fc-edge-packet"
          r={4}
          cx={0}
          cy={0}
          fill="var(--fc-edge-packet)"
          filter="url(#fc-packet-glow)"
          style={{ offsetPath: `path('${edgePath}')` }}
        />
      )}
    </>
  );
}

export default memo(AnimatedEdge);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- AnimatedEdge`
Expected: PASS — on-path / branch-color / untaken / idle / hidden cases all green.

- [ ] **Step 5: Run the full unit suite (no regressions)**

Run: `npm test`
Expected: PASS — all existing tests plus the three new files green.

- [ ] **Step 6: Commit**

```bash
git add src/nodes/AnimatedEdge.tsx src/nodes/__tests__/AnimatedEdge.test.tsx
git commit -m "feat(flow-canvas): render persistent execution path on edges"
```

---

## Task 5: "Clear Path" toolbar button

**Files:**
- Modify: `src/panels/Toolbar.tsx`

(No new unit test — the button is covered by the e2e in Task 6. It is a thin wiring of an already-tested store action.)

- [ ] **Step 1: Subscribe to the path state**

In `src/panels/Toolbar.tsx`, add these three subscriptions next to the other execution-related ones (after `const debugAction = useFlowStore((s) => s.debugAction);`, around line 34):

```ts
  const blockStates = useFlowStore((s) => s.blockStates);
  const pathVisible = useFlowStore((s) => s.pathVisible);
  const clearPath = useFlowStore((s) => s.clearPath);
  const hasPath = pathVisible && blockStates.size > 0;
```

- [ ] **Step 2: Add the button**

In the "Canvas controls" cluster, immediately after the `▭ Bands` button closes (after line 240, before the `⚠ Problems` button), insert:

```tsx
      <button
        onClick={clearPath}
        disabled={!hasPath}
        style={btnStyle('var(--fc-text-secondary)', hasPath)}
        title="Clear the highlighted execution path (block results stay)"
      >
        ⌫ Clear Path
      </button>
```

- [ ] **Step 3: Verify the build type-checks**

Run: `npm run build`
Expected: PASS — `tsc` clean (no unused vars; `clearPath`/`blockStates`/`pathVisible` all consumed).

- [ ] **Step 4: Commit**

```bash
git add src/panels/Toolbar.tsx
git commit -m "feat(flow-canvas): add Clear Path toolbar control"
```

---

## Task 6: End-to-end coverage

**Files:**
- Modify: `e2e/fixtures/graphs.ts`
- Create: `e2e/flow-canvas-execution-path.spec.ts`

- [ ] **Step 1: Add a branch fixture**

Append to `e2e/fixtures/graphs.ts` (before the final closing — it is a flat list of exported functions):

```ts
// If with both a then-branch and an else-branch, so a run can light the taken branch and
// fade the untaken one. Edge metadata mirrors what the importer/onConnect produce
// (branchPath + the if "false" sourceHandle on the else edge).
export function createBranchPathFixture(): GraphFixture {
  return {
    nodes: [
      { id: '__start__', type: 'start', position: { x: 80, y: 20 }, data: { blockType: '_start', label: 'Start', props: {} } },
      { id: 'if-1', type: 'block', position: { x: 80, y: 160 }, data: { blockType: 'if', label: 'If', props: { condition: '${enabled}' } } },
      { id: 'then-1', type: 'block', position: { x: 40, y: 320 }, data: { blockType: 'print', label: 'Then', props: { _isChildOf: 'if-1', _branchLabel: 'then', message: 'then-branch' } } },
      { id: 'else-1', type: 'block', position: { x: 360, y: 320 }, data: { blockType: 'print', label: 'Else', props: { _isChildOf: 'if-1', _branchLabel: 'else', message: 'else-branch' } } },
    ],
    edges: [
      { id: 'edge-start-if', source: '__start__', target: 'if-1' },
      // Branch edges carry the branch color in `style.stroke` (what getBranchVisual writes on
      // import), so the on-path overlay keeps the branch hue rather than the spine token.
      { id: 'edge-if-then', source: 'if-1', target: 'then-1', sourceHandle: 'true', data: { branchPath: 'then' }, style: { stroke: 'var(--fc-branch-then)' } },
      { id: 'edge-if-else', source: 'if-1', target: 'else-1', sourceHandle: 'false', data: { branchPath: 'else' }, style: { stroke: 'var(--fc-branch-else)' } },
    ],
  };
}
```

- [ ] **Step 2: Write the e2e spec**

Create `e2e/flow-canvas-execution-path.spec.ts`:

```ts
import { expect, test, type Locator, type Page } from '@playwright/test';
import { createBranchPathFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  getGraphSnapshot,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

function edgePath(page: Page, edgeId: string): Locator {
  return page.locator(`.react-flow__edge[data-id="${edgeId}"] .react-flow__edge-path`);
}
function nodeById(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"]`);
}

// Resolve a --fc-* token to Chromium's serialized <color>, so we can compare it to a path's
// computed `stroke` (both go through the same color serialization).
async function resolveVar(page: Page, name: string): Promise<string> {
  return page.evaluate((varName) => {
    const probe = document.createElement('div');
    probe.style.color = `var(${varName})`;
    document.body.appendChild(probe);
    const value = getComputedStyle(probe).color;
    probe.remove();
    return value;
  }, name);
}
async function strokeOf(page: Page, edgeId: string): Promise<string> {
  return edgePath(page, edgeId).evaluate((el) => getComputedStyle(el as Element).stroke);
}

// Drive a full run that takes the THEN branch, entirely via host messages (no SSH).
async function runThenBranch(page: Page): Promise<void> {
  await postHostMessage(page, { type: 'execution-started' });
  await postHostMessage(page, { type: 'execution-update', stepId: 'if-1', state: 'running' });
  await postHostMessage(page, { type: 'execution-update', stepId: 'if-1', state: 'success', duration: 10, branchTaken: 'then' });
  await postHostMessage(page, { type: 'execution-update', stepId: 'then-1', state: 'running' });
  await postHostMessage(page, { type: 'execution-update', stepId: 'then-1', state: 'success', duration: 10 });
  await postHostMessage(page, { type: 'execution-finished' });
}

test.describe('Flow Canvas Execution Path Highlight', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createBranchPathFixture());
    await expect(nodeById(page, 'if-1')).toBeVisible();
  });

  test('lights the taken branch and fades the untaken branch, and persists after the run', async ({ page }) => {
    await runThenBranch(page);

    await expect(edgePath(page, 'edge-if-then')).toHaveClass(/fc-edge-onpath/);
    await expect(edgePath(page, 'edge-if-else')).toHaveClass(/fc-edge-untaken/);
    // The start edge lights once its target (if-1) has run.
    await expect(edgePath(page, 'edge-start-if')).toHaveClass(/fc-edge-onpath/);

    // Color choice: a plain/spine traversed edge promotes to the traversed token; an on-path
    // branch edge keeps its branch hue.
    expect(await strokeOf(page, 'edge-start-if')).toBe(await resolveVar(page, '--fc-edge-traversed'));
    expect(await strokeOf(page, 'edge-if-then')).toBe(await resolveVar(page, '--fc-branch-then'));

    // The untaken branch is visibly faded (real Chromium resolves the class opacity).
    const untakenOpacity = await edgePath(page, 'edge-if-else').evaluate(
      (el) => getComputedStyle(el as Element).opacity,
    );
    expect(Number(untakenOpacity)).toBeCloseTo(0.35, 2);

    // Persists after execution-finished (isRunning is now false).
    await expect(edgePath(page, 'edge-if-then')).toHaveClass(/fc-edge-onpath/);
  });

  test('Clear Path resets the edges but keeps node result badges', async ({ page }) => {
    await runThenBranch(page);
    await expect(edgePath(page, 'edge-if-then')).toHaveClass(/fc-edge-onpath/);

    await page.getByRole('button', { name: '⌫ Clear Path' }).click();

    await expect(edgePath(page, 'edge-if-then')).not.toHaveClass(/fc-edge-onpath/);
    await expect(edgePath(page, 'edge-if-else')).not.toHaveClass(/fc-edge-untaken/);
    // The then-1 node's duration badge from the run is still on screen.
    await expect(nodeById(page, 'then-1').getByText('10ms', { exact: true })).toBeVisible();
  });

  test('a fresh run re-shows the path after a clear', async ({ page }) => {
    await runThenBranch(page);
    await page.getByRole('button', { name: '⌫ Clear Path' }).click();
    await expect(edgePath(page, 'edge-if-then')).not.toHaveClass(/fc-edge-onpath/);

    await runThenBranch(page);
    await expect(edgePath(page, 'edge-if-then')).toHaveClass(/fc-edge-onpath/);
  });

  test('PARITY: clearing the path is render-only and does not mutate the graph snapshot', async ({ page }) => {
    await runThenBranch(page);
    const before = await getGraphSnapshot(page);

    await page.getByRole('button', { name: '⌫ Clear Path' }).click();
    await expect(edgePath(page, 'edge-if-then')).not.toHaveClass(/fc-edge-onpath/);

    const after = await getGraphSnapshot(page);
    expect(JSON.stringify(after)).toBe(JSON.stringify(before));
  });
});
```

- [ ] **Step 3: Run the e2e spec**

Run: `npm run test:e2e -- flow-canvas-execution-path`
Expected: PASS — all four tests green. (If Chromium is not installed yet: `npm run test:e2e:install` first.)

- [ ] **Step 4: Commit**

```bash
git add e2e/fixtures/graphs.ts e2e/flow-canvas-execution-path.spec.ts
git commit -m "test(flow-canvas): e2e for execution path highlight and clear"
```

---

## Task 7: Full verification

- [ ] **Step 1: Type-check + build the bundle**

Run: `npm run build`
Expected: PASS — `tsc` reports no errors; `vite build` writes `dist/`.

- [ ] **Step 2: Full unit suite**

Run: `npm test`
Expected: PASS — every Vitest file green, including `edgePath`, `executionSlice.pathVisible`, and `AnimatedEdge`.

- [ ] **Step 3: Full e2e suite (catch cross-spec regressions)**

Run: `npm run test:e2e`
Expected: PASS — the new path spec plus all existing specs green. The run-timing PARITY tests still pass (edge highlighting writes nothing to node/edge data).

- [ ] **Step 4: Build the .NET host to confirm the embedded bundle target still works**

Run (from repo root `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper`):
`dotnet build SSH_Helper.sln`
Expected: PASS — the `BuildFlowCanvas` MSBuild target rebuilds the canvas and the solution compiles. (No C# source changed, so this only confirms the bundle still embeds.)

- [ ] **Step 5: Final review against the spec**

Confirm each spec decision is delivered:
- Build live, then persist → selector reads live-updating maps; style decoupled from `isRunning`. ✓
- Untaken branches fade → `.fc-edge-untaken { opacity: 0.35 }`. ✓
- Persist until next run/reload → `clearExecution()` (run start) resets `pathVisible`; graph load calls `clearExecution()`. ✓
- Manual clear, edges only → `clearPath()` flips `pathVisible` only; node badges untouched (e2e proves it). ✓
- No YAML/export impact → selector reads only transient exec maps; PARITY e2e proves the snapshot is unchanged. ✓

There is no separate commit for Task 7 unless a fix was needed; if verification surfaced a fix, commit it with a `fix(flow-canvas): …` message describing what verification caught.

---

## Known limitations (intentional, per spec — do not "fix" silently)

- **`try` branches without a `branchTaken` signal** render `idle` (the selector does not guess which of try/catch/finally ran). The child nodes inside the branch still highlight individually. If a future run instruments `try` with a `branchTaken` value matching the edge `branchPath` (`'try'`/`'catch'`/`'finally'`), those branch edges light automatically with no code change.
- **Loops show a boolean body highlight**, not a per-traversal count. The per-node `×N` badge already conveys iteration count.
- **No ordered replay / Timeline-scrubber sync.** Out of scope (Alternative B in the design).
