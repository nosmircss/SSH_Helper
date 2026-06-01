# Flow Canvas — Neon Block Design ("Classic Ring")

**Date:** 2026-05-31
**Status:** Approved (design), pending implementation plan
**Scope:** Flow Canvas block node visual redesign — make idle blocks "pop" against the dark canvas.

## Problem

On the Flow Canvas, idle block cards blend into the background. The cause is concrete and token-level:

- Node body `--fc-node-surface` = `oklch(22% 0.02 275)` sits on canvas `--fc-canvas-bg` (= `--fc-surface-1`) = `oklch(22% 0.035 275)` — **identical lightness**, so there is almost no figure/ground contrast.
- Idle blocks have **no box-shadow** (`existingBoxShadow` resolves to `'none'`).
- Category color is confined to the 3px left rail and the icon glyph; the wide card border is neutral (`--fc-node-border`).

The result reads as one grey column regardless of block type.

## Chosen direction

**Cyber Neon "Classic Ring", Balanced intensity.** Selected by the user after a visual brainstorming pass (12 anatomies → Color Spine / Saturated Tile / Cyber Neon → 5 variations each → Classic Ring → Balanced). Identity moves from the rail **into the card border** (category hue) plus a softened category-colored ambient glow. The neutral body is retained, so color comes from the edge, not a fill.

## Goals

- Idle blocks clearly separate from the canvas and read their category at a glance.
- Category identity is carried by a **category-hued border + balanced glow**, derived per-category from existing hue tokens (no new hex, no hue duplication).
- The idle treatment **yields gracefully** to every existing execution visual (running, error, success, skipped, selected, heatmap) with correct precedence and no flicker.
- Zero change to YAML export — this is presentation only.

## Non-goals

- No change to YAML import/export, graph data, or `FlowCanvasBridge` (verify byte-identical round-trip).
- No MiniMap ring parity (the MiniMap pre-resolves category colors to hex for SVG and ignores CSS box-shadow). Tracked as a possible follow-up, not in scope.
- No light/high-contrast theme values beyond a placeholder (the `data-theme` mechanism exists; authoring light values is follow-up tied to the theme work).
- No animation on the idle ring (it is a static glow; running/error keep their existing animations).
- StartNode is **not** restyled — it keeps its green identity (see Edge cases).

## Visual specification

### Idle block
- **Body:** unchanged — `--fc-node-surface`.
- **Border:** `var(--fc-cat-<category>-border)` (the full-strength category hue), replacing neutral `--fc-node-border`.
- **Glow (Balanced):** a composite box-shadow derived from the category hue via the existing `mix()` helper (`color-mix(in oklch, <color> <pct>%, transparent)`):
  ```
  0 0 0 1px  mix(border, 36%),     /* crisp structural ring  */
  0 0 10px -2px mix(border, 46%),  /* balanced ambient glow  */
  inset 0 0 10px -7px mix(border, 60%)  /* faint inner light */
  ```
- **Header:** category-tinted icon (unchanged); **TYPE label tinted toward the category hue**; **title in monospace** (the "cyber" read).
- **Code/preview strip:** faint category-tinted border.
- **Rail:** **removed** — the colored border carries identity. Header/body left padding drops the `--fc-rail-w` offset (becomes a fixed value).

### Border across execution states
The category-hued border is **persistent for all non-disabled, non-selected states** (idle / running / success / error / skipped). This prevents the border color from flickering between neutral and category as a block executes. Selected → white border (`--fc-border-selected`); disabled → muted (`--fc-border-muted`).

## Tokens

Add to `tokens.css` (glow section, after `--fc-glow-start`):

- `--fc-idle-ring-alpha: 36%` — structural 1px ring opacity.
- `--fc-idle-glow-alpha: 46%` — balanced ambient glow opacity.
- `--fc-idle-glow-inner-alpha: 60%` — inner light opacity.
- A glow intensity-scale comment: `-soft 0.15a (background) / -balanced ~0.25–0.46a (idle) / base 0.3–0.4a (running/success/error) / -strong 0.6a`.
- A `:root[data-theme='light']` placeholder block noting these must be re-authored for light theme.

Per-category color is **derived**, not duplicated: the ring/glow reads each block's own `var(--fc-cat-<c>-border)` hue. Timing blocks are intentionally desaturated (C 0.02–0.03), so their ring reads muted by design — accepted.

Add to `tokens.ts` a centralizing helper:

```ts
export function idleNeon(border: string): string {
  return `0 0 0 1px ${mix(border, 36)}, 0 0 10px -2px ${mix(border, 46)}, inset 0 0 10px -7px ${mix(border, 60)}`;
}
```

(The exact wiring of the alpha tokens vs. inline percentages is an implementation detail for the plan; the values above are the contract.)

## Integration & precedence

Implementation is an extension of the **existing** box-shadow ladder in `BaseBlock.tsx`, not a parallel mechanism. The idle ring is a **non-`!important` inline box-shadow** placed where `'none'` is today. Because CSS animations paint after inline styles, the class-driven `fc-exec-running` (`fc-run-breathe`) and `fc-exec-error` (`fc-err-ripple` / `fc-err-shake`) animations fully override the idle ring while active, and it returns cleanly when the class drops (the existing `transition: box-shadow 0.2s` smooths it).

`existingBoxShadow` gains an idle branch (note the `!heatTint` gate to avoid a double-ring):

```
execState === 'success' ? '0 0 10px var(--fc-glow-success)'
: execState === 'skipped' ? '0 0 16px var(--fc-glow-skipped)'
: selected ? '0 0 12px var(--fc-glow-selected)'
: (execState === 'idle' && !heatTint) ? idleNeon(colors.border)
: 'none'
```

Border:

```
border: 1px solid (selected ? 'var(--fc-border-selected)'
                  : isDisabled ? 'var(--fc-border-muted)'
                  : colors.border)
```

### Precedence (highest first)
1. **running** — `fc-exec-running` animation owns box-shadow; idle ring suppressed (+ comet halo when not reduced-motion). Wins.
2. **error** — `fc-exec-error` animations own box-shadow. Wins.
3. **disabled** — muted border, opacity 0.5, no ring (gated on `execState==='idle'`). Wins over idle styling.
4. **heatmap active** (`heatTint != null`, idle/success only) — the 3px heat ring (`0 0 0 3px ${heatTint}, …`) wins the idle slot; idle ring gated **off** so there is no double-ring.
5. **selected** — white `--fc-glow-selected` wins; idle ring not emitted when selected.
6. **success / skipped** — their inline glows win.
7. **idle** (not selected, not heat-active) — the new category ring; replaces today's `'none'`.
8. **breakpoint** — render-only badge; composes on top, never competes for the box-shadow slot.
9. **search match/current** — `outline` with `!important` on the outline property only; layers above any box-shadow; no conflict.

## Edge cases

- **StartNode:** keeps `--fc-glow-start` green identity; **no** category ring (explicit exception).
- **Disabled:** muted border + opacity 0.5, no ring.
- **Child blocks** (`_isChildOf`, minWidth 160, opacity 0.95): ring is on the same element, so it dims with child opacity; verify the 1px ring + icon chip don't crowd at the smallest size.
- **Container blocks** (if/foreach/try/switch/parallel): `BranchBandsLayer` renders at `zIndex -1` behind nodes; the ring renders outside the card border-box, above the bands — no clash. `overflow:hidden` does not clip box-shadow (proven by the existing `fc-run-halo`).
- **Reduced motion:** the ring is static — fine. No `reducedMotion.css` change unless animation is added later.
- **Handles / edges / markers:** handles are 8×8 inset targets; edge markers live on the SVG stroke. A perimeter glow does not occlude them.

## Files to touch

- `FlowCanvas/src/nodes/BaseBlock.tsx` — idle branch in the `existingBoxShadow` ladder; category-hued border for non-disabled/non-selected; remove the rail and its padding offset; tint the TYPE label and set the title to monospace.
- `FlowCanvas/src/styles/tokens.css` — add `--fc-idle-*` alpha tokens, intensity-scale comment, `:root[data-theme='light']` placeholder.
- `FlowCanvas/src/utils/tokens.ts` — add `idleNeon(border)` helper (reuses `mix()`).
- `FlowCanvas/src/nodes/StartNode.tsx` — confirm it is excluded from the category ring (no functional change expected).

No change required in `App.tsx` (MiniMap unaffected) or `reducedMotion.css` (static ring).

## Verification

- **Build:** `cd FlowCanvas && npm run build`; `dotnet build SSH_Helper.sln`.
- **Type/lint:** the React build runs `tsc`/eslint via the Vite build.
- **YAML round-trip:** import a sample script to the canvas and export — confirm byte-identical YAML before/after (no graph/data fields touched).
- **State-transition test:** idle → running → idle and idle → error → idle show no stale-ring flash and clean return to the idle ring.
- **Density review:** open a long (~30–50 block) FortiGate-style preset and confirm the idle rings read as separated-but-calm, and a running block still clearly out-shouts idle ones.
- **States review:** selected, disabled, success, skipped, heatmap-on, child, and container blocks all render correctly with the precedence above.
- **StartNode:** unchanged.

## Open risks / follow-ups

- **Visual hierarchy at density** — idle glow on every block at the Balanced alphas vs. running (0.4a)/error (0.3a). Confirm in the density review; the alphas are the tuning knob.
- **Selected-over-idle** — white selection glow over a colored border; gating idle off when selected mitigates wash-out, but verify the border hue still reads when selected.
- **MiniMap** — will not show the ring; confirm that's acceptable or schedule a `nodeStroke` extension as a separate task.
- **Light theme** — `--fc-idle-*` tokens are dark-only; add light values when the light theme ships (follow-up).
