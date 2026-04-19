## ADDED Requirements
### Requirement: Teams notify delivery uses Adaptive Cards
The `notify` command SHALL send Microsoft Teams notifications through an Incoming Webhook Adaptive Card envelope instead of the legacy MessageCard payload.

The generated Adaptive Card SHALL be derived from the existing `notify` fields only and SHALL support:
- an optional title block,
- an optional mention block,
- and a message block.

Severity SHALL be reflected through Adaptive Card styling rather than MessageCard `themeColor`.

#### Scenario: Teams notify sends Adaptive Card payload
- **WHEN** a `notify` step resolves to a Teams profile and provides `title`, `message`, and `level`
- **THEN** the outgoing webhook body is a Teams message envelope containing an Adaptive Card attachment
- **AND** the title and message render from the existing `notify` fields
- **AND** no legacy MessageCard fields are emitted

### Requirement: Teams notify supports typed mention strings
The `notify` command SHALL keep `mention` as a list of strings and SHALL accept these Teams-specific typed forms:
- `upn:<id>|<display>`
- `entra:<id>|<display>`

If the display segment is omitted, the identifier SHALL be used as the visible mention label.

For valid Teams typed mention strings, the runtime SHALL emit matching Adaptive Card `<at>...</at>` text and `msteams.entities` entries using the provided identifier and display label.

#### Scenario: Teams UPN mention token
- **WHEN** a Teams `notify` step includes `mention: ["upn:alice@contoso.com|Alice"]`
- **THEN** the outgoing Adaptive Card contains visible `<at>Alice</at>` mention text
- **AND** the Teams mention entity uses `alice@contoso.com` as the identifier

#### Scenario: Teams Entra mention token without display label
- **WHEN** a Teams `notify` step includes `mention: ["entra:87d349ed-44d7-43e1-9a83-5f2406dee5bd"]`
- **THEN** the outgoing Adaptive Card contains visible mention text using that identifier
- **AND** the Teams mention entity uses the same identifier value

### Requirement: Invalid Teams mention strings degrade without failing notify
Malformed or unsupported Teams mention strings SHALL NOT fail the `notify` step by themselves.

When a Teams mention string is malformed or unsupported:
- the visible message SHALL retain the literal text for that entry,
- the runtime SHALL omit the live mention entity for that entry,
- and the scripting layer SHALL surface a warning or debug diagnostic describing the degradation.

Slack, Discord, toast, and SMTP notify behavior SHALL remain unchanged by Teams Adaptive Card support.

#### Scenario: Mixed valid and invalid Teams mention strings
- **WHEN** a Teams `notify` step includes both valid typed mention strings and invalid literal mention strings
- **THEN** valid entries become live Teams mentions
- **AND** invalid entries remain plain text in the visible mention line
- **AND** the notify step still sends successfully if the webhook request succeeds
