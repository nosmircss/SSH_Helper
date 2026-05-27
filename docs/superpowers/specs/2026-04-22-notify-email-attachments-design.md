# Notify Email Attachments Design

## Context

`notify` already supports Slack, Teams, Discord, toast, and SMTP delivery, but the SMTP path can only send a subject/body pair. Operators currently have no way to include generated files in a completion email from the script itself.

The requested behavior is narrowly scoped:
- add per-step attachment paths to `notify`,
- support more than one file,
- only consume them for SMTP/email delivery,
- ignore them for non-email channels,
- and fail the step when an SMTP attachment cannot be read.

## Goals

- Add an optional `attachments` list to the `notify` command.
- Preserve full authoring parity across YAML, editor autocomplete/help text, and Flow Canvas.
- Apply normal variable substitution to each attachment path before dispatch.
- Attach files only for SMTP/email notifications.
- Fail the notify step with a clear error when an SMTP attachment is missing or unreadable.

## Non-Goals

- Adding webhook file uploads for Slack, Teams, or Discord.
- Adding profile-level default attachments.
- Adding MIME overrides, attachment renaming, or content transformation.

## Decisions

### 1. Use `attachments` as a string list on `notify`

The command surface should stay aligned with the existing `mention` list pattern. A list is the simplest shape that naturally supports one or more files without inventing a parallel scalar alias.

### 2. Resolve attachment paths in the scripting layer

`NotifyCommand` should apply variable substitution to each attachment entry and pass the resolved paths down to `NotificationService`. This keeps the service boundary free of script-template concerns and matches how other notify fields are resolved today.

### 3. Ignore attachments unless the effective channel is SMTP

Slack, Teams, Discord, and toast will not attempt file access. The feature is email-only, so those channels should behave exactly as they do today even when `attachments` is present.

### 4. Validate and attach files inside `SmtpDispatcher`

`SmtpDispatcher` already owns `MailMessage`, so it is the right place to materialize `System.Net.Mail.Attachment` instances. It should validate every resolved path before the mail send. If any file is missing or unreadable, it should return a normal notify failure with the path in the error message.

### 5. Keep Flow Canvas/editor parity in the same change

This repo already tests notify authoring through parser, autocomplete, and Flow Canvas import/registry coverage. The change should add `attachments` to all of those surfaces in one pass so YAML and visual authoring do not drift.

## Risks / Trade-Offs

- File reads can fail for reasons beyond missing paths, including permissions and sharing locks. The failure message should preserve the underlying exception detail where safe.
- Ignoring attachments for non-email channels could be surprising if undocumented. `SCRIPTING.md` and the OpenSpec delta should state that behavior explicitly.
- SMTP attachment support adds local file-system dependency to notify execution, so the regression suite needs both success and failure coverage.

## Test Strategy

- Parser coverage for `notify.attachments`.
- Autocomplete coverage so `attachments` appears with the rest of the notify keys.
- Flow Canvas import/registry coverage so the block preserves the new property.
- Notify command/runtime coverage for variable substitution and non-SMTP ignore behavior.
- SMTP dispatcher/service coverage for successful attachment inclusion and missing-file failure handling.
