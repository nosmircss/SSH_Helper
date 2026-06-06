# Flow Canvas — Bigger Blocks, Labeled Lanes & Expandable Settings — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Flow Canvas blocks bigger (330px spine / 300px children), redesign branch bands as labeled lanes, and let any block expand in place to a read-only settings summary — without touching edges, YAML, or graph data.

**Architecture:** Pure presentation + layout work in the React app (`FlowCanvas/src`) with mirrored layout constants and a persistence field in C# (`Services/FlowCanvasBridge.cs`, `Models/CanvasLayoutData.cs`). New per-node `expanded` state mirrors the existing `disabledBlocks` pattern exactly (Set in `debugSlice`, round-tripped via `node.data.expanded` + the layout-autosave path). The auto-layout becomes height-aware using **estimated** node heights (xyflow exposes no measured heights here), and a toggle re-runs the layout to reflow.

**Tech Stack:** React 18 + TypeScript + Zustand + @xyflow/react (Vite), vitest + @testing-library/react (unit/component), Playwright (e2e); C# .NET 8 (`Newtonsoft.Json.Linq`), xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-06-02-flow-canvas-blocks-bands-expansion-design.md`

---

## Deviations from the spec (intentional, discovered during recon)

- **State lives in `debugSlice.ts`** (alongside `breakpoints`/`disabledBlocks`), not `uiSlice.ts`. Reuses the proven Set + toggle + restore + autosave pattern. No behavior change.
- **`node.data.expanded`** (top-level, like `node.data.disabled`) is the round-trip carrier. YAML export reads only `node.data.props` and skips it (`IsMetadataProperty` / props-only loop), so the "visual never touches YAML" invariant holds.
- **Estimated heights** drive height-aware layout (no measured heights available); **`toggleExpanded` triggers a reflow** via `computeHierarchicalLayout`. ⚠️ See "Open decision" at the end — reflow-on-toggle interacts with manually-arranged (`hasUserLayout`) canvases; confirm the v1 behavior before executing Phase 5.

## Out of scope (do not touch)

`AnimatedEdge.tsx`, edge routing/colors/arrowheads/markers, YAML import/export semantics, inline field editing, `ChoiceOptionsEditor`, the Test-block panel, the floating-panel mechanism.

## File structure (created / modified)

**React**
- `FlowCanvas/src/nodes/BaseBlock.tsx` — widths 330/300, density, chevron + expansion, read-only summary, height carrier.
- `FlowCanvas/src/nodes/StartNode.tsx` — width 330.
- `FlowCanvas/src/utils/nodeSize.ts` *(new)* — single source for block width/height estimates (shared by BaseBlock display, layout, bands).
- `FlowCanvas/src/utils/blockSummary.ts` *(new)* — compute the read-only summary rows (required + non-default).
- `FlowCanvas/src/utils/branchBands.ts` — real-dimension geometry, PAD 18, pill-label helper.
- `FlowCanvas/src/nodes/BranchBandsLayer.tsx` — labeled lane (pill + soft border + left accent + nested tint).
- `FlowCanvas/src/utils/layout/hierarchicalLayout.ts` — `CHILD_NODE_MAX_WIDTH` 300 + column width; height-aware vertical advance.
- `FlowCanvas/src/stores/slices/debugSlice.ts` — `expandedNodes` Set + `toggleExpanded` + `isExpanded` + `restoreExpandedNodes`.
- `FlowCanvas/src/utils/layoutAutosave.ts` — serialize `expandedNodes`.
- `FlowCanvas/src/stores/messageBridge.ts` — restore `expandedNodes` on load-graph; reflow uses height-aware layout.
- `FlowCanvas/src/styles/tokens.css` — lane padding/pill tokens (optional; otherwise inline `mix()`).

**C#**
- `Services/FlowCanvasBridge.cs` — layout constants in lockstep; `ExpandedNodeIds` extract/merge.
- `Models/CanvasLayoutData.cs` — `ExpandedNodeIds` field + `Clone()`.

**Tests**
- `FlowCanvas/src/utils/__tests__/nodeSize.test.ts`, `blockSummary.test.ts`, `branchBands.test.ts` *(new)*
- `FlowCanvas/src/stores/slices/__tests__/debugSlice.expanded.test.ts` *(new)*
- `FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts` *(modify)*
- `FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx` *(modify)*
- `FlowCanvas/e2e/flow-canvas-auto-layout.spec.ts` *(modify)*, `flow-canvas-expansion.spec.ts` *(new)*
- `SSH_Helper.Tests/Services/FlowCanvasBridgeLayoutPersistenceTests.cs` *(new)*

**Commands** (run from repo root unless noted):
- Unit/component: `cd FlowCanvas && npx vitest run <path>`
- Full vitest: `cd FlowCanvas && npm test`
- React build (types/lint): `cd FlowCanvas && npm run build`
- e2e: `cd FlowCanvas && npx playwright test <spec>`
- C#: `dotnet build SSH_Helper.sln` ; `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`

---

## Phase 1 — Block sizing (330 / 300) + density

**Outcome:** blocks render at the new sizes; auto-layout columns/spacing match in TS and C#; YAML round-trip unchanged.

### Task 1.1: Centralize block dimensions in `nodeSize.ts`

**Files:**
- Create: `FlowCanvas/src/utils/nodeSize.ts`
- Test: `FlowCanvas/src/utils/__tests__/nodeSize.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// FlowCanvas/src/utils/__tests__/nodeSize.test.ts
import { describe, it, expect } from 'vitest';
import { SPINE_WIDTH, CHILD_WIDTH, COLLAPSED_HEIGHT, nodeWidth } from '../nodeSize';

describe('nodeSize', () => {
  it('exposes the new fixed widths', () => {
    expect(SPINE_WIDTH).toBe(330);
    expect(CHILD_WIDTH).toBe(300);
    expect(COLLAPSED_HEIGHT).toBe(52);
  });
  it('nodeWidth picks child vs spine by _isChildOf', () => {
    expect(nodeWidth({ _isChildOf: 'p' })).toBe(300);
    expect(nodeWidth({})).toBe(330);
    expect(nodeWidth(undefined)).toBe(330);
  });
});
```

- [ ] **Step 2: Run it, verify it fails**

Run: `cd FlowCanvas && npx vitest run src/utils/__tests__/nodeSize.test.ts`
Expected: FAIL — cannot find module `../nodeSize`.

- [ ] **Step 3: Create the module**

```ts
// FlowCanvas/src/utils/nodeSize.ts
// Single source of truth for Flow Canvas block dimensions. Width is fixed per role;
// height is an ESTIMATE (xyflow exposes no measured height at layout time) used by the
// hierarchical layout and the branch-band geometry so taller/expanded blocks don't overlap.

export const SPINE_WIDTH = 330;
export const CHILD_WIDTH = 300;

/** Collapsed block height estimate: header (~30) + single preview line (~22). */
export const COLLAPSED_HEIGHT = 52;

/** Expanded summary metrics (mirror BaseBlock's summary layout). */
export const SUMMARY_PAD = 14;     // top+bottom padding of the summary body
export const SUMMARY_ROW_H = 20;   // one label:value row
export const SUMMARY_FOOTER_H = 24; // "N at default" + Edit-in-Properties footer

export function nodeWidth(props: Record<string, unknown> | undefined): number {
  return props && props['_isChildOf'] ? CHILD_WIDTH : SPINE_WIDTH;
}
```

- [ ] **Step 4: Run it, verify it passes**

Run: `cd FlowCanvas && npx vitest run src/utils/__tests__/nodeSize.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/nodeSize.ts FlowCanvas/src/utils/__tests__/nodeSize.test.ts
git commit -m "feat(flow-canvas): add nodeSize module (330/300 widths, height estimates)"
```

### Task 1.2: Apply widths + density in `BaseBlock.tsx`

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx:149-160` (containerStyle), `:170-202` (icon/header/badge), `:312-338` (label/preview)
- Test: `FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx`

- [ ] **Step 1: Add a failing width assertion to the existing test**

Append to `FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx` inside `describe('BaseBlock', ...)`:

```tsx
it('renders a spine block at 330px and a child block at 300px', () => {
  const { rerender } = renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
  expect(screen.getByTestId('block-node').style.minWidth).toBe('330px');
  rerender(
    React.createElement(BaseBlock, {
      id: 'n1', selected: false, type: 'baseBlock', zIndex: 0, isConnectable: true,
      positionAbsoluteX: 0, positionAbsoluteY: 0, dragging: false,
      data: { blockType: 'send', label: 'Send', props: { _isChildOf: 'p' } },
    } as any),
  );
  expect(screen.getByTestId('block-node').style.minWidth).toBe('300px');
});
```

- [ ] **Step 2: Run it, verify it fails**

Run: `cd FlowCanvas && npx vitest run src/nodes/__tests__/BaseBlock.test.tsx`
Expected: FAIL — minWidth is `280px` / child `160px`.

- [ ] **Step 3: Update `containerStyle` to use `nodeSize` + density**

In `BaseBlock.tsx`, add the import near the other utils imports:

```tsx
import { SPINE_WIDTH, CHILD_WIDTH } from '../utils/nodeSize';
```

Replace the width lines in `containerStyle` (currently `minWidth: isChild ? 160 : 280, maxWidth: isChild ? 260 : 280,`) with:

```tsx
    minWidth: isChild ? CHILD_WIDTH : SPINE_WIDTH,
    maxWidth: isChild ? CHILD_WIDTH : SPINE_WIDTH,
```

Density bumps (apply each): in `iconChipStyle` set `width: 20, height: 20`; in `headerStyle` set `padding: '6px 9px'`; the label span (`fontSize: 12`) → `fontSize: 13`; the preview div (`fontSize: 11`) → `fontSize: 12`. Leave the icon SVG default size (14) and badge (10) as-is.

- [ ] **Step 4: Run it, verify it passes**

Run: `cd FlowCanvas && npx vitest run src/nodes/__tests__/BaseBlock.test.tsx`
Expected: PASS (both new and existing tests).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/nodes/BaseBlock.tsx FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx
git commit -m "feat(flow-canvas): widen blocks to 330/300 and bump density"
```

### Task 1.3: StartNode width 330

**Files:** Modify `FlowCanvas/src/nodes/StartNode.tsx:52-53`

- [ ] **Step 1: Change the width** — set both `minWidth` and `maxWidth` (currently `280`) to `330` (import `SPINE_WIDTH` from `../utils/nodeSize` and use it).
- [ ] **Step 2: Build to verify types** — Run: `cd FlowCanvas && npm run build` — Expected: success.
- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/nodes/StartNode.tsx
git commit -m "feat(flow-canvas): widen StartNode to 330"
```

### Task 1.4: Layout column width (TS) → 300/330

**Files:**
- Modify: `FlowCanvas/src/utils/layout/hierarchicalLayout.ts:11` (`CHILD_NODE_MAX_WIDTH`)
- Test: `FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts`

- [ ] **Step 1: Write the failing test** (append):

```ts
import { placeTree, LAYOUT } from '../hierarchicalLayout';
describe('column width tracks child width', () => {
  it('multi-branch columns are at least CHILD_NODE_MAX_WIDTH + COLUMN_GAP apart', () => {
    expect(LAYOUT.CHILD_NODE_MAX_WIDTH).toBe(300);
    expect(LAYOUT.MIN_COLUMN_WIDTH).toBe(330);
  });
});
```

- [ ] **Step 2: Run it, verify it fails** — Run: `cd FlowCanvas && npx vitest run src/utils/layout/__tests__/hierarchicalLayout.test.ts` — Expected: FAIL (260 / 290).
- [ ] **Step 3: Change the constant** — set `CHILD_NODE_MAX_WIDTH: 300` in the `LAYOUT` object (`BASE_COLUMN_WIDTH`/`MIN_COLUMN_WIDTH` getters derive 330 automatically). Leave `COLUMN_GAP: 30`.
- [ ] **Step 4: Run it, verify it passes** — Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/layout/hierarchicalLayout.ts FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts
git commit -m "feat(flow-canvas): widen layout columns to match 300px children"
```

### Task 1.5: Mirror constants in C#

**Files:**
- Modify: `Services/FlowCanvasBridge.cs:73-75` (`ChildNodeMaxWidth`, `ColumnGap`, `MinColumnWidth`)
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeLayoutPersistenceTests.cs` (create; first test asserts the constant via reflection)

- [ ] **Step 1: Write the failing test**

```csharp
// SSH_Helper.Tests/Services/FlowCanvasBridgeLayoutPersistenceTests.cs
using System.Reflection;
using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class FlowCanvasBridgeLayoutPersistenceTests
{
    private static double Const(string name) =>
        (double)typeof(FlowCanvasBridge).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Fact]
    public void ChildNodeMaxWidth_matches_typescript()
    {
        Const("ChildNodeMaxWidth").Should().Be(300);
        Const("MinColumnWidth").Should().Be(330);
    }
}
```

- [ ] **Step 2: Run it, verify it fails** — Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FlowCanvasBridgeLayoutPersistenceTests` — Expected: FAIL (260 / 290).
- [ ] **Step 3: Change the constant** — in `FlowCanvasBridge.cs` set `private const double ChildNodeMaxWidth = 300;` (`MinColumnWidth = ChildNodeMaxWidth + ColumnGap` derives 330). Leave `ColumnGap = 30`, `NodeSpacingY = 106`.
- [ ] **Step 4: Run it, verify it passes** — Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add Services/FlowCanvasBridge.cs SSH_Helper.Tests/Services/FlowCanvasBridgeLayoutPersistenceTests.cs
git commit -m "feat(flow-canvas): mirror 300/330 column width in C# bridge"
```

### Task 1.6: Update the auto-layout e2e threshold + YAML round-trip check

**Files:** Modify `FlowCanvas/e2e/flow-canvas-auto-layout.spec.ts:42-48`

- [ ] **Step 1: Update the column-gap assertion** — change `expect(Math.abs(then.x - els.x)).toBeGreaterThanOrEqual(260);` to `...toBeGreaterThanOrEqual(300);` (the new `CHILD_NODE_MAX_WIDTH`).
- [ ] **Step 2: Run the e2e** — Run: `cd FlowCanvas && npx playwright test flow-canvas-auto-layout.spec.ts` — Expected: PASS.
- [ ] **Step 3: Manual YAML round-trip check** — Run the app, import a sample preset, export, confirm byte-identical YAML (sizing is presentation-only). Record the result.
- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/e2e/flow-canvas-auto-layout.spec.ts
git commit -m "test(flow-canvas): update auto-layout column-gap threshold for 300px children"
```

**Phase 1 verification:** `cd FlowCanvas && npm run build` (clean) ; `dotnet build SSH_Helper.sln` (clean) ; both test suites pass ; blocks visibly 330/300 in the running app.

---

## Phase 2 — Labeled lanes (branch bands)

**Outcome:** bands render as labeled lanes (pill + soft border + left accent + nested tint), 18px padding, geometry from real per-node width (height comes in Phase 5; use collapsed estimate now).

### Task 2.1: Pill-label helper + verbatim geometry refactor in `branchBands.ts`

**Files:**
- Modify: `FlowCanvas/src/utils/branchBands.ts`
- Test: `FlowCanvas/src/utils/__tests__/branchBands.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// FlowCanvas/src/utils/__tests__/branchBands.test.ts
import { describe, it, expect } from 'vitest';
import type { Node } from '@xyflow/react';
import { computeBranchBands, branchPillLabel, BAND_PAD } from '../branchBands';

function child(id: string, parent: string, stepPath: string, x: number, y: number): Node {
  return { id, position: { x, y }, data: { props: { _isChildOf: parent, _stepPath: stepPath } } } as Node;
}

describe('branchBands', () => {
  it('uses 18px padding', () => { expect(BAND_PAD).toBe(18); });

  it('maps branch keys to human pill labels', () => {
    expect(branchPillLabel('then')).toBe('THEN');
    expect(branchPillLabel('else')).toBe('ELSE');
    expect(branchPillLabel('do')).toBe('LOOP');
    expect(branchPillLabel('case')).toBe('CASE');
    expect(branchPillLabel('elif')).toBe('ELIF');
  });

  it('wraps a 300px child with 18px padding on each side', () => {
    const bands = computeBranchBands([child('c1', 'p', 'steps/1/then/0', 100, 200)]);
    expect(bands).toHaveLength(1);
    const b = bands[0];
    expect(b.x).toBe(100 - 18);
    expect(b.width).toBe(300 + 18 * 2); // child width 300 + pad both sides
    expect(b.branchKey).toBe('then');
  });
});
```

- [ ] **Step 2: Run it, verify it fails** — Run: `cd FlowCanvas && npx vitest run src/utils/__tests__/branchBands.test.ts` — Expected: FAIL (`branchPillLabel`/`BAND_PAD` not exported; width uses 280 + 10).

- [ ] **Step 3: Edit `branchBands.ts`**

Add the import and exported pad at the top (replace the `NODE_W`/`NODE_H`/`PAD` block at lines 50-52):

```ts
import { CHILD_WIDTH, COLLAPSED_HEIGHT } from './nodeSize';

export const BAND_PAD = 18;

/** Human pill label for a branch key (single source for the band layer). */
export function branchPillLabel(key: string): string {
  const k = (key ?? '').toLowerCase();
  if (k === 'do') return 'LOOP';
  return k.toUpperCase();
}

/** Per-node box used for band geometry. Width is fixed per role; height is the COLLAPSED
 *  estimate here — Phase 5 swaps in the expanded estimate so lanes wrap expanded children. */
function nodeBox(n: Node): { w: number; h: number } {
  return { w: CHILD_WIDTH, h: COLLAPSED_HEIGHT };
}
```

In `computeBranchBands`, replace the bounding-box accumulation (currently uses `NODE_W`/`NODE_H`) with:

```ts
    for (const n of g.nodes) {
      const { w, h } = nodeBox(n);
      minX = Math.min(minX, n.position.x);
      minY = Math.min(minY, n.position.y);
      maxX = Math.max(maxX, n.position.x + w);
      maxY = Math.max(maxY, n.position.y + h);
    }
```

And the band push (currently uses `PAD`) with `BAND_PAD`:

```ts
    bands.push({
      id: groupId, parentId: g.parentId, branchKey: g.branchKey,
      x: minX - BAND_PAD, y: minY - BAND_PAD,
      width: (maxX - minX) + BAND_PAD * 2,
      height: (maxY - minY) + BAND_PAD * 2,
      colorVar: branchColorVar(g.branchKey),
    });
```

- [ ] **Step 4: Run it, verify it passes** — Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/branchBands.ts FlowCanvas/src/utils/__tests__/branchBands.test.ts
git commit -m "feat(flow-canvas): branch-band geometry from real dims + 18px pad + pill labels"
```

### Task 2.2: Render labeled lanes in `BranchBandsLayer.tsx`

**Files:** Modify `FlowCanvas/src/nodes/BranchBandsLayer.tsx`

- [ ] **Step 1: Add a nesting-depth signal to bands** — in `branchBands.ts`, add `depth` to `BranchBand` (count `/` segments of the parent's deepest child stepPath, or `_stepPath` segment count for the group's nodes). Minimal approach: `depth = (firstChildStepPath.split('/').length - 2)` clamped ≥ 0. Add `depth: number` to the `BranchBand` interface and set it in the push. (If this complicates Task 2.1's test, add `expect(b).toHaveProperty('depth')`.)

- [ ] **Step 2: Replace the band div** — render pill + soft border + left accent + tint; nested (`depth >= 1`) gets a brighter tint:

```tsx
import { ViewportPortal } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';
import { computeBranchBands, branchPillLabel } from '../utils/branchBands';
import { mix } from '../utils/tokens';

export default function BranchBandsLayer() {
  const nodes = useFlowStore((s) => s.nodes);
  const enabled = useFlowStore((s) => s.branchBandsEnabled);
  if (!enabled) return null;
  const bands = computeBranchBands(nodes);
  if (bands.length === 0) return null;

  return (
    <ViewportPortal>
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
              border: `1px solid ${mix(b.colorVar, nested ? 55 : 38)}`,
              borderLeft: `3px solid ${mix(b.colorVar, 70)}`,
              borderRadius: 9,
              pointerEvents: 'none',
              zIndex: -1,
            }}
          >
            <span style={{
              position: 'absolute', top: 0, left: 0,
              font: '800 9px/1.4 system-ui, sans-serif', letterSpacing: '0.08em',
              padding: '2px 10px', borderRadius: '9px 0 8px 0',
              color: 'oklch(17% 0.02 275)',
              background: nested ? `color-mix(in oklch, ${b.colorVar}, white 14%)` : b.colorVar,
            }}>
              {branchPillLabel(b.branchKey)}
            </span>
          </div>
        );
      })}
    </ViewportPortal>
  );
}
```

- [ ] **Step 3: Build to verify types** — Run: `cd FlowCanvas && npm run build` — Expected: success.
- [ ] **Step 4: e2e — pills render** — add to `flow-canvas-auto-layout.spec.ts` (or a new `flow-canvas-bands.spec.ts`): load `messyIfElse`, assert `page.locator('[data-testid="branch-band"][data-branch="then"]')` is present and its text contains `THEN`. Run the spec; Expected: PASS. (jsdom can't compute `color-mix`; assert structure/label here and verify color in the real app.)
- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/nodes/BranchBandsLayer.tsx FlowCanvas/src/utils/branchBands.ts FlowCanvas/e2e/
git commit -m "feat(flow-canvas): labeled-lane branch bands (pill + border + accent + nested tint)"
```

**Phase 2 verification:** build clean; bands show THEN/ELSE/LOOP/CASE pills, soft border, left accent, brighter nested tint, 18px padding, hugging content in the running app.

---

## Phase 3 — `expandedNodes` state + persistence

**Outcome:** an `expanded` flag per node, mirroring `disabledBlocks` end-to-end (store Set, `node.data.expanded` carrier, layout-autosave, C# `CanvasLayoutData`, restore on reload), never leaking to YAML.

### Task 3.1: `expandedNodes` in `debugSlice.ts`

**Files:**
- Modify: `FlowCanvas/src/stores/slices/debugSlice.ts`
- Test: `FlowCanvas/src/stores/slices/__tests__/debugSlice.expanded.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// FlowCanvas/src/stores/slices/__tests__/debugSlice.expanded.test.ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { useFlowStore } from '../../useFlowStore';
import { sendLayoutAutosave } from '../../../utils/layoutAutosave';

describe('expandedNodes', () => {
  beforeEach(() => { useFlowStore.setState({ expandedNodes: new Set() }); vi.clearAllMocks(); });

  it('toggleExpanded adds/removes and reports via isExpanded', () => {
    const s = useFlowStore.getState();
    s.toggleExpanded('n1');
    expect(useFlowStore.getState().isExpanded('n1')).toBe(true);
    s.toggleExpanded('n1');
    expect(useFlowStore.getState().isExpanded('n1')).toBe(false);
  });

  it('toggleExpanded persists via sendLayoutAutosave', () => {
    useFlowStore.getState().toggleExpanded('n1');
    expect(sendLayoutAutosave).toHaveBeenCalled();
  });

  it('restoreExpandedNodes replaces the set', () => {
    useFlowStore.getState().restoreExpandedNodes(['a', 'b']);
    expect(useFlowStore.getState().isExpanded('a')).toBe(true);
    expect(useFlowStore.getState().expandedNodes.size).toBe(2);
  });
});
```

- [ ] **Step 2: Run it, verify it fails** — Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/debugSlice.expanded.test.ts` — Expected: FAIL (`toggleExpanded` undefined).

- [ ] **Step 3: Edit `debugSlice.ts`** (mirror `disabledBlocks` exactly)

Interface — after `disabledBlocks: Set<string>;` add `expandedNodes: Set<string>;`. After `hasBreakpoint: (nodeId: string) => boolean;` add:

```ts
  toggleExpanded: (nodeId: string) => void;
  restoreExpandedNodes: (nodeIds: string[]) => void;
  isExpanded: (nodeId: string) => boolean;
```

Initializer — after `disabledBlocks: new Set<string>(),` add `expandedNodes: new Set<string>(),`.

Actions — after the `toggleDisabled` action (the `},` that closes it) add:

```ts
  toggleExpanded: (nodeId) => {
    let nowExpanded = false;
    set((s) => {
      const next = new Set(s.expandedNodes);
      nowExpanded = !next.has(nodeId);
      if (nowExpanded) next.add(nodeId); else next.delete(nodeId);
      return { expandedNodes: next };
    });
    // Carrier flag for layout/persistence (NOT node.data.props — never leaks to YAML).
    get().updateNodeData(nodeId, { expanded: nowExpanded });
    sendLayoutAutosave();
  },
  restoreExpandedNodes: (nodeIds) => {
    set({ expandedNodes: new Set(nodeIds) });
    for (const id of nodeIds) get().updateNodeData(id, { expanded: true });
  },
```

Selectors — after `hasBreakpoint: (nodeId) => get().breakpoints.has(nodeId),` add:

```ts
  isExpanded: (nodeId) => get().expandedNodes.has(nodeId),
```

(`sendLayoutAutosave` is already imported in this file — it's used by `toggleDisabled`.)

- [ ] **Step 4: Run it, verify it passes** — Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/slices/debugSlice.ts FlowCanvas/src/stores/slices/__tests__/debugSlice.expanded.test.ts
git commit -m "feat(flow-canvas): expandedNodes state in debugSlice (mirrors disabledBlocks)"
```

### Task 3.2: Serialize `expandedNodes` in layout-autosave + restore on load

**Files:**
- Modify: `FlowCanvas/src/utils/layoutAutosave.ts:44-52`, `FlowCanvas/src/stores/messageBridge.ts:143-154`

- [ ] **Step 1: Add to the payload** — in `layoutAutosave.ts`, after `const disabledBlocks = Array.from(state.disabledBlocks);` add `const expandedNodes = Array.from(state.expandedNodes);` and add `expandedNodes,` to the `messageBus.send({ ... })` object.
- [ ] **Step 2: Restore on load** — in `messageBridge.ts`, after the `if (disabledIds.length > 0) { state.restoreDisabledBlocks(disabledIds); }` block, add the mirror:

```ts
        const expandedIds: string[] = [];
        for (const node of state.nodes) {
          const data = node.data as Record<string, unknown> | undefined;
          if (data?.expanded === true) expandedIds.push(node.id);
        }
        if (expandedIds.length > 0) state.restoreExpandedNodes(expandedIds);
```

- [ ] **Step 3: Build to verify types** — Run: `cd FlowCanvas && npm run build` — Expected: success.
- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/utils/layoutAutosave.ts FlowCanvas/src/stores/messageBridge.ts
git commit -m "feat(flow-canvas): persist expandedNodes via layout-autosave round-trip"
```

### Task 3.3: `ExpandedNodeIds` in C# `CanvasLayoutData` + extract/merge

**Files:**
- Modify: `Models/CanvasLayoutData.cs`, `Services/FlowCanvasBridge.cs` (ExtractLayout ~4974, MergeLayout ~4893)
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeLayoutPersistenceTests.cs`

- [ ] **Step 1: Write the failing test** (append to the test class from 1.5)

```csharp
[Fact]
public void CanvasLayoutData_clones_expanded_ids()
{
    var data = new SSH_Helper.Models.CanvasLayoutData();
    data.ExpandedNodeIds.Add("node-3");
    var clone = data.Clone();
    clone.ExpandedNodeIds.Should().ContainSingle().Which.Should().Be("node-3");
    clone.ExpandedNodeIds.Add("node-4"); // independence
    data.ExpandedNodeIds.Should().HaveCount(1);
}
```

- [ ] **Step 2: Run it, verify it fails** — Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FlowCanvasBridgeLayoutPersistenceTests` — Expected: FAIL (`ExpandedNodeIds` missing).
- [ ] **Step 3: Edit `CanvasLayoutData.cs`** — after `public List<string> DisabledBlockIds { get; set; } = new();` add:

```csharp
        /// <summary>
        /// Node IDs of blocks expanded to their read-only settings summary (presentation only).
        /// </summary>
        public List<string> ExpandedNodeIds { get; set; } = new();
```

In `Clone()`, after `DisabledBlockIds = new List<string>(DisabledBlockIds),` add `ExpandedNodeIds = new List<string>(ExpandedNodeIds),`.

- [ ] **Step 4: Run it, verify it passes** — Expected: PASS.
- [ ] **Step 5: Thread extract + merge** — In `FlowCanvasBridge.cs` `ExtractLayout`, mirror the `DisabledBlockIds` block with an `expandedNodeIds` parameter/source (`if (expandedNodeIds != null) layout.ExpandedNodeIds.AddRange(expandedNodeIds);`). In `MergeLayout`, after the disabled-mark block add:

```csharp
                if (id != null && layout.ExpandedNodeIds.Contains(id))
                {
                    var dataExp = node["data"] as JObject;
                    if (dataExp != null) dataExp["expanded"] = true;
                }
```

Thread the `expandedNodeIds` from the layout-autosave message handler in the UI layer the same way `disabledBlockIds` is threaded (follow the `disabledBlockIds` call site in `UI/FlowCanvasForm.cs`).

- [ ] **Step 6: Verify export ignores it** — add a test asserting that a node with `data.expanded = true` exports identical YAML to one without (export reads `node.data.props`, not `node.data.expanded`). Run the C# suite; Expected: PASS.
- [ ] **Step 7: Commit**

```bash
git add Models/CanvasLayoutData.cs Services/FlowCanvasBridge.cs UI/FlowCanvasForm.cs SSH_Helper.Tests/Services/FlowCanvasBridgeLayoutPersistenceTests.cs
git commit -m "feat(flow-canvas): persist ExpandedNodeIds in CanvasLayoutData (excluded from YAML)"
```

**Phase 3 verification:** both suites pass; expand a block, reload the canvas, confirm it stays expanded; export YAML unchanged.

---

## Phase 4 — Read-only summary + chevron in `BaseBlock`

**Outcome:** expanding a block (chevron) replaces its preview with a read-only summary of required + non-default fields; "Edit in Properties" selects the node.

### Task 4.1: `blockSummary.ts` helper

**Files:**
- Create: `FlowCanvas/src/utils/blockSummary.ts`
- Test: `FlowCanvas/src/utils/__tests__/blockSummary.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// FlowCanvas/src/utils/__tests__/blockSummary.test.ts
import { describe, it, expect } from 'vitest';
import { summarizeBlock } from '../blockSummary';

describe('summarizeBlock', () => {
  it('shows required + non-default fields, hides defaults', () => {
    const r = summarizeBlock('send', { command: 'show ver', capture: 'out' });
    const keys = r.rows.map((x) => x.key);
    expect(keys).toContain('command'); // required
    expect(keys).toContain('capture'); // non-default
    expect(keys).not.toContain('timeout'); // default (empty)
    expect(keys).not.toContain('on_error'); // default 'stop'
    expect(r.hiddenCount).toBeGreaterThan(0);
  });
  it('marks a required-but-empty field as not set', () => {
    const r = summarizeBlock('send', {});
    const cmd = r.rows.find((x) => x.key === 'command')!;
    expect(cmd.notSet).toBe(true);
  });
  it('respects http auth conditional (auth=none hides token)', () => {
    const r = summarizeBlock('http', { url: 'https://x', auth: 'none', token: 'abc' });
    // token is advanced + auth=none → not required; it has a value so it still shows as non-default,
    // but is masked. Assert masking flag instead of visibility.
    const tok = r.rows.find((x) => x.key === 'token');
    if (tok) expect(tok.masked).toBe(true);
  });
});
```

- [ ] **Step 2: Run it, verify it fails** — Run: `cd FlowCanvas && npx vitest run src/utils/__tests__/blockSummary.test.ts` — Expected: FAIL (module missing).

- [ ] **Step 3: Create `blockSummary.ts`** (port the required/default logic from `Properties.tsx`)

```ts
// FlowCanvas/src/utils/blockSummary.ts
import { blockDefMap, type PropertyDef } from '../blockDefs/registry';

export interface SummaryRow {
  key: string; label: string; value: string;
  isCode: boolean; masked: boolean; notSet: boolean;
}
export interface BlockSummary { rows: SummaryRow[]; hiddenCount: number; }

const SECRET_KEYS = new Set(['password', 'token']);

function toBool(v: unknown, d: boolean): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'string') return v.trim().toLowerCase() === 'true';
  return d;
}
function hasValue(v: unknown): boolean {
  return v !== undefined && v !== null && String(v).trim() !== '';
}

/** Required logic ported from Properties.isPropertyRequired (conditional cases preserved). */
function isRequired(blockType: string, def: PropertyDef, props: Record<string, unknown>): boolean {
  const base = !!def.required;
  if (blockType === 'readfile' && def.key === 'path') return !toBool(props.select_file, false);
  if (blockType === 'readfile' && def.key === 'into') return !toBool(props.path_only, false);
  if (blockType === 'readfile' && def.key === 'path_into') return toBool(props.path_only, false);
  if (blockType === 'http') {
    const auth = String(props.auth ?? 'none').trim().toLowerCase();
    if (def.key === 'username' || def.key === 'password') return auth === 'basic';
    if (def.key === 'token') return auth === 'bearer';
    return base;
  }
  if (blockType === 'interactive') {
    if (!toBool(props.show_window, true) && def.key === 'command') return true;
  }
  return base;
}

/** A field is "non-default" if it has a value that differs from its declared defaultValue. */
function isNonDefault(def: PropertyDef, props: Record<string, unknown>): boolean {
  const v = props[def.key];
  if (!hasValue(v)) return false;
  if (def.defaultValue === undefined || def.defaultValue === null) return true;
  return String(v) !== String(def.defaultValue);
}

export function summarizeBlock(blockType: string, props: Record<string, unknown>): BlockSummary {
  const def = blockDefMap.get(blockType);
  if (!def) return { rows: [], hiddenCount: 0 };
  const rows: SummaryRow[] = [];
  let hidden = 0;
  for (const p of def.properties) {
    const required = isRequired(blockType, p, props);
    const nonDefault = isNonDefault(p, props);
    if (!required && !nonDefault) { hidden++; continue; }
    const raw = props[p.key];
    const notSet = required && !hasValue(raw);
    rows.push({
      key: p.key, label: p.label,
      value: notSet ? '— not set' : (SECRET_KEYS.has(p.key) ? '••••••••' : String(raw)),
      isCode: p.type === 'code',
      masked: SECRET_KEYS.has(p.key) && hasValue(raw),
      notSet,
    });
  }
  return { rows, hiddenCount: hidden };
}
```

- [ ] **Step 4: Run it, verify it passes** — Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/blockSummary.ts FlowCanvas/src/utils/__tests__/blockSummary.test.ts
git commit -m "feat(flow-canvas): blockSummary helper (required + non-default rows)"
```

### Task 4.2: Chevron + summary rendering in `BaseBlock.tsx`

**Files:** Modify `FlowCanvas/src/nodes/BaseBlock.tsx` (header `:288-323`, preview `:325-338`); Test: `BaseBlock.test.tsx`

- [ ] **Step 1: Write the failing test** (append)

```tsx
it('renders the read-only summary when expanded and hides the preview', () => {
  // store stub returns isExpanded=true
  renderNode({ data: { blockType: 'send', label: 'Send', props: { command: 'show ver', capture: 'out' } } as any });
  expect(screen.getByTestId('block-summary')).toBeInTheDocument();
  expect(screen.getByText('Edit in Properties')).toBeInTheDocument();
});
```

Update the `vi.mock('../../stores/useFlowStore', ...)` stub in this file to also return `isExpanded: () => true`, `toggleExpanded: vi.fn()`, and `selectNode: vi.fn()` so the selector hooks resolve.

- [ ] **Step 2: Run it, verify it fails** — Run: `cd FlowCanvas && npx vitest run src/nodes/__tests__/BaseBlock.test.tsx` — Expected: FAIL (no `block-summary`).

- [ ] **Step 3: Implement in `BaseBlock.tsx`**

Imports:

```tsx
import { summarizeBlock } from '../utils/blockSummary';
```

Store hooks (near the other `useFlowStore` calls at the top of the component):

```tsx
  const isExpanded = useFlowStore((s) => s.isExpanded(id));
  const toggleExpanded = useFlowStore((s) => s.toggleExpanded);
  const selectNode = useFlowStore((s) => s.selectNode);
```

Chevron — in the header, right after `{execIndicator}`, add a toggle (down when expanded, right when collapsed):

```tsx
        <span
          data-testid="expand-toggle"
          onClick={(e) => { e.stopPropagation(); toggleExpanded(id); }}
          style={{ marginLeft: 4, cursor: 'pointer', color: 'var(--fc-text-secondary)', display: 'flex' }}
          title={isExpanded ? 'Collapse' : 'Expand settings'}
        >
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <polyline points={isExpanded ? '6 9 12 15 18 9' : '9 6 15 12 9 18'} />
          </svg>
        </span>
```

Body — replace the existing `{previewText && (...)}` preview block with a conditional: summary when expanded, else the preview:

```tsx
      {isExpanded ? (
        <div data-testid="block-summary" style={{ background: 'var(--fc-surface-0)', padding: '8px 9px 6px' }}>
          {summarizeBlock(blockData.blockType, (blockData.props ?? {}) as Record<string, unknown>).rows.map((r) => (
            <div key={r.key} style={{ display: 'flex', gap: 10, padding: '3px 0', alignItems: 'baseline' }}>
              <span style={{ flex: 'none', width: 96, fontSize: 10.5, fontWeight: 600, color: 'var(--fc-text-secondary)' }}>{r.label}</span>
              <span style={{
                flex: 1, fontSize: 11.5, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                fontFamily: r.isCode ? 'var(--fc-font-mono)' : undefined,
                color: r.notSet ? 'var(--fc-text-faint)' : (r.isCode ? colors.text : 'var(--fc-text)'),
              }}>{r.value}</span>
            </div>
          ))}
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 5, paddingTop: 6,
                        borderTop: '1px solid var(--fc-border)', fontSize: 10 }}>
            <span style={{ color: 'var(--fc-text-faint)' }}>
              {summarizeBlock(blockData.blockType, (blockData.props ?? {}) as Record<string, unknown>).hiddenCount} fields at default
            </span>
            <span
              onClick={(e) => { e.stopPropagation(); selectNode(id); }}
              style={{ color: 'var(--fc-accent)', cursor: 'pointer' }}
            >Edit in Properties</span>
          </div>
        </div>
      ) : previewText ? (
        <div style={{ padding: '4px 8px', fontFamily: 'monospace', fontSize: 12,
          color: isDisabled ? 'var(--fc-text-disabled)' : colors.text,
          overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {previewText}
        </div>
      ) : null}
```

(Compute the summary once into a local `const summary = isExpanded ? summarizeBlock(...) : null;` above the return to avoid calling it twice — DRY.)

- [ ] **Step 4: Run it, verify it passes** — Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/nodes/BaseBlock.tsx FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx
git commit -m "feat(flow-canvas): in-place read-only settings summary with expand chevron"
```

**Phase 4 verification:** build clean; in the app, clicking the chevron expands a block to its summary (preview replaced), "Edit in Properties" selects the node and the panel focuses it; collapse restores the preview.

---

## Phase 5 — Height-aware layout (the integration phase)

> ⚠️ **Open decision before executing this phase** — see "Open decision" below. Confirm reflow-on-toggle vs. local-push for manually-arranged canvases.

**Outcome:** expanded (taller) blocks push their successors down and grow their enclosing lanes; toggling expand reflows.

### Task 5.1: Expanded height estimate in `nodeSize.ts`

**Files:** Modify `FlowCanvas/src/utils/nodeSize.ts`; Test: `nodeSize.test.ts`

- [ ] **Step 1: Write the failing test** (append)

```ts
import { estimateNodeHeight } from '../nodeSize';
it('estimates collapsed vs expanded height', () => {
  const collapsed = estimateNodeHeight('send', { command: 'x' }, false);
  const expanded = estimateNodeHeight('send', { command: 'x', capture: 'y' }, true);
  expect(collapsed).toBe(52);
  expect(expanded).toBeGreaterThan(collapsed); // header + 2 rows + footer
});
```

- [ ] **Step 2: Run it, verify it fails** — Expected: FAIL (`estimateNodeHeight` missing).
- [ ] **Step 3: Implement** — add to `nodeSize.ts`:

```ts
import { summarizeBlock } from './blockSummary';

export function estimateNodeHeight(blockType: string, props: Record<string, unknown>, expanded: boolean): number {
  if (!expanded) return COLLAPSED_HEIGHT;
  const rows = summarizeBlock(blockType, props).rows.length;
  // header (~30) + summary body (pad + rows + footer)
  return 30 + SUMMARY_PAD + rows * SUMMARY_ROW_H + SUMMARY_FOOTER_H;
}
```

(Watch the import cycle: `blockSummary.ts` must not import `nodeSize.ts`. It doesn't — keep it that way.)

- [ ] **Step 4: Run it, verify it passes** — Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/nodeSize.ts FlowCanvas/src/utils/__tests__/nodeSize.test.ts
git commit -m "feat(flow-canvas): expanded node height estimate"
```

### Task 5.2: Height-aware vertical advance in `hierarchicalLayout.ts`

**Files:** Modify `FlowCanvas/src/utils/layout/hierarchicalLayout.ts` (`placeBranchSteps` :63-80, `placeTree` :122-133); Test: `hierarchicalLayout.test.ts`

- [ ] **Step 1: Write the failing test** (append)

```ts
it('advances further past an expanded node than a collapsed one', () => {
  const exp: LayoutTreeNode = { id: 'x', node: { id: 'x', data: { blockType: 'send', expanded: true, props: { command: 'a', capture: 'b' } } } as never, isContainer: false, branches: [] };
  const after = leaf('after');
  const pos = placeTree({ spine: [exp, after] });
  expect(pos.get('after')!.y - pos.get('x')!.y).toBeGreaterThan(LAYOUT.NODE_SPACING_Y);
});
```

- [ ] **Step 2: Run it, verify it fails** — Expected: FAIL (fixed 106 advance).
- [ ] **Step 3: Implement a per-node advance** — add a helper and use it where `y += LAYOUT.NODE_SPACING_Y` appears (in `placeBranchSteps` and `placeTree`):

```ts
import { estimateNodeHeight, COLLAPSED_HEIGHT } from '../nodeSize';
const VERTICAL_GAP = LAYOUT.NODE_SPACING_Y - COLLAPSED_HEIGHT; // 54 — preserves collapsed spacing

function advanceFor(n: LayoutTreeNode): number {
  const data = (n.node?.data ?? {}) as { blockType?: string; expanded?: boolean; props?: Record<string, unknown> };
  if (!data.expanded) return LAYOUT.NODE_SPACING_Y;
  return estimateNodeHeight(data.blockType ?? '', data.props ?? {}, true) + VERTICAL_GAP;
}
```

Replace `y += LAYOUT.NODE_SPACING_Y;` in `placeBranchSteps` with `y += advanceFor(child);`. In `placeTree`, replace `currentY += LAYOUT.NODE_SPACING_Y;` with `currentY += advanceFor(node);`.

- [ ] **Step 4: Run it, verify it passes** — Expected: PASS. Also re-run the whole layout test file (collapsed spacing must still equal `NODE_SPACING_Y`).
- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/layout/hierarchicalLayout.ts FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts
git commit -m "feat(flow-canvas): height-aware vertical spacing for expanded blocks"
```

### Task 5.3: Band geometry uses expanded height

**Files:** Modify `FlowCanvas/src/utils/branchBands.ts` (`nodeBox`); Test: `branchBands.test.ts`

- [ ] **Step 1: Write the failing test** (append) — an expanded child produces a taller band than a collapsed one (same position).

```ts
it('grows the band for an expanded child', () => {
  const collapsed = computeBranchBands([child('c', 'p', 'steps/1/then/0', 100, 200)])[0];
  const expNode = child('c', 'p', 'steps/1/then/0', 100, 200);
  (expNode.data as any).blockType = 'send';
  (expNode.data as any).expanded = true;
  (expNode.data as any).props = { command: 'a', capture: 'b', _isChildOf: 'p', _stepPath: 'steps/1/then/0' };
  const expanded = computeBranchBands([expNode])[0];
  expect(expanded.height).toBeGreaterThan(collapsed.height);
});
```

- [ ] **Step 2: Run it, verify it fails** — Expected: FAIL (fixed COLLAPSED_HEIGHT).
- [ ] **Step 3: Implement** — change `nodeBox` to read expanded state:

```ts
import { CHILD_WIDTH, COLLAPSED_HEIGHT, estimateNodeHeight } from './nodeSize';

function nodeBox(n: Node): { w: number; h: number } {
  const data = (n.data ?? {}) as { blockType?: string; expanded?: boolean; props?: Record<string, unknown> };
  const h = data.expanded ? estimateNodeHeight(data.blockType ?? '', data.props ?? {}, true) : COLLAPSED_HEIGHT;
  return { w: CHILD_WIDTH, h };
}
```

- [ ] **Step 4: Run it, verify it passes** — Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/branchBands.ts FlowCanvas/src/utils/__tests__/branchBands.test.ts
git commit -m "feat(flow-canvas): branch bands wrap expanded children"
```

### Task 5.4: Reflow on toggle

**Files:** Modify `FlowCanvas/src/stores/slices/debugSlice.ts` (`toggleExpanded`)

- [ ] **Step 1: Decide the reflow rule** — see "Open decision". Default (recommended v1): reflow via the existing layout function. After `get().updateNodeData(nodeId, { expanded: nowExpanded });` and before `sendLayoutAutosave();`, add:

```ts
    // Reflow so the taller/shorter block pushes neighbors (height-aware layout).
    const st = get();
    st.setNodes(computeHierarchicalLayout(st.nodes, st.edges));
```

Add the import at the top of `debugSlice.ts`: `import { computeHierarchicalLayout } from '../../utils/layout/hierarchicalLayout';`

- [ ] **Step 2: Update the slice test** — extend `debugSlice.expanded.test.ts` to seed two spine nodes and assert the second moves further down after expanding the first (mock `computeHierarchicalLayout` is NOT mocked here; provide minimal `nodes`/`edges` + `setNodes`). If the store wiring is heavy to unit-test, assert instead that `setNodes` is called on toggle (spy).
- [ ] **Step 3: Run it, verify it passes** — Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/debugSlice.expanded.test.ts` — Expected: PASS.
- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/stores/slices/debugSlice.ts FlowCanvas/src/stores/slices/__tests__/debugSlice.expanded.test.ts
git commit -m "feat(flow-canvas): reflow layout when a block expands/collapses"
```

### Task 5.5: e2e — expansion pushes neighbors, persists, child grows lane

**Files:** Create `FlowCanvas/e2e/flow-canvas-expansion.spec.ts`

- [ ] **Step 1: Write the e2e** — load a fixture with a spine SEND (with `command`+`capture` set) and a following block; capture the follower's `y` via `getGraphSnapshot`; click `[data-testid="expand-toggle"]` on the SEND; assert the follower's `y` increased; assert `[data-testid="block-summary"]` visible. Add a second case: a child block inside a THEN lane expands and the `[data-testid="branch-band"]` height (via `offsetHeight`) grows. Use the helpers from `flow-canvas-auto-layout.spec.ts` (`installHostMessageCapture`, `postHostMessage`, `getGraphSnapshot`) and the `offsetHeight` pattern from `flow-canvas-edge-geometry.spec.ts`.
- [ ] **Step 2: Run it, verify it passes** — Run: `cd FlowCanvas && npx playwright test flow-canvas-expansion.spec.ts` — Expected: PASS.
- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/e2e/flow-canvas-expansion.spec.ts
git commit -m "test(flow-canvas): e2e for expansion push, persistence, and lane growth"
```

**Phase 5 verification:** full `npm test` + `npm run build` + `dotnet build` + `dotnet test` all pass; in the app, expanding a block pushes its successors and grows its lane with no overlap; reload keeps expanded state; YAML export unchanged.

---

## Open decision (confirm before Phase 5 Task 5.4)

Reflow-on-toggle re-runs `computeHierarchicalLayout`, which **re-derives all positions** — fine for auto-laid canvases (the norm), but it will **discard manual position tweaks** a user made (the `hasUserLayout` case). Options:
- **A (planned default):** always reflow on toggle. Simple, predictable, reuses existing code; loses manual nudges.
- **B:** when `hasUserLayout`, skip full reflow and instead shift only the nodes below the toggled block (and grow its lane) by the height delta. Preserves manual layout; more code, trickier with branches/columns.

Recommend A for v1 (this is a preset-builder, mostly auto-laid). Confirm before implementing 5.4; if B is wanted, 5.4 expands into its own sub-tasks.

## Self-review (completed)

- **Spec coverage:** sizing (Phase 1), labeled lanes (Phase 2), expansion state+persistence (Phase 3), read-only summary (Phase 4), height-aware layout (Phase 5), invariants (3.3 step 6 YAML test; expand via `node.data` not `props`), edges untouched (no edge files in the file list). ✓
- **Placeholder scan:** every code step has real code; the one deferred item (reflow rule) is an explicit, flagged decision, not a hidden TODO. ✓
- **Type consistency:** `expandedNodes`/`toggleExpanded`/`isExpanded`/`restoreExpandedNodes`, `summarizeBlock`→`{rows,hiddenCount}`/`SummaryRow`, `estimateNodeHeight(blockType, props, expanded)`, `nodeWidth`/`SPINE_WIDTH`/`CHILD_WIDTH`/`COLLAPSED_HEIGHT`, `BAND_PAD`/`branchPillLabel`, `ExpandedNodeIds` used consistently across phases. ✓
