# Coding Conventions

**Analysis Date:** 2026-03-07

## Naming Patterns

**Files:**
- PascalCase for all `.cs` files: `ConfigurationService.cs`, `HostConnection.cs`, `InputValidator.cs`
- Test files mirror source file names with `Tests` suffix: `InputValidatorTests.cs`, `CsvManagerTests.cs`
- Interface files prefixed with `I`: `IScriptCommand.cs`, `ICredentialProvider.cs`, `IScriptEditor.cs`
- Feature-split test files use `{Class}{Feature}Tests.cs`: `ConfigurationServiceFontSettingsTests.cs`, `ConfigurationServiceWindowStateTests.cs`, `ConfigurationServiceCommandEditorSettingsTests.cs`

**Classes:**
- PascalCase: `SshExecutionService`, `PresetManager`, `TerminalOutputProcessor`
- Interfaces prefixed with `I`: `IScriptCommand`, `ICredentialProvider`, `IScriptEditor`
- EventArgs classes use descriptive suffix: `SshProgressEventArgs`, `SshOutputEventArgs`, `EnvironmentChangedEventArgs`, `SshColumnUpdateEventArgs`, `SshCommandCompletedEventArgs`
- Enums use PascalCase singular: `PresetType`, `PresetSortMode`, `ScriptExitStatus`, `CsvFileSyncStatus`, `DiagnosticSeverity`
- Test harness classes use descriptive suffix: `FontApplicationTestHarness`

**Methods:**
- PascalCase for all methods: `ExecutePresetAsync`, `BuildPromptRegex`, `IsValidIpAddress`
- Async methods use `Async` suffix: `ExecuteAsync`, `ExecutePresetAsync`, `ValidateNowAsync`
- Boolean methods use `Is`/`Has`/`Try` prefix: `IsValid()`, `IsValidPort()`, `TryDetectPrompt()`, `HasVariable()`, `TryLoadRunPayload()`
- Static factory methods: `CommandResult.Ok()`, `CommandResult.Fail()`, `FontSettings.CreateDefault()`, `CommandResult.Suppressed()`, `CommandResult.Break()`, `CommandResult.Continue()`
- Parse methods: `HostConnection.Parse()`, `InputValidator.ParseIntOrDefault()`
- Normalization methods: `Normalize()`, `CloneNormalized()`

**Properties:**
- PascalCase: `IpAddress`, `Commands`, `ConfigFilePath`
- Boolean properties use `Is`/`Has`/`Enable`/`Use` prefix: `IsRunning`, `IsScript`, `IsFavorite`, `EnableSyntaxHighlighting`, `UseConnectionPooling`, `HasHostResults`, `HasDetails`

**Variables/Fields:**
- Private fields use `_camelCase` prefix: `_configFilePath`, `_cachedConfig`, `_presets`, `_folders`
- Local variables use `camelCase`: `trimmed`, `varName`, `expression`
- Constants use PascalCase: `DefaultTabSize`, `SavedStateCompressionPrefix`, `MinValidationDebounceMs`
- `const` for compile-time constants, `static readonly` for runtime constants (e.g., compiled Regex)

**Namespaces:**
- Root: `SSH_Helper`
- Subdirectories map to nested namespaces: `SSH_Helper.Models`, `SSH_Helper.Services`, `SSH_Helper.Utilities`
- Deeper nesting follows folder path: `SSH_Helper.Services.Scripting.Commands`, `SSH_Helper.Services.Editor`, `SSH_Helper.Services.Credentials`, `SSH_Helper.Services.Terminal`
- Test namespace mirrors source: `SSH_Helper.Tests.Services`, `SSH_Helper.Tests.Utilities`, `SSH_Helper.Tests.Scripting`, `SSH_Helper.Tests.Editor`

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
- Multiple related small types per file is acceptable for EventArgs (e.g., `SshProgressEventArgs`, `SshOutputEventArgs`, `SshColumnUpdateEventArgs`, `SshCommandCompletedEventArgs` in `Services/SshExecutionService.cs`)
- Configuration sub-models grouped in `Models/AppConfiguration.cs` (e.g., `WindowState`, `FontSettings`, `UpdateSettings`, `CommandEditorSettings`, `SshConfigSettings`, `CredentialSettings`)
- Related enums placed at top of their file: `CsvFileSyncStatus` in `Utilities/CsvFileSyncEvaluator.cs`, `PresetSortMode` in `Models/AppConfiguration.cs`

## Import Organization

**Order:**
1. System namespaces (`System`, `System.Text`, `System.Text.RegularExpressions`, `System.Text.Json`)
2. Third-party namespaces (`Newtonsoft.Json`, `Rebex.Net`, `FluentAssertions`, `YamlDotNet`)
3. Project namespaces (`SSH_Helper.Models`, `SSH_Helper.Services`, `SSH_Helper.Utilities`)

**Path Aliases:**
- Using alias for namespace conflicts: `using RebexScripting = Rebex.TerminalEmulation.Scripting;` in `Services/SshExecutionService.cs`

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
- `ArgumentOutOfRangeException` for invalid numeric ranges:
  ```csharp
  if (interval <= TimeSpan.Zero)
      throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
  ```
- Result objects for scripting commands instead of exceptions:
  ```csharp
  return Task.FromResult(CommandResult.Fail("Variable name cannot be empty"));
  return Task.FromResult(CommandResult.Ok());
  return Task.FromResult(CommandResult.Suppressed(message));
  ```
- Swallowed exceptions for best-effort operations with comment:
  ```csharp
  catch
  {
      // Best effort: keep the in-memory state even if write-back fails.
  }
  ```
- Corrupt file recovery pattern: backup corrupt config to `.corrupt`, then create fresh default in `Services/ConfigurationService.cs` `Load()`.
- `ConfigLoadError` property for deferred UI notification of load errors (not thrown, stored).
- Wrapped exceptions for clarity: `throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);`
- Backup-before-save pattern: copy existing config to `.bak` before overwriting in `Services/ConfigurationService.cs` `Save()`.

**Async error propagation:**
- Async methods use `CancellationToken` parameter (always last parameter).
- Task-based async pattern with `async Task<T>` return types.
- `CancellationToken` passed through the call chain to enable cooperative cancellation.

## Logging

**Framework:** No logging framework. Uses event-driven output.

**Patterns:**
- Services raise events for output: `OutputReceived`, `ProgressChanged`, `CommandCompleted`.
- Script context emits debug output via `context.EmitOutput(message, ScriptOutputType.Debug)`.
- `System.Diagnostics.Debug.WriteLine()` for internal diagnostics (config parse errors, decompression failures) in `Services/ConfigurationService.cs`.
- No `Console.WriteLine` in production code.

## Comments

**When to Comment:**
- XML doc comments (`///`) on all public classes, interfaces, properties, and methods.
- Summary comments explain purpose, param tags explain parameters.
- Inline comments for non-obvious logic: `// Normalize any newline style to LF first, then convert to Windows CRLF.`
- Comments on constant declarations: `// Max 1 hour`, `// Max 1 minute`.
- Comments on empty catch blocks explaining why they are swallowed.
- Region comments in `Form1.cs` to organize large files: `#region Constants`, `#region Services`, `#region Event Handlers`.

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
- Optional parameters use nullable types with defaults: `string? configFilePath = null`
- `CancellationToken` always last async parameter.
- Constructor injection for dependencies (no DI container, manual wiring).

**Return Values:**
- Boolean for validation: `IsValidIpAddress`, `IsValidPort`
- Nullable for "not found" scenarios: `PresetManager.Get()` returns `PresetInfo?`
- Result objects for complex operations: `CommandResult`, `ExecutionResult`
- Static factory methods on result types: `CommandResult.Ok()`, `CommandResult.Fail(message)`, `CommandResult.Break()`, `CommandResult.Continue()`, `CommandResult.Exit()`, `CommandResult.Suppressed()`
- Tuples for multi-value returns: `(Dictionary<string, EnvironmentConfig> Environments, string? ActiveEnvironment, string? BaseEnvironment)` in `Services/ConfigurationService.cs`
- `out` parameters for try-pattern: `TryLoadRunPayload(string id, out HistoryRunPayload? payload)` in `Services/HistoryStorageService.cs`

## Module Design

**Exports:**
- `public` for API surface. `internal` for implementation details (e.g., `NativeMethods`, `PresetNodeTag`, `CsvFileSyncEvaluator`).
- `InternalsVisibleTo("SSH_Helper.Tests")` in `SSH_Helper.csproj` for test access to `internal` types.
- `internal sealed` for test-only classes: `FontApplicationTestHarness`.

**Barrel Files:** Not used. Each class explicitly imports needed namespaces.

**Class Modifiers:**
- `sealed` for leaf service classes: `ExecutionCoordinator`, `EnvironmentService`, `OutputThrottler`, `HistoryStorageService`
- `static` for stateless utility classes: `InputValidator`, `TerminalOutputProcessor`, `CsvFileSyncEvaluator`, `FolderPathUtility`
- Records for immutable data: `sealed record ExecutionPreparation(...)`, `readonly record struct CsvFileSyncEvaluation(...)`

## Event Patterns

**Declaration:**
```csharp
public event EventHandler<SshProgressEventArgs>? ProgressChanged;
public event EventHandler? PresetsChanged;
```

**Custom EventArgs** for typed payloads. Plain `EventHandler` for simple notifications.

**Raising pattern:**
```csharp
private void OnPresetsChanged() => PresetsChanged?.Invoke(this, EventArgs.Empty);
```

**Communication Direction:** Services raise events; UI subscribes. Never the reverse.

## Collection Initialization

- Use `new()` target-typed initialization: `new Dictionary<string, PresetInfo>()` or `= new();`
- Expose internal collections as `IReadOnlyDictionary`: `public IReadOnlyDictionary<string, PresetInfo> Presets => _presets;`
- Case-insensitive dictionaries where needed: `new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)`

## String Handling

- `string.Empty` over `""` for defaults.
- `string.IsNullOrWhiteSpace()` for validation, `string.IsNullOrEmpty()` for emptiness checks.
- `StringComparison.Ordinal` for exact comparisons: `.Replace("\r\n", "\n", StringComparison.Ordinal)`
- `StringComparer.OrdinalIgnoreCase` for case-insensitive dictionaries.
- String interpolation for formatting: `$"{IpAddress}:{Port}"`.
- Raw string literals (`"""..."""`) in test files for multi-line YAML content.

## Regex Patterns

- Pre-compiled static readonly for hot paths:
  ```csharp
  private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
  ```
- `RegexOptions.Compiled | RegexOptions.CultureInvariant` for performance-critical patterns.
- Multiple related patterns grouped as static fields at the top of the class (see `Utilities/TerminalOutputProcessor.cs`, `Utilities/PromptDetector.cs`).
- `RegexOptions.IgnoreCase` added where case-insensitive matching is needed (e.g., pager patterns).

## Concurrency Patterns

- `volatile` for simple state flags: `private volatile bool _isRunning;`
- `lock` blocks for thread-safe state transitions: `lock (_executionLock) { ... }` in `Services/SshExecutionService.cs`
- `SynchronizationContext` for marshaling to UI thread: `OutputThrottler` uses `SyncContext.Post()`.
- `CancellationTokenSource` lifecycle: create in `BeginExecution()`, cancel + dispose in `EndExecution()`.
- `Interlocked` for atomic flag operations: `Interlocked.Exchange(ref _flushQueued, 0)` in `Utilities/OutputThrottler.cs`.
- `TaskCompletionSource` for async event-driven coordination (see `Services/Editor/ScriptEditorValidationService.cs` tests).

## Disposal Patterns

- `IDisposable` on classes that own unmanaged or long-lived resources: `SshExecutionService`, `OutputThrottler`, `ScriptEditorValidationService`.
- `_disposed` boolean guard in `Dispose()` to prevent double-disposal.
- `using var` for scoped disposal in test code: `using var harness = new FontApplicationTestHarness();`
- Deferred font disposal in WinForms via `BeginInvoke` to avoid invalidating active GDI+ handles.
- `using` statements for streams and compression: `using var gzip = new GZipStream(...)` in `Services/ConfigurationService.cs`.

## JSON Serialization Patterns

- Newtonsoft.Json (`JsonConvert`) for config persistence in `Services/ConfigurationService.cs`.
- `System.Text.Json` (`JsonNode`, `JsonObject`) for runtime scripting JSON manipulation in `Services/Scripting/Commands/SetCommand.cs`.
- Dual `[JsonIgnore]` attributes when both serializers might see the property:
  ```csharp
  [JsonIgnore]
  [Newtonsoft.Json.JsonIgnore]
  public PresetType Type => ...
  ```
- `Formatting.Indented` for human-readable config files, `Formatting.None` for compressed payloads.

## Normalization Patterns

- `Normalize()` methods on settings objects to clamp values within valid ranges:
  ```csharp
  public void Normalize()
  {
      ValidationDebounceMs = Math.Clamp(ValidationDebounceMs, MinValidationDebounceMs, MaxValidationDebounceMs);
  }
  ```
- `CloneNormalized()` for returning safe copies: `Models/AppConfiguration.cs` `CommandEditorSettings`.
- Line ending normalization in setters: `Models/PresetInfo.cs` `Commands` property normalizes to CRLF.

---

*Convention analysis: 2026-03-07*
