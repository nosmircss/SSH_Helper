## Why

Some scripts need host-scoped local bootstrap work (for example fetching a short-lived SSH certificate or key material) before the SSH login can succeed. The current runtime authenticates first and only then executes script steps, which blocks this workflow for mixed scripts that also contain `send` commands.

## What Changes

- Add an optional script pre-connection phase that runs before SSH authentication when a script requires SSH.
- Allow pre-connection steps to populate per-host SSH auth overrides (identity file path, identity passphrase, username, password).
- Ensure resolved overrides are used by both pooled and non-pooled SSH execution paths.
- Keep existing behavior unchanged for scripts that do not define pre-connection steps.
- Add parser and validation support for top-level `preconnect` script section.
- Add operator-facing documentation and examples for certificate bootstrap workflows.

## Capabilities

### New Capabilities
- `script-preconnect-bootstrap`: Pre-SSH local bootstrap phase for host-scoped auth preparation before SSH login.

### Modified Capabilities
- `ssh-execution`: SSH login flow now supports a pre-connection phase and runtime auth overrides before session establishment.
- `scripting-runtime`: Runtime gains a dedicated preconnect execution pass with host-scoped variable substitution and controlled step support.
- `scripting-validation`: Validation recognizes and validates `preconnect` blocks and preconnect step constraints.

## Impact

- Affected code:
  - `Services/SshExecutionService.cs`
  - `Services/SshConnectionPool.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/Models/Script.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/ScriptDependencyAnalyzer.cs`
  - `SCRIPTING.md`
  - `SSH_Helper.Tests/Services/*` (new and updated tests)
- Affected behavior:
  - Execution ordering for scripts with `preconnect` and SSH-requiring steps.
  - Authentication source precedence when preconnect sets override variables.
- Security considerations:
  - Sensitive override values must be redacted from output/history and debug logs.
