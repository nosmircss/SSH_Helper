# Change: Persist environment dialog layout

## Why
When users resize the Manage Environments dialog or adjust its splitter, those adjustments are currently lost on close. This breaks user expectations for resizable UI surfaces.

## What Changes
- Persist Manage Environments dialog size in `WindowState`
- Persist Manage Environments left/right splitter position in `WindowState`
- Restore persisted dialog size and splitter position when reopening the dialog

## Impact
- Affected specs:
  - `environment-management`
- Affected code:
  - `Models/AppConfiguration.cs`
  - `EnvironmentDialog.cs`
  - `Form1.cs`
  - `SSH_Helper.Tests/Services/ConfigurationServiceWindowStateTests.cs` (new)
