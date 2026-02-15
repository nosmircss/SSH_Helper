# Change: Add Ctrl+C capture mode for interactive

## Why
Long-running interactive commands (for example packet capture/tcpdump workflows) need operator-driven stop behavior. Current interactive mode requires closing the window, which does not match the common "run until Ctrl+C" workflow.

## What Changes
- Extend `interactive` options with `command`, `capture`, `max_seconds`, and `mirror_output`.
- Add capture-mode runtime for `interactive.command` in separate-session windows.
- Auto-run command when terminal is ready and complete on Ctrl+C, timeout auto-interrupt, natural completion, or early close.
- Keep the interactive window open as detached read-only after Ctrl+C/timeout/natural completion so operators can review/copy output.
- Persist capture transcript and close reason in execution details.
- Extend parser validation and editor autocomplete contracts for the new keys and values.
- Add docs and QA preset coverage for sniffer/tcpdump style usage.

## Impact
- Affected specs:
  - `scripting-runtime`
  - `scripting-validation`
  - `script-editor`
  - `execution-history`
- Affected code:
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/Commands/InteractiveCommand.cs`
  - `Services/Terminal/InteractiveTerminalService.cs`
  - `Forms/InteractiveTerminalForm.cs`
  - `Services/Editor/ScriptAutocompleteProvider.cs`
  - `SCRIPTING.md`
  - `qa_presets.json`
  - `SSH_Helper.Tests/Scripting/ScriptParserTests.cs`
  - `SSH_Helper.Tests/Scripting/CanonicalCommandMapSyntaxTests.cs`
  - `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs`
  - `SSH_Helper.Tests/Scripting/InteractiveCommandTests.cs`
  - `SSH_Helper.Tests/Services/InteractiveTerminalServiceTranscriptFilterTests.cs`
