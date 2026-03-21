## ADDED Requirements
### Requirement: Browser callback browser-mode selection
The `browser_callback_capture` step SHALL support `browser_mode` values `external` and `webview2` when `open_browser` is enabled.

If `browser_mode` is omitted, the runtime SHALL behave as `external` for backward compatibility.

Validation SHALL accept `browser_mode` values case-insensitively and SHALL report a line-specific validation error for unsupported values.

#### Scenario: Omitted browser_mode defaults to external browser
- **WHEN** a `browser_callback_capture` step sets `open_browser: true` and omits `browser_mode`
- **THEN** the runtime launches the system default external browser

#### Scenario: Explicit webview2 mode is accepted
- **WHEN** a `browser_callback_capture` step sets `open_browser: true` and `browser_mode: webview2`
- **THEN** validation accepts the step
- **AND** runtime selects the embedded browser path instead of shell-launching the default browser

#### Scenario: Invalid browser_mode is rejected
- **WHEN** a `browser_callback_capture` step sets `browser_mode: popup`
- **THEN** validation reports that `browser_mode` must be `external` or `webview2`
- **AND** the error includes the step line context

### Requirement: Browser callback launch precedence
`browser_callback_capture` SHALL preserve manual mode semantics when `open_browser: false`, regardless of `browser_mode`.

#### Scenario: Manual mode ignores browser_mode
- **WHEN** a `browser_callback_capture` step sets `open_browser: false` and `browser_mode: webview2`
- **THEN** the runtime does not auto-launch any browser surface
- **AND** the operator can still complete the callback manually against the emitted URL

### Requirement: Browser callback delayed WebView2 reveal
The `browser_callback_capture` step SHALL support an optional `show_after_seconds` non-negative integer.

If `show_after_seconds` is omitted, the runtime SHALL behave as `0`.

When `browser_mode: webview2` and `open_browser: true`, a `show_after_seconds` value greater than `0` SHALL keep the embedded browser hidden until the callback is still pending after the configured delay.

When `browser_mode` is `external` or `open_browser: false`, `show_after_seconds` SHALL be accepted but SHALL not change behavior.

#### Scenario: Omitted show_after_seconds defaults to immediate reveal
- **WHEN** a `browser_callback_capture` step omits `show_after_seconds`
- **THEN** validation accepts the step
- **AND** WebView2 mode behaves as `show_after_seconds: 0`

#### Scenario: Negative show_after_seconds is rejected
- **WHEN** a `browser_callback_capture` step sets `show_after_seconds: -1`
- **THEN** validation reports that `show_after_seconds` must be greater than or equal to `0`
- **AND** the error includes the step line context

### Requirement: Browser callback auto-close toggle
The `browser_callback_capture` step SHALL support an optional `auto_close_browser` boolean with a default of `true`.

When `auto_close_browser: false`, successful callback browser surfaces SHALL remain open for operator inspection instead of auto-closing.

In external-browser mode, the completion page SHALL remain open by omitting `window.close()`.

In `browser_mode: webview2`, SSH Helper SHALL leave a visible embedded callback dialog open after the step completes successfully until the operator closes it manually.

#### Scenario: Omitted auto_close_browser defaults to true
- **WHEN** a `browser_callback_capture` step omits `auto_close_browser`
- **THEN** validation accepts the step
- **AND** the runtime behaves as `auto_close_browser: true`

#### Scenario: Explicit false keeps the external completion page open
- **WHEN** a `browser_callback_capture` step sets `auto_close_browser: false`
- **THEN** validation accepts the step
- **AND** the callback completion page does not auto-close itself after success

#### Scenario: Explicit false keeps a visible WebView2 callback window open
- **WHEN** a `browser_callback_capture` step sets `browser_mode: webview2` and `auto_close_browser: false`
- **AND** the embedded callback dialog is shown to the operator
- **THEN** SSH Helper leaves the embedded callback window open after successful completion
