# Change: Update scripting correctness and diagnostics

## Why
The scripting engine still has several correctness and observability gaps: conditional pre-substitution can misparse expressions, arithmetic with multiple operators is unreliable, non-fatal errors are not surfaced to downstream logic, and parser diagnostics currently hide typo-level YAML mistakes.

## What Changes
- Remove pre-substitution in `if`/`while` condition execution paths
- Replace split-based arithmetic with a real precedence parser for `set` expressions
- Add `_last_error` lifecycle support for `on_error: continue`
- Keep webhook SSRF behavior documentation-only and align operator docs
- Add parser warning diagnostics for unknown YAML keys (non-fatal)
- Add per-step `max_iterations` for `while`
- Resolve `_timestamp` dynamically at substitution time
- Cache regex usage in variable substitution hot paths
- Replace foreach JSON scalar serialization behavior with string-value conversion
- Centralize JSON function dispatch entrypoint in `JsonUtilities`

## Impact
- Affected specs:
  - `scripting-runtime`
  - `scripting-validation`
- Affected code:
  - `Services/Scripting/Commands/IfCommand.cs`
  - `Services/Scripting/Commands/WhileCommand.cs`
  - `Services/Scripting/Commands/SetCommand.cs`
  - `Services/Scripting/Commands/ForeachCommand.cs`
  - `Services/Scripting/ScriptContext.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/JsonUtilities.cs`
  - `Services/Scripting/ExpressionEvaluator.cs`
  - `Services/Scripting/Commands/ExtractCommand.cs`
  - `Services/Scripting/Commands/WebhookCommand.cs`
  - `SCRIPTING.md`