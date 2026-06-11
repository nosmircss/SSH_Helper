# Subsystem map: Script commands M–Z + config parsers

Scope: `Services/Scripting/Commands/*.cs` filenames M–Z (25 command files + `ScriptPromptDialogRunner.cs`) and `Services/Scripting/Parsers/` (3 files). All paths relative to repo root `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper`. Line refs verified 2026-06-10.

All commands implement `IScriptCommand.ExecuteAsync(ScriptStep, ScriptContext, CancellationToken) -> CommandResult` (`Services/Scripting/Commands/IScriptCommand.cs:11-21`). Dispatch is the enum-keyed dictionary in `ScriptExecutor`'s ctor (`Services/Scripting/ScriptExecutor.cs:130-172`); every command here is registered. The shared error contract is `CommandResult.ApplyOnError(step, msg)` (`IScriptCommand.cs:136-141`): `on_error: continue` → `Suppressed` (Success=true, SuppressedError=true, sets nothing else), else `Fail`. `CommandResult` also carries the canvas contract: `IterationCount` (loops) and `BranchTaken` (`IScriptCommand.cs:73-80`).

---

## Feature inventory

### multiselect — multi-checkbox user prompt
`Services/Scripting/Commands/MultiselectCommand.cs`
- Options (`Models/ScriptStep.cs:748-791`): `title`, `prompt`, `into` (required), `options` (inline `ChoiceOption` list), `options_from` (variable/expression source resolved by shared `ChoiceOptionResolver.Resolve`, `ChoiceOptionResolver.cs:18-34` — also accepts JSON arrays, `List<string>`, `${var}` tokens), `min`/`max` selection bounds, `font_size` per-prompt override.
- Behavior: shows `ScriptMultiselectDialog` (same file, :85-317) via `ScriptPromptDialogRunner.ShowAsync` (:49-54). On OK stores `List<string>` of selected values into `into` plus `{into}_count` (:62-63). Min/max enforced in-dialog with inline error labels (:248-268). Cancel → `Fail("Selection cancelled by user")` (:56-60) — note: plain Fail, not exit-cancelled.
- Dialog scales geometry from font size (`S()` helper :113-115), uses GDI fallback for `Font.Height` ArgumentException (:120-122, the known GDI+ shared-handle gotcha), custom `ThemedCheckedListBox` owner-draw for dark selection (:274-316).

### notify — multi-channel notification dispatch
`Services/Scripting/Commands/NotifyCommand.cs`
- Options (`Models/ScriptStep.cs:1318-1368`): `profile`, `channel` override (slack/teams/discord/toast/smtp+email+mail aliases :138-155), `title`, `message` (required :22-23), `level` (info/warn|warning/error|err/success|ok :112-132), `mention` list, `attachments` (SMTP only), `into` capture, per-options `on_error`.
- Behavior: requires `context.NotificationService` (:18-19). Teams channels get mention-format warnings via `TeamsAdaptiveCardPayloadBuilder.CollectWarnings` pre-send (:57-61). Captures `{into}.sent/.channel/.status_code/.error` as dotted variables (:98-110). Toast level attribution only included when `level` was explicitly set (:30, 67).
- Failure path honors both `step.IsOnErrorContinue` and `options.OnError == "continue"`, setting `_last_error` (:89-93).

### parallel — concurrent child-step execution (container)
`Services/Scripting/Commands/ParallelCommand.cs`
- Options (`Models/ScriptStep.cs:1530-1541`): `steps` (required, non-empty :24-25), `max_concurrent` (0 = unlimited → steps.Count :27-29).
- Behavior: `SemaphoreSlim`-throttled `Task.Run` per child, each child re-enters `_executor.ExecuteStepsAsync` with a single **shared** `ScriptContext` (:40-61; comment at :45 says context/session enforce internal synchronization). Results collected with index, checked **in original order** after `WhenAll` (:81-92): first control-flow result propagates; first non-suppressed failure → Fail. No fail-fast: all siblings run to completion even if one fails early.

### parse — device config text → structured dictionary
`Services/Scripting/Commands/ParseCommand.cs` + `Services/Scripting/Parsers/`
- Options (`Models/ScriptStep.cs:1373-1395`): `format` (required), `from` (required, **bare variable name** read via `GetVariableString` :33 — not `${}` substitution like most commands), `into` (required), `sections` filter list.
- Empty source var → warning + stores empty dictionary + `Ok` (:34-40, silent success).
- `ParserFactory` (`Parsers/ParserFactory.cs:11-16`): registered formats are only `fortigate` and `fortios` (both → `FortiGateParser`). Unknown format → ArgumentException with available list (:32-35).
- `FortiGateParser` (`Parsers/FortiGateParser.cs`): regex-driven line parser for `config`/`edit`/`set`/`unset`/`next`/`end` (:17-22). Builds nested `Dictionary<string,object>` (OrdinalIgnoreCase); `edit "name"` entries become dict keys (:103-132); `set` values parse quoted strings, multi-quoted → `List<string>` (:187-233); `unset` intentionally omitted (:142-145); comments (`#`) skipped (:44).
- **Section filtering is broken**: `SkipConfigBlock` is an empty placeholder (:248-252, comment admits it), so when a top-level `config` block is filtered out (:56-65) only the `config` line itself is skipped — the block's `set`/`edit` lines then execute against the **root** result dict, polluting output with the very sections the user excluded.

### ping — ICMP reachability probe
`Services/Scripting/Commands/PingCommand.cs`
- Options (`Models/ScriptStep.cs:1146-1167`): `host` (required), `count` (default 4), `timeout` ms per probe (default 3000), `into`.
- Sequential `Ping.SendPingAsync` probes (no inter-probe delay, no per-probe cancellation token — token only checked between probes :53-59, :130-138). Test seam: internal `IPingProbe` ctor (:24-27).
- Capture: success → `into`="success", `{into}_avg` (rounded mean ms), `{into}_loss` (% rounded away-from-zero) (:100-108); any failure/zero-replies → "failure" + `_loss`=100 (:110-118). Zero successes → `ApplyOnError` (:86-90).

### playsound — local WAV/MP3 playback (NAudio)
`Services/Scripting/Commands/PlaySoundCommand.cs`
- Options (`Models/ScriptStep.cs:609-636`): `path` (env-var + variable expanded :37), `wait` (default true), `volume` 0–100 clamped (:39), `max_seconds` (fractional allowed, must be >0 :56-57), `into`.
- Only `.wav`/`.mp3` extensions allowed (:49-54). `wait:false` is fire-and-forget on a background task with all exceptions swallowed (:138-153). `max_seconds` timeout stops playback and throws `TimeoutException` (:180-187).
- Capture: `into` = bool success, `{into}_meta` = dictionary {path, wait, volume, backend:"naudio", duration_ms?, error?} (:83-112).
- **Unique default**: when `on_error` omitted, playsound failures are **suppressed** (continue) rather than stopping the script (:127-134) — opposite of every other command's default.

### portcheck — TCP port open/closed/timeout probe
`Services/Scripting/Commands/PortcheckCommand.cs`
- Options (`Models/ScriptStep.cs:1198-1219`): `host` (required), `port` (default 22), `timeout` seconds (default 5), `into`.
- `TcpClient.ConnectAsync` with linked-CTS timeout (:33-40). Capture: `into` = "open"/"closed"/"timeout", `{into}_latency` ms ("" on timeout) (:73-80). Timeout distinguished from script cancellation via `when (!cancellationToken.IsCancellationRequested)` (:48). TCP-only; no UDP, no banner grab.

### print — emit message to script output
`Services/Scripting/Commands/PrintCommand.cs` (25 lines). Variable substitution + `EmitOutput(Info)` (:18-20). Empty print is a no-op Ok (:14-15).

### readfile — read local file lines into a list variable (with optional runtime picker)
`Services/Scripting/Commands/ReadFileCommand.cs` (802 lines: command + `ReadFileOpenPathRequest` + `ReadFileSelectionOptions` helper + `ScriptReadFileOpenPathDialog`)
- Options (`Models/ScriptStep.cs:482-544`): `path`, `select_file` (runtime picker), `message` (picker prompt), `fileext` (comma/semicolon/pipe-separated allow-list, normalized :483-515), `auto_browse` (null → defaults true when `select_file && path_only` :260-266), `path_into`, `path_only`, `into`, `skip_empty_lines` (default true), `trim_lines` (default true), `max_lines` (default 10000, 0 = unlimited :129), `encoding` (utf-8 default; ascii/utf-16/utf-16be/utf-32/latin1 :438-449).
- Path resolution: env-var + variable expansion then `Path.GetFullPath` (:226-234). Picker mode gated on `context.AllowFileSelectionDialogs` — throws sentinel-message InvalidOperationException otherwise ("manual main-window runs only", :20, :237-238), caught at :171-174.
- Validation chain: extension allow-list (:79-88), then `ScriptFileAccessValidator.ValidateReadPath` (:91-99) — blocklist of C:\Windows/Program Files/etc. + other-users' profile dirs (`Services/Scripting/ScriptFileAccessValidator.cs:12-20, 35-72`).
- Derived outputs: `path_into` (or `{into}_path` auto-derived when contents read :338-350); missing file → empty list + warning, Fail unless continue (:104-117).
- **User cancel of the picker exits the whole script**: `CommandResult.Exit(ScriptExitStatus.Cancelled)` (:360-370) — by design "cancel means abort run", but inconsistent with multiselect (plain Fail) and writefile (Fail/Suppressed).
- `on_error: continue` on validation errors returns `Ok(message)` — not `Suppressed` (:62-65, :84-85, :95-96), so these errors don't even register as suppressed errors.

### repeat — do-while (bottom-tested) loop container
`Services/Scripting/Commands/RepeatCommand.cs`
- Syntax: `repeat:` with `until` + `do` (scalar `repeat: <until>` + sibling `do` also parsed) (:10-11). `until` required (:24-25), `do` required (:27-28).
- Body always runs once; `until` evaluated by `ExpressionEvaluator` at the bottom (:72-77). `max_iterations` honored, default hardcoded 10000 (:15, :31-33). Sets `_iteration` var + iteration frame (`PushIterationFrame`/`SetCurrentIterationFrame` :38, 45-46 — the AsyncLocal frame stack for the canvas loop stepper). Break/continue/exit/return handled (:51-68); reaching the cap emits only a Warning and returns Success (:85-90).

### return — exit current subroutine
`Services/Scripting/Commands/ReturnCommand.cs` (17 lines). Returns `CommandResult.Return()` only when the parser marked `step.ReturnFromSubroutine` (`Models/ScriptStep.cs:270`); otherwise silently Ok (:14).

### send — execute SSH command on the pooled shell session (core command)
`Services/Scripting/Commands/SendCommand.cs`
- Step fields (`Models/ScriptStep.cs`): `send` text, `expect` (:288), `timeout` (:293), `capture` (:277), `suppress` (:283), `fail_on_nonzero` (:319), `respond` expect/reply pairs (:325, `RespondPair` :1514-1525).
- Session via `context.Session` (Rebex `SshShellSession`) wrapped in `SshSendCommandSessionAdapter` (:126-132, 210-238); internal resolver seam for tests (:31-34).
- Flow: substitute vars → optionally wrap in exit-status sentinel → echo prompt+command unless `suppress` (:62-66) → `ExecuteWithRespondsAsync` if respond pairs else `ExecuteAsync` (:71-84) → strip command echo + trailing prompt via `TerminalOutputProcessor` (:86-87) → `context.RecordCommandOutput(output, step.Capture)` (:102) → emit output unless suppressed.
- `fail_on_nonzero`: wraps the command as `eval '<cmd>'; ...printf '\n<SENTINEL>:%s\n' $?` (:134-138, sentinel const :18) and regex-extracts status (:140-167). Mutually exclusive with `expect` (validated :41-47) but **not** with `respond`. POSIX-shell-only mechanism — meaningless/harmful on network appliances (FortiGate/Cisco) where `eval`/`printf` don't exist.

### set — variable assignment / expressions / list ops / nested JSON
`Services/Scripting/Commands/SetCommand.cs`
- Syntax `set: name = expression` (split on first `=` :24-26). Nested path `a.b.c = v` builds/merges a `JsonObject` root (string roots starting `{` are parsed; non-objects silently replaced :44-95).
- Expression pipeline (:97-198): list mutators `push(arr, v)` / `unshift` / `pop(arr)` / `shift(arr)` (writable only for simple identifiers or `${ident}` :325-363; pushed values stringified via `?.ToString()` :109, :129) → `JsonUtilities.TryEvaluateFunctionExpression` (the ~70 built-ins) → JSON expression eval → `ExpressionParser` when `HasExpressionOperator` heuristic fires (:214-264: `+`/`-` only when preceded by whitespace to protect hyphenated hostnames :245-247; compact `1+2` special case with leading-zero/date guard :266-318) → fallback `ValueResolver.ResolveExpressionValue`.
- Debug echo of every assignment (:39, :93). `FormatExpression` parse failure falls back to plain substitution (:190-194).

### sethistorylabel — label the current host's history entry
`Services/Scripting/Commands/SetHistoryLabelCommand.cs`
- Accepts scalar string, typed `SetHistoryLabelOptions` (`Models/ScriptStep.cs:959-984`: `value`, `mode` replace/append/prepend/clear, `separator`, `replace` bool = hide IP) or raw YAML dict (:36-62).
- Builds a `HistoryLabelOperation` (`Models/HistoryLabelOperation.cs`: mode normalization :38-50, apply semantics :63-102 — empty value ⇒ clear; `replace` flag default false on replace mode, preserved on append/prepend) and both records it on the context op-list and applies it to live `context.HistoryLabel`/`HistoryLabelReplacesAddress` (:17-24). Never fails (always Ok).

### sftp — file upload/download over SFTP (SSH.NET, separate connection)
`Services/Scripting/Commands/SftpCommand.cs`
- Options (`Models/ScriptStep.cs:1224-1275`): `action` upload|download (required), `local_path` (env+var expanded), `remote_path`, `host`/`port`/`username`/`password` overrides falling back to context vars `Host_IP`/`username`/`password` (:111-137), `overwrite` (default true), `timeout` s (default 120), `into`.
- `host` may embed `:port` (`ParseHostWithOptionalPort` :139-156 — `LastIndexOf(':')`, so bare IPv6 literals are mis-split). Pre-seeds failure capture (`into`="failure", `{into}_bytes`=0 :22, :167-174); success sets "success" + byte count (:158-165).
- Uses **SSH.NET `SftpClient` with password auth only** (:66-72): no key auth, no Vault-sourced credentials, no host-key verification, distinct from the Rebex pool used by `send`. Connect/transfer are synchronous on the calling thread inside a `Task`-returning method; the cancellation token is only checked before connecting (:64).

### switch — value dispatch container
`Services/Scripting/Commands/SwitchCommand.cs`
- `switch: <expr>` (substituted+trimmed :25), `cases[].value` + `cases[].do` (`Models/ScriptStep.cs:1493-1509`), `default` stored in `step.Else` (:74).
- Matching: case value starting `matches ` ⇒ regex IsMatch IgnoreCase, invalid regex silently = no-match (:38-51); otherwise **case-insensitive** equality (:55). First match wins; empty `do` returns Ok with branch recorded (:62-68).
- Sets `BranchTaken` = `cases/{i}/do` or `default` (:61, :77-80) — the exact Flow Canvas `edge.data.branchPath` vocabulary; no match + no default → Ok with null branch (:83-84).

### table — format data as aligned text columns
`Services/Scripting/Commands/TableCommand.cs`
- Options (`Models/ScriptStep.cs:1546-1598`): `data` (variable ref, `${}` wrapper stripped :26-27), `columns` (header/field/align/width), `into`, `align` default, `show_header` (default true).
- Accepts `List<string>`, `JsonElement`/`JsonNode` (objects → keyed rows, arrays → row-per-item), `IDictionary`, KeyValuePair-shaped enumerables (reflection :378-405), JSON-parseable strings, otherwise newline-split text (:129-192).
- Auto-columns = union of row keys; single "Value" column renamed to the source variable name (:69-75). Auto width = max(header, longest cell), no cap (:77-90). Fixed width **truncates with no ellipsis** (:410-414). Missing cell → "-" (:111). Output emitted as Info and optionally captured to `into` (:117-124). Missing variable → Warning + Ok (:30-34).

### try — try/catch/finally container
`Services/Scripting/Commands/TryCommand.cs`
- `try` block required non-empty (:21-22). Catch runs only on plain failure (not on exit/break/continue/return :26-27), with `preserveLastErrorOnSuccess: true` so `_last_error` survives a successful catch (:29-34). `finally` always runs; a failing or control-flow finally **overrides** the try/catch result (:37-42).

### updatecolumn — write a value back to the host grid CSV column
`Services/Scripting/Commands/UpdateColumnCommand.cs`
- Options `column` + `value` (null = fail; empty string ok) (`Models/ScriptStep.cs:909-921`, cmd :19-23). Substitutes vars then `context.RequestColumnUpdate` (:26-29) — a queued request consumed by Form1/CsvManager; the command always returns Ok, so persistence failures are invisible to the script.

### updateenvironment — persist an environment variable
`Services/Scripting/Commands/UpdateEnvironmentCommand.cs`
- Options `variable` + `value` (`Models/ScriptStep.cs:926-938`). Sets the live context variable **and** queues `RequestEnvironmentUpdate` (:28-30) so the script sees the new value immediately while the EnvironmentService persists it. Always Ok.

### vault — read/write/patch HashiCorp Vault secrets
`Services/Scripting/Commands/VaultCommand.cs`
- Options (`Models/VaultStepOptions.cs`): `path` (required), `profile` (else default via `VaultService.ResolveDefaultProfileName(context.EnvironmentVaultProfile)` :124-130), `key`+`into` (single read), `keys` map secret-key→variable (multi read), `version` (KV v2), `write` map, `patch` map, per-options `on_error`.
- Operation precedence: write → patch → keys → key (:32-44); none ⇒ error listing the four shapes (:44).
- Write/patch values are var-substituted then auto-promoted to JSON nodes when they look like objects/arrays, including a recovery path that un-escapes previously stringified payloads (:132-198).
- `VaultException` honors step-level or options-level continue, sets `_last_error` (:46-57). Multi-read: keys dict is OrdinalIgnoreCase (:81) so two keys differing only by case collapse; missing keys silently skipped (:90-96).

### wait — sleep N seconds
`Services/Scripting/Commands/WaitCommand.cs` (26 lines). `wait: <int seconds>`; ≤0/absent is a no-op Ok (:14-15). `Task.Delay(seconds * 1000)` (:21) — integer seconds only (no sub-second), int multiply could overflow for absurd values.

### webhook — generic HTTP request
`Services/Scripting/Commands/WebhookCommand.cs`
- Options (`Models/ScriptStep.cs:1280-1312`): `url` (required, http/https only :49-53), `method` (default POST), `body` (sent for POST/PUT/PATCH only :78-79), `headers` map (TryAddWithoutValidation :73), `into` (+`{into}_status`), `timeout` s (default 30 via fallback :62).
- Content-Type pulled from headers map else `application/json` (:83-95). Static shared `HttpClient` (:16) with linked-CTS timeout; internal handler-factory seam for tests (:23-26, 149-154). Non-2xx → `ApplyOnError` after capturing the response (:109-123).
- Explicit design note at :55-58: **no SSRF/private-range filtering by design** (localhost/RFC1918 allowed for infra automation).
- Capture cleared up-front to empty **strings** (:36-37, 156-163) but set to **int** status on success (:112) — `{into}_status` type varies by outcome. Whole response buffered in memory, no size cap, no response-header capture, no retry/auth helpers.

### while — top-tested loop container
`Services/Scripting/Commands/WhileCommand.cs`
- `while: <condition>` (ExpressionEvaluator) + `do` block required (:23-27). `max_iterations` default hardcoded 10000 (:14, 30-32). Sets `_iteration` + iteration frame per pass (:59-60). Control flow: exit/return propagate with `IterationCount` (:67-77), break stops (:79-83), continue advances (:85-89), child failure propagates (:91-95). Cap reached ⇒ Warning + Success (:105-110), same silent-truncation pattern as repeat.

### writefile — write text/JSON/JSONL/CSV to a local file
`Services/Scripting/Commands/WriteFileCommand.cs` (854 lines: command + `ScriptWriteFileSavePathDialog`)
- Options (`Models/ScriptStep.cs:549-583`): `path` (required), `content` (raw text or `${var}` reference for structured formats), `mode` overwrite (default) | append, `format` text (default) | json | jsonl | csv, `pretty` (json, default true), `headers` (csv).
- Path: env+var expansion; **non-fully-qualified paths trigger a modal "Choose Save Location" dialog** (:170-193, dialog :652-853 defaulting to Documents\output.txt) — not gated on `context.AllowFileSelectionDialogs` (unlike readfile), so an unattended/scheduled run with a relative path pops UI. Cancel → Fail or Suppressed (:42-51).
- Security: `ScriptFileAccessValidator.ValidateWritePath` (:54-62) — read blocklist + blocked executable/script extensions + allow-list of user-profile/Documents/Desktop/LocalAppData/Temp (`ScriptFileAccessValidator.cs:80-133`). Directory auto-created (:64-69).
- JSON append = read-modify-write merge (arrays concatenated, objects deep-merged via `JsonUtilities.MergeInto`; non-object into object ⇒ fall back to overwrite serialization) (:195-289). JSONL appends one normalized line, repairing a missing trailing newline first (:361-387). CSV: headers row, `${var}` of `List<string>`/JSON-array-of-objects extracted per header (:428-506); plain rows containing `,` or tab are re-split and re-escaped (:527-532); proper quote-escaping helper (:637-649).
- Sets `_writefile` variable to the resolved path on success (:23, :136). All writes are plain `File.WriteAllText`/`AppendAllText` (:101, 113, 118, 127, 131) — no atomic temp+replace, no encoding option (readfile has one).

### ScriptPromptDialogRunner — shared UI-thread prompt infrastructure
`Services/Scripting/Commands/ScriptPromptDialogRunner.cs`
- `ShowAsync<TDialog,TResult>` shows prompt dialogs **modeless** (owner = main form) so the main form isn't modal-blocked, completing a TCS on FormClosed (:30-128). `RunOnUiThreadAsync` variant for native dialogs (:130-204).
- `AnchorFormOverride` lets FlowCanvasForm own prompts during canvas runs (:19); `DefaultPromptFontSize` set by Form1.ApplyFontSettings, overridable per-step via `font_size` (:26).
- `MainFormPromptLock` disables the entire main-form control tree **except the ancestor chain of a control literally named "btnStopAll"** (:370-441, name lookup :384) so Stop stays clickable; if that button isn't found no lock is taken at all. Cancellation closes the dialog as Cancel (:217-254). DialogResult buttons are re-wired for modeless operation (:336-352). Dialog centered over the anchor with multi-monitor clamping (:256-294); main-form activation restored afterwards (:296-334).

---

## Integration points

- **Executor**: registration dictionary `Services/Scripting/ScriptExecutor.cs:130-172`; container set used for output attribution `:96-97` (While/Repeat/Try/Switch/Parallel re-enter via `ExecuteStepsAsync`). `IterationCount`/`BranchTaken` flow onto `StepExecutionEventArgs` → FlowCanvasForm debug bridge. Switch's `cases/{i}/do`/`default` strings are load-bearing for canvas path highlighting.
- **ScriptContext** (`Services/Scripting/ScriptContext.cs`): `Session` (Rebex shell, send), `VaultService` (:367) + `EnvironmentVaultProfile` (:372), `NotificationService` (:377), `RecordCommandOutput` (:855, feeds `{output}`/capture vars), `RequestColumnUpdate` (:941 → Form1/CsvManager grid write-back), `RequestEnvironmentUpdate` (:963 → EnvironmentService persistence), `AllowFileSelectionDialogs` (:267, false for scheduled/job runs — only readfile checks it), History label state + op log (:199-240, consumed by HistoryStorageService), iteration frames (:334-344, AsyncLocal — parallel-safe, drives the canvas loop iteration stepper).
- **UI**: `ScriptPromptDialogRunner` (above) is the single seam between commands and WinForms; dialogs in Multiselect/ReadFile/WriteFile each theme via `DialogTheme` but detect dark mode independently with `mainForm.BackColor.GetBrightness() < 0.2f` (MultiselectCommand.cs:216, ReadFileCommand.cs:695, WriteFileCommand.cs:729).
- **Other services**: `TerminalOutputProcessor` (send echo/prompt stripping, SendCommand.cs:86-87), `TeamsAdaptiveCardPayloadBuilder` (NotifyCommand.cs:59), SSH.NET `Renci.SshNet.SftpClient` (SftpCommand.cs:5,66 — separate stack from the Rebex pool), NAudio `AudioFileReader`/`WaveOutEvent` (PlaySoundCommand.cs:164-165), shared static `HttpClient` (WebhookCommand.cs:16).
- **Validation**: `ScriptFileAccessValidator` (readfile/writefile); `ScriptParser.ValidateSteps` recurses per-switch-case (see memory note) — container commands here must recurse themselves or nested steps silently skip.
- **Test seams**: `PingCommand.IPingProbe` (:123-128), `SendCommand.ISendCommandSession` (:193-208), `WebhookCommand(Func<HttpMessageHandler>)` (:23), `PlaySoundCommand(playAsync)` (:24), `ReadFileCommand.OpenFileDialogOverrideForTests` (:23), `ScriptPromptDialogRunner.RestoreMainFormActivationOverrideForTests` (:28).

---

## Observed gaps & quirks

### Correctness bugs
1. **FortiGateParser section filter leaks excluded content into the result** — `SkipConfigBlock` is an admitted no-op placeholder (`Parsers/FortiGateParser.cs:248-252`); after a filtered top-level `config` line is skipped (:56-65), the block's `set`/`edit`/`end` lines run against the root context, so `parse` with `sections:` returns root-level garbage keys from excluded sections instead of omitting them.
2. **Sftp host:port parsing breaks IPv6** — `ParseHostWithOptionalPort` uses `LastIndexOf(':')` (`SftpCommand.cs:144`), so `2001:db8::1` becomes host `2001:db8:` + port nonsense (or silently mangled host).
3. **`{into}_status` type drift in webhook** — cleared to `""` (string) up-front (`WebhookCommand.cs:161-162`) but set to `int` on response (:112); comparisons like `== "200"` vs `== 200` behave differently depending on whether the request was sent.
4. **Webhook timeout message uses raw option value** — fallback applies 30s at :62 but the timeout error message interpolates `options.Timeout` (:131), printing "timed out after 0 seconds" if the user set 0.
5. **FortiGate `ShouldIncludeSection` matches at character level** (`FortiGateParser.cs:238-242`): filter `"sys"` matches `"system interface"`; bidirectional `StartsWith` also means filter `"system interface extra"` includes `"system interface"`.

### Inconsistent contracts (user-visible behavior differs across sibling commands)
6. **Cancel semantics differ per prompt command**: readfile picker cancel ⇒ whole-script `Exit(Cancelled)` (`ReadFileCommand.cs:360-370`); multiselect cancel ⇒ plain `Fail` (`MultiselectCommand.cs:56-60`); writefile dialog cancel ⇒ `Fail`/`Suppressed` (`WriteFileCommand.cs:42-51`). A user can't predict whether Cancel aborts the run.
7. **`on_error: continue` maps to three different results**: most commands → `Suppressed` via `ApplyOnError`; readfile/writefile path-validation errors → `Ok(message)` (`ReadFileCommand.cs:62-65,84-85,95-96`; `WriteFileCommand.cs:58-59`) which doesn't even mark `SuppressedError`; playsound defaults to continue when `on_error` omitted (`PlaySoundCommand.cs:127-134`) — unique and only documented in a code comment.
8. **Duplicated on_error surface**: notify and vault honor a second per-options `on_error` (`NotifyCommand.cs:89`, `VaultCommand.cs:50`) in addition to the step-level one; no other M–Z command does.
9. **`parse.from` takes a bare variable name** (`ParseCommand.cs:33`) while nearly everything else accepts `${var}`/substitution — easy authoring trap; an accidental `${cfg}` value silently reads an empty/missing variable (empty source still returns Ok, :34-40).
10. **Unattended-run gating is readfile-only**: `AllowFileSelectionDialogs` blocks readfile pickers in scheduled runs (`ReadFileCommand.cs:237-238`) but writefile's relative-path save dialog has no such gate (`WriteFileCommand.cs:170-193`) — a scheduled job with `path: output.txt` will pop a modal dialog on the desktop (or stall headless).

### Robustness / security
11. **No host-key verification and password-only auth in sftp** (`SftpCommand.cs:66-72`); no key-file or Vault credential integration even though the app has both (`CredentialMode.Vault`, vault command). Synchronous `Connect`/transfer ignores the cancellation token mid-operation (:64 is the only check).
12. **Writefile is not crash-safe**: plain `File.WriteAllText/AppendAllText` (`WriteFileCommand.cs:101,113,118,127,131`) — the project's own convention (`JsonFileWriter.WriteJsonAtomic`, CLAUDE.md) exists precisely to avoid torn files; JSON append is a non-atomic read-modify-write of the whole file (:217-233).
13. **`ScriptFileAccessValidator` blocklist is C:-drive-literal and prefix-based** (`ScriptFileAccessValidator.cs:12-20`): a Windows install on another drive is unprotected; `StartsWith` without a trailing separator also mis-blocks siblings like `C:\ProgramData2`. Write allow-list (:101-118) silently forbids any non-user-profile drive (D:\ data disks, UNC shares) — a surprising limitation for an ops tool with no override setting.
14. **Webhook SSRF is explicitly by design** (`WebhookCommand.cs:55-58`) — acceptable for an infra tool but there is no allow/deny-list setting, no max response size, and the static `HttpClient` ignores per-request proxy/TLS needs.
15. **Ping probes can't be cancelled mid-flight** (`PingCommand.cs:135` — `SendPingAsync(host, timeoutMs)` without token) and fire back-to-back with no interval option (no `delay` between probes).

### Silent-success / observability gaps
16. **Loop caps end successfully**: while/repeat hitting `max_iterations` (hardcoded default 10000, `WhileCommand.cs:14`, `RepeatCommand.cs:15`) emit a Warning and return Success (:105-110 / :85-90) — a stuck `until` condition truncates work without failing the run.
17. **updatecolumn/updateenvironment always succeed** (`UpdateColumnCommand.cs:33`, `UpdateEnvironmentCommand.cs:33`): they only enqueue requests; persistence errors surface (if at all) in the UI layer with no feedback variable.
18. **Switch swallows invalid regex** (`SwitchCommand.cs:47-50`) — a typo'd `matches (` pattern silently never matches; no validation-time check, no warning emitted.
19. **Vault multi-read silently skips missing keys** (`VaultCommand.cs:90-96`) — target variables stay undefined with no warning; keys dictionary is case-insensitive (:81) so `Token` and `token` collapse.
20. **`return` outside a subroutine is a silent no-op** (`ReturnCommand.cs:14`, gated on parser-set `ReturnFromSubroutine`).

### Feature gaps a multi-host SSH tool user would expect
21. **Only one parse format** — ParserFactory registers fortigate/fortios only (`ParserFactory.cs:11-16`); no Cisco IOS/NX-OS, JunOS, or generic key-value/textfsm-style parser despite `IConfigParser` being designed for it and ParseCommand's doc claiming "and other network device configuration formats" (`ParseCommand.cs:10-11`).
22. **`send.fail_on_nonzero` is POSIX-only** (`SendCommand.cs:134-138`: `eval`+`printf` sentinel) — no shell-dialect detection or alternate strategy, so enabling it against the appliances this tool targets (FortiGate) emits garbage commands; also unvalidated in combination with `respond` (only `expect` is rejected :41-47).
23. **Parallel has no fail-fast/cancel-siblings option** (`ParallelCommand.cs:63-92`) and shares one context — no per-branch variable isolation or result aggregation variable (e.g. per-step statuses).
24. **Set list ops stringify** (`SetCommand.cs:109,129`): `push(list, ${jsonObj})` stores `obj.ToString()`; structured list mutation isn't possible.
25. **Table truncation without ellipsis** (`TableCommand.cs:410-414`) and unbounded auto-width (:86-89) make long-output tables unreadable; no max-width or wrap option, no sort/limit options.
26. **wait is whole-seconds only** (`WaitCommand.cs:21`) — no `ms`/fractional support (playsound's `max_seconds` accepts fractions, so the precedent exists).
27. **No encoding option on writefile** (readfile has 6 encodings, `ReadFileCommand.cs:438-449`; writefile always default UTF-8).

### Maintainability smells
28. Dark-mode detection heuristic duplicated in 3 dialogs (brightness < 0.2 check) instead of a `DialogTheme` query (MultiselectCommand.cs:216, ReadFileCommand.cs:695, WriteFileCommand.cs:729).
29. `MainFormPromptLock` couples to the literal control name `"btnStopAll"` (`ScriptPromptDialogRunner.cs:384`); a Form1 rename silently disables the whole keep-Stop-clickable lock (TryAcquire returns null → main form stays fully interactive during prompts).
30. `FortiGateParser.ParserContext.IsTable` is set (:107) but never read; `SkipConfigBlock`'s `ref` parameter signature is dead weight.
31. `SetCommand.ResolveValue` (:320-323) is a one-line pass-through wrapper; trailing comment at :391 documents an already-removed ArithmeticParser.
