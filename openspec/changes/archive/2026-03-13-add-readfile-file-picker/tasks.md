## 1. Implementation
- [x] 1.1 Add spec deltas for `readfile.select_file` runtime behavior and conditional validation.
- [x] 1.2 Extend readfile parser/model metadata and parser-driven editor vocabulary for `select_file`, custom picker message text, and `fileext` filtering.
- [x] 1.3 Implement manual-only `readfile` picker prompting with optional seeded path, custom prompt text, file-type restrictions, and existing read semantics.
- [x] 1.4 Propagate a file-selection policy flag through script execution contexts and block scheduler executions from opening picker dialogs.
- [x] 1.5 Add focused automated coverage for parsing, validation, command behavior, autocomplete, scheduler blocking, and picker dialog layout/validation.

## 2. Verification
- [x] 2.1 Run focused automated verification for scripting/editor/scheduler coverage.
- [x] 2.2 Validate change with `openspec validate add-readfile-file-picker --strict --no-interactive`.
- [x] 2.3 Manual interactive verification of the picker dialog remains pending from this CLI environment.
