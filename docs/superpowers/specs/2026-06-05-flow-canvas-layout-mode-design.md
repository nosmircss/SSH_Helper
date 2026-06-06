# Flow Canvas — Per-Preset Layout Mode

- **Date:** 2026-06-05
- **Status:** Approved (design); ready for implementation planning
- **Branch:** `flow-canvas-comment-flow`

## Problem

A user hand-arranges blocks on the Flow Canvas and saves the layout ("Layout
saved" toast). When they later reopen the canvas, their arrangement is gone —
the canvas re-runs auto-layout over it. There are actually two distinct ways
the arrangement is lost today:

1. **On reopen.** `Form1.LoadCurrentScriptIntoCanvas()` computes a structure
   hash; if the script's structure changed at all (a block added, removed, or
   reordered), the saved `CanvasLayout` is discarded *wholesale* and React runs
   `computeHierarchicalLayout`. There is no partial merge.
2. **On the next edit.** With "Auto-layout on edits" ON, `reflowLayout()` runs
   `computeHierarchicalLayout` and re-tidies even the blocks the user moved by
   hand.

The existing global `autoReflowEnabled` ("Auto-layout on edits") toggle only
addresses case 2, and even then incompletely — the reopen path (case 1) is not
gated by it. The behavior is invisible and surprising, which is the core
complaint.

## Solution Summary

Introduce a **per-preset Layout Mode** with two values, surfaced as a visible
toggle in the canvas toolbar. A single `LayoutMode` flag per preset becomes the
one source of truth for all layout behavior, closing both loss paths.

| When you… | Auto-flow mode | Manual mode |
|---|---|---|
| Hand-drag a block | Kept for now; next reflow re-tidies it (transient) | Position is saved and kept |
| Edit script / trigger reflow | Canvas re-lays-out hierarchically | No auto-reflow — blocks don't move |
| Reopen the canvas | Re-lays-out from scratch | Restores the exact arrangement |
| Add a step in the editor | Appears in its hierarchical slot | Drops near its neighbor, nudged off overlaps, briefly highlighted |
| Click Auto-Layout | Re-tidies everything | One-shot re-tidy (undoable); stays in Manual |
| Switch *into* Manual | — | Freezes whatever is on screen now as the saved layout |

## Locked Decisions

1. **Approach:** a Layout MODE toggle (Auto-flow | Manual), not two separately
   stored arrangements and not an invisible "sticky manual."
2. **Scope:** `LayoutMode` is **per preset**. Switching one preset to Manual
   affects only that preset.
3. **Global default:** a `DefaultLayoutMode` setting (Auto-flow | Manual),
   **default Auto-flow**, decides the mode for new / never-set presets.
4. **Drag in Auto-flow:** no auto-switch. The drag is kept until the next reflow
   re-tidies it. Switching to Manual is an explicit toggle action.
5. **New blocks in Manual:** placed **near their neighbor**, nudged off
   overlaps, with a brief "new" highlight. Existing blocks never move.
6. **Consolidation:** the new mode **replaces** the global
   `autoReflowEnabled` / "Auto-layout on edits" toggle. The old persisted value
   migrates into `DefaultLayoutMode`.

## Data Model

- **Per-preset.** Add `LayoutMode` (enum `AutoFlow | Manual`, nullable) to
  `PresetInfo` itself — a sibling of the existing `CanvasLayout` (which is
  `CanvasLayoutData?`, null until positions are saved). Keeping the mode on the
  preset rather than nested inside `CanvasLayoutData` lets a preset be Manual
  with no saved positions yet (e.g. set to Manual but never arranged) without
  materializing an empty layout record. `null` ⇒ inherit the global default.
  Switching *into* Manual is what first populates `CanvasLayout.Positions` (the
  freeze).
- **Global.** Add `DefaultLayoutMode` (enum, default `AutoFlow`) to
  `AppConfiguration` (Flow Canvas settings group). **Replaces**
  `WindowState.FlowCanvasAutoReflow`.

### Migration

On config load, map the old `FlowCanvasAutoReflow` → `DefaultLayoutMode`
(`true → AutoFlow`, `false → Manual`), then retire the old field (write-back,
matching the existing migration pattern in `ConfigurationService`). Per-preset
modes start unset, so every existing preset inherits the migrated default — no
visible behavior change on first launch. Existing tests that reference the old
field (`FlowCanvas/src/stores/slices/__tests__/autoReflow.test.ts`, the
`SaveAndLoad_FlowCanvasAutoReflow_RoundTrips` config test) are migrated to the
new mode as part of the consolidation.

## Behavior Architecture

### React (`FlowCanvas/src/`)

- **`stores/slices/uiSlice.ts`** — replace `autoReflowEnabled` /
  `toggleAutoReflow()` with `layoutMode` (the *active preset's* mode) and
  `setLayoutMode()`. The setter posts a `set-layout-mode` message to C# and
  updates local state (same echo pattern the old toggle used).
- **`stores/reflow.ts` `reflowLayout()`** — gate on `layoutMode === 'auto'`
  instead of `autoReflowEnabled`. In Manual it does what `autoReflow:false` did
  (re-anchor comments only via `placeAnchoredComments`, freeze blocks), now
  consistent with the reopen path. Auto-flow keeps `computeHierarchicalLayout`
  with `keepOrphans: true`.
- **`messageBridge.ts` `load-graph` handler** — replace the binary
  `hasUserLayout ? keep : reflow` with mode-keyed logic:
  - **Auto-flow:** `computeHierarchicalLayout(...)` (unchanged).
  - **Manual:** keep every node that arrived with a saved position; collect the
    nodes that arrived *without* one and run the near-neighbor placement pass on
    just those, then re-anchor comments.
- **`utils/layout/hierarchicalLayout.ts`** — new
  `placeNewBlocksNearNeighbors(nodes, edges)` (see algorithm below).
- **`panels/Toolbar.tsx`** — add the segmented `Auto-flow | Manual` toggle next
  to the existing Auto-Layout button. `hooks/useAutoLayout.ts` is unchanged: a
  one-shot re-tidy that works in either mode and does **not** change the mode
  (it already pushes an undo snapshot).
- **`panels/SettingsPopover.tsx`** — swap the "Auto-layout on edits" checkbox
  for the "Default layout mode for new presets" dropdown.

### C#

- **`Form1.cs` `LoadCurrentScriptIntoCanvas()`** — resolve the active preset's
  effective mode (preset `LayoutMode` ?? `DefaultLayoutMode`) and send it to
  React on `load-graph`. For Manual presets, do a **partial merge**: apply saved
  positions to every node whose id/step-path still matches the saved layout —
  *even when the overall `StructureHash` differs* — leaving genuinely-new nodes
  unpositioned for React to place near-neighbor. For Auto-flow presets the path
  is unchanged.
- **`FlowCanvasBridge.cs`** — add `MergeLayoutByNodeId()` (partial, id-keyed)
  alongside the existing all-or-nothing `MergeLayout()`. `ComputeStructureHash`
  stays, but in Manual mode it only answers "are there new blocks?", never
  "discard the whole layout."
- **`FlowCanvasForm.cs`** — extend `LoadGraph(...)` to carry `layoutMode`; add a
  `set-layout-mode` inbound message handler that raises an `OnSetLayoutMode`
  event. `Form1` handles it by persisting the mode onto the active preset via
  `_presetManager` (atomic write, same as `OnLayoutAutosave`).

## The One New Algorithm: near-neighbor placement

`placeNewBlocksNearNeighbors(nodes, edges)`:

1. Partition nodes into **placed** (arrived with a saved position) and **new**
   (no saved position).
2. For each new node, find its anchor: the preceding sibling in its branch, or
   its parent container if it is first in the branch (derived from the spine
   ordering already used by the hierarchical layout).
3. Place the new node at `anchor.position + offset` (down for a following step,
   indented for a first child).
4. Run a light overlap-avoidance nudge against all already-placed nodes (and
   previously-placed new nodes) so it never lands on top of an existing block.
5. Tag the new nodes so the UI shows a brief "new" highlight on first paint.

Degenerate case (all nodes new, e.g. a Manual-by-default preset never arranged):
the pass runs over everything and degrades gracefully to a roughly-hierarchical
arrangement.

## Edge Cases / Error Handling

- **Auto-flow → Manual** captures current on-screen positions as the saved
  layout (freeze on switch).
- **Manual → Auto-flow** reflows immediately. The arrangement remains in
  storage, so flipping back restores it — a free side-benefit, not a second
  saved "view" to keep in sync.
- **Mode without a layout** (preset set Manual but never arranged) persists the
  mode regardless of whether any positions exist.
- **Comments / orphans** keep their current anchor-aware placement; Manual means
  the *blocks* they anchor to don't move.

## Testing

- **C# (xUnit):** partial merge keeps matched positions across a structure
  change; migration maps `FlowCanvasAutoReflow` both directions; `set-layout-mode`
  round-trips to preset storage; effective-mode resolution (preset ?? default).
- **React (vitest):** `reflowLayout` freezes blocks in Manual and reflows in
  Auto; `load-graph` keeps saved positions and places only new nodes in Manual;
  toggle dispatches `set-layout-mode`; `placeNewBlocksNearNeighbors` overlap
  cases.
- **e2e (Playwright):** arrange → toggle Manual → trigger reflow → positions
  hold; add a node in the editor → reopen → existing positions hold and the new
  node is placed near its neighbor.

## Non-Goals

- Two separately stored arrangements per preset (Approach B) — rejected as
  over-engineered (two position sets to sync, ambiguous edit-propagation rules).
- Auto-switching to Manual on first drag — rejected; switching is explicit.
- A parking-lane staging area for new blocks — rejected in favor of
  near-neighbor placement.

## Affected Files (for planning)

React: `stores/slices/uiSlice.ts`, `stores/reflow.ts`, `stores/messageBridge.ts`,
`utils/layout/hierarchicalLayout.ts`, `panels/Toolbar.tsx`,
`panels/SettingsPopover.tsx` (+ vitest specs).
C#: `Form1.cs`, `UI/FlowCanvasForm.cs`, `Services/FlowCanvasBridge.cs`,
`Models/PresetInfo.cs` (+ `LayoutMode`), `Models/AppConfiguration.cs`
(+ `DefaultLayoutMode`, retire `WindowState.FlowCanvasAutoReflow`),
`Services/ConfigurationService.cs` (migration), `Services/PresetManager.cs`
(persist mode) (+ xUnit tests).
