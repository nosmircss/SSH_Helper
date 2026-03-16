# Tasks: Add script subroutines, calls, and file-based libraries

## 1. Models and parser
- [ ] 1.1 Extend script and step models with imports, subroutines, library metadata, `call`, and `return`
- [ ] 1.2 Parse `subroutines`, `imports`, and `library: true` at the top level
- [ ] 1.3 Parse `call` map payloads and `return: true`
- [ ] 1.4 Add validation for executable-script vs library-file shape, arg/out contracts, and call-cycle rejection

## 2. Runtime
- [ ] 2.1 Build a subroutine/import registry before execution and resolve imported library files once per script run
- [ ] 2.2 Add child-scope execution for `call` with cloned structured args and explicit output copy-back
- [ ] 2.3 Add `return` control-flow propagation that exits only the current subroutine
- [ ] 2.4 Enforce defensive call-depth limits and preserve existing `exit`/loop behavior outside the new subroutine boundary

## 3. Analysis and editor surfaces
- [ ] 3.1 Update static missing-column analysis so caller-side call args drive external dependency reporting
- [ ] 3.2 Update parser-driven autocomplete metadata and syntax-highlighting vocabulary for `subroutines`, `imports`, `library`, `call`, and `return`
- [ ] 3.3 Add or update validation/editor tests for the new syntax surface

## 4. Verification and docs
- [ ] 4.1 Add parser/runtime/analyzer/editor tests covering local calls, imported libraries, scope isolation, outputs, and invalid contracts
- [ ] 4.2 Update `SCRIPTING.md` with subroutine/library authoring guidance and examples
- [ ] 4.3 Add or refresh sample scripts showing reusable in-file subroutines and an imported library helper
- [ ] 4.4 Run focused tests, a solution build, and `openspec validate add-script-subroutines-and-libraries --strict --no-interactive`
