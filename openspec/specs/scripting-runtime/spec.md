# scripting-runtime Specification

## Purpose
TBD - created by archiving change update-scripting-correctness-and-diagnostics. Update Purpose after archive.
## Requirements
### Requirement: Expression and loop correctness
The scripting runtime SHALL evaluate conditional expressions and arithmetic deterministically without pre-substitution side effects.

#### Scenario: If condition with spaced variable value
- **WHEN** an `if` condition references a variable containing spaces via `${var}` syntax
- **THEN** the expression evaluator receives the original expression text
- **AND** the condition is evaluated correctly

#### Scenario: Arithmetic with mixed operators
- **WHEN** a `set` expression contains mixed operators such as `a + b * c`
- **THEN** the runtime evaluates using precedence rules
- **AND** supports parentheses for explicit grouping

### Requirement: Suppressed error observability
The scripting runtime SHALL retain suppressed error details for downstream logic.

#### Scenario: on_error continue captures last error
- **WHEN** a step fails and `on_error: continue` is configured
- **THEN** execution continues
- **AND** `_last_error` is set to the failure message

#### Scenario: successful step clears last error
- **WHEN** a subsequent step completes successfully
- **THEN** `_last_error` is cleared

### Requirement: Dynamic built-in variables
Built-in runtime variables SHALL be resolved dynamically at substitution time.

- `${_timestamp}` SHALL resolve to the current time at substitution time.
- `${_prompt}` SHALL resolve to the current detected SSH shell prompt when an SSH shell session is active.
- `${_prompt}` SHALL resolve to an empty string when no SSH shell prompt is available.
- `${_outputwindow}` SHALL resolve to the current host's pane-formatted transcript accumulated so far for the active run.
- `${_outputwindow}` SHALL use the same per-host formatted text that is appended to the operator-facing output pane, including script headers, separators, warnings, debug lines, mirrored interactive output, and command output.
- `${_outputwindow}` SHALL resolve to an empty string when no live per-host output relay is attached.

#### Scenario: timestamp changes during long script
- **WHEN** `${_timestamp}` is substituted in two different steps at different times
- **THEN** the values reflect current execution time at each substitution point

#### Scenario: prompt tracks current SSH shell prompt
- **WHEN** `${_prompt}` is substituted during SSH-backed execution
- **AND** the detected shell prompt changes during that session
- **THEN** the substituted value reflects the current detected prompt at each substitution point

#### Scenario: prompt is unavailable without SSH session
- **WHEN** `${_prompt}` is substituted before an SSH shell prompt is available
- **THEN** the substituted value is an empty string

#### Scenario: outputwindow uses per-host pane transcript
- **WHEN** a script step substitutes `${_outputwindow}` during a multi-host run
- **THEN** the substituted value contains only the current host's pane-formatted transcript accumulated so far
- **AND** it excludes output from other hosts in the same run

#### Scenario: outputwindow is unavailable without live relay
- **WHEN** `${_outputwindow}` is substituted outside a live execution relay
- **THEN** the substituted value is an empty string

### Requirement: While iteration controls
While loops SHALL support per-step iteration caps.

#### Scenario: custom max iterations on while step
- **WHEN** a while step defines `max_iterations`
- **THEN** that value overrides the default safety cap for that step

### Requirement: Foreach JSON scalar conversion
Foreach iteration over JSON arrays SHALL expose scalar items as plain string values.

#### Scenario: foreach over JSON string array
- **WHEN** a foreach loop iterates a JSON array of strings
- **THEN** each item variable is set to the scalar text value without extra JSON quotes

### Requirement: Update environment variables from script
The scripting runtime SHALL support an `updateenvironment` step that updates a named environment variable value.

#### Scenario: Updateenvironment writes value for remaining steps
- **WHEN** a script executes `updateenvironment` with both `variable` and `value`
- **THEN** the runtime requests persistence of that variable/value pair
- **AND** the script context exposes the updated value for later substitutions in the same execution

#### Scenario: Updateenvironment validates required fields
- **WHEN** an `updateenvironment` step omits `variable` or `value`
- **THEN** script validation reports an error and execution does not proceed with an incomplete updateenvironment step

### Requirement: Canonical command-map payload shape
The scripting runtime SHALL parse converted step commands from nested command-map payloads with explicit keys as the canonical contract.

Converted commands and required primary keys:
- `send.command`
- `print.message`
- `wait.seconds`
- `set.expression`
- `if.condition`
- `foreach.iterator`
- `while.condition`
- `try.do`

Optional/related keys remain command-specific (for example `send.capture`, `while.max_iterations`, `if.then`).

#### Scenario: Send command uses nested payload
- **WHEN** a step is authored as `send:` with nested `command` and optional keys
- **THEN** runtime executes the command text from `send.command`
- **AND** applies optional keys (`capture`, `suppress`, `expect`, `timeout`, `on_error`) from the same `send` map

#### Scenario: While command uses nested condition and do block
- **WHEN** a step is authored as `while:` with nested `condition`, `do`, and optional `max_iterations`
- **THEN** runtime evaluates `while.condition` each iteration
- **AND** executes nested `while.do` steps with existing loop semantics

### Requirement: Shorthand aliases for single-primary-field commands
The scripting runtime SHALL accept shorthand scalar forms for commands that have one clear primary payload field and map them to the same runtime behavior as canonical map forms.

Supported shorthand:
- `send: <command>` -> `send.command`
- `print: <message>` -> `print.message`
- `wait: <seconds>` -> `wait.seconds`
- `set: <expression>` -> `set.expression`
- `log: <message>` -> `log.message`
- `if: <condition>` -> `if.condition`
- `foreach: <iterator>` -> `foreach.iterator`
- `while: <condition>` -> `while.condition`
- `exit: <message>` -> `exit.status=success` + `exit.message`

#### Scenario: If shorthand with then block
- **WHEN** a step is authored as `if: status == "up"` with sibling `then`/`else` blocks
- **THEN** runtime evaluates the condition exactly as if authored under `if.condition`
- **AND** executes `then`/`else` blocks with unchanged control-flow semantics

#### Scenario: Exit shorthand defaults to success status
- **WHEN** a step is authored as `exit: "All checks passed"`
- **THEN** runtime terminates with success status
- **AND** uses the scalar text as the exit message

### Requirement: Canonical exit payload parsing
The runtime SHALL parse `exit` from a nested map with explicit fields.

#### Scenario: Exit status and message fields
- **WHEN** a step is authored with `exit.status` and `exit.message`
- **THEN** runtime emits the same status/message outcome currently produced by `exit` execution semantics
- **AND** script termination behavior remains unchanged

### Requirement: On-error placement within command maps
For commands that support continue/stop failure behavior, `on_error` SHALL be parsed from that command's nested map payload.

#### Scenario: Nested on_error on send
- **WHEN** `send.on_error` is set to `continue`
- **THEN** send failures are treated as suppressed failures
- **AND** execution continues according to existing suppressed-error runtime behavior

### Requirement: License-free SFTP runtime backend
The scripting runtime SHALL execute `sftp` steps using a backend that does not require Rebex SFTP licensing.

The implementation SHALL use `SSH.NET` (`Renci.SshNet`) for SFTP transfer operations while preserving the existing `sftp` step contract and failure semantics.

#### Scenario: SFTP step runs without Rebex SFTP package
- **WHEN** an operator runs a script with an `sftp` step in a build that does not include `Rebex.Sftp`
- **THEN** the runtime can still execute the transfer using `SSH.NET`
- **AND** the step continues to populate `${into}` and `${into}_bytes` according to existing behavior

### Requirement: Static SSH requirement analysis
The scripting engine SHALL provide static analysis of parsed scripts to determine SSH connection requirements before execution.

The analysis SHALL inspect `StepType` of every step in the script's step tree, including all nested step lists (`Then`, `Else`, `Elif[].Then`, `Do`, `Try`, `Catch`, `Finally`), and SHALL report:
- Whether any `send` command exists (requires SSH shell session) — `RequiresSshSession`
- Whether any `sftp` command exists (needs credentials but not SSH shell) — `UsesSftp`
- Whether any `sftp` step omits `host:` and will fall back to `Host_IP` at runtime — `SftpUsesDefaultHost`
- Whether any `sftp` step omits `username:` or `password:` and will fall back to context defaults at runtime — `SftpUsesDefaultCredentials`

The analysis SHALL be performed once after script parsing and validation, before the execution host loop begins. The result SHALL be passed through the execution call chain to avoid redundant analysis.

The analysis SHALL short-circuit (stop walking) once all detectable flags are set (`RequiresSshSession == true && UsesSftp == true`).

#### Scenario: Analysis detects send in deeply nested control flow
- **WHEN** a script contains a `send` command nested inside a `foreach.do` block inside a `try` block
- **THEN** the analysis reports `RequiresSshSession = true`

#### Scenario: Analysis reports no SSH needed for local-only script
- **WHEN** a script contains only `http`, `print`, `set`, `dns`, `ping`, `portcheck`, `webhook`, `log`, `wait`, `readfile`, `writefile`, and control-flow commands
- **THEN** the analysis reports `RequiresSshSession = false`

#### Scenario: Analysis detects sftp without send
- **WHEN** a script contains `sftp` steps but no `send` steps
- **THEN** the analysis reports `RequiresSshSession = false` and `UsesSftp = true`

#### Scenario: Analysis detects both send and sftp
- **WHEN** a script contains both `send` and `sftp` steps
- **THEN** the analysis reports `RequiresSshSession = true` and `UsesSftp = true`

#### Scenario: Empty script requires nothing
- **WHEN** a script has an empty `steps` list
- **THEN** the analysis reports `RequiresSshSession = false` and `UsesSftp = false`

#### Scenario: Analysis checks all control-flow nesting paths
- **WHEN** a script contains `send` in any of: `if.then`, `if.else`, `elif[].then`, `foreach.do`, `while.do`, `try`, `catch`, `finally`
- **THEN** the analysis detects it and reports `RequiresSshSession = true`

#### Scenario: Analysis detects sftp step using default host
- **WHEN** a script contains an `sftp` step that does not specify `host:` (the `Host` property is null or whitespace)
- **THEN** the analysis reports `SftpUsesDefaultHost = true` (the step will fall back to `Host_IP` from context at runtime)

#### Scenario: Analysis detects sftp step with explicit host
- **WHEN** a script contains an `sftp` step with `host: "10.0.0.5"` explicitly specified
- **THEN** the analysis reports `SftpUsesDefaultHost = false` (if no other sftp steps use the default host)

#### Scenario: Analysis detects sftp step using default credentials
- **WHEN** a script contains an `sftp` step that does not specify `username:` or `password:` (either property is null or whitespace)
- **THEN** the analysis reports `SftpUsesDefaultCredentials = true`

#### Scenario: Analysis detects sftp step with explicit credentials
- **WHEN** a script contains an `sftp` step with both `username:` and `password:` specified
- **THEN** the analysis reports `SftpUsesDefaultCredentials = false` (if no other sftp steps omit credentials)

#### Scenario: Multiple sftp steps — any using defaults sets the flag
- **WHEN** a script contains two `sftp` steps, one with explicit host and one without
- **THEN** the analysis reports `SftpUsesDefaultHost = true` (because any step using defaults sets the flag)

### Requirement: Interactive capture mode for long-running commands
The scripting runtime SHALL support `interactive.command` capture mode for long-running commands that are operator-stopped with `Ctrl+C`.

Capture mode contract:
- `interactive.command` SHALL auto-run once terminal startup is complete.
- Capture mode SHALL be supported only for `interactive.session=separate`.
- Step completion triggers SHALL include user `Ctrl+C`, timeout auto-interrupt (`max_seconds`), natural command completion, and early window close.
- On `Ctrl+C`, timeout, or natural completion, script execution SHALL continue while the terminal window remains open in detached read-only mode.
- Early window close before those triggers SHALL succeed with partial transcript.
- If `interactive.capture` is configured and the step succeeds, the transcript SHALL be stored into that variable and `_output`.
- `interactive.mirror_output` SHALL control whether capture chunks are mirrored into live script command output; default is disabled.

#### Scenario: Ctrl+C completes capture and script continues
- **WHEN** a script runs `interactive` with `command` and the operator presses `Ctrl+C`
- **THEN** the step completes successfully
- **AND** script execution continues to the next step
- **AND** the interactive window remains open in detached read-only mode

#### Scenario: Timeout auto-interrupt completes capture
- **WHEN** `interactive.max_seconds` is configured and elapses during capture mode
- **THEN** the runtime auto-sends `Ctrl+C`
- **AND** the step completes successfully
- **AND** script execution continues

#### Scenario: Early close keeps partial transcript
- **WHEN** the operator closes the interactive capture window before Ctrl+C/timeout/natural completion
- **THEN** the step is treated as successful partial completion
- **AND** captured transcript up to close time is retained

#### Scenario: Capture variable assignment is opt-in
- **WHEN** capture mode succeeds without `interactive.capture`
- **THEN** no user-named capture variable is written
- **AND** runtime completion remains successful

### Requirement: Interactive terminal step execution
The scripting runtime SHALL support an `interactive` step that opens an in-app terminal against the current SSH host and blocks script execution until the terminal window closes.

The step SHALL support:
- `session: separate|shared` (default `separate`)
- `emulation: full` (default `full`)

In `emulation: full`, the terminal SHALL render screen updates with terminal palette colors (foreground/background).

`session: separate` SHALL create a new SSH terminal connection using the current host execution context.

`session: shared` SHALL attach to the current script SSH shell session. The runtime SHALL NOT silently fall back to `separate` when shared attachment is unavailable.

Closing the interactive terminal window by the user SHALL be treated as successful step completion and script execution SHALL continue to the next step.

#### Scenario: User closes interactive terminal and script continues
- **WHEN** a script executes an `interactive` step and the operator closes the terminal window
- **THEN** the step is marked successful
- **AND** the next script step executes

#### Scenario: Shared session is unavailable
- **WHEN** a script executes `interactive` with `session: shared` and no shared shell session can be attached
- **THEN** the step fails with an explicit `InteractiveSharedUnavailable` error
- **AND** existing `on_error` step behavior is applied

#### Scenario: Script cancellation while interactive is open
- **WHEN** execution is canceled while an `interactive` window is open
- **THEN** the interactive window and backing terminal session are force-closed
- **AND** script execution ends with cancellation status

### Requirement: Collection-aware conditional membership
The scripting runtime SHALL support collection membership checks in conditions.

#### Scenario: Case-insensitive list membership
- **WHEN** a script evaluates `svc_key in exclude_service_matches_norm`
- **THEN** the runtime treats the right-hand side as a collection
- **AND** membership comparison is case-insensitive by default

#### Scenario: Negated collection membership
- **WHEN** a script evaluates `svc_key not in exclude_service_matches_norm`
- **THEN** the runtime returns true when the value is absent from the collection

### Requirement: Expression-backed foreach collections
The scripting runtime SHALL let `foreach` iterate any resolved collection expression, not only a named variable lookup.

#### Scenario: Foreach over split expression
- **WHEN** a script uses `foreach` with `iterator: item in split(csv_ports, ",")`
- **THEN** the loop iterates the resolved collection items in order

#### Scenario: Foreach over json.items expression
- **WHEN** a script uses `foreach` with `iterator: entry in json.items(response, "data.tags")`
- **THEN** the loop iterates the resolved JSON-derived items without requiring a temporary variable

### Requirement: Structural collection semantics
The scripting runtime SHALL treat lists and JSON containers as structural collections for length, emptiness, and truthiness checks.

#### Scenario: JSON array length uses element count
- **WHEN** a script evaluates `length(json.items(response, "check"))` or `${check_items.length}`
- **THEN** the result reflects the number of elements rather than the raw JSON string length

#### Scenario: Empty JSON collection is empty
- **WHEN** a script evaluates `items is empty` where `items` is an empty JSON array or object
- **THEN** the runtime treats the collection as empty

### Requirement: Readfile manual file picker
The scripting runtime SHALL support an explicit `readfile.select_file` mode that lets an operator choose the file to read during manual execution.

When `readfile.select_file` is `true`:
- the runtime SHALL show a file-selection prompt before reading;
- `readfile.message`, when provided, SHALL replace the default prompt text shown in that file-selection prompt;
- `readfile.path`, when provided, SHALL be used only as the initial seed for the prompt;
- `readfile.fileext`, when provided, SHALL restrict the browse dialog to those extensions and SHALL reject any final resolved path that does not match one of the allowed extensions;
- the selected file SHALL still pass the existing read-path validation and line-processing rules.

#### Scenario: Manual run selects a file to read
- **WHEN** a manual script execution runs a `readfile` step with `select_file: true`
- **AND** the operator chooses a file
- **THEN** the runtime reads the selected file
- **AND** stores the processed lines into the configured `into` variable

#### Scenario: File selection is cancelled
- **WHEN** a `readfile` step with `select_file: true` is prompted during a manual run
- **AND** the operator cancels the prompt
- **THEN** the runtime sets the `into` variable to an empty list
- **AND** the script stops immediately with a cancelled status

#### Scenario: Scheduler-triggered execution reaches picker mode
- **WHEN** a scheduler-triggered execution runs a `readfile` step with `select_file: true`
- **THEN** the runtime does not open a file-selection prompt
- **AND** the step fails with a manual-run-only error unless `on_error: continue` is configured

#### Scenario: Manual run customizes the picker prompt and file types
- **WHEN** a manual script execution runs a `readfile` step with `select_file: true`
- **AND** the step provides `message` and `fileext`
- **THEN** the prompt shows the custom message text
- **AND** the picker accepts only files matching the configured extensions

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
The embedded browser mode SHALL use a shared app-owned WebView2 user-data folder under the application storage root.

The application storage root SHALL be `%LocalAppData%\\SSH_Helper` for standard builds and the executable directory for portable builds.

The embedded profile SHALL persist across runs until the operator explicitly clears it from Settings.

#### Scenario: Standard build stores embedded browser data under LocalAppData
- **WHEN** SSH Helper runs as a standard build
- **THEN** embedded browser profile data is persisted under `%LocalAppData%\\SSH_Helper`

#### Scenario: Portable build stores embedded browser data beside executable
- **WHEN** SSH Helper runs as a portable build
- **THEN** embedded browser profile data is persisted under the executable directory

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

### Requirement: Local audio playback command
The scripting runtime SHALL support a `playsound` command that plays a local audio file.

The command SHALL resolve the playback path using script substitution and Windows environment-variable expansion before playback.

Supported options:
- `path` (required): input file path expression.
- `wait` (optional): whether to block step completion until playback finishes, default `true`.
- `volume` (optional): playback volume percentage from `0` to `100`, default `100`.
- `max_seconds` (optional): positive playback timeout when `wait=true`.
- `into` (optional): output variable name for success status and metadata.
- `on_error` (optional): `stop` (default) or `continue`.

#### Scenario: Wait mode blocks until playback completes
- **WHEN** a `playsound` step resolves to an existing supported file
- **AND** `wait` is omitted or `true`
- **THEN** the step completes only after playback completion
- **AND** execution proceeds to the next step

#### Scenario: Background mode returns immediately
- **WHEN** a `playsound` step sets `wait: false`
- **THEN** playback is started
- **AND** the step completes without waiting for playback to finish

#### Scenario: Missing file follows on_error behavior
- **WHEN** a `playsound` step resolves to a non-existent file
- **THEN** the step is treated as a failure condition
- **AND** runtime applies standard `on_error` behavior

### Requirement: Playsound metadata output contract
When `into` is provided, the runtime SHALL publish command outputs for `playsound`.

The runtime SHALL set:
- `${into}` to a boolean success value
- `${into}_meta` to an object including:
  - `path` (resolved playback path)
  - `wait` (effective wait mode)
  - `volume` (effective volume)
  - `backend` (playback backend identifier)
  - `error` (string, only when an operational error occurs)

#### Scenario: Into outputs are published on success
- **WHEN** a `playsound` step succeeds with `into` configured
- **THEN** `${into}` is set to `true`
- **AND** `${into}_meta.path` contains the resolved path

#### Scenario: Into outputs are published on suppressed error
- **WHEN** playback fails and `on_error: continue` is configured with `into`
- **THEN** `${into}` is set to `false`
- **AND** `${into}_meta.error` contains the error summary
- **AND** execution continues

### Requirement: Scoped Step Identity for Runtime Events
The scripting runtime SHALL emit a scope-aware `StepPath` identity for step lifecycle and debug pause/resume events.

`StepPath` SHALL uniquely identify steps across nested control-flow scopes.

#### Scenario: Nested step emits unique path
- **WHEN** execution enters a nested step inside control-flow containers
- **THEN** emitted step events include a `StepPath` distinct from top-level siblings

#### Scenario: Debug pause includes scoped identity
- **WHEN** debug pause state changes for a nested step
- **THEN** the pause/resume event includes `StepPath`
- **AND** callers can resolve the corresponding canvas node without relying on flat local step index

### Requirement: Local path existence command
The scripting runtime SHALL support an `exists` command that checks whether a local path exists.

The command SHALL resolve paths using script substitution and Windows environment-variable expansion before evaluation.

Supported options:
- `path` (required): input path expression.
- `into` (required): output variable name for boolean result.
- `type` (optional): `any` (default), `file`, or `directory`.
- `on_error` (optional): `stop` (default) or `continue`.

#### Scenario: Existing file returns true
- **WHEN** an `exists` step targets a path that resolves to an existing file
- **AND** the step type is `any` or `file`
- **THEN** `${into}` is set to `true`
- **AND** `${into}_meta.exists` is `true`
- **AND** `${into}_meta.is_file` is `true`

#### Scenario: Existing directory returns true
- **WHEN** an `exists` step targets a path that resolves to an existing directory
- **AND** the step type is `any` or `directory`
- **THEN** `${into}` is set to `true`
- **AND** `${into}_meta.exists` is `true`
- **AND** `${into}_meta.is_directory` is `true`

#### Scenario: Missing path returns false without failing
- **WHEN** an `exists` step targets a path that does not exist
- **THEN** `${into}` is set to `false`
- **AND** `${into}_meta.exists` is `false`
- **AND** the step completes successfully

### Requirement: Exists metadata output contract
The runtime SHALL publish metadata to `${into}_meta` for every `exists` step completion.

The metadata object SHALL include:
- `exists` (boolean)
- `is_file` (boolean)
- `is_directory` (boolean)
- `path` (resolved path string)
- `type` (effective type mode)
- `error` (string, only when an operational error is suppressed)

#### Scenario: Metadata includes resolved path
- **WHEN** an `exists` step runs with path variables
- **THEN** `${into}_meta.path` stores the fully resolved path used by the check

### Requirement: Exists error handling behavior
The runtime SHALL apply standard `on_error` behavior for operational errors in `exists`.

Operational errors include invalid path normalization failures and I/O exceptions raised while checking existence.

#### Scenario: Suppressed operational error preserves outputs
- **WHEN** an `exists` step encounters an operational error
- **AND** `on_error: continue` is configured
- **THEN** execution continues
- **AND** `${into}` is set to `false`
- **AND** `${into}_meta.error` contains the error summary
- **AND** `_last_error` is set according to suppressed-error behavior

### Requirement: Two-phase script runtime execution
The scripting runtime SHALL support two ordered phases per host: `preconnect` then `steps`.

When a script does not define `preconnect`, the runtime SHALL execute only the `steps` phase.

#### Scenario: Ordered phase execution
- **WHEN** a script defines both `preconnect` and `steps`
- **THEN** `preconnect` executes fully before `steps`
- **AND** variables set in `preconnect` are available to `steps` for that host

### Requirement: Preconnect output and cancellation semantics
Preconnect phase execution SHALL follow existing command result semantics for success, failure, `on_error`, and cancellation.

#### Scenario: Preconnect cancelled by operator
- **WHEN** execution is cancelled during preconnect
- **THEN** host execution ends as cancelled
- **AND** main steps do not start for that host

### Requirement: Sensitive override value handling
The runtime SHALL treat reserved SSH override variables as sensitive and SHALL avoid emitting their raw values in normal script output or debug traces.

#### Scenario: Override variable is set in preconnect
- **WHEN** preconnect sets `_ssh_password` or `_ssh_identity_passphrase`
- **THEN** output/debug streams do not include the raw secret value

### Requirement: Unified interpolation scanner
Variable interpolation SHALL use a single balanced-brace scanner for both `{{ }}` and `${ }` forms, with `{{ }}` as the canonical form and `${ }` as a supported alias.

#### Scenario: Consistent nested and adjacent handling
- **WHEN** a value contains nested or adjacent interpolations in either `{{ }}` or `${ }` form
- **THEN** both forms are scanned with identical balanced-brace rules (there is no escape syntax; backslashes pass through literally in both forms)

#### Scenario: Canonical and alias forms are equivalent
- **WHEN** the same expression is written as `{{ expr }}` or as `${ expr }`
- **THEN** both forms resolve to the same value

