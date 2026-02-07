# Change: Add environment management

## Why
Host data and variables are currently global, which forces manual host swapping for dev/staging/prod workflows and blocks safe repeatable automation.

## What Changes
- Add named environments with independent host-grid state (columns, rows, selection, last CSV path)
- Add active environment switching in the main toolbar with save/switch flow
- Add per-environment variables and merge them into host runtime variables
- Add backward-compatible migration from legacy single-environment configuration

## Impact
- Affected specs:
  - `environment-management` (new capability)
- Affected code:
  - `Models/EnvironmentConfig.cs` (new)
  - `Models/AppConfiguration.cs`
  - `Services/ConfigurationService.cs`
  - `Services/EnvironmentService.cs` (new)
  - `EnvironmentDialog.cs` (new)
  - `Form1.Designer.cs`
  - `Form1.cs`
