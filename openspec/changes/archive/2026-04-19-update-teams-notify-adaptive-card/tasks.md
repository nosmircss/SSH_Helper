## 1. Runtime and Models

- [x] 1.1 Replace the Teams webhook payload builder with an Adaptive Card envelope.
- [x] 1.2 Add Teams typed mention parsing for `upn:` and `entra:` forms while keeping `notify.mention` as a string list.
- [x] 1.3 Return mention degradation diagnostics to the scripting layer without changing non-Teams channel behavior.

## 2. Tests

- [x] 2.1 Replace the existing Teams payload test coverage with Adaptive Card assertions.
- [x] 2.2 Add Teams mention parsing tests for UPN, Entra Object ID, omitted display labels, and mixed valid/invalid entries.
- [x] 2.3 Add notify-command tests for variable substitution inside Teams typed mention strings and emitted degradation warnings.

## 3. Docs and Validation

- [x] 3.1 Update `SCRIPTING.md` and editor/help text for Teams Adaptive Card notify behavior.
- [x] 3.2 Run focused notification tests, broader notify regressions, and strict OpenSpec validation.
