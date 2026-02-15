# Change: Add list expression helper functions

## Why
Scripts can append to lists with `push()` but still need verbose loop workarounds for prepend, remove-first/remove-last, and list merging/indexing tasks.

## What Changes
- Add list-oriented expression helpers in `set` evaluation: `unshift()`, `shift()`, `pop()`, `first()`, `last()`, `indexof()`, and `concat()`
- Keep behavior compatible with existing list variables (`List<string>`) without requiring JSON wrapper functions
- Add tests and docs for destructive vs non-destructive behavior

## Impact
- Affected specs:
  - `scripting-expressions`
- Affected code:
  - `Services/Scripting/Commands/SetCommand.cs`
  - `SCRIPTING.md`
  - `SSH_Helper.Tests/Scripting/SetCommandTests.cs`
