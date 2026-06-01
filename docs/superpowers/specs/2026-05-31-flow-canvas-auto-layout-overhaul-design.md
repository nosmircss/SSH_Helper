# Flow Canvas Auto-Organize Overhaul — Design

**Date:** 2026-05-31
**Status:** Approved (design) — pending implementation plan
**Scope:** Visual layout only. Does not change YAML export (the canvas is a preset-builder; positions are visual state).

## Problem

"Auto-organize" on a branchy script produces a scattered, overlapping layout: branch
child blocks (e.g. the `PRINT HA Mode: …` blocks under a chain of `IF` containers) end
up stranded away from their parent, the colored branch bands overlap, and edges cross.
Separately, importing a preset and clicking Auto-organize produce *different* layouts.

## Root Cause

Two independent problems:

1. **The Auto-organize button is structure-blind.** `useAutoLayout.ts` →
   `computeAutoLayout()` in `FlowCanvas/src/utils/autoLayout.ts` runs dagre (`rankdir: TB`,
   `ranksep: 55`, `nodesep: 40`). It **deliberately excludes every container child**
   (any node whose `data.props._isChildOf` is set) from the dagre graph and **drops every
   edge that touches a child**, then keeps those children at their old absolute
   coordinates. So dagre only re-stacks the top-level spine; container children detach
   from their re-positioned parents → scatter + overlapping branch bands. dagre has no
   concept of branches, branch bands, or nesting, so it can never lay this out cleanly.

2. **Import and the button use different algorithms.** Import positions are computed in
   C# (`Services/FlowCanvasBridge.cs` → `TextToGraph` → `ExpandContainerChildren`), a
   structure-aware hybrid layout. The button uses the TS dagre pass. They disagree by
   design.

## Goals

- One **structure-aware** layout engine that knows containers, branches, branch bands,
  and nesting.
- The **smart-hybrid** arrangement: vertical spine; single-branch containers
  (foreach/while/repeat, if-without-else) indent their body; multi-branch containers
  (if/else, switch, parallel, try/catch) fan into measured side-by-side columns that
  rejoin below the tallest branch; recursive nesting.
- **Unify**: the same engine drives both the Auto-organize button and preset import, so
  they always agree. The spacing constants live in **one place** (TypeScript).
- Retire dagre.

## Non-Goals

- No change to YAML export or the YAML↔graph round-trip (visual only).
- No redesign of branch-band rendering (`BranchBandsLayer.tsx` / `branchBands.ts`) or the
  edge renderer (`AnimatedEdge`). The new layout produces geometry those existing systems
  already consume; good node positions ⇒ clean bands and edges.
- No change to the OKLCH token color system or branch color mapping.

## Behavior Matrix

| Trigger | Behavior |
| --- | --- |
| **Auto-organize button** | **Always** re-lays out the whole graph — overrides any manual arrangement. Explicit action; pushes an undo snapshot (reversible). |
| **Import — never arranged** | Auto hybrid layout (replaces today's cramped/scattered import). |
| **Import — previously arranged** | Keeps the user's saved positions (existing `MergeLayout`). Auto-layout does not clobber it; the **button** still overrides on demand. |

## Architecture

One pure function, in a new `FlowCanvas/src/utils/layout/` folder:

```
computeHierarchicalLayout(nodes, edges) → Node[]   // repositioned nodes
  1. reconstructTree(nodes, edges)   // treeBuilder.ts
  2. placeTree(tree)                 // hierarchicalLayout.ts
```

**Callers (both pass through the same engine):**

- `hooks/useAutoLayout.ts` — Auto-organize button. Always runs the engine:
  `pushSnapshot('Auto-layout')` → `computeHierarchicalLayout(...)` → `setNodes(..., { markDirty: true })`.
- `stores/messageBridge.ts` — on the inbound `load-graph` message: if `!msg.hasUserLayout`,
  compute the layout on `msg.nodes` and `setNodes` the result **once** (compute-then-set,
  no first-paint flash); otherwise `setNodes(msg.nodes)` as-is (saved positions win).

**Import with a saved arrangement** keeps the existing path: C# `MergeLayout` overlays the
saved positions when the structure hash matches, and React leaves them alone.

### New modules

- **`utils/layout/treeBuilder.ts`** — reconstructs the container→branch→child tree from
  the graph. Universal structure source = **edges + `sourceHandle` + branch metadata**,
  which exist for both imported and canvas-built graphs (`_isChildOf` / `_stepPath`
  corroborate imported graphs). Outline:
  - Roots: the `__start__` node (or nodes with no incoming edge).
  - Walk forward edges to build the sequence (spine).
  - Container detection via the block registry (`blockDefs/registry.ts` `isContainer`).
  - For a container, group its outgoing **branch** edges by branch key
    (`sourceHandle` like `then`/`false`/`continue` and/or `data.branchPath` like
    `then`/`else`/`elif/0/then`/`cases/2/do`/`parallel/1`/`do`/`try`/`catch`/`finally`),
    and the **continuation** edge (the `continue` handle / "next" edge) that exits the
    container to the next sibling. This mirrors what the C# importer's `PlaceBranchSteps`
    emits.
  - Recurse into branch children that are themselves containers (depth cap = 5, matching
    `MaxNestingDepth`).
  - **Cycle guards:** loop back-edges (while/foreach body → header) must not be treated as
    forward structure; track visited nodes; cap recursion depth.
  - **Orphans:** disconnected / mid-edit nodes go to a fallback column — never dropped.

- **`utils/layout/hierarchicalLayout.ts`** — `placeTree()`, a faithful TypeScript port of
  the C# hybrid algorithm (`ExpandContainerChildren` → `ExpandSingleBranch` /
  `ExpandMultiBranch` / `PlaceBranchSteps` / `MeasureSteps` / `GetColumnWidth`). Holds the
  spacing constants as the single source of truth. Preserves comment nodes and places
  `__start__` at the top center; positions are absolute.

### Layout rules (ported from C# `FlowCanvasBridge.cs`)

- **Spine:** vertical stack, `NodeSpacingY` between successive nodes, at a fixed center X.
- **Single-branch container:** children offset right by `SingleBranchChildOffset` and
  stacked vertically, leaving a clear left corridor for the continuation edge.
- **Multi-branch container:** measure each branch's column count (`MeasureSteps`),
  `colWidth = GetColumnWidth(depth)` (decays by `ColumnWidthDecay`, floored at
  `MinColumnWidth`), total spread capped at `MaxSpreadWidth`; branches centered around the
  container's center X; each branch stacked vertically; the next sibling starts below the
  **tallest** branch.
- **Recursion:** nested containers recurse up to `MaxNestingDepth`.

**Constants (move to TS as the single source; C# copy removed in Phase 3):**

| Constant | Value | Role |
| --- | --- | --- |
| `NodeSpacingY` | 106 | vertical step between blocks (incl. the earlier 85→106 bump) |
| `SingleBranchChildOffset` | 70 | right offset for single-branch children |
| `NodeStartX` / `NodeStartY` | 250 / 40 | spine origin |
| `ChildNodeMaxWidth` | 260 | node width used for column sizing |
| `ColumnGap` | 30 | gap between branch columns |
| `BaseColumnWidth` / `MinColumnWidth` | 290 | column width floor (node + gap) |
| `ColumnWidthDecay` | 0.92 | column narrowing per depth |
| `MaxSpreadWidth` | 1400 | cap on total horizontal spread |
| `MaxNestingDepth` | 5 | recursion cap |

## Import Data Flow (unify wiring)

```
C# TextToGraph → nodes + edges + metadata
  → load-graph { nodes, edges, hasUserLayout } → React
      React: hasUserLayout ? keep positions : computeHierarchicalLayout() → setNodes (once)
```

C# changes: `UI/FlowCanvasForm.cs` `LoadGraph(...)` gains a `hasUserLayout` argument;
`Form1.LoadCurrentScriptIntoCanvas` computes it = (active preset has a `CanvasLayout` whose
`StructureHash` matches the freshly-computed structure hash). React message type
`communication-message-types.ts` gains `hasUserLayout` on the `load-graph` message.

## Files

| Action | File | What |
| --- | --- | --- |
| NEW | `FlowCanvas/src/utils/layout/treeBuilder.ts` | dual-origin tree reconstruction from edges + branch metadata |
| NEW | `FlowCanvas/src/utils/layout/hierarchicalLayout.ts` | `placeTree()` + spacing constants (single source of truth) |
| NEW | `FlowCanvas/src/utils/layout/computeHierarchicalLayout` | entry that wires treeBuilder → placeTree (can live in hierarchicalLayout.ts) |
| EDIT | `FlowCanvas/src/hooks/useAutoLayout.ts` | call the new engine instead of dagre (always overrides) |
| EDIT | `FlowCanvas/src/stores/messageBridge.ts` | run the engine on import when `!hasUserLayout` (compute-then-set once) |
| EDIT | `UI/FlowCanvasForm.cs`, `Form1.cs` | add `hasUserLayout` to the `load-graph` message |
| EDIT | `FlowCanvas/src/communication-message-types.ts` | add `hasUserLayout` to the `load-graph` type |
| DELETE | `FlowCanvas/src/utils/autoLayout.ts` + `@dagrejs/dagre` dep | retire the structure-blind layout |
| EDIT (Phase 3) | `Services/FlowCanvasBridge.cs` | retire position math (`Expand*`/`PlaceBranchSteps`/`MeasureSteps`); keep node/edge/metadata creation + `ComputeStructureHash` |
| NEW | vitest unit tests + Playwright e2e | see Testing |

## Build Phases

Each phase touches ≤5 files and is independently verifiable.

- **Phase 1 — Engine + button.** Build `treeBuilder` + `hierarchicalLayout` +
  `computeHierarchicalLayout`; wire `useAutoLayout` (always overrides); vitest unit tests;
  delete `autoLayout.ts` + the dagre dependency.
  *Verify:* click Auto-organize on an imported branchy preset → clean hybrid, no overlaps,
  branch bands wrap their own columns.

- **Phase 2 — Import unification.** Add `hasUserLayout` (C# `LoadGraph`/`LoadCurrentScriptIntoCanvas`
  + message type); `messageBridge` runs the engine on fresh imports and preserves saved
  arrangements; Playwright e2e.
  *Verify:* fresh import is clean; re-opening a hand-arranged preset keeps its positions;
  the button still overrides.

- **Phase 3 — Retire C# position math.** Replace `ExpandContainerChildren` and friends with
  a trivial positioner (or drop position computation), keeping node/edge/metadata creation
  and `ComputeStructureHash`.
  *Verify:* `FlowCanvasBridgeTests` green; YAML round-trip intact.

## Testing

- **vitest unit** (existing jsdom harness): `treeBuilder` reconstructs the correct tree for
  **both** an imported-style graph (structure on child props) and a canvas-built graph
  (structure on edges); cycle guard on loop back-edges; orphan fallback. `placeTree`
  invariants: spine vertical, single-branch indented, multi-branch columns non-overlapping
  and rejoining, recursion. Assert no two nodes overlap (bbox using node width/height).
- **Playwright e2e** (per the canvas e2e gotchas): import a branchy preset, click
  Auto-organize, assert clean layout — use `offsetWidth`/flow coords (not `boundingBox`,
  which is zoom-scaled by `fitView`); vertical SVG paths assert via `toHaveCount`, not
  `toBeVisible`.
- **C#** (`dotnet test SSH_Helper.Tests`): remains green; no existing test asserts on
  computed import Y positions, so Phase 1–2 need no C# test changes; Phase 3 updates only
  if it touches asserted structure.

## Risks & Mitigations

| Risk | Mitigation |
| --- | --- |
| Tree reconstruction differs for imported vs canvas-built graphs | Derive structure from edges + `sourceHandle` + branch metadata (universal); reuse `branchBands`/`edgePath` correlation logic; unit-test both origins |
| Loop back-edges (while/foreach) create cycles | Cycle guards (visited set) + recursion depth cap; back-edges ignored for forward structure |
| Orphan / disconnected nodes during mid-edit | Fallback placement column — never drop a node |
| Layout flash on import | Compute layout, then `setNodes` once (no intermediate paint) |

## Decisions Log

- **Engine:** Custom hierarchical TS layout (vs ELK / dagre-clusters) — deterministic,
  matches the hybrid exactly, reuses proven C# logic, lets us delete dagre.
- **Arrangement:** Smart hybrid (loops indent; multi-branch columns).
- **Unify:** One engine for import + button; spacing constants move to TS.
- **Button:** Always overrides manual arrangement (undoable). Only automatic-on-import
  preserves saved arrangements.
