# Tasks: Update json.pop/json.shift Semantics

## 1. Runtime Semantics
- [x] 1.1 Add writable array-target resolution helper for `json.pop/json.shift`
- [x] 1.2 Persist mutated array state back into script context for destructive operations
- [x] 1.3 Add `json.first` and `json.last` non-destructive helpers
- [x] 1.4 Extend JSON function dispatchers to recognize `first` and `last`

## 2. Tests
- [x] 2.1 Add tests covering destructive `json.pop` behavior
- [x] 2.2 Add tests covering destructive `json.shift` behavior
- [x] 2.3 Add tests for non-destructive `json.first/json.last`
- [x] 2.4 Add tests for empty-array and non-writable-expression edge cases

## 3. Documentation
- [x] 3.1 Update JSON function table entries in `SCRIPTING.md`
- [x] 3.2 Update array-manipulation examples to include `json.first/json.last`
- [x] 3.3 Add migration note for users relying on old non-destructive `json.pop/json.shift` behavior
