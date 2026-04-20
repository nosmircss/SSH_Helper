## 1. Implementation
- [x] 1.1 Add portable-aware storage root resolution and writable validation in `Utilities/AppDataPaths.cs`.
- [x] 1.2 Enforce portable storage writability check in startup flow before launching `Form1`.
- [x] 1.3 Replace direct `%LocalAppData%` storage in `FlowCanvasForm` and `ScintillaNativeBootstrap` with portable-aware app storage paths.
- [x] 1.4 Add `PortableBuild` compile/publish controls in `SSH_Helper.csproj` and portable artifact naming (`SSH_Helper_Portable.exe`).
- [x] 1.5 Update GitHub release workflow to publish both standard and portable executables plus matching `.sha256` assets.
- [x] 1.6 Update docs (`README.md`, `CHANGELOG.md`) for standard vs portable behavior.

## 2. Validation
- [x] 2.1 Add focused tests for portable path resolution and writable checks in `SSH_Helper.Tests`.
- [x] 2.2 Run focused test suite for new/changed tests.
- [x] 2.3 Run OpenSpec validation for `add-portable-release-build`.
