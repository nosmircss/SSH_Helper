# Flow Canvas Wave 2b — Execution Cinematics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a running script feel alive on the canvas — the executing block wears a sweeping conic "comet" halo over a breathing glow, a finished block draws its checkmark in with a pop and settles to a soft green glow, a failed block gives a small shake + one red ripple that settles to the error glow, and the duration badge counts up live while running then locks to the measured time. Pure-frontend, render-only, round-trip-safe, reduced-motion-gated, **no new runtime dependency.**

**Architecture:** All four effects live in **`BaseBlock`**, driven entirely by the existing `execState` (`node.data.execState`, already written by `executionSlice`, NOT in `node.data.props` so never exported) + `blockTimings` (a store Map, read-only). Running + error become **class-driven** (`fc-exec-running` / `fc-exec-error`) whose CSS animations own the card's `box-shadow`/`transform` via the cascade (animations outrank inline styles); success stays on the **inline** `box-shadow` path so the Run Heatmap ring still stacks. Keyframes + the `@property` angle registration + helper classes go in a new **`nodes/execution-cinematics.css`**, gated by the existing `reducedMotion.css` blanket. The comet child is not rendered, and the live ticker does not tick, under reduced motion.

**Tech Stack:** TypeScript / React 19 / `@xyflow/react` v12 / Zustand 5 / CSS (OKLCH tokens, `conic-gradient`, `mask` + `mask-composite`, `@property`, SVG `stroke-dashoffset`) + one `requestAnimationFrame` ticker / Playwright e2e. Build `cd FlowCanvas && npm run build`; dist re-embed via `dotnet build SSH_Helper.sln`.

> **TDD note (presentational module):** node rendering is DOM/visual, so it cannot be strict unit-test-first. Each rendering task (3–5) is gated by `npm run build` clean + the live no-hex token-sweep + unchanged parity (22/22 under `--workers=1`) + the existing run-timing / reduced-motion / node-redesign specs. The dedicated `flow-canvas-execution-cinematics.spec.ts` regression lock is written in Task 6 once the DOM exists (the same pattern Wave 2a / Live Wires used successfully).

---

## Verified facts (do not re-derive — read from the live tree 2026-05-30)

> Absolute `file:line` references below are a **pre-edit snapshot**; Tasks 4–5 run against a file already shifted by Task 3. The edits themselves use verbatim `Find:` anchors (never line numbers), so execution is unaffected — cross-check by anchor text, not line.

- **Exec state** reaches `BaseBlock` via `node.data.execState` (`BaseBlock.tsx:64` `const execState = blockData.execState || 'idle'`). `executionSlice.setBlockState(id, state)` updates `blockStates` AND `updateNodeData(id, { execState })`; `execState` is a **sibling of `props`** on `node.data` and `exportGraph.ts` serializes `node.data.props` **only** → reading it is round-trip-safe.
- **Timings**: `executionSlice.ts:24` — `blockTimings: Map<string, { start: number; end?: number; duration?: number }>`. `messageBridge.ts:210` sets `start` (`setBlockTiming(stepId, Date.now())`) on `state==='running'`; on completion (`messageBridge.ts:225`) it sets `start`/`end`/`duration`. `BaseBlock.tsx:70-74` reads `blockTimings.get(id)` and renders `durationText` **only when `duration != null`** (after completion). The live ticker reads `start` while running.
- **Today's running treatment** (to replace): `BaseBlock.tsx:113-116` sets `containerStyle.animation = 'exec-pulse 1.5s ease-in-out infinite'` when running. `existingBoxShadow` (`94-98`) = `0 0 16px ${execGlowColors[execState]}` for non-idle/non-disabled; the `◌` spinner glyph is at `182` (`animation: spin 1s ...`).
- **`execGlowColors`** (`BaseBlock.tsx:21-27`) is referenced **only** at `BaseBlock.tsx:95` (grep-confirmed; not exported) → safe to delete once its single consumer is rewritten.
- **`heatTint` is idle/success-only** (`BaseBlock.tsx:79-82`). So the heat ring **never** coexists with `running` or `error`, only with `success` (and idle). This is why running/error can move to class-driven `box-shadow` animations without breaking the heat stack, while **success must keep the inline `box-shadow` + heat-stack path** (`108`: `boxShadow: heatTint ? '0 0 0 3px ' + heatTint + ', ' + existingBoxShadow : existingBoxShadow`).
- **CSS animations override inline styles** in the cascade (no `!important`). A state class whose `animation` animates `box-shadow`/`transform` cleanly supersedes the inline `boxShadow` for running/error. `animation-fill-mode: forwards` on an `infinite` animation is a no-op during normal motion, but under the reduced-motion blanket (which forces `animation-iteration-count: 1`) it makes the collapsed animation **hold its end keyframe** instead of reverting — this is how running keeps a soft static glow under reduced motion.
- **Reduced motion**: `uiSlice.ts:25` `reducedMotion: boolean` (default `false`, line 82), a top-level store field. `useFlowStore((s) => s.reducedMotion)` is the selector (already used by `AnimatedEdge`, Live Wires). `App.tsx:120` toggles `document.body.classList.toggle('fc-reduced-motion', reducedMotion)`. `styles/reducedMotion.css` is a **blanket**: `body.fc-reduced-motion *, *::before, *::after { animation-duration:.001ms !important; animation-iteration-count:1 !important; transition-duration:.001ms !important }` — collapses every keyframe to its end-state and keeps `animationend` firing.
- **`overflow:hidden` + `borderRadius:8`** on the node container (`BaseBlock.tsx:106,103`): an `inset:0; border-radius:8px` child stays inside the rounded border-box (never clipped away, never grows the node). A transient `transform` on the inner card does not move React-Flow handle/edge geometry (RF measures the node wrapper, not this child div).
- **State classes land on BaseBlock's inner card div** (the `<div style={containerStyle}>`), which currently has **no** `className`. The search highlight classes (`search-match`/`search-current`) are applied by `App.tsx:323-324` to the **React-Flow node wrapper**, a different element — no conflict.
- **Established CSS pattern**: `nodes/animatededge.css` (Live Wires) is the precedent for a dedicated keyframe file with shared keyframes, gated by the reduced-motion blanket; its `offset-path` packet survived the production bundle + dist gate, proving advanced CSS is viable in WebView2 Chromium. The mask-ring + `@property` here is proven by the same dist gate.
- **Tokens**: `tokens.css` is the only place colors are authored (OKLCH, no hex). Existing: `--fc-accent: oklch(68% 0.16 255)`, `--fc-glow-running-min: …/0.3`, `--fc-glow-running-max: …/0.6`, `--fc-glow-success: oklch(72% 0.17 150 / 0.3)`, `--fc-glow-error: oklch(60% 0.20 25 / 0.3)`, `--fc-glow-skipped`. Alpha helper `mix(color, pct)` (`utils/tokens.ts`) = `color-mix(in oklch, color pct%, transparent)`. There is **no lighten helper** — a brighter color must be authored as a `--fc-*` token. The token-sweep gate (`flow-canvas-token-sweep.spec.ts`) scans all DOM `[style]` attributes for raw `#hex` AND malformed `var(...)<hex>` concat (it does NOT scan stylesheet rules — but per the contract we still author every color as a token and use only the `black` mask keyword + the `transparent` keyword in the CSS file).
- **Test harness**: `e2e/support/harness.ts` exposes `loadGraphFixture` / `postHostMessage` / `installHostMessageCapture` / `waitForOutgoingMessage`. `createInteractionFixture()` (`e2e/fixtures/graphs.ts`) provides nodes `node-1`/`node-2`/`node-3`. Specs read the card via `node.locator('> div').first()`. The reduced-motion toggle is `page.locator('button[title*="motion" i]').first()`.

## File Structure

### New files

| Path | Responsibility |
| --- | --- |
| `FlowCanvas/src/nodes/execution-cinematics.css` | `@property --fc-halo-angle`; keyframes `fc-run-breathe`, `fc-run-comet-spin`, `fc-check-draw`, `fc-check-pop`, `fc-err-shake`, `fc-err-ripple`; helper classes `.fc-exec-running`, `.fc-run-halo`, `.fc-check`, `.fc-exec-error`. Imported by `BaseBlock`. |
| `FlowCanvas/e2e/flow-canvas-execution-cinematics.spec.ts` | Render-regression lock: running breathing class + comet child (gated); success SVG checkmark + soft glow; error shake/ripple class (gated); count-up live ticker → final lock. |

### Modified files

| Path | Change |
| --- | --- |
| `FlowCanvas/src/styles/tokens.css` | Add **two** tokens only: `--fc-run-comet`, `--fc-glow-error-strong`. Only place new colors are authored. |
| `FlowCanvas/src/nodes/BaseBlock.tsx` | Import the CSS; read `reducedMotion`; restructure `existingBoxShadow` (running/error → none, success → soft `0 0 10px`); apply `fc-exec-running`/`fc-exec-error` class; render the `fc-run-halo` comet child (gated `running && !reducedMotion`); drop the `◌` glyph; swap the success `✓` for an SVG checkmark; add `formatDuration` + the `useRunningElapsed` ticker + `badgeText`; delete the now-dead `execGlowColors`. |
| `FlowCanvas/src/nodes/baseblock.css` | Delete `@keyframes exec-pulse` (superseded) and `@keyframes spin` (orphaned by the `◌` drop — grep-verified). Keep `.search-*`. |
| `FlowCanvas/src/styles/reducedMotion.css` | Update the keyframe-inventory comment (no functional change). |
| `FlowCanvas/e2e/flow-canvas-node-redesign.spec.ts` | Update the one stale `exec-pulse` comment (assertion unchanged). |
| `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts` | Add a test that drives nodes through running/success/error and re-runs the no-hex scan (covers the new exec-state DOM). |

---

## Section 1: Tokens

### Task 1: Add the two execution-cinematics tokens

**Files:**
- Modify: `FlowCanvas/src/styles/tokens.css` (the "Glow / shadow scale" block, after `--fc-glow-error` ~line 122)

- [ ] **Step 1: Append the two new tokens** immediately after the `--fc-glow-error` line. Find:

```css
  --fc-glow-error: oklch(60% 0.20 25 / 0.3);
```

Replace with:

```css
  --fc-glow-error: oklch(60% 0.20 25 / 0.3);
  /* ── Wave 2b: execution cinematics (running comet leading edge + error ripple start) ── */
  --fc-run-comet: oklch(86% 0.13 255);
  --fc-glow-error-strong: oklch(60% 0.20 25 / 0.6);
```

- [ ] **Step 2: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green (new tokens resolve to OKLCH; no hex).
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 3: Commit.**

```bash
git add FlowCanvas/src/styles/tokens.css
git commit -m "feat(flow-canvas): add --fc-run-comet + --fc-glow-error-strong tokens (execution cinematics)"
```

---

## Section 2: Cinematics keyframe sheet

### Task 2: Create `execution-cinematics.css`

**Files:**
- Create: `FlowCanvas/src/nodes/execution-cinematics.css`

- [ ] **Step 1: Write the file** verbatim. Every color is a `--fc-*` token; the only literals are the `black` mask source (alpha-mask shape, not a design color) and the `transparent` keyword (gate-safe; never a `var()`+hex concat).

```css
/* FlowCanvas/src/nodes/execution-cinematics.css
 * Wave 2b — Execution Cinematics. Pure CSS/SVG running halo, success checkmark, and error
 * shake+ripple for BaseBlock. Driven entirely by execState (read-only); no node.data writes.
 * The global reducedMotion.css blanket collapses every keyframe here to its end-state; BaseBlock
 * ADDITIONALLY does not render the comet child and stops the live ticker under reduced motion.
 * Only literals: the `black` mask source and the `transparent` keyword (gate-safe). */

/* Animatable angle for the conic comet sweep. Without @property the `from <angle>` can't tween. */
@property --fc-halo-angle {
  syntax: '<angle>';
  initial-value: 0deg;
  inherits: false;
}

/* ── Running: breathing accent glow on the card. Owns the running box-shadow (heat is
   idle/success-only, so it never coexists). `forwards` makes the reduced-motion blanket freeze
   it to the soft min-glow end frame instead of reverting to no glow. ── */
.fc-exec-running {
  animation: fc-run-breathe 2.4s ease-in-out infinite forwards;
}
@keyframes fc-run-breathe {
  0%   { box-shadow: 0 0 8px var(--fc-glow-running-min); }
  50%  { box-shadow: 0 0 20px var(--fc-glow-running-max); }
  100% { box-shadow: 0 0 8px var(--fc-glow-running-min); }
}

/* ── Running: sweeping comet ring on the card edge. inset:0 keeps it inside the border-box
   (never clipped by overflow:hidden, never grows the node). Masked to a 1.5px ring. ── */
.fc-run-halo {
  position: absolute;
  inset: 0;
  border-radius: 8px;
  padding: 1.5px;
  pointer-events: none;
  background: conic-gradient(
    from var(--fc-halo-angle),
    var(--fc-run-comet) 0deg,
    var(--fc-accent) 24deg,
    transparent 120deg
  );
  -webkit-mask: linear-gradient(black 0 0) content-box, linear-gradient(black 0 0);
  -webkit-mask-composite: xor;
  mask: linear-gradient(black 0 0) content-box, linear-gradient(black 0 0);
  mask-composite: exclude;
  animation: fc-run-comet-spin 1.4s linear infinite;
}
@keyframes fc-run-comet-spin {
  to { --fc-halo-angle: 360deg; }
}

/* ── Success: the exec indicator's checkmark draws itself in and pops. pathLength=1 normalizes
   the dash math regardless of the path's real length. Stroke is the success state token. ── */
.fc-check {
  display: inline-block;
  transform-box: fill-box;
  transform-origin: center;
  animation: fc-check-pop 0.4s ease-out forwards;
}
.fc-check path {
  fill: none;
  stroke: var(--fc-state-success);
  stroke-width: 3;
  stroke-linecap: round;
  stroke-linejoin: round;
  stroke-dasharray: 1;
  stroke-dashoffset: 1;
  animation: fc-check-draw 0.4s ease-out forwards;
}
@keyframes fc-check-draw {
  to { stroke-dashoffset: 0; }
}
@keyframes fc-check-pop {
  0%   { transform: scale(0.6); }
  60%  { transform: scale(1.15); }
  100% { transform: scale(1); }
}

/* ── Error: a small one-shot shake plus a single ripple ring that settles to the error glow.
   Distinct properties (transform vs box-shadow) compose on the same element. The ripple's
   `forwards` holds the settled glow — including under the reduced-motion blanket. ── */
.fc-exec-error {
  animation: fc-err-shake 0.5s ease-in-out, fc-err-ripple 0.6s ease-out forwards;
}
@keyframes fc-err-shake {
  0%, 100% { transform: translateX(0); }
  20% { transform: translateX(-2px); }
  40% { transform: translateX(2px); }
  60% { transform: translateX(-2px); }
  80% { transform: translateX(2px); }
}
@keyframes fc-err-ripple {
  0%   { box-shadow: 0 0 0 0 var(--fc-glow-error-strong); }
  70%  { box-shadow: 0 0 8px 4px transparent; }
  100% { box-shadow: 0 0 8px 0 var(--fc-glow-error); }
}
```

- [ ] **Step 2: Verify** (file is additive — not imported yet; CSS is bundled, not tree-shaken, so it must still compile clean).
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 3: Commit.**

```bash
git add FlowCanvas/src/nodes/execution-cinematics.css
git commit -m "feat(flow-canvas): execution-cinematics keyframe sheet (halo/check/error + @property angle)"
```

---

## Section 3: BaseBlock node treatments (running + error) + cleanup

### Task 3: Running breathing-halo + error shake/ripple; remove exec-pulse, spin, and the ◌ glyph

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx`
- Modify: `FlowCanvas/src/nodes/baseblock.css`
- Modify: `FlowCanvas/src/styles/reducedMotion.css`
- Modify: `FlowCanvas/e2e/flow-canvas-node-redesign.spec.ts` (one stale comment)

- [ ] **Step 1: Import the cinematics CSS.** Find:

```tsx
import { BlockIcon } from './BlockIcon';
import './baseblock.css';
```

Replace with:

```tsx
import { BlockIcon } from './BlockIcon';
import './baseblock.css';
import './execution-cinematics.css';
```

- [ ] **Step 2: Delete the dead `execGlowColors` map** (its only consumer is rewritten in Step 5). Find and remove the entire block:

```tsx
const execGlowColors: Record<string, string> = {
  running: 'var(--fc-glow-running)',
  success: 'var(--fc-glow-success)',
  error: 'var(--fc-glow-error)',
  skipped: 'var(--fc-glow-skipped)',
  disabled: 'var(--fc-glow-disabled)',
};

```

(Delete the block and its trailing blank line; `heatColor` and the rest stay.)

- [ ] **Step 3: Read `reducedMotion` from the store.** Find:

```tsx
  const heatmapEnabled = useFlowStore((s) => s.heatmapEnabled);
```

Replace with:

```tsx
  const heatmapEnabled = useFlowStore((s) => s.heatmapEnabled);
  const reducedMotion = useFlowStore((s) => s.reducedMotion);
```

- [ ] **Step 4: Restructure `existingBoxShadow`.** Find:

```tsx
  const existingBoxShadow = execState !== 'idle' && execState !== 'disabled'
    ? `0 0 16px ${execGlowColors[execState] || 'none'}`
    : selected
      ? '0 0 12px var(--fc-glow-selected)'
      : 'none';
```

Replace with:

```tsx
  // running + error are class-driven: the fc-exec-running / fc-exec-error animations own the
  // box-shadow via the cascade (CSS animations outrank inline styles), so no inline glow here.
  // success settles to a soft static glow on the INLINE path so the heat ring still stacks;
  // skipped keeps its glow; selection / idle unchanged.
  const existingBoxShadow =
    execState === 'success' ? '0 0 10px var(--fc-glow-success)'
      : execState === 'skipped' ? '0 0 16px var(--fc-glow-skipped)'
        : selected ? '0 0 12px var(--fc-glow-selected)'
          : 'none';
```

- [ ] **Step 5: Replace the inline running-pulse with a state class.** Find:

```tsx
  // Running pulse animation via inline keyframes
  if (execState === 'running') {
    containerStyle.animation = 'exec-pulse 1.5s ease-in-out infinite';
  }
```

Replace with:

```tsx
  // running + error get a state class whose CSS animation owns the card's box-shadow/transform
  // (breathing glow / shake+ripple). success + skipped stay on the inline box-shadow path.
  const stateClass = execState === 'running' ? 'fc-exec-running'
    : execState === 'error' ? 'fc-exec-error'
      : undefined;
```

- [ ] **Step 6: Apply the class to the card div and render the comet child.** Find:

```tsx
  return (
    <div style={containerStyle}>
      {/* Accent rail (category identity; absolutely positioned, out of the boxShadow stack) */}
      <span style={railStyle} data-testid="node-rail" />
```

Replace with:

```tsx
  return (
    <div className={stateClass} style={containerStyle}>
      {/* Accent rail (category identity; absolutely positioned, out of the boxShadow stack) */}
      <span style={railStyle} data-testid="node-rail" />

      {/* Running comet halo: a sweeping conic ring on the card edge. Render-only and gated by
          reduced motion (no comet, no churn when motion is off). inset:0 keeps it inside the
          border-box so it never grows the node or gets clipped. */}
      {execState === 'running' && !reducedMotion && (
        <span className="fc-run-halo" aria-hidden="true" />
      )}
```

- [ ] **Step 7: Drop the `◌` spinner glyph** from the exec indicator. Find:

```tsx
      {execState === 'running' && <span style={{ animation: 'spin 1s linear infinite', display: 'inline-block' }}>◌</span>}
      {execState === 'running' ? 'RUNNING'
```

Replace with:

```tsx
      {execState === 'running' ? 'RUNNING'
```

- [ ] **Step 8: Confirm `@keyframes spin` is now orphaned**, then delete the superseded keyframes from `baseblock.css`.

Run: `cd FlowCanvas && grep -rEn "animation[^;]*\bspin\b|animation-name:\s*spin" src` (PowerShell: `Select-String -Path src\**\*.* -Pattern 'animation[^;]*\bspin\b|animation-name:\s*spin'`)
Expected: **no matches** (the only `spin` animation consumer was the `◌` glyph removed in Step 7; `Properties.tsx` only has a `// Reset testing spinner` comment, not an animation).

Then in `FlowCanvas/src/nodes/baseblock.css`, find:

```css
@keyframes exec-pulse {
  0%, 100% { box-shadow: 0 0 8px var(--fc-glow-running-min); }
  50% { box-shadow: 0 0 24px var(--fc-glow-running-max); }
}
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
.search-match { outline: 2px dashed var(--fc-search-outline) !important; outline-offset: 2px; }
```

Replace with:

```css
.search-match { outline: 2px dashed var(--fc-search-outline) !important; outline-offset: 2px; }
```

- [ ] **Step 9: Refresh the reduced-motion keyframe inventory comment.** In `FlowCanvas/src/styles/reducedMotion.css`, find:

```css
 * Global motion kill switch. The body-level class blankets ALL CSS @keyframes
 * (exec-pulse, spin, fc-packet-travel/fc-packet-pulse, the two inline pulse keyframes) and inline
 * transition:/animation: styles because the !important duration override wins.
```

Replace with:

```css
 * Global motion kill switch. The body-level class blankets ALL CSS @keyframes
 * (the execution-cinematics set fc-run-breathe / fc-run-comet-spin / fc-check-draw / fc-check-pop /
 * fc-err-shake / fc-err-ripple, plus fc-packet-travel / fc-packet-pulse) and inline
 * transition:/animation: styles because the !important duration override wins.
```

- [ ] **Step 10: Fix the stale `exec-pulse` comment** in the redesign spec (the assertion still holds — running now has the breathing animation, `animationDuration` 2.4s > 0). In `FlowCanvas/e2e/flow-canvas-node-redesign.spec.ts`, find:

```ts
    // running → exec-pulse animation present on the first-child card (the rail is a separate child).
```

Replace with:

```ts
    // running → breathing-halo (fc-exec-running) animation present on the first-child card (the rail is a separate child).
```

- [ ] **Step 11: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-node-redesign.spec.ts e2e/flow-canvas-run-timing.spec.ts e2e/flow-canvas-reduced-motion.spec.ts` → all green (running still has a live `animationDuration` > 0; under reduced motion it collapses to < 0.01s; duration badge + heatmap unchanged).
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green (no `node.data`/export change).

- [ ] **Step 12: Commit.**

```bash
git add FlowCanvas/src/nodes/BaseBlock.tsx FlowCanvas/src/nodes/baseblock.css FlowCanvas/src/styles/reducedMotion.css FlowCanvas/e2e/flow-canvas-node-redesign.spec.ts
git commit -m "feat(flow-canvas): running comet+breathing halo & error shake+ripple (class-driven); drop exec-pulse/spin/◌ glyph"
```

---

## Section 4: Success checkmark

### Task 4: Swap the success `✓` for a drawn-in SVG checkmark

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx` (exec indicator success branch)

- [ ] **Step 1: Replace the success label** with the animated SVG checkmark (stroke + draw/pop animations come from `.fc-check` in `execution-cinematics.css`; the parent span already provides the `gap` spacing and success color). Find:

```tsx
      {execState === 'running' ? 'RUNNING'
        : execState === 'success' ? '✓ DONE'
        : execState === 'skipped' ? '— SKIP'
        : '✗ ERROR'}
```

Replace with:

```tsx
      {execState === 'running' ? 'RUNNING'
        : execState === 'success' ? (
          <>
            <svg className="fc-check" viewBox="0 0 24 24" width="11" height="11" aria-hidden="true">
              <path d="M5 13l4 4L19 7" pathLength={1} />
            </svg>
            DONE
          </>
        )
        : execState === 'skipped' ? '— SKIP'
        : '✗ ERROR'}
```

- [ ] **Step 2: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-run-timing.spec.ts e2e/flow-canvas-reduced-motion.spec.ts` → green (the duration badge text `1.5s`/`250ms` and heatmap behavior are unchanged; success node still settles to the soft green glow from Task 3).
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green (the SVG carries no inline color — stroke is the `var(--fc-state-success)` token via the CSS class).
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 3: Commit.**

```bash
git add FlowCanvas/src/nodes/BaseBlock.tsx
git commit -m "feat(flow-canvas): success exec indicator draws an SVG checkmark (draw-in + pop)"
```

---

## Section 5: Count-up duration badge

### Task 5: Live elapsed ticker → settle on the measured duration

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx` (imports, helpers, badge wiring)

- [ ] **Step 1: Add the React hooks to the import.** Find:

```tsx
import { memo, type CSSProperties, useCallback } from 'react';
```

Replace with:

```tsx
import { memo, type CSSProperties, useCallback, useEffect, useState } from 'react';
```

- [ ] **Step 2: Add the `formatDuration` helper + the `useRunningElapsed` ticker hook** at module scope, immediately after the `heatColor` function (before `function BaseBlock`). Find:

```tsx
function heatColor(ratio: number): string {
  const r = Math.max(0, Math.min(1, ratio));
  const from = r < 0.5 ? 'var(--fc-heat-cold)' : 'var(--fc-heat-mid)';
  const to = r < 0.5 ? 'var(--fc-heat-mid)' : 'var(--fc-heat-hot)';
  const pct = Math.round((r < 0.5 ? r * 2 : (r - 0.5) * 2) * 100);
  return `color-mix(in oklch, ${to} ${pct}%, ${from})`;
}
```

Replace with:

```tsx
function heatColor(ratio: number): string {
  const r = Math.max(0, Math.min(1, ratio));
  const from = r < 0.5 ? 'var(--fc-heat-cold)' : 'var(--fc-heat-mid)';
  const to = r < 0.5 ? 'var(--fc-heat-mid)' : 'var(--fc-heat-hot)';
  const pct = Math.round((r < 0.5 ? r * 2 : (r - 0.5) * 2) * 100);
  return `color-mix(in oklch, ${to} ${pct}%, ${from})`;
}

// Single source for the duration format (sub-second → "Nms", else "N.Ns") — shared by the
// settled badge and the live ticker so the value can't drift between running and done.
function formatDuration(ms: number): string {
  return ms < 1000 ? `${ms}ms` : `${(ms / 1000).toFixed(1)}s`;
}

// Live elapsed ticker: while a block runs, formats `now - start` via requestAnimationFrame,
// re-rendering only when the formatted text changes. Returns null when not running, under reduced
// motion, or before `start` is known — the badge then falls back to the settled duration.
function useRunningElapsed(start: number | undefined, isRunning: boolean, reducedMotion: boolean): string | null {
  const [text, setText] = useState<string | null>(null);
  useEffect(() => {
    if (!isRunning || reducedMotion || start == null) {
      setText(null);
      return;
    }
    let raf = 0;
    let last = '';
    const tick = () => {
      const next = formatDuration(Date.now() - start);
      if (next !== last) {
        last = next;
        setText(next);
      }
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [start, isRunning, reducedMotion]);
  return text;
}
```

- [ ] **Step 3: Read `timing` + call the ticker BEFORE the early return** (rules of hooks). Find:

```tsx
  const handleBreakpointToggle = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    toggleBreakpoint(id);
  }, [id, toggleBreakpoint]);

  if (!def) return <div style={{ color: 'var(--fc-state-error-text)' }}>Unknown: {blockData.blockType}</div>;
```

Replace with:

```tsx
  const handleBreakpointToggle = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    toggleBreakpoint(id);
  }, [id, toggleBreakpoint]);

  // Live ticker — a hook, so it MUST run before the early return below (rules of hooks). It reads
  // `start` directly off the timings Map; `timing` itself stays declared in the badge block below.
  const liveText = useRunningElapsed(blockTimings.get(id)?.start, blockData.execState === 'running', reducedMotion);

  if (!def) return <div style={{ color: 'var(--fc-state-error-text)' }}>Unknown: {blockData.blockType}</div>;
```

  > This deliberately does its own `blockTimings.get(id)?.start` rather than reusing the `const timing` below: that `const timing` is declared **after** the early return, so it isn't in scope here, and hoisting it would create a transient duplicate declaration between this step and the next. A second `Map.get` is negligible.

- [ ] **Step 4: Use the shared helper + add `badgeText`** in the existing duration block (leave `const timing` exactly where it is). Find:

```tsx
  // Duration badge
  const timing = blockTimings.get(id);
  const durationMs = timing?.duration;
  const durationText = durationMs != null
    ? durationMs < 1000 ? `${durationMs}ms` : `${(durationMs / 1000).toFixed(1)}s`
    : null;
```

Replace with:

```tsx
  // Duration badge: settled value after completion. While running, the live ticker (read above)
  // drives the badge; on completion it locks to the measured duration.
  const timing = blockTimings.get(id);
  const durationMs = timing?.duration;
  const durationText = durationMs != null ? formatDuration(durationMs) : null;
  const badgeText = execState === 'running' ? liveText : durationText;
```

- [ ] **Step 5: Point the badge span at `badgeText` + add a stable test id.** Find:

```tsx
      {durationText && (
        <span style={{
          fontSize: 8,
          color: 'var(--fc-text-secondary)',
          background: 'var(--fc-surface-0)',
          padding: '1px 4px',
          borderRadius: 3,
          marginLeft: 2,
        }}>
          {durationText}
        </span>
      )}
```

Replace with:

```tsx
      {badgeText && (
        <span data-testid="exec-duration-badge" style={{
          fontSize: 8,
          color: 'var(--fc-text-secondary)',
          background: 'var(--fc-surface-0)',
          padding: '1px 4px',
          borderRadius: 3,
          marginLeft: 2,
        }}>
          {badgeText}
        </span>
      )}
```

- [ ] **Step 6: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0 (`tsc && vite build`; catches type + unused-symbol errors — it does NOT lint hook order, which the new running/count-up specs guard at runtime in Task 6).
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-run-timing.spec.ts e2e/flow-canvas-reduced-motion.spec.ts` → green (after `success`, the badge locks to `durationText` = `1.5s`/`250ms`; under reduced motion no live value appears while running, and the running-animation-collapse assertion is unaffected).
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green (the badge keeps its `var(--fc-*)` inline tokens; `data-testid` adds no style).
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 7: Commit.**

```bash
git add FlowCanvas/src/nodes/BaseBlock.tsx
git commit -m "feat(flow-canvas): live count-up duration badge (rAF ticker) settling to the measured duration"
```

---

## Section 6: Tests & integration

### Task 6: `flow-canvas-execution-cinematics.spec.ts` + extend the token-sweep fixture

**Files:**
- Create: `FlowCanvas/e2e/flow-canvas-execution-cinematics.spec.ts`
- Modify: `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts`

- [ ] **Step 1: Write `flow-canvas-execution-cinematics.spec.ts`** verbatim.

```ts
import { expect, test, type Locator, type Page } from '@playwright/test';
import { createInteractionFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

const nodeById = (page: Page, id: string): Locator => page.locator(`.react-flow__node[data-id="${id}"]`);
const cardOf = (page: Page, id: string): Locator => nodeById(page, id).locator('> div').first();
const motionButton = (page: Page): Locator => page.locator('button[title*="motion" i]').first();

// fc-exec-error sets two comma-separated tracks (shake 0.5s, ripple 0.6s), so animationDuration is
// e.g. "0.5s, 0.6s"; Number.parseFloat intentionally reads only the first track — enough to prove
// live (> 0.01s) vs reduced-motion-collapsed (~0.000001s). fc-exec-running is a single 2.4s track.
async function animationDurationSec(card: Locator): Promise<number> {
  return card.evaluate((el) => Number.parseFloat(getComputedStyle(el as HTMLElement).animationDuration));
}
async function hasReducedMotion(page: Page): Promise<boolean> {
  return page.evaluate(() => document.body.classList.contains('fc-reduced-motion'));
}

test.describe('Flow Canvas Execution Cinematics', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createInteractionFixture());
    await expect(nodeById(page, 'node-1')).toBeVisible();
  });

  test('running: breathing card class + comet halo child, both live', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });

    const card = cardOf(page, 'node-1');
    await expect.poll(() => card.evaluate((el) => el.classList.contains('fc-exec-running'))).toBe(true);
    await expect(nodeById(page, 'node-1').locator('.fc-run-halo')).toHaveCount(1);
    expect(await animationDurationSec(card)).toBeGreaterThan(0.01); // breathing not collapsed
  });

  test('running under reduced motion: no comet child, breathing collapses', async ({ page }) => {
    await motionButton(page).click();
    await expect.poll(() => hasReducedMotion(page)).toBe(true);

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });
    const card = cardOf(page, 'node-1');
    await expect.poll(() => card.evaluate((el) => el.classList.contains('fc-exec-running'))).toBe(true);
    await expect(nodeById(page, 'node-1').locator('.fc-run-halo')).toHaveCount(0); // comet not rendered
    await expect.poll(() => animationDurationSec(card)).toBeLessThan(0.01); // blanket collapses it
  });

  test('success: the exec indicator draws an SVG checkmark and the card settles to a green glow', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 500 });

    const check = nodeById(page, 'node-1').locator('svg.fc-check');
    await expect(check).toHaveCount(1);
    const animName = await check.locator('path').evaluate((el) => getComputedStyle(el as Element).animationName);
    expect(animName).toContain('fc-check-draw');
    // success card settles (0.2s box-shadow transition) to the soft INLINE glow
    // `0 0 10px var(--fc-glow-success)` — poll until stable, then lock the 10px settle radius
    // (running uses 8/20px, error 8px) so the heat-stack / inline-path split can't silently regress.
    await expect
      .poll(() => cardOf(page, 'node-1').evaluate((el) => getComputedStyle(el as HTMLElement).boxShadow))
      .toContain('10px');
  });

  test('error: shake + ripple class is live and collapses under reduced motion', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'error' });
    const card = cardOf(page, 'node-1');
    await expect.poll(() => card.evaluate((el) => el.classList.contains('fc-exec-error'))).toBe(true);
    expect(await animationDurationSec(card)).toBeGreaterThan(0.01);

    await motionButton(page).click();
    await expect.poll(() => hasReducedMotion(page)).toBe(true);
    await expect.poll(() => animationDurationSec(card)).toBeLessThan(0.01);
  });

  test('count-up: the badge ticks up live while running, then locks to the final duration', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });
    const badge = nodeById(page, 'node-1').getByTestId('exec-duration-badge');
    await expect(badge).toBeVisible();

    const readMs = async (): Promise<number> => {
      const t = (await badge.textContent()) ?? '';
      const m = t.match(/([\d.]+)\s*(ms|s)/);
      if (!m) return 0;
      return m[2] === 's' ? Number.parseFloat(m[1]) * 1000 : Number.parseFloat(m[1]);
    };
    // `start` (set in the running handler) and the ticker both use the page's Date.now() clock, so
    // elapsed is monotonic. Settle briefly so `first` is a real mid-run value (not the 0ms mount
    // frame), then prove a strict increase over a 220ms gap (sub-second format is exact ms).
    await page.waitForTimeout(80);
    const first = await readMs();
    await page.waitForTimeout(220);
    const second = await readMs();
    expect(first).toBeGreaterThan(0);
    expect(second).toBeGreaterThan(first);

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 1500 });
    await expect(nodeById(page, 'node-1').getByText('1.5s', { exact: true })).toBeVisible();
  });

  test('count-up under reduced motion: no live value while running', async ({ page }) => {
    await motionButton(page).click();
    await expect.poll(() => hasReducedMotion(page)).toBe(true);

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });
    await page.waitForTimeout(150);
    await expect(nodeById(page, 'node-1').getByTestId('exec-duration-badge')).toHaveCount(0);
  });
});
```

- [ ] **Step 2: Run the new spec.**

Run: `cd FlowCanvas && npx playwright test e2e/flow-canvas-execution-cinematics.spec.ts`
Expected: 6 passed.

- [ ] **Step 3: Extend the token-sweep fixture to cover exec-state nodes.** In `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts`:

First add `postHostMessage` to the harness import. Find:

```ts
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  waitForOutgoingMessage,
} from './support/harness';
```

Replace with:

```ts
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';
```

Then add a three-node exec fixture next to `createSshBlockFixture`. Find:

```ts
// Shared hex/malformed-var scan (Decision #4). Returns the two offender lists so each consumer can
```

Insert immediately ABOVE it:

```ts
// Three nodes so running / success / error can be rendered at once (Wave 2b execution cinematics),
// then re-scanned for raw hex — the comet halo + checkmark mount on these state nodes.
function createExecStateFixture(): GraphFixture {
  return {
    nodes: [
      { id: 'exec-run', type: 'block', position: { x: 80, y: 80 }, data: { blockType: 'send', label: 'Run', props: {} } },
      { id: 'exec-ok', type: 'block', position: { x: 80, y: 220 }, data: { blockType: 'print', label: 'Ok', props: {} } },
      { id: 'exec-err', type: 'block', position: { x: 80, y: 360 }, data: { blockType: 'print', label: 'Err', props: {} } },
    ],
    edges: [],
  };
}

```

Then add the new test as the last test inside the `describe` block. Find:

```ts
    const { hexOffenders, malformedOffenders } = await scanForRawColors(page, DEFAULT_COMMENT_COLOR);

    expect(hexOffenders, hexOffenders.join('\n')).toEqual([]);
    expect(malformedOffenders, malformedOffenders.join('\n')).toEqual([]);
  });
});
```

Replace with:

```ts
    const { hexOffenders, malformedOffenders } = await scanForRawColors(page, DEFAULT_COMMENT_COLOR);

    expect(hexOffenders, hexOffenders.join('\n')).toEqual([]);
    expect(malformedOffenders, malformedOffenders.join('\n')).toEqual([]);
  });

  // Wave 2b: re-run the no-hex scan with nodes in running / success / error so any INLINE [style]
  // on an exec-state node (e.g. the success card's `0 0 10px var(--fc-glow-success)` settle glow)
  // is covered. The comet halo + checkmark colors live in execution-cinematics.css (stylesheet, not
  // inline) — guarded by review + the dist gate, since this DOM [style] scan can't see stylesheet rules.
  test('no raw hex while nodes are running / success / error', async ({ page }) => {
    await loadGraphFixture(page, createExecStateFixture());
    await expect(page.locator('.react-flow__node[data-id="exec-run"]')).toBeVisible();

    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-run', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-ok', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-ok', state: 'success', duration: 300 });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-err', state: 'error' });

    // The comet halo + checkmark are mounted before scanning their styles.
    await expect(page.locator('.react-flow__node[data-id="exec-run"] .fc-run-halo')).toHaveCount(1);
    await expect(page.locator('.react-flow__node[data-id="exec-ok"] svg.fc-check')).toHaveCount(1);

    const { hexOffenders, malformedOffenders } = await scanForRawColors(page, DEFAULT_COMMENT_COLOR);

    expect(hexOffenders, hexOffenders.join('\n')).toEqual([]);
    expect(malformedOffenders, malformedOffenders.join('\n')).toEqual([]);
  });
});
```

- [ ] **Step 4: Verify.**
  - `cd FlowCanvas && npm run build` → exit 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-execution-cinematics.spec.ts e2e/flow-canvas-token-sweep.spec.ts` → all green.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-reduced-motion.spec.ts e2e/flow-canvas-run-timing.spec.ts e2e/flow-canvas-node-redesign.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 5: Commit.**

```bash
git add FlowCanvas/e2e/flow-canvas-execution-cinematics.spec.ts FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts
git commit -m "test(flow-canvas): execution-cinematics render spec + exec-state nodes in token-sweep"
```

### Task 7: Full-suite verification + embedded dist rebuild

**Files:**
- Test: full Playwright suite + parity + dist gate; `dotnet build SSH_Helper.sln`

- [ ] **Step 1: Full e2e suite.** `cd FlowCanvas && npm run test:e2e`. Expect green **except** the known pre-existing parity-CLI **parallel build-lock race** (gesture-smoke / preset-parity / connection-guards / properties-typing fail/flake when concurrent workers collide on `obj/SSH_Helper.dll` via VBCSCompiler/Defender). Confirm those are the only failures.

- [ ] **Step 2: Parity gate (round-trip proof).** `cd FlowCanvas && npm run test:e2e:parity` → **22/22** under `--workers=1`. Because no `node.data`/`exportGraph.ts`/`FlowCanvasBridge.cs` changed, the export is byte-identical.

- [ ] **Step 3: Dist build gate.** `cd FlowCanvas && npm run test:e2e:dist` → green (run `npx playwright test --workers=1 --config playwright.preview.config.ts` if the parity-CLI build-lock race appears). Proves `conic-gradient` + `mask`/`mask-composite` + `@property` + SVG `stroke-dashoffset` survive the production single-asset bundle. **If `@property` angle interpolation misbehaves in the bundle, the approved fallback is rotating the masked ring via `transform: rotate` (same shape, slightly different optics) — re-verify the dist gate after the fallback.**

- [ ] **Step 4: Embedded rebuild.** `dotnet build SSH_Helper.sln` → 0 errors (the `BuildFlowCanvas` target rebuilds + re-embeds `FlowCanvas/dist`).

- [ ] **Step 5: Manual smoke (fresh-eyes pass — PENDING-HUMAN, cannot be automated).** Launch the app, open Flow Canvas, run a script with a few steps. Confirm: a running block shows the sweeping comet ring + breathing glow (no `◌` glyph); the duration badge ticks up live while running; a finished block draws its checkmark in with a pop and settles to a soft green glow (heat ring still stacks); a failed block gives a small shake + one ripple that settles to the error glow; toggle Calm/reduced-motion and confirm the comet disappears, the ticker stops (final value only), and finished/failed nodes still read (drawn checkmark + settled glows). (Subagents: confirm `dotnet build` + dist gate succeed, record this as PENDING-HUMAN.)

- [ ] **Step 6: Commit.**

```bash
git add -A
git commit -m "chore(flow-canvas): rebuild embedded dist for Wave 2b Execution Cinematics"
```

  > `FlowCanvas/dist/` is gitignored (rebuilt at `dotnet build` time), so this records the integration pass like the Wave 1/2a/Live-Wires dist commits. `git add -A` here must add **only** intended files — confirm `git status` shows nothing unexpected and **never** stage `.gitignore` changes or unrelated untracked files; if the tree has stray untracked files, stage explicitly instead.

---

## Wave 2b Execution Cinematics Exit Criteria

- [ ] `cd FlowCanvas && npm run build` exits 0.
- [ ] Running block shows the comet ring + breathing glow; the comet child is **absent** under reduced motion (breathing collapses, soft static glow remains via `forwards`); the `◌` glyph is removed; `@keyframes exec-pulse` (and the orphaned `spin`) are deleted.
- [ ] Success draws the checkmark in with a pop and settles to a soft green `0 0 10px` glow; the heat ring still stacks on a success block.
- [ ] Error gives a small ±2px shake + one ripple that settles to the error glow; both collapse under reduced motion (ripple `forwards` holds the settled glow).
- [ ] Duration badge ticks up live while running and locks to the measured `duration` on completion; final-value-only under reduced motion.
- [ ] **Two** new `--fc-*` tokens only (`--fc-run-comet`, `--fc-glow-error-strong`); no hex outside the token layer (token-sweep green, incl. running/success/error nodes); no `node.data`/`exportGraph.ts`/`FlowCanvasBridge.cs` change; parity **22/22** under `--workers=1`.
- [ ] No new npm runtime dependency (Framer Motion still deferred).
- [ ] New `flow-canvas-execution-cinematics.spec.ts` green; reduced-motion + run-timing + node-redesign specs green; full `npm run test:e2e` green except the known parity-CLI parallel build-lock race (green serialized); dist gate green; `dotnet build SSH_Helper.sln` 0 errors; each task ended with a commit.

## Risks / notes

- **`@property` + `conic-gradient` + `mask` in the production bundle** — the main risk (advanced CSS through Vite + WebView2). Mitigation: the dist gate (`test:e2e:dist`) proves it, exactly as Live Wires proved `offset-path`. Fallback: rotate the masked ring via `transform: rotate` (Task 7 Step 3).
- **CSS-animation-overrides-inline cascade** — relied on for running/error box-shadow; verified behavior. Success deliberately stays on the inline path to preserve heat-ring stacking; do not move it to a class.
- **Reduced-motion static glow via `forwards`** — running's breathing keyframe uses `animation-fill-mode: forwards` so the blanket's forced `iteration-count:1` freezes it on the soft min-glow end frame (otherwise a running node would lose all glow under reduced motion). Error's ripple `forwards` similarly holds the settled error glow. This is intentional and load-bearing.
- **Hook ordering** — `useRunningElapsed` is a hook and MUST be called before the `if (!def)` early return; Task 5 Step 3 places it there. Note `npm run build` is `tsc && vite build` (no ESLint), so it does NOT flag a misplaced hook; the real guard is the new running/count-up specs (a hook after the early return throws React's "rendered fewer hooks than expected" at runtime, failing those tests) plus review.
- **One-shot replay** — success/error animations play on the `execState` transition (class added / SVG mounts). Re-running a node cycles idle→running→result, re-applying the class and re-mounting the SVG, so the one-shot replays with no persistent flag.
- **Live-ticker churn** — re-renders only when the formatted text changes, and only for the single running node (the canvas is a single-host preset debugger); the rAF tears down on stop/unmount. `Date.now()` here is browser code (BaseBlock), unrelated to the workflow-script `Date.now()` restriction.
- **`spin` keyframe** — dropping `◌` orphans `@keyframes spin`; Task 3 Step 8 greps to confirm no other consumer (the lone `Properties.tsx` hit is a `// spinner` comment) before deleting it.
