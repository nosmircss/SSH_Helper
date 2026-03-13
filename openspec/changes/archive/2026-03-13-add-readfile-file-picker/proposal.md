# Change: Add manual-only file picker support to `readfile`

## Why
Scripts that use `readfile` currently require a hard-coded file path. Operators want an explicit way to choose a file at runtime for manual executions without weakening existing non-interactive script behavior.

## What Changes
- Add an opt-in `readfile.select_file: true` mode that prompts the operator to choose a file before reading.
- Allow picker-enabled `readfile` steps to customize the prompt message and restrict accepted file extensions.
- Keep existing `readfile` path-based behavior unchanged when picker mode is not enabled.
- Restrict picker mode to manual executions and fail clearly for scheduler-triggered runs, including Job List `Run Now`.
- Update parser-driven validation, autocomplete, docs, and focused tests to cover the new contract.

## Impact
- Affected specs:
  - `scripting-runtime`
  - `scripting-validation`
- Affected code:
  - `Services/Scripting/Commands/ReadFileCommand.cs`
  - `Services/Scripting/ScriptContext.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/SshExecutionService.cs`
  - editor/docs/tests for scripting metadata and runtime behavior
