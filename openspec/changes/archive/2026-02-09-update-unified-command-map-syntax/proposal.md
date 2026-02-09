# Change: Update Unified Command Map Syntax

## Why
The current scripting format mixes inline scalar commands (`send: ...`, `print: ...`) with nested map commands (`log:`, `http:`). That inconsistency makes authoring less predictable, drives awkward smart-enter behavior, and increases autocomplete ambiguity.

## What Changes
- Standardize command payload keys so converted commands have one canonical map shape with explicit primary keys.
- Define canonical nested payload keys for these commands:
  - `send.command`
  - `print.message`
  - `wait.seconds`
  - `set.expression`
  - `exit.status` + `exit.message`
  - `if.condition`, `if.then`, `if.elif`, `if.else`
  - `foreach.iterator`, `foreach.when`, `foreach.do`
  - `while.condition`, `while.max_iterations`, `while.do`
  - `try.do`, `try.catch`, `try.finally`
- Keep existing map-style network/file/log commands map-based; align `on_error` placement to the command map for supported commands.
- Add shorthand aliases for readability on commands with one primary field:
  - `send: <command>` -> `send.command`
  - `print: <message>` -> `print.message`
  - `wait: <seconds>` -> `wait.seconds`
  - `set: <expression>` -> `set.expression`
  - `log: <message>` -> `log.message` (with existing default level)
  - `if: <condition>` -> `if.condition`
  - `foreach: <iterator>` -> `foreach.iterator`
  - `while: <condition>` -> `while.condition`
  - `exit: <message>` -> `exit.status=success` + `exit.message=<message>`
- Keep commands with multi-field payloads (`http`, `webhook`, `dns`, `ping`, `portcheck`, `sftp`, `readfile`, `writefile`, `input`, `updatecolumn`, `updateenvironment`, `parse`, `extract`, `try`) map-only.
- Update editor ergonomics for the new shape:
  - `Enter` on command-map key/value lines continues option-level indentation.
  - `Ctrl+Enter` inserts a sibling step (`- ...`) at step indentation.
- Update `SCRIPTING.md` to document canonical map syntax plus supported shorthand aliases.

## Impact
- Affected specs:
  - `scripting-runtime`
  - `scripting-validation`
  - `script-editor`
- Affected code:
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/Commands/*.cs` (canonicalized command handlers)
  - `Services/Editor/ScriptAutocompleteProvider.cs`
  - `Services/Editor/EditorTextUtilities.cs`
  - `UI/ScintillaScriptEditorControl.cs`
  - `SCRIPTING.md`
  - `qa_presets.json`
  - `ScriptSamples/**/*.yaml`
  - `SSH_Helper.Tests/Scripting/*`
  - `SSH_Helper.Tests/Editor/*`
  - `SSH_Helper.Tests/UI/*`
