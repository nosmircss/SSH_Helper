# Subsystem map: Shared UI controls & utilities

Scope: `UI/` excluding ScintillaScriptEditorControl, FlowCanvasForm, RunOutputWindowForm,
InteractiveTerminalViewportControl, CronBuilderControl (covered elsewhere), plus all of `Utilities/` (30 files).
All paths relative to repo root `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper`.

Maturity snapshot: this is the most test-covered, most "library-grade" layer of the app. Nearly every pure-logic
helper has a dedicated xUnit test file (`SSH_Helper.Tests/UI/*`, `SSH_Helper.Tests/Utilities/*` — ~40 matching test
files). The weak spots are concentrated in DialogTheme edge cases, clipboard/paste semantics, and silent-failure
error handling in file/parsing utilities.

---

## Feature inventory

### 1. Theming engine — `UI/DialogTheme.cs` (834 lines, internal static)

The single static class driving dark/light theme app-wide (per Development Guideline 9). Reached implicitly: every
dialog constructor calls into it.

| Capability | Location | Notes |
|---|---|---|
| Palette constants | `DialogTheme.cs:10-32` | Light + VS Code-inspired dark colors, plus grid-specific colors. |
| `ApplyTo(root, darkMode)` recursive control theming | `:37-192` | Type-switch over Form/Tab/TextBox/RichTextBox/GroupBox/CheckBox/Radio/NumericUpDown/ComboBox/ListBox/TrackBar/LinkLabel/Panel/Label. Buttons intentionally skipped (styled via `StyleButton`). |
| Dark GroupBox owner-draw | `:92-120` | Visual styles ignore ForeColor, so dark mode attaches a `Paint` handler drawing border + caption manually. |
| `StyleButton(btn, dark, isPrimary)` | `:197-225` | Accent primary vs surface secondary; flat style + hover/down colors in dark. |
| `StyleDataGridView(grid, dark, flattenHeaderBevel)` | `:230-277` | Keeps dialog tables consistent with main hosts grid. |
| `SetDarkTitleBar(form, dark)` | `:282-297` | `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)`; swallows failure on old Windows. |
| `ApplyNativeTheme(root, dark)` | `:303-356` | Dark scrollbars/native sub-windows via undocumented uxtheme ordinals (`AllowDarkModeForWindow` #133, `SetPreferredAppMode` #135) + `SetWindowTheme("DarkMode_Explorer")` + `EnumChildWindows` + `WM_THEMECHANGED` + `SWP_FRAMECHANGED`. Forces handle creation on every control (`:307-308`) so hidden tab pages get themed. |
| `StyleTabControl(tab, dark)` | `:362-419` | Owner-draw dark tab headers; special-cases `BorderlessTabControl` (defined `Form1.Designer.cs:2259`) vs generic Paint-overlay fallback. Properly unsubscribes DrawItem/Paint before re-subscribing. |
| `SetDialogFont(form, font)` | `:522-529` | Sets font with `AutoScaleMode.None` to avoid auto-scale relayout breaking absolute-pixel dialog layouts. |
| `Confirm(owner, msg, title, dark, font)` | `:535-586` | Themed Yes/No; **No is both AcceptButton and primary-styled** (`:577,582`) — safe default. |
| `ShowMessage` / `Show` x5 overloads | `:592-629` | Full themed `MessageBox.Show` replacement: builds button rows for OK/OKCancel/YesNo/YesNoCancel/RetryCancel/AbortRetryIgnore (`GetButtonSpecs :752`), auto-sizing message label (max width 560), system icons resized to 32px (`GetSystemIcon :799`). |
| Dark-mode auto-detection | `ResolveDarkMode :766-781` | Heuristic: owner form `BackColor.GetBrightness() < 0.2f`, falls back to `Application.OpenForms[0]`. |
| Font auto-detection | `ResolveDialogFont :783-797` | Owner control font, else first open form's font. |

Coverage status: raw `MessageBox.Show` is nearly eradicated — only `Program.cs:50` (pre-UI startup storage error)
and `SettingsDialog.cs:1069,1073` (Vault "Test Connection" result popups) remain. 147 `DialogTheme.Show/Confirm/ShowMessage`
call sites across 13 files.

Dependency: P/Invoke surface lives in `internal static class NativeMethods` in **`Form1.cs:27-77`** (uxtheme/dwmapi/
user32/psapi), not in a Utilities file — DialogTheme is coupled to Form1.cs for its native layer.

### 2. Repaint-safe container controls

- **`UI/BufferedPanel.cs` (55 lines)** + **`UI/BufferedSplitContainer.cs` (46 lines)** — double-buffered subclasses
  that additionally swallow `WM_ERASEBKGND` (`:22-28` in both) when safe, gated by `CanSkipBackgroundErase()` which
  bails if BackgroundImage set or any (direct-child / panel) BackColor has alpha < 255. Project convention: use these
  instead of stock Panel/SplitContainer on repaint-heavy surfaces. Namespace quirk: both live in `SSH_Helper` root
  namespace, not `SSH_Helper.UI`. Tested in `SSH_Helper.Tests/UI/BufferedContainerControlTests.cs` and
  `Form1BufferedSurfacesTests.cs`.
- **`UI/FlatVisualButton.cs` (139 lines)** — fully owner-painted flat button (`OnPaint :86-118`) with manual
  hover/pressed state tracking; honors `FlatAppearance.MouseOverBackColor/MouseDownBackColor/Border*`. Clears to
  `Parent?.BackColor` (`:89`) so it blends with themed parents. No focus rectangle / keyboard-cue rendering.

### 3. Preset tab header — `UI/PresetTabHeaderStrip.cs` (182 lines)

Owner-drawn 2-tab strip ("Presets" / "Favorites", hardcoded `:11`) replacing a native TabControl header: hover
tracking, accent underline on selected tab, `SelectedIndexChanged` event with clamped `SelectedIndex` setter
(`:41-56`). Constructor defaults are dark-palette (`:30-36`); Form1 re-skins it for light (`Form1.cs:3388-3394`) and
dark (`Form1.cs:3532-3538`) using **Form1's own color constants, not DialogTheme's** — two parallel palettes.
Two-way index sync with the real `presetsTabControl` at `Form1.cs:5114-5124`.

### 4. History list rendering stack

- **`UI/HistoryListBox.cs` (59 lines)** — `ListBox` subclass for `OwnerDrawVariable` mode; re-measures item heights
  only when client width actually changes (`_lastMeasuredClientWidth` guard `:21-26`) or on font change; wraps
  `RefreshItems` in `BeginUpdate/EndUpdate`.
- **`UI/HistoryListLayout.cs` (96 lines)** — pure measurement math: 4px padding, wrap to max 3 visible lines
  (`MaxVisibleLines :10`), `CalculateItemHeight :32`, text bounds helper. `GetLineHeight :64-78` catches
  `ArgumentException` from the GDI+ shared-font-handle disposal gotcha and falls back to `font.Size * 1.6`.
- **`UI/HistoryListCollectionUpdater.cs` (61 lines)** — pure list mutation: `InsertNewest :19` dedupes by id, inserts
  at top, trims to `maxEntries` returning removed ids (mirrors retention into both the item list and the index map).
- **`UI/HistoryStartupSelectionHydration.cs` (9 lines)** — one boolean rule: hydrate the selected history entry at
  startup only if selection-handling isn't enabled yet and a row is already selected (used `Form1.cs:1170`).

### 5. Preset tree pure helpers (all consumed by Form1's preset TreeView)

- **`UI/PresetTreeViewportRestorer.cs` (119 lines)** — captures `TopNode` as a `PresetNodeTag` (type defined
  `Form1.cs:82`) and restores scroll position after rebuilds; walks ancestors when the preferred node can't be top
  (`:77-96`); `TrySetTopNode` swallows Argument/InvalidOperation exceptions (`:105-117`).
- **`UI/PresetTreeDeleteMutation.cs` (43 lines)** — delete + select-replacement + viewport restore inside one
  `BeginUpdate/EndUpdate` (`:11-31`).
- **`UI/PresetTreeDisplayOrderBuilder.cs` (35 lines)** — flattens *visible* (expanded) nodes into display order.
- **`UI/PresetTreeSelectionGuard.cs` (27 lines)** — "can select without EnsureVisible" = all ancestors expanded
  (prevents selection-triggered auto-scroll).
- **`UI/PresetDeletionSelectionResolver.cs` (34 lines)** — picks the adjacent visible preset (prev, else next) to
  select after deletion; folders excluded.

Each has a matching test file in `SSH_Helper.Tests/UI/`.

### 6. Host grid support

- **`Utilities/HostGridUtilities.cs` (384 lines)** — the host grid's logic core:
  - `BuildSnapshot` (grid `:51`, DataTable `:71`) and `SnapshotsMatch :96` (column case-insensitive, cell ordinal) —
    drives dirty detection.
  - `BuildSchedulerCopySnapshot :27` — "copy hosts to scheduler" honoring the unnamed checkbox selection column
    (identified structurally by empty Name+HeaderText, `GetSelectionColumn :342-350`); checked rows win, else all
    eligible rows; always force-includes `Host_IP` column (`GetCopyableColumns :334-337`).
  - Clipboard: `BuildClipboardText :147` (all-selected → TSV with headers, skipping fully-empty rows; partial →
    row-major tab-joined cells), `PasteClipboardText :224` (grows rows AND auto-creates columns to fit, skips
    ReadOnly columns), `ClearSelectedCells :275`.
  - Column helpers: `GetNextGeneratedColumnName :286` (`ColumnN` dedupe), `CreateTextColumn :301` (Host=150px,
    others=120px, NotSortable), `IsProtectedHostColumn :314` (Host_IP non-deletable by Name or HeaderText).
- **`UI/HostGridRestoreBatcher.cs` (163 lines)** — re-entrant scope-based coalescing of scrollbar refresh / host
  count refresh / mark-dirty during bulk grid restores; mark-dirty suppressed only inside *restore* scopes (`:58-67`),
  repaint refreshes suppressed in either scope; throws on unbalanced scope end (`:71-73,87-89`); flush order is
  dirty → host count → scrollbar (`:101-120`).
- **`Utilities/CsvFileSyncEvaluator.cs` (164 lines)** — detects external edits to the loaded hosts CSV:
  `Capture :23` fingerprints (mtime UTC + size), `Evaluate :36` → `NotTracked/Current/ChangedOnDisk/MissingOnDisk/Unknown`,
  `EvaluateEnvironment :62` falls back to full content comparison via `CsvManager.LoadFromFile` when no fingerprint
  is stored (legacy environments).
- **`Utilities/HostsFileIndicatorFormatter.cs` (36 lines)** — status text `filename (unsaved, disk changed)` etc.

### 7. Validation utilities

- **`Utilities/InputValidator.cs` (173 lines)** — `IsValidIpAddress :15` (IPv4 + optional `:port`),
  `IsValidHostOrIp :49` (IPv4 or DNS via `Uri.CheckHostName`; explicitly **no IPv6**, comment `:76`),
  `IsValidPort :87`, `IsValidTimeout :95` (1..3600s), `IsValidDelay :103` (0..60000ms),
  `SanitizeColumnName :111` (spaces→underscores only), `ParseIntOrDefault :131`, `Clamp :141`,
  `ValidateCronExpression :152` (5-field via Cronos, error-message-or-null), `IsFutureDate :168`.
- **`Utilities/JobEditorValidator.cs` (261 lines)** — pure error-message-or-null validators for the job editor:
  name ≤100 chars (`:20`), target by `JobTargetType` (`:45` — CustomPreset skips target name), custom preset content
  (`:56`), cron via InputValidator (`:71`), one-time-future date (`:82`), ≥1 host with `Host_IP` (`:99` — hardcoded
  literal, not `CsvManager.HostColumnName`), per-host username/password columns + per-row completeness (`:137-172`),
  stored-credential username (`:123`), Vault path (`:177`), timeout overrides (command 1–300s, connection 5–120s,
  `:11-15,191-210`), `ValidateAll :215` chains them with `??`.

### 8. File-system & persistence utilities

- **`Utilities/JsonFileWriter.cs` (126 lines)** — `WriteJsonAtomic :21`: temp file + `File.Replace` (with optional
  `.bak`), fallback chain copy-backup → delete+move; `Serialize :93` (Newtonsoft indented);
  `TryBackupCorrupt :103` (`.corrupt` overwrite or timestamped). The project-wide crash-safe persistence primitive.
- **`Utilities/AppDataPaths.cs` (110 lines)** — storage root single source of truth: `%LocalAppData%\SSH_Helper` or
  exe dir under `PORTABLE_BUILD` (`IsPortableBuild :13`, `ResolveAppFolder :46`); `TryEnsureFolderWritable :65`
  (probe-file write); `ValidateStartupStorageWritable :95` — **portable-only check**, standard builds return true
  without probing (`:99-100`).
- **`Utilities/GZipBase64Utility.cs` (44 lines)** — gzip+base64 with optional `gz64:` prefix strip on decompress;
  backs the compressed `ApplicationState` blob.
- **`Utilities/ContentHasher.cs` (25 lines)** — SHA256 hex; scheduler preset-drift hashes (`FolderPresetHashes`).
- **`Utilities/FolderPathUtility.cs` (174 lines)** — forward-slash path algebra for nested preset folders: parent,
  name, segments, ancestors, hierarchy, descendant/immediate-child checks, combine, rename-prefix, depth. All Ordinal
  comparisons (folder paths are case-sensitive here, while preset/host columns elsewhere are case-insensitive).
- **`Utilities/FlowCanvasDistLocator.cs` (180 lines)** — layered dist resolution: exe-dir → project root (walks up
  looking for csproj/sln `:62-73`) → embedded resources extracted to
  `%AppData%/SSH_Helper/flow-canvas-dist/<BuildTimestamp>` (`:103-148`, skip-if-exists per file, cache-busted by
  build metadata, sanitized path segment). Returns searched-paths list for diagnostics. Extraction attempted once
  per process (`ExtractionSync` + flag `:81-101`).
- **`Utilities/ScintillaNativeBootstrap.cs` (137 lines)** — sets `ScintillaNativeLibrary.SatelliteDirectory`:
  prefers `runtimes/<rid>/native` on disk; else extracts embedded win-x64 Scintilla.dll/Lexilla.dll to app folder,
  falling back to `%TEMP%\SSH_Helper` (`EnumerateExtractionRoots :89-111`); throws `InvalidOperationException` with
  inner cause if no writable root (`:84-87`). Only win-x64 has embedded DLLs; win-x86/arm64 silently skip (`:30-33`).

### 9. Terminal output processing

- **`Utilities/TerminalOutputProcessor.cs` (551 lines)** — the terminal text pipeline (public static, used by SSH
  session + interactive terminal):
  - `Normalize :61` — cursor-accurate line model handling `\r` overwrite, tabs to stops, backspace, ESC s/u, and CSI
    commands K/X/C/D/G/H/f/@/P/m (`ProcessCsiCommand :395-447`); trims trailing spaces per committed line; optional
    `preserveTrailingSpacesOnFinalLine` for chunked streams.
  - `Sanitize :127` — strips non-printables except ESC/CR/LF/TAB/BS.
  - Pager artifacts: `StripPagerArtifacts :143` (`--More--` variants) + `StripPagerDismissalArtifacts :156`
    (FortiGate space-echo/overwrite sequence).
  - zsh PROMPT_SP: `StripZshPromptSp :177` + streaming variant with minimal ambiguous-suffix carry
    (`StripZshPromptSpStreaming :195`).
  - `BufferIncompleteFinalLineStreaming :227` — holds back the unfinished last line so CR/BS edits resolve before
    append-only UI emission.
  - `StripTrailingPrompt :262` (uses `PromptDetector.BuildPromptRegex`, also removes starship timestamp metadata
    line `:45-47,295-298`) and `StripCommandEcho :319` (first-line equals/ends-with command).
- **`Utilities/PromptDetector.cs` (254 lines)** — prompt heuristics: terminators `# > $ %` + arrow glyphs (`:11-14`);
  `IsLikelyPrompt :160` rejects >80 chars, <2 chars, no-alphanumeric-before-terminator, paired-quote instructional
  text; `BuildPromptRegex :26` extracts a stable anchor (prefers `user@host` token `:202-225`, else pre-paren/pre-colon
  token) and allows cwd/mode/terminator drift; patterns use `(?:^|[\r\n])` for Rebex ScriptEvent compatibility
  (`:51-53,59-61`); buffer-tail detection helpers `:92-153` (4096-char lookback). Flagged in project memory as
  regression-prone alongside the SshShellSession read loop.

### 10. Output throttling

- **`Utilities/OutputThrottler.cs` (99 lines)** — lock-guarded StringBuilder + `System.Threading.Timer` flushing on a
  fixed interval through a captured `SynchronizationContext` (`ScheduleFlush :59` dedupes with an Interlocked flag).
  Single production instance: `Form1.cs:332` (`_uiOutputThrottler`, UI append throttle). `Flush()` is asynchronous
  (schedules a post, doesn't block); `Dispose :90` kills the timer **without flushing pending text**.

### 11. Diffing

- **`Utilities/InlineDiffBuilder.cs` (258 lines)** — line-level LCS diff (`BuildLcsOperations :99`) with O(n·m) DP
  guarded by `MaxLcsCells = 2,000,000` (`:15`); above that, positional pairing fallback (`BuildFallbackOperations :62`)
  which produces low-fidelity diffs (any insertion misaligns everything after it). Rendering supports context
  collapse (`  ...`), output budget with `... diff truncated` marker, and `includeAllLines` full mode. Kinds:
  Context/Added/Removed/Meta.

### 12. Dialogs

- **`UI/UnsavedPresetDiffDialog.cs` (546 lines)** — the save-preset prompt: colored inline diff (RichTextBox,
  `RenderLines :315` with per-line SelectionColor), name/timeout change summary (`BuildSummaryLine :458`),
  scheduled-job impact warning with expandable affected-jobs list (`:100-156,431-445`), four `PresetSavePromptMode`
  layouts mapping to `PresetSaveImpactAction` (Cancel/SaveExisting/RenameExisting/CreateNew/Discard,
  `ConfigureActionButtons :338-404`). Full-diff budget computed from line counts (`:527-535`); CRLF/CR normalized
  before comparison (`:517-525`). Invoked from `Form1.cs:6126` and `Form1.cs:12099`.
- **`Utilities/PresetSaveImpactResolver.cs` (76 lines)** — builds `PresetSaveImpact` (affected `JobDefinition`s
  referencing the preset or its folder, deduped by Id, name-ordered `:62-69`); feeds the dialog above.
- **`UI/MemoryDebuggerDialog.cs` (223 lines)** — diagnostics window over injected delegates (snapshot provider,
  trim, aggressive trim); shows working set / private bytes / managed heap + monospace summary; trim actions run
  synchronously on the UI thread (`:172-200`). Opened from Form1 (`Form1.cs:6052`). Fully DialogTheme'd.
- **`UI/LocalCmdConfirmationDialog.cs` (149 lines)** — implements `ILocalCmdConfirmation` for the scripting engine's
  `local_cmd` safety gate; marshals onto the UI thread via `ScriptPromptDialogRunner.ShowAsync`
  (`Services/Scripting/Commands/ScriptPromptDialogRunner.cs`). Shows resolved command/shell/working dir; buttons
  Run / "Run Same Command" (approve per host for rest of run) / Cancel; FormClosed forces Cancel for any non-OK/Yes
  close (`:136-140`). Dark mode detected by its own brightness heuristic (`:125-126`); light mode left native.
- **`UI/BrowserCallbackWebViewDialog.cs` (135 lines)** — `IBrowserCallbackOwnedWindow` for the script `browser_callback`
  command: WebView2 with per-profile `userDataFolder`, explicit `InitializeAsync :89` (handle force-create, env
  create, `EnsureCoreWebView2Async`, status bar off), `SetCompletionState :76` flips title/instructions/Cancel→Close.
  Constructed via factory inside `Services/Scripting/BrowserCallbackUiHost.cs:513-514`.
- **`UI/SettingsDialogPromptService.cs` (17 lines)** — `ISettingsDialogPromptService` seam so SettingsDialog prompts
  (via `DialogTheme.Show`) are mockable in tests.
- **`UI/IScriptEditor.cs` (31 lines)** — editor abstraction (text/selection/caret/diagnostics) decoupling Form1 from
  the Scintilla control; includes `SetDiagnostics(IReadOnlyList<EditorDiagnostic>)`.

### 13. Modeless window management

- **`Utilities/ModelessDialogManager.cs` (132 lines)** — generic single-instance modeless lifecycle:
  `ShowOrActivate :10` revives minimized/hidden instances else creates; converts `CenterParent` to manual
  owner-centered positioning clamped to the working area (`:54-86`, handles minimized owner by centering on screen);
  on close, re-activates the owner with BeginInvoke marshaling and disposal guards (`:88-130`);
  `RestoreOwnerActivationOverrideForTests` static test hook (`:6`). Production use: `Form1.cs:166`
  (`ModelessDialogManager<JobListDialog>`); design doc plans one for `RunOutputWindowForm`.

### 14. Scheduler support utilities

- **`Utilities/SchedulerInstanceLock.cs` (92 lines)** — named local-session mutex (`Local\SSH_Helper_Scheduler_v1`)
  so exactly one app instance owns the scheduler; `TryAcquire :26` is non-blocking, treats `AbandonedMutexException`
  as acquired (`:42-45`), and an in-process static name set prevents same-process double ownership (`:33-36`,
  needed because Win32 mutexes are re-entrant per thread). Created at `Form1.cs:15091`.
- **`Utilities/SchedulerNotificationFormatter.cs` (115 lines)** — pure status text: `FormatCompletion :16`
  (`[HH:mm:ss] [Scheduled|Run Now: job] Completed -- n/m hosts (... )`), `FormatStateChange :45`
  (Started/Queued/Skipped/Cancelled; returns null for non-notifiable states), status-bar text + countdown formatting
  (`:69-113`).
- **`Utilities/SchedulerJobIntegrityUtilities.cs` (27 lines)** — disables imported jobs with missing targets and sets
  a human-readable `DisabledReason` per `JobTargetType`; stored-credential note text helper.
- **`Utilities/ManualExecutionStatusProgress.cs` (42 lines)** — monotonic "Running... N%" progress state for folder
  runs (never regresses completed count, clamps, guards divide-by-zero `:21-39`).
- **`Utilities/ExecutionDialogPolicy.cs` (10 lines)** — one rule: prompt for execution options only when hostCount > 1.

### 15. Environment/preset indicator formatters (pure, all tested)

- **`Utilities/PresetBaseEnvironmentResolver.cs` (47 lines)** — walks folder ancestry to find the nearest
  `FolderInfo.BaseEnvironment`, else global base; returns source kind + source folder path.
- **`Utilities/PresetEnvironmentLoadPlanner.cs` (47 lines)** — on preset load: declared env ≠ active → switch;
  no declared env and active ≠ base → restore base; else none.
- **`Utilities/PresetEnvironmentStatusFormatter.cs` (43 lines)** — status messages for restore/switch/missing-env.
- **`Utilities/BaseEnvironmentIndicatorFormatter.cs` (20 lines)** — "Base: X" indicator visible only when active ≠ base.
- **`Utilities/FolderBaseEnvironmentSummaryFormatter.cs` (32 lines)** — folder dialog summary + inherit-choice labels.
- **`Utilities/PresetHeaderIndicatorFormatter.cs` (46 lines)** — "Preset: X (unsaved)" / "Folder: Y" header, command
  section title, `Save*` button label.

### 16. SSH config parsing

- **`Utilities/SshConfigParser.cs` (260 lines)** — OpenSSH `~/.ssh/config` subset: only `hostname, port, user,
  identityfile, hostkeyalgorithms, ciphers` (`SupportedOptions :12-15`); `Key Value` and `Key=Value` forms, quote
  stripping, `*`/`?` wildcards via regex translation (`MatchesPattern :190`), first-match-wins merge across blocks
  (`GetConfigForHost :97-133`), `~` expansion for IdentityFile (`:217-231`). Stream overload for tests.

---

## Integration points

- **DialogTheme ↔ Form1.cs `NativeMethods` (Form1.cs:27-77)** — all native theming P/Invokes live inside Form1.cs;
  DialogTheme cannot compile without it. `BorderlessTabControl` (Form1.Designer.cs:2259) is special-cased by
  `StyleTabControl`.
- **DialogTheme.ResolveDarkMode** infers theme from `Application.OpenForms` BackColor brightness — the app has no
  central "current theme" service; every dialog passes `darkMode` booleans plumbed from Form1's config.
- **LocalCmdConfirmationDialog → scripting engine**: implements `Services/Scripting/Commands/ILocalCmdConfirmation`,
  shown through `ScriptPromptDialogRunner.ShowAsync` (UI-thread marshaling + cancellation for script-initiated prompts).
- **BrowserCallbackWebViewDialog → `Services/Scripting/BrowserCallbackUiHost.cs:97-514`** via factory/adapter/session
  layering (`IBrowserCallbackOwnedWindow`).
- **UnsavedPresetDiffDialog ← PresetSaveImpactResolver ← PresetManager** (`GetJobsReferencingPreset/Folder`) — save
  prompts surface scheduled-job blast radius; affected `JobDefinition`s listed with folder suffixes.
- **HostGridUtilities ↔ `CsvManager.HostColumnName`** — the enforced `Host_IP` contract; `BuildSchedulerCopySnapshot`
  is the bridge that copies the main grid into JobEditorDialog's host tab.
- **HistoryListCollectionUpdater/Layout/ListBox ← `Models/HistoryListItem` + `HistoryIndexEntry`** — Form1's history
  panel (`Form1.cs:1046,1100,1170`).
- **OutputThrottler ← Form1 (`Form1.cs:332`)** — SSH `OutputReceived` events funnel through it onto the UI context.
- **TerminalOutputProcessor / PromptDetector ← SshShellSession + interactive terminal** — Development Guideline 5
  mandates TerminalOutputProcessor for all terminal output handling; PromptDetector regexes are also handed to Rebex
  ScriptEvent (hence the `(?:^|[\r\n])` anchoring convention).
- **FlowCanvasDistLocator / ScintillaNativeBootstrap / AppDataPaths** — startup resource pipeline; both extractors
  root themselves at `AppDataPaths.GetAppFolder()` (versioned subdirs for cache busting).
- **SchedulerInstanceLock / SchedulerNotificationFormatter / SchedulerJobIntegrityUtilities ← JobSchedulerService +
  Form1 status bar** — single-owner scheduling across instances and all user-facing scheduler text.
- **JsonFileWriter** is the persistence primitive for ConfigurationService, jobs, and history (atomic write + `.bak`
  + `TryBackupCorrupt` salvage on load).
- **Test seams**: `ModelessDialogManager.RestoreOwnerActivationOverrideForTests`,
  `FlowCanvasDistLocator.ResolveDistPath(exeDir, finder, resolver)` overload, `SshConfigParser.Parse(Stream)`,
  `SchedulerInstanceLock(mutexName)`, `AppDataPaths.ResolveAppFolder(...)` — this layer is deliberately built for
  the test project (`InternalsVisibleTo`).

---

## Observed gaps & quirks

### Theming
1. **GroupBox dark Paint handler accumulates** — `DialogTheme.cs:95` subscribes `grp.Paint +=` on every `ApplyTo`
   call with a lambda and never unsubscribes; calling `ApplyTo` twice (or toggling dark→light at runtime) stacks
   handlers, and a light-mode re-apply leaves the dark owner-draw active (the handler is only *added* under
   `darkMode`, never removed). Contrast: `StyleTabControl` correctly does `-=` then `+=` (`:368-370,403-404`).
2. **Inconsistent Enter-key semantics across themed prompts** — `Confirm` sets `AcceptButton = btnNo`
   (`DialogTheme.cs:577`) and styles **No** as primary (`:582`), while `ShowCore` sets `AcceptButton` to the *first*
   (affirmative) button and styles it primary (`:724,729`). So Enter means "No" in `Confirm` but "Yes" in
   `Show(..., YesNo)`. Whichever is intended, the two paths disagree.
3. **`Confirm` clips long messages** — label is fixed `Size(310, 56)` with `AutoSize = false`
   (`DialogTheme.cs:552-555`); messages longer than ~3 lines silently truncate. `ShowCore` auto-sizes properly
   (`:681-707`); `Confirm` predates it and was never reflowed.
4. **Dark-mode detection is a duplicated heuristic, not a source of truth** — `BackColor.GetBrightness() < 0.2f`
   appears in `DialogTheme.cs:771,777` and again in `LocalCmdConfirmationDialog.cs:126`; ownerless dialogs guess from
   `Application.OpenForms[0]` (`:774`). A config-driven theme accessor would remove the guesswork.
5. **Label color preservation is hardcoded to 3 grays** — `DialogTheme.cs:180-182` only preserves `Color.Gray`,
   `(108,117,125)`, `(70,70,70)` as "secondary"; any other intentional label color (e.g., status red/green) gets
   overwritten to theme text on `ApplyTo`.
6. **System icon bitmaps never disposed** — `GetSystemIcon` allocates a 32px `Bitmap` (`DialogTheme.cs:812`);
   `PictureBox` does not dispose its Image; small GDI handle leak per themed message box with an icon.
7. **Residual unthemed message boxes** — `SettingsDialog.cs:1069,1073` (Vault Test result) use raw `MessageBox.Show`
   and will render light in dark mode. (`Program.cs:50` is pre-theme startup and acceptable.)
8. **Two parallel palettes** — Form1 themes `PresetTabHeaderStrip` with its own `LightBackground/DarkSurface2/...`
   constants (`Form1.cs:3388-3394,3532-3538`) that are not the `DialogTheme` constants; drift between the two palettes
   is unguarded.
9. **`LocalCmdConfirmationDialog` ignores user font settings** — never calls `DialogTheme.SetDialogFont`, uses
   absolute pixel layout with fixed button widths (`LocalCmdConfirmationDialog.cs:80-120`); "Run Same Command" in a
   140px button is prone to clipping at larger fonts/DPI. Light mode receives no theming at all (`:127-134`).
10. **`MemoryDebuggerDialog` Close button has no initial Location** — positioned only by the `buttonPanel.SizeChanged`
    handler (`MemoryDebuggerDialog.cs:128-131`); briefly sits at (0,0) overlapping Refresh until the first layout pass.

### Clipboard / grid
11. **Paste drops empty rows** — `HostGridUtilities.PasteClipboardText` splits with
    `StringSplitOptions.RemoveEmptyEntries` (`HostGridUtilities.cs:236`), so TSV data containing blank lines pastes
    shifted upward relative to the source.
12. **Paste error path leaves grid locked** — `grid.AllowUserToAddRows = false` (`:238`) is restored at `:265` with no
    try/finally; an exception mid-paste (e.g., cell validation) leaves the new-row affordance disabled.
13. **Partial-selection copy misaligns columns** — `BuildClipboardText`'s non-all-selected branch (`:194-219`) joins
    selected cells in a row with single tabs regardless of column gaps; pasting a non-contiguous selection produces
    columns shifted left.
14. **Checkbox selection column identified structurally** — `GetSelectionColumn :342-350` matches "a
    DataGridViewCheckBoxColumn with empty Name and HeaderText"; naming that column anywhere silently breaks
    scheduler host copy.

### Validation
15. **No IPv6 anywhere** — `InputValidator.IsValidHostOrIp` explicitly rejects IPv6 (`InputValidator.cs:76-79`), and
    the `host:port` split via `LastIndexOf(':')` would misparse a bare IPv6 literal anyway. For a multi-host SSH tool
    this is a real functional gap, not just validation pedantry.
16. **`IsValidIpAddress` accepts whitespace inside octets** — `int.TryParse` tolerates leading/trailing whitespace
    (`:30`), so `"1. 2.3.4"` validates. Also accepts leading zeros (`"01.02.03.04"`).
17. **`SanitizeColumnName` only replaces spaces** (`:117`) — other problematic characters (dots, brackets, braces used
    in `{{column}}` templating) pass through untouched.
18. **`JobEditorValidator.ValidateHosts` hardcodes `"Host_IP"`** (`JobEditorValidator.cs:107`) instead of
    `CsvManager.HostColumnName`, and the lookup is case-sensitive `TryGetValue` while the rest of the codebase treats
    host columns case-insensitively (`HostGridUtilities.cs:83`, `TryGetRowValue :243` is case-insensitive in the same
    file). A row dictionary keyed `host_ip` would pass the grid but fail this check (or vice versa).

### Error-handling / robustness
19. **`SshConfigParser` fails silent** — any I/O or parse exception returns an empty config (`SshConfigParser.cs:37-41`)
    with no error surfaced; supported-option set is 6 keys (no `Include`, `Match`, `ProxyJump`, `Port` ranges); OpenSSH
    `!negation` patterns are treated as literal hostnames (`MatchesPattern :190-212`) so a `Host * !prod` config
    silently matches the wrong hosts.
20. **`JsonFileWriter` fallback is non-atomic** — when `File.Replace` throws, the fallback is `File.Delete(path)` then
    `File.Move(tempPath, path)` (`JsonFileWriter.cs:69-72`); a crash between the two leaves no config at all. Bare
    `catch` blocks (`:43,63`) also discard the reason Replace failed.
21. **`OutputThrottler.Dispose` drops buffered output** (`OutputThrottler.cs:90-97`) — pending tail text is lost at
    shutdown; and `Flush()` is fire-and-forget (posts to the sync context) despite the synchronous-sounding name.
22. **`AppDataPaths.ValidateStartupStorageWritable` is portable-only** (`AppDataPaths.cs:99-100`) — standard installs
    never probe `%LocalAppData%` writability at startup; a redirected/locked profile fails later at first save instead.
23. **`TerminalOutputProcessor` has no OSC handling** — `ProcessEscapeSequence :351-393` covers only `ESC s/u` and CSI;
    OSC title sequences (`ESC ] 0 ; title BEL`) leak `]0;user@host...` text into normalized output (the BEL itself is
    stripped by `Sanitize`, the payload is not). Charset designators (`ESC ( B`) similarly leak.
24. **`InlineDiffBuilder` fallback diff quality** — above 2M LCS cells (`InlineDiffBuilder.cs:15,37-39`) the positional
    fallback marks every line after a single insertion as changed; `UnsavedPresetDiffDialog` then renders the full
    budget line-by-line into a RichTextBox with per-line `Select`/`SelectionColor` (`UnsavedPresetDiffDialog.cs:315-336`)
    — O(n) UI operations on very large presets.
25. **`CsvFileSyncEvaluator` fingerprint is mtime+size only** (`CsvFileSyncEvaluator.cs:29-33`) — same-size edits with
    a restored timestamp evade "changed on disk" detection (acceptable trade-off, but worth knowing); `Evaluate`
    catch-all maps any exception to `Unknown` with only `ex.Message` retained (`:56-59`).
26. **`SchedulerInstanceLock` cross-thread release** — the mutex is acquired on whatever thread calls `TryAcquire`
    and released in `Dispose`; if disposed from another thread `ReleaseMutex` throws `ApplicationException` (caught
    `:69-71`) and OS-level ownership persists until process exit while the in-process set is cleared — a second
    in-process acquire attempt would then fail at the OS level inconsistently with the local bookkeeping.
27. **`PresetTabHeaderStrip` hardcodes its two tabs** (`PresetTabHeaderStrip.cs:11`) — fine today, but it's presented
    as a reusable control while being single-purpose; no keyboard accessibility (TabStop=false, no arrow-key
    selection, mouse-only).
28. **`FlatVisualButton` renders no focus indicator** (`OnPaint :86-118` draws background/border/text only) — keyboard
    users get no visual cue which button has focus, an accessibility regression vs stock buttons.
29. **`ScintillaNativeBootstrap` silently skips non-x64** (`ScintillaNativeBootstrap.cs:30-33`) — on win-arm64 with no
    packaged runtimes dir, SatelliteDirectory is never set and the editor will fail to load later with a less
    actionable error.
30. **`DialogTheme.ApplyNativeTheme` forces handle creation recursively** (`DialogTheme.cs:307-308`) — intentional
    (theme hidden tab pages), but it defeats lazy handle creation on large dialogs; cost is paid at every theme apply.

### Hygiene notes
- `BufferedPanel`/`BufferedSplitContainer` live in namespace `SSH_Helper` while every other UI control is in
  `SSH_Helper.UI` — inconsistent, complicates discovery.
- No TODO/HACK/FIXME comments exist anywhere in the in-scope files — gaps above are behavioral, not flagged debt.
- `ExecutionDialogPolicy` (10 lines) and `HistoryStartupSelectionHydration` (9 lines) are single-expression classes —
  defensible as named test-locked rules, but borderline over-factoring.
