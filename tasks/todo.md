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
