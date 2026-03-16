# Technology Stack

**Analysis Date:** 2026-03-07

## Languages

**Primary:**
- C# 12 (implicit via .NET 8 SDK) - All application and test code

**Secondary:**
- YAML - Script definition files (`ScriptSamples/**/*.yaml`), CI/CD workflows (`.github/workflows/build-release.yml`)

## Runtime

**Environment:**
- .NET 8.0 (Windows-specific: `net8.0-windows`)
- Windows Forms UI framework (`<UseWindowsForms>true</UseWindowsForms>`)

**Package Manager:**
- NuGet (via `dotnet restore`)
- Lockfile: Not present (no `packages.lock.json`)

**Solution:**
- `SSH_Helper.sln` - Visual Studio 2022 solution (format version 17)
- Two projects: `SSH_Helper.csproj` (main app), `SSH_Helper.Tests/SSH_Helper.Tests.csproj` (tests)
- POC project: `RebexPOC/RebexPOC.csproj` (console app for Rebex experimentation)

## Frameworks

**Core:**
- Windows Forms (.NET 8.0) - Desktop UI framework
- `<UseWindowsForms>true</UseWindowsForms>` in `SSH_Helper.csproj`

**Testing:**
- xUnit 2.7.0 - Test framework
- FluentAssertions 6.12.0 - Assertion library
- Moq 4.20.70 - Mocking framework
- Xunit.StaFact 1.1.11 - STA thread support for WinForms tests (`[WinFormsFact]`/`[WinFormsTheory]`)
- Microsoft.NET.Test.Sdk 17.9.0 - Test SDK
- coverlet.collector 6.0.1 - Code coverage

**Build/Dev:**
- dotnet CLI - Build and publish toolchain
- MSBuild - Build system (implicit via SDK-style projects)
- Visual Studio 2022 - IDE (solution format version 17)

## Key Dependencies

**Critical (SSH/Terminal):**
- SSH.NET 2024.1.0 (`Renci.SshNet`) - SSH client library for SFTP file transfers
  - Used in: `Services/Scripting/Commands/SftpCommand.cs`
- Rebex.SshShell 7.0.9448 (`Rebex.Net`, `Rebex.TerminalEmulation`) - Advanced SSH shell sessions with scripting API
  - Used in: `Services/SshShellSession.cs`, `Services/SshConnectionPool.cs`, `Services/SshExecutionService.cs`
  - Requires license key: injected via `REBEX_LICENSE_KEY` env var or `rebex.key` file
  - Scripting API (`Rebex.TerminalEmulation.Scripting`) provides pattern-based terminal matching

**Editor:**
- Scintilla5.NET 6.1.1 (`ScintillaNET`) - Code editor component for YAML script editing
  - Native DLLs (`Scintilla.dll`, `Lexilla.dll`) embedded as resources for win-x64
  - Bootstrap: `Utilities/ScintillaNativeBootstrap.cs` handles native lib extraction
  - Used by: `Services/Editor/YamlSshSyntaxHighlighter.cs`, `Services/Editor/ScriptAutocompleteProvider.cs`

**Serialization:**
- Newtonsoft.Json 13.0.3 - JSON serialization for config persistence (`Services/ConfigurationService.cs`, `Services/HistoryStorageService.cs`)
- System.Text.Json (built-in) - Used for GitHub API responses (`Services/UpdateService.cs`), HTTP command responses
- YamlDotNet 16.3.0 - YAML parsing for script files (`Services/Scripting/ScriptParser.cs`)

**Infrastructure (built-in .NET):**
- System.Net.Http - HTTP client for webhooks, HTTP commands, and update checking
- System.Net.NetworkInformation - ICMP ping (`Services/Scripting/Commands/PingCommand.cs`)
- System.Net.Sockets - TCP port checking (`Services/Scripting/Commands/PortcheckCommand.cs`)
- System.Runtime.InteropServices - P/Invoke for Windows Credential Manager (`Services/Credentials/CredentialManagerProvider.cs`)
- System.IO.Compression - GZip for preset export/import and saved state compression
- System.Security.Cryptography - SHA256 for update verification (`Services/UpdateService.cs`)

## Configuration

**Application Config:**
- Persisted to `%LocalAppData%\SSH_Helper\config.json` via `Services/ConfigurationService.cs`
- Root model: `Models/AppConfiguration.cs`
- Includes: presets, environments, window state, font settings, SSH config settings, credential settings, update settings, history settings, editor settings
- Legacy format migration handled automatically on load (string presets to `PresetInfo` objects)
- Saved state uses GZip compression (`SavedStateCompressed` field with `gz64:` prefix)

**Environment Variables:**
- `REBEX_LICENSE_KEY` - Rebex SSH library license key (embedded at build time via `AssemblyMetadataAttribute`)

**License Key Files:**
- `rebex.key` - Local development Rebex license (copied to output if present, gitignored)

**Build Configuration:**
- `SSH_Helper.csproj` lines 15-21: Release mode publishes as single-file, self-contained, win-x64
- Build timestamp embedded as assembly metadata at compile time
- `InternalsVisibleTo("SSH_Helper.Tests")` enables test access to internal types

## Scripting Engine

**YAML Script System:**
- Parser: `Services/Scripting/ScriptParser.cs`
- Executor: `Services/Scripting/ScriptExecutor.cs`
- Context: `Services/Scripting/ScriptContext.cs`
- Expression evaluator: `Services/Scripting/ExpressionEvaluator.cs`
- Value resolver: `Services/Scripting/ValueResolver.cs`
- JSON utilities: `Services/Scripting/JsonUtilities.cs`, `Services/Scripting/JsonPathNavigator.cs`
- Validation: `Services/Scripting/ScriptValidationFormatter.cs`, `Services/Scripting/ScriptDependencyAnalyzer.cs`
- Regex defaults: `Services/Scripting/ScriptRegexDefaults.cs`
- File access: `Services/Scripting/ScriptFileAccessValidator.cs`
- Editor integration: `Services/Editor/ScriptEditorValidationService.cs`, `Services/Editor/ScriptAutocompleteProvider.cs`

**Script Commands (`Services/Scripting/Commands/`):**
- Flow control: `IfCommand`, `ForeachCommand`, `WhileCommand`, `SwitchCommand`, `TryCommand`, `BreakCommand`, `ContinueCommand`, `ExitCommand`
- Data: `SetCommand`, `ExtractCommand`, `ParseCommand`, `ReadFileCommand`, `WriteFileCommand`
- Network: `HttpCommand`, `WebhookCommand`, `DnsCommand`, `PingCommand`, `PortcheckCommand`, `SftpCommand`
- Interactive: `InputCommand`, `ChooseCommand`, `ConfirmCommand`, `MultiselectCommand`, `InteractiveCommand`
- Output: `PrintCommand`, `LogCommand`, `TableCommand`, `AssertCommand`
- SSH: `SendCommand`, `WaitCommand`
- State: `UpdateColumnCommand`, `UpdateEnvironmentCommand`
- Execution: `ParallelCommand`
- Config parsers: `Services/Scripting/Parsers/FortiGateParser.cs`, `Services/Scripting/Parsers/IConfigParser.cs`, `Services/Scripting/Parsers/ParserFactory.cs`

**Script Models:**
- `Services/Scripting/Models/Script.cs` - Parsed script representation
- `Services/Scripting/Models/ScriptStep.cs` - Individual script step
- `Services/Scripting/Models/DebugState.cs` - Debug/trace state

## Platform Requirements

**Development:**
- .NET 8.0 SDK
- Windows OS (WinForms dependency)
- Visual Studio 2022 or VS Code with C# extension
- Optional: Rebex license key for full SSH functionality

**Production:**
- Windows x64 (self-contained publish includes .NET runtime)
- Single-file executable: `SSH_Helper.exe`
- No additional runtime installation required (self-contained deployment)

**CI/CD:**
- GitHub Actions (`.github/workflows/build-release.yml`)
- Build runner: `windows-latest`
- Release runner: `ubuntu-latest` (for SHA256 checksum generation)
- Trigger: Git tags matching `v*` pattern
- Secrets: `REBEX_LICENSE_KEY`, `GITHUB_TOKEN`

## Build Commands

```bash
# Build
dotnet build SSH_Helper.sln

# Build release
dotnet build SSH_Helper.sln -c Release

# Run application
dotnet run --project SSH_Helper.csproj

# Run tests
dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj

# Publish single-file executable
dotnet publish SSH_Helper.csproj -c Release -o ./publish
```

---

*Stack analysis: 2026-03-07*
