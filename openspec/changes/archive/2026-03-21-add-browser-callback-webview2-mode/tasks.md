# Tasks: Add WebView2 browser callback mode

## 1. Parser and contract
- [x] 1.1 Extend `browser_callback_capture` parsing/validation for `browser_mode: external|webview2`
- [x] 1.2 Preserve precedence: `open_browser=false` stays manual mode, omitted `browser_mode` defaults to external, and invalid `browser_mode` values fail with line context
- [x] 1.3 Add parser and validation tests for the new option and precedence rules
- [x] 1.4 Extend parsing/validation for `show_after_seconds` with a default of `0` and a non-negative integer contract
- [x] 1.5 Extend parsing/validation for `auto_close_browser` with a default of `true`

## 2. Runtime and UI host
- [x] 2.1 Add WebView2 package/runtime plumbing with static loader preference for single-file publish
- [x] 2.2 Introduce a browser-callback UI host abstraction that supports both external shell launch and owned modal WebView2 launch
- [x] 2.3 Implement the WebView2 dialog flow, callback completion wait, and operator-close cancellation behavior
- [x] 2.4 Keep external-browser behavior and focus restoration unchanged unless a step explicitly opts into `browser_mode: webview2`
- [x] 2.5 Add delayed WebView2 reveal support so `show_after_seconds > 0` keeps the dialog hidden until the callback is still pending after the configured delay
- [x] 2.6 Add a browser auto-close toggle so successful callback pages can stay open and visible WebView2 callback windows can remain open for inspection

## 3. Embedded browser profile management
- [x] 3.1 Add a shared WebView2 profile manager with an app-owned user-data folder under `%LocalAppData%\\SSH_Helper`
- [x] 3.2 Track active embedded sessions and block data reset while a session is open
- [x] 3.3 Add focused tests for profile reset and active-session blocking

## 4. Settings and docs
- [x] 4.1 Add a Settings action to clear embedded browser data with explicit confirmation text
- [x] 4.2 Update `SCRIPTING.md` with `browser_mode`, `open_browser` precedence, WebView2 modal behavior, and cache-reset guidance
- [x] 4.3 Add any necessary sample/test fixture coverage without changing the existing self-contained sample's default external-browser behavior
- [x] 4.4 Update `SCRIPTING.md` to describe `show_after_seconds` and its WebView2-only delayed popup behavior
- [x] 4.5 Update `SCRIPTING.md` to describe `auto_close_browser` for callback pages and visible WebView2 success behavior

## 5. Verification
- [x] 5.1 Run focused parser/runtime/settings tests
- [x] 5.2 Run `dotnet build .\\SSH_Helper.sln -nologo`
- [x] 5.3 Run Release single-file publish verification and confirm no `WebView2Loader.dll` sidecar is emitted
- [x] 5.4 Run `openspec validate add-browser-callback-webview2-mode --strict --no-interactive`
- [x] 5.5 Re-run focused parser/runtime tests plus the browser-callback regression slice after the delayed reveal change
- [x] 5.6 Re-run focused parser/runtime tests plus the browser-callback regression slice after the auto-close toggle change
