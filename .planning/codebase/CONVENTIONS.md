# Coding Conventions

**Analysis Date:** 2026-03-06

## Naming Patterns

**Files:**
- PascalCase for all `.cs` files: `ConfigurationService.cs`, `HostConnection.cs`, `InputValidator.cs`
- Test files mirror source file names with `Tests` suffix: `InputValidatorTests.cs`, `CsvManagerTests.cs`
- Interface files prefixed with `I`: `IScriptCommand.cs`, `ICredentialProvider.cs`, `IScriptEditor.cs`

**Classes:**
- PascalCase: `SshExecutionService`, `PresetManager`, `TerminalOutputProcessor`
- Interfaces prefixed with `I`: `IScriptCommand`, `ICredentialProvider`, `IScriptEditor`
- EventArgs classes use descriptive suffix: `SshProgressEventArgs`, `SshOutputEventArgs`, `EnvironmentChangedEventArgs`
- Enums use PascalCase singular: `PresetType`, `PresetSortMode`, `ScriptExitStatus`
- Test harness classes use descriptive suffix: `FontApplicationTestHarness`

**Methods:**
- PascalCase for all methods: `ExecutePresetAsync`, `BuildPromptRegex`, `IsValidIpAddress`
- Async methods use `Async` suffix: `ExecuteAsync`, `ExecutePresetAsync`
- Boolean methods use `Is`/`Has`/`Try` prefix: `IsValid()`, `IsValidPort()`, `TryDetectPrompt()`, `HasVariable()`
- Static factory methods: `CommandResult.Ok()`, `CommandResult.Fail()`, `FontSettings.CreateDefault()`
- Parse methods: `HostConnection.Parse()`, `InputValidator.ParseIntOrDefault()`

**Properties:**
- PascalCase: `IpAddress`, `Commands`, `ConfigFilePath`
- Boolean properties use `Is`/`Has`/`Enable`/`Use` prefix: `IsRunning`, `IsScript`, `IsFavorite`, `EnableSyntaxHighlighting`, `UseConnectionPooling`

**Variables/Fields:**
- Private fields use `_camelCase` prefix: `_configFilePath`, `_cachedConfig`, `_presets`, `_folders`
- Local variables use `camelCase`: `trimmed`, `varName`, `expression`
- Constants use PascalCase: `DefaultTabSize`, `SavedStateCompressionPrefix`, `MinValidationDebounceMs`
- `const` for compile-time, `static readonly` for runtime constants (e.g., compiled Regex)

**Namespaces:**
- Root: `SSH_Helper`
- Subdirectories map to nested namespaces: `SSH_Helper.Models`, `SSH_Helper.Services`, `SSH_Helper.Utilities`
- Deeper nesting follows folder path: `SSH_Helper.Services.Scripting.Commands`, `SSH_Helper.Services.Editor`
- Test namespace mirrors source: `SSH_Helper.Tests.Services`, `SSH_Helper.Tests.Utilities`, `SSH_Helper.Tests.Scripting`

## Code Style

**Formatting:**
- No explicit formatter config (`.editorconfig` not present). Default Visual Studio formatting.
- Braces on own lines for class/method declarations.
- Single-line property bodies use expression-bodied syntax: `public override string ToString() => Port == 22 ? IpAddress : $"{IpAddress}:{Port}";`
- Guard clauses return early: `if (string.IsNullOrWhiteSpace(hostWithPort)) return false;`
- Single-statement `if` blocks without braces for early returns: `if (config == null) return;`
- Multi-statement `if` blocks always use braces.

**Linting:**
- No explicit linting config. Relies on default compiler warnings.
- Nullable reference types enabled project-wide: `<Nullable>enable</Nullable>` in `SSH_Helper.csproj`
- Implicit usings enabled: `<ImplicitUsings>enable</ImplicitUsings>`

**Type Declarations:**
- File-scoped namespaces in test files: `namespace SSH_Helper.Tests.Utilities;`
- Block-scoped namespaces in source files: `namespace SSH_Helper.Services { ... }`
- Multiple related small types per file is acceptable for EventArgs (e.g., `SshProgressEventArgs`, `SshOutputEventArgs` in `SshExecutionService.cs`)
- Configuration sub-models grouped in `AppConfiguration.cs` (e.g., `WindowState`, `FontSettings`, `UpdateSettings`)

## Import Organization

**Order:**
1. System namespaces (`System`, `System.Text`, `System.Text.RegularExpressions`)
2. Third-party namespaces (`Newtonsoft.Json`, `Rebex.Net`, `FluentAssertions`)
3. Project namespaces (`SSH_Helper.Models`, `SSH_Helper.Services`, `SSH_Helper.Utilities`)

**Path Aliases:**
- Using alias for namespace conflicts: `using RebexScripting = Rebex.TerminalEmulation.Scripting;` in `SshExecutionService.cs`

## Error Handling

**Patterns:**
- Guard clauses with `ArgumentNullException`/`ArgumentException` for required constructor params:
  ```csharp
  _sshService = sshService ?? throw new ArgumentNullException(nameof(sshService));
  ```
- `ArgumentException` for invalid string inputs:
  ```csharp
  if (string.IsNullOrEmpty(directory))
      throw new ArgumentException("Config file path must include a directory.", nameof(configFilePath));
  ```
- Result objects for scripting commands instead of exceptions:
  ```csharp
  return Task.FromResult(CommandResult.Fail("Variable name cannot be empty"));
  return Task.FromResult(CommandResult.Ok());
  ```
- Swallowed exceptions for best-effort operations with comment:
  ```csharp
  catch
  {
      // Best effort: keep the in-memory state even if write-back fails.
  }
  ```
- Corrupt file recovery pattern: backup corrupt config before creating fresh default in `ConfigurationService.Load()`.
- `ConfigLoadError` property for deferred UI notification of load errors (not thrown, stored).

**Async error propagation:**
- Async methods use `CancellationToken` parameter (always last parameter).
- Task-based async pattern with `async Task<T>` return types.

## Logging

**Framework:** No logging framework. Uses event-driven output.

**Patterns:**
- Services raise events for output: `OutputReceived`, `ProgressChanged`.
- Script context emits debug output via `context.EmitOutput(message, ScriptOutputType.Debug)`.
- No `Console.WriteLine` or `Debug.WriteLine` in production code.

## Comments

**When to Comment:**
- XML doc comments (`///`) on all public classes, interfaces, properties, and methods.
- Summary comments explain purpose, param tags explain parameters.
- Inline comments for non-obvious logic: `// Normalize any newline style to LF first, then convert to Windows CRLF.`
- Comments on constant declarations: `// Max 1 hour`, `// Max 1 minute`.

**XML Doc Style:**
```csharp
/// <summary>
/// Validates an IP address string (with optional port).
/// </summary>
/// <param name="ipWithPort">IP address in format "x.x.x.x" or "x.x.x.x:port"</param>
/// <returns>True if valid</returns>
public static bool IsValidIpAddress(string ipWithPort)
```

## Function Design

**Size:** Methods are focused and short. Utility methods typically 5-20 lines. Service methods may be longer but are well-structured with early returns.

**Parameters:**
- Optional parameters use nullable types with defaults: `string? configFilePath = null`, `string? comment = null`
- `CancellationToken` always last async parameter.
- Constructor injection for dependencies (no DI container, manual wiring).

**Return Values:**
- Boolean for validation: `IsValidIpAddress`, `IsValidPort`
- Nullable for "not found" scenarios: `PresetManager.Get()` returns `PresetInfo?`
- Result objects for complex operations: `CommandResult`, `ExecutionResult`
- Static factory methods on result types: `CommandResult.Ok()`, `CommandResult.Fail(message)`

## Module Design

**Exports:**
- `public` for API surface. `internal` for implementation details (e.g., `NativeMethods`, `PresetNodeTag`).
- `InternalsVisibleTo("SSH_Helper.Tests")` in `SSH_Helper.csproj` for test access.
- `internal sealed` for test-only classes: `FontApplicationTestHarness`.

**Barrel Files:** Not used. Each class explicitly imports needed namespaces.

**Class Modifiers:**
- `sealed` for leaf service classes: `ExecutionCoordinator`, `EnvironmentService`
- `static` for stateless utility classes: `InputValidator`, `TerminalOutputProcessor`
- Records for immutable data: `record ExecutionPreparation(...)`

## Event Patterns

**Declaration:**
```csharp
public event EventHandler<SshProgressEventArgs>? ProgressChanged;
public event EventHandler? PresetsChanged;
```

**Custom EventArgs** for typed payloads. Plain `EventHandler` for simple notifications.

**Communication Direction:** Services raise events; UI subscribes. Never the reverse.

## Collection Initialization

- Use `new()` target-typed initialization: `new Dictionary<string, PresetInfo>()` or `= new();`
- Expose internal collections as `IReadOnlyDictionary`: `public IReadOnlyDictionary<string, PresetInfo> Presets => _presets;`

## String Handling

- `string.Empty` over `""` for defaults.
- `string.IsNullOrWhiteSpace()` for validation, `string.IsNullOrEmpty()` for emptiness checks.
- `StringComparison.Ordinal` for exact comparisons: `.Replace("\r\n", "\n", StringComparison.Ordinal)`
- `StringComparer.OrdinalIgnoreCase` for case-insensitive dictionaries.
- String interpolation for formatting: `$"{IpAddress}:{Port}"`.

## Regex Patterns

- Pre-compiled static readonly for hot paths:
  ```csharp
  private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
  ```
- `RegexOptions.Compiled | RegexOptions.CultureInvariant` for performance-critical patterns.

---

*Convention analysis: 2026-03-06*
