# Change: Switch Teams notify delivery to Adaptive Cards

## Why
Teams `notify` delivery still emits a legacy MessageCard payload and ignores `mention:` values entirely. Microsoft Teams Incoming Webhooks now accept Adaptive Cards with mention entities, so the current implementation both misses live-mention support and relies on an older payload shape that Microsoft is moving away from.

## What Changes
- Replace the Teams `notify` payload builder with a generated Adaptive Card webhook envelope by default.
- Keep the existing `notify` command surface, but define Teams-only typed mention strings using UPN and Microsoft Entra Object ID tokens.
- Degrade malformed Teams mention entries to visible plain text with runtime diagnostics instead of failing the whole step.
- Update scripting/docs/editor help so Teams behavior is explicit and no longer documented as MessageCard-only.

## Impact
- Affected specs: `scripting-notifications`
- Affected code: `Services/Notifications/*`, `Services/Scripting/Commands/NotifyCommand.cs`, `SSH_Helper.Tests/*Notification*`, `SCRIPTING.md`, Flow Canvas/editor help text
