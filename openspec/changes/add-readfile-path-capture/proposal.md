# Change: Add readfile path capture modes

## Why
Scripts can already prompt the operator to choose a file with `readfile.select_file`, but the runtime only exposes the processed file contents. That blocks a common local-automation workflow where the operator chooses a file and a later `localcmd` step needs the selected path rather than, or in addition to, the file contents.

## What Changes
- Extend `readfile` with path-capture support for picker-driven workflows.
- Add a `path_only` mode so `readfile` can resolve and validate a selected path without reading the file contents.
- Add `path_into` so scripts can choose the variable name that receives the resolved absolute path.
- In normal read mode, also expose the resolved absolute path through `path_into` when provided or a predictable companion variable when omitted.
- Keep the workflow PowerShell-oriented in documentation and examples for later `localcmd` use.
- Update parser, runtime, validation, editor, Flow Canvas parity, tests, and docs.

## Impact
- Affected specs: `scripting-runtime`, `scripting-validation`
- Affected code: `Services/Scripting/Commands/ReadFileCommand.cs`, `Services/Scripting/Models/ScriptStep.cs`, `Services/Scripting/ScriptParser.cs`, `Services/Editor/ScriptAutocompleteProvider.cs`, `Services/FlowCanvasBridge.cs`, `FlowCanvas/src/blockDefs/registry.ts`, `FlowCanvas/src/panels/Properties.tsx`, `SSH_Helper.Tests/Scripting/ReadFileCommandTests.cs`, `SSH_Helper.Tests/Services/JobExecutionServiceTests.cs`, `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs`, `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`, `SCRIPTING.md`