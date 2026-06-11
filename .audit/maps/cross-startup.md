# Subsystem map: Startup, first-run & app shell

Scope: `Program.cs`, `Utilities/AppDataPaths.cs`, `Utilities/ScintillaNativeBootstrap.cs`,
`Utilities/FlowCanvasDistLocator.cs`, `Utilities/SchedulerInstanceLock.cs`,
`Form1.cs` constructor + `Form1_Shown` + Load-time restore chain, `Services/ConfigurationService.cs`
(load/default/corruption path), and how the shell surfaces first-run / corrupt-config conditions.
All paths relative to repo root `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper`.

---

## 1. Feature inventory

### 1.1 Process entry & licensing bootstrap (`Program.cs`)
- `Main()` (`Program.cs:13-60`) is the entire entry point; order of operations:
  1. **Rebex license resolution** (`Program.cs:17-40`): three-tier fallback — assembly metadata
     attribute `RebexLicenseKey` (injected at build from `REBEX_LICENSE_KEY`), then a dev-time
     `rebex.key` file beside the exe (`Program.cs:24-28`), then the `REBEX_LICENSE_KEY` env var.
     If all three are absent the app **continues silently unlicensed** — pooled SSH and the
     interactive terminal will fail later at first Rebex use, with no startup warning.
  2. **Rebex elliptic-curve plugin registration** (`Program.cs:62-68`): `EllipticCurveAlgorithm`,
     `Curve25519`, `Ed25519` registered unconditionally (the DLLs are conditional references in
     the csproj gated on `libs\RebexElliptic` existing).
  3. `ApplicationConfiguration.Initialize()` (DPI/font defaults) (`Program.cs:46`).
  4. **Portable-storage writability gate** (`Program.cs:48-56`): only meaningful in
     `PORTABLE_BUILD`; on failure shows a plain `MessageBox` ("Portable Storage Error") and
     exits cleanly before any form exists.
  5. `ScintillaNativeBootstrap.ConfigureSatelliteDirectory()` (`Program.cs:58`).
  6. `Application.Run(new Form1())` (`Program.cs:59`).
- **There is no global exception handler anywhere in the codebase** — no
  `Application.ThreadException`, no `AppDomain.CurrentDomain.UnhandledException`, no
  `Application.SetUnhandledExceptionMode` (grep across `*.cs` returns zero hits). Any unhandled
  exception in the 15,360-line `Form1.cs` (or in `ConfigureSatelliteDirectory`, see 1.3) produces
  the stock .NET crash dialog / silent process death with no logging.

### 1.2 Storage root resolution (`Utilities/AppDataPaths.cs`)
- `GetAppFolder()` (`AppDataPaths.cs:30-41`): standard build → `%LocalAppData%\SSH_Helper`
  (creating it); portable build (`PORTABLE_BUILD` define, `AppDataPaths.cs:13-23`) → exe directory.
- `TryEnsureFolderWritable` (`AppDataPaths.cs:65-89`) probes with a temp `.write-probe-*.tmp` file.
- `ValidateStartupStorageWritable` (`AppDataPaths.cs:95-108`) **returns true without probing in
  standard (non-portable) builds** (`:99-100`) — a locked-down/roaming-mandatory-profile
  `%LocalAppData%` failure surfaces later as raw exceptions, not as the friendly startup gate.
- Error message hardcodes the portable exe name: "Move SSH_Helper_Portable.exe …"
  (`AppDataPaths.cs:106`).

### 1.3 Native Scintilla bootstrap (`Utilities/ScintillaNativeBootstrap.cs`)
- `ConfigureSatelliteDirectory()` (`:15-37`): prefers on-disk `runtimes/<rid>/native` beside the
  exe (`GetPackagedNativeDirectory`, `:50-54`); falls back to extracting the two embedded DLLs
  (`Scintilla.dll`, `Lexilla.dll`) — but **only for win-x64** (`SupportedRuntime` const `:9`,
  check `:30-33`).
- Extraction targets, in order (`EnumerateExtractionRoots`, `:89-111`): app storage folder →
  `%TEMP%\SSH_Helper`; path is versioned by the ScintillaNET assembly version
  (`scintilla-native/<version>/win-x64`, `:61-65`) for cache-busting. Existing files are reused,
  never re-validated (`ExtractEmbeddedResource` early-returns on `File.Exists`, `:115-118` —
  a truncated DLL from a previous crashed extraction is kept forever).
- Failure behavior is asymmetric:
  - win-x86 / arm64 with no packaged dir: **silent return** (`:30-33`) — the app launches and the
    Scintilla editor control fails later with an opaque load error.
  - win-x64 with both extraction roots unwritable: `throw InvalidOperationException` (`:84-86`)
    which is **unhandled in `Main`** → hard crash before any UI.

### 1.4 Flow Canvas dist resolution (`Utilities/FlowCanvasDistLocator.cs`)
- `ResolveDistPath()` (`:16-55`) layered search: `exe-dir/FlowCanvas/dist` →
  `project-root/FlowCanvas/dist` (root found by walking up for `SSH_Helper.csproj`/`.sln`,
  `FindProjectRoot` `:62-73`) → embedded resources extracted to
  `%AppFolder%\flow-canvas-dist\<BuildTimestamp>` (`:103-148`). Returns a
  `FlowCanvasDistResolution` record carrying every searched path for diagnostics (`:6`).
- Extraction is once-per-process (cached + locked, `:81-101`), per-build-versioned via the
  `BuildTimestamp` assembly metadata (`GetExtractionVersionSegment`, `:150-162`), and skips files
  that already exist (`:136-137`). Old version directories are **never cleaned up** — every build
  the user runs leaves a full dist copy under AppData.
- Consumers: `UI/FlowCanvasForm.cs:165` (main canvas) and `UI/RunOutputWindowForm.cs:117`
  (detached run-output window).
- Failure surfacing in `FlowCanvasForm` is good: an in-window status label listing all searched
  paths plus dev/publish remediation hints (`UI/FlowCanvasForm.cs:181-191`). WebView2-runtime
  init failure, by contrast, is just `"Error: {ex.Message}"` (`UI/FlowCanvasForm.cs:101-104`)
  with no "install the WebView2 Evergreen runtime" guidance.

### 1.5 Configuration load, defaults & corruption recovery (`Services/ConfigurationService.cs`)
- Constructor (`:17-33`): default path `%AppFolder%\config.json`; optional override path for tests.
- `Load()` (`:46-103`):
  - **First run** (no file): `CreateDefaultConfiguration()` is built, **immediately saved to disk**,
    cached, returned (`:48-54`).
  - Normal path: read → `ParseConfiguration` (legacy string-preset migration, `:208-314`) →
    inflate `gz64:` compressed `SavedState` (`:316-343`) → one-time
    `FlowCanvasAutoReflow` → `FlowCanvasDefaultLayoutMode` migration (`:65-75`) → write-back if a
    legacy uncompressed state was migrated (`:77-87`).
  - **Corruption path** (`:91-102`): `JsonFileWriter.TryBackupCorrupt(_configFilePath)` (a **copy**
    to `config.json.corrupt` — default `useMove:false`, `Utilities/JsonFileWriter.cs:103-124`),
    sets `ConfigLoadError` to a fixed message naming `config.json.corrupt`, returns defaults.
    The corrupt `config.json` itself is left in place.
- `Save()` (`:108-151`): best-effort copy of the existing file to `config.json.bak` (`:116-120`),
  GZip-compress `SavedState`, then **`File.WriteAllText`** (`:144`) — *not*
  `JsonFileWriter.WriteJsonAtomic`, violating the project's own atomic-write convention
  (CLAUDE.md "Persist JSON via JsonFileWriter.WriteJsonAtomic"). A crash/power-loss mid-write can
  corrupt the file whose only safety nets are the `.bak` and `.corrupt` copies.
- **Defaults** (`CreateDefaultConfiguration`, `:427-440`): `Username=""`, `Timeout=10`,
  pooling off, and exactly two sample presets — `"Custom"` → `get system status` and
  `"Get external-address-resource list"` → `dia sys external-address-resource list`. Both are
  **FortiGate CLI commands**, hardcoded, with no labeling as samples and no relevance to generic
  SSH targets. Other defaults from `Models/AppConfiguration.cs`: `RememberState=true` (`:58`),
  `DarkMode=true` (`:79`), `MaxHistoryEntries=30` (`:73`), update check on startup `true`
  (`UpdateSettings.CheckOnStartup`, `:457`) pointed at GitHub `nosmircss/SSH_Helper` (`:447-452`).

### 1.6 Form1 constructor — the app-shell build (`Form1.cs:312-412`)
Single synchronous pass before first paint (no splash screen):
1. `SetPreferredAppMode(AllowDark)` for native dark scrollbars (`:315`), double-buffering (`:318`),
   `InitializeComponent` (`:320`), Flow Canvas menu item with **Ctrl+Shift+F** shortcut
   (`InitializeFlowCanvasMenuItem`, `Form1.cs:6605-6619`).
2. Window title = `"SSH Helper 0.51.23"` — `ApplicationName`/`ApplicationVersion` are **hardcoded
   consts** (`Form1.cs:115-116`), independent of the csproj assembly version; the update checker
   compares releases against this string (`Form1.cs:374-377`).
3. `FormClosed` cleanup lambda registered up-front (`:333-341`).
4. Service construction (`:343-360`): `ConfigurationService` → `HistoryStorageService` (base dir
   derived from the config **file's** directory, `:345`) → `EnvironmentService` → `PresetManager`
   → `CsvManager` → `config = Load()`.
5. **Corrupt-config surfacing** (`:350-354`): if `ConfigLoadError != null`,
   `DialogTheme.Show(...)` "Configuration Warning" — ownerless and pre-theme; see gaps 3.4.
6. `SshExecutionService` (pooling per config), `SshConfigService`, `ExecutionCoordinator`
   (`:355-360`); SSH/script/debug event wiring (`:362-371`); `UpdateService` (`:374-377`).
7. ~20 `Initialize*` calls (`:379-401`): configuration→UI (`InitializeFromConfiguration`,
   `:562-584` — active environment name, username textboxes, preset sort mode, preset tree,
   timeout placeholder), credentials (`:1358-1366` — Windows Credential Manager probe + default
   password autofill via `TryLoadDefaultPassword` `:1454-1464`), Vault (`:1373-1425` — only if
   `config.Vault.Enabled` with ≥1 profile; token/secret/password providers all back onto
   Credential Manager), notifications (`:1427-1452`), host grid (`:602-652` — Select checkbox +
   `Host_IP` columns only), Scintilla script editor (autocomplete/highlight/validation,
   `:654-662`), history persistence (`:1010+`), toolbar/environment toolbar/password masking,
   `RestoreWindowState(config)` (see 1.8), scheduler **status bar** (UI only, `:15110-15147`),
   `UpdateStatusBar("Ready")`.
8. Theme + fonts applied last (`ApplyTheme(config.DarkMode)`, `ApplyFontSettings`,
   `ApplyColumnAutoResize`, `:404-408`), then `Shown += Form1_Shown` (`:411`).

### 1.7 `Form1_Shown` — deferred startup work (`Form1.cs:414-443`)
- Runs once (self-unsubscribes `:417`). Sequence: arm history-selection hydration on idle
  (`:418-421`), restore preset-folder expand/collapse state (`RestoreFolderExpandState`,
  `:480-497`), pending host-column auto-size (`:427-431`), scroll selected preset into view
  (`:434`), **defer scheduler bootstrap to `Application.Idle`** (`:435-436`,
  `BootstrapSchedulerAfterStartupRestoreOnIdle` `:452-456` → `RunDeferredSchedulerBootstrap`
  `:469-478`), then the **startup update check** if `CheckOnStartup` and not running under a test
  host (`:438-442`; test detection = process-name sniff for "testhost"/"vstest", `:445-450`).
- `CheckForUpdatesAsync(silent:true)` (`Form1.cs:14965-15039`): silent mode suppresses all error
  dialogs and the "no update" dialog; an available update shows `UpdateDialog` unless that version
  was skipped (`:14999-15003`). Every check — including the silent startup one — writes
  `UpdateSettings.LastCheckTime` via `_configService.Update` (`:14983`), i.e. **a full config
  save happens seconds after every launch**.

### 1.8 Window/layout/state restore (`Form1.cs:1625-1705`, `:14782-14889`)
- `RestoreWindowState` (`:1625-1681`): restores size/position clamped to the working area of the
  screen containing the saved top-left (≥100px kept visible, `:1633-1641`), maximized flag
  (`:1644-1647`); splitter distances (5 of them) restored in a `Load` handler with swallow-all
  `catch` per splitter (`:1650-1676`), then `RestoreInitialEnvironmentState` +
  `ConfigureHistoryListLayout` (`:1678-1679`).
- `RestoreInitialEnvironmentState` (`:1683-1705`): if environments exist, merges the active
  environment's hosts over the saved `ApplicationState` (`MergeEnvironmentIntoSavedState`,
  `:1707-1724` — environment wins for hosts/columns/selection/CSV path; saved state wins for
  selected preset/folder/username); else falls back to raw `SavedState` when `RememberState`.
  **On a true first run (no environments, no saved state) neither branch fires — the grid stays
  empty.**
- `RestoreApplicationState` (`:14782-14889`) and `LoadEnvironmentIntoGrid` (`:2242-2337`) both
  rebuild the grid and — when Credential Manager is available — **migrate any plaintext
  `password` cell values into Credential Manager and blank the cell**
  (`:14818-14834`, `:2284-2304`) keyed by host+username (default username = toolbar value).

### 1.9 First-run user experience (synthesized walkthrough)
A brand-new user (no `%LocalAppData%\SSH_Helper`) sees:
- `config.json` created silently with defaults; **no welcome screen, no onboarding, no wizard, no
  sample hosts, no hint text**.
- Dark-themed main window (DarkMode default `true`), title "SSH Helper 0.51.23".
- An **empty host grid** with only the checkbox column and `Host_IP` (`InitializeDataGridView`,
  `Form1.cs:602-652`); nothing prompts CSV import or environment creation. The synthetic
  "Default" environment is only adopted lazily when the user first opens Manage Environments
  (`EnsureDefaultEnvironmentForFirstAdoption`, `Form1.cs:2219-2228`, called from
  `TsbManageEnvironments_Click` at `:1937`).
- A preset tree containing the two FortiGate sample presets (1.5) with no indication they are
  samples or vendor-specific.
- Empty username/password toolbar fields; status bar "Ready"; scheduler status label hidden
  (0 active jobs); a silent GitHub update check fires shortly after the window appears.
- No missed-job detection (first run has no `LastAppShutdownUtc`,
  `Form1.cs:15265-15269`; the value is written only in the closing path at `Form1.cs:3946`).

### 1.10 Scheduler bootstrap & single-instance coordination
- `InitializeSchedulerServices` (`Form1.cs:15053-15105`), deferred to first `Application.Idle`
  after `Shown`: builds `SchedulingService`, `JobStorageService` (+`Load()`),
  `JobExportService`, `JobExecutionService` (wired to Vault/notifications/environment Vault
  profile), `JobHistoryService`; subscribes job events; registers cleanup on `FormClosed`.
  Hard-gated on `_credentialProvider != null` (`:15055-15056`) — if Credential Manager
  initialization failed, **the entire scheduler silently never starts**.
- `SchedulerInstanceLock` (`Utilities/SchedulerInstanceLock.cs`): named local-session mutex
  `Local\SSH_Helper_Scheduler_v1` (`:9`) plus an in-process owned-name set so a second lock in
  the same process loses (`:33-53`); `AbandonedMutexException` treated as acquired (`:42-45`).
  Loser instances keep the UI but never start the job timer; the only immediate trace is a
  `Debug.WriteLine` (`Form1.cs:15103`) — the status bar later shows
  "Scheduler paused (another SSH Helper instance is running scheduled jobs)" (const at
  `Form1.cs:130`) only when there are active jobs to display.
- Winner path: `_jobExecutionService.Initialize()` (crash recovery) →
  `RecordMissedSchedulerRunsOnStartup` (`Form1.cs:15258-15283`, diff vs `LastAppShutdownUtc`) →
  `Start()` (`:15097-15099`).
- Scheduler status bar UI (`InitializeSchedulerStatusBar`, `Form1.cs:15110-15147`): link-style
  status label opening the Job List dialog, dynamically-inserted "&Scheduler" menu item before
  Help, 5-second refresh timer.

---

## 2. Integration points

| From | To | Mechanism | Evidence |
|---|---|---|---|
| `Program.Main` | Rebex runtime | `Rebex.Licensing.Key` static + algorithm registration | `Program.cs:39,62-68` |
| `Program.Main` | Scintilla editor control | `ScintillaNativeLibrary.SatelliteDirectory` static | `ScintillaNativeBootstrap.cs:26,36` |
| `ConfigurationService.Load` | Form1 UI | `ConfigLoadError` property polled once after `Load()` | `ConfigurationService.cs:41,97`; `Form1.cs:350-354` |
| `ConfigurationService` path | `HistoryStorageService` | history base dir derived from config file's directory | `Form1.cs:345` |
| Form1 ctor | SSH/script engine | 8 event subscriptions (`OutputReceived`, `StepStarting/Completed`, `DebugPauseStateChanged`, …) | `Form1.cs:362-370` |
| Form1 ctor | `EnvironmentService` | `EnvironmentChanged` event | `Form1.cs:371` |
| `InitializeVault` / `InitializeNotifications` | Credential Manager | provider lambdas resolving tokens/secrets/SMTP/webhook secrets per profile | `Form1.cs:1394-1420,1436-1447` |
| Grid restore | Credential Manager | plaintext-password migration on every state/environment load | `Form1.cs:2292-2304,14818-14834` |
| `Form1_Shown` | `UpdateService` → GitHub | silent release check; writes `LastCheckTime` back to config | `Form1.cs:438-442,14983` |
| Idle bootstrap | Scheduler subsystem | `JobStorageService.JobsChanged`, `JobCompleted`, `JobStateChanged` → status bar | `Form1.cs:15074-15082` |
| `SchedulerInstanceLock` | other SSH_Helper processes | named mutex `Local\SSH_Helper_Scheduler_v1` | `SchedulerInstanceLock.cs:9` |
| `FlowCanvasDistLocator` | `FlowCanvasForm` / `RunOutputWindowForm` | dist path + searched-path diagnostics | `UI/FlowCanvasForm.cs:165-191`; `UI/RunOutputWindowForm.cs:117` |
| Closing path | next startup's missed-run detection | `LastAppShutdownUtc` written on close, read by `RecordMissedSchedulerRunsOnStartup` | `Form1.cs:3946,15265` |

---

## 3. Observed gaps & quirks

1. **No global exception handling at all.** Zero hits for
   `Application.ThreadException` / `AppDomain...UnhandledException` /
   `SetUnhandledExceptionMode` across the repo. For a tool that holds unsaved host grids,
   running SSH sessions and a scheduler, any stray exception = stock crash dialog, no log,
   no state flush. (`Program.cs:13-60` whole file.)
2. **Corruption recovery destroys its own best backup.** `Load()`'s corrupt path *copies* (not
   moves) the bad file to `.corrupt` and leaves corrupt `config.json` on disk
   (`ConfigurationService.cs:94`; `JsonFileWriter.cs:114-117` default `useMove:false`). The next
   `Save()` — triggered within seconds of startup by the silent update check writing
   `LastCheckTime` (`Form1.cs:14983`) — copies the **still-corrupt** `config.json` over
   `config.json.bak` (`ConfigurationService.cs:116-120`), overwriting the last-known-good backup
   before the user can act. Since presets/environments/window state all live in `config.json`,
   this is real data loss.
3. **Config save is non-atomic**, violating the project's own convention:
   `File.WriteAllText` at `ConfigurationService.cs:144` instead of
   `JsonFileWriter.WriteJsonAtomic` (temp + `File.Replace`). The `.bak` copy is `catch {}`
   best-effort (`:118-119`).
4. **Corrupt-config warning is passive and mis-themed.** Shown ownerless from the constructor
   before the main form exists, so `DialogTheme.ResolveDarkMode(null)` falls through
   `Application.OpenForms` (empty) to `false` (`UI/DialogTheme.cs:766-781`) — a dark-mode user
   gets a light dialog. The message names `config.json.corrupt` but offers no path link, no
   "restore from .bak" action, and no mention that `.bak` exists at all
   (`ConfigurationService.cs:97`; `Form1.cs:350-354`).
5. **No first-run onboarding.** Empty grid, no CSV-import prompt, no environment creation nudge,
   no hint overlay; the only "first adoption" affordance is buried in Manage Environments
   (`Form1.cs:2219-2228`). For a multi-host SSH tool the blank-canvas first launch is a real
   adoption cliff.
6. **Hardcoded FortiGate sample presets** in defaults (`get system status`,
   `dia sys external-address-resource list`, `ConfigurationService.cs:434-438`) — vendor-specific,
   unlabeled, and confusing on generic Linux/network targets; also re-created after every
   corruption fallback.
7. **Silent unlicensed-Rebex startup** (`Program.cs:37-40`): no warning when no key is found;
   the failure surfaces only when a pooled/interactive session is attempted.
8. **Scintilla bootstrap failure modes** (1.3): silent no-op on non-x64 without packaged
   runtimes (editor breaks later, `ScintillaNativeBootstrap.cs:30-33`); unhandled
   `InvalidOperationException` crash pre-UI on x64 extraction failure (`:84-86` — `Main` has no
   try/catch); stale partially-extracted DLLs reused forever (`:115-118` existence check only).
9. **Standard-build storage writability never validated**
   (`AppDataPaths.cs:99-100` early-return) — `Directory.CreateDirectory`/config write failures
   surface as raw exceptions inside the Form1 constructor (which, per gap 1, is unhandled).
10. **Version string duplication**: `ApplicationVersion = "0.51.23"` const (`Form1.cs:115`) is
    the update-comparison and title source, maintained by hand and disjoint from the assembly
    version (branch name `0.51.23` suggests manual bump workflow).
11. **Scheduler silently disabled when Credential Manager is unavailable**
    (`Form1.cs:15055-15056` early-return in `InitializeSchedulerServices`): no status-bar text,
    no dialog — jobs simply never run and the Scheduler menu's dialog guard returns silently
    (`ShowJobListDialog` `:15166-15168`).
12. **Second-instance scheduler pause is near-invisible at startup**: lock-loss is only
    `Debug.WriteLine` (`Form1.cs:15103`); the paused status text exists (`:130`) but the status
    label is hidden until there are enabled jobs (`UpdateSchedulerStatusBar` `:15205-15212`).
13. **Config saved on every launch** by the silent update check (`Form1.cs:14983`) even with no
    user change — multiplies exposure to gap 2/3 and churns `.bak`.
14. **WebView2 runtime missing → bare exception text** (`UI/FlowCanvasForm.cs:101-104`) with no
    remediation guidance, in contrast to the excellent missing-dist message (`:183-191`).
15. **Flow Canvas embedded-dist extractions accumulate** under
    `%AppFolder%\flow-canvas-dist\<BuildTimestamp>` with no old-version cleanup
    (`FlowCanvasDistLocator.cs:114-119`); same pattern for `scintilla-native/<version>`.
16. **Heavy synchronous constructor** (`Form1.cs:312-412`): config load (+ possible write-back
    save), history index load, preset tree build, Vault/notification service construction all
    block before first paint; no splash/progress. On large configs (compressed state blob) cold
    start visibly lags.
17. **Splitter restore swallows everything** with five empty `catch { }` blocks
    (`Form1.cs:1654-1675`) — benign individually, but symptomatic of layout-restore failures
    never being observed.
18. **Test-host detection by process-name substring** (`Form1.cs:445-450`) gates the startup
    update check — fragile heuristic, and only suppresses the update check (the scheduler
    bootstrap and Credential Manager writes still run under test hosts).
19. **`LastAppShutdownUtc` only written on the orderly close path** (`Form1.cs:3946`); after a
    crash the next startup computes missed runs from the previous *clean* shutdown, potentially
    recording "skipped" summaries for windows where jobs may actually have executed
    (`Form1.cs:15258-15283`).
