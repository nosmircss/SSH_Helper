# Change: Add WebView2 browser callback mode

## Why
`browser_callback_capture` currently depends on the operator's external browser whenever `open_browser: true`. That works for real callback flows, but it can steal focus, leaves browser state outside SSH Helper's control, and gives operators no in-app way to reset cookies/cache between callback tests.

## What Changes
- Add `browser_mode: external|webview2` to `browser_callback_capture`.
- Add `show_after_seconds` to `browser_callback_capture` for WebView2 steps that should stay hidden unless the callback flow takes longer than a configured delay.
- Add `auto_close_browser` to `browser_callback_capture` so successful callback pages can optionally stay open, and visible embedded WebView2 callback windows can stay open for inspection after successful completion.
- Keep `open_browser=false` as the existing manual mode and keep external browser as the backward-compatible default when `browser_mode` is omitted.
- Add an injected browser-callback UI host so the runtime can either shell-launch the default browser or open an owned modal WebView2 dialog without changing the localhost callback listener contract.
- Add a shared WebView2 profile manager with persistent app-owned user-data storage and a Settings action to clear embedded browser data when no embedded session is active.
- Add parser, runtime, UI, documentation, and publish verification coverage for the new mode.

## Impact
- Affected specs:
  - `scripting-network-steps`
  - `scripting-runtime`
- Affected code:
  - `SSH_Helper.csproj`
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/SshExecutionService.cs`
  - `Services/Scripting/Commands/BrowserCallbackCaptureCommand.cs`
  - new browser callback UI host / WebView2 dialog / profile manager classes
  - `SettingsDialog.cs`
  - `SCRIPTING.md`
