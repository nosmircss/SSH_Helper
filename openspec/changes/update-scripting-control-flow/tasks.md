## 1. Universal `when:` guard
- [x] 1.1 In `ScriptExecutor.ExecuteStepsAsync` (the step loop, where `Skipped` events are emitted), evaluate `step.When` for all non-`foreach` steps; emit `StepCompleted{Skipped=true}` and continue when false
- [x] 1.2 `foreach` retains `when:` as its per-item filter (regression test added)
- [x] 1.3 Hoisted `ExtractVarReferences(step.When, …)` in `ScriptDependencyAnalyzer` to apply to every step (removed the foreach-only duplicate)
- [x] 1.4 `FlowCanvasBridge` `when:` round-trip for every block: exported as a common node prop and re-emitted as a step-root option (sibling of the command, not nested in the command map). Tests: `FlowCanvasBridgeTests.ExportGraphToYaml_WhenGuardOn{GeneratedStep,ContainerStep}_RoundTrips`
- [x] 1.5 Tests: `ScriptExecutorWhenGuardTests` (false→skip + Skipped event, true→runs, foreach per-item filter) + `ScriptDependencyAnalyzerTests` reference test

## 2. `repeat`/`until` loop
- [x] 2.1 `StepType.Repeat` + `Until` property; `RepeatCommand` (bottom-tested do-while), reuses `Do`/`MaxIterations`/`LoopDepth`; registered in executor
- [x] 2.2 Parser: `case "repeat"` + `ParseRepeatStep` (nested `until`/`do`/`max_iterations` or scalar shorthand); added to all command/canonical-key/scalar-value tables
- [x] 2.3 Validation: requires `until` + `do`; `max_iterations` > 0 check
- [x] 2.4 Autocomplete: `repeat` command + `until`/`do` required keys; `ScriptDependencyAnalyzer` Repeat case
- [x] 2.5 `FlowCanvasBridge` C# (10 sites: block-keys, branch export, container checks, props, reverse map, preview key, do-edge import, option building, scalar shorthand) + React `registry.ts` repeat block
- [x] 2.6 Runtime + parse tests: `ScriptRepeatLoopTests` (6) + YAML→canvas→YAML round-trip `FlowCanvasBridgeTests.ExportGraphToYaml_RepeatUntilContainer_RoundTripsToRepeatStep`

## 3. Loop scoping + metadata + dictionary iteration
- [x] 3.1 `ForeachCommand`: save prior values of the iterator + metadata names and restore in a `finally` (survives break/return/exit). (`while` introduces no iteration variable, so there is nothing to scope there — requirement is vacuously satisfied for `while`.)
- [x] 3.2 Flat metadata scalars set each iteration: `<item>_index` (existing), `<item>_number` (1-based), `<item>_first`, `<item>_last`, `<item>_count`
- [x] 3.3 Dictionary iteration: two-name `key, value in <expr>` form resolved via `JsonUtilities.GetJsonObject`, iterating entries
- [x] 3.4 Tests: `ScriptExecutorLoopScopingTests` (restore-after-loop, restore-on-break, metadata, single-item first/index, dict key/value, dict multi-entry). Single-name form unchanged (existing foreach tests stay green; full suite 2306/2306 excluding 2 pre-existing fragile UI tests)

## 4. Soft-assert summary
- [x] 4.1 `ScriptContext.RecordSoftAssert` (lock-guarded, shared-state counters); `AssertCommand` records pass/fail for `severity: warning` asserts
- [x] 4.2 `ScriptExecutor.ExecuteAsync` finally emits "Soft assertions: N passed, M failed" at completion when any soft asserts ran (Warning if any failed, else Success)
- [x] 4.3 Tests: `ScriptExecutorSoftAssertTests` (fail doesn't terminate + counted, pass counted, hard-assert excluded, summary emitted, no-summary-when-none)

## 5. Verification
- [x] 5.1 `dotnet build` — succeeds (incl. FlowCanvas npm/TS)
- [x] 5.2 `dotnet test` — all new tests pass; non-UI suite 1933/1933 (UI/native-dialog tests are pre-existing scheduling-fragile, see memory)
- [x] 5.3 `openspec validate update-scripting-control-flow --strict --no-interactive` — valid
- [ ] 5.4 Release-note entry for the BREAKING iterator-persistence change — pending (batch CHANGELOG update with Proposal C's breaking strict-keys change)
