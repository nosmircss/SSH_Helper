# SSH Execution Engine — Feature Map

Area: `ssh-execution` | Mapped 2026-06-10 | Source of truth for downstream audit agents.

Core files (all paths repo-relative):

| File | LOC | Role |
|---|---|---|
| `Services/SshExecutionService.cs` | 2897 | Orchestrates all multi-host runs (simple commands, YAML scripts, folder runs), auth, algorithm fallback, error formatting, events |
| `Services/SshConnectionPool.cs` | 844 | Pooled Rebex `Ssh` clients keyed by host+credentials, leases, health checks, keepalive sweep |
| `Services/SshShellSession.cs` | 1476 | Shell session over Rebex Scripting: polling read loop, prompt/pager/banner handling, expect/respond |
| `Services/IShellStream.cs` | 69 | Test seam over the Rebex scripting stream (`RebexShellStream` prod impl) |
| `Services/ExecutionCoordinator.cs` | 57 | Thin prep layer: builds `PresetInfo` + `SshTimeoutOptions` from config for Form1 |
| `Services/SshTimeoutOptions.cs` | 73 | Timeout model (connection/command/idle/poll/initial-prompt/keepalive) |
| `Services/SshTerminalOptionsFactory.cs` | 40 | UTF-8 `VirtualTerminal` 120x36 + 10k history → Rebex `Scripting` |
| `Services/SshConfigService.cs` | 95 | `~/.ssh/config` load/cache (5s mtime check) + per-host resolution |
| `Utilities/SshConfigParser.cs` | 260 | OpenSSH config parser (6 options, wildcard Host matching) |
| `Services/StopOnFirstErrorTracker.cs` | 22 | Interlocked first-error latch for folder runs |
| `Utilities/PromptDetector.cs` | 254 | Prompt heuristics + tolerant prompt-regex builder |
| `Utilities/TerminalOutputProcessor.cs` | 551 | ANSI sanitize/normalize, pager artifact strip, zsh `%` strip, echo strip (consumed by session) |
| `Utilities/ManualExecutionStatusProgress.cs` | 42 | Progress % math for folder-run status bar |
| `Utilities/ExecutionDialogPolicy.cs` | 10 | "prompt for execution options when >1 host" rule |

---

## Feature inventory

### 1. Multi-host simple-command execution (sequential)
- `SshExecutionService.ExecuteAsync` (two overloads: `Services/SshExecutionService.cs:375`, `:415`). Iterates hosts **strictly sequentially**, one `Task.Run(() => ExecuteSingleHost(...))` at a time (`:389-401`, `:434-451`). Reached from Form1's Execute button via `ExecutionCoordinator.ExecutePresetAsync` (`Services/ExecutionCoordinator.cs:38`) → `ExecutePresetAsync` (`SshExecutionService.cs:466`), which auto-detects script vs simple via `preset.IsScript`.
- Per host: credential resolution (host row values beat toolbar defaults, `:1197-1198`), pooled vs direct path switch on `UseConnectionPooling` (`:1203-1214`), banner header `########## CONNECTED TO host prompt ##########` (suppressed by `showHeader:false`), typed catch ladder → `ExecutionResult` (`:1219-1271`).
- Options honored: `AppConfiguration.UseConnectionPooling` (`Models/AppConfiguration.cs:22`, applied at `Form1.cs:357`, live-toggled at `Form1.cs:5731`), `ConnectionTimeout` (`AppConfiguration.cs:21`), per-preset `Timeout`, `DebugMode` toggle.
- Invalid hosts (`!host.IsValid()`) are silently `continue`d with **no result entry** (`:394-395`, `:439-443`, script path `:589-591`).

### 2. YAML script execution per host
- `ExecuteScriptAsync` (`SshExecutionService.cs:492`): parses + validates once (`ScriptParser.Parse/Validate`, enforceCanonicalSyntax, `:506-516`); parse failure emits an error result for **every** host (`:517-537`). Then per host sequentially → `ExecuteScriptOnHost` (`:1280`).
- Three execution backends chosen at `:1317-1363`:
  - **Local** (`ExecuteScriptLocal` `:1705`) when `ScriptDependencyAnalyzer.AnalyzeSshRequirements` says no SSH session needed — script runs with `context.Session = null`.
  - **Pooled** (`ExecuteScriptWithPool` `:1501`) — `_connectionPool.CreateSessionAsync` then `ScriptExecutor.ExecuteAsync(...).GetAwaiter().GetResult()`.
  - **Direct** (`ExecuteScriptWithoutPool` `:1578`) — fresh client via `CreateConnectedClientWithFallback`, key/password login, `SshShellSession.InitializeAsync` for prompt detection.
- Script-level flags honored: `script.Debug` (forces debug output), `script.NoBanner` (suppresses header), `script.CompactErrors` (one-line errors via `FormatCompactError` `:2603`).
- `ScriptExecutor` step events (`StepStarting`/`StepCompleted`/`DebugPauseStateChanged`) are re-raised on the service (`:1559-1561`, `:1694-1696`, `:1752-1754`) — this is the Flow Canvas debug contract.
- `EnsureScriptSucceeded` (`:1828`) converts `ScriptResult.Status` into exceptions so the catch ladder produces uniform results.

### 3. Preflight gates (script safety checks)
- **Single-host-only features**: scripts using `interactive` or `browser_callback_capture` are blocked for multi-host runs (`TryBuildSingleHostOnlyPreflightMessage` `:1041`, message builder `:1053`) and blocked entirely in folder runs (`FindSingleHostOnlyFolderPresets` `:901`).
- **Unattended LocalCmd**: scripts with `localcmd` steps requiring confirmation are refused when `allowFileSelectionDialogs == false` (scheduler runs) with guidance to set `localcmd.confirm: never` (`TryBuildUnattendedLocalCmdPreflightMessage` `:937`); the walker recurses Then/Else/Do/Try/Catch/Finally/Elif/Cases/Parallel and Call subroutines with cycle guard (`ContainsConfirmedLocalCmd` `:957-1023`).

### 4. Folder (multi-preset) execution with parallelism
- `ExecuteFolderAsync` (`SshExecutionService.cs:618-899`). The **only parallel path** in the engine: hosts grouped into batches of `ParallelHostCount` (clamped 1..`MaxParallelHosts`=100, `:76`, `:636`), batch hosts run via `Task.WhenAll` (`:888`).
- `RunPresetsInParallel` runs all selected presets of one host concurrently but is force-disabled when `ParallelHostCount > 1` (`:637`, user notified `:655-660`); preset results re-ordered via `presetResultsByOrder` for history-label aggregation (`:716`, `ApplyHistoryLabelResults` `:1466`).
- Per-preset separators `═══ name ═══` and `═══ name [FAILED] ═══` unless `SuppressPresetNames` (`:726-731`, `:767-772`).
- `IProgress<FolderExecutionProgress>` reporting per completed operation (`:776-786`, `:860-870`); consumed by `ManualExecutionStatusProgress.Advance` (`Utilities/ManualExecutionStatusProgress.cs:15`) for the "Running... NN%" status.
- Options model: `Models/FolderExecutionOptions.cs` (`RunPresetsInParallel:17`, `StopOnFirstError:22`, `ParallelHostCount:27`, `SuppressPresetNames:32`). Dialog shown only when >1 host (`Utilities/ExecutionDialogPolicy.cs:5`).

### 5. Stop-on-first-error
- `StopOnFirstErrorTracker` (`Services/StopOnFirstErrorTracker.cs`) — `Interlocked.Exchange` latch; first failing preset sets `_stopOnFirstErrorCancellationRequested = true` and cancels the shared CTS (`SshExecutionService.cs:759-764`, `:839-846`).
- The volatile flag distinguishes engine-triggered cancellation from user Stop so `WasCancelled` is only set for real user cancels (`:1258`, `:1426`, host-level reconciliation `:874-882`).

### 6. Connection pooling
- `SshConnectionPool` (`Services/SshConnectionPool.cs`). Key = `ip:port:username:SHA256(password):identityFile:SHA256(passphrase)` (`CreateConnectionKey :672-678`) — credential-scoped, secrets hashed not stored.
- `GetOrCreateAsync` (`:133`): healthy-reuse fast path, per-key creation `SemaphoreSlim` + global creation gate (default 12 concurrent creations, `:51`, `:115`), double-check after lock.
- Health policy (`IsConnectionHealthyAsync :529-576`): max age 30 min default, `IsConnected` check, and an **active probe at most every 30s**: `StartScripting()` + literal `echo 1\r` + wait for regex `[#$>%]\s*$` with 5s timeout (`:552-558`).
- **Lease model** (`CreateSessionAsync :232-267`): exclusive lease per key via `_leasedKeys.TryAdd`; a concurrent caller for the same key gets a **standalone non-pooled connection** (`:249-252`). `ReleaseSession(host,user,password)` (`:272`) clears the lease; obsolete password-less overloads sweep by key prefix (`:283-293`, `:307-313`).
- Idle keepalive sweep: 5s timer (`:52`, `:118-122`) sends `Session.KeepAlive()` to unleased connections past their `KeepAliveInterval` (`RunIdleKeepAliveSweep :609-670`); dead/failed connections removed async; `ConnectionError` event raised.
- Observability: `ConnectionCreated/Reused/Removed/Error` events (`:67-82`), `PoolStatistics` with reuse ratio (`:810-843`), `GetConnectionInfo()` (`:350`); Form1 surfaces pooled count in a stats line (`Form1.cs:6320`).
- `CleanupStaleConnectionsAsync` (`:331`) and `ClearAsync` (`:318`) exist for explicit maintenance.

### 7. Shell session & command read loop (`SshShellSession`)
- Built over Rebex `Scripting` from a `VirtualTerminal` (UTF-8, 120 cols x 36 rows, 10k history — `Services/SshTerminalOptionsFactory.cs:13-15`).
- `InitializeAsync` (`:279`) → `InitializeWithPollingAsync` (`:372`): accumulates all incoming data (1s polls, 3s in debug), runs `PromptDetector.TryDetectPrompt` on sanitized buffer, **auto-accepts pre-login banners/EULAs** (FortiGate-style) via `Patterns.BannerAcceptPrompt` (`:161-179`) sending the captured accept key (max 5 accepts + retry-with-CR escalation `:431-455`), retries up to 3 attempts sending `\r` between attempts (`:516-521`). Deliberately never sends `\r` before the first read (FortiGate closes the connection, `:293-295`).
- `ExecuteAsync(command)` (`:533`): serialized via `_commandExecutionLock`, **drains residual buffer** before send (`DrainResidualBuffer :1393` — 25ms quiet window, fixes FortiGate stale-prompt redraw misalignment), then `ReadUntilPromptWithPolling` (`:717`):
  - 50ms batch reads accumulate chunks; early-break when batch contains a prompt terminator after a newline (`:774-811`).
  - **Echo guard**: prompt matches are ignored until this command's collapsed-whitespace echo appears in the buffer (`echoNeedle`/`echoConsumed`, `:736-744`, `:835-846`), with a 500ms safety window for non-echoing devices (`minTimeBeforePromptMatch :737`).
  - **Pager auto-advance**: chunk matched against 5 pager patterns (`Patterns.PagerPatterns :132-139`) → sends a space, up to `maxPages = 50000` (`:723`).
  - Streaming output pipeline per chunk (`ProcessChunk :932-979`): Sanitize → StripPagerArtifacts → StripPagerDismissalArtifacts → StripZshPromptSpStreaming → BufferIncompleteFinalLineStreaming, then `OutputReceived` event with normalized text.
  - Prompt-change tracking after completion (`UpdatePromptTracking :1247-1259`): `_currentPrompt` refreshed to literal last prompt (drives `${_prompt}` and display) while `_promptPattern` is only rebuilt when the stable anchor genuinely changes.
- `ExecuteAsync(command, expectPattern, timeoutSeconds)` (`:563`) — custom expect regex (`/.../`, quoted, or raw; `BuildExpectRegex :1203`, `NormalizeExpectPattern :1216`) via `ReadUntilPatternWithPolling` (`:994`).
- `ExecuteWithRespondsAsync` (`:608`) — ordered expect/reply pairs then final prompt wait (drives the script `responds:` feature).
- `ExecuteBatchAsync` (`:664`) — sequential commands with `${var}` and `{{var}}` substitution (`SubstituteVariables :1264`); skips blank lines and lines starting `#`.
- In-band keepalive: `TrySendKeepAliveIfDue` (`:1308`) called inside init and both read loops; interval from `SshTimeoutOptions.KeepAliveInterval` (default 25s; ≤0 disables).
- `FlushBuffer` (`:317`) — post-init drain; `SyncAfterInteractive` (`:344`) — best-effort `\r` + prompt re-read after the interactive terminal borrowed the stream.
- Testability: internal ctor injecting `IShellStream` + initial prompt (`:214-230`) — read loop is unit-tested Rebex-free (`SSH_Helper.Tests/Services/SshShellSessionReadAlignmentTests.cs`).

### 8. Prompt detection (`Utilities/PromptDetector.cs`)
- Terminators: `# > $ %` plus arrow-style `→ ❯ ➜` (`:11-14`).
- `IsLikelyPrompt` (`:160-194`) heuristics: ends with terminator, length 2..80, alphanumeric content required before traditional terminators (arrows exempt), rejects lines with ≥2 quotes (instructional text).
- `BuildPromptRegex` (`:26-64`): extracts a stable anchor — prefers `user@host` token, else strips `(mode)` / `:path` / whitespace tails (`ExtractPromptAnchor :196-244`) — and allows the body after the anchor to change (cwd, config mode), terminator class `[X#>$%]`. Non-alphanumeric anchors (starship/oh-my-zsh) fall back to "any line ending in this terminator". Patterns deliberately use `(?:^|[\r\n])` for Rebex ScriptEvent compatibility (`:50-53`, `:58-61`).
- `TryDetectDifferentPrompt` (`:132`) powers config-submode prompt switching.

### 9. Host-key/cipher algorithm fallback ladder
- Three tiers (`HostKeyAlgorithmTier` enum `SshExecutionService.cs:123`): Default → NonRsa (`ssh-ed25519` + 3 ECDSA, `:91-97`) → Ed25519Only + conservative ciphers (`aes*-ctr/cbc`, `:104-110`) + conservative MACs via reflection (`TrySetSshParameterAlgorithms :2525` — survives Rebex versions lacking `SetMacAlgorithms`).
- Triggered only by "key algorithm is not supported" in the exception chain (`HasUnsupportedKeyAlgorithmError :2538`) and only when the user/ssh-config did **not** pin `HostKeyAlgorithms` (`ShouldRetryWithAlgorithmFallback :2516`).
- Successful tier cached per `ip:port` in the **static process-wide** `HostAlgorithmCache` (`:124`) so later connections skip failed tiers; evicted on connect failure (`:2398-2404`). Pool replicates the same ladder + cache (`SshConnectionPool.cs:365-459`).
- Explicit per-host `HostKeyAlgorithms`/`Ciphers` (from ssh-config) applied verbatim (`ApplyAlgorithmSettings` `SshExecutionService.cs:2838`, `SshConnectionPool.cs:512`).

### 10. OpenSSH config integration
- `SshConfigService` (`Services/SshConfigService.cs`): default path `%USERPROFILE%\.ssh\config` (`:19-20`), mtime-based cache with 5s re-check window, `ClearCache` for settings toggle.
- `SshConfigParser` (`Utilities/SshConfigParser.cs`): supports exactly `HostName, Port, User, IdentityFile, HostKeyAlgorithms, Ciphers` (`:12-15`), `Host` blocks with `*`/`?` wildcards, first-match-wins OpenSSH merge semantics (`GetConfigForHost :97-133`), `~` expansion (`:217`), `Key=Value` and `Key Value` forms.
- Applied per host in Form1's host-enumeration when the setting is enabled — **grid values take precedence** (`Form1.cs:13383-13388`, `host.ApplySshConfig`).

### 11. Authentication
- Order per connection: SSH agent (stub — see gaps) → identity file + passphrase if `host.IdentityFile` exists on disk → username/password (`SshExecutionService.cs:1602-1614`, `:2199-2213`; pool: `LoginClient` `SshConnectionPool.cs:461-475`).
- `PreferSshAgent` config (`AppConfiguration.cs:575`) plumbed to service and pool (`Form1.cs:358`, `:5732`) but `TryLoginWithAgent` always returns false: "current SSH library does not expose agent-backed authentication APIs" (`SshExecutionService.cs:2855-2869`, `SshConnectionPool.cs:689-697`). Agent presence detection only reads `SSH_AUTH_SOCK`/`SSH_AGENT_PID` env vars (`:2871-2879`) — Pageant/Windows-OpenSSH-agent not detected.

### 12. Preconnect (credential derivation before connecting)
- Scripts with a `preconnect:` block run those steps **locally first** (`ResolveEffectiveScriptAuthContext` `SshExecutionService.cs:1851-1980`); the resulting context's reserved vars `_ssh_username`, `_ssh_password`, `_ssh_identity_file`, `_ssh_identity_passphrase` (`:82-85`) override the connection credentials; all other vars merge into the effective host's variables (`BuildEffectiveHostVariables :1982`). Control-flow (exit/break/return) in preconnect is rejected (`:1909-1912`).
- Output redaction: `Set _ssh_password = ...` / `Set _ssh_identity_passphrase = ...` lines are rewritten to `[REDACTED]` in relayed script output (`SensitiveSetOutputRegex :87-89`, `RedactSensitiveScriptOutput :2731`).

### 13. Timeout model
- `SshTimeoutOptions` (`Services/SshTimeoutOptions.cs`): ConnectionTimeout 30s, CommandTimeout 60s, IdleTimeout 10s, PollInterval 50ms, InitialPromptTimeout 30s (banner-friendly), KeepAliveInterval 25s. `FromSeconds` legacy mapping sets connection=command and idle=max(10, n/3) (`:48-56`); `Create(command, connection)` used by `ExecutionCoordinator.PrepareExecution` (`Services/ExecutionCoordinator.cs:27-33`) from the toolbar timeout + config `ConnectionTimeout`.
- Per-command override: script `timeout:` flows into `ExecuteAsync(command, expect, timeoutSeconds)`; otherwise `CommandTimeout` rules the read loop (`SshShellSession.cs:727-729`).

### 14. Debug mode diagnostics
- `DebugMode` on service + session. Emits `[DEBUG HH:mm:ss.fff] (+Nms) PHASE: message` via OutputReceived (`SshDebugLog :2824`); raw TCP pre-check before SSH connect to isolate network vs negotiation latency (`:2177-2192`); full SSH negotiation dump on failure — server-offered vs client-supported host key/kex/cipher/MAC lists (`AppendSshNegotiationDiagnostics :2628-2662`); session-level chunk dumps, prompt-regex traces, and a line-by-line "why didn't the prompt match" autopsy on timeout (`SshShellSession.cs:893-921`).

### 15. Error handling & classification
- Catch ladder per host: auth / connection / timeout / socket / cancelled / generic, classified by **exception message substrings** (`IsAuthenticationError :2341`, `IsConnectionError :2350`, `IsTimeoutError :2359`).
- `FormatError` (`:2557`) — banner-framed, deduped inner-exception chain, strips Rebex's "Make sure you are connecting to an SSH server." filler; `compactErrors` produces one-liners (`:2603`).
- Each host failure is isolated: a failed host yields a failed `ExecutionResult` and the run continues to the next host.

### 16. Connection test
- `TestConnectionAsync` (`:2304-2339`) — **TCP-only** reachability probe (connect+close with timeout race); deliberately not SSH-auth (comment `:2301-2303`). Returns typed `ConnectionTestResult` (Timeout/Cancelled/Network/Unknown).

### 17. Event surface (service ↔ UI)
- `ProgressChanged` (Host/Message/IsError/IsConnected), `OutputReceived`, `ColumnUpdateRequested` (script `set_column` → grid write-back), `EnvironmentVariableUpdateRequested`, `CommandCompleted`, `ExecutionCompleted`, plus relayed `StepStarting/StepCompleted/DebugPauseStateChanged` (`SshExecutionService.cs:131-139`). Output boundary normalization keeps multi-source output line-aligned (`NormalizeScriptOutputBoundary :2739`, relay state `:159-162`, `:1780-1818`).

### 18. Flow Canvas debug bootstrap
- `ConfigureFlowCanvasDebugStateForRun` (`:237-265`) stores node→stepPath map + breakpoint/disabled node sets; applied to each new `ScriptContext` before its first step (`ApplyConfiguredFlowCanvasDebugState :2031-2058`); cleared at `EndExecution` (`:197`). `ActiveScriptContext` (`:170`) exposes the live `DebugState` for pause/step/resume.

### 19. Execution lifecycle / cancellation
- `BeginExecution` (`:172-183`) cancels+replaces any previous CTS — one execution at a time per service instance; `Stop()` (`:1170`) cancels; `EndExecution` raises `ExecutionCompleted` and clears Flow Canvas state. The scheduler creates its **own** `SshExecutionService` per job run (`Services/JobExecutionService.cs:403` — `new SshExecutionService()`, i.e. **non-pooled**), so manual and scheduled runs never contend.

### 20. Test coverage (maturity signal)
- `SSH_Helper.Tests/Services/`: SshShellSessionReadAlignmentTests (read-loop/echo-guard regression suite), SshConnectionPoolKeyTests, SshConnectionPoolCompatibilityTests, SshExecutionService{Progress,Cancellation,Preconnect,OutputWindow,OutputFormatting,BannerFormatting,HistoryLabel,InteractivePreflight,FlowCanvasDebugBootstrap}Tests, SshTerminalOptionsFactoryTests; `SSH_Helper.Tests/Utilities/PromptDetectorTests.cs`. The connect/login/Rebex layer itself has no test seam (only the read loop does).

---

## Integration points

- **Form1** owns the singleton pooled service (`Form1.cs:356-360`: `new SshExecutionService(enablePooling: true, poolTimeouts)` + `ExecutionCoordinator` + `SshConfigService`); wires all events; builds `HostConnection`s from the CSV grid with per-host `port/username/password/vault_path` overrides, Credential Manager fallback, environment variables, and ssh-config application (`Form1.cs:13340-13391`).
- **Scripting engine**: `ScriptExecutor`/`ScriptContext` (`Services/Scripting/`) — the service seeds `CurrentHost/ResolvedUsername/ResolvedPassword/Timeouts` and `Host_IP/username/password` variables (`SeedConnectionVariables :2008-2029`), passes `VaultService`, `NotificationService`, `EnvironmentVaultProfile`, `AllowFileSelectionDialogs` (`ConfigureScriptExecutionContext :1761`). `ScriptDependencyAnalyzer` decides local vs SSH execution.
- **Interactive terminal**: script `interactive` steps register sessions; `ExecutionResult.InteractiveSessions` snapshot returned to Form1 (`:1452`); `SshShellSession.SharedScripting/SharedTerminal` (`SshShellSession.cs:110-115`) + `SyncAfterInteractive` hand the live stream to `Forms/InteractiveTerminalForm`.
- **Scheduler**: `JobExecutionService` uses a fresh non-pooled `SshExecutionService` per run with `allowFileSelectionDialogs:false` semantics (LocalCmd preflight, no dialogs).
- **Vault**: `VaultService` resolves `vault://` refs inside scripts; environment-specific profile override via `EnvironmentVaultProfile`.
- **History**: `ExecutionResult.Output` (full per-host transcript incl. headers/errors) + history-label operations from `ScriptContext` (`:1453-1462`) feed `HistoryStorageService` via Form1.
- **UI helpers**: `ManualExecutionStatusProgress`, `ExecutionDialogPolicy`, `OutputThrottler` (Form1-side display throttling of `OutputReceived`).
- **Browser callback**: `IBrowserCallbackUiHost` injected through internal ctors (`:348-364`) and passed into every `ScriptExecutor`.

---

## Observed gaps & quirks

### Security
1. **No host-key verification anywhere.** No fingerprint check, no known_hosts, no TOFU prompt — grep for `Fingerprint|HostKeyReceived|known_hosts|VerifyHostKey` finds nothing in the SSH layer. Every Rebex `Connect()` (`SshExecutionService.cs:2393/2412/2444/2485`, `SshConnectionPool.cs:389/411/433/452`) accepts whatever key the server presents. MITM-trivial; the single biggest gap for a professional multi-host SSH tool.
2. **Password leaks into script variable space**: `host.Variables["password"] = host.Password` (`Form1.cs:13368-13371`) and `SeedConnectionVariables` sets a `password` variable (`SshExecutionService.cs:2027-2028`) — any `print`/`set_column`/log step can echo it. Redaction only covers `Set _ssh_password = ...` style lines (`:87-89`); the plain `password` variable is unredacted.
3. **Health-check probe sends `echo 1` to every pooled host** (`SshConnectionPool.cs:554`) — on network appliances (FortiGate, Cisco) `echo` may be an invalid command logged as a failure on the device, and the response prompt regex `[#$>%]\s*$` (`:557`) omits the arrow-style terminators PromptDetector supports, so starship/❯ prompts always fail the active health check → permanent connection churn (unhealthy → remove → recreate every 30s window).

### Pooling correctness
4. **Standalone-session lease/leak bug** (`SshConnectionPool.cs:244-252` + `:272-280`): when a key is already leased, `CreateSessionAsync` builds a standalone client, but the caller's cleanup (`SshExecutionService.cs:1567-1571`, `:2151-2156`) still calls `ReleaseSession(host,user,password)` which `TryRemove`s the **other caller's** lease — a third caller can now grab the pooled connection while the first still uses it. Additionally the standalone `Ssh` client is never disconnected/disposed (session.Dispose() only disposes scripting/terminal), leaking a live SSH connection per collision.
5. **Standalone creations bypass the global creation gate** — `CreateConnectionAsync` is called directly (`:251`), so the 12-concurrent-creation throttle only applies to pooled `GetOrCreateAsync` paths.
6. **`HostAlgorithmCache` is static, unbounded, process-lifetime** (`SshExecutionService.cs:124`) — no expiry; a host re-keyed back to RSA stays on a fallback tier until a connect failure evicts it; the dictionary grows monotonically across large host fleets.
7. Legacy `ReleaseSession`/`RemoveAsync` (no-password) overloads sweep by `ip:port:user:` prefix (`SshConnectionPool.cs:600-607`) — can release/remove connections belonging to a different credential set.

### Execution model
8. **Multi-host runs are strictly sequential** outside folder runs (`SshExecutionService.cs:389`, `:434`, `:584`) — 100 hosts at 5s each = 8+ minutes; the engine's only concurrency lives in `ExecuteFolderAsync`. A user of a multi-host SSH tool would expect a parallelism option on plain Execute. (Known audit theme.)
9. **Invalid hosts produce no result** (silently skipped, `:394`, `:439`, `:589`) — summary counts won't match the grid selection and the user gets no per-host explanation.
10. Folder preflight `FindSingleHostOnlyFolderPresets` silently `continue`s on parse/validation failure (`:919-925`) — broken presets pass preflight and fail later mid-run, per host.
11. Folder runs re-parse + re-validate each script preset **per host per preset** (`ExecutePresetOnHost :1086` → `ExecuteScriptTextOnHost :1105-1114`), plus once more in preflight — wasted work on large fleets; contrast with `ExecuteScriptAsync` which parses once.
12. Sync-over-async throughout the per-host paths (`.GetAwaiter().GetResult()` at `:1517`, `:1562`, `:1649`, `:1697`, `:2092`, `:2148`, `:2266`, `:2291`) — each "async" host run burns a threadpool thread blocking on Rebex I/O; with folder parallelism this multiplies.
13. `BeginExecution` cancels any in-flight previous run rather than rejecting reentry (`:176`) — a double-click on Run cancels run #1 mid-host with no warning.

### Session/read-loop
14. Timeout detection by exception **message text** ("timeout"/"timed out"/"time limit") in 8+ catch filters (`SshShellSession.cs:328-331`, `:475-477`, `:799-802`, `:877-880`, `:1061-1064`, `:1121-1124`, `IsTimeoutException :1421`) — documented as matching Rebex behaviour (`IShellStream.cs:26-28`) but locale/library-upgrade fragile; same pattern for error classification in the service (`IsAuthenticationError/IsConnectionError/IsTimeoutError`, `SshExecutionService.cs:2341-2363`).
15. **Pool path skips `FlushBuffer()`** after init: `ExecuteWithoutPool` flushes residual banner data (`SshExecutionService.cs:2270`) but `ExecuteWithPool` (`:2081-2157`) and both script paths don't — pooled/script runs can leak residual init data into the first command's output (partially mitigated by `DrainResidualBuffer` before each send).
16. `FlushBuffer` (`SshShellSession.cs:317-339`) and `DrainResidualBuffer` (`:1393`) loop until a timeout exception with no iteration cap — a chatty device that streams continuously (e.g., `monitor` output running) hangs them indefinitely.
17. `ExecuteBatchAsync` drops lines beginning with `#` as comments (`:678`) — impossible to send a literal `#`-prefixed command in simple mode (some device CLIs use them).
18. `SubstituteVariables` replaces unknown `${var}`/`{{var}}` with empty string silently (`:1273`) — typo'd variable names produce mangled commands with no warning.
19. Prompt heuristics will misbehave on edge devices: prompts >80 chars rejected (`PromptDetector.cs:174`), prompts with ≥2 quote chars rejected (`:190`), non-alphanumeric-anchor prompts degrade to "any line ending in X" (`:47-53`) which can false-match command output.
20. `InitializeAsync` **returns normally even when no prompt was detected** (`SshShellSession.cs:303-307` — debug-only warning); subsequent commands then run against the generic `Patterns.ShellPrompt` and typically die by timeout per command rather than failing fast with "could not detect prompt".
21. Banner-accept retry bookkeeping is convoluted: after `maxBannerAccepts` it sends key+CR and keeps counting to `maxBannerAccepts + 2` (`:431-445`) — off-by-feel logic that allows 7 total accepts despite the "5 max" comment.
22. `ReadUntilPatternWithPolling`'s early-break terminator check uses the hard-coded set `# > $ %` (`:1055`) — missing the arrow terminators, unlike the prompt loop's `ContainsPromptTerminator` (`:1373-1383`); only costs latency, not correctness.

### Configuration / hardcoded values
23. Hardcoded knobs a power user might need: `MaxParallelHosts = 100` (`SshExecutionService.cs:76`), batch read timeout 50ms / idle 2000ms / `minTimeBeforePromptMatch` 500ms (`SshShellSession.cs:730-737`), `maxPages = 50000`, `ResidualDrainQuietMs = 25` (`:68`), pool health-check timeout 5000ms (`SshConnectionPool.cs:553`), max connection age 30min / health interval 30s (defaults `:112-113`), terminal 120x36 (`SshTerminalOptionsFactory.cs:13-14`). None surfaced in Settings.
24. `SshConfigParser` supports only 6 directives (`Utilities/SshConfigParser.cs:12-15`) — no `Include`, `Match`, `ProxyJump`/`ProxyCommand`, `KexAlgorithms`, `MACs`, `IdentitiesOnly`, multiple `IdentityFile`s. No jump-host/bastion support anywhere in the engine.
25. SSH agent support is a stub on both service and pool (`SshExecutionService.cs:2855-2869`, `SshConnectionPool.cs:689-697`) while the `PreferSshAgent` setting exists in config/UI (`AppConfiguration.cs:575`, `Form1.cs:358`) — a user enabling it gets silent password fallback (debug-only notice). Half-finished feature.
26. `TestConnectionAsync` is TCP-only (`SshExecutionService.cs:2304`) — a "connection test" that passes on any open port (e.g., a captive portal on 22) without validating SSH or credentials.

### Cosmetic / hygiene
27. Merge-scar indentation in the folder sequential-failure block (`SshExecutionService.cs:836-855`) and in `ExecuteScriptWithoutPool` (`:1679-1691`) — logic intact but signals hand-merged edits; `FindSingleHostOnlyFolderPresets` closing braces mis-indented (`:933-935`).
28. Header text differs between paths: pooled script `"... {host} {prompt} SCRIPT: {name} ..."` (`:1531`) vs non-pooled `"... SCRIPT: {host} {prompt}{name} ..."` (`:1665`) — inconsistent transcript formatting for the same feature.
29. `ExecuteAsync` legacy overload maps one timeout value to both connection and command timeouts (`SshTimeoutOptions.FromSeconds`, `SshExecutionService.cs:385`) — short command timeouts silently shrink the connection window.
