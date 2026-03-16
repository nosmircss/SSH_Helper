# Change: Add script subroutines, calls, and file-based libraries

## Why
Large scripts still have to duplicate step trees because the language has no first-class reuse primitive above raw control flow. That makes common patterns like vendor-specific branching, normalization helpers, and repeated reporting blocks verbose and hard to maintain.

## What Changes
- Add top-level `subroutines:` for named reusable step blocks.
- Add top-level `imports:` plus `library: true` for file-based reusable script libraries.
- Add `call` and `return` steps to execute subroutines with explicit args, local scope, and explicit output bindings.
- Add parser, validation, static-analysis, and editor support for the new top-level keys and step commands.
- Add focused samples and documentation showing reusable lookup/reporting flows.

## Impact
- Affected specs:
  - `scripting-runtime`
  - `scripting-validation`
  - `script-editor`
- Affected code:
  - `Services/Scripting/Models/Script.cs`
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/ScriptContext.cs`
  - `Services/Scripting/ScriptDependencyAnalyzer.cs`
  - editor metadata/autocomplete/highlighting services
  - `SCRIPTING.md` and sample scripts
