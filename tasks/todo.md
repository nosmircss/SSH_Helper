# TODO

## 1. Space Loss Bug (Chunked Output)
- [x] 1.1 Confirm and document root cause in output normalization pipeline.
- [x] 1.2 Add targeted normalization option to preserve trailing spaces on unfinished chunk lines.
- [x] 1.3 Use the new option in live chunk UI emission path.
- [x] 1.4 Add regression tests for split chunks (`set ` + `resource ...`).
- [x] 1.5 Run focused tests and capture outcome.

## Review
- Root cause confirmed: chunk-level normalization trimmed trailing spaces on unfinished chunk lines.
- Implemented `Normalize(..., preserveTrailingSpacesOnFinalLine: true)` for live chunk rendering in `SshShellSession`.
- Added regression tests in `TerminalOutputProcessorTests` for trailing-space preservation and split-chunk word join prevention.
- Verification: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~TerminalOutputProcessorTests"` passed (47/47).
