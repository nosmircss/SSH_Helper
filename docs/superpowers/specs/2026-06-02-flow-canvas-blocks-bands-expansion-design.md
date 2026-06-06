# Flow Canvas — Bigger Blocks, Labeled Lanes & Expandable Settings

**Date:** 2026-06-02
**Status:** Approved (design), pending implementation plan
**Scope:** Three Flow Canvas presentation features + the layout change they require — bigger blocks, branch bands redesigned as labeled lanes, and in-place block expansion to a read-only settings summary. **No change to edges, YAML, or graph data.**

## Problem

The canvas is hard to read and follow at a glance, in three concrete ways:

1. **Blocks are small and fixed.** `BaseBlock.tsx:153-154` hardcodes `minWidth/maxWidth` 280 (non-child) and 160/260 (child); `StartNode.tsx:52-53` is 280. Labels truncate early and nested children are cramped.
2. **Branch bands carry almost no information.** `BranchBandsLayer.tsx:26-27` renders each band as an 8%-tint rectangle with a 3px colored left stripe — **no label, no nesting cue.** Nested THEN looks identical to outer THEN; color is the only signal. Geometry is computed from hardcoded estimates (`branchBands.ts:50-52` `NODE_W=280`, `NODE_H=64`, `PAD=10`), already 20px off (children are 260) and wrong the moment blocks resize.
3. **A block's settings are invisible on the canvas.** To see what a block is configured to do you must select it and read the Properties panel. There is no way to glance at a block's config in place.

## Chosen direction

Selected with the user through a visual brainstorming pass (faithful in-browser mockups against `tokens.css`):

- **Block size:** spine **330px**, children **300px**, with proportionally larger header/label/preview type and icon ("Comfortable" density).
- **Branch bands → labeled lanes:** a corner branch pill (THEN / ELSE / LOOP / CASE …) + soft full border + 3px left accent stripe + a light tint; **nested lanes get a brighter tint + lighter pill** for depth. **18px** padding; the lane hugs its content (bounding box + padding). Geometry derived from **real block dimensions**, not the hardcoded estimates.
- **In-place expansion:** a header **chevron** grows the block downward into a **read-only summary** of its settings (required fields + any non-default field; defaulted fields hidden behind a count). Editing stays in the Properties panel. **Children can expand too.** Expanded state **persists across reload.**
- **Layout becomes height-aware** so variable-height (expanded) blocks and taller lanes don't overlap their neighbors.

## Goals

- Blocks are legible and nested children no longer look starved; size is consistent (children ≈ spine).
- Branch lanes are self-labeling and depth reads instantly; lanes wrap their contents tightly at any block size.
- A block's meaningful configuration is readable in place, without opening the Properties panel; the panel remains the single editor.
- The auto-layout positions taller blocks/lanes without overlap.
- **Zero change to YAML export and zero change to edge rendering** — presentation only.

## Non-goals

- **No edge changes.** `AnimatedEdge.tsx`, the straight/smoothstep routing, colors, arrowheads, and the post-run cyan highlight are **untouched**. Edges re-route automatically (xyflow recomputes each path from handle positions) when blocks resize/move — that is the existing renderer doing its normal job, not a code change here.
- **No YAML / import-export / graph-data change.** `expanded` state lives in a UI slice and the layout-autosave payload, **never** in `node.data.props`. Verify byte-identical round-trip.
- **No inline editing.** Expansion is read-only; no duplication of the Properties `PropertyField` dispatcher, `ChoiceOptionsEditor`, or the Test-block panel.
- **No floating-panel mechanism** (the alternative considered and rejected) — expansion grows the block in place.
- **No new category hues / no raw hex** outside the token layer.

## Visual specification

### Block sizing
- Non-child block width **280 → 330**; child/nested width **160/260 → 300** (min and max set so children read ≈ spine width).
- Header/label/preview type and icon scale up to the "Comfortable" density (exact px in the plan; header padding ~`6px 9px`, label ~13–14px, icon chip ~20–22px, preview ~12px).
- StartNode width matches the spine (330) and keeps its green identity.

### Labeled lanes (branch bands)
- Each lane: soft full border `1px solid mix(branch, 38%)`, **3px left accent** `mix(branch, 70%)`, background tint `mix(branch, 7%)`; **nested lane** tint `mix(branch, 13%)`, border `mix(branch, 55%)`.
- **Corner pill** at top-left: branch label, `9px/800`, dark text on solid `var(--fc-branch-<key>)`, `border-radius: 9px 0 8px 0`. Nested pill uses `color-mix(branch, white 14-16%)`.
- Pill label derived from the branch key (the same key that drives color): `then`→THEN, `else`→ELSE, `do`→**LOOP** (loops set scopePath `do`, display label "loop" — see `FlowCanvasBridge.cs:737-747`), `case`→CASE n, `elif`→ELIF n, `catch`/`finally`/`default`/`parallel` likewise. Single source with `branchColorVar` in `branchBands.ts`.
- **Padding 18px** on all sides; the pill sits in the top padding and always clears the first child.
- The lane **hugs its content**: geometry = children bounding box + 18px, where the box is computed from **real per-node dimensions** (width + measured/estimated height), replacing `NODE_W=280 / NODE_H=64`.

### In-place expansion (read-only summary)
- A **chevron** in the block header toggles expanded/collapsed (present on both spine and child blocks).
- When expanded, the single preview line is **replaced** by a summary body (sub-surface `oklch(19% 0.025 275)`):
  - **Rows shown:** every **required** field (always) + every field whose value **differs from its `defaultValue`** (non-empty / non-default). Row = `label : value`; `code`-type values render monospace + category-tinted; secrets (password/token) masked.
  - **Required-but-empty:** shown as "*— not set*" so a missing required value is visible.
  - **Hidden:** fields left at default → a footer "*N fields at default*".
  - **Footer:** the count + an "**Edit in Properties**" affordance that selects the node and focuses the panel (read-only reinforcement).
- The shown/hidden decision reuses the registry `PropertyDef` (`defaultValue`, `required`, `group`) and the panel's conditional-required logic (`Properties.tsx isPropertyRequired`). Factor this into a small shared helper (e.g. `utils/blockSummary.ts`) so node and any future consumer agree.

### Layout (height-aware)
- The hierarchical layout currently spaces nodes by a fixed `NODE_SPACING_Y = 106` and never reads node size (`hierarchicalLayout.ts`). It must use **per-node height** (collapsed ≈ fixed; expanded = header + shown rows + footer) so a taller/expanded block pushes its successors down instead of overlapping them. Source of truth for height: xyflow's measured node height (`node.measured`) re-fed into a relayout, or a content-derived estimate — chosen in the plan.
- Column width follows the new child width: `CHILD_NODE_MAX_WIDTH 260 → 300`, so `BASE/MIN_COLUMN_WIDTH = 300 + COLUMN_GAP(30) = 330`. Keep the TS values (`hierarchicalLayout.ts:11-15`) and the C# mirror (`FlowCanvasBridge.cs:73-75`) in lockstep.

## State & persistence

- Add **`expandedNodes: Set<string>`** to a UI-oriented slice (`stores/slices/uiSlice.ts`) with `toggleExpanded(id)` and an `isExpanded(id)` selector — mirroring `debugSlice.ts:28-29`'s `breakpoints` / `disabledBlocks` + `toggleBreakpoint` / `toggleDisabled`. **Do not** put it in `debugSlice` (cosmetic ≠ execution) and **do not** write it into `node.data` (keeps it out of YAML export).
- **Persist across reload:** add the expanded set to the layout-autosave payload (`utils/layoutAutosave.ts`) and to `Models/CanvasLayoutData.cs` (alongside positions + comment dims). Because autosave today fires on disabled/position changes (not arbitrary state), `toggleExpanded` must call `sendLayoutAutosave()` explicitly. Confirm the `CanvasLayoutData` structure hash still validates when only the expanded set changes (it keys on structure, not size).
- This is **layout/visual** persistence, fully separate from the YAML script — it travels with the canvas layout, never the exported preset.

## Invariants respected

- **Visual never touches YAML:** export (`FlowCanvasBridge.ExportGraphToYaml`) reads only `node.data.props` + edge topology — never size, position, lane, or expand state. All new state lives in the UI slice / layout-autosave. Verify byte-identical round-trip.
- **No hex outside the token layer:** lanes/pills/tints all derive from `var(--fc-branch-*)` + `color-mix`/`mix()`.

## Files to touch

**React (`FlowCanvas/src/`)**
- `nodes/BaseBlock.tsx` — widths 280→330 / child→300; density bump; header chevron + `toggleExpanded`; render read-only summary when expanded (replace preview); wire `isExpanded`.
- `nodes/StartNode.tsx` — width 280→330.
- `nodes/BranchBandsLayer.tsx` — corner pill (label from branch key), soft border + left accent + nested tint; geometry from real dims.
- `utils/branchBands.ts` — replace `NODE_W=280/NODE_H=64`, `PAD 10→18`; derive box from measured/estimated heights; pill-label helper alongside `branchColorVar`.
- `utils/layout/hierarchicalLayout.ts` — `CHILD_NODE_MAX_WIDTH 260→300` + column width; **height-aware** vertical spacing (replace fixed `NODE_SPACING_Y` usage).
- `stores/slices/uiSlice.ts` — `expandedNodes` + `toggleExpanded` + `isExpanded`.
- `utils/layoutAutosave.ts` — serialize `expandedNodes`; explicit save on toggle.
- `utils/blockSummary.ts` *(new)* — compute shown rows (required + non-default) from `PropertyDef` + props.
- `styles/tokens.css` — lane padding + nested-tint/accent alphas if promoted to tokens (otherwise inline `mix()`); pill text color.

**C# (`SSH_Helper/`)**
- `Services/FlowCanvasBridge.cs` — `CHILD_NODE_MAX_WIDTH`/column/spacing mirror (`:73-75`, `:63`) updated in lockstep with TS; layout parity preserved.
- `Models/CanvasLayoutData.cs` — add persisted expanded-node id set; round-trip; **excluded** from YAML export.
- `UI/FlowCanvasForm.cs` — pass-through only if it mediates the layout-autosave payload.

## Verification

- **Build:** `cd FlowCanvas && npm run build`; `dotnet build SSH_Helper.sln`; `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`.
- **Type/lint:** Vite build runs `tsc`/eslint; `npm test` (vitest) for components.
- **YAML round-trip:** import a sample preset, expand blocks, resize, export → **byte-identical** YAML (no graph/data fields touched).
- **Layout no-overlap:** expand a spine block and a nested child on a dense FortiGate-style preset; confirm successors shift and nothing overlaps; lanes re-hug.
- **Bands:** labels correct (THEN/ELSE/LOOP/CASE…), nested depth reads, 18px padding, hug at every level, geometry correct at 330/300.
- **Expansion rule:** required + non-default shown; defaults hidden with count; required-empty shows "— not set"; preview replaced; "Edit in Properties" selects node + focuses panel.
- **Persistence:** expanded set survives reload (layout-autosave round-trip) and never appears in exported YAML.
- **Edges untouched:** confirm `AnimatedEdge` and routing unchanged; edges re-flow to moved/resized blocks automatically; resting vs post-run rendering identical to today.

## Open risks / follow-ups

- **Height accuracy.** If the layout estimates height wrong, blocks overlap. Prefer xyflow `node.measured` height fed into a relayout; if estimating, base it on shown-row count and err generous. Tracked as the primary implementation risk.
- **TS↔C# layout parity.** Width/spacing constants live in both `hierarchicalLayout.ts` and `FlowCanvasBridge.cs`; a missed mirror desyncs import vs. live layout. Update both; consider a parity assertion/test.
- **Band geometry needs heights at compute time.** `BranchBandsLayer` reads `nodes`; ensure measured/estimated heights are available there (via `node.measured` or a stored height) so lanes wrap expanded children.
- **Horizontal spread.** Wider blocks + 300px children + 18px lanes widen deeply nested branches noticeably (seen in the capstone). Acceptable; revisit column-decay (`COLUMN_WIDTH_DECAY`, currently clamped off) if it becomes a problem.
- **Very tall expansions.** A block with many non-default fields (e.g. LOCALCMD) grows tall when expanded; acceptable for read-only grow-in-place, but note for the density review.
- **Autosave trigger.** Layout-autosave doesn't fire on arbitrary state today; the explicit `sendLayoutAutosave()` on toggle must be verified to actually persist.
