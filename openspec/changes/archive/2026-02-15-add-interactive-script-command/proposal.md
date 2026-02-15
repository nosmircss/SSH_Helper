# Change: Add interactive script command

## Why
Scripts currently support non-blocking SSH commands (`send`) but do not support handing control to an operator for a live terminal interaction mid-script.

## What Changes
- Add a new `interactive` step that opens an in-app SSH terminal window and blocks script execution until the window closes.
- Support `session` mode (`separate` or `shared`) and `emulation` mode (`full`) with defaults.
- Render full emulation with terminal palette colors (foreground and background) for ANSI-color output.
- Enforce map-only syntax for `interactive` and strict option validation.
- Add execution preflight rules that reject `interactive` in multi-host and folder runs.
- Add editor/autocomplete vocabulary for `interactive`, `session`, and `emulation`.
- Add documentation and QA presets for `interactive` scenarios.

## Impact
- Affected specs:
  - `scripting-runtime`
  - `scripting-validation`
  - `script-editor`
  - `ssh-execution`
- Affected code:
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/Commands/InteractiveCommand.cs` (new)
  - `Services/Terminal/InteractiveTerminalService.cs` (new)
  - `Forms/InteractiveTerminalForm.cs` (new)
  - `Services/Scripting/ScriptDependencyAnalyzer.cs`
  - `Services/SshExecutionService.cs`
  - `SCRIPTING.md`
  - `qa_presets.json`
