# TODO

## 241. Archive completed OpenSpec changes
- [x] 241.1 Confirm the archive set from `openspec list` and exclude incomplete proposals.
- [x] 241.2 Archive `add-portable-release-build`, `add-vault-oidc-auth`, and `add-prompt-built-in-variable` with the OpenSpec CLI.
- [x] 241.3 Validate the resulting OpenSpec state and record the outcome below.

### 241 Review
- Archive set confirmation:
- `openspec list` showed these completed active changes at archive time:
  - `add-portable-release-build`
  - `add-vault-oidc-auth`
  - `add-prompt-built-in-variable`
- Left `add-preconnect-auth-bootstrap` active because it was still `15/16 tasks`.
- Archive results:
- `cmd /c openspec archive add-portable-release-build --yes`
  - Archived as `openspec/changes/archive/2026-04-17-add-portable-release-build/`
  - Updated `openspec/specs/scripting-runtime/spec.md`
- `cmd /c openspec archive add-vault-oidc-auth --yes`
  - Archived as `openspec/changes/archive/2026-04-17-add-vault-oidc-auth/`
  - Updated `openspec/specs/credentials/spec.md`
- `cmd /c openspec archive add-prompt-built-in-variable --yes`
  - Archived as `openspec/changes/archive/2026-04-17-add-prompt-built-in-variable/`
  - Updated `openspec/specs/script-editor/spec.md`
  - Updated `openspec/specs/scripting-runtime/spec.md`
- Validation:
- `cmd /c openspec validate --strict --no-interactive`
  - Returned `Nothing to validate` because there were no remaining active completed changes for that mode after archiving.
- `cmd /c openspec validate --specs --strict --no-interactive`
  - Passed `21/21` specs.
- Final OpenSpec state:
- `openspec list` now shows only `add-preconnect-auth-bootstrap` as the remaining active change.

## 240. Expose `_prompt` as a dynamic built-in script variable
- [x] 240.1 Confirm the change contract in OpenSpec: `${_prompt}` means the current detected remote SSH shell prompt, not UI/input prompt text.
- [x] 240.2 Add RED runtime coverage in `SSH_Helper.Tests/Scripting/ScriptContextTests.cs` and targeted executor/command tests proving `_prompt` is available when `context.Session.CurrentPrompt` exists, resolves dynamically as the prompt changes, and falls back safely when no prompt is available.
- [x] 240.3 Add RED editor/analyzer coverage in `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs` and `SSH_Helper.Tests/Scripting/ScriptDependencyAnalyzerTests.cs` proving `_prompt` is suggested in interpolation completion and never treated as a missing host-column dependency.
- [x] 240.4 Implement runtime resolution in `Services/Scripting/ScriptContext.cs` so `_prompt` is a dynamic built-in surfaced through `GetVariable(...)`, `HasVariable(...)`, and `GetAllVariables()`.
- [x] 240.5 Wire the authoring/debug surfaces in `Services/Editor/ScriptAutocompleteProvider.cs`, `Form1.cs`, and related tooltip plumbing so `_prompt` is visible and described during editing.
- [x] 240.6 Decide snapshot/history behavior and align the supporting code: either persist `_prompt` for debug/history inspection or explicitly filter it out alongside `_timestamp` and `_output` in `Services/SshExecutionService.cs` and any replay paths.
- [x] 240.7 Update docs and spec artifacts (`SCRIPTING.md`, OpenSpec deltas for `scripting-runtime` and `script-editor`) to define availability, meaning, and no-session behavior.
- [x] 240.8 Run focused verification plus broader regression/build coverage and record results below.

### 240 Review
- Implemented behavior:
- `${_prompt}` now resolves from `SshShellSession.CurrentPrompt` in `Services/Scripting/ScriptContext.cs`.
- It is dynamic like `${_timestamp}` and returns an empty string when no SSH prompt is available.
- `_prompt` is exposed in interpolation autocomplete, editor built-in hover preview, and script variable snapshots, but it is explicitly filtered out of `BuildEffectiveHostVariables(...)` so preconnect host-variable propagation does not persist it as a regular host variable.
- Docs/spec updates:
- Updated `SCRIPTING.md` built-in variable docs.
- Added OpenSpec change bundle `openspec/changes/add-prompt-built-in-variable/` with validated deltas for `scripting-runtime` and `script-editor`.
- RED verification:
- `dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~ScriptContextTests.PromptVariable_|FullyQualifiedName~SendCommandTests.ExecuteAsync_SubstitutesPromptBuiltInIntoCommandText|FullyQualifiedName~ScriptAutocompleteProviderTests.GetInterpolationSymbols_IncludesBuiltInsAndHostColumns|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_InterpolationPrefix_SuggestsPromptBuiltInWithDescription|FullyQualifiedName~Form1BuiltInEditorVariableTests.ResolveEditorVariableValue_PromptBuiltIn_ReturnsEditorPreviewValue" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/prompt-builtin-red/bin/ -v minimal`
- Result: failed `6/7` for the expected reasons: `_prompt` was absent from `ScriptContext`, autocomplete, `send` substitution, and `Form1` editor preview.
- Focused GREEN verification:
- `dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~ScriptContextTests.PromptVariable_|FullyQualifiedName~SendCommandTests.ExecuteAsync_SubstitutesPromptBuiltInIntoCommandText|FullyQualifiedName~ScriptAutocompleteProviderTests.GetInterpolationSymbols_IncludesBuiltInsAndHostColumns|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_InterpolationPrefix_SuggestsPromptBuiltInWithDescription|FullyQualifiedName~Form1BuiltInEditorVariableTests.ResolveEditorVariableValue_PromptBuiltIn_ReturnsEditorPreviewValue" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/prompt-builtin-green/bin/ -v minimal`
- Result: passed `7/7`.
- Broader regression verification:
- `dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~ScriptContextTests|FullyQualifiedName~SendCommandTests|FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServicePreconnectTests|FullyQualifiedName~Form1BuiltInEditorVariableTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/prompt-builtin-regression/bin/ -v minimal`
- Result: passed `131/131` with existing `MSB3277`, `CS8602`, `CS0618`, and `xUnit1031` warnings unchanged.
- Build/spec verification:
- `dotnet build SSH_Helper.csproj --no-restore -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts/prompt-builtin-build-app/bin/ -v minimal`
- Result: build succeeded with the existing `MSB3277` warning.
- `cmd /c openspec validate add-prompt-built-in-variable --strict --no-interactive`
- Result: change validated successfully; the tool emitted expected PostHog network-flush noise afterward due sandboxed network restrictions.

## 237. Restore sethistorylabel parser/editor wiring
- [x] 237.1 Add RED coverage proving `sethistorylabel` is recognized as a step command and supports scalar/object option suggestions.
- [x] 237.2 Run the focused RED tests and capture the failure evidence.
- [x] 237.3 Wire `ScriptParser` and related command metadata so `sethistorylabel` parses and autocompletes correctly.
- [x] 237.4 Run focused GREEN verification and record the outcome below.

### 237 Review
- Root cause:
- `SetHistoryLabelCommand` and `StepType.SetHistoryLabel` were added to the runtime model/executor, but `ScriptParser` still omitted `sethistorylabel` from `KnownStepKeys`, command option metadata, scalar-preprocess keys, and the `ParseStep(...)` dispatch switch.
- Because `ScriptAutocompleteProvider` sources its step command and option-key catalogs from `ScriptParser`, the editor never surfaced `sethistorylabel` even though a description string had been added locally.
- RED verification:
- Added focused regressions in:
- `SSH_Helper.Tests/Scripting/ScriptParserTests.cs`
- `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs`
- Command:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests.Parse_SetHistoryLabelScalarStep_ParsesCorrectly|FullyQualifiedName~ScriptParserTests.Parse_SetHistoryLabelMappingStep_ParsesValueAndReplace|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_StepPrefix_SetHistoryLabel_ShowsDetailText|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_SetHistoryLabelStepOptionKey_SuggestsValueAndReplace" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -v minimal`
- Result: failed `4/4` as expected because `sethistorylabel` still parsed as `StepType.Unknown` and autocomplete fell back to generic step-root keys.
- GREEN verification:
- Updated `Services/Scripting/ScriptParser.cs` to:
- register `sethistorylabel` as a known step command,
- expose `{ value, replace }` command option metadata for autocomplete,
- parse scalar and mapping forms into `ScriptStep.SetHistoryLabel`,
- treat inline scalar `sethistorylabel:` values like other scalar-style commands during YAML preprocess quoting.
- Command:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests.Parse_SetHistoryLabelScalarStep_ParsesCorrectly|FullyQualifiedName~ScriptParserTests.Parse_SetHistoryLabelMappingStep_ParsesValueAndReplace|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_StepPrefix_SetHistoryLabel_ShowsDetailText|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_SetHistoryLabelStepOptionKey_SuggestsValueAndReplace" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -v minimal`
- Result: passed `4/4`.
- Broader regression verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptAutocompleteProviderTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -v minimal`
- Result: passed `230/230` with the existing `MSB3277`, `CS8602`, `CS0618`, and `xUnit1031` warnings unchanged.

## 238. Make history-label aggregation deterministic and clearable across folder runs
- [x] 238.1 Add RED coverage proving a later preset can explicitly clear an earlier history label in sequential folder execution.
- [x] 238.2 Add RED coverage proving parallel preset execution resolves history labels by selected preset order, not completion order.
- [x] 238.3 Propagate explicit history-label touch state through script context/results and use it in folder host-result aggregation.
- [x] 238.4 Run focused GREEN verification and record the outcome below.

### 238 Review
- Root cause:
- `sethistorylabel` supported explicit clear semantics inside one script, but folder aggregation only copied non-empty labels, so a later preset could not clear an earlier label.
- In folder runs with `RunPresetsInParallel`, the host-level label was being overwritten directly from each completed preset task, so the final label depended on task completion timing rather than the user-selected preset order.
- RED verification:
- Added focused regressions in:
- `SSH_Helper.Tests/Services/SshExecutionServiceHistoryLabelTests.cs`
- Initial command:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServiceHistoryLabelTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -v minimal`
- Result: build blocked by the existing local `SSH_Helper.exe` file lock on `bin\Debug\net8.0-windows\SSH_Helper.dll`.
- Isolated-output RED command:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServiceHistoryLabelTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/history-label-red/bin/ -p:BaseIntermediateOutputPath=artifacts/history-label-red/obj/ -v minimal`
- Result: failed at compile time as expected because `ExecutionResult` did not yet expose `HistoryLabelTouched`, confirming the new touched-state contract was missing.
- GREEN verification:
- Updated:
- `Services/Scripting/ScriptContext.cs` to track `HistoryLabelTouched` in shared execution state,
- `Models/ExecutionResult.cs` to carry the touched flag out of script execution,
- `Services/Scripting/Commands/SetHistoryLabelCommand.cs` to mark any invocation, including explicit clears, as a history-label touch,
- `Services/SshExecutionService.cs` to:
- apply sequential folder label updates only when a preset explicitly touched the label,
- collect parallel preset results by selected preset index and merge label state after `Task.WhenAll` in deterministic preset order.
- Command:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServiceHistoryLabelTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/history-label-green/bin/ -p:BaseIntermediateOutputPath=artifacts/history-label-green/obj/ -v minimal`
- Result: passed `2/2`.
- Broader regression verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServiceHistoryLabelTests|FullyQualifiedName~SshExecutionServiceProgressTests|FullyQualifiedName~SshExecutionServiceCancellationTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/history-label-regression/bin/ -p:BaseIntermediateOutputPath=artifacts/history-label-regression/obj/ -v minimal`
- Result: passed `12/12` with the existing `MSB3277`, `CS8602`, `CS0618`, and `xUnit1031` warnings unchanged.

## 239. Add append/prepend/clear history-label modes
- [x] 239.1 Add RED coverage for parser/autocomplete support and command runtime semantics for `sethistorylabel.mode` and `separator`.
- [x] 239.2 Add RED folder-execution coverage proving append/prepend combine deterministically across presets.
- [x] 239.3 Implement operation-based history-label accumulation for single scripts and folder runs.
- [x] 239.4 Run focused GREEN verification and record the outcome below.

### 239 Review
- Root cause:
- The first history-label fix only propagated a final `HistoryLabel` string plus a touched bit. That was enough for replace/clear behavior, but it discarded the sequence of mutations, so later presets had no way to append/prepend onto earlier preset labels during folder aggregation.
- Autocomplete also reused the global `mode` enum catalog (`overwrite`, `append`), so even after adding a `mode` field the editor would have suggested the wrong values unless `sethistorylabel` got a command-specific override.
- RED verification:
- Added focused regressions in:
- `SSH_Helper.Tests/Scripting/ScriptParserTests.cs`
- `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs`
- `SSH_Helper.Tests/Scripting/SetHistoryLabelCommandTests.cs`
- `SSH_Helper.Tests/Services/SshExecutionServiceHistoryLabelTests.cs`
- Command:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests.Parse_SetHistoryLabelMappingStep_ParsesModeAndSeparator|FullyQualifiedName~ScriptParserTests.Validate_SetHistoryLabelInvalidMode_ReturnsError|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_SetHistoryLabelStepOptionKey_SuggestsValueReplaceModeAndSeparator|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_SetHistoryLabelModeValue_SuggestsKnownModes|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_StepPrefix_SetHistoryLabel_ShowsDetailText|FullyQualifiedName~SetHistoryLabelCommandTests|FullyQualifiedName~SshExecutionServiceHistoryLabelTests.ExecuteFolderAsync_SequentialLaterPresetCanAppendEarlierHistoryLabel|FullyQualifiedName~SshExecutionServiceHistoryLabelTests.ExecuteFolderAsync_ParallelPresetsAppendHistoryLabelBySelectedOrder" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/history-label-modes-red/bin/ -p:BaseIntermediateOutputPath=artifacts/history-label-modes-red/obj/ -v minimal`
- Result: failed at compile time as expected because `SetHistoryLabelOptions` did not yet expose `Mode`/`Separator`, `ScriptContext` had no history-label operation snapshot, and `Replace` was still modeled as a non-nullable boolean.
- GREEN verification:
- Implemented `HistoryLabelOperation` replay semantics and threaded them through:
- `Services/Scripting/Models/HistoryLabelOperation.cs`
- `Services/Scripting/Models/ScriptStep.cs`
- `Services/Scripting/ScriptContext.cs`
- `Models/ExecutionResult.cs`
- `Services/Scripting/Commands/SetHistoryLabelCommand.cs`
- `Services/Scripting/ScriptParser.cs`
- `Services/Editor/ScriptAutocompleteProvider.cs`
- `Services/SshExecutionService.cs`
- Command:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests.Parse_SetHistoryLabelMappingStep_ParsesModeAndSeparator|FullyQualifiedName~ScriptParserTests.Validate_SetHistoryLabelInvalidMode_ReturnsError|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_SetHistoryLabelStepOptionKey_SuggestsValueReplaceModeAndSeparator|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_SetHistoryLabelModeValue_SuggestsKnownModes|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_StepPrefix_SetHistoryLabel_ShowsDetailText|FullyQualifiedName~SetHistoryLabelCommandTests|FullyQualifiedName~SshExecutionServiceHistoryLabelTests.ExecuteFolderAsync_SequentialLaterPresetCanAppendEarlierHistoryLabel|FullyQualifiedName~SshExecutionServiceHistoryLabelTests.ExecuteFolderAsync_ParallelPresetsAppendHistoryLabelBySelectedOrder" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/history-label-modes-green/bin/ -p:BaseIntermediateOutputPath=artifacts/history-label-modes-green/obj/ -v minimal`
- Result: passed `10/10`.
- Broader regression verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~SetHistoryLabelCommandTests|FullyQualifiedName~SshExecutionServiceHistoryLabelTests|FullyQualifiedName~SshExecutionServiceProgressTests|FullyQualifiedName~SshExecutionServiceCancellationTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/history-label-modes-regression/bin/ -p:BaseIntermediateOutputPath=artifacts/history-label-modes-regression/obj/ -v minimal`
- Result: passed `250/250` with the existing `MSB3277`, `CS8602`, `CS0618`, and `xUnit1031` warnings unchanged.

## 236. Fix preconnect follow-through and localcmd shell suggestion drift
- [x] 236.1 Add or correct focused red tests for:
- [x] preconnect completion progress being emitted in non-debug runs
- [x] structured preconnect variables surviving into main steps without string-flattening
- [x] obsolete pooled session release/removal overloads handling non-empty-password leases
- [x] localcmd shell suggestions/tests matching the intended visual surface (`powershell`, `custom`; raw `cmd` still valid)
- [x] 236.2 Run the focused red tests and capture the failing evidence.
- [x] 236.3 Implement the runtime/editor fixes with the minimal coherent design.
- [x] 236.4 Run focused green verification for the touched areas.
- [x] 236.5 Run broader regression/build verification and record results below.

### 236 Review
- Root cause:
- `ResolveEffectiveScriptAuthContext(...)` emitted preconnect completion progress only inside the debug-gated branch, so non-debug runs never surfaced the completion status.
- Preconnect rebuilt the main execution `ScriptContext` from `HostConnection.Variables`, and `BuildEffectiveHostVariables(...)` flattened lists into comma-joined strings. Structured values created in preconnect therefore lost collection semantics before `steps:` executed.
- The obsolete `SshConnectionPool.ReleaseSession(host, username)` and `RemoveAsync(host, username)` overloads still resolved only the empty-password key even though pooled keys now include password/auth material.
- `localcmd.shell` suggestions still came from the global enum-like value map, so editor autocomplete offered `cmd` even though the intended visual surface had removed it.
- RED verification:
- Added/updated focused tests in:
- `SSH_Helper.Tests/Services/SshExecutionServicePreconnectTests.cs`
- `SSH_Helper.Tests/Services/SshConnectionPoolCompatibilityTests.cs`
- `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs`
- `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`
- Command:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServicePreconnectTests.ExecuteScriptAsync_Preconnect_EmitsProgressMessagesInNonDebugRuns|FullyQualifiedName~SshExecutionServicePreconnectTests.ExecuteScriptAsync_Preconnect_PreservesStructuredVariablesIntoMainSteps|FullyQualifiedName~SshConnectionPoolCompatibilityTests|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_LocalCmdShellValue_SuggestsPowershellAndCustomOnly|FullyQualifiedName~FlowCanvasBridgeTests.Registry_LocalCmdShellOptions_ExcludePwshAndCmd" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -v minimal`
- Result: failed `5/6` for the expected reasons:
- missing non-debug `"Preconnect completed"` progress message
- structured preconnect list flattened to `"alpha, beta"` and `count=11`
- obsolete pool overloads left matching leased/pooled entries in place
- autocomplete still suggested `cmd`
- GREEN verification:
- Command:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServicePreconnectTests.ExecuteScriptAsync_Preconnect_EmitsProgressMessagesInNonDebugRuns|FullyQualifiedName~SshExecutionServicePreconnectTests.ExecuteScriptAsync_Preconnect_EmitsStartAndCompletionOutputWhenDebugEnabled|FullyQualifiedName~SshExecutionServicePreconnectTests.ExecuteScriptAsync_Preconnect_PreservesStructuredVariablesIntoMainSteps|FullyQualifiedName~SshConnectionPoolCompatibilityTests|FullyQualifiedName~ScriptAutocompleteProviderTests.GetCompletion_LocalCmdShellValue_SuggestsPowershellAndCustomOnly|FullyQualifiedName~FlowCanvasBridgeTests.Registry_LocalCmdShellOptions_ExcludePwshAndCmd" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -v minimal`
- Result: passed `7/7`.
- Broader regression verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServicePreconnectTests|FullyQualifiedName~SshConnectionPool|FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~FlowCanvasBridgeTests|FullyQualifiedName~LocalCmdParserTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -v minimal`
- Result: passed `164/164`.
- `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true -v minimal`
- Result: build succeeded with the existing `MSB3277`, `CS8602`, and `xUnit1031` warnings.

## 235. Stop import-preset tests from blocking on modal dialogs
- [x] 235.1 Add a RED WinForms regression that installs a test dialog override for `ImportPreset()` and proves import success is reported without opening a blocking modal.
- [x] 235.2 Add a `Form1` message-dialog override seam and route `ImportPreset()` success messaging through it.
- [x] 235.3 Run focused verification and record the outcome below.

### 235 Review
- Root cause:
- `Form1.ImportPreset()` always ended with a real `DialogTheme.Show(...)` success modal, and the preset-tree WinForms regression suite runs `ImportPreset()` directly.
- The tests already had an input-box override seam, but there was no equivalent message-dialog seam, so the import path could block the test runner behind the success popup until someone clicked `OK`.
- RED verification:
- Extended `ImportPreset_ManualSort_InsertsImportedPresetBelowSelectedPresetAndSelectsIt` in `SSH_Helper.Tests/UI/Form1PresetTreeIncrementalMutationTests.cs` to install a dialog override and assert the captured success message.
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests.ImportPreset_ManualSort_InsertsImportedPresetBelowSelectedPresetAndSelectsIt" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/import-preset-dialog-red/bin/ -p:BaseIntermediateOutputPath=artifacts/import-preset-dialog-red/obj/ -v minimal`
- Result: failed as expected because `Form1` did not yet expose `_dialogPromptOverrideForTests`.
- GREEN verification:
- Added `_dialogPromptOverrideForTests` plus `ShowPromptDialog(...)` to `Form1`, and routed the `ImportPreset()` success/error dialogs through that seam.
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests.ImportPreset_ManualSort_InsertsImportedPresetBelowSelectedPresetAndSelectsIt" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/import-preset-dialog-green/bin/ -p:BaseIntermediateOutputPath=artifacts/import-preset-dialog-green/obj/ -v minimal`
- Result: passed `1/1`.
- Broader regression verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/import-preset-dialog-regression/bin/ -p:BaseIntermediateOutputPath=artifacts/import-preset-dialog-regression/obj/ -v minimal`
- Result: passed `12/12`.

## 234. Prevent disposed-history popup during WinForms tests
- [x] 234.1 Add a RED WinForms regression that reproduces startup-history idle hydration after `Form1` disposal and proves it does not touch disposed output controls.
- [x] 234.2 Patch `Form1` teardown and history/output update guards so idle callbacks no-op cleanly once the form or output controls are disposing/disposed.
- [x] 234.3 Run focused verification and record the outcome below.

### 234 Review
- Root cause:
- `Application.Idle` could still invoke `ArmHistorySelectionOnIdle(...)` after `Form1` disposal, which let `ApplySelectedHistoryEntry()` drive `SetOutputText(...)` against a disposed `txtOutput`.
- `SetOutputText(...)` forces `txtOutput.Handle` creation during redraw suspension, so once the `TextBox` was already disposed the callback raised the user-visible `ObjectDisposedException` popup.
- RED verification:
- Added `ArmHistorySelectionOnIdle_AfterFormDisposal_DoesNotTouchDisposedOutputControls` in `SSH_Helper.Tests/UI/Form1HistorySelectionLifecycleTests.cs`.
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1HistorySelectionLifecycleTests.ArmHistorySelectionOnIdle_AfterFormDisposal_DoesNotTouchDisposedOutputControls" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/history-idle-dispose-red/bin/ -p:BaseIntermediateOutputPath=artifacts/history-idle-dispose-red/obj/ -v minimal`
- Result: failed as expected because `ArmHistorySelectionOnIdle(...)` still reached `SetOutputText(...)` after disposal and threw `ObjectDisposedException`.
- GREEN verification:
- Updated `Form1.cs` to cancel pending history hydration on close and short-circuit history/output UI paths when the form or output controls are disposed.
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1HistorySelectionLifecycleTests.ArmHistorySelectionOnIdle_AfterFormDisposal_DoesNotTouchDisposedOutputControls" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/history-idle-dispose-green/bin/ -p:BaseIntermediateOutputPath=artifacts/history-idle-dispose-green/obj/ -v minimal`
- Result: passed `1/1`.
- Broader regression verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1HistorySelectionLifecycleTests|FullyQualifiedName~Form1PresetTabSelectionTests|FullyQualifiedName~Form1PresetTreeIncrementalMutationTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/history-idle-dispose-regression3/bin/ -p:BaseIntermediateOutputPath=artifacts/history-idle-dispose-regression3/obj/ -v minimal`
- Result: passed `14/14`.

## 233. Place new presets directly below the current selection
- [x] 233.1 Add RED UI regression coverage for add, duplicate, and import placing the created preset immediately below the selected preset and auto-selecting it.
- [x] 233.2 Centralize preset insertion ordering in `Form1` so add, duplicate, and import insert after the selected preset in the same folder/root list.
- [x] 233.3 Run focused verification and capture outcomes in the review section below.

### 233 Review
- Root cause:
- In manual sort mode, `InsertPresetNode(...)` derives placement from `config.ManualPresetOrderByFolder`, but `AddPreset()`, `DuplicatePreset()`, and `ImportPreset()` were not inserting the new preset into that folder-scoped order relative to the selected preset. The tree therefore kept the old next sibling under the selection and appended the new preset later in the list.
- `DuplicatePreset()` and `ImportPreset()` also skipped the explicit post-mutation visibility path that `AddPreset()` already used.
- RED verification:
- Added focused regressions in `SSH_Helper.Tests/UI/Form1PresetTreeIncrementalMutationTests.cs`:
- `AddPreset_ManualSort_InsertsNewPresetBelowSelectedPresetAndSelectsIt`
- `DuplicatePreset_ManualSort_InsertsDuplicateBelowSelectedPresetAndSelectsIt`
- `ImportPreset_ManualSort_InsertsImportedPresetBelowSelectedPresetAndSelectsIt`
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests.AddPreset_ManualSort_InsertsNewPresetBelowSelectedPresetAndSelectsIt|FullyQualifiedName~Form1PresetTreeIncrementalMutationTests.DuplicatePreset_ManualSort_InsertsDuplicateBelowSelectedPresetAndSelectsIt|FullyQualifiedName~Form1PresetTreeIncrementalMutationTests.ImportPreset_ManualSort_InsertsImportedPresetBelowSelectedPresetAndSelectsIt" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/preset-insert-order-red/bin/ -p:BaseIntermediateOutputPath=artifacts/preset-insert-order-red/obj/ -v minimal`
- Result: failed `3/3` as expected because the created/imported preset was not placed directly below the selected preset.
- GREEN verification:
- Updated `Form1.cs` to:
- reuse `InsertIntoPresetOrder(...)` through a new `PositionCreatedPresetAfterReference(...)` helper for add/import/duplicate in manual sort mode,
- sync the legacy root `_manualPresetOrder` list from folder-key `""` manual order updates,
- ensure duplicated/imported incremental nodes are also made fully visible and fallback selection uses `ensureVisible: true`.
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests.AddPreset_ManualSort_InsertsNewPresetBelowSelectedPresetAndSelectsIt|FullyQualifiedName~Form1PresetTreeIncrementalMutationTests.DuplicatePreset_ManualSort_InsertsDuplicateBelowSelectedPresetAndSelectsIt|FullyQualifiedName~Form1PresetTreeIncrementalMutationTests.ImportPreset_ManualSort_InsertsImportedPresetBelowSelectedPresetAndSelectsIt" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/preset-insert-order-green/bin/ -p:BaseIntermediateOutputPath=artifacts/preset-insert-order-green/obj/ -v minimal`
- Result: passed `3/3`.
- Broader regression verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/preset-insert-order-regression/bin/ -p:BaseIntermediateOutputPath=artifacts/preset-insert-order-regression/obj/ -v minimal`
- Result: passed `15/15`.

## 232. Suppress autocomplete on Backspace key-up
- [x] 232.1 Add a RED UI regression proving `Backspace` key-up does not auto-open the script editor autocomplete popup in a valid completion context.
- [x] 232.2 Update the script editor key-up trigger filter so `Backspace` no longer requests autocomplete automatically.
- [x] 232.3 Run focused verification for the new regression and record the outcome below.

### 232 Review
- Root cause:
- `ScintillaScriptEditorControl.ShouldTriggerAutocompleteOnKeyUp(...)` excluded several non-text keys but not `Keys.Back`, so any backspace key-up in a valid completion context reopened the autocomplete popup immediately.
- RED verification:
- Added `CompletionPopup_BackspaceKeyUp_DoesNotTriggerSuggestions` to `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs`.
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests.CompletionPopup_BackspaceKeyUp_DoesNotTriggerSuggestions" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/backspace-autocomplete-red/bin/ -p:BaseIntermediateOutputPath=artifacts/backspace-autocomplete-red/obj/ -v minimal`
- Result: failed as expected because the popup was still visible after `Backspace` key-up.
- GREEN verification:
- Updated `UI/ScintillaScriptEditorControl.cs` so `Keys.Back` is treated as a non-trigger in `ShouldTriggerAutocompleteOnKeyUp(...)`.
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/backspace-autocomplete-green/bin/ -p:BaseIntermediateOutputPath=artifacts/backspace-autocomplete-green/obj/ -v minimal`
- Result: passed `40/40`.

## 231. Refresh existing Flow Canvas when reopened after preset changes
- [x] 231.1 Capture the stale-state root cause in the existing `OpenFlowCanvas()` reuse branch.
- [x] 231.2 Add RED UI regression coverage proving `Edit -> Flow Canvas` rehydrates the current preset when the canvas window is already open.
- [x] 231.3 Patch the existing-window Flow Canvas open path to reload the current script and host state before focusing the window.
- [x] 231.4 Run focused verification and record the outcome below.

### 231 Review
- Root cause:
- `Form1.OpenFlowCanvas()` returned early when `_flowCanvasForm` already existed, so `Edit -> Flow Canvas` only focused the modeless window and never re-sent the current graph or host payload.
- Preset selection already called `LoadCurrentScriptIntoCanvas()`, but if the user relied on reopening/focusing the existing canvas window to refresh what they were looking at, the reuse branch sent nothing.
- RED verification:
- Added `ReopeningExistingFlowCanvas_AfterPresetSwitch_QueuesCurrentPresetGraph` to `SSH_Helper.Tests/UI/Form1FlowCanvasPresetSyncTests.cs`.
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasPresetSyncTests.ReopeningExistingFlowCanvas_AfterPresetSwitch_QueuesCurrentPresetGraph" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/flowcanvas-reopen-red/bin/ -p:BaseIntermediateOutputPath=artifacts/flowcanvas-reopen-red/obj/ -v minimal`
- Result: failed as expected because `pendingMessages` was empty after reopening the existing Flow Canvas window.
- GREEN verification:
- Patched `Form1.OpenFlowCanvas()` to call `LoadCurrentScriptIntoCanvas()` and `SendTargetHostToCanvas()` before focusing the existing window.
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasPresetSyncTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/flowcanvas-reopen-green/bin/ -p:BaseIntermediateOutputPath=artifacts/flowcanvas-reopen-green/obj/ -v minimal`
- Result: passed `2/2`.

## 230. Fix interactive localcmd cmd shell regression
- [x] 230.1 Capture the exact root cause for the interactive `shell: cmd` failure introduced by the localcmd output cleanup work.
- [x] 230.2 Add RED regression coverage proving the cmd interactive audit wrapper no longer relies on raw `-Command '...| Tee-Object ...'` quoting.
- [x] 230.3 Patch cmd interactive audit capture to use a quoting-safe launch form.
- [x] 230.4 Run focused `LocalCmdCommandTests` verification and rebuild the default debug output.

### 230 Review
- Root cause:
- interactive `localcmd` for `shell: cmd` wrapped the command as:
- `(...command...) 2>&1 | powershell.exe -NoLogo -NoProfile -NonInteractive -Command '$input | Tee-Object ...'`
- `cmd.exe` does not treat single quotes as grouping characters, so the raw `| Tee-Object ...` leaked into cmd parsing and caused the interactive run to fail with exit code `255`.
- External reproduction before the fix:
- the generated wrapper failed with `ExitCode=255` and `Tee-Object is not recognized as an internal or external command`.
- Added RED coverage in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`:
- strengthened `Interactive_Cmd_WrapsCommandForAuditCapture` to require `-EncodedCommand` instead of raw `Tee-Object` text in the launched `cmd.exe` arguments, and to require `$ProgressPreference = 'SilentlyContinue';` inside the decoded helper command.
- Implemented fix in `Services/Scripting/Commands/LocalCmdCommand.cs`:
- changed the cmd interactive audit helper to use `powershell.exe -NoLogo -NoProfile -NonInteractive -EncodedCommand ...`
- prepended `$ProgressPreference = 'SilentlyContinue';` to the tee helper itself so the transcript capture process does not inject its own CLIXML progress noise.
- RED verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests.Interactive_Cmd_WrapsCommandForAuditCapture" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/localcmd-cmd-red/bin/ -p:BaseIntermediateOutputPath=artifacts/localcmd-cmd-red/obj/ -v minimal`
- Result: failed as expected because args still used raw `-Command '$input | Tee-Object ...'`.
- Additional RED verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests.Interactive_Cmd_WrapsCommandForAuditCapture" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/localcmd-cmd-progress-red/bin/ -p:BaseIntermediateOutputPath=artifacts/localcmd-cmd-progress-red/obj/ -v minimal`
- Result: failed as expected because the decoded tee helper still lacked `$ProgressPreference = 'SilentlyContinue';`.
- GREEN verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/localcmd-cmd-green/bin/ -p:BaseIntermediateOutputPath=artifacts/localcmd-cmd-green/obj/ -v minimal`
- Result: passed `45/45`.
- External verification after the fix:
- equivalent `cmd -> powershell -EncodedCommand` wrapper now prints `hi`, exits `0`, and writes a clean transcript without CLIXML noise.
- Default build verification:
- `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true -v minimal`
- Result: succeeded and refreshed `bin\Debug\net8.0-windows`.

## 228. Verify the actual default app build after user reported CLIXML still showing
- [x] 228.1 Check whether the current `bin\Debug` output was rebuilt and contains the localcmd PowerShell suppression fix.
- [x] 228.2 Reproduce the exact suppressed PowerShell launch string outside the app to confirm the command itself no longer emits CLIXML.
- [x] 228.3 Record the likely remaining explanation for the user's observed output.

### 228 Review
- Verified the exact suppressed command outside the app:
- `powershell.exe -NoLogo -NoProfile -NonInteractive -EncodedCommand <"$ProgressPreference = 'SilentlyContinue'; date">`
- Result: normal `date` output on stdout and empty stderr.
- Rebuilt the default debug output successfully:
- `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true -v minimal`
- Result: build succeeded with only the existing `MSB3277`, `CS8602`, and `xUnit1031` warnings.
- Verified the default `bin\Debug` assembly contains the new suppression literal:
- binary Unicode-string search for `SilentlyContinue` returned `default=True` for `bin\Debug\net8.0-windows\SSH_Helper.dll`.
- Conclusion: the source fix and the current default debug build both contain the suppression behavior. The output the user pasted is therefore most likely from a run that occurred before the refreshed default build was launched, or from launching a different app output than the rebuilt `bin\Debug` instance.

## 229. Eliminate PowerShell startup CLIXML by fixing localcmd launch flags
- [x] 229.1 Add failing regressions proving non-interactive PowerShell localcmd launch args include `-NoProfile`.
- [x] 229.2 Patch the non-interactive PowerShell launch path to include `-NoProfile` where localcmd expects headless/scripted behavior.
- [x] 229.3 Run focused verification for `LocalCmdCommandTests` and record results below.

### 229 Review
- Root cause refinement:
- the earlier `$ProgressPreference = 'SilentlyContinue'` change was not sufficient because PowerShell startup/module initialization progress can be emitted before the encoded command executes.
- exact repro:
- `powershell.exe -NoLogo -NonInteractive -EncodedCommand <"$ProgressPreference = 'SilentlyContinue'; date">` still emitted CLIXML progress.
- `powershell.exe -NoLogo -NoProfile -NonInteractive -EncodedCommand <"$ProgressPreference = 'SilentlyContinue'; date">` emitted normal stdout and empty stderr.
- Added RED coverage in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`:
- `BuildProcessArgs_Powershell_UsesNoProfileAndPrependsProgressSuppression`
- `BuildInteractiveArgs_NonKeepOpen_Powershell_UsesDirectShellProcessForReliableCapture`
- Implemented fix in `Services/Scripting/Commands/LocalCmdCommand.cs`:
- non-interactive PowerShell localcmd launches now use `-NoLogo -NoProfile -NonInteractive -EncodedCommand ...`
- the same `-NoProfile` change was applied to the matching non-keep-open interactive fallback PowerShell encoded-command path for consistency.
- RED verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests.BuildProcessArgs_Powershell_UsesNoProfileAndPrependsProgressSuppression|FullyQualifiedName~LocalCmdCommandTests.BuildInteractiveArgs_NonKeepOpen_Powershell_UsesDirectShellProcessForReliableCapture" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/localcmd-noprofile-red/bin/ -p:BaseIntermediateOutputPath=artifacts/localcmd-noprofile-red/obj/ -v minimal`
- Result: failed `2/2` because args did not yet contain `-NoProfile`.
- GREEN verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/localcmd-noprofile-green/bin/ -p:BaseIntermediateOutputPath=artifacts/localcmd-noprofile-green/obj/ -v minimal`
- Result: passed `45/45`.
- External verification:
- exact expected startup command with `-NoProfile` produced normal `date` output and empty stderr.
- Default build verification:
- `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true -v minimal`
- Result: succeeded and refreshed `bin\Debug\net8.0-windows`.

## 226. Explain `localcmd` PowerShell CLIXML/progress output
- [x] 226.1 Inspect the current `localcmd` PowerShell launch path and stream capture behavior.
- [x] 226.2 Reproduce the reported `CLIXML`/`Preparing modules for first use.` output outside the app to confirm the source.
- [x] 226.3 Document the root cause, whether the output is expected, and the cleanest mitigation.

### 226 Review
- `localcmd` foreground PowerShell runs currently start `powershell.exe` with `-NoLogo -NonInteractive -EncodedCommand ...` in `Services/Scripting/Commands/LocalCmdCommand.cs`.
- `localcmd` also forwards redirected PowerShell stderr lines directly into script output as warnings.
- External reproduction matched the app exactly:
- `powershell.exe -NoLogo -NoProfile -NonInteractive -EncodedCommand <date>` produced normal `date` output on stdout plus `#< CLIXML ... Preparing modules for first use. ...` on stderr.
- The same command invoked as `powershell.exe -NoLogo -NoProfile -NonInteractive -Command date` did not emit the CLIXML payload in this environment.
- Root cause: the XML is coming from Windows PowerShell itself, not from YAML parsing or the `localcmd` output formatter. In this startup mode, PowerShell serializes progress records to CLIXML on stderr, and `localcmd` currently surfaces that stream verbatim.
- Clean mitigations:
- Best product fix: suppress PowerShell progress records for non-interactive `localcmd` PowerShell runs before executing the user command.
- Possible implementation: prepend `$ProgressPreference = 'SilentlyContinue';` before encoding the command, or otherwise normalize/filter PowerShell CLIXML progress payloads before emitting them to the user.

## 227. Suppress PowerShell CLIXML progress noise in localcmd output
- [x] 227.1 Add a failing `LocalCmdCommand` regression proving non-interactive PowerShell `localcmd` commands prepend progress suppression before encoding.
- [x] 227.2 Patch the non-interactive PowerShell `localcmd` launch path to suppress startup progress without affecting direct quoted executable invocations.
- [x] 227.3 Run focused verification for `LocalCmdCommandTests` and record results below.

### 227 Review
- Added regression coverage in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`:
- `BuildProcessArgs_Powershell_PrependsProgressSuppression`
- updated dependent expectation in `BuildInteractiveArgs_NonKeepOpen_Powershell_UsesDirectShellProcessForReliableCapture`, because that path intentionally reuses the same direct PowerShell encoded-command launch for capture reliability.
- Implemented the fix in `Services/Scripting/Commands/LocalCmdCommand.cs`:
- added `PrepareNonInteractivePowerShellCommand(...)`, which prepends `$ProgressPreference = 'SilentlyContinue';` after the existing PowerShell command normalization step.
- wired the helper into non-keep-open PowerShell encoded-command launch paths, while leaving direct quoted executable invocations and keep-open shells unchanged.
- RED verification:
- Initial focused test run without isolated output paths was blocked by a live `SSH_Helper.exe` file lock in `bin\Debug`; this was an environment collision, not the product signal.
- Isolated RED run:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests.BuildProcessArgs_Powershell_PrependsProgressSuppression" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/localcmd-clixml-red/bin/ -p:BaseIntermediateOutputPath=artifacts/localcmd-clixml-red/obj/ -v minimal`
- Result: failed as expected because decoded command text was still `Get-Process`.
- GREEN verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -p:BaseOutputPath=artifacts/localcmd-clixml-green/bin/ -p:BaseIntermediateOutputPath=artifacts/localcmd-clixml-green/obj/ -v minimal`
- Result: passed `45/45`.

## 225. Implement localcmd reliability and UX review fixes
- [x] 225.1 Add RED tests for cancellation-aware localcmd confirmation and for scheduler handling of localcmd confirmation requirements.
- [x] 225.2 Implement cancellation-aware `ILocalCmdConfirmation` plumbing and dialog behavior, and make scheduler runs fail clearly when a script/job uses `localcmd` without `confirm: never`.
- [x] 225.3 Add RED tests for detached interactive `localcmd.into` metadata analysis, supported shell consistency, and argument/quoting behavior that currently relies on fragile string concatenation.
- [x] 225.4 Implement localcmd metadata consistency across runtime/analyzer/Flow Canvas, align supported shell choices end-to-end, and harden process argument handling.
- [x] 225.5 Update docs/help text/changelog to match the post-fix behavior.
- [x] 225.6 Run focused verification for localcmd-related slices and record results in a `225 Review` section below.

### 225 Review
- Added regression coverage in:
- `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`
- confirmation cancellation propagation before process start
- powershell/custom argument quoting with space-containing args
- `SSH_Helper.Tests/Services/JobExecutionServiceTests.cs`
- run-now and scheduled-job failures when `localcmd` would require an unattended confirmation prompt
- `SSH_Helper.Tests/Scripting/ScriptDependencyAnalyzerTests.cs`
- detached interactive `localcmd.into` metadata definitions
- `SSH_Helper.Tests/Scripting/LocalCmdParserTests.cs`
- parser acceptance for `cmd` and `pwsh` shells
- `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`
- Flow Canvas export of `shell: cmd`
- registry shell options include `powershell`, `pwsh`, `cmd`, `custom`
- Implemented behavior changes:
- `ILocalCmdConfirmation` now accepts a cancellation token, `LocalCmdCommand` passes the script execution token into confirmation, and `LocalCmdConfirmationDialog` now closes on cancellation through `ScriptPromptDialogRunner`.
- Scheduler/unattended runs now preflight-fail scripts containing `localcmd` steps that do not explicitly set `confirm: never`, instead of risking a modal confirmation dialog.
- `localcmd` detached interactive mode is now modeled consistently across runtime/analyzer/Flow Canvas help text and docs as producing `_pid`, `_started`, and `_start_error`.
- `localcmd` shell support is now aligned end-to-end around `powershell`, `pwsh`, `cmd`, and `custom`, while still accepting executable/path variants in parser/runtime validation.
- Local process argument handling now quotes space/quote-containing shell args instead of raw `string.Join(" ", ...)` concatenation.
- Confirmation UX is less misleading: the dialog button now reads `Run Same Command`, with explanatory text about the scope.
- Updated docs/help in:
- `FlowCanvas/src/blockDefs/registry.ts`
- `SCRIPTING.md`
- `CHANGELOG.md`
- `docs/superpowers/specs/2026-04-04-localcmd-command-design.md`
- RED verification:
- Initial focused `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~JobExecutionServiceTests.RunNowAsync_CustomPresetLocalCmdConfirmAlways_FailsWithoutPrompt|FullyQualifiedName~JobExecutionServiceTests.ExecuteScheduledJobAsync_CustomPresetLocalCmdConfirmAlways_FailsWithoutPrompt|FullyQualifiedName~ScriptDependencyAnalyzerTests.AnalyzePresets_LocalCmdInteractiveDetachedInto_DefinesStartupMetadataVariables|FullyQualifiedName~LocalCmdParserTests.Validate_CmdShell_DoesNotReturnShellValidationError|FullyQualifiedName~LocalCmdParserTests.Validate_PwshShell_DoesNotReturnShellValidationError|FullyQualifiedName~FlowCanvasBridgeTests.ExportGraphToYaml_LocalCmdCmdShell_ExportsSuccessfully" -p:SkipFlowCanvasBuild=true -v minimal` failed at compile as expected because the production confirmation interface had not yet been updated for cancellation-aware tests.
- Additional RED verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowCanvasBridgeTests.Registry_LocalCmdShellOptions_IncludePwshAndCmd" -p:SkipFlowCanvasBuild=true -v minimal` failed because the registry still exposed only `powershell` and `custom`.
- GREEN verification:
- `npm run build` in `FlowCanvas` (passed).
- First combined verification attempt ran `.NET` tests in parallel with the Flow Canvas build and hit a transient stale-resource compile error for an old `dist/assets/index-*.js` filename while `dist` was being rewritten; this was a verification race, not a product failure.
- Final focused regression slice:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests|FullyQualifiedName~FlowCanvasBridgeTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~JobExecutionServiceTests" -p:SkipFlowCanvasBuild=true -v minimal`
- Result: passed `227/227`.

## 224. Full implementation review of `localcmd`
- [x] 224.1 Audit `localcmd` runtime behavior in `LocalCmdCommand`, `ScriptContext`, and confirmation UI for reliability, safety, and confusing semantics.
- [x] 224.2 Audit parser, dependency analysis, Flow Canvas, autocomplete, docs, and QA assets for `localcmd` drift or UX mismatches.
- [x] 224.3 Run focused `localcmd` verification and note whether coverage matches the implementation surface.
- [x] 224.4 Record prioritized findings, recommended changes, and verification evidence in a `224 Review` section below.

### 224 Review
- Focused verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests|FullyQualifiedName~FlowCanvasBridgeTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~ScriptAutocompleteProviderTests" -p:SkipFlowCanvasBuild=true -v minimal`
- Result: passed `206/206`.
- High: `localcmd` confirmation is not cancellation-aware. `ILocalCmdConfirmation` has no cancellation token, `LocalCmdCommand` awaits confirmation before entering any cancellable execution path, and `LocalCmdConfirmationDialog` hard-codes `CancellationToken.None`. If the user stops execution while the prompt is open, the prompt will not dismiss itself and the run can remain blocked behind the dialog.
- High: scheduler runs are not safe for the default confirmation model. `JobExecutionService` creates a fresh `SshExecutionService` for every job run, `SshExecutionService` always installs `LocalCmdConfirmationDialog`, and the job path only disables file-picker prompts. Because `localcmd` defaults to `confirm: always`, unattended jobs can still block on modal local-command confirmation instead of running headlessly or failing preflight with a clear message.
- Medium: detached interactive `localcmd` output metadata is implemented but not modeled consistently. Runtime detached interactive mode sets `<into>_pid`, `<into>_started`, and `<into>_start_error`, but `ScriptDependencyAnalyzer` still treats every interactive `localcmd` as defining only `<into>_exit_code`, and Flow Canvas help text still says interactive mode sets only `<into>_exit_code`. This will create false missing-variable warnings and misleading authoring guidance for the detached-interactive path.
- Medium: shell support is internally inconsistent. Runtime supports `cmd`, `cmd.exe`, `pwsh`, `pwsh.exe`, and path variants, but parser validation, Flow Canvas shell options, and scripting docs still describe `localcmd.shell` as `powershell|custom`, and the parser tests explicitly lock in `cmd` as invalid. The feature should either officially support the larger shell matrix end-to-end or remove the dead runtime branches.
- Medium: process argument construction is fragile because it relies on raw string concatenation. `LocalCmdCommand` flattens `args` with `string.Join(" ", options.Args)` and builds `cmd` invocations as `"{modeFlag} \"{command}\""`, so arguments containing spaces or embedded quotes are not escaped robustly. This is likely to produce broken launches for real commands before release unless argument handling moves to `ProcessStartInfo.ArgumentList` or a dedicated Windows quoting helper.
- Medium: the user-facing semantics are still confusing even where the code is intentional. The confirmation dialog button says `Run All`, but approval is still scoped to the same resolved command on the current host. Flow Canvas labels `lifetime` as `Background Lifetime`, while the interactive detached behavior also depends on explicitly setting `lifetime: detached`. Both cases encourage incorrect user expectations.
- Medium: documentation drift remains. `CHANGELOG.md` still documents `localcmd.lifetime` defaulting to `script` and `kill_on_cancel` defaulting to `true`, which no longer matches the runtime or `SCRIPTING.md`. That increases confusion during pre-release review because users will see multiple conflicting descriptions of the same command.
- Coverage gaps worth fixing with the next implementation pass:
- Add `JobExecutionService` coverage for scheduled `localcmd` runs that verifies the approved headless behavior (`confirm: never` success or explicit preflight failure).
- Add `ScriptDependencyAnalyzer` and Flow Canvas regression coverage for detached interactive `localcmd.into` metadata.
- Add `LocalCmdCommand` quoting/spacing tests for `args` entries that contain spaces or embedded quotes.

## 223. Analyze full suite failures and restore the current test project to green
- [x] 223.1 Run the full `SSH_Helper.Tests` suite in the current worktree and capture the exact failing tests/error signatures.
- [x] 223.2 Classify the failures by subsystem and identify root causes before attempting fixes.
- [x] 223.3 Add or tighten focused regression coverage for each real product bug exposed by the failing tests.
- [x] 223.4 Implement the minimal production/test fixes needed to restore intended behavior without disturbing unrelated in-flight work.
- [x] 223.5 Re-run targeted verification for each repaired slice, then re-run the full `SSH_Helper.Tests` suite.
- [x] 223.6 Add a review summary with verification evidence below.

### 223 Review
- Full-suite reproduction initially failed `14/2069`.
- Failure clusters:
- QA preset expectation parsing: `QaPresetCatalogTests` and `QaPresetExecutionTests` rejected the new descriptive clause `Expected: pass (timeout branch observed).` because the helpers only accepted the exact literal `Expected: pass.`.
- Job history persistence/UI: `JobHistoryServiceTests` and `JobListDialogRunNowTests` used fixed March 2026 timestamps. On April 8, 2026 those records were older than the default 30-day retention window, so `JobHistoryService.SaveRun(...)` immediately pruned them and the UI correctly rendered `Never run`.
- Implemented minimal fixes:
- Relaxed QA expected-outcome detection in:
- `SSH_Helper.Tests/Scripting/QaPresetCatalogTests.cs`
- `SSH_Helper.Tests/Scripting/QaPresetExecutionTests.cs`
- Pass outcomes now match `Expected: pass` prefixes, preserving richer descriptive suffixes without weakening the failure/exit classifications.
- Replaced brittle fixed UTC timestamps with recent relative UTC helpers in:
- `SSH_Helper.Tests/Services/JobHistoryServiceTests.cs`
- `SSH_Helper.Tests/UI/JobListDialogRunNowTests.cs`
- This keeps persistence/UI assertions inside the service's default retention policy while preserving the same ordering/duration/skipped-summary behaviors under test.
- Verification:
- Focused repaired slices:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~QaPresetCatalogTests|FullyQualifiedName~QaPresetExecutionTests|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobListDialogRunNowTests" -p:SkipFlowCanvasBuild=true -v minimal`
- Result: passed `55/55`.
- Full suite:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-restore -v minimal`
- Result: passed `2069/2069`.
- Existing warnings during verification remained non-blocking and unchanged in nature: `MSB3277` (`WindowsBase`/WebView2 conflict warnings), `CS8602` in existing test code, and `xUnit1031` analyzer warnings in `ExpressionParserTests`.

## 222. Fix OIDC hardening and detached localcmd round-trip regressions
- [x] 222.1 Add RED tests for Vault OIDC callback host validation in settings/runtime and for OIDC persisted-token reuse/fallback behavior.
- [x] 222.2 Add RED FlowCanvasBridge regression tests proving explicit interactive `lifetime: detached` survives YAML -> canvas -> YAML and explicit detached presence is exported.
- [x] 222.3 Implement Vault callback-host normalization/loopback-only validation in shared runtime helpers and settings validation.
- [x] 222.4 Implement OIDC persisted-token reuse with lookup-self validation, browser fallback on missing/401 token only, and unchanged fresh-token persistence.
- [x] 222.5 Implement Flow Canvas `localcmd` explicit-lifetime preservation end-to-end and update editor help text to match runtime semantics.
- [x] 222.6 Run focused verification for Vault, FlowCanvasBridge, LocalCmd, and ScriptParser slices; then run the full suite and record unrelated existing failures separately if they remain.
- [x] 222.7 Add a review summary with verification evidence below.

### 222 Review
- Added RED coverage in:
- `SSH_Helper.Tests/Vault/VaultServiceTests.cs`
- `OidcAuth_InvalidCallbackHost_ThrowsFriendlyErrorBeforeHttpCalls`
- `OidcAuth_ValidPersistedToken_SkipsBrowserLogin`
- `OidcAuth_UnauthorizedPersistedToken_FallsBackToBrowserLogin`
- `OidcAuth_PersistedTokenValidationTransportError_DoesNotFallBackToBrowserLogin`
- `OidcAuth_Ipv6LoopbackHost_NormalizesRedirectUri`
- `SSH_Helper.Tests/UI/SettingsDialogVaultTests.cs`
- `SavingOidcProfile_WithNonLoopbackCallbackHost_ShowsValidationAndDoesNotPersist`
- `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`
- `TextToGraph_LocalCmdInteractiveDetached_PreservesExplicitLifetimeProp`
- `ImportExportRoundTrip_LocalCmdInteractiveDetached_PreservesExplicitLifetime`
- Implemented Vault hardening with shared callback normalization in `Services/Vault/VaultOidcCallbackSettings.cs`, runtime enforcement in `Services/Vault/VaultService.cs` and `Services/Vault/VaultOidcLoginFlow.cs`, and save-time validation through `SettingsDialog.cs`.
- Runtime behavior now accepts only loopback callback hosts (`127.0.0.1`, `localhost`, `::1`, `[::1]`), normalizes IPv6 redirect URIs to bracketed form, and blocks invalid hosts before any Vault/OIDC HTTP calls.
- OIDC auth now attempts persisted token reuse first via `auth/token/lookup-self`, skips browser login on success, falls back only on missing/401/403 tokens, and surfaces transport/status failures instead of silently dropping into interactive login.
- Flow Canvas import now preserves explicit `localcmd.lifetime` presence when YAML explicitly sets `lifetime: detached`, and the block help text now documents the detached interactive behavior.
- Additional code/docs alignment:
- `Models/VaultSettings.cs` now documents the loopback-only callback-host constraint.
- Focused RED verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~SettingsDialogVaultTests.SavingOidcProfile_WithNonLoopbackCallbackHost_ShowsValidationAndDoesNotPersist|FullyQualifiedName~VaultServiceTests.OidcAuth_InvalidCallbackHost_ThrowsFriendlyErrorBeforeHttpCalls|FullyQualifiedName~VaultServiceTests.OidcAuth_ValidPersistedToken_SkipsBrowserLogin|FullyQualifiedName~VaultServiceTests.OidcAuth_UnauthorizedPersistedToken_FallsBackToBrowserLogin|FullyQualifiedName~VaultServiceTests.OidcAuth_PersistedTokenValidationTransportError_DoesNotFallBackToBrowserLogin|FullyQualifiedName~VaultServiceTests.OidcAuth_Ipv6LoopbackHost_NormalizesRedirectUri|FullyQualifiedName~FlowCanvasBridgeTests.TextToGraph_LocalCmdInteractiveDetached_PreservesExplicitLifetimeProp|FullyQualifiedName~FlowCanvasBridgeTests.ImportExportRoundTrip_LocalCmdInteractiveDetached_PreservesExplicitLifetime" -p:SkipFlowCanvasBuild=true` (failed `7/8` before implementation as expected).
- Focused GREEN verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~SettingsDialogVaultTests.SavingOidcProfile_WithNonLoopbackCallbackHost_ShowsValidationAndDoesNotPersist|FullyQualifiedName~VaultServiceTests.OidcAuth_InvalidCallbackHost_ThrowsFriendlyErrorBeforeHttpCalls|FullyQualifiedName~VaultServiceTests.OidcAuth_ValidPersistedToken_SkipsBrowserLogin|FullyQualifiedName~VaultServiceTests.OidcAuth_UnauthorizedPersistedToken_FallsBackToBrowserLogin|FullyQualifiedName~VaultServiceTests.OidcAuth_PersistedTokenValidationTransportError_DoesNotFallBackToBrowserLogin|FullyQualifiedName~VaultServiceTests.OidcAuth_Ipv6LoopbackHost_NormalizesRedirectUri|FullyQualifiedName~FlowCanvasBridgeTests.TextToGraph_LocalCmdInteractiveDetached_PreservesExplicitLifetimeProp|FullyQualifiedName~FlowCanvasBridgeTests.ImportExportRoundTrip_LocalCmdInteractiveDetached_PreservesExplicitLifetime" -p:SkipFlowCanvasBuild=true` (passed `8/8`).
- Regression slice verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~VaultServiceTests|FullyQualifiedName~VaultSettingsTests|FullyQualifiedName~SettingsDialogVaultTests|FullyQualifiedName~FlowCanvasBridgeTests|FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~ScriptParserTests" -p:SkipFlowCanvasBuild=true` (passed `315/315`).
- Flow Canvas build verification:
- `npm run build` in `FlowCanvas` (passed).
- Full suite verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-restore` rebuilt Flow Canvas and failed with `15` unrelated tests outside the touched Vault/Flow Canvas areas.
- Concentrated failures remained in QA preset expectation parsing, job-history/job-list history rendering, and one aggregate-only `BrowserCallbackUiHostTests` failure.
- Isolated follow-up:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserCallbackUiHostTests.LaunchAsync_WebView2_IgnoresActiveBrowserCallbackWindow_WhenSelectingOwner" -p:SkipFlowCanvasBuild=true` (passed `1/1`), indicating the extra browser-callback failure is not a deterministic regression from this change set.

## 221. Increase Vault settings field width for better text visibility
- [x] 221.1 Identify Vault tab row/input sizing constraints in `SettingsDialog.cs`.
- [x] 221.2 Increase minimum width of Vault text input and related combo fields while preserving existing behavior.
- [x] 221.3 Build solution with existing project flags and record verification evidence.
- [x] 221.4 Slightly widen `SettingsDialog`/tab layout to remove Vault tab horizontal scrollbar after field-width increase.

### 221 Review
- Updated Vault layout sizing in `SettingsDialog.cs`:
- Added shared sizing constants for Vault label and input widths in `CreateVaultTab()`.
- Increased minimum width for Vault text fields (`Profile Name`, `Address`, `Namespace`, `Mount Path`, auth credentials, token).
- Increased minimum width for Vault `KV Version` and `Auth Method` combo boxes to match widened field layout.
- Increased minimum width for `CA Certificate` path textbox.
- Increased `SettingsDialog` and tab-control width slightly so Vault content no longer overflows horizontally after the input-width increase.
- Verification:
- `dotnet build SSH_Helper.sln -v minimal -p:SkipFlowCanvasBuild=true -p:UseAppHost=false` (passed; existing project warnings only).

## 220. Always use Credential Manager for host/Vault credentials; checkbox controls main password persistence only
- [x] 220.1 Add RED UI tests in `SSH_Helper.Tests` proving host-password credential storage still happens when the checkbox/config flag is off.
- [x] 220.2 Add RED UI tests proving main-form default password load/save is disabled when the checkbox/config flag is off.
- [x] 220.3 Update `Form1` credential initialization and host password flows so host/Vault credential-manager usage is always on when provider availability allows.
- [x] 220.4 Update default password load/save behavior so it is gated by the checkbox/config flag (including cleanup when toggled off).
- [x] 220.5 Update settings text/comments to reflect the new checkbox meaning (main password persistence).
- [x] 220.6 Run focused verification and record outcomes in a `220 Review` section.

### 220 Review
- Added focused RED coverage in `SSH_Helper.Tests/UI/Form1CredentialManagerPreferenceTests.cs`:
- `BuildApplicationState_StoresHostPasswordsAndStripsPasswordField_WhenCheckboxSettingIsOff`
- `TryLoadDefaultPassword_DoesNotHydrateMainPassword_WhenCheckboxSettingIsOff`
- `StoreDefaultPassword_DoesNotPersistMainPassword_WhenCheckboxSettingIsOff`
- RED verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1CredentialManagerPreferenceTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts/testbuild/credential-manager-pref-red/ -p:BaseIntermediateOutputPath=artifacts/testobj/credential-manager-pref-red/` (failed `3/3` as expected before implementation).
- Updated runtime behavior in `Form1.cs`:
- Credential provider now initializes independently of the checkbox (Credential Manager always used when available).
- Host password migration/load/restore paths now gate only on provider availability, not the checkbox flag.
- Main-form password load/save now gates on the checkbox (`Credentials.UseCredentialManager`) via `ShouldPersistMainFormPassword()`.
- Added `ClearStoredDefaultPassword()` and invoked cleanup when checkbox is off.
- Updated settings behavior in `settingsToolStripMenuItem_Click(...)` to apply main-password persistence toggle without disabling Credential Manager host/Vault usage.
- Updated UI/docs semantics:
- `SettingsDialog.cs` checkbox label now reads `Store main form password in Windows Credential Manager`.
- `Models/AppConfiguration.cs` summary now clarifies checkbox scope is main-form password persistence.
- GREEN focused verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1CredentialManagerPreferenceTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts/testbuild/credential-manager-pref-green2/ -p:BaseIntermediateOutputPath=artifacts/testobj/credential-manager-pref-green2/` (passed `3/3`).
- Build verification:
- `dotnet build SSH_Helper.sln -v minimal -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts/build/credential-manager-pref2/ -p:BaseIntermediateOutputPath=artifacts/obj/credential-manager-pref2/` (passed).

## 219. Add Vault userpass authentication with portable-safe credential targets
- [x] 219.1 Add RED tests for `VaultService` userpass login flow and missing-password failure behavior.
- [x] 219.2 Extend Vault auth model/runtime to support `VaultAuthMethod.Userpass` (`/v1/auth/userpass/login/{username}`).
- [x] 219.3 Extend Settings/Form wiring so userpass username/password can be configured and loaded/saved via Credential Manager.
- [x] 219.4 Ensure userpass credential storage uses `CredentialTargets.VaultAuthTarget(...)` so portable/non-portable targets stay isolated.
- [x] 219.5 Run focused Vault/settings/credential-target tests and record results.
- [x] 219.6 Add review notes in this task entry with verification evidence.

### 219 Review
- Added RED coverage in `SSH_Helper.Tests/Vault/VaultServiceTests.cs` for userpass auth:
- `UserpassAuth_GetsClientToken`
- `UserpassAuth_MissingPassword_ThrowsFriendlyError`
- `UserpassAuth_MissingUsername_ThrowsFriendlyError`
- RED verification (before implementation) showed expected failures with unsupported auth method:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultServiceTests.UserpassAuth_GetsClientToken|FullyQualifiedName~VaultServiceTests.UserpassAuth_MissingPassword_ThrowsFriendlyError" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/vault-userpass-red/ -p:BaseIntermediateOutputPath=artifacts/testobj/vault-userpass-red/` (failed `2/2` as expected).
- Implemented userpass support end-to-end:
- `Models/VaultSettings.cs`: added `VaultProfileConfig.UserpassUsername` and `VaultAuthMethod.Userpass = 3`.
- `Services/Vault/VaultService.cs`: added `userpassPasswordProvider`, auth switch case, and `/v1/auth/userpass/login/{username}` flow.
- `SettingsDialog.cs`: added `Userpass` auth panel (username/password), profile persistence, credential load/save/delete wiring.
- `Form1.cs`: wired runtime credential retrieval through `CredentialTargets.VaultAuthTarget(profile, "userpass_password")`.
- Added portability/isolation verification and settings persistence coverage:
- `SSH_Helper.Tests/Vault/VaultSettingsTests.cs` now verifies userpass enum value and portable target format `SSH_Helper_Portable:vault:my-profile:userpass_password`.
- `SSH_Helper.Tests/UI/SettingsDialogVaultTests.cs` now verifies userpass username persistence and credential-manager target storage.
- GREEN focused verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultServiceTests|FullyQualifiedName~VaultSettingsTests|FullyQualifiedName~SettingsDialogVaultTests|FullyQualifiedName~CredentialTargetsTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/vault-userpass-green/ -p:BaseIntermediateOutputPath=artifacts/testobj/vault-userpass-green/` (passed `50/50`).

## 218. Add native uuid() scripting function
- [x] 218.1 Add focused RED tests covering UUID generation shape/uniqueness expectations for `uuid()`.
- [x] 218.2 Implement `uuid()` in built-in string functions and register it in the function registry.
- [x] 218.3 Update `SCRIPTING.md` function catalog/examples to document `uuid()`.
- [x] 218.4 Run focused string-function verification and capture outcomes.
- [x] 218.5 Add review notes and final outcome summary to this task entry.

### 218 Review
- Added focused RED regression coverage in `SSH_Helper.Tests/Scripting/StringFunctionTests.cs`:
- `Uuid_DefaultFormat_ReturnsParseableGuid`
- `Uuid_ConsecutiveCalls_ReturnDifferentValues`
- Implemented `uuid()` in `Services/Scripting/Functions/StringFunctions.cs` and registered it in `StringFunctions.Register(...)`.
- Runtime behavior:
- `uuid()` returns a new GUID string in canonical dashed format (`Guid.NewGuid().ToString("D")`).
- Updated `SCRIPTING.md` documentation in three places:
- Function catalog table now includes `uuid()`.
- Additional string function examples now show `request_id = uuid()` style usage.
- Inline function summary list now includes `uuid()`.
- Verification:
- RED: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~StringFunctionTests.Uuid" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/uuid-red/ -p:BaseIntermediateOutputPath=artifacts/testobj/uuid-red/` (failed `2/2` before implementation).
- GREEN (focused): `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~StringFunctionTests.Uuid" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/uuid-green-focused/ -p:BaseIntermediateOutputPath=artifacts/testobj/uuid-green-focused/` (passed `2/2`).
- GREEN (regression slice): `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~StringFunctionTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/uuid-green-regression/ -p:BaseIntermediateOutputPath=artifacts/testobj/uuid-green-regression/` (passed `31/31`).

## 216. Support nested Vault JSON value rotation (immutable secret shape)
- [x] 216.1 Add focused failing tests for rotating a nested field (for example `entities[0].r7_api_key`) via `vault` + JSON functions.
- [x] 216.2 Update Vault write/patch runtime to accept object/array values (not only flat strings) while preserving existing scalar behavior.
- [x] 216.3 Keep parser/command behavior backward compatible for existing scalar map syntax in `vault.write` and `vault.patch`.
- [x] 216.4 Run focused Vault/scripting tests and capture verification evidence.
- [x] 216.5 Document the runnable rotation recipe in the final response and add review notes here.

### 216 Review
- Updated `Services/Scripting/Commands/VaultCommand.cs` to coerce `write`/`patch` values that resolve to JSON object/array text into structured payload values (`JsonNode`) instead of sending them as quoted strings.
- Updated `Services/Vault/VaultService.cs` write/patch pipeline to accept `Dictionary<string, object?>`, preserving structured values during KV v1/v2 write and patch requests.
- Updated Vault read internals to keep raw JSON nodes and convert to string output only at command/function boundaries, so cache/consumer behavior remains compatible while nested data remains structurally safe for fallback merge writes.
- Added regression coverage in `SSH_Helper.Tests/Scripting/VaultCommandTests.cs`:
- `Write_WithJsonArrayString_WritesStructuredArrayValue`
- `Patch_WithJsonArrayString_WritesStructuredArrayValue`
- Updated `SSH_Helper.Tests/Vault/VaultServiceTests.cs` for the new write/patch signature.
- Updated `SCRIPTING.md` vault docs with a nested JSON rotation example that uses `write` (for update-only policies) and explains structured-value serialization behavior.
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultCommandTests|FullyQualifiedName~VaultServiceTests|FullyQualifiedName~VaultFunctionsTests|FullyQualifiedName~VaultInlineSyntaxTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/vault-nested-json/ -p:BaseIntermediateOutputPath=artifacts/testobj/vault-nested-json/` (passed `33/33`).

## 217. Harden Vault write/update against escaped JSON string literals
- [x] 217.1 Add failing regression for `vault.write` when value is escaped JSON text (`[{\"...\"}]`) so it serializes as array/object, not string.
- [x] 217.2 Update vault write/patch value coercion to recover escaped JSON payloads safely.
- [x] 217.3 Re-run focused Vault command/service tests and capture verification evidence.
- [x] 217.4 Add review notes and runnable guidance for update-only rotation flow.

### 217 Review
- Updated JSON coercion in `Services/Scripting/Commands/VaultCommand.cs`:
- Added object/array detection helper (`TryParseJsonObjectOrArray`).
- Added recovery normalization for escaped JSON fragments (for example `[{\"k\":\"v\"}]`) before parsing.
- Added regression coverage in `SSH_Helper.Tests/Scripting/VaultCommandTests.cs`:
- `Write_WithEscapedJsonArrayString_WritesStructuredArrayValue`
- Re-ran focused verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultCommandTests.Write_WithEscapedJsonArrayString_WritesStructuredArrayValue|FullyQualifiedName~VaultCommandTests.Write_WithJsonArrayString_WritesStructuredArrayValue|FullyQualifiedName~VaultCommandTests.Patch_WithJsonArrayString_WritesStructuredArrayValue|FullyQualifiedName~VaultCommandTests.Write_Succeeds|FullyQualifiedName~VaultCommandTests.Patch_Succeeds|FullyQualifiedName~VaultServiceTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/vault-escaped-json/ -p:BaseIntermediateOutputPath=artifacts/testobj/vault-escaped-json/` (passed `22/22`).

## 215. Add bracket charset shorthand to random_string()
- [x] 215.1 Add focused tests for `random_string(..., "[a-zA-Z0-9@#$%^]")` style charset specifications.
- [x] 215.2 Implement bracket/range charset expansion in `random_string()` while preserving literal charset behavior.
- [x] 215.3 Update `SCRIPTING.md` to document bracket shorthand examples for password generation.
- [x] 215.4 Run focused string-function tests and record verification evidence.
- [x] 215.5 Add review notes and final outcome summary to this task entry.

### 215 Review
- Extended `random_string()` in `Services/Scripting/Functions/StringFunctions.cs` to accept bracket charset shorthand:
- Example: `[a-zA-Z0-9@#$%^]`
- Supports range expansion (`a-z`, `A-Z`, `0-9`) and literal symbols in the same bracket expression.
- Keeps backward compatibility:
- Plain literal charset strings (for example `"abc123!@"`) still work unchanged.
- Empty/invalid effective charset still falls back to the default charset.
- Added test coverage in `SSH_Helper.Tests/Scripting/StringFunctionTests.cs`:
- `RandomString_WithBracketCharsetRange_ExpandsRanges`
- Existing random-string tests continue to pass for default and explicit literal charsets.
- Updated `SCRIPTING.md` docs:
- Function table now documents bracket shorthand support.
- Added password example with `random_string(16, "[a-zA-Z0-9@#$%^]")`.
- Verification:
- Focused: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~StringFunctionTests.RandomString" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/random-string-bracket-focused/ -p:BaseIntermediateOutputPath=artifacts/testobj/random-string-bracket-focused/` (passed `4/4`).
- Regression slice: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~StringFunctionTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/random-string-bracket-regression/ -p:BaseIntermediateOutputPath=artifacts/testobj/random-string-bracket-regression/` (passed `29/29`).

## 214. Add native random_string() scripting function
- [x] 214.1 Add focused RED tests for `random_string(length)` behavior (length + allowed character set expectations).
- [x] 214.2 Implement `random_string()` as a built-in scripting function and register it for expression evaluation.
- [x] 214.3 Update documentation/examples to use `random_string(...)` instead of hash/substring workaround snippets.
- [x] 214.4 Run focused scripting tests and record verification evidence.
- [x] 214.5 Add review notes and final outcome summary to this task entry.

### 214 Review
- Added first-class `random_string()` function in `Services/Scripting/Functions/StringFunctions.cs`.
- Function behavior:
- Signature: `random_string(length?, charset?)`
- Default length: `16`
- Default charset: `A-Z`, `a-z`, `0-9`
- Custom charset supported for constrained password policies (e.g., digits-only or policy-safe symbols).
- Length is clamped to `[0, 4096]`; `0` returns empty string.
- Uses `RandomNumberGenerator.GetInt32(...)` for cryptographically secure randomness.
- Added focused tests in `SSH_Helper.Tests/Scripting/StringFunctionTests.cs`:
- `RandomString_DefaultLength_UsesDefaultCharset`
- `RandomString_WithLength_UsesDefaultCharset`
- `RandomString_WithCustomCharset_OnlyUsesAllowedCharacters`
- Updated docs/examples:
- `SCRIPTING.md` function catalog + string function examples now include `random_string(...)`.
- Vault rotation recipe in `SCRIPTING.md` now uses `random_string(24)` again.
- `docs/superpowers/specs/2026-04-05-vault-integration-design.md` examples now use `random_string(24)`.
- Verification:
- RED: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~StringFunctionTests.RandomString" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/random-string-red/ -p:BaseIntermediateOutputPath=artifacts/testobj/random-string-red/` (failed `3/3` before implementation).
- GREEN (focused): `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~StringFunctionTests.RandomString" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/random-string-green-focused/ -p:BaseIntermediateOutputPath=artifacts/testobj/random-string-green-focused/` (passed `3/3`).
- GREEN (regression slice): `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~StringFunctionTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/random-string-green-regression/ -p:BaseIntermediateOutputPath=artifacts/testobj/random-string-green-regression/` (passed `28/28`).

## 213. Implement Vault hardening findings 1-9
- [x] 213.1 Add/extend RED tests for all planned Vault findings:
- scheduler Vault runtime wiring,
- stale/disposed provider rebinding,
- cache version key correctness,
- settings profile/default persistence,
- environment snapshot Vault profile retention,
- Flow Canvas Vault import mapping,
- thread-safe profile dictionary access,
- `vault_path` runtime resolution and job-profile override behavior,
- Job Editor Vault mode + validation/data persistence.
- [x] 213.2 Implement scheduler/runtime Vault fixes in `JobExecutionService`, `Form1`, and Vault provider wiring.
- [x] 213.3 Implement Vault service correctness fixes (`version`-aware cache keys + synchronized profile dictionary access).
- [x] 213.4 Implement Settings dialog profile/default behavior fixes.
- [x] 213.5 Implement environment snapshot + Flow Canvas Vault mapping fixes.
- [x] 213.6 Implement full `vault_path` runtime support for scheduler and manual host execution paths.
- [x] 213.7 Implement Job Editor Vault credential mode UI, validation, and persistence including per-job Vault profile override.
- [x] 213.8 Update docs for Vault credential-flow/runtime parity (`SCRIPTING.md` + relevant model/docs comments).
- [x] 213.9 Run focused Vault slices first, then broader Vault-related suites, and capture verification evidence.
- [x] 213.10 Add review notes and final verification summary to this task entry.

### 213 Review
- Implemented scheduler Vault runtime wiring and per-run default profile propagation across `Form1`, `JobExecutionService`, `VaultCredentialProvider`, and `SshExecutionService` integration points.
- Added per-job Vault profile override model support via `JobDefinition.VaultProfileName` and applied precedence `job -> environment -> app default` when path omits explicit `profile@`.
- Fixed Vault service correctness issues by making cache keys `version`-aware and synchronizing profile dictionary create/read/clear/dispose access.
- Fixed Settings Vault profile UX/data bugs by persisting edits to the previously selected profile and tracking default profile independently from list selection, including rename/remove behavior.
- Preserved environment Vault profile during grid snapshot saves in `EnvironmentService.BuildSnapshot`.
- Added missing Flow Canvas Vault import/preview/property extraction mapping and deterministic Vault export key ordering.
- Implemented `vault_path` resolution for both scheduler and manual host-connection paths, with fallback to row/global credentials on lookup failure.
- Added Job Editor Vault credential mode UI + validation + persistence for `CredentialMode.Vault`, `VaultCredentialPath`, and optional job-level profile override.
- Updated Vault runtime/credential behavior docs in `SCRIPTING.md` to match implementation.
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultServiceTests|FullyQualifiedName~VaultCredentialProviderTests|FullyQualifiedName~SettingsDialogVaultTests|FullyQualifiedName~JobEditorDialogVaultCredentialTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~SaveCurrentGridToEnvironment_PreservesVaultProfileName|FullyQualifiedName~TextToGraph_VaultStep_ImportsAsVaultBlock_WithPreviewAndExtractedProps|FullyQualifiedName~RunNowAsync_CustomPresetWithVaultStep_SucceedsWhenVaultRuntimeIsAvailable|FullyQualifiedName~BuildHostConnections_VaultPath_OverridesRowCredentials_AndUsesJobDefaultProfile" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/vault-focused-final/ -p:BaseIntermediateOutputPath=artifacts/testobj/vault-focused-final/` (passed `84/84`).
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultServiceTests|FullyQualifiedName~VaultCredentialProviderTests|FullyQualifiedName~VaultSettingsTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~EnvironmentServiceTests|FullyQualifiedName~FlowCanvasBridgeTests|FullyQualifiedName~SettingsDialogVaultTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~JobEditorDialogVaultCredentialTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/vault-final/ -p:BaseIntermediateOutputPath=artifacts/testobj/vault-final/` (passed `234/234`).

## 212. Add rejected feature-idea tracking
- [x] 212.1 Create `rejected_ideas.md` with a clear purpose and reusable entry template.
- [x] 212.2 Update `CLAUDE.md` to instruct tracking rejected feature ideas in that file.
- [x] 212.3 Verify both docs exist and contain the expected guidance.

### 212 Review
- Added root-level `rejected_ideas.md` with:
- purpose statement,
- required field list (`date`, `idea`, `category`, `reason`, `revisit trigger`, related refs),
- starter `Entries` section.
- Updated `CLAUDE.md` Development Guidelines with a new item directing assistants to log declined feature ideas in `rejected_ideas.md`.
- Verification:
- `Test-Path rejected_ideas.md` returned `True`.
- `rg -n "Track rejected feature ideas|rejected_ideas.md" CLAUDE.md rejected_ideas.md` confirmed the new guidance and file content.

## 208. Add playsound QA preset using Windows media files
- [x] 208.1 Add a `QA PlaySound [Windows]` preset in `qa_presets.json` following current QA style conventions.
- [x] 208.2 Cover both successful playback and an intentional unsupported-extension error path with assertions.
- [x] 208.3 Validate `qa_presets.json` parses successfully after edits.
- [x] 208.4 Document a concise review summary in this file.

### 208 Review
- Added `QA PlaySound [Windows]` to `qa_presets.json` under `QA/LocalCmd`.
- Preset behavior coverage:
- Resolves a usable Windows media sound path with a primary (`%WINDIR%\\Media\\notify.wav`) + fallback (`%WINDIR%\\Media\\Windows Notify.wav`) check.
- Verifies successful `playsound` execution for both `wait: true` and `wait: false`, asserting captured metadata (`backend`, `wait`, `volume`).
- Verifies unsupported-extension handling by writing a `.txt` file and running `playsound` with `on_error: continue`, asserting failure capture/meta error and `_last_error` population.
- Verification:
- `Get-Content -Raw qa_presets.json | ConvertFrom-Json` (parsed successfully).

## 207. Add localcmd QA presets for feature coverage
- [x] 207.1 Add multiple `QA LocalCmd ...` preset scenarios in `qa_presets.json` that cover distinct runtime behaviors.
- [x] 207.2 Keep naming, descriptions, assertions, and folder conventions aligned with existing QA preset patterns.
- [x] 207.3 Validate `qa_presets.json` parses successfully after insertion.
- [x] 207.4 Record a concise review summary in this file.

### 207 Review
- Added six new presets under `QA/LocalCmd` in `qa_presets.json`:
- `QA LocalCmd Foreground Capture [Windows]`
- `QA LocalCmd Env WorkingDir [Windows]`
- `QA LocalCmd Exit Policies [Windows]`
- `QA LocalCmd Background Metadata [Windows]`
- `QA LocalCmd Quiet Suppress [Windows]`
- `QA LocalCmd Timeout Continue [Windows]`
- Scenarios cover distinct `localcmd` paths: foreground stdout/stderr capture, env + working dir, non-zero policy handling (`fail_on_nonzero`, `success_codes`, `on_error`), background metadata (`into_pid`, `into_started`, `into_start_error`), quiet/suppress output controls, and timeout recovery behavior.
- Verification:
- `Get-Content -Raw qa_presets.json | ConvertFrom-Json` (parsed successfully).
- `Select-String -Path qa_presets.json -Pattern '"QA LocalCmd'` (confirmed six inserted localcmd presets).

## 206. Remove `cmd` shell option from `localcmd`
- [x] 206.1 Remove `cmd` from Flow Canvas `localcmd.shell` selectable options.
- [x] 206.2 Align `localcmd` shell validation/docs to `powershell` + `custom`.
- [x] 206.3 Update parser coverage and run focused verification.

### 206 Review
- Updated `FlowCanvas/src/blockDefs/registry.ts` localcmd shell selector options to `['powershell', 'custom']`.
- Updated `Services/Scripting/ScriptParser.cs` shell validation error text and allowed shell set to remove `cmd`.
- Updated parser tests in `SSH_Helper.Tests/Scripting/LocalCmdParserTests.cs`:
- Full-form parse fixture now uses `shell: powershell`.
- Added validation regression `Validate_CmdShell_ReturnsShellValidationError`.
- Updated docs in `SCRIPTING.md` and `docs/superpowers/specs/2026-04-04-localcmd-command-design.md` to remove `cmd` as a documented localcmd shell option.
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdParserTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/localcmd-shell-removal/ -p:BaseIntermediateOutputPath=artifacts/testobj/localcmd-shell-removal/` (passed `18/18`).

## 205. Fix localcmd command-banner formatting for folded multiline commands
- [x] 205.1 Add failing parser/runtime regression(s) that reproduce folded multiline `localcmd.command` collapsing into `utf8notepad` in emitted command banners.
- [x] 205.2 Patch localcmd command formatting so displayed banner text preserves readable command boundaries (at minimum, no token concatenation across folded lines).
- [x] 205.3 Run focused localcmd/parser/output-format tests and capture RED/GREEN evidence.
- [x] 205.4 Document review notes and update `tasks/lessons.md` with this correction pattern.

### 205 Review
- Root-cause isolation:
- Added parser regression `Parse_FoldedMultilineCommand_DoesNotConcatenateAdjacentTokens` in `SSH_Helper.Tests/Scripting/LocalCmdParserTests.cs`.
- Result: parser already preserved command boundaries (no `utf8notepad` concatenation), so the improvement target was command-banner rendering.
- Added RED runtime regression `Background_CommandBanner_MultilineCommand_UsesVisibleLineBreakMarkers` in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`.
- RED evidence (pre-fix): banner emitted raw multiline text and did not contain visible `\n` markers.
- Runtime patch in `Services/Scripting/Commands/LocalCmdCommand.cs`:
- Command-banner output for foreground/background/interactive localcmd now formats the displayed command through `ScriptingHelpers.FormatForDisplay(...)`.
- This preserves execution behavior while making line boundaries explicit in output (`\n` markers), avoiding ambiguous merged tokens in one-line render surfaces.
- Verification:
- RED: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Background_CommandBanner_MultilineCommand_UsesVisibleLineBreakMarkers" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/205-red-banner/ -p:BaseIntermediateOutputPath=artifacts/testobj/205-red-banner/` (failed as expected).
- GREEN (focused): `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Background_CommandBanner_MultilineCommand_UsesVisibleLineBreakMarkers|FullyQualifiedName~Parse_FoldedMultilineCommand_DoesNotConcatenateAdjacentTokens" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/205-green/ -p:BaseIntermediateOutputPath=artifacts/testobj/205-green/` (passed `2/2`).
- GREEN (regression slice): `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts/testbuild/205-regression/ -p:BaseIntermediateOutputPath=artifacts/testobj/205-regression/` (passed `55/55`).

## 204. Fix keep_open immediate-close when shell aliases are used
- [x] 204.1 Add a failing regression test proving `shell: powershell.exe` + `keep_open: true` must still launch keep-open mode (`-NoExit`).
- [x] 204.2 Patch localcmd shell normalization/alias handling so keep-open and direct-capture logic recognizes `powershell.exe`/`cmd.exe` (and path variants).
- [x] 204.3 Keep parser validation aligned so shell aliases do not get rejected as invalid.
- [x] 204.4 Run focused localcmd tests and capture verification evidence.
- [x] 204.5 Update lessons with the correction pattern.

### 204 Review
- Added RED regression in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`:
- `BuildInteractiveArgs_KeepOpen_PowerShellExeAlias_UsesNoExit`
- RED evidence (pre-fix): keep-open alias case launched via `wt.exe` (non-keep-open path), causing immediate close behavior instead of `-NoExit`.
- Patched `Services/Scripting/Commands/LocalCmdCommand.cs`:
- Added shell normalization (`NormalizeShell`) at runtime entry.
- Added shell alias/path detection helpers:
- `IsPowerShellShell(...)`
- `IsCmdShell(...)`
- `ResolvePowerShellExecutable(...)`
- `ResolveCmdExecutable(...)`
- Updated keep-open decision points and process-arg builders to use alias-aware shell checks.
- Updated interactive reliable-capture and audit-wrapper shell checks to use alias-aware detection.
- Patched parser shell validation in `Services/Scripting/ScriptParser.cs`:
- `IsValidLocalCmdShell(...)` now accepts `powershell.exe`/`cmd.exe` and path variants in addition to canonical tokens.
- Added parser validation regression in `SSH_Helper.Tests/Scripting/LocalCmdParserTests.cs`:
- `Validate_PowerShellExeShellAlias_DoesNotReturnShellValidationError`
- Verification:
- RED:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BuildInteractiveArgs_KeepOpen_PowerShellExeAlias_UsesNoExit" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` failed before patch.
- GREEN:
- same alias keep-open test passed (`1/1`) after patch.
- focused slice: `BuildInteractiveArgs_KeepOpen_PowerShellExeAlias_UsesNoExit|Validate_PowerShellExeShellAlias_DoesNotReturnShellValidationError|Interactive_WindowCloseExitCode_DoesNotFail_WhenFailOnNonZeroTrue` passed (`3/3`).
- broader localcmd slice: `LocalCmdCommandTests|LocalCmdParserTests|AnalyzePresets_LocalCmdInteractiveInto_DefinesOnlyExitCodeVariable` passed (`54/54`).

## 203. Treat interactive window close (X) as graceful localcmd completion
- [x] 203.1 Add a failing regression test for `interactive: true` where process exit code `-1073741510` (window closed) does not fail the step.
- [x] 203.2 Patch `LocalCmdCommand.ExecuteInteractive(...)` to classify window-close exit code as user-initiated close and bypass `fail_on_nonzero` failure.
- [x] 203.3 Preserve `into` exit-code capture and interactive-session audit details while marking close reason explicitly.
- [x] 203.4 Run focused localcmd regression tests and record verification output.
- [x] 203.5 Document review notes in `tasks/todo.md` and update `tasks/lessons.md` for this correction pattern.

### 203 Review
- Added RED regression in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`:
- `Interactive_WindowCloseExitCode_DoesNotFail_WhenFailOnNonZeroTrue`
- RED evidence (pre-fix): interactive step failed when process exit code was `-1073741510` (`0xC000013A`) even though this is a user-closed terminal window case.
- Patched `Services/Scripting/Commands/LocalCmdCommand.cs`:
- Added `UserClosedInteractiveWindowExitCode` constant (`0xC000013A`).
- Added `IsInteractiveWindowCloseExitCode(...)` helper.
- In `ExecuteInteractive(...)`, window-close exit is now mapped to close reason `user_closed_window` and excluded from `fail_on_nonzero` failure evaluation.
- `into` behavior is preserved: `<into>_exit_code` still records the raw exit code for audit/logic.
- Interactive session audit remains preserved via `CaptureInteractiveAuditSession(...)`.
- Updated `tasks/lessons.md` with this correction pattern.
- Verification:
- RED:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Interactive_WindowCloseExitCode_DoesNotFail_WhenFailOnNonZeroTrue" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` (failed before patch).
- GREEN:
- Same focused test passed (`1/1`) after patch.
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests|FullyQualifiedName~AnalyzePresets_LocalCmdInteractiveInto_DefinesOnlyExitCodeVariable" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` passed (`52/52`).

## 202. Capture full PowerShell interactive session history in execution details
- [x] 202.1 Add a failing test that proves `localcmd` PowerShell `interactive: true` + `keep_open: true` uses session-level transcript capture (not one-shot `Tee-Object` capture).
- [x] 202.2 Patch interactive localcmd audit wrapping so PowerShell starts a session transcript before running the initial command, allowing follow-up user-entered commands to be captured until shell close.
- [x] 202.3 Keep existing interactive history plumbing (`ScriptContext.AddInteractiveSession`) and transcript cleanup behavior intact.
- [x] 202.4 Run focused localcmd tests and record verification output.
- [x] 202.5 Document review notes and behavior change in `tasks/todo.md` and `SCRIPTING.md`.

### 202 Review
- Added RED regression in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`:
- `Interactive_KeepOpen_PowerShell_UsesSessionTranscriptCapture`
- RED evidence (pre-fix): keep-open PowerShell interactive launch arguments used one-shot `Tee-Object` wrapping and did not contain `Start-Transcript`.
- Patched `Services/Scripting/Commands/LocalCmdCommand.cs`:
- `ExecuteInteractive(...)` now passes `keep_open` into interactive audit preparation.
- PowerShell interactive audit wrapping now uses session-level `Start-Transcript` capture:
- keep-open mode starts transcript and leaves it active until shell close (captures follow-up user-entered commands).
- non-keep-open mode starts transcript and best-effort stops it in a `finally` block.
- Existing interactive history storage path remains unchanged (`CaptureInteractiveAuditSession(...)` -> `context.AddInteractiveSession(...)`).
- Updated test helper `TryExtractInteractiveAuditTranscriptPath(...)` in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs` to parse both `-FilePath` and `-Path` transcript markers.
- Updated `SCRIPTING.md` localcmd behavior notes:
- PowerShell interactive capture now documents session transcript behavior for `keep_open: true`.
- cmd shell remains best-effort launched-command capture.
- Verification:
- RED:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Interactive_KeepOpen_PowerShell_UsesSessionTranscriptCapture" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` (failed as expected before patch).
- GREEN:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` passed (`35/35`).
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests|FullyQualifiedName~AnalyzePresets_LocalCmdInteractiveInto_DefinesOnlyExitCodeVariable" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` passed (`51/51`).

## 201. Make localcmd interactive transcript capture reliable for non-keep-open runs
- [x] 201.1 Add a failing regression test that demonstrates non-keep-open interactive PowerShell launch must use a directly tracked shell process (not `wt.exe` launcher).
- [x] 201.2 Patch `LocalCmdCommand` interactive argument selection to bypass `wt.exe` launcher for powershell/cmd when transcript reliability is required.
- [x] 201.3 Run focused localcmd tests to verify the regression and surrounding behavior.
- [x] 201.4 Add review notes and verification evidence.

### 201 Review
- Added RED regression in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`:
- `BuildInteractiveArgs_NonKeepOpen_Powershell_UsesDirectShellProcessForReliableCapture`
- RED evidence (pre-fix): test failed because interactive arg selection returned `...\\WindowsApps\\wt.exe` instead of `powershell.exe`.
- Patched `Services/Scripting/Commands/LocalCmdCommand.cs`:
- `BuildInteractiveArgs(...)` now bypasses Windows Terminal launcher for non-keep-open `powershell`/`cmd` interactive runs and returns a directly tracked shell process via `BuildProcessArgs(...)`.
- Added helper `RequiresDirectInteractiveShellForReliableCapture(...)` to centralize this rule.
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BuildInteractiveArgs_NonKeepOpen_Powershell_UsesDirectShellProcessForReliableCapture" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/`
- RED run failed as expected (`0/1` pass), then GREEN rerun passed (`1/1`).
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` passed (`34/34`).

## 200. Add localcmd interactive audit transcript capture to execution details
- [x] 200.1 Design always-on interactive localcmd audit capture behavior and confirm always-on mode.
- [x] 200.2 Implement interactive launch wrapping for powershell/cmd to tee output into a transient transcript file.
- [x] 200.3 Persist captured transcript into execution history via `ScriptContext.AddInteractiveSession(...)`.
- [x] 200.4 Ensure timeout/cancel paths still record partial audit sessions and always clean up temp transcript files.
- [x] 200.5 Add focused tests for transcript capture/session persistence and cmd wrapper coverage.
- [x] 200.6 Update scripting docs to clarify localcmd interactive audit behavior and run focused verification.

### 200 Review
- Added always-on localcmd interactive audit capture in `Services/Scripting/Commands/LocalCmdCommand.cs`.
- Interactive command launch now uses an audit wrapper for:
- `shell: powershell` -> `Tee-Object` transcript capture.
- `shell: cmd` -> pipeline to PowerShell `Tee-Object` transcript capture.
- Captured transcript is cleaned and size-limited (using `max_output_bytes`), then stored as an interactive session via `context.AddInteractiveSession(...)` with:
- `SessionMode = localcmd-interactive`
- `EmulationMode = <shell>`
- close reason (`exit_code:<n>`, `timeout`, `cancelled`)
- `Completed` status and transcript text for history details.
- Timeout/cancel/success paths all capture audit session metadata, and temp transcript files are cleaned up in a `finally` path.
- Added tests in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`:
- `Interactive_PowerShell_CapturesAuditTranscriptIntoInteractiveSessions`
- `Interactive_Cmd_WrapsCommandForAuditCapture`
- Updated docs in `SCRIPTING.md` localcmd section to state that interactive runs are recorded in history interactive sessions and that transcript capture is best-effort for powershell/cmd.
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests|FullyQualifiedName~AnalyzePresets_LocalCmdInteractiveInto_DefinesOnlyExitCodeVariable|FullyQualifiedName~ExportGraphToYaml_LocalCmdCustomShellMissingShellPath_ReturnsRequiredOptionError" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` passed (`50/50`).

## 199. Address localcmd option-review findings (runtime, analyzer, validation, docs)
- [x] 199.1 Align interactive localcmd exit handling with `fail_on_nonzero` + `success_codes`.
- [x] 199.2 Ensure cmd-shell `args` are honored across foreground/interactive/keep-open argument builders.
- [x] 199.3 Fix dependency analyzer localcmd `into` definitions for interactive mode (`*_exit_code` only).
- [x] 199.4 Add parser/FlowCanvas validation for localcmd conditional requirements and invalid combinations.
- [x] 199.5 Document localcmd in `SCRIPTING.md` and clarify option semantics in Flow Canvas help text.
- [x] 199.6 Add focused regression tests and run verification.

### 199 Review
- Runtime updates in `Services/Scripting/Commands/LocalCmdCommand.cs`:
- Interactive mode now applies `fail_on_nonzero` + `success_codes` against the interactive close exit code (with normal `on_error` handling).
- `cmd` shell now honors `args` in both `/c` and `/K` paths.
- Analyzer update in `Services/Scripting/ScriptDependencyAnalyzer.cs`:
- `localcmd` interactive `into` now defines only `<into>_exit_code`; stdout/stderr are no longer treated as defined for interactive mode.
- Parser validation updates in `Services/Scripting/ScriptParser.cs`:
- Added localcmd validation for `command`, shell enum, conditional `shell_path` requirement, `run_mode`, `lifetime`, `confirm`, `max_output_bytes`, interactive/background mutual exclusion, and `keep_open` requiring `interactive: true`.
- Flow Canvas bridge update in `Services/FlowCanvasBridge.cs`:
- Added conditional required-option enforcement so `shell: custom` requires `shell_path`.
- UX/docs updates:
- `FlowCanvas/src/blockDefs/registry.ts` localcmd help text now clarifies interactive `into` behavior and success-code scope.
- `SCRIPTING.md` now includes `localcmd` in the command list plus a dedicated command section with syntax, mode matrix, and examples.
- Added/updated focused tests:
- `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs` for interactive exit policy and cmd-args forwarding.
- `SSH_Helper.Tests/Scripting/LocalCmdParserTests.cs` for new localcmd parser validation rules.
- `SSH_Helper.Tests/Scripting/ScriptDependencyAnalyzerTests.cs` for interactive-into variable definition semantics.
- `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` for conditional `shell_path` required validation.
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests|FullyQualifiedName~AnalyzePresets_LocalCmdInteractiveInto_DefinesOnlyExitCodeVariable|FullyQualifiedName~ExportGraphToYaml_LocalCmdCustomShellMissingShellPath_ReturnsRequiredOptionError" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` passed (`48/48`).

## 198. Fix localcmd interactive keep_open working_dir propagation
- [x] 198.1 Reproduce and isolate why `working_dir` was ignored for `interactive: true` + `keep_open: true`.
- [x] 198.2 Patch interactive startup to set `ProcessStartInfo.WorkingDirectory` for interactive runs.
- [x] 198.3 Add focused regression coverage for keep-open interactive working directory behavior.
- [x] 198.4 Run focused runtime verification and capture results.

### 198 Review
- Root cause: `ExecuteInteractive(...)` did not set `ProcessStartInfo.WorkingDirectory`; non-keep-open paths could still work via Windows Terminal `-d`, but keep-open direct-shell launches ignored `working_dir`.
- Runtime patch in `Services/Scripting/Commands/LocalCmdCommand.cs`:
- interactive start now applies `startInfo.WorkingDirectory = Environment.ExpandEnvironmentVariables(workingDir)` when provided.
- Added test in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`:
- `Interactive_KeepOpen_HonorsWorkingDirectory`
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` passed (`27/27`).

## 197. Fix localcmd interactive keep_open capture regression
- [x] 197.1 Reproduce and isolate why `interactive: true` + `keep_open: true` failed to preserve expected capture behavior.
- [x] 197.2 Patch interactive launch path to avoid waiting on transient Windows Terminal launcher processes when keep-open is enabled.
- [x] 197.3 Ensure non-keep-open fallback shells auto-close (`powershell -Command` / `cmd /c`) instead of always holding windows open.
- [x] 197.4 Add focused command tests for keep-open interactive execution/capture and argument construction.
- [x] 197.5 Run focused parser/runtime/bridge verification and document outcomes.

### 197 Review
- Root cause: with `keep_open: true`, interactive localcmd could run through `wt.exe`; waiting on that launcher process is not a reliable proxy for the real shell lifetime, so `into` capture expectations were inconsistent.
- Runtime patch in `Services/Scripting/Commands/LocalCmdCommand.cs`:
- `BuildInteractiveArgs(...)` now bypasses Windows Terminal for keep-open PowerShell/cmd runs and launches a directly tracked shell process (`powershell.exe -NoExit` / `cmd.exe /K`).
- Non-keep-open fallback interactive args now auto-close correctly (`powershell.exe -Command` / `cmd.exe /c`).
- Added focused regression coverage in `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs`:
- `Interactive_KeepOpen_CapturesExitCodeAndUsesDirectShellProcess`
- `BuildInteractiveArgs_KeepOpen_Powershell_BypassesWindowsTerminalLauncher`
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests|FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:BaseOutputPath=artifacts/testbuild/ -p:BaseIntermediateOutputPath=artifacts/testobj/` passed (`98/98`).

## 196. Add localcmd interactive keep-open option
- [x] 196.1 Add `keep_open` to localcmd model/parser and command option catalogs.
- [x] 196.2 Implement Windows Terminal keep-open behavior for interactive localcmd runs.
- [x] 196.3 Expose `keep_open` in Flow Canvas localcmd properties and bridge round-trip/export ordering.
- [x] 196.4 Add focused parser/runtime/bridge tests and run verification.

### 196 Review
- Added `localcmd.keep_open` (interactive-only behavior).
- Runtime behavior:
- When `interactive: true` and `keep_open: true` with Windows Terminal available, localcmd now launches PowerShell with `-NoExit` (or cmd with `/K`) so the terminal does not auto-close when the command exits.
- Existing non-Windows-Terminal fallback behavior remains unchanged.
- Added parser/model support:
- `Services/Scripting/Models/ScriptStep.cs` (`LocalCmdOptions.KeepOpen`)
- `Services/Scripting/ScriptParser.cs` (`keep_open` parse + option catalog)
- Added Flow Canvas support:
- `FlowCanvas/src/blockDefs/registry.ts` localcmd property `keep_open`
- `Services/FlowCanvasBridge.cs` boolean normalization + localcmd export/order mapping for `keep_open`
- Added/updated tests:
- `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs` for keep-open shell arg construction (`-NoExit`/`/K`).
- `SSH_Helper.Tests/Scripting/LocalCmdParserTests.cs` for `keep_open` parse coverage.
- `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` for `keep_open` export round-trip.
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests|FullyQualifiedName~FlowCanvasBridgeTests" -v minimal` passed (`96/96`).

## 195. Add autocomplete required-tag coverage for localcmd
- [x] 195.1 Add `localcmd.command` to autocomplete required-option metadata.
- [x] 195.2 Extend autocomplete required-tag test matrix to include `localcmd`.
- [x] 195.3 Run focused autocomplete verification and capture result.

### 195 Review
- Root cause: `ScriptAutocompleteProvider` required-option map did not include `localcmd`, so option completions did not show `command` as `required`.
- Implementation:
- `Services/Editor/ScriptAutocompleteProvider.cs` now includes `["localcmd"] = ["command"]` in `RequiredOptionKeysByCommand`.
- `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs` now validates `localcmd` in `GetRequiredOptionTagCases`.
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptAutocompleteProviderTests" -v minimal` passed (`46/46`).

## 194. Add localcmd quiet + suppress output controls
- [x] 194.1 Add `quiet` and `suppress` options to localcmd runtime model/parser support.
- [x] 194.2 Apply send-style suppression behavior in `LocalCmdCommand` (banner + live stream suppression).
- [x] 194.3 Expose and round-trip localcmd `quiet`/`suppress` in Flow Canvas registry/bridge.
- [x] 194.4 Add focused parser/runtime/bridge tests and run verification.

### 194 Review
- Added localcmd options:
- `quiet`: suppresses only localcmd command-banner lines (`[localcmd] ...`, `[localcmd:background] ...`, `[localcmd:interactive] ...`).
- `suppress`: send-style suppression for localcmd, hiding both command-banner lines and live stdout/stderr panel streaming while preserving capture/`into_*` values.
- Runtime implementation (`LocalCmdCommand`) now resolves suppression as:
- `suppressOutput = localcmd.suppress || step.suppress`
- `suppressCommandEcho = suppressOutput || localcmd.quiet`
- Flow Canvas:
- `FlowCanvas/src/blockDefs/registry.ts` localcmd block now includes `quiet` and `suppress`.
- `Services/FlowCanvasBridge.cs` now exports/imports these booleans and includes localcmd preferred export ordering for drift-guard parity.
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests|FullyQualifiedName~FlowCanvasBridgeTests" -v minimal` passed (`93/93`).

## 193. Harden detached localcmd background startup against process-handle metadata faults
- [x] 193.1 Patch detached background cleanup to ignore post-spawn process handle disposal failures.
- [x] 193.2 Add regression coverage for detached background startup when PID access and handle disposal throw `InvalidOperationException`.
- [x] 193.3 Run focused localcmd command tests and capture verification evidence.

### 193 Review
- Root cause: detached background mode can encounter process-handle metadata failures (`No process is associated with this object.`) after successful spawn, but background semantics require success unless spawn itself fails.
- Implementation:
- `Services/Scripting/Commands/LocalCmdCommand.cs` now uses best-effort dispose (`TryDispose`) in detached mode so non-spawn metadata/disposal failures do not fail the step.
- `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs` adds `Background_Detached_IgnoresPidAndDisposeMetadataErrors`, forcing both PID access and dispose to throw while asserting startup still succeeds and `into_*` metadata degrades gracefully (`pid = -1`).
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests" -v minimal` passed (`20/20`).

## 192. Close localcmd implementation gaps from spec review
- [x] 192.1 Enforce confirmation provider behavior and wire default localcmd confirmation for all script execution paths.
- [x] 192.2 Implement foreground `timeout` enforcement for localcmd (including interactive foreground behavior).
- [x] 192.3 Implement background lifetime management (`detached|script|app`) and `kill_on_cancel` cleanup behavior.
- [x] 192.4 Add captured-output truncation marker behavior for `max_output_bytes`.
- [x] 192.5 Fix Flow Canvas localcmd option normalization (`args` list/scalar and `env` object JSON handling).
- [x] 192.6 Add/extend focused tests for the above behavior and run verification.

### 192 Review
- Local command confirmation is now enforced when `confirm != never`; if no provider is configured, `localcmd` fails with `on_error` semantics instead of auto-running.
- `SshExecutionService` now defaults to a concrete localcmd confirmation provider (`LocalCmdConfirmationDialog`) in all constructor paths, covering manual, Flow Canvas, and scheduler script execution.
- Foreground localcmd now enforces step timeout (`step.Timeout`) for both normal and interactive foreground runs, returning `ApplyOnError(...)` with a timeout message when exceeded.
- Background localcmd now implements lifetime tracking:
- `lifetime: detached` disposes process handles immediately and leaves process running.
- `lifetime: script` kills/disposes tracked background processes when the script run ends.
- `lifetime: app` keeps processes alive until app shutdown (`ProcessExit` best-effort kill/dispose).
- `kill_on_cancel` now applies during cancelled script cleanup for non-detached tracked background processes.
- Captured foreground stdout/stderr now append a truncation marker when `max_output_bytes` is exceeded per stream.
- Flow Canvas export normalization now handles localcmd options correctly:
- `args` accepts JSON array text or scalar string and serializes to valid runtime YAML.
- `env` accepts JSON object text and serializes as a mapping.
- Added/updated focused tests:
- `SSH_Helper.Tests/Scripting/LocalCmdCommandTests.cs` for confirmation-required behavior, timeout, truncation marker, and background lifetime cleanup.
- `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` for localcmd `args` and `env` export normalization.
- Verification:
- `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LocalCmdCommandTests|FullyQualifiedName~LocalCmdParserTests|FullyQualifiedName~FlowCanvasBridgeTests"` passed (`89/89`).

## 191. Harden updater against transient file locks on relaunch
- [x] 191.1 Add updater-script retry behavior for package copy and updated executable relaunch.
- [x] 191.2 Isolate updater temp folder by executable path identity to avoid cross-copy collisions.
- [x] 191.3 Extend focused updater temp-path tests for hashed path suffix and same-name/different-path isolation.
- [x] 191.4 Run focused/regression verification and document outcomes.

### 191 Review
- Root-cause hypothesis: portable installs under synced folders (for example OneDrive Desktop) can introduce transient file locks around self-update copy/relaunch, surfacing as Windows shell error “Another program is currently using this file.”
- Updated updater script in `Services/UpdateService.cs`:
- Added `Invoke-WithRetry` helper in embedded PowerShell.
- Wrapped `Copy-Item` update-package replace with retry.
- Wrapped `Start-Process` relaunch with retry.
- Updated temp staging isolation in `BuildUpdateTempDirectory(...)`:
- Temp path now includes executable-name stem + short hash token from normalized executable path (`SSH_Helper_Update_<name>_<hash8>`).
- This preserves standard vs portable separation and also isolates same exe names in different folders.
- Extended tests in `SSH_Helper.Tests/Services/UpdateServiceTempPathTests.cs`:
- existing tests now assert hashed suffix format.
- added `BuildUpdateTempDirectory_SameExeNameDifferentPaths_UsesDifferentDirectories`.
- Verification:
- Focused: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~UpdateServiceTempPathTests|FullyQualifiedName~SchedulerInstanceLockTests" ...` passed (`5/5`).
- Regression slice: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobStorageServiceTests|FullyQualifiedName~SchedulingServiceTests|FullyQualifiedName~UpdateServiceTempPathTests|FullyQualifiedName~SchedulerInstanceLockTests" ...` passed (`127/127`).

## 190. Prevent cross-edition update/scheduler runtime collisions
- [x] 190.1 Isolate updater temp working directory by executable/build flavor.
- [x] 190.2 Add a cross-instance scheduler lock so only one SSH Helper instance runs timed scheduler evaluations at once.
- [x] 190.3 Add focused tests for updater temp-path derivation and scheduler lock behavior.
- [x] 190.4 Run focused regression verification and document outcomes.

### 190 Review
- Updater temp path isolation:
- `Services/UpdateService.cs` now derives update temp working folders from the running exe name via `BuildUpdateTempDirectory(processPath, tempRoot)`.
- Both download staging (`DownloadUpdateAsync`) and updater script launch (`LaunchUpdaterAndExit`) now use this helper, so standard and portable builds use distinct temp directories (for example: `SSH_Helper_Update_SSH_Helper` vs `SSH_Helper_Update_SSH_Helper_Portable`).
- Added focused tests in `SSH_Helper.Tests/Services/UpdateServiceTempPathTests.cs`:
- `BuildUpdateTempDirectory_UsesExecutableFileNameStem`
- `BuildUpdateTempDirectory_EmptyProcessPath_UsesFallbackToken`
- Scheduler collision prevention:
- Added `Utilities/SchedulerInstanceLock.cs` (named mutex lock) and wired it into `Form1` scheduler bootstrap/cleanup flow so timed scheduler evaluation starts only when lock ownership is acquired.
- Strengthened `SchedulerInstanceLock` with in-process ownership tracking to prevent same-process re-entrant ownership through multiple lock objects.
- Added focused tests in `SSH_Helper.Tests/Utilities/SchedulerInstanceLockTests.cs`:
- `TryAcquire_SameNameSecondLock_FailsWhileFirstHeld`
- `TryAcquire_AfterFirstDisposed_SecondCanAcquire`
- Verification:
- RED (before scheduler-lock ownership guard): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulerInstanceLockTests|FullyQualifiedName~UpdateServiceTempPathTests" ...` failed as expected (`1` failing test).
- GREEN focused: same filter passed (`4/4`).
- Regression slice: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobStorageServiceTests|FullyQualifiedName~SchedulingServiceTests|FullyQualifiedName~SchedulerInstanceLockTests|FullyQualifiedName~UpdateServiceTempPathTests" ...` passed (`126/126`).

## 189. Isolate Credential Manager targets between standard and portable builds
- [x] 189.1 Add build-flavor-aware credential target generation in `CredentialTargets`.
- [x] 189.2 Add focused tests proving portable targets use a separate prefix.
- [x] 189.3 Run credential regression tests and document results.
- [x] 189.4 Update docs to mention credential target isolation behavior.

### 189 Review
- Implemented build-flavor credential target scoping in `Services/Credentials/CredentialTargets.cs`.
- Public target APIs now resolve prefix from `AppDataPaths.IsPortableBuild`.
- Added internal helpers for deterministic testing:
- `BuildDefaultPasswordTarget(bool portableBuild)`
- `BuildHostPasswordTarget(bool portableBuild, ...)`
- `BuildJobPasswordTarget(bool portableBuild, ...)`
- Prefix behavior:
- Standard build: `SSH_Helper:*`
- Portable build: `SSH_Helper_Portable:*`
- Added tests in `SSH_Helper.Tests/Services/CredentialTargetsTests.cs`:
- `BuildDefaultPasswordTarget_PortableBuild_UsesPortablePrefix`
- `BuildHostPasswordTarget_PortableBuild_UsesPortablePrefix`
- `BuildJobPasswordTarget_PortableBuild_UsesPortablePrefix`
- Verification:
- RED: `dotnet test ... --filter "FullyQualifiedName~CredentialTargetsTests"` failed before implementation with missing helper APIs.
- GREEN: same filter passed (`9/9`).
- Regression: `CredentialTargetsTests|JobExecutionServiceTests|JobStorageServiceTests` passed (`100/100`).
- Docs:
- Updated `README.md` release flavor section with credential isolation note.
- Updated `CHANGELOG.md` portable-release section with credential target scoping note.

## 188. Add portable release build with exe-local storage
- [x] 188.1 Create OpenSpec change (`proposal.md`, `tasks.md`, and spec delta) for portable release/storage behavior.
- [x] 188.2 Add portable-aware storage root resolution + writable validation in `AppDataPaths` and enforce startup failure in portable mode when unwritable.
- [x] 188.3 Replace direct `%LocalAppData%` runtime storage paths (`FlowCanvasForm`, `ScintillaNativeBootstrap`) with portable-aware app storage paths.
- [x] 188.4 Add build/publish support for `PortableBuild=true` (`SSH_Helper_Portable.exe`) and update GitHub release workflow to publish both standard + portable assets with checksums.
- [x] 188.5 Add/adjust automated tests for portable storage resolution and publish workflow assumptions; run focused verification commands.
- [x] 188.6 Update docs (`README.md`, `CHANGELOG.md`) for standard vs portable storage semantics and artifact names.

### 188 Review
- OpenSpec:
- Added `openspec/changes/add-portable-release-build/` with `proposal.md`, `tasks.md`, and `specs/scripting-runtime/spec.md` delta.
- Validation: `openspec validate add-portable-release-build --strict --no-interactive` passed.
- Runtime/storage implementation:
- `Utilities/AppDataPaths.cs` now supports compile-time portable mode via `PORTABLE_BUILD`, with:
- `IsPortableBuild`
- `ResolveAppFolder(...)` (portable => exe dir; standard => `%LocalAppData%\\SSH_Helper`)
- `TryEnsureFolderWritable(...)`
- `ValidateStartupStorageWritable(...)` for portable startup guard
- `Program.cs` now fails fast in portable mode with a clear message when storage is not writable.
- Path consumer updates:
- `UI/FlowCanvasForm.cs` WebView2 user data now resolves from `AppDataPaths.GetAppFolder()`.
- `Utilities/ScintillaNativeBootstrap.cs` extraction roots now prioritize app storage folder (portable-aware) before temp fallback.
- Build + release:
- `SSH_Helper.csproj` now supports `PortableBuild` property and conditionally defines `PORTABLE_BUILD` + `AssemblyName=SSH_Helper_Portable`.
- `.github/workflows/build-release.yml` now publishes standard + portable builds and releases:
- `SSH_Helper.exe`
- `SSH_Helper.exe.sha256`
- `SSH_Helper_Portable.exe`
- `SSH_Helper_Portable.exe.sha256`
- Documentation:
- Updated `README.md` install/config sections to document standard vs portable behavior.
- Added `CHANGELOG.md` entry describing portable artifact and storage semantics.
- Verification:
- RED: `dotnet test ... --filter "FullyQualifiedName~AppDataPathsTests"` failed initially with missing `AppDataPaths` APIs (expected).
- GREEN: same test filter passed (`4/4`).
- Focused regression: `AppDataPathsTests|FlowCanvasDistLocatorTests|BrowserCallbackWebViewProfileManagerTests|JobStorageServiceTests` passed (`49/49`).
- Publish verification:
- `dotnet publish ... -o artifacts/publish-standard` produced `SSH_Helper.exe`.
- `dotnet publish ... -p:PortableBuild=true -o artifacts/publish-portable` produced `SSH_Helper_Portable.exe`.

## 187. Harden GitHub build-release workflow before next tag build
- [x] 187.1 Re-confirm workflow risks and define minimal safe fix scope.
- [x] 187.2 Patch `build-release.yml` to ensure build job has Node/FlowCanvas dependencies available.
- [x] 187.3 Add explicit workflow token permissions needed for release publishing.
- [x] 187.4 Verify final workflow diff and capture review notes.

### 187 Review
- Root cause: `dotnet publish` in the Windows `build` job implicitly runs the `BuildFlowCanvas` target from `SSH_Helper.csproj`, but that job had no Node setup or `FlowCanvas` dependency install on a clean runner.
- Fix in `.github/workflows/build-release.yml`:
- added top-level `permissions: contents: write` so release creation does not depend on repository default workflow token permissions.
- added `actions/setup-node@v4` (`node-version: 20`) and `npm ci` in `build` before restore/publish so `npm run build` invoked by MSBuild has local dependencies.
- Verification:
- `git diff -- .github/workflows/build-release.yml` shows only the intended permission + build-job Node/npm additions.

## 186. Restore host-grid row-header current-row indicator glyph visibility
- [x] 186.1 Confirm root cause in row-header paint path and define minimal fix scope.
- [x] 186.2 Patch row-header number rendering to preserve the built-in indicator glyph area.
- [x] 186.3 Run focused host-grid UI verification and capture evidence.
- [x] 186.4 Document review notes (root cause, fix, verification).

### 186 Review
- Root cause: `Dgv_Variables_RowPostPaint` repainted the full row-header rectangle (`FillRectangle` + custom border) after default `DataGridView` painting, which erased the built-in current-row triangle glyph.
- Fix in `Form1.cs`:
- added `HostGridRowHeaderGlyphReservationWidth` constant.
- removed full row-header background/border repaint from `Dgv_Variables_RowPostPaint`.
- row index text now renders only in a right-side text rectangle that reserves the left glyph lane so the indicator remains visible.
- Verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ConnectionTestStatusTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\row-header-indicator-fix\bin\ -p:BaseIntermediateOutputPath=artifacts\row-header-indicator-fix\obj\` (passed: `6/6`).
- `dotnet build .\SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\row-header-indicator-fix-build\bin\ -p:BaseIntermediateOutputPath=artifacts\row-header-indicator-fix-build\obj\` (passed; existing warnings unchanged: `MSB3277`, `CS8602`, `xUnit1031`).

## 185. Enforce panel-order YAML export for all Flow Canvas blocks
- [x] 185.1 Audit panel order vs bridge preferred export order across all block commands.
- [x] 185.2 Patch remaining command-order mismatch(es) so export key order matches Properties panel order.
- [x] 185.3 Add drift-guard coverage that validates all block property orders against bridge export ordering.
- [x] 185.4 Run focused + regression verification and capture outcomes.
- [x] 185.5 Document review notes (root cause, implementation, evidence).

### 185 Review
- Root cause: after moving fallback ordering to parser declared keys, one command still diverged from panel display order: `extract` parser order (`from, pattern, into, ...`) did not match Properties panel order (`pattern, into, from, ...`).
- Fix in `Services/FlowCanvasBridge.cs`:
- extended `PreferredOptionOrderOverridesByCommand` with `extract` panel order: `pattern`, `into`, `from`, `match`, `required`.
- added `GetPreferredExportOptionOrderByCommand()` to expose resolved command ordering for drift-guard verification.
- Added/updated tests in `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`:
- `ExportGraphToYaml_ExtractOptions_AreSerializedInPropertiesPanelOrder`
- `DriftGuard_RegistryPanelOrder_MatchesBridgePreferredExportOrder_ForAllBlocks`
- Enhanced registry helper parsing to preserve property order and include `timeoutProp`/`onErrorProp` references so drift checks compare real panel order.
- Verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_ExtractOptions_AreSerializedInPropertiesPanelOrder|FullyQualifiedName~DriftGuard_RegistryPanelOrder_MatchesBridgePreferredExportOrder_ForAllBlocks" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\all-block-order-focused\bin\ -p:BaseIntermediateOutputPath=artifacts\all-block-order-focused\obj\` (passed: `2/2`).
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\all-block-order-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\all-block-order-regression\obj\` (passed: `56/56`).

## 184. Align playsound YAML key order with Properties panel
- [x] 184.1 Add a failing FlowCanvasBridge regression for `playsound` option order using the reported key set.
- [x] 184.2 Extend export preferred-order overrides so `playsound` emits panel-order keys (`path`, `max_seconds`, `into`, `wait`, `volume`, `on_error`).
- [x] 184.3 Run focused + bridge-regression verification and capture outcomes.
- [x] 184.4 Document review notes with root cause and verification evidence.

### 184 Review
- Root cause: export fallback ordering for non-overridden commands used `ScriptParser.GetKnownStepOptionKeysByCommand()`, which returns alphabetically sorted keys; for `playsound` that produced `into, max_seconds, path, volume, ...` instead of panel order.
- Added RED regression in `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`:
- `ExportGraphToYaml_PlaySoundOptions_AreSerializedInPropertiesPanelOrder`
- RED verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_PlaySoundOptions_AreSerializedInPropertiesPanelOrder" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\playsound-order-red\bin\ -p:BaseIntermediateOutputPath=artifacts\playsound-order-red\obj\` (failed as expected: actual `into, max_seconds, path, volume, on_error`).
- Fix in `Services/FlowCanvasBridge.cs`:
- extended `PreferredOptionOrderOverridesByCommand` with `playsound` panel order:
- `path`, `max_seconds`, `into`, `wait`, `volume`, `on_error`.
- GREEN verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_PlaySoundOptions_AreSerializedInPropertiesPanelOrder" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\playsound-order-green\bin\ -p:BaseIntermediateOutputPath=artifacts\playsound-order-green\obj\` (passed: `1/1`).
- Regression verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\playsound-order-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\playsound-order-regression\obj\` (passed: `52/52`).

## 183. Align Flow Canvas YAML option order with Properties panel order
- [x] 183.1 Confirm current export ordering behavior and identify panel-order mismatches (e.g., `send`, `choose`, `multiselect`, `confirm`).
- [x] 183.2 Add failing FlowCanvasBridge regressions that assert canonical export option ordering matches Properties panel order.
- [x] 183.3 Patch export serialization to emit options in panel-aligned order while preserving unknown keys.
- [x] 183.4 Run focused + regression verification and capture outcomes.
- [x] 183.5 Add review notes with root cause, implementation details, and test evidence.

### 183 Review
- Root cause: non-container step export builds options from mutable `JObject` insertion order (snippet parse + prop-write sequence), so emitted YAML key order reflected property-write timing, not Properties panel ordering.
- Added RED regressions in `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`:
- `ExportGraphToYaml_SendOptions_AreSerializedInPropertiesPanelOrder`
- `ExportGraphToYaml_ChooseOptions_AreSerializedInPropertiesPanelOrder`
- RED verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_SendOptions_AreSerializedInPropertiesPanelOrder|FullyQualifiedName~ExportGraphToYaml_ChooseOptions_AreSerializedInPropertiesPanelOrder" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\panel-order-red\bin\ -p:BaseIntermediateOutputPath=artifacts\panel-order-red\obj\` (failed as expected with non-panel option order).
- Fix in `Services/FlowCanvasBridge.cs`:
- added panel-order overrides for `send`, `choose`, `multiselect`, and `confirm` (`PreferredOptionOrderOverridesByCommand`).
- added `ReorderOptionsForSerialization(...)` and `ResolvePreferredOptionOrder(...)`.
- serialization now reorders keys to panel-aligned sequence before YAML emission and appends unknown/non-panel keys afterward in stable order.
- default ordering for other commands uses parser option order with `on_error` forced to trailing position to match panel section layout.
- GREEN verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_SendOptions_AreSerializedInPropertiesPanelOrder|FullyQualifiedName~ExportGraphToYaml_ChooseOptions_AreSerializedInPropertiesPanelOrder" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\panel-order-green\bin\ -p:BaseIntermediateOutputPath=artifacts\panel-order-green\obj\` (passed: `2/2`).
- Regression verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\panel-order-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\panel-order-regression\obj\` (passed: `51/51`).

## 182. Fix Flow Canvas input `on_error` export rejection
- [x] 182.1 Confirm root cause across registry, export option catalogs, and parser input-map handling.
- [x] 182.2 Add focused failing regression coverage proving `input.on_error` exports and parses as canonical runtime syntax.
- [x] 182.3 Patch parser option catalog + input-map parsing so `input.on_error` is treated as a supported nested alias.
- [x] 182.4 Run focused verification (`FlowCanvasBridgeTests` + parser tests) and record outcomes.
- [x] 182.5 Add review notes with root cause, fix summary, and verification evidence.

### 182 Review
- Root cause: Flow Canvas registry exposes `input.on_error`, but the parser/export option catalog (`ScriptParser.CommandOptionKeys["input"]`) omitted `on_error`, and `ParseInputOptions(...)` did not map nested `on_error` into `step.OnError`.
- This mismatch caused bridge export rejection with: `Block 'input' contains unsupported or invalid properties: on_error`.
- Added RED regressions:
- `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`: `ExportGraphToYaml_InputWithOnError_ExportsSuccessfully`
- `SSH_Helper.Tests/Scripting/ScriptParserTests.cs`: `Parse_InputOnErrorInsideMap_ParsesOnError`
- RED verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Parse_InputOnErrorInsideMap_ParsesOnError|FullyQualifiedName~ExportGraphToYaml_InputWithOnError_ExportsSuccessfully" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\input-onerror-red\bin\ -p:BaseIntermediateOutputPath=artifacts\input-onerror-red\obj\` (failed as expected: export rejected `on_error`; parser left `OnError` null).
- Fix in `Services/Scripting/ScriptParser.cs`:
- added `on_error` to known input option keys.
- updated input parse dispatch to pass `ScriptStep` into `ParseInputOptions(...)`.
- added `on_error`/`onerror` handling in `ParseInputOptions(...)` via `ApplyNestedOnErrorAlias(step, parser)`.
- GREEN verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Parse_InputOnErrorInsideMap_ParsesOnError|FullyQualifiedName~ExportGraphToYaml_InputWithOnError_ExportsSuccessfully" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\input-onerror-green\bin\ -p:BaseIntermediateOutputPath=artifacts\input-onerror-green\obj\` (passed: `2/2`).
- Regression verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests|FullyQualifiedName~ScriptParserTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\input-onerror-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\input-onerror-regression\obj\` (passed: `204/204`).

## 180. Align Flow Canvas required markers with parser/runtime validation
- [x] 180.1 Add failing parser validation tests for missing required checks (`choose.into/options`, `multiselect.into/options`, `confirm.into`, `webhook.url`, `log.message`).
- [x] 180.2 Add/extend FlowCanvas bridge export tests so required-option enforcement matches parser-required behavior (including `extract.from`, `browser_callback_capture.into`, and conditional `readfile.path`).
- [x] 180.3 Add failing FlowCanvas e2e coverage for static + conditional required `*` markers in the Properties panel.
- [x] 180.4 Patch parser validation, FlowCanvas export required checks, block registry required flags, and dynamic Properties required evaluation.
- [x] 180.5 Update docs (`SCRIPTING.md`, `README.md`, `docs/flow-canvas-browser-harness.md`, `CHANGELOG.md`) and run focused verification (`dotnet test`, Playwright, `npm run build`).
- [x] 180.6 Record implementation + verification outcomes in the review section.

### 180 Review
- Added parser validation coverage and implementation for missing required fields:
- `choose.into/options`, `multiselect.into/options`, `confirm.into`, `webhook.url`, and `log.message`.
- Added/updated FlowCanvas export coverage to enforce parser-led required behavior:
- added missing required checks (`extract.from`, `browser_callback_capture.into`),
- removed incorrect hard requirements (`input.prompt`, `choose.prompt`, `multiselect.prompt`, `confirm.prompt`, `portcheck.port`, `writefile.content`),
- preserved/validated conditional requirements (`readfile.path` with `select_file`, HTTP auth credentials, interactive headless constraints).
- Added dedicated FlowCanvas required-marker e2e fixture + tests in `FlowCanvas/e2e/flow-canvas-properties-typing.spec.ts` and `FlowCanvas/e2e/fixtures/graphs.ts`:
- static required stars (`extract.from`, `browser_callback_capture.into`, prompt/port/content optional fields),
- conditional required stars (`readfile.path`, HTTP auth credentials by mode, interactive headless command/limiter behavior).
- Updated runtime/UI alignment code:
- `Services/Scripting/ScriptParser.cs`: required validation cases added.
- `Services/FlowCanvasBridge.cs`: required-option map aligned + conditional required checks.
- `FlowCanvas/src/blockDefs/registry.ts`: static required flags corrected.
- `FlowCanvas/src/panels/Properties.tsx`: dynamic required-evaluation logic for conditional fields.
- Documentation updates completed:
- `SCRIPTING.md`, `README.md`, `docs/flow-canvas-browser-harness.md`, `CHANGELOG.md`.
- Red verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Validate_ChooseWithoutInto_ReturnsError|FullyQualifiedName~Validate_ChooseWithoutOptions_ReturnsError|FullyQualifiedName~Validate_MultiselectWithoutInto_ReturnsError|FullyQualifiedName~Validate_MultiselectWithoutOptions_ReturnsError|FullyQualifiedName~Validate_ConfirmWithoutInto_ReturnsError|FullyQualifiedName~Validate_WebhookWithoutUrl_ReturnsError|FullyQualifiedName~Validate_LogMapWithoutMessage_ReturnsError|FullyQualifiedName~ExportGraphToYaml_ExtractMissingFrom_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_BrowserCallbackCaptureMissingInto_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_InputWithoutPrompt_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_ChooseWithoutPrompt_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_MultiselectWithoutPrompt_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_ConfirmWithoutPrompt_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_PortcheckWithoutPort_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_WritefileWithoutContent_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_ReadfileSelectFileWithoutPath_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_HttpBasicAuthWithoutUsername_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_HttpBearerAuthWithoutToken_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_InteractiveHeadlessWithoutCommand_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_InteractiveHeadlessWithoutLimiter_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_InteractiveHeadlessWithCommandAndLimiter_ExportsSuccessfully" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\required-markers-red\bin\ -p:BaseIntermediateOutputPath=artifacts\required-markers-red\obj\` (failed as expected: 19 failures before fixes).
- `npx playwright test e2e/flow-canvas-properties-typing.spec.ts --grep "Flow Canvas Required Markers"` (failed as expected on missing required stars before fixes).
- Green verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Validate_ChooseWithoutInto_ReturnsError|FullyQualifiedName~Validate_ChooseWithoutOptions_ReturnsError|FullyQualifiedName~Validate_MultiselectWithoutInto_ReturnsError|FullyQualifiedName~Validate_MultiselectWithoutOptions_ReturnsError|FullyQualifiedName~Validate_ConfirmWithoutInto_ReturnsError|FullyQualifiedName~Validate_WebhookWithoutUrl_ReturnsError|FullyQualifiedName~Validate_LogMapWithoutMessage_ReturnsError|FullyQualifiedName~ExportGraphToYaml_ExtractMissingFrom_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_BrowserCallbackCaptureMissingInto_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_InputWithoutPrompt_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_ChooseWithoutPrompt_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_MultiselectWithoutPrompt_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_ConfirmWithoutPrompt_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_PortcheckWithoutPort_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_WritefileWithoutContent_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_ReadfileSelectFileWithoutPath_ExportsSuccessfully|FullyQualifiedName~ExportGraphToYaml_HttpBasicAuthWithoutUsername_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_HttpBearerAuthWithoutToken_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_InteractiveHeadlessWithoutCommand_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_InteractiveHeadlessWithoutLimiter_ReturnsRequiredOptionError|FullyQualifiedName~ExportGraphToYaml_InteractiveHeadlessWithCommandAndLimiter_ExportsSuccessfully" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\required-markers-green3\bin\ -p:BaseIntermediateOutputPath=artifacts\required-markers-green3\obj\` (passed: 21/21).
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests|FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\required-markers-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\required-markers-regression\obj\` (passed: 199/199).
- `npx playwright test e2e/flow-canvas-properties-typing.spec.ts` (passed: 8/8).
- `npm run build` in `FlowCanvas` (passed; refreshed `FlowCanvas/dist` assets).

## 178. Flow Canvas path property file browser support
- [x] 178.1 Add focused FlowCanvas e2e coverage that path property fields expose a Browse action and apply the host-selected file path.
- [x] 178.2 Add a canvas host message contract for browse-path request/response and wire the Properties panel to request and consume browse results.
- [x] 178.3 Handle browse-path requests in WinForms (`FlowCanvasForm` + `Form1`) using the existing file picker flow with test override support.
- [x] 178.4 Run focused FlowCanvas e2e and .NET UI tests, then record outcomes in the review section.

### 178 Review
- Added focused FlowCanvas e2e coverage in `FlowCanvas/e2e/flow-canvas-properties-typing.spec.ts` plus `createPathPropertyFixture()` in `FlowCanvas/e2e/fixtures/graphs.ts` to prove path fields can request browse and consume host-selected file paths.
- Red verification confirmed missing browse UI before implementation:
- `npm run test:e2e -- e2e/flow-canvas-properties-typing.spec.ts -g "path fields can request host browse and apply selected path"` (failed as expected on missing `properties-field-path-text-browse`).
- Added explicit path-browse metadata (`browse: 'file'`) for local-path fields in `FlowCanvas/src/blockDefs/registry.ts`.
- Added canvas browse request/response contract in `FlowCanvas/src/communication-message-types.ts`:
- outgoing: `browse-path`
- incoming: `browse-path-result`
- Updated `FlowCanvas/src/panels/Properties.tsx`:
- path text fields with `browse: 'file'` now render a `Browse...` button (`*-browse` test id).
- click sends `browse-path` with `requestId`, node/field context, current path, and title.
- incoming `browse-path-result` matched by `requestId` updates the input value when not canceled.
- Updated host wiring:
- `UI/FlowCanvasForm.cs` now forwards `browse-path` messages via new `OnBrowsePath` event.
- `Form1.cs` now handles browse requests, reuses shared file-picker logic (with existing `_filePathPickerOverrideForTests` support), and sends `browse-path-result` back to canvas.
- Added focused WinForms coverage: `SSH_Helper.Tests/UI/Form1FlowCanvasBrowsePathTests.cs`.
- Green verification:
- `npx playwright test e2e/flow-canvas-properties-typing.spec.ts --grep "Flow Canvas Path Browsing"` (passed: `1/1`).
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasBrowsePathTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\flow-canvas-browse-path-green\bin\ -p:BaseIntermediateOutputPath=artifacts\flow-canvas-browse-path-green\obj\` (passed: `2/2`).
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ScriptContextMenuTests|FullyQualifiedName~Form1FlowCanvasBrowsePathTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\flow-canvas-browse-path-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\flow-canvas-browse-path-regression\obj\` (passed: `7/7`).
- `npm run build` in `FlowCanvas` succeeded and refreshed `FlowCanvas/dist` assets.

## 179. Flow Canvas browse dialog owner focus regression
- [x] 179.1 Add a focused WinForms regression proving Flow Canvas browse requests pass the Flow Canvas window as dialog owner.
- [x] 179.2 Patch Flow Canvas browse path selection to use the canvas form (not main `Form1`) as the picker owner.
- [x] 179.3 Run focused WinForms verification and capture outcomes.

### 179 Review
- Root cause: `Form1.SelectPathForFlowCanvas(...)` was still calling `SelectFilePath(this, ...)`, making the main form (`Form1`) the dialog owner, so Windows activated the main window when Browse opened.
- Added focused regression in `SSH_Helper.Tests/UI/Form1FlowCanvasBrowsePathTests.cs`:
- `HandleFlowCanvasBrowsePathRequest_UsesFlowCanvasAsDialogOwner`
- Red verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasBrowsePathTests.HandleFlowCanvasBrowsePathRequest_UsesFlowCanvasAsDialogOwner" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\flow-canvas-browse-owner-red\bin\ -p:BaseIntermediateOutputPath=artifacts\flow-canvas-browse-owner-red\obj\` (failed as expected: owner was `Form1` instead of `FlowCanvasForm`).
- Fix in `Form1.cs`:
- `SelectPathForFlowCanvas(...)` now uses `_flowCanvasForm` as dialog owner when available (falls back to `Form1` only if the canvas form is missing/disposed).
- Green verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasBrowsePathTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\flow-canvas-browse-owner-green\bin\ -p:BaseIntermediateOutputPath=artifacts\flow-canvas-browse-owner-green\obj\` (passed: `3/3`).

## 177. Align autocomplete required-option tags with command requirements
- [x] 177.1 Audit required option keys across step commands and identify mismatches between parser/runtime validation and autocomplete required-tag metadata.
- [x] 177.2 Update autocomplete required-key metadata to mark missing required options (including choose/multiselect options and other audited gaps).
- [x] 177.3 Add focused regression tests that assert required-tag details on command option suggestions.
- [x] 177.4 Run focused autocomplete verification and record outcomes.

### 177 Review
- Root cause: `ScriptAutocompleteProvider` used a static `RequiredOptionKeysByCommand` map that had drifted from command validation/runtime requirements. Example: `choose.options` (and `multiselect.options`) were not tagged required even though those commands fail without options.
- Audited and corrected required-key metadata in `Services/Editor/ScriptAutocompleteProvider.cs`:
- added missing required tags for `choose.options`, `multiselect.options`, and `exists.into`.
- added block-required tags for control-flow commands: `if.then`, `foreach.do`, `while.do`.
- aligned `readfile` required tags to include both `path` and `into`.
- Added focused regression coverage in `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs`:
- updated choose/multiselect/readfile option tests to assert `Detail == "required"` on the required keys.
- added theory `GetCompletion_CommandStepOptionKey_MarksAuditedRequiredOptions` to enforce required tags across a command set (`send`, `if`, `foreach`, `while`, `exists`, `choose`, `multiselect`, `confirm`, `assert`, `switch`, `browser_callback_capture`).
- Verification:
- Focused: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptAutocompleteProviderTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-required-tags-green\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-required-tags-green\obj\` (passed: `45/45`).
- Regression: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-required-tags-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-required-tags-regression\obj\` (passed: `84/84`).

## 176. Smart Enter fallback indentation on empty payload lines
- [x] 176.1 Add failing utility/UI regressions for pressing `Enter` on an empty indented line inside a step payload and expecting fallback to sibling command indentation.
- [x] 176.2 Update smart-enter logic so empty payload lines dedent to the next command/block indentation level.
- [x] 176.3 Run focused editor utility/UI verification and record outcomes.

### 176 Review
- Added regressions:
- `SSH_Helper.Tests/Editor/EditorTextUtilitiesTests.cs`: `ApplySmartEnter_OnEmptyIndentedRootCommandPayloadLine_DedentsToCommandIndent`
- `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs`: `SmartEnter_OnEmptyIndentedRootCommandPayloadLine_DedentsToCommandIndent`
- Root cause: `EditorTextUtilities.ApplySmartEnter(...)` always reused current line indentation when `Enter` was pressed on a whitespace-only line, so an empty payload line stayed nested instead of falling back to the sibling command/block level.
- Updated `Services/Editor/EditorTextUtilities.cs`:
- added `TryResolveEmptyStepPayloadFallbackIndent(...)` and `TryGetPreviousSignificantLine(...)`.
- on empty lines under direct step payload options, smart-enter now inserts the next line at the sibling step indentation (where the next command block belongs).
- Verification:
- Red: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ApplySmartEnter_OnEmptyIndentedRootCommandPayloadLine_DedentsToCommandIndent|FullyQualifiedName~SmartEnter_OnEmptyIndentedRootCommandPayloadLine_DedentsToCommandIndent" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\smart-enter-empty-line-fallback-red\bin\ -p:BaseIntermediateOutputPath=artifacts\smart-enter-empty-line-fallback-red\obj\` (failed as expected: `2/2`).
- Green (focused): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ApplySmartEnter_OnEmptyIndentedRootCommandPayloadLine_DedentsToCommandIndent|FullyQualifiedName~SmartEnter_OnEmptyIndentedRootCommandPayloadLine_DedentsToCommandIndent" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\smart-enter-empty-line-fallback-green\bin\ -p:BaseIntermediateOutputPath=artifacts\smart-enter-empty-line-fallback-green\obj\` (passed: `2/2`).
- Green (regression): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~EditorTextUtilitiesTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\smart-enter-empty-line-fallback-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\smart-enter-empty-line-fallback-regression\obj\` (passed: `52/52`).

## 175. Prevent autocomplete on non-text keyup (Print Screen)
- [x] 175.1 Add failing UI regression proving `Print Screen` keyup does not open autocomplete popup.
- [x] 175.2 Tighten keyup autocomplete trigger filtering to ignore non-text keys like `Print Screen`.
- [x] 175.3 Run focused `ScintillaScriptEditorControl` verification and record outcomes.

### 175 Review
- Added focused regression in `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs`:
- `CompletionPopup_PrintScreenKeyUp_DoesNotTriggerSuggestions`
- Root cause: `ScintillaScriptEditorControl.ShouldTriggerAutocompleteOnKeyUp(...)` treated `Keys.Snapshot` (`Print Screen`) as a valid key-up trigger, so taking a screenshot could open the autocomplete popup unexpectedly.
- Updated `UI/ScintillaScriptEditorControl.cs`:
- added `Keys.Snapshot` to the excluded key list in `ShouldTriggerAutocompleteOnKeyUp(...)`.
- Verification:
- Red: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CompletionPopup_PrintScreenKeyUp_DoesNotTriggerSuggestions" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-printscreen-red\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-printscreen-red\obj\` (failed as expected before fix).
- Green: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CompletionPopup_PrintScreenKeyUp_DoesNotTriggerSuggestions|FullyQualifiedName~ScintillaScriptEditorControlTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-printscreen-green\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-printscreen-green\obj\` (passed: `38/38`).
- Green (verification rerun): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CompletionPopup_PrintScreenKeyUp_DoesNotTriggerSuggestions" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-printscreen-verify\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-printscreen-verify\obj\` (passed: `1/1`).

## 174. Smart Enter indentation for step option keys
- [x] 174.1 Add failing smart-enter tests for scalar step option keys ending with `:` to ensure sibling-option indentation is preserved.
- [x] 174.2 Update smart-enter indentation logic so step option keys default to same-indent continuation while nested block keys still indent deeper.
- [x] 174.3 Run focused editor utility/UI autocomplete verification and record outcomes.

### 174 Review
- Added regressions:
- `SSH_Helper.Tests/Editor/EditorTextUtilitiesTests.cs`
- `ApplySmartEnter_OnScalarStepOptionKeyWithoutValue_KeepsSameIndent`
- `ApplySmartEnter_OnNestedStepOptionKeyWithoutValue_IndentsDeeper`
- `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs`
- `SmartEnter_OnScalarStepOptionKey_DoesNotOverIndentNextLine`
- Root cause: `EditorTextUtilities.ApplySmartEnter(...)` always deepened indentation for any line ending with `:`, which incorrectly treated scalar step options (`from:`, `into:`, `pattern:`) like nested mapping roots.
- Updated `Services/Editor/EditorTextUtilities.cs`:
- added step-option-aware mapping-key indentation logic.
- scalar step option keys now continue at the same indentation level.
- known nested block step keys (for example `respond`, `cases`, `then`, `do`, `catch`, `finally`, `headers`, `options`, `columns`) still indent one level deeper.
- Verification:
- Red: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ApplySmartEnter_OnScalarStepOptionKeyWithoutValue_KeepsSameIndent|FullyQualifiedName~ApplySmartEnter_OnNestedStepOptionKeyWithoutValue_IndentsDeeper|FullyQualifiedName~SmartEnter_OnScalarStepOptionKey_DoesNotOverIndentNextLine" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\smart-enter-step-option-red\bin\ -p:BaseIntermediateOutputPath=artifacts\smart-enter-step-option-red\obj\` (failed as expected on scalar option indent behavior).
- Green (focused): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ApplySmartEnter_OnScalarStepOptionKeyWithoutValue_KeepsSameIndent|FullyQualifiedName~ApplySmartEnter_OnNestedStepOptionKeyWithoutValue_IndentsDeeper|FullyQualifiedName~SmartEnter_OnScalarStepOptionKey_DoesNotOverIndentNextLine" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\smart-enter-step-option-green\bin\ -p:BaseIntermediateOutputPath=artifacts\smart-enter-step-option-green\obj\` (passed: `3/3`).
- Green (regression): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~EditorTextUtilitiesTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\smart-enter-step-option-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\smart-enter-step-option-regression\obj\` (passed: `49/49`).

## 173. Ctrl+Space on root-level command-list blank lines
- [x] 173.1 Add failing provider/UI tests for manual autocomplete on blank lines after root-level `- <command>` blocks (no `steps:` key).
- [x] 173.2 Extend manual autocomplete context inference to detect root-level command-list continuation and show step suggestions there.
- [x] 173.3 Run focused verification for autocomplete provider/editor tests and record outcomes.

### 173 Review
- Added regressions:
- `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs`: `GetCompletion_BlankLine_AfterIndentlessStepsSequence_ManualRequest_SuggestsStepCommands`
- `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs`: `CompletionPopup_BlankLine_AfterIndentlessStepsSequence_CtrlSpaceShowsStepCommands`
- Root cause: step context inference assumed `steps` children were always indented (`steps:` then `  - ...`). Your script uses valid YAML indentless sequence style (`steps:` then `- ...`), so `Ctrl+Space` on the trailing blank line resolved to no context and returned no items.
- Updated `Services/Editor/ScriptAutocompleteProvider.cs`:
- step-sequence inference now recognizes both direct/ancestor step command lines (including indentless style) when resolving continuation context.
- no-dash step-command completion now only activates at true step-item indent, plus the explicit manual blank-line bridge case at column 0; this prevents overriding option-key autocomplete contexts.
- Verification:
- Red: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~GetCompletion_BlankLine_AfterIndentlessStepsSequence_ManualRequest_SuggestsStepCommands|FullyQualifiedName~CompletionPopup_BlankLine_AfterIndentlessStepsSequence_CtrlSpaceShowsStepCommands" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-indentless-steps-red\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-indentless-steps-red\obj\` (failed as expected: `2/2`).
- Green (focused): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~GetCompletion_BlankLine_AfterIndentlessStepsSequence_ManualRequest_SuggestsStepCommands|FullyQualifiedName~CompletionPopup_BlankLine_AfterIndentlessStepsSequence_CtrlSpaceShowsStepCommands|FullyQualifiedName~GetCompletion_StepPrefixWithoutDash_WithinSteps_SuggestsStepCommandsAndPrependsListMarker|FullyQualifiedName~CompletionCommit_StepCommandWithoutDash_PrependsDashAndAppendsColonAndSpace" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-indentless-steps-green\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-indentless-steps-green\obj\` (passed: `4/4`).
- Green (regression): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-indentless-steps-regression2\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-indentless-steps-regression2\obj\` (passed: `70/70`).

## 172. Ctrl+Space autocomplete on blank line after steps
- [x] 172.1 Add failing provider/UI tests for manual (`Ctrl+Space`) autocomplete on blank lines following a `steps` block.
- [x] 172.2 Implement manual-request autocomplete context so blank lines after `steps` suggest step commands and commit inserts proper list marker/indent.
- [x] 172.3 Run focused verification for autocomplete provider and editor control tests; record outcomes.

### 172 Review
- Added/updated regressions:
- `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs`: `GetCompletion_BlankTopLevelLine_AfterVarsAndSteps_ManualRequest_SuggestsStepCommands`
- `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs`: `CompletionPopup_BlankTopLevelLine_AfterSteps_CtrlSpaceShowsStepCommands`
- Root cause: blank-line completion after `steps:` used the same provider path as auto typing and had no manual-request context, so it resolved as top-level blank line and returned no suggestions.
- Updated runtime behavior:
- `UI/ScintillaScriptEditorControl.cs` now routes `Ctrl+Space` through a manual completion request flag and preserves that mode while popup content refreshes/commits.
- `Services/Editor/ScriptAutocompleteProvider.cs` now accepts a manual-request mode and, for blank or unindented lines immediately after a `steps` block, infers step-item context and returns step command completions with proper `  - ` insertion text.
- Verification:
- Red: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~GetCompletion_BlankTopLevelLine_AfterVarsAndSteps_ManualRequest_SuggestsStepCommands|FullyQualifiedName~CompletionPopup_BlankTopLevelLine_AfterSteps_CtrlSpaceShowsStepCommands" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-blankline-manual-red\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-blankline-manual-red\obj\` (failed as expected before implementation: provider API/manual context missing).
- Green: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-blankline-manual-green\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-blankline-manual-green\obj\` (passed: `68/68`).

## 171. Root step autocomplete without manual dash prefix
- [x] 171.1 Add failing coverage for step-root autocomplete on blank `steps` list lines without a leading `- ` marker.
- [x] 171.2 Update autocomplete completion logic so root step commands are suggested without `- ` and selecting a suggestion injects `- ` when missing.
- [x] 171.3 Run focused verification for provider/UI autocomplete tests and capture results.

### 171 Review
- Added regressions:
- `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs`: `GetCompletion_StepPrefixWithoutDash_WithinSteps_SuggestsStepCommandsAndPrependsListMarker`
- `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs`: `CompletionCommit_StepCommandWithoutDash_PrependsDashAndAppendsColonAndSpace`
- Root cause: step-command autocomplete only matched `^\s*-\s+...$`, so `steps` list lines without the manual `- ` prefix were not treated as command context.
- Updated `Services/Editor/ScriptAutocompleteProvider.cs`:
- added step-root detection for lines inside `steps:` blocks at the item indent even when no leading list marker is present.
- for that context, completion items keep command labels but insert text is emitted as `- <command>`, so commit adds the missing YAML list marker automatically.
- Verification:
- Red: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~GetCompletion_StepPrefixWithoutDash_WithinSteps_SuggestsStepCommandsAndPrependsListMarker|FullyQualifiedName~CompletionCommit_StepCommandWithoutDash_PrependsDashAndAppendsColonAndSpace" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-step-root-red\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-step-root-red\obj\` (failed as expected: `2/2`).
- Green: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~ScintillaScriptEditorControlTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\autocomplete-step-root-green\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-step-root-green\obj\` (passed: `68/68`).

## 170. Fix empty-quote closing-caret path insertion regression
- [x] 170.1 Add focused WinForms regression for `path: ""` when caret is positioned after the closing quote and `Path Browser...` is clicked.
- [x] 170.2 Refine quote-context detection so closing-quote caret positions on empty placeholders normalize to single-quoted YAML path output.
- [x] 170.3 Ensure non-empty closed quoted values are not misclassified as lone-opening quote contexts.
- [x] 170.4 Run focused verification for `Form1ScriptContextMenuTests` and capture evidence.

### 170 Review
- Added regression test in `SSH_Helper.Tests/UI/Form1ScriptContextMenuTests.cs`:
- `PathBrowserMenuClick_AfterClosingDoubleQuoteOfEmptyPair_ConvertsToSingleQuotedYamlPath`
- Root cause: quote-context detection assumed `selectionStart - 1` was always the opening quote. With caret after the closing quote of `""`, that index is the closing quote, so insertion preserved the leading `"` and produced `"'<path>'`.
- Updated `Form1.cs` quote insertion logic:
- supports explicit empty-pair closing-caret detection (`path: ""` with caret after second quote) by shifting replacement start to the first placeholder quote.
- adds lone-opening quote gating based on odd quote-count context so non-empty closed quoted values are not incorrectly treated as open quote contexts.
- Verification:
- Red: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PathBrowserMenuClick_AfterClosingDoubleQuoteOfEmptyPair_ConvertsToSingleQuotedYamlPath" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\path-browser-empty-pair-red\bin\ -p:BaseIntermediateOutputPath=artifacts\path-browser-empty-pair-red\obj\` (failed as expected with leading extra `"`).
- Green: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ScriptContextMenuTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\path-browser-empty-pair-green\bin\ -p:BaseIntermediateOutputPath=artifacts\path-browser-empty-pair-green\obj\` (passed: `5/5`).

## 169. Auto-complete lone quote path insertion
- [x] 169.1 Add focused WinForms tests for `Path Browser...` behavior after lone opening `"` and `'`.
- [x] 169.2 Update insertion logic to convert quote-triggered insertions into YAML-safe single-quoted values.
- [x] 169.3 Preserve existing behaviors for wrapped-quote and non-quote insertion paths.
- [x] 169.4 Run focused verification for `Form1ScriptContextMenuTests` and capture outcomes.

### 169 Review
- Added quote-completion regressions in `SSH_Helper.Tests/UI/Form1ScriptContextMenuTests.cs`:
- `PathBrowserMenuClick_AfterLoneDoubleQuote_ConvertsToSingleQuotedYamlPath`
- `PathBrowserMenuClick_AfterLoneSingleQuote_ConvertsToSingleQuotedYamlPath`
- Updated `Form1.cs` insertion engine:
- quote-triggered insertion now supports both wrapped-quote (`"..."` / `'...'`) and lone opening quote contexts (`path: "` / `path: '`).
- inserted values are normalized to YAML single-quoted scalars via `BuildSingleQuotedYamlScalar(...)`.
- single quotes inside paths are escaped (`''`) to keep YAML valid.
- non-quote insertion remains raw text insertion behavior.
- Verification:
- Red: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ScriptContextMenuTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\path-browser-lone-quote-red\bin\ -p:BaseIntermediateOutputPath=artifacts\path-browser-lone-quote-red\obj\` (failed as expected on lone-quote assertions before code update).
- Green: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ScriptContextMenuTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\path-browser-lone-quote-green\bin\ -p:BaseIntermediateOutputPath=artifacts\path-browser-lone-quote-green\obj\` (passed: `4/4`).

## 168. Improve path browser insertion for YAML-valid Windows paths
- [x] 168.1 Add/adjust focused WinForms tests that reproduce quoted-path insertion behavior and enforce YAML-safe output for Windows paths.
- [x] 168.2 Update path insertion logic so when insertion happens inside surrounding double quotes, the value is converted to single-quoted YAML path text.
- [x] 168.3 Keep non-quoted insertion behavior unchanged and verify caret placement remains sensible after insertion.
- [x] 168.4 Run focused verification for the updated Form1 script context-menu tests and capture evidence.

### 168 Review
- Updated `SSH_Helper.Tests/UI/Form1ScriptContextMenuTests.cs`:
- `PathBrowserMenuClick_InsideDoubleQuotes_ConvertsToSingleQuotedYamlPath`
- `PathBrowserMenuClick_OutsideDoubleQuotes_InsertsRawPathAtCaret`
- Updated `Form1.cs` path insertion behavior:
- added context-aware insertion result builder that detects insertion wrapped by `"` and rewrites to single-quoted YAML (`'C:\...`).
- preserved default insertion path when not wrapped by double quotes.
- caret placement is now based on computed insertion result for both paths.
- Verification:
- Red: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ScriptContextMenuTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\path-browser-quote-red\bin\ -p:BaseIntermediateOutputPath=artifacts\path-browser-quote-red\obj\` (failed as expected on quoted-path assertion before code change).
- Green: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ScriptContextMenuTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\path-browser-quote-green\bin\ -p:BaseIntermediateOutputPath=artifacts\path-browser-quote-green\obj\` (passed: `2/2`).

## 167. Add script editor context-menu path browser insertion
- [x] 167.1 Add a focused WinForms regression proving the script editor context menu exposes a `Path Browser...` action that inserts the selected full file path at the current caret position.
- [x] 167.2 Add a test seam for file picking so UI tests can drive the path-browser behavior without launching a real `OpenFileDialog`.
- [x] 167.3 Wire a new `Path Browser...` item into the script editor right-click menu and connect it to the insertion handler.
- [x] 167.4 Run focused verification for the new Form1 context-menu tests and capture evidence.

### 167 Review
- Added focused WinForms regression `PathBrowserMenuClick_InsertsSelectedPathAtCaret` in `SSH_Helper.Tests/UI/Form1ScriptContextMenuTests.cs`.
- Added a file-picker test seam in `Form1` (`_filePathPickerOverrideForTests`) so tests can drive path insertion without opening a native dialog.
- Wired new script-editor context-menu item `ctxPathBrowser` (`Path Browser...`) in `Form1.Designer.cs` and connected it to `ctxPathBrowser_Click`.
- Implemented `SelectPathForScriptEditor()`, `InsertSelectedFilePathAtCaret()`, and `InsertTextIntoScriptEditor(...)` in `Form1.cs` to insert the selected full path at caret/selection.
- Verification:
- Red: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ScriptContextMenuTests.PathBrowserMenuClick_InsertsSelectedPathAtCaret" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\path-browser-red\bin\ -p:BaseIntermediateOutputPath=artifacts\path-browser-red\obj\` (failed as expected: missing `ctxPathBrowser`).
- Green: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ScriptContextMenuTests.PathBrowserMenuClick_InsertsSelectedPathAtCaret" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\path-browser-green\bin\ -p:BaseIntermediateOutputPath=artifacts\path-browser-green\obj\` (passed: `1/1`).

## 166. Fix interactive transcript assembly for cursor-rewrite chunks
- [x] 166.1 Reproduce and confirm corruption source from debug logs (`RawData` carries ANSI cursor rewrites while transcript assembly appends stripped fragments).
- [x] 166.2 Update interactive transcript assembly to stream raw non-alternate chunks through cursor-aware normalization before appending to transcript builder.
- [x] 166.3 Add focused tests for transcript assembly source selection and cross-chunk cursor rewrite reconstruction.
- [x] 166.4 Run focused interactive transcript tests and capture verification evidence.

### 166 Review
- Root cause confirmed from user debug trace: transcript corruption happened in `interactive-window` assembly before `CleanTranscriptForAudit(...)`. The terminal emitted rewrite chunks (`\x1B[15D...`, `\x1B[14D...`) but assembly used stripped fragments (`rtup-error-log`, `tus`) and appended them, producing `...standalone-clusterrtup-error-logtus`.
- Fix in `Services/Terminal/InteractiveTerminalService.cs`:
- added `ResolveTranscriptAssemblyInput(...)` to prefer raw chunk data when not in alternate-screen transitions so ANSI cursor edits are preserved.
- switched all interactive transcript builders (`capture-window`, `capture-headless`, `interactive-window`) to stream through `PrepareMirroredChunkForEmission(...)` with per-loop pending buffers, then append normalized output.
- flushes pending transcript buffer at loop end before final transcript snapshot, so incomplete final lines are still captured correctly.
- Added tests in `SSH_Helper.Tests/Services/InteractiveTerminalServiceTranscriptFilterTests.cs`:
- `ResolveTranscriptAssemblyInput_NonAlternateRawChunk_UsesRawData`
- `ResolveTranscriptAssemblyInput_AlternateScreenTransition_UsesCapturedText`
- `PrepareMirroredChunkForEmission_CursorRewriteAcrossChunks_MatchesWholeStreamNormalization`
- Verification:
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~InteractiveTerminalServiceTranscriptFilterTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\interactive-assembly-fix\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\interactive-assembly-fix\\obj\\` (passed: `54/54`).

## 165. Add interactive transcript debug tracing for autocomplete/backspace corruption
- [x] 165.1 Add focused regression/diagnostic tests for transcript-chunk debug formatting and control-char escaping.
- [x] 165.2 Instrument interactive terminal capture pipeline to emit debug traces for raw/stripped/captured chunks and final transcript state.
- [x] 165.3 Run focused interactive transcript tests and confirm no regressions.
- [x] 165.4 Add review notes with where to read traces and verification evidence.

### 165 Review
- Added diagnostic helper tests in `SSH_Helper.Tests/Services/InteractiveTerminalServiceTranscriptFilterTests.cs`:
- `ShouldEmitTranscriptChunkDebug_BackspaceChunk_ReturnsTrue`
- `ShouldEmitTranscriptChunkDebug_UnchangedPlainChunk_ReturnsFalse`
- `FormatInteractiveDebugText_EscapesControlCharactersAndTruncates`
- Added interactive debug instrumentation (gated by `context.DebugMode`) in `Services/Terminal/InteractiveTerminalService.cs`:
- chunk-level tracing across all interactive loops (`capture-window`, `capture-headless`, and non-capture `interactive-window`) with `RawData`, `StrippedData`, `CapturedText`, and alternate-screen state transitions.
- final transcript tracing before/after startup-prompt prepend and before/after session-audit cleaning.
- Diagnostic logs are emitted as script debug output (`ScriptOutputType.Debug`) and mirrored to `System.Diagnostics.Debug.WriteLine`.
- Added helper APIs for consistent trace formatting:
- `ShouldEmitTranscriptChunkDebug(...)`
- `FormatInteractiveDebugText(...)`
- control-char escaping/truncation + chunk message formatting helpers.
- Verification:
- Red: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ShouldEmitTranscriptChunkDebug_|FullyQualifiedName~FormatInteractiveDebugText_EscapesControlCharactersAndTruncates" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\interactive-debug-helpers-red\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\interactive-debug-helpers-red\\obj\\` (failed as expected before helper implementation: missing method compile errors).
- Green: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~InteractiveTerminalServiceTranscriptFilterTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\interactive-debug-helpers-green2\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\interactive-debug-helpers-green2\\obj\\` (passed: `51/51`).

## 164. Fix interactive transcript command reconstruction for tab/autocomplete edits
- [x] 164.1 Reproduce the corruption with a failing transcript-filter unit test that exercises backspace-heavy command rewrites.
- [x] 164.2 Fix transcript audit cleaning to apply terminal editing semantics (not raw control-char removal) so executed commands are preserved correctly.
- [x] 164.3 Run focused verification for interactive terminal transcript tests and record command outcomes.
- [x] 164.4 Add review notes with root cause, fix summary, and verification evidence.

### 164 Review
- Root cause: `InteractiveTerminalService.CleanTranscriptForAudit(...)` removed `\b` and DEL bytes as plain characters instead of applying cursor-edit semantics. For tab-cycled autocomplete, this preserved every intermediate candidate token and produced concatenated command fragments in audit logs.
- Added regression: `CleanTranscriptForAudit_TabAutocompleteBackspaces_PreservesExecutedCommand` in `SSH_Helper.Tests/Services/InteractiveTerminalServiceTranscriptFilterTests.cs` to model multiple autocomplete rewrites on one prompt line and assert the final executed command is reconstructed.
- Fix: `CleanTranscriptForAudit(...)` now maps DEL (`0x7F`) to backspace and runs `TerminalOutputProcessor.Sanitize(...)` + `TerminalOutputProcessor.Normalize(...)`, which applies cursor/backspace behavior before persisting transcript text.
- Verification:
- Red: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CleanTranscriptForAudit_TabAutocompleteBackspaces_PreservesExecutedCommand" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\interactive-transcript-red\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\interactive-transcript-red\\obj\\` (failed as expected with concatenated command fragments).
- Green: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~InteractiveTerminalServiceTranscriptFilterTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\interactive-transcript-green2\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\interactive-transcript-green2\\obj\\` (passed: `48/48`).

## 163. Implement PuTTY-style scroll-persistent terminal selection
- [x] 163.1 Add failing tests proving selection remains anchored to text while scrollback changes and can extend across scroll.
- [x] 163.2 Refactor terminal viewport selection state to use buffer-relative coordinates instead of viewport-relative coordinates.
- [x] 163.3 Add selection text provider plumbing so copy operations can resolve off-screen selected rows from terminal history.
- [x] 163.4 Run focused verification for new interactive terminal tests and capture outcomes.
- [x] 163.5 Add review notes with root cause, behavior changes, and verification evidence.

### 163 Review
- Added/redesigned WinForms regressions in `SSH_Helper.Tests/UI/InteractiveTerminalFormTests.cs`:
- `AdjustScrollbackOffset_WhenSelectionExists_PreservesSelectedText`
- `MouseDragSelection_AcrossScrollback_CanSpanBeyondSingleViewport`
- Root cause: viewport control stored selection in viewport row coordinates and copied text only from the current snapshot grid. Any scrollback movement either rebound selection to different text or dropped selection entirely.
- Refactor: `InteractiveTerminalViewportControl` now stores selection in buffer coordinates (`column + absolute buffer row`) and resolves paint bounds against current viewport-to-buffer mapping.
- Added `TerminalScreenSnapshot.EffectiveScrollOffset` so viewport controls can map visible rows to stable buffer rows even when follow-tail anchoring is active.
- Added selection-provider plumbing:
- `TerminalBufferSelection` record in `Forms/InteractiveTerminalForm.cs`
- `InteractiveTerminalViewportControl.SelectionTextProvider`
- `InteractiveTerminalForm.SelectionTextProvider` passthrough plus detached-mode provider
- `InteractiveTerminalService` now wires terminal-backed selection copy via `BuildSelectionClipboardText(...)` so off-screen selected text copies correctly.
- Behavior update: supersedes task `162` clear-on-scroll behavior; selection now persists and remains text-anchored while scrolling, matching PuTTY-style expectations.
- Verification:
- Red: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~InteractiveTerminalFormTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\interactive-terminal-putty-red\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\interactive-terminal-putty-red\\obj\\` (failed as expected: selection cleared / drag-cross-scroll copy empty).
- Green: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~InteractiveTerminalFormTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\interactive-terminal-putty-green\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\interactive-terminal-putty-green\\obj\\` (passed: `2/2`).
- Regression slice: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~InteractiveTerminalFormTests|FullyQualifiedName~InteractiveTerminalServiceTranscriptFilterTests" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\interactive-terminal-putty-regression\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\interactive-terminal-putty-regression\\obj\\` (passed: `49/49`).

## 162. Fix interactive terminal selection persistence while scrolling
- [x] 162.1 Reproduce with a failing UI regression test showing selection remains after scrollback offset changes.
- [x] 162.2 Update interactive terminal scroll handling to clear viewport selection whenever scrollback offset changes.
- [x] 162.3 Run focused verification for the new regression and nearby interactive terminal tests.
- [x] 162.4 Add review notes with root cause, fix summary, and command evidence.

### 162 Review
- Root cause: `InteractiveTerminalViewportControl` stores selection in viewport cell coordinates, and `InteractiveTerminalForm` changed `_scrollbackOffset` without clearing that selection; after scrolling, the highlight stayed on the same screen cells while underlying text changed.
- Added WinForms regression `AdjustScrollbackOffset_WhenSelectionExists_ClearsTerminalSelection` in `SSH_Helper.Tests/UI/InteractiveTerminalFormTests.cs` (reflection-based internal-form coverage) to prove selection is cleared on viewport scroll movement.
- Patched `Forms/InteractiveTerminalForm.cs` to call `_terminalView.ClearSelection()` before applying new `_scrollbackOffset` in both scrollback-change paths:
- `HistoryScrollBar_ValueChanged(...)`
- `AdjustScrollbackOffset(...)` (mouse-wheel path)
- Verification:
- Red: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~InteractiveTerminalFormTests.AdjustScrollbackOffset_WhenSelectionExists_ClearsTerminalSelection" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\interactive-terminal-selection-red\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\interactive-terminal-selection-red\\obj\\` (failed as expected before fix: selection remained true).
- Green: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~InteractiveTerminalFormTests.AdjustScrollbackOffset_WhenSelectionExists_ClearsTerminalSelection" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\interactive-terminal-selection-green\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\interactive-terminal-selection-green\\obj\\` (passed: `1/1`).

## 160. Draft OpenSpec proposal for Flow Canvas preset snapshot persistence
- [x] 160.1 Ground current behavior across preset persistence, Flow Canvas export, and comment/layout handling.
- [x] 160.2 Create detailed OpenSpec proposal for preserving Flow Canvas nodes/edges/comments across preset save + export/import portability.
- [x] 160.3 Capture decision defaults in the proposal (script-first hash policy, visual-dirty save behavior, auto-apply executable edits on save, backward compatibility).

### 160 Review
- Added `openspec/changes/add-flow-canvas-preset-snapshot-persistence/proposal.md` with detailed scope, behavior contracts, compatibility expectations, and implementation impact.
- Proposal explicitly locks key decisions:
- Script-first load policy with `commandHash` validation.
- Visual edits (layout/comments) mark presets dirty and persist on `Save Preset`.
- `Save Preset` auto-applies executable Flow Canvas edits to YAML when needed.
- Snapshot metadata is included in both single preset and bulk export/import paths.
- Backward compatibility for existing preset payloads is preserved.

## 159. Enable editable nested container blocks in Flow Canvas
- [x] 159.1 Remove read-only properties behavior for `_isChildOf` nodes and render standard editable fields with branch context badge.
- [x] 159.2 Update Flow store node-prop mutation to propagate `_forceGraphExport: true` up the `_isChildOf` ancestor chain (and on direct container edits).
- [x] 159.3 Update `FlowCanvasBridge` container export precedence to force graph regeneration when `_forceGraphExport` is present (top-level and nested paths).
- [x] 159.4 Add backend regressions proving forced container regeneration persists nested branch-child edits across container families.
- [x] 159.5 Add frontend e2e regression coverage for editable imported branch children and `Apply YAML` parity after nested edits.
- [x] 159.6 Run focused backend + e2e verification and capture command evidence.
- [x] 159.7 Add review notes with root cause, fix summary, and verification outcomes.

### 159 Review
- Root cause: branch-child nodes imported from YAML carry `_isChildOf` metadata, and `Properties.tsx` hard-switched those nodes to a read-only renderer. Child edits could not be made from the canvas.
- Secondary root cause: imported containers with `_yamlSnippet` could continue exporting from stale snippet text, so child-level graph edits were not guaranteed to persist.
- Frontend fix:
- `FlowCanvas/src/panels/Properties.tsx` now renders the same editable property controls for all selected executable nodes, including `_isChildOf` children, while keeping a branch context badge.
- `FlowCanvas/src/stores/slices/graphSlice.ts` now marks `_forceGraphExport: true` on edited container nodes and on all ancestor containers of an edited child node by walking `_isChildOf` links.
- Backend fix:
- `Services/FlowCanvasBridge.cs` adds `HasForceGraphExport(...)` and uses it in both top-level and nested container export paths (`ExportGraphToYaml` and `TryGenerateSingleNodeYaml`) to force container regeneration from graph structure when set.
- Snippet fallback is now gated to untouched containers (`!_forceGraphExport`), preserving prior behavior for unchanged imports.
- Added backend regression tests in `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` for forced graph export persistence on imported child edits across:
- `if`, `foreach`, `while`, `try`, `switch`, and `parallel`.
- Added frontend e2e fixture + coverage:
- `FlowCanvas/e2e/fixtures/graphs.ts`: `createImportedChildEditingFixture()`.
- `FlowCanvas/e2e/flow-canvas-properties-typing.spec.ts`: two tests validating child-node editability/reselection persistence and `Apply YAML` parity with `_forceGraphExport`.
- Verification:
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\\flowcanvas-force-export-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\flowcanvas-force-export-tests\\obj\\` (failed: `1/31`, existing unrelated test `ExportGraphToYaml_IncludesChildNodeStepPathMapping`).
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_IfWithForceGraphExport_UsesEditedVisualChildBranchValues|FullyQualifiedName~ExportGraphToYaml_ForeachWithForceGraphExport_UsesEditedVisualChildBranchValues|FullyQualifiedName~ExportGraphToYaml_WhileWithForceGraphExport_UsesEditedVisualChildBranchValues|FullyQualifiedName~ExportGraphToYaml_TryWithForceGraphExport_UsesEditedVisualChildBranchValues|FullyQualifiedName~ExportGraphToYaml_SwitchWithForceGraphExport_UsesEditedVisualChildBranchValues|FullyQualifiedName~ExportGraphToYaml_ParallelWithForceGraphExport_UsesEditedVisualChildBranchValues|FullyQualifiedName~ExportGraphToYaml_IfWithStoredSnippetAndBranchEdges_UsesGraphBranchShape|FullyQualifiedName~ExportGraphToYaml_TryWithStoredSnippetAndBranchEdges_UsesGraphBranchShape|FullyQualifiedName~ExportGraphToYaml_SwitchWithStoredSnippetAndBranchEdges_UsesGraphBranchShape|FullyQualifiedName~ExportGraphToYaml_ParallelWithStoredSnippetAndBranchEdges_UsesGraphBranchShape|FullyQualifiedName~ExportGraphToYaml_IfWithContinueEdge_ContinuationTargetNotConsumedAsBranch|FullyQualifiedName~ExportGraphToYaml_ForeachWithContinueEdge_ContinuationTargetNotConsumedAsDo" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts\\flowcanvas-force-export-focused\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\flowcanvas-force-export-focused\\obj\\` (passed: `12/12`).
- `cd FlowCanvas; npx playwright test e2e/flow-canvas-properties-typing.spec.ts --grep "imported branch child|apply yaml uses edited imported"` (passed: `2/2`).
- `cd FlowCanvas; npm run build` (passed).

## 158. Sync open Flow Canvas when preset selection changes in main form
- [x] 158.1 Add a focused failing WinForms regression proving that switching presets in `Form1` while `_flowCanvasForm` is open does not currently push a new `load-graph` payload.
- [x] 158.2 Patch the preset-load path so selecting a different preset refreshes the open Flow Canvas graph with the newly selected preset script.
- [x] 158.3 Run focused verification for the new/related Form1 Flow Canvas tests and capture evidence.
- [x] 158.4 Add review notes with root cause, behavior change, and verification outcomes.

### 158 Review
- Root cause: `LoadCurrentScriptIntoCanvas()` was only called when the Flow Canvas window first opened. Preset changes in `Form1` updated editor text via `LoadPresetIntoEditor(...)` but never pushed a fresh `load-graph` message to the already-open canvas.
- Fix: `Form1.LoadPresetIntoEditor(...)` now calls `LoadCurrentScriptIntoCanvas()` immediately after loading command text/preset metadata into the editor, so any open canvas is synchronized to the newly selected preset.
- Added regression: `SSH_Helper.Tests/UI/Form1FlowCanvasPresetSyncTests.cs` (`SelectingDifferentPreset_WithOpenFlowCanvas_QueuesUpdatedGraphForNewPreset`) proves preset A -> preset B selection while `_flowCanvasForm` is open queues an updated `load-graph` payload containing the new preset marker.
- Verification:
- Red: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasPresetSyncTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\flowcanvas-preset-sync-red\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\flowcanvas-preset-sync-red\\obj\\` (failed: `1/1`, empty pending message queue).
- Green: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasPresetSyncTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\flowcanvas-preset-sync-green\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\flowcanvas-preset-sync-green\\obj\\` (passed: `1/1`).
- Regression: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTabSelectionTests|FullyQualifiedName~Form1FlowCanvasBreakpointPersistenceTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\flowcanvas-preset-sync-regression\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\flowcanvas-preset-sync-regression\\obj\\` (passed: `2/2`).

## 157. Remove save-diff truncation in preset save/discard prompt
- [x] 157.1 Locate the preset save/discard diff rendering path and confirm the truncation source.
- [x] 157.2 Patch the diff line-budget logic so command diffs in the save prompt are never truncated.
- [x] 157.3 Add/update automated coverage to guard against future truncation regressions.
- [x] 157.4 Run focused verification for the touched tests and capture results.
- [x] 157.5 Add review notes with root cause, fix summary, and verification evidence.

### 157 Review
- Root cause: `UnsavedPresetDiffDialog` passed `maxOutputLines` from `EstimateCommandDiffLineBudget(...)`, which clamped to `10,000`, so large command diffs were forcibly replaced with `... diff truncated`.
- Updated `UnsavedPresetDiffDialog` to compute a full diff line budget from both command texts (`saved line count + current line count + headroom`), capped only at a safe `int.MaxValue - 1` guard to avoid overflow in `InlineDiffBuilder`.
- Added WinForms regression `SavePrompt_LongCommandDiff_DoesNotTruncateOutput` in `SSH_Helper.Tests/UI/UnsavedPresetDiffDialogTests.cs` to cover a 12,050-line diff and assert the updated final line is present with no truncation marker.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~UnsavedPresetDiffDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\unsaved-diff-no-truncate\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\unsaved-diff-no-truncate\\obj\\` (passed: `4/4`).

## 156. Fix single-file Flow Canvas asset resolution
- [x] 156.1 Confirm root cause and capture failing single-file asset lookup behavior.
- [x] 156.2 Package `FlowCanvas/dist` into the app so single-file publish carries web assets.
- [x] 156.3 Resolve Flow Canvas runtime asset path from extracted app-owned storage under `%LocalAppData%`.
- [x] 156.4 Run verification (`dotnet publish`/build + targeted tests) and confirm no regressions.
- [x] 156.5 Add review notes with root cause, fix summary, and command evidence.

### 156 Review
- Root cause: `FlowCanvasForm` only searched `AppDomain.CurrentDomain.BaseDirectory\\FlowCanvas\\dist` and repo-root fallback; single-file publish output has no sidecar `FlowCanvas/dist`, so the canvas could never resolve assets outside a source checkout.
- Added `Utilities/FlowCanvasDistLocator.cs` to centralize dist resolution:
- `exe-relative` dist (existing behavior),
- `project-root` dist (dev fallback),
- embedded-resource extraction fallback to `%LocalAppData%\\SSH_Helper\\flow-canvas-dist\\<buildTimestamp>`.
- Updated `UI/FlowCanvasForm.cs` to use `FlowCanvasDistLocator.ResolveDistPath()` and show richer diagnostics that include all searched locations.
- Updated `SSH_Helper.csproj` with target `IncludeFlowCanvasDistEmbeddedResources` (before `AssignTargetPaths`) so `FlowCanvas/dist/**` is embedded with stable logical names (`SSH_Helper.Resources.FlowCanvasDist/...`) for single-file runtime extraction.
- Added/updated tests in `SSH_Helper.Tests/Utilities/FlowCanvasDistLocatorTests.cs`:
- resource embedding presence,
- embedded extraction writes `index.html`,
- fallback resolution behavior and precedence.
- Verification:
- Red (before implementation): `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasDistLocatorTests" -p:UseAppHost=false` (failed with missing `FlowCanvasDistLocator`).
- Green (targeted): `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasDistLocatorTests" -p:UseAppHost=false` (passed: 5/5).
- Publish parity (user command): `dotnet publish SSH_Helper.csproj -c Release --self-contained -r win-x64 -p:PublishSingleFile=true` (passed).

## 155. Audit stale-container-snippet branch export impact across container families
- [x] 155.1 Inspect `FlowCanvasBridge` container export precedence to confirm whether snippet-vs-graph logic is shared beyond `if`.
- [x] 155.2 Add focused stale-snippet regression coverage for `try`, `switch`, and `parallel` with explicit graph branch metadata.
- [x] 155.3 Run focused verification for stale-snippet branch-shape tests (`if`, `try`, `switch`, `parallel`).
- [x] 155.4 Add review notes with findings and verification evidence.

### 155 Review
- `FlowCanvasBridge` uses shared container precedence (`IsContainerBlockType` + `HasGraphAuthoredContainerBranches`) for `if`, `foreach`, `while`, `switch`, `parallel`, and `try` in both top-level and nested export paths.
- Added stale-snippet regression tests in `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`:
- `ExportGraphToYaml_TryWithStoredSnippetAndBranchEdges_UsesGraphBranchShape`
- `ExportGraphToYaml_SwitchWithStoredSnippetAndBranchEdges_UsesGraphBranchShape`
- `ExportGraphToYaml_ParallelWithStoredSnippetAndBranchEdges_UsesGraphBranchShape`
- Verification:
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_IfWithStoredSnippetAndBranchEdges_UsesGraphBranchShape|FullyQualifiedName~ExportGraphToYaml_TryWithStoredSnippetAndBranchEdges_UsesGraphBranchShape|FullyQualifiedName~ExportGraphToYaml_SwitchWithStoredSnippetAndBranchEdges_UsesGraphBranchShape|FullyQualifiedName~ExportGraphToYaml_ParallelWithStoredSnippetAndBranchEdges_UsesGraphBranchShape" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\\if-branch-impact-check2\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\if-branch-impact-check2\\obj\\` (passed: `4/4`).

## 154. Add focused FlowCanvas e2e coverage for `if` Apply-to-YAML branch nesting
- [x] 154.1 Add a Playwright test that builds the `send -> extract -> print -> confirm -> if(then ping / else ping)` graph (with stale `if._yamlSnippet`) and clicks `Apply YAML`.
- [x] 154.2 Assert exported graph semantics include nested `if.then` and `if.else` ping branches (not flattened top-level pings).
- [x] 154.3 Run focused Playwright verification for the new test and capture command evidence.
- [x] 154.4 Add review notes below with scope, assertions, and verification result.

### 154 Review
- Added Playwright regression `apply yaml preserves nested if branch shape when if node has stale snippet` in `FlowCanvas/e2e/flow-canvas-preset-parity.spec.ts`.
- The test builds an action-based graph fixture with `send -> extract -> print -> confirm -> if`, attaches explicit `then`/`else` branch edges to two ping nodes, and seeds stale `if._yamlSnippet` to match the reported Apply/Run scenario.
- Assertion path uses `evaluateParityCases(...)` against canonical source YAML that includes nested `if.then`/`if.else` blocks; test fails if export flattens branch pings to top-level.
- Hardened parity CLI build helper in `FlowCanvas/e2e/support/parityCli.ts` to emit to an isolated output path and avoid local app-output locks during Playwright parity runs.
- Verification:
- `cd FlowCanvas; npx playwright test e2e/flow-canvas-preset-parity.spec.ts --grep "apply yaml preserves nested if branch shape when if node has stale snippet"` (passed: `1/1`).

## 153. Fix Flow Canvas `if` export flattening on Apply-to-YAML/Run
- [x] 153.1 Reproduce with a focused failing `FlowCanvasBridgeTests` case that includes an `if` node with stored `_yamlSnippet` plus branch edges (`then`/`else`) and assert nested branch export.
- [x] 153.2 Patch `FlowCanvasBridge.ExportGraphToYaml` so container blocks prefer graph-derived branch export when branch topology exists, even if `_yamlSnippet` is present.
- [x] 153.3 Run focused verification for the new regression and existing `if` container export tests.
- [x] 153.4 Add review notes with root cause, behavior delta, and command evidence.

### 153 Review
- Root cause: `FlowCanvasBridge.ExportGraphToYaml` always prioritized container `_yamlSnippet` when present, so edited/authored `if` branch edges were ignored and branch nodes emitted as top-level sequential steps.
- Fix: added branch-topology detection (`HasGraphAuthoredContainerBranches`) and changed container export precedence to regenerate from graph when explicit branch metadata (`data.branchPath`) points to non-child authored nodes; preserved snippet round-trip behavior for imported visual-child container graphs.
- Added regression test: `ExportGraphToYaml_IfWithStoredSnippetAndBranchEdges_UsesGraphBranchShape`.
- Verification:
- Red: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_IfWithStoredSnippetAndBranchEdges_UsesGraphBranchShape" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\\if-branch-shape-red\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\if-branch-shape-red\\obj\\` (failed as expected before patch).
- Green (focused): `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_IfWithStoredSnippetAndBranchEdges_UsesGraphBranchShape|FullyQualifiedName~ExportGraphToYaml_IfWithElifAndElse_BranchMetadataProducesCanonicalYaml" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\\if-branch-shape-green\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\if-branch-shape-green\\obj\\` (passed: 2/2).
- Green (regression): `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\\if-branch-shape-regression2\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\if-branch-shape-regression2\\obj\\` (passed: 17/17).

## 152. Implement add-flow-canvas-preset-parity-process
- [x] 152.1 Read `openspec/changes/add-flow-canvas-preset-parity-process/proposal.md`, `design.md`, and `tasks.md` to confirm scope and acceptance criteria.
- [x] 152.2 Create and verify implementation plan/checklist in this tracker before coding.
- [x] 152.3 Implement OpenSpec implementation tasks 1.1-1.10 sequentially with minimal scoped edits.
- [x] 152.4 Run verification gates 2.1-2.5 with command evidence.
- [x] 152.5 Complete rollout tasks 3.1-3.2 (manual-run only + CI-gating follow-up capture).
- [x] 152.6 Update `openspec/changes/add-flow-canvas-preset-parity-process/tasks.md` so all items are `- [x]` only after confirmed completion.
- [x] 152.7 Add review notes below with changes, root-cause/tradeoff context, and verification results.

### 152 Review
- Implemented the full parity process across `FlowCanvasBridge`, Flow Canvas state/UI, and Playwright harness so QA presets are reconstructed through graph actions and verified on export.
- Root cause addressed: prior coverage relied on preset import/load paths and missed graph-native container-branch modeling (`if/elif/else`, `try/catch/finally`, `switch`, `parallel`) plus Start advanced preamble sections (`vars/imports/subroutines`), creating parity blind spots.
- Tradeoff: added a small helper CLI (`FlowCanvas/tools/FlowCanvasParityCli`) for parser-backed semantic comparison and validation to avoid duplicating canonical YAML semantics in frontend-only test code.
- Added action-based test hooks (`setGraphViaActions`, `clearGraphViaActions`, `getGraphSnapshot`) so parity suites do not depend on `load-graph`.
- Added parity suites for valid QA presets + synthetic `browser_callback`, intentional-invalid presets, and gesture/property smoke coverage; added manual-run script/docs and `npm run test:e2e:parity`.
- Updated OpenSpec checklist file `openspec/changes/add-flow-canvas-preset-parity-process/tasks.md` to all `- [x]`, including rollout item to keep this phase manual-run only and capture CI-gating as follow-up.
- Verification:
- `cd FlowCanvas; npm run test:e2e:parity` (passed: `6/6`).
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_IfWithElifAndElse_BranchMetadataProducesCanonicalYaml|FullyQualifiedName~ExportGraphToYaml_TryWithoutSnippet_ExportsDoCatchFinally|FullyQualifiedName~ExportGraphToYaml_SwitchWithoutSnippet_ExportsCasesAndDefault|FullyQualifiedName~ExportGraphToYaml_ParallelWithoutSnippet_ExportsBranchSteps|FullyQualifiedName~ExportGraphToYaml_StartAdvancedSectionsFromEditors_AreSerializedInPreamble" -v minimal -p:UseAppHost=false` (passed: `5/5`).
- `openspec validate add-flow-canvas-preset-parity-process --strict --no-interactive` (passed).

## 151. Fix current `FlowCanvasBridgeTests` failures
- [x] 151.1 Update `FlowCanvasBridgeTests` graph fixtures to include `__start__` node/edge for start-rooted export traversal.
- [x] 151.2 Patch `FlowCanvasBridge` preamble serialization so known sections (including `subroutines:`) are preserved correctly without orphaned indented lines.
- [x] 151.3 Run focused verification for `FlowCanvasBridgeTests` and capture results.
- [x] 151.4 Add review notes with root-cause and fix summary.

### 151 Review
- Updated three fixture-only tests in `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` to include the required `__start__` node and start-linked edges:
- `ExportGraphToYaml_UnsupportedBlockType_ReturnsErrorDiagnostic`
- `ExportGraphToYaml_IncludesChildNodeStepPathMapping`
- `ExportGraphToYaml_CommentNodes_AreIgnoredWithWarning`
- Patched preamble serialization in `Services/FlowCanvasBridge.cs` to preserve `subroutines:` as a first-class section and prevent indented child lines under known sections from being misclassified as unrecognized top-level content.
- Patched top-level `steps:` header detection to check for an actual top-level key (`HasTopLevelStepsHeader`) instead of substring search, avoiding false positives from nested `subroutines.*.steps`.
- Verification:
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\\flowcanvasbridge-fix2\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\flowcanvasbridge-fix2\\obj\\` (passed: `11/11`).

## 150. Review failing `FlowCanvasBridgeTests` and classify root cause
- [x] 150.1 Reproduce current `FlowCanvasBridgeTests` failures with a focused `dotnet test` run and capture failing test names plus error messages.
- [x] 150.2 Trace each failure to the responsible code path (`FlowCanvasBridge` logic vs test fixture/assertion assumptions) and compare against nearby working tests.
- [x] 150.3 Classify each failure as either runtime regression or test issue, with concrete evidence references.
- [x] 150.4 Add a short review section below with findings and recommended next action.

### 150 Review
- Reproduced on current workspace and on a clean `HEAD` worktree: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:UseAppHost=false` fails `4/11` in both places, so none of these are newly introduced by current uncommitted edits.
- `ExportGraphToYaml_UnsupportedBlockType_ReturnsErrorDiagnostic`, `ExportGraphToYaml_IncludesChildNodeStepPathMapping`, and `ExportGraphToYaml_CommentNodes_AreIgnoredWithWarning` are test-fixture contract mismatches: those tests build graphs without the mandatory `__start__` node/edge, while current Flow Canvas/bridge contract traverses from `__start__` and excludes disconnected nodes with warnings (`Services/FlowCanvasBridge.cs` export traversal; `FlowCanvas/src/stores/slices/graphSlice.ts` protects `__start__`; `FlowCanvas/src/stores/messageBridge.ts` auto-injects `__start__` on load).
- `RoundTrip_AllQaPresetYamlScripts_MaintainValidationContract` failure for `QA Local Subroutines` is a real bridge bug, not a test issue: preamble serialization currently appends nested `subroutines` children without the `subroutines:` header (via `ExtractUnrecognizedSections`), which yields invalid YAML parse at line 4.
- Recommended next action: update the three graph-construction tests to include `__start__` linkage, and patch preamble serialization so `subroutines` is preserved as a full section (or excluded from `ExtractUnrecognizedSections` child-line capture).

## 149. Fix verified FlowCanvas bugfix batch (debug side effects, dirty tracking, variable timer, BaseBlock style dedupe)
- [x] 149.1 Refactor `toggleDisabled` in `FlowCanvas/src/stores/slices/debugSlice.ts` so updater remains side-effect free and side effects run after `set()`.
- [x] 149.2 Fix variable highlight timer overlap in `FlowCanvas/src/stores/slices/variableSlice.ts` by tracking and resetting a single timeout.
- [x] 149.3 Implement dirty-tracking hardening for direct `setNodes`/`setEdges` callers:
- [x] 149.3a Add optional `markDirty` options to `GraphSlice.setNodes`/`setEdges` in `FlowCanvas/src/stores/slices/graphSlice.ts`.
- [x] 149.3b Update Ctrl+V paste path in `FlowCanvas/src/hooks/useKeyboardShortcuts.ts` to pass `markDirty: true`.
- [x] 149.3c Update auto-layout path in `FlowCanvas/src/hooks/useAutoLayout.ts` to pass `markDirty: true`.
- [x] 149.4 Remove duplicate per-node inline `<style>` from `FlowCanvas/src/nodes/BaseBlock.tsx`, move styles into a shared CSS file loaded once.
- [x] 149.5 Run verification (`npm run build`, targeted Playwright coverage) and record outcomes.

### 149 Review
- Refactored `toggleDisabled` in `debugSlice` so the Zustand updater is pure and side effects (`updateNodeData`, `disableBlock` message) run after `set()`.
- Added a module-scoped timer guard in `variableSlice` to cancel/reset prior highlight-clear timers and prevent early `changed=false` clearing during rapid variable updates.
- Extended `GraphSlice.setNodes`/`setEdges` with optional `markDirty` options and routed paste (`useKeyboardShortcuts`) + auto-layout (`useAutoLayout`) through `markDirty: true` so run/test payloads correctly report `graphChanged`.
- Extracted BaseBlock keyframes/search highlight CSS into `FlowCanvas/src/nodes/baseblock.css` and removed per-node inline `<style>` injection from `BaseBlock.tsx`.
- Added two Playwright regressions in `FlowCanvas/e2e/flow-canvas-parity.spec.ts`:
- `run payload sets graphChanged after Ctrl+V paste`
- `run payload sets graphChanged after auto-layout`
- Hardened existing parity tests in the same file to match current harness behavior:
- normalize edge payloads by dropping runtime-only `selected`/`style` fields before parity comparison
- replace brittle browser `dialog` wait with `show-error` outgoing-message assertion
- Verification:
- `cd FlowCanvas; npm run build` (pass)
- `cd FlowCanvas; npm run test:e2e -- e2e/flow-canvas-parity.spec.ts e2e/flow-canvas-interactions.spec.ts` (9 passed)

## 148. Fix Flow Canvas print-space export validation and stale Run disable
- [x] 148.1 Add failing `FlowCanvasBridgeTests` coverage proving `print.message` accepts whitespace-only payloads (for blank-line separators).
- [x] 148.2 Add failing FlowCanvas Playwright coverage proving Run/Test re-enable after editing any graph property following an export validation error.
- [x] 148.3 Patch `FlowCanvasBridge` required-option validation so `print.message` allows whitespace-only strings while still rejecting empty/`null` values.
- [x] 148.4 Patch FlowCanvas graph mutation state handling to clear stale export errors after user edits so Run/Test can be retried.
- [x] 148.5 Run focused verification (`dotnet` + Playwright + FlowCanvas build) and capture results in the review section below.

### 148 Review
- Added `FlowCanvasBridgeTests.ExportGraphToYaml_PrintWithSingleSpaceMessage_IsAccepted` to reproduce the whitespace-only `print.message` export failure.
- Added Playwright regression `run re-enables after editing graph following export error` in `FlowCanvas/e2e/flow-canvas-parity.spec.ts` to reproduce sticky Run disable after `apply-result` validation error.
- Patched `Services/FlowCanvasBridge.cs` so required-option validation and string normalization preserve non-empty whitespace for `print.message` (still rejects empty string/`null`).
- Patched `FlowCanvas/src/stores/slices/graphSlice.ts` and `FlowCanvas/src/stores/slices/undoSlice.ts` to clear stale export-error state on graph mutations and undo/redo, re-enabling Run/Test once user edits.
- Red verification:
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests.ExportGraphToYaml_PrintWithSingleSpaceMessage_IsAccepted" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\\flowcanvas-print-space-red\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\flowcanvas-print-space-red\\obj\\` (failed with `Block 'print' is missing required option(s): message.`).
- `cd FlowCanvas; npm run test:e2e -- e2e/flow-canvas-parity.spec.ts --grep "run re-enables after editing graph following export error"` (failed with Run button still disabled).
- Green verification:
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests.ExportGraphToYaml_PrintWithSingleSpaceMessage_IsAccepted" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\\flowcanvas-print-space-green2\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\flowcanvas-print-space-green2\\obj\\` (passed).
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\\flowcanvas-print-space-regression\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\flowcanvas-print-space-regression\\obj\\` (11 passed).
- `cd FlowCanvas; npm run test:e2e -- e2e/flow-canvas-parity.spec.ts` (3 passed).
- `cd FlowCanvas; npm run build` (passed).

## 147. Implement Flow Canvas preset rewrite correctness plan
- [x] 147.1 Add Flow Canvas bridge regression coverage for full `qa_presets.json` rewrite parity and targeted option-preservation cases.
- [x] 147.2 Refactor `FlowCanvasBridge` non-container export to schema-driven map emission with hard diagnostics for unsupported props.
- [x] 147.3 Expand `FlowCanvasBridge` property extraction/export support for high-risk command options (`send`, `log`, `sftp`, `table`, `portcheck`, `http`, `readfile`, `interactive`, `browser_callback_capture`).
- [x] 147.4 Align Flow Canvas block property schema in `FlowCanvas/src/blockDefs/registry.ts` (add missing send capture/options, remove invalid drifted fields, fix enum options).
- [x] 147.5 Add drift-guard tests comparing parser-known command options vs bridge extraction/export support and Flow Canvas block definitions.
- [x] 147.6 Run focused verification (`FlowCanvasBridgeTests`, QA catalog/execution tests, and FlowCanvas Playwright parity/properties tests where applicable) and record review notes.

### 147 Review
- Added new Flow Canvas rewrite regression tests covering targeted option preservation (`send`, `log`, `sftp`, `portcheck`, `table`) plus full YAML QA catalog rewrite/validation contract checks.
- Replaced `FlowCanvasBridge` non-container export from hardcoded string templates with schema-driven map emission: parse existing snippet, preserve untouched options, apply canonical property aliases, normalize value types, enforce required keys, and serialize YAML with command-key fidelity.
- Added hard export diagnostics for unsupported/invalid drifted properties (`send.delay`, `interactive.timeout`, `return.value`) and legacy-to-canonical normalization (`sftp local/remote -> local_path/remote_path`, `table source -> data`, `portcheck target -> host/port`, `writefile append -> mode`).
- Expanded property extraction for high-risk commands/options including `send.capture/retry/retry_delay/fail_on_nonzero/respond`, `log.message/level`, full `http` options, `readfile` options, `interactive` options, `browser_callback_capture` options, canonical `sftp`, canonical `table`, and canonical `portcheck`.
- Replaced `FlowCanvas/src/blockDefs/registry.ts` with a runtime-aligned canonical property schema: added missing send capture/options, removed drifted invalid fields, aligned key names and enums (including `log.level=success`), and updated preview keys to canonical fields.
- Added drift guards:
- parser-vs-bridge export option catalog parity test,
- registry property key mapping test against parser-known runtime options plus bridge alias map,
- explicit assertions for high-risk schema points (`send.capture` present, `send.delay` absent, `interactive.timeout` absent, `return.value` absent, canonical `sftp`/`table`/`portcheck` fields present, `log.level` includes `success`).
- Verification passed:
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests" -v minimal -p:UseAppHost=false`
- `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~QaPresetsSyntaxTests|FullyQualifiedName~QaPresetCatalogTests|FullyQualifiedName~QaPresetExecutionTests" -v minimal -p:UseAppHost=false`
- `cd FlowCanvas; npm run test:e2e -- e2e/flow-canvas-properties-typing.spec.ts e2e/flow-canvas-parity.spec.ts`
- `cd FlowCanvas; npm run build`

## 146. Fix stale block command preview after properties edit
- [x] 146.1 Trace block preview render data flow and confirm why display-name updates but command preview stays stale.
- [x] 146.2 Patch block preview rendering to prefer live `props[previewKey]` over importer metadata (`_preview`) for editable block types.
- [x] 146.3 Add a focused Playwright regression fixture/spec that includes stale imported `_preview` and verifies command preview updates after edit.
- [x] 146.4 Run focused verification and capture results below.

### 146 Review
- Root cause: `FlowCanvas/src/nodes/BaseBlock.tsx` rendered preview text from `props._preview` first, while `props._preview` is import-time metadata set by `FlowCanvasBridge`. Editing `props.command` in the Properties panel did not update `_preview`, so node preview text remained stale.
- Patched `BaseBlock` so when a block definition has `previewKey`, preview comes from the live property value (`props[previewKey]`) and no longer falls back to stale `_preview` for that block type.
- Kept `_preview` fallback only for block types without a `previewKey`.
- Updated `FlowCanvas/e2e/fixtures/graphs.ts` to seed `node-send` with stale imported `_preview` + matching old `command`.
- Extended `FlowCanvas/e2e/flow-canvas-properties-typing.spec.ts` to assert the node shows the new command and not the stale imported preview after editing.
- Verification passed:
- `cd FlowCanvas; npm run test:e2e -- e2e/flow-canvas-properties-typing.spec.ts`
- `cd FlowCanvas; npm run test:e2e -- e2e/flow-canvas-interactions.spec.ts e2e/flow-canvas-parity.spec.ts`
- `cd FlowCanvas; npm run build`

## 145. Fix Flow Canvas properties canonical state path + dropdown first-select persistence
- [x] 145.1 Add/route properties edits through Zustand graph-slice actions (`updateNodeLabel`, `updateNodeProp`) and remove `Properties.tsx` dependence on ReactFlow `getNode/setNodes`.
- [x] 145.2 Fix buffered input commit race (`onBlur` stale closure) and add node+field identity resets to prevent cross-field/node state bleed.
- [x] 145.3 Harden select/dropdown behavior so first interaction persists explicit value, including default-backed selects.
- [x] 145.4 Extend Playwright properties coverage for dropdown persistence and mixed text/code/textarea/select focus-switch regressions.
- [x] 145.5 Stabilize parity harness preconditions (`set-target-host`) and add shipped-bundle (`dist`) e2e script/config.
- [x] 145.6 Run verification commands and record results in the review section.

### 145 Review
- Moved properties-panel editing to a single canonical state path in Zustand: `Properties.tsx` now reads selected nodes from `useFlowStore.nodes` and writes through new `graphSlice` actions `updateNodeLabel(...)` and `updateNodeProp(...)`.
- Removed `Properties.tsx` usage of ReactFlow `getNode/setNodes`, eliminating split-brain state between ReactFlow internals and the app store/undo snapshots.
- Replaced the old local input hook with `useBufferedInput(...)` that tracks the latest typed value in a ref and commits only when changed, preventing stale `onBlur` closure commits.
- Added node+field identity-scoped buffering (`${nodeId}:${fieldKey}:${fieldType}` plus display-name identity) to prevent cross-node/cross-field local state bleed during rapid reselection.
- Hardened dropdown/select behavior: first user interaction now persists explicit selection even when the displayed value comes from `defaultValue` fallback and the user re-confirms that same option.
- Extended Playwright coverage in `flow-canvas-properties-typing.spec.ts` with three select-focused regressions:
- mixed text/code/textarea/select persistence across reselection,
- default-backed select first-interaction persistence,
- immediate select-change payload persistence via `apply-yaml`.
- Stabilized parity harness precondition by setting `set-target-host` in `flow-canvas-parity.spec.ts` before asserting toolbar Run behavior.
- Added shipped-bundle E2E path:
- `FlowCanvas/playwright.preview.config.ts`,
- `FlowCanvas/package.json` script `test:e2e:dist`.
- Verification passed:
- `cd FlowCanvas; npm run build`
- `cd FlowCanvas; npm run test:e2e -- e2e/flow-canvas-properties-typing.spec.ts e2e/flow-canvas-interactions.spec.ts e2e/flow-canvas-parity.spec.ts`
- `cd FlowCanvas; npm run test:e2e:dist -- e2e/flow-canvas-properties-typing.spec.ts e2e/flow-canvas-parity.spec.ts`

## 144. Fix Flow Canvas properties-panel keypress swallowing and add Playwright regression coverage
- [x] 144.1 Add focused Playwright regression fixture/spec covering per-keystroke typing for `Display Name`, one `text`, one `code`, and one `textarea` property.
- [x] 144.2 Run focused properties regression to capture red failure prior to fix.
- [x] 144.3 Implement minimal `Properties.tsx` fix (local buffered `Display Name`, safer blur sync) and add stable `data-testid` hooks.
- [x] 144.4 Re-run focused Playwright verification (new properties spec + nearby interaction sanity) and document results below.

### 144 Review
- Root cause in `FlowCanvas/src/panels/Properties.tsx` was twofold: `Display Name` was still a directly controlled input (unlike other text-like property fields), and `useLocalInput.onBlur` reset local value from external state, which could overwrite in-flight typed characters during rapid render churn.
- Added deterministic fixture `createPropertiesTypingFixture()` in `FlowCanvas/e2e/fixtures/graphs.ts` with `send` (`code` + `text`) and `http` (`textarea`) blocks to cover representative text-like input types.
- Added focused regression `FlowCanvas/e2e/flow-canvas-properties-typing.spec.ts` with per-keystroke assertions for:
- `Display Name` (`properties-display-name-input`)
- `command` code input (`properties-field-command-code-input`)
- `expect` text input (`properties-field-expect-text-input`)
- `body` textarea (`properties-field-body-textarea-input`)
- Updated `FlowCanvas/src/panels/Properties.tsx` to:
- apply local buffered input behavior to `Display Name`,
- commit current local value on blur instead of resetting from external state,
- expose stable `data-testid` hooks for panel root and per-property controls.
- Red verification (before fix): `cd FlowCanvas; npm run test:e2e -- e2e/flow-canvas-properties-typing.spec.ts` failed as expected (`getByTestId('properties-panel')` not found).
- Green verification (after fix): same focused command passed (`1` passed).
- Focused sanity suite passed:
- `cd FlowCanvas; npm run test:e2e -- e2e/flow-canvas-properties-typing.spec.ts e2e/flow-canvas-interactions.spec.ts e2e/flow-canvas-variable-inspector.spec.ts` (`6` passed).

## 143. Obscure `password` in Flow Canvas Variables panel
- [x] 143.1 Confirm Variables panel render path masks `password`-named variables.
- [x] 143.2 Rebuild Flow Canvas dist bundle so runtime WebView uses the masking code.
- [x] 143.3 Run focused masking verification and capture review notes below.

### 143 Review
- Root cause was deployment drift: `FlowCanvas/src/panels/VariableInspector.tsx` already masks `password`/secret-style names, but the checked-in `FlowCanvas/dist` bundle was stale and still rendered raw string values.
- Rebuilt Flow Canvas (`npm run build`) so `dist/assets/index-gKmJIjpA.js` includes the `formatVariableDisplay` masking path and `"********"` output for sensitive variable names.
- Focused verification passed: `npm run test:e2e -- e2e/flow-canvas-variable-inspector.spec.ts` (`1` passed).

## 142. Fix Flow Canvas breakpoint rerun persistence
- [x] 142.1 Add a focused failing regression that reproduces: breakpoint hit on run 1, ignored on run 2 with no toggle changes.
- [x] 142.2 Fix Flow Canvas debug-state lifecycle so user breakpoint/disabled toggles persist across runs while run-local maps still reset safely.
- [x] 142.3 Run focused verification and capture review notes below.

### 142 Review
- Root cause was lifecycle mismatch in `Form1`: `_pendingBreakpoints`/`_pendingDisabledBlocks` were treated as run-prep state and cleared during `CleanupFlowCanvasExecutionStateAfterRun()`, so reruns lost breakpoint/disabled toggles even though Flow Canvas still showed them enabled.
- Added focused regression `Form1FlowCanvasBreakpointPersistenceTests.CleanupFlowCanvasExecutionStateAfterRun_PreservesPendingDebugTogglesForRerun` to prove pending debug toggles survive cleanup and remain available for the next run-start bootstrap.
- Updated Flow Canvas toggle handlers in `Form1` so pending sets are always updated (even during active debug sessions), then active `DebugState` is synchronized to the desired pending state without double-toggling.
- Moved pending-set clearing back to run-start preparation (`PrepareFlowCanvasExecutionStateForRunStart`) and removed it from run cleanup, preserving rerun persistence while still allowing per-run node-map filtering.
- Focused red verification (before fix): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasBreakpointPersistenceTests.CleanupFlowCanvasExecutionStateAfterRun_PreservesPendingDebugTogglesForRerun" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\flowcanvas-breakpoint-rerun-red\bin\ -p:BaseIntermediateOutputPath=artifacts\flowcanvas-breakpoint-rerun-red\obj\` failed as expected (`pendingBreakpoints {empty} to contain "node-breakpoint"`).
- Focused green verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasBreakpointPersistenceTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\flowcanvas-breakpoint-rerun-green-newtest\bin\ -p:BaseIntermediateOutputPath=artifacts\flowcanvas-breakpoint-rerun-green-newtest\obj\` passed (`1` passed).
- Adjacent verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasTestStepScopingTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\flowcanvas-breakpoint-rerun-green-scoping\bin\ -p:BaseIntermediateOutputPath=artifacts\flowcanvas-breakpoint-rerun-green-scoping\obj\` passed (`4` passed).
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServiceFlowCanvasDebugBootstrapTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\flowcanvas-breakpoint-rerun-green-bootstrap\bin\ -p:BaseIntermediateOutputPath=artifacts\flowcanvas-breakpoint-rerun-green-bootstrap\obj\` passed (`1` passed).

## 141. Fix debug step-into hang inside parallel branches
- [x] 141.1 Reproduce and capture a focused failing regression for stepping into a `parallel` block under debug mode.
- [x] 141.2 Implement a minimal runtime fix so stepping through parallel branches does not deadlock/stall in `running`.
- [x] 141.3 Run focused + adjacent debug regressions and document review findings below.

### 141 Review
- Root cause was `DebugState` resume signaling not being multi-waiter-safe: each paused branch in `parallel` called `WaitForResumeAsync(...)` and overwrote `_resumeSignal`, so only the most recent waiter resumed; earlier paused branches stayed blocked forever and the parent `parallel` step remained `running`.
- Added focused regression `ScriptExecutorDebugStepTests.ExecuteAsync_StepIntoParallel_ContinueReleasesAllPausedBranches`.
- Focused red verification (before fix): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptExecutorDebugStepTests.ExecuteAsync_StepIntoParallel_ContinueReleasesAllPausedBranches" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\parallel-step-red\bin\ -p:BaseIntermediateOutputPath=artifacts\parallel-step-red\obj\` failed as expected (`continue should release all paused parallel branches` timeout).
- Implemented fix in `DebugState.WaitForResumeAsync(...)`: concurrent pause waiters now share one active `TaskCompletionSource<DebugResumeAction>` until it completes, instead of replacing the signal per waiter.
- Focused green verification: same command with `parallel-step-green` paths passed (`1` passed).
- Adjacent regression verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptExecutorDebugStepTests|FullyQualifiedName~ScriptExecutorControlFlowTests.ExecuteAsync_ParallelPropagatesBreak_OutOfWhileLoop|FullyQualifiedName~ScriptExecutorControlFlowTests.ExecuteAsync_ParallelPropagatesContinue_SkipsRemainingWhileBody|FullyQualifiedName~SshExecutionServiceFlowCanvasDebugBootstrapTests|FullyQualifiedName~DebugStateStepPathTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\parallel-step-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\parallel-step-regression\obj\` passed (`7` passed).

## 140. Fix Flow Canvas debug-step semantics and secret-variable masking
- [x] 140.1 Add failing regression coverage for breakpoint `Step` so it pauses on the next executable block instead of continuing to completion.
- [x] 140.2 Implement the minimal runtime fix for debug resume handling and verify step-by-step pause behavior.
- [x] 140.3 Add focused Flow Canvas UI coverage for masking password/secret variables in the Vars panel and implement masking.
- [x] 140.4 Run focused verification and capture review notes below.

### 140 Review
- Root cause for `Step` behavior was in `ScriptExecutor.HandleDebugPauseAsync(...)`: `Continue` explicitly disabled `StepMode`, but `Step` did not enable `StepMode`, so a step-resume from breakpoint behaved like continue and execution ran to completion.
- Added focused regression `ScriptExecutorDebugStepTests.ExecuteAsync_StepResumeFromBreakpoint_PausesAtNextStep` to require pause-at-next-step semantics.
- Runtime fix: when resume action is `DebugResumeAction.Step`, executor now sets `context.DebugState.StepMode = true`; `Continue` still disables step mode.
- Added focused browser regression `FlowCanvas/e2e/flow-canvas-variable-inspector.spec.ts` requiring password-like variable names to be masked in the Vars panel.
- Flow Canvas fix: `VariableInspector` now masks values for sensitive names (`password`, `secret`, `token`, key variants) with `"********"` while preserving existing rendering for non-sensitive variables.
- Focused red verification (step semantics): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptExecutorDebugStepTests.ExecuteAsync_StepResumeFromBreakpoint_PausesAtNextStep" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\flow-step-red\bin\ -p:BaseIntermediateOutputPath=artifacts\flow-step-red\obj\` failed as expected (`second debug pause after step should be observed within 2000ms`).
- Focused green verification (step semantics): same command with `flow-step-green` paths passed (`1` passed).
- Focused red verification (vars masking): `npm run test:e2e -- e2e/flow-canvas-variable-inspector.spec.ts` failed as expected (`password = "super-secret-password"` visible).
- Focused green verification (vars masking): same command passed (`1` passed).
- Broader verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptExecutorDebugStepTests|FullyQualifiedName~ScriptExecutorStepPathTests|FullyQualifiedName~DebugStateStepPathTests|FullyQualifiedName~SshExecutionServiceFlowCanvasDebugBootstrapTests" -v minimal -p:UseAppHost=false -p:BaseOutputPath=artifacts\flow-step-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\flow-step-regression\obj\` passed (`6` passed).
- `npm run test:e2e -- e2e/flow-canvas-parity.spec.ts e2e/flow-canvas-interactions.spec.ts e2e/flow-canvas-variable-inspector.spec.ts` passed (`7` passed).

## 139. Fix Flow Canvas first-breakpoint run-start race
- [x] 139.1 Add a focused failing regression that proves Flow Canvas-configured breakpoints pause before first script step.
- [x] 139.2 Add deterministic run-start debug bootstrap in `SshExecutionService` and wire Flow Canvas run path to use it.
- [x] 139.3 Run focused and broader debug/flow-canvas verification and record review findings.

### 139 Review
- Root cause was a race in run-start debug bootstrap: Flow Canvas breakpoints were applied via async polling (`WaitForActiveDebugStateAsync`), which could attach after step `steps/0` had already executed. This made first-block breakpoints unreliable and the run appeared to ignore pause/debug controls.
- Added focused regression `SshExecutionServiceFlowCanvasDebugBootstrapTests.ExecutePresetAsync_WithConfiguredFlowCanvasBreakpoint_PausesBeforeFirstStep` to require deterministic run-start breakpoint pause behavior.
- Added deterministic bootstrap API in `SshExecutionService`: `ConfigureFlowCanvasDebugStateForRun(...)` and `ClearFlowCanvasDebugStateForRun()`, with synchronous application to each new `ScriptContext` before `ScriptExecutor.ExecuteAsync(...)`.
- Updated `Form1` Flow Canvas run wiring to use `_sshService.ConfigureFlowCanvasDebugStateForRun(...)` instead of async bootstrap polling for initial run-state attach, and clear pending run bootstrap state on both run start prep and cleanup.
- Focused red verification (before service API): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServiceFlowCanvasDebugBootstrapTests.ExecutePresetAsync_WithConfiguredFlowCanvasBreakpoint_PausesBeforeFirstStep" -v minimal` failed as expected.
- Focused green verification: same command passed (`1` passed).
- Broader regression verification:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --no-build --filter "FullyQualifiedName~FlowCanvasBridgeTests|FullyQualifiedName~DebugStateStepPathTests|FullyQualifiedName~Form1FlowCanvasTestStepScopingTests|FullyQualifiedName~ScriptExecutorStepPathTests" -v minimal` passed (`12` passed).
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --no-build --filter "FullyQualifiedName~SshExecutionServiceFlowCanvasDebugBootstrapTests" -v minimal` passed (`1` passed).

## 138. Fix Flow Canvas run export rewriting valid scripts into invalid YAML
- [x] 138.1 Add a focused failing regression test that reproduces `set + while + parallel` round-trip export and parser failure risk.
- [x] 138.2 Normalize container `_yamlSnippet` indentation during export so mixed generated/snippet top-level steps stay YAML-valid.
- [x] 138.3 Run focused bridge/parsing verification and record findings in a review section.

### 138 Review
- Root cause was mixed top-level sequence indentation during Flow Canvas export: simple blocks were regenerated at column 0 while container blocks (`while/if/parallel/...`) were appended from stored `_yamlSnippet` with original leading indentation, producing invalid YAML when both appeared in the same `steps` list.
- Added focused regression `ExportGraphToYaml_MixedGeneratedAndContainerSteps_ProducesParsableYaml` in `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` using a `set + while + parallel` script shape matching the user repro. Red run failed with `ScriptParseException` (`While parsing a block mapping, did not find expected key.`).
- Implemented fix in `Services/FlowCanvasBridge.cs` by normalizing (dedenting) stored container snippets to top-level step indentation before appending.
- Focused red verification (before fix): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests.ExportGraphToYaml_MixedGeneratedAndContainerSteps_ProducesParsableYaml" -v minimal` failed with the expected YAML parse error.
- Focused green verification (after fix): same command passed (`1` passed).
- Broader regression verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests|FullyQualifiedName~Form1FlowCanvasTestStepScopingTests|FullyQualifiedName~DebugStateStepPathTests" -v minimal` passed (`10` passed).

## 135. Implement Flow Canvas correctness recovery plan
- [x] 135.1 Phase 0: Scaffold OpenSpec change `refactor-flow-canvas-correctness` with proposal/tasks/spec deltas and define acceptance gates.
- [x] 135.2 Phase 0: Add temporary Flow Canvas safety guardrails to prevent silent export loss and block unsafe run/test actions.
- [x] 135.3 Phase 1: Replace fragile snippet-first export path with canonical graph export behavior and structured diagnostics.
- [x] 135.4 Phase 1: Ensure all palette block types are export-supported or explicitly rejected with actionable diagnostics.
- [x] 135.5 Phase 2: Introduce scope-aware step identity (`StepPath`) in execution/debug events and remove flat step-index-only mapping assumptions.
- [x] 135.6 Phase 2: Update host-side Flow Canvas mapping and event routing to resolve by `StepPath -> nodeId` for nested flow correctness.
- [x] 135.7 Phase 3: Implement unified canvas execution message flow (`execute-canvas`) and route toolbar + keyboard run/test through it.
- [x] 135.8 Phase 3: Implement true `Test Step` semantics (single-host prerequisite chain + target step) and remove button-proxy behavior.
- [x] 135.9 Phase 4: Fix interaction regressions (move undo timing, breakpoint visual parity, context-menu separation, comment rendering, box-selection sync).
- [x] 135.10 Phase 5: Add focused tests for bridge/export diagnostics, nested mapping, run/test parity, and interaction correctness.
- [x] 135.11 Phase 5: Run focused + broader verification and document rollout notes in changelog/operator-facing docs.
- [x] 135.12 Phase 6 (Follow-up): Add a browser test harness for Flow Canvas (`FlowCanvas` app boot + host bridge stubs + deterministic fixture loading).
- [x] 135.13 Phase 6 (Follow-up): Add browser-driven parity/interaction specs (run vs test-step entry parity, context-menu/breakpoint gesture separation, comment persistence, box-select sync, drag undo behavior).
- [x] 135.14 Phase 6 (Follow-up): Wire browser harness into CI and document local/operator usage (`npm` scripts, artifact capture, troubleshooting notes).

### 135 Review
- Added OpenSpec change set under `openspec/changes/refactor-flow-canvas-correctness/` (`proposal.md`, `tasks.md`, `design.md`, and spec deltas for flow-canvas + scripting-runtime) and validated with `openspec validate refactor-flow-canvas-correctness --strict --no-interactive`.
- Added host/bridge safety gate: `apply-result` diagnostics now return structured `success/errors/warnings/nodeStepMap`; run/test requests are rejected when export is invalid or selected step is not executable.
- Implemented `execute-canvas` unified path end-to-end (toolbar + keyboard) and removed legacy step-into UI usage while preserving compatibility handlers for deprecated host message aliases.
- Implemented test-step execution scoping: host now truncates script to the selected top-level boundary and applies path-aware disable filters so only prerequisite-chain nodes + target scope execute for nested targets.
- Implemented runtime StepPath propagation: `ScriptStep.StepPath`, executor-assigned canonical nested paths, debug pause/start/complete payloads with `StepPath`, and host-side `StepPath -> nodeId` resolution.
- Updated debug bootstrap to configure `DebugState` with node-to-step-path mapping, keeping line breakpoints compatibility while removing index-only assumptions from the main event path.
- Added focused regression tests: `FlowCanvasBridgeTests` (unsupported blocks, comment diagnostics, child-node step-path mapping), `DebugStateStepPathTests` (step-path breakpoint resolution + index-map compatibility), `Form1FlowCanvasTestStepScopingTests` (YAML truncation + prerequisite chain scoping), and `ScriptExecutorStepPathTests` (nested canonical paths + step lifecycle event payload paths).
- Added follow-up Phase 6 plan for browser-harness automation to cover browser-level Flow Canvas parity and interaction behavior beyond host/runtime unit coverage.
- Scaffolded browser harness in `FlowCanvas` with Playwright (`playwright.config.ts`, `e2e/support/harness.ts`, deterministic graph fixtures, and first parity suite in `e2e/flow-canvas-parity.spec.ts`), plus local run/operator notes in `docs/flow-canvas-browser-harness.md`.
- Added interaction browser specs in `e2e/flow-canvas-interactions.spec.ts` for drag-undo restoration, context-menu/breakpoint gesture separation, comment undo/redo persistence, and box-select -> delete sync.
- Wired browser harness into CI in `.github/workflows/build-release.yml` (`flowcanvas-browser-tests` with Playwright artifact upload) and extended troubleshooting/operator notes.
- Verification executed:
- `dotnet build -v minimal`
- `npm run build` in `FlowCanvas/`
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests|FullyQualifiedName~DebugStateStepPathTests|FullyQualifiedName~Form1FlowCanvasTestStepScopingTests|FullyQualifiedName~ScriptExecutorStepPathTests" -v minimal`
- `dotnet test -v minimal` (`1724` passed)
- `npm run test:e2e` in `FlowCanvas/` (`6` passed)

## 134. Archive completed OpenSpec changes
- [x] 134.1 Confirm which active OpenSpec changes are complete and archive-ready.
- [x] 134.2 Archive each completed change with `openspec archive <id> --yes` and review the CLI output for spec updates/archive placement.
- [x] 134.3 Run strict OpenSpec validation after archiving and capture the review outcome below.

### 134 Review
- `openspec list` showed one archive-ready completed change: `add-browser-callback-webview2-mode`. The other active changes (`add-preset-delete-undo`, `add-script-subroutines-and-libraries`, `add-script-assertions`) were not complete and were left untouched.
- `openspec show add-browser-callback-webview2-mode` confirmed the change was still active before archiving.
- `openspec archive add-browser-callback-webview2-mode --yes` succeeded. The CLI updated two live specs: `openspec/specs/scripting-network-steps/spec.md` (`+4`) and `openspec/specs/scripting-runtime/spec.md` (`+5`), then moved the change into `openspec/changes/archive/2026-03-21-add-browser-callback-webview2-mode`.
- Post-archive verification: `openspec list` no longer shows `add-browser-callback-webview2-mode` as an active change, and the archive directory now contains `2026-03-21-add-browser-callback-webview2-mode`.
- `openspec validate --strict --no-interactive` produced the CLI's "Nothing to validate" message after the archive because no explicit target was selected, so I reran the effective repo-wide check as `openspec validate --all --strict --no-interactive`, which passed (`23` items, `0` failed).

## 133. Show connection-test status in row headers for selected hosts
- [x] 133.1 Add focused failing WinForms coverage for row-header connection-test visuals, including selected-row visibility, clearing on reset/edit, and theme reapplication.
- [x] 133.2 Update `Form1` connection-test state/rendering so row headers reflect testing/success/failure while preserving existing `Host_IP` cell tinting for unselected rows.
- [x] 133.3 Run focused UI verification, broader regression verification, and build verification; then capture the review outcome below.

### 133 Review
- Root cause was the host grid’s owner-drawn selected-cell path in `Form1`: `Dgv_Variables_CellPainting(...)` repaints selected data cells with the selection color, so the existing green/red `Host_IP` tint disappeared whenever the tested row stayed selected.
- Added focused coverage in `SSH_Helper.Tests/UI/Form1ConnectionTestStatusTests.cs` for the exact contracts the user asked for: successful selected rows, failed selected rows, clearing via `ClearConnectionTestIndicators()`, clearing on `Host_IP` edits, theme reapplication, and the existing queued-progress completion-status regression.
- The red verification failed for the right runtime reason before the fix: every new assertion showed `row.HeaderCell.Style.BackColor` staying `Color.Empty`, proving there was no row-header status lane for selected hosts.
- `Form1.cs` now keeps per-row connection-test visual state, applies both `Host_IP` cell styling and row-header styling from that state, marks rows as `Testing` before async connection checks complete, and clears/reapplies the state through clear, edit, and theme-change paths.
- `Dgv_Variables_RowPostPaint(...)` now paints the row-header background from the stored connection-test state and draws the row number with a contrast-aware foreground so success/failure/testing remain readable in both themes.
- Focused red verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ConnectionTestStatusTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\connection-status-red\bin\ -p:BaseIntermediateOutputPath=artifacts\connection-status-red\obj\`` failed as intended (`5` failed, `1` passed) because row-header styles remained empty before the implementation.
- Focused green verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ConnectionTestStatusTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\connection-status-green\bin\ -p:BaseIntermediateOutputPath=artifacts\connection-status-green\obj\`` passed (`6` passed, `0` failed).
- Broader host-grid/UI verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ConnectionTestStatusTests|FullyQualifiedName~Form1BufferedSurfacesTests|FullyQualifiedName~HostGridUtilitiesTests|FullyQualifiedName~JobEditorDialogHostGridParityTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\connection-status-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\connection-status-ui\obj\`` (`27` passed, `0` failed).
- `dotnet build .\SSH_Helper.sln -nologo` passed. The build still reported the existing `MSB3277` `WindowsBase`/WebView2 warnings, the existing `xUnit1031` warnings in `ExpressionParserTests.cs`, and transient `MSB3026` copy-retry warnings because a running `SSH_Helper.exe` process (`PID 86392`) had the apphost locked while the build produced `SSH_Helper.dll`.

## 132. Restore Presets/Favorites selection sync across tab switches
- [x] 132.1 Add failing WinForms coverage for switching between Presets and Favorites while each tab keeps its own selected preset.
- [x] 132.2 Update `Form1` so tab changes re-sync the commands pane to the active tab selection and Favorites rebuilds preserve their selected node.
- [x] 132.3 Run focused UI verification, broader regression verification, and capture the review outcome below.

### 132 Review
- Root cause was split across two gaps in `Form1`: switching between `Presets` and `Favorites` only synchronized the visible tab/header state, not the commands editor, and `RefreshFavoritesList()` rebuilt the Favorites tree from scratch without restoring its selected node.
- Added `SSH_Helper.Tests/UI/Form1PresetTabSelectionTests.cs` with a focused WinForms regression that loads a temp config snapshot, selects different presets on each tab, and proves two contracts: returning to `Presets` reloads the preset already selected there, and returning to `Favorites` restores the previously selected favorite and its commands.
- `Form1.cs` now routes both tree selection handlers and tab switches through one shared preset/folder selection application path, including the existing dirty-check behavior. The form also remembers the last selected node per tree so tab changes do not depend on `TreeView.SelectedNode` surviving a hide/show cycle.
- `RefreshFavoritesList()` now preserves the previously selected favorite across tree rebuilds and restores it quietly before the active-tab sync reapplies the corresponding commands in the editor.
- Focused red verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTabSelectionTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-tab-selection-red\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tab-selection-red\obj\`` failed as intended on the original bug (`editor.Text` stayed on `echo beta` after switching back to `Presets`).
- Focused green verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTabSelectionTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-tab-selection-green\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tab-selection-green\obj\`` passed (`1` passed, `0` failed).
- Broader preset/UI verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTabSelectionTests|FullyQualifiedName~Form1BufferedSurfacesTests|FullyQualifiedName~PresetTreeSelectionGuardTests|FullyQualifiedName~PresetTreeViewportRestorerTests|FullyQualifiedName~PresetTreeDeleteMutationTests|FullyQualifiedName~PresetDeletionSelectionResolverTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-tab-selection-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tab-selection-ui\obj\`` (`22` passed, `0` failed).
- `dotnet build .\SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\preset-tab-selection-build\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tab-selection-build\obj\`` passed. The build still reports the existing `MSB3277` WebView2/`WindowsBase` warnings and the existing `xUnit1031` warnings from `ExpressionParserTests`.

## 131. Replace visible Presets/Favorites native tab chrome with a buffered custom header
- [x] 131.1 Add failing coverage that requires the presets area to use a dedicated buffered header strip and a clipped viewport for the underlying native tab control.
- [x] 131.2 Keep `presetsTabControl` as the content/state host, but hide its native header from the visible surface and route the visible `Presets` / `Favorites` switching through a custom buffered header strip.
- [x] 131.3 Run focused buffered-surface verification, broader UI verification, callback-adjacent verification, and build verification; then record the corrected root cause below.

### 131 Review
- The latest user repro showed Tasks 126-130 still were not at the real owner of the remaining flicker. Even after buffering adjacent panels and patching the hidden-border seam, the visible `Presets` / `Favorites` header still belonged to the native Win32 `TabControl`, so native repaint could still flash in that strip during `Run Selected`.
- Added red coverage in `Form1BufferedSurfacesTests` that requires three concrete contracts: a dedicated `PresetTabHeaderStrip` control exists, the native `presetsTabControl` is hosted inside a viewport panel and shifted upward to hide its header, and tab selection remains synchronized between the visible header strip and the underlying tab control. The focused red run failed exactly there because none of those contracts existed yet.
- Added `UI/PresetTabHeaderStrip.cs`, a buffered custom control that paints the visible `Presets` / `Favorites` tabs in dark/light themes and raises `SelectedIndexChanged` like a normal two-tab switcher.
- `Form1.Designer.cs` now hosts the presets tab content inside `presetsTabViewportPanel` and places `presetsTabHeaderStrip` above it. The native `presetsTabControl` stays in place as the actual content/state host, but its header is clipped out of the visible surface by shifting the control upward inside the viewport.
- `Form1.cs` now initializes the custom header strip, keeps its selection synchronized with `presetsTabControl`, updates the viewport offset from the native tab-header height, and applies the same theme/font changes to the custom header so the visible behavior stays aligned with the previous UI.
- Focused red verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1BufferedSurfacesTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\custom-presets-header-red\bin\ -p:BaseIntermediateOutputPath=artifacts\custom-presets-header-red\obj\`` failed as intended (`3` failed, `8` passed).
- Focused green verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1BufferedSurfacesTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\custom-presets-header-green2\bin\ -p:BaseIntermediateOutputPath=artifacts\custom-presets-header-green2\obj\`` passed (`11` passed, `0` failed).
- Broader UI verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~Form1BufferedSurfacesTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\custom-presets-header-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\custom-presets-header-ui\obj\`` (`33` passed, `0` failed).
- Callback-adjacent verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\custom-presets-header-callback\bin\ -p:BaseIntermediateOutputPath=artifacts\custom-presets-header-callback\obj\`` (`9` passed, `0` failed).
- `dotnet build .\SSH_Helper.sln -nologo` passed with only the existing `MSB3277` WebView2/`WindowsBase` warnings and the existing five `xUnit1031` warnings.
- Manual validation is still required in the live app: rerun the preset flow and confirm the remaining visible flicker around `Presets` / `Favorites` is gone now that the native header is no longer on the visible surface.

## 130. Remove residual Presets/Favorites header-gap flicker during Run Selected
- [x] 130.1 Add failing coverage for the borderless tab control’s early background paint contract around the header gap beside `Favorites`.
- [x] 130.2 Paint the hidden-border header background during the tab control’s erase/paint lifecycle so the trailing header gap stays dark before native repaint finishes.
- [x] 130.3 Run focused tab/UI verification, callback-adjacent verification, and build verification; then record the corrected root cause below.

### 130 Review
- The latest user repro showed Task 129 was still not at the real seam. The remaining flash was not the run strip anymore; it was the borderless tab header gap beside `Favorites` itself.
- Added a red test in `BorderlessTabControlTests` that dispatches `WM_ERASEBKGND` into a bitmap-backed HDC and samples the trailing header gap after erase, before the later post-`WM_PAINT` seam overlay runs. That red test failed exactly as expected: the gap remained pure white during erase.
- Root cause was the `BorderlessTabControl.WndProc(...)` erase path. When `HideBorder` was enabled, `WM_ERASEBKGND` returned handled (`m.Result = 1`) but painted nothing, so the header gap beside the last tab stayed unpainted until the later seam overlay. During `Run Selected` invalidations, that left a visible flash in the exact region the user pointed out.
- `BorderlessTabControl` now paints the hidden-border background during `WM_ERASEBKGND` when an HDC is provided. The new `PaintHiddenBorderBackground(...)` helper fills the client area with the dark content color and fills the full tab-header band with the dark header color before native paint continues. The existing post-`WM_PAINT` overlay remains in place for the seam/edge cleanup.
- Focused red verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\tab-header-gap-red\bin\ -p:BaseIntermediateOutputPath=artifacts\tab-header-gap-red\obj\`` failed as intended (`1` failed, `5` passed).
- Focused green verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\tab-header-gap-green\bin\ -p:BaseIntermediateOutputPath=artifacts\tab-header-gap-green\obj\`` passed (`6` passed, `0` failed).
- Broader UI verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~Form1BufferedSurfacesTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\tab-header-gap-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\tab-header-gap-ui\obj\`` (`30` passed, `0` failed).
- Callback-adjacent verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\tab-header-gap-callback\bin\ -p:BaseIntermediateOutputPath=artifacts\tab-header-gap-callback\obj\`` (`9` passed, `0` failed).
- A normal `dotnet build .\SSH_Helper.sln -nologo` attempt was blocked because the running SSH Helper instance locked `bin\Debug\net8.0-windows\SSH_Helper.exe` (`PID 177044`).
- Equivalent compile verification passed with `dotnet build .\SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\tab-header-gap-build\bin\ -p:BaseIntermediateOutputPath=artifacts\tab-header-gap-build\obj\`` with only the existing `MSB3277` and `xUnit1031` warnings.
- Manual validation is still required in the live app: click `Run Selected` repeatedly and confirm the small flash beside `Favorites` is actually gone.

## 129. Remove remaining Run Selected flicker around Presets/Favorites
- [x] 129.1 Add failing coverage for the remaining run-start repaint seams around the Presets/Favorites area.
- [x] 129.2 Convert the remaining run-strip and preset-search surfaces to buffered containers and batch the run-strip execution-state transition to avoid live layout churn.
- [x] 129.3 Run focused UI verification, callback-adjacent verification, and a build; then record the corrected root cause below.

### 129 Review
- The latest repro moved the remaining flicker off the tab-strip paint path and onto the `Run Selected` state transition itself. Tracing that click path showed `SetExecutionMode(true)` immediately toggles `btnStopAll.Visible`, and the two surfaces nearest the Presets/Favorites seam that participate in that transition were still plain `Panel`s: the bottom execute strip and the runtime-created preset search strip.
- Added red coverage in `Form1BufferedSurfacesTests` for both seams. The initial focused run failed exactly where expected: `executePanel` was still declared as `System.Windows.Forms.Panel`, and `_presetSearchPanel` was still instantiated as `System.Windows.Forms.Panel`. The tab-page `UseVisualStyleBackColor` contract already passed, so the fix stayed tight on the still-unbuffered surfaces.
- `Form1.Designer.cs` now declares `executePanel` as `BufferedPanel`, and `Form1.InitializePresetSearchFilter()` now creates `_presetSearchPanel` as `BufferedPanel`. `SetExecutionMode(...)` also batches the run-strip button-state flip inside `executePanel.SuspendLayout()/ResumeLayout(false)` and avoids redundant property sets before invalidating just that strip.
- Focused red verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1BufferedSurfacesTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\run-selected-flicker-red\bin\ -p:BaseIntermediateOutputPath=artifacts\run-selected-flicker-red\obj\`` failed as intended (`2` failed, `6` passed).
- Focused green verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1BufferedSurfacesTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\run-selected-flicker-green\bin\ -p:BaseIntermediateOutputPath=artifacts\run-selected-flicker-green\obj\`` passed (`8` passed, `0` failed).
- Broader UI verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~Form1BufferedSurfacesTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\run-selected-flicker-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\run-selected-flicker-ui\obj\`` (`29` passed, `0` failed).
- Callback-adjacent verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\run-selected-flicker-callback\bin\ -p:BaseIntermediateOutputPath=artifacts\run-selected-flicker-callback\obj\`` (`9` passed, `0` failed).
- A normal `dotnet build .\SSH_Helper.sln -nologo` attempt was blocked because `bin\Debug\net8.0-windows\SSH_Helper.exe` was locked by the running SSH Helper process (`PID 53200`).
- Equivalent compile verification passed with `dotnet build .\SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\run-selected-flicker-build\bin\ -p:BaseIntermediateOutputPath=artifacts\run-selected-flicker-build\obj\`` with only the existing `MSB3277` and `xUnit1031` warnings.
- Manual validation is still required in the live app: click `Run Selected` repeatedly on the preset flow and confirm the remaining Presets/Favorites-area flicker is actually gone.

## 128. Restore dark tab-strip coverage while keeping Presets/Favorites stable
- [x] 128.1 Replace the incorrect managed-paint-only contract with failing tests for a buffered post-native hidden-border overlay path.
- [x] 128.2 Reintroduce the hidden-border seam cleanup through a dedicated buffered overlay renderer instead of direct multi-draw `Graphics.FromHwnd` patches.
- [x] 128.3 Run focused borderless-tab, broader UI, callback-adjacent verification, and a build; then record the corrected root cause below.

### 128 Review
- The latest user repro showed Task 127 was still incomplete: removing the direct `WM_PAINT` overlay eliminated one source of flicker, but it also exposed native white seams at launch and still left a small flicker around the `Presets` tab.
- Root cause was paint order. The managed `TabControl_Paint(...)` path can render the right colors in isolation, but the live WinForms/native `TabControl` lifecycle still paints chrome after that path for hidden-border tabs. So the remaining seams required a post-native overlay, just not the old direct multi-rectangle `Graphics.FromHwnd` patching that flickered.
- Added corrected red tests in `BorderlessTabControlTests`: one requires dark borderless tabs to avoid a managed `Paint` handler again, and one requires a dedicated hidden-border overlay renderer (`PaintHiddenBorderOverlay`) that can repaint the trailing header gap dark. The red slice failed for the expected reasons (`2` failed, `3` passed).
- `BorderlessTabControl` now owns a dedicated hidden-border overlay renderer with configurable header/inactive-tab colors. `WndProc(...)` still suppresses `WM_ERASEBKGND`, but its `WM_PAINT` follow-up now renders the seam overlay into an offscreen bitmap and blits once, instead of issuing a sequence of direct `Graphics.FromHwnd` fills against the live window surface.
- `Form1.ApplyDarkTabControl(...)` and `DialogTheme.StyleTabControl(...)` now configure that buffered hidden-border overlay for borderless tabs and no longer attach the managed `Paint` handler in those cases. Non-borderless tabs still use the existing managed `Paint` path.
- Focused red verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-buffered-overlay-red\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-buffered-overlay-red\obj\`` failed as intended (`2` failed, `3` passed).
- Focused green verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-buffered-overlay-green\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-buffered-overlay-green\obj\`` passed (`5` passed, `0` failed).
- Broader UI verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~Form1BufferedSurfacesTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-buffered-overlay-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-buffered-overlay-ui\obj\`` (`26` passed, `0` failed).
- Callback-adjacent verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-buffered-overlay-callback\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-buffered-overlay-callback\obj\`` (`9` passed, `0` failed).
- `dotnet build .\SSH_Helper.sln -nologo` passed with only the existing `MSB3277` WebView2/`WindowsBase` warnings and the existing five `xUnit1031` warnings.
- Manual validation is still required in the live app: relaunch SSH Helper in dark mode and rerun the callback preset to confirm the tab strip no longer shows the white launch seam or the residual flicker around `Presets` / `Favorites`.

## 127. Remove residual native-overlay flicker from Presets/Favorites tabs
- [x] 127.1 Replace the incorrect borderless-tab paint hypothesis with a failing test that proves dark borderless tabs should use one buffered managed overlay path.
- [x] 127.2 Move the borderless-tab hidden-border overlay off the direct post-`WM_PAINT` path and onto the normal managed paint path used during dark tab styling.
- [x] 127.3 Run focused tab/UI verification, callback-adjacent verification, and a build; then record the corrected root cause below.

### 127 Review
- The user's follow-up video showed Task 126's hypothesis was incomplete: removing the extra managed `Paint` hookup reduced one overlay path, but the tabs still flickered during the callback activation flow.
- Root cause was the remaining direct post-`WM_PAINT` overdraw in `BorderlessTabControl.WndProc(...)`, which used `Graphics.FromHwnd(Handle)` to patch the tab strip after native painting. That bypassed the normal buffered paint path, so a small tab-header flash could still show during activation repaints.
- Replaced the earlier test with a more accurate red contract in `BorderlessTabControlTests`: `ApplyDarkTabControl_WhenBorderlessTabControl_AttachesManagedPaintOverlay`. The red slice failed for the correct reason because borderless dark tabs had zero managed overlay handlers.
- `Form1.ApplyDarkTabControl(...)` now restores the managed `TabControl_Paint` overlay for `BorderlessTabControl`, while `BorderlessTabControl.WndProc(...)` keeps only `WM_ERASEBKGND` suppression and no longer performs direct post-`WM_PAINT` drawing. That leaves one buffered managed overlay path instead of a native overdraw patch.
- While touching the same owner-draw path, `TabControl_DrawItem(...)` now uses `DarkSurface2` instead of the stray `Color.Red` top-edge pen for unselected tabs, aligning the strip with the rest of the dark header colors.
- Red verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-native-overlay-red\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-native-overlay-red\obj\`` failed as intended (`1` failed, `2` passed).
- Green verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-native-overlay-green\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-native-overlay-green\obj\`` passed (`3` passed, `0` failed).
- Broader UI verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~Form1BufferedSurfacesTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-native-overlay-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-native-overlay-ui\obj\`` (`24` passed, `0` failed).
- Callback-adjacent verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-native-overlay-callback\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-native-overlay-callback\obj\`` (`9` passed, `0` failed).
- `dotnet build .\SSH_Helper.sln -nologo` passed with only the existing `MSB3277` WebView2/`WindowsBase` warnings and the existing five `xUnit1031` warnings.
- Manual validation is still required in the live app: rerun the callback preset and confirm the `Presets` / `Favorites` tabs no longer show the residual flicker during launch/close activation repaint.

## 126. Remove residual Presets/Favorites tab-header flicker
- [x] 126.1 Add failing coverage proving `ApplyDarkTabControl` does not attach a redundant managed `Paint` overlay to `BorderlessTabControl`.
- [x] 126.2 Remove the duplicate paint hookup so the preset/favorites header overlay is owned in one place only.
- [x] 126.3 Run targeted UI and callback verification, build, and record the outcome below.

### 126 Review
- The user's latest repro narrowed the remaining issue to a small flash around the `Presets` / `Favorites` tab header after the larger callback and whole-form flicker fixes were already in place.
- Root cause was duplicate painting on the same surface: `BorderlessTabControl` already owns its hidden-border/header overlay in `WndProc` during `WM_PAINT`, but `Form1.ApplyDarkTabControl(...)` was also attaching `TabControl_Paint` to the same control.
- Added a focused red test in `BorderlessTabControlTests`: `ApplyDarkTabControl_WhenBorderlessTabControl_DoesNotAttachExtraPaintHandler`. The red slice failed for the correct reason because one managed event entry remained attached after dark-tab styling.
- `Form1.ApplyDarkTabControl(...)` now still applies `DrawItem` styling and the `BorderlessTabControl` appearance properties, but only non-borderless tabs receive the extra `TabControl_Paint` handler. That leaves `BorderlessTabControl` as the sole owner of the tab-header overlay path.
- Red verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-flicker-red\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-flicker-red\obj\`` failed as intended (`1` failed, `2` passed).
- Green verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-flicker-green\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-flicker-green\obj\`` passed (`3` passed, `0` failed).
- Broader UI verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~Form1BufferedSurfacesTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-flicker-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-flicker-ui\obj\`` (`24` passed, `0` failed).
- Callback-adjacent verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\presets-tab-flicker-callback\bin\ -p:BaseIntermediateOutputPath=artifacts\presets-tab-flicker-callback\obj\`` (`9` passed, `0` failed).
- `dotnet build .\SSH_Helper.sln -nologo` passed with only the existing `MSB3277` WebView2/`WindowsBase` warnings and the existing five `xUnit1031` warnings.
- Manual validation is still required in the live app: rerun the self-contained callback preset and confirm the `Presets` / `Favorites` tab header no longer shows the residual flicker during activation repaint.

## 125. Remove remaining callback-launch flicker in the main form
- [x] 125.1 Lock the root cause with focused failing tests for keep-open WebView2 callback launch behavior and the presets tab repaint path.
- [x] 125.2 Implement the minimal fix so keep-open callback windows stop forcing whole-form disabled-state repaints, and narrow any remaining preset-tab overpaint that still flickers during activation.
- [x] 125.3 Run focused callback/UI verification, rerun the relevant regression slices, and record the outcome below.

### 125 Review
- Root-cause investigation from the user’s latest repro points at the keep-open WebView2 path disabling the entire SSH Helper owner form while the callback is pending. That broad `_owner.Enabled = false` repaint lines up with the “labels disappear” symptom during launch.
- Added red tests to prove the issue at the correct seams: `BrowserCallbackUiHostTests` now requires keep-open WebView2 launch to leave the owner form enabled, and `Form1BufferedSurfacesTests` requires the preset/script/history header surfaces to use `BufferedPanel` instead of raw `Panel`.
- The red slice failed for the expected reasons: the callback host was disabling the owner form, and the affected header panels were still plain `Panel` fields.
- The production fix removed the keep-open owner-disable/reenable path from `BrowserCallbackUiHost`, preserving the modeless WebView2 behavior and close-path focus restore without forcing a whole-form disabled-state repaint.
- `Form1.Designer.cs` now uses `BufferedPanel` for `hostsHeaderPanel`, `presetsHeaderPanel`, `scriptHeaderPanel`, `scriptFooterPanel`, and `historyHeaderPanel` so the label-heavy header surfaces exposed during callback activation repaint through the buffered container wrapper.
- Focused verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~Form1BufferedSurfacesTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-task125-green\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-task125-green\obj\`` (`9` passed, `0` failed).
- Focused UI verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BufferedContainerControlTests|FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~Form1BufferedSurfacesTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-task125-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-task125-ui\obj\`` (`26` passed, `0` failed).
- Focused callback verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-task125-callback-focused\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-task125-callback-focused\obj\`` (`9` passed, `0` failed).
- Browser-callback regression verification passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallback|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-task125-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-task125-regression\obj\`` (`90` passed, `0` failed).
- `dotnet build .\SSH_Helper.sln -nologo` passed with the existing `MSB3277` WebView2/`WindowsBase` warnings and the existing five `xUnit1031` warnings only.
- Manual validation is still required for the exact user repro: rerun the two-callback self-contained preset in SSH Helper and confirm the preset tab area and header labels stay visually stable during callback launch and close.

## 124. Review Task 2 buffered container tests quality
- [x] 124.1 Inspect `BufferedContainerControlTests.cs` against existing UI-test patterns and Task 2 requirements.
- [x] 124.2 Run a focused test command to determine whether the current red state is a runtime failure or a compile-time break.
- [x] 124.3 Record strengths, issues, and approval status for the Task 2 review.

### 124 Review
- Initial review found a real issue: `BufferedContainerControlTests` directly referenced the not-yet-created `SSH_Helper.BufferedSplitContainer` type, so the red slice failed at compile time instead of as an executable test run.
- The test file was then corrected to resolve `SSH_Helper.BufferedPanel` and `SSH_Helper.BufferedSplitContainer` via reflection at runtime, which preserves the intended red state without blocking compilation.
- Follow-up quality review approved the revised file. The only remaining note was a minor brittleness concern around anchoring assembly lookup to an unrelated type; I removed that by switching the test helper to use `typeof(SSH_Helper.Form1).Assembly`.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BufferedContainerControlTests|FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase1-red\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase1-red\obj\` failed as expected at runtime (`3` failed, `2` passed) because `SSH_Helper.BufferedPanel` and `SSH_Helper.BufferedSplitContainer` do not exist yet.

## 123. Reduce general-interaction whole-form flicker
- [x] 123.1 Add failing tests for host-grid/history redraw batching and any new main-form repaint helper seam.
- [x] 123.2 Implement the phase-2 redraw narrowing in `HostGridRestoreBatcher`, `HistoryListBox`, and `Form1` without expanding into unrelated dialog/theme cleanup.
- [x] 123.3 Run focused UI verification, rerun phase-1 callback regressions, build, and capture the review outcome below.

### 123 Review
- Task 123.1 is complete. Extended `HostGridRestoreBatcherTests` with an exact public `BeginMutationScope()` contract check plus explicit deferral assertions, and added `HistoryListBoxTests` covering both the width-stable resize path and the font-change-plus-explicit-refresh duplicate-work path.
- Kept the new history-list coverage handle-based only with an attached in-memory host control and no visible top-level form. `ApplyFontSettingsTests.cs` did not need modification because no new helper seam was required.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostGridRestoreBatcherTests|FullyQualifiedName~HistoryListBoxTests|FullyQualifiedName~ApplyFontSettingsTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase2-red\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase2-red\obj\` failed as intended at assertion time, not compile time (`2` failed, `38` passed).
- The two intended red failures are:
- missing public `BeginMutationScope()` on `HostGridRestoreBatcher`
- duplicate `HistoryListBox` work when a font change is followed by an explicit `RefreshVariableItemHeights()` call
- A quality-review concern remained about using exact public reflection instead of a typed call for the future `BeginMutationScope()` API. I kept the reflection contract because Task 5 explicitly required a compile-safe red state before the API exists; a direct typed call would fail the whole test project at compile time.
- Task 123.2 is complete. `HostGridRestoreBatcher` now exposes `BeginMutationScope()` and batches scrollbar/host-count refreshes until both restore and mutation scopes exit, `HistoryListBox` suppresses the immediate post-font duplicate refresh, and `Form1` now uses mutation scopes for `ClearGrid()` and `PasteFromClipboard()`, narrows `DeleteSelectedCells()` repainting, removes the whole-form `Refresh()` from `ApplyTheme()`, and narrows `scriptHeaderPanel.Invalidate(true)` to `Invalidate()`.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostGridRestoreBatcherTests|FullyQualifiedName~HistoryListBoxTests|FullyQualifiedName~ApplyFontSettingsTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase2-green\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase2-green\obj\` passed (`40` passed, `0` failed).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with the existing `MSB3277` WindowsBase conflicts and existing `xUnit1031` warnings.
- Task 123.2 correction pass tightened the remaining review gaps: `OpenCsvFile()` now runs under `BeginHostGridMutationScope()`, `DeleteSelectedCells()` batches its host-count churn through the existing mutation scope path, and `HistoryListBox` now suppresses width-stable explicit refresh repeats more generally instead of only after a font change.
- Verification for the correction pass: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostGridRestoreBatcherTests|FullyQualifiedName~HistoryListBoxTests|FullyQualifiedName~ApplyFontSettingsTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase2-green\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase2-green\obj\` passed (`41` passed, `0` failed).
- Task 123.2 review-fix pass addressed the remaining follow-up notes: `ClearGrid()` now refreshes the hosts-file indicator after clearing loaded-file tracking state, `HistoryListBoxTests` now proves the font change itself did observable work before checking the follow-up explicit refresh, and `HostGridRestoreBatcherTests` now uses the public `BeginMutationScope()` API directly.
- Verification for the review-fix pass: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostGridRestoreBatcherTests|FullyQualifiedName~HistoryListBoxTests|FullyQualifiedName~ApplyFontSettingsTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase2-green\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase2-green\obj\` passed (`41` passed, `0` failed).
- Task 123.3 verification is complete. The broadened phase-2 UI slice passed with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostGridRestoreBatcherTests|FullyQualifiedName~HistoryListBoxTests|FullyQualifiedName~ApplyFontSettingsTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase2-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase2-ui\obj\`` (`57` passed, `0` failed).
- Reran the callback-related regression slice against the phase-1 buffering/focus surfaces with `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BufferedContainerControlTests|FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase2-callback-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase2-callback-regression\obj\`` (`11` passed, `0` failed).
- To keep that callback slice deterministic, the callback/focus WinForms tests now run inside a shared non-parallel xUnit collection so the process-global activation override and visible-form cleanup cannot race across classes.
- A prior verification attempt produced misleading failures because I ran WinForms-heavy test processes in parallel. After rerunning those same slices serially and cleaning the stale `testhost`, the failures disappeared; the passing serial reruns above are the results that count.
- Final verification: `dotnet build .\SSH_Helper.sln -nologo` passed with the same existing `MSB3277` WebView2/`WindowsBase` conflict warning and the same existing five `xUnit1031` warnings in `ExpressionParserTests.cs`.
- Remaining manual validation for the whole-form flicker work is still interactive: rerun the two-callback preset in SSH Helper and confirm the main form regain-focus path looks clean end to end.

## 122. Reduce callback regain-focus whole-form flicker
- [x] 122.1 Add failing tests for new buffered panel/split-container infrastructure used by the main form.
- [x] 122.2 Implement buffered container controls and apply them to the top-level `Form1` split/panel hierarchy that repaints during callback regain-focus.
- [ ] 122.3 Run focused UI/browser-callback verification, build, manually validate the two-callback preset flow, and capture the review outcome below.

### 122 Review
- Task 122.1 is complete. Added `BufferedContainerControlTests` to lock the new buffered panel and split-container contracts: buffered painting styles for both controls plus `WM_ERASEBKGND` suppression coverage for the split-container path.
- Kept the tests handle-based only and added `Application.OpenForms.Count == 0` guards so the focused slice cannot leak a visible top-level form during test execution.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BufferedContainerControlTests|FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase1-red\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase1-red\obj\` failed as intended (`3` failed, `2` passed) because the buffered container controls are not implemented yet.
- Task 122.2 is complete. Added `BufferedPanel` and `BufferedSplitContainer` as small buffering wrappers, then swapped the top-level phase-1 `Form1` surfaces to the buffered types without expanding into header/footer panels.
- `BufferedPanel` and `BufferedSplitContainer` both enable `OptimizedDoubleBuffer`, `AllPaintingInWmPaint`, and `ResizeRedraw`. After review feedback, both controls now gate `WM_ERASEBKGND` suppression behind opaque-surface checks instead of swallowing background erase unconditionally.
- Updated the following `Form1` activation surfaces to buffered types: `mainSplitContainer`, `topSplitContainer`, `commandSplitContainer`, `outputSplitContainer`, `historySplitContainer`, `hostsPanel`, `commandPanel`, `presetsPanel`, `scriptPanel`, `outputPanel`, `outputRightPanel`, `historyPanel`, and `hostListPanel`.
- Focused implementation verification passed with:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BufferedContainerControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-task3-buffered-only\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-task3-buffered-only\obj\` (`3/3`)
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BufferedContainerControlTests|FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-task3-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-task3-ui\obj\` (`5/5`)
- Automated Task 122.3 verification also passed with:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BufferedContainerControlTests|FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase1-ui\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase1-ui\obj\` (`21/21`)
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase1-callback-focused\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase1-callback-focused\obj\` (`8/8`)
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallback|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\whole-form-flicker-phase1-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\whole-form-flicker-phase1-regression\obj\` (`89/89`)
- `dotnet build .\SSH_Helper.sln -nologo` passed.
- The first attempt at the broader regression slice timed out and left a stale `testhost` process locking the shared output path. After stopping the orphaned `testhost` processes, the rerun passed cleanly. Existing `MSB3277` WebView2/`WindowsBase` warnings and the existing five `xUnit1031` warnings remain unchanged.
- Manual interactive verification is still pending: rerun the two-callback preset in SSH Helper and confirm the main window regains focus without the broad client-area flash that originally exposed the whole-form flicker.

## 121. Plan whole-form flicker reduction
- [x] 121.1 Trace the main-form activation, layout, and repaint paths that still flicker after the presets-tab fix.
- [x] 121.2 Decompose the remaining flicker into a first scoped phase instead of treating the entire form as one opaque bug.
- [x] 121.3 Capture the agreed design/plan before implementation.

### 121 Review
- Reviewed the main-form redraw path and confirmed the remaining flicker is not one missing buffering flag. `Form1` already uses form-level double buffering, but the app still contains a deep split-container/panel hierarchy plus several broad `Refresh()` / `Invalidate()` paths that can flash during activation and interaction repaint.
- Agreed on a two-phase design instead of a single risky whole-form pass: phase 1 targets callback regain-focus flicker after embedded callback windows close; phase 2 targets broader interaction/layout flicker once the activation path is stable.
- Wrote the approved design to `docs\superpowers\specs\2026-03-20-whole-form-flicker-reduction-design.md`.
- Wrote the implementation plan to `docs\superpowers\plans\2026-03-20-whole-form-flicker-reduction.md`.
- I did not create a commit because the worktree already contains unrelated in-flight changes.

## 120. Remove visible host window from borderless tab control tests
- [x] 120.1 Update the new `BorderlessTabControl` regression test so it exercises the handle/erase path without showing a top-level `Form`.
- [x] 120.2 Rerun the targeted test suite and confirm the test still passes without leaving a visible blank window behind.

### 120 Review
- Root cause was the new `BorderlessTabControlTests.WndProc_WhenHideBorderAndEraseBackground_SuppressesNativeErase` test creating and showing a plain top-level `Form` just to parent the tab control. That host window matched the blank desktop form the user saw during test runs.
- Updated the test to create the tab control handle directly, keep the `WM_ERASEBKGND` regression coverage, and assert `Application.OpenForms.Count == 0` so the test can no longer leak a visible host form.
- Verification passed with:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\borderless-tab-test-host-fix\bin\ -p:BaseIntermediateOutputPath=artifacts\borderless-tab-test-host-fix\obj\` (`2/2`)
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BorderlessTabControlTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\borderless-tab-test-host-ui-focused\bin\ -p:BaseIntermediateOutputPath=artifacts\borderless-tab-test-host-ui-focused\obj\` (`18/18`)
- Existing `MSB3277` and `xUnit1031` warnings remain unchanged.

## 119. Remove presets-tab flicker during callback focus restoration
- [ ] 119.1 Add focused failing tests proving the custom presets tab control uses buffered painting and suppresses native background erase while border-hiding is active.
- [ ] 119.2 Implement the minimal `BorderlessTabControl` paint-path changes needed to reduce activation flicker without changing preset behavior or callback focus logic.
- [ ] 119.3 Run focused UI/browser-callback verification plus a build, then capture the review outcome below.

### 119 Review
- Pending implementation.

## 118. Restore main app focus when closing keep-open callback windows
- [x] 118.1 Reproduce the remaining multi-callback focus issue in the modeless WebView2 close path and identify the missing owner-restore step.
- [x] 118.2 Add a focused failing test proving closing a keep-open callback window requests activation of the SSH Helper main form.
- [x] 118.3 Implement the minimal close-path focus restore and rerun focused plus regression browser-callback verification.

### 118 Review
- Root cause was the modeless keep-open WebView2 close path never explicitly reactivating SSH Helper. Once the last callback window closed, Windows was free to leave focus on whatever app had most recently been active instead of the main form.
- Added a focused host-level regression test in `BrowserCallbackUiHostTests` that opens two keep-open callback windows, closes one, and asserts the close path requests activation for the main SSH Helper form rather than falling through to another window.
- Added a small test seam to `BrowserCallbackFocusRestorer` so WinForms tests can observe scheduled activation attempts without relying on native foreground behavior, then used that seam from the new host test.
- `BrowserCallbackUiHost` now calls the existing focus-restorer from the modeless dialog `FormClosed` path whenever a keep-open callback window is dismissed and the main owner form is still valid.
- Verification passed with:
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ClosingKeepOpenBrowserCallbackWindow_RequestsMainFormActivation" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-focus-restore-green1\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-focus-restore-green1\obj\` (`1/1`)
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-focus-restore-focused-serial2\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-focus-restore-focused-serial2\obj\` (`8/8`)
- `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallback|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-focus-restore-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-focus-restore-regression\obj\` (`89/89`)
- `dotnet build .\SSH_Helper.sln -nologo`
- The build still reports the existing `MSB3277` WebView2/`WindowsBase` warnings and the existing five `xUnit1031` warnings in `ExpressionParserTests.cs`.
- I did not run a manual interactive UI smoke test from the CLI; the remaining validation is to rerun the two-callback preset in SSH Helper and confirm both window closes restore focus to the main app.

## 117. Remove embedded browser close flicker in keep-open WebView2 flows
- [x] 117.1 Identify the smallest WebView2 host/session change that avoids flashing the owner window when a completed callback window is closed.
- [x] 117.2 Add focused failing tests for the revised keep-open window lifecycle and close behavior.
- [x] 117.3 Implement the minimal callback UI host/dialog changes needed to satisfy the tests without regressing cancel/timeout behavior.
- [x] 117.4 Run focused browser-callback tests, a regression slice, and a build, then capture the review outcome below.

### 117 Review
- Root cause was the keep-open WebView2 path entering a modal `ShowDialog(owner)` loop. When the operator later closed that completed callback window, WinForms necessarily reactivated the owner form, which produced the visible flash. Owner selection also preferred `Form.ActiveForm`, so a still-open callback window could become the owner for a later callback step.
- Added focused regression coverage in `BrowserCallbackUiHostTests` for two contracts: keep-open WebView2 sessions are shown modeless instead of modal, and owner resolution ignores already-open callback windows in favor of the main application form. Extended `BrowserCallbackCaptureCommandTests` so the command must pass the new keep-open launch hint through to the UI host.
- Extended `BrowserCallbackUiLaunchRequest` with a `KeepWindowOpenOnSuccess` hint and threaded it from `BrowserCallbackCaptureCommand` based on `auto_close_browser`. The WebView2 host now keeps the existing modal behavior for normal auto-close flows, but uses a modeless show path for keep-open flows and temporarily disables the owner form only while the callback is still pending. Once the callback completes, the owner is re-enabled and the completed browser window can be closed without dropping out of a modal dialog loop.
- Added a lightweight callback-window marker so owner resolution skips active callback windows and attaches new callback windows to the main app form instead of chaining callback dialogs together.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackUiHostTests|FullyQualifiedName~ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-flicker-green3\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-flicker-green3\obj\` passed (4/4).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallback|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-flicker-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-flicker-regression\obj\` passed (88/88).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with the existing `MSB3277` `WindowsBase` conflict warnings from the WebView2 package and the existing five `xUnit1031` warnings in `ExpressionParserTests.cs`.
- Manual interactive verification was not run from this CLI session. The remaining live check is to rerun your keep-open WebView2 preset in the app and confirm the completed callback window now closes without the old owner-window flash.

## 116. Investigate embedded browser close flicker between callback steps
- [x] 116.1 Confirm the live sample preset sequence and whether it opens one or multiple callback windows.
- [x] 116.2 Trace the WebView2 callback dialog lifecycle, including owner resolution, modal show, completion state, and close behavior.
- [x] 116.3 Document the root cause, expected WinForms behavior, and any practical mitigation options in the review section below.

### 116 Review
- Confirmed the shipped self-contained preset bundle contains two sequential `browser_callback_capture` steps, one query and one fragment, so the sample flow can show more than one callback window during a single run.
- In `browser_mode: webview2`, the "browser" is not an external browser process. SSH Helper creates a `BrowserCallbackWebViewDialog`, centers it on the owner, hides it from the taskbar, and shows it modally via `_dialog.ShowDialog(_owner)`.
- The dialog owner is chosen by `ResolveOwnerForm()`, which prefers `Form.ActiveForm`. That means the active SSH Helper window, or even a previous callback dialog if one is still open, becomes the owner for the next callback window.
- When `auto_close_browser: false`, `BrowserCallbackCaptureCommand` marks the dialog completed and intentionally keeps the WebView2 session open after the callback succeeds instead of disposing it. The script executor then continues immediately to later steps while that modal dialog is still on screen.
- Because the callback window is an owned modal WinForms dialog, closing it necessarily returns activation to its owner before anything else happens. That is the brief flash of the underlying SSH Helper window the user is seeing; it is normal for the current `ShowDialog(owner)` implementation rather than an external-browser close path.
- In multi-step keep-open flows, there is an extra visual handoff: after one callback dialog closes, WinForms reactivates the owner, and a later callback step may then create/show a separate modal dialog. That produces a visible transition between windows instead of a seamless swap.
- No production code was changed for this investigation. Verification was source inspection of `BrowserCallbackUiHost`, `BrowserCallbackCaptureCommand`, `ScriptExecutor`, and the shipped sample preset bundle; no live UI smoke test was run from this CLI session.

## 115. Fix embedded callback completion affordances and dark mode rendering
- [x] 115.1 Add focused failing tests for the embedded browser callback dialog completion state and the callback HTML theme styling.
- [x] 115.2 Update the embedded WebView2 session/dialog so a successful keep-open callback changes the footer action from `Cancel` to `Close` with matching instruction text.
- [x] 115.3 Update browser callback completion/bridge HTML to render correctly in dark mode instead of relying on default text/background colors.
- [x] 115.4 Run focused tests, the browser-callback regression slice, and a solution build, then capture the review outcome below.

### 115 Review
- Root cause was split across two layers. The embedded WebView2 dialog never received a success-state transition, so its footer button remained the static `Cancel` control and the header text still described closing the window as cancellation even after the callback had already completed. Separately, the generated callback HTML had no explicit theme CSS, so dark mode depended on browser defaults and could render black text against the dark WebView background.
- Added `BrowserCallbackWebViewDialogTests` to lock the dialog completion-state contract and added `ExecuteAsync_QueryCapture_ReturnsThemeAwareHtmlResponse` so callback success HTML must ship with explicit light/dark styling.
- Extended `IBrowserCallbackUiSession` with `MarkCompletedAsync()` and updated the WebView2 session to forward successful keep-open completion into the dialog. The dialog now changes its title, instructions, and footer button text from `Cancel` to `Close` after success instead of staying in the pre-completion state.
- Updated the callback completion and fragment bridge pages to emit explicit CSS with `color-scheme: light dark` plus dark-mode background/foreground rules, so the success content renders legibly inside the embedded browser and in external browsers that honor `prefers-color-scheme`.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackWebViewDialogTests|FullyQualifiedName~ExecuteAsync_QueryCapture_ReturnsThemeAwareHtmlResponse" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-ui-theme-red\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-ui-theme-red\obj\` failed first because the dialog had no completion-state method and the HTML did not contain theme-aware styling.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackWebViewDialogTests|FullyQualifiedName~ExecuteAsync_QueryCapture_ReturnsThemeAwareHtmlResponse|FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-ui-theme-green\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-ui-theme-green\obj\` passed (3/3).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallback|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-ui-theme-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-ui-theme-regression\obj\` passed (86/86).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with the existing `MSB3277` `WindowsBase` conflict warnings from the WebView2 package and the existing five `xUnit1031` warnings in `ExpressionParserTests.cs`.
- Manual WinForms/WebView2 smoke testing was not run from this CLI session, so the remaining live check is to rerun your preset once and confirm the footer now says `Close` after success and the page text renders correctly in dark mode.

## 114. Keep embedded browser open when auto-close is disabled
- [x] 114.1 Update the active `add-browser-callback-webview2-mode` OpenSpec change so `auto_close_browser: false` keeps the successful embedded WebView2 callback surface open instead of only changing page HTML.
- [x] 114.2 Add a focused failing WebView2 runtime test proving the embedded callback session is not closed after success when `auto_close_browser: false`.
- [x] 114.3 Update `BrowserCallbackCaptureCommand` session-lifecycle handling so successful `browser_mode: webview2` steps only auto-close when `auto_close_browser` is `true`, while failure/timeout/cancel cleanup stays intact.
- [x] 114.4 Update `SCRIPTING.md`, rerun focused/regression/build/spec verification, and capture the review outcome below.

### 114 Review
- Corrected the `auto_close_browser` contract so successful visible WebView2 callback windows now stay open for inspection when `auto_close_browser: false`, instead of only omitting `window.close()` from the callback page HTML.
- Added focused WebView2 runtime coverage for two distinct behaviors: a shown embedded callback window stays open after success when auto-close is disabled, and a delayed hidden session that never became visible still gets disposed instead of lingering invisibly.
- Updated `BrowserCallbackCaptureCommand` to keep the embedded UI session alive only after a successful WebView2 callback when `auto_close_browser: false` and the dialog was actually shown to the operator; timeout, cancellation, validation failure, and never-shown delayed sessions still clean up through the existing dispose path.
- Extended the browser-callback UI session contract with `WasShownToUser` so the command can distinguish a visible embedded window from a hidden delayed session.
- Updated the active OpenSpec change and `SCRIPTING.md` so the documented behavior now matches the runtime, including the hidden delayed-session cleanup rule.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-webview2-keep-open-red\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-webview2-keep-open-red\obj\` failed first because the embedded session was still auto-closing (`CloseCallCount` was `1`).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-webview2-keep-open-red2\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-webview2-keep-open-red2\obj\` failed during the first pass because the hidden delayed session was not being disposed (`DisposeCallCount` was `0`).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-webview2-keep-open-green\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-webview2-keep-open-green\obj\` passed (2/2).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~BrowserCallbackCaptureCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-webview2-keep-open-focused\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-webview2-keep-open-focused\obj\` passed (29/29).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallback|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-webview2-keep-open-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-webview2-keep-open-regression\obj\` passed (84/84).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with the existing `MSB3277` `WindowsBase` conflict warnings from the WebView2 package and the existing five `xUnit1031` warnings in `ExpressionParserTests.cs`.
- Verification: `openspec validate add-browser-callback-webview2-mode --strict --no-interactive` passed.
- Manual WinForms/WebView2 smoke testing was not run from this CLI session, so the remaining live check is to rerun your preset once and confirm the visible embedded callback window now stays open when `auto_close_browser: false`.

## 113. Add browser callback auto-close toggle
- [x] 113.1 Update the active `add-browser-callback-webview2-mode` OpenSpec change to cover a per-step `auto_close_browser` option and completion-page close behavior.
- [x] 113.2 Add focused failing parser and completion-page tests for `auto_close_browser`, including query and fragment success pages that stay open.
- [x] 113.3 Extend `browser_callback_capture` parsing/validation and runtime HTML generation to carry `auto_close_browser` with a default of `true`.
- [x] 113.4 Update `SCRIPTING.md`, run focused/regression/build/spec verification, and capture the review outcome below.

### 113 Review
- Extended the active OpenSpec change `add-browser-callback-webview2-mode` so the browser callback contract now includes a per-step `auto_close_browser` toggle for successful callback pages.
- Added `BrowserCallbackCaptureOptions.AutoCloseBrowser`, parser support for `auto_close_browser`, and the documented default of `true`.
- Updated query and fragment completion-page HTML generation in `BrowserCallbackCaptureCommand` so `auto_close_browser: false` omits `window.close()` while preserving the existing `/complete` acknowledgement flow and keeping the owned WebView2 dialog close behavior unchanged.
- Added focused parser/runtime coverage for explicit `auto_close_browser: false`, default `true`, query-mode stay-open completion pages, and fragment-mode stay-open bridge pages.
- Updated `SCRIPTING.md` to document `auto_close_browser` and clarify that it only affects the callback page's self-close behavior, not the WebView2 dialog lifecycle.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~BrowserCallbackCaptureCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-auto-close-red\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-auto-close-red\obj\` failed first because `BrowserCallbackCaptureOptions` did not yet define `AutoCloseBrowser`.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~BrowserCallbackCaptureCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-auto-close-green1\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-auto-close-green1\obj\` passed (27/27).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallback|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-auto-close-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-auto-close-regression\obj\` passed (82/82).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with the existing `MSB3277` `WindowsBase` conflict warnings from the WebView2 package and the existing five `xUnit1031` warnings in `ExpressionParserTests.cs`.
- Verification: `openspec validate add-browser-callback-webview2-mode --strict --no-interactive` passed.
- Manual interactive browser smoke testing was not run from this CLI session, so the new stay-open completion-page behavior should still be click-tested once in the app.

## 112. Add delayed WebView2 reveal for browser callbacks
- [x] 112.1 Update the active `add-browser-callback-webview2-mode` OpenSpec change to cover a per-step `show_after_seconds` option and hidden-until-slow WebView2 behavior.
- [x] 112.2 Add focused failing parser and runtime tests for `show_after_seconds`, including "completes before reveal" and "reveals after delay" behavior.
- [x] 112.3 Extend `browser_callback_capture` parsing/validation and runtime launch plumbing to carry `show_after_seconds` into the WebView2 UI host.
- [x] 112.4 Implement delayed WebView2 dialog reveal so `show_after_seconds: 0` keeps the current immediate popup, values above zero stay hidden until the timeout elapses, and early callback completion never shows the popup.
- [x] 112.5 Update `SCRIPTING.md`, run focused/regression/build/spec verification, and capture the review outcome below.

### 112 Review
- Extended the active OpenSpec change `add-browser-callback-webview2-mode` so the script contract and runtime behavior now cover per-step `show_after_seconds` delayed reveal semantics for WebView2 browser callback steps.
- Added `BrowserCallbackCaptureOptions.ShowAfterSeconds`, parser support for `show_after_seconds`, and validation that rejects negative values with line-specific `browser_callback_capture.show_after_seconds` errors.
- Threaded `show_after_seconds` through `BrowserCallbackCaptureCommand` and `BrowserCallbackUiLaunchRequest`, then updated the WebView2 dialog session so delayed steps initialize the embedded browser immediately but only call `ShowDialog(...)` after the configured delay if the callback is still pending.
- Added focused parser/runtime coverage for three new contracts: parsing/defaulting `show_after_seconds`, early callback completion before reveal, and delayed reveal when the callback remains pending. Also hardened the hidden-start runtime by forcing WinForms/WebView2 handle creation before initializing the control.
- Updated `SCRIPTING.md` to document `show_after_seconds`, its default of `0`, and that it only changes behavior in `browser_mode: webview2`.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~BrowserCallbackCaptureCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-show-after-red\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-show-after-red\obj\` failed first because `BrowserCallbackCaptureOptions` and `BrowserCallbackUiLaunchRequest` did not yet define `ShowAfterSeconds`.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~BrowserCallbackCaptureCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-show-after-focused\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-show-after-focused\obj\` passed (25/25).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallback|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-show-after-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-show-after-regression\obj\` passed (80/80).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with the existing `MSB3277` `WindowsBase` conflict warnings from the WebView2 package and the existing five `xUnit1031` warnings in `ExpressionParserTests.cs`.
- Verification: `openspec validate add-browser-callback-webview2-mode --strict --no-interactive` passed.
- Manual WinForms/WebView2 click-through was not run from this CLI session, so the new hidden-then-reveal behavior should still be smoke-tested once in the app.

## 111. Investigate WebView2 preset still launching external browser
- [x] 111.1 Inspect the live saved preset text and confirm whether every `browser_callback_capture` step actually sets `browser_mode: webview2`.
- [x] 111.2 Trace the runtime launch-selection path to confirm whether the code still routes `browser_mode: webview2` steps to the external-browser host.
- [x] 111.3 Capture the root cause and the exact corrective action for the user below.

### 111 Review
- The runtime launch-selection code is honoring `browser_mode` correctly: `BrowserCallbackCaptureCommand` parses `BrowserMode`, resolves `webview2` to `BrowserCallbackUiMode.WebView2`, and only falls back to the external host when the option is omitted or resolves to `external`.
- The saved live preset in `%LocalAppData%\\SSH_Helper\\config.json` is the mismatch. The preset named `Browser Callback Self-Contained Demo webview2` contains `browser_mode: webview2` on the first `browser_callback_capture` step only.
- The second `browser_callback_capture` step in that same saved preset omits `browser_mode`, so it defaults to the external browser by design. That matches the user's observed behavior exactly.
- No production code change was required for this report. The corrective action is to add `browser_mode: webview2` to every callback step that should stay inside SSH Helper, or create/import a dedicated all-WebView2 sample preset.

## 110. Add WebView2 browser callback mode
- [x] 110.1 Add the OpenSpec change `add-browser-callback-webview2-mode` with proposal, tasks, and spec deltas for the new script option plus embedded-browser runtime/profile reset behavior.
- [x] 110.2 Add focused failing tests for `browser_callback_capture browser_mode`, WebView2 launch/cancel behavior, and the settings clear-data action before changing production code.
- [x] 110.3 Add the WebView2 package/runtime plumbing, including static loader preference and single-file publish compatibility with the installed Evergreen runtime.
- [x] 110.4 Implement the browser-callback UI host abstraction plus `browser_mode: webview2` support, keeping `open_browser=false` as manual mode and external browser as the backward-compatible default.
- [x] 110.5 Add a shared WebView2 profile manager, persistent app-owned user-data folder, active-session tracking, and full profile reset behavior.
- [x] 110.6 Add the Settings UI action to clear embedded browser data with explicit confirmation and active-session blocking.
- [x] 110.7 Update `SCRIPTING.md`, add any necessary sample coverage, run focused verification plus build/publish checks, and capture the review outcome below.

### 110 Review
- Added OpenSpec change `add-browser-callback-webview2-mode` with validated proposal, tasks, design notes, and delta specs for `scripting-network-steps` plus `scripting-runtime`.
- Extended `browser_callback_capture` with `browser_mode`, parser normalization/validation, and the documented precedence that keeps `open_browser=false` in manual mode while defaulting omitted browser mode to the external browser path.
- Introduced a browser-callback UI host seam plus a shared WebView2 profile manager. `BrowserCallbackCaptureCommand` now selects between external browser launch and an owned modal WebView2 dialog, fails cleanly if the embedded dialog is closed early, and only uses the focus restorer for external-browser runs.
- Added a Settings action to clear embedded browser data with explicit confirmation text and active-session blocking, backed by the shared profile manager.
- Updated `SCRIPTING.md` to document `browser_mode`, `open_browser` precedence, WebView2 behavior, persistent embedded browser data, and the settings-based reset path.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~BrowserCallbackCaptureCommandTests|FullyQualifiedName~BrowserCallbackWebViewProfileManagerTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-webview2-red\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-webview2-red\obj\` failed first because the new WebView2 host/profile/settings seam types did not exist yet.
- Verification: the same focused command/parser/settings slice passed after implementation (26/26).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallback|FullyQualifiedName~NetworkStepParserTests|FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~SettingsDialogBrowserCallbackTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-webview2-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-webview2-regression\obj\` passed (78/78).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with 0 errors. The build emitted the existing 5 xUnit analyzer warnings in `ExpressionParserTests.cs` plus new `MSB3277` `WindowsBase` conflict warnings from the `Microsoft.Web.WebView2` package's unused WPF reference path.
- Verification: `dotnet publish .\SSH_Helper.csproj -c Release -nologo -o artifacts\browser-callback-webview2-publish` passed. `artifacts\browser-callback-webview2-publish\WebView2Loader.dll`, `artifacts\browser-callback-webview2-publish\runtimes\win-x64\native\WebView2Loader.dll`, and `artifacts\browser-callback-webview2-publish\Microsoft.Web.WebView2.Wpf.dll` were all absent.
- Verification: `openspec validate add-browser-callback-webview2-mode --strict --no-interactive` passed.
- Manual interactive browser smoke testing was not run from this CLI session, so embedded WebView2 navigation/focus return should still be click-tested in the app.

## 109. Fix browser callback focus-restorer native imports
- [x] 109.1 Trace the JIT crash and confirm that the focus-restorer P/Invokes are targeting non-existent `Native...` exports instead of the real Windows entry-point names.
- [x] 109.2 Add a focused failing test that the focus-restorer native imports map to the real `user32.dll` and `kernel32.dll` export names.
- [x] 109.3 Correct the `DllImport` declarations so the focus-restorer uses the intended Windows entry points without changing its activation behavior.
- [x] 109.4 Run focused verification plus a build, then capture the review outcome below.

### 109 Review
- Root cause is concrete: `NativeMethodsAdapter` uses wrapper method names like `NativeIsIconic`, but the `DllImport` declarations did not specify `EntryPoint`, so the CLR tried to resolve exports like `NativeIsIconic` in `user32.dll` and threw `EntryPointNotFoundException` at runtime.
- Added `NativeMethodsAdapter_ImportsUseRealWindowsEntryPoints` to `SSH_Helper.Tests\UI\BrowserCallbackFocusRestorerTests.cs` so the native import mapping is now covered by an automated test instead of being left to runtime/manual validation.
- Updated every focus-restorer import to declare the real Windows entry point explicitly: `IsIconic`, `SetForegroundWindow`, `ShowWindow`, `GetForegroundWindow`, `GetWindowThreadProcessId`, `GetCurrentThreadId`, `AttachThreadInput`, `BringWindowToTop`, and `SetFocus`.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackFocusRestorerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-native-import-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-native-import-tests\obj\` failed first because the import contract still resolved `NativeIsIconic` instead of `IsIconic`.
- Verification: the same focused test command passed after the implementation (3/3).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackSelfContainedPresetTests|FullyQualifiedName~BrowserCallbackCaptureCommandTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~NetworkStepParserTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-focus-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-focus-regression\obj\` passed (53/53).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with 0 errors. The build emitted 5 existing xUnit analyzer warnings in `ExpressionParserTests.cs` unrelated to this change.

## 108. Align focus restore with browser close completion
- [x] 108.1 Trace the callback completion path and confirm that focus restore currently fires before the browser completion page actually closes the tab.
- [x] 108.2 Add a focused failing test that successful browser callback capture waits for a browser completion acknowledgement before returning.
- [x] 108.3 Implement the minimal runtime change so query and fragment callback flows acknowledge browser completion before the command returns and requests app focus.
- [x] 108.4 Run focused verification plus a build, then capture the review outcome below.

### 108 Review
- Root cause turned out to be two separate timing gaps. Browser-driven query/fragment capture returned as soon as the payload arrived, which could request app focus before the completion page had posted its final acknowledgement and attempted to close the tab. Separately, the focus retry path relied on a local `System.Windows.Forms.Timer` and made no immediate activation attempt, so the restore sequence itself could be delayed or dropped.
- Updated `BrowserCallbackCaptureCommand` so browser GET/fragment flows now hold captured values in a pending payload and return only after the browser completion page POSTs `/complete`. The completion/capture HTML now sends that acknowledgement before `window.close()`.
- Updated `BrowserCallbackFocusRestorer` so it prefers the app's active form when available, performs an immediate `TryActivateForm(...)` before any delayed retries, and runs the retry loop through an async delay path instead of a local WinForms timer.
- Updated `BrowserCallbackCaptureCommandTests` to model the new browser completion acknowledgement sequence and added focused regression coverage in `BrowserCallbackFocusRestorerTests` for the immediate activation attempt.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackCaptureCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-complete-signal-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-complete-signal-tests\obj\` failed first because browser-driven query capture no longer completed immediately and the existing query tests were still assuming the old contract.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~BrowserCallbackCaptureCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-focus-handshake-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-focus-handshake-tests\obj\` failed first because `ScheduleUiActivationAttempts(...)` did not attempt activation immediately.
- Verification: the same browser-callback/focus handshake command passed after the implementation (8/8).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackSelfContainedPresetTests|FullyQualifiedName~BrowserCallbackCaptureCommandTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~NetworkStepParserTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-focus-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-focus-regression\obj\` passed (52/52).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with 0 errors. The build emitted 5 existing xUnit analyzer warnings in `ExpressionParserTests.cs` unrelated to this change.
- Residual risk: foreground activation is still best-effort under Windows/browser policy, but the app now waits for browser completion before restoring focus and the retry mechanism no longer depends on an unrooted local timer.

## 107. Restore app focus after query-mode browser callbacks
- [x] 107.1 Re-check the self-contained browser-callback flow and confirm whether query-mode capture leaves the browser foreground because it returns a plain text completion page instead of the auto-close HTML path.
- [x] 107.2 Add a focused failing test that expects successful query-mode callback capture to return an HTML completion page that attempts to close the browser tab.
- [x] 107.3 Implement the minimal runtime change so query-mode callbacks use the same auto-close completion-page behavior and align the focus restore timing with that page.
- [x] 107.4 Run focused verification plus a build, then capture the review outcome below.

### 107 Review
- Root cause in the self-contained preset flow is concrete and code-level: `capture_mode: query` returned a plain text success page (`"Callback captured. You may close this tab."`) and never attempted `window.close()`. The demo preset hits query mode first, so the browser was expected to remain foreground unless the OS focus restore won on its own.
- Updated `BrowserCallbackCaptureCommand` so successful query-mode callbacks now return an HTML completion page with the same auto-close attempt pattern used elsewhere, instead of a plain text page.
- Also widened the browser-callback focus retry schedule in `BrowserCallbackFocusRestorer` so the activation attempts continue after the completion page has had time to render and attempt to close.
- Added `ExecuteAsync_QueryCapture_ReturnsAutoCloseHtmlResponse` to `SSH_Helper.Tests\Scripting\BrowserCallbackCaptureCommandTests.cs` to lock the regression: successful query-mode capture must now return a page containing `window.close()`.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackCaptureCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-query-html-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-query-html-tests\obj\` failed first because the query response body was still plain text.
- Verification: the same focused test command passed after the implementation (6/6).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackCaptureCommandTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~NetworkStepParserTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-query-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-query-regression\obj\` passed (50/50).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with 0 errors. The build emitted 5 existing xUnit analyzer warnings in `ExpressionParserTests.cs` unrelated to this change.
- Residual risk: browser auto-close remains best-effort because modern browsers may still block `window.close()` on tabs they did not open via script. If that happens, the longer focus retry path is still in effect, but foreground return is not fully guaranteed by Windows or the browser.

## 106. Improve browser callback focus restoration
- [x] 106.1 Trace the existing browser callback focus-restore path and confirm whether the failure is in our code or Windows foreground-lock policy.
- [x] 106.2 Add a focused failing test for the activation strategy needed when another foreground window owns the input queue.
- [x] 106.3 Implement the minimal fix so browser callback restore can attach to the foreground thread and make SSH Helper active more reliably.
- [x] 106.4 Run focused verification plus a build, then capture the review outcome below.

### 106 Review
- Root cause is Windows foreground-lock behavior, not the absence of a restore attempt. The old browser callback path already tried `TopMost`, `BringToFront`, `Activate`, and `SetForegroundWindow`, but when the browser still owned the active input queue Windows could deny the foreground switch and flash the taskbar instead.
- Added `Services\Scripting\Commands\BrowserCallbackFocusRestorer.cs` to own browser-callback focus restoration and to make the native-window activation sequence testable.
- The new activation path now detects the current foreground window, temporarily calls `AttachThreadInput(...)` when that window belongs to another thread, and then uses `BringWindowToTop(...)`, `SetForegroundWindow(...)`, and `SetFocus(...)` around the existing WinForms activation steps before detaching again.
- `BrowserCallbackCaptureCommand` now delegates to the shared focus restorer instead of carrying its own private restore logic.
- Added `SSH_Helper.Tests\UI\BrowserCallbackFocusRestorerTests.cs` to lock the regression: when another foreground thread is active, the restore path must attach/detach input queues around the foreground switch.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackFocusRestorerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-focus-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-focus-tests\obj\` failed first with `CS0246` because the new focus-restorer seam did not exist yet.
- Verification: the same focused test command passed after the implementation (1/1).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackCaptureCommandTests|FullyQualifiedName~BrowserCallbackFocusRestorerTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~NetworkStepParserTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-focus-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-focus-regression\obj\` passed (49/49).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with 0 errors. The build emitted 5 existing xUnit analyzer warnings in `ExpressionParserTests.cs` unrelated to this change.
- Residual risk: Windows can still refuse foreground activation in some policy or shell scenarios; this change makes activation materially more reliable, but it cannot guarantee focus in every OS state.

## 105. Build browser callback scripting test fixture
- [x] 105.1 Review the existing `browser_callback_capture` runtime, docs, QA fixture patterns, and import/export surfaces.
- [x] 105.2 Clarify the desired callback test target and success criteria with the user.
- [x] 105.3 Propose the fixture approaches, present the recommended design, and get approval before implementation.
- [x] 105.4 Implement the approved preset/environment artifacts with tests or fixture validation as appropriate.
- [x] 105.5 Run verification, capture how to import/use the fixture, and write the review outcome below.

### 105.4 Task Plan
- [x] 105.4.1 Add a failing test that expects a dedicated self-contained browser-callback preset export file to exist and validate cleanly.
- [x] 105.4.2 Run the focused test slice and confirm it fails for the missing fixture.
- [x] 105.4.3 Create a single importable preset-export JSON that exercises local query and fragment browser callback capture with no separate environment file.
- [x] 105.4.4 Re-run the focused fixture-validation tests and confirm they pass.

### 105 Review
- Added `ScriptSamples\browser_callback_self_contained_presets.json`, a single-file import bundle containing one preset: `Browser Callback Self-Contained Demo`.
- The preset is fully self-contained: it uses only script-local `vars`, no separate environment file, no SSH commands, and no external identity provider. It exercises `browser_callback_capture` twice against `127.0.0.1:38086`: once in `query` mode and once in `fragment` mode.
- The preset description now states exact prerequisites and expected outcome: one selected/current host row, single-host execution, a local browser, and an available localhost port.
- Added `SSH_Helper.Tests\Scripting\BrowserCallbackSelfContainedPresetTests.cs` to lock the fixture contract: the bundle must exist, contain exactly one preset, parse/validate cleanly, use two browser callback capture steps, require no SSH session, require no external columns/environment variables, and end with an explicit success exit.
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackSelfContainedPresetTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-self-contained-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-self-contained-tests\obj\` failed first as expected because the bundle file did not exist.
- Verification: the same focused test command passed after adding the bundle (1/1).
- Verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackSelfContainedPresetTests|FullyQualifiedName~BrowserCallbackCaptureCommandTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~NetworkStepParserTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\browser-callback-self-contained-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\browser-callback-self-contained-regression\obj\` passed (49/49).
- Verification: `dotnet build .\SSH_Helper.sln -nologo` passed with 0 errors. The build emitted 5 existing xUnit analyzer warnings in `ExpressionParserTests.cs` unrelated to this fixture.

## 104. Fix top-pane truncation under menu/tool strip
- [x] 104.1 Confirm whether runtime font scaling leaves the top chrome and section headers on stale designer-era heights.
- [x] 104.2 Reflow the menu/tool strip stack and the top section headers from current font metrics so the top panes sit below the bars and their titles no longer clip.
- [ ] 104.3 Run the affected UI path and verify the layout in the reproduced theme/font setup.

### 104 Review
- Root cause is mixed layout strategy: `ApplyFontSettings(...)` recalculates menu and tool strip heights at runtime, but the surrounding top-level content/header layout still relies on fixed measurements from the original designer layout.
- `Form1.cs` now reapplies the form's top chrome bounds after font changes so the menu strip, main tool strip, and `mainSplitContainer` stack from their actual current heights instead of the old `24 + 25 = 49` assumption.
- The top section headers now recalculate their title heights from the active fonts and resize their panels so the Hosts, Presets, and Commands titles do not get clipped when the UI font scale grows.
- Verification has not been run in this pass.

## 103. Merge callback_test without keycloak sample
- [x] 103.1 Review `origin/callback_test` and decide import scope based on the requirement to keep `SCRIPTING.md` and drop `keycloak_block_site.yaml`.
- [x] 103.2 Import browser callback capture feature work and explicitly exclude the sample keycloak block site file from the merge.
- [x] 103.3 Preserve low-risk cleanup behavior for stale capture fields in `BrowserCallbackCaptureCommand.ClearCapture(...)`.
- [x] 103.4 Add/update targeted tests for parser, dependency, preflight, and callback stale-suffix cleanup paths.
- [x] 103.5 Verify final branch state excludes `keycloak_block_site.yaml` and commit changes as one atomic merge.

### 103 Review
- Reviewed `origin/callback_test` and confirmed it is two commits: `6e82241` (feature + `SCRIPTING.md`) and `75cc4ef` (deletes `keycloak_block_site.yaml`).
- Kept the feature path by cherry-picking `6e82241` without commit and manually excluding the keycloak sample file before committing the merge.
- Added `BrowserCallbackCaptureCommand` support plus runtime/parser/dependency integrations and updated `SCRIPTING.md` in commit `4950903`.
- `ClearCapture(...)` now removes all stale `into_*` variables before writing the new callback capture result, not just `into`, `into_count`, and `into_keys`.
- Added regression coverage in `SSH_Helper.Tests/Scripting/BrowserCallbackCaptureCommandTests.cs` for stale-suffixed capture variable cleanup.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~BrowserCallbackCaptureCommandTests"` passed (5/5).
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests|FullyQualifiedName~NetworkStepParserTests"` passed (48/48).
- Verification: `Test-Path keycloak_block_site.yaml` returned `False`; `git status --short` has no `keycloak_block_site.yaml` entries.

## 102. Improve startup load time
- [x] 102.1 Inspect the startup path, identify the largest synchronous load-time costs, and agree whether to optimize first paint, fully-ready state, or a balanced target.
- [x] 102.2 Implement the chosen load-time improvements with minimal behavior change and verify they reduce synchronous startup work.
- [x] 102.3 Run focused verification, run a solution build, and capture the review outcome below.

### 102.2 Task 2 Plan
- [x] 102.2.1 Add focused `HostGridRestoreBatcher` tests for collapsed restore-scope flush behavior.
- [x] 102.2.2 Run the targeted batcher test filter and confirm the expected red failure because the helper does not exist yet.
- [x] 102.2.3 Implement `UI/HostGridRestoreBatcher.cs` with nested restore scopes and deferred single-flush requests.
- [x] 102.2.4 Wire `Form1` startup/bulk host-grid restore paths to batch scrollbar, host-count, dirty-mark, and hosts-file-indicator work without changing non-restore behavior.
- [x] 102.2.5 Re-run the targeted batcher test slice and the requested host-grid regression slice.
- [x] 102.2.6 Commit the scoped Task 2 changes.

### 102 Review
- Task 1 reused the constructor's first `AppConfiguration` snapshot across startup-sensitive paths. `PresetManager` now supports `Load(AppConfiguration config)`, `Form1.InitializeFromConfiguration(...)` and `RestoreWindowState(...)` take the startup snapshot explicitly, and startup preset-tree construction can use the supplied config instead of rereading `config.json`.
- Task 2 added `UI\\HostGridRestoreBatcher.cs` plus focused tests in `SSH_Helper.Tests\\UI\\HostGridRestoreBatcherTests.cs` to lock the batching contract: repeated requests collapse into one flush and nested restore scopes wait for the outermost dispose.
- `Form1` now routes host-grid scrollbar/count/dirty requests through the batcher, and both `RestoreApplicationState(...)` and `LoadEnvironmentIntoGrid(...)` populate the grid inside a restore scope so startup restore no longer recomputes scrollbars and host counts on every row/column mutation.
- Task 3 moved heavy scheduler bootstrap out of the constructor and onto a once-only idle continuation after `Form1_Shown` completes restore/layout work. The scheduler shell still appears immediately, while the existing `InitializeSchedulerServices()` path now runs once after startup restore and still performs job load, crash recovery, missed-run recording, timer start, and status refresh.
- After each restore scope completes, `Form1` still resets `_csvDirty` to `false`, captures the loaded snapshot, and refreshes the hosts-file indicator so restored startup state stays clean while row heights, host count text, and themed custom scrollbars settle once at the end.
- Startup measurement method: release build, one warm-up launch, then five measured launches of the app against the same local config; each run records `WindowMs` (main window appearance) and `ReadyMs` (time until the process stays under ~1% CPU for three consecutive 200 ms samples after the window appears).
- Baseline startup metrics before implementation: `WindowAvg=1021.1 ms`, `WindowMedian=1009.8 ms`, `ReadyAvg=2174.8 ms`, `ReadyMedian=2250.2 ms`.
- Post-change startup metrics after implementation: `WindowAvg=515.7 ms`, `WindowMedian=533.6 ms`, `ReadyAvg=1486.8 ms`, `ReadyMedian=1535.1 ms`.
- Measured improvement from the same method/config: `WindowAvg` improved by about `49.5%` and `ReadyAvg` improved by about `31.6%`.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetManagerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\startup-load-time-preset-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\startup-load-time-preset-tests\\obj\\` passed (49/49).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetManagerTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~ConfigurationServiceWindowStateTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\startup-load-time-config-regression\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\startup-load-time-config-regression\\obj\\` passed (55/55).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~HostGridRestoreBatcherTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\startup-load-time-grid-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\startup-load-time-grid-tests\\obj\\` passed (2/2).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~HostGridUtilitiesTests|FullyQualifiedName~ApplyFontSettingsTests|FullyQualifiedName~JobEditorDialogHostGridParityTests|FullyQualifiedName~HostGridRestoreBatcherTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\startup-load-time-grid-regression\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\startup-load-time-grid-regression\\obj\\` passed (47/47).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~SchedulerNotificationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\startup-load-time-scheduler-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\startup-load-time-scheduler-tests\\obj\\` passed (87/87).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetManagerTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~ConfigurationServiceWindowStateTests|FullyQualifiedName~HostGridUtilitiesTests|FullyQualifiedName~ApplyFontSettingsTests|FullyQualifiedName~JobEditorDialogHostGridParityTests|FullyQualifiedName~HostGridRestoreBatcherTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~SchedulerNotificationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\startup-load-time-final-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\startup-load-time-final-tests\\obj\\` passed (189/189).
- Verification: `dotnet build SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\\startup-load-time-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\startup-load-time-build\\obj\\` passed with 0 warnings and 0 errors.

## 101. Eliminate delete flicker with in-place tree mutation
- [x] 101.1 Re-check why the refresh-based viewport preservation still leaves the presets tree in a bad scroll state after delete.
- [x] 101.2 Replace the normal preset delete path with an in-place tree node removal so deleting one preset does not rebuild the whole presets tree.
- [x] 101.3 Add focused WinForms coverage, rerun verification, and capture the review outcome below.

### 101 Review
- The remaining problem was architectural rather than cosmetic: even with `TopNode` restoration, deleting one preset still went through `RefreshPresetList()`, which clears and rebuilds the entire presets tree. That full rebuild let WinForms re-normalize scroll/selection state in ways that still produced bad live behavior.
- Replaced the normal unfiltered delete path in `Form1.DeletePreset(...)` with an in-place tree mutation. After the preset is removed from storage, `UI\\PresetTreeDeleteMutation.cs` removes just that `TreeNode`, selects the already-computed replacement node, and restores the viewport against the existing tree instead of rebuilding every node.
- The old full refresh path is still used as a fallback for filtered cases where a delete can change which folders should remain visible, but the standard delete flow now avoids the expensive/repaint-heavy tree rebuild that was causing the flicker.
- Added `SSH_Helper.Tests\\UI\\PresetTreeDeleteMutationTests.cs` to verify the in-place delete path removes the selected node, keeps the viewport away from the first row, and leaves the replacement selection visible.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetDeletionSelectionResolverTests|FullyQualifiedName~PresetTreeDisplayOrderBuilderTests|FullyQualifiedName~PresetTreeSelectionGuardTests|FullyQualifiedName~PresetTreeViewportRestorerTests|FullyQualifiedName~PresetTreeDeleteMutationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\preset-delete-selection-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\preset-delete-selection-tests\\obj\\` passed (12/12).
- Verification: `dotnet build SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\\preset-delete-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\preset-delete-build\\obj\\` passed with 0 warnings and 0 errors.
- Verification note: a normal `dotnet build SSH_Helper.sln -nologo` attempt was blocked by a running `SSH_Helper.exe` process holding `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` open.

## 100. Preserve preset tree viewport on delete
- [x] 100.1 Trace the delete flicker and confirm whether `RefreshPresetList()` is resetting the presets tree viewport before the replacement selection is applied.
- [x] 100.2 Preserve the presets tree top-node anchor during delete refresh so the tree does not visibly jump to the top and then back down.
- [x] 100.3 Add focused coverage for top-node restoration, rerun verification, and capture the review outcome below.

### 100 Review
- Root cause was the refresh sequence in `Form1.DeletePreset(...)`: `RefreshPresetList()` cleared and rebuilt `trvPresets`, which reset the viewport to the top, and only after that did the replacement preset get reselected. That produced the visible jump-to-top/jump-back flicker.
- Extended `RefreshPresetList(...)` so callers can provide a replacement selection and a `TopNode` anchor. The method now reapplies selection and restores the tree viewport while `BeginUpdate()` is still active, before `EndUpdate()` allows redraw.
- Added `UI\\PresetTreeViewportRestorer.cs` to snapshot/resolve preset tree node tags across a rebuild and to restore the top node with fallback logic. `ExpandCollapseFolderSubtree(...)` now uses the same shared helper, so the tree keeps one viewport-restoration path.
- `DeletePreset(...)` now captures the current `TopNode`, passes the adjacent replacement preset as the refresh-time selection override, and lets `RefreshPresetList(...)` rebuild the tree without exposing the intermediate scroll reset.
- Added `SSH_Helper.Tests\\UI\\PresetTreeViewportRestorerTests.cs` covering both the direct top-node restore path and the preferred-missing fallback resolution used after a delete.
- Verification: `dotnet build SSH_Helper.sln -nologo` passed with 0 warnings and 0 errors.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetDeletionSelectionResolverTests|FullyQualifiedName~PresetTreeDisplayOrderBuilderTests|FullyQualifiedName~PresetTreeSelectionGuardTests|FullyQualifiedName~PresetTreeViewportRestorerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\preset-delete-selection-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\preset-delete-selection-tests\\obj\\` passed (11/11).

## 99. Fix off-screen delete reselection
- [x] 99.1 Trace why the corrected adjacent-preset target still fails after delete and confirm whether the later selection guard is blocking off-screen root nodes.
- [x] 99.2 Replace the `IsVisible`-based no-scroll selection guard with one that preserves expansion state without rejecting logically visible nodes.
- [x] 99.3 Add focused WinForms regression coverage, rerun verification, and capture the review outcome below.

### 99 Review
- The remaining failure was after target resolution, not during it. `DeletePreset(...)` had the right adjacent preset name, but `SelectPresetByName(..., ensureVisible: false)` still refused to select that node whenever `targetNode.IsVisible` was false.
- In WinForms, `TreeNode.IsVisible` is a viewport check, so off-screen root presets fail that test even though selecting them would not expand any folders. That caused the reselection to silently abort and fall through to unrelated fallback behavior.
- Added `UI\\PresetTreeSelectionGuard.cs` and updated both `SelectPresetByName(...)` and `SelectFolderByName(...)` to allow no-scroll selection whenever all ancestors are already expanded. Collapsed descendants are still blocked, so state-preserving flows do not auto-expand folders.
- Added `SSH_Helper.Tests\\UI\\PresetTreeSelectionGuardTests.cs` covering the exact missed case: a root-level node in an unshown/off-screen tree must still be selectable, while a child under a collapsed folder must remain blocked.
- Verification: `dotnet build SSH_Helper.sln -nologo` passed with 0 warnings and 0 errors.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetDeletionSelectionResolverTests|FullyQualifiedName~PresetTreeDisplayOrderBuilderTests|FullyQualifiedName~PresetTreeSelectionGuardTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\preset-delete-selection-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\preset-delete-selection-tests\\obj\\` passed (9/9).

## 98. Fix root-level preset delete selection
- [x] 98.1 Re-check the root-level preset delete flow after the failed first patch and confirm the real WinForms tree-order bug.
- [x] 98.2 Replace the delete-selection traversal so it uses logical display order instead of viewport visibility.
- [x] 98.3 Add regression coverage for root-level tree ordering, rerun focused verification, and capture the review outcome below.

### 98 Review
- Root cause in the failed first patch was the use of `TreeNode.IsVisible` inside the preset-delete traversal. In WinForms that flag is viewport-dependent, not a reliable representation of the tree's logical display order, so root-level predecessors could be skipped.
- Replaced the inline traversal in `Form1` with `UI\\PresetTreeDisplayOrderBuilder.cs`, which walks the tree in display order, always includes root nodes, and descends only into expanded folders.
- `Form1.DeletePreset(...)` now resolves the adjacent preset from that display-order snapshot, so deleting a root-level preset chooses the preceding root-level preset when one exists.
- Added `SSH_Helper.Tests\\UI\\PresetTreeDisplayOrderBuilderTests.cs` to lock the missing case: an unshown tree with root presets must still preserve root order for delete selection, plus a branch-order test proving collapsed folders do not leak hidden children into the order.
- Verification: `dotnet build SSH_Helper.sln -nologo` passed with 0 warnings and 0 errors.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetDeletionSelectionResolverTests|FullyQualifiedName~PresetTreeDisplayOrderBuilderTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\preset-delete-selection-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\preset-delete-selection-tests\\obj\\` passed (6/6).

## 97. Select previous preset after delete
- [x] 97.1 Trace the preset delete selection rule in `Form1` and lock the intended behavior: choose the preset directly before the deleted preset when one exists.
- [x] 97.2 Patch the delete-selection logic to skip folder headers and fall back to the next preset only when there is no previous preset.
- [x] 97.3 Add focused regression coverage for the delete-selection rule, run targeted verification, and capture the review outcome below.

### 97 Review
- Root cause was in `Form1.DeletePreset(...)`: it asked `GetSelectionTargetAboveDeletedPreset(...)` for `PrevVisibleNode ?? NextVisibleNode`, which meant a folder header could win simply because it was the nearest visible tree node above the deleted preset.
- Replaced that rule with a visible-preset-only resolver. `Form1` now snapshots the current tree's visible nodes, skips folder tags, and selects the previous preset in display order; only when there is no previous preset does it fall back to the next preset.
- Tightened the final fallback in `DeletePreset(...)` to use the first visible preset in the rebuilt presets tree instead of the first dictionary key from `_presetManager.Presets`, which keeps fallback behavior aligned with the on-screen ordering.
- Added `UI\\PresetDeletionSelectionResolver.cs` plus focused tests in `SSH_Helper.Tests\\UI\\PresetDeletionSelectionResolverTests.cs` covering the folder-header case, first-item fallback-to-next, only-item null case, and missing-preset null case.
- Verification: `dotnet build SSH_Helper.sln -nologo` passed with 0 warnings and 0 errors.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetDeletionSelectionResolverTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\preset-delete-selection-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\preset-delete-selection-tests\\obj\\` passed (4/4).

## 96. Audit changelog section since 729f4e6
- [x] 96.1 Read the `## Changes Since \`729f4e6\` (0.51.8)` section in `CHANGELOG.md` and capture its claims.
- [x] 96.2 Compare those claims to the actual commit and file history from `729f4e6..HEAD`.
- [x] 96.3 Record the review result below with any inaccuracies, omissions, or confirmation that the section is accurate.

### 96 Review
- Compared the current working-copy changelog section against the repo state since `729f4e6`, including the commit/file history and the concrete implementations in `Services`, `UI`, `Form1`, and the new test suites.
- Found two clear wording inaccuracies in the changelog: the library-import example implies a relative import path is valid even though `ScriptSubroutineRegistryBuilder` currently rejects non-absolute import paths, and the job-duplication section says duplicates get a `(Copy)` suffix even though the implementation uses lowercase `(copy)`.
- Found one lower-severity wording issue: `HistoryStartupSelectionHydration` does not restore list selection by itself; it only decides whether an already-selected history row should be hydrated into the output/hosts panes during startup.
- Patched `CHANGELOG.md` to correct those three points.
- Aside from those points, the section is broadly aligned with the implemented changes since `729f4e6`.

## 95. Center scheduler jobs window over main form
- [x] 95.1 Confirm why the scheduler dialog's `CenterParent` intent is not honored on first modeless show.
- [x] 95.2 Patch the shared modeless dialog launcher to explicitly center `CenterParent` dialogs over the owner on first show.
- [x] 95.3 Add a focused regression test for the initial centered position.
- [x] 95.4 Run focused verification and a build, then capture the review outcome below.

### 95 Review
- Root cause was the gap between `JobListDialog` and the shared launcher: `JobListDialog` already declared `StartPosition = CenterParent`, but `ModelessDialogManager.ShowOrActivate(...)` showed the form modeless with `Show(owner)` and never translated that intent into an explicit screen location. Windows therefore chose the initial position, which on this machine landed to the left of `Form1`.
- Patched `Utilities\\ModelessDialogManager.cs` so first-show modeless dialogs with `StartPosition == CenterParent` are converted to `Manual`, positioned over the owner before show, and re-centered once on `Load` to account for final layout/auto-scale size.
- Added a focused regression in `SSH_Helper.Tests\\UI\\ModelessDialogManagerTests.cs` that opens a `CenterParent` modeless dialog over a manually placed owner form and asserts the dialog lands at the centered owner-relative coordinates.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~ModelessDialogManagerTests"` passed (3/3).
- Verification: `dotnet build SSH_Helper.sln -nologo` passed with 0 warnings and 0 errors.

## 94. Normalize modal popup ownership
- [x] 94.1 Audit `Form1` and modal child dialogs for ownerless modal message/file/color popups and separate intentional startup/global exceptions.
- [x] 94.2 Patch visible-form modal popup call sites to use the immediate launching form as owner.
- [x] 94.3 Add a short ownership rule note near the shared dialog helper.
- [x] 94.4 Run verification searches and a solution build, then capture the review outcome below.

### 94 Review
- Patched modal popup ownership across the visible-form call sites in `Form1`, `SettingsDialog`, `JobEditorDialog`, `EnvironmentDialog`, `ExecutionDetailsDialog`, and `UpdateDialog` so nested message/file/color dialogs now use the immediate launching form via `DialogTheme.Show(this, ...)` or `ShowDialog(this)`.
- Preserved the narrow ownerless exception in `Form1` startup initialization for the configuration-load warning, because that path runs before the main window is reliably shown; no modeless ownership flows were changed.
- Added a short note in `UI\\DialogTheme.cs` clarifying that ownerless dialogs are exceptional and that visible forms should pass themselves as owners.
- Verification: a targeted search over the touched forms found no remaining ownerless `ShowDialog()` usages and no remaining ownerless `DialogTheme.Show(...)` call sites besides the intentional `Form1` startup warning.
- Verification: `dotnet build SSH_Helper.sln -nologo` passed with 0 errors. The running `SSH_Helper` process (PID 31784) held `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` open, so MSBuild emitted four retry warnings before finishing successfully.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~SettingsDialogAppearanceTests|FullyQualifiedName~ExecutionDetailsDialogTests|FullyQualifiedName~JobEditorDialogLayoutTests|FullyQualifiedName~JobEditorDialogCustomPresetTests|FullyQualifiedName~JobEditorDialogTimeoutOverrideTests"` passed (24/24).

## 93. Investigate popup ownership behavior
- [x] 93.1 Inspect dialog and popup launch paths to separate owned windows from ownerless windows.
- [x] 93.2 Verify how the shared dialog helpers behave when no owner is supplied.
- [x] 93.3 Document the root cause and the concrete call sites that let popups appear behind `Form1`.

### 93 Review
- Root cause is inconsistent dialog ownership. The shared helper in `UI\\DialogTheme.cs` explicitly treats ownerless dialogs as standalone windows: `Show(string...)` overloads pass `owner = null`, `ShowCore(...)` switches ownerless dialogs to `FormStartPosition.CenterScreen`, and it finally calls `dlg.ShowDialog(owner)`. Without an owner, Windows does not keep the popup parented above `Form1`.
- The app already has counterexamples proving the intended behavior: owned modal dialogs use `ShowDialog(this)` in `Form1` and other forms, modeless singletons use `ModelessDialogManager.ShowOrActivate(..., this)` in `Utilities\\ModelessDialogManager.cs`, script prompts use `dialog.Show(mainForm)` in `Services\\Scripting\\Commands\\ScriptPromptDialogRunner.cs`, the terminal window uses `form.Show(Application.OpenForms[0])` in `Services\\Terminal\\InteractiveTerminalService.cs`, and `FindDialog` sets `Owner = owner`.
- The issue is broad rather than isolated. App code still has many ownerless `DialogTheme.Show(...)` call sites plus several ownerless `ShowDialog()` file/color dialogs. Representative examples in `Form1.cs` include ownerless save/open dialogs at lines 6746, 7102, 8042, 8066, 10299, and 10330, and ownerless message dialogs such as the history-load warning at line 1058 and the CSV reload prompt at line 1758.
- Secondary dialogs repeat the same pattern, so popups can appear behind the dialog that triggered them: `SettingsDialog.cs` uses ownerless `colorDialog.ShowDialog()` at line 1069 and ownerless `DialogTheme.Show(...)` confirmations; `UpdateDialog.cs`, `EnvironmentDialog.cs`, `JobEditorDialog.cs`, and `ExecutionDetailsDialog.cs` also call ownerless `DialogTheme.Show(...)`.
- Smallest clean fix: standardize on passing the current form as owner for all modal UI (`DialogTheme.Show(this, ...)`, `openDialog.ShowDialog(this)`, `saveDialog.ShowDialog(this)`, `colorDialog.ShowDialog(this)`) and reserve ownerless dialogs only for cases that are intentionally app-global.

## 91. Remove scheduler close focus flicker
- [x] 91.1 Inspect the modeless dialog owner-reactivation timing and confirm why close briefly activates another app before `Form1`.
- [x] 91.2 Patch the reactivation path to avoid deferred owner activation flicker while preserving the focus restore fix.
- [x] 91.3 Run focused verification and capture the review outcome below.

## 92. Fix startup history restore hydration
- [x] 92.1 Trace the startup history-selection guard and confirm why a visually selected history row can still leave output/hosts blank after launch.
- [x] 92.2 Patch the startup arming path so any already-selected history entry is hydrated into the output and hosts panes once startup input settles.
- [x] 92.3 Add focused regression coverage for the startup selection/rehydration rule and capture verification results below.

### 92 Review
- Root cause was the startup history arming guard in `Form1`: a carried-over launch click could still change `lstOutput` selection before `_historySelectionHandlingEnabled` was turned on, so the first history row looked selected while `lstOutput_SelectedIndexChanged(...)` skipped the output/hosts hydrate work.
- Patched `Form1` so `ArmHistorySelectionOnIdle(...)` now checks for an already-selected history entry once startup input settles, enables history selection handling, and immediately applies the visible selection to the output pane and host list.
- Extracted the shared history-pane hydrate body into `ApplySelectedHistoryEntry()` so the normal selection-changed path and the startup-rehydrate path use the same logic.
- Added `UI\\HistoryStartupSelectionHydration.cs` plus focused tests in `SSH_Helper.Tests\\UI\\HistoryStartupSelectionHydrationTests.cs` to lock the startup rule: hydrate only when a history row is already selected and handling has not yet been enabled.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HistoryStartupSelectionHydrationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\startup-history-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\startup-history-tests\\obj\\` passed (3/3).
- Verification: `dotnet build SSH_Helper.sln -p:BaseOutputPath=artifacts\\startup-history-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\startup-history-build\\obj\\` passed with 0 warnings and 0 errors.

### 91 Review
- Confirmed the flicker cause in `Utilities\\ModelessDialogManager.cs`: the earlier focus-restore patch always used `BeginInvoke` for owner reactivation, even when already on the UI thread during the dialog `FormClosed` event. That deferred `BringToFront()` / `Activate()` by one message loop turn, which gave Windows time to activate another app first and then pull `Form1` back to the foreground.
- Patched the shared modeless-dialog manager so owner reactivation now runs immediately when already on the UI thread, while still using `BeginInvoke` only for real cross-thread cases.
- Tightened the regression in `SSH_Helper.Tests\\UI\\ModelessDialogManagerTests.cs` so it asserts owner-reactivation happens synchronously on `dialog.Close()` instead of only after a later `Application.DoEvents()`.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ModelessDialogManagerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\modeless-focus-timing-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\modeless-focus-timing-tests\\obj\\` passed (2/2).

## 90. Restore main window focus after scheduler closes
- [x] 90.1 Inspect the scheduler dialog show/close ownership path and identify why closing it does not reactivate `Form1`.
- [x] 90.2 Patch the modeless scheduler close path so the main app regains focus when the scheduler window closes.
- [x] 90.3 Run focused verification and capture the review outcome below.

### 90 Review
- Root cause was in `Utilities\\ModelessDialogManager.cs`: `ShowOrActivate(...)` showed the scheduler dialog with `Form1` as owner, but the `FormClosed` handler only cleared `_current` and never reactivated the owner window, so Windows focus could fall through to another app behind SSH Helper.
- Patched the shared modeless-dialog manager to capture the owner form, restore it on close with `BringToFront()` + `Activate()` via a UI-thread-safe helper, and bring newly created dialogs to the front on first show for consistency with the reuse path.
- Added a focused regression in `SSH_Helper.Tests\\UI\\ModelessDialogManagerTests.cs` proving the manager requests owner reactivation when the modeless dialog closes.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ModelessDialogManagerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\modeless-focus-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\modeless-focus-tests\\obj\\` passed (2/2).

## 89. Lighten scheduler status link color
- [x] 89.1 Inspect the scheduler status label styling and find where its current blue link color is assigned or inherited.
- [x] 89.2 Patch the scheduler status label to use a slightly lighter blue without changing its visibility or click behavior.
- [x] 89.3 Run focused verification and capture the review outcome below.

### 89 Review
- Traced the blue text to the scheduler `ToolStripStatusLabel` link styling in `Form1`, which previously inherited the default WinForms link color because the label had `IsLink = true` but no explicit themed link colors.
- Added explicit light/dark scheduler link colors in `Form1` and a small `ApplySchedulerStatusBarTheme()` helper so the status label keeps the lighter blue both at startup and when the app theme is reapplied later from settings.
- The new shades are intentionally only a small lift from the old default: light mode uses `Color.FromArgb(36, 120, 214)` with a slightly lighter active state, and dark mode uses `Color.FromArgb(92, 171, 226)` with a slightly lighter active state.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulerNotificationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\scheduler-link-color-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-link-color-tests\\obj\\` passed (18/18).

## 88. Hide zero-task scheduler status
- [x] 88.1 Inspect the status-bar code that renders the scheduler active-task segment and trace the enabled-task count source.
- [x] 88.2 Patch the UI so the scheduler status segment appears only when the enabled-task count is greater than zero.
- [x] 88.3 Run focused verification and capture the review outcome below.

### 88 Review
- Traced the screenshot text to `Form1.UpdateSchedulerStatusBar()`, where the existing `activeCount` is already the enabled-job count (`_jobStorage.Jobs.Values.Count(j => j.IsEnabled)`), not currently-running jobs.
- Patched `Form1.InitializeSchedulerStatusBar()` to create the scheduler status label hidden by default and updated `UpdateSchedulerStatusBar()` to show it only when `activeCount > 0`, leaving the menu entry and formatter text unchanged for positive counts.
- Added `SchedulerNotificationFormatter.ShouldShowStatusBar(int activeJobCount)` as the small pure visibility rule and covered both zero and positive counts in the existing scheduler notification test class.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulerNotificationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\scheduler-status-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-status-tests\\obj\\` passed (18/18).

## 86. Diagnose canonical sample validation failure
- [x] 86.1 Inspect the failing canonical-sample test, validator, and implicated script sample to identify the exact mismatch.
- [x] 86.2 Run focused verification to capture the concrete validation error(s) for the sample.
- [x] 86.3 Decide whether the defect is in test coverage/data or in production validation/parsing logic, then record the review outcome below.

### 86 Review
- Focused verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CanonicalCommandMapSyntaxTests.Validate_ScriptSamples_AreCanonicalAndPassEnforcedValidation" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\canonical-samples-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\canonical-samples-tests\\obj\\` failed only because `ScriptSamples\\generic\\library_import_demo.yaml` imports the placeholder path `C:\\Path\\To\\SSH_Helper\\ScriptSamples\\libraries\\string_sections.yaml`, which does not exist in the repo checkout.
- Production validation is behaving as designed: `ScriptSubroutineRegistryBuilder.LoadImports(...)` explicitly requires absolute import paths and rejects missing files before resolving imported subroutines, so the downstream `Unknown subroutine` errors are expected once the import fails.
- The broad sample-sweep test is the mismatch. It assumes every checked-in `ScriptSamples\\**\\*.yaml` file is immediately self-validating, but at least one sample is intentionally not portable as committed (`library_import_demo.yaml` says to update the absolute path before running), and the QA fixture pattern in `QaPresetCatalogTests` already shows the repo sometimes rewrites placeholder import paths before validation.
- Conclusion: not a parser/validator bug. The failure belongs to test/sample expectations. Smallest clean fix is either to exclude placeholder/fixture samples from the canonical sweep or to preprocess known placeholder import tokens into repo-local absolute paths before validating them.

## 87. Fix canonical sample validation placeholders
- [x] 87.1 Audit `ScriptSamples` for placeholder import-path tokens that cannot validate as committed.
- [x] 87.2 Patch `CanonicalCommandMapSyntaxTests` to normalize known repo-local placeholder import paths before parse/validate.
- [x] 87.3 Re-run focused canonical-sample validation and capture the review outcome below.

### 87 Review
- Audited `ScriptSamples` and confirmed three test-only portability cases the old broad sweep did not account for: `generic\\library_import_demo.yaml` uses the documented `C:\\Path\\To\\SSH_Helper\\ScriptSamples...` placeholder prefix, `qa\\catalog_runner.yaml` uses the `__QA_CATALOG_LIBRARY_PATH__` token, and `libraries\\string_sections.yaml` / `qa\\catalog_library.yaml` are `library: true` files that should validate as libraries rather than executable scripts.
- Patched `SSH_Helper.Tests\\Scripting\\CanonicalCommandMapSyntaxTests.cs` so the sample-sweep test now resolves repo-known placeholder import paths to actual repo-local library files before parsing and validating, while leaving production import validation unchanged.
- The same test now passes `allowLibraryDefinitions: script.Library` so reusable library samples under `ScriptSamples` validate against the correct contract instead of being treated as directly executable scripts.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CanonicalCommandMapSyntaxTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\canonical-command-map-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\canonical-command-map-tests\\obj\\` passed (8/8).

## 85. Optimize history-pane updates after manual runs
- [x] 85.1 Replace the post-run full history reload with an incremental in-memory insert/select path for new history entries.
- [x] 85.2 Reuse the freshly built history payload for the selected entry so selecting the new run does not immediately reread it from disk.
- [x] 85.3 Run focused verification for the touched history/UI path and capture the review outcome below.

### 85 Review
- Added `UI\\HistoryListCollectionUpdater.cs` as the small pure helper that builds a `HistoryListItem`, inserts a new run at the top of the in-memory history list, replaces duplicate ids safely, and mirrors retention trimming by removing overflow ids from both the list and the index map.
- Updated `Form1.LoadHistoryIndexIntoList(...)` to use the shared item builder and wrap bulk history-list refreshes in `lstOutput.BeginUpdate()/EndUpdate()` so cold reloads avoid unnecessary paint churn.
- Replaced the post-run `LoadHistoryIndexIntoList(...)` call in both manual preset and folder history save paths with a new `InsertHistoryEntryIntoList(...)` flow that updates `_outputHistory` and `_historyIndexEntries` in place, clears the old selection, and selects the new entry without clearing and rebuilding the entire history pane.
- Added `CacheLoadedHistoryPayload(...)` so the freshly built `HistoryRunPayload` is reused immediately when the new history row is selected, avoiding the redundant disk read that previously happened right after save.
- Verification: normal `dotnet build SSH_Helper.csproj -nologo` failed because the running `SSH_Helper` process held the default debug outputs open.
- Verification: `dotnet build SSH_Helper.csproj -nologo -p:BaseOutputPath=artifacts\\history-incremental-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\history-incremental-build\\obj\\` passed with 0 warnings and 0 errors.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~HistoryListCollectionUpdaterTests|FullyQualifiedName~HistoryStorageServiceTests|FullyQualifiedName~HistoryListLayoutTests" -p:BaseOutputPath=artifacts\\history-incremental-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\history-incremental-tests\\obj\\` passed (15/15).

## 84. Fix manual progress visibility regressions
- [x] 84.1 Prevent stale manual-progress callbacks from re-showing the status bar after execution completes.
- [x] 84.2 Limit manual progress visibility to runs with more than one host-task operation so 1x1 runs stay bar-free.
- [x] 84.3 Add focused regression coverage for the visibility rule and rerun progress verification.
- [x] 84.4 Run targeted tests plus a normal solution build, then capture the review outcome below.

### 84 Review
- Reworked the `Form1` manual-progress lifecycle so `BeginManualExecutionProgress(...)` now returns a reporter only when the run has more than one host-task operation, which keeps 1 host x 1 preset runs from showing the progress bar at all.
- Added a per-run token on the form side and an `EndManualExecutionProgress()` invalidation step, so any queued `Progress<FolderExecutionProgress>` callbacks posted after completion are ignored instead of re-showing the status bar.
- Updated the multi-host preset branch to use the new conditional progress start and fall back to the normal start status text when the selected execution collapses to a single operation after the dialog filters hosts.
- Updated the folder execution path with the same conditional visibility rule so single-operation folder runs show only status text, while multi-operation runs still get percent-based determinate progress.
- Extended `ManualExecutionStatusProgressTests` with an explicit visibility-rule test that requires `totalOperations > 1` before progress should be shown.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServiceProgressTests|FullyQualifiedName~ManualExecutionStatusProgressTests|FullyQualifiedName~SshExecutionServiceCancellationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\manual-progress-visibility-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\manual-progress-visibility-tests\\obj\\` passed (11/11).
- Verification: normal `dotnet build .\\SSH_Helper.sln` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 48112).
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\manual-progress-visibility-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\manual-progress-visibility-build\\obj\\` passed with 0 warnings and 0 errors.

## 83. Simplify manual execution status progress
- [x] 83.1 Update manual execution progress reporting to use completed operations out of total host-task operations.
- [x] 83.2 Patch Form1 status-bar handling so multi-host preset runs and folder runs show simple monotonic percent progress.
- [x] 83.3 Add focused regression coverage for execution progress reporting and form-side percent formatting.
- [x] 83.4 Run targeted tests plus a normal solution build, then capture the review outcome below.

### 83 Review
- Added additive `CompletedOperations` and `TotalOperations` to `FolderExecutionProgress` and changed `SshExecutionService.ExecuteFolderAsync(...)` to report progress only when a host-task unit finishes, where one unit is one preset completed on one host.
- Removed the earlier start/batch progress inference so manual progress now tracks actual completed work across both sequential and parallel folder execution paths, including the multi-host preset path that reuses `ExecuteFolderAsync(...)` with a single selected preset.
- Added `Utilities\\ManualExecutionStatusProgress.cs` as the shared helper that converts operation counts into the simple `Running... {percent}%` text and clamps out-of-order parallel reports so the status bar never moves backward.
- Updated `Form1` so multi-host preset runs and folder runs initialize determinate progress from `total hosts x total tasks`, feed the shared progress reporter into the service, and keep the existing final success/failure/cancel summary messages unchanged.
- Added focused regressions in `SSH_Helper.Tests\\Services\\SshExecutionServiceProgressTests.cs` and `SSH_Helper.Tests\\UI\\ManualExecutionStatusProgressTests.cs`, and kept the existing cancellation coverage in scope to guard the touched execution path.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SshExecutionServiceProgressTests|FullyQualifiedName~ManualExecutionStatusProgressTests|FullyQualifiedName~SshExecutionServiceCancellationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\manual-progress-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\manual-progress-tests\\obj\\` passed (8/8).
- Verification: `dotnet build .\\SSH_Helper.sln` passed with 0 warnings and 0 errors.

## 82. Add variable-height history rows
- [x] 82.1 Replace the history list fixed-height configuration with measured variable-height rows that wrap full labels and cap at 3 lines.
- [x] 82.2 Add reusable row-height measurement logic plus targeted automated coverage for short, wrapped, and capped labels.
- [x] 82.3 Verify the history list still behaves correctly after font/layout changes and capture the result below.

### 82 Review
- Replaced the history sidebar `ListBox` with a small `HistoryListBox` subclass and switched the history rows to `OwnerDrawVariable`, so row height now remeasures from the current list width and font instead of staying pinned to the old 22px fixed height.
- Added `UI\\HistoryListLayout.cs` as the shared measurement/drawing helper. It wraps the full existing history label, derives the baseline row height from the active font, and clamps very long entries to 3 visible lines.
- Simplified `LstOutput_DrawItem(...)` so it draws the full label inside padded multi-line bounds with the existing light/dark selection styling preserved; the stored history label format and persistence model were left unchanged.
- Extended the WinForms font harness so the history list can be configured in variable-height mode during tests, and added focused coverage in `SSH_Helper.Tests\\UI\\HistoryListLayoutTests.cs` plus an extra `ApplyFontSettingsTests` case for wrapped history rows after font changes.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HistoryListLayoutTests|FullyQualifiedName~ApplyFontSettingsTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\history-rows-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\history-rows-tests\\obj\\` passed (38/38).
- Verification: normal `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false` failed because the running `SSH_Helper` process held `bin\\Debug\\net8.0-windows\\SSH_Helper.dll` open.
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\history-rows-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\history-rows-build\\obj\\` passed with 0 warnings and 0 errors.

## 81. Fix webhook suppressed-error capture state
- [x] 81.1 Confirm why `QA Webhook GET POST [Internet]` fails on the final bad-URL assertion.
- [x] 81.2 Patch the webhook runtime so suppressed failures leave capture variables in a deterministic empty state.
- [x] 81.3 Add focused regression coverage and run targeted verification.

### 81 Review
- Root cause was in `Services\\Scripting\\Commands\\WebhookCommand.cs`: unlike `HttpCommand`, the webhook path did not initialize `into` capture variables before validation or request execution. On a transport failure with `on_error: continue`, the step returned a suppressed error but left `bad_response` and `bad_response_status` undefined, and this scripting engine treats an undefined variable as not-empty for `x is empty` checks because unresolved identifiers fall back to literal text.
- Patched `WebhookCommand` to clear `${into}` and `${into}_status` at the start of each execution so both stale and previously-undefined capture variables become deterministic empty values across all failure paths, including bad URLs, timeouts, and transport exceptions. The command also now supports an internal test-only handler factory so transport failures can be exercised without real network dependencies.
- Added a focused regression in `SSH_Helper.Tests\\Scripting\\NetworkCommandTests.cs` proving a suppressed webhook transport failure clears stale `webhook_result` and `webhook_result_status` values instead of leaving old or undefined state behind.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~NetworkCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\webhook-fix-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\webhook-fix-tests\\obj\\` passed (17/17).
- Verification: normal `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false` failed because the running `SSH_Helper` process held `bin\\Debug\\net8.0-windows\\SSH_Helper.dll` open.
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\webhook-fix-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\webhook-fix-build\\obj\\` passed with 0 warnings and 0 errors.

## 80. Clarify intentional non-success QA preset wording
- [x] 80.1 Update non-success QA preset descriptions so they explicitly say the shown failure/error is the intended QA pass condition.
- [x] 80.2 Adjust the catalog audit test to match the clarified non-success wording.
- [x] 80.3 Re-run targeted QA catalog verification and capture the result below.

### 80 Review
- Updated the non-success QA preset descriptions so they now state that the displayed failure, error, or validation rejection is intentional and should be read as a QA pass condition rather than an accidental script failure.
- The clarified wording now covers `QA Exit Failure`, `QA Exit Error`, `QA Assert Error Stop`, and the `[Expected Fail]` validation samples, using explicit phrases like `Expected: intentional failure exit. QA pass when the failure is shown.`
- Updated `SSH_Helper.Tests\\Scripting\\QaPresetCatalogTests.cs` so the catalog audit enforces the new intentional non-success wording while preserving the existing result-contract checks for failure exits, error exits, error-severity assert stops, and invalid presets.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~QaPresetsSyntaxTests|FullyQualifiedName~QaPresetCatalogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\qa-catalog-wording-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\qa-catalog-wording-tests\\obj\\` passed.

## 79. Refresh QA preset catalog coverage and conventions
- [x] 79.1 Audit `qa_presets.json` against the current scripting surface and map missing coverage plus unclear preset outcomes/prerequisites.
- [x] 79.2 Add QA fixture files plus automated catalog tests for syntax, description conventions, coverage, and final-result contracts.
- [x] 79.3 Rewrite `qa_presets.json` so every preset description states requirements and expected result, positive presets end with an explicit success marker, expected-failure presets are labeled, and missing feature coverage is added.
- [x] 79.4 Run isolated verification and capture the review outcome below.

### 79 Review
- Refreshed `qa_presets.json` from 53 to 59 QA presets so every YAML `description` now includes both `Requires:` and `Expected:` and requirement wording is explicit for user interaction, shell assumptions, internet access, grid inputs, Windows-local file access, and other environment constraints.
- Split ambiguous outcome presets into separate entries (`QA Exit Success`, `QA Exit Bare Success`, `QA Exit Failure`, `QA Exit Error`, `QA Assert`, `QA Assert Error Stop`), tagged intentional validation samples with `[Expected Fail]`, and normalized positive presets to end with one visible terminal pass marker instead of finishing on plain prints or status-only logs.
- Added file-backed QA fixtures under `ScriptSamples\\qa\\` plus catalog coverage for `environment`, `suppress_missing_column_warning`, `library`, `imports`, `subroutines`, `call`, `return`, `send.expect`, `readfile.select_file`, `readfile.message`, `readfile.file_ext`, `readfile.encoding`, `http.follow_redirects`, `interactive.show_window`, `interactive.max_lines`, `interactive.width`, `interactive.height`, and `_writefile`.
- Added `SSH_Helper.Tests\\Scripting\\QaPresetCatalogTests.cs` to enforce description conventions, coverage requirements, validation expectations for `[Expected Fail]` presets, and a stricter result contract that rejects hidden earlier top-level exits before the final visible outcome.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~QaPresetsSyntaxTests|FullyQualifiedName~QaPresetCatalogTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\qa-catalog-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\qa-catalog-tests\\obj\\` passed.
- Verification: normal `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false` failed because the running `SSH_Helper` process held `bin\\Debug\\net8.0-windows\\SSH_Helper.dll` open.
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\qa-catalog-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\qa-catalog-build\\obj\\` passed with 0 warnings and 0 errors.

## 78. Fix bare list index expression resolution
- [x] 78.1 Confirm the runtime path causing bare list index expressions like `ports[0]` to be treated as literal text in conditions.
- [x] 78.2 Patch the shared expression resolver so bare top-level index expressions resolve consistently outside `${...}` interpolation.
- [x] 78.3 Add focused regression coverage for indexed list reads in conditions and plain `set` assignment.
- [x] 78.4 Run targeted verification and capture the review outcome below.

### 78 Review
- Root cause was separate from task 77: the shared `ValueResolver.ResolveExpressionValue(...)` path did not understand bare top-level index expressions such as `ports[0]`, so condition evaluation treated them as literal text unless they were wrapped in `${...}` interpolation.
- Patched the shared resolver to recognize top-level `name[index]` expressions, resolve literal or variable-backed indexes, and read from the same collection view used by script context interpolation.
- Added focused regressions proving bare indexed list expressions now work in `if` conditions (`ports[0] == '22'`, `parts[idx] == 'beta'`) and in normal `set` assignment after `pop`/`shift` mutation.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExpressionEvaluatorTests|FullyQualifiedName~SetCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\index-expression-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\index-expression-tests\\obj\\` passed (46/46).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\index-expression-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\index-expression-build\\obj\\` passed with 0 warnings and 0 errors.

## 77. Fix null-valued variable expression resolution
- [x] 77.1 Confirm the expression/runtime path causing defined `null` variables to be treated as unresolved identifiers in conditions.
- [x] 77.2 Patch the shared expression resolver so defined variables preserve `null` values across condition and set evaluation.
- [x] 77.3 Add focused regression coverage for `is empty`, truthiness, and equality checks against defined `null` variables.
- [x] 77.4 Run targeted verification and capture the review outcome below.

### 77 Review
- Root cause was in `ValueResolver.ResolveExpressionValue(...)`: it only returned a direct variable lookup when `context.GetVariable(expr)` was non-null, so a defined variable whose value was actually `null` fell through and got reinterpreted as the literal identifier text.
- Patched the shared resolver to treat `context.HasVariable(expr)` as authoritative for direct variable references, preserving `null` values across condition evaluation and plain `set` assignment.
- Added focused regressions proving defined-null variables are still `defined`, evaluate as `empty` and falsy in conditions, compare equal to another defined-null variable, and remain `null` when assigned into another variable through `set`.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExpressionEvaluatorTests|FullyQualifiedName~SetCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\null-resolution-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\null-resolution-tests\\obj\\` passed (43/43).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\null-resolution-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\null-resolution-build\\obj\\` passed with 0 warnings and 0 errors.

## 76. Add opt-in send shell-exit failure handling
- [x] 76.1 Extend `send` model/parser/validation/editor metadata with `fail_on_nonzero`.
- [x] 76.2 Add the `SendCommand` runtime path that detects non-zero shell exit status when opted in, preserving captured output and existing default behavior.
- [x] 76.3 Add focused parser/runtime/control-flow/editor coverage for `fail_on_nonzero` success, failure, and invalid combinations.
- [x] 76.4 Update scripting docs and QA/control-flow examples, run focused verification, and capture the review outcome below.

### 76 Review
- Added `ScriptStep.FailOnNonZero`, parser support for `send.fail_on_nonzero`, send-specific validation rejecting `fail_on_nonzero` with `expect`/`respond`, and boolean autocomplete suggestions for the new option.
- Refactored `SendCommand` to use a small injectable send-session adapter for tests, wrap opted-in commands with an injected exit-status sentinel, strip the sentinel from captured/user-visible output, and convert non-zero shell status into normal step failure while preserving `_output`/`capture`.
- Kept default `send` behavior unchanged when `fail_on_nonzero` is omitted, so plain shell error text still behaves as output unless the script explicitly opts into exit-status checking.
- Fixed a separate control-flow correctness gap uncovered during implementation: `_last_error` now remains available for the full duration of a `catch` block, matching the scripting control-flow spec and allowing multi-step catch handlers like the QA preset to read `_last_error` more than once.
- Updated `SCRIPTING.md` and the bundled `QA Control Flow Primitives` preset in `qa_presets.json` so the documented and shipped examples use `fail_on_nonzero: true` when they expect shell command failure to enter `catch`.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SendCommandTests|FullyQualifiedName~ScriptExecutorControlFlowTests|FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptAutocompleteProviderTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\send-fail-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\send-fail-tests\\obj\\` passed (177/177).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\send-fail-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\send-fail-build\\obj\\` passed with 0 warnings and 0 errors.
- Verification: normal `dotnet build .\\SSH_Helper.sln` also passed with 0 warnings and 0 errors.

## 75. Fix call-arg literal missing-column warnings
- [x] 75.1 Update `ScriptDependencyAnalyzer` so plain literal `call.args` text is not tokenized as missing-column expressions.
- [x] 75.2 Add focused regression coverage for literal `call.args` text and expression-backed `call.args`.
- [x] 75.3 Run targeted verification and capture the review outcome below.

### 75 Review
- Tightened `ScriptDependencyAnalyzer` so `AnalyzeExpressionReferences(...)` now only treats `call.args` text as a dependency-bearing expression when it matches forms the runtime actually resolves structurally: bare variable names, function-style expressions, member/indexer paths, or `.length` access.
- This removes the false-positive path where decorative literal strings like `=== IPv4 Unique Internet Service Matches ===` were being tokenized into fake grid columns just because they contained identifier-like words.
- Added regressions proving literal `call.args.title` text produces no missing-column warnings, while structured expression args such as `compact(split(source_services, ','))` still report the real external dependency.
- Verification: an initial parallel `dotnet test` plus `dotnet build` run hit a transient shared-`obj` file lock, so the final verification was re-run sequentially with isolated output paths.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj -p:UseAppHost=false -p:BaseOutputPath=artifacts\\call-arg-missing-column-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\call-arg-missing-column-tests\\obj\\ --filter "FullyQualifiedName~ScriptSubroutineDependencyAnalyzerTests|FullyQualifiedName~ScriptDependencyAnalyzerTests"` passed (35/35).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\call-arg-missing-column-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\call-arg-missing-column-build\\obj\\` passed with 0 warnings and 0 errors.

## 74. Add Script Subroutines And File Libraries
- [x] 74.1 Add OpenSpec change artifacts for script subroutines, calls, returns, and file-based libraries.
- [x] 74.2 Extend scripting models, parser, validation, and import/subroutine registry for `subroutines`, `imports`, `library`, `call`, and `return`.
- [x] 74.3 Implement runtime call stack, child variable scopes, explicit output binding, and `return` control flow.
- [x] 74.4 Update dependency analysis and editor metadata so validation, autocomplete, highlighting, and missing-column preflight understand the new syntax.
- [x] 74.5 Add focused tests for parser/runtime/analyzer/editor behavior, update docs and samples, and capture verification results below.

### 74 Review
- Added the OpenSpec change set under `openspec\\changes\\add-script-subroutines-and-libraries` and kept it valid throughout the implementation pass.
- Extended the scripting model/parser with top-level `library`, `imports`, and `subroutines`, plus step-level `call` and `return`, including validation for library-only files, absolute-path imports, required args, output bindings, `return` placement, and local recursive call cycles.
- Added a reusable `ScriptSubroutineRegistryBuilder`, runtime `CallCommand`/`ReturnCommand`, shared child-scope execution in `ScriptContext`, explicit output copy-back, and a defensive max subroutine call depth of 32.
- Updated dependency analysis and SSH preflight analysis so reachable local subroutines and resolved `call` edges are understood without leaking subroutine params/locals as fake grid-column dependencies.
- Updated editor surfaces so parser-driven autocomplete/highlighting recognize the new syntax, interpolation symbol extraction includes subroutine params/outputs and `call.out` bindings, and inline editor validation accepts library-definition files.
- Updated `SCRIPTING.md`, refactored `ScriptSamples\\fortigate\\internet_service_lookup_from_file.yaml` to the new subroutine-based style, and added bundled library/import demo samples under `ScriptSamples\\libraries\\string_sections.yaml` and `ScriptSamples\\generic\\library_import_demo.yaml`.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj -p:UseAppHost=false --filter "FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptSubroutine|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~YamlSshSyntaxHighlighterTests|FullyQualifiedName~ScriptExecutorControlFlowTests"` passed (223/223).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors.
- Verification: `openspec validate add-script-subroutines-and-libraries --strict --no-interactive` passed.

## 73. Archive Newly Completed OpenSpec Proposals
- [x] 73.1 Confirm the currently completed active change IDs and use that set as the archive target for this pass.
- [x] 73.2 Archive each newly completed change with `openspec archive <id> --yes`.
- [x] 73.3 Run strict OpenSpec validation on the updated result and capture the outcome below.

### 73 Review
- Started this pass with the two active completed changes shown by `openspec list`: `update-scheduler-host-grid-parity` and `update-scheduler-job-timeouts`.
- Archived both with `openspec archive <id> --yes`, which updated `openspec\\specs\\job-scheduler\\spec.md` and moved the changes into `openspec\\changes\\archive\\2026-03-13-*`.
- After that archive pass, `openspec list` showed `add-readfile-file-picker` as newly complete as well, so it was included in the same run to satisfy the request to archive all completed proposals. Archiving it updated `openspec\\specs\\scripting-runtime\\spec.md` and `openspec\\specs\\scripting-validation\\spec.md`, and moved it to `openspec\\changes\\archive\\2026-03-13-add-readfile-file-picker`.
- A verification attempt that ran `openspec list` in parallel with `openspec archive add-readfile-file-picker --yes` hit a transient `ENOENT` while the change directory was moving. Re-running the checks sequentially resolved that race cleanly.
- Verification: `openspec list` now shows only incomplete active changes: `add-script-assertions` and `add-job-scheduler`.
- Verification: archive entries confirmed for `2026-03-13-update-scheduler-host-grid-parity`, `2026-03-13-update-scheduler-job-timeouts`, and `2026-03-13-add-readfile-file-picker`.
- Verification: `openspec validate --all --strict --no-interactive` passed (`22 passed, 0 failed`).

## 72. Archive Completed OpenSpec Proposals
- [x] 72.1 Confirm the active OpenSpec changes currently marked complete and treat that set as the archive target.
- [x] 72.2 Archive each completed change with `openspec archive <id> --yes`.
- [x] 72.3 Run strict OpenSpec validation on the archived result and capture the outcome below.

### 72 Review
- Archived the nine active changes that `openspec list` reported as `✓ Complete`: `update-environment-csv-sync`, `update-folder-base-environments`, `update-script-load-environment`, `replace-scheduler-drift-with-save-warning`, `update-scheduler-job-integrity`, `update-scheduler-runtime-history`, `update-cancellation-outcomes`, `add-scheduler-custom-presets`, and `update-scripting-collection-ergonomics`.
- Ran `openspec archive <id> --yes` for each change oldest-to-newest so spec updates applied in a predictable sequence. All nine archived successfully into `openspec\\changes\\archive\\2026-03-13-*`.
- The archive command for `replace-scheduler-drift-with-save-warning` emitted non-blocking proposal authoring warnings and a warning that one removed requirement was ignored because `job-scheduler` was being created from archive deltas at that point. The archive still completed successfully and later strict validation passed for the resulting spec tree.
- Verification: `openspec list` now shows only incomplete active changes: `add-readfile-file-picker`, `update-scheduler-job-timeouts`, `update-scheduler-host-grid-parity`, `add-script-assertions`, and `add-job-scheduler`.
- Verification: `openspec list --specs` shows the updated live spec set, including `environment-management`, `execution-control`, `execution-history`, `job-scheduler`, `preset-organization`, `scripting-expressions`, `scripting-runtime`, and `scripting-validation`.
- Verification: bare `openspec validate --strict --no-interactive` is not accepted by this CLI build without an explicit target, so the final strict pass used `openspec validate --all --strict --no-interactive`, which passed (`25 passed, 0 failed`).

## 71. Restore Main Window Focus After Script Prompt Close
- [x] 71.1 Patch the shared modeless script prompt cleanup path so the main form is explicitly reactivated after the prompt closes.
- [x] 71.2 Add focused automated coverage for the shared prompt-runner reactivation path.
- [x] 71.3 Run targeted verification and capture the review outcome below.

### 71 Review
- Updated `Services\\Scripting\\Commands\\ScriptPromptDialogRunner.cs` so the shared `dialog.FormClosed` cleanup path now calls `RestoreMainFormActivation(mainForm)` immediately after releasing `MainFormPromptLock`. That makes the owner-form reactivation happen for the `readfile.select_file` cancel path and the other script prompt dialogs that share the same runner.
- `RestoreMainFormActivation(...)` is defensive: it skips disposed, hidden, or minimized owners, and otherwise brings `mainForm` to the front and activates it on the UI thread. That keeps the change narrowly scoped to the exact cleanup point where the prompt closes.
- Added `SSH_Helper.Tests\\UI\\ScriptPromptDialogRunnerTests.cs` to cover the shared runner. The regression uses a test hook on `ScriptPromptDialogRunner` to verify that closing a modeless prompt requests owner reactivation exactly once for the correct main form, without depending on desktop-focus behavior from the xUnit host.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptPromptDialogRunnerTests|FullyQualifiedName~ScriptReadFileOpenPathDialogTests|FullyQualifiedName~ReadFileCommandTests|FullyQualifiedName~ScriptExecutorControlFlowTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\prompt-focus-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\prompt-focus-tests\\obj\\` passed (20/20).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\prompt-focus-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\prompt-focus-build\\obj\\` passed with 0 warnings and 0 errors.
- Verification: normal `dotnet build .\\SSH_Helper.sln` also passed with 0 warnings and 0 errors.

## 70. Inspect Readfile Picker Cancel Focus Recovery
- [x] 70.1 Trace the shared script prompt dialog owner/focus flow for the `readfile.select_file` cancel path and identify the most likely focus-loss cause.
- [x] 70.2 Inspect the smallest relevant automated tests to confirm whether owner/focus recovery is covered.
- [x] 70.3 Capture the concise root-cause summary, smallest safe edit point, and coverage notes in the review section below.

### 70 Review
- `ReadFileCommand.ResolveFilePathAsync(...)` routes `readfile.select_file` through `PromptForOpenPathAsync(...)`, which delegates to `ScriptPromptDialogRunner.ShowAsync<ScriptReadFileOpenPathDialog, string?>()` for the actual WinForms prompt ownership path.
- In `ScriptPromptDialogRunner.ShowAsync(...)`, the modeless prompt is shown with `dialog.Show(mainForm)` and closed through the shared `FormClosed` handler. That handler only disposes the cancellation registration, re-enables the main-form control tree via `MainFormPromptLock.Dispose()`, and disposes the dialog; it never explicitly re-activates `mainForm` or restores a previously focused control.
- `MainFormPromptLock.Dispose()` only flips disabled controls back to `Enabled = true`. Because the prompt lock disables whichever child control in `Form1` previously held focus, cancelling the picker can leave `Form1` re-enabled but without focus being restored, which matches the reported symptom more closely than any `ReadFileCommand`-specific logic.
- The smallest safe edit point is the shared cleanup path in `Services\\Scripting\\Commands\\ScriptPromptDialogRunner.cs`, immediately after `promptLock?.Dispose()` inside the `dialog.FormClosed` handler. That is the narrowest common place to restore/activate `mainForm` for picker cancel without touching `ReadFileCommand` semantics.
- Existing automated coverage does not exercise this focus-recovery path. `ReadFileCommandTests` and `ScriptExecutorControlFlowTests` cover cancel semantics and script exit behavior, while `ScriptReadFileOpenPathDialogTests` cover layout and extension validation only; there is no focused test for `ScriptPromptDialogRunner`, owner activation, or `Form1` focus restoration after a modeless prompt closes.

## 69. Fix foreach expression missing-column warning regression
- [x] 69.1 Update preflight dependency analysis so expression-backed `foreach` collections do not get reported as literal missing column names.
- [x] 69.2 Add focused regression coverage for expression-backed `foreach` collection analysis.
- [x] 69.3 Run targeted verification and capture the review outcome below.

### 69 Review
- Fixed `ScriptDependencyAnalyzer` so `foreach: item in ...` no longer treats the entire collection expression as a bare variable reference. Expression-backed collections now tokenize bare identifiers inside the expression, skip function names and quoted text, and still report real external variables such as `source_services`.
- Added focused regressions proving `compact(matched_services)` no longer shows up as a missing column when `matched_services` is script-defined, while `compact(split(source_services, ','))` still reports `source_services` as an external dependency.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptDependencyAnalyzerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\foreach-missing-column-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\foreach-missing-column-tests\\obj\\` passed (31/31).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\foreach-missing-column-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\foreach-missing-column-build\\obj\\` passed with 0 warnings and 0 errors.

## 68. Implement update-scripting-collection-ergonomics
- [x] 68.1 Add the OpenSpec change artifacts for scripting collection ergonomics and validate the change definition.
- [x] 68.2 Consolidate collection resolution helpers shared by `set`, conditional evaluation, interpolation length access, and `foreach`.
- [x] 68.3 Add `in` / `not in`, structural emptiness/length semantics, and shared read-only collection helper support across expression surfaces.
- [x] 68.4 Add `list`, `compact`, `distinct`, `push_unique`, `trim_all`, `lower_all`, and `upper_all`, preserving additive behavior for existing scripts.
- [x] 68.5 Add focused automated coverage for new operators/helpers, expression-backed `foreach`, and an end-to-end collection-heavy script flow.
- [x] 68.6 Update docs plus at least one bundled sample to use `vars:` YAML lists and the new collection helpers.
- [x] 68.7 Run focused verification, validate the OpenSpec change, and capture the review outcome below.

### 68 Review
- Added OpenSpec change `update-scripting-collection-ergonomics` with proposal, checklist, and spec deltas covering collection membership operators, structural collection semantics, expression-backed `foreach`, and the new collection helpers.
- Consolidated collection-aware value handling in `ValueResolver` so `set`, condition evaluation, interpolation length access, truthiness, emptiness, and `foreach` all resolve lists, JSON arrays/objects, JSON strings, and newline-delimited strings through the same structural rules.
- Extended the expression surface with `list`, `compact`, `distinct`, `push_unique`, `trim_all`, `lower_all`, and `upper_all`, then wired `SetCommand` and `ExpressionEvaluator` through the shared function path so read-only helpers behave consistently across assignments and conditions.
- Added `in` / `not in` with case-insensitive membership by default, updated `foreach` to accept collection expressions such as `split(...)` and `json.items(...)`, and fixed missing bare collection identifiers so they no longer iterate the identifier text itself.
- Added focused automated coverage for the new operators/helpers, structural emptiness/length semantics, expression-backed `foreach`, JSON-array string interpolation/indexing, and an end-to-end collection-heavy script flow.
- Updated `SCRIPTING.md`, refreshed `ScriptSamples\\generic\\portchecker_api_query.yaml` to use the new collection helpers, and added `ScriptSamples\\fortigate\\internet_service_lookup_from_file.yaml` as the benchmark-style sample for the simplified workflow.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExpressionEvaluatorTests|FullyQualifiedName~SetCommandTests|FullyQualifiedName~ForeachCommandTests|FullyQualifiedName~ScriptContextTests|FullyQualifiedName~ScriptExecutorControlFlowTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\collection-ergonomics-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\collection-ergonomics-tests\\obj\\` passed (63/63).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\collection-ergonomics-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\collection-ergonomics-build\\obj\\` passed with 0 warnings and 0 errors.
- Verification: `openspec validate update-scripting-collection-ergonomics --strict --no-interactive` passed.

## 67. Enhance Readfile Picker Options
- [x] 67.1 Extend `readfile` parsing/editor metadata to accept picker message and file-extension restriction options.
- [x] 67.2 Update `ReadFileCommand` and the picker dialog so `select_file` can show a custom message and limit selectable extensions.
- [x] 67.3 Add focused tests for parser acceptance/validation and runtime picker option flow.
- [x] 67.4 Update scripting docs and capture verification results in the review section below.

### 67 Review
- Extended `ReadfileOptions`, `ScriptParser`, and parser-driven editor metadata so `readfile` now accepts `message` plus `fileext`, with `fileext` allowing comma/semicolon/pipe-separated extension lists such as `txt,json`.
- Refactored `ReadFileCommand` to pass a structured picker request into the dialog, substitute variables into the custom message, normalize/validate allowed extensions, and reject resolved paths that do not match the configured file types.
- Updated `ScriptReadFileOpenPathDialog` so the prompt label reflows for longer custom text, the browse dialog applies an extension filter/default extension, and manual path entry is blocked when the extension does not match the allowlist.
- Added focused coverage for parser acceptance, autocomplete suggestions, runtime extension enforcement, and WinForms dialog layout/validation.
- Updated `SCRIPTING.md` plus the active OpenSpec change `add-readfile-file-picker` so the documented/spec’d contract includes custom picker text and file-extension restrictions.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "(FullyQualifiedName~ReadFileCommandTests|FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~ScriptReadFileOpenPathDialogTests)" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\readfile-picker-custom-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\readfile-picker-custom-tests\\obj\\` passed (167/167).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\readfile-picker-custom-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\readfile-picker-custom-build\\obj\\` passed with 0 warnings and 0 errors.
- Verification: `openspec validate add-readfile-file-picker --strict --no-interactive` passed.
- Manual interactive smoke testing of the real picker from the app UI was not run from this CLI environment.

## 66. Stop Script On Readfile Picker Cancel
- [x] 66.1 Change `readfile.select_file` cancel behavior to stop the script immediately instead of returning a normal step failure.
- [x] 66.2 Update focused tests and docs/spec text so cancel semantics match the runtime behavior.
- [x] 66.3 Run focused verification and capture the review below.

### 66 Review
- Changed `ReadFileCommand` so user-canceling the `select_file` picker now returns `CommandResult.Exit(ScriptExitStatus.Cancelled, ...)` after setting `into` to an empty list, which makes the script stop immediately and ignores `on_error: continue` for that path.
- Left the scheduler/manual-only blocked path unchanged: it still returns a normal step failure or suppressed failure with the manual-only error message.
- Updated `ReadFileCommandTests` to assert cancelled exit semantics for picker cancellation, including the `on_error: continue` case, and added `ScriptExecutorControlFlowTests.ExecuteAsync_ReadfilePickerCancel_StopsScriptImmediately` to prove later steps do not run.
- Updated `SCRIPTING.md`, `CHANGELOG.md`, and the active OpenSpec runtime delta so the documented cancel behavior now matches the runtime.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "(FullyQualifiedName~ReadFileCommandTests|FullyQualifiedName~ScriptExecutorControlFlowTests)" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\readfile-cancel-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\readfile-cancel-tests\\obj\\` passed (15/15).
- Verification: `openspec validate add-readfile-file-picker --strict --no-interactive` passed.
- Verification: `dotnet build .\\SSH_Helper.sln` was blocked because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 193740).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\readfile-cancel-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\readfile-cancel-build\\obj\\` passed with 0 warnings and 0 errors.

## 65. Add Readfile File Picker
- [x] 65.1 Add OpenSpec change `add-readfile-file-picker` with proposal, checklist, and spec deltas for runtime and validation behavior.
- [x] 65.2 Extend scripting models, parser metadata, validation, and editor autocomplete support for `readfile.select_file`.
- [x] 65.3 Implement manual-only `readfile` file-picker prompting with seeded path support and scheduler blocking.
- [x] 65.4 Thread the manual-only file-selection policy through script execution contexts and scheduler execution entry points.
- [x] 65.5 Add focused automated coverage for parser, command behavior, autocomplete, and scheduler blocking.
- [x] 65.6 Run focused verification, validate the OpenSpec change, and capture the review below.

### 65 Review
- Added OpenSpec change `add-readfile-file-picker` with proposal, checklist, and spec deltas covering `readfile.select_file` runtime behavior plus the conditional `path` validation rule.
- Extended `ReadfileOptions`, `ScriptParser`, and parser-driven editor metadata so `readfile` now accepts `select_file`, only requires `path` when picker mode is off, and suggests `true`/`false` in autocomplete.
- Refactored `ReadFileCommand` to support an injectable file-picker callback, a themed `ScriptReadFileOpenPathDialog`, seeded picker paths, manual-only scheduler blocking, and empty-list handling for cancel/blocked flows while preserving the existing direct-path read behavior.
- Threaded `AllowFileSelectionDialogs` through `ScriptContext` and `SshExecutionService`, then forced it off from `JobExecutionService` for both scheduler timer runs and Job List `Run Now`.
- Added focused tests for parser validation, readfile picker behavior, autocomplete suggestions, and scheduler failure paths for custom preset jobs using `readfile.select_file`.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "(FullyQualifiedName~ReadFileCommandTests|FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~SshExecutionServiceCancellationTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests)" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\readfile-picker-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\readfile-picker-tests\\obj\\` passed (222/222).
- Verification: `openspec validate add-readfile-file-picker --strict --no-interactive` passed.
- Verification: `dotnet build .\\SSH_Helper.sln` passed with 0 warnings and 0 errors.
- Remaining gap: the real WinForms picker interaction itself was not manually exercised from this CLI-only environment.

## 64. Inspect Script Prompt Execution Contexts
- [x] 64.1 Trace the concrete execution paths for manual preset runs, folder runs, scheduler jobs, and local-only scripts that can reach script prompt dialogs.
- [x] 64.2 Inspect the prompt-dialog runtime (`ScriptPromptDialogRunner` plus prompt commands) for UI-thread marshaling, owner selection, disabled-owner behavior, and cancellation handling.
- [x] 64.3 Review only the focused tests/docs that cover these paths, then capture the concrete file/method summary in the review section below.

### 64 Review
- Manual preset runs enter `Form1.ExecutePresetOnRowsAsync(...)`. Single-host runs go through `ExecutionCoordinator.ExecutePresetAsync(...)` -> `SshExecutionService.ExecutePresetAsync(...)`; multi-host runs (`ExecutionDialogPolicy.ShouldPromptForPresetExecutionOptions(hostCount > 1)`) are rerouted through `SshExecutionService.ExecuteFolderAsync(...)` with a single-preset dictionary. YAML prompt steps (`input`/`choose`/`multiselect`/`confirm`) are dispatched inside `ScriptExecutor.ExecuteAsync(...)` via the normal command table, so direct preset runs and prompt steps share the same runtime as other script commands.
- Folder runs enter `Form1.ExecuteFolderWithOptionsAsync(...)` -> `SshExecutionService.ExecuteFolderAsync(...)` -> `ExecutePresetOnHostAsync(...)` -> `ExecuteScriptTextOnHost(...)` / `ExecuteScriptOnHost(...)`. Prompt steps are allowed on folder runs; only `interactive` steps are blocked (`Form1.ValidateFolderInteractiveRestrictions(...)` plus `SshExecutionService.FindInteractiveFolderPresets(...)`). Because folder execution batches hosts by `ParallelHostCount` and can also run presets in parallel, prompt dialogs can be reached concurrently from multiple background script tasks.
- Scheduler jobs enter from `JobListDialog.OnRunNowClick(...)` -> injected `Form1.RunTrackedJobNowAsync(...)` -> `JobExecutionService.RunNowAsync(...)`, while scheduled timer jobs enter `JobExecutionService.TimerCallback(...)` -> `ExecuteScheduledJobAsync(...)`. Both converge on `ExecuteJobCoreAsync(...)`, which creates a dedicated per-job `SshExecutionService` and dispatches either `ExecuteSinglePresetAsync(...)` (`sshService.ExecutePresetAsync(...)`) or `ExecuteFolderJobAsync(...)` (`sshService.ExecuteFolderAsync(...)`). Scheduler folder jobs inherit folder prompt behavior, but `JobExecutionService` leaves `FolderExecutionOptions.ParallelHostCount` at the model default `1`, so scheduler folder jobs do not fan out hosts in parallel unless that code changes.
- Local-only scripts are identified by `ScriptDependencyAnalyzer.AnalyzeSshRequirements(...)`: only `send` and `interactive` force `RequiresSshSession = true`; prompt commands do not. `SshExecutionService.ExecuteScriptOnHost(...)` routes `!RequiresSshSession` scripts into `ExecuteScriptLocal(...)`, which sets `context.Session = null` but still runs the same `ScriptExecutor` and therefore the same prompt commands. The local path changes transport only: no SSH connect/login, `LOCAL SCRIPT` banner, same output/column/environment hooks, same cancellation token.
- `ScriptPromptDialogRunner.ShowAsync<TDialog, TResult>(...)` is the sole dialog launcher for `input`, `choose`, `multiselect`, `confirm` (and the relative-path `writefile` save-path prompt). It grabs `Application.OpenForms[0]` as the owner (normally `Form1` from `Program.Main()`), marshals to that form's UI thread with `BeginInvoke(...)` when needed, shows the prompt modeless with `dialog.Show(mainForm)`, centers it on the main form, and wires dialog-result buttons manually because modeless forms do not auto-close like `ShowDialog(...)`.
- While a prompt is open, `MainFormPromptLock.TryAcquire(mainForm)` disables only the main form's control tree and explicitly preserves the `btnStopAll` ancestor chain. That means manual runs keep the Stop button usable, but modeless secondary windows such as `JobListDialog` are not part of the disabled control tree because the lock only walks `mainForm.Controls`.
- Cancellation behavior splits cleanly by source. User-cancel inside `input`/`choose`/`multiselect` returns `null`, logs a warning, and fails the step unless `on_error: continue`; `confirm` instead stores `"false"` on No/Cancel/Escape and does not fail. Execution cancellation (`Form1.StopExecution()` -> `_sshService.Stop()` or `JobExecutionService.CancelJob(...)` -> tracked CTS -> `sshService.Stop()`) closes any active prompt through `ScriptPromptDialogRunner.RegisterCancellation(...)`, causes `ShowAsync(...)` to complete as cancelled, and ultimately marks the host/job result cancelled through `ScriptExecutor`, `EnsureScriptSucceeded(...)`, and `ExecutionResult.WasCancelled`.
- Focused docs/tests reviewed: `CHANGELOG.md` documents modeless prompt dialogs and local-only routing; `SCRIPTING.md` documents per-command cancel semantics; `ScriptDependencyAnalyzerTests` covers prompt commands as non-SSH/local-compatible and `interactive` as SSH-only; `SshExecutionServiceInteractivePreflightTests` covers folder/multi-host `interactive` blocking; `SshExecutionServiceCancellationTests` covers local-script and folder cancellation; `JobExecutionServiceTests` covers custom-preset resolution and scheduled custom-script cancellation. There is no direct automated coverage for `ScriptPromptDialogRunner` owner selection, main-form locking, or multi-dialog concurrency.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "(FullyQualifiedName~SshExecutionServiceCancellationTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~JobExecutionServiceTests.CancelJob_ScheduledExecution_CustomPresetScript_PublishesCancelledResult|FullyQualifiedName~JobExecutionServiceTests.ResolvePresetForExecution_CustomPreset_ReturnsTransientPresetInfo|FullyQualifiedName~JobExecutionServiceTests.RunNowAsync_FolderJob_RespectsSequentialMode|FullyQualifiedName~JobExecutionServiceTests.RunNowAsync_FolderJob_RespectsParallelMode)" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\prompt-exec-review-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\prompt-exec-review-tests\\obj\\` passed (39/39).

## 63. Inspect Popup File-Picker Constraints For Script Commands
- [x] 63.1 Trace the `ScriptPromptDialogRunner`, `ReadFileCommand`, and `WriteFileCommand` flow to identify existing interactive-command guards and prompt contracts.
- [x] 63.2 Inspect scripting validation, dependency analysis, and local/scheduler execution paths for policies that would constrain a popup file-open picker.
- [x] 63.3 Review focused tests, specs, and docs covering interactive commands and file commands, then capture concrete findings and risks in the review section below.

### 63 Review
- `Services/Scripting/Commands/ScriptPromptDialogRunner.cs` centralizes script UI prompts through `ShowAsync<TDialog, TResult>()`, marshals dialog creation onto `Application.OpenForms[0]`, and only applies the main-form lock when it can find `btnStopAll`. That means prompt-capable commands can run from background execution paths, but the safety contract is UI-thread marshalling plus best-effort disabling of the main form, not a scheduler/manual execution policy gate.
- `Services/Scripting/Commands/ReadFileCommand.cs` stays non-interactive today: `ExecuteAsync()` requires `readfile.path` and `readfile.into`, expands script/env variables, then immediately calls `ScriptFileAccessValidator.ValidateReadPath(...)`. It never prompts and it never checks `Path.IsPathFullyQualified`, so relative read paths currently resolve via `Path.GetFullPath(...)` inside the validator/runtime instead of forcing a picker.
- `Services/Scripting/Commands/WriteFileCommand.cs` is the existing precedent for prompt-driven file selection. `ResolveFilePathAsync()` prompts only when the path is not fully qualified, `PromptForSavePathAsync()` routes through `ScriptPromptDialogRunner`, and `ScriptWriteFileSavePathDialog.BrowseForPath()` uses a real `SaveFileDialog`. Validation still happens after prompt resolution via `ValidateWritePath(...)`, and successful writes set `_writefile`.
- `Services/Scripting/ScriptParser.cs` is the main shape-policy surface. `CommandOptionKeys`, `ParseReadfileOptions()`, `ParseWritefileOptions()`, and `Validate()` only know the current fixed keys for `readfile`/`writefile`. Adding a picker flag or picker-specific options would require parser, validation, docs, and editor-surface updates; otherwise the editor/runtime will warn or error on unknown keys. `Services/Editor/ScriptEditorValidationService.cs`, `Services/Editor/ScriptAutocompleteProvider.cs`, and `Services/Editor/YamlSshSyntaxHighlighter.cs` all derive their command metadata from `ScriptParser`.
- `Services/Scripting/ScriptDependencyAnalyzer.cs` tracks `readfile`/`writefile` variable references in `AnalyzeSteps(...)`, but `AnalyzeSshRequirementsInSteps(...)` marks only `StepType.Interactive` as `UsesInteractive`. Existing multi-host/folder/scheduler preflight therefore ignores prompt-capable `writefile`, and a future popup-enabled `readfile` would also bypass those guards unless the analyzer and consumers are widened intentionally.
- Manual and scheduler execution both consume that narrow `UsesInteractive` signal. `Services/SshExecutionService.cs` blocks only `interactive` scripts in `ExecuteScriptAsync(...)`, `ExecuteFolderAsync(...)`, and `ExecuteScriptTextOnHost(...)`; `Form1.cs` mirrors that in `ValidateFolderInteractiveRestrictions()` / `GetInteractiveFolderPresetNames()`. `Services/JobExecutionService.cs` runs scheduled jobs on a 30-second `System.Threading.Timer` ThreadPool callback and uses a dedicated `SshExecutionService` per job, so a popup picker inside `readfile`/`writefile` would marshal back to the main form from a background scheduled run and could stall a running job or consume a concurrency slot until the dialog is answered.
- The biggest behavior risk is multiplicity: script execution is per-host. A popup-enabled `readfile` would likely fire once per host/preset execution unless the selected path is cached above `ScriptContext`, and current scheduler/manual multi-host preflight would not stop that because it only understands `interactive`.
- Focused coverage/docs are asymmetric. `SSH_Helper.Tests/Scripting/WriteFileCommandTests.cs` covers relative-path prompt injection, cancel behavior, and `_writefile`; `SSH_Helper.Tests/Scripting/ReadFileCommandTests.cs` has only a single env-var read test; `SSH_Helper.Tests/Services/SshExecutionServiceInteractivePreflightTests.cs` covers only `interactive`-step blocking; `SSH_Helper.Tests/Scripting/ScriptParserTests.cs` covers unknown `readfile` keys and strict `interactive` validation. There are no focused tests for `ScriptPromptDialogRunner` itself, no actual WinForms file-dialog tests, and `SCRIPTING.md` documents runtime prompting only for `writefile`, not for `readfile` or scheduler/background prompt implications.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~WriteFileCommandTests|FullyQualifiedName~ReadFileCommandTests|FullyQualifiedName~ScriptDependencyAnalyzerTests|FullyQualifiedName~SshExecutionServiceInteractivePreflightTests|FullyQualifiedName~ScriptParserTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\popup-picker-inspection-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\popup-picker-inspection-tests\\obj\\` passed (165/165).

## 62. Fix Scheduler Job Right-Click Selection
- [x] 62.1 Inspect the `JobListDialog` job-grid right-click and context-menu flow, and confirm the smallest safe hook for row selection before menu open.
- [x] 62.2 Update the scheduler jobs grid so right-clicking a non-selected job row selects that row before the context menu opens, without changing empty-space behavior.
- [x] 62.3 Add focused WinForms regression coverage for the right-click selection path and run targeted verification.
- [x] 62.4 Capture the root cause, fix, and verification notes in the review section below.

### 62 Review
- Root cause confirmed in `JobListDialog`: the jobs grid kept using the previously selected row for scheduler actions because WinForms `DataGridView` does not automatically change row selection on right-click before opening the attached context menu.
- Patched `JobListDialog` to handle `_gridJobs.CellMouseDown` on right-click and route clicked-row activation through a shared `SelectJobRowAt(...)` helper, which also keeps the checkbox-toggle path aligned with the same active-row selection logic.
- Added a focused WinForms regression in `JobListDialogRunNowTests` that starts with one job selected, simulates a right-click on a different row, and asserts the subsequent `Run Now` action uses the clicked job ID instead of the stale selection.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialogRunNowTests"` initially failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 163356).
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialogRunNowTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\joblist-rightclick-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\joblist-rightclick-tests\\obj\\` passed (18/18).
- Verification: `dotnet build .\\SSH_Helper.sln` passed with 0 warnings and 0 errors.

## 61. Inspect Checkbox Toggle Reference Patterns
- [x] 61.1 Locate the concrete WinForms `DataGridView` checkbox-toggle handlers and event wiring relevant to click-to-toggle behavior.
- [x] 61.2 Check git history and current worktree for the edit patterns that introduced or changed those handlers.
- [x] 61.3 Capture concise review notes below and return only the relevant references.

### 61 Review
- Relevant committed patterns: `Form1` commit `38ca71f` adds a checkbox column plus `CellClick` manual toggle and `CurrentCellDirtyStateChanged`/`CommitEdit` immediate-commit handling; `ImportPreviewDialog` commit `4a0e585` uses `EditOnEnter` with `CurrentCellDirtyStateChanged`/`CommitEdit` so checkbox clicks take effect immediately.
- Relevant current worktree pattern: `JobListDialog` keeps the `Enabled` checkbox column read-only and adds `CellContentClick` to route the click into a shared `ToggleJobEnabled(...)` helper that saves and refreshes the grid; the local matching test invokes `OnJobGridCellContentClick(...)` directly.
- Source inspection only; no production code behavior was changed for this task.

## 60. Inspect DataGridView Checkbox Test Simulation
- [x] 60.1 Search the test project for `DataGridView` checkbox interactions, click simulation, and commit/edit handling.
- [x] 60.2 Classify each relevant test path as true click simulation, edit/commit flow simulation, or direct checkbox cell value assignment.
- [x] 60.3 Capture concise file/method references and findings in the review section below.

### 60 Review
- No test in `SSH_Helper.Tests` performs a true `DataGridView` checkbox click simulation, and no test drives the checkbox edit/commit pipeline (`CommitEdit`, `CurrentCellDirtyStateChanged`, `BeginEdit`, `EndEdit`, `NotifyCurrentCellDirty`, and editing-control hooks were not found in the test project).
- `SSH_Helper.Tests/UI/JobListDialogRunNowTests.cs` `EnabledCheckboxClick_TogglesJobAndRefreshesGrid()` exercises the checkbox-toggle path by directly invoking `OnJobGridCellContentClick(...)` with a `DataGridViewCellEventArgs`; this is handler-path simulation, not a real UI click and not an edit/commit-flow simulation.
- `SSH_Helper.Tests/UI/HostGridUtilitiesTests.cs` `BuildSchedulerCopySnapshot_WhenCheckedRowsExist_UsesOnlyCheckedEligibleRows()` uses a `DataGridViewCheckBoxColumn` but sets checkbox state via direct cell assignment (`Cells[0].Value = true/false`), not by clicking or committing an edit.
- `SSH_Helper.Tests/UI/HostGridUtilitiesTests.cs` `BuildSnapshot_FromDataGridView_UsesDisplayOrderAndExcludesSelectionColumn()` includes a checkbox column only to verify snapshot/export behavior; it does not simulate checkbox interaction and only assigns text-cell values.
- Verification: source inspection only; no test execution was required for this task.

## 59. Enable Scheduler Toggle By Checkbox
- [x] 59.1 Confirm why the Scheduled Jobs `On` checkbox is not clickable and identify the smallest safe edit path in `JobListDialog`.
- [x] 59.2 Make the `On` checkbox toggle the selected job enabled state through the existing save/refresh flow.
- [x] 59.3 Add focused WinForms regression coverage for checkbox-driven enable/disable behavior and run verification.
- [x] 59.4 Capture the fix and verification notes in the review section below.

### 59 Review
- Root cause confirmed in `JobListDialog`: the `On` column was rendered as a checkbox but nothing listened for checkbox clicks, so the only enable/disable path was the toolbar/context-menu command.
- Patched `JobListDialog` to handle `CellContentClick` on the `Enabled` column, pin the clicked row as the active selection, and route both checkbox clicks and the toolbar command through a shared `ToggleJobEnabled(...)` helper.
- Added a focused WinForms regression in `JobListDialogRunNowTests` that invokes the checkbox click handler, then asserts the persisted job state flips to disabled and the refreshed grid still shows the same row selected with the checkbox cleared.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialogRunNowTests"` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 161468).
- Verification: `dotnet build .\\SSH_Helper.sln` failed for the same locked `bin\\Debug\\net8.0-windows\\SSH_Helper.exe`.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialogRunNowTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\joblist-checkbox-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\joblist-checkbox-tests\\obj\\` passed (17/17).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\joblist-checkbox-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\joblist-checkbox-build\\obj\\` passed with 0 warnings and 0 errors.

## 58. Fix Custom Preset General-Tab Overlap
- [x] 58.1 Inspect the `JobEditorDialog` general-tab layout path for the custom preset target and confirm why the help text overlaps the schedule controls.
- [x] 58.2 Patch the general-tab layout so target-mode changes and tab resize reflow the schedule row and schedule panels below the custom preset help text.
- [x] 58.3 Add focused WinForms regression coverage for the custom preset help-text spacing and run verification.
- [x] 58.4 Capture the fix and verification notes in the review section below.

### 58 Review
- The overlap came from the general tab still reserving the original single-row target height (`yPos += 32`) after swapping in the multi-line custom preset help label, so the schedule row stayed fixed at the old location and rendered underneath the label.
- Patched `JobEditorDialog` to keep the schedule label as a field and recalculate the general-tab vertical layout whenever the target type changes, the schedule mode changes, or the general tab resizes. The custom preset help label now measures its wrapped height and the schedule row/panels are repositioned beneath it.
- Added a focused WinForms regression in `JobEditorDialogLayoutTests` that opens the dialog, switches to `Custom Preset`, and asserts the help label ends above both the schedule label and schedule combo.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobEditorDialogLayoutTests|FullyQualifiedName~JobEditorDialogCustomPresetTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\custom-preset-layout-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\custom-preset-layout-tests\\obj\\` passed (5/5).
- Verification: `dotnet build .\\SSH_Helper.sln` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 138632).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\custom-preset-layout-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\custom-preset-layout-build\\obj\\` passed with 0 warnings and 0 errors.

## 57. Add Scheduler-Local Custom Presets
- [x] 57.1 Add the OpenSpec change artifacts for scheduler-local custom presets and validate the new requirement delta.
- [x] 57.2 Extend scheduler job models, persistence, target display, and execution to support a custom preset job target with job-owned content.
- [x] 57.3 Add `Custom Preset` authoring and validation to `JobEditorDialog` using the existing script editor stack.
- [x] 57.4 Add focused automated coverage for custom preset model, storage/export, editor, and execution behavior.
- [x] 57.5 Run verification and capture the review below.

### 57 Review
- Added OpenSpec change `add-scheduler-custom-presets` with proposal, checklist, and `job-scheduler` delta covering save, execution, and import/export behavior for scheduler-local custom presets.
- Extended `JobDefinition` with `JobTargetType.CustomPreset` and normalized `CustomPresetCommands` storage so scheduler jobs can persist their own command or YAML content without referencing the shared preset tree.
- Updated scheduler execution to materialize custom job content as a transient `PresetInfo`, preserving the existing command-vs-script detection, runtime validation, cancellation, and interactive-script preflight while using the application default timeout for custom jobs.
- Added a dedicated Content tab to `JobEditorDialog` with the existing Scintilla editor stack, `Custom Preset` target selection, blank-content validation, and scheduler-local authoring hints while leaving preset/folder flows intact.
- Updated scheduler list/import flows so custom preset jobs display `[Custom] Scheduler-local content` and are never treated as missing preset or folder targets.
- Added focused regression coverage for model defaults, storage/export round-trip, import-state utilities, custom preset validation, custom preset dialog save/reload behavior, timeout fallback, transient preset resolution, and custom-script cancellation on the real scheduler execution path.
- Verification: `openspec validate add-scheduler-custom-presets --strict --no-interactive` passed.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobDefinitionTests|FullyQualifiedName~SchedulerJobIntegrityUtilitiesTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~JobEditorDialogCustomPresetTests|FullyQualifiedName~JobStorageServiceTests|FullyQualifiedName~JobExportServiceTests|FullyQualifiedName~JobExecutionServiceTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\custom-preset-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\custom-preset-tests\\obj\\` passed (159/159).
- Verification: `dotnet build .\\SSH_Helper.sln -p:UseAppHost=false -p:BaseOutputPath=artifacts\\custom-preset-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\custom-preset-build\\obj\\` passed with 0 warnings and 0 errors.

## 56. Fix Shell Echo Duplicate-Character Artifact
- [x] 56.1 Trace the `df -> ddf` shell echo artifact through `SshShellSession` and confirm whether the duplicate character comes from incremental transcript rendering rather than duplicate command sends.
- [x] 56.2 Patch the live shell-output path so unfinished editable lines are buffered until they are stable, allowing backspaces/carriage returns to resolve before appending to the UI/history stream.
- [x] 56.3 Add focused regression coverage for a shell chunk sequence like `d\bdf\r\r\n...` and run targeted verification.
- [x] 56.4 Capture the root cause, fix, and verification notes in the review section below.

### 56 Review
- The raw shell data already showed the real behavior: the command was sent once as `df`, and the remote PTY echoed an inline edit sequence (`d\bdf\r\r\n`) rather than a literal duplicated command send.
- The bug was in live rendering, not command dispatch. `SshShellSession.ProcessChunk(...)` normalized and emitted each processed chunk immediately, which works for complete lines but can leak partially edited shell-echo text into the append-only UI before later backspaces/carriage returns have finished rewriting that line.
- Patched `SshShellSession` to keep an in-memory `pendingLineCarry` for the unfinished final line, emit only newline-complete stable text during streaming, and flush the remaining tail only when the command completes. That lets sequences such as `d\bdf\r\r\n` normalize to `df` before they ever reach the UI/history stream.
- Added `TerminalOutputProcessor.BufferIncompleteFinalLineStreaming(...)` plus focused regression tests covering both split-chunk and single-chunk `d\bdf\r\r\n` command-echo cases.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~TerminalOutputProcessorTests" -p:BaseOutputPath=artifacts\\shell-echo-fix-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\shell-echo-fix-tests\\obj\\` passed (53/53).
- Verification: `dotnet build .\\SSH_Helper.csproj -p:BaseOutputPath=artifacts\\shell-echo-fix-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\shell-echo-fix-build\\obj\\` passed with 0 warnings and 0 errors.

## 55. Inspect Manual Single-Preset Cancellation
- [x] 55.1 Trace the manual `Stop` path in `Form1.cs` for a single active preset run, including UI state transitions and history persistence.
- [x] 55.2 Trace cancellation propagation in `Services/SshExecutionService.cs`, including token flow, cancellation checks, and final per-host results.
- [x] 55.3 Reconcile actual cancellation behavior against the final status/history/output the user sees and capture the review below.

### 55 Review
- `Form1` runs single-preset execution through the shared `_sshService` instance (`_executionCoordinator = new ExecutionCoordinator(_sshService, _configService)`), and the Stop button just calls `StopExecution()`, which disables the button, changes it to `Stopping...`, updates the status bar to `Stopping execution...`, calls `_sshService.Stop()`, and appends `Execution Stopped by User` to the live output immediately.
- The cancellation signal does propagate into `SshExecutionService`: `BeginExecution()` creates `_cts`, `Stop()` cancels that same `_cts`, and the token is checked by the single-preset host loops before launching additional hosts. The active host path also receives that token in `ExecuteSingleHost(...)` / `ExecuteScriptOnHost(...)`, and token-aware stages pass it into `session.InitializeAsync(...)`, `session.ExecuteBatchAsync(...)`, or `ScriptExecutor.ExecuteAsync(...)`.
- Cancellation is cooperative, not a hard abort. In the non-pooled path, `client.Connect(...)` and `client.Login(...)` do not take the token, so Stop can lag until the current host gets past connect/login and reaches a token-aware stage.
- When the token is finally observed, the service catches `OperationCanceledException` and converts it into a normal `ExecutionResult` with `Success = false` and `ErrorMessage = "Operation cancelled"` instead of rethrowing. `Form1.ExecutePresetOnRowsAsync(...)` then treats the run as a normal completion path: it still builds execution details, stores history, and overwrites the temporary `Stopping execution...` status with the normal completion text.
- The user-visible result therefore mismatches the actual cancel: the live output pane shows `Execution Stopped by User`, but the final status bar says `Completed execution ...`, the history entry label is just timestamp + preset name with no cancelled state, and the host row is stored/rendered as a generic failure (`Success = false`, red X) rather than a distinct cancelled outcome. The overall history output defaults to the live output buffer, while selecting the host row shows the host-specific stored output containing the formatted `CANCELLED` block.
- Source inspection only; no code changes or test runs were performed for this review task.

## 54. Inspect Folder Execution Cancellation
- [x] 54.1 Trace the folder-run stop flow in `Form1.cs`, including button handling, status/output updates, and post-run reporting.
- [x] 54.2 Trace `SshExecutionService.ExecuteFolderAsync(...)` for both sequential and parallel folder modes, focusing on stop responsiveness and cancellation boundaries.
- [x] 54.3 Summarize the final user-visible/history outcome with exact file references, then capture the review below.

### 54 Review
- `Form1.StopExecution()` only gives immediate UI feedback: it disables the button, changes it to `Stopping...`, updates the status bar to `Stopping execution...`, and appends `Execution Stopped by User` to the live output pane before calling `_sshService.Stop()`.
- `SshExecutionService.Stop()` only cancels the current `_cts`; it does not force-abort running tasks. In `ExecuteFolderAsync(...)`, cancellation is cooperative: the outer host-batch loop stops launching later batches, sequential preset mode stops before the next preset, and parallel preset mode only prevents preset tasks that have not yet started real work. Already-running preset executions continue until their inner SSH/script path notices the token.
- Promptness is therefore mixed. Sequential folder mode is reasonably prompt between presets/hosts, but not necessarily during a synchronous connect/login segment. Parallel folder mode is less prompt because the current host batch and any already-started preset tasks are still awaited with `Task.WhenAll(...)`.
- Folder cancellation is not surfaced as a dedicated cancelled outcome. `ExecuteFolderWithOptionsAsync(...)` always stores a normal folder history entry and then reports either `Completed folder ...` or `X succeeded, Y failed`; there is no cancellation-specific status or history label.
- Persisted folder history is built only from returned `ExecutionResult` objects, not from the live output pane text. That means the manual `Execution Stopped by User` banner is visible live but is not itself what gets stored in folder history. If an in-flight preset catches `OperationCanceledException`, its `ExecutionResult` is marked failed with `Operation cancelled` and the cancel text goes into that host's stored output.
- Source inspection only; no code changes or test runs were performed for this review task.

## 53. Inspect Scheduled Job Cancellation
- [x] 53.1 Trace the user-facing scheduled-job UI in `Form1.cs` and `JobListDialog.cs` to confirm whether a running scheduled job can be cancelled by a user action.
- [x] 53.2 Trace `Services/JobExecutionService.cs` and `Services/SshExecutionService.cs` to confirm how internal `CancelJob(...)` affects the real SSH execution path and final reported result.
- [x] 53.3 Inspect focused tests for scheduled-job cancellation coverage, then capture the concrete answer and any gaps in the review below.

### 53 Review
- There is no user-facing scheduled-job cancel action in the inspected UI. `Form1.ShowJobListDialog()` passes only `RunTrackedJobNowAsync` into `JobListDialog`, and the dialog exposes `Run Now`, enable/disable, delete, duplicate, and import/export actions, but no stop/cancel command or shortcut.
- `JobListDialog` does refresh running state and color running jobs green through `_executionService.IsJobRunning(job.Id)`, so users can see that a scheduled job is active, but they cannot cancel it from that dialog while the app remains open.
- `CancelJob(jobId)` does cancel the tracked per-job `CancellationTokenSource`, and `ExecuteJobCoreAsync(...)` registers that token to call `sshService.Stop()` on the per-run `SshExecutionService`.
- On the real SSH path, cancellation is converted into failed host results, not a propagated `OperationCanceledException`: `SshExecutionService` catches `OperationCanceledException`, sets `Success = false`, and records `ErrorMessage = "Operation cancelled"` / `CANCELLED` output per host.
- `JobExecutionService.ExecuteJobCoreAsync(...)` then aggregates those returned host results into a `JobRunResult` with `Success = false` and raises `JobExecutionState.Failed`, so the scheduler/history UI surfaces the run as failure (`FAIL`), not as `Cancelled`.
- Focused tests cover the internal token-plumbing path with an injected execution override that throws `OperationCanceledException`, and those tests assert `JobExecutionState.Cancelled` for both run-now and scheduled execution. They do not cover the concrete `SshExecutionService` path, no UI test exercises a user cancel action for scheduled jobs, and no test asserts how a cancelled scheduled SSH run is recorded in persisted history/UI.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobListDialogRunNowTests" -p:BaseOutputPath=artifacts\\scheduled-cancel-review-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduled-cancel-review-tests\\obj\\` passed (58/58).

## 52. Fix Scheduler Reliability Shutdown and Evaluation Faults
- [x] 52.1 Harden `JobExecutionService` shutdown so scheduled background tasks cannot release or reacquire the concurrency gate after disposal, and queued jobs do not start during shutdown.
- [x] 52.2 Clean up the scheduler cancellation contract by removing the unused folder-execution token parameter while keeping the existing `sshService.Stop()` cancellation model.
- [x] 52.3 Make the evaluation loop resilient to per-job failures, add explicit scheduler fault logging, and remove the dead async-completion shim.
- [x] 52.4 Add focused regression coverage for shutdown races and evaluation-fault isolation, then run verification and capture the review below.

### 52 Review
- `JobExecutionService` now gates scheduler shutdown with an explicit `_shutdownRequested` flag, tracks fire-and-forget scheduled executions, routes semaphore access through shutdown-aware helpers, and waits briefly for tracked scheduled tasks before disposing scheduler-owned resources.
- Scheduled queue draining now exits during shutdown, and late-finishing scheduled tasks no longer touch the concurrency gate after disposal begins. This closes the `Dispose()` race and prevents queued jobs from starting while the form is shutting down.
- The private folder execution helper no longer accepts an unused `CancellationToken`, and the scheduler execution comments now state the real cancellation model: both single-preset and folder jobs cancel through `sshService.Stop()` on the per-run `SshExecutionService`.
- The evaluation loop no longer uses the dummy `await Task.CompletedTask` shim. It now returns `Task.CompletedTask` directly, isolates per-job failures with job/stage-aware debug logging, and keeps the reentrancy guard reset in `finally`.
- Added focused `JobExecutionServiceTests` coverage for disposing an in-flight scheduled job without semaphore-disposal faults, preventing queued jobs from starting after shutdown begins, continuing evaluation after a synthetic per-job evaluation fault, and clearing `_evaluating` after injected evaluation exceptions.
- Verification: `dotnet build .\\SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 8316).
- Verification: `dotnet build .\\SSH_Helper.csproj -p:BaseOutputPath=artifacts\\scheduler-reliability-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-reliability-build\\obj\\` passed with 0 warnings and 0 errors.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~SchedulerNotificationTests" -p:BaseOutputPath=artifacts\\scheduler-reliability-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-reliability-tests\\obj\\` passed (59/59).

## 51. Fix Scheduler Per-Job Cancellation
- [x] 51.1 Patch `JobExecutionService` so run-now and scheduled executions pass the per-job cancellation token into the execution pipeline instead of the disposal-only token.
- [x] 51.2 Add focused regression coverage proving `CancelJob(...)` now reaches the active job execution path.
- [x] 51.3 Run focused verification and capture the review below.

### 51 Review
- `JobExecutionService` now routes both run-now and scheduled execution through a shared tracked-job helper that resolves the active job's own `CancellationTokenSource` token, so `CancelJob(jobId)` cancels the token the running execution is actually listening to instead of only the service-disposal token.
- Added a narrow internal execution override seam for tests and used it to block on `Task.Delay(..., token)` until cancellation, which lets the tests prove that both run-now and scheduled execution paths observe per-job cancellation and emit `Cancelled`.
- Verification: `dotnet build .\\SSH_Helper.csproj` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 31936).
- Verification: `dotnet build .\\SSH_Helper.csproj -p:BaseOutputPath=artifacts\\job-cancel-fix-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\job-cancel-fix-build\\obj\\` passed with 0 warnings and 0 errors.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecutionServiceTests" -p:BaseOutputPath=artifacts\\job-cancel-fix-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\job-cancel-fix-tests\\obj\\` passed (40/40).

## 50. Review Approval-Ready Runtime Bugs
- [x] 50.1 Inspect current runtime code for concrete defects that are still present after the recent scheduler/UI fixes.
- [x] 50.2 Validate two approval-ready bugs with exact file/line references and current behavior impact.
- [x] 50.3 Present the findings for approval and capture the review below.

### 50 Review
- Confirmed a pooled-session ownership bug in `Services/SshConnectionPool.cs` and `Services/SshExecutionService.cs`: when the pooled key is already leased, `CreateSessionAsync(...)` falls back to a standalone `Ssh` client, but the callers still always route cleanup through `ReleaseSession(...)`. That unconditionally clears the pooled lease for the host key and never disposes the standalone fallback client, so same-host overlapping pooled runs can both leak extra SSH connections and let a later execution reuse the pooled connection while the original leased session is still active.
- Confirmed a scheduler cancellation bug in `Services/JobExecutionService.cs`: `CancelJob(jobId)` cancels the per-job `RunningJobInfo.Cts`, but both `RunNowAsync(...)` and `ExecuteScheduledJobAsync(...)` pass `_disposalCts.Token` into `ExecuteJobCoreAsync(...)` instead of the per-job token. That means per-job cancellation never reaches the registered `sshService.Stop()` callback, so cancel requests do not actually stop a running job unless the whole service is disposing.
- Verification: source review only; no code changes or automated tests were run for this review task.

## 49. Fix Low-Hanging Scheduler Job List Bugs
- [x] 49.1 Patch job duplication so stored-credential jobs copy their saved credential to the duplicated job ID.
- [x] 49.2 Patch Clear History so the jobs grid refreshes immediately and `Last Result` no longer shows stale data.
- [x] 49.3 Add focused regression coverage for both scheduler job list behaviors.
- [x] 49.4 Run focused verification and capture the review below.

### 49 Review
- `JobListDialog` duplication now routes through a small helper that copies any existing stored credential from the source job's credential-manager target to the duplicate job's new target after the clone is saved.
- If that credential copy fails, the new duplicate job is rolled back immediately so the UI does not leave behind a broken stored-credential clone.
- `Clear History` now routes through `ClearHistoryForJob(...)`, which deletes persisted history and refreshes the jobs grid, so the top `Last Result` column switches back to `Never run` immediately instead of staying stale until a later refresh.
- Added focused WinForms regressions covering both behaviors: duplicating a stored-credential job now preserves the copied secret under the new job ID, and clearing a job's history now empties the history grid while updating `Last Result` in-place.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialogRunNowTests" -p:BaseOutputPath=artifacts\\job-list-low-hanging-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\job-list-low-hanging-tests\\obj\\` passed (14/14).
- Verification: `dotnet build .\\SSH_Helper.csproj` passed with 0 warnings and 0 errors.

## 48. Investigate SSH Reverse-DNS-Like Connect Delay
- [x] 48.1 Trace the SSH connection and login code paths used by normal execution, pooled execution, and interactive terminal sessions.
- [x] 48.2 Verify whether the client performs hostname canonicalization or reverse DNS lookups when connecting to a literal IP address.
- [x] 48.3 If a client-side fix exists, implement and verify it; otherwise capture the root-cause guidance and evidence in the review below.

### 48 Review
- The app connects directly with Rebex `Ssh.Connect(host.IpAddress, host.Port)` in every SSH path: normal execution, pooled execution, and interactive terminal. There is no shell-out to `ssh.exe`, no client-side OpenSSH config canonicalization layer, and no hostname preprocessing beyond storing the host string in `HostConnection.IpAddress`.
- The only SSH config options this app imports are `HostName`, `Port`, `User`, `IdentityFile`, `HostKeyAlgorithms`, and `Ciphers`. There is no app-level support for `CanonicalizeHostname`, `UseDNS`, or any reverse-DNS-related client toggle.
- The current Rebex `SshSettings` surface used by this project also does not expose a reverse-DNS or hostname-canonicalization option. Official Rebex docs for `SshSettings` list authentication, buffering, tunnel, and welcome-message settings, but nothing DNS-related.
- Because the app already passes a literal dotted address straight into the SSH client, a slow connect-by-IP flow is more likely server-side behavior after accept/authentication or post-login shell startup than a client-side reverse lookup inside this repo.
- The repo already has enough debug timing to separate those phases: SSH Debug mode logs `client.Connect()`, `client.Login()`, and `session.InitializeAsync` timing independently. If the delay is during `client.Connect()` or `client.Login()` against an IP, the likely fix is on the SSH server (`sshd_config UseDNS no` where applicable). If the delay is after login during `session.InitializeAsync`, the bottleneck is more likely shell/banner/prompt startup rather than DNS.
- Verification: source review only; no code change was made because there is no client-side reverse-DNS toggle in the current implementation to disable.

## 47. Fix Empty Send Into Variable Evaluation
- [x] 47.1 Trace the `send ... into ...` capture path and confirm how no-output commands populate the target variable.
- [x] 47.2 Patch the null/empty handling so `if: <var> is empty` evaluates safely after a no-output send.
- [x] 47.3 Add focused regression coverage for empty send output captured into a variable and checked with `is empty`.
- [x] 47.4 Run focused verification and capture the review below.

### 47 Review
- Root cause was in `ExtractCommand`, not the `send` capture itself: when the source variable was empty, `ExecuteAsync(...)` emitted a warning and returned before initializing the `into` target variable(s).
- That left follow-up conditions like `if: version is empty` checking an unset variable instead of an explicit empty string, which broke the intended empty-result flow after commands that produced no output.
- Patched `ExtractCommand` so the early empty-source branch now calls `SetEmptyResults(...)` before returning, keeping `into` variables defined and empty.
- Added a focused regression test that sets an empty captured source, runs `extract ... into version`, and verifies both `HasVariable("version")` and `ExpressionEvaluator.Evaluate("version is empty")`.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExtractCommandTests|FullyQualifiedName~ExpressionEvaluatorTests" -p:BaseOutputPath=artifacts\\empty-extract-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\empty-extract-tests\\obj\\` passed (11/11).
- Verification: `dotnet build .\\SSH_Helper.csproj -p:BaseOutputPath=artifacts\\empty-extract-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\empty-extract-build\\obj\\` passed with 0 warnings and 0 errors.

## 46. Refine Hosts Unsaved Indicator
- [x] 46.1 Update the Hosts header so `unsaved` only appears for CSV-backed grids when the current grid actually differs from the CSV-backed snapshot.
- [x] 46.2 Preserve existing `disk changed` and `missing on disk` indicator behavior.
- [x] 46.3 Add focused regression coverage and capture verification notes below.

### 46 Review
- Added a cached CSV-backed host-grid snapshot in `Form1` and switched the Hosts header to derive `unsaved` from a pure snapshot comparison instead of the raw `_csvDirty` flag.
- The header now stops showing `unsaved` after a user edits a CSV-backed grid and then returns it to the same row/column/value state as the last loaded or saved CSV-backed snapshot.
- Existing `disk changed` and `missing on disk` handling remains in the fingerprint-based sync path; this change only refines when the `unsaved` suffix appears.
- Added focused host-grid utility coverage for DataGridView snapshot capture and snapshot equality comparisons.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostGridUtilitiesTests" -p:BaseOutputPath=artifacts\\host-indicator-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\host-indicator-tests\\obj\\` passed (6/6).
- Verification: `dotnet build .\\SSH_Helper.csproj -p:BaseOutputPath=artifacts\\host-indicator-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\host-indicator-build\\obj\\` passed with 0 warnings and 0 errors.

## 45. Autosave Dirty Grid On Environment Switch
- [x] 45.1 Replace the dirty host-grid environment-switch prompt with automatic save-to-environment behavior.
- [x] 45.2 Verify all environment-switch entry points still complete cleanly after autosave.
- [x] 45.3 Capture the implementation and verification notes in the review section below.

### 45 Review
- Removed the dirty host-grid confirmation from the environment-switch path and kept the existing save-to-environment snapshot behavior unconditional inside `TrySwitchEnvironment(...)`.
- Simplified the related folder-selection and preset-driven switch callers by dropping the now-unused `promptIfDirty` plumbing from `TrySwitchEnvironment(...)` and `TryApplyFolderEnvironment(...)`.
- Verified the remaining switch entry points still compile and route through the same shared switch helper: toolbar environment changes, Manage Environments selection changes, folder base-environment application, folder selection, and preset-driven environment restore/switch.
- Verification: `dotnet build .\\SSH_Helper.csproj -p:BaseOutputPath=artifacts\\env-switch-autosave-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\env-switch-autosave-build\\obj\\` passed with 0 warnings and 0 errors.

## 43. Investigate CSV Save Exit Hang
- [x] 43.1 Trace the normal exit path in `Form1` and identify all conditions that can cancel shutdown.
- [x] 43.2 Trace CSV save/save-as flows and any dialog interactions that can leave the form in a state where exit requests are ignored.
- [x] 43.3 Verify the most plausible failure mode against related event handlers/background work and capture the findings below.

### 43 Review
- Both `File -> Exit` and the window close button funnel through `ConfirmExitWorkflow()` (`Form1_FormClosing` for X, `ExitMenuItem_Click` for the menu). That method cancels shutdown whenever execution is running and the user declines to stop, whenever the dirty-CSV prompt returns `Cancel`, whenever dirty-CSV save returns `false`, or whenever dirty-preset resolution returns `false`.
- The most plausible “exit does nothing but app stays responsive” path is the dirty-CSV save branch: `ConfirmExitWorkflow()` calls `SaveCurrentCsv(promptIfNoPath: true)`, which returns `false` if the user answers `Yes` to save but then cancels `Save As`, or if saving throws and the error path returns `false`. In that case `ConfirmExitWorkflow()` returns `false`, `FormClosing` sets `e.Cancel = true`, and both exit routes appear to do nothing.
- `SaveCurrentCsv(...)` makes that behavior easy to hit because the no-path branch calls `SaveCsvAs()` and infers success only from whether `_loadedFilePath` ended up non-empty after the dialog. There is no follow-up status explaining that the close was canceled because the save dialog was canceled.
- A second close-cancel path still exists even after CSV save succeeds: if `IsPresetDirty()` is true, `TryResolvePendingPresetChanges()` can also veto shutdown. That means a user can associate the issue with the CSV prompt even though the actual final cancellation came from unsaved preset changes.
- I did not find a stronger hard-lock path in the main-form shutdown flow. This looks like repeated close cancellation rather than the app getting stuck in an unresponsive state.
- Verification: source review only; no code changes or UI automation run for this investigation.

## 44. Patch CSV Exit Cancellation UX
- [x] 44.1 Refactor the CSV save/save-as path so close handling can distinguish save success, save cancellation, and save failure.
- [x] 44.2 Update the exit workflow to offer exit-without-saving when the CSV save attempt is canceled or fails, instead of silently canceling shutdown.
- [x] 44.3 Verify the patch builds cleanly and capture the review below.

### 44 Review
- Added a small `CsvSaveAttemptResult` flow in `Form1` so CSV save/save-as now distinguishes successful save, canceled save dialog, and failed save instead of collapsing everything to `true`/`false`.
- `SaveCsvAs()` now uses an owned `SaveFileDialog` (`ShowDialog(this)`) and both save paths share one `TrySaveCsvToPath(...)` method that updates `_loadedFilePath`, fingerprint, status bar, and save-error messaging consistently.
- `ConfirmExitWorkflow()` now routes CSV handling through `TryResolvePendingCsvChangesForExit()`. If the user says `Yes` to save but then cancels `Save As`, or if saving fails, the app now asks whether to exit without saving instead of silently canceling the close.
- Verification: `dotnet build .\\SSH_Helper.csproj -p:BaseOutputPath=artifacts\\csv-exit-fix\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\csv-exit-fix\\obj\\` passed with 0 warnings and 0 errors.
- Automated tests were not added for this patch because the affected behavior is inside the WinForms main-form dialog workflow; verification here was compile-only.

## 42. Rebase Branch Onto origin/master
- [x] 42.1 Confirm the current branch/worktree state before rebasing.
- [x] 42.2 Fetch the latest `origin/master`.
- [x] 42.3 Rebase the current branch onto `origin/master` and capture whether conflicts were encountered.

### 42 Review
- Confirmed the current branch was `0.51.8` and the worktree was clean before I wrote the task plan.
- Fetched the latest `origin/master`.
- Temporarily stashed the local `tasks/todo.md` planning edit, rebased `0.51.8` onto `origin/master`, and restored the stash afterward.
- The rebase completed successfully with no conflicts.

## 41. Review Connection Pooling Feature
- [x] 41.1 Trace the UI/config toggle and runtime execution paths that enable or bypass SSH connection pooling.
- [x] 41.2 Inspect the pool lifecycle, health-check, keep-alive, and session-leasing behavior plus any focused specs/tests.
- [x] 41.3 Deliver a concise review of concrete benefits, drawbacks, and implementation-specific risks below.

### 41 Review
- The settings/UI wiring is straightforward: the checkbox in `SettingsDialog` persists `UseConnectionPooling`, `Form1` keeps a long-lived `SshExecutionService` with an internal pool, and manual runs switch between pooled and non-pooled execution by checking `UseConnectionPooling`.
- Real benefits in this implementation are limited to repeated manual UI runs against the same `host:port:username` within one app session: pooled execution skips reconnect/login work, preserves timeout/algorithm/UTF-8 parity with non-pooled execution, and leases a host key so one pooled connection is not shared concurrently.
- The feature is narrower than the label suggests: scheduler jobs create a fresh `SshExecutionService` and force `UseConnectionPooling = false`, so scheduled runs and `Run Now` job execution do not benefit from this toggle at all.
- Operational drawbacks: pooled connections stay alive via a background timer/SSH keepalive sweep, active reuse can issue a real `echo 1` shell command as a health check, and disabling the setting only stops future reuse; it does not immediately clear already pooled connections.
- Implementation risk: when a same-host pooled connection is already leased, `CreateSessionAsync(...)` falls back to a standalone SSH client, but the pooled execution callers only dispose the `SshShellSession` and release the lease. I do not see an explicit `client.Dispose()`/`Disconnect()` path for that fallback client, so concurrent same-host pooled runs appear capable of leaking standalone SSH connections.
- Coverage gap: I did not find direct unit/integration tests for `SshConnectionPool` behavior or the pooled execution branches. Current tests only cover persisting the `UseConnectionPooling` flag inside execution-details/history metadata.
- Verification: source review only; no build or test run was needed for this analysis task.

## 40. Fix Scheduler Retry, Import Naming, and Per-Host Validation
- [x] 40.1 De-duplicate queued scheduled jobs and correct one-time failure handling so scheduled one-time jobs do not requeue or auto-retry after a failed scheduled attempt.
- [x] 40.2 Implement deterministic import conflict naming with `(imported)`, `(imported 2)`, etc., and surface partial import save failures in the completion message.
- [x] 40.3 Tighten per-host credential validation so every populated host row requires non-blank `username` and `password` values in per-host mode.
- [x] 40.4 Add focused regression coverage for scheduler queueing/one-time behavior, import naming and failure reporting, and per-host validation.
- [x] 40.5 Run focused verification and capture the review outcome below.

### 40 Review
- `JobExecutionService` now tracks queued job IDs to prevent duplicate pending entries, skips re-queueing jobs that are already waiting, clears that tracking on dequeue, and auto-disables failed scheduled one-time jobs with `DisabledReason = "One-time schedule failed"` while preserving manual `Run Now` behavior.
- `JobExportService.PrepareImport(...)` now reserves names across the full import batch and resolves conflicts deterministically as `Name (imported)`, `Name (imported 2)`, `Name (imported 3)`, etc. `JobListDialog` now records per-entry save failures and reports them in the import completion message instead of silently swallowing them.
- `JobEditorValidator.ValidateAll(...)` now accepts host-column input, enforces per-host `username` and `password` columns case-insensitively, and blocks save on the first populated row missing either value. `JobExecutionService.BuildHostConnections(...)` now reads those per-host credential fields case-insensitively at runtime so validation and execution match.
- Added focused regression coverage in `JobExecutionServiceTests`, `JobExportServiceTests`, `JobEditorValidationTests`, and `JobListDialogRunNowTests` for the new scheduler, import, and per-host validation behavior.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj -p:BaseOutputPath=artifacts\\test-output\\ -p:BaseIntermediateOutputPath=artifacts\\test-obj\\ --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobExportServiceTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~JobListDialogRunNowTests"` passed (101/101).
- Verification: `dotnet build .\\SSH_Helper.csproj` passed.

## 39. Review UI Diff Since 3937c252
- [x] 39.1 Collect the UI/interaction diff for the requested dialogs, control, and related UI utilities since `3937c2522f7b2eb12931594746d1bd7754da48ed`.
- [x] 39.2 Inspect the changed behavior in `JobEditorDialog`, `JobListDialog`, `ImportPreviewDialog`, `RunOutputViewerDialog`, `UI/CronBuilderControl`, `UI/UnsavedPresetDiffDialog`, and any directly related UI helpers.
- [x] 39.3 Consult targeted tests only if needed to confirm expected behavior, then record concrete bugs, regressions, and worthwhile enhancements below.

### 39 Review
- Review scope stayed limited to the requested UI/interaction files plus directly related helpers: `JobEditorDialog`, `JobListDialog`, `ImportPreviewDialog`, `RunOutputViewerDialog`, `UI/CronBuilderControl`, `UI/UnsavedPresetDiffDialog`, `Utilities/JobEditorValidator`, `Utilities/HostGridUtilities`, `Utilities/ModelessDialogManager`, `Utilities/PresetSaveImpactResolver`, and `Utilities/SchedulerNotificationFormatter`.
- Confirmed four concrete issues worth raising: stored-credential duplication produces a new job with no matching saved secret, per-host credential mode is not validated despite the UI promising required columns, clear-history leaves the jobs list's `Last Result` stale until a later refresh, and import save failures are silently swallowed after the preview step.
- Reviewed targeted WinForms/unit coverage only where it clarified intent (`JobListDialogRunNowTests`, `JobEditorValidationTests`, `JobEditorDialogStoredCredentialTests`, `UnsavedPresetDiffDialogTests`, `CronBuilderControl*Tests`, `HostGridUtilitiesTests`, `ModelessDialogManagerTests`). Those tests do not currently cover the four issues above.

## 38. Review Scoped Storage Export Integrity Diff
- [ ] 38.1 Inspect the scoped git diff for the targeted storage, export, preset-integrity, model, and credential-target files since `3937c2522f7b2eb12931594746d1bd7754da48ed`.
- [ ] 38.2 Check only relevant tests as supporting evidence for the reviewed behaviors.
- [ ] 38.3 Deliver prioritized findings with concrete file/line references, plus up to two worthwhile enhancements, and capture the review below.

## 39. Review Scheduler Runtime Diff
- [x] 39.1 Inspect the git diff since `3937c2522f7b2eb12931594746d1bd7754da48ed` for `SchedulingService`, `JobExecutionService`, `JobHistoryService`, `HistoryStorageService`, `SchedulerHistoryPolicyResolver`, and `Form1` scheduler wiring.
- [x] 39.2 Verify related models/utilities only where needed to confirm behavior, edge cases, and line-accurate findings.
- [x] 39.3 Deliver concrete review findings with severity ordering, file/line references, and up to two worthwhile enhancements.

### 39 Review
- Reviewed the scoped diff in the scheduler runtime/history path plus directly implicated supporting types (`JobDefinition`, run-history models, `JsonFileWriter`, `JobStorageService`, status-bar wiring, and cron UI consumption points).
- Main findings: recurring cron execution currently evaluates against UTC overloads while the UI surfaces local next-run times; failed/cancelled one-time jobs are left eligible and will re-trigger every evaluation cycle; startup missed-run handling can both over-count downtime after crashes and double-handle occurrences that land between service construction and the first timer tick.
- Additional execution risks: concurrent scheduler threads persist `RunningState` through unsynchronized `JobStorageService.Save(...)` calls, shutdown disposal can race with background semaphore release, and the per-job cancellation token created in `TryStartJob(...)` is never passed into execution so `CancelJob(...)` does not stop a running job.
- No material regression stood out in the `HistoryStorageService` refactor itself; the risky behavior in this range is concentrated in scheduling/execution startup and concurrency handling rather than the extracted atomic JSON writer.
- Verification: source review only; no tests were run for this review task.

## 37. Restore Unified Preset Save Diff
- [x] 37.1 Refactor the preset save confirmation UI so the diff dialog can also show optional scheduled-job impact details and rename/create-new actions.
- [x] 37.2 Route `Form1` preset-save confirmation flows through the unified dialog while preserving no-op saves and non-impact save behavior.
- [x] 37.3 Update OpenSpec/task artifacts and focused WinForms coverage for combined diff-plus-impact behavior, collapsed affected-job listing, and rename-choice flows.
- [x] 37.4 Run focused verification, clean build, OpenSpec validation, and capture the review outcome below.

### 37 Review
- `UnsavedPresetDiffDialog` now serves as the single preset-save confirmation surface: it preserves the existing diff-first review layout, adds an optional scheduled-impact header, and keeps the affected-job list behind a collapsed toggle so the diff remains dominant.
- `Form1.ShowPresetSavePrompt(...)` now routes referenced preset saves, rename-vs-create decisions, and unsaved-change confirmations for existing presets through that unified dialog instead of splitting between the old diff dialog, the impact-only dialog, and a rename message box.
- Referenced rename flows keep the one-dialog behavior while clarifying that `Rename Existing` carries scheduled jobs forward and `Create New` saves a separate preset; non-impacted dirty saves still retain the diff prompt without showing scheduler impact controls.
- Retired the dedicated `PresetSaveImpactDialog` implementation and replaced its coverage with unified-dialog WinForms tests for impact summary visibility, collapsed/expanded affected-job lists, rename-choice buttons, and the non-impacted diff regression.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~UnsavedPresetDiffDialogTests|FullyQualifiedName~PresetSaveImpactResolverTests"` passed (7/7).
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~UnsavedPresetDiffDialogTests|FullyQualifiedName~PresetSaveImpactResolverTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~PresetManagerJobReferenceTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~JobEditorDialogStoredCredentialTests" -p:BaseOutputPath=artifacts\\preset-save-unified-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\preset-save-unified-tests\\obj\\` passed (75/75).
- Verification: `dotnet build .\\SSH_Helper.sln` passed.
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\preset-save-unified-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\preset-save-unified-build\\obj\\` passed.
- Verification: `openspec validate replace-scheduler-drift-with-save-warning --strict --no-interactive` passed.

## 36. Replace Scheduler Drift With Save-Time Warning
- [x] 36.1 Add OpenSpec change artifacts for replacing scheduler drift blocking with a preset save-time warning.
- [x] 36.2 Add preset save impact resolution and a single save confirmation dialog for referenced preset saves, including rename-vs-create-new handling without stacked popups.
- [x] 36.3 Remove drift reevaluation, UI indicators, and execution blocking while keeping legacy drift fields file-compatible.
- [x] 36.4 Add focused tests for preset save impact resolution, referenced-save dialog flows, and legacy `HasDriftWarning` execution behavior.
- [x] 36.5 Run focused verification, clean build, OpenSpec validation, and capture the review outcome below.

### 36 Review
- `Form1` now routes referenced preset saves through `PresetSaveImpactResolver` plus the new `PresetSaveImpactDialog`, so users see one save-time confirmation with affected scheduled job names instead of discovering drift later in the scheduler UI.
- Referenced-save prompts cover direct preset jobs and folder jobs targeting the preset's current folder, sort those jobs by name, and de-duplicate by job ID before display.
- Direct save, unsaved-change save, and referenced rename flows now share the same warning surface without a follow-up drift acknowledgement step; unreferenced saves continue using the existing lightweight flows.
- `PresetManager` no longer reevaluates or writes drift state when presets or folders change, `JobListDialog` no longer renders `[DRIFT]` or drift-colored rows, and `JobExecutionService` no longer blocks scheduled or Run Now execution on legacy `HasDriftWarning`.
- Legacy scheduler compatibility stays intact: job JSON still carries `TargetContentHash`, `FolderPresetHashes`, and `HasDriftWarning`, and job save/export paths normalize `HasDriftWarning` to `false` without using it as active runtime behavior.
- Added focused coverage for preset save impact resolution, the new save confirmation dialog modes, `PresetManager` no-longer-recomputes behavior, `SchedulerJobIntegrityUtilities` remaining helpers, and legacy-drift execution through both Run Now and scheduler evaluation.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetSaveImpactResolverTests|FullyQualifiedName~PresetSaveImpactDialogTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~PresetManagerJobReferenceTests|FullyQualifiedName~SchedulerJobIntegrityUtilitiesTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~JobEditorDialogStoredCredentialTests"` passed (77/77).
- Verification: `dotnet build .\\SSH_Helper.sln` passed.
- Verification: `openspec validate replace-scheduler-drift-with-save-warning --strict --no-interactive` passed.

## 35. Audit Scheduler Drift Touchpoints
- [x] 35.1 Identify production code paths and symbols for scheduler drift state, target-hash drift detection, UI banners/indicators, and save/run blocking.
- [x] 35.2 Identify scheduler drift test coverage and relevant OpenSpec references.
- [x] 35.3 Summarize dependencies that remain if drift indicators/blocking are removed but preset-save warnings are introduced.
- [x] 35.4 Capture the audit review below.

### 35 Review
- Drift state is modeled on `JobDefinition` via `TargetContentHash`, `FolderPresetHashes`, and `HasDriftWarning`; preset/folder mutations flow through `PresetManager.ReevaluateAffectedJobDriftStates(...)`, which delegates comparison to `SchedulerJobIntegrityUtilities.IsDrifted(...)` and persists changed flags through `JobStorageService`.
- UI drift touchpoints are limited to `JobEditorDialog` (banner visibility, acknowledge action, save-time snapshot recompute and drift clear) and `JobListDialog` (name suffix/color indicator plus generic Run Now warning when service-level blocking returns false).
- Execution blocking lives only in `JobExecutionService`: `RunNowAsync(...)` returns false and emits `Skipped` when `HasDriftWarning` is set, and the recurring evaluation loop silently skips drifted jobs.
- Export/import integrity touchpoints are `JobExportService.CloneForExport(...)` clearing `HasDriftWarning` while preserving target hashes and `SchedulerJobIntegrityUtilities.ApplyMissingTargetImportState(...)` disabling missing-target imports with explicit reasons.
- Test coverage exists for model fields/defaults, hash utility behavior, reference lookups, drift activation in `PresetManager`, service-level run blocking, and export stripping. No direct automated coverage exists for the `JobEditorDialog` drift banner/acknowledge flow or the `JobListDialog` `[DRIFT]` indicator/warning dialog.
- If drift indicators/blocking are removed and preset-save warnings are added, the minimal surviving backend is the preset-save entry point plus reference lookup (`Form1.SaveCurrentPreset`, `PresetManager.GetJobsReferencingPreset/GetJobsReferencingFolder`, `JobStorageService` queries). Saved hashes and `SchedulerJobIntegrityUtilities.IsDrifted(...)` remain necessary only if the new warning should be content-aware or limited to actual snapshot changes rather than warning on every referenced preset save.

## 34. Collapse Consecutive Identical Scheduler Failures
- [x] 34.1 Extend job-history persistence so the newest matching failed run for a job is updated with an incrementing repeat counter instead of adding another row.
- [x] 34.2 Surface collapsed failure counts in the scheduler history UI and last-result column without changing success or skipped-run behavior.
- [x] 34.3 Add focused service and WinForms regression coverage for repeated-failure collapse and reset behavior.
- [x] 34.4 Run focused and full verification, then capture the review outcome below.

### 34 Review
- `JobHistoryService` now collapses only the newest consecutive identical failure for a job: same failure counts, same top-level error text, same per-host success/error signature, not skipped, and still failure-only.
- Collapsed failures keep a single history row/payload file, overwrite that payload with the latest run details, and increment a persisted `ConsecutiveFailureCount` on both the index record and payload so the count survives refresh and restart.
- `JobListDialog` now renders collapsed failures as `FAIL xN (...)` in both the run-history grid and the jobs list `Last Result` column while leaving success and skipped summary formatting unchanged.
- Added service coverage for collapse, no-collapse on different failures, and no-collapse after a success resets the streak, plus a WinForms regression that verifies two identical failures render as one `FAIL x2` history row.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobListDialogRunNowTests"` passed (40/40).
- Verification: `dotnet build .\\SSH_Helper.sln` passed.
- Manual interactive UI verification was not run from this CLI environment.

## 33. Fix Cron Builder Dialog Clipping
- [x] 33.1 Replace fixed cron-builder height assumptions with measured responsive layout inside `CronBuilderControl`.
- [x] 33.2 Make `JobEditorDialog` size the recurring schedule host panel from the cron builder's computed height.
- [x] 33.3 Add WinForms regression coverage for cron control layout and New Job recurring-panel visibility.
- [x] 33.4 Run focused and full verification, then capture the review outcome below.

### 33 Review
- `CronBuilderControl` now remeasures its preset flow panel, dropdown row, raw expression row, and status labels whenever content, width, or font-related layout changes occur, then updates its own `Height`, `MinimumSize`, and `AutoScrollMinSize` from the actual visible content bottom instead of fixed constants.
- The preset button area no longer assumes a fixed two-row `64` px slot, so narrower widths or larger fonts can wrap buttons without hiding the fields and expression controls below.
- `JobEditorDialog` now syncs `_panelCron.Height` to the embedded cron builder's computed height and refreshes that sizing on dialog/tab resize, cron-builder size changes, schedule-mode switches, prepopulation, and post-theme initialization.
- Added WinForms regressions covering both the cron control's wrapped preset layout and the New Job dialog's recurring schedule section at the current default window size.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CronBuilderControl|FullyQualifiedName~JobEditorDialog"` passed (41/41).
- Verification: `dotnet build .\\SSH_Helper.sln` passed.
- Manual interactive UI verification was not run from this CLI environment.


## 32. Collapse Scheduler Downtime Misses Into One Summary Entry
- [x] 32.1 Add a scheduling summary model/path that groups missed recurring runs by job for a startup downtime window.
- [x] 32.2 Persist one skipped summary history entry per affected job, including skipped-count and downtime-window metadata.
- [x] 32.3 Update the scheduler history UI to render summarized skipped rows compactly and block output viewing for new skipped-summary entries.
- [x] 32.4 Add focused service and WinForms regression coverage for skipped-run aggregation, rendering, and history-slot compression.
- [x] 32.5 Update the scheduler spec text and capture verification results in the review section.

### 32 Review
- `SchedulingService` now exposes `DetectMissedRunSummaries(...)`, which collapses all missed recurring occurrences for a job into one `SkippedRunSummaryEntry` with count plus first/last scheduled timestamps.
- `Form1.RecordMissedSchedulerRunsOnStartup()` now persists one skipped history summary per affected job/startup window instead of one history row per missed cron slot.
- `JobHistoryService` now persists skipped-summary metadata (`SkippedRunCount`, `SkippedWindowStartUtc`, `SkippedWindowEndUtc`) on both the index record and payload while keeping legacy single skipped rows compatible through the old `SaveSkippedRun(...)` path.
- `JobListDialog` now renders summarized skipped rows as `SKIPPED (N)`, keeps the `Started` column on the most recent missed time, shows compact downtime messages in `Error`, and disables `View Output` for the new skipped-summary entries so they do not open an empty viewer.
- Added focused coverage for summary detection, summary persistence, single-summary and multi-summary UI rendering, legacy skipped-row rendering, and the regression that a long downtime window now compresses into one history slot per job.
- Verification: `dotnet build .\\SSH_Helper.sln` failed because `bin\\Debug\\net8.0-windows\\SSH_Helper.exe` was locked by a running `SSH_Helper` process (PID 81020).
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobListDialogRunNowTests"` failed for the same locked default `obj\\Debug\\net8.0-windows\\SSH_Helper.dll` path.
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\downtime-summary-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\downtime-summary-build\\obj\\` passed.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobListDialogRunNowTests" -p:BaseOutputPath=artifacts\\downtime-summary-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\downtime-summary-tests\\obj\\` passed (82/82).
- Verification: `openspec validate update-scheduler-runtime-history --strict --no-interactive` passed.

## 31. Scheduler Notification Output Suppression
- [x] 31.1 Confirm which scheduler event paths append lifecycle messages into the main output pane.
- [x] 31.2 Stop appending scheduler start/completion/skipped messages into the shared output pane while preserving scheduler history and status updates.
- [x] 31.3 Run focused verification and capture the review results.

### 31 Review
- Root cause: `Form1` appended scheduler lifecycle lines directly into the same output buffer used for live host command output from `OnSchedulerJobCompleted(...)`, `OnSchedulerJobStateChanged(...)`, and startup skipped-run reporting, which merged scheduler metadata into normal terminal output.
- `Form1` now keeps scheduler lifecycle updates out of the shared output pane while still persisting skipped runs and refreshing scheduler status-bar state.
- Focused verification used the existing scheduler/history/dialog test suite plus a clean solution build; there is not yet a dedicated `Form1` output-routing test harness that asserts against the live output textbox directly.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulerNotificationTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests" -p:BaseOutputPath=artifacts\\scheduler-output-suppression-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-output-suppression-tests\\obj\\` passed (61/61).
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\scheduler-output-suppression-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-output-suppression-build\\obj\\` passed.

## 30. Scheduler History Row Selection Stability
- [x] 30.1 Confirm why the run-history grid falls back to the first row after the scheduler dialog refresh timer ticks.
- [x] 30.2 Preserve the active history run selection across timer-driven and event-driven history refreshes.
- [x] 30.3 Add focused WinForms regression coverage for selecting a non-first history row before refresh.
- [x] 30.4 Run focused verification and capture the review results.

### 30 Review
- Root cause: `JobListDialog` runs a 5-second `_refreshTimer` that calls `RefreshJobList()`, which in turn rebuilds `_gridHistory` via `RefreshHistory(...)`; the old code cleared and repopulated the history rows without restoring the selected run, so WinForms fell back to the first row.
- `JobListDialog` now tracks the active history `RunFileName`, suppresses history selection churn while the grid is rebuilt, and reapplies the matching history row after timer-driven and event-driven refreshes.
- `ViewSelectedOutput()` now resolves the active history run through the preserved selection state instead of depending only on the transient current `SelectedRows` collection.
- Added a focused WinForms regression test that selects the second history row, invokes `RefreshJobList()`, and verifies the same run remains selected afterward.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialogRunNowTests" -p:BaseOutputPath=artifacts\\history-row-selection-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\history-row-selection-tests\\obj\\` passed (5/5).
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\history-row-selection-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\history-row-selection-build\\obj\\` passed.

## 23. Scheduler History Dialog Selection Stability
- [x] 23.1 Make scheduler job selection deterministic on dialog load and refresh.
- [x] 23.2 Keep history rendering bound to a stable active job ID instead of transient grid selection state.
- [x] 23.3 Add WinForms regression coverage for initial history population and post-refresh stability.
- [x] 23.4 Run focused verification with isolated build output paths and capture review results.

### 23 Review
- `JobListDialog` now uses a stable `_selectedJobId` plus deterministic fallback selection order (`previous active job -> current row -> first available row`) so the Run History pane populates immediately on dialog load and survives job-grid rebuilds.
- The jobs grid now runs as single-select, suppresses selection-change handling while rows are rebuilt, and refreshes the history pane explicitly after the active job row is restored.
- Job actions and history actions now resolve the active job through the stabilized selection path instead of depending on transient `SelectedRows` state during refresh timing.
- Added WinForms regression coverage for first-load history population without manual clicking and for preserving the active job/history after a completion-driven refresh.
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\scheduler-history-dialog-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-history-dialog-build\\obj\\` passed.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~JobHistoryServiceTests" -p:BaseOutputPath=artifacts\\scheduler-history-dialog-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-history-dialog-tests\\obj\\` passed (31/31).

## 22. Scheduler Runtime History Correctness
- [x] 22.1 Read `openspec/changes/update-scheduler-runtime-history/proposal.md`, `tasks.md`, and related scheduler/runtime code paths to confirm scope.
- [x] 22.2 Wire persisted shutdown timestamps into scheduler startup so missed recurring runs are recorded as skipped without auto-running them.
- [x] 22.3 Apply per-job scheduler history retention overrides with fallback to global defaults and output caps.
- [x] 22.4 Correct scheduler history presentation to show persisted run start time and derived duration.
- [x] 22.5 Add focused regression tests for missed-run recording, retention selection, and history timestamp display.
- [x] 22.6 Run verification, update OpenSpec task checkboxes, and capture review results.

### 22 Review
- Scheduler startup now reads `LastAppShutdownUtc`, detects recurring runs missed while the app was closed, appends skipped scheduler notifications, and persists skipped history rows without auto-executing those jobs.
- Scheduler shutdown now stops the execution timer during form close and persists a fresh `LastAppShutdownUtc` anchor before configuration save.
- Scheduler history persistence now resolves per-job `MaxHistoryRuns` and `HistoryRetentionDays` overrides with fallback to global config defaults and the global per-host output cap.
- Skipped startup runs are persisted with an explicit `WasSkipped` flag so the history list can render `SKIPPED` instead of misclassifying them as failures.
- Scheduler history rows now display `StartedUtc` in the `Started` column and derive duration from the stored start/completion timestamps, clamping invalid negative durations to zero.
- Added focused regression coverage for skipped-run persistence, retention policy resolution, and the scheduler history grid timestamp/duration display.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~SchedulerHistoryPolicyResolverTests|FullyQualifiedName~JobListDialogRunNowTests" -p:BaseOutputPath=artifacts\\runtime-history-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\runtime-history-tests\\obj\\` passed (45/45).
- Verification: `dotnet build SSH_Helper.sln -p:BaseOutputPath=artifacts\\runtime-history-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\runtime-history-build\\obj\\` passed.
- Verification: `openspec validate update-scheduler-runtime-history --strict --no-interactive` passed.

## 21. Scheduler OpenSpec Follow-Up Proposals
- [x] 21.1 Create a scheduler integrity proposal covering stored credentials, drift activation, safe import disabling, run-now attribution, and single-instance dialog behavior.
- [x] 21.2 Create a scheduler host-grid parity proposal covering column operations, keyboard/clipboard behavior, CSV import parity, and host-count refresh rules.
- [x] 21.3 Create a scheduler runtime/history proposal covering missed-run recording, retention-policy enforcement, and history timestamp correctness.
- [x] 21.4 Validate all new OpenSpec changes with strict validation and capture results.
- [x] 21.5 Amend the scheduler host-grid parity proposal to include visual/styling parity with the main hosts grid.

### 21 Review
- Added standalone OpenSpec change `update-scheduler-job-integrity` with proposal, tasks, design, and `job-scheduler` spec deltas for stored credentials, drift activation, safe missing-target imports, run-now attribution, and single-instance scheduler dialog behavior.
- Added standalone OpenSpec change `update-scheduler-host-grid-parity` with proposal, tasks, and `job-scheduler` spec deltas for host-grid column parity, keyboard/clipboard parity, CSV/copy parity, live host-count refresh, and visual/styling parity with the main hosts grid.
- Added standalone OpenSpec change `update-scheduler-runtime-history` with proposal, tasks, and `job-scheduler` spec deltas for missed-run recording, retention policy enforcement, and correct history timestamps.
- Validation: `openspec validate update-scheduler-job-integrity --strict --no-interactive` passed.
- Validation: `openspec validate update-scheduler-host-grid-parity --strict --no-interactive` passed.
- Validation: `openspec validate update-scheduler-runtime-history --strict --no-interactive` passed.
- Validation: `openspec validate update-scheduler-host-grid-parity --strict --no-interactive` passed again after adding visual parity requirements.

## 20. Scheduler Implementation Review
- [x] 20.1 Cross-check `.planning/phases` scheduler requirements, plans, and validation notes against the implemented code paths.
- [x] 20.2 Review scheduler UI behavior with explicit comparison between the scheduler hosts grid and the main form hosts grid.
- [x] 20.3 Review scheduler persistence, execution, history, import/export, and notification flows for functional gaps or regressions.
- [x] 20.4 Run targeted verification and capture concrete review results.

### 20 Review
- Stored-credential jobs are not actually persisted or reloaded: the editor collects username/password text but save logic only stores `CredentialMode`, while execution expects credentials to already exist in Credential Manager.
- Missed-run recording is not wired into startup/shutdown flow: `SchedulingService.DetectMissedRuns(...)` and `AppConfiguration.LastAppShutdownUtc` exist, but the scheduler initialization path never uses them.
- Drift detection is incomplete: the editor saves target hashes and can clear `HasDriftWarning`, but no reviewed code path marks jobs drifted after preset or folder content changes.
- Scheduler host-grid parity is materially incomplete versus the main hosts grid: no column add/rename/delete flow, no copy/paste/delete keyboard behavior, no checked-row copy semantics, and no immediate host-count refresh on inline `Host_IP` edits.
- Import preview warns that missing-target jobs will be disabled, but the import save path persists them without disabling them.
- Run-now notifications are misclassified because Form1 only labels them as run-now when `TrackRunNow(...)` is called, and the current Job List run-now action never calls it.
- Per-job history retention overrides are captured in the editor but not used by `JobHistoryService`, which always applies hard-coded defaults on `JobCompleted`.
- Job history UI labels completion time as the run start time in the history grid.
- Verification: `dotnet build SSH_Helper.sln -p:BaseOutputPath=artifacts\\review-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\review-build\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobStorageServiceTests|FullyQualifiedName~SchedulingService|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobExportServiceTests|FullyQualifiedName~SchedulerNotificationTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~PresetManagerJobReferenceTests" -p:BaseOutputPath=artifacts\\review-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\review-tests\\obj\\` passed (217/217).

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

## 25. Scheduler Code Map
- [x] 25.1 Inspect scheduler UI entry points in `JobListDialog.cs`, `JobEditorDialog.cs`, and `Form1.cs`.
- [x] 25.2 Inspect implemented scheduler models, services, and utilities without reading planning docs.
- [x] 25.3 Inspect scheduler-focused tests and note covered versus uncovered behaviors.
- [x] 25.4 Produce a concise architecture/code map with file references and likely weak spots.

## 26. Scheduler Planning Artifact Review
- [x] 26.1 Read `.planning/REQUIREMENTS.md` and scheduler phase artifacts `01-job-definitions-persistence` through `05-scheduler-ui-integration` only.
- [x] 26.2 Extract required scheduler behaviors, validations, and explicit UX/functional details from those planning documents.
- [x] 26.3 Deliver a concise referenced summary for the user and capture the review result below.

## 27. Implement update-scheduler-host-grid-parity
- [x] 27.1 Read the approved OpenSpec change artifacts for `update-scheduler-host-grid-parity` and map the main-grid behaviors that must be mirrored in `JobEditorDialog`.
- [x] 27.2 Add scheduler Hosts-tab column, keyboard/clipboard, import/copy, and host-count parity with minimal shared helper logic.
- [x] 27.3 Align the scheduler host-grid visual treatment with the main hosts grid, including row sizing, row numbers, selection styling, and themed scroll handling.
- [x] 27.4 Add focused automated coverage for scheduler host-grid parity helpers and dialog behaviors.
- [x] 27.5 Run verification, update the OpenSpec checklist, and capture the review outcome below.

## 28. Implement update-scheduler-job-integrity
- [x] 28.1 Read the approved OpenSpec change artifacts for `update-scheduler-job-integrity` and map the affected credential, drift, import, and Form1 integration paths.
- [x] 28.2 Add secure stored-credential round-trip support for scheduler jobs without persisting plaintext to `jobs.json`.
- [x] 28.3 Recompute scheduler drift state when referenced preset or folder snapshots change, and normalize missing-target imports into disabled jobs with explicit reasons.
- [x] 28.4 Fix Run Now attribution and modeless scheduler single-instance reuse from Form1/job-list entry points.
- [x] 28.5 Add focused automated coverage and run verification, then update the OpenSpec checklist and capture the outcome below.

## 29. Inspect update-scheduler-runtime-history
- [x] 29.1 Read the approved OpenSpec change artifacts for `update-scheduler-runtime-history` and confirm the required behavior deltas.
- [x] 29.2 Trace the current shutdown timestamp persistence/read paths and startup missed-run detection entry points.
- [x] 29.3 Trace scheduler event/history recording plus history UI bindings for started/duration values.
- [x] 29.4 Return the concrete files, methods, behavior gaps, and smallest likely edit points.

### 29 Review
- `LastAppShutdownUtc` exists on `AppConfiguration` and round-trips through `ConfigurationService`, but no production path sets it on shutdown or reads it during scheduler startup.
- Startup missed-run detection logic exists only as pure helpers in `SchedulingService`; production scheduler startup goes through `Form1.InitializeSchedulerServices()` and `JobExecutionService.Initialize()` without calling `DetectMissedRuns(...)`.
- Scheduler history persistence is driven solely by `JobHistoryService.SubscribeTo(JobExecutionService)` -> `OnJobCompleted(...)`, which always saves with hard-coded retention/output defaults and has no skipped-run write path.
- Scheduler history UI binds `Started` from `CompletedUtc` in `JobListDialog.RefreshHistory()`, while duration correctly uses `CompletedUtc - StartedUtc`; result rendering also only supports `OK`/`FAIL`, not a skipped state.

### 29 Review
- OpenSpec change `update-scheduler-runtime-history` requires a persisted shutdown anchor plus startup-time missed recurring runs to be recorded as skipped without auto-execution; see `openspec/changes/update-scheduler-runtime-history/proposal.md` and `openspec/changes/update-scheduler-runtime-history/specs/job-scheduler/spec.md`.
- `AppConfiguration.LastAppShutdownUtc` exists in the config model, and `ConfigurationService` will serialize/deserialize it generically, but production runtime code does not currently set or read that property anywhere.
- Actual startup wiring in `Form1.InitializeSchedulerServices()` loads jobs, creates scheduler services, runs `JobExecutionService.Initialize()` crash recovery, and starts the timer immediately; no startup path calls `SchedulingService.DetectMissedRuns(...)`.
- `JobExecutionService` does call `SchedulingService.GetMissedOccurrences(...)`, but only inside the live 30-second evaluation loop using `_lastEvaluationUtc`, which is initialized to `DateTime.UtcNow`; that covers only in-process gaps between timer evaluations, not downtime between app shutdown and restart.
- There is also no production consumer for `SkippedRunEntry`: `JobHistoryService` only persists `JobRunResult` instances received from the `JobCompleted` event, so startup-detected missed occurrences currently have no path into persisted scheduler history.
- Smallest likely edit points are `Form1_FormClosing()` for writing a dedicated shutdown anchor, `Form1.InitializeSchedulerServices()` for reading it and invoking missed-run detection before `_jobExecutionService.Start()`, and a narrow bridge in `JobHistoryService` (or adjacent startup wiring) to persist/report each `SkippedRunEntry`.
- Source inspection only; no code changes or test runs were performed for this task.

## Review Addendum
- Reviewed scheduler implementation only from code and tests: `Form1`, `JobListDialog`, `JobEditorDialog`, scheduler-related models/services/utilities, and scheduler-focused tests. No planning docs were read for this task.
- Confirmed the implemented scheduler stack is split into UI wiring (`Form1`/dialogs), pure cron helpers (`SchedulingService`, `CronBuilderControl`, validators/formatters), persistence (`JobStorageService`, `JobHistoryService`, `JobExportService`), and timer-driven execution (`JobExecutionService`).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobStorageServiceTests|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~JobExportServiceTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~SchedulerNotificationTests|FullyQualifiedName~CronBuilderControlTests|FullyQualifiedName~JobDefinitionTests|FullyQualifiedName~MaxConcurrentJobsTests|FullyQualifiedName~ExecutionPipelineModelTests|FullyQualifiedName~PresetManagerJobReferenceTests" -p:BaseOutputPath=artifacts\\verify-scheduler-map\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\verify-scheduler-map\\obj\\` passed (292/292). Build emitted two existing warnings about unused `_schedulerStatusDirty` and `_loaded` fields.
- Main implementation risks found: missed-run detection exists but is not wired into production flow; stored job credentials UI appears to validate input without persisting it; per-job/global history retention settings are modeled in UI/config but not applied by the event-driven history writer; cancellation and run-now notification paths have disconnected plumbing; drift metadata is saved/checked but no production path sets `HasDriftWarning = true`.
- Reviewed only `.planning/REQUIREMENTS.md` plus scheduler phase artifacts `01-job-definitions-persistence` through `05-scheduler-ui-integration`; implementation code was intentionally not inspected.
- Consolidated the planned scheduler contract across job persistence, scheduling, execution, history, export/import, and Form1/UI integration with file/line references for user review.
- Noted one planning nuance for follow-up: `.planning/REQUIREMENTS.md` still marks `UI-03` notifications as pending even though Phase 5 planning specifies the intended notification/status-bar behavior in detail.
- Focused scheduler hosts-grid parity review completed against the phase note that calls for the Hosts tab mini-grid to use the same column structure as the main grid.
- Findings from the comparison: the scheduler grid lacks manual column add/rename/delete/reorder flows, its host count label does not refresh on inline `Host_IP` edits, its CSV import path diverges from the main grid's `CsvManager` behavior, keyboard clipboard/selection workflows are not carried over, and visual parity is only partial because the main grid adds custom scrollbars and painting on top of shared theme colors.
- Verification: source review only for this parity check; no tests were run.
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
- Implemented scheduler host-grid parity in `JobEditorDialog` with add/rename/delete/reorder support, main-grid-style keyboard/clipboard editing, shared CSV import semantics, and immediate host-count refresh on inline `Host_IP` edits.
- Added shared `HostGridUtilities` coverage for scheduler copy-source selection, DataTable snapshot conversion, and paste expansion, plus WinForms dialog tests for grid parity properties, copy-from-main behavior, host-count refresh, and persisted display-order extraction.
- Implemented scheduler job-integrity fixes across `JobEditorDialog`, `JobListDialog`, `Form1`, `PresetManager`, and supporting utilities so stored credentials round-trip through Credential Manager, missing-target imports save disabled, preset/folder mutations activate drift warnings, and the scheduler window/run-now flows reuse Form1-owned integration seams.
- Added focused coverage for stored-credential save/reopen behavior, preset/folder drift activation, missing-target import normalization helpers, run-now callback routing, and modeless dialog reuse.
- Verification: `dotnet build SSH_Helper.sln -p:BaseOutputPath=artifacts\\job-integrity-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\job-integrity-build\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetManagerJobReferenceTests|FullyQualifiedName~SchedulerJobIntegrityUtilitiesTests|FullyQualifiedName~JobEditorDialogStoredCredentialTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~ModelessDialogManagerTests" -p:BaseOutputPath=artifacts\\job-integrity-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\job-integrity-tests\\obj\\` passed (28/28).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobStorageServiceTests|FullyQualifiedName~PresetManagerJobReferenceTests|FullyQualifiedName~JobExportServiceTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~JobEditorDialogStoredCredentialTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~ModelessDialogManagerTests|FullyQualifiedName~SchedulerJobIntegrityUtilitiesTests" -p:BaseOutputPath=artifacts\\job-integrity-regression-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\job-integrity-regression-tests\\obj\\` passed (144/144).
- Verification: `openspec validate update-scheduler-job-integrity --strict --no-interactive` passed.
- Updated the main-form scheduler handoff to copy checked rows first when any host rows are checked, otherwise all eligible host rows, while excluding the select-checkbox column.
- Updated `DialogTheme.ApplyNativeTheme(...)` to theme `DataGridView` scrollbars so the scheduler grid inherits themed scroll treatment in dark/light modes.
- Verification: `dotnet build SSH_Helper.sln -p:BaseOutputPath=artifacts\\host-grid-parity-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\host-grid-parity-build\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~HostGridUtilitiesTests|FullyQualifiedName~JobEditorDialogHostGridParityTests" -p:BaseOutputPath=artifacts\\host-grid-parity-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\host-grid-parity-tests\\obj\\` passed (7/7).
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CsvManagerTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~HostGridUtilitiesTests|FullyQualifiedName~JobEditorDialogHostGridParityTests" -p:BaseOutputPath=artifacts\\host-grid-parity-tests2\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\host-grid-parity-tests2\\obj\\` passed (50/50).
- Verification: `openspec validate update-scheduler-host-grid-parity --strict --no-interactive` passed.
- Manual interactive UI verification was not run from this CLI environment; OpenSpec task `5.2` remains unchecked pending a live click-through.

## 30. Implement update-cancellation-outcomes
- [x] 30.1 Add the OpenSpec change artifacts for cancellation outcome normalization and scheduler cancel UI, then validate the change.
- [x] 30.2 Add additive `WasCancelled` flags across execution, history, and scheduler models with backward-compatible persistence defaults.
- [x] 30.3 Propagate cancellation through SSH execution, manual preset/folder completion handling, and history storage so cancelled runs retain partial output and explicit cancelled status.
- [x] 30.4 Update scheduler aggregation, history, notifications, and the Job List UI to expose and persist cancellation distinctly from failure.
- [x] 30.5 Add focused automated coverage for manual, folder, and scheduled cancellation behavior plus persistence/UI rendering.
- [x] 30.6 Run verification, update the OpenSpec checklist, and capture the review outcome below.

### 30 Review
- Added a focused OpenSpec change `update-cancellation-outcomes` with validated deltas for `execution-control`, `execution-history`, and `job-scheduler`, including the explicit Job List `Cancel` action and cancelled-history retention contract.
- Normalized cancellation into additive `WasCancelled` flags across manual execution results, execution details, host history, and scheduler run payload/index models. Older history continues to deserialize with the default `false` value.
- Fixed the low-level propagation gap where script execution could return `ScriptExitStatus.Cancelled` or `ScriptExitStatus.Error` without surfacing that outcome through `ExecutionResult`; the SSH execution service now converts cancelled script runs into cancelled host results instead of reporting success.
- Manual preset and folder runs now treat Stop as `cancellation requested` immediately, save the final run as `CANCELLED` only after unwind, preserve partial output from the live buffer in history, and carry cancelled host/detail status into the details dialog and history host list.
- Scheduled runs now persist `WasCancelled`, avoid collapsing cancelled runs into failure streaks or auto-disabling one-time jobs as failures, render `CANCELLED` distinctly in the scheduler history/result columns, and expose a Job List toolbar/context-menu `Cancel` action enabled only for running jobs.
- Verification: `dotnet build SSH_Helper.sln -p:BaseOutputPath=artifacts\\cancel-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\cancel-build\\obj\\` passed.
- Verification: `dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Cancel|FullyQualifiedName~Cancelled|FullyQualifiedName~SshExecutionServiceCancellationTests|FullyQualifiedName~ExecutionDetailsDialogTests|FullyQualifiedName~JobListDialogRunNowTests|FullyQualifiedName~JobHistoryServiceTests|FullyQualifiedName~HistoryStorageServiceTests|FullyQualifiedName~ConfigurationServiceExecutionDetailsTests|FullyQualifiedName~SchedulerNotificationTests|FullyQualifiedName~ExecutionPipelineModelTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\cancel-tests-full\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\cancel-tests-full\\obj\\` passed (116/116).
- Verification: `openspec validate update-cancellation-outcomes --strict --no-interactive` passed.
- Manual interactive smoke testing was not run from this CLI environment.

## 31. Implement update-scheduler-job-timeouts
- [x] 31.1 Add the OpenSpec change artifacts for scheduler per-job timeout overrides and mirror the checklist into this task tracker.
- [x] 31.2 Extend scheduler job models, persistence, and import/export round-trip support for nullable command and connection timeout overrides.
- [x] 31.3 Add scheduler job-editor timeout override controls, inherited timeout guidance, prepopulation, and save/reset behavior.
- [x] 31.4 Extend validation and scheduler timeout resolution precedence for preset, folder, and custom-preset jobs.
- [x] 31.5 Add focused automated coverage for model defaults, storage/export round-trip, dialog behavior, validation, and timeout precedence.
- [x] 31.6 Run focused verification, update the OpenSpec checklist, and capture the outcome below.

### 31 Review
- Added OpenSpec change `update-scheduler-job-timeouts` with proposal, checklist, and `job-scheduler` delta covering optional per-job command and connection timeout overrides for scheduled jobs.
- Extended `JobDefinition` with nullable `CommandTimeoutOverrideSeconds` and `ConnectionTimeoutOverrideSeconds`, and confirmed both `jobs.json` persistence plus `.sshjobs` import/export round-trip preserve the new fields without breaking older payloads.
- Updated `JobExecutionService.BuildTimeouts(...)` so job overrides win when present, while unset values keep the existing inherited behavior: preset timeout or app default for command timeout, and app default for connection timeout.
- Added a new `Timeouts (Per-Job Overrides)` section to `JobEditorDialog` with inherited-value guidance, first-enable seeding from the current effective timeout, prepopulation for existing jobs, and clear-on-save behavior when overrides are unchecked.
- Extended `JobEditorValidator` with explicit timeout override bounds validation and covered the new paths with focused model, service, export/storage, validation, and WinForms dialog tests.
- Verification: `dotnet build .\\SSH_Helper.sln -p:BaseOutputPath=artifacts\\scheduler-timeouts-build\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-timeouts-build\\obj\\` passed with 0 warnings and 0 errors.
- Verification: `dotnet test .\\SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~JobStorageServiceTests|FullyQualifiedName~JobExportServiceTests|FullyQualifiedName~JobEditorValidationTests|FullyQualifiedName~JobDefinitionTests|FullyQualifiedName~JobEditorDialogTimeoutOverrideTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\\scheduler-timeouts-tests\\bin\\ -p:BaseIntermediateOutputPath=artifacts\\scheduler-timeouts-tests\\obj\\` passed (163/163).
- Verification: `openspec validate update-scheduler-job-timeouts --strict --no-interactive` passed.
- Manual interactive verification was not run from this CLI environment; the OpenSpec manual verification item remains unchecked.

## 133. Implement add-preset-delete-undo
- [x] 133.1 Add OpenSpec change `add-preset-delete-undo` plus this task checklist, then validate the change once the deltas are written.
- [x] 133.2 Add failing service and UI coverage for session-scoped delete undo, guarded `Ctrl+Z`, delete-stack invalidation, descendant-folder preservation, and recursive folder-delete scheduler effects.
- [x] 133.3 Implement snapshot-based preset/folder delete undo support, including preset-library snapshot capture/restore and affected-job snapshot restore.
- [x] 133.4 Correct folder delete semantics so move-to-parent preserves descendant folder structure, and recursive folder delete disables preset-target jobs for removed presets.
- [x] 133.5 Wire `Edit > Undo Delete`, guarded `Ctrl+Z`, undo-menu state refresh, and non-delete mutation invalidation through `Form1`.
- [x] 133.6 Run focused verification, broader regression verification, `openspec validate`, and capture the review outcome below.

### 133 Review
- Added OpenSpec change `add-preset-delete-undo` with proposal, tasks, and spec deltas for `preset-organization` and `job-scheduler`.
- Added focused regression coverage for preset/folder delete undo, undo stack ordering/clearing, guarded `Ctrl+Z`, folder delete subtree preservation, and scheduler job disable/restore behavior.
- Implemented `PresetDeleteUndoService` plus preset-library snapshot restore and affected-job snapshot restore.
- Fixed `PresetManager.DeleteFolder(..., deletePresets: false)` so descendant folders are renamed upward instead of flattened, and `deletePresets: true` now disables preset-target jobs for presets removed from the subtree.
- Wired `Edit > Undo Delete`, session-scoped multi-level undo capture/restore, and stale-history invalidation across preset/folder/order/favorite/base-environment mutations in `Form1`.
- Verification passed:
  - `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetManagerDeleteBehaviorTests|FullyQualifiedName~PresetDeleteUndoServiceTests|FullyQualifiedName~Form1DeleteUndoTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-delete-undo-red\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-delete-undo-red\obj\`
  - `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-delete-undo-full\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-delete-undo-full\obj\`
  - `openspec validate add-preset-delete-undo --strict --no-interactive`

## 134. Preset tree incremental mutation cleanup
- [x] 134.1 Add failing WinForms coverage for in-place preset-tree insertion/restore flows, including add preset, single-preset undo delete, and at least one adjacent single-item mutation path.
- [x] 134.2 Add a focused preset-tree mutation helper layer that can insert, relabel, move, and restore local nodes while preserving viewport and selection memory.
- [x] 134.3 Route eligible single-item preset-tree operations through the incremental path and keep full rebuilds only for filtered, bulk, or structural folder-tree changes.
- [x] 134.4 Run focused regression verification, broader preset-tree regression verification, build verification, and capture the review outcome below.

### 134 Review
- Added focused WinForms regression coverage for add-preset insertion, single-preset undo delete restore, in-place rename mutation, filtered fallback rebuild behavior, and favorite-rename Favorites-tree refresh.
- Added an internal incremental preset-tree mutation layer in `Form1` that captures/restores `TopNode`, preserves remembered selection, and supports local preset/folder insert, relabel, move, and reinsertion without rebuilding unrelated nodes.
- Routed local unfiltered mutations through that helper for add preset, save-as-new, duplicate, single import, rename, move-to-folder, empty-folder create, single-preset undo delete, and preset/folder favorite toggles, while keeping filtered, bulk, and structural operations on the rebuild path.
- Kept Favorites refreshes scoped to actual visibility/order changes, and fixed the rename path so favorite presets or presets inside favorite folders still update the Favorites tree label/order.
- Verification passed:
  - `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-tree-incremental-green6\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tree-incremental-green6\obj\`
  - `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1DeleteUndoTests|FullyQualifiedName~PresetTreeDeleteMutationTests|FullyQualifiedName~PresetTreeViewportRestorerTests|FullyQualifiedName~PresetTreeSelectionGuardTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-tree-incremental-regression2\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tree-incremental-regression2\obj\`
  - `dotnet build .\SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\preset-tree-incremental-build2\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tree-incremental-build2\obj\`
- Build/test warnings were unchanged existing warnings: `MSB3277` `WindowsBase`/WebView2 conflicts and `xUnit1031` warnings in `ExpressionParserTests`.

## 135. Fix add-preset visibility and base-environment restore regressions
- [x] 135.1 Add focused WinForms regressions for add-preset viewport visibility and add-preset base-environment restore behavior.
- [x] 135.2 Patch the add-preset incremental selection path so a newly inserted preset is fully visible without unnecessary tree rebuilds or viewport jumps.
- [x] 135.3 Route add-preset editor loading through the normal preset load path so environment restore behavior matches other preset selections.
- [x] 135.4 Run focused verification, update lessons, and capture the review outcome below.

### 135 Review
- Added focused WinForms regressions for the two reported follow-up bugs: add-preset now verifies a newly inserted row becomes fully visible when it lands below the fold, and creating a blank preset now verifies the active environment restores to the base environment instead of staying on the prior preset's declared environment.
- Updated `AddPreset` to stop hand-populating the editor and instead load the new preset through `EnsurePresetLoadedInEditor(...)`, which reuses the existing environment-restore logic from normal preset selection.
- Added a small `TreeView` visibility helper in `Form1` so the incremental add path only scrolls when the new node is not fully visible, while still avoiding full preset-tree rebuilds.
- Tightened the older add-preset no-rebuild regression to focus on preserved node instances rather than strict `TopNode` equality; with the corrected UX, add-preset is allowed to scroll just enough to reveal the new row.
- Updated `tasks/lessons.md` with the two missed patterns from this regression.
- Verification passed:
  - `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-tree-followup-green4\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tree-followup-green4\obj\`
  - `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests|FullyQualifiedName~Form1DeleteUndoTests|FullyQualifiedName~PresetTreeDeleteMutationTests|FullyQualifiedName~PresetTreeViewportRestorerTests|FullyQualifiedName~PresetTreeSelectionGuardTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-tree-followup-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tree-followup-regression\obj\`
  - `dotnet build .\SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\preset-tree-followup-build\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tree-followup-build\obj\`
- Build/test warnings were unchanged existing warnings: `MSB3277` `WindowsBase`/WebView2 conflicts and `xUnit1031` warnings in `ExpressionParserTests`.

## 136. Fix undo-delete preset visibility regression
- [x] 136.1 Add focused WinForms coverage around single-preset undelete visibility scenarios, including scroll-away and rebuild-fallback undo flows.
- [x] 136.2 Patch the undo-delete path so restored presets are fully visible after both incremental restore and fallback rebuild selection.
- [x] 136.3 Run focused verification, update lessons, and capture the review outcome below.

### 136 Review
- Added focused WinForms coverage for undo-delete visibility scenarios in the shared preset-tree mutation test class, including user-scroll-before-undo and filtered rebuild-fallback undo flows.
- The exact invisible restored-node case from the user report did not reproduce in the current WinForms harness, but code inspection showed the undo path still lacked the same explicit visibility guarantee already added for add-preset.
- Patched `UndoLatestPresetDelete` so the rebuild fallback reselects with `ensureVisible: true`, and the incremental restore path now calls the shared tree-visibility helper on the restored node.
- Updated `tasks/lessons.md` with the missed pattern: when I fix visibility for add, I must audit the matching undo/restore path in the same pass.
- Verification passed:
  - `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-tree-undo-visibility-green\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tree-undo-visibility-green\obj\`
  - `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1PresetTreeIncrementalMutationTests|FullyQualifiedName~Form1DeleteUndoTests|FullyQualifiedName~PresetTreeDeleteMutationTests|FullyQualifiedName~PresetTreeViewportRestorerTests|FullyQualifiedName~PresetTreeSelectionGuardTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\preset-tree-undo-visibility-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tree-undo-visibility-regression\obj\`
  - `dotnet build .\SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\preset-tree-undo-visibility-build\bin\ -p:BaseIntermediateOutputPath=artifacts\preset-tree-undo-visibility-build\obj\`
- Build/test warnings were unchanged existing warnings: `MSB3277` `WindowsBase`/WebView2 conflicts and `xUnit1031` warnings in `ExpressionParserTests`.

## 137. Fix Test Connection status-bar completion regression
- [x] 137.1 Add focused WinForms coverage that reproduces `Test Connection(s)` leaving the status label on `Testing connections...` after queued UI callbacks drain.
- [x] 137.2 Patch `Form1` connection-test progress handling so only the active test run can update the status/progress UI, and completed/cancelled runs cannot be overwritten by stale callbacks.
- [x] 137.3 Run focused verification, build verification, and capture the review outcome below.

### 137 Review
- Added `SSH_Helper.Tests/UI/Form1ConnectionTestStatusTests.cs` with a focused WinForms regression that drives the real `TestSelectedConnections()` path against a loopback `TcpListener` and proves the status label remains `Connection test complete (1 hosts)` after queued UI callbacks drain.
- Root cause was `TestSelectedConnections()` queuing per-host progress/status updates through `BeginInvoke(...)`. `Task.WhenAll(...)` could complete before those UI callbacks executed, so the method would set the final completion text and then a late `Testing connections... N of N` callback would overwrite it.
- `Form1.cs` now tracks connection-test progress with a run id, mirroring the existing manual-execution progress pattern. Per-host cell coloring still applies when callbacks arrive, but status/progress updates are ignored once that connection-test run has been invalidated by completion or cancellation.
- Focused red verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ConnectionTestStatusTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\connection-test-status-red\bin\ -p:BaseIntermediateOutputPath=artifacts\connection-test-status-red\obj\`` failed as intended with the status label stuck on `Testing connections... 1 of 1`.
- Focused green verification: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ConnectionTestStatusTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\connection-test-status-green\bin\ -p:BaseIntermediateOutputPath=artifacts\connection-test-status-green\obj\`` passed (`1` passed, `0` failed).
- Build verification: `dotnet build .\SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\connection-test-status-build\bin\ -p:BaseIntermediateOutputPath=artifacts\connection-test-status-build\obj\`` passed.
- Build/test warnings were unchanged existing warnings: `MSB3277` `WindowsBase`/WebView2 conflicts and `xUnit1031` warnings in `ExpressionParserTests`.


## 161. Fix exists dynamic type runtime + dependency analyzer tracking
- [x] 161.1 Add failing runtime test proving `exists.type` resolves from `${var}` and is honored.
- [x] 161.2 Add failing analyzer test proving variable references inside `exists.type` are tracked.
- [x] 161.3 Implement runtime substitution for `exists.type` before normalization.
- [x] 161.4 Update `ScriptDependencyAnalyzer` to extract refs from `exists.type`.
- [x] 161.5 Run focused tests for ExistsCommand + dependency analyzer and capture results.
- [x] 161.6 Add review notes below.

### 161 Review
- Added RED runtime regression `ExecuteAsync_TypeFromVariable_HonorsResolvedType` in `SSH_Helper.Tests/Scripting/ExistsCommandTests.cs`.
- Added RED dependency regression `AnalyzePresets_ExistsTypeFromVariable_TracksTypeDependency` in `SSH_Helper.Tests/Scripting/ScriptDependencyAnalyzerTests.cs`.
- Implemented runtime fix in `Services/Scripting/Commands/ExistsCommand.cs`: `exists.type` now resolves through script substitution + environment expansion before type normalization.
- Implemented analyzer fix in `Services/Scripting/ScriptDependencyAnalyzer.cs`: `StepType.Exists` now extracts variable references from `step.Exists.Type` in addition to `Path`.
- Verification:
  - RED: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExecuteAsync_TypeFromVariable_HonorsResolvedType|FullyQualifiedName~AnalyzePresets_ExistsTypeFromVariable_TracksTypeDependency"` (failed as expected: `2/2`).
  - GREEN: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExistsCommandTests|FullyQualifiedName~ScriptDependencyAnalyzerTests"` (passed: `39/39`).

## 162. Fix preset-editor autocomplete mouse interaction
- [x] 162.1 Add focused failing UI regressions for autocomplete mouse selection and native child-handle click behavior while the completion popup is open.
- [x] 162.2 Patch `ScintillaScriptEditorControl` completion popup/list interaction so operators can click suggestions and use popup scroll affordances without premature dismissal.
- [x] 162.3 Run focused verification for `ScintillaScriptEditorControl` UI tests and capture the review outcome below.

### 162 Review
- Added two focused WinForms regressions in `ScintillaScriptEditorControlTests`: one proving a mouse click on a non-default autocomplete item commits the clicked item, and one proving clicks on native child handles inside the completion popup are not treated as external dismiss clicks.
- Root causes were twofold: list click interaction was too brittle in the non-focusable completion list path, and the external-click dismissal path only trusted `Control.FromHandle(...)`, which fails for native child handles such as scrollbar internals.
- Patched `ScintillaScriptEditorControl` to set/commit selection explicitly on completion-list mouse down/up, keep completion-list interaction focus-safe, and treat HWND descendants of editor/popup/list as internal via `IsChild(...)` before dismissing suggestions.
- Follow-up root cause from manual repro: the editor’s unconditional `LostFocus` dismissal plus queued `BeginInvoke(EnsureEditorFocus)` from completion-list focus created a click race where the popup dismissed between mouse-down and mouse-up, so no suggestion was committed.
- Follow-up patch: replaced unconditional editor `LostFocus` handling with a deferred handle-aware check (`GetFocus` + `IsHandleInEditorHierarchy`), removed aggressive completion-list/popup auto-refocus hooks, and restored editor focus only after completion commit.
- Added a follow-up regression `CompletionPopup_ListFocus_DoesNotDismissSuggestions` and tightened the click-commit test to process queued UI work between mouse-down and mouse-up (`Application.DoEvents()`), matching real interaction timing.
- Verification:
  - RED: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests.CompletionPopup_MouseClickOnSuggestion_CommitsClickedItem|FullyQualifiedName~ScintillaScriptEditorControlTests.CompletionPopup_NativeChildHandleClick_IsNotTreatedAsExternal" -p:UseAppHost=false -p:BaseOutputPath=artifacts\autocomplete-mouse-red\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-mouse-red\obj\` (failed as expected: `2/2`).
  - GREEN: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests.CompletionPopup_MouseClickOnSuggestion_CommitsClickedItem|FullyQualifiedName~ScintillaScriptEditorControlTests.CompletionPopup_NativeChildHandleClick_IsNotTreatedAsExternal" -p:UseAppHost=false -p:BaseOutputPath=artifacts\autocomplete-mouse-green\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-mouse-green\obj\` (passed: `2/2`).
  - Regression: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\autocomplete-mouse-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-mouse-regression\obj\` (passed: `33/33`).
  - Follow-up focused: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests.CompletionPopup_MouseClickOnSuggestion_CommitsClickedItem|FullyQualifiedName~ScintillaScriptEditorControlTests.CompletionPopup_ListFocus_DoesNotDismissSuggestions|FullyQualifiedName~ScintillaScriptEditorControlTests.CompletionPopup_NativeChildHandleClick_IsNotTreatedAsExternal" -p:UseAppHost=false -p:BaseOutputPath=artifacts\autocomplete-click-followup\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-click-followup\obj\` (passed: `3/3`).
  - Follow-up regression: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScintillaScriptEditorControlTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\autocomplete-click-followup-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\autocomplete-click-followup-regression\obj\` (passed: `34/34`).

## 163. Adjust playsound on_error default + sub-second max_seconds
- [x] 163.1 Add focused failing tests for playsound default `on_error` behavior and fractional `max_seconds` parsing/runtime timeout.
- [x] 163.2 Update playsound parsing/model/runtime so `on_error` defaults to `continue` and `max_seconds` supports positive sub-second values.
- [x] 163.3 Update scripting docs and run focused verification; capture review notes below.

### 163 Review
- Added RED regressions in `SSH_Helper.Tests/Scripting/PlaySoundCommandTests.cs` and `SSH_Helper.Tests/Scripting/ScriptParserTests.cs` proving: (a) playsound failures were not suppressed when `on_error` was omitted, and (b) `playsound.max_seconds: 0.25` was rejected/not parsed.
- Updated playsound runtime behavior in `Services/Scripting/Commands/PlaySoundCommand.cs` so omitted `on_error` now defaults to continue for playsound only, while explicit `on_error: stop` still fails.
- Updated playsound timeout shape end-to-end:
  - `Services/Scripting/Models/ScriptStep.cs`: `PlaySoundOptions.MaxSeconds` changed from `int?` to `double?`.
  - `Services/Scripting/ScriptParser.cs`: `playsound.max_seconds` now parses as an invariant-culture floating-point value and reports `must be a positive number` on parse failure.
  - `Services/FlowCanvasBridge.cs`: numeric property export helper now accepts nullable `double` so fractional `max_seconds` round-trips out of parsed steps.
  - `Services/Scripting/Commands/PlaySoundCommand.cs`: timeout wait and timeout message formatting now use fractional seconds.
- Updated docs in `SCRIPTING.md`: playsound `max_seconds` now documents fractional support and playsound default `on_error` is now `continue`.
- Verification:
  - RED: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PlaySoundCommandTests.ExecuteAsync_FailureWithoutOnError_DefaultsToContinueAndCapturesMeta|FullyQualifiedName~ScriptParserTests.Validate_PlaySoundFractionalMaxSeconds_IsAccepted" -p:UseAppHost=false -p:BaseOutputPath=artifacts\playsound-default-red\bin\ -p:BaseIntermediateOutputPath=artifacts\playsound-default-red\obj\` (failed as expected: `2/2`).
  - GREEN (focused): `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PlaySoundCommandTests|FullyQualifiedName~ScriptParserTests.Validate_PlaySound" -p:UseAppHost=false -p:BaseOutputPath=artifacts\playsound-default-green2\bin\ -p:BaseIntermediateOutputPath=artifacts\playsound-default-green2\obj\` (passed: `9/9`).
  - Regression: `dotnet test .\SSH_Helper.Tests\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests|FullyQualifiedName~PlaySoundCommandTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\playsound-default-regression2\bin\ -p:BaseIntermediateOutputPath=artifacts\playsound-default-regression2\obj\` (passed: `153/153`).

## 181. Flow Canvas properties UX upgrade (choose-focused + quick wins)
- [x] 181.1 Add failing coverage for choose/multiselect dual-mode options editor (mode switch, row edits, legacy hydration, and default mismatch validation).
- [x] 181.2 Add failing FlowCanvasBridge coverage for choose/multiselect label/value round-trip and scalar source preservation.
- [x] 181.3 Extend FlowCanvas property metadata/schema (`helpText`, `group`, `editor`) and update choose/multiselect definitions.
- [x] 181.4 Implement specialized choose/multiselect options editor with source/static modes, variable insertion assist, validation, and compatibility-safe storage.
- [x] 181.5 Improve pane UX: section grouping (`Core`/`Advanced`/`On Error`), inline required/error states, select placeholders, helper text, and variable insertion affordance for text-like fields.
- [x] 181.6 Preserve choose/multiselect label/value fidelity in `FlowCanvasBridge` import/export.
- [x] 181.7 Run focused verification (`Playwright`, `.NET tests`, `npm run build`) and record outcomes.

### 181 Review
- Added new Flow Canvas e2e coverage in `FlowCanvas/e2e/flow-canvas-properties-typing.spec.ts` with `createChoiceOptionsUxFixture()` in `FlowCanvas/e2e/fixtures/graphs.ts` for:
  - legacy hydration to static rows (`"alpha,beta"`),
  - source-mode variable insertion and scalar options persistence,
  - static row add/edit/reorder behavior with mixed string/object export payloads,
  - inline choose-default mismatch warning.
- Extended property metadata in `FlowCanvas/src/blockDefs/registry.ts` (`helpText`, `group`, `editor`) and marked choose/multiselect `options` with `editor: 'choice-options'` plus helper text.
- Updated `FlowCanvas/src/panels/Properties.tsx`:
  - dual-mode options editor for choose/multiselect (`From Variable` vs `Static Options`),
  - compatibility-safe hydration/serialization across scalar, comma/newline legacy text, array strings, and array `{label,value}` objects,
  - inline option/source validation and choose `default` mismatch warning,
  - grouped properties sections (`Core`, `Advanced`, trailing `On Error`),
  - inline required/error rendering and helper-text rendering,
  - required-select placeholder/invalid behavior and variable insertion affordances for text/code/textarea fields.
- Updated FlowCanvas bridge fidelity in `Services/FlowCanvasBridge.cs` so choose/multiselect options now round-trip as:
  - string items when `label == value`,
  - `{ label, value }` objects when they differ,
  - scalar `OptionsFrom` preserved for source mode.
- Hardened drift-guard test parsing in `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` so multiline property `type:` lines in registry definitions are not misread as block definitions.
- Verification:
  - RED (pre-existing run before implementation): targeted new choose UX Playwright tests and new bridge round-trip tests failed as expected.
  - GREEN: `npm run build` (FlowCanvas).
  - GREEN: `npx playwright test e2e/flow-canvas-properties-typing.spec.ts --grep "Flow Canvas Choice Options UX"` (passed `3/3`).
  - GREEN: `npx playwright test e2e/flow-canvas-properties-typing.spec.ts` (passed `11/11`).
  - GREEN: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ImportExportRoundTrip_ChooseLabelValueOptions_PreservesLabelValuePairs|FullyQualifiedName~ImportExportRoundTrip_MultiselectLabelValueOptions_PreservesLabelValuePairs|FullyQualifiedName~ImportExportRoundTrip_ChooseOptionsSourceScalar_PreservesSource"` (passed `3/3`).
  - GREEN: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridgeTests"` (passed `48/48`).

## 192. Fix review findings #2 and #3 (JSON exception type + branch first-child ordering)
- [x] 192.1 Add failing regression coverage for imported-container branch ordering when `_stepPath` includes two-digit indices.
- [x] 192.2 Patch FlowCanvas bridge branch-first-child selection to use numeric-aware step-path comparison.
- [x] 192.3 Patch FlowCanvasParityCli invalid-JSON handling to catch Newtonsoft parse exceptions.
- [x] 192.4 Run focused verification (new regression + related bridge test) and solution build; document outcomes.

### 192 Review
- Added RED regression in `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`:
  - `ExportGraphToYaml_ImportedIfWithTwoDigitThenIndex_UsesStoredSnippetWhenFirstChildEdgeExists`
- RED evidence (before fix): targeted test failed because export regenerated YAML and dropped snippet marker comment (`# keep-imported-snippet`).
- Patched branch-first-child selection in `Services/FlowCanvasBridge.cs`:
  - replaced lexicographic `_stepPath` comparison with `CompareStepPathSegments(...)`, which compares numeric segments numerically (`.../10` > `.../2`).
- Patched CLI invalid-JSON handling in `FlowCanvas/tools/FlowCanvasParityCli/Program.cs`:
  - changed catch from `System.Text.Json.JsonException` to `Newtonsoft.Json.JsonReaderException` for `JObject.Parse(...)`.
- Verification:
  - `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ExportGraphToYaml_ImportedIfWithTwoDigitThenIndex_UsesStoredSnippetWhenFirstChildEdgeExists|FullyQualifiedName~ExportGraphToYaml_ImportedIfWithDeletedElseEdge_RegeneratesWithoutElse" -v minimal -p:UseAppHost=false -p:SkipFlowCanvasBuild=true` (passed `2/2`).
  - `dotnet run --project FlowCanvas/tools/FlowCanvasParityCli/FlowCanvasParityCli.csproj -- evaluate-cases` with malformed stdin payload now prints `Invalid input JSON: ...` and exits with code `1`.
  - `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true -v minimal` (passed; existing warnings unchanged: `MSB3277`, `CS8602`, `xUnit1031`).

## 209. Remove redundant Run All button from main execute bar
- [x] 209.1 Remove `btnExecuteAll` from WinForms designer and control declarations.
- [x] 209.2 Update `Form1.cs` runtime styling/layout/state logic to operate with `Run Selected` + `Stop` only.
- [x] 209.3 Retarget legacy click forwarding that depended on `btnExecuteAll`.
- [x] 209.4 Build solution and record verification results.

### 209 Review
- Removed `btnExecuteAll` from `Form1.Designer.cs` initialization, execute-panel control list, and private field declarations.
- Updated execute-bar layout defaults to place `Stop` beside `Run Selected` and removed all runtime references to `btnExecuteAll` in:
  - font/accent application,
  - `UpdateRunButtonText()` width/position logic,
  - `SetExecutionMode(...)` enable/disable handling.
- Removed obsolete `btnExecuteAll_Click(...)` handler and changed deprecated FlowCanvas run-request forwarding to `btnExecuteSelected.PerformClick()`.
- Verification:
  - `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true -v minimal` (failed due expected local lock: `SSH_Helper.exe` running and locking `bin\Debug\net8.0-windows\SSH_Helper.exe`).
  - `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts/runall-removal-build/bin/ -p:BaseIntermediateOutputPath=artifacts/runall-removal-build/obj/ -v minimal` (passed; existing warnings remain: `MSB3277`, `CS8602`, `xUnit1031`).
  - `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ApplyFontSettingsTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts/runall-removal-tests/bin/ -p:BaseIntermediateOutputPath=artifacts/runall-removal-tests/obj/ -v minimal` (passed: `35/35`).

## 210. Full README refresh for current app capabilities
- [x] 210.1 Audit README claims against current code/docs (execution UI, shortcuts, scheduler, Flow Canvas, scripting).
- [x] 210.2 Rewrite README sections to reflect current behavior and latest features.
- [x] 210.3 Verify README wording against code paths and remove stale/incorrect statements.

### 210 Review
- Rewrote `README.md` to reflect current capabilities and removed stale execution/shortcut guidance:
  - replaced old execute guidance with `Run Selected` + checked-host behavior,
  - added explicit Scheduler and Flow Canvas usage sections,
  - updated scripting summary to cover current command families and local command support,
  - corrected source-build instructions to include `FlowCanvas` npm dependency setup.
- Updated keyboard shortcuts to match actual bindings in `Form1.Designer.cs` and `Form1.cs`:
  - removed stale `F5`/`F6`/`Alt+W`/`Alt+R` claims,
  - added `Ctrl+Shift+V` (Validate Script), `Ctrl+Shift+F` (Flow Canvas), and current supported shortcuts.
- Verification:
  - `rg -n "Execute All|F5|F6|Alt\\+W|Alt\\+R|Escape \\| Stop|Execute on all hosts|Execute on selected hosts" README.md` (no matches).
  - `rg -n "ShortcutKeys\\s*=|case Keys\\.|btnExecuteSelected|Run Selected|Flow Canvas" Form1.Designer.cs Form1.cs` (confirmed README shortcut/run wording parity).
  - `rg -n "public enum StepType|LocalCmd|PlaySound|BrowserCallbackCapture|Parallel|Switch|Call|Return" Services/Scripting/Models/ScriptStep.cs` (confirmed scripting feature wording aligns with current step model).

## 211. Complete SCRIPTING.md TOC coverage
- [x] 211.1 Audit `SCRIPTING.md` TOC vs actual section headings and command entries.
- [x] 211.2 Add missing command links and missing top-level sections to the TOC.
- [x] 211.3 Verify TOC now covers all command sections and top-level sections.

### 211 Review
- Updated `SCRIPTING.md` TOC command list to include missing command sections:
  - `exists`
  - `playsound`
  - `localcmd`
- Added missing top-level TOC entries and renumbered:
  - `Output Options`
  - `Tips and Best Practices`
- Verification:
  - Coverage check script comparing command headings vs TOC command entries now reports:
    - `missing-in-toc=` (empty)
    - `extra-in-toc=` (empty)
  - `rg -n "^## " SCRIPTING.md` confirms TOC now includes all major top-level sections, including `Output Options` and `Tips and Best Practices`.

## 212. Make execution-start debug logs reflect local-only scripts
- [x] 212.1 Add a regression test that distinguishes local-only script execution from SSH-required script execution in the `Form1` execution-start debug message.
- [x] 212.2 Update `Form1` to choose the execution-start debug message from script dependency analysis instead of always saying SSH is starting.
- [x] 212.3 Run targeted verification for the new test coverage and a build, then record the review notes here.

### 212 Review
- Added `SSH_Helper.Tests/UI/Form1ExecutionStartDebugMessageTests.cs` to lock the execution-start debug wording to:
  - `Calling ExecutePresetAsync - Local execution starting` for local-only YAML scripts
  - `Calling ExecutePresetAsync - SSH connection starting` for SSH-required YAML scripts
- Updated `Form1.cs` to route the existing debug log through a new private static helper that parses YAML presets, analyzes SSH requirements, and falls back to a generic execution-start message if script analysis fails.
- Verification:
  - RED: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ExecutionStartDebugMessageTests" -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts/form1-exec-debug-tests/bin/ -p:BaseIntermediateOutputPath=artifacts/form1-exec-debug-tests/obj/ -v minimal` failed `2/2` before implementation because `Form1` did not yet expose `BuildExecutionStartDebugMessage`.
  - GREEN: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1ExecutionStartDebugMessageTests" -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts/form1-exec-debug-tests/bin/ -p:BaseIntermediateOutputPath=artifacts/form1-exec-debug-tests/obj/ -v minimal` passed `2/2`.
  - `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true -v minimal` passed; existing warnings remain (`MSB3277`, `CS8602`, `xUnit1031`).

## 213. Remove built-in localcmd pwsh support
- [x] 213.1 Add regression coverage for removing built-in `pwsh` support from parser, Flow Canvas shell options, and localcmd runtime expectations.
- [x] 213.2 Remove built-in `pwsh` support from `localcmd` parser/runtime/docs/UI metadata while preserving `powershell`, `cmd`, and `custom`.
- [x] 213.3 Run targeted verification for the updated localcmd coverage and a build, then record review notes here.

### 213 Review
- Removed built-in `pwsh` support end-to-end for `localcmd`:
  - parser validation now accepts `powershell`, `cmd`, and `custom`,
  - runtime PowerShell-shell detection no longer treats `pwsh` as a built-in alias,
  - Flow Canvas shell dropdown no longer advertises `pwsh`,
  - user-facing docs/changelog/spec notes now describe the reduced shell matrix.
- Updated regression coverage to pin the new behavior:
  - `LocalCmdParserTests.Validate_PwshShell_ReturnsShellValidationError`
  - `FlowCanvasBridgeTests.Registry_LocalCmdShellOptions_ExcludePwsh`
- Verification:
  - RED: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Validate_PwshShell_ReturnsShellValidationError|FullyQualifiedName~Registry_LocalCmdShellOptions_ExcludePwsh" -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts/remove-pwsh-tests/bin/ -p:BaseIntermediateOutputPath=artifacts/remove-pwsh-tests/obj/ -v minimal` failed `2/2` before implementation because parser validation and Flow Canvas registry still exposed `pwsh`.
  - GREEN: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Validate_PwshShell_ReturnsShellValidationError|FullyQualifiedName~Registry_LocalCmdShellOptions_ExcludePwsh" -p:UseAppHost=false -p:SkipFlowCanvasBuild=true -p:BaseOutputPath=artifacts/remove-pwsh-tests/bin/ -p:BaseIntermediateOutputPath=artifacts/remove-pwsh-tests/obj/ -v minimal` passed `2/2`.
  - `npm run build` in `FlowCanvas/` regenerated `dist/` without `pwsh` in the shipped bundle.
  - `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true -v minimal` initially failed with stale embedded-resource state for the old hashed Flow Canvas asset after the Vite rebuild.
  - `dotnet clean SSH_Helper.sln -p:SkipFlowCanvasBuild=true -v minimal` cleared the stale resource state.
  - `dotnet build SSH_Helper.sln -v minimal` passed; existing warnings remain (`MSB3277`, `CS8602`, `xUnit1031`, Vite chunk-size warning).
  - `rg -n "pwsh" FlowCanvas\\dist -S` returned no matches after the rebuild.
