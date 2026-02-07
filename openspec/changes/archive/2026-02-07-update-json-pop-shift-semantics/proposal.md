# Change: Update json.pop/json.shift Semantics

## Why
`json.pop()` and `json.shift()` are currently documented and commented as destructive operations ("remove and return"), but the runtime implementation only returns the element and leaves the source array unchanged. This mismatch causes script behavior surprises and makes JSON array manipulation inconsistent with common scripting expectations.

## What Changes
- Change `json.pop(arr)` to remove the last element from `arr` and return that removed value
- Change `json.shift(arr)` to remove the first element from `arr` and return that removed value
- Add `json.last(arr)` as a non-destructive "peek last element" helper
- Add `json.first(arr)` as a non-destructive "peek first element" helper
- Restrict destructive mutation (`json.pop/json.shift`) to writable top-level variable references (`arr` or `${arr}`)
- For non-writable expressions, return `null` and emit a warning that directs users to `json.last/json.first`
- Update scripting documentation with migration guidance for users who relied on old non-destructive behavior

## Impact
- Affected specs: `scripting-json-functions` (new capability)
- Affected code:
  - `Services/Scripting/Commands/JsonFunctions.cs`
  - `Services/Scripting/Commands/SetCommand.cs`
  - `Services/Scripting/JsonUtilities.cs`
  - `SCRIPTING.md`
  - `SSH_Helper.Tests/Scripting/SetCommandTests.cs`

