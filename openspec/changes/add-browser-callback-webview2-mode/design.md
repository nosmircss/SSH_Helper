## Context
`browser_callback_capture` already owns the localhost callback listener and browser-completion handshake, but it launches only through the Windows shell. Adding an in-app WebView2 mode crosses parser, runtime, WinForms UI threading, packaging, and settings/profile management. The runtime must remain backward-compatible for existing scripts and keep the callback listener semantics identical regardless of browser surface.

## Goals / Non-Goals
- Goals:
  - Add an opt-in embedded browser mode for callback steps.
  - Preserve existing external/manual behavior by default.
  - Persist embedded browser state across runs and provide an explicit reset path.
  - Keep the app publishable as a single-file executable while relying on an installed Evergreen WebView2 runtime.
- Non-Goals:
  - A global browser-mode preference.
  - Replacing the external-browser path.
  - Shipping a fixed-version WebView2 runtime.
  - Multi-host or scheduler support for interactive embedded browser flows.

## Decisions
- Decision: add `browser_mode` as a per-step option instead of a global setting.
  - Rationale: browser selection is part of the script contract and must stay explicit per callback step.
- Decision: add `show_after_seconds` as a per-step WebView2-only reveal delay instead of a global browser setting.
  - Rationale: operators may want some callback steps to stay fully hidden while others should still surface immediately, and the delay belongs with the callback step contract.
- Decision: add `auto_close_browser` as a per-step callback-surface auto-close toggle instead of a global preference.
  - Rationale: some callback steps are test fixtures where the operator wants to inspect the final browser result, while others should retain the current self-closing behavior.
- Decision: use an injected browser-callback UI host instead of directly coupling `BrowserCallbackCaptureCommand` to WinForms/WebView2 controls.
  - Rationale: it keeps the command testable and preserves existing constructor call sites through optional plumbing.
- Decision: store WebView2 user data under an app-owned `%LocalAppData%\\SSH_Helper` folder shared across runs.
  - Rationale: operators asked for persistent callback state plus an explicit reset action.
- Decision: clear embedded data by deleting the app-owned WebView2 profile only when no embedded session is active.
  - Rationale: it is deterministic, covers cookies/cache/local storage together, and avoids partial live-reset behavior.
- Decision: keep external-browser focus restoration separate from WebView2 mode.
  - Rationale: closing the owned modal dialog should naturally return activation to SSH Helper, while external-browser mode still needs the existing best-effort focus restorer.

## Risks / Trade-offs
- WebView2 initialization adds a new machine dependency.
  - Mitigation: assume the Evergreen runtime is installed, static-link the loader, and fail with a clear message if WebView2 initialization cannot start.
- Blocking the script worker on UI-owned modal state can deadlock if invoked from the wrong thread.
  - Mitigation: create/show/close the embedded dialog through a UI host that marshals work onto the owner form's UI thread.
- Delaying dialog reveal can race with callback completion or user cancellation.
  - Mitigation: track close/completion state in the embedded session and only show the modal dialog if the delay elapses while the session is still active.
- `auto_close_browser: false` can keep visible WebView2 callback windows open after success, which risks leaking a hidden session if the dialog never actually surfaced.
  - Mitigation: only keep the embedded session alive when the WebView2 dialog was actually shown to the operator; unrevealed delayed sessions still clean up automatically.
- Persistent browser state can make callback testing sticky.
  - Mitigation: expose an explicit Settings reset action with confirmation text and active-session blocking.

## Migration Plan
No migration is required. Existing scripts without `browser_mode` keep external-browser behavior, and scripts with `open_browser: false` continue to use manual mode.
