# Change: Execution Safety, Credentials, and UX Enhancements

## Why
- Prevent history collisions and stale per-host history data.
- Improve cancellation safety and parallel error handling reliability.
- Align pooled SSH sessions with non-pooled behavior and reduce resource leakage.
- Expand host targeting to include DNS names, not just IPv4.
- Provide safer credential storage options and an SSH agent pathway.
- Improve output usability and script validation confidence.

## What Changes
- Introduce unique history entry IDs and cleanup of per-host history on delete/clear.
- Dispose per-run cancellation tokens after executions complete.
- Make Stop-on-first-error thread-safe and propagate cancellation to in-flight work.
- Ensure pooled sessions use UTF-8 terminal options and dispose health-check scripting resources.
- Accept hostnames (RFC 1123-style) with optional port in host grid validation and parsing.
- Add credential provider abstraction with Windows Credential Manager storage; optional SSH agent usage.
- Output UX improvements (filtering/highlighting, copy/export affordances, auto-scroll control).
- Script dry-run/validator action with structured error reporting (no execution).

## Impact
- Affected specs (new capabilities):
  - execution-history
  - execution-control
  - ssh-pooling
  - host-targets
  - credentials
  - output-experience
  - scripting-validation
- Affected code:
  - Form1.cs / Form1.Designer.cs (UI, history handling, output UX, validation)
  - Models/AppConfiguration.cs (history entries, credential settings)
  - Services/SshExecutionService.cs (cancellation, folder error handling)
  - Services/SshConnectionPool.cs (encoding parity, health-check disposal)
  - Utilities/InputValidator.cs (hostname validation)
  - New services/utilities for credentials/agent integration
- Data migration: saved state history entries include unique IDs; password storage moves to Credential Manager when enabled.
