# Change: Add local script execution (smart-detect SSH requirement)

## Why
Currently, all YAML script execution establishes an SSH connection — even when the script contains only local commands (http, webhook, dns, ping, portcheck, print, set, etc.). This blocks users who want to run pure HTTP/webhook/API presets without configuring SSH credentials. Of the 26 scripting commands, only `send` requires an SSH shell session (`context.Session.ExecuteAsync()`), and `sftp` creates its own independent `Renci.SshNet.SftpClient` connection from context variables.

Host rows in the grid are still used — the `Host_IP` column can contain any value (a URL, reference ID, hostname, etc.) for tracking and variable substitution purposes. The change only skips the SSH connection itself.

## What Changes
- **Static SSH analysis**: Add `AnalyzeSshRequirements(Script)` to `ScriptDependencyAnalyzer` that recursively walks the step tree (including nested `Then`/`Else`/`Elif`/`Do`/`Try`/`Catch`/`Finally` blocks) to detect `StepType.Send` and `StepType.Sftp`. For sftp steps, also inspects whether `Host`, `Username`, and `Password` are specified — if omitted, the step falls back to `Host_IP`/`username`/`password` from context at runtime, which affects host validation and credential requirements
- **Local execution path**: Add `ExecuteScriptLocal()` to `SshExecutionService` that creates a `ScriptContext` with `Session = null`, wires up existing events, and runs `ScriptExecutor.ExecuteAsync()` — bypassing SSH connection, login, and session initialization entirely
- **Execution routing**: Modify `ExecuteScriptOnHost()` to accept an `SshRequirementResult?` parameter and branch to the local path when `!RequiresSshSession`
- **Visual distinction**: Local execution output uses a `LOCAL SCRIPT:` header prefix (vs. SSH's `SCRIPT: host prompt`)
- **Folder execution**: The analysis flows through `ExecutePresetOnHostAsync()` → `ExecutePresetOnHost()` → `ExecuteScriptTextOnHost()` → `ExecuteScriptOnHost()`, so folder execution of local-only scripts benefits automatically

## Impact
- Affected specs: `ssh-execution`, `scripting-runtime`
- Affected code:
  - `Services/Scripting/ScriptDependencyAnalyzer.cs` — new `SshRequirementResult` class + `AnalyzeSshRequirements()` method
  - `Services/SshExecutionService.cs` — new `ExecuteScriptLocal()` method, modified `ExecuteScriptOnHost()`, `ExecuteScriptAsync()`, `ExecuteScriptTextOnHost()`
  - `SSH_Helper.Tests/Scripting/ScriptDependencyAnalyzerTests.cs` — new test cases
- No breaking changes:
  - Existing SSH scripts are unaffected — `send` triggers SSH as always
  - `SendCommand` already handles `Session == null` gracefully (returns `CommandResult.Fail("No SSH session available")` or suppressed if `on_error: continue`)
  - Simple (non-YAML) presets always go through `ExecuteSingleHost()` SSH path — unchanged
  - History recording, execution details, cancellation all flow through existing infrastructure
  - Host grid still required — users populate `Host_IP` with whatever tracking value they want
