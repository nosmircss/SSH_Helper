## 1. Universal `when:` guard
- [x] 1.1 In `ScriptExecutor.ExecuteStepsAsync` (the step loop, where `Skipped` events are emitted), evaluate `step.When` for all non-`foreach` steps; emit `StepCompleted{Skipped=true}` and continue when false
- [x] 1.2 `foreach` retains `when:` as its per-item filter (regression test added)
- [x] 1.3 Hoisted `ExtractVarReferences(step.When, …)` in `ScriptDependencyAnalyzer` to apply to every step (removed the foreach-only duplicate)
- [ ] 1.4 `FlowCanvasBridge` + React block round-trip for `when:` — DEFERRED (Flow Canvas UI layer; tracked as a follow-up, not required for runtime behavior)
- [x] 1.5 Tests: `ScriptExecutorWhenGuardTests` (false→skip + Skipped event, true→runs, foreach per-item filter) + `ScriptDependencyAnalyzerTests` reference test

## 2. `repeat`/`until` loop
- [ ] 2.1 Add `StepType.Repeat`; add `RepeatCommand` cloning `WhileCommand` with the condition checked at the bottom; reuse `Do`/`MaxIterations`; increment `LoopDepth`
- [ ] 2.2 Parser: add `repeat`/`until` to the canonical key tables and step-dispatch path
- [ ] 2.3 Validation: required `until` + `do`; `max_iterations` integer check
- [ ] 2.4 Autocomplete entries for `repeat`/`until`
- [ ] 2.5 `FlowCanvasBridge` C# + React block registry: add the repeat node type
- [ ] 2.6 Tests: body-runs-once, repeats-until-true, break inside repeat, YAML→canvas→YAML round-trip

## 3. Loop scoping + metadata + dictionary iteration
- [ ] 3.1 `ForeachCommand`/`WhileCommand`: save prior values of the iterator (and metadata names) and restore in a `finally` (survives break/return)
- [ ] 3.2 Set flat metadata scalars each iteration: `<item>_index` (existing), `<item>_number`, `<item>_first`, `<item>_last`, `<item>_count`
- [ ] 3.3 Dictionary iteration: parse the two-name `k, v in <expr>` form; resolve `<expr>` to a `JsonObject`; iterate entries setting `k`/`v`
- [ ] 3.4 Tests: outer variable restored after loop; restore on break/return; metadata values; dict iteration; single-name form unchanged

## 4. Soft-assert summary
- [ ] 4.1 Record each soft-assert (`assert` with `severity: warning`) outcome in `ScriptContext`
- [ ] 4.2 Emit an aggregate pass/fail summary at run completion (reuse existing output events)
- [ ] 4.3 Tests: passed/failed counts; failures do not terminate the script

## 5. Verification
- [ ] 5.1 `dotnet build SSH_Helper.sln`
- [ ] 5.2 `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- [ ] 5.3 `openspec validate update-scripting-control-flow --strict --no-interactive`
- [ ] 5.4 Add a release-note entry for the BREAKING iterator-persistence change
