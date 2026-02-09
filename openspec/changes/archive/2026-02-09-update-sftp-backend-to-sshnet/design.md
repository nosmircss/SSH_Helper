## Context
The `sftp` script step currently depends on Rebex SFTP runtime APIs. Operators without Rebex SFTP licensing cannot use script-level SFTP transfers.

## Goals / Non-Goals
- Goals:
  - Remove Rebex SFTP licensing dependency from script `sftp` steps.
  - Preserve existing YAML contract, error handling, and capture-variable semantics.
- Non-Goals:
  - Changing `sftp` step syntax.
  - Introducing key-based authentication behavior changes for `sftp` in this change.

## Decisions
- Decision: Use `SSH.NET` (`Renci.SshNet.SftpClient`) for `sftp` runtime operations.
- Alternatives considered:
  - Keep Rebex reflection-based SFTP: rejected due license dependency.
  - WinSCP automation: rejected due additional process/tooling complexity and license considerations.

## Risks / Trade-offs
- Risk: Different exception messages from SSH.NET can change operator-visible error text.
  - Mitigation: Preserve existing high-level failure formatting (`Sftp error: ...`) and keep destination/required-field prechecks.
- Risk: Runtime behavior differences vs Rebex under edge server implementations.
  - Mitigation: Keep defaults and prechecks unchanged; add targeted scripted QA verification.

## Migration Plan
1. Add `SSH.NET` package dependency.
2. Remove `Rebex.Sftp` dependency.
3. Replace `SftpCommand` implementation to call `SftpClient` directly.
4. Update SFTP documentation backend note.

## Open Questions
- Should `sftp` support key-based auth overrides in a follow-up change?