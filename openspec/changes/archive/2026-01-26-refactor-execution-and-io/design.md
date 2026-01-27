## Context
Execution orchestration is duplicated in Form1 and mixes UI state with execution logic. Pooling disables algorithm settings, the updater runs PowerShell with ExecutionPolicy Bypass without integrity checks, and CSV parsing fails on embedded newlines.

## Goals / Non-Goals
Goals:
- Unify execution orchestration and keep Form1 focused on UI concerns.
- Ensure pooled and non-pooled execution behave consistently.
- Verify update package integrity and document updater behavior.
- Support CSV fields with embedded newlines.

Non-Goals:
- Replace the SSH library or scripting engine.
- Change update distribution beyond integrity checks.

## Decisions
- Introduce a small coordinator service to own execution orchestration and raise events for UI updates.
- Centralize algorithm settings so both pooled and non-pooled paths apply the same configuration.
- Add integrity verification before launching the updater (method TBD).
- Use a multiline-safe CSV parser (library vs streaming parser TBD).

## Risks / Trade-offs
- Adding a CSV library increases dependencies but reduces parsing bugs.
- Verification may require release metadata changes.
- Refactoring execution flow risks regressions in UI behavior.

## Migration Plan
- Add the coordinator in parallel, then switch Form1 to use it.
- Add update verification with a user-visible failure path.
- Replace CSV parsing and validate import/export with existing samples.

## Open Questions
- Preferred update verification method: embedded hash, signed package, or release metadata?
- Preferred CSV parser library (CsvHelper vs custom streaming parser)?
- Should configuration packages be removed now or wired into ConfigurationService?
