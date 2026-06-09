# Flow Canvas — Loop Iteration Stepper (Design)

**Date:** 2026-06-09
**Status:** Approved (brainstorm complete, pending implementation plan)

## Summary

Post-run, every loop band (Foreach / While / Repeat) gets a control cluster at its top-right that steps through the loop's recorded iterations. Stepping to iteration *k* re-scopes the loop subtree's neon path, branch badges, durations, inner-loop counts, and Block Output to just that iteration — replacing the current behavior where every path taken across all iterations is merged into one aggregate highlight. The aggregate view remains the default ("ALL").

## Background — current state

- Nested steps inside loops fire `StepStarting`/`StepCompleted` on **every** iteration with the **same** `StepPath` (e.g. `steps/2/do/0`); no iteration identity exists anywhere in the event pipeline (`ScriptExecutor.cs`, loop commands).
- `IterationCount` is a post-loop summary on the loop's single `StepCompleted` — it feeds the `×N` badge (`BaseBlock.tsx`) and arrives once, after completion.
- Loop variables (`item`, `item_index`, `_iteration`, …) exist in `ScriptContext` per iteration but reach the canvas only inside full `variables` snapshots.
- React already accumulates per-node output **history** (`blockOutputs: Map<string, BlockOutput[]>` in `executionSlice.ts`, with ◀▶ navigation in `OutputPreview.tsx`) and a chronological `timelineEntries[]` log — but neither is attributable to a specific iteration.
- The post-run path overlay (`selectEdgePathStatus` in `edgePath.ts`, `pathVisible`) marks a loop-body edge `on-path` once `loopIterations > 0`; an `if` inside a loop that took different branches on different iterations lights **both** arms.
- Loop containers already get a `LOOP` band with an interactive label pill, rendered via ViewportPortal in `BranchBandsLayer.tsx`; band geometry comes from `branchBands.ts`.

The gap is **iteration attribution**, not recording infrastructure.

## Decisions (locked during brainstorm)

1. **Post-run replay** — the stepper operates after a run completes; live behavior unchanged.
2. **Full iteration context** — stepping re-scopes path, branch badges, timings, inner `×N`, and Block Output together.
3. **Hierarchical nesting** — an inner loop's stepper ranges over the iterations inside the selected outer iteration; stepping the inner while the outer is on ALL auto-pulls the outer selection along (clusters can never contradict).
4. **Retention: cap per loop, keep latest** — default 500, tunable in the canvas display settings popover, persisted with the other display prefs.
5. **Architecture A: iteration-tagged events** — a loop-iteration stack rides on every step event; rejected canvas-side inference (mis-attributes under break/guarded bodies/parallel) and iteration-indexed StepPath (blast radius on the bridge's core correlation contract).
6. **Control design B, growing to C at N > 20** — context cluster (arrows + loop-variable value + ALL + ⚠ failure-jump), with a bucketed tick-track scrubber appearing along the band's top edge for loops with more than 20 iterations.

## UX contract

**Appearance.** `[ALL] [◀ web-02 · 3/12 ▶] [⚠ 2]` at the loop band's top-right, styled like the existing band pills, viewport-space (scales with zoom, moves with band drag). Rendered only when the run is finished **and** the loop recorded ≥ 1 iteration. Hidden while `isRunning`; cleared by `clearExecution()` on the next run.

**Default = ALL.** Post-run view is exactly today's aggregate. The ALL chip is both reset button and mode indicator (highlighted when active).

**Stepping to iteration k** re-scopes the loop's subtree only:
- Edges glow only if traversed during k; others render with the existing faded-dashed untaken look.
- Nodes not reached in k drop to idle appearance; reached nodes show k's exec state.
- Branch badges show k's `branchTaken`; duration chips show k's timing; inner loops' `×N` badges show k's inner count.
- Block Output jumps to k's entry (`historyIndex`) for the selected block.
- Everything outside the loop's subtree keeps the aggregate view.

**Label.** Foreach shows the stringified item value (truncated for display, full value on hover); While/Repeat show `#k`. The `⚠ n` chip counts failed iterations, cycles through them on click, and is hidden when none failed.

**Nesting.** Outer at iteration i → inner stepper ranges over the inner iterations recorded inside i. Outer at ALL → inner ranges over all its iterations in time order, and stepping it sets the outer selection to the containing iteration (outer cluster updates to match).

**Scrubber (N > 20).** A tick-track along the band's top edge, in the dead space between the LOOP pill and the cluster — the cluster itself never widens. Track width capped; above ~60 ticks each tick is a bucket (red if any iteration in it failed). Click jumps to the bucket's first retained iteration; hover tooltip shows `label · k/N`; drag scrubs.

**Retention display.** Past the cap, oldest records drop; the counter reads like `3/500 of 8,213`; ticks and ⚠ cover retained iterations only.

**Interactions.** "Clear Path" also resets all iteration selections to ALL. Zero-iteration loops (`×0`) show no cluster.

## Architecture

### C# (additive only)

- `ScriptContext` gains an **iteration stack**: frames of `(loopStepPath, index, itemLabel?)`. `ForeachCommand`/`WhileCommand`/`RepeatCommand` push a frame on loop entry, bump `index` (and `itemLabel` for foreach) each iteration, pop on exit — same threading pattern as `context.LoopDepth`.
- `itemLabel` is the stringified foreach item, capped (~48 chars) on the C# side; null for While/Repeat.
- `ScriptExecutor` snapshots the stack (**copy, not reference** — it mutates) onto `StepStarting`/`StepCompleted` event args as `IterationStack`.
- `IterationCount`-at-completion behavior is untouched (the `×N` badge keeps working as today).

### Bridge (Form1 / FlowCanvasForm)

- Form1's step handlers map each frame's loop step-path to its canvas node id (the same path→nodeId mapping already used for `stepId`) and attach `iterationStack: [{loopId, i, label?}]` to the existing `execution-update` and `step-output` messages. No new message types; the field is optional and ignored by anything that doesn't know it.

### React store

New compact transient state (extend `executionSlice` or a sibling slice), built incrementally from incoming messages:

```
iterationLog: Map<loopNodeId, IterationRecord[]>
IterationRecord = {
  i: number,
  label?: string,
  failed: boolean,
  parent: { loopId: string, i: number } | null,
  nodes: Map<nodeId, { state, branchTaken?, duration?, outputIdx? }>
}
iterationSelections: Map<loopNodeId, number | null>   // null = ALL
totalIterations: Map<loopNodeId, number>               // survives eviction, for "of 8,213"
```

- Each event writes into the records of **every frame on its stack**: the innermost record gets the exact per-occurrence values; ancestor records aggregate (error state is sticky, otherwise last write wins — incl. `outputIdx`, so a partially-scoped view shows the block's last output within that outer iteration). This is what makes "outer at iteration 2, inner at ALL" correct: the outer record's `nodes` map covers inner-body nodes with values aggregated over the inner iterations that ran inside outer 2. Failures propagate `failed = true` up all ancestor records. `parent` is the second-innermost frame — it makes hierarchical scoping a lookup, not a search.
- Output **text** stays single-sourced in `blockOutputs[]`; records store only `outputIdx` (the index of the entry appended for that occurrence).
- Eviction at the cap drops oldest records (and their `nodes` maps); `totalIterations` keeps counting.
- Nothing writes to `node.data` — exports and YAML are untouched (same rule as `loopIterations`/`branchTaken` today).

### Selectors

- `selectIterationScope(nodeId)`: memoized resolver walking the node's ancestor loops; returns the governing record or null (= aggregate). The **innermost ancestor loop with a non-ALL selection** governs; thanks to the write-to-all-frames rule its record answers for every node beneath it.
- `selectEdgePathStatus` consults the scope first: scoped → `on-path` iff the edge's target was reached in that record; unscoped → existing aggregate logic.
- BaseBlock reads scoped variants for exec-state chip, branch badge, duration, and inner `×N`.
- `OutputPreview` effect: scope change → set `historyIndex` from the record's `outputIdx` for the selected node.

### Components

- **`IterationClusterLayer`** — third sibling in `BranchBandsLayer`'s ViewportPortal loop (alongside band rectangle and label pill; that's the established precedent), `pointerEvents: auto`, z-order above pills. Renders only for loop bands (`branchKey 'do'`) with records, positioned from the band geometry `branchBands.ts` already computes.
- Cluster sub-elements: ALL chip, ◀ ▶ arrows, label/counter, ⚠ chip, and (N > 20) the tick-track.

## Edge cases

- **`break` / `continue` / mid-iteration error:** attribution stays exact (stack rides each event); the record simply has fewer nodes. Failed iterations are what ⚠ navigates.
- **Re-run / mid-run:** `clearExecution()` clears `iterationLog`, `iterationSelections`, `totalIterations`; cluster hidden while running.
- **Parallel arms:** loops in different arms get independent clusters; event interleaving is irrelevant because attribution is carried per event. **Verify-item:** confirm `ParallelCommand` gives each arm an isolated context (or clone the stack per arm) — a shared mutable stack across concurrent arms would corrupt frames. Dedicated test required.
- **Unknown loop ids** (e.g. a `call`'d sub-script's loops with no canvas node): frames that don't resolve to a node are ignored gracefully.
- **Eviction mid-browse:** selections clamp to retained range.
- **Layout/zoom:** viewport-space like the pills; unaffected by Auto-flow vs Manual layout mode.
- **Reduced motion:** cluster renders identically; no animation dependencies.

## Settings & persistence

- `FlowCanvasIterationHistoryCap` (default 500) added to the canvas display settings popover; persisted to `WindowState` via the existing `pref-save` path like other display settings.
- Iteration selections are session-transient (not persisted).

## Testing

- **C# (xUnit):** loop commands push/bump/pop correctly across break/continue/error exits; event args carry copies (mutation-safe); nested loops produce correct stacks; parallel-arm context isolation.
- **React (vitest, real-store style):** `iterationLog` built from realistic message sequences (nested loops, failures, eviction at the cap); scope resolver incl. inner-pulls-outer; `OutputPreview` historyIndex sync; cluster render states (ALL / stepped / ⚠ hidden when no failures / `of N` eviction text / scrubber appears at 21).
- **Playwright e2e:** simulate a 3-host run with an `if` taking different branches per iteration → step → assert per-iteration edge classes (use `toHaveCount`, not `toBeVisible`, per known canvas e2e gotchas), badge text, Block Output index; reduced-motion parity.

## Out of scope (noted for later)

- Live mid-run iteration browsing (the recording model built here supports it if ever wanted).
- Variables-panel time-travel per iteration (the existing timeline scrub could be tied to iteration boundaries later).
- Keyboard arrow-key stepping when the cluster is focused — nice-to-have, not required.

## Risks

- **ParallelCommand context sharing** (above) — the one place attribution could silently break; gated by a dedicated test before the feature is considered done.
- **Memory at scale** — bounded by the retention cap; `blockOutputs[]` and `timelineEntries[]` growth for huge loops is pre-existing behavior, not made worse by this feature (records store indices, not copies).
