# Change: Add email attachments to `notify`

## Why
The `notify` command can already deliver SMTP email, but it can only send a body and subject prefix. Scripts cannot attach generated reports or exported files directly to a completion email, which forces operators into extra out-of-band steps after the script finishes.

## What Changes
- Add an optional `attachments` list to the `notify` command surface.
- Apply variable substitution to each attachment entry before dispatch.
- Attach files only when the effective notify channel resolves to SMTP/email.
- Ignore `attachments` for Slack, Teams, Discord, and toast without changing those channel behaviors.
- Fail the notify step when an SMTP attachment path is missing or unreadable.
- Update parser/editor/Flow Canvas/docs so the new field is authorable everywhere `notify` already appears.

## Impact
- Affected specs: `scripting-notifications`
- Affected code: `Services/Scripting/*Notify*`, `Services/Notifications/*`, `Services/Editor/ScriptAutocompleteProvider.cs`, `Services/FlowCanvasBridge.cs`, `FlowCanvas/src/blockDefs/registry.ts`, `SSH_Helper.Tests/*Notify*`, `SSH_Helper.Tests/Scripting/ScriptParserTests.cs`, `SCRIPTING.md`
