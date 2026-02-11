## 1. SSH Requirement Analysis

### 1.1 Add `SshRequirementResult` class
**File**: `Services/Scripting/ScriptDependencyAnalyzer.cs`
**Location**: Before the `ScriptDependencyAnalyzer` class (after `ColumnDependencyResult` at line 20)
**Code**:
```csharp
public class SshRequirementResult
{
    public bool RequiresSshSession { get; set; }      // true if any StepType.Send found
    public bool UsesSftp { get; set; }                // true if any StepType.Sftp found
    public bool SftpUsesDefaultHost { get; set; }     // true if any sftp step omits Host (falls back to Host_IP)
    public bool SftpUsesDefaultCredentials { get; set; } // true if any sftp step omits Username or Password
}
```
- [x] 1.1 Add `SshRequirementResult` class

### 1.2 Add `AnalyzeSshRequirements()` method
**File**: `Services/Scripting/ScriptDependencyAnalyzer.cs`
**Location**: After `AnalyzePresets()` overloads (after line 124)
**Pattern**: Mirror existing `AnalyzeScript()`/`AnalyzeSteps()` recursive structure (line 56/126)
**Signature**:
```csharp
public SshRequirementResult AnalyzeSshRequirements(Script script)
```
**Private helper**:
```csharp
private void AnalyzeSshRequirementsInSteps(List<ScriptStep>? steps, SshRequirementResult result)
```
**Logic**:
- Walk steps, check `step.GetStepType()`:
  - `StepType.Send` → `result.RequiresSshSession = true`
  - `StepType.Sftp` → `result.UsesSftp = true`, then inspect `step.Sftp` options:
    - If `step.Sftp.Host` is null or whitespace → `result.SftpUsesDefaultHost = true` (will fall back to `Host_IP` at runtime via `SftpCommand.ResolveEndpoint()` line 120-121)
    - If `step.Sftp.Username` is null or whitespace → `result.SftpUsesDefaultCredentials = true` (will fall back to context `username` at runtime via line 130-132)
    - If `step.Sftp.Password` is null or whitespace → `result.SftpUsesDefaultCredentials = true` (will fall back to context `password` at runtime via line 134-136)
- Recurse into: `step.Then`, `step.Else`, `step.Do`, `step.Try`, `step.Catch`, `step.Finally`
- For `step.Elif`: iterate each branch and recurse into `branch.Then`
- Short-circuit when `RequiresSshSession && UsesSftp && SftpUsesDefaultHost && SftpUsesDefaultCredentials` are all true
- [x] 1.2 Add `AnalyzeSshRequirements()` and `AnalyzeSshRequirementsInSteps()` methods

### 1.3 Unit tests for SSH analysis
**File**: `SSH_Helper.Tests/Scripting/ScriptDependencyAnalyzerTests.cs`
**Pattern**: Follow existing test style (line 1-59) — create `ScriptDependencyAnalyzer`, parse YAML via `new ScriptParser().Parse()`, call `AnalyzeSshRequirements()`, assert with FluentAssertions
**Test cases**:
1. `AnalyzeSshRequirements_SendAtRoot_RequiresSshSession` — `steps: [- send: "show ver"]` → `RequiresSshSession == true`
2. `AnalyzeSshRequirements_OnlyLocalCommands_NoSshRequired` — `steps: [- http: {url: ...}, - print: "done", - set: "x = 1"]` → `RequiresSshSession == false`
3. `AnalyzeSshRequirements_SendInIfThen_RequiresSshSession` — `send` nested in `if.then` → detected
4. `AnalyzeSshRequirements_SendInForeachDo_RequiresSshSession` — `send` nested in `foreach.do` → detected
5. `AnalyzeSshRequirements_SendInWhileDo_RequiresSshSession` — `send` nested in `while.do` → detected
6. `AnalyzeSshRequirements_SendInTryBlock_RequiresSshSession` — `send` inside `try` → detected
7. `AnalyzeSshRequirements_SendInCatchBlock_RequiresSshSession` — `send` inside `catch` → detected
8. `AnalyzeSshRequirements_SendInElifBranch_RequiresSshSession` — `send` inside `elif[].then` → detected
9. `AnalyzeSshRequirements_SendInElseBlock_RequiresSshSession` — `send` inside `else` → detected
10. `AnalyzeSshRequirements_SftpOnly_DetectsSftpNoSsh` — `sftp` without `send` → `RequiresSshSession == false, UsesSftp == true`
11. `AnalyzeSshRequirements_EmptyScript_NothingRequired` — empty `steps: []` → all false
12. `AnalyzeSshRequirements_SendAndSftp_BothDetected` — both present → both true
13. `AnalyzeSshRequirements_DeeplyNested_Detected` — `send` inside `try > foreach > if.then` → detected
14. `AnalyzeSshRequirements_SftpWithoutHost_SftpUsesDefaultHost` — `sftp` step with no `host:` specified → `SftpUsesDefaultHost == true`
15. `AnalyzeSshRequirements_SftpWithExplicitHost_NoDefaultHost` — `sftp` step with `host: "10.0.0.1"` → `SftpUsesDefaultHost == false`
16. `AnalyzeSshRequirements_SftpWithoutUsername_SftpUsesDefaultCredentials` — `sftp` step with no `username:` → `SftpUsesDefaultCredentials == true`
17. `AnalyzeSshRequirements_SftpWithoutPassword_SftpUsesDefaultCredentials` — `sftp` step with no `password:` → `SftpUsesDefaultCredentials == true`
18. `AnalyzeSshRequirements_SftpWithExplicitCredentials_NoDefaultCredentials` — `sftp` step with both `username:` and `password:` specified → `SftpUsesDefaultCredentials == false`
19. `AnalyzeSshRequirements_MultipleSftpSteps_AnyDefaultSetsFlag` — two sftp steps, one with explicit host, one without → `SftpUsesDefaultHost == true` (any sftp using defaults sets the flag)
- [x] 1.3 Add 19 unit tests for SSH requirement analysis

## 2. Local Execution Path

### 2.1 Add `ExecuteScriptLocal()` method
**File**: `Services/SshExecutionService.cs`
**Location**: After `ExecuteScriptWithoutPool()` (after line 1081)
**Signature**:
```csharp
private void ExecuteScriptLocal(
    HostConnection host,
    Script script,
    string username,
    string password,
    StringBuilder outputBuilder,
    CancellationToken cancellationToken,
    bool showHeader = true)
```
**Implementation** (mirrors `ExecuteScriptWithoutPool` at line 955 but without SSH):
1. Emit progress: `OnProgressChanged(host, $"Running locally for {host} (no SSH required)", false, false)`
2. Build header if `showHeader && !script.NoBanner`:
   - Format: `"#################### LOCAL SCRIPT: {host} {scriptName} ####################"` with separator lines
   - Append to `outputBuilder`, emit via `OnOutputReceived(host, ...)`
3. Create context: `var context = new ScriptContext(host.Variables);`
4. Set `context.Session = null;` (explicit)
5. Set `context.DebugMode = DebugMode;`
6. Call `SeedConnectionVariables(context, host, username, password);` (line 1083 — seeds Host_IP, username, password into context)
7. Wire events (same pattern as line 1056-1073):
   - `context.OutputReceived += ...` → append to outputBuilder + `OnOutputReceived(host, ...)`
   - `context.ColumnUpdateRequested += ...` → `OnColumnUpdateRequested(host, ...)`
   - `context.EnvironmentUpdateRequested += ...` → `OnEnvironmentVariableUpdateRequested(host, ...)`
8. Execute: `var executor = new ScriptExecutor(); executor.ExecuteAsync(script, context, cancellationToken).GetAwaiter().GetResult();`
9. No client to disconnect — method ends

**What's NOT included** (because no SSH):
- No `client.Connect()`, `client.Login()`, `client.StartScripting()`
- No `SshShellSession`, no `session.InitializeAsync()`
- No `session.DebugOutput` subscription
- No `session.CommandCompleted` subscription (local commands don't fire this)
- No `client.Disconnect()`
- [x] 2.1 Add `ExecuteScriptLocal()` private method

### 2.2 Modify `ExecuteScriptOnHost()` to branch on analysis
**File**: `Services/SshExecutionService.cs`
**Location**: Line 780
**Change**: Add optional parameter and branch in try block
```csharp
private ExecutionResult ExecuteScriptOnHost(
    HostConnection host,
    Script script,
    string defaultUsername,
    string defaultPassword,
    SshTimeoutOptions timeouts,
    CancellationToken cancellationToken,
    bool showHeader = true,
    SshRequirementResult? sshRequirement = null)  // NEW parameter
```
**Inside try block** (line 799-808), replace with:
```csharp
try
{
    if (sshRequirement != null && !sshRequirement.RequiresSshSession)
    {
        ExecuteScriptLocal(host, script, username, password, outputBuilder, cancellationToken, showHeader);
    }
    else if (UseConnectionPooling && _connectionPool != null)
    {
        ExecuteScriptWithPool(host, script, username, password, timeouts, outputBuilder, cancellationToken, showHeader);
    }
    else
    {
        ExecuteScriptWithoutPool(host, script, username, password, timeouts, outputBuilder, cancellationToken, showHeader);
    }
    result.Success = true;
}
```
**Error handling**: Existing catch blocks (SshException auth/connection/timeout, SocketException, OperationCanceledException, generic Exception) remain unchanged. For local execution, SSH-specific exceptions never fire. `OperationCanceledException` still fires if user cancels. Generic `Exception` catches any local command errors (e.g., HTTP timeout, DNS failure).
- [x] 2.2 Modify `ExecuteScriptOnHost()` signature and try block

### 2.3 Thread analysis through `ExecuteScriptAsync()`
**File**: `Services/SshExecutionService.cs`
**Location**: Line 318 (`ExecuteScriptAsync`)
**After** parse/validate succeeds (line 334-339), **before** the host loop (line 363), add:
```csharp
var analyzer = new ScriptDependencyAnalyzer();
var sshRequirement = analyzer.AnalyzeSshRequirements(script);
```
**In the host loop** (line 373-374), pass the result:
```csharp
var result = await Task.Run(() =>
    ExecuteScriptOnHost(host, script, defaultUsername, defaultPassword, timeouts, cancellationToken, showHeader, sshRequirement));
```
**Also**: When SSH is not required AND sftp doesn't use the default host, skip the `host.IsValid()` check (line 370) so that arbitrary `Host_IP` values (URLs, reference IDs) are allowed. But when sftp falls back to `Host_IP` as its connection host, validation must remain. Wrap the `IsValid()` check:
```csharp
var needsValidHost = sshRequirement.RequiresSshSession || sshRequirement.SftpUsesDefaultHost;
if (needsValidHost && !host.IsValid())
    continue;
```
- [x] 2.3 Add analysis call, relax host validation, and pass through in `ExecuteScriptAsync()`

### 2.4 Thread analysis through `ExecuteScriptTextOnHost()`
**File**: `Services/SshExecutionService.cs`
**Location**: Line 630 (`ExecuteScriptTextOnHost`)
**After** parse/validate succeeds (line 643-648), **before** calling `ExecuteScriptOnHost` (line 665), add analysis and pass:
```csharp
var analyzer = new ScriptDependencyAnalyzer();
var sshRequirement = analyzer.AnalyzeSshRequirements(script);
return ExecuteScriptOnHost(host, script, defaultUsername, defaultPassword, timeouts, cancellationToken, showHeader, sshRequirement);
```
This covers the `ExecuteFolderAsync()` path (folder execution calls `ExecutePresetOnHostAsync()` → `ExecutePresetOnHost()` → `ExecuteScriptTextOnHost()`).
- [x] 2.4 Add analysis call and pass through in `ExecuteScriptTextOnHost()`

## 3. Verification
- [ ] 3.1 Run full test suite: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` — no regressions
  - Attempted on 2026-02-10; existing unrelated failures remain (`PresetManagerTests` access denied to `%LOCALAPPDATA%\\SSH_Helper\\config.json`, and `ScriptValidationFormatterTests.FormatFailureMessage_WithNoErrors_ReturnsFallback`)
- [x] 3.2 Run focused new tests: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptDependencyAnalyzer"`
- [ ] 3.3 Manual test — local script with host rows: script uses `http`/`print`/`set`, grid has hosts with URLs/IDs in Host_IP → runs without SSH, `LOCAL SCRIPT:` header, grid columns available as variables
- [ ] 3.4 Manual test — SSH script unchanged: script with `send` commands, grid has hosts → SSH connection established normally, `SCRIPT:` header
- [ ] 3.5 Manual test — mixed script: both `send` and `http` → SSH connection established
- [ ] 3.6 Manual test — folder execution: folder with local-only presets → each preset runs locally
- [ ] 3.7 Manual test — cancellation: run local script, click Stop → `OperationCanceledException` handled, execution stops
- [ ] 3.8 Manual test — history: local execution appears in history list with output and execution details
- [ ] 3.9 Manual test — Host_IP with non-hostname value: put a URL like `https://api.example.com` in Host_IP, run local script (no sftp) → executes successfully, `${Host_IP}` resolves to the URL
- [ ] 3.10 Manual test — sftp without explicit host: sftp step omits `host:`, Host_IP has valid hostname → sftp connects to Host_IP
- [ ] 3.11 Manual test — sftp without explicit host, invalid Host_IP: sftp step omits `host:`, Host_IP has URL → host skipped (IsValid fails because sftp needs valid host)
- [ ] 3.12 Manual test — sftp with explicit host, arbitrary Host_IP: sftp step has `host: "10.0.0.5"`, Host_IP has arbitrary value → host_IP validation skipped, sftp connects to 10.0.0.5
- [ ] 3.13 Manual test — sftp without credentials: sftp step omits `username:`/`password:`, grid columns empty → sftp reports "requires username/password" at runtime
