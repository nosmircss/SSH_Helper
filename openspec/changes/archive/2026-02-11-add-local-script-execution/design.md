## Context
SSH_Helper's scripting engine supports 26 command types. Only `send` requires an active SSH shell session (accesses `context.Session.ExecuteAsync()`). `sftp` creates its own `Renci.SshNet.SftpClient` using credentials from context variables. All 24 other commands run locally (HTTP/DNS/ping/portcheck/file I/O/control flow/etc.). Despite this, the execution pipeline (`SshExecutionService.ExecuteScriptOnHost()`) always establishes an SSH connection before running any script.

Users want to run HTTP/webhook/API presets without SSH connections. The current code structure already supports this — `ScriptContext.Session` is nullable, `SendCommand` handles null session, and `ScriptExecutor` dispatches commands regardless of session state. The missing piece is a decision point before execution.

Host rows remain required. The `Host_IP` column can contain any tracking value (URL, reference ID, hostname). Grid columns are still injected as variables into `ScriptContext` via `HostConnection.Variables`. The change only skips the SSH connection/login/session steps.

## Goals
- Automatically detect when SSH is unnecessary and skip connection — zero user configuration
- Preserve all existing execution infrastructure (events, history, cancellation, progress, debug output)
- Keep host grid as the variable source and execution target
- Support folder execution of local-only scripts automatically

## Non-Goals
- Transport abstraction layer (`ITransportProvider` etc.) — over-engineered for this use case
- Changes to `IScriptCommand` interface, `ScriptExecutor`, or any command implementations
- UI changes (no new buttons, dialogs, or settings)
- Zero-host/hostless execution — grid rows are always required

## Decisions

### 1. Static analysis over runtime detection
**Decision**: Walk the parsed step tree before execution. If `StepType.Send` exists at any nesting depth, require SSH.

**Rationale**: Conservative and predictable. A `send` inside a never-true `if` block still triggers SSH. This is safe — false positives (connecting SSH when not needed) are harmless; false negatives (skipping SSH when needed) would be bugs.

### 2. Reuse `ScriptDependencyAnalyzer` (line 27, `Services/Scripting/ScriptDependencyAnalyzer.cs`)
**Decision**: Add the SSH analysis method to the existing `ScriptDependencyAnalyzer` class.

**Rationale**: This class already recursively walks the step tree via `AnalyzeSteps()` (line 126). The SSH check follows the same recursive pattern. A new `AnalyzeSshRequirementsInSteps()` private method mirrors the existing structure. No new class needed.

### 3. SFTP doesn't require SSH shell, but may require valid host/credentials
**Decision**: `sftp` sets `UsesSftp = true` but NOT `RequiresSshSession = true`. Scripts with only `sftp` + local commands skip SSH shell connection. However, the analysis also inspects each sftp step's `Host`, `Username`, and `Password` options to determine if the step relies on context defaults.

**Rationale**: `SftpCommand` creates its own `SftpClient` — it never touches `context.Session`. But `SftpCommand.ResolveEndpoint()` (line 111-137 in `SftpCommand.cs`) has a fallback chain:
- `options.Host` → falls back to `context.GetVariableString("Host_IP")` (line 120-121)
- `options.Username` → falls back to `context.GetVariableString("username")` (line 130-132)
- `options.Password` → falls back to `context.GetVariableString("password")` (line 134-136)

When an sftp step omits `host:`, it will use `Host_IP` from the grid at runtime — so `Host_IP` must still be a valid hostname/IP for the SFTP connection (not an arbitrary URL or reference ID). Similarly, when `username:` or `password:` are omitted, the grid/global defaults must provide non-empty values.

**Static detection**: If `step.Sftp.Host` is null/whitespace → `SftpUsesDefaultHost = true`. If `step.Sftp.Username` or `step.Sftp.Password` is null/whitespace → `SftpUsesDefaultCredentials = true`. These flags propagate across all sftp steps in the script (any one using defaults sets the flag).

**Impact on validation**:
- `Host_IP` validation (`host.IsValid()`) is skipped only when `!RequiresSshSession && !SftpUsesDefaultHost`
- When `SftpUsesDefaultHost` is true, `Host_IP` must pass standard host/IP validation
- When `SftpUsesDefaultCredentials` is true, username/password must be non-empty (validated at runtime by `SftpCommand` lines 58-60, same as today)

### 4. Analysis performed once, passed through call chain
**Decision**: Analyze in `ExecuteScriptAsync()` (after parse/validate, before host loop) and pass the result to each `ExecuteScriptOnHost()` call. Same for `ExecuteScriptTextOnHost()`.

**Rationale**: Parse once, analyze once, decide once. No per-host or per-step overhead. Clean data flow.

**Call chain for single-preset execution**:
```
ExecuteScriptAsync() [parse, validate, analyze here]
  → foreach host: ExecuteScriptOnHost(host, ..., sshRequirement)
      → if !RequiresSshSession: ExecuteScriptLocal()
      → else: ExecuteScriptWithPool() or ExecuteScriptWithoutPool()
```

**Call chain for folder execution** (inherits automatically):
```
ExecuteFolderAsync()
  → foreach host: ExecutePresetOnHostAsync()
      → ExecutePresetOnHost()
          → ExecuteScriptTextOnHost() [parse, validate, analyze here]
              → ExecuteScriptOnHost(host, ..., sshRequirement)
```

### 5. Host grid remains the execution target
**Decision**: Host rows are always required. `Host_IP` can contain any value — a URL, API endpoint, reference ID, or traditional hostname. Grid columns become `HostConnection.Variables` which are injected into `ScriptContext`.

**Rationale**: The grid serves as both the execution target list (iterate per row) and the variable source (columns → variables). For HTTP/webhook scripts, users put API endpoints or tracking IDs in `Host_IP` and reference them via `${Host_IP}` or `{{Host_IP}}`.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Conservative analysis may connect SSH unnecessarily | Harmless — existing behavior. SSH connects immediately and validates upfront before any commands execute. |
| Host_IP validation rejects non-hostname values | Validation is skipped when `!RequiresSshSession && !SftpUsesDefaultHost`. When sftp uses default host, validation stays — user must provide a valid hostname for SFTP to connect to. |
| `sftp` without explicit credentials fails at runtime | Same as today — `SftpCommand` already reports "requires username/password" when credentials missing (lines 58-60). The `SftpUsesDefaultCredentials` flag enables pre-flight warnings but runtime validation is the safety net. |

## Resolved Questions
- **Host_IP validation**: `host.IsValid()` is skipped when `!RequiresSshSession && !SftpUsesDefaultHost`. When sftp uses the default host (falls back to Host_IP), validation stays to ensure SFTP can connect. When no ssh/sftp-with-default-host is needed, arbitrary values (URLs, reference IDs) are allowed.
- **SFTP credential validation**: When sftp steps don't specify their own username/password, the existing runtime validation in `SftpCommand` (lines 58-60) catches empty credentials. The `SftpUsesDefaultCredentials` flag enables the system to know this upfront but does not add new pre-flight blocking — runtime behavior is unchanged.
