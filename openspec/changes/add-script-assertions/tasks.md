# Tasks: Add scripting assertions and result reporting

## 1. Step model and parser
- [ ] 1.1 Add `Assert` step type and options model in `ScriptStep.cs`
- [ ] 1.2 Parse shorthand (`assert: "<expr>"`) and mapping (`condition/message/fail_fast`) forms
- [ ] 1.3 Add `expect` alias parsing to shared assertion step model
- [ ] 1.4 Add validation for required `condition` and expression syntax errors

## 2. Runtime behavior
- [ ] 2.1 Implement `AssertCommand.cs` using expression evaluation engine
- [ ] 2.2 Add default soft-fail behavior that records failed assertions and continues
- [ ] 2.3 Add `fail_fast` handling that terminates execution on first failed assertion when enabled
- [ ] 2.4 Capture per-step assertion result details (condition, message, pass/fail, actual value)

## 3. History and reporting
- [ ] 3.1 Extend execution result models to persist assertion outcomes per host
- [ ] 3.2 Add assertion summaries to history views and execution detail views
- [ ] 3.3 Add scheduler run-summary integration for assertion pass/fail counts

## 4. Verification
- [ ] 4.1 Add parser tests for assert and expect syntax variants
- [ ] 4.2 Add executor tests for soft-fail and fail-fast modes
- [ ] 4.3 Add integration tests for multi-host assertion result aggregation
- [ ] 4.4 Add manual smoke test for scheduled workflow assertion summaries

## 5. Documentation
- [ ] 5.1 Document assertion syntax, semantics, and examples in `SCRIPTING.md`
- [ ] 5.2 Document report fields for assertion history and scheduler notifications
