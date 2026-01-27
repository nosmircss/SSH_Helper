# Change: Refactor execution flow and IO reliability

## Why
Execution orchestration is duplicated in Form1 and pooled execution diverges from non-pooled behavior. The update flow lacks integrity checks and the CSV parser fails on embedded newlines, which increases risk and maintenance cost.

## What Changes
- Consolidate execution orchestration into a single path and move non-UI logic out of Form1.
- Align pooled and non-pooled execution behavior (timeouts, algorithm settings, error handling).
- Verify update packages before running the updater and document ExecutionPolicy usage.
- Replace CSV parsing with multiline-safe parsing and tests.
- Resolve unused configuration dependencies/appsettings.json (remove or wire in).

## Impact
- Affected specs: ssh-execution, update-flow, csv-import
- Affected code: Form1.cs, Services/SshExecutionService.cs, Services/SshConnectionPool.cs, Services/UpdateService.cs, Services/CsvManager.cs, SSH_Helper.csproj
