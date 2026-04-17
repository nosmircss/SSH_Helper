# Change: Add `_prompt` built-in script variable

## Why
Scripts can already read dynamic runtime values like `${_timestamp}` and `${_output}`, but they cannot reference the current detected SSH shell prompt. That makes prompt-aware scripting and prompt-debug output harder than it needs to be.

## What Changes
- Expose `${_prompt}` as a dynamic scripting built-in backed by the current SSH shell session prompt.
- Surface `_prompt` in editor interpolation autocomplete and variable hover previews.
- Document `${_prompt}` availability and its empty-string behavior when no SSH prompt is available.

## Impact
- Affected specs: `scripting-runtime`, `script-editor`
- Affected code: `Services/Scripting/ScriptContext.cs`, `Services/Editor/ScriptAutocompleteProvider.cs`, `Form1.cs`, `SCRIPTING.md`
