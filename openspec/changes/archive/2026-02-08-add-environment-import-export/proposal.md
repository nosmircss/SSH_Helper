# Change: Add environment export/import

## Why
Operators need to back up environment definitions and share them between machines or teammates. Today environments can only be managed locally.

## What Changes
- Add environment export from the Manage Environments dialog to a portable JSON file
- Add environment import from a JSON file with conflict handling for existing names
- Persist imported environment data (hosts, columns, selection, variables, metadata) through existing environment storage

## Impact
- Affected specs:
  - `environment-management`
- Affected code:
  - `EnvironmentDialog.cs`
  - `Services/EnvironmentService.cs`
  - `SSH_Helper.Tests/Services/EnvironmentServiceTests.cs`
