# Expand / Collapse all blocks

**Date:** 2026-06-03
**Component:** Flow Canvas (`FlowCanvas/src`)
**Status:** Approved (brainstorm) — pending spec review

## Goal

Give the user a one-action way to expand or collapse every block on the canvas
at once, instead of clicking each block's chevron individually. Exposed on two
surfaces: a smart-toggle button in the top toolbar and two entries in the block
right-click context menu.

## Background

Each `type: 'block'` node (rendered by `nodes/BaseBlock.tsx`) has a per-block
expand chevron (`data-testid="expand-toggle"`) that calls
`toggleExpanded(id)`. The `StartNode` (`type: 'start'`) and `CommentNode`
(`type: 'comment'`) are separate components with **no** expand affordance.

Expansion state lives in `stores/slices/debugSlice.ts`:

- `expandedNodes: Set<string>` — the source of truth; `isExpanded(id)` reads it.
- `toggleExpanded(id)` — flips one id in the set, writes the `data.expanded`
  carrier flag via `updateNodeData(id, { expanded })`, then reflows the graph
  with `setNodes(computeHierarchicalLayout(nodes, edges))` and calls
  `sendLayoutAutosave()`.
- `restoreExpandedNodes(ids)` — replaces the whole set on load (host-driven).

`data.expanded` is a **carrier flag only** — it feeds layout/band height
estimates and never leaks to YAML. Expansion is **view state**: per-block
`toggleExpanded` pushes **no** undo snapshot, and the state persists through the
existing layout-autosave channel (restored by `restoreExpandedNodes`).

Because the state is a single set plus a carrier flag, "expand/collapse all" is
just "set the whole set + flag in one pass and reflow once" — no new state and no
C# change.

## Decisions (from brainstorm)

1. **Surfaces:** both — a toolbar toggle button **and** two context-menu entries.
   Both call one shared store action.
2. **Toolbar toggle rule — expand-first:** the button reads **`⊞ Expand All`**
   whenever *any* block is collapsed; it flips to **`⊟ Collapse All`** only when
   *every* block is already expanded. Clicking always moves toward the opposite
   extreme.
3. **Scope — every block on the canvas:** acts on all `type: 'block'` nodes,
   ignoring selection. `start`/`comment` nodes are untouched (no detail view).
4. **Context-menu entries are global:** "Expand All Blocks" / "Collapse All
   Blocks" act on the whole canvas regardless of which node was right-clicked, so
   they appear for any node (including the start node).
5. **No undo snapshot:** matches per-block `toggleExpanded` — expansion is view
   state, not graph structure.
6. **Persistence:** one `sendLayoutAutosave()` after the bulk change; reload is
   restored by the existing `restoreExpandedNodes`.
7. **One reflow:** the bulk change updates all nodes in a single batched pass and
   runs `computeHierarchicalLayout` exactly once (not once per block).

## Approach

**One batched store action driving both surfaces.** Rejected alternative: looping
`toggleExpanded` over every node — it would fire N store updates, N re-renders,
and N layout reflows. A single `setAllExpanded(expanded)` keeps it to one update
and one reflow.

### Store action — `setAllExpanded(expanded: boolean)`

In `debugSlice.ts` (added to the `DebugSlice` interface):

- Read `nodes` from the store; collect `blockIds = nodes.filter(type === 'block')`.
- `set({ expandedNodes: expanded ? new Set(blockIds) : new Set() })`.
- Build the next nodes array in one map: for each `type: 'block'` node, merge
  `{ data: { ...n.data, expanded } }`; leave other nodes as-is.
- `setNodes(computeHierarchicalLayout(nextNodes, edges))` — single reflow.
- `sendLayoutAutosave()`.
- No `pushSnapshot`.

### Toolbar button — `Toolbar.tsx`

Placed in the existing "Canvas controls" group (next to Layout / 🔍 / Snap):

- Subscribe to `nodes` and `expandedNodes`; derive
  `blockNodes = nodes.filter(type === 'block')`,
  `hasBlocks = blockNodes.length > 0`,
  `allExpanded = hasBlocks && blockNodes.every(n => expandedNodes.has(n.id))`.
- Label/icon (expand-first): `allExpanded ? '⊟ Collapse All' : '⊞ Expand All'`.
- `onClick = () => setAllExpanded(!allExpanded)`.
- `disabled = !hasBlocks`; reuse the existing `btnStyle` helper and a tooltip.

### Context-menu entries — `BlockContextMenu.tsx`

Add a separated group with two always-present items (wired to `setAllExpanded`
from the store), each calling `hideContextMenu()` after:

- `⊞ Expand All Blocks` → `setAllExpanded(true)`.
- `⊟ Collapse All Blocks` → `setAllExpanded(false)`.

These are independent of `nodeId`/`isStartNode` (global action).

## Components / changes

- **`stores/slices/debugSlice.ts`** — add `setAllExpanded: (expanded: boolean) => void`
  to the `DebugSlice` interface and implement it as above.
- **`panels/Toolbar.tsx`** — add the smart-toggle button + the derived
  `allExpanded` state.
- **`panels/BlockContextMenu.tsx`** — add the two global menu entries (and the
  `setAllExpanded` selector).

No C# changes; no YAML/import-export changes; no new message types.

## Edge cases

- **Empty canvas / start node only.** `hasBlocks` is false → toolbar button is
  disabled. (The context menu only appears on a node right-click.)
- **Mixed state (some expanded).** Expand-first rule → button shows `⊞ Expand All`;
  one click expands the rest. A second click (now all expanded) collapses all.
- **Blocks with no summary rows.** Still get the chevron today and are treated as
  expandable; expanding them is a no-op visually but keeps `allExpanded`
  consistent. Targeting all `type: 'block'` nodes matches the per-block chevron.
- **Re-render cost.** One batched `setNodes` + one reflow; identical cost profile
  to a single `toggleExpanded`, just over more nodes.

## Testing

Extend **`stores/slices/__tests__/debugSlice.expanded.test.ts`** (real
`useFlowStore`, mocked `layoutAutosave` + `MessageBus`, as in the existing file):

- `setAllExpanded(true)` puts every `type: 'block'` id in `expandedNodes`, sets
  `data.expanded === true` on those nodes, and leaves `start`/`comment` nodes out
  of the set and untouched.
- `setAllExpanded(false)` empties `expandedNodes` and sets `data.expanded === false`
  on block nodes.
- `setAllExpanded` persists via `sendLayoutAutosave` (`toHaveBeenCalled`).
- Reflow: with a `start → A(block) → B(block)` chain laid out collapsed,
  `setAllExpanded(true)` pushes B's `position.y` lower (mirrors the existing
  `toggleExpanded` reflow test).

(Toolbar/context-menu rendering is thin glue over the tested action; covered by
the store tests plus the existing component-test harness if a label-state test is
cheap to add.)

## Out of scope (YAGNI)

- Selection-aware scope (expand/collapse only selected blocks).
- Undo integration for expansion.
- A dedicated keyboard shortcut.
- Any C# / YAML / message-protocol change.
