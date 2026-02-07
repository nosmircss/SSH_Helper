# Change: Add multi-protocol workflow steps

## Why
The scripting engine is SSH-centric today, which limits workflows that need API checks, network reachability probes, DNS validation, and file transfer in a single run.

## What Changes
- Add first-class `http`, `ping`, `dns`, `portcheck`, and `sftp` scripting steps
- Extend script model, parser, validation, and runtime registration for all new step types
- Add normalized result capture conventions (`into`, derived status/metrics fields)
- Preserve existing `webhook` behavior for backward compatibility

## Impact
- Affected specs:
  - `scripting-network-steps` (new capability)
- Affected code:
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/Commands/HttpCommand.cs` (new)
  - `Services/Scripting/Commands/PingCommand.cs` (new)
  - `Services/Scripting/Commands/DnsCommand.cs` (new)
  - `Services/Scripting/Commands/PortcheckCommand.cs` (new)
  - `Services/Scripting/Commands/SftpCommand.cs` (new)
  - `SCRIPTING.md`
