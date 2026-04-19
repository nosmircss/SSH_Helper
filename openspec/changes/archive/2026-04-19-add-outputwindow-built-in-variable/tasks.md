## 1. Implementation
- [x] 1.1 Add focused RED coverage for `${_outputwindow}` in runtime, relay, editor, and notify behavior.
- [x] 1.2 Expose `${_outputwindow}` from `ScriptContext` built-in metadata with empty-string fallback when no live relay is attached.
- [x] 1.3 Seed and update the host-scoped transcript from `SshExecutionService`'s pane-formatted per-host output relay.
- [x] 1.4 Keep `_outputwindow` out of propagated host-variable/preconnect merges.
- [x] 1.5 Update editor hover/autocomplete metadata and `SCRIPTING.md` documentation/examples.
- [x] 1.6 Run targeted regression suites plus strict OpenSpec validation.
