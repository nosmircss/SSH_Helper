# Change: Add environment CSV freshness detection

## Why
Multiple environments can point at the same CSV file while keeping independent host snapshots. When one environment saves newer CSV content, switching to another environment that still references the same file gives no indication that its stored snapshot is older than the file on disk.

## What Changes
- Persist lightweight CSV fingerprint metadata with environment snapshots and saved application state
- Detect when an environment's remembered CSV reference is newer, missing, or unchanged on disk when that environment becomes active
- Prompt the operator to reload hosts from disk when a target environment's remembered CSV is stale
- Show the active hosts-file sync state in the hosts header when the operator keeps using a stale or missing on-disk file reference

## Impact
- Affected specs:
  - `environment-management`
- Affected code:
  - `Models/EnvironmentConfig.cs`
  - `Models/AppConfiguration.cs`
  - `Services/EnvironmentService.cs`
  - `Form1.cs`
  - `Utilities/HostsFileIndicatorFormatter.cs`
  - `tasks/todo.md`
  - `SSH_Helper.Tests/Services/EnvironmentServiceTests.cs`
  - `SSH_Helper.Tests/Utilities/HostsFileIndicatorFormatterTests.cs`
