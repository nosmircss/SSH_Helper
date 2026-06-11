# Subsystem map: Models, ScriptSamples & Build/Release pipeline

Scope: `Models/` (32 files), `ScriptSamples/` (35 files), `SSH_Helper.csproj`, `Program.cs`, `.github/workflows/build-release.yml`, `FlowCanvas/package.json` + `FlowCanvas/vite.config.ts`. All paths relative to repo root `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper`.

---

## Feature inventory

### 1. Configuration data model (config.json root)

**`Models/AppConfiguration.cs` (578 lines)** — the single root DTO persisted by `ConfigurationService` to `%LocalAppData%\SSH_Helper\config.json`. Contains:

- **Presets** `Dictionary<string, PresetInfo>` (line 18) plus four parallel ordering structures: `ManualPresetOrder` (29), `ManualPresetOrderByFolder` (41), `ManualFolderOrder` (46), `ManualFavoriteOrder` (52, items prefixed `"preset:"`/`"folder:"`). `PresetSortMode` enum (6-11): Ascending/Descending/Manual.
- **Connection defaults**: `Username` (19, plaintext in config), `Timeout` 10s (20), `ConnectionTimeout` 30s (21), `UseConnectionPooling` default **false** (22).
- **State restore**: `RememberState` (58), `SavedState` (59) + `SavedStateCompressed` (64, `gz64:` GZip blob — compression handled in ConfigurationService, model only holds the string).
- **Environments**: `Environments` dict (case-insensitive, 65), `ActiveEnvironment` (66), `BaseEnvironment` (70, operator-selected restore base).
- **History/jobs knobs**: `MaxHistoryEntries` 30 (73), `MaxConcurrentJobs` 3 (140), `DefaultMaxHistoryRuns` 50 (146), `DefaultHistoryRetentionDays` 30 (152), `MaxJobOutputCharsPerHost` 1 MiB (158), `LastAppShutdownUtc` (134 — anchors missed-run detection).
- **Sub-settings objects**: `WindowState` (25), `UpdateSettings` (55), `FontSettings` (91), `CommandEditor` (96), `SshConfig` (102), `Credentials` (107), `Vault` (112), `Notifications` (117).
- **Recent files**: `RecentFiles` + `MaxRecentFiles` 10 (123-128).
- **Theme**: `DarkMode` default true (79); `AutoResizeHostColumns` (85).

**`CommandEditorSettings`** (165-208): 20 Scintilla editor flags (highlighting, autocomplete, inline validation w/ debounce 150-2000ms clamp, hygiene warnings, indent 2-8, smart enter, folding, brace matching). Has `Normalize()`/`CloneNormalized()` clamping (195-207) — the only settings class with self-validation.

**`FontSettings`** (213-343): per-surface font families/sizes (13 size knobs), `GlobalScaleFactor` (303), word-wrap flags, row heights, `CustomAccentColor` int? ARGB (332), `ScaledSize()` helper (342). `ScriptPromptFontSize` (296) is deliberately independent of `DialogFontSize` and overridable per-step via YAML `font_size:`.

**`ApplicationState`** (348-395): hosts grid snapshot (rows as `List<Dictionary<string,string>>`), columns, checked indices, `LastCsvPath`+`LastCsvFingerprint`, selected preset/folder, `Username` ("not password for security", 388), and legacy in-config `History` list kept **only for migration** (394).

**`HistoryEntry`/`HostHistoryEntry`** (400-437): legacy + current display shapes. NOTE `HistoryEntry.Timestamp` (410) is actually a **display label** (doc admits "legacy field name: Timestamp").

**`UpdateSettings`** (442-473): auto-update against GitHub releases — hardcoded defaults `GitHubOwner="nosmircss"`/`GitHubRepo="SSH_Helper"` (447-452), `CheckOnStartup` true, `SkippedVersion`, opt-in update log.

**`WindowState`** (478-547): window geometry (default 1850×1050 at 50,50 — lines 480-483), 5 main splitter distances, environment-dialog geometry, and **24 Flow Canvas persistence fields** (499-546): panel sizes, reduced-motion, heatmap, block width/text scale/density, default-expanded, snap, branch bands, compact comments, iteration history cap, Run Output color/wrap/follow prefs, detached Run Output window geometry (530-533), legacy `FlowCanvasAutoReflow` (540, explicitly marked for removal "after one release cycle"), `FlowCanvasDefaultLayoutMode` (546). This class is the React-side display-settings sink (written via `layout-save`/`pref-save` bridge messages).

**`SshConfigSettings`** (552-559): single flag `EnableSshConfig` (default false) to read `%USERPROFILE%\.ssh\config`.

**`CredentialSettings`** (564-576): `UseCredentialManager` (default false), `PreferSshAgent` (default false).

### 2. Preset model

**`Models/PresetInfo.cs` (97 lines)**: `Commands` setter normalizes any newline style to CRLF (83-95 — load-bearing for content-hash drift comparison). `Timeout` is the **only** per-preset override (int?, line 34); no per-preset delay. `Type` is computed, never stored (`ScriptParser.IsYamlScript`, line 61) with **both** `System.Text.Json` and Newtonsoft `[JsonIgnore]` attributes (59-60 — dual-serializer hedge). `Folder` supports nested paths "Network/Cisco/Switches" (42). Carries Flow Canvas persistence: `CanvasLayout` (48) and per-preset `LayoutMode?` (54, null = inherit global). `Clone()` (70-81) deep-copies.

**`Models/FolderInfo.cs` (40 lines)**: per-folder `BaseEnvironment` override (12, inherits up the ancestor chain), `IsExpanded`, `SortOrder`, `IsFavorite`, `Clone()`.

**`Models/CanvasLayoutData.cs` (110 lines)**: Flow Canvas layout persisted per preset — `StructureHash` SHA256 gate (18), `Positions` keyed by node id with `StepPath`+`BlockType` match tuple for prefix-safe merge (72-82), `Comments` (`CanvasComment` 84-102: kind 'comment' vs 'sticky', `Anchor` with Type/StepPath/LineOffset 104-109, default color hardcoded `#e0c040` at 88), `DisabledBlockIds`, `ExpandedNodeIds`. Manual deep `Clone()` (40-69).

**`Models/LayoutMode.cs`**: `AutoFlow`/`Manual` enum — int-serialized, **member order is a wire contract** (do not reorder).

### 3. Job/scheduler model

**`Models/JobDefinition.cs` (274 lines)** — persisted to `jobs.json`:
- Enums: `CredentialMode` (8-29: InheritFromApp/Stored/PerHostColumn/Vault), `ScheduleType` (34-50: None/Recurring/OneTime), `FolderExecutionMode` (55-66: Sequential/Parallel), `JobExecutionState` (71-79: Queued..Skipped), `JobTargetType` (84-100: Preset/Folder/CustomPreset).
- `CustomPresetCommands` setter CRLF-normalizes (138-142, private helper at 261-272 — duplicated verbatim from PresetInfo).
- Legacy compat fields kept alive: `TargetContentHash` (147), `FolderPresetHashes` (153), `HasDriftWarning` (201 — "scheduler execution no longer blocks on this").
- Embedded host snapshot: `Hosts` + `HostColumns` (159-164) — jobs carry their own copy of the grid, not a reference to an environment.
- Vault: `VaultCredentialPath` (175), `VaultProfileName` (181).
- Schedules: `CronExpression` (186), `OneTimeScheduleUtc` (191) — both doc-commented "placeholder for Phase 3" though scheduling has long since shipped (stale comments).
- `DisabledReason` (207) for auto-disable, `RunningState` (223, `RunningJobState` = just `StartedUtc`, persisted mid-run for crash recovery), `StopOnError` (235), per-job overrides: command/connection timeout, MaxHistoryRuns, HistoryRetentionDays (241-259, all nullable → global default).

**Job history (file-pair design)**: `Models/JobRunRecord.cs` (index entry, 108 lines — incl. failure-streak collapse `ConsecutiveFailureCount` 63, skipped-run summary fields 68-84, `RunFileName` pointer 89; wrapped in `JobRunIndexDocument` with `SchemaVersion` 96-107) + `Models/JobRunPayload.cs` (91 lines — full per-host outputs, loaded on demand). The two duplicate ~12 metadata fields by design (index never needs payload IO). `Models/JobHostOutput.cs` (44 lines): per-host output + `Label`/`LabelReplacesAddress` from the `sethistorylabel` script command. `Models/JobRunResult.cs` (60 lines): event-payload for `JobCompleted` ("Phase 4 history handoff"); `HostOutputs` may be null on error paths (58). `Models/JobRunFilter.cs` (29 lines): success/date-window query, MaxResults 50. `Models/JobHistoryRetentionOptions.cs` (38 lines): MaxRuns 50/RetentionDays 30/MaxOutputChars 1 MiB constants mirrored from AppConfiguration defaults.

**Scheduler runtime**: `Models/QueuedJob.cs` (FIFO entry, in-memory only), `Models/RunningJobState.cs` (crash-recovery marker), `Models/SkippedRunEntry.cs` (one missed run, "never auto-executed"), `Models/SkippedRunSummaryEntry.cs` (aggregated per-job downtime window). `Models/JobExportDocument.cs`: `.sshjobs` export wrapper with `Version=1` + `ExportedUtc`.

### 4. Execution/history model

- **`Models/ExecutionResult.cs` (23 lines)**: per-host result — output, success, cancel, exception, interactive-session transcripts, history-label operations (depends on `Services.Scripting.Models.HistoryLabelOperation`, line 1).
- **`Models/ExecutionDetails.cs` (54 lines)**: "View Details" metadata — preset name/commands/type, env, username, timeouts, pooling, run mode, folder-run preset list, `HostExecutionContext` per host (vars snapshot at exec time, 30-37), `InteractiveTerminalSessionDetails` (42-53: session number/mode/emulation/close reason/full transcript).
- **`Models/HistoryIndex.cs` + `HistoryRunPayload.cs` + `HistoryListItem.cs`**: same index/payload split as job history for main-form execution history (`history/` dir). `HistoryIndexDocument.SchemaVersion=1`.
- **`Models/FolderExecutionOptions.cs` (40 lines)**: folder run dialog options — selected presets, parallel presets, stop-on-first-error, `ParallelHostCount` default 1 (27), suppress separators, selected host indices. **`Models/FolderExecutionProgress.cs` (49 lines)**: operation/preset/host counters for progress UI.
- **`Models/ConnectionTestResult.cs`**: positional record (Success, ErrorCategory, ErrorMessage, LatencyMs) — the only `record` in Models.
- **`Models/CsvFileFingerprint.cs` (37 lines)**: LastWriteTimeUtc+size identity for "CSV changed on disk" detection; `Normalize()` coerces DateTimeKind and clamps negative sizes (20-35).

### 5. SSH connection model

- **`Models/HostConnection.cs` (94 lines)**: ip/port/user/pass + per-host `Variables`, key auth (`IdentityFile`/passphrase 18-23), algorithm pinning (`HostKeyAlgorithms`/`Ciphers` 28-33). `Parse("ip:port")` (38-54) — **splits on `:`; IPv6 literals are mis-parsed** (e.g. `2001:db8::1` → IpAddress "2001"). `ApplySshConfig` merge precedence grid > config (71-90); port merge condition `Port == 22` means an *explicit* grid port 22 gets overridden by ssh-config (76-77). `ToString()` omits `:22`.
- **`Models/SshConfigFile.cs` + `SshHostConfig.cs`**: parsed `~/.ssh/config` representation (Host blocks with wildcard patterns, case-insensitive options) and the merged per-host resolution (HostName/Port/User/IdentityFile/HostKeyAlgorithms/Ciphers). Only 6 directives modeled — no ProxyJump, no KexAlgorithms, no MACs.

### 6. Environment model

**`Models/EnvironmentConfig.cs` (91 lines)**: named host-grid profile — `DefaultName="Default"` (8), description, `LabelColor` int? (12), full hosts/columns/selection snapshot, CSV path+fingerprint, case-insensitive `Variables` (18), per-environment `VaultProfileName` (24). `FromApplicationState` factory (26-46), `Clone` (48-69), `Normalize` (71-89, re-wraps every row dict case-insensitively).

### 7. Vault & notification settings models

- **`Models/VaultSettings.cs` (163 lines)**: `Enabled` + named `VaultProfileConfig` list + `DefaultProfileName`. Profile = address, namespace, `MountPath` "secret" (47), 5 auth methods (`VaultAuthMethod` 131-147: Token/AppRole/Ldap/Userpass/Oidc — secrets always in Credential Manager, only usernames/role-ids in config), OIDC loopback callback config (host whitelist 127.0.0.1/localhost/::1 at 87, port 8250, path `/oidc/callback`, 180s timeout, 76-105), `CacheTtlSeconds` 300 (110), custom CA path, **`SkipTlsVerification`** (120, "development/test only" — model allows persisting it), `VaultKvVersion` AutoDetect/V1/V2 (152-162).
- **`Models/NotificationSettings.cs` (111 lines)**: `Enabled` gate + `NotificationProfile` list + default profile. `NotificationChannelKind` (83-99): Slack/Teams/Discord/Toast/Smtp — webhook URLs and SMTP password live in Credential Manager (doc at 27). SMTP fields: host, port 587, from, to-list, username, `UseStartTls` true (46-78). `NotificationLevel` Info/Warn/Error/Success (104-110).

### 8. Application entry point

**`Program.cs` (70 lines)**:
- Rebex license resolution chain (17-40): assembly metadata (CI-injected) → `rebex.key` beside exe (dev) → `REBEX_LICENSE_KEY` env var. Silently proceeds unlicensed if all three miss (Rebex features then fail at first use, not at startup).
- `RegisterRebexEllipticPlugins()` (62-68) **unconditionally** references `EllipticCurveAlgorithm`/`Curve25519`/`Ed25519` types from the *conditionally referenced* `libs\RebexElliptic` DLLs — the csproj `Exists()` condition (csproj:75) is illusory: the project does not compile without those DLLs (they are committed to git, including binaries, so it works in practice).
- Portable-build storage validation (48-56): `AppDataPaths.ValidateStartupStorageWritable` → modal error + exit if the exe dir isn't writable.
- `ScintillaNativeBootstrap.ConfigureSatelliteDirectory()` (58) before `Form1` — native Scintilla/Lexilla extraction.

### 9. .NET build pipeline (SSH_Helper.csproj, 143 lines)

- TFM `net8.0-windows10.0.17763.0`, WinExe, nullable+implicit usings, `WebView2LoaderPreference=Static` (12).
- **Portable flavor** (18-21): `-p:PortableBuild=true` → `PORTABLE_BUILD` define + assembly renamed `SSH_Helper_Portable` (storage root redirected to exe dir via `AppDataPaths`).
- **Release publish** (24-30): single-file, self-contained, win-x64, native-libs self-extract, compression — *only* when `Configuration==Release`; Debug stays framework-multi-file.
- **BuildTimestamp** (33-43): UTC compile time baked into assembly metadata; used by `FlowCanvasDistLocator` to version the embedded-dist extraction dir. Side effect: builds are never deterministic.
- **Rebex license injection** (13, 45-50): `REBEX_LICENSE_KEY` env → `RebexLicenseKey` assembly metadata; `rebex.key` copied to output when present (53-55; gitignored at .gitignore:375).
- **Packages** (62-73): CronExpressionDescriptor 2.45.0, Cronos 0.11.1, Toolkit.Uwp.Notifications 7.1.3, WebView2 1.0.3124.44, NAudio 2.2.1, Newtonsoft 13.0.3, Rebex.SshShell 7.0.9561, Scintilla5.NET 6.1.1 (`GeneratePathProperty` for native-DLL embedding), SSH.NET 2024.1.0, YamlDotNet 16.3.0.
- **Rebex elliptic manual refs** (75-88): three DLLs from `libs\RebexElliptic\net8.0\`, gated on existence (but see Program.cs note above).
- **WebView2 WPF exclusion**: two redundant mechanisms — hardcoded-path `Reference Remove` (90-92, pinned to `1.0.3124.44` so it silently stops working on package bump) **and** a generic `RemoveWebView2WpfReference` target (112-117).
- **FlowCanvas integration**: `BuildFlowCanvas` target runs `npm run build` before every .NET build (95-97, skip via `-p:SkipFlowCanvasBuild=true`); `IncludeFlowCanvasDistEmbeddedResources` (100-110) embeds `FlowCanvas/dist/**` as `SSH_Helper.Resources.FlowCanvasDist/...` resources for single-file extraction.
- **Scintilla natives embedded** (119-126) as `SSH_Helper.Resources.Scintilla.win-x64.*` (win-x64 only — no arm64).
- `InternalsVisibleTo`: SSH_Helper.Tests + FlowCanvasParityCli (57-60). Excludes `RebexPOC/`, `FlowCanvas/tools/`, tests from compile (4, 38).

### 10. FlowCanvas frontend build (package.json 41 lines, vite.config.ts 17 lines)

- Deps: `@xyflow/react` ^12.10.1, react/react-dom ^19.2.4, zustand ^5.0.12. Dev: vite ^8.0.1, vitest ^4.1.7, Playwright ^1.60.0 (recent CI fix commit 981f2a7), testing-library, jsdom ^29, TS ^5.9.3, **`vite-plugin-singlefile` ^2.3.2 — declared but never imported in vite.config.ts (dead dependency; only hits are package.json/package-lock)**.
- Scripts: `build` = `tsc && vite build`; `test` = vitest run (jsdom, setup `src/test-setup.ts`, excludes `e2e/**`); `test:e2e` = full Playwright; `test:e2e:parity` = serialized 4-spec parity subset; `test:e2e:dist` = build + preview-config run.
- vite.config (one file doubles as vitest config): `base:'./'` (relative URLs — required for WebView2 virtual-host/file hosting), outDir `dist`, assetsDir `assets`. Multi-file dist is intentional; the C# locator/embedder handles directories.
- `"type": "commonjs"` (21) despite an ESM-style TS config — works because Vite transpiles, but is an odd signal.

### 11. CI / release pipeline (.github/workflows/build-release.yml, 138 lines)

Triggers (3-10): **tags `v*` and manual `workflow_dispatch` only** — the `branches: ["master"]` push trigger and PR trigger are commented out (5, 8-9). Three chained jobs:

1. **flowcanvas-browser-tests** (16-53, ubuntu): npm ci → `playwright install --with-deps chromium` → `npm run test:e2e` → upload playwright-report/test-results artifacts (`if: always()`).
2. **build** (55-93, windows-2022, `needs` job 1): `REBEX_LICENSE_KEY` from secrets (60), setup .NET 8 + Node 24, npm ci, `dotnet restore -r win-x64 /p:Configuration=Release`, then **two publishes**: standard → `publish/standard`, portable (`-p:PortableBuild=true`) → `publish/portable`; upload combined artifact.
3. **release** (95-138, ubuntu, tag-gated): download artifact, stage `SSH_Helper.exe` + `SSH_Helper_Portable.exe`, copy `README.md` + `SCRIPTING.md`, generate SHA256 checksum files (120-124), `softprops/action-gh-release@v3` with `generate_release_notes: true`.

Action versions: checkout@v6, setup-dotnet@v5, setup-node@v6 (Node 24), upload-artifact@v7, download-artifact@v8.

### 12. Script samples (ScriptSamples/, 31 YAML + README + NOT_CONVERTED + 1 JSON bundle)

Layout: `bash/` (3), `cisco/` (6: IOS version/backup/interface-status/foreach + ASA shun/unshun), `checkpoint/` (1: block_ip), `fortigate/` (6 incl. 167-line `internet_service_lookup_from_file.yaml`), `generic/` (12 "command reference" samples mapped to commands in README table at README.md:160-176), `libraries/` (1 reusable subroutine library), `qa/` (2 fixture files for catalog coverage tests), root `generic_health_check.yaml` (112-line commented teaching template), `browser_callback_self_contained_presets.json` (importable preset bundle exercising `browser_callback_capture` + `assert` against localhost:38086).

Quality characteristics:
- Samples use the **canonical map syntax** (`- send:\n    command:`) and are CI-testable: `SSH_Helper.Tests/Scripting/CanonicalCommandMapSyntaxTests.cs:218-246` parses **every** `ScriptSamples/**/*.yaml` through enforced validation; `QaPresetCatalogTests` consumes `qa/`; `BrowserCallbackSelfContainedPresetTests.cs:15` validates the JSON bundle; `FlowCanvasBridgeTests.cs:2875` round-trips `bash/system_info.yaml` through the canvas bridge. Samples double as regression fixtures.
- Provenance: converted from a legacy "dudescript" format (headers like `fortigate/block_ip.yaml:2`); vendor samples document required CSV columns in header comments (e.g. block_ip.yaml:5-8).
- Library/import feature demonstrated twice: `libraries/string_sections.yaml` (subroutines with params/outputs) + `generic/library_import_demo.yaml` (imports it — but via a hardcoded placeholder absolute path, see gaps).
- Command coverage by actual usage tally (grep across all sample YAML): only **20 of 43** StepTypes appear — print(122), set(115), send(92), if(57), exit(44), extract(30), log(19), foreach(15), writefile(11), call(9), updatecolumn(8), return(5), input(5), webhook(2), wait(2), readfile(2), while(1), table(1), http(1), continue(1). **Zero YAML samples** for: repeat, try, break, exists, playsound, updateenvironment, ping, dns, portcheck, sftp, parse, choose, multiselect, confirm, interactive, switch, parallel, localcmd, vault, sethistorylabel, notify, browser_callback_capture/assert (those last two only in the JSON preset bundle).

Documentation chain: root `README.md:102` points users to `ScriptSamples/`; the deep reference is `SCRIPTING.md` (6,425 lines — covers all 43 commands with per-command sections at lines 138-4309, variables/expressions/error-handling/debug chapters after). Both README.md and SCRIPTING.md ship in releases; **ScriptSamples does not** (not in publish output, not embedded, not in release files list at build-release.yml:129-135).

---

## Integration points

- **`ConfigurationService`** owns AppConfiguration load/save (GZip `SavedStateCompressed`, `.bak`, `config.json.corrupt` salvage, legacy migrations incl. `WindowState.FlowCanvasAutoReflow` → `FlowCanvasDefaultLayoutMode` nulling, AppConfiguration.cs:540).
- **`PresetInfo.Type`** calls into `Services.Scripting.ScriptParser.IsYamlScript` (PresetInfo.cs:2,61) — Models → Scripting dependency. `ExecutionResult` depends on `Services.Scripting.Models.HistoryLabelOperation` (ExecutionResult.cs:1).
- **Flow Canvas bridge**: `CanvasLayoutData`/`LayoutMode`/`WindowState.FlowCanvas*` fields are written by React via `layout-save`/`pref-save` WebView2 messages through `FlowCanvasForm`; `NodePosition.StepPath+BlockType` is the tuple contract for prefix-safe layout merge.
- **Scheduler events**: `JobRunResult` is the `JobCompleted` event payload; `JobRunRecord/Payload` are what `JobHistoryService` persists under `job-history/<jobId>/`; `SkippedRun*` feed the startup missed-run dialog; `RunningJobState` round-trips through `jobs.json` for crash recovery.
- **Vault/Notifications/Credentials**: settings models hold only non-secret halves; secrets resolve through `Services/Credentials` (Windows Credential Manager) at runtime.
- **HostConnection.ApplySshConfig ← SshConfigService** parse of `~/.ssh/config` (gated by `SshConfigSettings.EnableSshConfig`).
- **Tests as consumers**: 4 test files reach into `ScriptSamples/` by walking up from `AppContext.BaseDirectory` to find the repo root (CanonicalCommandMapSyntaxTests.cs:247-254 et al.) — samples are part of the test contract.
- **Update pipeline loop**: `UpdateSettings` (owner/repo) ↔ GitHub Releases produced by build-release.yml; checksums published alongside exes support manual verification (no in-app signature/checksum verification implied by the model).
- **csproj ↔ runtime resource contract**: embedded resource logical names `SSH_Helper.Resources.FlowCanvasDist/...` (csproj:107) and `SSH_Helper.Resources.Scintilla.win-x64.*` (csproj:121-124) must match `FlowCanvasDistLocator`/`ScintillaNativeBootstrap` lookups; `BuildTimestamp` metadata (csproj:40-42) is the extraction cache-buster.

---

## Observed gaps & quirks

### CI / release pipeline
1. **No automatic CI on push/PR at all** — the only triggers are `v*` tags and manual dispatch (build-release.yml:3-10; branch/PR triggers commented out at lines 5, 8-9). Regressions land on master unverified.
2. **`dotnet test` is never run in any workflow** — SSH_Helper.Tests (xUnit, incl. the sample-validation and bridge parity suites) is invisible to CI. Release binaries are cut from code whose .NET tests may be red.
3. **FlowCanvas vitest unit suite (`npm test`) also never runs in CI** — only Playwright e2e (build-release.yml:43).
4. **Release gating on a flaky/red e2e suite**: `build` `needs: flowcanvas-browser-tests` which runs the *full* Playwright suite; project memory records ~11 pre-existing e2e failures (stale reduced-motion selector). If still red, tag builds fail; if CI is green, the memory is stale — either way the gate and the suite are out of sync.
5. **No code signing** of either exe; only SHA256 checksums (build-release.yml:120-124). SmartScreen/AV friction for a tool that opens SSH sessions.
6. **Missing `REBEX_LICENSE_KEY` secret degrades silently**: csproj condition (13) and Program.cs fallback chain produce an unlicensed build with no CI failure; first symptom is runtime SSH failure.
7. Both publishes re-run `npm run build` (BuildFlowCanvas fires per publish, build-release.yml:84-87) — wasted minutes, and two non-identical `BuildTimestamp`s between standard and portable artifacts of the same release.
8. Release stages artifacts with bare `cp` (111-118); a rename in publish layout breaks release silently at tag time only.

### Build / csproj / Program.cs
9. **Illusory conditional reference**: `libs\RebexElliptic` ItemGroup is `Exists()`-gated (csproj:75) but `Program.cs:62-68` references the plugin types unconditionally — removing/forgetting the libs breaks compile, contradicting the "optional" shape. Binary DLLs are committed to git (incl. an unused `monoandroid40/` flavor).
10. **WebView2 WPF removal duplicated and version-pinned**: hardcoded NuGet path `...\1.0.3124.44\...` (csproj:91) silently no-ops on any WebView2 upgrade; the generic target (112-117) is the one that actually works. Dead/booby-trapped line.
11. `vite-plugin-singlefile` is an unused devDependency (FlowCanvas/package.json:38; zero imports) — stale remnant of an abandoned single-file approach.
12. Scintilla natives embedded for win-x64 only (csproj:120-125) while `RuntimeIdentifier` is also win-x64-only — fine today, but arm64 Windows is unsupported with no guard or message.
13. `BuildTimestamp` makes every build non-reproducible (csproj:34); no version stamping otherwise visible in csproj (no `<Version>`), so release versioning rests entirely on git tags.

### Models
14. **IPv6 unsupported in `HostConnection.Parse`** (HostConnection.cs:45-52): `Split(':')` mangles any IPv6 literal; no bracket syntax handling. A multi-host SSH tool without IPv6 host entry is a real product gap.
15. **`ApplySshConfig` port edge case** (HostConnection.cs:76-77): explicit grid port 22 is indistinguishable from default → ssh-config Port wins over a user's deliberate 22.
16. **CRLF normalizer copy-pasted** in PresetInfo.cs:83-95 and JobDefinition.cs:261-272 — divergence risk for a hash-load-bearing transform.
17. **Stale "placeholder for Phase 3" doc-comments** on `CronExpression`/`OneTimeScheduleUtc` (JobDefinition.cs:184-191) — scheduling shipped long ago; misleads readers about feature maturity.
18. **Legacy field accumulation**: `TargetContentHash`, `FolderPresetHashes`, `HasDriftWarning` (JobDefinition.cs:147-153, 201), `ApplicationState.History` (AppConfiguration.cs:394), `HistoryEntry.Timestamp`-as-label (410), `WindowState.FlowCanvasAutoReflow` (540, removal promised "after one release cycle"). No schema-version field on config.json itself (job/history files have `SchemaVersion`; AppConfiguration does not).
19. **Secrets posture is good but uneven**: passwords/webhooks/tokens live in Credential Manager, yet `VaultProfileConfig.SkipTlsVerification` (VaultSettings.cs:120) is persistable production config with only a doc-comment warning, and `AppConfiguration.Username` (19) plus per-host CSV `password` columns (convention, stored in `ApplicationState.Hosts`/environment snapshots inside config.json) mean cleartext credentials can still land on disk via the state blob.
20. **Manual `Clone()` proliferation** (PresetInfo, FolderInfo, CanvasLayoutData, EnvironmentConfig, CsvFileFingerprint) — each new property must be hand-added to its clone; `CanvasLayoutData.Clone` (40-69) is already 30 lines of field copying. Easy future desync, no tests implied per-property.
21. Job index/payload twelve-field duplication (`JobRunRecord` vs `JobRunPayload`) is deliberate but has no shared base/contract test keeping them aligned.
22. `WindowState` has become a 24-field Flow Canvas preference dump (AppConfiguration.cs:499-546) — naming says "window state", role is now "all React-side display prefs"; future canvas prefs will keep landing here.
23. `EnvironmentConfig.Clone/FromApplicationState` carry a redundant `.Cast<Dictionary<string,string>>()` after a `Select` that already yields that type (37-41, 56-60) — harmless noise indicating copy-paste evolution.
24. Default window geometry hardcoded 1850×1050 (WindowState 482-483) — larger than a 1366/1600-wide laptop screen; relies on downstream clamping if any.

### Samples & user documentation
25. **`ScriptSamples/README.md` is badly stale**: command table lists 15 of 43 commands (README.md:23-41); the directory tree (134-158) omits `libraries/`, `qa/`, `generic/library_import_demo.yaml`, `generic/portchecker_api_query.yaml`, `fortigate/internet_service_lookup_from_file.yaml`, and the JSON preset bundle; all examples use compact inline syntax while the repo's own test (CanonicalCommandMapSyntaxTests.cs:218) enforces canonical map syntax for samples — the README teaches a style the samples are forbidden from using.
26. **`fortigate/NOT_CONVERTED.md` is obsolete**: it claims local command execution is "not supported" and lists it as a "future consideration" (lines 3, 47-53) — but `localcmd` exists (StepType.LocalCmd, ScriptStep.cs:1442; SCRIPTING.md:2753). The four unconverted FortiGate scripts (clearblocks, listblocks, smart_block/unblock) are now convertible and nobody has revisited.
27. **23 of 43 commands have zero YAML sample coverage** (see tally above) — notably the entire interactive family (choose/multiselect/confirm/interactive), network probes (ping/dns/portcheck/sftp), `try`/`switch`/`parallel`, `vault`, `notify`, `parse`. As "samples are user documentation," the newest/most differentiating features are the least demonstrated.
28. **Samples aren't shipped**: release artifacts = exes + README + SCRIPTING.md only (build-release.yml:126-135); samples are neither embedded nor zipped, and there's no in-app "import sample" affordance in this area — single-file-exe users never see them.
29. `generic/library_import_demo.yaml:7` hardcodes `C:\Path\To\SSH_Helper\ScriptSamples\...` requiring manual editing (comment admits it); the import system apparently lacks relative/app-relative path resolution for the demo to lean on. Same placeholder pattern in qa/catalog_runner.yaml:7 (`__QA_CATALOG_LIBRARY_PATH__`, test-substituted).
30. Vendor coverage skew: bash/Cisco/FortiGate/CheckPoint only — no Juniper, Arista, Palo Alto, Aruba, or MikroTik samples, which the README's vendor-folder structure invites. `checkpoint/` has exactly one script.
31. Root `generic_health_check.yaml` is 60% commented-out template code (lines 40-99) — useful as a teaching doc, but it executes only an uptime check; misleading as a "health check" preset if imported as-is.
