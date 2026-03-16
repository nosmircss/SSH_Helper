# Simplification Plan: SSH_Helper Codebase

## Context
The codebase has grown organically with significant code duplication across services and within Form1.cs. This plan targets behavior-preserving simplifications — extracting shared utilities, eliminating copy-paste duplication, and fixing obvious inefficiencies. No features are added or removed.

---

## Wave 1: Shared Utilities (Cross-Service Duplication)

These extract duplicated logic into shared utility classes/methods, reducing maintenance burden across the most files.

### 1.1 — Extract `GZipBase64Utility` static class
- **Files:** `Services/PresetManager.cs` (~924), `Services/JobExportService.cs` (~181), `Services/ConfigurationService.cs` (~351)
- **Action:** Create `Utilities/GZipBase64Utility.cs` with `CompressAndEncode(string)` and `Decompress(string, string? prefixToStrip = null)`. Replace all 3 implementations.

### 1.2 — Extract `AppDataPaths.GetAppFolder()` static helper
- **Files:** `Services/ConfigurationService.cs` (~21-31), `Services/JobStorageService.cs` (~28-34), `Services/JobHistoryService.cs` (~26-30)
- **Action:** Create `Utilities/AppDataPaths.cs` with `GetAppFolder()` returning `%LocalAppData%\SSH_Helper` (creates dir if needed). Replace all 3 sites.

### 1.3 — Add `JsonFileWriter.TryBackupCorrupt(string path)`
- **Files:** `Services/HistoryStorageService.cs` (~727), `Services/JobHistoryService.cs` (~429), `Services/ConfigurationService.cs` (~92), `Services/JobStorageService.cs` (~96)
- **Action:** Add static method to existing `JsonFileWriter`. Replace all 4 inline try/catch File.Copy blocks.

### 1.4 — Expose `CsvManager.ParseCsvLine(string)` as static
- **Files:** `Services/CsvManager.cs` (~118), `Services/JobStorageService.cs` (~299-349)
- **Action:** Extract the CSV line parsing logic to a public static method on `CsvManager`. Replace `JobStorageService.ParseCsvLine` with a call to it.

### 1.5 — Add `JsonFileWriter.Serialize<T>()` static method
- **Files:** `Services/HistoryStorageService.cs` (~604), `Services/JobHistoryService.cs` (~634), `Services/JobStorageService.cs` (~232)
- **Action:** Add one-liner to `JsonFileWriter`. Replace 3 private `Serialize` helpers.

---

## Wave 2: Scripting Command Deduplication

These target the 6+ command files with identical private methods.

### 2.1 — Extract `ApplyOnError` to shared location
- **Files:** `SendCommand.cs`, `HttpCommand.cs`, `PingCommand.cs`, `DnsCommand.cs`, `PortcheckCommand.cs`, `SftpCommand.cs` (6 identical copies)
- **Action:** Add `static CommandResult ApplyOnError(ScriptStep step, string message)` to `CommandResult` class. Remove all 6 private copies. Also fix `WebhookCommand` (4 inline checks) and other commands using `step.OnError?.ToLowerInvariant() == "continue"` to use the same helper.

### 2.2 — Add `OnErrorContinue` constant + `ScriptStep.IsOnErrorContinue` property
- **Files:** 9+ files with stringly-typed `"continue"` checks
- **Action:** Add `public const string OnErrorContinue = "continue"` and `[JsonIgnore] public bool IsOnErrorContinue => string.Equals(OnError, OnErrorContinue, StringComparison.OrdinalIgnoreCase)` to `ScriptStep`. Update all call sites.

### 2.3 — Extract `TruncateForDisplay` to shared utility
- **Files:** `SetCommand.cs` (~342), `ExtractCommand.cs` (~201), `UpdateColumnCommand.cs` (~36), `UpdateEnvironmentCommand.cs` (~36)
- **Action:** Add to a shared scripting utility (e.g., `ScriptingHelpers.cs` or existing class). Remove 4 copies.

### 2.4 — Consolidate `IsSimpleVariableName` / `IsSimpleIdentifier`
- **Files:** `ValueResolver.cs` (~457), `SetCommand.cs` (~284), `ScriptSubroutineRegistryBuilder.cs` (~361)
- **Action:** `ValueResolver.IsSimpleIdentifier` already exists. Make other 2 sites call it.

### 2.5 — Add `CommandResult.IsControlFlow` property
- **Files:** `IfCommand.cs`, `TryCommand.cs`, `ScriptExecutor.cs`, `ParallelCommand.cs`
- **Action:** Add `[JsonIgnore] public bool IsControlFlow => ShouldExit || ShouldBreak || ShouldContinue || ShouldReturn` to `CommandResult`. Replace all 4-condition checks.

### 2.6 — Remove trivial forwarding methods in `ExpressionEvaluator`
- **File:** `Services/Scripting/ExpressionEvaluator.cs` (~260, ~386)
- **Action:** Inline `FindOperator` → `FindLogicalOperator` and `IsTruthy` → `ValueResolver.IsTruthyValue` at call sites. Delete both wrapper methods.

---

## Wave 3: Form1.cs Internal Deduplication

These extract repeated patterns within the 11K-line Form1.cs.

### 3.1 — Extract `ExecuteFolderPresetsAsync(string folderName, IEnumerable<DataGridViewRow> rows)`
- **Lines:** ~9326-9447 (3 near-identical methods)
- **Action:** Unify `ExecuteFolderPresetsOnAllHosts`, `ExecuteFolderPresetsOnSelectedHost`, `ExecuteFolderPresetsOnCheckedHosts` into callers that only differ in row selection, delegating to one shared method.

### 3.2 — Extract `FindNodeByTag(TreeNodeCollection, string name, bool isFolder)`
- **Lines:** ~8515-8603 (2 duplicate local `FindNode` functions)
- **Action:** Replace duplicate local functions in `SelectPresetByName` and `SelectFolderByName`.

### 3.3 — Extract `GetManualOrdered(IEnumerable<string> all, IEnumerable<string> manualOrder)`
- **Lines:** ~8286-8355 (2 near-identical methods)
- **Action:** Unify `GetManualOrderedFolders` and `GetManualOrderedPresets`.

### 3.4 — Extract `ReorderInList(List<string>, string source, string target, int position)`
- **Lines:** ~4076-4119 (2 near-identical methods)
- **Action:** Unify `ReorderFolders` and `ReorderPresetsInFolder`.

### 3.5 — Move TreeView owner-draw wiring outside if/else in `ApplyTheme`
- **Lines:** ~2243-2263
- **Action:** The 8 identical lines in both branches move before/after the if/else.

### 3.6 — Extract `CreateSelectColumn()` helper
- **Lines:** ~437-448, ~2118-2130 (2 identical construction blocks)
- **Action:** One factory method, two call sites.

### 3.7 — Add `IsCredentialManagerAvailable` computed property
- **Lines:** ~1112, ~1124, ~1133, ~1141, ~1151 (5 identical guards)
- **Action:** `private bool IsCredentialManagerAvailable => _credentialProvider?.IsAvailable == true;`

### 3.8 — Extract `RunGcCompaction()` static helper
- **Lines:** ~1070-1082 (2 identical 3-line blocks)
- **Action:** One static method, two call sites.

### 3.9 — Merge `trvPresets_AfterCollapse` / `trvPresets_AfterExpand`
- **Lines:** ~3662-3689
- **Action:** Both call a shared `SetFolderExpandedFromEvent(TreeNode, bool)`.

### 3.10 — Add favorite key constants + `ParseFavoriteKey` helper
- **Lines:** ~4246, ~4291, ~4505 (3+ sites with `"folder:"/"preset:"` prefix parsing)
- **Action:** Constants and a parser method eliminate magic strings and repeated `.StartsWith`/`.Substring` patterns.

### 3.11 — Replace hardcoded `"Host_IP"` with `CsvManager.HostColumnName`
- **Line:** ~3415
- **Action:** One-liner fix.

---

## Wave 4: Duplicate Validation

### 4.1 — Remove `SchedulingService.ValidateCronExpression`, delegate to `InputValidator`
- **Files:** `Services/SchedulingService.cs` (~20-30), `Utilities/InputValidator.cs` (~152-161)
- **Action:** `SchedulingService` calls `InputValidator.ValidateCronExpression` instead of its own copy.

---

## Wave 5: Safe Efficiency Fixes

These are low-risk efficiency improvements that don't change behavior.

### 5.1 — `PresetManager.PersistToConfig()`: Use `GetCurrent()` instead of `Load()`
- **File:** `Services/PresetManager.cs` (~896)
- **Action:** Replace `_configService.Load()` with `_configService.GetCurrent()` (avoids unnecessary disk read on every preset operation).

### 5.2 — `ConfigurationService.SaveEnvironmentState()`: Remove redundant `NormalizeEnvironmentData` call
- **File:** `Services/ConfigurationService.cs` (~201)
- **Action:** Remove the call since `Save()` already calls it.

### 5.3 — `JobExecutionService.ExecuteJobCoreAsync()`: Single-pass result counting
- **File:** `Services/JobExecutionService.cs` (~402-405)
- **Action:** Replace 4 LINQ `.Count()` calls with one `foreach` loop.

### 5.4 — `TerminalOutputProcessor.StripPagerArtifacts()`: Single regex pass
- **File:** `Utilities/TerminalOutputProcessor.cs` (~147-153)
- **Action:** Replace `IsMatch` + `Replace` with single `Replace` + compare.

### 5.5 — `JsonUtilities.ConvertToJsonNode()`: Cache `TrimStart()` result
- **File:** `Services/Scripting/JsonUtilities.cs` (~27-28)
- **Action:** `var trimmed = str.TrimStart(); if (trimmed.StartsWith("{") || trimmed.StartsWith("["))` — eliminates double allocation.

### 5.6 — `SchedulingService.DetectMissedRunSummaries()`: Remove redundant `OrderBy`
- **File:** `Services/SchedulingService.cs` (~171)
- **Action:** Cronos already returns chronological order; remove the no-op sort.

### 5.7 — `TerminalOutputProcessor.StripTrailingPrompt()`: Use array directly instead of `.ToList()`
- **File:** `Utilities/TerminalOutputProcessor.cs` (~273)
- **Action:** `string[]` already has `.Length` and indexing; `.ToList()` wrapper is unnecessary.

---

## Verification

After each wave:
1. `dotnet build SSH_Helper.sln` — must compile cleanly
2. `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` — all tests must pass
3. Spot-check that extracted methods are called from all original sites (no orphaned code)

After all waves:
- Full build + test pass
- Git diff review to confirm no behavioral changes
