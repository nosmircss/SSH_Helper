# Change: Add Configuration Parsing Command

## Why
Users need to capture FortiGate (and eventually other network device) configurations via SSH and extract specific values or iterate over configuration sections. Currently, the only extraction method is regex via the `extract` command, which cannot handle the hierarchical `config/edit/set/next/end` structure of FortiGate configs.

## What Changes
- Add a new `parse` scripting command that transforms raw config text into structured JSON
- Create a pluggable parser architecture starting with FortiGate format support
- Parsed data integrates with existing `json.*` functions for access and iteration
- Users can export parsed data to CSV using existing `writefile` command

## Impact
- Affected specs: New `config-parsing` capability
- Affected code:
  - `Services/Scripting/Commands/ParseCommand.cs` (new)
  - `Services/Scripting/Parsers/` directory (new)
  - `Services/Scripting/Models/ScriptStep.cs` (add ParseOptions)
  - `Services/Scripting/ScriptParser.cs` (parse `parse:` blocks)
  - `Services/Scripting/ScriptExecutor.cs` (register command)
