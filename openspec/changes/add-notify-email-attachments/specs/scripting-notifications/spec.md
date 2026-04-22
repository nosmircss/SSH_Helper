## ADDED Requirements
### Requirement: SMTP notify supports per-step file attachments
The `notify` command SHALL accept `attachments` as an optional list of file paths.

The runtime SHALL apply normal notify variable substitution to each attachment entry before dispatch.

When the effective notify channel resolves to SMTP/email:
- every resolved attachment path SHALL be read from the local file system and attached to the outgoing email in list order,
- and missing or unreadable attachment files SHALL fail the notify step like any other delivery input error.

When the effective notify channel resolves to Slack, Teams, Discord, or toast, `attachments` SHALL be ignored and SHALL NOT change delivery behavior for those channels.

#### Scenario: SMTP notify attaches resolved files
- **WHEN** a `notify` step resolves to an SMTP profile and includes `attachments: ["C:\\reports\\{{ Host_IP }}.txt", "C:\\reports\\summary.csv"]`
- **THEN** the runtime resolves the file paths before dispatch
- **AND** the outgoing email includes both files as attachments in the same order

#### Scenario: Non-email notify ignores attachments
- **WHEN** a `notify` step resolves to Slack, Teams, Discord, or toast and includes `attachments`
- **THEN** the notification sends using that channel's existing behavior
- **AND** the attachment list is ignored

#### Scenario: Missing SMTP attachment fails notify
- **WHEN** a `notify` step resolves to SMTP and any referenced attachment file is missing or unreadable
- **THEN** the notify step fails with an attachment-path error
- **AND** normal `on_error` behavior controls whether the failure is suppressed or aborts the script
