# Configurable block sizing + canvas settings menu

**Date:** 2026-06-03
**Component:** Flow Canvas (`FlowCanvas/src`) + C# host (`UI/FlowCanvasForm.cs`, `Models/AppConfiguration.cs`)
**Status:** Approved (brainstorm) — pending spec review

## Goal

Let the user control how blocks are sized and how the canvas reads, instead of
the current fixed constants. Driven by the real pain in the screenshot: every
`SET` block clips its expression (`interface = json.get(current_host.interface_select_method)`)
because block width is hard-coded at 330px. The user wants wider blocks — and a
single place to control width, text size, and other view preferences.

Deliver a **toolbar gear → floating "Display Settings" popover** housing:

- **Block width** (global) — 5 presets: Compact 300 / Normal 330 / Wide 440 / Extra 560 / Max 700
- **Text size** — S / M / L (0.9 / 1.0 / 1.15 scale)
- **Canvas density** — Tight / Normal / Roomy (vertical-spacing multiplier)
- **New blocks default** — Collapsed / Expanded
- **Consolidated view toggles** (moved off the toolbar) — Snap to grid, Branch bands, Heatmap, Reduced motion
- **Reset to defaults**

All settings persist across sessions. **YAML/preset export is never touched** —
these are pure view state, consistent with the canvas-as-preset-builder rule.

## Background

### Width is defined in two places (the source-of-truth problem)

1. **`utils/nodeSize.ts:7-8`** — `SPINE_WIDTH = 330`, `CHILD_WIDTH = 300`; the
   helper `nodeWidth(props)` (`:27-29`) returns `CHILD_WIDTH` for nodes carrying
   `_isChildOf`, else `SPINE_WIDTH`. Consumed by:
   - `nodes/BaseBlock.tsx:162-163` — `minWidth`/`maxWidth` inline style.
   - `nodes/StartNode.tsx:54-55` — `minWidth`/`maxWidth` (`SPINE_WIDTH` only).
   - `utils/branchBands.ts:88` — `nodeBox()` **hard-codes `CHILD_WIDTH`** for band geometry.
2. **`utils/layout/hierarchicalLayout.ts:7-29`** — the `LAYOUT` object has its
   *own* width copy: `CHILD_NODE_MAX_WIDTH = 300`, `COLUMN_GAP = 30`, and a
   derived `BASE_COLUMN_WIDTH = 330` used for column placement. It does **not**
   import `nodeSize.ts`.

Two independent definitions of "how wide is a block" must be collapsed into one
before width can become a runtime value.

### Layout spacing constants (`utils/layout/hierarchicalLayout.ts:7-31`)

- `NODE_SPACING_Y = 106` (vertical pitch) → `VERTICAL_GAP = NODE_SPACING_Y - COLLAPSED_HEIGHT = 54`.
- `BRANCH_CHILD_OFFSET = 220` (indent branch children right of the spine).
- `LANE_GAP = 72`, `MAX_SPREAD_WIDTH = 1400`, `COLUMN_WIDTH_DECAY = 0.92`.

`computeHierarchicalLayout(nodes, edges)` (`:193-201`) takes **no** sizing param
today. Call sites: `stores/slices/debugSlice.ts:88` (`toggleExpanded`),
`:101` (`setAllExpanded`), `hooks/useAutoLayout.ts:14`, and
`stores/messageBridge.ts:138` (initial layout on load when no saved positions).

### Text tokens & height estimation

`styles/tokens.css:147-149` defines `--fc-fs-badge:10px`, `--fc-fs-body:12px`,
`--fc-fs-header:13px`. But `BaseBlock.tsx` **hard-codes** several font sizes that
bypass the tokens: badge `10` (`:203`), summary label `10.5` (`:350`), summary
value `11.5` (`:352`), footer `10` (`:359`), preview `12` (`:370`). Layout height
is estimated by `estimateNodeHeight()` (`nodeSize.ts:31-36`) from fixed constants
`COLLAPSED_HEIGHT=52`, `SUMMARY_PAD=14`, `SUMMARY_ROW_H=20`, `SUMMARY_FOOTER_H=24`
plus a `~30` header. If rendered text scales but these estimates don't, blocks
overlap.

### Default-expanded

New blocks are created in `App.tsx` `onDrop` (~`:198-207`) with **no** `expanded`
field (collapsed). On load, `messageBridge.ts:143-160` rebuilds `expandedNodes`
from each node's `data.expanded === true`.

### The reflow pattern (canonical — `debugSlice.ts` `setAllExpanded`)

Mutate state → write the carrier flag on the affected nodes in one batched map →
`setNodes(computeHierarchicalLayout(next, edges))` once → `sendLayoutAutosave()`.
No undo snapshot for view state.

### Persistence path (already exists for reducedMotion / heatmap / panel sizes)

- **Channels** (`communication-message-types.ts`): outgoing `prefSave='pref-save'`,
  `layoutSave='layout-save'`; incoming `layoutRestore='layout-restore'`,
  `prefRestore='pref-restore'`.
- **Save (React):** `uiSlice.ts` — `setReducedMotion` → `prefSave`;
  `toggleHeatmap` / `setPanelSize` → `layoutSave`.
- **Restore (React):** `messageBridge.ts:382-400` handlers call
  `restorePanelSizes` / `restoreHeatmapEnabled` / `restoreReducedMotion`.
- **C# host (`UI/FlowCanvasForm.cs`):** `OnWebMessageReceived` (`:199-285`)
  dispatches `layout-save` → `SavePanelSizes` (`:374-392`), `pref-save` →
  `SaveReducedMotionPref` (`:394-404`); on React `ready` (`:209-214`) it calls
  `SendPersistedLayout` (`:356-372`) which posts `layout-restore` + `pref-restore`.
- **Config model:** `Models/AppConfiguration.cs` `WindowState` (`:478-511`) holds
  `FlowCanvasRightPanelWidth`, `FlowCanvasOutputHeight`, `FlowCanvasReducedMotion`,
  `FlowCanvasHeatmapEnabled` (nullable, Newtonsoft default naming).

> Note: `snapToGrid`, `gridSize`, and `branchBandsEnabled` are currently
> **transient** (`uiSlice.ts` toggles with no persist). Consolidating them into
> the menu makes `snapToGrid` and `branchBandsEnabled` persist too.

## Decisions (from brainstorm)

1. **Width scope: global, not per-block.** One width applies to all blocks. Per-block
   override is explicitly deferred (YAGNI) — it would force the layout/bands to
   handle mixed widths and add per-node persisted state.
2. **Five named-preset widths:** Compact 300 / Normal 330 / Wide 440 / Extra 560 /
   Max 700. **Normal (330) is the default** (today's value). Control is a
   **segmented preset selector**, not a slider (user choice).
3. **Surface: toolbar gear → floating popover** (chose option A over a docked
   right-panel tab and a modal). Preserves the tweak-and-see-live loop.
4. **Menu contents:** block width, text size, canvas density, default block state,
   plus the four consolidated view toggles, plus a Reset link.
5. **Consolidation split:** *view toggles* (snap, branch bands, heatmap, reduced
   motion) move **into** the popover and are **removed** from the toolbar; *action*
   buttons (Expand All, Layout, Find) **stay** on the toolbar.
6. **Persistence:** all settings persist via the existing `layout-save` /
   `layout-restore` path into `WindowState` (no new message channel).
7. **Export untouched:** width/text/density are view state; YAML import/export and
   the graph-export path are not modified.

## Approach

### One sizing source of truth

Introduce a single sizing descriptor derived from the settings:

```
sizing = {
  spineWidth,            // = preset width (300|330|440|560|700)
  childWidth,            // = spineWidth - COLUMN_GAP (30) — preserves the current inset
  vGap,                  // = base NODE_SPACING_Y * densityFactor
  textScale,             // 0.9 | 1.0 | 1.15
}
```

- `nodeSize.ts` becomes the **only** width definition. `SPINE_WIDTH`/`CHILD_WIDTH`
  stay exported as the **defaults** (back-compat for existing tests), but the live
  values come from the settings.
- `computeHierarchicalLayout(nodes, edges, sizing?)` and
  `computeBranchBands(nodes, sizing?)` gain an **optional** `sizing` param that
  **defaults to today's constants** — so every existing test and untouched call
  site keeps passing unchanged. `branchBands.nodeBox` uses `sizing.childWidth`
  instead of the hard-coded `CHILD_WIDTH`.
- `LAYOUT.BASE_COLUMN_WIDTH` / `MIN_COLUMN_WIDTH` derive from `sizing.spineWidth`
  (= `childWidth + COLUMN_GAP`), eliminating the duplicate width.
- `BaseBlock`/`StartNode` read the live width from a store selector (re-renders on
  change; no reflow needed for render, only for layout).

**Reflow on change:** width/density/text-size setters follow the `setAllExpanded`
pattern — recompute `computeHierarchicalLayout(nodes, edges, sizing)` once and
persist.

**Sharp edge — branch geometry at wide presets.** `BRANCH_CHILD_OFFSET = 220` and
the lane spread are tuned for 300-wide children; at Wide/Extra/Max a branch child
would overlap the spine. Branch indentation and lane width must scale with
`sizing.childWidth` (e.g. offset grows with width). This is the riskiest change
and gets a dedicated plan step verified against `hierarchicalLayout.test.ts` plus
a manual visual check at Max.

### Text size

- Add `--fc-fs-scale` (default `1`) to `tokens.css` and rewrite the font tokens as
  `--fc-fs-header: calc(13px * var(--fc-fs-scale))` (and body/badge). The settings
  action sets `--fc-fs-scale` on the canvas root.
- **Cleanup:** replace the hard-coded `fontSize` numbers in `BaseBlock.tsx`
  (`10`,`10.5`,`11.5`,`12`) with the tokens so the scale actually applies
  everywhere (deletes the divergence; one-source-of-truth for type).
- `estimateNodeHeight(blockType, props, expanded, textScale=1)` multiplies its
  header/row/footer constants by `textScale` so layout heights track rendered text.

### Canvas density

`density ∈ {tight:0.85, normal:1.0, roomy:1.2}` scales `NODE_SPACING_Y` (→ `vGap`).
Fed through the same `sizing` param to the layout. Horizontal spacing unchanged in
v1.

### Default block state

Add `defaultBlockExpanded: boolean` to settings. In `App.tsx` `onDrop`, when true,
create the node with `data.expanded = true` and add its id to `expandedNodes`
(reusing the existing carrier-flag mechanism).

### Settings state

A dedicated `stores/slices/settingsSlice.ts` (keeps `uiSlice` focused). Holds the
4 sizing/behavior settings + the migrated `snapToGrid` / `branchBandsEnabled` /
`heatmapEnabled` / `reducedMotion` (moved or aliased from `uiSlice` to avoid
duplicate state — **one source of truth**). Each setter:

- updates state,
- for geometry-affecting settings (width/density/text), writes any needed carrier
  data and reflows once,
- sends a `layout-save` message with the changed field(s),
- no undo snapshot.

`resetToDefaults()` sets every field back to its default and reflows once.

### Persistence (extend, don't invent)

- **`WindowState` (`AppConfiguration.cs`)** — add nullable fields:
  `FlowCanvasBlockWidthPreset` (int, the px value — e.g. 440 — so the stored
  setting survives any future change to the preset table),
  `FlowCanvasTextScale` (double), `FlowCanvasDensity` (string),
  `FlowCanvasDefaultExpanded` (bool), `FlowCanvasSnapToGrid` (bool),
  `FlowCanvasBranchBands` (bool). (`HeatmapEnabled`/`ReducedMotion` already exist.)
- **`SavePanelSizes` (`FlowCanvasForm.cs`)** — extract the new fields from the
  `layout-save` payload and write them to `WindowState` (same `_configService.Update`
  block). (Rename remains `SavePanelSizes` or generalize to `SaveCanvasState`.)
- **`SendPersistedLayout` (`FlowCanvasForm.cs`)** — include the new fields in the
  outgoing `layout-restore` message.
- **`messageBridge.ts` `layout-restore` handler** — call the new `restore*` actions
  for each field.
- **Ordering constraint:** persisted sizing must be applied to the store **before**
  the initial `computeHierarchicalLayout` in `messageBridge.ts:138`, so a fresh
  open lays out blocks at the saved width/density. The restore handler that sets
  sizing must run (or the initial layout must read sizing) ahead of that compute.

### The gear popover UI

`panels/SettingsPopover.tsx` (new) — anchored to a new gear button in
`Toolbar.tsx`. Segmented controls for width/text/density/default-state, toggle
rows for the view toggles, a Reset link. Styled with existing tokens (matches the
mockup approved in brainstorm). Close on outside-click / Esc (reuse the
context-menu dismiss pattern if present).

## Components / changes

**React (`FlowCanvas/src`):**

- `utils/nodeSize.ts` — single width source; `estimateNodeHeight` takes `textScale`.
- `utils/layout/hierarchicalLayout.ts` — `computeHierarchicalLayout(nodes, edges, sizing?)`;
  derive widths/spacing/branch-offset from `sizing`; keep defaults.
- `utils/branchBands.ts` — `computeBranchBands(nodes, sizing?)`; `nodeBox` uses `sizing.childWidth`.
- `nodes/BaseBlock.tsx` — width from store selector; replace hard-coded font sizes with tokens.
- `nodes/StartNode.tsx` — width from store selector.
- `styles/tokens.css` — `--fc-fs-scale`; font tokens as `calc()`.
- `stores/slices/settingsSlice.ts` (new) — state + setters + `restore*` + `resetToDefaults`.
- `stores/useFlowStore.ts` — register the slice.
- `stores/slices/uiSlice.ts` — remove/alias the migrated toggles (no duplicate state).
- `stores/slices/debugSlice.ts`, `hooks/useAutoLayout.ts` — pass `sizing` into layout calls.
- `stores/messageBridge.ts` — apply persisted settings before initial layout; `layout-restore` handler extended.
- `panels/Toolbar.tsx` — add gear button; remove the migrated toggle buttons.
- `panels/SettingsPopover.tsx` (new) — the popover UI.
- `App.tsx` — `onDrop` honors `defaultBlockExpanded`.
- `communication-message-types.ts` — extend the `layout-save`/`layout-restore` payload typings (no new channel).

**C# host:**

- `Models/AppConfiguration.cs` — new `WindowState` fields.
- `UI/FlowCanvasForm.cs` — extend `SavePanelSizes` + `SendPersistedLayout`.

## Edge cases

- **Empty canvas.** Gear/popover still opens; sizing settings apply to future blocks.
- **Width change with branches/nesting.** Must not overlap at Max — covered by the
  branch-offset scaling step + layout tests + visual check.
- **Old config (no new fields).** Nullable fields → fall back to defaults
  (Normal/M/Normal/Collapsed); first save writes them.
- **Restore ordering.** If sizing arrives after the initial layout, blocks would
  render at default width then jump. The restore-before-layout constraint prevents
  the flash.
- **Text scale vs height estimate drift.** `estimateNodeHeight` must use the same
  `textScale`; verified by a unit test asserting height grows with scale.
- **Migrated toggles keep working.** Snap/bands/heatmap/motion behave identically;
  they just live in the popover now and (snap/bands newly) persist.

## Testing

vitest (real `useFlowStore`, mocked `layoutAutosave` + `MessageBus`, mirroring
`debugSlice.expanded.test.ts`):

- **settingsSlice:** each sizing setter updates state, reflows (downstream block
  `position.y`/`x` shifts as expected), and calls `sendLayoutAutosave`/`layout-save`;
  `resetToDefaults` restores every field and reflows once.
- **width → layout:** `computeHierarchicalLayout(nodes, edges, sizing)` with a wide
  `sizing` widens column placement and **no nodes overlap** (incl. a nested-branch
  fixture at Max); default param reproduces today's positions (regression guard).
- **branchBands:** `computeBranchBands(nodes, sizing)` band rects track `childWidth`.
- **estimateNodeHeight:** height strictly increases with `textScale`.
- **default-expanded:** `onDrop` with the pref on creates an expanded node in
  `expandedNodes` with `data.expanded === true`.
- **restore:** a `layout-restore` payload with the new fields drives the matching
  `restore*` actions.

Manual: open the popover, cycle each width preset (confirm the screenshot's
expression stops clipping at Wide+), text S/M/L, density, toggle migration, Reset;
reopen the canvas and confirm settings persisted.

## Phasing (≤5 files each)

1. **Sizing single-source + param threading** — `nodeSize.ts`,
   `hierarchicalLayout.ts`, `branchBands.ts`, `BaseBlock.tsx`/`StartNode.tsx`
   (read defaults), update layout call sites; all existing tests stay green.
2. **Settings slice** — `settingsSlice.ts`, `useFlowStore.ts`, `uiSlice.ts`
   (de-dupe toggles), wire reflow; unit tests.
3. **Gear popover UI + toolbar consolidation** — `SettingsPopover.tsx`,
   `Toolbar.tsx`.
4. **Text scale + density + default-expanded** — `tokens.css`, `BaseBlock.tsx`
   (token cleanup), `estimateNodeHeight`, `App.tsx`; branch-offset scaling +
   tests.
5. **C# persistence + restore wiring** — `AppConfiguration.cs`, `FlowCanvasForm.cs`,
   `messageBridge.ts`, `communication-message-types.ts`; restore-before-layout.

## Out of scope (YAGNI)

- Per-block width override.
- Per-element independent font controls (single scale only).
- Horizontal-density control (vertical only in v1).
- Any change to YAML/preset import-export or the graph-export path.
- A keyboard shortcut for the settings menu.
