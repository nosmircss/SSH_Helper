# Drag a band by its label to move the whole band

**Date:** 2026-06-03
**Component:** Flow Canvas (`FlowCanvas/src`)
**Status:** Approved (brainstorm) — pending spec review

## Goal

Let the user grab a branch band's label pill ("THEN" / "ELSE" / "LOOP" / etc.)
and drag it to move the entire band — every block inside it — as one unit.

## Background

Branch bands are **derived geometry**, not real nodes. `computeBranchBands()`
(`utils/branchBands.ts`) groups nodes by their `_isChildOf` parent + branch key,
then boxes every node whose `_stepPath` falls under the branch's subtree prefix.
`BranchBandsLayer.tsx` renders each band in a `ViewportPortal` at `zIndex: -1`
with `pointerEvents: 'none'`; the label is a small pill in the band's top-left
corner. Bands write nothing to node data — they are a pure visual overlay that
re-derives whenever node positions change.

Because the band is derived from member positions, "moving a band" simply means
**translating every member node by the same delta**; the band (and any nested
bands) redraw themselves automatically. No new band state is needed.

Auto-layout is an explicit user action (`useAutoLayout`, a Toolbar button), so
manual positions persist exactly like individual node drags do today.

## Decisions (from brainstorm)

1. **Grab affordance:** the corner label pill is the handle (option A). The band
   background stays non-interactive.
2. **Grip dots (⠿):** shown **on hover only**, with `cursor: grab` →
   `grabbing` while dragging.
3. **What moves:** the **whole branch subtree** — every block visually inside the
   band, including nested-branch bodies. The container block, the sibling band
   (e.g. ELSE), and any blocks after the if/loop stay put.
4. **Free-form:** the band moves anywhere; the wire from the container re-routes
   automatically; overlaps are allowed. No snapping.
5. **Undo + autosave:** one `"Move band"` undo snapshot at grab; `sendLayoutAutosave()`
   on release — identical to a normal node drag.
6. **Persistence:** the move sticks like any manual drag; the explicit Auto-layout
   button (or re-importing YAML) reflows it.
7. **No YAML / membership change:** purely visual. `_isChildOf` / `_stepPath` /
   exported YAML are untouched.
8. **Label spacing fix:** add a top-only `BAND_LABEL_HEADROOM` (~12px) so the pill
   clears the first block (today the ~17px pill sits ~1px from it). Do **not** bump
   the shared `BAND_PAD = 18` — that constant feeds the layout engine's horizontal
   spacing; a separate top constant keeps this visual-only.

## Approach

**Pointer handlers on the pill, translating member nodes directly.** Rejected
alternatives: turning bands into real React Flow parent/group nodes (rips up the
"derived, writes-no-YAML" model — huge blast radius); hijacking React Flow's
multi-select drag (clobbers the user's selection and the Properties panel).

### Stacking: two-layer render

The band's `zIndex: -1` creates a stacking context that would trap a nested pill
below the React Flow pane, so a nested pill can't reliably receive a left-button
pointer-down. Therefore `BranchBandsLayer` renders **two layers** from the same
`computeBranchBands()` result:

- **Band rectangles** — unchanged: `zIndex: -1`, `pointerEvents: 'none'`.
- **Pill handles** — a second `ViewportPortal` layer above the pane. Each pill is
  positioned at its band's `(x, y)`, with `pointerEvents: 'auto'` and the drag
  handlers. Pills sit in the empty headroom strip above the first block, so a
  positive zIndex does not cover blocks.

### Drag mechanics

On the pill:

- **`onPointerDown`** (left button only): `e.stopPropagation()`,
  `setPointerCapture`, push `"Move band"` undo snapshot once, record the band's
  `memberIds` and the pointer's start position in flow coords
  (`screenToFlowPosition`).
- **`onPointerMove`** (while captured): convert the current pointer to flow coords,
  diff against the recorded start to get a zoom-correct `(dx, dy)`, and translate
  all `memberIds` by that delta via a single batched store action. Track the last
  applied delta so each move applies the *incremental* shift (or recompute absolute
  positions from a snapshot of start positions — implementer's choice, but it must
  be a single batched update per move).
- **`onPointerUp` / `onLostPointerCapture`**: release capture, `sendLayoutAutosave()`,
  reset the drag state.

Using `screenToFlowPosition` for both endpoints makes the delta zoom-correct with
no manual math. `sendLayoutAutosave` is a plain importable function
(`utils/layoutAutosave`), and `BranchBandsLayer` is inside the React Flow provider
(via `ViewportPortal`), so `useReactFlow()` is available.

## Components / changes

- **`utils/nodeSize.ts`** — add `export const BAND_LABEL_HEADROOM = 12` next to
  `BAND_PAD`, documented as render-only (not consumed by the layout engine).
- **`utils/branchBands.ts`**
  - Add `memberIds: string[]` to the `BranchBand` interface, populated from the
    `boxNodes` set already computed per group.
  - Extend the band's top by `BAND_LABEL_HEADROOM`:
    `y = minY - BAND_PAD - BAND_LABEL_HEADROOM`,
    `height = (maxY - minY) + BAND_PAD * 2 + BAND_LABEL_HEADROOM`.
    (Left inset / x / width unchanged.)
- **`stores/slices/graphSlice.ts`** — add `translateNodesBy(ids: string[], dx, dy)`:
  one `set` that adds `(dx, dy)` to `position` for nodes whose id is in the set,
  mirroring node-drag side effects (`clearedExportStatusState()`). The START node
  is never a band member, so no special-casing is needed.
- **`nodes/BranchBandsLayer.tsx`** — split into the two layers above; add the pill
  drag handlers; hover-only `⠿` grip; `cursor: grab`/`grabbing`. Drag logic may be
  extracted into a small `useBandDrag` hook to keep the component readable.

## Edge cases

- **Nested bands.** Dragging an outer band moves the inner band's nodes too (they
  are under the outer prefix), so the inner band rides along. Dragging the inner
  band moves only its own subtree; the outer band re-derives to keep wrapping its
  contents (the moved nodes are still its members). Both are correct.
- **Switch `cases`.** All cases share one lane/prefix, so dragging the band moves
  every case together — consistent with the single derived band.
- **Container → first-child gap.** The band already extends `BAND_PAD` (18px) up
  into that gap without colliding; +12px headroom must still clear the container.
  Verify visually/e2e; the typical row gap is comfortably larger.
- **Pane interaction.** `panOnDrag` is `[1, 2]` (middle/right buttons), so a
  left-drag never pans; combined with `stopPropagation` + pointer capture on the
  pill, the drag won't trigger marquee-select or panning.

## Testing

- **vitest (`utils/__tests__/branchBands.test.ts`)** — update existing geometry
  expectations for the new top extension; assert `memberIds` contains exactly the
  boxed subtree (incl. nested-branch members) and excludes non-members.
- **vitest (graphSlice)** — `translateNodesBy` shifts only the targeted ids by the
  given delta and leaves others untouched.
- **Playwright e2e** — import a script with an if/then(+nested) band, drag the THEN
  pill by a known delta, assert every member block moved by that delta (in
  zoom-independent flow coords per the e2e gotchas) while the container, the ELSE
  band, and post-if blocks did not. Assert one undo reverts the whole move.

## Out of scope

- Dragging the spine/container itself (already draggable as a node).
- Snapping, sibling push-apart, or constraining a band to its parent.
- Any change to YAML import/export or membership metadata.
