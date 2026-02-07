# Change: Add scripting control-flow primitives

## Why
The scripting language currently relies on indirect control-flow patterns and cannot express loop breaks, loop continues, multi-branch conditionals, or structured error handling directly.

## What Changes
- Add explicit `break` and `continue` step types
- Add `elif` support under `if` blocks
- Add structured `try`/`catch`/`finally` step blocks
- Add parser and validator rules for these new control-flow constructs
- Add runtime command handlers and control-flow propagation behavior

## Impact
- Affected specs:
  - `scripting-control-flow`
- Affected code:
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/Commands/IfCommand.cs`
  - `Services/Scripting/Commands/IScriptCommand.cs`
  - `Services/Scripting/Commands/BreakCommand.cs`
  - `Services/Scripting/Commands/ContinueCommand.cs`
  - `Services/Scripting/Commands/TryCommand.cs`
  - `SCRIPTING.md`