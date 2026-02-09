## 1. Implementation
- [x] 1.1 Add `updateenvironment` step model, parser detection/parsing, and validation rules (`variable` + `value` required).
- [x] 1.2 Implement `UpdateEnvironmentCommand` and runtime plumbing so script execution can request environment-variable persistence and continue with the new value in current context.
- [x] 1.3 Persist requested updates into the active environment profile via `EnvironmentService`, including legacy/default environment handling.
- [x] 1.4 Add automated tests for parsing/validation, command execution behavior, and environment persistence.
- [x] 1.5 Document `updateenvironment` in `SCRIPTING.md` with syntax, behavior, and examples.
