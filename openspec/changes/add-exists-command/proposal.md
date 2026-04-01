# Change: Add exists Command for Local Path Checks

## Why
Scripting currently requires `try`/`on_error` workarounds to branch on whether a file or directory is present. This is cumbersome for common portability scenarios such as OneDrive vs local profile paths.

A dedicated existence check command improves readability, reduces failure-driven control flow, and enables explicit preflight checks before file operations.

## What Changes
- Add a new `exists` scripting command for local path checks.
- Support path resolution using script variables and Windows `%NAME%` environment expansion.
- Support optional target type mode: `any` (default), `file`, or `directory`.
- Add dual outputs:
  - primary boolean output (`into`)
  - metadata output (`<into>_meta`) including resolved path and type flags.
- Add parser/validation support for `exists` command shape and required keys.
- Add Flow Canvas support so the command can round-trip between YAML and canvas blocks.
- Update scripting documentation with syntax, parameters, output contract, and fallback examples.

## Impact
- Affected specs:
  - `scripting-runtime`
  - `scripting-validation`
- Affected code:
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/Commands/ExistsCommand.cs` (new)
  - `Services/FlowCanvasBridge.cs`
  - `FlowCanvas/src/blockDefs/registry.ts`
  - `SCRIPTING.md`
  - `SSH_Helper.Tests/...` (scripting parser/command tests)

## Notes
- Scope intentionally allows local path checks beyond read/write restricted folders because this command reads metadata only and does not read or write file contents.
- This proposal does not add remote (SSH/SFTP) existence checks.
