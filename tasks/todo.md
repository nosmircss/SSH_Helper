# TODO

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
