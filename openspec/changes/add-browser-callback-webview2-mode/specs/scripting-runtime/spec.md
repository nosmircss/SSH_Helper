## ADDED Requirements
### Requirement: Embedded browser callback execution
The scripting runtime SHALL support executing `browser_callback_capture` in an owned modal WebView2 dialog when the step selects `browser_mode: webview2` and `open_browser: true`.

The WebView2 dialog SHALL be created on the UI thread through a browser-callback UI host, SHALL navigate to the step `start_url`, and SHALL remain coupled to the existing localhost callback listener and `/complete` acknowledgement contract.

#### Scenario: WebView2 mode waits for callback completion
- **WHEN** a `browser_callback_capture` step runs with `browser_mode: webview2`
- **AND** the embedded browser reaches the callback URL and posts `/complete`
- **THEN** the step completes with the captured values
- **AND** the owned modal dialog closes without launching the default external browser

#### Scenario: Operator closes the embedded browser early
- **WHEN** a `browser_callback_capture` step runs with `browser_mode: webview2`
- **AND** the operator closes the embedded browser dialog before callback completion
- **THEN** the step fails with a clear cancellation message

### Requirement: Delayed embedded browser reveal
When a `browser_callback_capture` step runs with `browser_mode: webview2`, `open_browser: true`, and `show_after_seconds > 0`, the scripting runtime SHALL start the embedded browser session without immediately surfacing the modal dialog.

The runtime SHALL reveal the owned modal dialog only if the configured delay elapses while callback completion is still pending.

#### Scenario: Callback completes before the reveal delay
- **WHEN** a `browser_callback_capture` step runs with `browser_mode: webview2` and `show_after_seconds: 5`
- **AND** the callback completes before five seconds elapse
- **THEN** the step succeeds
- **AND** the embedded browser dialog is never shown

#### Scenario: Callback remains pending past the reveal delay
- **WHEN** a `browser_callback_capture` step runs with `browser_mode: webview2` and `show_after_seconds: 2`
- **AND** callback completion is still pending after two seconds
- **THEN** SSH Helper shows the owned modal embedded browser dialog
- **AND** the step continues waiting on the existing callback completion flow

### Requirement: Callback auto-close behavior
The browser callback runtime SHALL control whether successful callback pages call `window.close()` and whether successful visible WebView2 callback dialogs auto-close based on the step `auto_close_browser` option.

When `auto_close_browser: false`, the runtime SHALL leave the successful callback surface open for inspection.

If a delayed WebView2 session never became visible because callback completion happened before reveal, the runtime SHALL still clean up that hidden session automatically.

#### Scenario: Query completion page stays open when auto-close is disabled
- **WHEN** a successful query-mode callback runs with `auto_close_browser: false`
- **THEN** the returned completion page omits `window.close()`
- **AND** the callback still completes after the existing `/complete` acknowledgement

#### Scenario: Fragment bridge page stays open when auto-close is disabled
- **WHEN** a successful fragment-mode callback runs with `auto_close_browser: false`
- **THEN** the bridge page omits `window.close()`
- **AND** it still posts the captured values and `/complete` acknowledgement before reporting success

#### Scenario: Visible WebView2 dialog stays open when auto-close is disabled
- **WHEN** a successful `browser_callback_capture` step runs with `browser_mode: webview2` and `auto_close_browser: false`
- **AND** the embedded callback dialog was shown to the operator
- **THEN** SSH Helper does not auto-close the embedded callback dialog when the step completes

#### Scenario: Hidden delayed WebView2 session still cleans up
- **WHEN** a successful `browser_callback_capture` step runs with `browser_mode: webview2`, `auto_close_browser: false`, and `show_after_seconds: 5`
- **AND** callback completion happens before the dialog is ever shown
- **THEN** SSH Helper disposes the hidden embedded session instead of leaving it open invisibly

### Requirement: Persistent embedded browser profile
The embedded browser mode SHALL use a shared app-owned WebView2 user-data folder under `%LocalAppData%\\SSH_Helper`.

The embedded profile SHALL persist across runs until the operator explicitly clears it from Settings.

#### Scenario: Embedded browser reuses prior site data
- **WHEN** an operator runs two `browser_callback_capture` steps with `browser_mode: webview2` across separate app sessions
- **THEN** the second run can reuse cookies and other site data from the first run unless the profile has been cleared

### Requirement: Embedded browser profile reset
SSH Helper SHALL provide a Settings action to clear embedded browser data for the shared WebView2 profile.

The reset action SHALL remove cookies, cache, local storage, IndexedDB, and related site data by resetting the app-owned profile.

If any embedded browser session is currently active, the reset action SHALL be blocked with an informative message instead of partially clearing live data.

#### Scenario: Clear embedded browser data while idle
- **WHEN** the operator confirms the Settings action while no embedded browser session is active
- **THEN** SSH Helper resets the shared embedded browser profile

#### Scenario: Clear embedded browser data while a session is active
- **WHEN** the operator invokes the Settings action while an embedded browser dialog is open
- **THEN** SSH Helper does not clear the profile
- **AND** shows an informative message that embedded browser data cannot be cleared during an active session
