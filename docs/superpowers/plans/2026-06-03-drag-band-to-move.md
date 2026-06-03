# Drag a Band by its Label Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the user grab a branch band's label pill ("THEN" / "ELSE" / "LOOP" / etc.) and drag it to move the entire band — every block inside it — as one unit.

**Architecture:** Branch bands are derived geometry (`computeBranchBands`), not real nodes, so "moving a band" means translating every member node by the same delta; the band re-derives automatically. The label pill becomes a draggable handle that, on pointer-move, shifts all of the band's member nodes through a batched store action using zoom-correct deltas from `screenToFlowPosition`. Because the band's `zIndex:-1` would trap a nested pill below the React Flow pane, the pill is rendered as a **sibling** of the band rectangle (same `ViewportPortal`) with its own `zIndex` and `pointerEvents:auto`.

**Tech Stack:** React 19 + TypeScript, @xyflow/react (React Flow) 12, Zustand 5, Vitest 4 (jsdom) for units, Playwright 1.58 for e2e.

---

## File Structure

- `FlowCanvas/src/utils/nodeSize.ts` — **modify**: add `BAND_LABEL_HEADROOM` constant (render-only top padding).
- `FlowCanvas/src/utils/branchBands.ts` — **modify**: add `memberIds` to `BranchBand`; extend band top by the headroom.
- `FlowCanvas/src/utils/__tests__/branchBands.test.ts` — **modify**: update one geometry assertion; add headroom + memberIds tests.
- `FlowCanvas/src/stores/slices/graphSlice.ts` — **modify**: add `translateNodesBy(ids, dx, dy)` action.
- `FlowCanvas/src/stores/slices/__tests__/graphSlice.translateNodesBy.test.ts` — **create**: unit test for the new action.
- `FlowCanvas/src/nodes/BranchBandsLayer.tsx` — **modify**: split into a non-interactive rectangle layer + a draggable pill-handle layer; wire pointer drag.
- `FlowCanvas/src/nodes/bandlayer.css` — **create**: hover-only grip + `grabbing` cursor.
- `FlowCanvas/e2e/fixtures/graphs.ts` — **modify**: add `createBandDragFixture()` (THEN band with two members + ELSE sibling + spine successor).
- `FlowCanvas/e2e/flow-canvas-band-drag.spec.ts` — **create**: drag, undo, and non-interactive-rectangle e2e tests.

All commands below run from `FlowCanvas/` (i.e. `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper\FlowCanvas`).

> **Note on verification:** This project has a type-checker (`npx tsc --noEmit`) and tests (vitest + Playwright) but **no ESLint** is configured (none in `package.json` devDependencies). Verification uses typecheck + tests + build only.

---

## Task 1: Top headroom so the pill clears the first block

**Files:**
- Modify: `FlowCanvas/src/utils/nodeSize.ts`
- Modify: `FlowCanvas/src/utils/branchBands.ts:5` (import) and the `bands.push({...})` block (`branchBands.ts:150-160`)
- Test: `FlowCanvas/src/utils/__tests__/branchBands.test.ts`

Today the band's top padding is `BAND_PAD = 18` and the pill is ~17px tall, so the pill sits ~1px from the first block. Add a render-only top extension so it clears the block. Do **not** change `BAND_PAD` — the layout engine consumes it for horizontal lane spacing.

- [ ] **Step 1: Update the existing geometry assertion and add failing tests**

In `FlowCanvas/src/utils/__tests__/branchBands.test.ts`, update the imports at the top (line 4) to also pull the new constants from `nodeSize`:

```ts
import { computeBranchBands, branchPillLabel, BAND_PAD } from '../branchBands';
import { BAND_LABEL_HEADROOM, COLLAPSED_HEIGHT } from '../nodeSize';
```

Change the nested-band top assertion (currently `expect(nested.y).toBe(100 - 18);` with the comment `// top NOT inset (pill clears the first block)`) to account for the new headroom:

```ts
    expect(nested.x).toBeGreaterThan(100 - 18);           // left pulled inward (reveals parent accent)
    expect(nested.y).toBe(100 - 18 - 12);                 // top extended by headroom (pill clears the first block)
    expect(nested.x + nested.width).toBe(100 + 300 + 18); // right NOT inset (full padding)
    expect(nested.y + nested.height).toBe(100 + 52 + 18); // bottom unchanged (headroom is top-only)
```

Add a new test inside the `describe('branchBands', ...)` block:

```ts
  it('adds top-only headroom so the draggable label pill clears the first block', () => {
    const b = computeBranchBands([child('c1', 'p', 'steps/1/then/0', 100, 200)])[0];
    // Top is extended by BAND_PAD + BAND_LABEL_HEADROOM; the bottom keeps BAND_PAD only.
    expect(b.y).toBe(200 - BAND_PAD - BAND_LABEL_HEADROOM);
    expect(b.y + b.height).toBe(200 + COLLAPSED_HEIGHT + BAND_PAD);
  });
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npx vitest run src/utils/__tests__/branchBands.test.ts`
Expected: FAIL — `BAND_LABEL_HEADROOM` is not exported from `nodeSize` (import error), and the headroom assertions don't match current geometry.

- [ ] **Step 3: Add the constant in `nodeSize.ts`**

In `FlowCanvas/src/utils/nodeSize.ts`, add immediately after the `BAND_PAD` declaration (after line 16):

```ts
/** Extra vertical room added to the TOP of a branch band only (render-only — NOT consumed by
 *  the hierarchical layout engine) so the draggable label pill clears the first child block. */
export const BAND_LABEL_HEADROOM = 12;
```

- [ ] **Step 4: Apply the headroom in `branchBands.ts`**

In `FlowCanvas/src/utils/branchBands.ts`, extend the import on line 5:

```ts
import { CHILD_WIDTH, COLLAPSED_HEIGHT, estimateNodeHeight, BAND_PAD, BAND_LABEL_HEADROOM } from './nodeSize';
```

In the `bands.push({...})` object, change the `y` and `height` lines (leave `x` and `width` untouched):

```ts
      x: minX - BAND_PAD + leftInset,
      y: minY - BAND_PAD - BAND_LABEL_HEADROOM,
      width: (maxX - minX) + BAND_PAD * 2 - leftInset,
      height: (maxY - minY) + BAND_PAD * 2 + BAND_LABEL_HEADROOM,
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `npx vitest run src/utils/__tests__/branchBands.test.ts`
Expected: PASS (all branchBands tests green).

- [ ] **Step 6: Commit**

```bash
git add FlowCanvas/src/utils/nodeSize.ts FlowCanvas/src/utils/branchBands.ts FlowCanvas/src/utils/__tests__/branchBands.test.ts
git commit -m "feat(flow-canvas): add top headroom so band label clears the first block"
```

---

## Task 2: Expose `memberIds` on each band

**Files:**
- Modify: `FlowCanvas/src/utils/branchBands.ts` (interface `BranchBand` at `branchBands.ts:14-24`; the `bands.push({...})` block)
- Test: `FlowCanvas/src/utils/__tests__/branchBands.test.ts`

The drag handler needs the set of nodes a band wraps. `computeBranchBands` already computes `boxNodes` per group — expose their ids.

- [ ] **Step 1: Write the failing test**

Add inside the `describe('branchBands', ...)` block in `FlowCanvas/src/utils/__tests__/branchBands.test.ts`:

```ts
  it('exposes memberIds for every node in the branch subtree, including nested bodies', () => {
    const direct = child('d', 'p', 'steps/0/then/0', 100, 100);
    const nestedIf = child('nif', 'p', 'steps/0/then/1', 100, 200);
    const nestedBody = child('g', 'nif', 'steps/0/then/1/then/0', 280, 300); // indented nested THEN body
    const bands = computeBranchBands([direct, nestedIf, nestedBody]);

    const outer = bands.find((b) => b.id === 'p::then')!;
    expect([...outer.memberIds].sort()).toEqual(['d', 'g', 'nif']); // whole subtree

    const nested = bands.find((b) => b.id === 'nif::then')!;
    expect(nested.memberIds).toEqual(['g']); // just its own body
  });
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npx vitest run src/utils/__tests__/branchBands.test.ts -t memberIds`
Expected: FAIL — `outer.memberIds` is `undefined` (property does not exist).

- [ ] **Step 3: Add `memberIds` to the interface and populate it**

In `FlowCanvas/src/utils/branchBands.ts`, add the field to the `BranchBand` interface:

```ts
export interface BranchBand {
  id: string;
  parentId: string;
  branchKey: string;
  x: number;
  y: number;
  width: number;
  height: number;
  colorVar: string;
  depth: number;
  /** Ids of every node this band wraps (the boxed subtree). Drives "drag the band to move it". */
  memberIds: string[];
}
```

In the `bands.push({...})` object, add the `memberIds` property (derive it from the already-computed `boxNodes`):

```ts
      colorVar: branchColorVar(g.branchKey),
      depth,
      memberIds: boxNodes.map((n) => n.id),
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npx vitest run src/utils/__tests__/branchBands.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/branchBands.ts FlowCanvas/src/utils/__tests__/branchBands.test.ts
git commit -m "feat(flow-canvas): expose memberIds on each branch band"
```

---

## Task 3: `translateNodesBy` store action

**Files:**
- Modify: `FlowCanvas/src/stores/slices/graphSlice.ts` (interface `GraphSlice` at `graphSlice.ts:204`; implementation after `updateNodePosition` at `graphSlice.ts:485-490`)
- Test: `FlowCanvas/src/stores/slices/__tests__/graphSlice.translateNodesBy.test.ts` (create)

A single batched action that shifts a set of nodes by `(dx, dy)`, mirroring `updateNodePosition`'s side effects (`clearedExportStatusState()`), so one pointer-move = one store update.

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/stores/slices/__tests__/graphSlice.translateNodesBy.test.ts`:

```ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import type { Node } from '@xyflow/react';
import { useFlowStore } from '../../useFlowStore';

function node(id: string, x: number, y: number): Node {
  return { id, type: 'block', position: { x, y }, data: { props: {} } } as Node;
}

describe('translateNodesBy', () => {
  beforeEach(() => {
    useFlowStore.setState({
      nodes: [node('a', 100, 100), node('b', 200, 300), node('c', 500, 500)],
      edges: [],
    });
  });

  it('shifts only the targeted ids by the delta and leaves others untouched', () => {
    useFlowStore.getState().translateNodesBy(['a', 'b'], 25, -10);
    const byId = Object.fromEntries(useFlowStore.getState().nodes.map((n) => [n.id, n.position]));
    expect(byId['a']).toEqual({ x: 125, y: 90 });
    expect(byId['b']).toEqual({ x: 225, y: 290 });
    expect(byId['c']).toEqual({ x: 500, y: 500 });
  });

  it('is a no-op for ids not present in the graph', () => {
    useFlowStore.getState().translateNodesBy(['missing'], 50, 50);
    const byId = Object.fromEntries(useFlowStore.getState().nodes.map((n) => [n.id, n.position]));
    expect(byId['a']).toEqual({ x: 100, y: 100 });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npx vitest run src/stores/slices/__tests__/graphSlice.translateNodesBy.test.ts`
Expected: FAIL — `translateNodesBy is not a function`.

- [ ] **Step 3: Declare the action on the interface**

In `FlowCanvas/src/stores/slices/graphSlice.ts`, add to the `GraphSlice` interface, directly after the `updateNodePosition` line (line 204):

```ts
  updateNodePosition: (id: string, position: { x: number; y: number }) => void;
  /** Shift several nodes by the same delta in one batched update (drag a band by its label). */
  translateNodesBy: (ids: string[], dx: number, dy: number) => void;
```

- [ ] **Step 4: Implement the action**

In `FlowCanvas/src/stores/slices/graphSlice.ts`, add directly after the `updateNodePosition` implementation (after line 490):

```ts
  translateNodesBy: (ids, dx, dy) => {
    const idSet = new Set(ids);
    set((state) => ({
      nodes: state.nodes.map((n) =>
        idSet.has(n.id) ? { ...n, position: { x: n.position.x + dx, y: n.position.y + dy } } : n,
      ),
      ...clearedExportStatusState(),
    }));
  },
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `npx vitest run src/stores/slices/__tests__/graphSlice.translateNodesBy.test.ts`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add FlowCanvas/src/stores/slices/graphSlice.ts FlowCanvas/src/stores/slices/__tests__/graphSlice.translateNodesBy.test.ts
git commit -m "feat(flow-canvas): add translateNodesBy batched node-move action"
```

---

## Task 4: Draggable label handle in `BranchBandsLayer`

**Files:**
- Modify: `FlowCanvas/src/nodes/BranchBandsLayer.tsx`
- Create: `FlowCanvas/src/nodes/bandlayer.css`
- Create: `FlowCanvas/e2e/fixtures/graphs.ts` addition `createBandDragFixture()`
- Create: `FlowCanvas/e2e/flow-canvas-band-drag.spec.ts`

This task is integration-level (DOM + React Flow viewport + pointer capture), so it's driven by the Playwright e2e rather than a jsdom unit test (jsdom can't host the React Flow viewport portal or pointer capture reliably).

- [ ] **Step 1: Add the e2e fixture**

In `FlowCanvas/e2e/fixtures/graphs.ts`, append a new exported function at the end of the file:

```ts
// A THEN band with TWO members (then-a, then-b) plus an ELSE sibling and a spine successor
// (after-1) below the IF. Drives the "drag a band by its label" e2e: dragging the THEN pill must
// move then-a + then-b by the same delta while if-1, else-1, after-1 and __start__ stay put.
export function createBandDragFixture(): GraphFixture {
  return {
    nodes: [
      { id: '__start__', type: 'start', position: { x: 80, y: 20 }, data: { blockType: '_start', label: 'Start', props: {} } },
      { id: 'if-1', type: 'block', position: { x: 80, y: 160 }, data: { blockType: 'if', label: 'If', props: { condition: '${enabled}', _stepPath: 'steps/0' } } },
      { id: 'then-a', type: 'block', position: { x: 60, y: 320 }, data: { blockType: 'print', label: 'Then A', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then', message: 'then-a' } } },
      { id: 'then-b', type: 'block', position: { x: 60, y: 460 }, data: { blockType: 'print', label: 'Then B', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/1', _branchLabel: 'then', message: 'then-b' } } },
      { id: 'else-1', type: 'block', position: { x: 420, y: 320 }, data: { blockType: 'print', label: 'Else', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/else/0', _branchLabel: 'else', message: 'else' } } },
      { id: 'after-1', type: 'block', position: { x: 80, y: 640 }, data: { blockType: 'print', label: 'After', props: { message: 'after' } } },
    ],
    edges: [
      { id: 'edge-start-if', source: '__start__', target: 'if-1', style: { stroke: '#666' } },
      { id: 'edge-if-then', source: 'if-1', target: 'then-a', label: 'then', style: { stroke: 'var(--fc-branch-then)' } },
      { id: 'edge-then-a-b', source: 'then-a', target: 'then-b' },
      { id: 'edge-if-else', source: 'if-1', target: 'else-1', sourceHandle: 'false', label: 'else', style: { stroke: 'var(--fc-branch-else)' } },
      { id: 'edge-if-after', source: 'if-1', target: 'after-1', sourceHandle: 'continue' },
    ],
  };
}
```

- [ ] **Step 2: Write the failing e2e spec**

Create `FlowCanvas/e2e/flow-canvas-band-drag.spec.ts`:

```ts
import { expect, test, type Page } from '@playwright/test';
import { createBandDragFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, getGraphSnapshot, installHostMessageCapture, loadGraphFixture, waitForOutgoingMessage,
} from './support/harness';

async function positions(page: Page): Promise<Record<string, { x: number; y: number }>> {
  const snap = await getGraphSnapshot(page);
  const out: Record<string, { x: number; y: number }> = {};
  for (const n of snap.nodes as Array<{ id: string; position: { x: number; y: number } }>) {
    out[n.id] = n.position;
  }
  return out;
}

async function dragThenBand(page: Page, dx: number, dy: number): Promise<void> {
  const handle = page.locator('[data-testid="branch-band-handle"][data-branch="then"]');
  const box = await handle.boundingBox();
  if (!box) throw new Error('THEN band handle has no bounding box');
  const cx = box.x + box.width / 2;
  const cy = box.y + box.height / 2;
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx + dx, cy + dy, { steps: 8 });
  await page.mouse.up();
}

test.describe('Flow Canvas — drag a band by its label', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createBandDragFixture());
    await expect(page.locator('.react-flow__node[data-id="then-a"]')).toBeVisible();
    await expect(page.locator('[data-testid="branch-band-handle"][data-branch="then"]')).toBeVisible();
  });

  test('dragging the THEN label moves every block in the band by the same delta', async ({ page }) => {
    const before = await positions(page);
    await dragThenBand(page, 120, 80);
    const after = await positions(page);

    const dxA = after['then-a'].x - before['then-a'].x;
    const dyA = after['then-a'].y - before['then-a'].y;
    const dxB = after['then-b'].x - before['then-b'].x;
    const dyB = after['then-b'].y - before['then-b'].y;

    expect(Math.abs(dxA)).toBeGreaterThan(1); // the band actually moved
    expect(Math.abs(dyA)).toBeGreaterThan(1);
    expect(dxB).toBeCloseTo(dxA, 5);          // both members moved as one unit
    expect(dyB).toBeCloseTo(dyA, 5);

    expect(after['if-1']).toEqual(before['if-1']);       // non-members untouched
    expect(after['else-1']).toEqual(before['else-1']);
    expect(after['after-1']).toEqual(before['after-1']);
    expect(after['__start__']).toEqual(before['__start__']);
  });

  test('one undo reverts the whole band move', async ({ page }) => {
    const before = await positions(page);
    await dragThenBand(page, 100, 60);
    expect((await positions(page))['then-a']).not.toEqual(before['then-a']);

    await page.keyboard.press('Control+z');

    await expect.poll(async () => (await positions(page))['then-a']).toEqual(before['then-a']);
    expect((await positions(page))['then-b']).toEqual(before['then-b']);
  });

  test('the band rectangle stays non-interactive; only the handle is grabbable', async ({ page }) => {
    const band = page.locator('[data-testid="branch-band"][data-branch="then"]');
    expect(await band.evaluate((el) => getComputedStyle(el as HTMLElement).pointerEvents)).toBe('none');
    const handle = page.locator('[data-testid="branch-band-handle"][data-branch="then"]');
    expect(await handle.evaluate((el) => getComputedStyle(el as HTMLElement).pointerEvents)).toBe('auto');
    expect(await handle.evaluate((el) => getComputedStyle(el as HTMLElement).cursor)).toBe('grab');
  });
});
```

- [ ] **Step 3: Run the e2e to verify it fails**

Run: `npx playwright test e2e/flow-canvas-band-drag.spec.ts`
Expected: FAIL — `[data-testid="branch-band-handle"]` does not exist yet (the `beforeEach` visibility check fails).

- [ ] **Step 4: Create the hover-grip stylesheet**

Create `FlowCanvas/src/nodes/bandlayer.css`:

```css
/* Branch-band drag handles (the label pill). The grip dots (⠿) occupy space always so the pill
   doesn't reflow, but are invisible until the pill is hovered. */
.fc-band-grip {
  opacity: 0;
  letter-spacing: -1px;
  transition: opacity 120ms ease;
}
.fc-band-handle:hover .fc-band-grip {
  opacity: 0.9;
}
.fc-band-handle:active {
  cursor: grabbing;
}
```

- [ ] **Step 5: Rewrite `BranchBandsLayer.tsx` as rectangle layer + handle layer**

Replace the entire contents of `FlowCanvas/src/nodes/BranchBandsLayer.tsx` with:

```tsx
// FlowCanvas/src/nodes/BranchBandsLayer.tsx
import { ViewportPortal, useReactFlow } from '@xyflow/react';
import { useRef, type PointerEvent } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { computeBranchBands, branchPillLabel, type BranchBand } from '../utils/branchBands';
import { mix } from '../utils/tokens';
import { sendLayoutAutosave } from '../utils/layoutAutosave';
import './bandlayer.css';

export default function BranchBandsLayer() {
  const nodes = useFlowStore((s) => s.nodes);
  const enabled = useFlowStore((s) => s.branchBandsEnabled);
  const { screenToFlowPosition } = useReactFlow();
  const drag = useRef<{ memberIds: string[]; lastX: number; lastY: number } | null>(null);

  if (!enabled) return null;
  const bands = computeBranchBands(nodes);
  if (bands.length === 0) return null;

  const startDrag = (e: PointerEvent<HTMLDivElement>, band: BranchBand) => {
    if (e.button !== 0) return; // left button only — middle/right still pan the canvas
    e.stopPropagation();
    e.currentTarget.setPointerCapture(e.pointerId);
    useFlowStore.getState().pushSnapshot('Move band');
    const p = screenToFlowPosition({ x: e.clientX, y: e.clientY });
    drag.current = { memberIds: band.memberIds, lastX: p.x, lastY: p.y };
  };

  const moveDrag = (e: PointerEvent<HTMLDivElement>) => {
    const d = drag.current;
    if (!d) return;
    const p = screenToFlowPosition({ x: e.clientX, y: e.clientY });
    const dx = p.x - d.lastX;
    const dy = p.y - d.lastY;
    if (dx === 0 && dy === 0) return;
    d.lastX = p.x;
    d.lastY = p.y;
    useFlowStore.getState().translateNodesBy(d.memberIds, dx, dy);
  };

  const endDrag = (e: PointerEvent<HTMLDivElement>) => {
    if (!drag.current) return;
    drag.current = null;
    try { e.currentTarget.releasePointerCapture(e.pointerId); } catch { /* already released */ }
    sendLayoutAutosave();
  };

  return (
    <ViewportPortal>
      {/* Band rectangles: behind nodes, non-interactive (geometry/look unchanged). */}
      {bands.map((b) => {
        const nested = b.depth >= 1;
        const tint = nested ? 13 : 7;
        return (
          <div
            key={b.id}
            data-testid="branch-band"
            data-branch={b.branchKey}
            style={{
              position: 'absolute',
              transform: `translate(${b.x}px, ${b.y}px)`,
              width: b.width, height: b.height,
              background: mix(b.colorVar, tint),
              // Longhand per-side borders (not the `border` shorthand) so the 3px left accent
              // can't be clobbered by the shorthand on rerender — React warns about mixing them.
              borderTop: `1px solid ${mix(b.colorVar, nested ? 55 : 38)}`,
              borderRight: `1px solid ${mix(b.colorVar, nested ? 55 : 38)}`,
              borderBottom: `1px solid ${mix(b.colorVar, nested ? 55 : 38)}`,
              borderLeft: `3px solid ${mix(b.colorVar, 70)}`,
              borderRadius: 9,
              pointerEvents: 'none',
              zIndex: -1,
            }}
          />
        );
      })}

      {/* Draggable label handles: rendered as siblings (not children) of the rectangles so their
          zIndex isn't trapped by the rectangle's zIndex:-1 stacking context. They sit in the band's
          top headroom, above the pane, and catch the pointer to move the whole band. */}
      {bands.map((b) => {
        const nested = b.depth >= 1;
        return (
          <div
            key={`${b.id}::handle`}
            data-testid="branch-band-handle"
            data-branch={b.branchKey}
            className="fc-band-handle"
            title="Drag to move this band"
            onPointerDown={(e) => startDrag(e, b)}
            onPointerMove={moveDrag}
            onPointerUp={endDrag}
            onLostPointerCapture={endDrag}
            style={{
              position: 'absolute',
              transform: `translate(${b.x}px, ${b.y}px)`,
              font: '800 9px/1.4 system-ui, sans-serif', letterSpacing: '0.08em',
              padding: '2px 10px', borderRadius: '9px 0 8px 0',
              color: 'oklch(17% 0.02 275)',
              background: nested ? `color-mix(in oklch, ${b.colorVar}, white 14%)` : b.colorVar,
              display: 'inline-flex', alignItems: 'center', gap: '4px',
              cursor: 'grab', pointerEvents: 'auto', userSelect: 'none',
              zIndex: 5,
            }}
          >
            <span className="fc-band-grip" aria-hidden="true">⠿</span>
            {branchPillLabel(b.branchKey)}
          </div>
        );
      })}
    </ViewportPortal>
  );
}
```

- [ ] **Step 6: Run the e2e to verify it passes**

Run: `npx playwright test e2e/flow-canvas-band-drag.spec.ts`
Expected: PASS (all three tests).

If the drag test is flaky because pointer capture didn't latch under Playwright, switch the move/up listeners to `window` for the duration of the drag (add `window.addEventListener('pointermove'/'pointerup', ...)` in `startDrag`, removing them in `endDrag`) instead of relying on `setPointerCapture`. Keep `setPointerCapture` as the default; only fall back if needed.

- [ ] **Step 7: Confirm the existing band e2e still passes**

The pill moved out of the rectangle into its own element, so re-run the band suite:

Run: `npx playwright test e2e/flow-canvas-branch-bands.spec.ts e2e/flow-canvas-bands.spec.ts`
Expected: PASS. (These assert the rectangle's `data-testid="branch-band"`, `data-branch`, border color, and `pointer-events:none`, all preserved.)

- [ ] **Step 8: Commit**

```bash
git add FlowCanvas/src/nodes/BranchBandsLayer.tsx FlowCanvas/src/nodes/bandlayer.css FlowCanvas/e2e/fixtures/graphs.ts FlowCanvas/e2e/flow-canvas-band-drag.spec.ts
git commit -m "feat(flow-canvas): drag a band by its label to move the whole band"
```

---

## Task 5: Full verification

**Files:** none (verification only)

- [ ] **Step 1: Typecheck the whole Flow Canvas project**

Run: `npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 2: Run the full vitest suite**

Run: `npm test`
Expected: all unit tests pass (branchBands, translateNodesBy, and the rest).

- [ ] **Step 3: Run the full e2e suite**

Run: `npm run test:e2e`
Expected: all Playwright specs pass, including `flow-canvas-band-drag.spec.ts` and the existing band specs.

- [ ] **Step 4: Production build (also exercises the C# embed path's `npm run build`)**

Run: `npm run build`
Expected: `tsc && vite build` completes with no errors.

- [ ] **Step 5: Final commit (only if any cleanup was needed)**

```bash
git status
# If nothing changed in this task, no commit is needed.
```

---

## Self-Review notes (against the spec)

- **Grab affordance = corner pill (A):** the handle is rendered at the band's top-left `(b.x, b.y)` with the same pill styling — Task 4.
- **Hover-only ⠿ grip + grab cursor:** `bandlayer.css` (`.fc-band-grip` opacity 0 → 0.9 on hover; `cursor: grab`/`grabbing`) — Task 4.
- **What moves = whole subtree:** `memberIds` is the boxed-subtree id set (incl. nested bodies) — Task 2 + e2e assertion that both THEN members move.
- **Container/sibling/successor stay put:** e2e asserts `if-1`, `else-1`, `after-1`, `__start__` unchanged — Task 4.
- **Free-form, zoom-correct:** delta from `screenToFlowPosition` diff; no snapping — Task 4.
- **Undo "Move band" + autosave:** `pushSnapshot('Move band')` on down, `sendLayoutAutosave()` on up; e2e asserts one Ctrl+Z reverts — Task 3/4.
- **No YAML/membership change:** only `position` is mutated; `_isChildOf`/`_stepPath`/edges untouched — Task 3.
- **Spacing fix without touching `BAND_PAD`:** new `BAND_LABEL_HEADROOM` top-only — Task 1.
- **Two-layer stacking:** handle is a sibling of the rectangle with its own `zIndex:5`/`pointerEvents:auto`; rectangle stays `zIndex:-1`/`pointerEvents:none` — Task 4 + e2e assertion.
