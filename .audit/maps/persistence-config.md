# Subsystem map: Config & history persistence

Scope: `Services/ConfigurationService.cs`, `Services/EnvironmentService.cs`, `Services/CsvManager.cs`,
`Services/HistoryStorageService.cs`, `Services/HistoryResultStore.cs`, `Services/HistoryIdGenerator.cs`,
`Services/PresetDeleteUndoService.cs`, `Services/UpdateService.cs`, `Models/AppConfiguration.cs` + related DTOs
(`EnvironmentConfig`, `CsvFileFingerprint`, `HistoryIndex*`, `HistoryRunPayload`, `HistoryListItem`,
`ExecutionDetails`, `PresetInfo`, `FolderInfo`), plus supporting utilities `Utilities/JsonFileWriter.cs`,
`Utilities/GZipBase64Utility.cs`, `Utilities/AppDataPaths.cs`, `Utilities/CsvFileSyncEvaluator.cs`.

All line references verified against working tree on 2026-06-10 (branch `0.51.23`).

---

## Feature inventory

### 1. Configuration load/save (`config.json`)

**What:** Single-file JSON persistence of the entire app configuration (`AppConfiguration` root DTO).
**Reach:** Implicit — loaded once in `Form1` ctor (`Form1.cs:344-354`), saved on form close
(`Form1.cs:3946-3947` → `SaveConfiguration` at `Form1.cs:14625`), and incrementally via
`ConfigurationService.Update(Action<AppConfiguration>)` from ~dozens of call sites.

Key behavior:
- **Path resolution** — default `%LocalAppData%\SSH_Helper\config.json` via `AppDataPaths.GetAppFolder()`
  (`ConfigurationService.cs:17-33`, `AppDataPaths.cs:30-41`). `PORTABLE_BUILD` compile flag redirects the
  storage root to the exe directory; portable startup validates writability with a probe file
  (`AppDataPaths.cs:65-108`). Optional ctor `configFilePath` enables test isolation.
- **Load resilience** (`ConfigurationService.cs:46-103`): missing file → create+save defaults
  (`CreateDefaultConfiguration` at :427-440 seeds two FortiGate-flavored presets). Parse failure → back up to
  `config.json.corrupt` via `JsonFileWriter.TryBackupCorrupt`, set `ConfigLoadError` (:97), return defaults.
  `Form1.cs:350-354` surfaces `ConfigLoadError` as a warning dialog.
- **Save** (`ConfigurationService.cs:108-151`): pre-copy previous file to `config.json.bak` (best-effort,
  :116-120), GZip+Base64-compress `SavedState` into `SavedStateCompressed` (`gz64:` prefix, :122-130,
  `GZipBase64Utility.cs`), serialize indented, **`File.WriteAllText` direct write** (:144), then re-hydrate
  the in-memory `SavedState` (:137-142). Throws wrapped `InvalidOperationException` on failure (:149).
- **In-memory cache** — `_cachedConfig` with `GetCurrent()` / `Update()` read-modify-write (:156-169).
- **Migrations on load:**
  - Legacy string-valued presets → `PresetInfo` objects, with a cheap streaming pre-detector to avoid the
    JObject path for modern configs (`ContainsLegacyPresetFormat` :229-278, `ParseConfigurationWithLegacyPresets`
    :280-294, `ApplyLegacyPresetFormat` :296-314).
  - Legacy uncompressed `SavedState` → write-back compressed on first load (:60-62, :77-87).
  - Flow Canvas `FlowCanvasAutoReflow` bool → `FlowCanvasDefaultLayoutMode` enum, one-shot, nulls the legacy
    field (:65-75; the legacy property is documented for removal in `AppConfiguration.cs:535-540`).
- **Normalization on parse/save:** environment dictionary keys forced case-insensitive, dangling
  `ActiveEnvironment`/`BaseEnvironment` references nulled, `BaseEnvironment` defaulted to active-or-Default
  (`NormalizeEnvironmentData` :360-403); `CommandEditorSettings.Normalize()` clamps debounce/indent/long-line
  values (:405-409, `AppConfiguration.cs:195-200`).

**Options honored:** `RememberState` (SavedState null'd when off — `Form1.cs:14679-14686`), settings dialog
clamps `MaxHistoryEntries` 1..500 (`SettingsDialog.cs:2550`), everything in `WindowState` (geometry, 5 splitter
positions, environment-dialog layout, ~25 Flow Canvas display prefs — `AppConfiguration.cs:478-547`).

### 2. AppConfiguration DTO surface (`Models/AppConfiguration.cs`)

Root config aggregates: presets dictionary + folder metadata + 4 separate manual-ordering lists
(:18-52), `UpdateSettings` (:55), `RememberState`/`SavedState`/`SavedStateCompressed` (:58-64), environments
map + active/base names (:65-70), `MaxHistoryEntries` default 30 (:73), `DarkMode` (:79),
`AutoResizeHostColumns` (:85), `FontSettings` (16 size knobs + scale factor + accent color, :213-343),
`CommandEditorSettings` (20+ Scintilla options, :165-208), `SshConfig`/`Credentials`/`Vault`/`Notifications`
(:102-117), `RecentFiles` + `MaxRecentFiles`=10 (:123-128), `LastAppShutdownUtc` (missed-run anchor for
SchedulingService, :134), scheduler caps `MaxConcurrentJobs`=3 / `DefaultMaxHistoryRuns`=50 /
`DefaultHistoryRetentionDays`=30 / `MaxJobOutputCharsPerHost`=1 MiB (:140-158).

`ApplicationState` (:348-395) is the compressed restore blob: host grid rows/columns/selection, last CSV path +
`CsvFileFingerprint`, selected preset/folder, username (no password), and a **legacy-only** `History` list kept
purely for one-shot migration (:391-394).

### 3. Named environments (`Services/EnvironmentService.cs`, `Models/EnvironmentConfig.cs`)

**What:** Named host-grid profiles (dev/staging/prod) with per-environment variables, Vault profile binding,
description, label color, CSV path + fingerprint. Stored inline in `config.json` under `Environments`.
**Reach:** Environment toolbar selector + Manage Environments dialog (`EnvironmentDialog`), wired in `Form1`
(`InitializeEnvironmentToolbar`, `EnvironmentService_EnvironmentChanged` at `Form1.cs:371`).

- **Model** (`EnvironmentConfig.cs`): `DefaultName = "Default"` reserved; deep `Clone()` (:48-69) and
  `Normalize()` (:71-89) re-wrap all dictionaries case-insensitively.
- **Legacy "Default" environment is synthetic** — `BuildLegacyDefaultEnvironment()` materializes it on demand
  from `SavedState` (`ConfigurationService.cs:202-206`, `EnvironmentConfig.FromApplicationState` :26-46). It is
  only persisted into the `Environments` map on "first adoption" events: first explicit save of a non-Default
  env (`EnvironmentService.cs:126-129`), create (:162-165), import (:338-341), or variable update while Default
  is active (:282-285).
- **Operations:** list (Default always injected first, :36-46), get/switch (clone-out semantics, switch persists
  active name then raises `EnvironmentChanged` only on a real transition, :74-98, :429-438), set base env
  (:100-112), snapshot current grid (`SaveCurrentGridToEnvironment` :114-149 — preserves existing
  description/color/Vault/variables via `BuildSnapshot` :358-389), create-with-copy (:151-185), delete (reserved
  Default protected; active/base fall back gracefully, :187-214), rename (active/base follow the rename,
  :216-255), per-env variable CRUD (:257-299), details update incl. Vault profile (:301-327), import with
  overwrite flag (:329-356).
- **State transfer through `ConfigurationService`:** `LoadEnvironmentState()` returns deep clones;
  `SaveEnvironmentState()` clones again and persists via `Update()` (`ConfigurationService.cs:174-197`,
  `CloneEnvironmentMap` :411-425) — every environment mutation is a full config rewrite.
- **Active grid duplication:** `Form1.SaveConfiguration` snapshots the live grid into both the active
  environment (`Form1.cs:14630-14633` → :2230-2240) and `config.SavedState` (`BuildApplicationState`,
  `Form1.cs:14679-14686`).

### 4. CSV host grid import/export (`Services/CsvManager.cs`)

**What:** Load/save of the host grid CSV.
**Reach:** File → Open CSV / recent-files menu (`Form1.OpenCsvFile` :9191-9243), Save / Save-As
(`Form1.SaveCsvToFile` :9310-9325), grid clear (:9327+).

- **Load** (`CsvManager.cs:18-68`): RFC-style record reader supporting quoted fields with embedded
  commas/newlines and escaped quotes (`ReadCsvRecord` :118-189); BOM-strip on first header (:32); **first
  column is always forcibly renamed `Host_IP`** (:34-35, `HostColumnName` const :11); blank later headers become
  `Column{i}`, spaces become `_` (:40-46); empty rows skipped; over-long rows truncated to header width (:61-62).
- **Save** (:76-103): plain `StreamWriter`, UTF-8 no BOM, quoting only when needed (`EscapeCsvValue` :248-260).
- A second, simpler line parser `ParseCsvLine` (:194-243) is public/static for other callers.
- **Change detection** lives in `Utilities/CsvFileSyncEvaluator.cs`: `CsvFileFingerprint` (mtime+size,
  `Models/CsvFileFingerprint.cs`) captured on load/save (`Form1.cs:9225, 9294`), evaluated per-environment to
  classify `NotTracked/Current/ChangedOnDisk/MissingOnDisk/Unknown` (:36-100); fallback full content
  comparison when an environment has no fingerprint (`SnapshotMatchesCsv` :114-147).
- **Recent files:** MRU list persisted in config (`Form1.AddToRecentFiles` :9106-9117, rebuild :9119,
  clear :9169), cap `MaxRecentFiles` (default 10).

### 5. Execution history persistence (`Services/HistoryStorageService.cs` + history models)

**What:** Per-run payload files plus a lightweight index, replacing legacy in-config history.
**Layout:** `history.index.json` + `history/{id}.json` next to `config.json` — base dir derived from the
**config file's directory** (`HistoryStorageService.cs:22-33`), so redirecting config in tests redirects history.
**Reach:** History list in main form. Writes from execution completion (`Form1.StoreFolderExecutionHistory`
:12979-13020 and the single-run analog at :14240); reads on selection (`Form1.TryLoadHistoryPayload`
:1201-1258); delete one/all from history context menu (:14355, :14381).

- **Shapes:** `HistoryIndexDocument{SchemaVersion=1, Entries[]}` / `HistoryIndexEntry{Id, Label, CreatedAtUtc,
  HasHostResults, HasDetails, RunFileName}` (`Models/HistoryIndex.cs`); `HistoryRunPayload{Id, Output,
  HostResults[], Details}` (`Models/HistoryRunPayload.cs`); `ExecutionDetails` captures preset/commands/
  environment/timeouts/run-mode/per-host variable context/interactive-terminal transcripts
  (`Models/ExecutionDetails.cs`).
- **SaveRun** (:439-476): normalizes entry (label fallback to id, UTC default), writes payload via
  `JsonFileWriter.WriteJsonAtomic` (temp + `File.Replace`), inserts newest-first into the index, enforces
  count-based retention (`EnforceRetention` :674-683 deletes evicted run files), atomically rewrites the index
  **with** `.bak`.
- **Three-tier lazy read** (:41-97): full deserialize; selective Newtonsoft streaming that skips `Details`
  and/or host `Output` bodies (`DeserializePayloadSelective` :285-359, `DeserializeHostResultsWithoutOutputs`
  :361-437); and an allocation-light `Utf8JsonReader` mode with bounded output + truncation marker
  (`DeserializePayloadLightweight` :99-142, `ReadBoundedStringValue` :223-236). `Form1` caches one hydrated
  payload and upgrades lightweight→detailed on demand (:1212-1245).
- **Index resilience:** corrupt index → timestamped `.corrupt.{utc}` backup + fresh empty index (:591-607,
  :736-737); normalization dedupes ids, back-fills labels, recovers `CreatedAtUtc` by parsing the first 19 chars
  of the label as `yyyy-MM-dd HH:mm:ss` local time (`ParseCreatedAtUtc` :618-633, `NormalizeIndexDocument`
  :635-672).
- **Delete:** `DeleteRun` removes the index row plus both the recorded filename and the default `{id}.json`
  (:478-508); `DeleteAll` also sweeps every `*.json` in the run folder, catching orphans (:510-528).
- **Legacy migration:** `ImportLegacyHistory` (:530-589) converts in-config `SavedState.History` entries to
  files (capped at maxEntries), prepends them to the index. Triggered once in
  `Form1.InitializeHistoryPersistence` (:1010-1027) when the index is empty but legacy entries exist; legacy
  list is cleared and config re-saved.
- **Ids:** `HistoryIdGenerator.NewId()` = `Guid.ToString("N")` (`HistoryIdGenerator.cs:8`).
- **`HistoryResultStore`** (`HistoryResultStore.cs`): in-memory id→host-results/details maps. **Not referenced
  by any production code** — only `SSH_Helper.Tests/Services/HistoryResultStoreTests.cs` (grep across repo).

### 6. Preset delete undo (`Services/PresetDeleteUndoService.cs`)

**What:** In-memory LIFO undo stack (max 50, :8-9) for preset/folder deletion. Each entry snapshots the entire
preset library (presets, folders, all 4 manual-order lists — `PresetLibrarySnapshot.Capture` :119-135) plus
JSON-deep-cloned affected job definitions (:61-74). `UndoLatest` restores via
`PresetManager.RestoreLibrarySnapshot` and `JobStorageService.RestoreSnapshots` (:41-59).
**Reach:** Edit-menu/toolbar Undo in `Form1` (`_presetDeleteUndoService` field at `Form1.cs:156`,
`RefreshPresetDeleteUndoUi` :401). Volatile: cleared on restart; no redo.

### 7. App update flow (`Services/UpdateService.cs`, `UpdateDialog.cs`)

**What:** GitHub-Releases-based self-update.
**Reach:** Help → Check for Updates (`Form1.cs:15041-15044`) and silent startup check gated on
`UpdateSettings.CheckOnStartup` + not-under-test-host (`Form1.cs:438-442`, test-host sniff :445-449).

- **Check** (`CheckForUpdatesAsync` :119-187): unauthenticated GET `releases/latest`, asset selection prefers an
  asset matching the running exe's filename (portable-build aware), then any `.exe`, then `.zip`
  (`SelectInstallAsset` :649-665); pairs it with a `.sha256[(.txt|sum…)]` sibling asset
  (`FindChecksumAsset` :627-647). Version comparison: numeric-segment semver with pre-release rules — release >
  pre-release, pre-releases compared **alphabetically** (`IsNewerVersion` :727-762).
- **Settings honored:** `GitHubOwner`/`GitHubRepo` (configurable! `AppConfiguration.cs:447-452`),
  `CheckOnStartup`, `LastCheckTime` (written at `Form1.cs:14983`), `SkippedVersion` (skip-this-version persists,
  `Form1.cs:14999-15008`), `EnableUpdateLog` (`SettingsDialog.cs:2679`).
- **Download** (:197-279): per-install temp dir `%TEMP%\SSH_Helper_Update_{exeName}_{8-char path-hash}`
  (`BuildUpdateTempDirectory` :427-463 — concurrent installs of distinct copies can't collide), streaming with
  progress events, 3 retries with 1/3/5s backoff, retryability classifier (:284-319).
- **Verification is mandatory:** `UpdateDialog.cs:542-555` aborts install if the release has no checksum asset;
  `VerifyUpdatePackageAsync` (:366-379) compares SHA256 against the checksum file (filename-matched line or
  first 64-hex token, `ExtractSha256` :667-696).
- **Install** (`LaunchUpdaterAndExit` :388-425): writes an embedded PowerShell script (:470-624) to temp, runs
  it hidden with `-ExecutionPolicy Bypass`, then `Environment.Exit(0)`. Script waits ≤60s for the PID to exit,
  `Expand-Archive -Force` for zips or retried `Copy-Item` over the running exe for single-exe, relaunches (with
  fallback exe discovery), self-deletes temp dir via delayed `cmd /c rd`.
- Errors get user-friendly translation (`GetUserFriendlyErrorMessage` :324-358); failures surface in
  `UpdateErrorDialog` / status bar (`Form1.cs:14985-14994`).

### 8. Shared persistence utilities

- **`JsonFileWriter`** (`Utilities/JsonFileWriter.cs`): `WriteJsonAtomic` = temp file + `File.Replace`
  (optional `.bak`), with copy/delete/move fallbacks (:21-88); `TryBackupCorrupt` with overwrite-or-timestamped
  modes (:103-124). Used by history index/payloads (and jobs subsystem) — **not** by `ConfigurationService` or
  `CsvManager`.
- **`GZipBase64Utility`**: SmallestSize GZip + Base64, prefix-stripping decompress.
- **`AppDataPaths`**: single source of truth for the storage root; portable-mode write-probe validation.

---

## Integration points

- **`EnvironmentService.EnvironmentChanged`** → `Form1.EnvironmentService_EnvironmentChanged` (`Form1.cs:371`)
  reloads the host grid; payload carries previous/current names + cloned `EnvironmentConfig`. Raised only on
  switch/delete/rename when the active environment actually changes (`EnvironmentService.cs:429-438`); **not**
  raised by `SaveCurrentGridToEnvironment` or variable updates.
- **`HistoryStorageService` ctor takes `ConfigurationService.ConfigFilePath`** (`Form1.cs:345`) — config-path
  redirection (tests, portable build) transparently relocates history.
- **Environment ↔ Vault:** `EnvironmentConfig.VaultProfileName` consumed by `JobExecutionService`
  (`Form1.cs:15068-15069`) and credential resolution.
- **Environment ↔ scripting:** YAML scripts may declare `environment:`; preset load auto-switches via
  `PresetEnvironmentLoadPlanner` against the environment list/base env (`Form1.cs:2165-2199`); per-folder
  `FolderInfo.BaseEnvironment` overrides feed `PresetBaseEnvironmentResolver`. The `update_environment` script
  command writes through `EnvironmentService.UpdateActiveEnvironmentVariable`.
- **`AppConfiguration.LastAppShutdownUtc`** (written `Form1.cs:3946` in `FormClosing`) anchors
  `SchedulingService.DetectMissedRuns` (read `Form1.cs:15265`).
- **Scheduler config knobs:** `MaxConcurrentJobs`, `DefaultMaxHistoryRuns`, `DefaultHistoryRetentionDays`,
  `MaxJobOutputCharsPerHost` consumed by `JobExecutionService`/`SchedulerHistoryPolicyResolver` (job history is
  a **separate** stream: `jobs.json` + `job-history/`, out of scope here).
- **`PresetDeleteUndoService` ↔ `PresetManager` + `JobStorageService`:** undo restores both the preset library
  and job definitions that referenced the deleted preset (`PresetDeleteUndoService.cs:41-59`).
- **History label ↔ scripting:** `set_history_label` command (see `SshExecutionServiceHistoryLabelTests`)
  customizes the entry label — which is also the string `ParseCreatedAtUtc` mines for a timestamp.
- **`ConfigLoadError`** → modal warning at startup (`Form1.cs:350-354`).
- **Flow Canvas display prefs** persist through `WindowState.FlowCanvas*` fields via `layout-save`/`pref-save`
  WebView2 messages (other subsystem writes, this subsystem stores).

Test coverage (maturity signal): dedicated suites for ConfigurationService (font/window-state/layout-mode/
command-editor/execution-details), EnvironmentService, CsvManager, CsvFileSyncEvaluator, HistoryStorageService,
HistoryIdGenerator, PresetDeleteUndoService, UpdateService temp paths, and history UI updaters. No tests found
for UpdateService version-comparison edge cases beyond what's in the file, none for CsvManager duplicate-header
or encoding behavior.

---

## Observed gaps & quirks

### Durability / atomicity
1. **`config.json` save is NOT atomic** — `ConfigurationService.cs:144` uses raw `File.WriteAllText` even though
   `JsonFileWriter.WriteJsonAtomic` exists and the project's own conventions mandate it. A crash/power-loss
   mid-write corrupts the live file; recovery then depends on the best-effort `.bak` copied *before* the write
   (:116-120) plus the `.corrupt` fallback path. config.json is the single highest-value file in the app
   (presets, environments, saved state all live in it). Known enhancement-audit theme; still unfixed.
2. **CSV save is not atomic and makes no backup** — `CsvManager.cs:78` writes the user's hosts file with a bare
   `StreamWriter`. A crash mid-save destroys the operator's source-of-truth host inventory.
3. **`Save()` mutates shared state non-atomically in memory** — `ConfigurationService.cs:122-142` temporarily
   nulls `config.SavedState` on the live cached object during serialization. Any concurrent reader (scheduler
   thread, async update-check calling `Update()`) can observe a half-mutated config; `Update()`/`Save()` have no
   locking at all despite being called from `Invoke`-marshalled UI code *and* background continuations.

### Config model
4. **Everything lives in one file** — environments (full host tables per environment), the entire preset
   library, and the compressed app state are all inside `config.json`. Every environment variable tweak
   (`EnvironmentService.cs:295-299`) re-serializes and rewrites the whole file including all environments'
   host lists; with many large environments this is an O(total-hosts) write per keystroke-level action, and one
   corruption event risks all of it.
5. **Active host grid has two sources of truth** — `SavedState.Hosts` and the active `EnvironmentConfig.Hosts`
   are both written on every save (`Form1.cs:14630-14633` + :14679-14686) and reconciled by Form1 convention
   only. The synthetic legacy `Default` environment (`ConfigurationService.cs:202-206`) is regenerated from
   `SavedState` until "first adoption", so e.g. `GetEnvironment("Default")` before adoption always returns an
   object with **empty `Variables`** — Default-environment variables conceptually don't exist until some other
   action materializes Default into the map (`EnvironmentService.cs:282-285`).
6. **No config schema version** — `AppConfiguration` has no version field (contrast:
   `HistoryIndexDocument.SchemaVersion`). All migrations are heuristic shape-sniffing
   (`ContainsLegacyPresetFormat`), which works but won't scale to a third format change.
7. **`MaxRecentFiles` and several other knobs are config-only** — present in the DTO
   (`AppConfiguration.cs:128`) but not exposed in `SettingsDialog` (grep shows no editor); same for
   `GitHubOwner`/`GitHubRepo` and `MaxJobOutputCharsPerHost`. Editable only by hand-editing JSON.
8. Debug leftovers: dead debug block with commented-out dialog in `Form1.SaveConfiguration`
   (`Form1.cs:14653-14658`).

### CSV handling
9. **First header cell is silently discarded** — whatever the user's first column is called, it is replaced by
   `Host_IP` (`CsvManager.cs:32-35`). A CSV whose first column is *not* the host address loads without any
   warning and every row targets the wrong "host".
10. **Duplicate header names crash the load** — `dt.Columns.Add(headerName)` (`CsvManager.cs:46`) throws
    `DuplicateNameException` for repeated column names (common in exported spreadsheets); user sees a raw
    "Failed to load CSV: A column named 'X' already belongs to this DataTable" (`Form1.cs:9241`).
11. **Silent data loss on ragged rows** — rows longer than the header are truncated without notice
    (`CsvManager.cs:61-62`).
12. **No encoding handling beyond BOM** — reads default UTF-8 detection only; ANSI/Windows-1252 CSVs (typical
    Excel export) load with mojibake. No delimiter option (semicolon-delimited locales unsupported).
13. Two parallel CSV parser implementations (`ReadCsvRecord` :118 vs `ParseCsvLine` :194) — the public one does
    not support embedded newlines; divergence risk for whoever calls it.
14. CSV fingerprint is mtime+size only (`CsvFileFingerprint`) — a same-second same-size edit is undetected
    (acceptable, but the fallback content compare only runs when fingerprint is absent,
    `CsvFileSyncEvaluator.cs:88-94`).

### Environments
15. **Environment rename does not cascade** — `RenameEnvironment` (`EnvironmentService.cs:216-255`) updates
    active/base pointers but nothing rewrites references held elsewhere: YAML scripts' `environment:` headers,
    `FolderInfo.BaseEnvironment` (`FolderInfo.cs:12`), or job definitions. Renaming an environment silently
    breaks preset auto-switch ("environment not found" status at `Form1.cs:2189-2192`) and folder base-env
    overrides.
16. **No environment export API in the service** — `ImportEnvironment` exists (:329-356) but export is left to
    the dialog layer; no file-format/versioning contract for shared environment files.
17. Every `EnvironmentService` getter call does a full `LoadEnvironmentState()` deep clone of *all*
    environments (e.g., `GetEnvironmentNames`, `GetActiveEnvironmentVariables`) — O(everything) allocations per
    UI query; fine today, scales poorly with big host inventories.

### History
18. **`HistoryResultStore` is dead production code** — referenced only by its own test file
    (`SSH_Helper.Tests/Services/HistoryResultStoreTests.cs`); the per-run-file design replaced it. Should be
    deleted (per repo's own "no ghosts" rule).
19. **Timestamp is mined from the display label** — `ParseCreatedAtUtc` (`HistoryStorageService.cs:618-633`)
    parses `label[0..19]` as `yyyy-MM-dd HH:mm:ss` **local** time. Entries whose label was customized via the
    `set_history_label` script command (and legacy imports with odd labels) get `CreatedAtUtc = DateTime.UtcNow`
    at normalization time — i.e., fabricated timestamps. Fragile label/metadata coupling.
20. **Retention is count-only** — `EnforceRetention` (:674-683) caps entries at `MaxHistoryEntries` (default 30)
    but there is no age- or size-based pruning, and run payloads include full transcripts + per-host outputs +
    interactive-terminal transcripts (`ExecutionDetails.InteractiveSessionDetails.Transcript`). 30 huge runs can
    occupy arbitrary disk. (Job history *does* have day-based retention; main history doesn't.)
21. **Orphaned run files after index corruption** — a corrupt `history.index.json` is backed up and replaced
    with an empty index (:591-607); the `history/` payload files remain on disk but are invisible and
    unrecoverable in the UI (no reconciliation scan; only `DeleteAll` ever sweeps the folder, :518-524).
22. **Silent failure modes** — `TryLoadRunPayload` swallows all exceptions into `return false` (:92-96), so
    a locked vs corrupt vs missing payload all read as the same generic "could not be loaded" dialog
    (`Form1.cs:1249-1254`). `TryDeleteFile` failures (file locked) are silently ignored (:720-734), leaking
    run files past retention.
23. No history search/filter/export and no cross-run comparison surface in the storage layer — for a multi-host
    operations tool, the only query is "load index, newest first".

### Update flow
24. **Update install bypasses shutdown persistence** — `LaunchUpdaterAndExit` calls `Environment.Exit(0)`
    (`UpdateService.cs:424`), which skips `Form1_FormClosing`. `ConfirmExitWorkflow` (invoked first via
    callback, `UpdateDialog.cs:563`, defined `Form1.cs:14891-14917`) only resolves dirty CSV/preset prompts —
    it does **not** call `SaveConfiguration` and does not stamp `LastAppShutdownUtc`. Installing an update
    therefore loses window/splitter state and grid changes since the last save, and leaves a stale missed-run
    anchor for the scheduler on relaunch.
25. **All `HttpRequestException`s are "retryable"** — `IsRetryableException` (`UpdateService.cs:300-304`)
    returns true for every HTTP error including 404/403, so permanent failures burn the full retry/backoff
    cycle.
26. **Alphabetic pre-release comparison** — `IsNewerVersion` (:757) makes `Beta9` > `Beta10` and only orders
    `RC > Beta > Alpha` by alphabetical accident; no numeric pre-release segment handling.
27. **Integrity ≠ authenticity** — SHA256 verification is mandatory (good, `UpdateDialog.cs:542-558`), but the
    checksum file ships in the same GitHub release as the binary; anyone who can tamper with the release assets
    can regenerate both. No Authenticode/signature check on the downloaded exe before it is copied over the
    installed binary by an `-ExecutionPolicy Bypass` PowerShell script.
28. **Zip installs have no rollback** — `Expand-Archive -Force` over the app dir (script line ~559); a
    half-failed extraction leaves a mixed-version install with no recovery path (single-exe path at least
    retries the copy and verifies the destination exists).
29. Unauthenticated GitHub API (60 req/hr/IP) — corporate NAT users may see rate-limit errors as
    "GitHub API returned 403"; no token support or `Retry-After` handling (`UpdateService.cs:128-141`).

### Undo
30. `PresetDeleteUndoService` is memory-only (lost on restart/crash), and `UndoLatest` restores a **whole-library
    snapshot** — any preset edits made *after* the delete but *before* the undo are silently reverted too
    (snapshot-restore, not inverse-operation). No redo. (`PresetDeleteUndoService.cs:41-59, 119-135`).
