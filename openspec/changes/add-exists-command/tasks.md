## 1. OpenSpec
- [x] 1.1 Validate `add-exists-command` with strict OpenSpec validation.

## 2. Runtime Implementation
- [x] 2.1 Add `exists` step model support in scripting step models/enums.
- [x] 2.2 Add parser support for `exists` syntax, required fields, and allowed keys.
- [x] 2.3 Implement `ExistsCommand` runtime behavior and output contract.
- [x] 2.4 Register `exists` in script command dispatch.

## 3. UI/Canvas Integration
- [x] 3.1 Add Flow Canvas bridge mapping for `exists` import/export.
- [x] 3.2 Add Flow Canvas block definition for `exists`.

## 4. Documentation
- [x] 4.1 Add `exists` command documentation and examples in `SCRIPTING.md`.

## 5. Verification
- [x] 5.1 Add/extend automated tests for parser validation and runtime behavior.
- [ ] 5.2 Run `dotnet build SSH_Helper.sln`.
- [x] 5.3 Run targeted non-interactive tests for scripting/runtime parser coverage.
