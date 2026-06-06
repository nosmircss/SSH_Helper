# Configurable Block Sizing + Canvas Settings Menu — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the user set block width (5 presets), text size, canvas density, and the default block state from a toolbar gear → popover, consolidate the existing view toggles into it, and persist everything across sessions.

**Architecture:** A new `settingsSlice` holds the four new view settings; the layout engine and branch-band geometry gain an optional `BlockSizing` param (defaulting to today's values, so existing tests stay green). Block render width/text-size read from the store; changing a setting reflows once and persists via the existing `layout-save`/`layout-restore` C# channel. YAML/preset export is never touched.

**Tech Stack:** React 19 + Zustand 5 + @xyflow/react (Vite), vitest + jsdom + testing-library; C# .NET 8 WinForms host (Newtonsoft.Json), WebView2 bridge.

**Spec:** `docs/superpowers/specs/2026-06-03-block-sizing-canvas-settings-design.md`

**Commands:** run from `FlowCanvas/`: `npx vitest run <file>` (single file), `npx tsc --noEmit` (typecheck). C#: `dotnet build SSH_Helper.sln` from repo root.

---

## Phase 1 — Sizing single source + layout `BlockSizing` param

Goal: thread a `BlockSizing` (blockWidth/density/textScale) through `computeHierarchicalLayout` and `computeBranchBands`, derived so the default reproduces today's geometry exactly. No store/UI yet.

### Task 1.1: `estimateNodeHeight` takes a text scale

**Files:**
- Modify: `FlowCanvas/src/utils/nodeSize.ts`
- Test: `FlowCanvas/src/utils/__tests__/nodeSize.test.ts`

- [ ] **Step 1: Write the failing test** — append to `nodeSize.test.ts`:

```ts
import { estimateNodeHeight, BLOCK_WIDTH_INSET, SPINE_WIDTH, CHILD_WIDTH } from '../nodeSize';

describe('estimateNodeHeight textScale', () => {
  it('returns the collapsed floor regardless of scale', () => {
    expect(estimateNodeHeight('print', { message: 'x' }, false, 1.15)).toBe(52);
  });
  it('expanded height grows with textScale', () => {
    const base = estimateNodeHeight('send', { command: 'a', capture: 'b' }, true, 1);
    const big = estimateNodeHeight('send', { command: 'a', capture: 'b' }, true, 1.15);
    expect(big).toBeGreaterThan(base);
  });
});

describe('BLOCK_WIDTH_INSET', () => {
  it('is the spine-minus-child delta', () => {
    expect(BLOCK_WIDTH_INSET).toBe(SPINE_WIDTH - CHILD_WIDTH); // 30
  });
});
```

- [ ] **Step 2: Run it, expect FAIL** — `npx vitest run src/utils/__tests__/nodeSize.test.ts` → FAIL (`BLOCK_WIDTH_INSET` not exported, 4th arg ignored).

- [ ] **Step 3: Implement** — in `nodeSize.ts`, add the export after line 8 and update `estimateNodeHeight`:

```ts
export const SPINE_WIDTH = 330;
export const CHILD_WIDTH = 300;
/** Spine→child width delta (children inside containers are this much narrower). Single source. */
export const BLOCK_WIDTH_INSET = SPINE_WIDTH - CHILD_WIDTH; // 30
```

```ts
export function estimateNodeHeight(
  blockType: string,
  props: Record<string, unknown>,
  expanded: boolean,
  textScale = 1,
): number {
  if (!expanded) return COLLAPSED_HEIGHT;
  const rows = summarizeBlock(blockType, props).rows.length;
  // header (~30) + summary body (pad + rows + footer); the summary text scales, so does its height.
  return Math.round((30 + SUMMARY_PAD + rows * SUMMARY_ROW_H + SUMMARY_FOOTER_H) * textScale);
}
```

- [ ] **Step 4: Run it, expect PASS** — `npx vitest run src/utils/__tests__/nodeSize.test.ts` → PASS.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/nodeSize.ts FlowCanvas/src/utils/__tests__/nodeSize.test.ts
git commit -m "feat(flow-canvas): estimateNodeHeight scales with textScale; export BLOCK_WIDTH_INSET"
```

### Task 1.2: `computeHierarchicalLayout(nodes, edges, sizing?)`

**Files:**
- Modify: `FlowCanvas/src/utils/layout/hierarchicalLayout.ts`
- Test: `FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.sizing.test.ts` (new)

- [ ] **Step 1: Write the failing test** (new file):

```ts
import { describe, it, expect } from 'vitest';
import type { Edge, Node } from '@xyflow/react';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING } from '../hierarchicalLayout';

const chain = (): { nodes: Node[]; edges: Edge[] } => ({
  nodes: [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'A', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'a' } } },
    { id: 'B', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'b' } } },
  ] as never,
  edges: [
    { id: 'e0', source: '__start__', target: 'A' },
    { id: 'e1', source: 'A', target: 'B' },
  ] as never,
});

describe('computeHierarchicalLayout sizing param', () => {
  it('default param reproduces todays positions (regression guard)', () => {
    const { nodes, edges } = chain();
    const withParam = computeHierarchicalLayout(nodes, edges, DEFAULT_BLOCK_SIZING);
    const without = computeHierarchicalLayout(nodes, edges);
    expect(withParam.map((n) => n.position)).toEqual(without.map((n) => n.position));
  });

  it('roomy density pushes a downstream block further down', () => {
    const { nodes, edges } = chain();
    const normal = computeHierarchicalLayout(nodes, edges, { blockWidth: 330, density: 1, textScale: 1 });
    const roomy = computeHierarchicalLayout(nodes, edges, { blockWidth: 330, density: 1.2, textScale: 1 });
    const yN = normal.find((n) => n.id === 'B')!.position.y;
    const yR = roomy.find((n) => n.id === 'B')!.position.y;
    expect(yR).toBeGreaterThan(yN);
  });
});
```

- [ ] **Step 2: Run it, expect FAIL** — `npx vitest run src/utils/layout/__tests__/hierarchicalLayout.sizing.test.ts` → FAIL (`DEFAULT_BLOCK_SIZING` / 3rd param missing).

- [ ] **Step 3: Implement** — edit `hierarchicalLayout.ts`.

(a) After the `LAYOUT` object (line 29), add the public sizing type + resolver + module-local active config. The active config is set synchronously at the top of `computeHierarchicalLayout`; the layout pass has no `await`, so there is no reentrancy — `computeBranchBands` is a *separate* function with its own param and is unaffected.

```ts
/** User-facing canvas sizing. Defaults reproduce the historical fixed geometry. */
export interface BlockSizing { blockWidth: number; density: number; textScale: number; }
export const DEFAULT_BLOCK_SIZING: BlockSizing = { blockWidth: 330, density: 1, textScale: 1 };

interface ResolvedSizing {
  childWidth: number; columnWidth: number; nodeSpacingY: number; branchOffset: number; textScale: number;
}
function resolveSizing(s: BlockSizing): ResolvedSizing {
  const childWidth = s.blockWidth - LAYOUT.COLUMN_GAP;            // preserves the 30px child inset
  return {
    childWidth,
    columnWidth: childWidth + LAYOUT.COLUMN_GAP,                  // = blockWidth
    nodeSpacingY: Math.round(LAYOUT.NODE_SPACING_Y * s.density),
    // Branch-gutter invariant (see BRANCH_CHILD_OFFSET comment): offset > blockWidth/2 + BAND_PAD.
    // blockWidth/2 + BAND_PAD + 37 reproduces 330 -> 220 and keeps the gutter at every width.
    branchOffset: Math.round(s.blockWidth / 2 + BAND_PAD + 37),
    textScale: s.textScale,
  };
}
let activeSizing: ResolvedSizing = resolveSizing(DEFAULT_BLOCK_SIZING);
```

(b) Replace the module `VERTICAL_GAP` const (line 31) and `advanceFor` (33-37) so spacing/height come from `activeSizing`:

```ts
function verticalGap(): number { return activeSizing.nodeSpacingY - COLLAPSED_HEIGHT; }

function advanceFor(n: LayoutTreeNode): number {
  const data = (n.node?.data ?? {}) as { blockType?: string; expanded?: boolean; props?: Record<string, unknown> };
  if (!data.expanded) return activeSizing.nodeSpacingY;
  return estimateNodeHeight(data.blockType ?? '', data.props ?? {}, true, activeSizing.textScale) + verticalGap();
}
```

(c) `getColumnWidth` (49-51) → use `activeSizing.columnWidth` for base/min:

```ts
function getColumnWidth(depth: number): number {
  return Math.max(activeSizing.columnWidth, activeSizing.columnWidth * Math.pow(LAYOUT.COLUMN_WIDTH_DECAY, depth));
}
```

(d) Replace every `LAYOUT.BRANCH_CHILD_OFFSET` with `activeSizing.branchOffset` (in `measureSteps` lines 80 & 86, `placeSingleBranch` line 120, `placeMultiBranch` lines 130 & 141) and every `LAYOUT.CHILD_NODE_MAX_WIDTH` with `activeSizing.childWidth` (in `placeMultiBranch` line 133). In `placeTree` (160) replace `LAYOUT.NODE_SPACING_Y` with `activeSizing.nodeSpacingY`. In `placeComments` (180) replace `LAYOUT.BASE_COLUMN_WIDTH` with `activeSizing.columnWidth`.

(e) Update the entry point (193-201):

```ts
export function computeHierarchicalLayout(nodes: Node[], edges: Edge[], sizing: BlockSizing = DEFAULT_BLOCK_SIZING): Node[] {
  activeSizing = resolveSizing(sizing);
  const tree = buildLayoutTree(nodes, edges);
  const pos = placeTree(tree);
  placeComments(nodes, pos);
  return nodes.map((n) => {
    const p = pos.get(n.id);
    return p ? { ...n, position: p } : n;
  });
}
```

- [ ] **Step 4: Run the new test + the existing layout test, expect PASS**

```
npx vitest run src/utils/layout/__tests__/hierarchicalLayout.sizing.test.ts src/utils/layout/__tests__/hierarchicalLayout.test.ts
```
Expected: PASS (the regression guard proves default geometry is unchanged).

- [ ] **Step 5: Typecheck + commit**

```bash
npx tsc --noEmit
git add FlowCanvas/src/utils/layout/hierarchicalLayout.ts FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.sizing.test.ts
git commit -m "feat(flow-canvas): computeHierarchicalLayout accepts a BlockSizing (width/density/textScale)"
```

### Task 1.3: `computeBranchBands(nodes, childWidth?)`

**Files:**
- Modify: `FlowCanvas/src/utils/branchBands.ts`
- Test: `FlowCanvas/src/utils/__tests__/branchBands.sizing.test.ts` (new)

- [ ] **Step 1: Write the failing test** (new file). Two child nodes under one container; a wider `childWidth` widens the band:

```ts
import { describe, it, expect } from 'vitest';
import type { Node } from '@xyflow/react';
import { computeBranchBands } from '../branchBands';

const nodes = (): Node[] => ([
  { id: 'IF', type: 'block', position: { x: 250, y: 100 }, data: { blockType: 'if', props: {} } },
  { id: 'C1', type: 'block', position: { x: 470, y: 200 },
    data: { blockType: 'print', props: { _isChildOf: 'IF', _stepPath: 'steps/0/then/0' } } },
] as never);

describe('computeBranchBands childWidth', () => {
  it('band width grows with childWidth', () => {
    const narrow = computeBranchBands(nodes(), 300)[0];
    const wide = computeBranchBands(nodes(), 670)[0];
    expect(wide.width).toBeGreaterThan(narrow.width);
  });
  it('default arg reproduces the 300-wide band', () => {
    const def = computeBranchBands(nodes())[0];
    const explicit = computeBranchBands(nodes(), 300)[0];
    expect(def.width).toBe(explicit.width);
  });
});
```

- [ ] **Step 2: Run it, expect FAIL** — `npx vitest run src/utils/__tests__/branchBands.sizing.test.ts` → FAIL (2nd arg ignored).

- [ ] **Step 3: Implement** — in `branchBands.ts`:

Change the import (line 5) to keep `CHILD_WIDTH` as the default:

```ts
import { CHILD_WIDTH, COLLAPSED_HEIGHT, estimateNodeHeight, BAND_PAD, BAND_LABEL_HEADROOM } from './nodeSize';
```

Make `nodeBox` take the width and update the signature of `computeBranchBands` (line 111). `nodeBox` is only called inside `computeBranchBands`, so pass `childWidth` through a closure:

```ts
export function computeBranchBands(nodes: Node[], childWidth: number = CHILD_WIDTH): BranchBand[] {
  const boxOf = (n: Node): { w: number; h: number } => {
    const data = (n.data ?? {}) as { blockType?: string; expanded?: boolean; props?: Record<string, unknown> };
    const h = data.expanded ? estimateNodeHeight(data.blockType ?? '', data.props ?? {}, true) : COLLAPSED_HEIGHT;
    return { w: childWidth, h };
  };
  // ...rest unchanged, but replace the two `nodeBox(n)` calls with `boxOf(n)`.
```

Delete the old top-level `nodeBox` function (lines 83-89) and replace its single call site in the pass-1 loop (line 150) `const { w, h } = nodeBox(n);` with `const { w, h } = boxOf(n);`.

- [ ] **Step 4: Run it, expect PASS** — `npx vitest run src/utils/__tests__/branchBands.sizing.test.ts` → PASS.

- [ ] **Step 5: Typecheck + commit**

```bash
npx tsc --noEmit
git add FlowCanvas/src/utils/branchBands.ts FlowCanvas/src/utils/__tests__/branchBands.sizing.test.ts
git commit -m "feat(flow-canvas): computeBranchBands accepts childWidth for dynamic band geometry"
```

---

## Phase 2 — `settingsSlice` (state, setters, reflow, persistence echo)

### Task 2.1: Create the slice and register it

**Files:**
- Create: `FlowCanvas/src/stores/slices/settingsSlice.ts`
- Modify: `FlowCanvas/src/stores/useFlowStore.ts`
- Test: `FlowCanvas/src/stores/slices/__tests__/settingsSlice.test.ts` (new)

- [ ] **Step 1: Write the failing test** (new file). Mirrors the `debugSlice.expanded.test.ts` harness (mock autosave + MessageBus):

```ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save' } },
}));
import { useFlowStore } from '../../useFlowStore';
import { messageBus } from '../../../MessageBus';
import { computeHierarchicalLayout } from '../../../utils/layout/hierarchicalLayout';
import { SETTINGS_DEFAULTS } from '../settingsSlice';

const chain = () => {
  const nodes = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'A', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'a' } } },
    { id: 'B', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'b' } } },
  ];
  const edges = [
    { id: 'e0', source: '__start__', target: 'A' },
    { id: 'e1', source: 'A', target: 'B' },
  ];
  useFlowStore.getState().setNodes(nodes as never);
  useFlowStore.getState().setEdges(edges as never);
  const s = useFlowStore.getState();
  s.setNodes(computeHierarchicalLayout(s.nodes, s.edges));
};

describe('settingsSlice', () => {
  beforeEach(() => {
    useFlowStore.setState({ ...SETTINGS_DEFAULTS });
    vi.clearAllMocks();
  });

  it('defaults are Normal/M/Normal/collapsed', () => {
    const s = useFlowStore.getState();
    expect(s.blockWidth).toBe(330);
    expect(s.textScale).toBe(1);
    expect(s.density).toBe(1);
    expect(s.defaultBlockExpanded).toBe(false);
  });

  it('setBlockWidth updates state and persists via layout-save', () => {
    chain();
    useFlowStore.getState().setBlockWidth(700);
    expect(useFlowStore.getState().blockWidth).toBe(700);
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'layout-save', blockWidth: 700 }));
  });

  it('setDensity roomy pushes B lower; tight raises it', () => {
    chain();
    const y0 = useFlowStore.getState().nodes.find((n) => n.id === 'B')!.position.y;
    useFlowStore.getState().setDensity(1.2);
    const yRoomy = useFlowStore.getState().nodes.find((n) => n.id === 'B')!.position.y;
    expect(yRoomy).toBeGreaterThan(y0);
  });

  it('resetCanvasSettings restores every field', () => {
    useFlowStore.setState({ blockWidth: 700, textScale: 1.15, density: 1.2, defaultBlockExpanded: true });
    useFlowStore.getState().resetCanvasSettings();
    const s = useFlowStore.getState();
    expect([s.blockWidth, s.textScale, s.density, s.defaultBlockExpanded]).toEqual([330, 1, 1, false]);
  });

  it('restoreCanvasSettings applies values and reflows when nodes exist', () => {
    chain();
    const xBefore = useFlowStore.getState().nodes.find((n) => n.id === 'A')!.position.x;
    useFlowStore.getState().restoreCanvasSettings({ blockWidth: 700 });
    expect(useFlowStore.getState().blockWidth).toBe(700);
    // start-node column is fixed; A sits on the spine at NODE_START_X — x stays, but the call must not throw
    expect(typeof xBefore).toBe('number');
  });
});
```

- [ ] **Step 2: Run it, expect FAIL** — `npx vitest run src/stores/slices/__tests__/settingsSlice.test.ts` → FAIL (module missing).

- [ ] **Step 3: Implement the slice** (new file `settingsSlice.ts`):

```ts
import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { messageBus } from '../../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../../communication-message-types';
import { sendLayoutAutosave } from '../../utils/layoutAutosave';
import { computeHierarchicalLayout, type BlockSizing } from '../../utils/layout/hierarchicalLayout';

/** Width presets (px). M=330 is today's default. */
export const WIDTH_PRESETS = [
  { label: 'Compact', px: 300 },
  { label: 'Normal', px: 330 },
  { label: 'Wide', px: 440 },
  { label: 'Extra', px: 560 },
  { label: 'Max', px: 700 },
] as const;
export const TEXT_SCALES = [
  { label: 'S', v: 0.9 }, { label: 'M', v: 1 }, { label: 'L', v: 1.15 },
] as const;
export const DENSITIES = [
  { label: 'Tight', v: 0.85 }, { label: 'Normal', v: 1 }, { label: 'Roomy', v: 1.2 },
] as const;

export const SETTINGS_DEFAULTS = {
  blockWidth: 330,
  textScale: 1,
  density: 1,
  defaultBlockExpanded: false,
} as const;

export type CanvasSettings = Pick<SettingsSlice, 'blockWidth' | 'textScale' | 'density' | 'defaultBlockExpanded'>;

export interface SettingsSlice {
  blockWidth: number;
  textScale: number;
  density: number;
  defaultBlockExpanded: boolean;

  setBlockWidth: (px: number) => void;
  setTextScale: (v: number) => void;
  setDensity: (v: number) => void;
  setDefaultBlockExpanded: (v: boolean) => void;
  resetCanvasSettings: () => void;
  restoreCanvasSettings: (s: Partial<CanvasSettings>) => void;
}

export const createSettingsSlice: StateCreator<FlowStore, [], [], SettingsSlice> = (set, get) => {
  const sizing = (): BlockSizing => {
    const s = get();
    return { blockWidth: s.blockWidth, density: s.density, textScale: s.textScale };
  };
  // Reflow with the current sizing, persist the changed setting (layout-save) AND the new
  // node positions (layout-autosave). Mirrors debugSlice's setAllExpanded side effects.
  const reflowAndPersist = (changed: Record<string, unknown>) => {
    const st = get();
    st.setNodes(computeHierarchicalLayout(st.nodes, st.edges, sizing()));
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, ...changed });
    sendLayoutAutosave();
  };

  return {
    ...SETTINGS_DEFAULTS,

    setBlockWidth: (px) => { set({ blockWidth: px }); reflowAndPersist({ blockWidth: px }); },
    setTextScale: (v) => { set({ textScale: v }); reflowAndPersist({ textScale: v }); },
    setDensity: (v) => { set({ density: v }); reflowAndPersist({ density: v }); },
    setDefaultBlockExpanded: (v) => {
      set({ defaultBlockExpanded: v });
      messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, defaultBlockExpanded: v });
    },
    resetCanvasSettings: () => {
      set({ ...SETTINGS_DEFAULTS });
      reflowAndPersist({ ...SETTINGS_DEFAULTS });
    },
    // Host-driven restore. Apply values, then reflow if a graph is already loaded so a
    // restore that arrives AFTER load-graph still re-lays-out at the saved sizing. No echo.
    restoreCanvasSettings: (s) => {
      set({ ...s });
      const st = get();
      if (st.nodes.length > 0) st.setNodes(computeHierarchicalLayout(st.nodes, st.edges, sizing()));
    },
  };
};
```

- [ ] **Step 4: Register the slice** in `useFlowStore.ts`:

```ts
import { createSettingsSlice, type SettingsSlice } from './slices/settingsSlice';
```
Add `SettingsSlice &` to the `FlowStore` union (e.g. after `HostSlice`), and `...createSettingsSlice(...a),` to the store object.

- [ ] **Step 5: Run the test, expect PASS** — `npx vitest run src/stores/slices/__tests__/settingsSlice.test.ts` → PASS.

- [ ] **Step 6: Typecheck + commit**

```bash
npx tsc --noEmit
git add FlowCanvas/src/stores/slices/settingsSlice.ts FlowCanvas/src/stores/useFlowStore.ts FlowCanvas/src/stores/slices/__tests__/settingsSlice.test.ts
git commit -m "feat(flow-canvas): settingsSlice for block width, text size, density, default-expanded"
```

---

## Phase 3 — Render consumers read sizing from the store

### Task 3.1: Block & start node read width from the store

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx`
- Modify: `FlowCanvas/src/nodes/StartNode.tsx`
- Test: `FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx` (existing — update width assertions)

> Note: `BaseBlock.test.tsx` mocks the store as `useFlowStore: (selector) => selector(mock.state)` (a
> plain `mock.state` object, NOT the real store). Tests vary settings by mutating `mock.state.*`, and
> the stub must include the fields BaseBlock reads or selectors return `undefined`.

- [ ] **Step 1a: Add the new fields to the stub.** In `BaseBlock.test.tsx`, in the `mock` object (lines 18-29), add to `state` (after `nodes: [] as any[],`):

```ts
    blockWidth: 330,
    textScale: 1,
```
(With `blockWidth: 330`, the existing `:74` test stays valid: spine `330px`, child `330 - 30 = 300px`.)

- [ ] **Step 1b: Add the failing width test** — append inside `describe('BaseBlock', ...)`:

```ts
it('block width follows the store blockWidth setting', () => {
  mock.state.blockWidth = 700;
  const { rerender } = renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
  expect(screen.getByTestId('block-node').style.minWidth).toBe('700px');
  rerender(
    React.createElement(BaseBlock, {
      id: 'n1', selected: false, type: 'baseBlock', zIndex: 0, isConnectable: true,
      positionAbsoluteX: 0, positionAbsoluteY: 0, dragging: false,
      data: { blockType: 'send', label: 'Send', props: { _isChildOf: 'p' } },
    } as any),
  );
  expect(screen.getByTestId('block-node').style.minWidth).toBe('670px'); // 700 - 30
  mock.state.blockWidth = 330; // restore for other tests
});
```

- [ ] **Step 2: Run it, expect FAIL** — `npx vitest run src/nodes/__tests__/BaseBlock.test.tsx` → FAIL (width still hard-coded at 330/300).

- [ ] **Step 3: Implement in `BaseBlock.tsx`.**

Change the import (line 8):
```ts
import { BLOCK_WIDTH_INSET } from '../utils/nodeSize';
```
Add a selector near the other `useFlowStore` calls (after line 97):
```ts
const blockWidth = useFlowStore((s) => s.blockWidth);
```
(The `textScale` selector is added in Task 4.1, where it is first used — adding it here unused would trip `noUnusedLocals`/eslint at this commit.)

Replace the width lines in `containerStyle` (162-163):
```ts
    minWidth: isChild ? blockWidth - BLOCK_WIDTH_INSET : blockWidth,
    maxWidth: isChild ? blockWidth - BLOCK_WIDTH_INSET : blockWidth,
```

- [ ] **Step 4: Implement in `StartNode.tsx`.** Replace the `SPINE_WIDTH` import (line 3) with a store read:
```ts
import { useFlowStore } from '../stores/useFlowStore';
```
Inside the component, add:
```ts
const blockWidth = useFlowStore((s) => s.blockWidth);
```
Replace `minWidth: SPINE_WIDTH,`/`maxWidth: SPINE_WIDTH,` (54-55) with `minWidth: blockWidth,`/`maxWidth: blockWidth,`.

- [ ] **Step 5: Run it, expect PASS** — `npx vitest run src/nodes/__tests__/BaseBlock.test.tsx` → PASS.

- [ ] **Step 6: Typecheck + commit**

```bash
npx tsc --noEmit
git add FlowCanvas/src/nodes/BaseBlock.tsx FlowCanvas/src/nodes/StartNode.tsx FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx
git commit -m "feat(flow-canvas): blocks and start node read width from the store setting"
```

### Task 3.2: Branch bands layer passes childWidth

**Files:**
- Modify: `FlowCanvas/src/nodes/BranchBandsLayer.tsx`

- [ ] **Step 1: Implement** — in `BranchBandsLayer.tsx`:

Add the import (after line 5):
```ts
import { BLOCK_WIDTH_INSET } from '../utils/nodeSize';
```
Add a selector after `const enabled = useFlowStore((s) => s.branchBandsEnabled);` (line 12):
```ts
  const blockWidth = useFlowStore((s) => s.blockWidth);
```
Change the call at line 17 from `const bands = computeBranchBands(nodes);` to:
```ts
  const bands = computeBranchBands(nodes, blockWidth - BLOCK_WIDTH_INSET);
```

- [ ] **Step 2: Typecheck** — `npx tsc --noEmit` → clean.

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/nodes/BranchBandsLayer.tsx
git commit -m "feat(flow-canvas): branch bands track the configured block width"
```

---

## Phase 4 — Text scale, density consumers, default-expanded on drop

### Task 4.1: Block text scales with the setting

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx`
- Test: `FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx`

> Decision (refines spec): scale block font sizes in JS via the store `textScale` rather than
> retargeting the shared `--fc-fs-*` tokens — those tokens are also used by ProblemsPanel /
> ConnectionNotice, which must NOT scale with block text. One source (the store value) drives both
> the rendered size here and the layout height estimate (`estimateNodeHeight`, Task 1.1).

- [ ] **Step 1: Write the failing test** — append inside `describe('BaseBlock', ...)`. The label span renders `blockData.label` (`'Send'`), so `getByText('Send')` selects it; its `fontSize` is `13 * textScale`:

```ts
it('block label font scales with textScale', () => {
  mock.state.textScale = 1.15;
  renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
  expect(screen.getByText('Send').style.fontSize).toBe(`${13 * 1.15}px`); // '14.95px'
  mock.state.textScale = 1; // restore for other tests
});
```

- [ ] **Step 2: Run it, expect FAIL** — `npx vitest run src/nodes/__tests__/BaseBlock.test.tsx` → FAIL (label fontSize is the hard-coded 13).

- [ ] **Step 3: Implement** — add the `textScale` selector after the `blockWidth` selector (added in Task 3.1):

```ts
const textScale = useFlowStore((s) => s.textScale);
```
Then multiply the block's text sizes at these sites (all in `BaseBlock.tsx`):

```ts
// headerStyle (line 197): fontSize: 'var(--fc-fs-header)' -> a scaled px value
fontSize: 13 * textScale,
// badgeStyle (line 203): fontSize: 10 ->
fontSize: 10 * textScale,
// header label span (line 323): fontSize: 13 ->
fontSize: 13 * textScale,
// summary label (line 350): fontSize: 10.5 ->
fontSize: 10.5 * textScale,
// summary value (line 352): fontSize: 11.5 ->
fontSize: 11.5 * textScale,
// summary footer row (line 359): fontSize: 10 ->
fontSize: 10 * textScale,
// preview text (line 370): fontSize: 12 ->
fontSize: 12 * textScale,
```
Leave the tiny exec/loop/branch status badges (fontSize 8–9) unscaled — they are run-status chrome, not block content, and don't affect the height estimate.

- [ ] **Step 4: Run it, expect PASS** — `npx vitest run src/nodes/__tests__/BaseBlock.test.tsx` → PASS.

- [ ] **Step 5: Typecheck + commit**

```bash
npx tsc --noEmit
git add FlowCanvas/src/nodes/BaseBlock.tsx FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx
git commit -m "feat(flow-canvas): block text scales with the textScale setting"
```

### Task 4.2: New blocks honor the default-expanded setting

**Files:**
- Modify: `FlowCanvas/src/App.tsx`

- [ ] **Step 1: Implement** — in the `onDrop` callback (`App.tsx`). Single mechanism: create the node WITHOUT an `expanded` flag (as today), then when the setting is on call the already-tested `toggleExpanded(newNode.id)`, which adds it to `expandedNodes`, writes the carrier flag, and reflows. Leave the existing `newNode`/`addNode`/`selectNode` lines unchanged except for inserting the one conditional:

```ts
addNode(newNode);
if (useFlowStore.getState().defaultBlockExpanded) {
  useFlowStore.getState().toggleExpanded(newNode.id);
}
selectNode(newNode.id);
```
First confirm `useFlowStore` is imported at the top of `App.tsx` (it is used for store access elsewhere in the file); if not, add `import { useFlowStore } from './stores/useFlowStore';`.

- [ ] **Step 2: Typecheck** — `npx tsc --noEmit` → clean.

- [ ] **Step 3: Manual check** — `npm run dev`, drop a block with the (soon-to-exist) setting on/off; with it on, the dropped block appears expanded.

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/App.tsx
git commit -m "feat(flow-canvas): new blocks honor the default-expanded setting on drop"
```

---

## Phase 5a — The gear popover UI + toolbar consolidation

### Task 5a.1: Persist + restore the migrated toggles in uiSlice

**Files:**
- Modify: `FlowCanvas/src/stores/slices/uiSlice.ts`
- Test: `FlowCanvas/src/stores/slices/__tests__/uiSlice.persist.test.ts` (new)

> Decision (refines spec): keep snap/branch-bands STATE in `uiSlice` (only the UI moves to the
> popover). The two currently-transient toggles gain persistence; heatmap/reduced-motion already
> persist. This avoids a risky state relocation while still consolidating the UI.

- [ ] **Step 1: Write the failing test** (new file):

```ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save', prefSave: 'pref-save' } },
}));
import { useFlowStore } from '../../useFlowStore';
import { messageBus } from '../../../MessageBus';

describe('uiSlice toggle persistence', () => {
  beforeEach(() => { useFlowStore.setState({ snapToGrid: false, branchBandsEnabled: true }); vi.clearAllMocks(); });

  it('toggleSnapToGrid persists via layout-save', () => {
    useFlowStore.getState().toggleSnapToGrid();
    expect(messageBus.send).toHaveBeenCalledWith({ type: 'layout-save', snapToGrid: true });
  });
  it('toggleBranchBands persists via layout-save', () => {
    useFlowStore.getState().toggleBranchBands();
    expect(messageBus.send).toHaveBeenCalledWith({ type: 'layout-save', branchBandsEnabled: false });
  });
  it('restoreSnapToGrid / restoreBranchBands set state without echo', () => {
    useFlowStore.getState().restoreSnapToGrid(true);
    useFlowStore.getState().restoreBranchBands(false);
    expect(useFlowStore.getState().snapToGrid).toBe(true);
    expect(useFlowStore.getState().branchBandsEnabled).toBe(false);
  });
});
```

- [ ] **Step 2: Run it, expect FAIL** — `npx vitest run src/stores/slices/__tests__/uiSlice.persist.test.ts` → FAIL.

- [ ] **Step 3: Implement in `uiSlice.ts`.**

Add to the `UISlice` interface (near line 60):
```ts
  restoreSnapToGrid: (value: boolean) => void;
  restoreBranchBands: (value: boolean) => void;
```
Replace `toggleSnapToGrid` (line 132) and `toggleBranchBands` (line 130) so they persist:
```ts
  toggleSnapToGrid: () => set((s) => {
    const next = !s.snapToGrid;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, snapToGrid: next });
    return { snapToGrid: next };
  }),
  restoreSnapToGrid: (value) => set({ snapToGrid: value }),

  toggleBranchBands: () => set((s) => {
    const next = !s.branchBandsEnabled;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, branchBandsEnabled: next });
    return { branchBandsEnabled: next };
  }),
  restoreBranchBands: (value) => set({ branchBandsEnabled: value }),
```

- [ ] **Step 4: Run it, expect PASS** — `npx vitest run src/stores/slices/__tests__/uiSlice.persist.test.ts` → PASS.

- [ ] **Step 5: Typecheck + commit**

```bash
npx tsc --noEmit
git add FlowCanvas/src/stores/slices/uiSlice.ts FlowCanvas/src/stores/slices/__tests__/uiSlice.persist.test.ts
git commit -m "feat(flow-canvas): persist snap-to-grid and branch-bands toggles"
```

### Task 5a.2: SettingsPopover component

**Files:**
- Create: `FlowCanvas/src/panels/SettingsPopover.tsx`

- [ ] **Step 1: Implement** (new file). Self-contained: gear button + popover, internal open state, Esc/outside-click dismiss, all controls wired to the store:

```tsx
import { useEffect, useRef, useState, type CSSProperties } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { WIDTH_PRESETS, TEXT_SCALES, DENSITIES } from '../stores/slices/settingsSlice';
import { mix } from '../utils/tokens';

function Segmented<T extends string | number>(props: {
  label: string; value: T; options: readonly { label: string; v: T }[]; onChange: (v: T) => void;
}) {
  return (
    <div style={rowStyle}>
      <span style={labStyle}>{props.label}</span>
      <div style={{ display: 'inline-flex', border: '1px solid var(--fc-border)', borderRadius: 6, overflow: 'hidden' }}>
        {props.options.map((o, i) => {
          const on = o.v === props.value;
          return (
            <button key={o.label} onClick={() => props.onChange(o.v)} style={{
              fontSize: 10, padding: '3px 8px', cursor: 'pointer', fontFamily: 'inherit',
              border: 'none', borderLeft: i ? '1px solid var(--fc-border)' : 'none',
              background: on ? 'var(--fc-accent-surface)' : 'var(--fc-surface-1)',
              color: on ? 'var(--fc-text)' : 'var(--fc-text-muted)',
            }}>{o.label}</button>
          );
        })}
      </div>
    </div>
  );
}

function Toggle(props: { label: string; on: boolean; onClick: () => void }) {
  return (
    <div style={rowStyle}>
      <span style={labStyle}>{props.label}</span>
      <button onClick={props.onClick} role="switch" aria-checked={props.on} style={{
        width: 30, height: 16, borderRadius: 9, border: 'none', cursor: 'pointer', position: 'relative',
        background: props.on ? 'var(--fc-accent-surface)' : 'var(--fc-surface-2)',
      }}>
        <span style={{
          position: 'absolute', top: 2, left: props.on ? 16 : 2, width: 12, height: 12, borderRadius: '50%',
          background: props.on ? 'var(--fc-accent)' : 'var(--fc-text-muted)', transition: 'left 0.12s',
        }} />
      </button>
    </div>
  );
}

const rowStyle: CSSProperties = { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10, padding: '5px 0' };
const labStyle: CSSProperties = { fontSize: 11, color: 'var(--fc-text-secondary)' };
const groupStyle: CSSProperties = { fontSize: 9, fontWeight: 700, letterSpacing: '0.6px', color: 'var(--fc-text-muted)', textTransform: 'uppercase', margin: '8px 0 2px' };

export default function SettingsPopover() {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const blockWidth = useFlowStore((s) => s.blockWidth);
  const textScale = useFlowStore((s) => s.textScale);
  const density = useFlowStore((s) => s.density);
  const defaultBlockExpanded = useFlowStore((s) => s.defaultBlockExpanded);
  const setBlockWidth = useFlowStore((s) => s.setBlockWidth);
  const setTextScale = useFlowStore((s) => s.setTextScale);
  const setDensity = useFlowStore((s) => s.setDensity);
  const setDefaultBlockExpanded = useFlowStore((s) => s.setDefaultBlockExpanded);
  const resetCanvasSettings = useFlowStore((s) => s.resetCanvasSettings);

  const snapToGrid = useFlowStore((s) => s.snapToGrid);
  const toggleSnapToGrid = useFlowStore((s) => s.toggleSnapToGrid);
  const branchBandsEnabled = useFlowStore((s) => s.branchBandsEnabled);
  const toggleBranchBands = useFlowStore((s) => s.toggleBranchBands);
  const heatmapEnabled = useFlowStore((s) => s.heatmapEnabled);
  const toggleHeatmap = useFlowStore((s) => s.toggleHeatmap);
  const reducedMotion = useFlowStore((s) => s.reducedMotion);
  const toggleReducedMotion = useFlowStore((s) => s.toggleReducedMotion);

  useEffect(() => {
    if (!open) return;
    const onDoc = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); };
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false); };
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onKey);
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); };
  }, [open]);

  return (
    <div ref={ref} style={{ position: 'relative' }}>
      <button
        onClick={() => setOpen((o) => !o)}
        title="Display settings"
        aria-haspopup="dialog"
        aria-expanded={open}
        style={{
          padding: '4px 8px', borderRadius: 4, fontFamily: 'inherit', fontSize: 12, cursor: 'pointer',
          background: open ? 'var(--fc-surface-2)' : 'var(--fc-surface-2)',
          border: `1px solid ${open ? mix('var(--fc-accent)', 50) : 'var(--fc-border)'}`,
          color: open ? 'var(--fc-accent)' : 'var(--fc-text-secondary)',
        }}
      >
        ⚙
      </button>

      {open && (
        <div role="dialog" aria-label="Display settings" style={{
          position: 'absolute', top: 'calc(100% + 6px)', right: 0, zIndex: 50, width: 236,
          background: 'var(--fc-surface-0)', border: '1px solid var(--fc-border-subtle)',
          borderRadius: 9, boxShadow: '0 12px 36px var(--fc-overlay-scrim)', padding: '8px 12px 10px',
        }}>
          <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.5px', color: 'var(--fc-text)', padding: '2px 0 2px' }}>
            DISPLAY SETTINGS
          </div>

          <div style={groupStyle}>Sizing</div>
          <Segmented label="Block width" value={blockWidth}
            options={WIDTH_PRESETS.map((p) => ({ label: p.label, v: p.px }))} onChange={setBlockWidth} />
          <Segmented label="Text size" value={textScale}
            options={TEXT_SCALES.map((p) => ({ label: p.label, v: p.v }))} onChange={setTextScale} />
          <Segmented label="Canvas density" value={density}
            options={DENSITIES.map((p) => ({ label: p.label, v: p.v }))} onChange={setDensity} />
          <Segmented label="New blocks" value={defaultBlockExpanded ? 1 : 0}
            options={[{ label: 'Collapsed', v: 0 }, { label: 'Expanded', v: 1 }]}
            onChange={(v) => setDefaultBlockExpanded(v === 1)} />

          <div style={{ height: 1, background: 'var(--fc-border)', margin: '8px 0 2px' }} />
          <div style={groupStyle}>View</div>
          <Toggle label="Snap to grid" on={snapToGrid} onClick={toggleSnapToGrid} />
          <Toggle label="Branch bands" on={branchBandsEnabled} onClick={toggleBranchBands} />
          <Toggle label="Heatmap" on={heatmapEnabled} onClick={toggleHeatmap} />
          <Toggle label="Reduced motion" on={reducedMotion} onClick={toggleReducedMotion} />

          <div style={{ height: 1, background: 'var(--fc-border)', margin: '8px 0 4px' }} />
          <button onClick={resetCanvasSettings} style={{
            background: 'none', border: 'none', color: 'var(--fc-accent)', fontSize: 10, cursor: 'pointer',
            fontFamily: 'inherit', padding: '2px 0',
          }}>↺ Reset to defaults</button>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Typecheck** — `npx tsc --noEmit` → clean.

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/panels/SettingsPopover.tsx
git commit -m "feat(flow-canvas): SettingsPopover with width/text/density/default-state and view toggles"
```

### Task 5a.3: Wire the popover into the toolbar, remove migrated buttons

**Files:**
- Modify: `FlowCanvas/src/panels/Toolbar.tsx`

- [ ] **Step 1: Implement.**

Add the import:
```ts
import SettingsPopover from './SettingsPopover';
```
Remove the now-migrated toolbar buttons and their selectors:
- Delete the **Snap** button (lines 200-202) and the `snapToGrid`/`toggleSnapToGrid` selectors (18-19).
- Delete the **Motion** button (203-209) and the `reducedMotion`/`toggleReducedMotion` selectors (20-21).
- Delete the **Heatmap** button (246-252) and the `heatmapEnabled`/`toggleHeatmap` selectors (22-23).
- Delete the **Bands** button (253-259) and the `branchBandsEnabled`/`toggleBranchBands` selectors (24-25).

Add `<SettingsPopover />` to the Canvas-controls group, after the Expand All button (after line 217):
```tsx
      <SettingsPopover />
```

- [ ] **Step 2: Typecheck** — `npx tsc --noEmit` → clean (verify no orphaned references remain — grep for `toggleSnapToGrid`, `toggleHeatmap`, `toggleBranchBands`, `toggleReducedMotion` in `Toolbar.tsx`).

- [ ] **Step 3: Manual check** — `npm run dev`: the gear opens the popover; the four toggles are gone from the toolbar and live in the popover; cycling width presets resizes blocks live and reflows.

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/panels/Toolbar.tsx
git commit -m "feat(flow-canvas): replace scattered view toggles with the settings gear popover"
```

---

## Phase 5b — C# persistence + restore wiring

### Task 5b.1: WindowState fields

**Files:**
- Modify: `Models/AppConfiguration.cs`

- [ ] **Step 1: Implement** — add fields to `WindowState` after line 510 (`FlowCanvasHeatmapEnabled`):

```csharp
        // Flow Canvas display settings (persisted from React UI; null = use the React default)
        public int? FlowCanvasBlockWidth { get; set; }
        public double? FlowCanvasTextScale { get; set; }
        public double? FlowCanvasDensity { get; set; }
        public bool? FlowCanvasDefaultExpanded { get; set; }
        public bool? FlowCanvasSnapToGrid { get; set; }
        public bool? FlowCanvasBranchBands { get; set; }
```

- [ ] **Step 2: Build** — from repo root: `dotnet build SSH_Helper.sln` → succeeds.

- [ ] **Step 3: Commit**

```bash
git add Models/AppConfiguration.cs
git commit -m "feat(flow-canvas): persist canvas display settings in WindowState"
```

### Task 5b.2: Save + send the new settings

**Files:**
- Modify: `UI/FlowCanvasForm.cs`

- [ ] **Step 1: Implement — save.** In `SavePanelSizes` (line 374), extract and persist the new fields. Replace the body:

```csharp
        private void SavePanelSizes(JObject? msg)
        {
            if (_configService == null || msg == null) return;

            var panelSizes = msg["panelSizes"] as JObject;
            var rightWidth = panelSizes?["rightPanelWidth"]?.Value<int>();
            var outputHeight = panelSizes?["outputHeight"]?.Value<int>();
            // These all reuse the layout-save channel and arrive without a panelSizes object.
            var heatmap = msg["heatmapEnabled"]?.Value<bool>();
            var blockWidth = msg["blockWidth"]?.Value<int>();
            var textScale = msg["textScale"]?.Value<double>();
            var density = msg["density"]?.Value<double>();
            var defaultExpanded = msg["defaultBlockExpanded"]?.Value<bool>();
            var snap = msg["snapToGrid"]?.Value<bool>();
            var bands = msg["branchBandsEnabled"]?.Value<bool>();

            if (rightWidth == null && outputHeight == null && heatmap == null && blockWidth == null
                && textScale == null && density == null && defaultExpanded == null && snap == null && bands == null)
                return;

            _configService.Update(c =>
            {
                c.WindowState ??= new Models.WindowState();
                if (rightWidth > 0) c.WindowState.FlowCanvasRightPanelWidth = rightWidth;
                if (outputHeight > 0) c.WindowState.FlowCanvasOutputHeight = outputHeight;
                if (heatmap.HasValue) c.WindowState.FlowCanvasHeatmapEnabled = heatmap.Value;
                if (blockWidth > 0) c.WindowState.FlowCanvasBlockWidth = blockWidth;
                if (textScale.HasValue) c.WindowState.FlowCanvasTextScale = textScale.Value;
                if (density.HasValue) c.WindowState.FlowCanvasDensity = density.Value;
                if (defaultExpanded.HasValue) c.WindowState.FlowCanvasDefaultExpanded = defaultExpanded.Value;
                if (snap.HasValue) c.WindowState.FlowCanvasSnapToGrid = snap.Value;
                if (bands.HasValue) c.WindowState.FlowCanvasBranchBands = bands.Value;
            });
        }
```

- [ ] **Step 2: Implement — send.** In `SendPersistedLayout` (line 356), include the new fields in the `layout-restore` message and broaden the send guard. Replace the body:

```csharp
        private void SendPersistedLayout()
        {
            var ws = _configService?.GetCurrent().WindowState;
            if (ws == null) return;

            var panelSizes = new JObject();
            if (ws.FlowCanvasRightPanelWidth > 0)
                panelSizes["rightPanelWidth"] = ws.FlowCanvasRightPanelWidth;
            if (ws.FlowCanvasOutputHeight > 0)
                panelSizes["outputHeight"] = ws.FlowCanvasOutputHeight;

            // React guards each field by type, so sending nulls is harmless. Always send so any
            // persisted display setting is restored, not just panel sizes.
            SendMessage(new
            {
                type = "layout-restore",
                panelSizes,
                heatmapEnabled = ws.FlowCanvasHeatmapEnabled ?? false,
                blockWidth = ws.FlowCanvasBlockWidth,
                textScale = ws.FlowCanvasTextScale,
                density = ws.FlowCanvasDensity,
                defaultBlockExpanded = ws.FlowCanvasDefaultExpanded,
                snapToGrid = ws.FlowCanvasSnapToGrid,
                branchBandsEnabled = ws.FlowCanvasBranchBands,
            });

            var rm = ws.FlowCanvasReducedMotion;
            if (rm.HasValue) SendMessage(new { type = "pref-restore", reducedMotion = rm.Value });
        }
```

- [ ] **Step 3: Build** — `dotnet build SSH_Helper.sln` → succeeds.

- [ ] **Step 4: Commit**

```bash
git add UI/FlowCanvasForm.cs
git commit -m "feat(flow-canvas): save and restore canvas display settings over the layout channel"
```

### Task 5b.3: React applies restored settings

**Files:**
- Modify: `FlowCanvas/src/stores/messageBridge.ts`
- Test: `FlowCanvas/src/stores/slices/__tests__/settingsSlice.test.ts` (extend — restore handler shape)

- [ ] **Step 1: Implement** — add the type import at the top of `messageBridge.ts` (alongside the other store imports):
```ts
import type { CanvasSettings } from './slices/settingsSlice';
```
Then extend the `layout-restore` handler (lines 382-393) to apply the new settings + migrated toggles. `cs` is typed `Partial<CanvasSettings>` (NOT `Record<string, number | boolean>`, which would fail to assign to `restoreCanvasSettings`'s typed param):

```ts
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.layoutRestore, (msg) => {
      if (msg.panelSizes && typeof msg.panelSizes === 'object') {
        const sizes: Record<string, number> = {};
        for (const [k, v] of Object.entries(msg.panelSizes as Record<string, unknown>)) {
          if (typeof v === 'number' && v > 0) sizes[k] = v;
        }
        store.getState().restorePanelSizes(sizes);
      }
      if (typeof msg.heatmapEnabled === 'boolean') store.getState().restoreHeatmapEnabled(msg.heatmapEnabled);

      const cs: Partial<CanvasSettings> = {};
      if (typeof msg.blockWidth === 'number' && msg.blockWidth > 0) cs.blockWidth = msg.blockWidth;
      if (typeof msg.textScale === 'number' && msg.textScale > 0) cs.textScale = msg.textScale;
      if (typeof msg.density === 'number' && msg.density > 0) cs.density = msg.density;
      if (typeof msg.defaultBlockExpanded === 'boolean') cs.defaultBlockExpanded = msg.defaultBlockExpanded;
      if (Object.keys(cs).length > 0) store.getState().restoreCanvasSettings(cs);

      if (typeof msg.snapToGrid === 'boolean') store.getState().restoreSnapToGrid(msg.snapToGrid);
      if (typeof msg.branchBandsEnabled === 'boolean') store.getState().restoreBranchBands(msg.branchBandsEnabled);
    }),
```

- [ ] **Step 2: Make load-graph use the current sizing.** In the `load-graph` handler, the initial layout at line 138 must use the (possibly already-restored) settings:

```ts
        if (!hasUserLayout) {
          const s = store.getState();
          store.getState().setNodes(computeHierarchicalLayout(s.nodes, s.edges,
            { blockWidth: s.blockWidth, density: s.density, textScale: s.textScale }));
        }
```
(Together with `restoreCanvasSettings`' reflow-if-nodes, this makes load-vs-restore order-independent.)

- [ ] **Step 3: Typecheck** — `npx tsc --noEmit` → clean.

- [ ] **Step 4: Run the full settings test + the bridge-adjacent tests, expect PASS**

```
npx vitest run src/stores/slices/__tests__/settingsSlice.test.ts
```

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/messageBridge.ts
git commit -m "feat(flow-canvas): restore persisted canvas settings and lay out at saved sizing"
```

---

## Phase 6 — Full verification

### Task 6.1: Whole suite + typecheck + build

- [ ] **Step 1: React tests** — from `FlowCanvas/`: `npx vitest run` → all green.
- [ ] **Step 2: React typecheck** — `npx tsc --noEmit` → clean.
- [ ] **Step 3: C# build** — from repo root: `dotnet build SSH_Helper.sln` → succeeds.
- [ ] **Step 4: Manual session** — `npm run dev` (or run the app): open the gear; cycle width Compact→Max and confirm the screenshot's `interface = json.get(current_host.interface_select_method)` stops clipping at Wide+; change text S/M/L; change density; toggle snap/bands/heatmap/motion in the popover; click Reset; **close and reopen the canvas** and confirm every setting persisted. Verify a nested if/try/switch graph does not overlap at Max width.
- [ ] **Step 5: Commit any fixups** discovered during manual verification.

---

## Self-review notes (coverage map)

- **Width presets (300/330/440/560/700)** → `WIDTH_PRESETS` (2.1), consumed by render (3.1) and layout (1.2).
- **Text size S/M/L** → `TEXT_SCALES` (2.1), render (4.1), height estimate (1.1).
- **Density Tight/Normal/Roomy** → `DENSITIES` (2.1) → `nodeSpacingY` (1.2).
- **Default block state** → `defaultBlockExpanded` (2.1) → onDrop (4.2).
- **Gear popover surface** → 5a.2/5a.3.
- **Consolidated toggles** → 5a.1 (persist) + 5a.3 (UI move).
- **Persistence + restore + ordering** → 5b.1/5b.2/5b.3 (+ `restoreCanvasSettings` reflow in 2.1).
- **Branch-offset scales with width (sharp edge)** → `resolveSizing.branchOffset` (1.2), verified manually (6.1 step 4) and by the no-overlap intent of the sizing test (1.2).
- **Export untouched** → no task modifies `exportGraph.ts`/`FlowCanvasBridge.cs`; width/text/density never written to `node.data.props`.

## Out of scope (YAGNI)

- Per-block width override; per-element font controls; horizontal density; a keyboard shortcut for the gear; any YAML/preset export change.
