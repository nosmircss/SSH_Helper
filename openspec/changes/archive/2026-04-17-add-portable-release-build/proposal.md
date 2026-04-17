# Change: Add Portable Release Build

## Why
SSH Helper currently persists app-managed runtime data under `%LocalAppData%\SSH_Helper`, which is correct for installed usage but does not satisfy a portable distribution model where data should remain beside the executable.

We need a second official release artifact that keeps state local to the extracted folder while preserving existing standard-build behavior.

## What Changes
- Add a portable build flavor that compiles with portable storage semantics and emits `SSH_Helper_Portable.exe`.
- Keep the standard build unchanged (`%LocalAppData%\SSH_Helper` storage root).
- Route app-managed storage paths through portable-aware resolution for config/history/jobs/WebView2/FlowCanvas/scintilla extraction.
- Add startup writability validation for portable mode and fail with a clear message when the executable directory is not writable.
- Update CI/release workflow to publish both standard and portable executables with checksums.
- Update docs to describe both release flavors and storage behavior.

## Impact
- Affected specs: `scripting-runtime`
- Affected code:
  - `Utilities/AppDataPaths.cs`
  - `Program.cs`
  - `UI/FlowCanvasForm.cs`
  - `Utilities/ScintillaNativeBootstrap.cs`
  - `SSH_Helper.csproj`
  - `.github/workflows/build-release.yml`
  - `README.md`
  - `CHANGELOG.md`
