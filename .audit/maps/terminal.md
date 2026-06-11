# Subsystem map — Interactive Terminal

Scope: `Services/Terminal/InteractiveTerminalService.cs` (3793 LOC), `Forms/InteractiveTerminalForm.cs` (817), `UI/InteractiveTerminalViewportControl.cs` (810), `Utilities/TerminalOutputProcessor.cs` (551). Closely related files discovered and included: `Services/Scripting/Commands/InteractiveCommand.cs`, `Services/Scripting/Models/ScriptStep.cs:828-904` (`InteractiveOptions` / `InteractiveSessionMode`), `Services/SshTerminalOptionsFactory.cs`, `Utilities/PromptDetector.cs`, `Services/SshShellSession.cs` (shared-session seams), `Models/ExecutionDetails.cs:42-53`, `ExecutionDetailsDialog.cs:467-524`, `Services/Scripting/ScriptParser.cs:2522-2660 + 5034-5100`.

The terminal is reached **only** via the YAML `interactive:` script step (dispatched through `InteractiveCommand`). There is no standalone "open a terminal on this host" UI affordance anywhere in the app.

---

## Feature inventory

### 1. Three run modes (mode dispatch: `InteractiveTerminalService.RunAsync`, Services/Terminal/InteractiveTerminalService.cs:231-265)
| Mode | Trigger | Entry point |
|---|---|---|
| **Interactive window, shared session** | `interactive: {session: shared}` | `RunSharedAsync` (267-333) — attaches to the pooled Rebex shell of the current script session (`SshShellSession.SharedScripting`/`SharedTerminal`, Services/SshShellSession.cs:110/115) |
| **Interactive window, separate session** | `interactive: {session: separate}` (default) | `RunSeparateAsync` (335-440) — dials a brand-new Rebex `Ssh` client + `VirtualTerminal` |
| **Capture mode** (auto-runs a long command) | `interactive: {command: "...", session: separate}` | `RunSeparateCaptureAsync` (442-587) → windowed loop `RunCaptureWindowLoopAsync` (589-1201) or headless loop `RunCaptureHeadlessLoopAsync` (1203-1622) when `show_window: false` |

Validation matrix (RunAsync 240-257, mirrored in `ScriptParser.ValidateSteps` 5034-5100): `command` requires `session: separate`; `show_window: false` requires a `command` **and** one of `max_seconds`/`max_lines`. `columns`/`rows` are deprecated in favor of pixel `width`/`height` (parser deprecation warnings at ScriptParser.cs:2621, 2633).

### 2. Options honored (`InteractiveOptions`, Services/Scripting/Models/ScriptStep.cs:828-895)
`session`, `title` (variable-substituted, InteractiveCommand.cs:78-80), `command` (substituted), `capture` (variable name receiving the transcript, InteractiveCommand.cs:43-46), `max_seconds` / `max_lines` (capture safety caps that auto-send Ctrl+C, service 982-1002 / 793-798), `width`/`height` (window pixels; default 980x620), `columns`/`rows` (deprecated grid sizing, Form `ApplyInitialGridSize` 786-805), `mirror_output` (stream transcript chunks into the script output as `RawChunk`, service 767-791), `show_window`. Timeouts ride on `ScriptContext.Timeouts` (`ConnectionTimeout`, `InitialPromptTimeout`, `KeepAliveInterval`).

### 3. Session lifecycle
- **Connect (separate):** `new Ssh {Timeout=ConnectionTimeout}` (370-373); `ApplyAlgorithmSettings` sets host-key algorithms and ciphers from per-host overrides only (3742-3753); `ConnectAndLogin` (3728-3740) uses identity file + passphrase when present, otherwise password. No host-key fingerprint verification of any kind.
- **Startup negotiation:** `InitializeSeparateSessionStartupAsync` (2753-2844) polls for any data, auto-answers up to 5 "press any key" banner prompts (`SshShellSession.Patterns.BannerAcceptPrompt`, accept key extracted by `ResolveBannerAcceptKey` 2901-2912), detects the prompt via `PromptDetector.TryDetectPromptFromTail` and builds a prompt regex + literal for capture completion and transcript prefixing. `FlushCaptureStartupBuffer` (3640-3671) drains leftovers at 150 ms timeout.
- **Close-reason vocabulary** (62-70): `user_closed`, `disconnected`, `cancelled`, `error`, `ctrl_c_continue`, `timeout_continue`, `max_lines_continue`, `early_close_partial`, `natural_complete`. `Completed` definition at 93-100. Capture treats `disconnected`/`error` as step failure (553-561).
- **Cancellation:** token registration force-closes the window; in shared mode it **disposes the entire shared session** by design (300-304, comment "Required behavior: stop/cancel force-closes active shared interactive session").
- **Post-close resync (shared):** `SshShellSession.SyncAfterInteractive` (SshShellSession.cs:344-365) sends `\r` and re-reads to the prompt, best-effort.
- **Audit trail:** every launched window appends an `InteractiveTerminalSessionDetails` (host, mode, close reason, completed, start/end UTC, cleaned transcript) to the `ScriptContext` in `finally` blocks (320-332, 426-439, 573-586; `CreateSessionDetails` 2914-2941). Flows: `ScriptContext.InteractiveSessions` → `ExecutionResult` → `ExecutionDetails` → history → "Interactive" tab of `ExecutionDetailsDialog` (467-524) with per-session transcript viewer.

### 4. Terminal emulation & rendering pipeline
- Rebex `VirtualTerminal`, UTF-8, defaults 120 cols x 36 rows, 10,000-line scrollback (`SshTerminalOptionsFactory.cs:13-15`).
- **Pump** `PumpFullAsync` (2108-2270): one `ioLock` serializes `scripting.Process(2ms)`, keep-alive (`TrySendKeepAliveIfDue` 3255-3279), and queued input flush (`FlushPendingInput` 3230-3245). Adaptive render cadence: 20 ms while output flows, 120 ms idle; loop delays 2 ms/8 ms (2128-2132). PuTTY-style scrollback anchoring keeps the user's scroll position stable while new output arrives (2206-2214). Snapshots dedupe by hash (2252-2255). `ScreenUpdateDispatcher` (117-227) coalesces to latest-snapshot-wins via `BeginInvoke` with backlog detection.
- **Snapshot** `BuildScreenSnapshot` (3333-3469): per-cell char + ARGB colors with palette cache, bold→bright (index+8) mapping (3386-3388), black-on-black "invisible text" normalization (3392-3398), follow-tail cursor anchoring after resize (3344-3351), inverted-color cursor with Lime fallback on blank cells (3414-3445), per-row hashes powering dirty-row invalidation.
- **Viewport** (`InteractiveTerminalViewportControl`): owner-drawn GDI `TextRenderer` with color-run batching (OnPaint 346-427), `SolidBrush` cache (429-438), row-delta invalidation (99-150), 530 ms cursor blink only when focused (52-74), monospace cell metrics from a 64-char 'W' sample (749-777).
- **Headless capture pump** `PumpCaptureHeadlessAsync` (2036-2106): same processing without any rendering.
- **Resize:** form computes cols/rows from client size and raises `TerminalSizeChanged` → `terminal.SetScreenSize` (form 339-364; service 873-889, 1843-1859).

### 5. Transcript capture & mirroring
- `DataReceived` handlers filter **alternate-screen** content out of the audit transcript: `AlternateScreenSequenceRegex` (58-60; modes `?1049/?1047/?47 h|l`), `FilterTranscriptChunkForAudit` (2272-2357) keeps the pre-enter prefix and resumes on leave; `ResolveTranscriptAssemblyInput` (2979-2994) prefers raw data when no alt-screen sequences are involved.
- Incomplete final lines are held back so backspace/CR edits resolve before emission (`PrepareMirroredChunkForEmission` 2601-2623), then normalized via `TerminalOutputProcessor.Sanitize`+`Normalize` (`NormalizeMirroredTranscript` 2467-2473).
- **Caps:** transcript 500,000 lines (const 71), mirror output 50,000 lines (const 74), each appending an explicit cap notice; CRLF-aware line counting across chunk boundaries (`CountLinesFromCapturedChunk` 2636-2672, `GetMirrorChunkPrefixLengthWithinCaps` 2545-2599).
- **Mirror output:** chunks emitted live as `ScriptOutputType.RawChunk` (790, 1380); a synthesized startup prompt prefix makes the transcript read naturally (`BuildMirroredStartupPromptPrefix` 2846-2863, `PrependStartupPromptIfMissing` 2878-2899).
- **Natural completion (capture):** arm on command echo or non-prompt output (`ShouldArmCaptureNaturalCompletion` 2674-2683), complete when the last line matches the detected prompt regex, falling back to `PromptDetector.IsLikelyPrompt` heuristics (2714-2734).
- **Audit cleanup:** `CleanTranscriptForAudit` (2962-2977) maps DEL(0x7F)→BS then runs cursor-aware normalization so the transcript reflects the final visible command line.
- Rich debug instrumentation when `context.DebugMode` (`ShouldEmitTranscriptChunkDebug` 2996, `BuildTranscriptChunkDebugMessage` 3028, control-char escaping 3070-3110) emitted as `ScriptOutputType.Debug`.

### 6. Keyboard handling (`Forms/InteractiveTerminalForm.cs`)
- Printable chars via `KeyPress` → `TextInput` (271-282). `KeyDown`: Ctrl+A..Z → `ConsoleKey` with Control (290-301); function keys mapped: Enter, Tab, Backspace, Escape, arrows, Home/End, PageUp/Down, Insert, Delete with Ctrl/Shift/Alt modifiers (303-336). Tab is hijacked from dialog navigation via `ProcessDialogKey` (260-269). Any input snaps the view back to tail and clears selection (`SnapToTailForInput` 427-437).
- **Capture-window mode special:** Ctrl+C after the command dispatched sends SIGINT *and* detaches the step (`ctrl_c_continue`) — script continues, window flips to read-only (service 848-857).
- **Shared-mode guards:** Ctrl+D closes the window *without* sending EOF so the pooled session survives (`ShouldCloseSharedWindowWithoutSendingEof` 2943-2950); typing `exit`/`logout` + Enter is intercepted — the typed chars are backspaced on the host and the window closes instead (`SharedCommandGuardState` 103-113, `ShouldBlockSharedShellCommandOnEnter` 3121-3129, `EnqueueSharedTextInput` 3168-3209 line-splits pasted text, `EnqueueSharedDetachRequest` 3211-3228).
- **Separate-mode Ctrl+D:** records a tick (1813-1817); a subsequent `logout` line closes the window as disconnected (1753-1767, `ContainsLogoutLine` 2368-2377).

### 7. Scrollback
- `VScrollBar` synced both ways (form 569-587, 387-405); mouse wheel scrolls `SystemInformation.MouseWheelScrollLines` per notch (366-377); offset preserved against incoming output (service 2206-2214). "Clear Scrollback" wipes history while preserving the visible screen via region copy/restore (`ClearScrollbackPreservingScreen` 3543-3576); "Reset Terminal" is `Screen.Clear(true)` only (3578-3581). History limit hardcoded 10k.

### 8. Copy / paste
- Left-drag selection in absolute **buffer** coordinates (survives scrolling), auto-copies on mouse-up — PuTTY style (viewport OnMouseUp 298-318); double-click selects word + copies (254-276); selection colors hardcoded `#2A5CAA`/white (viewport 23-24); selection dropped when terminal width changes (NormalizeSelectionBounds 512-533). Selected text is pulled from the real Rebex buffer incl. history via `SelectionTextProvider` → `BuildSelectionClipboardText` (service 3497-3541), with viewport-snapshot fallback (viewport 193-214, 696-736).
- **Right-click = paste** clipboard (form 379-385, 449-467); CRLF→LF normalization (481-486); no bracketed paste, no multi-line confirmation.
- **System menu** (window icon / Alt+Space) gets three appended commands: "Copy All to Clipboard" (entire buffer + history, `BuildClipboardText` 3471-3495), "Clear Scrollback", "Reset Terminal" (form 545-558 via `AppendMenu` P/Invoke, dispatched in WndProc 235-258).

### 9. Window management
- Default 980x620, min 760x420 (relaxed to 320x200 when YAML sizes the window) (form 101-107); remembers last screen location for the process lifetime in a static, clamped to a visible monitor (50-51, 739-784); dark title bar + `DialogTheme` applied when main form is dark (154-160); title from `options.Title` else `"{host} - Interactive ({mode})"` / `"- Interactive Capture"` (service 3763-3783); owner = `Application.OpenForms[0]` (935, 1904).
- **Detached read-only mode** (`EnableDetachedReadOnlyMode` form 194-214): after a capture step detaches (timeout/Ctrl+C/max-lines/natural complete) the window stays open showing a static history snapshot (`RenderDetachedHistorySnapshot` 589-676) with its own selection provider (698-737); input and Clear/Reset disabled; title suffixed "Detached (read-only)". Triggered from service 1044-1071.

### 10. Reconnect
**Does not exist.** Disconnect (Rebex `Disconnected`/`ActionRequested(DisconnectRequest)` events or `IsConnected` polling) closes the window (service 1687-1704) or fails the capture step (553-556). No reconnect button, no retry, no "connection lost" overlay — the window just vanishes.

### 11. TerminalOutputProcessor (`Utilities/TerminalOutputProcessor.cs`)
Static text-normalization toolkit shared by the interactive transcript pipeline and the main `SshShellSession` read loop:
- `Normalize` (61-121): cursor-aware single-line emulation — CR overwrite, BS, tab stops, ESC s/u save/restore, CSI K/X/C/D/G/H/f/@/P; SGR ignored. CSI H honors only the **column** (495-504; row is meaningless in the line model).
- `Sanitize` (127-133): strips non-printables except ESC/CR/LF/TAB/BS.
- Pager artifact strippers: `--More--` (143-148) and FortiGate dismissal echo (156-168).
- zsh PROMPT_SP `%`-artifact strippers incl. a streaming variant holding back an ambiguous suffix across chunks (177-217).
- `BufferIncompleteFinalLineStreaming` (227-254), `StripTrailingPrompt` with starship metadata-line awareness (262-310), `StripCommandEcho` (319-349).

---

## Integration points

| Connection | Detail |
|---|---|
| **Script engine** | `InteractiveCommand : IScriptCommand` (Services/Scripting/Commands/InteractiveCommand.cs) — registered in `ScriptExecutor`'s enum-keyed dictionary; honors `on_error: continue` (→ `CommandResult.Suppressed`); writes `capture:` variable via `context.RecordCommandOutput` (43-46); mirrors window-mode transcripts post-run (31-41) |
| **ScriptContext** | `Session` (shared shell), `CurrentHost`, `ResolvedUsername/Password`, `Timeouts`, `DebugMode`, `EmitOutput(RawChunk/Debug/Error)`, `AddInteractiveSession` (ScriptContext.cs:983), `SubstituteVariables` for title/command/capture |
| **SshShellSession** | `FlushBuffer()` before attach (service 284), internal `SharedScripting`/`SharedTerminal` (SshShellSession.cs:110/115), `SyncAfterInteractive()` after detach (service 309), `CurrentPrompt` as startup-prompt fallback (service 394, 507), shared `Patterns.BannerAcceptPrompt` |
| **Execution preflight** | `ScriptDependencyAnalyzer.UsesInteractive` (ScriptDependencyAnalyzer.cs:253) → `SshExecutionService` blocks multi-host runs ("single-host only", SshExecutionService.cs:1041-1059) and folder runs with interactive presets list blocked names (928-932, 1031-1039); `Form1.ValidateFolderInteractiveRestrictions` (Form1.cs:12613-12668) |
| **History / details UI** | `InteractiveTerminalSessionDetails` list rides `ExecutionResult` → `ExecutionDetails` → history JSON → `ExecutionDetailsDialog` "Interactive" tab grid + transcript box (ExecutionDetailsDialog.cs:467-524, 715-760); Form1 maps sessions at 1756-1770 and 13151+ |
| **Flow Canvas** | `FlowCanvasBridge.cs:3038` special-cases `InteractiveSessionMode.Shared` (test-step gating) |
| **UI thread** | `InvokeOnUiThreadAsync` marshals through `Application.OpenForms[0]` (service 3615-3638) — also the owner for `form.Show` |
| **Theming** | `DialogTheme.ApplyTo` + `SetDarkTitleBar` (form 154-160) |
| **Memory diagnostics** | Form1.cs:6269/6321 reports the terminal history limit in its memory estimate |

---

## Observed gaps & quirks

### Error-visibility / behavior bugs
1. **`AppendOutput` is a deliberate no-op** (Forms/InteractiveTerminalForm.cs:168-172). Every `[interactive-error] {ex.Message}` line routed through `AppendOutputSafe` (service 1037, 1940, 2123, 2246) is silently discarded — on a pump failure the window just closes with zero explanation to the user.
2. **No reconnect of any kind** — a dropped connection closes the window silently (1687-1704); a professional SSH tool would show "connection lost" with a reconnect affordance.
3. **No scheduler/unattended guard**: nothing in the job pipeline blocks `interactive:` scripts (grep across Job* services: zero hits). A scheduled job running such a preset will pop a terminal window on the desktop and block the job until a human closes it. The single-host/folder preflights only cover the main-window path (SshExecutionService.cs:1041, Form1.cs:12613).
4. **`IsTimeoutException` is message-string matching** ("timeout"/"timed out"/"time limit", service 3673-3678) — brittle and locale-sensitive; any reworded Rebex exception is treated as fatal instead of a poll timeout.

### Keyboard / input gaps
5. **F1–F12 are not mapped** (form KeyDown switch 303-320) — function-key-driven full-screen apps (mc, aptitude, htop F-bar) are unusable.
6. **No keyboard copy/paste**: copy is select-to-copy only, paste is right-click only; Ctrl+Shift+C/V, Ctrl+Insert/Shift+Insert all absent. No bracketed paste — a multi-line paste executes each line immediately (form 449-467, 481-486), a footgun on production hosts.
7. **No mouse reporting** forwarded to the host (no xterm mouse protocol), despite alternate-screen support in the transcript filter.
8. **Shared `exit`/`logout` guard is bypassable**: guard state resets on any nav/control key (service 3160-3165, acknowledged in comment), and only exact `exit`/`logout` match (`ShouldBlockSharedShellCommand` 2952-2960) — `exit 0`, `;exit`, Ctrl+D-into-subshell etc. pass through. Tripwire, not protection.

### Configuration / hardcoded values
9. **Appearance is fully hardcoded**: Courier New 10pt (form 117), 0xBBBBBB-on-black (117-119, 159, 163), selection blue (viewport 23-24), cursor blink 530 ms (viewport 52). Ignores `AppConfiguration.FontSettings`; no font-size/zoom control.
10. **Scrollback limit 10,000 lines hardcoded** (SshTerminalOptionsFactory.cs:15), not in Settings.
11. **No host-key verification** for separate sessions (`ConnectAndLogin` 3728-3740) — matches the app-wide audit theme; an interactive terminal is exactly where MITM matters.
12. Window position remembered only in a per-process `static` (form 50-51) — not persisted to `WindowState`/config like every other window in the app.

### Mode inconsistencies / half-finished work
13. **`EmulationMode` never populated** — `CreateSessionDetails` hardcodes `string.Empty` (service 2934); the field exists in the model (ExecutionDetails.cs:47) and serializes empty forever.
14. **`width`/`height`/`columns`/`rows` ignored in shared mode** — `RunSharedAsync` passes no sizing args (288-304); only separate/capture honor them.
15. **Shared-mode resize leaks**: resizing the window calls `SetScreenSize` on the *pooled session's* terminal (1843-1859) and nothing restores the original size after close — subsequent script steps run against the altered geometry (wrapping/pager behavior).
16. **Detached read-only mode loses all color** — `RenderDetachedHistorySnapshot` fills monochrome defaults (form 607-610) even though the live screen had colors moments earlier.
17. `SelectAllVisible`/`HasSnapshot` are public API wired to nothing in the UI (only tests invoke them — no Select-All menu item or shortcut exists).
18. **Banner-accept loop logic is convoluted** (service 2812-2829): once `bannerAcceptCount >= maxBannerAccepts` it *still* sends the accept key (now with `\r`) and only bails after `maxBannerAccepts + 2` — reads like leftover trial-and-error.

### Heuristic fragility
19. **Capture natural completion can fire early**: `ContainsCommandEchoLine` is a case-insensitive *substring* match on any line (2707) — output that merely mentions the command text arms completion; the completion itself trusts prompt-likeness of the last line (`PromptDetector.IsLikelyPrompt` fallback, 2714-2734) when startup prompt detection failed.
20. Viewport assumes 1 char = 1 cell (metrics from a 'W' sample, viewport 749-777) — CJK double-width and emoji misalign; the Rebex screen model and the renderer will disagree on columns.
21. `TerminalOutputProcessor.Normalize` CSI H honors only the column parameter (495-504) — inherent to the single-line model, but multi-row cursor-addressed output flattens unexpectedly in transcripts.

### Performance / hygiene
22. **Busy-poll pump per open terminal**: 2 ms `Process` + 2-8 ms delays continuously on a threadpool thread for the life of each window (2128-2132; headless 2052-2053) — measurable CPU with several terminals open.
23. Pervasive empty `catch { }` on shutdown paths (handler detach, sends, clipboard, keepalive, resize) — appropriate individually, but combined with gap #1 the pump's fatal-error detail is lost entirely.
24. `InvokeOnUiThreadAsync` and window ownership key off `Application.OpenForms[0]` (3615-3638, 935, 1904) — fragile if the first open form isn't the main form (e.g., a detached Flow Canvas window opened first).
25. "Reset Terminal" is only `Screen.Clear(true)` (3578-3581) — does not reset emulation modes/charset/palette; the menu name oversells it.

### Docs / test coverage
26. **No sample script demonstrates the `interactive:` step** — `ScriptSamples/generic/interactive_config.yaml` is about `input:` prompts, not the terminal.
27. Test coverage is strong for **static helpers** (`InteractiveTerminalServiceTranscriptFilterTests` ~47 tests over filtering/caps/close-reasons/prompt logic) and `TerminalOutputProcessorTests` (~60 tests), plus 2 WinForms selection/scrollback tests (`InteractiveTerminalFormTests`) and mocked `InteractiveCommandTests` — but the ~1,500 LOC of window/pump orchestration (`Run*LoopAsync`, `PumpFullAsync`, `ScreenUpdateDispatcher`) has **zero** automated coverage; lifecycle and race behavior rely on manual testing.
