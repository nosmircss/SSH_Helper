# Change: Add `_outputwindow` built-in script variable

## Why
Scripts can already reference dynamic runtime values like `${_timestamp}`, `${_prompt}`, and `${_output}`, but there is no built-in for the accumulated pane text the operator sees during the current host run. That blocks workflows where a final `notify` or `writefile` step should summarize the execution transcript without manually capturing every command.

## What Changes
- Expose `${_outputwindow}` as a dynamic runtime built-in backed by the current host's pane-formatted transcript so far.
- Seed and update `${_outputwindow}` from the existing script-output relay after output formatting and boundary normalization so it matches pane text, not raw logical messages.
- Keep `${_output}` unchanged as last-command output only.
- Surface `_outputwindow` in editor interpolation autocomplete and built-in hover previews.
- Document `${_outputwindow}` availability, host scoping, and empty-string behavior when no live relay is attached.

## Impact
- Affected specs: `scripting-runtime`, `script-editor`
- Affected code: `Services/Scripting/ScriptContext.cs`, `Services/SshExecutionService.cs`, `Services/Editor/ScriptAutocompleteProvider.cs`, `Form1.cs`, `SCRIPTING.md`
