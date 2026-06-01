# Flow Canvas Wave 2b — Loop & Branch Instrumentation Design

**Date:** 2026-05-30
**Status:** Approved (brainstorming) — ready for implementation plan
**Branch:** `0.51.21`
**Initiative:** Flow Canvas "flashy + feature-rich" enhancement — Wave 2b, **third cycle** (follows Live Wires + Execution Cinematics); the **first cross-stack cycle** (C# **and** React).

---

## Goal

Give the canvas the **execution DATA** it currently lacks: how many times a loop actually ran, and which branch a conditional actually took. Today both facts exist only as locals/debug strings deep inside the command handlers and never leave the C# engine. This cycle plumbs two new summary facts — `iterationCount` and `branchTaken` — from the command, through the existing `StepExecutionEventArgs` → `execution-update` message protocol, into transient React store Maps, and surfaces them as a **single static tokenized badge** on the loop/branch node (`×5`, `else`, `case #3`).

This is the **instrumentation layer** the later cycle (inline result chips + edge value pills) depends on. It is deliberately the **data plumbing + a minimal visual readout** — NOT the chips/pills, NOT live per-iteration ticking, NOT edge highlighting. Those stay deferred; the `branchTaken` scope-key is chosen so a later cycle can light the matching edge **for free**.

Unlike the prior two 2b cycles (pure-frontend), this one touches C#, so `dotnet build` + `dotnet test` are gates alongside the frontend ones.

## Scope

**In scope (this cycle — "Loop & Branch Instrumentation"):**
- **C# data layer:** add `IterationCount` (`int?`) + `BranchTaken` (`string?`) to `CommandResult` and to `StepExecutionEventArgs`; populate them in the loop commands (`foreach`/`while`/`repeat` → final iteration count) and the branch commands (`if`/`switch` → scope-path key); the executor copies them onto the `StepCompleted` event arg at the existing build site.
- **Message protocol:** two additive optional fields (`iterationCount`, `branchTaken`) on the existing `execution-update` (StepCompleted) message. **No new event, no new message kind.**
- **React store:** two new **transient** `executionSlice` Maps (`loopIterations`, `branchTaken`), their setters, init, and `clearExecution` reset; parse the two fields in `messageBridge`; an optional `ExecutionUpdateMessage` TS interface for type safety.
- **Minimal visual readout:** a single **static** tokenized chip rendered in `BaseBlock` beside the existing exec-indicator/duration badge — `×N` for loop nodes, a derived label (`else`, `elif #1`, `case #3`, `default`) for branch nodes. Appears on completion (the data arrives with `StepCompleted`).
- C# unit + executor-event tests; React `messageBridge`/store/render tests; token-sweep extension.

**Explicitly deferred (later cycles — NOT this spec):**
- **Live per-iteration ticking** (`3 / 5` updating mid-run). Rejected in brainstorming (D1): would require a new mid-execution event on `ScriptExecutor` + a new `loop-iteration` message kind + all three loop commands firing per pass. This cycle is **final/summary** only.
- **`try`/`catch` `branchTaken`** (D3): only `if` + `switch` emit `branchTaken` this cycle. The error-path branch fact is deferred.
- **Taken-edge highlight / inline result chips / edge value pills** (D4): the canvas shows a node badge only. The `branchTaken` scope-key still lands in the store Map, so the later edge/chips cycle gets the edge match free.
- **Badge animation** (D4): the chip is static — no new keyframe, no new reduced-motion surface.
- **`parallel` reporting:** `parallel` runs all branches, so there is no branch to report; it emits nothing.
- **Framer Motion / `motion`** — still deferred; this cycle adds **no new npm runtime dependency** and **no new CSS animation**.

## Resolved decisions (from brainstorming)

| # | Decision | Choice |
|---|----------|--------|
| D1 | How the canvas learns the facts | **Final/summary.** Extend `CommandResult` + `StepExecutionEventArgs`; read at the existing `StepCompleted`; roll into the existing `execution-update` message. No new event/message kind, no live ticking. |
| D2 | `branchTaken` representation | **Scope-path key string** matching `edge.data.branchPath` / C# `StepPath` vocabulary: `then`, `else`, `elif/{i}/then`, `cases/{i}/do`, `default`. Machine-matchable → future edge-highlight is free; the canvas derives a short display label this cycle. `iterationCount` is a plain `number`. |
| D3 | Which blocks emit what | `iterationCount`: `foreach` / `while` / `repeat`. `branchTaken`: `if` / `switch` (try/catch deferred). |
| D4 | Minimal visual readout | **Node badge only, static.** Tokenized chip beside the exec indicator; no edge work, no AnimatedEdge change, no new keyframe. |
| D5 | C# plumbing channel | **Extend `CommandResult`** (the channel the executor already consumes at `StepCompleted`). No new per-step `ScriptContext` fields, no context-variable scraping. |

## Current state (grounding — verified facts, do not re-derive)

### C# event surface
- **`StepExecutionEventArgs`** (`ScriptExecutor.cs:13-41`) has exactly 9 `init`-only props: `StepIndex`, `StepPath`, `StepType`, `LineNumber`, `StepName`, `Success`, `Output`, `DurationMs`, `Skipped`. **No iteration/branch field exists.**
- **`StepStarting`** is raised once per step at `ScriptExecutor.cs:332-339`. **`StepCompleted`** is raised at three sites: disabled-node (`:299-307`), guard-false (`:318-326`), and after execution (`:346-356`). The third site is built from `var result = await ExecuteStepAsync(step, …)` (`:342`) and already reads `result.Success` (`:353`) + a stopwatch `sw.ElapsedMilliseconds` (`:355`).
- **`ExecuteStepAsync` → `ExecuteStepCoreAsync` dispatches to the command and returns its `CommandResult` unmodified** (`ScriptExecutor.cs:465`: `return await command.ExecuteAsync(step, context, cancellationToken);`). So any prop the command sets on its returned result survives to `:346-356`. **One clean copy site, no executor restructuring.**
- The loop/branch **statement** node fires `StepStarting`/`StepCompleted` once. Its child steps fire their own events (the executor re-fires `StepStarting` per child, and `ForeachCommand` re-invokes the children per iteration) — but the **count/branch is a property of the statement node**, attached at its own `StepCompleted`.

### C# `CommandResult` (the plumbing channel)
- `CommandResult` (`IScriptCommand.cs:26-129`) is a plain mutable class — `bool Success`, `string? Message`, `bool ShouldExit/ShouldBreak/ShouldContinue/ShouldReturn`, `ScriptExitStatus ExitStatus`, `bool SuppressedError` — with static factories `Ok`/`Fail`/`Break`/`Continue`/`Return`/`Suppressed`/`Exit`. Trivially extended with two optional props.

### C# loop/branch commands — where the facts live, and the return-path footgun
- **Iteration counters are locals + context vars:** `ForeachCommand` (`IterateAsync:86-162`) uses local `index` (`:116`) with total `count` (`:86/90`); `WhileCommand` local `iteration` (`:33`, increment `:82`); `RepeatCommand` local `iteration` (`:35`, increment `:57`). All also write context vars (`{var}_count`, `_iteration`) but **nothing reaches an event**.
- **Branch decisions are local bools, no context var:** `IfCommand` (`:28` `result`, elif loop `:44-63`, else `:68-72`, final `:76`); `SwitchCommand` (`:34` `matches`, cases loop `:28-65`, default `:69-73`, no-match `:77`). Only emitted as debug strings (`IfCommand.cs:30/52`, `SwitchCommand.cs:57`).
- **Footgun (load-bearing for the plan):** these commands have **multiple return paths**, and several construct a **fresh `CommandResult.Ok()`** rather than passing the child result through — `ForeachCommand:149` (normal end), `IfCommand:63` (elif matched) and `:76` (final), `SwitchCommand:63` (empty case) and `:77` (no match). A field set only on the pass-through paths would be **lost** on the fresh-`Ok()` paths. The fix is to set the field on **every** exit path (see Architecture).
- **Branch scope vocabulary already matches the canvas:** `StepPath` is assigned pre-run with scope segments `then / else / elif/{i}/then / cases/{i}/do / default / catch / finally` (`ScriptExecutor.cs:527-584`), and these are exactly the `edge.data.branchPath` values the canvas tags edges with (Live Wires grounding). So a command reporting `"elif/0/then"` is directly edge-matchable later.

### C# → React bridge
- `StepStarting` handler `Form1.cs:13547-13552` → `new { type = "execution-update", stepId = nodeId, state }`. **Carries nothing new this cycle** (final/summary).
- `StepCompleted` handler `Form1.cs:13573-13580` → `new { type = "execution-update", stepId = nodeId, state, duration = e.DurationMs, variables }`. **This is where `iterationCount = e.IterationCount` + `branchTaken = e.BranchTaken` are added.** `e` is the `StepExecutionEventArgs`.
- Serialized by `JsonConvert.SerializeObject` with **default settings** (`FlowCanvasForm.cs:301`) → `PostWebMessageAsJson` (`:318`). Default `NullValueHandling.Include` emits unset fields as JSON `null`; the parity CLI's `NullValueHandling.Ignore` is a **separate** serializer and irrelevant here. Adding initializer keys is sufficient — **no DTO/schema to touch**.
- `nodeId` resolves via `TryResolveCanvasNodeId` (`Form1.cs:13686-13735`, `stepPath`-first with `stepIndex` fallback). Node ids are `node-{n}` (`FlowCanvasBridge.cs:329`); `_stepPath` is stored in `data.props._stepPath` (`FlowCanvasBridge.cs:378`).

### React message + store
- `executionUpdate = 'execution-update'` constant (`communication-message-types.ts`, the `CANVAS_HOST_MESSAGES.incoming` block). The incoming message is parsed loosely (no strict TS type today).
- `messageBridge.ts:190-237` validates `stepId`/`state`, then calls `setBlockState(stepId, execState)` (`:206`) and `setBlockTiming` (`:209-225`); `variables`/`changedKeys` route to a separate `VariableSlice` action (`:234-237`).
- `executionSlice.ts` holds **pure transient Maps**: `blockStates: Map<string,BlockExecState>` (`:22`), `blockOutputs` (`:23`), `blockTimings: Map<string,{start;end?;duration?}>` (`:24`); initialized at `:37-42`; `setBlockTiming` at `:70-76`; `clearExecution` resets all Maps at `:78-91`.
- **`blockTimings` is the gold-standard transient pattern:** a store Map, read at render via `useFlowStore` selector (`BaseBlock.tsx:68`), **never written to `node.data`** (`setBlockTiming` does *not* call `updateNodeData`). `BaseBlock` already reads `heatmapEnabled`/`reducedMotion`/`maxDuration` selectors (`:69-79`) and renders the exec indicator + duration badge at `:204-245`.
- An existing branch-key helper `branchColorVar(key)` and `branchKeyFromStepPath` live in `utils/branchBands.ts:23/35`, but `branchKeyFromStepPath` reads `node.data` metadata for **layout bands** — a different concern. The badge gets its own tiny label helper.

### Round-trip boundary (the hard gate)
- `exportGraph.ts` `stripDefaultProps` (`:60-101`) serializes **`node.data.props` only**; `buildExecutableGraphPayload` calls it at `:132`. It never reads `node.position`, `node.style`, `node.data.execState`, or any store Map. `BlockNodeData` (`BaseBlock.tsx:10-20`) = `{ blockType, label?, props?, execState?, breakpoint?, [k]:unknown }`.
- **The single rule (confirmed):** a new instrumentation field rides in an `executionSlice` Map keyed by `nodeId` — **never** `node.data.props` (and, following `blockTimings`, **never** `node.data` at all). Store-only Maps are excluded from export by construction.
- Parity bundle (`flow-canvas-preset-parity` / `preset-negative` / `gesture-smoke` / `connection-guards`) MUST stay **22/22 under `--workers=1`** — the round-trip proof (`package.json` `test:e2e:parity`, `--workers=1`).

## Architecture

End-to-end data flow (all additive; the only "new" runtime object is two store Maps):

```
foreach/while/repeat  ──set IterationCount on returned CommandResult──┐
if/switch             ──set BranchTaken   on returned CommandResult──┤
                                                                     ▼
ScriptExecutor.cs:346-356  (StepCompleted)  reads result.IterationCount/BranchTaken
                                                                     ▼
StepExecutionEventArgs { …, IterationCount, BranchTaken }            (ScriptExecutor.cs:13-41 +2)
                                                                     ▼
Form1.cs:13573-13580  →  execution-update { …, iterationCount, branchTaken }   (anonymous obj +2 keys)
                                                                     ▼
messageBridge.ts:190-237  →  setLoopIteration(id,n) / setBranchTaken(id,key)
                                                                     ▼
executionSlice  loopIterations: Map<string,number> ; branchTaken: Map<string,string>   (transient, never on node.data)
                                                                     ▼
BaseBlock.tsx  selectors → static tokenized chip  (×N  |  derived label)
```

### Field contract

| Layer | Loop fact | Branch fact |
|-------|-----------|-------------|
| `CommandResult` (`IScriptCommand.cs`) | `int? IterationCount` (null = not a loop; `0` = ran zero times) | `string? BranchTaken` (null = not a branch / no branch ran) |
| `StepExecutionEventArgs` (`ScriptExecutor.cs:13-41`) | `int? IterationCount { get; init; }` | `string? BranchTaken { get; init; }` |
| `execution-update` JSON | `iterationCount: number \| null` | `branchTaken: string \| null` |
| React store Map | `loopIterations: Map<string, number>` | `branchTaken: Map<string, string>` |
| Emitted by | `foreach` (`count`), `while`/`repeat` (`iteration`) | `if` (`then`/`elif/{i}/then`/`else`), `switch` (`cases/{i}/do`/`default`) |

### C# command changes — set on every exit path (the elegant form)

Each command holds **one local** for its fact and assigns it onto whatever `CommandResult` it returns, at **every** `return` — including the fresh-`Ok()` paths the grounding flagged. Concretely:

- **`ForeachCommand` / `WhileCommand` / `RepeatCommand`:** the loop counter is already a local (`index`/`count`/`iteration`). At each return (early-fail, control-flow pass-through `ShouldExit`/`ShouldReturn`, `ShouldBreak`, error, normal completion), set `result.IterationCount = <count-executed>` before returning. Normal-completion fresh `Ok()` gets the total; break/error gets the partial count. Empty foreach → `0`.
- **`IfCommand`:** a local `string? branchTaken = null;`. Set it the moment a branch is chosen — `"then"` (`:32`), `$"elif/{i}/then"` with the enumerated elif index (`:44-63`), `"else"` (`:68`). At **every** return (`:39`, `:60`, `:63`, `:72`, `:76`) assign `result.BranchTaken = branchTaken` (still `null` at `:76` when no branch ran). This kills the `:76` ambiguity the grounding called out.
- **`SwitchCommand`:** a local `string? branchTaken = null;` set to `$"cases/{i}/do"` on the matched case (`:55-63`, tracking the case index in the `:28-65` loop) or `"default"` (`:69-73`); assigned onto the result at every return (`:61`, `:63`, `:72`, `:77`).

`ScriptExecutor.cs:346-356` then copies both: `IterationCount = result.IterationCount, BranchTaken = result.BranchTaken`. (The two skipped-step `StepCompleted` sites at `:299-307`/`:318-326` leave both unset — a skipped loop/branch reports no count/branch, which is correct.)

### React store + render

- `executionSlice`: add `loopIterations: Map<string,number>` and `branchTaken: Map<string,string>` to the interface (`:20-35`), the initial state (`:37-42`), and `clearExecution` (`:78-91`). Add `setLoopIteration(id,n)` and `setBranchTaken(id,key)` modeled **exactly** on `setBlockTiming` (`:70-76`) — new `Map(s.X)`, `.set`, return — and **never** calling `updateNodeData`.
- `messageBridge.ts` (after `setBlockState`, `:206`): parse `iterationCount` (`msg.iterationCount != null ? Number(...) : undefined`, guard `Number.isFinite` and `>= 0`) and `branchTaken` (`typeof msg.branchTaken === 'string' ? trim : undefined`, guard non-empty); call the new setters only when valid. Null/absent → no-op (no instrumentation).
- `BaseBlock.tsx`: add selectors `const loopIteration = useFlowStore((s) => s.loopIterations.get(id))` and `const branchTakenKey = useFlowStore((s) => s.branchTaken.get(id))` (named `…Key` to avoid shadowing the slice Map). In the exec-indicator JSX (`:204-245`), render — beside the duration badge — a static tokenized chip: loop nodes show `×{loopIteration}` (`data-testid="exec-loop-badge"`); branch nodes show `deriveBranchLabel(branchTakenKey)` (`data-testid="exec-branch-badge"`). A node has at most one (loops emit only the count, branches only the key).
- **`deriveBranchLabel(key)`** — a tiny module-scope helper in `BaseBlock.tsx`: `else→"else"`, `then→"then"`, `default→"default"`, `cases/{n}/do→"case #{n+1}"`, `elif/{n}/then→"elif #{n+1}"`, fallback → the raw key. Covered by the render spec (extract to `utils/` only if it gets reused).
- Chip styling reuses the existing duration-badge tokens (`var(--fc-surface-0)`, `var(--fc-text-secondary)`, etc.) — **no new token, no hex**. (Branch-color tinting via the existing `--fc-branch-*` map is a possible later polish, explicitly out of scope here.)
- `communication-message-types.ts`: keep the constant; add an optional `ExecutionUpdateMessage` interface (`stepId`, `state`, `duration?`, `variables?`, `changedKeys?`, `iterationCount?`, `branchTaken?`) and have `messageBridge` treat the message as that type for safety.

## Message protocol contract

The **existing** `execution-update` message (StepCompleted variant) gains two optional, additive fields. Nothing else changes; older/other senders that omit them are unaffected.

```jsonc
// execution-update (StepCompleted)
{
  "type": "execution-update",
  "stepId": "node-7",
  "state": "success",          // unchanged: running | success | error | skipped
  "duration": 1234,            // unchanged: ms (nullable)
  "variables": { /* … */ },    // unchanged
  "iterationCount": 5,         // NEW — number|null; present for foreach/while/repeat
  "branchTaken": "else"        // NEW — string|null; scope-key for if/switch
}
```

- **StepStarting** `execution-update` is **unchanged** (no `iterationCount`/`branchTaken`).
- `null`/absent both mean "no instrumentation" on the React side (identical in JS).
- `branchTaken` values are exactly `then | else | elif/{i}/then | cases/{i}/do | default` — the canvas `edge.data.branchPath` vocabulary.

## Round-trip safety (the hard gate)

- The two new facts are **runtime-only**. `execution-update` is a live event message, **never** part of the export path (`exportGraph.ts` → `FlowCanvasBridge.ExportToYaml`). The React fields live in `executionSlice` Maps (`loopIterations`, `branchTaken`), read at render, **never** written to `node.data`/`node.data.props`. `exportGraph` reads `node.data.props` only (`:60-101`). **Export is byte-identical by construction.**
- Parity bundle stays **22/22 under `--workers=1`** — the proof. The C# change adds fields to an event/message only; it does not touch `FlowCanvasBridge.cs` export, `exportGraph.ts`, or any `node.data` write.
- No hex outside the token layer: the chip uses only existing `var(--fc-*)` tokens. Token-sweep gate stays green (extended to a node carrying instrumentation).

## Reduced-motion

The badge is **static** (D4) — no new keyframe, no transition. Nothing new to gate; the `.fc-reduced-motion` blanket is unaffected. (The render spec still asserts the badge renders identically under `.fc-reduced-motion`, confirming we added no motion.)

## Cleanup (delete-before-build)

This is an **additive** cycle — no superseded code to remove (contrast the cinematics cycle, which deleted `exec-pulse`). The implementation must still run a dead-code grep sweep afterward to confirm no orphaned imports/vars were introduced (e.g. an unused selector if a render path is reworked).

## Files

**New**
- `FlowCanvas/e2e/flow-canvas-loop-branch-instrumentation.spec.ts` — render spec (below).
- `SSH_Helper.Tests/Scripting/LoopBranchInstrumentationTests.cs` — C# unit + executor-event tests (below). *(Or fold into the existing fixtures — see Testing.)*

**Modified — C#**
- `Services/Scripting/Commands/IScriptCommand.cs` — add `int? IterationCount` + `string? BranchTaken` to `CommandResult` (`:26-129`).
- `Services/Scripting/ScriptExecutor.cs` — add `IterationCount`/`BranchTaken` to `StepExecutionEventArgs` (`:13-41`); copy `result.IterationCount`/`result.BranchTaken` at the `StepCompleted` build site (`:346-356`).
- `Services/Scripting/Commands/ForeachCommand.cs` — set `IterationCount` on every return (`IterateAsync:137/140/146/149`).
- `Services/Scripting/Commands/WhileCommand.cs` — set `IterationCount` on every return (`:24/27/62/65/80/90`).
- `Services/Scripting/Commands/RepeatCommand.cs` — set `IterationCount` on every return (`:25/28/45/55/72`).
- `Services/Scripting/Commands/IfCommand.cs` — local `branchTaken`; set on every return (`:39/60/63/72/76`).
- `Services/Scripting/Commands/SwitchCommand.cs` — local `branchTaken` (+ case index); set on every return (`:61/63/72/77`).
- `Form1.cs` — add `iterationCount = e.IterationCount, branchTaken = e.BranchTaken` to the StepCompleted `execution-update` object (`:13573-13580`).

**Modified — React**
- `FlowCanvas/src/stores/slices/executionSlice.ts` — two new Maps + setters + init + `clearExecution` reset.
- `FlowCanvas/src/stores/messageBridge.ts` — parse `iterationCount`/`branchTaken`, call the new setters (`:190-237`).
- `FlowCanvas/src/nodes/BaseBlock.tsx` — two selectors + `deriveBranchLabel` + the static chip in the exec indicator (`:204-245`).
- `FlowCanvas/src/communication-message-types.ts` — optional `ExecutionUpdateMessage` interface.
- `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts` — drive a node to completion **with** `iterationCount`/`branchTaken` and re-scan (the chip is covered by the no-hex gate).

## Testing

**C# (`dotnet test` gate):**
- **Command-level** (model on `ForeachCommandTests.cs`, `ScriptRepeatLoopTests.cs`, `ScriptExecutorControlFlowTests.cs`): run a `foreach` over a 3-item list → returned/observed `IterationCount == 3`; empty collection → `0`; `while`/`repeat` of known length → matching count; `break` mid-loop → partial count. `if` true → `BranchTaken == "then"`; elif #2 matches → `"elif/1/then"`; else → `"else"`; condition false, no else → `null`. `switch` case 3 → `"cases/2/do"`; default → `"default"`; no match → `null`.
- **Executor-event** (model on `ScriptExecutorStepPathTests.cs`, which already captures `StepCompleted` by step path): run a script containing a `foreach` and an `if`, capture `StepCompleted` args, assert the foreach node's `IterationCount` and the if node's `BranchTaken` arrive on the event arg — proving the `:346-356` copy + the every-return-path discipline.

**React (`npm run build` + e2e):**
- **New `flow-canvas-loop-branch-instrumentation.spec.ts`** (drive `execution-update` exactly as the cinematics/live-wires specs do): (a) a loop node sent `state:'success', iterationCount:5` shows `×5` (`exec-loop-badge`); `iterationCount:0` shows `×0`; absent → no loop badge. (b) an if node sent `branchTaken:'else'` shows `else`; `'cases/2/do'` shows `case #3`; `'elif/0/then'` shows `elif #1`; absent → no branch badge. (c) `clearExecution` (re-run) clears both badges. (d) the badge renders identically under `.fc-reduced-motion` (proving no motion added).
- **Store/bridge unit:** `messageBridge` test — an `execution-update` with `iterationCount`/`branchTaken` populates `loopIterations`/`branchTaken` Maps; malformed values (NaN, negative, empty string) are ignored. `executionSlice` test — setters update the Maps without touching `node.data`; `clearExecution` resets them.
- **Token-sweep gate** stays green, extended to a node carrying the chip (no hex, no `var()`+alpha concat).
- **Parity** 22/22 under `--workers=1` — export byte-identical (the round-trip proof).
- **Dist gate** (`test:e2e:dist`) green.
- `dotnet build SSH_Helper.sln` 0 errors (re-embeds dist); `dotnet test SSH_Helper.Tests` green.

## Exit criteria

- [ ] `CommandResult` + `StepExecutionEventArgs` carry `IterationCount` + `BranchTaken`; the 5 commands set them on **every** return path (no fresh-`Ok()` drops); the executor copies them at `StepCompleted` (`:346-356`).
- [ ] `execution-update` (StepCompleted) carries `iterationCount` + `branchTaken`; StepStarting unchanged.
- [ ] React `executionSlice` has transient `loopIterations` + `branchTaken` Maps (setters, init, `clearExecution` reset); `messageBridge` parses + guards the two fields; **nothing** written to `node.data`/`node.data.props`.
- [ ] Loop nodes show a static `×N` chip; `if`/`switch` nodes show the derived branch label; chip uses only existing tokens (no new token, no hex); badge is static (no new animation/reduced-motion surface).
- [ ] Parity 22/22 under `--workers=1` (byte-identical export); token-sweep green incl. an instrumented node; no `exportGraph.ts`/`FlowCanvasBridge.cs`/`node.data` change.
- [ ] No new npm runtime dependency (Framer Motion still deferred).
- [ ] C# unit tests (command-level + executor-event) green; React messageBridge/store/render specs green; reduced-motion spec green.
- [ ] `npm run build` 0; full e2e green (modulo the known parity-CLI parallel build-lock race — green serialized); dist gate green; `dotnet build` 0 errors; `dotnet test` green.

## Risks / open items

- **Multiple return paths + fresh `CommandResult.Ok()` (the main C# risk).** `ForeachCommand:149`, `IfCommand:63/76`, `SwitchCommand:63/77` build fresh results; a field set only on pass-through paths is silently lost. **Mitigation:** the local-variable-then-set-on-every-return pattern (Architecture) + a command-level test for the break/empty/elif-matched/empty-case paths specifically. This is the highest-value test surface.
- **`break` / control-flow iteration semantics.** `break` exits before the iteration counter increments (`WhileCommand:82`, `RepeatCommand:57`), so a break on pass 3 reports the count of completed passes, not 3. The spec treats `IterationCount` as **iterations executed** (completed passes); the test asserts this convention so it's intentional, not accidental.
- **`StepPath`↔`branchPath` correspondence.** `branchTaken` is only edge-matchable later if the C# scope-key and the canvas `branchPath` stay in lockstep (`then`/`else`/`elif/{i}/then`/`cases/{i}/do`/`default`). They match today (`ScriptExecutor.cs:527-584` vs Live Wires grounding); a divergence would only cost the *future* edge-highlight, never this cycle's badge (which derives its own label).
- **Newtonsoft null emission.** Default settings emit unset fields as JSON `null`; harmless (React treats null/absent identically) and never reaches the `NullValueHandling.Ignore` parity serializer. No action needed.
- **Selector shadowing.** Name the BaseBlock local `branchTakenKey`, not `branchTaken`, to avoid shadowing the slice Map of the same name.
- **Known infra (not a regression).** The VBCSCompiler/Defender parity-CLI parallel build-lock race persists; prove parity/dist green under `--workers=1`.
