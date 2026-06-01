# Flow Canvas Straight-Spine Edges Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Flow Canvas continuation edges render as a dead-straight vertical spine (including Start→first block) instead of the smoothstep "down-up-down" hook, while branch/loop corridor edges keep their orthogonal routing.

**Architecture:** The hook comes from `getSmoothStepPath`'s 20px `offset` overshooting the ~10px gap between blocks whose centered handles are horizontally misaligned (variable node widths, left-aligned). Fix = align the handles by giving top-level blocks (and Start) a single fixed 280px width, then route X-aligned downward edges with `getStraightPath` and keep `getSmoothStepPath` for X-offset (corridor/branch) edges. A geometry test (`|sourceX − targetX| < 0.5`) is the discriminator — robust because imported branch edges don't carry `data.branchPath`. All changes are presentation-only; YAML export, connection rules, and persisted data are untouched.

**Tech Stack:** React 19 + @xyflow/react v12 (React Flow), TypeScript, Vite, Playwright e2e. Spec: `docs/superpowers/specs/2026-05-30-flow-canvas-straight-spine-edges-design.md`.

---

## Prerequisites (one-time, run from `FlowCanvas/`)

- `npm install` if `node_modules` is absent.
- `npx playwright install chromium` (or `npm run test:e2e:install`) if the browser isn't installed.
- All `npm`/`npx` commands below run **from the `FlowCanvas/` directory**. `dotnet` commands run from the repo root.

## File Structure

- **Create** `FlowCanvas/e2e/flow-canvas-edge-geometry.spec.ts` — the new regression spec (uniform width + edge geometry). Mirrors the existing `flow-canvas-live-wires.spec.ts` harness pattern.
- **Modify** `FlowCanvas/src/nodes/BaseBlock.tsx:150` — non-child block fixed width.
- **Modify** `FlowCanvas/src/nodes/StartNode.tsx:53-54` — Start node fixed width.
- **Modify** `FlowCanvas/src/nodes/AnimatedEdge.tsx:2,15-17` — import `getStraightPath`; geometry discriminator.

No other files change. `branchBands.ts` is intentionally untouched (width stays 280, so its `NODE_W=280` constant/comment remain correct).

---

### Task 1: Uniform top-level block width

**Files:**
- Create: `FlowCanvas/e2e/flow-canvas-edge-geometry.spec.ts`
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx:150`

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/e2e/flow-canvas-edge-geometry.spec.ts`:

```ts
import { expect, test, type Page } from '@playwright/test';
import type { GraphFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, installHostMessageCapture, loadGraphFixture, waitForOutgoingMessage,
} from './support/harness';

const SHORT = 'Hi';
const LONG = 'A deliberately long label that pushes this block out to its maximum rendered width';

function block(id: string, x: number, y: number, label: string): GraphFixture['nodes'][number] {
  return { id, type: 'block', position: { x, y }, data: { blockType: 'print', label, props: { message: label } } };
}

async function nodeWidth(page: Page, id: string): Promise<number> {
  const box = await page.locator(`.react-flow__node[data-id="${id}"]`).boundingBox();
  if (!box) throw new Error(`node ${id} has no bounding box`);
  return box.width;
}

test.describe('Flow Canvas Edge Geometry', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('top-level blocks render at a uniform width regardless of label length', async ({ page }) => {
    await loadGraphFixture(page, {
      nodes: [block('short', 200, 80, SHORT), block('long', 640, 80, LONG)],
      edges: [],
    });
    await expect(page.locator('.react-flow__node[data-id="short"]')).toBeVisible();
    await expect(page.locator('.react-flow__node[data-id="long"]')).toBeVisible();
    const wShort = await nodeWidth(page, 'short');
    const wLong = await nodeWidth(page, 'long');
    expect(Math.abs(wShort - wLong)).toBeLessThanOrEqual(1);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npx playwright test e2e/flow-canvas-edge-geometry.spec.ts -g "uniform width"`
Expected: FAIL — the short-label node renders near `minWidth:180` and the long-label node near `maxWidth:280`, so `Math.abs(wShort - wLong)` is ~100, not ≤ 1.

- [ ] **Step 3: Make the non-child block width fixed**

In `FlowCanvas/src/nodes/BaseBlock.tsx`, change line 150 so non-child blocks have `min === max === 280` (children unchanged):

```tsx
    minWidth: isChild ? 160 : 280,
    maxWidth: isChild ? 260 : 280,
```

(Only `minWidth`'s non-child value changes, `180` → `280`. `overflow: 'hidden'` at line 152 already clips long labels.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `npx playwright test e2e/flow-canvas-edge-geometry.spec.ts -g "uniform width"`
Expected: PASS — both blocks render at 280, diff ≤ 1px.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/e2e/flow-canvas-edge-geometry.spec.ts FlowCanvas/src/nodes/BaseBlock.tsx
git commit -m "feat(flow-canvas): fixed 280px width for top-level blocks so handles align"
```

---

### Task 2: Start node matches block width

**Files:**
- Modify: `FlowCanvas/e2e/flow-canvas-edge-geometry.spec.ts` (add a test)
- Modify: `FlowCanvas/src/nodes/StartNode.tsx:53-54`

- [ ] **Step 1: Write the failing test**

Append this test inside the `test.describe('Flow Canvas Edge Geometry', ...)` block in `FlowCanvas/e2e/flow-canvas-edge-geometry.spec.ts` (reuse the `block` and `nodeWidth` helpers already defined):

```ts
  test('the Start node shares the uniform top-level width (~280px)', async ({ page }) => {
    await loadGraphFixture(page, {
      nodes: [
        { id: 'start', type: 'start', position: { x: 200, y: 40 }, data: { blockType: '_start', label: 'S', props: { name: 'S' } } },
        block('first', 200, 340, 'First'),
      ],
      edges: [{ id: 'e-start', source: 'start', target: 'first' }],
    });
    await expect(page.locator('.react-flow__node[data-id="start"]')).toBeVisible();
    const wStart = await nodeWidth(page, 'start');
    expect(wStart).toBeGreaterThan(276);
    expect(wStart).toBeLessThan(290);
  });
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npx playwright test e2e/flow-canvas-edge-geometry.spec.ts -g "Start node shares"`
Expected: FAIL — the Start node currently renders near `minWidth:260` (short name "S"), so `wStart` ≈ 260–264, below the `> 276` floor.

- [ ] **Step 3: Fix the Start node width**

In `FlowCanvas/src/nodes/StartNode.tsx`, change lines 53-54 from:

```tsx
    minWidth: 260,
    maxWidth: 300,
```

to:

```tsx
    minWidth: 280,
    maxWidth: 280,
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npx playwright test e2e/flow-canvas-edge-geometry.spec.ts -g "Start node shares"`
Expected: PASS — Start renders at 280 (`wStart` ≈ 280–284 depending on box-sizing/border).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/e2e/flow-canvas-edge-geometry.spec.ts FlowCanvas/src/nodes/StartNode.tsx
git commit -m "feat(flow-canvas): match Start node width to blocks so the first edge aligns"
```

---

### Task 3: Geometry discriminator in AnimatedEdge

**Files:**
- Modify: `FlowCanvas/e2e/flow-canvas-edge-geometry.spec.ts` (add two tests)
- Modify: `FlowCanvas/src/nodes/AnimatedEdge.tsx:2,15-17`

- [ ] **Step 1: Write the failing tests**

Append these two tests inside the `test.describe('Flow Canvas Edge Geometry', ...)` block:

```ts
  test('an aligned, downward continuation edge renders as a straight line', async ({ page }) => {
    await loadGraphFixture(page, {
      nodes: [block('top', 200, 80, SHORT), block('bottom', 200, 360, LONG)],
      edges: [{ id: 'e1', source: 'top', target: 'bottom' }],
    });
    await expect(page.locator('path#e1')).toBeVisible();
    // getStraightPath emits exactly one line segment: "M x,yL x,y" — no extra L/Q/C commands.
    await expect(page.locator('path#e1')).toHaveAttribute('d', /^M[\s\d.,-]+L[\s\d.,-]+$/);
  });

  test('an X-offset edge keeps its orthogonal (smoothstep) routing', async ({ page }) => {
    await loadGraphFixture(page, {
      nodes: [block('a', 200, 80, SHORT), block('b', 600, 360, SHORT)],
      edges: [{ id: 'e2', source: 'a', target: 'b' }],
    });
    await expect(page.locator('path#e2')).toBeVisible();
    // smoothstep with borderRadius:8 emits a quadratic-curved corner (Q) on any real bend.
    await expect(page.locator('path#e2')).toHaveAttribute('d', /Q/);
  });
```

- [ ] **Step 2: Run the tests to verify the spine one fails**

Run: `npx playwright test e2e/flow-canvas-edge-geometry.spec.ts -g "straight line|orthogonal"`
Expected: the "orthogonal" test PASSES (offset edge is already smoothstep → contains `Q`); the "straight line" test FAILS — `AnimatedEdge` still calls `getSmoothStepPath` for every edge, so even the aligned spine path is a multi-segment smoothstep `d` that doesn't match the single-segment regex.

(Note: if the spine test unexpectedly passes here, smoothstep collapsed the aligned path to a single segment — the change in Step 3 is still correct as robustness and the test remains a valid guard.)

- [ ] **Step 3: Add the geometry discriminator**

In `FlowCanvas/src/nodes/AnimatedEdge.tsx`, add `getStraightPath` to the import on line 2:

```tsx
import { BaseEdge, getSmoothStepPath, getStraightPath, type EdgeProps } from '@xyflow/react';
```

Then replace the `getSmoothStepPath` call (lines 15-17) with the geometry split:

```tsx
  // Aligned, downward edges (the continuation spine) get a literal straight line so the run
  // packet glides cleanly; X-offset edges (branch/loop corridors, the IF "false" and container
  // "continue" handles) keep smoothstep so they route orthogonally around child blocks.
  const isSpine = Math.abs(sourceX - targetX) < 0.5 && targetY > sourceY;
  const [edgePath] = isSpine
    ? getStraightPath({ sourceX, sourceY, targetX, targetY })
    : getSmoothStepPath({
        sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, borderRadius: 8,
      });
```

No other lines in `AnimatedEdge.tsx` change — the gradient, `markerEnd`, packet `offsetPath`, selection `strokeWidth`, and reduced-motion gating all consume `edgePath`/endpoints unchanged.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `npx playwright test e2e/flow-canvas-edge-geometry.spec.ts`
Expected: PASS — all four tests (uniform width, Start width, straight spine, orthogonal corridor).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/e2e/flow-canvas-edge-geometry.spec.ts FlowCanvas/src/nodes/AnimatedEdge.tsx
git commit -m "feat(flow-canvas): straight-path spine edges, smoothstep retained for corridors"
```

---

### Task 4: Full verification

**Files:** none (verification only).

- [ ] **Step 1: Type-check + build the Flow Canvas**

Run (from `FlowCanvas/`): `npm run build`
Expected: PASS — `tsc` reports no type errors, `vite build` produces `dist/`. (No ESLint is configured for `FlowCanvas/`; `tsc` is the type-check gate.)

- [ ] **Step 2: Run the new spec + the edge/node regression specs**

Run (from `FlowCanvas/`):
```bash
npx playwright test \
  e2e/flow-canvas-edge-geometry.spec.ts \
  e2e/flow-canvas-live-wires.spec.ts \
  e2e/flow-canvas-node-redesign.spec.ts \
  e2e/flow-canvas-branch-bands.spec.ts \
  e2e/flow-canvas-connection-guards.spec.ts \
  e2e/flow-canvas-execution-cinematics.spec.ts \
  e2e/flow-canvas-reduced-motion.spec.ts \
  e2e/flow-canvas-token-sweep.spec.ts
```
Expected: PASS — markers/gradient/packet, branch bands, connection guards, cinematics, reduced-motion, and token-sweep all green (geometry change touches only the path `d` of aligned edges).

- [ ] **Step 3: Build the solution and run the bridge tests (export parity)**

Run (from repo root):
```bash
dotnet build SSH_Helper.sln
dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridge"
```
Expected: PASS — the `BuildFlowCanvas` target rebuilds the canvas, and the bridge/export-parity tests stay green (no YAML/export change).

- [ ] **Step 4: Manual confirmation**

Run the app (`dotnet run --project SSH_Helper.csproj`), open the Flow Canvas, import the script from the bug report, and confirm:
- The continuation spine is dead-straight top-to-bottom, **including Start → first block**.
- Running the script shows the packet gliding straight down the spine (no hook).
- IF / foreach / switch / try corridors still bend orthogonally around their child blocks.

- [ ] **Step 5: Final commit (if any manual tweaks were needed)**

```bash
git add -A
git commit -m "test(flow-canvas): verify straight-spine edge routing end-to-end"
```

---

## Self-Review

**Spec coverage:**
- Uniform 280px top-level width → Task 1. ✓
- Start matches block width → Task 2. ✓
- `getStraightPath` spine + `getSmoothStepPath` corridor via geometry discriminator → Task 3. ✓
- Invariants (YAML export, connection guards, markers/gradient/packet/selection/reduced-motion) → Task 4 Steps 2-3. ✓
- New geometry regression guard → Task 3 tests. ✓
- `branchBands.ts` comment → intentionally dropped (width stays 280; constant remains accurate). Documented in File Structure. ✓

**Placeholder scan:** none — every code/command/expected-output is concrete.

**Type/name consistency:** `block()` and `nodeWidth()` helpers are defined in Task 1 and reused verbatim in Tasks 2-3; edge ids (`e1`, `e2`, `e-start`) match their `path#<id>` selectors; the `isSpine` discriminator uses `sourceX/targetX/sourceY/targetY` already destructured at `AnimatedEdge.tsx:9`.
