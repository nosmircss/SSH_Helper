# Flow Canvas — Execution Path Highlight

**Date:** 2026-05-31
**Status:** Design — awaiting spec review before planning
**Scope:** Presentation-layer feature in the Flow Canvas (React). No C# changes. No effect on YAML export.

## Problem

When a preset runs in the Flow Canvas, each **block** already gives durable feedback: it pulses while running, settles to a green check or red shake, and shows duration / loop-count / branch-taken badges that stay on screen after the run. The **connectors (edges)** do not. They light up with a traveling "packet" only while `isRunning` is true, then revert to idle the instant the run ends.

The result: after a run there is no visual of *the route taken* — which branch fired, which connectors were traversed. A user reviewing a finished run can read per-node badges but cannot see the path threaded through the graph at a glance.

## Goal

After (and during) a run, show the **path the execution took** as a persistent highlight on the connectors, with branches that were *not* taken faded back so the route pops.

## Decisions (from brainstorming)

| Decision | Choice |
|----------|--------|
| **Timing** | Build live, then persist — edges light up in order as the run advances and stay lit after it finishes. |
| **Untaken branches** | Dim / fade them — taken path at full strength, untaken branch connectors faded back. |
| **Persistence** | Persist until the next run starts or the canvas reloads (matches how node badges behave today), **plus** a manual clear control. |
| **Clear scope** | The clear control resets **only the edge path** — node badges/results stay on screen. |

## Key insight: the path is already in the store

Exploration confirmed everything needed to reconstruct the path already flows through the existing pipeline and **already persists** after a run:

- `blockStates: Map<nodeId, state>` — every node that ran (`running` / `success` / `error` / `skipped`).
- `branchTaken: Map<nodeId, branchKey>` — which branch each conditional fired (`then` / `else` / `elif/0/then` / `cases/2/do` / `default`).
- `loopIterations: Map<nodeId, number>` — revisit counts.
- Edge metadata: `edge.sourceHandle` and `edge.data.branchPath`, with `graphSlice` helpers `getEdgeBranchPath` / `getBranchVisual` already mapping branches to handles/colors.

These arrive over the existing `execution-update` message (`{ stepId, state, branchTaken, ... }`) and are **not** cleared on `execution-finished`. So:

- **Live** falls out for free — the maps update as messages arrive, so edges transition idle → on-path/untaken as the run proceeds.
- **Persist** falls out for free — the maps survive `execution-finished`; the only reason edges currently revert is that their style is gated on `isRunning`.

**No C# changes are required.**

## Approach

**Derived selector + one gate flag.** Classify each edge's path status on the fly from the persistent store maps (no duplicated path state — respects one-source-of-truth). Add a single `pathVisible` boolean so the manual clear can hide the edge path *without* touching the node badges.

Alternatives considered and deferred:
- *Explicit `edgeTraversed` map* — only needed if we later want ordered trail *replay* synced to the Timeline scrubber. More state to keep consistent. Not now.
- *Record the path C#-side* — unnecessary; the data already arrives over `execution-update`.

## Component design

### 1. Edge path-status selector (new)

A memoized selector classifies every edge into one of three states. Reuses `graphSlice`'s branch helpers; lives in a selectors module alongside the store.

```
selectEdgePathStatus(state, edge) -> 'on-path' | 'untaken' | 'idle'
```

Predicate:

| Result | Condition |
|--------|-----------|
| `idle` | `pathVisible === false`, **or** the edge's source node has no recorded exec state (never ran / still running). |
| `on-path` | source completed **and** one of: (a) source is non-branching → its continuation/successor edge (source state ∈ {success, skipped, disabled}); (b) source is a conditional and this branch edge matches `branchTaken.get(source)`; (c) source is a loop and this is the body edge with `loopIterations.get(source) > 0`; (d) source is a parallel fan-out → **all** its branch edges. |
| `untaken` | source ran, but this edge is a sibling branch of a conditional/loop that did **not** fire. |

**Branch detection and matching must handle two graph origins** (this is load-bearing — the canvas is primarily a preset-builder fed by imported YAML):

- **Canvas-built edges** carry `data.branchPath` (set by `graphSlice`'s `onConnect`/`getBranchVisual`), e.g. `'then'`, `'cases/2/do'`. Detection: `!!branchPath && sourceHandle !== 'continue'`. Matching: `branchPath === branchTaken`.
- **Imported edges** (from `FlowCanvasBridge`) carry **no** `data.branchPath` — only `style.stroke` (color), `label`, and (for `else` only) `sourceHandle='false'`. Their branch identity lives on the **target child node's `props._stepPath`** (e.g. `steps/3/cases/2/do/0`), with `props._isChildOf === source`. Detection: `targetNode.props._isChildOf === edge.source`. Matching: strip the container node's `props._stepPath` prefix (with trailing slash, so `steps/3` ≠ `steps/30`) from the child's `_stepPath`; the edge matches iff the relative remainder equals `branchTaken` or `startsWith(branchTaken + '/')`. This mirrors `FlowCanvasBridge.ExtractBranchKeyFromStepPath` and `branchBands.ts`, but keeps the index (`cases/2`) so it disambiguates switch cases and `then` vs `elif/0/then`.

When a branch edge can't be correlated to a recorded `branchTaken` (e.g. `try` emits none), return `idle` — never guess a false `on-path`. `branchTaken`/`loopIterations` arrive on the container's completion, so branch highlighting appears when the container resolves.

### 2. `AnimatedEdge.tsx` (modify)

The single renderer for all edges. Read `selectEdgePathStatus` and apply styling **decoupled from `isRunning`**:

- **on-path:** the edge's own color at full strength (branch edges keep their branch token; spine edges use the new `--fc-edge-traversed` token), a persistent soft glow, and a slightly heavier stroke. Add class `fc-edge-onpath`.
- **untaken:** fade the stroke via the existing `color-mix` `mix()` helper (~25%). Add class `fc-edge-untaken`.
- **idle:** unchanged from today.

The traveling **packet** stays exactly as-is — it remains gated on `active = isRunning && sourceState ∈ {success, running}`. Packet = live "currently moving" affordance; path glow = durable trail. Two independent conditions.

### 3. Tokens / CSS (token layer only, OKLCH)

- `tokens.css`: add `--fc-edge-traversed` (derived from the existing success/neutral scale; no raw hex). Used for traveled **spine** edges, which today fall back to `--fc-edge-idle`. Branch edges keep their branch token, intensified.
- `.fc-edge-onpath` (persistent glow via `drop-shadow` + weight) and `.fc-edge-untaken` (fade) in the edge stylesheet (`animatededge.css` / `execution-cinematics.css`). **Static** styling — no animation added, so `prefers-reduced-motion` is unaffected.

### 4. `pathVisible` flag + clear control

- Add `pathVisible: boolean` (default `true`) to the execution slice, with `setPathVisible` / a `clearPath()` action.
- On a new run starting (`executionStarted` / `clearExecution`), reset `pathVisible = true` so the next run's path shows.
- `selectEdgePathStatus` returns `idle` for all edges when `pathVisible === false`.
- Add a **"Clear path"** button to the toolbar that calls `clearPath()`. It sets `pathVisible = false` only — node `blockStates`, badges, and timeline are untouched, satisfying the "clear only the edge path" decision.

## Data flow

```
C# ScriptExecutor (StepStarting/StepCompleted, unchanged)
   → execution-update message  { stepId, state, branchTaken, iterationCount }
   → executionSlice: blockStates / branchTaken / loopIterations updated (live)
   → AnimatedEdge re-renders → selectEdgePathStatus(edge) → on-path | untaken | idle
   → persists after execution-finished (style no longer gated on isRunning)

Clear path button → clearPath() → pathVisible=false → selector returns idle for all edges
Next run starts → executionStarted → pathVisible=true (+ blockStates cleared) → fresh path
```

## Edge cases

- **Error mid-run:** the trail lights up to the point execution reached. The failed node already turns red via its `error` state; its outgoing edges never become on-path, so the trail visibly stops at the failure.
- **Loops:** the body edge is on-path once `loopIterations > 0`; the per-node `×N` count badge already conveys revisit count, so the edge stays a boolean highlight (no per-edge counter).
- **Parallel fan-out:** all branch edges of a parallel node are on-path simultaneously (multiple lit trails) — there is no single "taken" branch, so none are faded.
- **Skipped (disabled) nodes:** treated as having run (control flow reached them); their connectors are on-path. The node itself still renders its existing skip style.
- **Imported graphs:** highlight is session-scoped (node IDs / step paths are reconstructed per session). No persistence of path state into YAML or graph JSON.

## Constraints honored

- **Visual change never touches YAML export** — path status is pure presentation, derived from runtime exec state; nothing leaks into `FlowCanvasBridge.ExportToYaml` or persisted graph JSON.
- **No hex outside the token layer; OKLCH tokens only** — new color is a `--fc-*` token; alphas via the `mix()` helper.
- **AnimatedEdge is the single renderer for all edges** — one change covers spine, branch, and loop edges; spine-vs-corridor geometry (`isSpine`) is preserved, path styling applies to both.
- **Run and Debug share the rendering path** — the feature works identically in both; debug just pauses.

## Testing

- **vitest (jsdom):** unit-test `selectEdgePathStatus` against crafted store state for each predicate branch (non-branching continuation, taken vs untaken conditional, loop body, parallel, `pathVisible=false`). Assert `AnimatedEdge` applies the expected class / stroke-token **strings** for each status — jsdom cannot compute `color-mix`/`var()`, so assert at the string level (memory: flow-canvas-vitest-harness).
- **Playwright e2e:** run a scripted graph with a branch; assert (1) the taken-branch edge has `fc-edge-onpath` + heavier stroke, (2) the untaken sibling has `fc-edge-untaken`, (3) the path persists after the run finishes (`isRunning=false`), (4) "Clear path" resets edges to idle while node badges remain, (5) a subsequent run re-shows the path. Edge `d` is read in zoom-independent flow coords (memory: flow-canvas-e2e-gotchas). Update the e2e design contracts alongside, consistent with the existing neon-ring contracts.

## Files touched

| File | Change |
|------|--------|
| `FlowCanvas/src/stores/slices/executionSlice.ts` | `pathVisible` flag, `clearPath()`, reset on run start |
| `FlowCanvas/src/stores/selectors/*` (or `graphSlice`) | `selectEdgePathStatus` (memoized; reuses `getEdgeBranchPath`/`getBranchVisual`) |
| `FlowCanvas/src/nodes/AnimatedEdge.tsx` | apply path status styling, decoupled from `isRunning` |
| `FlowCanvas/src/nodes/animatededge.css` (or `execution-cinematics.css`) | `.fc-edge-onpath`, `.fc-edge-untaken` |
| `FlowCanvas/src/styles/tokens.css` | `--fc-edge-traversed` token |
| `FlowCanvas/src/panels/Toolbar.tsx` | "Clear path" button |
| `FlowCanvas/tests/*` (vitest + Playwright) | unit + e2e coverage and contract updates |

**No C# files change.**

## Out of scope (YAGNI)

- Ordered trail *replay* synced to the Timeline scrubber.
- Per-edge traversal counters.
- Persisting path highlight across sessions or into exported YAML.
- Any new animation/motion on the edges (static highlight only).
