# Change: Add scripting string and array functions

## Why
Scripts currently require verbose workarounds for common string and list operations, reducing readability and increasing error-prone step counts.

## What Changes
- Add `replace()`, `split()`, `join()`, and `substring()` functions to `set` expression evaluation
- Add `sort()` function for list-like values with optional descending mode
- Add tests and docs for deterministic behavior and edge handling

## Impact
- Affected specs:
  - `scripting-expressions`
- Affected code:
  - `Services/Scripting/Commands/SetCommand.cs`
  - `Services/Scripting/JsonUtilities.cs`
  - `SCRIPTING.md`
  - `SSH_Helper.Tests/Scripting/SetCommandTests.cs`