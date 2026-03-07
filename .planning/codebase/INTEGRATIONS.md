# External Integrations

**Analysis Date:** 2026-03-06

## APIs & External Services

**GitHub API:**
- Used for auto-update checks and downloads
- Service: `Services/UpdateService.cs`
- Endpoint: `https://api.github.com/repos/{owner}/{repo}/releases/latest`
- Auth: None required (public API, rate-limited)
- Headers: `User-Agent` and `Accept: application/vnd.github.v3+json`
- Features: Version comparison, asset download with SHA256 verification, exponential backoff retry

**Webhook HTTP Requests (Scripting Engine):**
- Outbound HTTP requests from script `webhook` commands
- Service: `Services/Scripting/Commands/WebhookCommand.cs`
- Supports: GET, POST, and other HTTP methods
- Headers and body support variable substitution from script context
- Timeout: Configurable per-command (default 30s)
- Note: No SSRF protection by design (allows localhost and internal IPs for infrastructure automation)

## SSH Connections

**SSH.NET Library:**
- Client: `Renci.SshNet` namespace
- Service: `Services/SshExecutionService.cs`
- Purpose: Primary SSH command execution against remote hosts
- Auth: Username/password from grid columns or Windows Credential Manager
- Connection details parsed from `Models/HostConnection.cs` (supports `host:port` format)

**Rebex SSH Terminal:**
- Client: `Rebex.Net.Ssh`, `Rebex.TerminalEmulation`
- Services: `Services/SshShellSession.cs`, `Services/SshConnectionPool.cs`
- Purpose: Interactive shell sessions with Scripting API for prompt detection, pager handling, and pattern matching
- License: Via `REBEX_LICENSE_KEY` env var or `rebex.key` file
- Connection pooling: `Services/SshConnectionPool.cs` manages reusable connections with health checks and idle cleanup

**SSH Config File Parsing:**
- Service: `Services/SshConfigService.cs`
- Parser: `Utilities/SshConfigParser.cs`
- Reads: `%USERPROFILE%\.ssh\config`
- Models: `Models/SshConfigFile.cs`, `Models/SshHostConfig.cs`
- Caching: In-memory with 5-second staleness check

## Data Storage

**Local Filesystem (JSON):**
- Config: `%LocalAppData%\SSH_Helper\config.json`
- Service: `Services/ConfigurationService.cs`
- Model: `Models/AppConfiguration.cs`
- Format: JSON via Newtonsoft.Json
- Features: Caching, legacy format migration, GZip-compressed saved state

**Execution History:**
- Index: `%LocalAppData%\SSH_Helper\history.index.json`
- Run data: `%LocalAppData%\SSH_Helper\history/` (per-run JSON files)
- Service: `Services/HistoryStorageService.cs`
- Models: `Models/HistoryIndex.cs`, `Models/HistoryRunPayload.cs`

**CSV Files:**
- Service: `Services/CsvManager.cs`
- Purpose: Import/export host grids
- Format: Standard CSV with proper quoting/escaping
- Required column: `Host_IP`

**No external databases.** All persistence is local filesystem.

## Authentication & Identity

**Windows Credential Manager:**
- Provider: `Services/Credentials/CredentialManagerProvider.cs`
- Interface: `Services/Credentials/ICredentialProvider.cs`
- Target naming: `Services/Credentials/CredentialTargets.cs`
- Uses P/Invoke to `advapi32.dll` (`CredRead`, `CredWrite`, `CredDelete`, `CredFree`)
- Stores SSH credentials per-host in Windows Credential Manager
- Only available on Windows (`OperatingSystem.IsWindows()` check)

**SSH Authentication:**
- Username/password from DataGridView columns (`username`, `password`)
- Fallback to global username from `AppConfiguration.Username`
- Credential Manager lookup via target naming convention

## Monitoring & Observability

**Error Tracking:**
- None (no external error tracking service)

**Logs:**
- Console/debug output only
- Event-driven progress reporting via `SshProgressEventArgs` and `SshOutputEventArgs`
- Scripting engine log command: `Services/Scripting/Commands/LogCommand.cs`

## CI/CD & Deployment

**Hosting:**
- Desktop application (no server hosting)
- Distributed as self-contained single-file `.exe`

**CI Pipeline:**
- GitHub Actions: `.github/workflows/build-release.yml`
- Trigger: Push tags matching `v*` or manual `workflow_dispatch`
- Runner: `windows-latest`
- Steps: Restore, publish (single-file), upload artifact
- Release: Auto-creates GitHub Release with exe, SHA256 checksum, README, and SCRIPTING.md
- Secrets: `REBEX_LICENSE_KEY` (build), `GITHUB_TOKEN` (release creation)

## Environment Configuration

**Required env vars:**
- None required for basic operation

**Optional env vars:**
- `REBEX_LICENSE_KEY` - Rebex SSH library license (build-time only, embedded in assembly)

**Secrets location:**
- SSH credentials: Windows Credential Manager
- Rebex license: Assembly metadata (injected at build) or `rebex.key` file
- GitHub Actions secrets: `REBEX_LICENSE_KEY`, `GITHUB_TOKEN`

## Webhooks & Callbacks

**Incoming:**
- None (desktop application, no server)

**Outgoing:**
- Script-driven HTTP webhooks via `Services/Scripting/Commands/WebhookCommand.cs`
- Supports variable substitution in URL, headers, and body
- Configurable method, headers, body, and timeout

## File System Access (Scripting)

**Script File Operations:**
- Read files: `Services/Scripting/Commands/ReadFileCommand.cs`
- Access validation: `Services/Scripting/ScriptFileAccessValidator.cs`
- Scripts can read local files and use content in command execution

## Native Interop

**Win32 P/Invoke:**
- Credential Manager: `advapi32.dll` (CredRead, CredWrite, CredDelete, CredFree)
- Scintilla editor: Native DLLs loaded via `Utilities/ScintillaNativeBootstrap.cs`

---

*Integration audit: 2026-03-06*
