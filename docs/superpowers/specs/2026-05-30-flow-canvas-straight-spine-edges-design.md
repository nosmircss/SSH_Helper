# Flow Canvas — Straight-Spine Edge Routing

**Date:** 2026-05-30
**Status:** Design — awaiting review
**Area:** `FlowCanvas/` (React + @xyflow/react v12), `Services/FlowCanvasBridge.cs` (read-only verification only)

## Problem

Continuation edges between vertically-stacked blocks render a "down → up → down"
paperclip hook (see the user's screenshot). It is most visible during a run,
because the moving packet animates along the hooked path.

## Root cause (verified against the compiled @xyflow/system algorithm)

Three facts combine:

1. **Default smoothstep `offset`.** `AnimatedEdge.tsx:15` calls
   `getSmoothStepPath({ ..., borderRadius: 8 })` and leaves `offset` at its
   default `20`. smoothstep pushes a 20px perpendicular stub out of *each*
   handle before turning (`sourceGapped = source + dir*offset`).
2. **Centered handles on variable-width nodes.** Top-level blocks are variable
   width (`minWidth:180 / maxWidth:280`, `BaseBlock.tsx:150-151`) and left-aligned
   at `NodeStartX=250` (`FlowCanvasBridge.cs:65`). The Top/Bottom handles are
   centered (`BaseBlock.tsx:296-300`, `:357-361`), so consecutive blocks of
   different widths put the source-bottom and target-top handles ~40px apart in X.
3. **Tiny vertical gap.** `NodeSpacingY=85` (`FlowCanvasBridge.cs:63`) is the delta
   between node *tops*; a block is ~75px tall, so the real bottom→top gap is ~10px.

Because the gap (~10px) is far smaller than `2 × offset` (40px), the two stubs
overshoot past each other; the orthogonal router doubles back and jogs sideways
to cover the 40px X offset → the rounded hook. The packet
(`offsetPath: path(edgePath)`, `AnimatedEdge.tsx:53`) and the gradient both follow
that path, so the artifact is visible at rest and amplified during a run.

## Goals

- The main vertical continuation chain renders as a **dead-straight spine**,
  including the **Start → first block** edge.
- The running packet glides straight down that spine.
- Branch / loop **corridor edges keep their orthogonal routing** (they carve
  paths around child blocks and must not become diagonals).
- **Zero change to YAML export, connection rules, or any persisted data** — this
  is purely presentational.

## Non-goals

- No change to branch/loop layout, column math, or branch coloring.
- No change to the node card design beyond a fixed width.
- No new edge *types* in React Flow (`AnimatedEdge` stays the single renderer).

## Decisions (locked with the user)

| Decision | Choice | Rationale |
|---|---|---|
| Top-level block width | **Fixed 280px** (current `maxWidth`) | No block truncates more than today; blocks simply stop shrinking for short content. |
| Start node | **Match block width (280px)** | Simplest path to a straight Start→first edge; Start stays distinct via its gradient, accent border, glow, and START badge. |
| Spine geometry | **`getStraightPath`** | Unconditional literal `M..L`; cannot reintroduce a lateral jog from sub-pixel drift. |
| Spine vs corridor discriminator | **Geometry-based** (`|sourceX − targetX| < 0.5`) | Robust across import *and* user-drawn edges; needs no metadata. See below. |

## Why a geometry discriminator (not metadata)

`AnimatedEdge` renders **every** edge (`App.tsx:353` forces `type:'animated'`;
`:400` `defaultEdgeOptions`), so it must decide per-edge whether to route straight
or orthogonally.

The verification panel first proposed keying on `sourceHandleId` + `data.branchPath`.
Reading the bridge's actual emission showed that is **leaky**: on *import*
(`FlowCanvasBridge.cs:666-695`), branch edges are tagged by `style.stroke =
branch.Color` and `sourceHandle` (only on the first edge of a branch), and carry
**no `data.branchPath`**. `data.branchPath` is only attached to *user-drawn* edges
(`graphSlice.ts:300`) and reconstructed on export. So `!branchPath && emptyHandle`
would misclassify imported branch child-chains.

A geometry test is exact and source-agnostic — an edge is a straight spine run
**iff its endpoints are horizontally aligned and it runs downward**:

```
EPS = 0.5
isSpine = Math.abs(sourceX - targetX) < EPS && targetY > sourceY
```

Validated against every edge class the bridge + `onConnect` produce:

| Edge | After fix | dx | Result |
|---|---|---|---|
| Top-level spine (uniform width, centered) | aligned | 0 | **straight** ✓ |
| Start → first block (Start now 280) | aligned | 0 | **straight** ✓ |
| `continue` (container Left handle → sibling) | offset | ~145 | smoothstep ✓ |
| `false` (IF Right handle → else child) | offset | large | smoothstep ✓ |
| Branch-first (container center → offset child column) | offset | >0 | smoothstep ✓ |
| Branch child→child (column, may differ in width) | small/0 | — | straight if aligned, else smoothstep ✓ |

If two handles are vertically aligned, a straight line between them is *always*
the ideal route, so the rule has no false positives for this top-down graph
(which has no X-aligned back-edges spanning intervening nodes).

## Changes (the contract)

### 1. `FlowCanvas/src/nodes/BaseBlock.tsx`
Make non-child blocks a fixed 280px so centered handles align. Children unchanged.

- Line 150: `minWidth: isChild ? 160 : 180` → `minWidth: isChild ? 160 : 280`
  (so non-child `min === max === 280`). `maxWidth` stays `isChild ? 260 : 280`.
- `overflow: 'hidden'` (already at `:152`) + existing ellipsis keep long labels clipped.

### 2. `FlowCanvas/src/nodes/StartNode.tsx`
- Lines 53-54: `minWidth: 260, maxWidth: 300` → `minWidth: 280, maxWidth: 280`
  so Start's centered bottom handle shares the spine X.

### 3. `FlowCanvas/src/nodes/AnimatedEdge.tsx`
- Import `getStraightPath` alongside `getSmoothStepPath` (line 2).
- Replace the single `getSmoothStepPath` call (lines 15-17) with the geometry split:

```tsx
const EPS = 0.5;
const isSpine = Math.abs(sourceX - targetX) < EPS && targetY > sourceY;
const [edgePath] = isSpine
  ? getStraightPath({ sourceX, sourceY, targetX, targetY })
  : getSmoothStepPath({
      sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, borderRadius: 8,
    });
```

- Everything else in `AnimatedEdge` is unchanged: the `userSpaceOnUse` gradient
  (a vertical vector is valid, not degenerate), `markerEnd` / `markerIdForStroke`,
  the packet `offsetPath`, the selection `strokeWidth`, and reduced-motion gating
  all consume `edgePath`/endpoints and work identically on a straight path.

### 4. `FlowCanvas/src/nodes/branchBands.ts` (housekeeping)
- Line 50: the `NODE_W = 280` comment ("non-child 280 / child 260") is now
  partially stale. Bands only wrap children (260). Reconcile the comment (or split
  into child/top-level constants) so a future maintainer isn't misled. No geometry
  change — bands are unaffected.

## Invariants that MUST stay green

- **YAML export parity** — the bridge writes only `position x/y` (never width);
  `ExportToYaml` ignores geometry. Confirmed: `SSH_Helper.Tests` export-parity
  tests construct synthetic positions and assert YAML only.
- **Connection guards** — `connectionRules.ts` / `flow-canvas-connection-guards`
  key on handle ids, untouched here.
- **Markers / gradient / packet / selection / badges / reduced-motion** — no code
  path changed; only the `d` string of spine edges changes shape.

## Risks & mitigations

| Risk | Sev | Mitigation |
|---|---|---|
| Start→first edge still hooks if only blocks are unified | med | Start width set to 280 too (decision above). |
| `maxWidth` alone leaves variable centers | med | Set non-child `min === max === 280` (a real fixed width), not just `maxWidth`. |
| A corridor edge accidentally straightened | high | Geometry rule only straightens X-aligned, downward edges; corridor edges are inherently X-offset → always smoothstep. No metadata reliance. |
| Long labels clip at 280 | low | Width = current `maxWidth`, so nothing clips more than today; ellipsis already present. |
| Stale `NODE_W` comment misleads later | low | Reconcile comment in change #4. |

## Testing & verification

1. `cd FlowCanvas && npm run build` — TypeScript + Vite build must pass (type-check gate).
2. Run the Playwright e2e specs that touch edges/nodes and confirm green:
   `flow-canvas-live-wires` (marker/gradient/packet), `flow-canvas-connection-guards`,
   `flow-canvas-branch-bands`, `flow-canvas-node-redesign`, `flow-canvas-token-sweep`,
   `flow-canvas-execution-cinematics`, `flow-canvas-reduced-motion`.
3. `dotnet build SSH_Helper.sln` and `dotnet test SSH_Helper.Tests/...` — export-parity
   and bridge tests stay green (presentation-only change).
4. **New regression guard:** add an e2e assertion that a plain successor edge's path
   `d` is a straight `M..L` (no curve/elbow commands) while a branch/`continue` edge's
   `d` contains smoothstep elbows. (No geometry guard exists today.)
5. Manual: import the screenshot's script, confirm the spine is dead-straight
   top-to-bottom including Start→first; run it and confirm the packet glides
   straight; confirm IF/foreach/switch corridors still bend around children.

## Out of scope / possible follow-ups

- Adaptive curvature for *offset* edges (currently they stay smoothstep) — not needed
  for this fix.
- Tuning `NodeSpacingY` for more breathing room — independent, not required once the
  geometry is straight.
