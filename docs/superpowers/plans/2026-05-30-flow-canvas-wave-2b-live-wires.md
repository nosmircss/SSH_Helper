# Flow Canvas Wave 2b — Live Wires (Gradient Edges + Data Packets) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make canvas connections read as a living dataflow — every edge gets a tokenized arrowhead, branch edges carry their branch color (rest + run) via a source→target gradient, and a single CSS `offset-path` "pulse-dot" packet travels along active edges while a script runs; replacing the marching-ants animation. Pure-frontend, render-only, round-trip-safe, reduced-motion-gated, **no new runtime dependency.**

**Architecture:** `AnimatedEdge` becomes the **universal** custom edge (rest + running). `getBranchVisual` stays the single blockType-aware branch→color authority, re-pointed through Wave 2a's `branchColorVar()` token map. Arrowheads come from a small tokenized `<marker>` registry (`EdgeMarkers`). The packet is one SVG `<circle>` positioned by CSS `offset-path` with a shared keyframe, rendered only when `isRunning && sourceExecuted && !reducedMotion`. All edge styling is visual; `node.data`/`exportGraph.ts`/`FlowCanvasBridge.cs` are untouched.

**Tech Stack:** TypeScript / React 19 / `@xyflow/react` v12 (`BaseEdge`, `getSmoothStepPath`) / Zustand 5 / CSS (OKLCH tokens, `offset-path`/`offset-distance`, `color-mix` via the `mix()` helper) / SVG `<marker>`+`<linearGradient>` / Playwright e2e. Build `cd FlowCanvas && npm run build`; dist re-embed via `dotnet build SSH_Helper.sln`.

> **TDD note (presentational module):** edge rendering is DOM/visual, so it cannot be strict unit-test-first. Each rendering task is gated by `npm run build` clean + the live no-hex token-sweep + unchanged parity; the dedicated `flow-canvas-live-wires.spec.ts` regression lock is written in Task 6 once the DOM exists (the same pattern Wave 2a used successfully).

---

## Verified facts (do not re-derive)

- `edge.data.branchPath` carries branch identity (`then`, `else`, `elif/<n>/then`, `do`, `catch`, `finally`, `cases/<n>/do`, `default`, `parallel/<n>`); set on connect via `inferDefaultBranchMetadata`, edited via `EdgeContextMenu`.
- `getBranchVisual(blockType, metadata)` (`graphSlice.ts:125-221`) maps branch→`{label, style:{stroke}, labelStyle}`, today using `--fc-state-*`/`--fc-accent`/`--fc-cat-network-border` + a `dashed` (`strokeDasharray:'5,5'`). `parseIndexedBranch` is already defined in the file.
- Wave 2a `branchColorVar(key)` (`utils/branchBands.ts`) → `var(--fc-branch-*)` tokens (which **alias** the same state/accent hues; `then/try`→success, `else/catch/default`→error, `elif/do/case`→warning, `finally`→accent, `parallel`→network, `fallback`→text-disabled).
- `App.tsx`: `edgeTypes = { animated: AnimatedEdge }` (52-53); `displayEdges` injects `type:'animated'` **only when `isRunning`** (351-361); `defaultEdgeOptions={{ type:'smoothstep', style:{stroke:'var(--fc-edge-idle)'} }}` (399).
- `AnimatedEdge.tsx` ignores branch `style.stroke` (forces state-color/idle), passes `markerEnd` straight through (nothing assigns a marker → no arrowhead), injects a per-edge `<style>` `marchingAnts`.
- Reduced motion: `useFlowStore((s) => s.reducedMotion)` (`uiSlice`); `App.tsx:120` toggles `document.body.classList.toggle('fc-reduced-motion', reducedMotion)`; `FlowCanvas/src/styles/reducedMotion.css` blankets all `@keyframes` under `body.fc-reduced-motion *`.
- Run lifecycle messages (`communication-message-types.ts`): `execution-started`→`setRunning(true)` (`messageBridge.ts:176-181`), `execution-update {stepId,state}`→sets `blockStates` (190+), `execution-finished`→`setRunning(false)`.
- `isRunning`/`blockStates` live in `executionSlice`; `AnimatedEdge` already reads `useFlowStore((s) => s.isRunning)` and `s.blockStates`.
- Global `@keyframes` live in component-adjacent CSS (e.g. `nodes/baseblock.css` → `exec-pulse`). `mix(color,pct)` (`utils/tokens.ts`) = `color-mix(in oklch, color pct%, transparent)`; **no lighten helper** — brighter colors must be `--fc-*` tokens authored in `tokens.css`.
- Round-trip: `exportGraph.ts` serializes `node.data.props` only; edge styling/markers/labels are never exported; parity bundle = 22/22 under `--workers=1`. No-hex token-sweep gate (`flow-canvas-token-sweep.spec.ts`) is live; allowed literal is `DEFAULT_COMMENT_COLOR` only.

## File Structure

### New files

| Path | Responsibility |
| --- | --- |
| `FlowCanvas/src/nodes/EdgeMarkers.tsx` | Hidden `<svg><defs>` rendering the tokenized arrowhead `<marker>` registry (one per branch/state token + idle + accent) and the shared `fc-packet-glow` filter. Exports `markerIdForStroke(stroke)`. Mounted once in `App`. |
| `FlowCanvas/src/nodes/animatededge.css` | The packet keyframes (`fc-packet-travel` via `offset-distance`, `fc-packet-pulse` via `transform: scale`) + the `.fc-edge-packet` class. Imported by `AnimatedEdge`. |
| `FlowCanvas/e2e/flow-canvas-live-wires.spec.ts` | Edge-rendering regression lock: arrowhead marker present; branch edge resolves to `--fc-branch-*`; plain edge neutral; packet only while running and absent under reduced-motion. |

### Modified files

| Path | Change |
| --- | --- |
| `FlowCanvas/src/styles/tokens.css` | Add `--fc-edge-packet` (bright packet core). Only place new colors are authored. |
| `FlowCanvas/src/stores/slices/graphSlice.ts` | `getBranchVisual`: drop the dashes, route stroke + label color through `branchColorVar` (single token source); keep labels + blockType-aware resolution. Import `branchColorVar`. |
| `FlowCanvas/src/nodes/AnimatedEdge.tsx` | Rewrite: consume `style.stroke` for color, render a `userSpaceOnUse` gradient, set a tokenized `markerEnd` arrowhead, render the `offset-path` pulse packet gated by `running && sourceExecuted && !reducedMotion`; remove the inline `<style>`/marching-ants. |
| `FlowCanvas/src/App.tsx` | Use `animated` for all edges (drop the running-only split + flip `defaultEdgeOptions.type`); mount `<EdgeMarkers />`. |
| `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts` | Add an edge to the scanned fixture so the no-hex/no-concat scan covers a rendered edge. |

---

## Section 1: Tokens & branch-color unification

### Task 1: Add the `--fc-edge-packet` token

**Files:**
- Modify: `FlowCanvas/src/styles/tokens.css` (near `--fc-edge-idle`, line ~163)

- [ ] **Step 1: Append the packet token** inside `:root`, right after the `--fc-edge-idle` line:

```css
  /* ── Wave 2b: live-wire data packet (bright core for the traveling dot) ── */
  --fc-edge-packet: oklch(95% 0.03 200);
```

- [ ] **Step 2: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green (new token resolves to OKLCH; no hex).
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 3: Commit.**

```bash
git add FlowCanvas/src/styles/tokens.css
git commit -m "feat(flow-canvas): add --fc-edge-packet token for the live-wire data packet"
```

### Task 2: Unify branch color — `getBranchVisual` → `branchColorVar`, drop dashes

**Files:**
- Modify: `FlowCanvas/src/stores/slices/graphSlice.ts` (import + `getBranchVisual` body 136-220)

- [ ] **Step 1: Import `branchColorVar`.** Add to the imports at the top of `graphSlice.ts` (after the `connectionRules` import, line 6):

```ts
import { branchColorVar } from '../../utils/branchBands';
```

- [ ] **Step 2: Replace the `getBranchVisual` body.** Replace everything from `const branchPath = metadata.branchPath;` (line 136) through the end of the `parallel` block and its trailing `return defaultVisual;` (line ~220) with:

```ts
  const branchPath = metadata.branchPath;
  if (!branchPath) return defaultVisual;

  // getBranchVisual stays the blockType-aware KEY resolver; branchColorVar is the single
  // branch→token map (shared with the Wave 2a bands + Properties chip). No more dashes —
  // color now carries branch meaning (Wave 2b Live Wires).
  const visual = (key: string, label: string) => ({
    label,
    style: { stroke: branchColorVar(key) },
    labelStyle: { fill: branchColorVar(key), fontSize: 11, fontWeight: 600 },
  });

  if (blockType === 'if') {
    if (branchPath === 'else') return visual('else', 'else');
    if (branchPath.startsWith('elif/')) {
      const condition = (metadata.condition ?? '').trim();
      return visual('elif', condition ? `elif: ${condition}` : 'elif');
    }
    return visual('then', 'then');
  }

  if (blockType === 'foreach' || blockType === 'while') {
    return visual('do', 'do');
  }

  if (blockType === 'try') {
    if (branchPath === 'catch') return visual('catch', 'catch');
    if (branchPath === 'finally') return visual('finally', 'finally');
    return visual('try', 'do');
  }

  if (blockType === 'switch') {
    if (branchPath === 'default' || branchPath === 'else') return visual('default', 'default');
    const caseValue = (metadata.caseValue ?? '').trim();
    return visual('case', caseValue ? `case: ${caseValue}` : 'case');
  }

  if (blockType === 'parallel') {
    const index = parseIndexedBranch(branchPath, 'parallel');
    return visual('parallel', index === null ? 'branch' : `branch ${index + 1}`);
  }

  return defaultVisual;
```

  > This removes the local `const dashed = { strokeDasharray: '5,5' };` (delete that line too). `--fc-branch-*` tokens alias the same hues as the old `--fc-state-*`, so computed colors are unchanged — only the token *names* differ. The `defaultVisual` (plain edges → `--fc-edge-idle`) and the `if (!blockType) return defaultVisual;` guard above stay as-is.

- [ ] **Step 3: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-gesture-smoke.spec.ts` → green. **If the branch-metadata test asserts a literal `--fc-state-*` token string, update it to the `--fc-branch-*` equivalent (same computed color).**
  - `cd FlowCanvas && npm run test:e2e:parity` → green (edge label/style are not exported; `branchPath` unchanged → round-trip byte-identical).
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green.

- [ ] **Step 4: Commit.**

```bash
git add FlowCanvas/src/stores/slices/graphSlice.ts
git commit -m "refactor(flow-canvas): route edge branch colors through branchColorVar; drop branch-edge dashes"
```

---

## Section 2: Universal edge rendering (arrowhead + gradient)

### Task 3: Tokenized arrowhead markers (`EdgeMarkers.tsx`) + mount

**Files:**
- Create: `FlowCanvas/src/nodes/EdgeMarkers.tsx`
- Modify: `FlowCanvas/src/App.tsx` (mount the component)

- [ ] **Step 1: Write `EdgeMarkers.tsx`.** A hidden SVG of one arrowhead `<marker>` per branch/state token plus idle/accent, and the shared packet glow filter. `markerIdForStroke` maps an edge's `style.stroke` var-token to its marker id (fallback `idle`). All colors are `var(--fc-*)` — gate-safe.

```tsx
// FlowCanvas/src/nodes/EdgeMarkers.tsx
// Tokenized arrowhead markers + the shared packet glow filter for Live Wires (Wave 2b).
// Rendered once (hidden) in App; url(#id) marker refs resolve document-wide. Every fill is a
// var(--fc-*) token (Decision #4 gate-safe). markerIdForStroke maps an edge's resolved
// style.stroke token to its marker id so the arrowhead matches the edge color.
import { type JSX } from 'react';

const EDGE_MARKERS = [
  { key: 'then', colorVar: 'var(--fc-branch-then)' },
  { key: 'else', colorVar: 'var(--fc-branch-else)' },
  { key: 'elif', colorVar: 'var(--fc-branch-elif)' },
  { key: 'do', colorVar: 'var(--fc-branch-do)' },
  { key: 'try', colorVar: 'var(--fc-branch-try)' },
  { key: 'catch', colorVar: 'var(--fc-branch-catch)' },
  { key: 'finally', colorVar: 'var(--fc-branch-finally)' },
  { key: 'case', colorVar: 'var(--fc-branch-case)' },
  { key: 'default', colorVar: 'var(--fc-branch-default)' },
  { key: 'parallel', colorVar: 'var(--fc-branch-parallel)' },
  { key: 'fallback', colorVar: 'var(--fc-branch-fallback)' },
  { key: 'idle', colorVar: 'var(--fc-edge-idle)' },
  { key: 'accent', colorVar: 'var(--fc-accent)' },
] as const;

/** Map an edge's resolved style.stroke (a var(--fc-*) token) to its arrowhead marker id. */
export function markerIdForStroke(stroke: string | undefined): string {
  const found = EDGE_MARKERS.find((m) => m.colorVar === stroke);
  return `fc-arrow-${found ? found.key : 'idle'}`;
}

export function EdgeMarkers(): JSX.Element {
  return (
    <svg width="0" height="0" aria-hidden="true" style={{ position: 'absolute', overflow: 'hidden' }}>
      <defs>
        {EDGE_MARKERS.map(({ key, colorVar }) => (
          <marker
            key={key}
            id={`fc-arrow-${key}`}
            viewBox="0 0 10 10"
            refX="9"
            refY="5"
            markerWidth="7"
            markerHeight="7"
            orient="auto-start-reverse"
          >
            <path d="M0 0 L10 5 L0 10 z" fill={colorVar} />
          </marker>
        ))}
        <filter id="fc-packet-glow" x="-150%" y="-150%" width="400%" height="400%">
          <feGaussianBlur stdDeviation="2" result="b" />
          <feMerge>
            <feMergeNode in="b" />
            <feMergeNode in="SourceGraphic" />
          </feMerge>
        </filter>
      </defs>
    </svg>
  );
}
```

- [ ] **Step 2: Mount `<EdgeMarkers />` in `App.tsx`.** Add the import beside the other node imports near the top:

```tsx
import { EdgeMarkers } from './nodes/EdgeMarkers';
```

  And render it as the first child of the top-level return `<div>` (line ~364, just before `<Toolbar />`):

```tsx
    <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column' }}>
      <EdgeMarkers />
      <Toolbar />
```

- [ ] **Step 3: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green (marker fills are `var()` tokens; no hex).
  - `cd FlowCanvas && npm run test:e2e:parity` → green (markers are inert defs; no graph/export impact).

- [ ] **Step 4: Commit.**

```bash
git add FlowCanvas/src/nodes/EdgeMarkers.tsx FlowCanvas/src/App.tsx
git commit -m "feat(flow-canvas): tokenized arrowhead marker registry + shared packet glow (EdgeMarkers)"
```

### Task 4: Rewrite `AnimatedEdge` (color + gradient + arrowhead) + make all edges animated

**Files:**
- Modify: `FlowCanvas/src/nodes/AnimatedEdge.tsx` (full rewrite, no packet yet)
- Modify: `FlowCanvas/src/App.tsx` (all edges `animated`)

- [ ] **Step 1: Rewrite `AnimatedEdge.tsx`** to consume `style.stroke`, render a `userSpaceOnUse` gradient stroke + tokenized arrowhead, and remove the marching-ants `<style>`. (Packet added in Task 5 — leave it out here.)

```tsx
import { memo } from 'react';
import { BaseEdge, getSmoothStepPath, type EdgeProps } from '@xyflow/react';
import { mix } from '../utils/tokens';
import { markerIdForStroke } from './EdgeMarkers';
import { useFlowStore } from '../stores/useFlowStore';

function AnimatedEdge(props: EdgeProps) {
  const { id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, source, style } = props;

  const isRunning = useFlowStore((s) => s.isRunning);
  const blockStates = useFlowStore((s) => s.blockStates);

  const [edgePath] = getSmoothStepPath({
    sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, borderRadius: 8,
  });

  // Color comes from the edge's style.stroke (set by getBranchVisual / defaultEdgeOptions /
  // selection). Branch edges = --fc-branch-*, continuation = --fc-accent, plain = --fc-edge-idle.
  const color = (typeof style?.stroke === 'string' ? style.stroke : undefined) ?? 'var(--fc-edge-idle)';
  const markerId = markerIdForStroke(color);

  const sourceState = blockStates.get(source);
  const active = isRunning && (sourceState === 'success' || sourceState === 'running');

  const gradientId = `fc-grad-${id}`;
  const strokeWidth = typeof style?.strokeWidth === 'number' ? style.strokeWidth : active ? 2.5 : 2;

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
        path={edgePath}
        markerEnd={`url(#${markerId})`}
        style={{ ...style, stroke: `url(#${gradientId})`, strokeWidth }}
      />
    </>
  );
}

export default memo(AnimatedEdge);
```

- [ ] **Step 2: Make every edge use `AnimatedEdge`.** In `App.tsx`, change `displayEdges` (line 353) from the running-only injection to always-animated:

```tsx
  const displayEdges = edges.map((e) => ({
    ...e,
    type: 'animated',
    selected: selectedEdgeIds.has(e.id),
    style: {
      ...e.style,
      ...(selectedEdgeIds.has(e.id)
        ? { stroke: selectedStroke, strokeWidth: 3 }
        : {}),
    },
  }));
```

  And flip `defaultEdgeOptions` (line 399) so edges with no explicit type also animate:

```tsx
              defaultEdgeOptions={{ type: 'animated', style: { stroke: 'var(--fc-edge-idle)' } }}
```

- [ ] **Step 3: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green (gradient stops use `color`/`mix()`; marker via token; no hex, no `var()`+alpha concat).
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-run-timing.spec.ts e2e/flow-canvas-reduced-motion.spec.ts` → green (node box-shadows unaffected; edges carry no animation yet).
  - `cd FlowCanvas && npm run test:e2e:parity` → green (no `node.data`/export change).

- [ ] **Step 4: Commit.**

```bash
git add FlowCanvas/src/nodes/AnimatedEdge.tsx FlowCanvas/src/App.tsx
git commit -m "feat(flow-canvas): universal AnimatedEdge — branch-color gradient stroke + tokenized arrowhead"
```

---

## Section 3: Traveling packet + reduced-motion

### Task 5: Pulse-dot packet via CSS `offset-path`, gated by reduced-motion

**Files:**
- Create: `FlowCanvas/src/nodes/animatededge.css`
- Modify: `FlowCanvas/src/nodes/AnimatedEdge.tsx`

- [ ] **Step 1: Write `animatededge.css`** — the shared packet keyframes + class (one keyframe for all edges, not per-edge):

```css
/* FlowCanvas/src/nodes/animatededge.css
 * Live-wire data packet (Wave 2b). One shared keyframe set drives every active edge's dot;
 * offset-path is set per-edge inline (each edge's path). The global reducedMotion.css blanket
 * also disables these, but AnimatedEdge additionally does not render the packet under reduced
 * motion (so there is no frozen dot at the source). No color literals here — gate-safe. */
@keyframes fc-packet-travel {
  from { offset-distance: 0%; }
  to   { offset-distance: 100%; }
}
@keyframes fc-packet-pulse {
  0%, 100% { transform: scale(1); }
  50%      { transform: scale(1.4); }
}
.fc-edge-packet {
  offset-rotate: 0deg;
  offset-anchor: center;
  transform-box: fill-box;
  transform-origin: center;
  animation: fc-packet-travel 1.6s linear infinite, fc-packet-pulse 0.8s ease-in-out infinite;
}
```

- [ ] **Step 2: Render the packet in `AnimatedEdge.tsx`.** Add the CSS import and the `reducedMotion` selector, then render the packet circle when active and motion is on. Add the import beside the others:

```tsx
import './animatededge.css';
```

  Add the selector next to the existing store reads:

```tsx
  const reducedMotion = useFlowStore((s) => s.reducedMotion);
```

  And add the packet as a sibling after `<BaseEdge ... />`, inside the fragment:

```tsx
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
```

- [ ] **Step 3: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts e2e/flow-canvas-reduced-motion.spec.ts e2e/flow-canvas-run-timing.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 4: Commit.**

```bash
git add FlowCanvas/src/nodes/animatededge.css FlowCanvas/src/nodes/AnimatedEdge.tsx
git commit -m "feat(flow-canvas): traveling pulse-dot packet (offset-path) on active edges; reduced-motion gated"
```

---

## Section 4: Tests & integration

### Task 6: `flow-canvas-live-wires.spec.ts` + extend the token-sweep fixture

**Files:**
- Create: `FlowCanvas/e2e/flow-canvas-live-wires.spec.ts`
- Modify: `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts`

- [ ] **Step 1: Write `flow-canvas-live-wires.spec.ts`.** Loads a two-node graph + one edge whose `style.stroke` is a token (simulating the post-`getBranchVisual` state), so it tests `AnimatedEdge` consumption deterministically.

```ts
import { expect, test, type Page } from '@playwright/test';
import type { GraphFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, installHostMessageCapture, loadGraphFixture, postHostMessage, waitForOutgoingMessage,
} from './support/harness';

function edgeFixture(stroke: string): GraphFixture {
  return {
    nodes: [
      { id: 'src', type: 'block', position: { x: 80, y: 120 }, data: { blockType: 'send', label: 'Send', props: {} } },
      { id: 'dst', type: 'block', position: { x: 420, y: 120 }, data: { blockType: 'print', label: 'Print', props: {} } },
    ],
    edges: [{ id: 'e1', source: 'src', target: 'dst', style: { stroke } }],
  };
}

async function resolveVar(page: Page, name: string): Promise<string> {
  return page.evaluate((n) => {
    const probe = document.createElement('div');
    probe.style.color = `var(${n})`;
    document.body.appendChild(probe);
    const v = getComputedStyle(probe).color;
    probe.remove();
    return v;
  }, name);
}

const edgePath = (page: Page) => page.locator('path#e1');
const lastStop = (page: Page) => page.locator('#fc-grad-e1 stop').last();

test.describe('Flow Canvas Live Wires', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('every edge renders a tokenized arrowhead marker', async ({ page }) => {
    await loadGraphFixture(page, edgeFixture('var(--fc-edge-idle)'));
    await expect(edgePath(page)).toBeVisible();
    expect(await edgePath(page).getAttribute('marker-end')).toBe('url(#fc-arrow-idle)');
    await expect(page.locator('#fc-arrow-idle')).toHaveCount(1);
  });

  test('branch edge resolves to its branch token (then) on marker + gradient end', async ({ page }) => {
    await loadGraphFixture(page, edgeFixture('var(--fc-branch-then)'));
    expect(await edgePath(page).getAttribute('marker-end')).toBe('url(#fc-arrow-then)');
    const stopColor = await lastStop(page).evaluate((el) => getComputedStyle(el as Element).stopColor);
    expect(stopColor).toBe(await resolveVar(page, '--fc-branch-then'));
  });

  test('plain edge gradient end resolves to --fc-edge-idle', async ({ page }) => {
    await loadGraphFixture(page, edgeFixture('var(--fc-edge-idle)'));
    const stopColor = await lastStop(page).evaluate((el) => getComputedStyle(el as Element).stopColor);
    expect(stopColor).toBe(await resolveVar(page, '--fc-edge-idle'));
  });

  test('packet travels only while running, and never under reduced-motion', async ({ page }) => {
    await loadGraphFixture(page, edgeFixture('var(--fc-branch-then)'));
    const packet = page.locator('.fc-edge-packet');
    await expect(packet).toHaveCount(0); // at rest

    await postHostMessage(page, { type: 'execution-started' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'src', state: 'success' });
    await expect(packet).toHaveCount(1);
    expect(await packet.evaluate((el) => getComputedStyle(el as Element).animationName)).toContain('fc-packet-travel');

    await page.getByRole('button', { name: '▶ Motion' }).click(); // enable reduced motion
    await expect(packet).toHaveCount(0);
  });
});
```

  > If `loadGraphFixture` rewrites edge styles on load (so the explicit `style.stroke` doesn't survive), set the same value via `data` and adjust — but the harness loads `GraphFixture` nodes/edges directly, so the explicit `style.stroke` is preserved (this is the same direct-fixture pattern the Wave 2a specs use).

- [ ] **Step 2: Run the new spec.**

Run: `cd FlowCanvas && npx playwright test e2e/flow-canvas-live-wires.spec.ts`
Expected: 4 passed.

- [ ] **Step 3: Extend the token-sweep fixture to include an edge.** In `flow-canvas-token-sweep.spec.ts`, add a second node + a branch edge to the fixture the no-hex test mounts (so the scan covers a rendered gradient/arrowhead/edge). Locate `createSshBlockFixture()` (the inline fixture) and give it a second node + edge, e.g.:

```ts
function createSshBlockFixture(): GraphFixture {
  return {
    nodes: [
      { id: 'node-ssh', type: 'block', position: { x: 140, y: 120 },
        data: { blockType: 'send', label: 'Send', props: { command: 'echo hello' } } },
      { id: 'node-ssh-2', type: 'block', position: { x: 460, y: 120 },
        data: { blockType: 'print', label: 'Print', props: {} } },
    ],
    edges: [{ id: 'sweep-edge', source: 'node-ssh', target: 'node-ssh-2', style: { stroke: 'var(--fc-branch-then)' } }],
  };
}
```

  Keep the existing assertions; the point is the scan now traverses a rendered edge. (If the file already imports a shared fixture rather than defining one inline, add the second node + edge there instead — match the file's existing shape.)

- [ ] **Step 4: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-live-wires.spec.ts e2e/flow-canvas-token-sweep.spec.ts` → all green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 5: Commit.**

```bash
git add FlowCanvas/e2e/flow-canvas-live-wires.spec.ts FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts
git commit -m "test(flow-canvas): live-wires edge spec (arrowhead/branch-gradient/packet) + edge in token-sweep"
```

### Task 7: Full-suite verification + embedded dist rebuild

**Files:**
- Test: full Playwright suite + parity + dist gate; `dotnet build SSH_Helper.sln`

- [ ] **Step 1: Full e2e suite.** `cd FlowCanvas && npm run test:e2e`. Expect green except the known pre-existing parity-CLI **parallel build-lock race** (gesture-smoke / preset-parity / connection-guards / properties-typing fail/flake when concurrent workers collide on `obj/SSH_Helper.dll` via VBCSCompiler/Defender). Confirm those are the only failures.

- [ ] **Step 2: Parity gate (round-trip proof).** `cd FlowCanvas && npm run test:e2e:parity` → **22/22** under `--workers=1`. Because no `node.data`/`exportGraph.ts`/`FlowCanvasBridge.cs` changed, the export is byte-identical.

- [ ] **Step 3: Dist build gate.** `cd FlowCanvas && npm run test:e2e:dist` → green (run `--workers=1` if the parity-CLI build-lock race appears: `npx playwright test --workers=1 --config playwright.preview.config.ts`). Proves SVG markers + `offset-path` packet survive the production single-asset bundle.

- [ ] **Step 4: Embedded rebuild.** `dotnet build SSH_Helper.sln` → 0 errors (the `BuildFlowCanvas` target rebuilds + re-embeds `FlowCanvas/dist`).

- [ ] **Step 5: Manual smoke (fresh-eyes pass — PENDING-HUMAN, cannot be automated).** Launch the app, open Flow Canvas, import a script with an `if`/`try`/`switch` container + plain steps. Confirm: every edge has an arrowhead; branch edges read their branch color (then=green, else=red, elif/case=amber, finally=accent) and are solid (no dashes); run a script and confirm a single glowing dot travels source→target along active edges (no marching ants); toggle ⏸ Calm and confirm packets stop while edges/arrowheads stay. (Subagents: confirm `dotnet build` + dist gate succeed, record this as PENDING-HUMAN.)

- [ ] **Step 6: Commit.**

```bash
git add -A
git commit -m "chore(flow-canvas): rebuild embedded dist for Wave 2b Live Wires"
```

  > Note: `FlowCanvas/dist/` is gitignored (rebuilt at `dotnet build` time), so this records the integration-verification pass like the Wave 1/2a dist commits. `git add -A` here must add **only** intended files — confirm `git status` shows nothing unexpected and **never** stage `.gitignore` changes or unrelated untracked files; if the tree has stray untracked files, stage explicitly instead.

---

## Wave 2b Live Wires Exit Criteria

- [ ] `cd FlowCanvas && npm run build` exits 0.
- [ ] Every edge renders a tokenized arrowhead (the never-assigned-`markerEnd` bug is fixed); `flow-canvas-live-wires.spec.ts` green.
- [ ] Branch edges carry their branch color via `branchColorVar` at rest **and** running, solid (no resting dashes); plain edges `--fc-edge-idle`; continuation `--fc-accent`.
- [ ] Source→target gradient stroke renders, oriented along the edge (`userSpaceOnUse`).
- [ ] A single pulse-dot packet travels source→target on active edges while running; absent at rest and under `.fc-reduced-motion`.
- [ ] Marching-ants animation + per-edge inline `<style>` removed; one shared keyframe; rest/run edge-type split removed.
- [ ] NO hardcoded hex outside `tokens.css`/`tokens.ts`; token-sweep green incl. a rendered edge; no `var()`+alpha concat.
- [ ] Round-trip unaffected: parity 22/22 under `--workers=1`; no `node.data`/`exportGraph.ts`/`FlowCanvasBridge.cs` change.
- [ ] `npm run test:e2e:dist` green; `dotnet build SSH_Helper.sln` 0 errors (dist re-embedded).
- [ ] NO new npm runtime dependency (Framer Motion still deferred to a later 2b cycle).
- [ ] Full `npm run test:e2e` green except the known parity-CLI parallel build-lock race (green serialized); each task ended with a commit.

## Risks / notes

- **`offset-path` on an SVG `<circle>`:** Chromium (WebView2) supports CSS motion path on SVG elements; `offset-anchor: center` + `transform-box: fill-box` keep the dot centered on the path. If a build target rejects it, the fallback is SMIL `<animateMotion><mpath href="#<pathId>"/>` — but the approved approach is `offset-path`.
- **branchPath→token nuance (must preserve):** `getBranchVisual` keeps its blockType-aware resolution — loop `do`=amber vs `try` body=green; `elif/<n>/then`=amber vs plain `then`=green; switch `default`=red vs `case`=amber. Do **not** route the edge's raw `branchPath` through `branchKeyFromStepPath` (it scans last-to-first and would mis-map `elif/0/then`→`then`).
- **Selected edges:** `selectedStroke` is not in the marker registry, so a selected edge falls back to `fc-arrow-idle`; its emphasis still reads via `strokeWidth: 3` + the gradient base. Acceptable; revisit only if it looks off in the manual smoke.
- **`stopColor` vs `color` compare:** the spec compares a gradient `stop-color` computed value to a probe's `color`; Chromium serializes the same resolved token identically across both properties.
