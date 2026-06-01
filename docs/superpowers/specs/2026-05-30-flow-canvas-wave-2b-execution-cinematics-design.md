# Flow Canvas Wave 2b — Execution Cinematics Design

**Date:** 2026-05-30
**Status:** Approved (brainstorming) — ready for implementation plan
**Branch:** `0.51.21`
**Initiative:** Flow Canvas "flashy + feature-rich" enhancement — Wave 2b, **second cycle** (follows Live Wires)

---

## Goal

Make a running script feel **alive on the canvas**: the executing block wears a sweeping comet halo with a soft breathing glow; a finished block draws its checkmark in with a pop and settles to a calm green; a failed block gives a small shake and a single red ripple; and the duration badge **counts up live** while the step runs, then settles to the measured time. Like Live Wires, this is **pure-frontend, render-only, and round-trip-safe** — the same discipline Wave 2a/2b-LiveWires held. It is fully static CSS/SVG + one tiny `requestAnimationFrame`-class timer; **no new runtime dependency** (Framer Motion stays deferred).

## Scope

**In scope (this cycle — "Execution Cinematics"):**
- **Running halo** — a rotating `conic-gradient` "comet" ring on the node edge **plus** a breathing accent glow. (Visual decision **C**.)
- **Success** — the `✓` glyph becomes an SVG checkmark that draws in (`stroke-dashoffset`) with a scale pop; the node settles to a soft green glow. (Intensity **Restrained**.)
- **Error** — a small one-shot shake (±2px) + a single red ripple ring that settles to the error glow. (Intensity **Restrained**.)
- **Count-up duration badge** — a **live elapsed ticker** while running (reads `blockTimings.start`), settling to the final measured `duration` on completion.
- Reduced-motion gating for every new animation; removal of the superseded `exec-pulse` running treatment.

**Explicitly deferred (later cycles — NOT this spec):**
- **Loop & Branch Instrumentation** (C# `StepExecutionEventArgs`/`execution-update` fields) — cross-stack.
- **Inline result chips** + **edge value pills** (depend on the instrumentation).
- **View-transitions / pop-in lifecycle / first-light empty state / hover toolbar / minimap radar** — Wave 2c "Delight."
- **`StartNode` cinematics** — `StartNode` is a separate, non-executing entry-marker component; it keeps its current green-gradient treatment this cycle.
- **Framer Motion / `motion`** — still deferred; this cycle is fully static CSS/SVG + one rAF timer, **no new runtime dependency.**

## Resolved decisions (from brainstorming)

| # | Decision | Choice |
|---|----------|--------|
| D1 | Scope | **One cohesive cycle** — all four effects in a single ~7-task plan (they share one CSS file + the same exec-state read; no cross-stack work to isolate). |
| D2 | Animation library | **Pure CSS + a tiny rAF timer.** No Framer Motion; no new npm dependency. |
| D3 | Running halo | **C — comet ring + soft glow.** A sweeping `conic-gradient` arc on the edge over a breathing accent glow. |
| D4 | Success / error intensity | **Restrained.** Success: checkmark draw+pop, soft green settle (no whole-node bloom/scale). Error: ±2px shake + one gentle ripple (no extra red glow flash). |
| D5 | Count-up | **Live ticker → settle.** Ticks up live from `blockTimings.start` while running; locks to the final `duration` on completion. Reduced-motion shows the final value only. |
| D6 | RUNNING indicator glyph | **Drop the `◌` spinner glyph** — the comet ring now carries the "spinning" signal. Keep the `RUNNING` text + the live ticker. |

## Current state (grounding — verified facts, do not re-derive)

- **Exec state reaches `BaseBlock` via `node.data.execState`.** `executionSlice.setBlockState(id, state)` updates a `blockStates` Map **and** calls `updateNodeData(id, { execState })`; `BaseBlock` reads `blockData.execState` (`BaseBlock.tsx:64`). `execState` is a **sibling of `props`** on `node.data` — `exportGraph.ts` serializes `node.data.props` **only**, so `execState` is never exported. Reading it (and not writing new fields) is round-trip-safe.
- **Timings**: `blockTimings: Map<id, { start, end?, duration? }>`. On `state==='running'`, `messageBridge` calls `setBlockTiming(id, Date.now())` (sets `start`). On success/error/skipped it sets `end` + `duration`. `BaseBlock.tsx:70-74` reads `blockTimings.get(id)` and renders `durationText` **only when `duration != null`** (i.e., after completion). The live ticker will read `start` while running.
- **Today's running treatment** (to be replaced): `BaseBlock.tsx:94-116` — `existingBoxShadow = '0 0 16px ' + execGlowColors[state]` for non-idle/non-disabled, plus `containerStyle.animation = 'exec-pulse 1.5s ...'` when running. `execGlowColors` maps state → `var(--fc-glow-*)`. Heat ring stacks as `boxShadow: heatTint ? '0 0 0 3px ' + heatTint + ', ' + existingBoxShadow : existingBoxShadow`.
- **`heatTint` is idle/success-only** (`BaseBlock.tsx:79-82`: `execState === 'idle' || execState === 'success'`). So the heat ring **never** coexists with `running` or `error` — only with `success` (and idle). This is why running/error can move to class-driven `box-shadow` animations without breaking the heat-ring stack, while **success must keep the inline `box-shadow` + heat-stack path**.
- **CSS animations override inline styles** in the cascade (no `!important` needed). So a state class whose `animation` animates `box-shadow`/`transform` cleanly supersedes the inline `boxShadow` for running/error.
- **Reduced-motion kill switch**: `uiSlice.reducedMotion` (default `false`, OS-seeded, persisted via C# `pref-save`/`pref-restore`); `App.tsx:120` toggles `document.body.classList.toggle('fc-reduced-motion', reducedMotion)`. `styles/reducedMotion.css` is a **blanket**: `body.fc-reduced-motion *, *::before, *::after { animation-duration:.001ms !important; animation-iteration-count:1 !important; transition-duration:.001ms !important }`. It collapses **every** keyframe (inline or class) to a static end-state and keeps `animationend` firing. `BaseBlock` already subscribes to store slices via `useFlowStore`; it will read `reducedMotion` to conditionally render the comet child + gate the live ticker.
- **`overflow:hidden` on the node container** (`BaseBlock.tsx:106`): clips descendant content but **not** the element's own outset `box-shadow`. So the breathing glow + ripple (container `box-shadow`) render outside the box uncliped; an `inset:0` child ring stays within the border-box (never clipped, never grows the node). React Flow measures the node wrapper, so a transient `transform` shake on the inner container does **not** move handle/edge geometry.
- **Established CSS pattern**: `baseblock.css` holds `@keyframes exec-pulse` + `@keyframes spin` (+ `.search-*`); `nodes/animatededge.css` (Live Wires) is the precedent for a dedicated keyframe file with shared keyframes, gated by the reduced-motion blanket. The Live Wires `offset-path` packet survived the production bundle + dist gate — advanced CSS (here: `conic-gradient` + `mask` + `@property`) is viable in the WebView2 Chromium runtime and will be proven by the dist gate.
- **Tokens**: `tokens.css` is the only place colors are authored (OKLCH, no hex). Relevant existing tokens: `--fc-accent: oklch(68% 0.16 255)`, `--fc-glow-running-min: …/0.3`, `--fc-glow-running-max: …/0.6`, `--fc-glow-success: oklch(72% 0.17 150 / 0.3)`, `--fc-glow-error: oklch(60% 0.20 25 / 0.3)`. Alpha helper `mix(color, pct)` (`utils/tokens.ts`) = `color-mix(in oklch, color pct%, transparent)`. There is **no lighten helper** — a brighter color must be authored as a `--fc-*` token. The token-sweep gate (`flow-canvas-token-sweep.spec.ts`) scans all `[style]` attributes for raw `#hex` **and** malformed `var(...)<hex>` concat.

## Architecture

All four effects live in **`BaseBlock`**, driven entirely by the existing `execState` + `blockTimings` (read-only). Keyframes + the `@property` registration + state helper classes go in a new **`nodes/execution-cinematics.css`**.

### 1. Running halo (comet ring + breathing glow)
- **Breathing glow** — when `running`, apply a class (`fc-exec-running`) whose `animation` drives `box-shadow` between `--fc-glow-running-min` and `--fc-glow-running-max` (~2.4s ease-in-out). Because heat is idle/success-only, this owns the running `box-shadow` (the inline `boxShadow` is not set for running). The blanket freezes it to a soft static glow under reduced-motion.
- **Comet ring** — a child `<span class="fc-run-halo" aria-hidden>` rendered **only when `execState==='running' && !reducedMotion`**. CSS: `position:absolute; inset:0; border-radius:8px; padding:1.5px; pointer-events:none;` background `conic-gradient(from var(--fc-halo-angle), var(--fc-run-comet) 0deg, var(--fc-accent) 24deg, transparent 120deg)`, masked to a ring (`mask: linear-gradient(#000 0 0) content-box, linear-gradient(#000 0 0); mask-composite: exclude` + `-webkit-` peers), rotated by `@keyframes fc-run-comet-spin { to { --fc-halo-angle: 360deg } }` with `@property --fc-halo-angle { syntax:'<angle>'; initial-value:0deg; inherits:false }`. `inset:0` keeps it inside the border-box (never clipped, never grows the node).

### 2. Success (checkmark draw-in + pop, soft settle)
- The exec indicator's static `✓` becomes an inline `<svg class="fc-check"><path d="M5 13l4 4L19 7"/></svg>`; the path animates `stroke-dashoffset` (full→0, `fc-check-draw` ~0.4s `forwards`) and the svg scales (`fc-check-pop`). Stroke = `var(--fc-state-success)`. The animations play on mount (the SVG mounts when `execState` becomes `success`).
- Node settles to a **static** inline `box-shadow: 0 0 10px var(--fc-glow-success)` (kept on the inline path so the heat ring still stacks for success). No whole-node bloom/scale (Restrained).
- Keep the `DONE` text + duration badge.

### 3. Error (small shake + one ripple)
- When `error`, apply `fc-exec-error` whose `animation` runs two comma-separated tracks: `fc-err-shake` (`transform: translateX` ±2px, ~0.5s, one-shot) and `fc-err-ripple` (`box-shadow` ring starting at `0 0 0 0 var(--fc-glow-error-strong)`, spreading to `~8px` while fading through the `transparent` keyword, **settling** to `0 0 8px var(--fc-glow-error)` via `forwards`, ~0.6s). Distinct properties (transform vs box-shadow) so they compose. Error is not heat-eligible, so the class owns the box-shadow. The blanket collapses both under reduced-motion (no shake; ripple instant → static error glow).
- Keep the `✗ ERROR` text + duration badge.

### 4. Count-up (live ticker → settle)
- A small hook (e.g. `useRunningElapsed(start, isRunning, reducedMotion)`): while `isRunning && !reducedMotion && start != null`, an `requestAnimationFrame` loop computes `now - start`, formats it (`<1000ms → "Nms"`, else `"N.Ns"`), and `setState` **only when the formatted text changes** (≈10 updates/sec, one running node at a time — the canvas is a single-host preset debugger). Cleans up on stop/unmount. Returns the formatted string or `null`.
- Badge text = `execState==='running' ? liveText : durationText`. Under reduced-motion `liveText` is `null` and `durationMs` is still `null` while running → badge hidden during the run, then shows the final value on completion (today's behavior preserved).

### Color & motion states

| State | Node treatment | Exec indicator | Reduced-motion |
|-------|----------------|----------------|----------------|
| running | breathing accent glow + sweeping comet ring | `RUNNING` + **live ticker** | static soft glow; **no comet**, **no ticking** (final value only) |
| success | static soft green glow (`0 0 10px`) | **drawn checkmark** + pop + `DONE` + duration | checkmark appears drawn (instant); static glow |
| error | one-shot shake + ripple → static error glow | `✗ ERROR` + duration | no shake; ripple instant → static error glow |
| skipped / disabled / selected / idle | **unchanged** | unchanged | unchanged |

## Round-trip safety (the hard gate)
- Every effect is **visual**. It only **reads** `node.data.execState` (already written by `executionSlice`; not in `node.data.props`, so not exported) and `blockTimings` (a store Map, not on nodes). **No** `node.data` writes; **no** `exportGraph.ts` / `FlowCanvasBridge.cs` change.
- Parity bundle (`preset-parity` / `preset-negative` / `gesture-smoke` / `connection-guards`) MUST stay **22/22 under `--workers=1`** — the round-trip proof.
- New colors are `--fc-*` tokens in `tokens.css`, referenced via `var()`; translucency via `mix()`/`color-mix`; ripple fade via the `transparent` keyword — **never** a `var()`+hex concat. The live token-sweep gate (extended to exec-state nodes) must stay green.

## Reduced-motion
The `.fc-reduced-motion` blanket collapses every new keyframe to its static end-state. Additionally: the **comet child is not rendered** under reduced-motion, and the **live ticker does not tick** (shows the final value only). Static glow + drawn checkmark + settled error glow remain so a finished run is still legible. The spec asserts the comet child's absence and the animation-duration collapse.

## Cleanup (delete-before-build)
- **Remove** `@keyframes exec-pulse` from `baseblock.css` and its inline use (`containerStyle.animation = 'exec-pulse …'`) — superseded by the running halo. Keep `@keyframes spin` (still used elsewhere) unless the only remaining consumer was the dropped `◌` glyph — if so, remove `spin` too and verify no other reference.
- **Drop the `◌` spinner glyph** from the RUNNING indicator (D6).
- Keep the running `box-shadow` logic for non-running states; only the running/error branches move to class-driven animations.

## Files

**New**
- `FlowCanvas/src/nodes/execution-cinematics.css` — `@property --fc-halo-angle`; keyframes `fc-run-breathe`, `fc-run-comet-spin`, `fc-check-draw`, `fc-check-pop`, `fc-err-shake`, `fc-err-ripple`; state helper classes (`.fc-exec-running`, `.fc-exec-error`, `.fc-run-halo`, `.fc-check`).
- `FlowCanvas/e2e/flow-canvas-execution-cinematics.spec.ts` — render spec (below).

**Modified**
- `FlowCanvas/src/nodes/BaseBlock.tsx` — apply `fc-exec-running`/`fc-exec-error` classes; render the `fc-run-halo` child (gated `running && !reducedMotion`); swap the SVG checkmark in for `✓`; drop the `◌` glyph; add the `useRunningElapsed` hook + badge text logic; read `reducedMotion` from `uiSlice`. Import `./execution-cinematics.css`.
- `FlowCanvas/src/styles/tokens.css` — **two** new tokens only: `--fc-run-comet` (bright azure comet leading edge, e.g. `oklch(86% 0.13 255)`) and `--fc-glow-error-strong` (ripple start, e.g. `oklch(60% 0.20 25 / 0.6)`).
- `FlowCanvas/src/nodes/baseblock.css` — delete `@keyframes exec-pulse` (and `spin` iff now unused).
- `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts` — extend to drive a node through running/success/error (via `execution-update`) and re-scan, so the new exec-state styles are covered by the no-hex gate.

## Testing

- **New `flow-canvas-execution-cinematics.spec.ts`:**
  - **running**: after an `execution-update` running, the node has the `fc-exec-running` class with a non-collapsed `animation-duration` (breathing); a `.fc-run-halo` comet child is present. Under `.fc-reduced-motion`: the comet child is **absent** and the breathing `animation-duration` collapses (`< 0.01s`).
  - **success**: the exec indicator renders the SVG checkmark (`.fc-check`) with a draw animation; the node carries the soft green settle glow.
  - **error**: the node has `fc-exec-error` with a non-collapsed shake/ripple animation; under `.fc-reduced-motion` it collapses.
  - **count-up**: with a running `execution-update` whose `start` is in the past, the badge shows a live elapsed value that **increases between two samples**; after a success `execution-update`, the badge shows the final `duration`. Under `.fc-reduced-motion`, no live value appears while running.
- **Token-sweep gate** stays green, **extended** so the scanned fixture includes a node in each of running/success/error (no hex, no `var()`+alpha concat in halo/glow/checkmark/ripple styles).
- **Reduced-motion spec** (`flow-canvas-reduced-motion.spec.ts`) stays green; the new keyframes are covered by the blanket.
- **Parity** 22/22 under `--workers=1` (export byte-identical — the round-trip proof).
- **Dist gate** (`test:e2e:dist`) green — `conic-gradient` + `mask` + `@property` + SVG `stroke-dashoffset` survive the production single-asset bundle.
- `dotnet build SSH_Helper.sln` 0 errors (re-embeds dist).

## Exit criteria

- [ ] Running block shows the comet ring + breathing glow; comet absent under reduced-motion; `◌` glyph removed; `exec-pulse` deleted.
- [ ] Success draws the checkmark in with a pop and settles to a soft green glow; heat ring still stacks on a success block.
- [ ] Error gives a small shake + one ripple that settles to the error glow; collapses under reduced-motion.
- [ ] Duration badge ticks up live while running and locks to the measured `duration` on completion; final-value-only under reduced-motion.
- [ ] Two new `--fc-*` tokens only; no hex outside the token layer (token-sweep green, incl. exec-state nodes); no `node.data`/`exportGraph.ts`/`FlowCanvasBridge.cs` change; parity 22/22 under `--workers=1`.
- [ ] No new npm runtime dependency (Framer Motion still deferred).
- [ ] `npm run build` 0; new cinematics spec green; reduced-motion + run-timing specs green; full e2e green (modulo the known parity-CLI parallel build-lock race — green serialized); dist gate green; `dotnet build` 0 errors.

## Risks / open items

- **`@property` + `conic-gradient` + `mask` in the production bundle** — the main risk (advanced CSS through Vite + WebView2). Mitigation: the dist gate (`test:e2e:dist`) proves it, exactly as Live Wires proved `offset-path`. If `@property` angle-interpolation misbehaves in the bundle, fall back to rotating the masked ring element via `transform: rotate` (slightly different optics, same shape).
- **CSS-animation-overrides-inline cascade** — relied on for running/error box-shadow. Verified behavior (animations outrank normal inline declarations). Success deliberately stays on the inline path to preserve heat-ring stacking; the spec/plan must keep that split.
- **One-shot replay** — the success/error animations play on the `execState` transition (class added). If a node is re-run, `execState` cycles idle→running→result and the class re-applies, replaying the one-shot. No persistent flag needed.
- **Live-ticker churn** — re-renders only when the formatted 0.1s text changes (≈10/sec) and only for the single running node; negligible. Hook tears down on stop/unmount.
- **`spin` keyframe** — dropping `◌` may orphan `@keyframes spin`. The plan must grep for other consumers before deleting it.
