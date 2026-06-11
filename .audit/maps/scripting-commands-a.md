# Feature Map: Script Commands A–L (`Services/Scripting/Commands/`)

Scope: command handlers whose filename starts A–L, plus their shared plumbing (`ChoiceOptionResolver`, `ScriptPromptDialogRunner`, `ILocalCmdConfirmation`, `BrowserCallbackFocusRestorer`) and the option models / parser entries that define their YAML syntax. All paths relative to repo root `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper`.

Dispatch model: every command implements `IScriptCommand.ExecuteAsync(ScriptStep, ScriptContext, CancellationToken) → CommandResult` (`Services/Scripting/Commands/IScriptCommand.cs:11-21`). Registration is manual and enum-keyed in `ScriptExecutor`'s ctor dictionary (`Services/Scripting/ScriptExecutor.cs:128-173`). `CommandResult` carries control flow (`ShouldExit/Break/Continue/Return`), `SuppressedError` (on_error: continue), `IterationCount` (loops) and `BranchTaken` (if/switch — must match Flow Canvas `edge.data.branchPath` vocabulary) (`IScriptCommand.cs:26-142`). `CommandResult.ApplyOnError(step, msg)` (`IScriptCommand.cs:136-141`) is the standard error funnel: returns `Suppressed` when `on_error: continue`, else `Fail`. The executor wraps every step with retry (`retry`/`retry_delay`, on_error forced to "stop" on non-final attempts — `ScriptExecutor.cs:445-494`), catches handler exceptions into `ApplyOnError` (`ScriptExecutor.cs:517-531`), and writes `_last_error` on failure/suppression (`ScriptExecutor.cs:412-429`).

YAML key→options parsing is in `Services/Scripting/ScriptParser.cs` (step-key switch at 905-1155; per-command allowed-key catalog at 113-145 used for validation/autocomplete).

---

## Feature inventory

### assert — condition gate with soft/hard severity
- `Services/Scripting/Commands/AssertCommand.cs:13-57`. YAML: `assert: { condition|that: "expr", message: "...", severity: error|warning }`; shorthand `assert: "expr"` (`ScriptParser.cs:3487-3536`, shorthand at 3525-3529, `that:` alias at 3503).
- Substitutes variables into the condition text **before** evaluating with `ExpressionEvaluator` (line 18) — unlike `if` which passes raw text.
- `severity: warning` records pass/fail counters via `context.RecordSoftAssert` (`AssertCommand.cs:38,50`; counters in `ScriptContext.cs:128-156`) and never fails the step; `error` (default) emits `[ASSERT FAILED]` and returns `Fail` (55-56). Evaluation exceptions are caught and fail the step (26-31). Does **not** honor `on_error` itself (no `ApplyOnError`); parser accepts `on_error` key under assert though (3512-3515).

### break / continue — loop control
- `BreakCommand.cs:12-19`, `ContinueCommand.cs:12-19`. YAML: `break: true`, `continue: true` (booleanish, `ScriptParser.cs:957-964`).
- Runtime-guarded: fail with "can only be used inside a loop" when `context.LoopDepth <= 0` (line 14-15 of each). Signal travels via `CommandResult.Break()/Continue()` and is consumed by the loop commands.

### browser_callback_capture — local OAuth/browser callback listener
- `BrowserCallbackCaptureCommand.cs:37-217`. YAML keys (`ScriptParser.cs:129`, options model `Models/ScriptStep.cs:1070-1141`): `start_url` (req), `callback_path` (req, default `/oauth_callback`), `local_port` (default **8086**), `capture_mode` auto|fragment|query|post_body (489-499), `browser_mode` external|webview2 (501-506), `show_after_seconds` (WebView2 stays hidden until pending this long), `into` (req), `required_fields`, `timeout` (default 300s), `open_browser` (default true), `auto_close_browser` (default true), `completion_message`, `failure_message`, `quiet` (default true).
- Spins an `HttpListener` on `http://127.0.0.1:{port}/` (`CreateListener` 219-224). GET on callback path serves a self-posting capture page that forwards query (`q:` prefix) + fragment (`h:` prefix) values back via POST (`BuildCaptureHtml` 544-593); `/capture` and `/complete` subpaths finalize (`CaptureAsync` 226-309). Query-mode short-circuits on GET with querystring (249-259).
- Browser UX: external default-browser launch, or embedded WebView2 dialog (`Services/Scripting/BrowserCallbackUiHost.cs:91+`, profile manager `BrowserCallbackWebViewProfileManager.cs`). WebView2 window closed-by-user races the capture and fails the step (127-135). External mode triggers `BrowserCallbackFocusRestorer.RequestApplicationFocusRestore()` after capture (146-149) — an `AttachThreadInput`/`SetForegroundWindow` focus-steal with retry intervals {350,650,1000,1500,2200}ms (`BrowserCallbackFocusRestorer.cs:56-119`).
- Capture persistence (`PersistCapture` 436-451): `into` = sorted JSON object, `into_count`, `into_keys`, plus one `into_{sanitized_key}` var per field (suffix sanitizer 475-487). Prior `into_*` variables are swept before each run (`ClearCapture` 453-473). `required_fields` validated post-capture (152-168). All failures funnel through `ApplyOnError`.
- Dialog dark mode keyed off active form brightness `< 0.5f` (687-703).

### call — subroutine invocation
- `CallCommand.cs:20-54`. YAML: `call: { subroutine: name, args: {k: expr}, out: {caller_var: sub_output}, on_error }` — must be a mapping; `subroutine` required (`ScriptParser.cs:1736-1784`); `args`/`out` must be scalar maps (1786-1814).
- Resolves through `context.SubroutineRegistry.TryResolve` (supports imported/qualified names, relative to `context.CurrentSubroutine`) (28-32); args evaluated by `ValueResolver.ResolveExpressionValue` before entering the child scope (34-38); delegates to `_executor.ExecuteSubroutineAsync` (isolated child scope + declared outputs) (40-45). `on_error: continue` converts failure to `Suppressed` (47-51). Container command — constructed with the executor (`ScriptExecutor.cs:166`); listed in `ContainerStepTypes` for output attribution (`ScriptExecutor.cs:94-98`).

### choose — single-select prompt dialog
- `ChooseCommand.cs:17-79` + `ScriptChooseDialog` (87-249). YAML: `choose: { title, prompt, into (req), options: [label/value pairs | strings] | <var ref>, default, font_size, on_error }` (`ScriptParser.cs:2351-2405`). The `options:` key doubles as `options_from` when given a scalar (`ParseChoiceOptions` shared with multiselect, 2376/2432).
- Option resolution: `ChoiceOptionResolver.Resolve` (`ChoiceOptionResolver.cs:18-34`) accepts inline label/value lists (labels and values cross-default to each other, 137-167), `${var}` / `{{var}}` whole-token refs, bare variable names, or substituted strings; string sources are parsed as JSON arrays first, then comma/newline-split (210-260). Runtime lists (`List<string>`, `List<ChoiceOption>`, any `IEnumerable`) are supported (125-188).
- Dialog UX: list box capped at 10 visible rows (112), double-click = OK (188-195), OK disabled until a selection exists (162-186), default pre-selected by value (OrdinalIgnoreCase, 149-160), per-step `font_size` clamped 7–36pt scaling the whole dialog (101-103, only scales **up**: `Math.Max(1f, ...)` at 102), ambient font family from main form (105-107), dark theme + owner-drawn items when main form brightness `< 0.2f` (205-217, 227-249). Shown modeless via `ScriptPromptDialogRunner`.
- Cancel returns plain `Fail("Selection cancelled by user")` (58-62) — bypasses `on_error`; other exceptions honor `ApplyOnError` (73-79). Result stored as string into `into` (64).

### confirm — yes/no prompt dialog
- `ConfirmCommand.cs:16-58` + `ScriptConfirmDialog` (64-137). YAML: `confirm: { title, prompt, into (req), default: true|false, font_size, on_error }` (`ScriptParser.cs:2468-2520`).
- Stores literal string `"true"`/`"false"` into `into` (41-42). `default` chooses which button is `AcceptButton`/focused and styled primary (113, 122-123, 127-135). ESC/window-close maps to No — there is **no cancel/abort path** from a confirm; the script always proceeds with false.

### dns — DNS resolution
- `DnsCommand.cs:29-116`. YAML: `dns: { host (req), type: A|AAAA|PTR (default A), timeout (default 10s), into, on_error }` (`ScriptParser.cs:131,3155`).
- Uses system resolver (`System.Net.Dns`) behind an injectable `IDnsResolver` seam for tests (137-154). A/AAAA filter address family and de-dupe (60-79); PTR returns hostname + aliases (80-89). Host-not-found / no-data is **success with empty list** (104-110); timeout and other errors go through `ApplyOnError` (99-115). Captures `into` (List<string>) + `into_count` (125-132); capture is written (empty) even on the failure paths.

### exists — local path existence check
- `ExistsCommand.cs:15-54`. YAML: `exists: { path (req), into, type: any|file|directory, on_error }` (`ScriptParser.cs:2168-2211`).
- Path gets variable substitution **and** `%ENV%` expansion (27), then `Path.GetFullPath` (36). Captures `into` = **bool** plus `into_meta` dictionary (`exists/is_file/is_directory/path/type[/error]`) (67-95); failure paths still write the capture with `error` populated (97-116). Oddity: `type` is also run through `Environment.ExpandEnvironmentVariables` (24). No `ScriptFileAccessValidator` gating (contrast `ReadFileCommand.cs:91`, `WriteFileCommand.cs:54`).

### exit — terminate script with status
- `ExitCommand.cs:13-74`. YAML: `exit: success|failure|fail|error [message]` or bare `exit: message`; mapping form `{status, message}` also parses (`ScriptParser.cs:113,1399`).
- English keyword prefix parsing (24-64), surrounding quotes trimmed (76-89). **Bare messages default to `Success` status** (21-22). Emits `[EXIT {status}] msg` as Success/Error output and returns `CommandResult.Exit` (66-73).

### extract — regex capture into variables
- `ExtractCommand.cs:15-120`. YAML: `extract: { from (req var name), pattern (req), into (req; string or list of strings), match: first|last|all|<index>, required: true(default)|false }` (`ScriptParser.cs:114,1898`).
- Pattern gets variable substitution, then optional `/.../`, `'...'`, `"..."` delimiter stripping (45-56). Regex always compiled with **hardcoded `Multiline | IgnoreCase`** plus `ScriptRegexDefaults.UserPatternTimeout` (60). `required: true` fails on empty source or zero matches; `false` sets empties and continues (33-42, 63-72).
- Single `into`: group 1 (or whole match) trimmed (126-132). List `into`: positional groups 1..N mapped to the names (133-144). `match: all`: list-of-values per variable (147-193). Named groups are **always auto-exported as variables of the same name** (`ExposeNamedGroups` 195-208). Match index out of range → warning + empties, still Ok (87-99). Regex parse/timeout errors fail the step (108-115); no `ApplyOnError` usage anywhere in this command.

### foreach — collection / map iteration
- `ForeachCommand.cs:53-97` (container; executor-recursive). YAML: `foreach: "item in collection"` or `"key, value in map"` + `do:` block (req) + optional `when:` per-item filter (`ScriptParser.cs:116,1531`). Grammar regexes at 20-21; `IsValidIteratorSyntax` (33-40) shared with parse-time validation.
- Items resolved via `ValueResolver.ResolveCollectionExpression` → `List<string>` (193-196); map form via `JsonUtilities.GetJsonObject` with values stringified (198-205) — iteration values are always strings.
- Per-iteration metadata vars: `{var}_index/_number/_first/_last/_count` (111-118, 141-145). **Block scoping**: prior values of all loop-written vars saved and restored/removed on exit (120-124, 179-190).
- Pushes/maintains an iteration frame (`PushIterationFrame`/`SetCurrentIterationFrame`, 132/146) so the canvas iteration stepper can attribute nested events; labels truncated to 48 chars surrogate-safely (42-51). Handles break/continue/exit/return and returns `IterationCount` (155-177). `when:` is substituted then evaluated (148-153) — filtered items don't count toward `IterationCount`.

### http — general HTTP client
- `HttpCommand.cs:50-207`. YAML (`ScriptParser.cs:128,2902`; model `ScriptStep.cs:989-1065`): `http: { url (req, absolute http/https), method GET..OPTIONS (22-25), body, headers: {}, into, timeout (30s), follow_redirects (true), allow_failure, verify_tls (true), auth: none|basic|bearer, username, password, token, content_type: json|form|text|xml, on_error }`.
- Validation up front: URL/method/auth/content_type whitelists each `ApplyOnError` (62-87). Basic auth requires non-empty username and password (100-110); bearer requires token (111-119). Explicit `Content-Type` header overrides the shorthand (121-137). Headers applied with `TryAddWithoutValidation`, falling back to content headers (225-243).
- Captures (`into`, `into_status`, `into_headers` JSON, `into_api_ms`, `into_total_ms`) — cleared to empty at step start to avoid staleness (59, 245-274). Timing via two stopwatches (57, 153-160).
- Non-2xx → `ApplyOnError` unless `allow_failure: true` (then `Ok` with the error as message, 178-187). Timeout/transport/any errors → `ApplyOnError` (192-206).
- `verify_tls: false` installs `DangerousAcceptAnyServerCertificateValidator` (320-333). A fresh `HttpClient` + handler is constructed per step (92-93).
- Debug output is verbose: request options incl. `basic username={username}` (109, 143-144), full serialized request headers — **including the `Authorization` header** (145 with `SerializeHeaders(request)` 298-315), request body (146-149), response status/headers/body (171-176).

### if / elif / else — branching (container)
- `IfCommand.cs:19-93`. YAML: `if: "expr"` + `then:`/`elif: [{if|condition, then}]`/`else:` (`ScriptParser.cs:941-944, 1135-1139, 1849-1896`; elif accepts both `if` and `condition` keys, 1876-1878).
- Condition passed **raw** to `ExpressionEvaluator` (24-28; evaluator handles var refs itself — contrast assert). Branch bodies executed via `_executor.ExecuteStepsAsync` preserving `LoopDepth`. Sets `BranchTaken` to `then` | `elif/{i}/then` | `else` | null (no branch ran) (32-92) — the Flow Canvas path-highlight contract. Control-flow/failure results from a branch propagate with `BranchTaken` attached (41-45, 67-75, 84-88). Blank elif conditions are skipped silently (55-56).

### input — free-text prompt dialog
- `InputCommand.cs:16-84` + `ScriptInputDialog` (90-222). YAML: `input: { title, prompt, into (req), default, password: bool, validate: regex, validation_error, font_size, on_error }` (`ScriptParser.cs:124,2287-2349`).
- `validate` regex compiled with `RegexOptions.Compiled` and **no match timeout** (41); invalid pattern fails the step up front (43-46). Validation runs in-dialog on OK with inline red error label and refocus/select-all (204-221), error cleared on typing (198-201). `password: true` masks via `UseSystemPasswordChar` (141). Cancel → plain `Fail("Input cancelled by user")` bypassing `on_error` (60-65); other exceptions use `ApplyOnError` (77-83).

### interactive — in-app SSH terminal step
- `InteractiveCommand.cs:18-68`; heavy lifting in `Services/Terminal/InteractiveTerminalService` (out of scope). YAML (`ScriptParser.cs:139,2522`; model `ScriptStep.cs:828-904`): `interactive: { session: separate|shared, title, command, capture, max_seconds, max_lines, width, height, mirror_output, show_window (default true), on_error }`; `columns`/`rows` deprecated in favor of pixel sizing (877-883); separate-window defaults 980×620 (862-871).
- Blocks the script until the terminal closes. `command` auto-runs and enables capture mode; `max_seconds`/`max_lines` auto-send Ctrl+C as safety limits. Transcript mirroring to the script output stream (`ScriptOutputType.RawChunk`) happens only when `mirror_output: true` **and no `command` is set** (31-41); `capture` records the transcript via `context.RecordCommandOutput` (43-46). Failure honors `on_error: continue` → `Suppressed` (57-60).

### localcmd — local process execution (foreground/background/interactive)
- `LocalCmdCommand.cs` (1271 lines). YAML (`ScriptParser.cs:145,3781`; model `ScriptStep.cs:1600-1641`): `localcmd: { command (req), shell: powershell(default)|cmd|custom, shell_path (req for custom, 552-559), args: [], env: {}, working_dir, interactive, keep_open, run_mode: foreground|background, lifetime: script|app|detached, kill_on_cancel, fail_on_nonzero (default true), success_codes (default [0]), max_output_bytes (default 1 MiB, 17), confirm: always(default)|once|never, quiet, suppress, title, into, timeout, on_error }`. `interactive: true` + `run_mode: background` are mutually exclusive (57-58).
- **Confirmation UX** (`HandleConfirmation` 104-146 + `UI/LocalCmdConfirmationDialog.cs:9-148`): unless `confirm: never`, a dialog shows the resolved command in a read-only Consolas box plus shell/working-dir, with buttons Run / "Run Same Command" (approves this exact resolved command for the current host for the run) / Cancel. Approval state lives on `ScriptContext` (`LocalCmdRunAllApproved`, `LocalCmdApprovedHost` single value, `LocalCmdApprovedCommands` set; 114-143). Production wiring: `SshExecutionService` instantiates `LocalCmdConfirmationDialog` (`Services/SshExecutionService.cs:305,328,340`) and exposes `SetLocalCmdConfirmation` (343). Missing provider + confirm required = hard error (127-129). Cancel → `Fail` (75-76).
- **Foreground** (148-282): redirected stdout/stderr streamed line-by-line to output (Command/CommandOutput vs Warning types) and captured with byte-budget truncation + marker (196-231, marker at 19); per-step `timeout:` enforced via linked CTS (236-252); captures `into_stdout/_stderr/_exit_code` (258-263) and step-level `capture:` records stdout (265-266); `fail_on_nonzero` checked against `success_codes` (268-278).
- **Background** (284-360): no redirection; captures `into_pid/_started/_start_error` (318-342); `lifetime` normalization (1046-1054: anything not script/app = detached); script/app-lifetime processes tracked per-context and killed on cleanup/cancel/app-exit (`RegisterBackgroundProcess`/`CleanupTrackedBackgroundProcesses`/`CleanupAppLifetimeProcesses` 1056-1137; `ProcessExit` hook 28-31); `kill_on_cancel` honored.
- **Interactive** (362-522): opens a real terminal window — prefers Windows Terminal `wt.exe` (found via WindowsApps/PATH, 635-651) except for powershell/cmd which are launched directly so the process lifetime and transcript are trackable (565-611). `keep_open` uses `-NoExit`/`/K` (613-633, 988-995). Transcript audit: PowerShell `Start-Transcript`, cmd wrapped as `( cmd ) 2>&1 | powershell Tee-Object` (673-701, 757-776); transcript cleaned + size-limited and recorded as an `InteractiveTerminalSessionDetails` via `context.AddInteractiveSession` (900-942) for execution-details history. Exit code `0xC000013A` (user closed window) is treated as non-failure (18, 491-513). `lifetime: detached` + interactive skips audit/wait, warns `fail_on_nonzero` is unevaluable (407-436).
- Shell arg construction details: PowerShell commands run via `-EncodedCommand` with `$ProgressPreference='SilentlyContinue'` prelude (543, 794-801); quoted-`.exe` invocations bypass the shell entirely (533-541, 832-886); Windows-rule argument quoting (1002-1044). Env vars from `env:` substituted and applied (1165+).

### log — leveled output
- `LogCommand.cs:13-64`. YAML: `log: "msg"` (info) or `log: { message, level: debug|info|warning|warn|error|err|success }` (`ScriptParser.cs:127,2786`); a raw `IDictionary<object,object>` shape is also tolerated (31-39). Variables substituted; empty message is a silent no-op Ok (42-43); unknown levels fall back to info (54-64).

### Shared infrastructure in this area
- **`ScriptPromptDialogRunner`** (`Commands/ScriptPromptDialogRunner.cs`): shows all script prompt dialogs (input/choose/multiselect/confirm/localcmd-confirm) **modeless** on the UI thread via TCS so the main form's Stop button stays usable (30-128). While a prompt is open, `MainFormPromptLock` disables every main-form control except the tree containing the control literally named `"btnStopAll"` (370-442, name probe at 384-400). Dialogs are centered over the anchor form, re-centered after autoscale (256-294); `AnchorFormOverride` lets Flow Canvas own the prompts (16-19); `DefaultPromptFontSize` is fed from `FontSettings.ScriptPromptFontSize` by Form1 (21-26). Cancellation tokens close open dialogs (217-254); main form re-activated after close (296-334).
- **`ChoiceOptionResolver`** (above, under choose).
- **`ILocalCmdConfirmation`** (`Commands/ILocalCmdConfirmation.cs:6-16`): Run/RunAll/Cancel seam, UI impl in `UI/LocalCmdConfirmationDialog.cs`.
- **`BrowserCallbackFocusRestorer`** (`Commands/BrowserCallbackFocusRestorer.cs`): Win32 foreground restoration with injectable native seam for tests (8-24).
- **`JsonFunctions.cs`** (`Commands/JsonFunctions.cs`, 769 lines): **not a command** — static helpers backing expression functions (`Constructor/Get/Set/Delete/Merge/Format/Exists/Len/Type/Push/Pop/Shift/Unshift/First/Last/Slice/Concat/IndexOf`, lines 19-619), wired via `FunctionRegistry`. Lives in the Commands folder by historical accident (also noted in CLAUDE.md).
- **`ScriptingHelpers`** (`IScriptCommand.cs:147-175`): newline-flattening display formatting used by banners/debug lines.

---

## Integration points

- **ScriptExecutor** (`Services/Scripting/ScriptExecutor.cs:128-173`): adding a command requires a `StepType` enum member + dictionary entry. Container commands (`If`, `Foreach`, `Call` here) get `this` and re-enter `ExecuteStepsAsync`/`ExecuteSubroutineAsync`; `ContainerStepTypes` (94-98) controls per-block output attribution for the canvas.
- **Flow Canvas contract**: `BranchTaken` strings emitted by `IfCommand` (36, 63, 82) and `IterationCount`/iteration frames from `ForeachCommand` (132, 146, 160-177) surface on `StepExecutionEventArgs` (`ScriptExecutor.cs:396-410`) and drive path highlighting and the iteration stepper. Breaking the vocabulary breaks canvas highlighting.
- **ScriptContext**: variable store (`SetVariable`/`RemoveVariable`/`SubstituteVariables`), output sink (`EmitOutput` with `ScriptOutputType` levels), `RecordCommandOutput` (interactive/localcmd capture feeding history), `AddInteractiveSession` (localcmd interactive audit → execution-details history), `RecordSoftAssert` counters, localcmd approval state, `LoopDepth`, iteration frames (AsyncLocal, parallel-safe).
- **UI layer**: all prompts route through `ScriptPromptDialogRunner` and theme via `DialogTheme`; `SshExecutionService` injects `LocalCmdConfirmationDialog` (`SshExecutionService.cs:305-343`); Flow Canvas sets `AnchorFormOverride`; `FontSettings.ScriptPromptFontSize` flows into `DefaultPromptFontSize`.
- **Parser** (`ScriptParser.cs`): per-command allowed-key tables (113-145) feed editor validation/autocomplete; `ForeachCommand.IsValidIteratorSyntax` is reused at parse time; nested `on_error` aliases hoist to the step root (`ApplyNestedOnErrorAlias`).
- **WebView2 / OS**: `BrowserCallbackCaptureCommand` → `BrowserCallbackUiHost` (WebView2 dialog, dedicated user-data profile via `BrowserCallbackWebViewProfileManager.Shared`) and `HttpListener`; `LocalCmdCommand` → `wt.exe`, powershell.exe, cmd.exe, `AppDomain.ProcessExit` cleanup hook (28-31).

---

## Observed gaps & quirks

Security / secret hygiene
1. **HTTP debug output echoes the `Authorization` header** — `SerializeHeaders(request)` (HttpCommand.cs:298-315) includes all request headers and is emitted at HttpCommand.cs:145; basic-auth Base64 and bearer tokens land in script output. The auth summary also echoes the basic username (109). Response headers/body are dumped too (174-176) — `Set-Cookie`/tokens leak.
2. `verify_tls: false` silently accepts any cert (HttpCommand.cs:327-330) with no output warning at runtime.
3. `browser_callback_capture` has **no nonce/state validation**: any local process can POST form data to `127.0.0.1:8086/<path>/capture` and complete the step with spoofed values (CaptureAsync 282-305). Listener is loopback-only (219-224), which limits but does not eliminate the risk.
4. `localcmd confirm: never` permits unattended arbitrary local execution; there is no global "always require confirmation" override above the per-step setting (HandleConfirmation 107-110).
5. `exists` performs no `ScriptFileAccessValidator` gating, unlike readfile/writefile (ExistsCommand.cs vs ReadFileCommand.cs:91, WriteFileCommand.cs:54) — scripts can probe arbitrary filesystem paths.

Error-handling inconsistencies
6. **User-cancel bypasses `on_error: continue`** in `choose` (ChooseCommand.cs:58-62) and `input` (InputCommand.cs:60-65) — plain `Fail`, while every other failure in those commands honors `ApplyOnError` (78/82). Possibly intentional ("cancel = abort") but undocumented and inconsistent with `confirm`, which offers **no way to cancel at all** (ScriptConfirmDialog maps close/ESC to No, ConfirmCommand.cs:113-114).
7. `assert` and `extract` never consult `on_error` (`AssertCommand.cs:56`, `ExtractCommand.cs:67,110-118`) even though the parser accepts `on_error` under assert (ScriptParser.cs:3512) — a declared `on_error: continue` there is honored only via the executor's exception catch, not command-level failures.
8. `if` evaluates the raw condition while `assert` textually substitutes variables first (IfCommand.cs:24-28 vs AssertCommand.cs:18) — substitute-then-parse can break on values containing quotes/operators and behaves differently between the two commands for identical expressions.
9. Unknown step types are skipped at runtime with only a warning and `Ok` (ScriptExecutor.cs:506-510).

Regex / parsing
10. `input.validate` regex is compiled **without** `ScriptRegexDefaults.UserPatternTimeout` (InputCommand.cs:41) — inconsistent with extract (ExtractCommand.cs:60); a pathological user pattern can hang the UI thread on each OK click.
11. `extract` hardcodes `IgnoreCase | Multiline` (ExtractCommand.cs:60) — no case-sensitive option for precise device-output parsing.
12. `extract` auto-exports every named group as a context variable (ExposeNamedGroups 195-208), silently overwriting existing variables of the same name.
13. `extract` list-form `into` with `match: all` maps groups purely positionally and drops groups beyond the var list (161-192); index out-of-range is a warning + empty values + `Ok` (87-99) — divergent from `required` semantics.

UX gaps (prompts / dialogs)
14. `choose` dialog has no search/filter and a 10-row viewport (ChooseCommand.cs:112) — painful for realistic lists (e.g., picking a host out of hundreds).
15. Font-size scaling only scales up from 9pt (`Math.Max(1f, ...)` ChooseCommand.cs:102, ConfirmCommand.cs:69, InputCommand.cs:112) — `font_size: 7` is accepted but the dialog doesn't shrink.
16. Dark-mode detection thresholds differ: prompts use main-form brightness `< 0.2f` (ChooseCommand.cs:207 etc.) while browser-callback uses `< 0.5f` (BrowserCallbackCaptureCommand.cs:687-703).
17. `MainFormPromptLock` keys on the hardcoded control name `"btnStopAll"` (ScriptPromptDialogRunner.cs:384) and silently no-ops if not found — a rename quietly removes the "main form locked during prompt" protection.
18. `BrowserCallbackFocusRestorer` is an aggressive ~5.7s foreground-steal retry loop (intervals at line 69) using `AttachThreadInput`; acknowledged best-effort (43-46) but can fight the user.
19. `auto_close_browser` relies on `window.close()` (BuildCompletionHtml 605-607), which modern external browsers block for non-script-opened tabs — frequently a no-op outside WebView2.

localcmd specifics
20. "Run Same Command" approval is keyed to the exact **resolved** command text + a single `LocalCmdApprovedHost` (LocalCmdCommand.cs:112-124) — loops with variable-substituted commands re-prompt every iteration, and multi-host runs thrash the single-host approval slot.
21. cmd-shell interactive audit rewrites the command into `( cmd ) 2>&1 | powershell Tee-Object` (694-697) — changes the actual execution semantics (pipeline, exit code, interactivity) of audited interactive cmd sessions.
22. Default `lifetime` is `detached` (ScriptStep.cs:1620, NormalizeLifetime 1046-1054): background processes are orphaned unless the user opts into tracking — a surprising default for a "script ran something" mental model.
23. `localcmd` default port of trust: confirmation default is `always` (good), but `quiet`/`suppress` can hide what ran from output once approved (50-52, 179-182).

HTTP/network
24. New `HttpClient` per step (HttpCommand.cs:92-93) — socket exhaustion risk in tight loops; no proxy, retry/backoff, or file upload/download support; body is string-only UTF-8.
25. `dns` supports only A/AAAA/PTR via the OS resolver (DnsCommand.cs:118-123) — no TXT/MX/CNAME/SRV, no custom server, limiting for a network-operations tool.
26. `browser_callback_capture` default fixed port 8086 (ScriptStep.cs:1085) with no auto-port (0) option — parallel runs or two app instances collide; failure surfaces only as "failed to start local listener" (71-74).

Loops / data model
27. `foreach` coerces all items to `List<string>` (ForeachCommand.cs:89-96, 193-205) — iterating a JSON array of objects stringifies each element; structured iteration requires re-parsing inside the loop.
28. Executor retry mutates the shared `step.OnError` during attempts (ScriptExecutor.cs:456-471) — temporary mutation of parsed-step state; safe only as long as a single `Script` instance is never executed concurrently across hosts.

Misc
29. `exit` with a bare message defaults to **Success** (ExitCommand.cs:21-22) — `exit: could not connect` reports success unless prefixed with `failure`/`error`; keywords are English-only.
30. `exists` runs env-var expansion on the `type` field (ExistsCommand.cs:24) — harmless but clearly unintended copy-paste.
31. `confirm` stores `"true"`/`"false"` strings while `exists` stores a real bool (ConfirmCommand.cs:41 vs ExistsCommand.cs:80) — typing inconsistency in the variable model.
32. `interactive` only mirrors transcripts when **no** `command` is set (InteractiveCommand.cs:31-33) — `mirror_output: true` with a `command` silently does nothing for mirroring.
33. `JsonFunctions.cs` location (Commands folder, not a command) is a recurring discoverability trap for new contributors (Commands/JsonFunctions.cs:1-19).
