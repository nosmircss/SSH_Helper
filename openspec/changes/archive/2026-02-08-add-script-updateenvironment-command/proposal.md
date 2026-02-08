# Change: add-script-updateenvironment-command

## Why
Scripts can change runtime variables with `set`, but cannot persist updates back to the active environment profile. This blocks workflows where a script refreshes a token, timestamp, or derived setting and operators want later runs to use the updated value.

## What Changes
- Add a new scripting step `updateenvironment` with `variable` and `value` fields.
- Persist `updateenvironment` writes into the active environment's `Variables` map.
- Update runtime context so subsequent steps in the same script can immediately use the updated value.
- Add parser/validation support, command execution support, and documentation/tests.

## Impact
- Affected specs:
  - `scripting-runtime`
  - `environment-management`
- Affected code:
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/ScriptContext.cs`
  - `Services/Scripting/Commands/`
  - `Services/SshExecutionService.cs`
  - `Services/EnvironmentService.cs`
  - `Form1.cs`
  - `SCRIPTING.md`
  - `SSH_Helper.Tests/Scripting/`
  - `SSH_Helper.Tests/Services/`
