# TODO

## 5. Base Environment Rebase and Restore
- [x] 5.1 Extend environment persistence with a separate base-environment value and normalization rules.
- [x] 5.2 Update environment service operations so base environment survives rename/delete and can be manually rebased.
- [x] 5.3 Update preset/manual environment switching in `Form1` to preserve base on preset loads and restore it on no-environment presets.
- [x] 5.4 Add the conditional toolbar base-environment indicator and refresh/status behavior.
- [x] 5.5 Amend OpenSpec/docs for persisted base-environment semantics.
- [x] 5.6 Add focused regression tests for base-environment persistence, preset-load decisions, and indicator visibility.
- [x] 5.7 Run verification and capture outcomes.

## 4. Script Load Environment Switching
- [x] 4.1 Add OpenSpec change artifacts for script-declared environment switching.
- [x] 4.2 Extend the script model/parser/editor metadata with the new top-level `environment` key.
- [x] 4.3 Consolidate preset editor loading in `Form1` and apply script-declared environment switching on load.
- [x] 4.4 Document the new root option and load-time behavior in `SCRIPTING.md`.
- [x] 4.5 Add focused parser/editor regression tests.
- [x] 4.6 Run focused verification and capture outcomes.

## 3. Missing Column Warning Script Suppression
- [x] 3.1 Add a top-level YAML script option to suppress the missing-column warning.
- [x] 3.2 Respect the new option during single-preset and folder execution preflight checks.
- [x] 3.3 Document the new option in `SCRIPTING.md`.
- [x] 3.4 Add parser/dependency-analysis regression tests.
- [x] 3.5 Run focused tests and capture outcome.

## 2. Prompt Spacing Bug (zsh PROMPT_SP Chunk Split)
- [x] 2.1 Confirm and document root cause in the live shell streaming path.
- [x] 2.2 Implement boundary-safe cleanup for split zsh prompt redraw artifacts before UI/history emission.
- [x] 2.3 Add regression tests for `%` + clear-sequence + prompt split across chunks.
- [x] 2.4 Run focused tests and capture outcome.

## 1. Space Loss Bug (Chunked Output)
- [x] 1.1 Confirm and document root cause in output normalization pipeline.
- [x] 1.2 Add targeted normalization option to preserve trailing spaces on unfinished chunk lines.
- [x] 1.3 Use the new option in live chunk UI emission path.
- [x] 1.4 Add regression tests for split chunks (`set ` + `resource ...`).
- [x] 1.5 Run focused tests and capture outcome.

## 6. Folder Base Environment Inheritance
- [x] 6.1 Add OpenSpec change artifacts for folder-level base-environment overrides.
- [x] 6.2 Persist folder base-environment metadata and normalize invalid values.
- [x] 6.3 Add preset-folder context-menu assignment UI with inherited fallback behavior.
- [x] 6.4 Apply resolved folder-base environments when loading presets and selecting/executing folders.
- [x] 6.5 Keep folder base-environment references valid across folder rename/delete and environment rename/delete flows.
- [x] 6.6 Add focused regression tests for folder base resolution and persistence.
- [x] 6.7 Run verification and capture outcomes.

## 7. Folder Base Menu Click Regression
- [x] 7.1 Confirm why the folder base environment context-menu entry does not open.
- [x] 7.2 Patch the menu item so a normal click opens its dropdown.
- [x] 7.3 Run verification and capture outcome.

## 8. Folder Base Menu Interaction Rework
- [x] 8.1 Confirm the click-to-open submenu patch still does not work in the real UI flow.
- [x] 8.2 Replace the nested submenu interaction with a direct chooser launched from the context-menu command.
- [x] 8.3 Run verification and capture outcome.

## 9. Folder Base Chooser Crash
- [x] 9.1 Confirm the secondary chooser menu is crashing in the WinForms context-menu disposal path.
- [x] 9.2 Replace the secondary chooser menu with a stable dialog-based selection flow.
- [x] 9.3 Run verification and capture outcome.

## 10. Folder Summary Base Environment Refresh
- [x] 10.1 Confirm the folder details pane can be ambiguous or stale when switching folders with different base-environment sources.
- [x] 10.2 Make the folder summary explicitly show inherited source folders and refresh selected-folder details when environment state changes.
- [x] 10.3 Run verification and capture outcome.

## 11. Folder Click Summary Refresh
- [x] 11.1 Confirm folder-to-folder clicks can leave the first folder summary in the command pane.
- [x] 11.2 Make folder click handling refresh the folder summary even when `AfterSelect` does not deliver the expected update.
- [x] 11.3 Run verification and capture outcome.

## 12. Read-Only Folder Summary Refresh
- [x] 12.1 Confirm the command editor can block programmatic folder-summary updates once the first folder leaves it read-only.
- [x] 12.2 Patch the editor control so programmatic text updates still work while preserving read-only mode for user edits.
- [x] 12.3 Add focused regression tests for read-only programmatic updates.
- [x] 12.4 Run verification and capture outcome.

## 13. Manual Environment Switch Folder Refresh
- [x] 13.1 Confirm folder details refresh too early during manual environment/base switches, leaving the global base label stale.
- [x] 13.2 Refresh selected-folder details after the final base environment is applied in manual environment-switch flows.
- [x] 13.3 Run verification and capture outcome.

## 14. Preset Environment Switch Status Message
- [x] 14.1 Confirm preset-load environment handling only reports base restores and missing environments, not successful declared-environment switches.
- [x] 14.2 Add a shared formatter/helper for preset-load environment status messages and use it for restore/switch/missing cases.
- [x] 14.3 Add focused regression tests for preset-load environment status text.
- [x] 14.4 Run focused verification and capture outcome.

## 15. Hosts File Header Indicator
- [x] 15.1 Confirm the current hosts header and CSV state transitions that should drive a filename/unsaved indicator.
- [x] 15.2 Add a hosts-file indicator that shows the current filename and whether the grid is unsaved/new.
- [x] 15.3 Add focused regression tests for the indicator formatting.
- [x] 15.4 Run verification and capture outcome.

## 16. Environment CSV Drift Detection
- [x] 16.1 Add OpenSpec change artifacts for environment CSV freshness tracking and stale-snapshot handling.
- [x] 16.2 Persist CSV fingerprint metadata with environment and saved-state host snapshots.
- [x] 16.3 Detect backing-file drift when switching environments and offer a safe reload path from disk.
- [x] 16.4 Show active hosts-file drift state in the hosts header and status messaging.
- [x] 16.5 Add focused regression tests for fingerprint persistence, drift evaluation, and indicator text.
- [x] 16.6 Run verification and capture outcome.

## Review
- Added OpenSpec change `update-script-load-environment` with proposal, implementation checklist, and spec deltas for load-time script environment selection.
- Added a top-level YAML `environment` key to the script model/parser/editor metadata without changing YAML auto-detection semantics for metadata-only text.
- Consolidated preset loading into a shared `Form1` helper and applied script-declared environment switching across tree selection, favorites, import/duplicate, and fallback load flows.
- Missing script-declared environments now leave the current environment unchanged and emit a non-blocking status-bar message.
- Documented the new root option in `SCRIPTING.md` and added parser/autocomplete/highlighter regression coverage.
- Hardened [SSH_Helper.csproj] against repo-local generated source leakage by excluding `artifacts/**` from default compile items, preventing duplicate assembly-attribute build failures after local verification runs.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-build --filter "FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~YamlSshSyntaxHighlighterTests"` passed (152/152).
- Verification: `openspec validate update-script-load-environment --strict --no-interactive` passed.
- Added top-level YAML flag `suppress_missing_column_warning: true` to the script model/parser and exposed it through dependency analysis.
- Updated `ValidateColumnDependencies(...)` to analyze presets individually so suppressed scripts skip the dialog while unsuppressed presets in the same run still trigger it.
- Documented the new header option in `SCRIPTING.md` with an optional-column example.
- Added parser/dependency-analysis regression tests for the new flag and metadata detection behavior.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptDependencyAnalyzerTests"` passed (150/150). Build emitted copy warnings because `SSH_Helper.exe` was running, but tests completed successfully.
- Prompt spacing bug root cause confirmed: zsh `PROMPT_SP` redraw artifacts were being stripped per chunk, so a split `%` + spaces/CR clear sequence leaked into the live output buffer.
- Implemented `StripZshPromptSpStreaming(..., ref carry)` and applied it in `SshShellSession` so ambiguous prompt-redraw suffixes are held across chunk boundaries and flushed safely at command end.
- Added regression tests for whole-sequence cleanup, split-chunk cleanup, legitimate mid-line percent preservation, and end-of-stream flushing.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~TerminalOutputProcessorTests"` passed (51/51). Build emitted copy warnings because `SSH_Helper.exe` was running, but tests completed successfully.
- Root cause confirmed: chunk-level normalization trimmed trailing spaces on unfinished chunk lines.
- Implemented `Normalize(..., preserveTrailingSpacesOnFinalLine: true)` for live chunk rendering in `SshShellSession`.
- Added regression tests in `TerminalOutputProcessorTests` for trailing-space preservation and split-chunk word join prevention.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~TerminalOutputProcessorTests"` passed (47/47).
- Added persisted `BaseEnvironment` configuration state and taught environment normalization to default/fix it alongside `ActiveEnvironment`.
- Updated `EnvironmentService` so manual rebases can persist a base environment and rename/delete/import flows keep that base valid.
- Updated `Form1` preset-load behavior so `environment:` presets switch only the active environment, while presets without `environment` restore the active environment back to the base environment.
- Added a conditional toolbar indicator that shows `Base: <name>` only while the active environment differs from the base environment.
- Added focused regression coverage for base-environment persistence plus utility tests for preset-load decisions and indicator visibility.
- Hardened both project files against generated-source leakage from repo-local `bin/**`, `obj/**`, and `artifacts/**` verification outputs.
- Verification: `dotnet build SSH_Helper.csproj` was attempted but failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 15128).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~EnvironmentServiceTests|FullyQualifiedName~PresetEnvironmentLoadPlannerTests|FullyQualifiedName~BaseEnvironmentIndicatorFormatterTests" -p:BaseOutputPath=artifacts\\verify-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-tests\\obj\\` passed (22/22).
- Verification: `openspec validate update-script-load-environment --strict --no-interactive` passed.
- Added OpenSpec change `update-folder-base-environments` with environment-management and preset-organization deltas for folder-level base-environment overrides.
- Extended `FolderInfo`/`PresetManager` with persisted folder base-environment metadata, invalid-reference cleanup on load, and repair helpers for environment rename/delete flows.
- Added a `Folder Base Environment` preset-folder context-menu submenu with inherited fallback labeling and immediate folder summary/environment refresh behavior.
- Preset loads now resolve environment precedence as global base -> nearest folder base -> script-declared preset environment, and folder selection/execution now applies the resolved folder base before use.
- Added focused regression coverage for pure folder-base resolution and temp-config preset-manager persistence/repair flows.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests"` passed (9/9).
- Verification: `openspec validate update-folder-base-environments --strict --no-interactive` passed.
- Patched the `Folder Base Environment` context-menu entry so clicking it explicitly opens the dropdown instead of relying on implicit submenu behavior.
- Replaced the fragile nested `Folder Base Environment` submenu interaction with a direct chooser context menu launched after the parent menu closes.
- Verification: `dotnet build SSH_Helper.csproj` passed after the chooser rework.
- Confirmed the second-stage chooser `ContextMenuStrip` could be disposed while WinForms was still closing the parent context menu, causing the reported `ObjectDisposedException`.
- Replaced the folder base chooser with a modal selection dialog built on the existing `ScriptChooseDialog` path, keeping the interaction outside the context-menu disposal lifecycle.
- Verification: `dotnet build SSH_Helper.csproj` passed after the dialog-based crash fix.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests"` passed (9/9).
- Updated folder-detail base-environment text to include the inherited source folder path, so switching between folders shows which ancestor is supplying the effective base.
- Added selected-folder summary refresh on environment changes so the details pane stays synchronized while folder-driven environment switching occurs.
- Added focused formatter regression tests for folder summary and inherit-choice labels.
- Verification: `dotnet build SSH_Helper.csproj` passed with one retry warning because `SSH_Helper.dll` was in use during the copy step.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~FolderBaseEnvironmentSummaryFormatterTests"` passed (14/14).
- Confirmed folder-to-folder clicks could leave the first folder summary visible because the custom TreeView click flow could miss the expected `AfterSelect`-driven refresh.
- Added a shared folder-selection handler plus click-path fallback refresh in both preset and favorites trees so folder clicks update the command pane even when WinForms selection events are inconsistent.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~FolderBaseEnvironmentSummaryFormatterTests"` initially failed because `obj\\Debug\\net8.0-windows\\SSH_Helper.dll` was locked by another process.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-build --filter "FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~FolderBaseEnvironmentSummaryFormatterTests"` passed (14/14).
- Confirmed the real blocker was the Scintilla-based command editor staying read-only after the first folder summary, which prevented later programmatic text replacements from taking effect.
- Patched `ScintillaScriptEditorControl` so `Text` and `Clear()` temporarily disable read-only during programmatic updates and then restore the prior read-only state.
- Added focused UI regression tests covering programmatic `Text` replacement and `Clear()` while the editor remains read-only.
- Verification: `dotnet build SSH_Helper.csproj` passed with apphost copy retry warnings because `SSH_Helper.exe` was running.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests|FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~FolderBaseEnvironmentSummaryFormatterTests"` passed (40/40) with the same running-exe copy warnings.
- Confirmed manual environment switches could refresh folder details too early from the environment-changed event, before the new base environment was persisted, leaving the folder summary on the old global-base label.
- Refreshed selected-folder details after manual environment/base-switch completion and after environment-management flows that keep a folder summary visible.
- Verification: `dotnet build SSH_Helper.csproj` failed because `obj\\Debug\\net8.0-windows\\SSH_Helper.dll` and `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` were locked by a running `SSH_Helper` process (PID 11172).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-env-refresh\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-env-refresh\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests|FullyQualifiedName~PresetBaseEnvironmentResolverTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~FolderBaseEnvironmentSummaryFormatterTests" -p:BaseOutputPath=artifacts\\verify-env-refresh-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-env-refresh-tests\\obj\\` passed (40/40).
- Extracted preset-load environment status text into `PresetEnvironmentStatusFormatter` so restore, successful switch, and missing-environment notifications stay consistent.
- Added the missing success message for preset-declared environment switches, emitted only after `TrySwitchEnvironment(...)` succeeds.
- Added focused formatter regression tests for global-base restore, folder-base restore, successful environment switch, and missing-environment messaging.
- Verification: `dotnet build SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 56684).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-preset-switch\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-preset-switch\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetEnvironmentLoadPlannerTests|FullyQualifiedName~PresetEnvironmentStatusFormatterTests" -p:BaseOutputPath=artifacts\\verify-preset-switch-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-preset-switch-tests\\obj\\` passed (8/8).
- Added `HostsFileIndicatorFormatter` and wired the hosts header label to show `Hosts: <file>` or `Hosts: <file> (unsaved)` with `Unsaved` fallback when no backing CSV path exists.
- Refreshed the hosts header through the shared host-count/selection paths and the remaining save, column-edit, delete-cell, and restore-state transitions that change CSV identity or dirty state without changing host counts.
- Adjusted the hosts header title label to fill available space with ellipsis so longer filenames do not crowd out the host count on the right.
- Added focused regression tests for missing-path, clean-file, and dirty-file indicator formatting.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostsFileIndicatorFormatterTests"` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 59064).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-hosts-header\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-hosts-header\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostsFileIndicatorFormatterTests" -p:BaseOutputPath=artifacts\\verify-hosts-header-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-hosts-header-tests\\obj\\` passed (3/3).
- Added OpenSpec change `update-environment-csv-sync` covering persisted CSV fingerprints, stale-snapshot detection on environment activation, reload prompting, and hosts-header drift indicators.
- Extended environment snapshots and remembered application state with `LastCsvFingerprint`, then persisted that metadata through environment save/load/import flows and current-grid saves.
- Added `CsvFileSyncEvaluator` plus switch-time stale-file handling in `Form1` so activating an environment now detects changed or missing backing CSVs, prompts to reload when the file changed, and can refresh the environment snapshot directly from disk.
- Expanded the hosts header indicator to show `disk changed` and `missing on disk` states in addition to `unsaved`, and report reload/stale outcomes through manual environment-switch status messages.
- Added focused regression coverage for environment fingerprint persistence, stale-file evaluation, and expanded hosts-file indicator text.
- Verification: `dotnet build SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 59064).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-env-csv-sync\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-env-csv-sync\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~EnvironmentServiceTests|FullyQualifiedName~HostsFileIndicatorFormatterTests|FullyQualifiedName~CsvFileSyncEvaluatorTests" -p:BaseOutputPath=artifacts\\verify-env-csv-sync-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-env-csv-sync-tests\\obj\\` passed (26/26).
- Verification: `openspec validate update-environment-csv-sync --strict --no-interactive` passed.
- Root cause confirmed: blank top-level lines were treated as an empty identifier, so the provider returned every root key whenever the popup was refreshed after header edits or other non-manual caret moves.
- Split autocomplete invocation into automatic vs manual blank-line behavior so `Ctrl+Space` can still offer root keys on an empty top-level line, while normal typing/refresh paths suppress that noisy popup.
- Added focused regression tests for provider-level blank-line root completion behavior and the Scintilla editor's auto-vs-manual popup integration.
- Verification: `dotnet build SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 48888).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-autocomplete\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-autocomplete\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -p:BaseOutputPath=artifacts\\verify-autocomplete-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-autocomplete-tests\\obj\\` passed (56/56).
- Refined the blank-line root autocomplete rule after user feedback: automatic root-key suggestions now still appear in the top-level metadata/header area, but only until the first top-level `vars:` or `steps:` section is reached.
- Kept the post-section suppression for blank-line auto-popup behavior and preserved explicit `Ctrl+Space` root-key suggestions anywhere at the top level.
- Added regression coverage for provider and Scintilla popup behavior before `vars:` / `steps:` and after those sections.
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-header-autocomplete\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-header-autocomplete\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -p:BaseOutputPath=artifacts\\verify-header-autocomplete-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-header-autocomplete-tests\\obj\\` passed (58/58).
- Confirmed the preset header had no selection/dirty indicator yet, while `IsPresetDirty()` already defined the exact unsaved-state rules to reuse.
- Added `PresetHeaderIndicatorFormatter` plus a shared `Form1` header refresh path so the presets header now shows the active preset or folder and appends `(unsaved)` during editor drift.
- Wired the preset header refresh to command/name/timeout edits and to preset save/load/rename/folder-summary transitions, and let the header label auto-ellipsis long names.
- Added focused regression tests for clean default, clean preset, dirty preset, folder selection, and unnamed dirty-editor formatter cases.
- Verification: `dotnet build SSH_Helper.csproj` failed because `obj\\Debug\\net8.0-windows\\SSH_Helper.dll` was locked by another process.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetHeaderIndicatorFormatterTests"` passed (5/5).
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-preset-header\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-preset-header\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetHeaderIndicatorFormatterTests" -p:BaseOutputPath=artifacts\\verify-preset-header-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-preset-header-tests\\obj\\` passed (5/5).
- User follow-up confirmed the first preset indicator landed in the presets pane header, not in the active editor header where edits are made.
- Mirrored the dirty indicator into the visible script editor header by switching the section label to `Commands (unsaved)` and the button text to `Save*` while `IsPresetDirty()` is true.
- Extended the formatter coverage for the visible command-header and save-button labels.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetHeaderIndicatorFormatterTests"` passed (9/9).
- Root cause confirmed for the autocomplete follow-up: when a completion popup was already open, caret movement only repositioned it and never re-ran completion for the new caret context, so header/root suggestions could visually follow the caret below `vars:` / `steps:`.
- Updated `ScintillaScriptEditorControl` to remember the active blank-line completion mode and refresh the visible popup on selection changes, which hides stale root suggestions once the caret moves into a suppressed context.
- Added a focused WinForms regression test covering a root popup opened in the header and then moved to a blank line after `steps:`.
- Verification: `dotnet build SSH_Helper.csproj` passed with apphost copy retry warnings because `SSH_Helper.exe` was running (PID 60432).
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests|FullyQualifiedName~ScriptAutocompleteProviderTests"` passed (59/59).
- User correction narrowed the requirement further: root-level autocomplete must stay suppressed below top-level `vars:` / `steps:` even when completion is triggered manually with `Ctrl+Space`.
- Removed the provider/editor blank-line manual override so manual completion now follows the same post-section suppression rule as automatic popup refresh.
- Updated focused regression coverage so provider/editor tests now assert that a blank top-level line after `steps:` stays hidden for both auto-popup and `Ctrl+Space`.
- Verification: `dotnet build SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 73144).
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests|FullyQualifiedName~ScriptAutocompleteProviderTests"` failed because `obj\\Debug\\net8.0-windows\\SSH_Helper.dll` was locked by the same running process.
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-autocomplete-manual\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-autocomplete-manual\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests|FullyQualifiedName~ScriptAutocompleteProviderTests" -p:BaseOutputPath=artifacts\\verify-autocomplete-manual-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-autocomplete-manual-tests\\obj\\` passed (59/59).

## 17. YAML Root Autocomplete Noise
- [x] 17.1 Confirm why top-level/root key suggestions appear on blank lines below the metadata header and around step editing.
- [x] 17.2 Limit blank-line root suggestions to explicit/manual completion while keeping typed-prefix and step-scope suggestions intact.
- [x] 17.3 Add focused regression tests for auto vs manual blank-line root completions.
- [x] 17.4 Run focused verification and capture outcome.

## 18. Header Region Root Autocomplete
- [x] 18.1 Refine blank-line root autocomplete so the metadata/header area still auto-suggests top-level keys before `vars:` or `steps:`.
- [x] 18.2 Keep blank-line auto suggestions suppressed once the script is at or below top-level `vars:` / `steps:` sections, while preserving manual `Ctrl+Space`.
- [x] 18.3 Add focused regression tests for header-region vs post-section blank-line completion behavior.
- [x] 18.4 Run focused verification and capture outcome.

## 19. Preset Dirty Header Indicator
- [x] 19.1 Confirm the preset header states and reuse the existing preset dirty rules for indicator behavior.
- [x] 19.2 Add a preset header indicator that shows the active preset or folder and appends an unsaved marker when the editor is dirty.
- [x] 19.3 Add focused regression tests for the preset indicator formatting.
- [x] 19.4 Run focused verification and capture outcome.

## 20. Visible Preset Dirty Indicator
- [x] 20.1 Correct the preset dirty indicator placement so it appears in the active editor header while editing.
- [x] 20.2 Reuse the existing dirty-state rules in the visible editor header text without regressing the presets-pane label.
- [x] 20.3 Extend focused regression tests for the visible editor indicator text.
- [x] 20.4 Run focused verification and capture outcome.

## 21. Root Autocomplete Popup Follow-Up
- [x] 21.1 Confirm why root-level completion items still appear when the caret moves below top-level `vars:` / `steps:` content.
- [x] 21.2 Patch the popup refresh/hide behavior so stale root suggestions do not persist in suppressed contexts.
- [x] 21.3 Add focused regression coverage for caret-move/update flows after a root popup is already visible.
- [x] 21.4 Run focused verification and capture outcome.

## 22. Post-Section Manual Root Autocomplete Suppression
- [x] 22.1 Confirm the remaining root autocomplete path below `vars:` / `steps:` is the explicit/manual blank-line request flow.
- [x] 22.2 Remove blank-line root suggestions after `vars:` / `steps:` for both automatic and manual popup requests while preserving valid scoped completions.
- [x] 22.3 Update focused provider/editor regression coverage for the corrected manual behavior.
- [x] 22.4 Run focused verification and capture outcome.

## 23. Trailing Blank Line Tab Indent
- [x] 23.1 Confirm why pressing `Tab` on a trailing blank line indents the previous line instead of the current blank line.
- [x] 23.2 Patch indentation line targeting so a final blank line after a newline is treated as its own editable line.
- [x] 23.3 Add focused regression coverage for utility/control Tab behavior on a trailing blank line.
- [x] 23.4 Run focused verification and capture outcome.

## 24. Table Column Highlight Consistency
- [x] 24.1 Confirm the current syntax-highlighting gap for nested `table.columns` keys and keep the fix scoped to editor coloring only.
- [x] 24.2 Patch YAML highlighting so nested table-column keys render consistently with other recognized option keys.
- [x] 24.3 Add focused regression coverage for nested table-column key highlighting.
- [x] 24.4 Run focused verification and capture outcome.

## Review Addendum
- Root cause confirmed for the table-column highlighting inconsistency: the editor only colored top-level keys, step commands, and global step-option keys, so nested `table.columns` keys like `header` and `field` were left white.
- Extended the YAML highlighter's option-key set with nested table-column keys and taught list-item mappings like `- header:` to render as option keys when they are not actual step commands.
- Added focused regression tests for both `- header:` and `field:` under `table.columns`.
- Verification: `dotnet build SSH_Helper.csproj` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~YamlSshSyntaxHighlighterTests"` passed (5/5).
- Root cause confirmed for the trailing blank-line Tab bug: `EditorTextUtilities.GetLineStartIndices(...)` did not create a line-start entry for a final newline, so a caret on the trailing blank line was mapped back to the previous content line during indentation.
- Patched trailing line-start enumeration so a final blank line is treated as its own line target for indentation edits.
- Added focused regression coverage at both the utility layer and the Scintilla control layer for pressing `Tab` on a trailing blank line.
- Verification: `dotnet build SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 9196, plus .NET Host child processes).
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~EditorTextUtilitiesTests|FullyQualifiedName~ScintillaScriptEditorControlTests"` failed for the same locked-output reason while rebuilding the app project.
- Verification: `dotnet build SSH_Helper.csproj -p:BaseOutputPath=artifacts\\verify-trailing-tab\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-trailing-tab\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~EditorTextUtilitiesTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -p:BaseOutputPath=artifacts\\verify-trailing-tab-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-trailing-tab-tests\\obj\\` passed (41/41).
