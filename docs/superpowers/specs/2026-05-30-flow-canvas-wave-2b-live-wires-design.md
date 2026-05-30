# Flow Canvas Wave 2b — Live Wires (Gradient Edges + Data Packets) Design

**Date:** 2026-05-30
**Status:** Approved (brainstorming) — ready for implementation plan
**Branch:** `0.51.21`
**Initiative:** Flow Canvas "flashy + feature-rich" enhancement — Wave 2b, **first cycle**

---

## Goal

Make canvas connections read as a **living dataflow**: every edge gets a proper arrowhead, branch edges carry their branch color at rest and during a run, and a single glowing "data packet" travels source → target along active edges while a script runs. This is the first slice of Wave 2b; it is **pure-frontend, render-only, and round-trip-safe** — exactly the discipline Wave 2a held.

## Scope

**In scope (this cycle — "Live Wires"):**
- Arrowheads on all edges (fixes the never-assigned-`markerEnd` bug).
- Branch-aware edge color (then=green, else/catch/default=red, elif/do/case=amber, finally=accent, parallel=network) — reusing the Wave 2a branch-color language.
- Source→target gradient stroke.
- A single traveling **pulse-dot** packet while running.
- Replacing the marching-ants animation (and its per-edge inline `<style>`) with the gradient + packet.
- Reduced-motion gating for the packet.

**Explicitly deferred (later 2b cycles — NOT this spec):**
- **Value pills** at the edge's leading end (overlap with inline result chips; need execution-data plumbing).
- **Loop & Branch Instrumentation** (C# `StepExecutionEventArgs`/`execution-update` fields) — cross-stack.
- **Execution Cinematics** (running halo, success checkmark, error shake, count-up, view-transitions).
- **Inline Result Chips.**
- **Framer Motion / `motion`** — still deferred; this cycle is fully static + CSS/SVG, **no new runtime dependency.**

## Resolved decisions (from brainstorming)

| # | Decision | Choice |
|---|----------|--------|
| D1 | Edge color semantics | **Branch-Aware** — branch edges carry branch color at rest; plain edges neutral; live run brightens the active edge. |
| D2 | Packet shape & count | **Single Pulse Dot** — one glowing dot with a soft size-pulse; continuous loop (~1.6s/traversal) while running. |
| D3 | Marching ants | **Replace** with the brighter gradient + packet (one motion signal); delete per-edge inline `<style>`. |
| D4 | Value pills | **Defer** to the result-chips cycle. |
| D5 | Render path | **`AnimatedEdge` for all edges** (rest + running) — single source for arrowhead + gradient + branch color + packet; fixes branch-color-lost-on-run. |
| D6 | Resting branch edges | **Solid** (drop today's resting dashes; color carries branch meaning). |
| D7 | Packet motion tech | **CSS `offset-path: path()` + `offset-distance` keyframe** (one shared keyframe; GPU-friendly; gated by `running && !reduced-motion`). |

## Current state (grounding — verified facts, do not re-derive)

- `edge.data.branchPath` already carries branch identity: `then`, `else`, `elif/<n>/then`, `do`, `catch`, `finally`, `cases/<n>/do`, `default`, `parallel/<n>`. Set on connect via `inferDefaultBranchMetadata` and editable via `EdgeContextMenu` → `updateEdgeBranchMetadata`.
- `getBranchVisual(blockType, metadata)` (`graphSlice.ts` ~133-221) already maps branch → `style.stroke` token + `label` + `labelStyle`, currently with a `...dashed` strokeDasharray. Tokens used: then/do(try)→`--fc-state-success`, elif/do(loop)/case→`--fc-state-warning`, else/catch/default→`--fc-state-error`, finally→`--fc-accent`, parallel→`--fc-cat-network-border`, plain→`--fc-edge-idle`.
- Wave 2a shipped `branchColorVar(key)` (`utils/branchBands.ts`) → `var(--fc-branch-*)` tokens (which **alias** the state/accent hues). It is the single branch→token source for the bands + Properties chip. `--fc-branch-then/try`=success, `else/catch/default`=error, `elif/do/case`=warning, `finally`=accent, `parallel`=network, `fallback`=text-disabled.
- Edges only become `AnimatedEdge` **while running** (`App.tsx:353` → `...(isRunning ? { type: 'animated' } : {})`); otherwise built-in `smoothstep` via `defaultEdgeOptions` (`App.tsx:399`). `edgeTypes = { animated: AnimatedEdge }` (`App.tsx:52-53`).
- `AnimatedEdge.tsx` ignores the branch `style.stroke` (forces `stateColors[sourceState]` or `--fc-edge-idle`), passes `markerEnd` straight through (and **nothing assigns a marker**, so no arrowhead), and injects a per-edge `<style>` block for `marchingAnts`.
- **Round-trip:** `exportGraph.ts` serializes `node.data.props` only; edges are reconstructed from graph structure; edge styling/markers are visual and never exported. Parity bundle = 22/22 under `--workers=1` (the round-trip proof). No-hex token-sweep gate is live.
- No `--fc-edge-packet` / per-state "bright" stop tokens exist yet. `mix(color,pct)` (color-mix over transparent) is the only translucency helper; there is **no lighten helper** — any new/brighter color must be authored as a `--fc-*` token in `tokens.css`.

## Architecture

Extend `AnimatedEdge` into the **universal custom edge** used for every edge (rest + running). It owns four render concerns, all visual-only:

1. **Branch color** — `getBranchVisual` stays the **single, blockType-aware edge-color authority** (it already correctly resolves every case: loop `do`=warning vs `try` body=success; `elif`=warning vs a trailing `then`=success; switch `case`=warning vs `default`=error; etc.). Refactor it to resolve the branch **key** and route the color through the 2a `branchColorVar()` token map, so edges + bands + Properties chip share one token source while preserving the blockType nuance. `AnimatedEdge` simply **consumes the resolved `edge.style.stroke`** (a `var(--fc-*)` token) — it does **not** re-derive the branch from `branchPath`. Continuation edges → `--fc-accent`; plain successors → `--fc-edge-idle` (unchanged).
2. **Gradient stroke** — an SVG `<linearGradient gradientUnits="userSpaceOnUse">` oriented along the edge using the existing `sourceX/Y` → `targetX/Y` props, from a dimmer stop `mix(edgeColor, ~30)` to the full `edgeColor` toward the target, where `edgeColor` is the consumed `edge.style.stroke`. (No new color token for the dim stop — `mix()` is gate-safe.)
3. **Arrowhead** — set a real `markerEnd` so an arrowhead finally renders, colored to match the edge. **Preferred:** React Flow's `markerEnd={{ type: MarkerType.ArrowClosed, color: edgeColor }}` where `edgeColor` is the edge's `var(--fc-*)` token (React Flow generates+dedupes a `<marker>` per color; the token resolves as the marker fill). **Fallback** (only if a `var()` token does not resolve in RF's generated marker): a small `EdgeMarkers` component rendering a hidden `<svg><defs>` of tokenized `<marker>`s (one per branch/state token, `fill: var(--fc-*)`) referenced by `markerEnd={url(#fc-arrow-<key>)}`. Either way the color stays inside the token layer.
4. **Packet** — a single SVG dot positioned via CSS `offset-path: path('<edgePath>')`, animated `offset-distance: 0%→100%` by one shared keyframe (authored once, not per-edge), plus a soft size-pulse. Rendered **only** when `isRunning && sourceHasExecuted && !reducedMotion`.

### Color & motion states

| Edge kind | Rest | Running (source executed/active) | Reduced-motion |
|-----------|------|----------------------------------|----------------|
| Branch (then/else/…) | Solid branch-color gradient + arrowhead | Brighter gradient + **pulse-dot packet** | Static gradient + arrowhead, **no packet** |
| Plain successor | Neutral `--fc-edge-idle` + arrowhead | Accent (`--fc-accent`) gradient + packet | Static neutral + arrowhead, no packet |
| Continuation (`next`) | `--fc-accent` gradient + arrowhead | Brighter + packet | Static, no packet |

The packet "core" is a single shared bright token (`--fc-edge-packet`, a near-white tint authored in `tokens.css`); an optional outer glow inherits the edge color. Execution-state readability is preserved — the source block's state can still tint a plain edge (success/active/error) during a run, consistent with today.

## Round-trip safety (the hard gate)

- Edges, markers, gradient, and packet are **all visual**. Export serializes `node.data.props` only; we **only read** `edge.data.branchPath` (never write it, never touch `node.data`).
- No change to `exportGraph.ts` / `FlowCanvasBridge.cs`. Parity bundle MUST stay 22/22 under `--workers=1`.
- Any new color is a `--fc-*` token in `tokens.css`, referenced via `var()`; translucency via `mix()`/`color-mix` — **never** a `var()`+alpha string concat. The live token-sweep gate (extended to cover an edge) must stay green.

## Reduced-motion

The packet element/animation is not rendered (or is paused) when the `.fc-reduced-motion` body class is set (existing Wave 1 kill switch). Static gradient + arrowhead remain so the graph is still legible. The shared packet keyframe lives behind a `.fc-reduced-motion … { animation: none }` (or the element is conditionally rendered).

## Cleanup (delete-before-build)

- Remove the per-edge inline `<style>` marching-ants block and the `marchingAnts` keyframe.
- Remove the rest=`smoothstep` / run=`animated` split (`App.tsx:353`); set the edge type to the universal `animated` once.
- Drop the `...dashed` resting dashes from `getBranchVisual` (color now carries branch meaning); keep its `label`/`labelStyle` and its blockType-aware resolution. Re-point its color tokens through `branchColorVar` so there is one branch→token source (the `--fc-branch-*` tokens already alias the same hues, so the change is naming-only, not visual).

## Files

**New**
- `FlowCanvas/e2e/flow-canvas-live-wires.spec.ts` — edge rendering spec (below).
- `FlowCanvas/src/nodes/EdgeMarkers.tsx` — *(only if the React Flow `markerEnd` color approach can't carry a `var()` token)* hidden `<svg><defs>` of tokenized arrowhead `<marker>`s, mounted once in `App`.

**Modified**
- `FlowCanvas/src/nodes/AnimatedEdge.tsx` — universal edge: consume `edge.style.stroke` for color, `userSpaceOnUse` gradient, real `markerEnd` arrowhead, CSS `offset-path` pulse packet gated by running + reduced-motion; remove inline `<style>`/marching-ants.
- `FlowCanvas/src/App.tsx` — use `animated` for all edges (remove the running-only split); mount `<EdgeMarkers />` only if the fallback marker approach is used.
- `FlowCanvas/src/stores/slices/graphSlice.ts` — `getBranchVisual`: drop `...dashed`, re-point color tokens through `branchColorVar` (keep labels + blockType-aware resolution); plain/continuation unchanged in meaning.
- `FlowCanvas/src/styles/tokens.css` — add `--fc-edge-packet` (bright packet core) and any gradient-stop token if `mix()` is insufficient. Only place new colors are authored.

## Testing

- **New `flow-canvas-live-wires.spec.ts`:** (a) every edge renders an arrowhead marker (`marker-end` resolves to a non-empty `<marker>`); (b) a `then` branch edge's stroke/gradient resolves to `--fc-branch-then`; an `else` edge to `--fc-branch-else`; (c) a plain edge is `--fc-edge-idle` at rest; (d) on `execution-update` running, the packet element is present and animating, and **absent** under `.fc-reduced-motion`; (e) packet absent at rest.
- **Token-sweep gate** stays green, extended so the scanned fixture includes an edge (no hex, no `var()`+alpha concat in edge/marker/gradient/packet styles).
- **Parity** 22/22 under `--workers=1` (export byte-identical — the round-trip proof).
- **Dist gate** (`test:e2e:dist`) green — SVG markers + CSS offset-path survive the production single-asset bundle.
- `dotnet build SSH_Helper.sln` 0 errors (re-embeds dist).

## Exit criteria

- [ ] All edges render a tokenized arrowhead (never-assigned-marker bug fixed).
- [ ] Branch edges carry their branch color (via `branchColorVar`) at rest **and** running; solid (no resting dashes); plain edges neutral.
- [ ] Source→target gradient stroke renders, oriented along the edge.
- [ ] A single pulse-dot packet travels source→target on active edges while running; absent at rest and under reduced-motion.
- [ ] Marching-ants animation + per-edge inline `<style>` removed; rest/run edge-type split removed; one shared keyframe.
- [ ] No hex outside the token layer (token-sweep green, incl. an edge); no `node.data`/`exportGraph.ts`/`FlowCanvasBridge.cs` change; parity 22/22 under `--workers=1`.
- [ ] No new npm runtime dependency (Framer Motion still deferred).
- [ ] `npm run build` 0, full e2e green (modulo the known parity-CLI parallel build-lock race — green serialized), dist gate green, `dotnet build` 0 errors.

## Risks / open items

- **Gradient orientation on re-layout:** `userSpaceOnUse` coords must use the live `sourceX/Y`–`targetX/Y` each render so the gradient re-orients when nodes move (React Flow re-renders the edge with fresh coords — low risk).
- **`offset-path` + React Flow edge re-render:** the packet's `offset-path` must update when the edge path changes (drag). Acceptable: recompute `path()` from the same `edgePath` string the edge uses; CSS picks it up on re-render.
- **Marker id resolution:** `url(#id)` references resolve document-wide as long as the `<marker>` exists in a mounted SVG; `EdgeMarkers` mounted once in `App` satisfies this.
- **Dense-graph perf:** one shared keyframe + GPU-composited `offset-distance`/transform keeps cost low; packet only mounts for active edges during a run.
- **branchPath→token nuance (must preserve):** keep `getBranchVisual` as the authority — it correctly distinguishes loop `do` (amber) from a `try` body (green) and `elif/<n>/then` (amber) from a plain `then` (green). Do **not** naively reuse `branchKeyFromStepPath` on the edge's `branchPath` (it scans last-to-first and would mis-map `elif/0/then` → `then`). The refactor only swaps the *token names* (`--fc-state-*` → aliased `--fc-branch-*`); the resolution logic stays.
