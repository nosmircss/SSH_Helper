# External Integrations

**Analysis Date:** 2026-03-07

## APIs & External Services

**GitHub API:**
- Purpose: Auto-update checking and release download
- SDK/Client: `System.Net.Http.HttpClient` with `System.Text.Json` deserialization
- Implementation: `Services/UpdateService.cs`
- Endpoint: `https://api.github.com/repos/{owner}/{repo}/releases/latest`
- Auth: None (public repo, unauthenticated GitHub API)
- Headers: `User-Agent: {repo}/{version}`, `Accept: application/vnd.github.v3+json`
- Config: `Models/AppConfiguration.cs` > `UpdateSettings` class
  - `GitHubOwner`: defaults to `"nosmircss"`
  - `GitHubRepo`: defaults to `"SSH_Helper"`
  - `CheckOnStartup`: defaults to `true`
- Features: Version comparison, asset download with SHA256 verification, retry with exponential backoff (1s, 3s, 5s)

## SSH Connections

**Rebex SSH (Primary Shell Engine):**
- Library: Rebex.SshShell 7.0.9448
- Purpose: Interactive SSH shell sessions with terminal emulation and scripting API
- Implementation:
  - `Services/SshShellSession.cs` - Individual shell session management with pattern-based prompt detection
  - `Services/SshConnectionPool.cs` - Connection pooling with `ConcurrentDictionary` for reuse across command batches
  - `Services/SshExecutionService.cs` - Execution orchestration with event-driven progress reporting
  - `Services/SshTerminalOptionsFactory.cs` - Terminal option configuration
  - `Services/SshTimeoutOptions.cs` - Timeout configuration
- Auth methods: Password, SSH agent, SSH key (via `~/.ssh/config` integration)
- Config: `Models/AppConfiguration.cs` > `SshConfigSettings`, `CredentialSettings`
- License: Requires `REBEX_LICENSE_KEY` env var or `rebex.key` file
- Scripting API: `Rebex.TerminalEmulation.Scripting` for pattern-based terminal matching, pager handling, and prompt detection

**SSH.NET (SFTP Operations):**
- Library: SSH.NET 2024.1.0 (`Renci.SshNet`)
- Purpose: SFTP file transfers within scripts
- Implementation: `Services/Scripting/Commands/SftpCommand.cs`
- Operations: Upload and download with overwrite control and timeout
- Auth: Uses host connection credentials from script context

**SSH Config File Integration:**
- Parser: `Utilities/SshConfigParser.cs`
- Service: `Services/SshConfigService.cs`
- Models: `Models/SshConfigFile.cs`, `Models/SshHostConfig.cs`
- Reads: `%USERPROFILE%\.ssh\config`
- Applies: IdentityFile, algorithms, and other SSH settings to connections
- Toggle: `SshConfigSettings.EnableSshConfig` (default: `false`)
- Caching: In-memory with staleness check

## Script Commands (Network)

**HTTP Requests (`http` command):**
- Implementation: `Services/Scripting/Commands/HttpCommand.cs`
- Methods: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS
- Auth modes: none, basic, bearer
- Content types: json (`application/json`), form (`application/x-www-form-urlencoded`), text (`text/plain`), xml (`application/xml`)
- Response capture: Status code, body, and headers into script variables
- Testable: Uses injectable `Func<HttpOptions, HttpMessageHandler>` factory

**Webhook Requests (`webhook` command):**
- Implementation: `Services/Scripting/Commands/WebhookCommand.cs`
- Simpler than HTTP command, primarily for outbound notifications
- Static `HttpClient` reuse (Microsoft best practice)
- Variable substitution in URL, body, and headers
- Security note: No SSRF protection by design (allows localhost and RFC1918 for infrastructure automation)

**DNS Resolution (`dns` command):**
- Implementation: `Services/Scripting/Commands/DnsCommand.cs`
- Uses `System.Net.Dns` via injectable `IDnsResolver` abstraction
- Captures results as list variables

**ICMP Ping (`ping` command):**
- Implementation: `Services/Scripting/Commands/PingCommand.cs`
- Uses `System.Net.NetworkInformation.Ping` via injectable `IPingProbe` abstraction
- Captures status and round-trip metrics

**TCP Port Check (`portcheck` command):**
- Implementation: `Services/Scripting/Commands/PortcheckCommand.cs`
- Uses `System.Net.Sockets.TcpClient`
- Reports: open, closed, or timed out
- Default port: 22, default timeout: 5s

## Data Storage

**Configuration (JSON):**
- Location: `%LocalAppData%\SSH_Helper\config.json`
- Client: `Newtonsoft.Json` 13.0.3
- Service: `Services/ConfigurationService.cs`
- Model: `Models/AppConfiguration.cs`
- Contains: Presets, environments, window state, font settings, update settings, credential preferences, editor settings, SSH config settings
- Features: Caching, legacy format migration, GZip-compressed saved state (`gz64:` prefix)

**Execution History:**
- Index: `%LocalAppData%\SSH_Helper\history.index.json`
- Run data: `%LocalAppData%\SSH_Helper\history/` directory (per-run JSON payload files)
- Service: `Services/HistoryStorageService.cs`
- Models: `Models/HistoryIndex.cs`, `Models/HistoryRunPayload.cs`, `Models/HistoryListItem.cs`
- ID generation: `Services/HistoryIdGenerator.cs`

**CSV Host Files:**
- Service: `Services/CsvManager.cs`
- Format: Standard CSV with proper quoting/escaping
- Required column: `Host_IP`
- Optional columns with implemented connection semantics: `port`, `username`, `password`, `vault_path`
- Custom columns usable as `{{column_name}}` variables in scripts
- Freshness tracking: `Models/CsvFileFingerprint.cs`, `Utilities/CsvFileSyncEvaluator.cs`

**File Storage:**
- Local filesystem only, no external databases
- Script file I/O: `Services/Scripting/Commands/ReadFileCommand.cs`, `Services/Scripting/Commands/WriteFileCommand.cs`
- File access validation: `Services/Scripting/ScriptFileAccessValidator.cs`

**Caching:**
- In-memory config cache: `ConfigurationService._cachedConfig`
- SSH connection pooling: `Services/SshConnectionPool.cs`

## Authentication & Identity

**SSH Authentication:**
- Password-based: Entered in UI or from CSV `password` column or Windows Credential Manager
- SSH key-based: Via `~/.ssh/config` IdentityFile settings
- SSH agent: Optional preference (`CredentialSettings.PreferSshAgent`)
- Fallback chain: Per-host credential -> default credential -> UI-entered password

**Credential Storage (Windows Credential Manager):**
- Provider: `Services/Credentials/CredentialManagerProvider.cs`
- Interface: `Services/Credentials/ICredentialProvider.cs`
- Target naming: `Services/Credentials/CredentialTargets.cs`
  - Default password: `SSH_Helper:default`
  - Per-host password: `SSH_Helper:host:{host}|user:{username}`
- Win32 P/Invoke: `CredRead`, `CredWrite`, `CredDelete`, `CredFree` from `advapi32.dll`
- Toggle: `CredentialSettings.UseCredentialManager` (default: `false`)
- Platform check: `OperatingSystem.IsWindows()`

## Monitoring & Observability

**Error Tracking:**
- None (no external error tracking service)
- Errors reported via UI events and dialog messages

**Logs:**
- No structured logging framework
- Event-driven progress: `SshProgressEventArgs`, `SshOutputEventArgs` from `Services/SshExecutionService.cs`
- Update process: Optional logging via `UpdateSettings.EnableUpdateLog`
- Script engine: `log` command writes to script output (`Services/Scripting/Commands/LogCommand.cs`)

## CI/CD & Deployment

**Hosting:**
- Desktop application (no server hosting)
- Distributed via GitHub Releases as single-file executable

**CI Pipeline:**
- GitHub Actions: `.github/workflows/build-release.yml`
- Trigger: Push tags matching `v*` or manual `workflow_dispatch`
- Build job (`windows-latest`):
  - `dotnet restore` with `win-x64` runtime and Release config
  - `dotnet publish` producing single-file self-contained executable
  - Uploads build artifact
- Release job (`ubuntu-latest`, runs only for tag pushes):
  - Downloads build artifact
  - Copies `README.md` and `SCRIPTING.md` documentation
  - Generates `SHA256` checksum
  - Creates GitHub Release via `softprops/action-gh-release@v1`
- Secrets: `REBEX_LICENSE_KEY` (build), `GITHUB_TOKEN` (release creation)

**Auto-Update Flow:**
1. `UpdateService.CheckForUpdatesAsync()` queries GitHub API for latest release
2. Compares semantic version with current version
3. Downloads asset with retry and progress reporting (`DownloadProgressChanged` event)
4. Verifies SHA256 checksum against `.sha256` companion file
5. User-initiated install replaces current executable

## Environment Configuration

**Required env vars:**
- `REBEX_LICENSE_KEY` - Rebex SSH library license key (CI/CD builds; embedded at build time via `AssemblyMetadataAttribute`)

**Optional env vars:**
- None detected beyond Rebex license

**Secrets location:**
- GitHub Actions secrets: `REBEX_LICENSE_KEY`, `GITHUB_TOKEN`
- Local dev: `rebex.key` file in project root (gitignored)
- Runtime SSH passwords: Windows Credential Manager

## Webhooks & Callbacks

**Incoming:**
- None (desktop application, no server endpoints)

**Outgoing:**
- Script-driven webhooks via `webhook` command (`Services/Scripting/Commands/WebhookCommand.cs`)
- Script-driven HTTP requests via `http` command (`Services/Scripting/Commands/HttpCommand.cs`)
- Configurable URL, method, headers, body, and timeout per script step
- Variable substitution in all URL/header/body fields

## File System Access (Scripting)

**Script File Operations:**
- Read files: `Services/Scripting/Commands/ReadFileCommand.cs`
- Write files: `Services/Scripting/Commands/WriteFileCommand.cs`
- Access validation: `Services/Scripting/ScriptFileAccessValidator.cs`
- Scripts can read/write local files and use content in command execution

## Native Interop

**Win32 P/Invoke:**
- Credential Manager: `advapi32.dll` (`CredRead`, `CredWrite`, `CredDelete`, `CredFree`) in `Services/Credentials/CredentialManagerProvider.cs`
- Scintilla editor: Native `Scintilla.dll` and `Lexilla.dll` loaded via `Utilities/ScintillaNativeBootstrap.cs` (extracted from embedded resources)

---

*Integration audit: 2026-03-07*
