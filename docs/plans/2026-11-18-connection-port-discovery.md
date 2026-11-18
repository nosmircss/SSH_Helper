# Connection Port Discovery Implementation Plan

> **For agentic workers:** REQUIRED AGENT WORKFLOW: Read and follow `subagent-driven-development.agent.md` (recommended) or `executing-plans.agent.md` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow ordinary SSH connections to discover their port from an ordered `port` list, without requiring YAML.

**Architecture:** Capture the selected raw connection policy and its source when building hosts, separately from the concrete transport port. After YAML preconnect, resolve that policy into a run-only host using the existing unauthenticated probe implementation; all connection paths consume the concrete result while events, credentials, and history retain the original row identity. Share policy capture and resolution between manual execution, jobs, and current-host terminal/SFTP operations.

**Tech Stack:** Existing .NET 8 Windows Forms application, Rebex.SshShell 7.0.9561, SSH.NET, xUnit 2.7.0, FluentAssertions 6.12.0, React/TypeScript/Vitest for the existing Flow Canvas host preview. No new dependencies.

**Spec:** The approved user design is reproduced in “Approved specification” below in this document; read it together with the task list.

## Global Constraints

- PLAN ONLY now: create only this document; implementation requires subsequent user review and authorization.
- “DEFAULT TIMEOUT 2 SECONDS PER CANDIDATE.”
- “Existing YAML portcheck default5 should remain backward compatible; new automatic feature defaults2.”
- “User-defined candidates, no hardcoding.”
- “Treat explicit source precedence deliberately; do not infer explicitness from Port==22.”
- “Single fixed ports bypass probe; optional probe settings apply to discovery only.”
- “No matching list must not silently fall back22.”
- “Validate entire selected candidate list before network access.”
- “Authentication failure after selection does NOT retry other ports.”
- “No persistent winner cache.”
- “Maintain original row identity for events/UI/history/credential lookup and keep grid/env unchanged.”
- “No auto probes for local-only scripts.”
- “Avoid new competing ssh_ports setting or large settings UI redesign.”
- Follow `AGENTS.md` and `CLAUDE.md`: keep Form1 thin, use InputValidator for user-input validation, preserve algorithm settings and cancellation behavior.
- Do not access `rebex.key`, inspect copied variants, or reconstruct its contents. Licensed verification requires an explicitly supplied `REBEX_LICENSE_KEY`; never print or save it.
- No staging, commits, resets, or other Git mutations are authorized. Preserve all pre-existing worktree changes.

## Review Focus

1. Explicit port 22 versus an SSH-config nonstandard port: explicit 22 remains 22, including endpoint and row sources. Task 1 pins this.
2. Invalid ignored settings versus invalid selected settings: an explicit endpoint or `_ssh_port` bypasses malformed lower-priority lists; a selected list is fully validated before its first connection. Tasks 1–3 pin this.
3. Warm connection pools versus changed reachability: every new discovery resolution respects candidate order before ordinary reuse of the selected endpoint; a previously winning later port cannot jump the queue. Tasks 2–3 pin this.
4. Two rows for the same address with different policies, and concurrent runs: runtime port/variables cannot leak between rows, credentials, output routes, or later runs. Tasks 3–4 pin this.
5. Scripts with no primary SSH shell but default-current-host SFTP, and explicit alternate endpoints: probe only the connection actually needed; do not change explicitly supplied alternate host/port behavior. Task 5 pins this.

---

## Approved specification

### User-facing examples

`port` is an existing environment variable / optional host-grid column, not a new settings field:

| Environment `port` | Row `port` | Host_IP | Result |
|---|---|---|---|
| `22,222,2222` | blank/missing | `router-a` | Discover 22, then 222, then 2222 |
| `22,222,2222` | `22` | `router-a` | Fixed 22; **no discovery** |
| `22,222,2222` | `2200,2222` | `router-a` | Discover 2200, then 2222 |
| `2222` | whitespace | `router-a` | Fixed 2222 |
| missing/blank | missing/blank | `router-a` | SSH-config port, otherwise 22 |
| `bad,list` | `also,bad` | `router-a:22` | Fixed 22; ignored lists cannot block it |

Basic setup: environment variable `port=22,222,2222`; optionally `port_check=ssh`. Clear existing row `port=22` cells if those rows should inherit the environment list. No migration clears those cells automatically.

### Precedence and parsing

Highest first:

1. Valid `_ssh_port` produced by YAML preconnect.
2. Explicit valid `Host_IP` endpoint port.
3. Nonblank row `port`.
4. Nonblank environment `port`.
5. SSH-config port when enabled, otherwise 22.

Run preconnect to completion before automatic resolution. Invalid `_ssh_port` fails explicitly; do not downgrade to a list. A higher-priority fixed source bypasses all lower-priority policy validation and probing. Capture policy without eagerly rejecting a list that preconnect may override.

For the selected `port`, trim surrounding/token whitespace. A single integer in 1..65535 connects normally. A comma-separated value is an ordered discovery list: validate every token first, including empty tokens and range violations. Reject `22,`, `,22`, `22,,2222`, `22,nope`, 0, 65536, and integer overflow with the offending value/source in the diagnostic. Do not silently drop bad tokens. Preserve duplicate candidates and their written order; do not introduce implicit candidates, ranges, or a cache. Use invariant integer parsing.

Reserved discovery settings resolve independently, using nonblank row value, then nonblank environment value, then default:

| Setting | Accepted value | Default |
|---|---|---|
| `port_check` | `ssh` or `tcp` (trim, case-insensitive) | `ssh` |
| `port_check_timeout` | integer seconds, 1..2147483 | **2** |

The upper timeout bound matches the existing probe's millisecond conversion/deadline limits. Validate these settings only when a selected list actually needs discovery. Fixed ports ignore invalid discovery-only settings. Blank row settings inherit their respective environment values, not the other settings' sources.

### Network, runtime, and compatibility behavior

- Sequentially choose the first qualifying candidate. TCP means a socket connection. SSH means version exchange plus cryptographic negotiation through Rebex `Connect`, with **no authentication**.
- Each candidate has one total deadline, default **2 seconds**, shared across all existing algorithm fallback tiers. Preserve configured ciphers/host-key algorithms and the existing fallback rules.
- Ordinary closed/timeout/handshake failures advance to the next candidate. Cancellation propagates and stops attempts. Configuration/licensing/programming failures must not be disguised as an unreachable list.
- Exhaustion fails with ordered candidate diagnostics; it never attempts an unlisted 22.
- After selection, perform normal authentication once for that selected endpoint. Authentication failure, or a subsequent connection race, fails the run rather than trying another candidate.
- Resolve each new run/connection intent in list order before asking the existing pool for its concrete endpoint. Healthy pooled connections may be reused **only after that endpoint has been selected**. An already resolved active script/session is reused without probing again. This deliberately favors simple strict ordering over a pool-aware winner shortcut.
- Store raw selected policy/source separately from run-only `Port` and `${port}`. Preconnect sees the original raw variable until it selects `_ssh_port`; after selection, runtime variables reflect the actual port. Never rewrite saved rows, environment values, or job definitions.
- Output includes candidate attempt, outcome, and selected actual port even outside debug mode. Route it and later transport events using the original host object; textual connection banners may name the concrete endpoint.
- Preserve credential lookup/storage keys, existing per-row overrides, preconnect credential overrides, and SSH-config algorithm/authentication settings.
- Local-only scripts do not acquire a connection or auto-probe. Default-current-host SFTP resolves lazily if no shell already resolved it. Explicit alternate SFTP host/port bypasses this host policy. Interactive shared sessions use their existing endpoint; separate/capture sessions reuse a resolved current-host endpoint or resolve once before connecting.
- Existing YAML `portcheck` remains opt-in, with its existing default mode `tcp`, default timeout **5 seconds**, capture fields, validation, and error semantics.
- UI help teaches scalar/list/blank inheritance. Preview must show the selected raw list, never a misleading fixed 22.

### Grounding and baseline

Baseline HEAD inspected: `557691c5d77c2cc00b6b834601de664831bfc163`. The worktree already contains implemented preconnect discovery, including untracked `Services\Scripting\Commands\PortcheckProbe.cs` and `SSH_Helper.Tests\Scripting\PortcheckProbeTests.cs`. These are **inputs**, not changes made by this plan.

Existing modified paths at planning time: Flow Canvas registry/properties/graphSlice and its add/connect/delete tests; `SCRIPTING.md`; scripting NetworkCommand/NetworkStepParser/ScriptDependencyAnalyzer tests; FlowCanvasBridge and SshExecutionServicePreconnect tests; editor autocomplete; C# FlowCanvasBridge; PortcheckCommand; ScriptStep; ScriptDependencyAnalyzer; ScriptParser; SshExecutionService. Do not replace these files from HEAD or attribute their cumulative diff solely to this feature.

Relevant inspected seams:

- `Models\HostConnection.cs`: integer Port, Parse, and ApplySshConfig currently treating `Port == 22` as implicit.
- `Form1.cs:GetHostConnections` (~13300): numeric row parsing precedes environment merge; credential lookup occurs during construction. `BuildHostPayload` (~13455) currently substitutes 22 for lists.
- `Services\JobExecutionService.cs:BuildHostConnections` (~487): separate numeric parsing; currently copies only job rows, with no environment-variable merge or SSH-config port policy.
- `Services\SshExecutionService.cs`: ExecuteSingleHost; ExecuteScriptOnHost; ResolveEffectiveScriptAuthContext (~1852), including its no-preconnect early return; pooled and direct transport methods.
- `Services\Scripting\Commands\PortcheckProbe.cs`: existing TCP/SSH transport, cancellation disposal, shared fallback deadline, and fake handshake-client seam.
- `Services\Terminal\InteractiveTerminalService.cs`: RunAsync dispatches shared, separate, and separate-capture paths; separate paths connect CurrentHost directly.
- `Services\Scripting\Commands\SftpCommand.cs:ResolveEndpoint`: currently defaults to 22 independently of CurrentHost.Port.
- `FlowCanvas\src\panels\HostBar.tsx` suppresses the raw `port` variable and displays only numeric targetHost.port; existing variables map can carry the policy without a new bridge field.

## File responsibilities and interfaces

New production files:

- `Models\ConnectionPortPolicy.cs`: immutable policy/source data; no network work.
- `Services\ConnectionPortPolicyService.cs`: pure capture of row/environment/SSH-config precedence, and selected-value validation via InputValidator.
- `Services\ConnectionPortProbe.cs`: extracted shared transport from PortcheckProbe; no candidate selection or authentication.
- `Services\ConnectionPortResolver.cs`: ordered selection, run-only host clone, diagnostics, cancellation.

Existing files are changed only for the owning task below. Keep the existing transport implementations and dependency analyzer rather than restructuring the execution engine or adding a new scripting command.

Required shared contracts (define these in Tasks 1–2; later tasks consume them):

- `public enum ConnectionPortSource { Default, SshConfig, Environment, Row, Endpoint, Programmatic, Preconnect }`.
- `public sealed record ConnectionPortPolicy(string RawPort, ConnectionPortSource Source, string? RawCheck, string? RawTimeout)`.
- On `HostConnection`: `ConnectionPortPolicy? PortPolicy`, `bool HasExplicitPort { get; private set; }`, `bool IsPortResolvedForRun { get; internal set; }`. Keep `int Port` and default 22; an explicit setter assignment marks HasExplicitPort, but default initialization and applying fallback configuration do not. Parse sets it only for a valid written endpoint.
- `internal static void ConnectionPortPolicyService.Apply(HostConnection host, IReadOnlyDictionary<string,string> row, IReadOnlyDictionary<string,string> environment, SshHostConfig? sshConfig)`: capture policy and merge ordinary variables (nonblank row wins), apply existing non-port SSH configuration, and materialize valid fixed scalar ports for legacy identity/credential-key compatibility. Never probe or throw for an unvalidated list/discovery setting here.
- `internal static (IReadOnlyList<int> Ports, string Mode, TimeSpan Timeout) ConnectionPortPolicyService.ValidateDiscovery(ConnectionPortPolicy policy)`: selected-list validation only; throws actionable `InvalidOperationException`.
- Add InputValidator APIs `TryParsePortCandidates(string value, out IReadOnlyList<int> ports, out string? error)` and `TryParsePortCheckTimeout(string value, out int seconds)`; successful candidate parsing returns every token in order, not a partial list.
- `internal sealed record ConnectionPortProbeResult(string Status, string Latency, string Diagnostic)`, retaining `open`/`closed`/`timeout` and current latency conventions.
- `internal interface IConnectionPortProbe`: `Task<ConnectionPortProbeResult> CheckAsync(HostConnection host, int port, string mode, TimeSpan timeout, CancellationToken cancellationToken)`. `ConnectionPortProbe` implements it, retaining the existing optional fake-handshake factory seam internally.
- `internal sealed class ConnectionPortResolver(IConnectionPortProbe? probe = null)`: `Task<HostConnection> ResolveAsync(HostConnection original, string? preconnectPort, Action<string> report, CancellationToken cancellationToken)`. Null preconnectPort means absent; empty means invalid. Return an isolated host/variables clone marked resolved, including for fixed ports. A host already marked resolved bypasses discovery when no new preconnect override is supplied.
- Transport injection for execution tests: `internal ConnectionPortResolver PortResolver { get; set; }` on SshExecutionService and InteractiveTerminalService, initialized normally in production.

Policy capture selects RawPort/source before overwriting runtime variables. No policy means an existing programmatically constructed host's concrete Port remains authoritative; do not reinterpret a script-created runtime variable as a new candidate policy. Fallback config application must use explicitness/source metadata, never equality with 22. Preserve the full host authentication/algorithm fields when cloning.

---

### Task 1: Capture and validate connection policy without losing explicitness

**Files:** Create `Models\ConnectionPortPolicy.cs`, `Services\ConnectionPortPolicyService.cs`, `SSH_Helper.Tests\Services\ConnectionPortPolicyTests.cs`. Modify `Models\HostConnection.cs`, `Utilities\InputValidator.cs`, `SSH_Helper.Tests\Utilities\InputValidatorTests.cs`.

**Interfaces:** Produces the policy, host metadata, Apply, ValidateDiscovery, and InputValidator APIs above. Consumes existing SshHostConfig and ApplySshConfig behavior for non-port fields.

- [ ] **Step 1: Write policy/validation tests.** Use dictionaries as row/environment fixtures and real HostConnection.Parse. Pin these named cases and assertions:
  - `Apply_BlankRowInheritsEnvironmentList`: environment `22,222,2222`, row whitespace; `host.PortPolicy!.RawPort.Should().Be("22,222,2222")`, source Environment; inputs remain unchanged.
  - `Apply_RowScalar22RemainsExplicit`: row 22, environment list, SSH-config port 2200; `host.Port.Should().Be(22)` and source Row.
  - `Apply_Endpoint22Wins`: endpoint `router:22`, malformed row/environment lists; Port 22, source Endpoint, no exception.
  - `Apply_ImplicitPortUsesSshConfig`: `Parse("router")` with config 2200 yields 2200/source SshConfig; no config yields 22/source Default.
  - `Apply_ExplicitProgrammatic22IsNotAnImplicitDefault`: `new HostConnection { Port = 22 }` followed by ApplySshConfig(2200) remains 22; default construction without assignment accepts 2200.
  - `ValidateDiscovery_ValidatesEntireList`: theory inputs `22,`, `,22`, `22,,2222`, `22,nope`, `22,0`, `22,65536`, `22,2147483648` all throw; error contains offending token and Row/Environment source.
  - `ValidateDiscovery_PreservesOrderWhitespaceAndDuplicates`: `" 2222, 22,2222 "` yields `[2222,22,2222]`; omitted options yield `"ssh"` and `TimeSpan.FromSeconds(2)`.
  - `Apply_SettingsInheritIndependently`: row port list and `port_check_timeout=3`, environment `port_check=tcp` and timeout 9 yield tcp/3; case-insensitive keys, `" SSH "` normalizes to ssh.
  - `ValidateDiscovery_TimeoutBounds`: accept 1 and 2147483; reject 0, -1, 2147484, 2.5, overflow, and nonnumeric input; unsupported check mode rejects with setting name.
- [ ] **Step 2: Run failing tests.** `dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -p:SkipFlowCanvasBuild=true --filter "FullyQualifiedName~ConnectionPortPolicyTests|FullyQualifiedName~InputValidatorTests"` — new contract/tests must fail before implementation.
- [ ] **Step 3: Implement the Task 1 contracts.** Capture highest-priority nonblank raw value without eagerly validating it; materialize only valid fixed ports. ApplySshConfig must respect explicit ports and pending policies while preserving username/key/algorithm merging. Copy dictionaries case-insensitively; never mutate caller dictionaries.
- [ ] **Step 4: Run the Step 2 command again.** Expected: all selected tests pass, including existing validator cases.
- [ ] **Step 5: Leave a reviewable working-tree diff.** No staging/commit; record only Task 1 paths relative to the existing dirty baseline.

### Task 2: Reuse the probe and implement ordered run-only resolution

**Files:** Create `Services\ConnectionPortProbe.cs`, `Services\ConnectionPortResolver.cs`, `SSH_Helper.Tests\Services\ConnectionPortResolverTests.cs`. Modify `Services\Scripting\Commands\PortcheckProbe.cs`, `Services\Scripting\Commands\PortcheckCommand.cs`, `SSH_Helper.Tests\Scripting\PortcheckProbeTests.cs`, `SSH_Helper.Tests\Scripting\NetworkCommandTests.cs`.

**Interfaces:** Consumes Task 1 policy. Produces IConnectionPortProbe/ConnectionPortProbeResult and ResolveAsync above. Keep PortcheckProbe as a thin adapter to the extracted transport if needed to preserve its existing test call sites; there must be only one handshake/deadline implementation.

- [ ] **Step 1: Write resolver and extraction regression tests.** Implement a recording IConnectionPortProbe fake returning queued results and recording host, port, mode, timeout, token; it must never authenticate.
  - `Resolve_FirstQualifyingCandidateWins`: queue closed/open/open for `[22,222,2222]`; assert attempted ports equal `[22,222]`, result.Port is 222, runtime Variables["port"] is `"222"`, and original raw policy/Port/Variables are unchanged.
  - `Resolve_FixedPortIgnoresDiscoveryOptions`: scalar22 with invalid mode/timeout returns22 and `calls.Should().BeEmpty()`.
  - `Resolve_OverrideSkipsInvalidLowerPolicy`: `_ssh_port="2200"` plus malformed list/options selects2200 with no probe; invalid/empty override fails with `_ssh_port` diagnostic and no probe.
  - `Resolve_WholeListValidatedBeforeFirstAttempt`: `22,bad` throws and calls remain empty even when fake would report22 open.
  - `Resolve_ExhaustedDoesNotFallBack`: list `222,2222` closed/timeout fails; calls equal only `[222,2222]`, message contains both candidate outcomes.
  - `Resolve_CancellationStopsBeforeNextCandidate`: cancel during first probe; assert OperationCanceledException and exactly one attempt; already-cancelled token causes zero attempts.
  - `Resolve_EachNewRunStartsAtFirstCandidate`: first run picks222; second run with22 now open picks22. An already resolved host causes no further probe. Two independent concurrent resolves keep distinct result-variable dictionaries.
  - `Resolve_ReportsAttemptsAndSelection`: captured messages contain each attempted endpoint, mode, timeout/outcome, and selected endpoint; no credential text.
  - `Probe_FallbackTiersShareCandidateDeadline`: preserve fake handshake-client coverage for all three tiers; remaining timeout decreases rather than resetting to2s, cancellation disposes the blocked client. Test within-deadline behavior with synchronization, not tight wall-clock assertions.
  - `Portcheck_DefaultsRemainCompatible`: `new PortcheckOptions().Timeout.Should().Be(5)` and Mode `"tcp"`; existing YAML capture/error tests still pass after extraction.
- [ ] **Step 2: Run failures.** `dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -p:SkipFlowCanvasBuild=true --filter "FullyQualifiedName~ConnectionPortResolverTests|FullyQualifiedName~PortcheckProbeTests|FullyQualifiedName~NetworkCommandTests"`.
- [ ] **Step 3: Extract transport and implement ResolveAsync.** Preserve existing Rebex Connect-only handshake, algorithm setup, fallback classifier, candidate-level deadline and cancellation disposal. Resolve a fixed override before touching list/options; validate all selected list inputs before invoking the probe. Clone all host fields/variables, set actual runtime port invariantly, mark resolved, and surface ordered exhaustion diagnostics. Do not absorb license/configuration faults into ordinary candidate failures.
- [ ] **Step 4: Repeat Step 2.** Expected: all selected tests pass without needing a license for fake-handshake tests; real TCP tests use loopback only.
- [ ] **Step 5: Leave Task 2 changes unstaged.** Preserve the prior preconnect implementation and tests; no task creates a winner cache.

### Task 3: Resolve once in normal/YAML execution and preserve row identity

**Files:** Modify `Services\SshExecutionService.cs`, `SSH_Helper.Tests\Services\SshExecutionServicePreconnectTests.cs`. Create `SSH_Helper.Tests\Services\SshExecutionServicePortDiscoveryTests.cs`.

**Interfaces:** Consumes ResolveAsync and the PortResolver injection property. Existing public execution APIs remain unchanged. Internally carry `originalHost` separately from the effective transport host through pooled/direct execution and script event relays.

- [ ] **Step 1: Write execution-path tests.** Use injected recording probes and a narrow internal transport callback seam at the pooled/direct connection-dispatch boundary, so tests capture the concrete HostConnection without real credentials. Define the seam as `internal Action<HostConnection>? BeforeConnectionForTesting { get; set; }`; invoke immediately before either primary transport acquires/connects, after selection. Test callbacks throw a recognizable sentinel exception to stop before network access, or an authentication-classified exception for the authentication case. Assert endpoint/order/events and the expected sentinel failure; do not claim these fake-dispatch tests prove a successful real login.
  - `Simple_PooledAndDirectResolveBeforeConnect`: theory for UseConnectionPooling true/false; fake selects222; callback sees Port222 and Variables["port"] `"222"`; probes run before callback, once.
  - `Yaml_NoPreconnectStillResolves`: shell-requiring YAML with no preconnect selects222, defeating the current early-return gap.
  - `Yaml_PreconnectRunsBeforeAutomaticDiscovery`: preconnect prints a marker; fake reports attempt after marker. A preconnect `_ssh_port=2200` with invalid lower policy/options produces no probe and callback sees2200.
  - `Yaml_PreconnectFailureOrCancellationStopsDiscovery`: failure/cancellation prevents probe and transport callback.
  - `Yaml_LocalOnlyDoesNotDiscover`: print-only YAML with a selected malformed list succeeds without probing; retain existing local preconnect `_ssh_port` tests.
  - `SelectedEndpointAuthenticationFailureDoesNotAdvance`: fake selects22; callback throws an authentication-classified Rebex exception; result fails as authentication error and probe attempts equal `[22]`, never222.
  - `EventsAndResultsKeepOriginalHost`: collect ProgressChanged, OutputReceived, CommandCompleted and results; every event Host and result.Host is the original object, while output names selected222. Repeat with two original rows sharing IpAddress but different lists.
  - `SecondRunDoesNotInheritRuntimePort`: same original host reused twice; first222 then22; original Variables["port"] stays the raw list.
  - `WarmPoolDoesNotSkipEarlierCandidates`: ordering trace is probe22, probe222, connection dispatch222, not pool reuse before probe22. In the next run when22 qualifies, dispatch22 even if222 was previously selected.
- [ ] **Step 2: Run failures.** `dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -p:SkipFlowCanvasBuild=true --filter "FullyQualifiedName~SshExecutionServicePortDiscoveryTests|FullyQualifiedName~SshExecutionServicePreconnectTests"`.
- [ ] **Step 3: Integrate run-only endpoint selection.** Resolve simple execution before pooled/direct dispatch. For YAML, finish ResolveEffectiveScriptAuthContext first, honor `_ssh_port`, then resolve only when a primary SSH session is required. Preserve raw PortPolicy through preconnect cloning and seed the final ScriptContext.CurrentHost/`${port}` after resolution. Local-only YAML retains preconnect behavior without automatic probing. Use originalHost for events/results/history and effectiveHost for connection keys, headers, commands and runtime substitution; do not mutate the source host to make UI matching work.
- [ ] **Step 4: Repeat Step 2.** Expected: all selected tests pass. Verify the callback seam covers pooled/direct, simple/YAML paths and is null/no-op in production. Existing pool APIs continue accepting concrete ports only.
- [ ] **Step 5: Leave Task 3 changes unstaged.** Keep output history and cancellation assertions in the review scope, not only successful selection.

### Task 4: Apply the same policy to manual host building and scheduled jobs

**Files:** Modify `Form1.cs:GetHostConnections`, `Services\JobExecutionService.cs:BuildHostConnections/ExecuteJobCoreAsync`, `SSH_Helper.Tests\Services\JobExecutionServiceTests.cs`. Create `SSH_Helper.Tests\UI\ConnectionPortHostBuilderTests.cs`.

**Interfaces:** Both builders consume ConnectionPortPolicyService.Apply. Preserve the existing private `BuildHostConnections(JobDefinition job)` signature for current tests. Obtain a copy of active environment variables once per job run through EnvironmentService using the existing ConfigurationService; obtain enabled SSH configuration through SshConfigService and configured path. Manual execution snapshots its active execution environment once per host batch.

- [ ] **Step 1: Write builder/parity tests.** Use temporary ConfigurationService/environment state and STA tests for any actual Form1/grid fixture.
  - `Builders_BlankAndScalarRowsMatchEnvironmentPolicy`: for manual and job construction, environment list plus blank row produces Environment policy; row22 produces fixed Row22; row list overrides environment; missing values use configured port/default22.
  - `Builders_EndpointPortOverridesInvalidPolicy`: endpoint22 plus invalid row/env list remains fixed22.
  - `Builders_ProbeSettingsMergeIndependently`: row tcp/environment timeout3 gives tcp/3; whitespace row values inherit and case-insensitive column names work.
  - `Jobs_RunNowAndScheduledUseSameSnapshot`: both dispatch paths see the same policy given identical configuration. A later environment change affects the next run, not hosts already built for an in-progress run. No job serialization rewrite occurs.
  - `CredentialLookupRetainsOriginalKeys`: row scalar port continues using its pre-feature host key; discovered winners never change lookup/storage keys. Two same-address rows retain their original identities and independent policies.
  - `Builders_PreserveSshConfigAuthAndAlgorithms`: fixed/list policy capture preserves key file, username and cipher/host-key arrays from enabled SSH config.
- [ ] **Step 2: Run failures.** `dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -p:SkipFlowCanvasBuild=true --filter "FullyQualifiedName~ConnectionPortHostBuilderTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~SshConnectionPoolKeyTests"`.
- [ ] **Step 3: Replace duplicate numeric parsing with shared policy capture.** Build row dictionaries, snapshot environment defaults, and call Apply before credential lookup so existing fixed-port keys remain stable. Retain vault/password resolution logic and event behavior. Jobs read the active environment at run start (matching the existing active-environment Vault default), not by binding new environment state into JobDefinition. Do not broaden job/preset environment selection or persist merged variables into job rows.
- [ ] **Step 4: Repeat Step 2.** Expected: old explicit endpoint/row port tests and all new parity tests pass; no builder performs network access.
- [ ] **Step 5: Leave Task 4 changes unstaged.** Record that scheduled-job environment defaults are run-start snapshots, with row values still authoritative.

### Task 5: Make current-host terminal and SFTP endpoints consistent

**Files:** Modify `Services\Terminal\InteractiveTerminalService.cs`, `Services\Scripting\Commands\SftpCommand.cs`. Create `SSH_Helper.Tests\Services\InteractiveTerminalPortDiscoveryTests.cs`, `SSH_Helper.Tests\Scripting\SftpEndpointTests.cs`. Extend `SSH_Helper.Tests\Scripting\ScriptDependencyAnalyzerTests.cs` only for regression coverage; do not force SFTP-only scripts to acquire a primary shell.

**Interfaces:** Terminal consumes its injected PortResolver and resolved CurrentHost. Add `internal Action<HostConnection>? BeforeConnectionForTesting { get; set; }` to InteractiveTerminalService and invoke it before either separate/capture ConnectAndLogin call; tests capture the endpoint and throw a sentinel to avoid real login/window creation, as in Task 3. Replace the private SFTP endpoint helper with `internal static Task<(string Host, int Port, string Username, string Password)> ResolveEndpointAsync(SftpOptions options, ScriptContext context, ConnectionPortResolver resolver, CancellationToken cancellationToken)`; its caller awaits it before creating SftpClient. Preserve existing explicit option precedence and credential substitution.

- [ ] **Step 1: Write endpoint tests without network/authentication.**
  - `Terminal_SeparateAndCaptureUseResolvedCurrentHost`: both modes with an already resolved222 host make zero new probes and dispatch222; shared mode never probes.
  - `Terminal_UnresolvedCurrentHostResolvesOnce`: selected policy probes in order before ConnectAndLogin, updates current runtime port, preserves original row; cancellation prevents connect/window creation.
  - `Sftp_DefaultHostUsesSelectedPort`: omitted Host and Port with unresolved current-host policy resolves222; tuple.Port is222 and context.GetVariableString("port") is `"222"`. A second default-host operation in the same context does not probe again.
  - `Sftp_ResolvedCurrentHostDoesNotReprobe`: default-current-host SFTP after primary shell selection uses that selected endpoint, including a preconnect override.
  - `Sftp_ExplicitEndpointIsNotOverridden`: explicit alternate Host with no port stays22; alternate `host:2200` stays2200; explicit Port2201 wins over endpoint port2200. Each case makes zero policy probes even with an invalid current-host policy.
  - `Sftp_ExplicitPortOnCurrentHostBypassesDiscovery`: omitted Host with Port2201 uses current address/2201 and does not resolve its list.
  - `Analyzer_LocalAndSftpOnlyPreserveSessionRequirements`: print-only and SFTP-only scripts still report RequiresSshSession false; default-current-host SFTP resolves lazily in the command, not before a purely local print.
- [ ] **Step 2: Run failures.** `dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -p:SkipFlowCanvasBuild=true --filter "FullyQualifiedName~InteractiveTerminalPortDiscoveryTests|FullyQualifiedName~SftpEndpointTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~InteractiveTerminalServiceTranscriptFilterTests"`.
- [ ] **Step 3: Resolve only default-current-host connection intents.** In terminal separate/capture paths resolve before resource creation/ConnectAndLogin and keep cancellation/TerminalOutputProcessor handling. For SFTP use explicit options first; only omitted host and port inherit the current-host policy. Seed the effective context after lazy resolution without changing original row/credential identity. Report attempts via context output. No new terminal endpoint option is introduced; existing InteractiveOptions has no alternate host field.
- [ ] **Step 4: Repeat Step 2.** Expected: all selected tests pass without a real terminal or SSH server.
- [ ] **Step 5: Leave Task 5 changes unstaged.** Retain explicitly separate endpoints and existing shared-session behavior in the review brief.

### Task 6: Teach inheritance, preserve accurate preview, and verify end-to-end parity

**Files:** Modify `Form1.cs` reserved-column help and BuildHostPayload, `EnvironmentDialog.cs` variable help, `JobEditorDialog.cs` host help, `UI\FlowCanvasForm.cs:SetTargetHost`, `FlowCanvas\src\panels\HostBar.tsx`, `README.md`, `SCRIPTING.md`. Create `SSH_Helper.Tests\UI\ConnectionPortPreviewTests.cs`, `FlowCanvas\src\panels\__tests__\HostBar.test.tsx`. Keep `TargetHost.port: number` and the existing variables-map bridge contract unchanged.

**Interfaces:** Preview consumes the captured selected RawPort/source without calling ResolveAsync, fetching vault credentials, or performing network work. BuildHostPayload puts selected raw port policy in existing `variables["port"]` and merges environment defaults; legacy numeric port is only a compatibility field. SetTargetHost documents this display rule. HostBar reads variables.port first for policy display and falls back to numeric port for older payloads.

- [ ] **Step 1: Write preview/help tests.**
  - `Preview_EnvironmentListSurvivesBridge`: blank row/environment list produces variables.port `"22,222,2222"`; no probing or mutation.
  - `Preview_EndpointWinsOverRowList`: Host_IP explicit22 renders fixed22, not the ignored list.
  - `HostBar_DisplaysRawCandidatePolicy`: render payload numeric port22 plus variables.port list; assert `expect(screen.getByText("22,222,2222")).toBeInTheDocument()` and no fixed22 Port chip is rendered.
  - `HostBar_LegacyScalarPayloadStillWorks`: no variables.port, numeric2222 renders2222; no target retains the existing no-target message.
  - `Help_ExplainsBlankInheritanceAndDefaults`: reserved-column help contains the list example, blank inheritance, `ssh`/`tcp`, and timeout2 seconds; existing row22 is never described as automatic.
- [ ] **Step 2: Run failures.** Run the .NET UI filter below and, from `FlowCanvas`, `npm test -- src/panels/__tests__/HostBar.test.tsx`; expected new assertions fail before UI implementation.
- [ ] **Step 3: Implement preview and concise help.** Reuse pure policy capture, not credential-bearing GetHostConnections, for preview. Preserve the existing bridge fields and existing YAML snippets/round-trip logic; no block registry or new script node is needed. Make coordinated C# and React host-preview updates as required by CLAUDE.md. Use these help meanings: “Port: a fixed number or ordered list such as 22,222,2222. Blank inherits environment; Host_IP :port overrides.” “port_check: ssh (default, no authentication) or tcp.” “port_check_timeout: seconds per candidate; default 2.” Show advisory input diagnostics without preventing execution of valid higher-priority overrides.
- [ ] **Step 4: Update documentation.** README explains the setup table, existing row22 caveat, sequential checks, 2-second deadline, strict ordering with pool reuse, and no retry after authentication. SCRIPTING distinguishes automatic settings from YAML `portcheck`'s unchanged tcp/5-second defaults and advanced preconnect `_ssh_port`.
- [ ] **Step 5: Run focused .NET/UI and full verification commands below.** Expected: successful build and all relevant tests pass; report any unrelated existing failures separately, not as success.
- [ ] **Step 6: Leave a reviewable working-tree diff.** No commits. Preserve pre-existing changes and keep the baseline/task path ledger for later review. If a review package is requested during execution, use baseline HEAD plus `WORKTREE` and state that the package is cumulative.

## Verification commands and acceptance

Run from the repository root unless noted. Tasks use filtered commands for red/green; final integration includes:

```powershell
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -p:SkipFlowCanvasBuild=true --filter "FullyQualifiedName~ConnectionPort|FullyQualifiedName~PortcheckProbeTests|FullyQualifiedName~NetworkCommandTests|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SshExecutionServicePreconnectTests|FullyQualifiedName~SshExecutionServicePortDiscoveryTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~SftpEndpointTests|FullyQualifiedName~InteractiveTerminalPortDiscoveryTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~FlowCanvasBridgeTests|FullyQualifiedName~SshConnectionPoolKeyTests|FullyQualifiedName~SshConnectionPoolCompatibilityTests"
dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -p:SkipFlowCanvasBuild=true
```

In `FlowCanvas`:

```powershell
npm test
npm run build
```

Expected: filtered and full suites report zero failures, .NET build succeeds, TypeScript/Vite production build succeeds. Prior full .NET runs reportedly had timing/callback failures under load; reproduce and distinguish them with targeted reruns rather than disabling tests or claiming an unrun suite passed. No implementation/test execution is claimed by this planning document.

Optional licensed/manual acceptance requires an explicitly available license and controlled SSH test endpoints; never access `rebex.key`:

1. With environment list and blank row, run a simple preset and shell-using YAML; verify attempt order, actual connected endpoint and `${port}`. Repeat with pooling enabled/disabled and a scheduled/run-now job.
2. Test an open non-SSH socket before a real SSH server: ssh skips the non-SSH service; tcp selects it and the subsequent SSH connection fails without continuing discovery.
3. Exercise an endpoint needing existing algorithm fallback; confirm all fallback tiers share the candidate's 2-second budget and missing license/configuration errors are surfaced accurately.
4. Make first qualifying server reject credentials; verify only that endpoint receives authentication, and a later candidate receives none.
5. Cancel during a blocked handshake; verify prompt cancellation/resource release and no next attempt.
6. Open separate/shared/capture in-app terminals and default-current-host SFTP after selection; verify endpoint parity and no duplicate discovery. Explicit SFTP alternatives remain independent.
7. Re-run after earlier candidate becomes healthy with a later endpoint warm in pool; verify order restarts at the first candidate. Grid, environment and job source values remain unchanged.

## Self-review and handoff

Coverage checked against the approved design: precedence/explicit22 and validation (Task 1); reuse/deadlines/cancellation/default compatibility (Task 2); preconnect ordering, local bypass, authentication boundary, identity and pooling (Task 3); manual/job parity and environment snapshots (Task 4); terminals/default SFTP versus explicit alternatives (Task 5); previews/help/documentation and verification (Task 6).

Interfaces use the same policy, probe result and ResolveAsync signatures throughout. The five Review Focus conditions have named owning tests. No new setting namespace, persistent cache, package upgrade, scripting command, job schema redesign, or implementation is included.

No unresolved material design questions. Decisions made explicit here: preserve duplicate candidates; normalize check mode case; snapshot the active environment at job start; strict candidate order before pool reuse; lazy SFTP-only discovery. These keep the approved design within existing execution semantics.

**Stop here for user review.** This is a Superpowers plan, not an OpenSpec change. Execution method has not been selected in this request; after review, choose native execution or task-by-task subagent-driven execution. Native is the lower-overhead recommendation because the six tasks share a small set of tightly coupled policy/resolver interfaces; preserve the user's no-nested-agents restriction for this planning session.
