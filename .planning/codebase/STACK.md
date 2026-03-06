# Technology Stack

**Analysis Date:** 2026-03-06

## Languages

**Primary:**
- C# 12 (implicit via .NET 8) - All application and test code

**Secondary:**
- YAML - Script definitions parsed by the scripting engine (`Services/Scripting/`)
- JSON - Configuration persistence (`config.json` via Newtonsoft.Json)

## Runtime

**Environment:**
- .NET 8.0 (Windows-specific: `net8.0-windows`)
- Windows Forms UI framework

**Package Manager:**
- NuGet (via `dotnet restore`)
- Lockfile: Not present (no `packages.lock.json`)

## Frameworks

**Core:**
- Windows Forms (.NET 8.0) - Desktop GUI framework
- `SSH_Helper.csproj` line 8: `<UseWindowsForms>true</UseWindowsForms>`

**Testing:**
- xUnit 2.7.0 - Test runner (`SSH_Helper.Tests/SSH_Helper.Tests.csproj`)
- FluentAssertions 6.12.0 - Assertion library
- Moq 4.20.70 - Mocking framework
- Xunit.StaFact 1.1.11 - STA-thread support for WinForms tests
- Microsoft.NET.Test.Sdk 17.9.0 - Test infrastructure
- coverlet.collector 6.0.1 - Code coverage

**Build/Dev:**
- dotnet CLI - Build and publish toolchain
- MSBuild - Project build system
- Visual Studio 2022 (v17.9) - IDE (per solution file)

## Key Dependencies

**Critical:**
- **SSH.NET** 2024.1.0 - Primary SSH client library for command execution (`Services/SshExecutionService.cs`)
- **Rebex.SshShell** 7.0.9448 - Advanced SSH terminal emulation with Scripting API for prompt detection and shell sessions (`Services/SshShellSession.cs`, `Services/SshConnectionPool.cs`)
- **Newtonsoft.Json** 13.0.3 - Configuration serialization/deserialization (`Services/ConfigurationService.cs`, `Services/HistoryStorageService.cs`)

**Infrastructure:**
- **Scintilla5.NET** 6.1.1 - Code editor control for script editing (`Services/Editor/`, `Utilities/ScintillaNativeBootstrap.cs`). Native DLLs (Scintilla.dll, Lexilla.dll) are embedded as resources for single-file publish.
- **YamlDotNet** 16.3.0 - YAML parsing for the scripting engine (`Services/Scripting/ScriptParser.cs`)

## Configuration

**Application Config:**
- Persisted to `%LocalAppData%\SSH_Helper\config.json`
- Root model: `Models/AppConfiguration.cs`
- Managed by `Services/ConfigurationService.cs` with in-memory caching
- Legacy format migration (string presets to PresetInfo objects) handled on load

**Build Config:**
- `SSH_Helper.csproj` - Main project file
- `SSH_Helper.Tests/SSH_Helper.Tests.csproj` - Test project
- `SSH_Helper.sln` - Solution file (2 projects: main + tests)

**Rebex Licensing:**
- License key injected via `REBEX_LICENSE_KEY` environment variable or `rebex.key` file
- Embedded as assembly metadata at build time (`SSH_Helper.csproj` lines 36-41)

**Build Timestamp:**
- UTC timestamp embedded as assembly metadata at compile time (`SSH_Helper.csproj` lines 24-26)

## Publish Configuration

**Release Build:**
- Single-file publish: `PublishSingleFile=true`
- Self-contained: `SelfContained=true`
- Target: `win-x64`
- Compressed: `EnableCompressionInSingleFile=true`
- Native libraries included: `IncludeNativeLibrariesForSelfExtract=true`

## Platform Requirements

**Development:**
- Windows (WinForms dependency)
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension
- Optional: Rebex license key for advanced SSH features

**Production:**
- Windows x64
- No .NET runtime required (self-contained publish)
- Network access to SSH target hosts

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

*Stack analysis: 2026-03-06*
