## Context
SSH Helper currently stores history entries keyed by display strings, uses per-run CancellationTokenSource without disposal, and executes folder presets in parallel with a shared error flag. Pooled SSH sessions use default terminal settings that can diverge from non-pooled behavior, and health checks allocate scripting resources without explicit disposal. The host grid validates only IPv4 addresses, and passwords are stored in app config. Output interactions and script validation are limited.

## Goals / Non-Goals
- Goals:
  - Prevent history collisions and clean up per-host history data on deletion.
  - Ensure cancellation tokens are disposed per execution.
  - Make Stop-on-first-error deterministic and thread-safe.
  - Align pooled session encoding with non-pooled sessions and dispose health-check scripting resources.
  - Accept hostnames with optional ports in validation and parsing.
  - Support Windows Credential Manager storage and an SSH agent path.
  - Improve output usability and add script validation without executing.
- Non-Goals:
  - Full rewrite of SSH execution architecture.
  - IPv6 host support (out of scope for this change).
  - Replacing Rebex with an external SSH client.

## Decisions
- **History identity:** Add a unique `HistoryEntryId` (GUID) persisted in saved state. UI list items use a display label, but internal maps use the ID.
- **Cancellation lifecycle:** Create a helper in `SshExecutionService` to set up and dispose `_cts` after each execution path (including errors/cancellation).
- **Stop-on-first-error:** Use an `int` error flag with `Interlocked.Exchange` and call `_cts.Cancel()` on first error. Tasks check cancellation and stop scheduling new work.
- **Pooled session parity:** Use `TerminalOptions { Encoding = UTF8 }` in pooled sessions to match non-pooled behavior.
- **Health-check disposal:** Dispose or release scripting resources used during pool health checks (using `IDisposable` detection to avoid compile-time assumptions).
- **Hostname validation:** Introduce `IsValidHostOrIp` using `Uri.CheckHostName` and explicit IPv4 + optional port parsing. Maintain existing IPv4 rules for strict validation elsewhere.
- **Credential storage:** Add a credential provider abstraction (default: config/password in-memory; optional: Windows Credential Manager). Store only a credential key/reference in config when enabled.
- **SSH agent path:** Add a setting to prefer agent-backed auth when available; fall back to identity file or password. Implementation will be best-effort for OpenSSH agent on Windows.
- **Output UX:** Add a lightweight output toolbar with filter/highlight, copy, and auto-scroll toggle.
- **Script validation:** Add a “Validate Script” action to parse and validate YAML scripts, returning structured errors without executing any commands.

## Risks / Trade-offs
- Windows Credential Manager and SSH agent integration add platform-specific code and UI complexity.
- Output filtering/highlighting can add UI overhead; keep implementation lightweight and optional.
- Hostname validation may accept values that don’t resolve; connection failures must be surfaced clearly.

## Migration Plan
- Add `HistoryEntryId` to saved state; on load, generate IDs for legacy history entries.
- When Credential Manager is enabled, migrate stored passwords by writing to the vault and clearing plaintext in config.

## Open Questions
- Which SSH agent should be supported first (OpenSSH agent, Pageant, or both)?
- Preferred output UX additions (filter/highlight/copy/auto-scroll) OK as scoped above?
