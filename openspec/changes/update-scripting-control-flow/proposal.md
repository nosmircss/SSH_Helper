# Change: Control-flow ergonomics and loop correctness

## Why
Guarding a single step today requires wrapping it in `if`/`then` (two indent levels per conditional), and almost every step in real SSH automation is conditional. There is no do-while loop, so "run, then poll until healthy" must duplicate the body. `foreach` writes its iterator into the shared context and never removes it, silently clobbering a same-named global, exposes only a thin `<item>_index`, and yields nothing when iterating an object. Soft assertions (`severity: warning`) produce no aggregate result.

## What Changes
- Add a universal step-level `when:` guard to **every** command, not just `if`/`foreach`.
- Add a `repeat`/`until` (do-while) loop that runs the body once before testing the exit condition.
- Give `foreach`/`while` real block scope — the iterator is saved/restored and removed after the loop — so loops stop clobbering globals. **BREAKING** for scripts that read the iterator value *after* the loop ends.
- Add flat-scalar loop metadata: `<item>_number` (1-based), `<item>_first`, `<item>_last`, `<item>_count`, alongside the existing `<item>_index`.
- Add dictionary iteration: `foreach: k, v in {{map}}`.
- Aggregate soft-assert (`severity: warning`) results into an end-of-run test summary.

## Impact
- Affected specs: `scripting-control-flow` (when guard, repeat/until, soft-assert summary), `scripting-runtime` (loop scoping, metadata, dictionary iteration)
- Affected code: `ScriptExecutor` (universal `when:` guard in `ExecuteStepCoreAsync`, loop scope save/restore), `ForeachCommand`, `WhileCommand`, new `RepeatCommand`, `ScriptStep`/`StepType`, `ScriptParser` (canonical key tables + repeat), `ScriptAutocompleteProvider`, `ScriptEditorValidationService`, `FlowCanvasBridge` + React block registry, `AssertCommand`/`ScriptContext` (soft-assert summary), `ScriptDependencyAnalyzer`, tests
- **BREAKING**: `foreach`/`while` iterator no longer persists after the loop body. See `design.md` for migration.
