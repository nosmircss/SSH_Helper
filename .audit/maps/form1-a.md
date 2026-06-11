# Feature map: Form1.cs — first half (lines 1–7700)

Scope: `Form1.cs` lines 1–7700 of 15360 (constructor/init, host grid, environments, theming/fonts, preset tree + favorites, command editor wiring, menus/context menus, Flow Canvas window plumbing, memory debugger). Control inventory cross-checked against `Form1.Designer.cs` (field declarations at Designer lines 1802–1987). The second half (execution pipeline, output panes internals, history persistence actions, scheduler, find) is mapped in `form1-b.md`.

Version constant: `ApplicationVersion = "0.51.23"` (Form1.cs:115).

---

## 1. Feature inventory

### 1.1 Startup & construction (Form1.cs:312–478)
- **Constructor** (312–412): enables dark-mode app model (`SetPreferredAppMode`, NativeMethods:39), builds all services (`ConfigurationService`, `HistoryStorageService`, `EnvironmentService`, `PresetManager`, `CsvManager`, `SshExecutionService` w/ pooling + `SshTimeoutOptions`, `SshConfigService`, `ExecutionCoordinator`, `UpdateService`), wires all 8 SSH-service events (363–370), then runs ~20 `Initialize*` methods. Config load errors surface as a warning dialog (350–354, `ConfigurationService.ConfigLoadError`).
- **Form1_Shown** (414–443): one-shot; arms history selection hydration on `Application.Idle` (`ArmHistorySelectionOnIdle` 1148 — deliberately ignores a carried-over launch click via `Control.MouseButtons` check at 1164), restores folder expand state (480), runs deferred column auto-size, defers scheduler bootstrap to idle (452–478), and kicks a silent update check gated on `UpdateSettings.CheckOnStartup` and not-running-under-testhost (445–450).
- **Window state restore** (`RestoreWindowState` 1625–1681): size/position clamped to the screen, maximized flag, and 5 splitter distances (main/top/command/output/history) restored on `Load` with each wrapped in `try { } catch { /* ignore */ }`.

### 1.2 Host grid (dgv_variables) — multi-host CSV grid
- **Initialization** (`InitializeDataGridView` 602–652): adds a checkbox "Select" column (`CreateSelectColumn` 586, name = empty string `SelectColumnName`) + enforced `Host_IP` column (`CsvManager.HostColumnName`); hardcoded modern styling (colors, `Segoe UI`, row height 28).
- **Custom scrollbars** (`SetupDataGridViewScrollbars` 775–836, `UpdateDataGridViewScrollbars` 890–966): native scrollbars are disabled and replaced with owned `VScrollBar`/`HScrollBar`/corner panel so dark mode can be themed; manual geometry math against `hostsPanel`; wheel handler scrolls by `SystemInformation.MouseWheelScrollLines` (864–888). Refreshes are batched through `HostGridRestoreBatcher` (UI/HostGridRestoreBatcher.cs; scopes at 988–996).
- **Multi-host selection**: header checkbox custom-painted (select-all, `Dgv_Variables_CellPainting` 4087–4129 + `_selectAllCheckboxBounds`), per-row dark-mode checkboxes custom-painted (4131–4165), single-click toggle (`Dgv_Variables_CellClick` 4274–4283), select-all/deselect-all/invert via grid context menu (6523–6546). Checked count drives Run button text and the Flow Canvas Host Bar (`UpdateSelectionCount` 2536 → `SendTargetHostToCanvas`).
- **Special columns**: `SpecialHostGridColumnTooltips` (134–141) marks `Host_IP`, `port`, `username`, `password`, `vault_path`; headers get an `*` marker custom-painted (4167–4205) and decorations via `ApplySpecialHostGridColumnDecorations` (9449, second half). `Host_IP` is protected from rename/delete (`IsProtectedColumn` 4050–4059).
- **Editing UX**: type-to-edit (`Dgv_Variables_KeyPress` 4408), Ctrl+A/Ctrl+C/Ctrl+V/Delete (`Dgv_Variables_KeyDown` 4422–4444 → Copy/Paste/DeleteSelectedCells in second half), double-click edit (4446), commit-on-cell-leave (4310), checkbox commit-on-dirty (4344), row-header click selects whole row (3954–3966), leaving the grid collapses selection to the Host_IP column (`SelectHostIpColumnOnly` 4233–4259).
- **Row header numbering**: custom-drawn row numbers (`Dgv_Variables_RowPostPaint` 4061–4085) with auto-grow-never-shrink width logic for >999 rows (`EnsureRowHeaderWidthFitsRowCount` 4371–4394).
- **Grid context menu** (`contextMenuStrip1`, Designer:1942–1952): Add/Rename/Delete column, Insert/Delete row, Select All/Deselect/Invert, plus dynamically injected Test Connection items; visibility per hit-zone managed in `HandleRightClick` (3981–4032) and `UpdateHostGridContextMenuSeparators` (4034).
- **Dirty tracking / file indicator**: `_csvDirty` + `_loadedFilePath`/`_loadedFileFingerprint`/`_loadedFileSnapshot`; hosts header shows file name + unsaved/sync status via `HostsFileIndicatorFormatter` (`UpdateHostsFileIndicator` 2370–2373, `IsHostsGridUnsaved` 2375–2393 does a full snapshot diff via `HostGridUtilities.SnapshotsMatch`). `ResolveLoadedFileSyncStatus` (2343) re-fingerprints the file on disk (`CsvFileSyncEvaluator`, Utilities/CsvFileSyncEvaluator.cs).
- **Connection-test visuals**: per-row state held in a `ConditionalWeakTable` (215), painted onto Host_IP cell + row header with light/dark palettes (`GetConnectionTestPalette` 2632–2660, `ApplyConnectionTestVisualState` 2687–2721), tooltip carries latency or error category (`ApplyConnectionTestCellResult` 2747–2772); progress run-id pattern guards stale updates (2598–2624). Test execution itself (`TestSelectedConnections` 8875) is in the second half.

### 1.3 Environments (toolbar selector + base-environment model)
- **Selector**: `tsbEnvironment` dropdown rebuilt by `RefreshEnvironmentSelector` (1791–1827); per-environment label colors tint menu items and the dropdown button with contrast-aware foreground (`ApplyEnvironmentMenuItemColor` 1864, `ApplyActiveEnvironmentLabelColor` 1873–1890). Window title shows active environment (`UpdateWindowTitle` 1892).
- **Base environment indicator**: `toolStripLabelBaseEnvironment` formatted by `BaseEnvironmentIndicatorFormatter` (1829–1835); folders can pin a base environment (per-folder `BaseEnvironment`, dialog at `ShowFolderBaseEnvironmentDialog` 7846–7895 using `ScriptChooseDialog` with an "(inherit)" sentinel `FolderBaseEnvironmentInheritChoiceValue` 121; resolution by `PresetBaseEnvironmentResolver`, Utilities/).
- **Switching** (`TrySwitchEnvironment` 1967–2018): commits in-flight cell edit, saves current grid into outgoing environment (`SaveCurrentGridToEnvironment` 2230), evaluates CSV sync (`ResolveEnvironmentCsvSyncBeforeSwitch` 2020–2088: NotTracked / Current / ChangedOnDisk with reload-from-disk Yes/No prompt / MissingOnDisk), loads target environment into grid, optionally updates the base environment. Status messages built by `BuildEnvironmentSwitchStatusMessage` (2137).
- **Preset-driven environment switching** (`ApplyPresetEnvironmentOnPresetLoad` 2165–2199): a YAML script's `environment:` field (`TryGetScriptDeclaredEnvironment` 2201) can auto-switch the active environment on preset load, or restore the (folder-resolved) base environment; planned by `PresetEnvironmentLoadPlanner` (Utilities/). Missing environments produce a status-bar message only.
- **Environment load into grid** (`LoadEnvironmentIntoGrid` 2242–2337): rebuilds columns/rows from `EnvironmentConfig`, restores checkbox selection indices, and — when Credential Manager is available — **moves any plaintext `password` cell values into Windows Credential Manager and blanks the cell** (2292–2303). First-adoption bootstrap creates "Default" (`EnsureDefaultEnvironmentForFirstAdoption` 2219).
- **Manage Environments**: `tsbManageEnvironments` → `EnvironmentDialog` (1935–1965); `EnvironmentChanged` event handler (1897–1912) re-syncs vault profile onto `_sshService`/`_jobExecutionService` and refreshes folder summary.

### 1.4 Credentials / Vault / Notifications bootstrap
- **Credential Manager** (`InitializeCredentials` 1358; `CredentialManagerProvider`): default password load/store/clear gated on `Credentials.UseCredentialManager` (`ShouldPersistMainFormPassword` 1370); per-host password store/resolve keyed by host+username (`StoreHostPassword`/`TryResolveHostPassword` 1482–1499); one-shot migration of grid plaintext passwords (`MigratePasswordsToCredentialManager` 1501–1523). Toolbar password box masked via `UseSystemPasswordChar` (`InitializePasswordMasking` 1599–1606); toolbar text mirrors into hidden `txtUsername`/`txtPassword` (`InitializeToolbarSync` 1576–1590).
- **Vault** (`InitializeVault` 1373–1425): builds `VaultService` only when `config.Vault.Enabled` and profiles exist; wires 5 credential-provider lambdas (token / approle secret / LDAP / userpass / token-saver) all backed by Credential Manager targets; injects into `_sshService.VaultService` and (if alive) `_jobExecutionService.VaultCredentialProvider`. Re-run after Settings OK (5747).
- **Notifications** (`InitializeNotifications` 1427–1452): always constructs `NotificationService` with webhook-URL and SMTP-password providers from Credential Manager; injected into SSH service and job execution service. Re-run after Settings OK (5748).

### 1.5 Command/script editor (txtCommand — Scintilla-based)
- **Wiring** (`InitializeScriptEditor` 654–662): autocomplete provider fed live host-grid column names (`GetEditorHostColumns` 670), YAML syntax highlighter, background validation service, and variable hover tooltips resolved against: built-ins (`TryGetBuiltInEditorVariable` 727–745: `_prompt`, `_timestamp`, `_iteration`, `_last_error`, `_output`, `_outputwindow`, `_host`, `_port`, `_username`, `_password`), parsed script `vars:`, active environment variables, and dynamic symbols (679–725). Column values previewed from the current/first grid row (`ResolveEditorColumnValue` 747, `GetSelectedHostPreviewRow` 759).
- **Editor settings**: `CommandEditorSettings` (20+ Scintilla options) applied via `ApplyCommandEditorSettings` (664) on startup and after Settings dialog.
- **Header row** (script panel): preset name box `txtPreset`, per-preset timeout `txtTimeoutHeader` (digits-only KeyPress filter 1583–1589; placeholder = global default timeout, 583/5726), Save button; dirty indicator re-renders title/save label via `PresetHeaderIndicatorFormatter` (`UpdatePresetHeaderIndicator` 2402–2417) on every text change (1570–1572). Caret position label `lblLinePosition` (`UpdateLinePosition` 4463–4470).
- **Context menu** (`contextCommandBox`, Designer:1867–1876): Cut/Copy/Paste/Select All, Comment/Uncomment selected lines (5827–5835 → Scintilla control methods), **Path Browser** (`ctxPathBrowser_Click` 5822 → `InsertSelectedFilePathAtCaret` 5837): inserts a picked file path at the caret with smart YAML single-quoting — detects adjacent/lone opening quotes and empty `""` placeholders, normalizes to `'…'` with `''` escaping (`TryBuildSingleQuotedPathInsertion` 5965–6015, `BuildSingleQuotedYamlScalar` 6031). Validate Script also on this menu.
- **Validate Script** (menu Edit + editor context; `validateScriptToolStripMenuItem_Click` 5775–5820): parses with `enforceCanonicalSyntax: true`, distinguishes success / warnings-only / errors, echoes results into the output pane AND shows a themed dialog.

### 1.6 Preset tree (Presets tab) & folders
- **Tree behavior**: custom owner-drawn nodes (`TreeView_DrawNode` 8644, second half boundary) for both trees; full-row click selection (`trvPresets_MouseDown` 4534–4558); expand/collapse only via the +/- glyph or double-click (Before-handlers cancel otherwise, 4560–4582; `_clickedOnPlusMinus` flag); expand state persisted per folder through `PresetManager.SetFolderExpanded` (4515–4529, with debug-mode verification message).
- **Selection→load**: `trvPresets_AfterSelect` (4487) → `TryApplySelectedPresetNode` → `LoadPresetIntoEditor` (2148–2163: fills editor, timeout, run-button text, pushes script into open Flow Canvas, applies preset environment). Unsaved-change interception lives in `TryResolvePendingPresetChanges` (12114, second half).
- **Drag & drop** (4615–4710): presets and folders reorder/move with Above/Inside/Below drop positions (`GetDropPosition` 4854 — 25%/75% bands for folders), cycle prevention (`CanDropAt` 4876–4918, `FolderPathUtility.IsDescendantOf`), drop-on-empty-space moves to root (`HandleDropOnEmptySpace` 4712). Manual ordering persisted in `config.ManualFolderOrder` / `ManualPresetOrderByFolder` with legacy root-order sync (`SyncLegacyRootPresetOrder` 5045); every reorder calls `ClearPresetDeleteUndoHistory`.
- **Sorting**: `ctxToggleSorting` cycles Ascending → Descending → Manual (6573–6598); indicator text on the context item (`UpdateSortModeIndicator` 6600).
- **Folder context features**: Add Folder (supports nested paths and subfolder creation, 8034–8078), Folder Base Environment dialog (7575/7846), Expand/Collapse All Subfolders with viewport anchoring via `PresetTreeViewportRestorer` (7592–7663), Move-to-Folder hierarchical submenu rebuilt on open (`BuildMoveToFolderSubmenu` 7915–8032) with incremental tree mutation fast-path (`TryReinsertExistingPresetNodeIncrementally`).
- **Context menu visibility matrix** (`ContextPresetLst_Opening` 7750–7844): tracks source tree (`_contextMenuSourceTreeView`); Favorites tab shows only Rename + Toggle Favorite; Presets tab toggles ~16 items based on preset/folder/subfolder selection.
- **Toolbar** (`presetsToolStrip`, Designer:1846–1853): Add/Delete/Rename/Duplicate preset, Add/Delete folder buttons (handlers 6548–6571, 8190+).

### 1.7 Favorites tab
- **Build** (`RefreshFavoritesList` 5158–5257): favorite folders + favorite presets gathered from `PresetManager`, ordered by persisted `config.ManualFavoriteOrder` with `folder:`/`preset:` key prefixes (5259–5279), unknown items appended alphabetically (`GetOrderedFavoriteItems` 5281–5324); empty-state label; folders auto-expanded; selection restored.
- **Reordering**: drag-drop reorders root-level favorites or presets within the same folder (5405–5473, `ReorderFavoriteItems` 5475); drop highlight uses hardcoded `Color.LightBlue` (5396). No drag-into-folder support here (by design — membership comes from the Presets tab).
- **Tab switching**: custom header strip (`presetsTabHeaderStrip`) two-way synced with hidden native tab control (5112–5156); switching tabs revalidates pending preset changes with rollback (`RestorePresetTabSelection`).

### 1.8 Preset search/filter
- `InitializePresetSearchFilter` (8967, body in second half) creates `_presetSearchPanel`/`_txtPresetSearch`/clear button + debounce timer (fields 218–221); filter predicates `PresetMatchesFilter` (9080) / `FolderHasMatchingPresets` (9087) are honored by `RefreshFavoritesList` in this half (5181–5183, 5218).

### 1.9 CSV file operations (entry points)
- Toolbar + File menu both route to: `OpenCsvFile` (9191), `SaveCurrentCsv`, `SaveCsvAs`, `ClearGrid` with confirm (5601–5607), guarded by `EnsureCsvChangesSaved` (5590–5594, 5691–5705). Recent-files menu is injected at runtime (`_recentFilesMenuItem` 206, `RebuildRecentFilesMenu` 9119 — second half).

### 1.10 Execution start/stop UX (dispatch side)
- **Run button** (`btnExecuteSelected_Click` 5614–5680): dispatch matrix — folder selected (from active tab tree or `_selectedFolderName` fallback because TreeView selection can drop on button click, comment at 5619) × checked-hosts vs selected-host → `ExecuteFolderPresetsOnCheckedHosts` / `ExecuteFolderPresetsOnSelectedHost` / `ExecuteOnCheckedHosts` / `ExecuteOnSelectedHost` (all in second half). Every branch logs through `SshDebugLog` (2816–2824, gated on the Edit▸Debug Mode check item; also sets `_sshService.DebugMode` at 6458–6462).
- **Stop**: `btnStopAll_Click` 5682 → `StopExecution` (13282, second half). Stop-button width constant `StopButtonHorizontalPadding` (273).
- **Status bar**: `UpdateStatusBar` (2587–2596) message + optional progress bar; connection-test progress (2598–2624) and manual-execution progress (`BeginManualExecutionProgress`/`UpdateManualExecutionProgress` 2774–2811 via `ManualExecutionStatusProgress`, Utilities/) both use incrementing run-ids to discard stale callbacks.

### 1.11 Output & history panes (first-half portions)
- **History list binding** (`InitializeOutputHistory` 1003): `lstOutput` bound to `BindingList<HistoryListItem>`; owner-drawn items.
- **History persistence bootstrap** (`InitializeHistoryPersistence` 1010–1027): loads on-disk index via `HistoryStorageService.LoadIndex()`; one-time migration of legacy `SavedState.History` into per-run files (`ImportLegacyHistory`) then clears the config copy.
- **Index→list loading** (`LoadHistoryIndexIntoList` 1029–1083, `InsertHistoryEntryIntoList` 1085–1124 via `HistoryListCollectionUpdater`): suppressed-selection batched updates, newest-first insertion with `MaxHistoryEntries` eviction (evicted ids release cached payloads).
- **Payload lazy-loading + memory management**: single cached payload (`TryLoadHistoryPayload` 1201–1258) with on-demand re-load when details/host-outputs are required; size estimation (`EstimatePayloadTextChars` 1260–1303); automatic LOH compaction when swapping a ≥10M-char payload for a ≤500K one (`MaybeCompactAfterPayloadSwap`, thresholds at 124–125, 2s cooldown 131, `RunGcCompaction` 1337 forces `CompactOnce` + double GC).
- **Startup selection hydration**: deferred arming on idle prevents a launch click from hydrating history before the UI settles (1148–1199, `HistoryStartupSelectionHydration` helper).

### 1.12 Theming & fonts
- **Theme engine**: `ApplyTheme` (2860–2891) → `ApplyLightTheme` (3336–3478) / `ApplyDarkTheme` (3480–3621); two parallel hand-maintained palettes (constants 2829–2855); DWM immersive dark title bar (3340/3484); per-control colorization of ~50 controls each; toolstrip renderers (`ModernToolStripRenderer`/`DarkToolStripRenderer`); native dark scrollbars via undocumented uxtheme ordinals #133/#135/#136 (NativeMethods 38–45) applied per-handle including child windows (`ApplyScrollbarThemeToHandle` 3707–3730); owner-drawn dark TabControl (3735–3907); connection-test visuals re-applied on theme change (2888).
- **Font engine**: `ApplyFontSettings` (2909–3081) builds ~10 fonts from `FontSettings` (UI family, code family, global scale, ~12 per-area sizes, tree row height, host row height) and reflows menu/toolbar/header geometry (`ReflowTopBarsForCurrentFont` 3083, `ReflowMainChromeBounds` 3123, `ReflowScriptHeader` 3195); deferred old-font disposal with GDI+ shared-handle guard (3056–3080); GDI+ `Font.Height` exception fallback (2949–2959); accent color applied to the Run button (`ApplyAccentColor` 3316–3328); script-prompt font size published to `ScriptPromptDialogRunner.DefaultPromptFontSize` (3048).
- `SafeDrawString` (3916–3926) swallows `ArgumentException` from disposed fonts during transitions.

### 1.13 Menus (full inventory, Designer:1911–1939 + runtime injections)
- **File**: Open CSV, Save, Save As, Export/Import All Presets, Environments, Settings, Exit. Recent Files injected at runtime.
- **Edit**: Undo Delete, Find (Ctrl+F via `ProcessCmdKey` 14450), Validate Script, Memory Debugger (DEBUG build only), View All Popups (DEBUG: popup-gallery walkthrough 6061–6175), Debug Mode (checkable).
- **Flow Canvas**: injected at runtime before Help (`InitializeFlowCanvasMenuItem` 6605–6619), shortcut Ctrl+Shift+F.
- **Help**: Documentation (opens GitHub URL 6465), Scripting Documentation (SCRIPTING.md URL 6478), Check for Updates, About (`AboutDialog` 6491).
- **Settings dialog handler** (5714–5758): applies theme/fonts/editor settings/column auto-resize/pooling/SSH-agent preference; migrates default password to/from Credential Manager on toggle (5735–5745); re-inits Vault + Notifications; syncs cleared preset timeouts into the editor field (5753–5757).

### 1.14 Flow Canvas host window plumbing (C# side of the bridge)
- **Open/reuse** (`OpenFlowCanvas` 6645–6829): single window reuse with reload+activate; closes an orphaned Run Output window when opening a fresh canvas (6660–6663); wires 12 callbacks: `OnApplyYaml`, `OnExecuteCanvas` (mode `run`/`test-step`), `OnRunRequest` (deprecated → clicks Run button), `OnTestDataBlock`, `OnTestStep` (deprecated), `OnDebugAction` (continue/step/step-into-alias/stop → `ActiveScriptContext.DebugState`), `OnBreakpointToggle` + `OnDisableBlock` (maintain `_pendingBreakpoints`/`_pendingDisabledBlocks` sets 182–184 and mirror into live `DebugState`), `OnLayoutAutosave`, `OnSetLayoutMode`, `OnBrowsePath`, `OnOpenRunOutputWindow`/`OnCloseRunOutputWindow`. Seeds the Run Output tab from the buffered output snapshot (6826–6828).
- **Run Output window** (`OpenRunOutputWindow` 6621–6643): detachable `RunOutputWindowForm` (second WebView2); on close notifies the canvas to dock back (`run-output-window-closed`).
- **Script→graph load** (`LoadCurrentScriptIntoCanvas` 6831–6890): empty/non-YAML → empty graph (canvas creates Start block); else `FlowCanvasBridge.TextToGraph`; layout-mode resolution: per-preset `LayoutMode` else `WindowState.FlowCanvasDefaultLayoutMode` else AutoFlow; Manual mode preserves positions via structure-hash match (`MergeLayout`) or prefix-safe tuple merge (`TryMergeLayoutByTuple`) with new-node id reporting; any exception is swallowed (6886–6889).
- **Graph→YAML apply** (`ApplyFlowCanvasGraph` 6914–7026): honors `graphChanged=false` to skip re-serialization and preserve user formatting (6932/6966); rebuilds the 4 node↔stepPath/stepIndex maps (6971–6974); validates a selected test step is executable (6976–6987); persists canvas layout onto the preset (`FlowCanvasBridge.ExtractLayout` 6999); result + diagnostics posted back as `apply-result` (`SendFlowCanvasApplyResult` 7028–7053).
- **Layout autosave** (`ApplyLayoutAutosave` 2419–2502): positions (with stepPath/blockType tuples), comments (id/text/color/geometry/anchor), disabled blocks, expanded nodes → `PresetManager.UpdateCanvasLayout`; computes a structure hash on first save. `ApplySetLayoutMode` (2504–2512) persists auto/manual per preset.
- **Test Step** (`ExecuteCanvasTestStep` 7055–7160): resolves node→stepPath, truncates the YAML to steps 0..N (`TryBuildYamlThroughTopLevelStep` 7356–7394 + hand-rolled splitter `SplitTopLevelYamlSteps` 7458–7531), scopes the node maps to the truncated script (7123–7129), computes allowed prerequisite roots inside containers (`BuildTestStepAllowedRoots` 7396–7442 — understands `elif`/`cases`/`parallel` index segments) and disables out-of-scope nodes by merging into `_pendingDisabledBlocks` (7132–7142); runs on a single resolved host row with host validation (`InputValidator.IsValidHostOrIp` 7110); anchors script prompts to the canvas window (`ScriptPromptDialogRunner.AnchorFormOverride` 7145/7158).
- **Test Data Block** (`ExecuteCanvasTestDataBlock` 7166–7354): SSH-free single-command execution for `extract`/`parse`/`set`/`table`/`assert` against a variable snapshot; builds a throwaway `ScriptContext` (DebugMode on, Vault/Notification services injected), special-cases `_output`/`_outputwindow` runtime state (7206–7213), reports output, full variable map, and changed keys back as `test-data-block-result`.
- **Browse path bridge** (`HandleFlowCanvasBrowsePathRequest` 6892–6912): requestId-correlated OpenFileDialog owned by the canvas window; initial directory derived from `currentPath` (5888–5919).

### 1.15 Memory debugger (DEBUG builds only, 6049–6456)
- `MemoryDebuggerDialog` fed by `CaptureMemoryDebuggerSnapshot` (6177–6348): working set/private/managed-heap numbers plus an itemized estimate of every large text bucket (history labels, cached payload, presets, grid cells, editor, output buffer) and on-disk config/history sizes. `AggressiveTrimMemoryNow` (6377–6431) force-recreates the output textbox, clears the cached payload, LOH-compacts, and calls `EmptyWorkingSet`.

---

## 2. Integration points

| Connection | Mechanism | Where |
|---|---|---|
| SSH execution → UI | `OutputReceived`, `CommandCompleted`, `ExecutionCompleted`, `ColumnUpdateRequested`, `EnvironmentVariableUpdateRequested`, `StepStarting`, `StepCompleted`, `DebugPauseStateChanged` on `SshExecutionService` | wired 363–370; handlers in second half (13583+) |
| UI output throttling | `OutputThrottler` (50 ms, `UiOutputThrottleMs` 118) marshals appends to UI context | 331–332 |
| Environments | `EnvironmentService.EnvironmentChanged` → vault profile re-sync into `_sshService` + `_jobExecutionService`, label colors, folder summary | 1897–1912 |
| Config | `ConfigurationService` is the single source for window state, fonts, theme, sort orders, manual orders, favorites order, recent files; many handlers do `_configService.Load()` → mutate → `Save()` (e.g. 4946, 4954, 5477) | throughout |
| Presets | `PresetManager` (folders, favorites, expand state, canvas layout/layout-mode persistence via `UpdateCanvasLayout`/`UpdateLayoutMode`) | 2501, 2509, 7000 |
| History | `HistoryStorageService` (index + per-run payload files; legacy migration); base dir derives from config path | 1010–1027, 1232 |
| Flow Canvas (React) | `FlowCanvasForm.SendMessage`/typed `On*` events; messages in this half: `apply-result`, `test-step-result`, `test-data-block-result`, `browse-path-result`, `run-output-window-closed` | 6645–7354 |
| Flow Canvas debug | `_pendingBreakpoints`/`_pendingDisabledBlocks` staged pre-run, mirrored into `_sshService.ActiveScriptContext.DebugState`; node↔stepPath maps are the correlation contract | 6756–6789, 6971–6974 |
| Scripting engine | `ScriptParser` (validation, environment declaration, IsYamlScript), direct command instantiation for data-block tests (`ExtractCommand`, `ParseCommand`, `SetCommand`, `TableCommand`, `AssertCommand`), `ScriptPromptDialogRunner` anchor/font statics | 5775+, 7229–7297, 3048, 7145 |
| Vault / Notifications | `VaultService` + `NotificationService` constructed here and injected into SSH + job services; all secrets resolved via `ICredentialProvider` lambdas | 1373–1452 |
| Scheduler | services fields (159–169) initialized via deferred idle bootstrap (452–478); `SchedulerInstanceLock` single-instance text (130); details in second half |
| Dialogs | `DialogTheme` for all themed message boxes/dialog fonts; `SettingsDialog`, `EnvironmentDialog`, `AboutDialog`, `MemoryDebuggerDialog`, `ScriptChooseDialog`, `UnsavedPresetDiffDialog` | throughout |
| Test seams | `_inputBoxPromptOverrideForTests`, `_dialogPromptOverrideForTests`, `_filePathPickerOverrideForTests`, `_saveFilePathPickerOverrideForTests` (243–246); `IsRunningUnderTestHost` (445) | — |
| Utilities | `CsvFileSyncEvaluator`, `HostGridUtilities`, `HostsFileIndicatorFormatter`, `PresetHeaderIndicatorFormatter`, `BaseEnvironmentIndicatorFormatter`, `PresetBaseEnvironmentResolver`, `PresetEnvironmentLoadPlanner`, `ManualExecutionStatusProgress`, `FolderPathUtility` (all in `Utilities/`); `HostGridRestoreBatcher`, `PresetTreeViewportRestorer`, `HistoryListCollectionUpdater` (in `UI/`) | — |

---

## 3. Observed gaps & quirks

### Dead / vestigial code
- **`TrimMemoryPressureNow` is a no-op shell** (Form1.cs:6350–6375): `removedChars` and `trimmedOutputBuffer` are hardcoded 0; it only forces a GC and prints "Estimated reduction: 0 B". Either trimming logic was removed and the reporting scaffold left behind, or it was never implemented.
- **`CanDropOn` (Form1.cs:4823–4852) is dead** — only its definition exists; `trvPresets_DragOver` uses `CanDropAt` and favorites use `CanDropOnFavorites`. Drift risk if someone "fixes" the wrong predicate.
- `trvFavorites_NodeMouseDoubleClick` (5358–5365) only writes a status-bar message; the load already happened on select — the "confirms action" comment papers over a no-op.

### Inconsistencies
- **Hardcoded `"Host_IP"` string** instead of `CsvManager.HostColumnName` at Form1.cs:2664 (`GetHostIpColumnIndex`) and 4329 (`Dgv_Variables_CellValueChanged`). Works only because the constant equals the literal.
- **Two input-box pathways**: most prompts go through `ShowInputBox` (11304, test-overridable, themed) but `ctxRenameFolder_Click` (8093, just past this half's boundary) calls `Microsoft.VisualBasic.Interaction.InputBox` directly — untestable and unthemed; check the second half for more direct calls.
- **Theme duplication**: Form1 maintains its own light/dark palettes (2829–2855) parallel to `DialogTheme` (only `GridLightSelection`/`GridDarkSelection` are shared). Any palette change must be made in 2+ places; project guideline says `DialogTheme` is the single driver.
- Favorites drag-highlight uses hardcoded `Color.LightBlue` (5396) — not theme-aware; invisible-ish contrast in light mode, jarring in dark mode.
- `RefreshFavoritesList` ignores `ManualPresetOrderByFolder` interplay subtleties: presets inside a favorite folder use the *folder's* sorted order (5215) while loose favorite presets use `ManualFavoriteOrder` — two ordering systems visible in one tree.

### Error-handling weak spots
- Splitter restore swallows all exceptions five times (`catch { /* Ignore invalid splitter distances */ }` 1654–1675).
- `LoadCurrentScriptIntoCanvas` swallows every exception with "canvas will show empty state" (6886–6889) — a parse/bridge bug silently presents as an empty canvas with no diagnostics.
- `documentationToolStripMenuItem_Click`/`scriptingDocumentation…` use bare `catch { }` (6475, 6488) — a missing browser silently does nothing.
- `GetEnvironmentLabelColor` catches everything and returns null (1852–1862).
- Memory snapshot file-size block resets all four metrics on any IO error without telling the user (6304–6311).

### Security-adjacent
- **Environment load silently rewrites data**: when Credential Manager is available, `LoadEnvironmentIntoGrid` moves `password` cell values into the credential store and blanks the cell (2292–2303) with no user notification; if the user later saves the CSV the passwords are gone from the file (may be intended, but it's invisible). `MigratePasswordsToCredentialManager` (1501) does the same but it is never called from this half — verify the call site exists (possible dead path).
- The toolbar password syncs into a hidden plain `txtPassword` TextBox (1580) — not masked (it's invisible, but it's an extra plaintext copy).
- Documentation/Help URLs are hardcoded to `github.com/nosmircss/SSH_Helper` (6471, 6484) while `UpdateService` reads owner/repo from `UpdateSettings` (374–377) — fork/rename drift.

### Functional gaps a multi-host SSH tool user would expect
- **Validate Script gives zero feedback when it does nothing**: with a folder selected or a non-YAML (simple) preset, `validateScriptToolStripMenuItem_Click` returns silently (5777–5783). User clicks Validate, nothing happens.
- **Host grid has no column sorting at all**: every column is forced `NotSortable` (4306) and header-click selects the column instead (4264–4272). For large fleets, no way to sort by hostname/any variable.
- **No grid filtering/search for hosts** — preset tree has a search box, the host grid does not; with hundreds of rows the only navigation is scroll.
- **Custom scrollbar replacement** (775–966) re-implements scroll math by hand; horizontal `Maximum` math (950–953) is approximation-based; row-granular vertical scroll only; smooth scrolling/pixel scrolling lost. Any DataGridView feature interacting with native scrolling (e.g., `EnsureVisible`-style APIs) bypasses the custom bars until next event.
- **Select-all checkbox state can desync**: `_selectAllChecked` is only reset by invert (6543) and header toggles; manually unchecking every row leaves the header checkbox drawn checked until next header click.
- **Environment switch data-loss edge**: `TrySwitchEnvironment` always saves the current grid into the *outgoing* environment (1977) — there is no way to discard accidental grid edits before switching; combined with silent password-blanking above, environment hopping mutates state aggressively.
- **Folder rename/delete clear undo history broadly**: nearly every structural action calls `ClearPresetDeleteUndoHistory` (4722, 4949, 5005, 5042, 5513, 6597, 7904, 8076…), so the advertised delete-undo is easily wiped by an unrelated reorder.
- `btnExecuteSelected_Click` fallback `_selectedFolderName` (5631/5643) can dispatch a **folder** run after the user clicked a preset if selection events didn't update the tracker — the comment at 5619 acknowledges TreeView selection unreliability; the workaround makes run-target determination implicit state.

### Flow Canvas seams (verify against second half / React side)
- **Test-step disable leakage risk**: `ExecuteCanvasTestStep` merges its temporary out-of-scope disables into `_pendingDisabledBlocks` (7137–7142). Restoration depends on `PrepareFlowCanvasExecutionStateForRunStart`/`CleanupFlowCanvasExecutionStateAfterRun` (13811/13826, second half — 13823 clears the set). If cleanup clears the whole set, the user's own pre-set disabled blocks are also lost after any run; if it doesn't run on a failed launch path (e.g., host-validation early returns at 7096–7120 happen *before* the disable merge — OK — but failures after 7142 inside `ExecutePresetOnRowsAsync` rely on the finally in second half). Cross-check needed.
- `SplitTopLevelYamlSteps` (7458) is a hand-rolled YAML splitter keyed on literal `steps:` at column 0 and `- ` indentation; flow-style or unusual-but-valid YAML (e.g., `steps:` with trailing comment, document markers) will break test-step truncation. `TryBuildYamlThroughTopLevelStep` appends `steps:` if absent from the preamble (7386) — duct tape over the splitter's assumptions.
- `ApplyLayoutAutosave` default comment color hardcoded `#e0c040` (2458) — should live with the React token layer per project conventions.
- `OnRunRequest` legacy path performs `btnExecuteSelected.PerformClick()` (6701) — meaning a deprecated canvas message can trigger a *folder* execution depending on tree state, not necessarily the canvas script.

### Performance / scale notes
- `IsHostsGridUnsaved` builds a full grid snapshot and deep-compares on every hosts-header refresh (2392), which is called from `UpdateHostCount`/`UpdateSelectionCount` — i.e., on every cell change and checkbox toggle. O(rows×cols) per keystroke on large grids.
- Several drag/reorder paths call `_configService.Load()` (full config reload from disk) instead of `GetCurrent()` (4946, 4954, 4973, 5010, 5085, 5197, 5477) — repeated disk IO during a single drag session, and a potential lost-update hazard against in-memory state.
- `ApplyFontSettings` iterates every grid row to set Height (3006–3009) and rebuilds ~10 fonts; fine at startup, noticeable when toggling settings on big grids.
- GC behavior is managed manually in 4 places (`RunGcCompaction` 1337, `TrimMemoryPressureNow`, `AggressiveTrimMemoryNow`, history-swap compaction) — symptomatic of large-string churn in the history/output design.

### Minor
- `FolderSummarySubSeparator` is 9 `=` chars vs the 60-char main separator (132–133) — looks like a typo'd width.
- `RestoreFolderExpandState` only iterates **root-level** nodes (483) — nested folder expand state restoration depends on `RefreshPresetList`'s own restore (second half); the Shown-time pass won't fix nested folders if that path missed them.
- `_lastPresetsTabIndex`/`RestorePresetTabSelection` dance (5120–5156) exists because the tab control + custom header strip are doubly-synced; fragile two-way binding.
- DEBUG-only popup gallery and memory debugger menu items exist in the Designer unconditionally (Designer:1929–1930) but handlers are `#if DEBUG` (6049) — verify the items are hidden in Release (`Form1MenuInitializationTests` likely covers this).
