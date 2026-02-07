# SSH_Helper Application Review Findings

> Compiled from reviews by 4 specialized agents covering Frontend, UX, Scripting, and Core Architecture.
> Each item includes **Effort** (S/M/L) and **Impact** (High/Med/Low) to help prioritize.

---

## 1. Critical Issues

These items affect reliability, security, or data integrity and should be addressed first.

### 1.1 ~~GDI+ Resource Leaks in Owner-Draw Methods~~ FIXED
- **Area:** Frontend
- **Effort:** Small | **Impact:** High
- **Files:** `Form1.cs:1682-1688`, `1819-1823`, `3938`, `3967`, `4103`, `4110`
- **Issue:** `StringFormat` objects created in paint handlers (`TabControl_DrawItem`, `Dgv_Variables_RowPostPaint`, `LstOutput_DrawItem`, `TreeView_DrawNode`) are never disposed. These methods fire frequently during rendering, causing GDI+ handle pressure that can lead to handle exhaustion.
- **Fix:** Wrap each `new StringFormat()` in a `using` statement.
- **Status:** FIXED — Added `using` to all 6 StringFormat allocations in paint handlers.

### 1.2 ~~Font Objects Leaked on Theme/Settings Changes~~ FIXED
- **Area:** Frontend
- **Effort:** Small | **Impact:** High
- **Files:** `Form1.cs:979-1074` (`ApplyFontSettings`), `Form1.cs:1141` (`ApplyContextMenuFont`)
- **Issue:** ~30+ `new Font(...)` assignments replace previous fonts on controls without disposing the old ones. Calling this repeatedly (e.g., after settings changes) leaks GDI handles.
- **Fix:** Capture `control.Font` in a temp variable, assign the new font, then dispose the old one.
- **Status:** FIXED — Added `SetFont()` helper that disposes old non-system fonts. Shared font instances across similar controls. Fixed `ApplyContextMenuFont` to dispose old fonts.

### 1.3 ~~Incomplete Dispose Pattern on Form1~~ FIXED
- **Area:** Frontend
- **Effort:** Small | **Impact:** High
- **Files:** `Form1.Designer.cs:14-21`
- **Issue:** `Form1.Dispose` only disposes `components`. It does NOT dispose `_sshService` (holds SSH connection pool resources), `_uiOutputThrottler`, `_findDialog`, or unsubscribe event handlers on `_sshService`.
- **Fix:** Override `Dispose` to clean up all service references and unsubscribe events.
- **Status:** FIXED — Dispose now unsubscribes all 4 SSH service event handlers, disposes `_sshService`, `_uiOutputThrottler`, `_findDialog`, and `_credentialProvider`.

### 1.4 ~~Race Condition in BeginExecution / EndExecution~~ FIXED
- **Area:** Core
- **Effort:** Medium | **Impact:** High
- **Files:** `SshExecutionService.cs:74-90`
- **Issue:** `_cts` (CancellationTokenSource) is accessed without synchronization. Concurrent `ExecuteAsync` calls (e.g., folder execution) can race on `_cts` creation/disposal. `_isRunning` is `volatile` but the begin/end sequence is not atomic.
- **Fix:** Use `lock` or `SemaphoreSlim` around `_cts` access, or enforce single-execution by throwing if already running.
- **Status:** FIXED — Added `_executionLock` object. `BeginExecution`, `EndExecution`, and `Stop` all synchronize on it.

### 1.5 ~~Regex DoS (ReDoS) in User-Provided Patterns~~ FIXED
- **Area:** Scripting
- **Effort:** Small | **Impact:** High
- **Files:** `ExpressionEvaluator.cs:99`, `ExtractCommand.cs:53`
- **Issue:** User-provided regex patterns in `matches` conditions and `extract` commands are compiled without a timeout. Malicious or poorly-crafted patterns can cause catastrophic backtracking.
- **Fix:** Use the `Regex` constructor overload with `TimeSpan` timeout: `new Regex(pattern, options, TimeSpan.FromSeconds(5))`.
- **Status:** FIXED — Added 5-second timeout to both `ExpressionEvaluator.cs` and `ExtractCommand.cs`.

### 1.6 ~~Connection Pool Session Leasing Not Thread-Safe~~ FIXED
- **Area:** Core
- **Effort:** Medium | **Impact:** High
- **Files:** `SshConnectionPool.cs:199-217`
- **Issue:** Two callers requesting sessions for the same host simultaneously get the same `Ssh` client and both call `StartScripting()`, potentially corrupting each other's sessions. `_creationLock` only protects connection creation, not session creation.
- **Fix:** Implement exclusive "lease" semantics where a connection is checked out and unavailable to others until returned.
- **Status:** FIXED — Added `_leasedKeys` tracking. `CreateSessionAsync` leases connections exclusively; concurrent callers for the same host get a fresh connection. `ReleaseSession()` releases the lease. Both callers in `SshExecutionService` now call `ReleaseSession` in their `finally` blocks.

### 1.7 ~~Webhook SSRF Potential~~ DOCUMENTED
- **Area:** Scripting/Security
- **Effort:** Small | **Impact:** Medium
- **Files:** `WebhookCommand.cs:35-39`
- **Issue:** URL validation checks for `http`/`https` scheme but does not restrict internal/private IP ranges (127.0.0.1, 10.x.x.x, 169.254.x.x). Scripts could probe internal networks.
- **Fix:** Add private IP range validation, or document the risk prominently.
- **Status:** DOCUMENTED - Kept runtime behavior as-is by design; added explicit risk notes in `WebhookCommand.cs` and `SCRIPTING.md`.

### 1.8 ~~Silent Config Loss on Parse Error~~ FIXED
- **Area:** Core
- **Effort:** Small | **Impact:** High
- **Files:** `ConfigurationService.cs:56-68`
- **Issue:** If the config file has invalid JSON, the catch block silently returns a fresh default config. Users lose ALL presets, settings, and history without any notification.
- **Fix:** Keep a backup of the last known-good config. Show a warning to the user. Consider writing a `.bak` file before overwriting.
- **Status:** FIXED — Corrupt files are now saved as `.corrupt` backup. Added `ConfigLoadError` property. `Save()` now creates a `.bak` backup before overwriting. Form1 shows a MessageBox warning if config load fails.

---

## 2. Important Improvements

High-value improvements organized by area.

### Frontend

#### 2.1 Decompose Form1.cs (6,981 lines)
- **Effort:** Large | **Impact:** High
- **Issue:** Single class handles theming, grid management, preset tree, SSH execution, history, clipboard, find dialog, settings, updates, and 150+ event handlers.
- **Suggested extractions:**
  - `ThemeManager` (~400 lines): Light/dark colors, `ApplyTheme`, scrollbar theming, tab control drawing
  - `HostGridController` (~300 lines): DataGridView init, custom scrollbars, column/row ops, clipboard, select-all checkbox
  - `PresetTreeViewController` (~500 lines): TreeView events, drag-drop, folder reordering, custom drawing
  - `ExecutionController` (~400 lines): SSH orchestration, folder execution, connection building
  - `HistoryController` (~200 lines): Output history, per-host results, history list drawing

#### 2.2 ~~Dialogs Don't Support Dark Mode~~ FIXED
- **Effort:** Medium | **Impact:** Medium
- **Files:** `UpdateDialog.cs`, `SettingsDialog.cs`, `FolderExecutionDialog.cs`, `AboutDialog.cs`
- **Issue:** Only `FindDialog` supports dark mode. All other dialogs display with light backgrounds regardless of theme setting.
- **Fix:** Extract theme colors into a shared `ThemeColors` class that all forms consume.
- **Status:** FIXED

#### 2.3 Duplicated Unsaved-Changes Check Logic
- **Effort:** Small | **Impact:** Medium
- **Files:** `Form1.cs:2140`, `2178`, `2686`, `2718`, `4570`, `6859`
- **Issue:** The same `IsPresetDirty()` + MessageBox pattern appears 6 times.
- **Fix:** Extract to a single `bool PromptForUnsavedPresetChanges()` method.

#### 2.4 Duplicated Preset Loading Logic
- **Effort:** Small | **Impact:** Medium
- **Files:** `Form1.cs:2202-2218` vs `Form1.cs:2739-2757`
- **Issue:** Nearly identical blocks load presets into the editor from both trvPresets and trvFavorites.
- **Fix:** Extract to `LoadPresetIntoEditor(string presetName)`.

#### 2.5 SettingsDialog Uses Fragile Name-Based Control Lookup
- **Effort:** Small | **Impact:** Medium
- **Files:** `SettingsDialog.cs:157-169`
- **Issue:** Controls found via `tabGeneral.Controls["chkRememberState"]!`. A rename causes NullReferenceException at runtime.
- **Fix:** Assign to fields directly during control creation.

#### 2.6 Custom Controls Living in Designer File
- **Effort:** Small | **Impact:** Low
- **Files:** `Form1.Designer.cs:1806-2080`
- **Issue:** `BorderlessTabControl`, `ModernToolStripRenderer`, `DarkToolStripRenderer`, `DarkColorTable` are defined in the Designer file.
- **Fix:** Move to separate files under a `Controls/` or `UI/` folder.

### User Experience

#### 2.7 DataGridView Cell Editing is Destructive
- **Effort:** Medium | **Impact:** High
- **Files:** `Form1.Designer.cs:282`, `Form1.cs:2048-2059`
- **Issue:** `EditMode.EditProgrammatically` means single-click-then-type always overwrites the cell content. The `KeyPress` handler replaces existing content with only the pressed character.
- **Fix:** Switch to `EditOnKeystrokeOrF2`, or fix KeyPress to append rather than replace.

#### 2.8 Ctrl+S Has Ambiguous Behavior
- **Effort:** Small | **Impact:** High
- **Files:** `Form1.cs:6387-6394`, `Form1.Designer.cs:1372`
- **Issue:** Ctrl+S saves the preset when script editor has focus, but saves the CSV otherwise. Users will accidentally overwrite the wrong thing.
- **Fix:** Use distinct shortcuts (e.g., Ctrl+S always for CSV, Ctrl+Shift+S for preset save).

#### 2.9 "Run Selected" Button Behavior is Non-Obvious
- **Effort:** Medium | **Impact:** High
- **Files:** `Form1.cs:3070-3135`
- **Issue:** 4-way fallback chain depending on folder selection and host check state. No visual indication of which execution path will be taken.
- **Fix:** Show a tooltip/status preview on hover describing what will happen. Consider splitting into more explicit buttons.

#### 2.10 No Keyboard Shortcut for Execute
- **Effort:** Small | **Impact:** High
- **Issue:** No shortcut for the most common action. Users must click buttons.
- **Fix:** Add F5 for "Run Selected" and Shift+F5 for "Run All" (or Ctrl+Enter).

#### 2.11 Find Dialog Only Searches Output Window
- **Effort:** Medium | **Impact:** Medium
- **Files:** `Form1.cs:6408-6422`
- **Issue:** Ctrl+F only searches the output panel. Users can't search the script editor or host grid.
- **Fix:** Make Ctrl+F context-aware based on which panel has focus.

#### 2.12 Unsaved Preset Warning Blocks Navigation
- **Effort:** Medium | **Impact:** Medium
- **Files:** `Form1.cs:2127-2219`
- **Issue:** Clicking a different preset triggers a blocking MessageBox for unsaved changes. Annoying during browsing.
- **Fix:** Use a visual dirty indicator (asterisk in title) instead of blocking dialogs. Offer auto-save option.

#### 2.13 Settings Dialog is Overwhelming
- **Effort:** Medium | **Impact:** Medium
- **Files:** `SettingsDialog.cs`
- **Issue:** 50+ settings presented flat across 3 tabs. 12 individual font size controls for different UI elements.
- **Fix:** Hide granular font sizes behind an "Advanced" expander. Present only "UI Scale" + "Code Font" + "UI Font" as primary controls.

### Scripting Engine

#### 2.14 ~~IfCommand/WhileCommand Pre-Substitution Bug~~ FIXED
- **Effort:** Small | **Impact:** High
- **Files:** `IfCommand.cs:25`
- **Issue:** Variables are substituted BEFORE the expression evaluator sees the condition. A variable containing spaces corrupts the expression. E.g., `${name} == "John Doe"` becomes `John Doe == "John Doe"` which misparsens.
- **Fix:** Remove pre-substitution in `IfCommand.cs` and `WhileCommand.cs`. The `ExpressionEvaluator.ResolveValue` already handles `${var}` references.
- **Status:** FIXED - Removed pre-substitution in both commands and evaluate raw expressions through `ExpressionEvaluator`.

#### 2.15 ~~`json.pop()` and `json.shift()` Don't Modify the Array~~ FIXED
- **Effort:** Small | **Impact:** Medium
- **Files:** `JsonFunctions.cs:445-456`, `496-504`
- **Issue:** These return the element but don't remove it. The doc comment says "Remove and return" but implementation only returns. Semantic mismatch with JavaScript/Python conventions.
- **Fix:** Make these destructive (modify and return updated array), or rename to `last()` and `first()`.
- **Status:** FIXED — `json.pop()` / `json.shift()` are now destructive for writable top-level array references, and non-destructive `json.last()` / `json.first()` were added.

#### 2.16 ~~Duplicated JSON Function Dispatch~~ FIXED
- **Effort:** Small | **Impact:** Medium
- **Files:** `SetCommand.cs:242-303` vs `JsonUtilities.cs:360-421`
- **Issue:** `TryDispatchJsonFunction` switch statement is duplicated. Adding a new JSON function requires updating both.
- **Fix:** Remove the duplicate in `SetCommand.cs` and delegate to `JsonUtilities.TryDispatchJsonFunction`.
- **Status:** FIXED - Removed local duplicate dispatch in `SetCommand` and routed JSON expression handling through shared `JsonUtilities`.

#### 2.17 ~~Single-Operator Arithmetic with No Warning~~ FIXED
- **Effort:** Medium | **Impact:** Medium
- **Files:** `SetCommand.cs:206-211`, `306-366`
- **Issue:** `a + b * c` silently evaluates incorrectly (splits on `*` first). No warning when multi-operator expression is used.
- **Fix:** Detect multiple operators and emit a warning, or implement proper operator precedence parsing.
- **Status:** FIXED - Replaced split-based arithmetic with precedence parsing for `+ - * / %` and parentheses (with divide/mod-by-zero warnings preserved).

#### 2.18 ~~Error Swallowing with `on_error: continue`~~ FIXED
- **Effort:** Small | **Impact:** Medium
- **Files:** `ScriptExecutor.cs:188-189`
- **Issue:** When `on_error: continue` fires, there's no way for subsequent steps to detect that an error occurred.
- **Fix:** Set a `_last_error` context variable when an error is caught, allowing `if: _last_error is not empty`.
- **Status:** FIXED - Added `_last_error` lifecycle in executor/context: set on suppressed error, clear on subsequent success.

#### 2.19 ~~`IsYamlScript` Detection False Positives~~ FIXED
- **Effort:** Small | **Impact:** Medium
- **Files:** `ScriptParser.cs:49-93`
- **Issue:** Commands containing `name:` or `description:` in the first 10 lines are misidentified as YAML scripts.
- **Fix:** Require `---` YAML document marker, or a more distinctive indicator.
- **Status:** FIXED - `IsYamlScript` now uses stronger indicators (`---`, `steps:`, `vars:`, known `- step:` entries) and no longer treats metadata-only keys like `name:` / `description:` / `version:` as YAML. Added unit tests for metadata false positives.

### Core Architecture

#### 2.20 Massive Error-Handling Duplication in SshExecutionService
- **Effort:** Medium | **Impact:** Medium
- **Files:** `SshExecutionService.cs:699-753` vs `791-846`
- **Issue:** `ExecuteSingleHost` and `ExecuteScriptOnHost` have identical 55+ line exception handling blocks.
- **Fix:** Extract a common `HandleHostExecutionError()` method.

#### 2.21 Duplicated Code Between ExecuteWithPool and ExecuteWithoutPool
- **Effort:** Medium | **Impact:** Medium
- **Files:** `SshExecutionService.cs:1053-1128` vs `1134-1276`
- **Issue:** ~80 lines of shared session setup logic (debug mode, events, headers, batch execution).
- **Fix:** Extract shared configuration into a helper. Abstract connection acquisition behind a factory.

#### 2.22 Duplicated ApplyAlgorithmSettings and TryLoginWithAgent
- **Effort:** Small | **Impact:** Medium
- **Files:** `SshExecutionService.cs:1392-1407` / `SshConnectionPool.cs:323-338`, `SshExecutionService.cs:1409-1423` / `SshConnectionPool.cs:415-423`
- **Issue:** Both classes have identical copies.
- **Fix:** Move to a shared `SshConnectionHelper` class.

#### 2.23 No Interfaces for Key Services
- **Effort:** Medium | **Impact:** Medium
- **Files:** `SshExecutionService.cs`, `SshConnectionPool.cs`, `ConfigurationService.cs`
- **Issue:** Concrete classes without interfaces. Unit testing the SSH execution pipeline is impossible without real SSH connections.
- **Fix:** Introduce `ISshExecutionService`, `IConnectionPool`, `IConfigurationService`.

#### 2.24 Blocking `.GetAwaiter().GetResult()` Throughout Execution Pipeline
- **Effort:** Large | **Impact:** Medium
- **Files:** `SshExecutionService.cs:862`, `907`, `996`, `1043`, `1064`, `1120`, `1244`, `1269`
- **Issue:** Extensive sync-over-async via `.GetAwaiter().GetResult()`. Works because wrapped in `Task.Run()`, but fragile maintenance trap.
- **Fix:** Make the entire call chain properly async, or add `ConfigureAwait(false)` to all internal awaits.

#### 2.25 Timeout Detection via String Matching
- **Effort:** Small | **Impact:** Medium
- **Files:** `SshShellSession.cs:274-278`, `394-397`, `644-648`
- **Issue:** Timeout detection relies on matching exception message strings. A Rebex library update could break all timeout handling.
- **Fix:** Centralize into a single `IsTimeoutException(Exception ex)` helper.

---

## 3. Nice-to-Have Enhancements

Lower priority polish and feature items.

### Frontend
| # | Item | Effort | Impact | Files |
|---|------|--------|--------|-------|
| 3.1 | Move `NativeMethods` and `PresetNodeTag` to own files | S | Low | `Form1.cs:17-73` |
| 3.2 | Fix drag-drop highlight color for dark mode (hardcoded `LightBlue`) | S | Low | `Form1.cs:2359`, `2816` |
| 3.3 | Extract magic number `28` (row height) to a constant | S | Low | `Form1.cs:344`, `2033`, `6745`, `6766` |
| 3.4 | Remove empty `tsbPassword_Click` handler | S | Low | `Form1.cs:6973-6976` |
| 3.5 | Cache frequently-used GDI objects in TreeView_DrawNode | S | Low | `Form1.cs:3983-4112` |
| 3.6 | Add DPI scaling to FolderExecutionDialog and UpdateDialog | S | Med | `FolderExecutionDialog.cs`, `UpdateDialog.cs` |

### User Experience
| # | Item | Effort | Impact | Files |
|---|------|--------|--------|-------|
| 3.7 | Add first-run welcome/onboarding placeholder text | S | Med | `Form1.cs` |
| 3.8 | Add timestamps/host count to history list entries | S | Low | `Form1.cs:3920+` |
| 3.9 | Fix context menu mnemonic conflicts (`&A`, `&I`) | S | Low | Designer menus |
| 3.10 | Add keyboard shortcuts to toggle panels (Ctrl+Shift+H, etc.) | S | Med | `Form1.cs` |
| 3.11 | Rename "Export Preset" to "Copy Preset to Clipboard" | S | Low | Context menu |
| 3.12 | Make FolderExecutionDialog resizable | S | Med | `FolderExecutionDialog.cs` |
| 3.13 | Show per-host connection status during SSH execution | M | Med | `SshExecutionService.cs` |
| 3.14 | Improve script editor undo (multi-level or RichTextBox) | M | Med | `Form1.cs` |
| 3.15 | Move password field out of main toolbar | M | Low | `Form1.Designer.cs` |
| 3.16 | Optimize Tab order for primary workflow | S | Low | `Form1.Designer.cs` |

### Scripting
| # | Item | Effort | Impact | Files |
|---|------|--------|--------|-------|
| 3.17 | ~~Add `break` and `continue` as explicit step types~~ IMPLEMENTED | S | Med | `ScriptStep.cs`, `ScriptParser.cs`, `BreakCommand.cs`, `ContinueCommand.cs` |
| 3.18 | ~~Add `elif` support~~ IMPLEMENTED | M | Med | `ScriptStep.cs`, `IfCommand.cs`, `ScriptParser.cs` |
| 3.19 | ~~Add string functions: `replace()`, `split()`, `join()`, `substring()`~~ IMPLEMENTED | M | Med | `SetCommand.cs` |
| 3.20 | ~~Add `sort()` function for arrays~~ IMPLEMENTED | S | Med | `SetCommand.cs` |
| 3.21 | ~~Warn on unknown YAML keys (typo detection)~~ IMPLEMENTED | S | Med | `ScriptParser.cs`, `Form1.cs` |
| 3.22 | ~~Fix ForEach JSON string quoting (`ToJsonString` -> `JsonNodeToStringValue`)~~ IMPLEMENTED | S | Med | `ForeachCommand.cs` |
| 3.23 | ~~Make WhileCommand max iterations configurable per-step~~ IMPLEMENTED | S | Low | `WhileCommand.cs`, `ScriptParser.cs` |
| 3.24 | ~~Make `_timestamp` resolve dynamically instead of at context creation~~ IMPLEMENTED | S | Low | `ScriptContext.cs` |
| 3.25 | ~~Cache regex in `SubstituteVariables` hot path~~ IMPLEMENTED | S | Low | `ScriptContext.cs` |
| 3.26 | ~~Add `try/catch` block structure for scripts~~ IMPLEMENTED | L | Med | `ScriptStep.cs`, `ScriptParser.cs`, `TryCommand.cs`, `ScriptExecutor.cs` |

### Core
| # | Item | Effort | Impact | Files |
|---|------|--------|--------|-------|
| 3.27 | Flush pending output in `OutputThrottler.Dispose` | S | Low | `OutputThrottler.cs:90-97` |
| 3.28 | Change `SshTimeoutOptions.Default` to singleton | S | Low | `SshTimeoutOptions.cs:37` |
| 3.29 | Optimize `PromptDetector` LINQ on hot path | S | Low | `PromptDetector.cs:203` |
| 3.30 | Consolidate duplicate regex in `StripPagerArtifacts` | S | Low | `TerminalOutputProcessor.cs:130-138` |
| 3.31 | Add test coverage for PromptDetector, ConfigurationService, SshTimeoutOptions | M | Med | `SSH_Helper.Tests/` |
| 3.32 | Clear `ExecutionResult.Exception` after extracting info (memory) | S | Low | `ExecutionResult.cs:12` |
| 3.33 | Ensure passwords excluded from serialized Variables dictionary | S | Med | `HostConnection.cs:11` |

---

## Quick Reference: Top 10 Priorities

| # | Item | Area | Effort | Status |
|---|------|------|--------|--------|
| 1 | GDI+ StringFormat leaks in paint handlers | Frontend | S | FIXED |
| 2 | Font disposal on theme changes | Frontend | S | FIXED |
| 3 | Silent config loss on parse error | Core | S | FIXED |
| 4 | ReDoS in user-provided regex patterns | Scripting | S | FIXED |
| 5 | IfCommand pre-substitution bug | Scripting | S | FIXED |
| 6 | Ctrl+S ambiguous behavior | UX | S | Open |
| 7 | Add execute keyboard shortcut (F5) | UX | S | Open |
| 8 | Form1 Dispose pattern incomplete | Frontend | S | FIXED |
| 9 | Race condition in BeginExecution/EndExecution | Core | M | FIXED |
| 10 | DataGridView cell editing is destructive | UX | M | Open |
