# Subsystem map: General dialogs (root-level)

Scope: `SettingsDialog.cs` (2812 LOC), `EnvironmentDialog.cs` (910), `UpdateDialog.cs` (832, also contains `NoUpdateDialog` + `UpdateErrorDialog`), `AboutDialog.cs` (157), `FindDialog.cs` (550), `ImportPreviewDialog.cs` (309).
Closely related root dialogs NOT covered here (other audit areas): `JobListDialog.cs`, `JobEditorDialog.cs`, `ExecutionDetailsDialog.cs`, `FolderExecutionDialog.cs`, `RunOutputViewerDialog.cs`.

All dialogs are `internal sealed : Form`, hand-built (no Designer files), themed through `UI/DialogTheme` (except FindDialog — see gaps), and instantiated from `Form1` (except ImportPreviewDialog, which is opened by `JobListDialog`).

---

## Feature inventory

### 1. SettingsDialog — application preferences hub
- **Entry point:** Form1 menu → `settingsToolStripMenuItem_Click` (`Form1.cs:5714-5758`). Modal; on OK Form1 re-applies theme/fonts/editor settings/column auto-resize, toggles `_sshService.UseConnectionPooling` + `PreferSshAgent`, migrates the stored main-form password when `UseCredentialManager` flips (`Form1.cs:5735-5745`), then re-runs `InitializeVault()` + `InitializeNotifications()` (`Form1.cs:5747-5748`).
- **Construction:** public ctor delegates to an internal DI ctor (`SettingsDialog.cs:182-199`) taking `ConfigurationService`, optional `PresetManager`, `IBrowserCallbackWebViewProfileManager` (default `BrowserCallbackWebViewProfileManager.Shared`), `ISettingsDialogPromptService` (`UI/SettingsDialogPromptService.cs` — thin `DialogTheme.Show` wrapper for testability), optional `ICredentialProvider`. Fixed-size 544x620 `FixedDialog` (`SettingsDialog.cs:212-213`). Six tabs in a `BorderlessTabControl`. Controls are located post-construction by recursive name lookup `FindControl<T>` (`SettingsDialog.cs:2308-2317`) — returns `null!` if not found (silent NRE risk on rename).
- **Save:** `BtnSave_Click` (`SettingsDialog.cs:2657-2755`) — single `_configService.Update(...)` writing every tab; gated only by `ValidateVaultProfiles()` (`SettingsDialog.cs:2757-2798`).

#### Tab: General (`CreateGeneralTab`, SettingsDialog.cs:365-475)
| Control | Setting |
|---|---|
| chkRememberState | `config.RememberState` (hosts/preset/history persistence on exit) |
| numMaxHistory (1-500, default 30) | `config.MaxHistoryEntries` |
| numDefaultTimeout (1-300 s) | `config.Timeout` (global command timeout; Form1 mirrors it into `txtTimeoutHeader.PlaceholderText` at 5726) |
| numConnectionTimeout (5-120 s) | `config.ConnectionTimeout` |
| "Reset All Preset Timeouts to Default" button | `_presetManager.ClearAllTimeouts()` via confirm prompt (`SettingsDialog.cs:2440-2464`); sets `PresetTimeoutsWereCleared`, which Form1 reads at 5753-5757 to resync the active editor timeout box. **Acts immediately, not on Save.** Silently no-ops if `PresetManager` is null. |
| chkDarkMode | `config.DarkMode` (note label: output window is always dark). Exposed live via `IsDarkModeEnabled` property (`SettingsDialog.cs:2655`) — currently unused by Form1 (theme applies only after OK). |
| chkAutoResizeHostColumns | `config.AutoResizeHostColumns` |
| chkEnableSshConfig | `config.SshConfig.EnableSshConfig`; note label shows resolved `~/.ssh/config` path (`SettingsDialog.cs:448-449`) |
| chkUseConnectionPooling | `config.UseConnectionPooling` |
| chkUseCredentialManager | `config.Credentials.UseCredentialManager` |
| chkPreferSshAgent | `config.Credentials.PreferSshAgent` |
| "Clear Embedded Browser Data..." button | `_browserCallbackProfileManager.ClearEmbeddedBrowserData()` (`SettingsDialog.cs:2466-2502`) — confirm prompt, handles `Cleared` and `ActiveSessionBlocked` results. **Acts immediately, not on Save.** |

#### Tab: Updates (`CreateUpdatesTab`, SettingsDialog.cs:1695-1744)
- chkCheckForUpdatesOnStartup → `config.UpdateSettings.CheckOnStartup`.
- chkEnableUpdateLog → `config.UpdateSettings.EnableUpdateLog`; note label hardcodes log path `%TEMP%\SSH_Helper_Update\update.log` (`SettingsDialog.cs:1737`).

#### Tab: Command Editor (`CreateCommandEditorTab`, SettingsDialog.cs:1746-1940)
20 Scintilla-editor options in four sections, all persisted to `config.CommandEditor` with `Math.Clamp` + `Normalize()` on save (`SettingsDialog.cs:2682-2712`); load goes through `CloneNormalized()` (`SettingsDialog.cs:2565`):
- Features: syntax highlighting, autocomplete, autocomplete-on-typing (indented child option).
- Validation & Diagnostics: inline validation, debounce ms (bounds from `CommandEditorSettings.Min/MaxValidationDebounceMs`), inline warnings, diagnostic hover tooltips, variable inspector tooltips, YAML hygiene warnings.
- Indentation & Newline: spaces-for-tab, indent size, smart Enter, preserve blank line between steps.
- Visual Aids: current-line highlight, indent guides, whitespace markers, long-line guide + column, code folding, brace matching.

#### Tab: Appearance (`CreateAppearanceTab`, SettingsDialog.cs:1942-2224) — absolute-positioned scroll panel (unlike other tabs' FlowLayoutPanels)
- Font families: UI font combo (all installed families), Code font combo filtered by `IsLikelyMonospaced` — a hardcoded name-substring list of ~19 patterns (`SettingsDialog.cs:2620-2629`); a genuinely monospaced font with an unlisted name is invisible.
- Global scale trackbar 80-150% → `FontSettings.GlobalScaleFactor`.
- 13 per-surface font size spinners (7-16pt, 0.5 steps): section titles, tree views, empty labels, execute buttons, code editor, output area, tab headers, buttons, host list, menus, status bar, dialogs, script prompts (script prompts allows 7-24).
- Layout: code-editor word wrap, output-area word wrap, tree row height (0=auto, max 50), host list row height (16-50).
- Accent color: enable checkbox + swatch panel + `ColorDialog` picker (`SettingsDialog.cs:2407-2421`); stored as nullable ARGB in `FontSettings.CustomAccentColor`; default Win-blue `(0,120,215)` (`SettingsDialog.cs:178`).
- Live preview panel (title label, TreeView, code TextBox, Execute button) re-rendered by `UpdatePreview()` (`SettingsDialog.cs:2345-2399`); preview fonts accumulate in `_previewFonts` and are disposed only in `Dispose` (deliberate — GDI+ shared-handle gotcha documented at 2361-2365). Contrast color for accent computed by luminance (`GetContrastColor`, 2401-2405).
- "Reset Appearance to Defaults" → `FontSettings.CreateDefault()` re-applied to controls after confirm (`SettingsDialog.cs:2423-2438`) — staged, only persisted on Save (unlike preset-timeout reset).

#### Tab: Vault (`CreateVaultTab`, SettingsDialog.cs:477-740) — HashiCorp Vault profile manager
- Master enable `chkVaultEnabled` → `config.Vault.Enabled`; gates the whole tab via `UpdateVaultControlStates` (742-749).
- Profile list (left) with Add/Remove; details (right): name, address, namespace, mount path (defaults "secret"), KV version combo (Auto-detect/v1/v2 — index cast straight to `VaultKvVersion`, 981), auth method combo (Token/AppRole/LDAP/Userpass/OIDC, index cast to `VaultAuthMethod`, 982) showing one of 5 stacked auth panels (`UpdateVaultAuthFieldVisibility`, 780-788).
- OIDC sub-fields: auth mount (default "oidc"), role, callback host (default 127.0.0.1), port (default 8250), path (default /oidc/callback), timeout 15-3600 s.
- TLS: CA cert path + Browse (`OpenFileDialog`, pem/crt/cer filter, 1029-1039), "Skip TLS verification (development only)" checkbox.
- Cache TTL 0-86400 s (default 300).
- "Set as default profile" checkbox → `_vaultDefaultProfileName`; save resolves it and falls back to first profile (`SaveVaultSettings`, 1143-1178).
- **Secrets never live in config:** token / AppRole secret-id / LDAP password / Userpass password are loaded from and saved to Windows Credential Manager via `CredentialTargets.VaultAuthTarget(profile, authType)` (`LoadVaultProfileDetails` 916-932, `SaveVaultCredential` 1020-1027). Empty textbox ⇒ credential deleted.
- "Test Connection" (`BtnVaultTestConnection_Click`, 1041-1080): builds a throwaway single-profile `VaultSettings` + `VaultService` with textbox-backed secret providers and a `tokenSaver` that writes the OIDC-obtained token back into Credential Manager; async, button disabled + relabeled while running.
- Save-time validation (`ValidateVaultProfiles`, 2757-2798): only when Vault enabled — non-empty profile names, OIDC role required, OIDC callback validated by `VaultOidcCallbackSettings.TryCreate`.
- Profile-list editing model: edits to the active profile are flushed into `_vaultProfiles[i]` on selection change / Add / Remove / Test / Save (`PersistVaultProfileByIndex`, 970-1018), with `_suppress*` reentrancy flags.

#### Tab: Notifications (`CreateNotificationsTab`, SettingsDialog.cs:1180-1340) — channel profile manager
- Master enable → `config.Notifications.Enabled`; profile list + Add/Remove mirroring Vault tab.
- Per profile: name, channel kind combo (Slack/Teams/Discord/SMTP Email — explicit index↔enum mapping at 1374-1394), default title; webhook panel (URL, password-masked) for Slack/Teams/Discord, SMTP panel (host, port 1-65535 default 587, from, multiline to-list split on newline/comma/semicolon via `SplitSmtpRecipients` 1604-1616, username, password, STARTTLS checkbox) for SMTP.
- Secrets in Credential Manager: webhook URL → `CredentialTargets.NotifyWebhookTarget`, SMTP password → `NotifySmtpPasswordTarget` (1577-1581, 1499-1505).
- **Rename migrates credentials** old→new target then deletes old (`MigrateNotificationCredential`, 1584-1593) — Vault tab has no equivalent (see gaps).
- Default profile checkbox is clearable (unchecking clears the default, 1412-1422) — Vault's is not.
- Save clones profiles into `config.Notifications.Profiles` + resolves `DefaultProfileName` (1668-1693).

### 2. EnvironmentDialog — named environments + environment variables
- **Entry point:** Form1 environment toolbar → `TsbManageEnvironments_Click` (`Form1.cs:1935-1965`); on OK, Form1 switches to `dialog.SelectedEnvironmentName` (set only by Save, `EnvironmentDialog.cs:639-643`) via `TrySwitchEnvironment`, else refreshes the selector.
- **Layout:** sizable form, persisted geometry — width/height/splitter saved to `WindowState.EnvironmentDialog*` on close (`SaveDialogLayout`, `EnvironmentDialog.cs:303-323`) and restored clamped on load (`ApplySavedSplitterDistance`, 285-301). Left: owner-drawn environment ListBox with per-item color swatch (`LstEnvironments_DrawItem`, 359-401; horizontal extent computed at 762-777). Right: action buttons + metadata + variables grid.
- **Actions (all act immediately through `EnvironmentService`, each persisting to config on the spot):**
  - New (416-434): `Interaction.InputBox` name prompt → `CreateEnvironment(name, copyFrom: current)` — i.e. New always copies the currently selected environment.
  - Duplicate (436-456): same with `"<name>-copy"` suggested.
  - Rename (458-479): `RenameEnvironment` + `_presetManager.RenameFolderBaseEnvironment` to keep folder base-environment references valid.
  - Delete (481-507): Yes/No confirm → `DeleteEnvironment` + `_presetManager.ClearFolderBaseEnvironment`; reselects `EnvironmentConfig.DefaultName`.
  - Export (509-545): `SaveFileDialog` (`*.sshenv.json`), writes an `EnvironmentTransferPackage` envelope `{format:"ssh-helper.environment", version:1, exportedAtUtc, environment}` via plain `File.WriteAllText` (539).
  - Import (547-602): accepts either the package envelope or a bare `EnvironmentConfig` JSON (`ParseImportedEnvironment`, 844-878); name-conflict prompt Yes=overwrite / No=rename (`-imported` suggestion) / Cancel; `_environmentService.ImportEnvironment`.
- **Metadata fields:** Name (read-only — rename only via button), Description, Label Color (ColorDialog + "Default" reset, 604-637, ARGB int stored as `EnvironmentConfig.LabelColor`), Vault Profile combo populated from `config.Vault.Profiles` only when Vault is enabled, "(none)" sentinel string (779-793).
- **Variables grid:** 2-column DataGridView (Name/Value), add-row enabled, Ctrl+Del or right-click "Delete Variable" context menu with row-targeting + menu-cancel when on the new-row (645-710). `CollectVariables` (825-842) trims keys, skips blanks, case-insensitive dict (silent last-wins on duplicate names).
- **Persistence model:** `PersistCurrentEnvironmentDetails` (802-823) pushes description/color/variables/vault-profile through `EnvironmentService.UpdateEnvironmentDetails` → `SaveEnvironmentState` (writes config to disk, `Services/EnvironmentService.cs:301-327`). Called on every selection change and before every action — see gaps re: Cancel.

### 3. UpdateDialog family — update available / up-to-date / check failed
- **Entry point:** `Form1.CheckForUpdatesAsync(bool silent)` (`Form1.cs:14965-15039`) — silent path on startup (honors `UpdateSettings.SkippedVersion`, 14999), loud path from the "Check for Updates" menu (shows `NoUpdateDialog` / `UpdateErrorDialog` too).
- **UpdateDialog** (`UpdateDialog.cs:12-696`): sizable; shows installed vs latest version, "What's New" release notes rendered by a hand-rolled markdown→RTF converter (`FormatReleaseNotesToRtf`, 255-329: #/##/### headers, -/* bullets, `code`, **bold**, *italic*/_italic_, unicode-escaped; plain-text fallback 419-431), `DetectUrls` link clicks open the browser (433-447), "View full release notes on GitHub" LinkLabel (109-126).
- Buttons:
  - **Yes, Update Now** (`BtnYes_Click`, 477-664): if no `DownloadUrl`, offers the release page instead (479-511). Otherwise swaps button row for progress UI, downloads via `UpdateService.DownloadUpdateAsync` (maxRetries 3, retry progress relabels the bar, 528-540), **requires `ChecksumUrl`** — refuses install without it (542-555), verifies via `VerifyUpdatePackageAsync`, runs the optional `_confirmExitBeforeInstall` callback (Form1 passes `ConfirmExitWorkflow`, `Form1.cs:15008`), then `LaunchUpdaterAndExit(downloadPath, null, enableUpdateLog)`. Error handling distinguishes cancel / `InvalidDataException` (verification) / `FileNotFoundException` (updater missing → open release page) / generic with `IsRetryableException` → Yes(retry)/No(GitHub)/Cancel (614-659).
  - **Not Now** → DialogResult.No (462-467); **Skip This Version** → `_onSkipVersion(latest)` which Form1 persists to `UpdateSettings.SkippedVersion` (`Form1.cs:15005-15007`).
- Download progress: subscribes `UpdateService.DownloadProgressChanged` with Invoke marshaling (676-695), MB + % readout; CTS cancelled on form close (457-460).
- **NoUpdateDialog** (`UpdateDialog.cs:701-763`): fixed "You're up to date!" confirmation with green check glyph.
- **UpdateErrorDialog** (`UpdateDialog.cs:768-831`): fixed "Could not check for updates" + error text, warning glyph.

### 4. AboutDialog (`AboutDialog.cs`)
- **Entry point:** Form1 Help menu (`Form1.cs:6493`). Shows app name/version, build time from `BuildTimestamp` assembly metadata (`ResolveBuildTimeSafe`, 139-155, "Unknown" fallback), hardcoded author line `Chris Dudek (chris_dudek@nwcd.net)` (66), runtime description, and a "Project Home" LinkLabel to `https://github.com/nosmircss/SSH_Helper` (40). Self-sizes to content via `FitToContent` text measurement (103-137); resize locked by Min/MaxSize pinning (90-91).

### 5. FindDialog — VS Code-style find bar for the output area
- **Entry points:** Ctrl+F (`Form1.ProcessCmdKey`, `Form1.cs:14462-14464`) or Edit→Find menu (5770-5773) → `ShowFindDialog` (14482-14497): borderless singleton owned by Form1, seeded from `txtOutput.SelectedText` or last term, anchored to the top-right of `txtOutput` via `AnchorTo` + owner move/resize tracking (`FindDialog.cs:213-241`).
- **Search target is `txtOutput` only** — Form1's `FindFromDialog` / `UpdateFindStatus` / `BuildMatchList` operate on the output textbox (`Form1.cs:14499-14556`); F3/Shift+F3 navigate matches even when the bar is closed (14472-14477, `NavigateToMatch` 14558+).
- Controls: search textbox (incremental — TextChanged triggers find-from-cursor, 174-180), "N of M"/"No results" match counter (`SetMatchInfo`, 303-319), prev/next arrow IconButtons, Match Case toggle (Alt+C while textbox focused, 193-198), close (Escape). Enter/Shift+Enter and F3/Shift+F3 navigate (182-199, 279-295).
- Close hides instead of disposing (`OnFormClosing`, 331-339) so term/toggles persist per session.
- Custom owner-painted `InputPanel`, `IconButton`, `ToggleButton` private controls with hardcoded VS Code dark palette (10-19).

### 6. ImportPreviewDialog — job-import review grid
- **Entry point:** `JobListDialog` import flow (`JobListDialog.cs:1452`), fed `JobExportService.ImportJobEntry` list.
- Grid columns: Import checkbox (default checked), Name (`entry.ResolvedName`), Schedule (`Recurring (cron)` formatting, 208-212), Target (`[Folder] name` / `[Custom] Scheduler-local content` / preset name, 215-220), Status with per-cell color (223-253): amber "Renamed (original: X)" on name conflict, red "Target folder/preset not found - will be disabled" on `MissingTarget`, green "OK".
- Checkbox commits immediately (`CurrentCellDirtyStateChanged`→`CommitEdit`, 260-267) so the "N of M jobs selected" summary stays live (277-291).
- OK collects checked rows (entry stashed in `row.Tag`) into `AcceptedEntries`; null means cancelled (293-307). Honors caller-passed dark mode + font family/size (`DialogTheme.SetDialogFont`, 188-191).

---

## Integration points

- **ConfigurationService** — SettingsDialog reads `GetCurrent()` and writes via a single `Update(...)` transaction (`SettingsDialog.cs:2663`); EnvironmentDialog persists its window geometry into `WindowState` (303-323) and indirectly writes config through every EnvironmentService call; Form1 persists `UpdateSettings.SkippedVersion`/`LastCheckTime` around the update dialogs.
- **EnvironmentService** — Create/Rename/Delete/Import/UpdateEnvironmentDetails all save environment state to disk immediately (`Services/EnvironmentService.cs:301-327` etc.). Form1's `EnvironmentChanged` reload happens after dialog OK via `TrySwitchEnvironment`.
- **PresetManager** — SettingsDialog `ClearAllTimeouts()`; EnvironmentDialog `RenameFolderBaseEnvironment` / `ClearFolderBaseEnvironment` keep folder→environment bindings consistent on rename/delete (`EnvironmentDialog.cs:472,500`).
- **ICredentialProvider / CredentialTargets** (`Services/Credentials/CredentialTargets.cs`) — Vault auth secrets (`VaultAuthTarget`, 4 auth types) and notification secrets (`NotifyWebhookTarget`, `NotifySmtpPasswordTarget`) live exclusively in Windows Credential Manager; targets are portable-build-aware (`AppDataPaths.IsPortableBuild`).
- **VaultService** — instantiated ad hoc for Test Connection (`SettingsDialog.cs:1060-1068`); the OIDC `tokenSaver` writes obtained tokens back to Credential Manager. EnvironmentDialog's Vault Profile combo consumes `config.Vault.Profiles` (so settings saved in SettingsDialog feed it).
- **UpdateService** — `CheckForUpdatesAsync`, `DownloadUpdateAsync`, `VerifyUpdatePackageAsync`, `LaunchUpdaterAndExit`, `DownloadProgressChanged` event, static `GetUserFriendlyErrorMessage`/`IsRetryableException` helpers.
- **BrowserCallbackWebViewProfileManager** — `ClearEmbeddedBrowserData()` with `EmbeddedBrowserDataClearResult` enum; the dialog handles the active-session-blocked case.
- **Form1 find plumbing** — FindDialog is UI-only; matching/highlighting state (`_findMatches`, `_currentMatchIndex`, `_lastFindTerm/MatchCase`) lives in Form1 (`Form1.cs:14499-14580`), called back via `internal` methods `FindFromDialog`/`UpdateFindStatus` and reported back via `SetMatchInfo`/`SetStatus`.
- **JobExportService** — ImportPreviewDialog is a pure view over `ImportJobEntry` (HasConflict/MissingTarget/ResolvedName resolved upstream).
- **DialogTheme** — every dialog except FindDialog routes through `ApplyTo`/`StyleButton`/`SetDarkTitleBar`/`StyleDataGridView`/`ApplyNativeTheme`; SettingsDialog re-applies native theme per tab on dark mode (`SettingsDialog.cs:345-349`) because hidden tab pages lose native dark rendering.
- **Tests** — `SSH_Helper.Tests/UI/SettingsDialogVaultTests.cs`, `SettingsDialogAppearanceTests.cs`, `SettingsDialogBrowserCallbackTests.cs` exercise the internal DI ctor; no tests found for EnvironmentDialog, UpdateDialog, FindDialog, ImportPreviewDialog.

---

## Observed gaps & quirks

### Cancel-doesn't-cancel (state mutated before Save)
1. **EnvironmentDialog Cancel is cosmetic.** Every action (New/Duplicate/Rename/Delete/Import) and every selection change persists straight through `EnvironmentService` to disk (`EnvironmentDialog.cs:408,418,441,463,499`; `Services/EnvironmentService.cs:323-326`). The Save button's only real effect is setting `SelectedEnvironmentName` so Form1 switches environments (`EnvironmentDialog.cs:639-643`). A user who deletes an environment and clicks Cancel has still deleted it. No affordance communicates this.
2. **SettingsDialog secret writes ignore Cancel.** Vault/notification secrets are written to / deleted from Windows Credential Manager during `PersistVaultProfileByIndex` (1011-1017) and `PersistNotificationProfileByIndex` (1577-1581), which fire on profile selection change and Test Connection — before Save. Worse, **Remove deletes stored credentials immediately** (`BtnVaultRemove_Click` 852-858, `BtnNotificationRemove_Click` 1451-1455); cancelling the dialog leaves config still referencing the profile but its secrets gone.
3. **SettingsDialog "Reset All Preset Timeouts" and "Clear Embedded Browser Data" execute immediately** (2440-2464, 2466-2502) inside a dialog whose other 60+ controls are staged-until-Save — inconsistent mental model.

### Vault/Notification profile-manager asymmetries (same pattern, drifted implementations)
4. **Vault rename orphans Credential Manager entries.** Notifications migrate+delete on rename (`MigrateNotificationCredential`, 1584-1593); Vault just re-saves under the new name (997-1017), leaving `ssh_helper:vault:<oldname>:*` entries behind forever.
5. **Vault default profile cannot be cleared** — `ChkVaultDefault_CheckedChanged` early-returns when unchecked (806-816); notifications support clearing (1412-1422). Vault save also silently force-assigns the first profile as default (1173-1174).
6. **Notification Add drops pending edits.** `BtnVaultAdd_Click` flushes the active profile first (`PersistCurrentVaultProfile()`, 820); `BtnNotificationAdd_Click` (1424-1440) does not, and selection-change persistence is suppressed during the add — unsaved edits to the previously selected notification profile are lost.
7. **No duplicate-name guard for profiles.** Both Add paths generate `profile-N` from `Count+1` (821, 1426), which can collide after deletions (add 2, delete #1, add → two "profile-2"); duplicate names silently share one Credential Manager target and the default-profile resolution picks the first match (1169-1171).
8. **Enum mapping fragility:** Vault KV-version and auth-method are raw `(enum)SelectedIndex` casts (981-982) — reordering combo items or enum members silently corrupts profiles. Notifications do it explicitly (1374-1394) but **omit `NotificationChannelKind.Toast` (=3)** (`Models/NotificationSettings.cs:95`): a profile with Kind=Toast loads as Slack (`SetSelectedNotificationKind` default arm, 1392) and is re-persisted as Slack on the next selection change. (Toast is documented "no profile required", so this is latent rather than live.)
9. **Vault Test Connection uses raw `MessageBox.Show`** (1069, 1073) instead of `_promptService`/`DialogTheme.Show` — unthemed in dark mode and invisible to the prompt-service test seam used everywhere else in the file.
10. **Webhook URL / SMTP recipients are not validated** (no URL-shape or address-shape check anywhere in the save path); only Vault gets `ValidateVaultProfiles`. A typo'd webhook silently fails at dispatch time.

### EnvironmentDialog
11. **`Interaction.InputBox` (Microsoft.VisualBasic) for all name prompts** (893-900) — never dark-themed, can't distinguish Cancel from empty input (both return null), no inline validation.
12. **Export uses bare `File.WriteAllText`** (539) — contradicts the project convention of `JsonFileWriter.WriteJsonAtomic` (CLAUDE.md Utilities); a crash mid-write corrupts the export silently.
13. **`PersistCurrentEnvironmentDetails` swallows all exceptions** (819-822) — a failed disk write of variables/description is silently discarded; the comment claims "actionable errors are shown on explicit operations" but Save itself routes through the same silent path (639-641).
14. **Duplicate variable names silently collapse last-wins** in `CollectVariables` (825-842) with no warning; the grid happily displays two rows with the same name until reload.
15. `LoadEnvironmentList` assumes at least one environment exists — `SelectedIndex = index >= 0 ? index : 0` (728) throws on an empty list (currently guarded only by the service always providing Default).
16. Variable values are plain text with no masking/secret affordance — environment variables holding passwords are fully visible (may be by design; vault_path is the sanctioned secret route).

### UpdateDialog family
17. **Retry recursion vs `finally` unsubscribe race:** the generic-error retry path calls `BtnYes_Click(sender, e)` re-entrantly (644); since it's `async void`, the inner call returns at its first await and the **outer** `finally` (660-663) then unsubscribes `DownloadProgressChanged` — which the inner invocation just subscribed — so a retried download can show a frozen progress bar.
18. Missing-checksum path sets the label to "Verification failed." (552) when nothing was verified — misleading copy; the message box wording ("Verification Required") is correct.
19. `NoUpdateDialog`/`UpdateErrorDialog` apply theming only when `darkMode == true` (754-761, 822-829) and never receive `SetDialogFont` from Form1 (15016, 14989) — they ignore the user's dialog-font setting that `UpdateDialog` itself receives (15009).
20. Hand-rolled markdown→RTF (255-393) doesn't handle `[text](url)` links (only bare URLs via `DetectUrls`), nested formatting, or code fences; release notes using those degrade silently. Acceptable, but a known limitation.
21. `_lblVersionInfo` aligns the two version numbers with manual spaces in a non-mono layout context (72-73) — cosmetic.

### FindDialog
22. **Hardcoded dark palette regardless of app theme** (10-19) — in light mode the find bar is a floating dark rectangle; it's the only root dialog bypassing `DialogTheme` colors.
23. **Half-built features shipped hidden:** whole-word toggle (115-122), regex toggle (124-131), and find-in-selection button (147-153) are constructed then `Visible=false // Hide for now - not implemented`. Dead weight or roadmap markers.
24. **Searches only the output area.** Ctrl+F always anchors to `txtOutput` (`Form1.cs:14492`); there is no find for the command/YAML editor from this dialog (Scintilla editor users would expect editor search), and no "replace" anywhere.
25. Alt+C (match case) works only while the textbox has focus (193-198), despite the tooltip advertising it as a dialog-level shortcut; form-level `OnKeyDown` handles only F3/Escape (279-295).
26. Borderless window repositions on owner move/resize but not on owner minimize/restore edge cases; `AnchorTo` re-subscribes defensively (221-224) — fine, but the dialog can float orphaned if `txtOutput` is hidden by layout changes.

### ImportPreviewDialog
27. No select-all/none affordance — large imports require per-row clicking (grid at 58-117).
28. Conflict handling is rename-only (`Renamed (original: X)`, 227-230); no option to overwrite the existing job or edit the resolved name in the preview.
29. Rows with missing targets stay default-checked (244-249) — importing known-broken (auto-disabled) jobs is one OK click away; arguably should default unchecked or be visually gated.
30. Status colors are hardcoded RGB (229, 236, 241) rather than theme tokens; red/green on dark mode is fine today but bypasses `DialogTheme`.

### SettingsDialog structural
31. **Fixed 544x620 non-resizable dialog** (212-213) for ~80 settings across 6 tabs; the tab control has resize anchors (223) that are dead because the border style forbids resizing. Vault/Notifications detail panes hardcode `Width = 430` rows (550, 579, etc.) — DPI/long-text brittle.
32. `FindControl<T>` returns `null!` on miss (2308-2317) — a renamed control name string produces an NRE at first use, far from the cause.
33. Per-tab section header fonts are created inline (`new Font("Segoe UI Semibold"...)` at 379, 536, 1239, 1709, 1760...) and never disposed; ~10 leaked Font handles per dialog open (minor, GC-collected eventually, but the Appearance preview path was carefully fixed for exactly this class of issue while these were not).
34. `PopulateFontComboBox`'s monospace heuristic (2620-2629) is a name-substring list — cannot detect actual font metrics; uncommon mono fonts can't be chosen as Code Font.
35. Settings are one big save — there's no per-tab apply, no dirty indicator, and no "settings changed externally" reconciliation (dialog snapshot is from open time; a concurrent config change is overwritten wholesale by `Update` at 2663).

### Cross-cutting
36. None of the immediate-action paths (environment ops, credential deletes, preset-timeout reset, browser-data clear) write to any log/undo facility — destructive operations have confirmation prompts but no recovery.
37. AboutDialog hardcodes the author email and GitHub URL inline (40, 66) rather than sourcing from assembly metadata like the build timestamp.
