# Change: Add scripting assertions and result reporting

## Why
Scripts currently execute commands but do not provide structured pass/fail checks, making compliance workflows and scheduled health validation harder to reason about and report.

## What Changes
- Add an `assert` step for expression-based checks with optional custom messages
- Add `expect` as an alias with identical behavior for readability preference
- Default assertion behavior to soft-fail collection (continue execution) with optional `fail_fast` mode
- Persist assertion results as structured per-host execution data and include them in run summaries
- Surface assertion pass/fail totals in manual and scheduled execution reporting

## Impact
- Affected specs:
  - `scripting-assertions` (new capability)
- Affected code:
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/Commands/AssertCommand.cs` (new)
  - `Models/ExecutionResult`-related models (or equivalent assertion result model additions)
  - Scheduler/history presentation components for assertion summaries
  - `SCRIPTING.md`
