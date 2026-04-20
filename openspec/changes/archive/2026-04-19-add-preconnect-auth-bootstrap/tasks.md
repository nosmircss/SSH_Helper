## 1. Script Model and Validation

- [x] 1.1 Extend script model/parser to support top-level `preconnect` steps list.
- [x] 1.2 Add validation rules for `preconnect` shape and supported commands.
- [x] 1.3 Add validation errors for SSH-session-dependent commands in `preconnect`.

## 2. Runtime Orchestration

- [x] 2.1 Add preconnect execution pass in host script orchestration before SSH login.
- [x] 2.2 Define and resolve reserved auth override variables from preconnect context.
- [x] 2.3 Apply effective auth overrides to non-pooled script execution login path.
- [x] 2.4 Apply effective auth overrides to pooled session creation/login path.
- [x] 2.5 Ensure SSH dependency analysis and local-only execution behavior remain compatible.

## 3. Security and Observability

- [x] 3.1 Redact reserved auth override variables from script output, history, and debug logs.
- [x] 3.2 Add explicit progress/output messages indicating preconnect phase start/end without leaking secrets.

## 4. Tests and Documentation

- [x] 4.1 Add parser and validation unit tests for `preconnect` syntax and invalid command usage.
- [X] 4.2 Add runtime tests for successful cert bootstrap -> SSH login -> send flow.
- [x] 4.3 Add runtime tests for failure paths (missing cert output, invalid identity file, canceled preconnect).
- [x] 4.4 Add pooling parity tests to ensure dynamic auth inputs do not reuse stale sessions.
- [x] 4.5 Update `SCRIPTING.md` with preconnect examples and override-variable contract.
- [x] 4.6 Run focused test suite and strict OpenSpec validation for this change.
