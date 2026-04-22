## 1. Runtime and Models

- [x] 1.1 Add `attachments` to the `notify` command model, parser, and command/editor vocabularies.
- [x] 1.2 Resolve attachment paths through notify variable substitution and pass them into the notification service boundary.
- [x] 1.3 Attach files only for SMTP delivery, ignore them for non-email channels, and fail SMTP notify when a referenced file is missing or unreadable.
- [x] 1.4 Extend Flow Canvas notify authoring/import-export parity for the new field.

## 2. Tests

- [x] 2.1 Add parser, autocomplete, and Flow Canvas coverage for `notify.attachments`.
- [x] 2.2 Add notify-command coverage for attachment substitution, SMTP forwarding, and non-email ignore behavior.
- [x] 2.3 Add SMTP notification coverage for successful attachment inclusion and missing-file failure handling.

## 3. Docs and Validation

- [x] 3.1 Update `SCRIPTING.md` and notify authoring/help text for SMTP-only attachment behavior.
- [x] 3.2 Run focused notify/parser/editor/Flow Canvas verification plus strict OpenSpec validation.
