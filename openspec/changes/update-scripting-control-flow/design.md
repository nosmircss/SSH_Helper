## Context
The control-flow surface (`ScriptExecutor`, `ForeachCommand`, `WhileCommand`) has grown organically. `foreach` calls `context.SetVariable(itemVarName, item)` and `context.SetVariable($"{itemVarName}_index", index)` but never removes them (`ForeachCommand.cs`), so iterator state leaks past the loop. `when:` is currently only honored by the standalone `if` step and as a per-item filter inside `foreach`. The variable resolver (`ValueResolver` / `ScriptContext`) supports `var[i]` indexing and `.length` but has **no** bare dotted member access — so any `<item>.index`-style object would silently resolve to empty.

## Goals / Non-Goals
- Goals: universal `when:` guard; a do-while loop; loops that don't clobber outer variables; richer, resolver-compatible loop metadata; object iteration; a soft-assert summary.
- Non-Goals: a new typed object/value model; dotted member access; labeled breaks; per-step timeouts (deferred Tier-2/Tier-3 items).

## Decisions
- **Decision: Loop metadata is exposed as flat suffixed scalars, not a dotted object.** `<item>_number`, `<item>_first`, `<item>_last`, `<item>_count` join the existing `<item>_index`. This matches the engine's established convention (`into_status`, `into_avg`, `ping_loss`) and works with the current resolver.
  - Alternatives considered: a `<item>_loop` object accessed as `{{item_loop.index}}` (as the source roadmap suggested) — rejected because the resolver has no dotted member access, so the syntax would silently resolve to empty. This is the exact trap that sank the typed-error proposal in review.
- **Decision: `when:` becomes a common step option evaluated in `ExecuteStepCoreAsync` before dispatch.** A false guard marks the step skipped (reusing the existing `StepCompleted.Skipped` flag) and returns success. `foreach` keeps `when:` as its per-item filter (it is evaluated per iteration, not at dispatch).
- **Decision: `repeat`/`until` is a new `StepType.Repeat` cloning `WhileCommand` with a bottom-tested condition**, reusing the shared `Do`/`MaxIterations` properties and `LoopDepth` so `break`/`continue`/`return` work unchanged. Only `repeat:` + `until:` are introduced (no `while:` alias on it).
- **Decision: Loop scope is implemented with save/restore using existing `HasVariable`/`GetVariable`/`SetVariable`/`RemoveVariable` primitives, restored in a `finally`** so early `break`/`return` still restore prior state.
- **Decision: Dictionary iteration uses a two-name form `foreach: k, v in <expr>`** that resolves the expression to a `JsonObject` and iterates its entries. The single-name form is unchanged.

## Risks / Trade-offs
- **Risk: loop scope restore is BREAKING** for scripts that intentionally read the iterator (or `<item>_index`) after the loop ends → those reads now see the restored/removed value. Mitigation: documented in migration; the previous value is recoverable by assigning it inside the body (`set: last_host` ... `value: "{{host}}"`).
- **Risk: new `StepType.Repeat` carries the ~12-touchpoint integration tax** (StepType enum, ~6 parser key/array tables, validation, autocomplete, `FlowCanvasBridge` C# + React block registry). Mitigation: treat the checklist in `tasks.md` as mandatory; require a YAML→canvas→YAML round-trip test and a break-inside-`repeat` test so the visual editor cannot silently drop the node.
- **Risk: universal `when:` could be lost in Flow Canvas round-trips.** Mitigation: explicit round-trip parity task + test.

## Migration Plan
- Iterator persistence change ships as a documented breaking change in the release notes. Scripts relying on post-loop iterator values must capture the value into another variable inside the body.
- No data migration; no config changes.
- Rollback: revert the scope save/restore commit; the additive metadata and `when:`/`repeat` features are independent and can stay.

## Open Questions
- None blocking. (`while`-loop metadata parity with `foreach` can follow if requested; this change scopes metadata to `foreach`.)
