## 1. Runtime correctness
- [x] 1.1 Remove conditional pre-substitution in `if` and `while`
- [x] 1.2 Replace split-based arithmetic with precedence parsing in `set`
- [x] 1.3 Move JSON function dispatch to shared `JsonUtilities` entrypoint
- [x] 1.4 Add `_last_error` context lifecycle for suppressed errors
- [x] 1.5 Add per-step `max_iterations` and enforce in `while`
- [x] 1.6 Update foreach JSON scalar handling to use string-value conversion
- [x] 1.7 Make `_timestamp` resolve dynamically
- [x] 1.8 Cache regex use in substitution hot path

## 2. Diagnostics and docs
- [x] 2.1 Add parser warnings for unknown YAML keys with line numbers
- [x] 2.2 Surface parser warnings in validation UI without failing execution
- [x] 2.3 Align scripting docs for YAML detection and webhook SSRF design note
- [x] 2.4 Centralize regex timeout constants and add hardening regression tests

## 3. Verification
- [x] 3.1 Expand parser and executor tests for warnings and `_last_error`
- [x] 3.2 Expand script context and foreach tests for timestamp and JSON scalar behavior
- [x] 3.3 Run scripting-focused test suite
