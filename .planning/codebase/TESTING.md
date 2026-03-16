# Testing Patterns

**Analysis Date:** 2026-03-07

## Test Framework

**Runner:**
- xUnit 2.7.0
- Config: `SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- Microsoft.NET.Test.Sdk 17.9.0
- xunit.runner.visualstudio 2.5.7

**Assertion Library:**
- FluentAssertions 6.12.0

**Mocking Library:**
- Moq 4.20.70

**WinForms Support:**
- Xunit.StaFact 1.1.11 (provides `[WinFormsFact]` and `[WinFormsTheory]` for STA thread tests)

**Coverage:**
- coverlet.collector 6.0.1

**Run Commands:**
```bash
dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj          # Run all tests
dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~InputValidator"  # Filter
dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --collect:"XPlat Code Coverage"  # Coverage
```

## Test File Organization

**Location:**
- Separate test project: `SSH_Helper.Tests/`
- Test directories mirror source directories: `Services/`, `Utilities/`, `Models/`, `Scripting/`, `Editor/`, `UI/`

**Naming:**
- Test files: `{ClassName}Tests.cs` (e.g., `InputValidatorTests.cs`, `PresetManagerTests.cs`)
- Some service tests split by feature: `ConfigurationServiceFontSettingsTests.cs`, `ConfigurationServiceWindowStateTests.cs`, `ConfigurationServiceCommandEditorSettingsTests.cs`, `ConfigurationServiceExecutionDetailsTests.cs`
- Feature-specific tests: `PresetManagerFolderBaseEnvironmentTests.cs`, `SshExecutionServiceInteractivePreflightTests.cs`, `SshExecutionServiceOutputFormattingTests.cs`
- Test harness files named descriptively: `FontApplicationTestHarness.cs`

**Structure:**
```
SSH_Helper.Tests/
+-- Editor/
|   +-- EditorTextUtilitiesTests.cs
|   +-- ScriptAutocompleteProviderTests.cs
|   +-- ScriptEditorValidationServiceTests.cs
|   +-- YamlSshSyntaxHighlighterTests.cs
+-- Models/
|   +-- FontSettingsTests.cs
|   +-- PresetInfoTests.cs
+-- Scripting/
|   +-- CanonicalCommandMapSyntaxTests.cs
|   +-- ChoiceOptionResolverTests.cs
|   +-- ChooseCommandTests.cs
|   +-- ConfirmCommandTests.cs
|   +-- ExitCommandTests.cs
|   +-- ExpressionEvaluatorTests.cs
|   +-- ExtractCommandTests.cs
|   +-- ForeachCommandTests.cs
|   +-- InteractiveCommandTests.cs
|   +-- LogCommandTests.cs
|   +-- LogParsingTests.cs
|   +-- MultiselectCommandTests.cs
|   +-- NetworkCommandTests.cs
|   +-- NetworkStepParserTests.cs
|   +-- Parsers/
|   |   +-- FortiGateParserTests.cs
|   +-- QaPresetsSyntaxTests.cs
|   +-- ReadFileCommandTests.cs
|   +-- ScriptContextTests.cs
|   +-- ScriptDependencyAnalyzerTests.cs
|   +-- ScriptExecutorControlFlowTests.cs
|   +-- ScriptParserTests.cs
|   +-- ScriptValidationFormatterTests.cs
|   +-- SetCommandTests.cs
|   +-- TableCommandTests.cs
|   +-- UpdateColumnCommandTests.cs
|   +-- UpdateEnvironmentCommandTests.cs
|   +-- WriteFileCommandTests.cs
+-- Services/
|   +-- ConfigurationServiceCommandEditorSettingsTests.cs
|   +-- ConfigurationServiceExecutionDetailsTests.cs
|   +-- ConfigurationServiceFontSettingsTests.cs
|   +-- ConfigurationServiceWindowStateTests.cs
|   +-- CredentialManagerProviderTests.cs
|   +-- CredentialTargetsTests.cs
|   +-- CsvManagerTests.cs
|   +-- EnvironmentServiceTests.cs
|   +-- ExecutionCoordinatorTests.cs
|   +-- HistoryIdGeneratorTests.cs
|   +-- HistoryResultStoreTests.cs
|   +-- HistoryStorageServiceTests.cs
|   +-- InteractiveTerminalServiceTranscriptFilterTests.cs
|   +-- PresetManagerFolderBaseEnvironmentTests.cs
|   +-- PresetManagerTests.cs
|   +-- SshExecutionServiceInteractivePreflightTests.cs
|   +-- SshExecutionServiceOutputFormattingTests.cs
|   +-- SshTerminalOptionsFactoryTests.cs
|   +-- StopOnFirstErrorTrackerTests.cs
+-- UI/
|   +-- ApplyFontSettingsTests.cs
|   +-- ExecutionDetailsDialogTests.cs
|   +-- FontApplicationTestHarness.cs
|   +-- ScintillaScriptEditorControlTests.cs
|   +-- ScintillaScriptEditorPerformanceTests.cs
|   +-- SettingsDialogAppearanceTests.cs
+-- Utilities/
    +-- BaseEnvironmentIndicatorFormatterTests.cs
    +-- CsvFileSyncEvaluatorTests.cs
    +-- ExecutionDialogPolicyTests.cs
    +-- FolderBaseEnvironmentSummaryFormatterTests.cs
    +-- HostsFileIndicatorFormatterTests.cs
    +-- InlineDiffBuilderTests.cs
    +-- InputValidatorTests.cs
    +-- OutputThrottlerTests.cs
    +-- PresetBaseEnvironmentResolverTests.cs
    +-- PresetEnvironmentLoadPlannerTests.cs
    +-- PresetEnvironmentStatusFormatterTests.cs
    +-- PresetHeaderIndicatorFormatterTests.cs
    +-- PromptDetectorTests.cs
    +-- TerminalOutputProcessorTests.cs
```

## Test Structure

**Suite Organization:**
```csharp
using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

/// <summary>
/// Tests for the InputValidator utility class.
/// </summary>
public class InputValidatorTests
{
    #region IsValidIpAddress Tests

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("0.0.0.0", true)]
    public void IsValidIpAddress_ValidIpAddresses_ReturnsTrue(string ip, bool expected)
    {
        var result = InputValidator.IsValidIpAddress(ip);
        result.Should().Be(expected);
    }

    #endregion

    #region IsValidPort Tests
    // ... more tests grouped by method
    #endregion
}
```

**Patterns:**
- `#region` blocks group tests by method or feature under test.
- Test method naming: `MethodName_Scenario_ExpectedResult` (e.g., `IsValidIpAddress_ValidIpAddresses_ReturnsTrue`)
- `[Fact]` for single test cases.
- `[Theory]` with `[InlineData]` for parameterized tests (heavily used).
- File-scoped namespaces in all test files.
- XML summary comments on test class describing purpose.

**Setup:**
- Constructor injection (xUnit creates new instance per test):
  ```csharp
  public class ScriptParserTests
  {
      private readonly ScriptParser _parser;

      public ScriptParserTests()
      {
          _parser = new ScriptParser();
      }
  }
  ```

**Teardown:**
- `IDisposable` for cleanup (temp directories, file cleanup):
  ```csharp
  public class CsvManagerTests : IDisposable
  {
      private readonly string _testDirectory;

      public CsvManagerTests()
      {
          _testDirectory = Path.Combine(Path.GetTempPath(), $"CsvManagerTests_{Guid.NewGuid()}");
          Directory.CreateDirectory(_testDirectory);
      }

      public void Dispose()
      {
          if (Directory.Exists(_testDirectory))
              Directory.Delete(_testDirectory, true);
      }
  }
  ```

- `sealed` modifier on test classes with disposal for best practice:
  ```csharp
  public sealed class HistoryStorageServiceTests : IDisposable
  ```

- Best-effort cleanup pattern (swallow errors in Dispose):
  ```csharp
  public void Dispose()
  {
      try
      {
          if (Directory.Exists(_testDirectory))
              Directory.Delete(_testDirectory, recursive: true);
      }
      catch
      {
          // Best-effort cleanup.
      }
  }
  ```

**Assertion Style:**
- FluentAssertions exclusively. Never use `Assert.Equal()` or similar xUnit assertions.
  ```csharp
  result.Should().Be(expected);
  result.Should().BeTrue();
  result.Should().BeFalse();
  result.Should().NotBeNull();
  result.Should().BeEmpty();
  result.Should().ContainKey(uniqueName);
  result.Should().BeOfType<List<string>>();
  names.Should().Equal(EnvironmentConfig.DefaultName);
  action.Should().Throw<ArgumentException>();
  result.Should().ContainSingle().Which.Should().Be("sshd");
  result.Should().BeApproximately(15f, 0.5f);
  result.Should().BeSameAs(otherReference);
  result.Should().NotBeSameAs(otherReference);
  result.Should().HaveCount(1);
  result.Should().Contain(d => d.Message.Contains("then"));
  stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(7));
  ```

## Mocking

**Framework:** Moq 4.20.70

**Patterns:**
- Moq is available but sparingly used. Most tests use real implementations.
- `ConfigurationService` accepts an optional `configFilePath` constructor parameter for test isolation:
  ```csharp
  var configPath = Path.Combine(_testDirectory, "config.json");
  var configService = new ConfigurationService(configPath);
  ```

**What to Mock:**
- External dependencies (SSH connections, file system when appropriate).
- Interfaces like `ICredentialProvider` when testing consumers.

**What NOT to Mock:**
- `ConfigurationService` -- use real instance with temp directory.
- `PresetManager` -- use real instance with temp config.
- `CsvManager` -- use real instance with temp files.
- `EnvironmentService` -- chain through real `ConfigurationService`.
- Pure utility classes (`InputValidator`, `TerminalOutputProcessor`, `PromptDetector`).
- Scripting commands (`SetCommand`, `ExitCommand`, etc.) -- test directly.

## Fixtures and Factories

**Test Data:**
```csharp
// Unique names to avoid test collisions
var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);

// Temp directories for file-based tests (with Guid for uniqueness)
_testDirectory = Path.Combine(Path.GetTempPath(), $"CsvManagerTests_{Guid.NewGuid()}");
_testDirectory = Path.Combine(Path.GetTempPath(), $"HistoryStorageTests_{Guid.NewGuid():N}");

// Inline CSV content
File.WriteAllText(filePath, "Host_IP,port,username\n192.168.1.1,22,admin\n192.168.1.2,2222,root");

// ScriptStep for command tests
var step = new ScriptStep { Set = "result_str = \"${hn} | Kernel ${ver}\"" };
var context = new ScriptContext();
context.SetVariable("hn", "chris-NUC7i7DNHE");

// Large payloads for storage tests
var largeOutput = string.Concat(Enumerable.Repeat("sniffer-line-abcdefghijklmnopqrstuvwxyz0123456789\n", 12000));

// Config state setup via Update()
_configService.Update(config =>
{
    config.SavedState = new ApplicationState
    {
        HostColumns = new List<string> { CsvManager.HostColumnName, "username" },
        Hosts = new List<Dictionary<string, string>>
        {
            new() { [CsvManager.HostColumnName] = "10.0.0.1", ["username"] = "admin" }
        }
    };
});
```

**Helper Methods in Test Classes:**
```csharp
// Private helpers for creating test data (seen in CsvFileSyncEvaluatorTests, HistoryStorageServiceTests)
private string WriteCsv(string fileName, Dictionary<string, string>[] rows) { ... }
private EnvironmentConfig CreateEnvironment(string filePath, CsvFileFingerprint? fingerprint) { ... }
private ExecutionDetails CreateExecutionDetails(string presetName) { ... }
```

**Test Harness:**
- `FontApplicationTestHarness` (`SSH_Helper.Tests/UI/FontApplicationTestHarness.cs`): Lightweight surrogate for `Form1.ApplyFontSettings` that creates the same controls without full Form1 startup (no SSH services, no P/Invoke).
  ```csharp
  using var harness = new FontApplicationTestHarness();
  harness.ApplyFontSettings(FontSettings.CreateDefault());
  harness.lblHostsTitle.Font.Should().NotBeNull();
  harness.ManagedFonts.Should().NotBeEmpty();
  ```

**Location:**
- Test harness classes live alongside their tests in `SSH_Helper.Tests/UI/`.
- No shared fixtures directory. Test data is inline or created in constructor.

## Coverage

**Requirements:** No enforced coverage threshold.

**View Coverage:**
```bash
dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --collect:"XPlat Code Coverage"
```

## Test Types

**Unit Tests:**
- Majority of tests are unit tests.
- Test individual classes/methods in isolation.
- Pure utility classes tested thoroughly with `[Theory]`/`[InlineData]` for boundary conditions.
- Scripting command tests use `ScriptContext` and `ScriptStep` directly, no SSH connection needed.
- Expression evaluator tests cover edge cases including ReDoS protection.
- Model tests verify normalization behavior (e.g., `PresetInfoTests` for line ending normalization).

**Integration Tests:**
- `ConfigurationService` tests with real file I/O (temp directory isolation).
- `EnvironmentService` tests chain through `ConfigurationService` with real persistence.
- `PresetManager` tests exercise full save/load/delete cycle against real config.
- `CsvManager` tests write/read actual CSV files.
- `ExecutionCoordinator` tests create real `SshExecutionService` instances.
- `HistoryStorageService` tests verify full save/load/prune cycle with real files.
- `CsvFileSyncEvaluator` tests combine real `CsvManager` with file I/O.

**WinForms UI Tests:**
- Use `[WinFormsFact]` and `[WinFormsTheory]` from Xunit.StaFact for STA thread requirement.
- `FontApplicationTestHarness` avoids full Form1 dependencies.
- `SettingsDialog` tested via reflection (class is `internal sealed`).
- Access internal members via `InternalsVisibleTo("SSH_Helper.Tests")`.
- Performance tests (e.g., `ScintillaScriptEditorPerformanceTests`) verify responsiveness.

**E2E Tests:**
- Not used. No end-to-end SSH connection tests in the suite.

## Common Patterns

**Async Testing:**
```csharp
[Fact]
public async Task ExecuteAsync_QuotedInterpolatedString_DoesNotKeepOuterQuotes()
{
    var step = new ScriptStep { Set = "result_str = \"${hn} | Kernel ${ver}\"" };
    var context = new ScriptContext();
    context.SetVariable("hn", "chris-NUC7i7DNHE");
    context.SetVariable("ver", "6.8.0-90-generic");

    var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

    result.Success.Should().BeTrue();
    context.GetVariableString("result_str").Should().Be("chris-NUC7i7DNHE | Kernel 6.8.0-90-generic");
}
```

**Async Event Testing with TaskCompletionSource:**
```csharp
[Fact]
public async Task RequestValidation_DebouncedLastEditWins_PublishesLatestDiagnostics()
{
    using var service = new ScriptEditorValidationService { DebounceMilliseconds = 40 };

    var completion = new TaskCompletionSource<IReadOnlyList<EditorDiagnostic>>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    service.DiagnosticsUpdated += (_, diagnostics) =>
    {
        if (diagnostics.Any(d => d.Message.Contains("then")))
            completion.TrySetResult(diagnostics);
    };

    service.RequestValidation("steps:\n  - send:\n      command: show version");
    await Task.Delay(10);
    service.RequestValidation("steps:\n  - if:\n      condition: condition");

    var finalDiagnostics = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
    finalDiagnostics.Should().Contain(d => d.Message.Contains("then"));
}
```

**Error Testing:**
```csharp
[Fact]
public void Save_EmptyName_ThrowsArgumentException()
{
    _presetManager.Load();
    var preset = new PresetInfo { Commands = "test" };

    var action = () => _presetManager.Save("", preset);

    action.Should().Throw<ArgumentException>();
}

[Fact]
public void LoadFromFile_EmptyFile_ThrowsInvalidDataException()
{
    var filePath = Path.Combine(_testDirectory, "empty.csv");
    File.WriteAllText(filePath, "");

    var action = () => _csvManager.LoadFromFile(filePath);

    action.Should().Throw<InvalidDataException>()
        .WithMessage("*empty*");
}
```

**Event Testing:**
```csharp
[Fact]
public void Load_RaisesPresetsChangedEvent()
{
    bool eventRaised = false;
    _presetManager.PresetsChanged += (s, e) => eventRaised = true;

    _presetManager.Load();

    eventRaised.Should().BeTrue();
}
```

**Cleanup with try/finally:**
```csharp
[Fact]
public void Save_NewPreset_AddsToCollection()
{
    _presetManager.Load();
    var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
    var newPreset = new PresetInfo { Commands = "test command" };

    try
    {
        _presetManager.Save(uniqueName, newPreset);
        _presetManager.Presets.Should().ContainKey(uniqueName);
    }
    finally
    {
        _presetManager.Delete(uniqueName);
    }
}
```

**WinForms Testing:**
```csharp
[WinFormsFact]
public void ApplyFontSettings_DefaultSettings_AllControlsHaveNonNullFonts()
{
    using var harness = new FontApplicationTestHarness();
    harness.ApplyFontSettings(FontSettings.CreateDefault());

    harness.lblHostsTitle.Font.Should().NotBeNull();
    harness.trvPresets.Font.Should().NotBeNull();
}

[WinFormsTheory]
[InlineData(true)]
[InlineData(false)]
public void ApplyFontSettings_WordWrap_AppliedToCodeEditor(bool wordWrap)
{
    using var harness = new FontApplicationTestHarness();
    var settings = FontSettings.CreateDefault();
    settings.CodeEditorWordWrap = wordWrap;
    harness.ApplyFontSettings(settings);
    // ... assertions
}
```

**Performance / Timing Tests:**
```csharp
[Fact]
public void Evaluate_MatchesCatastrophicPattern_CompletesWithoutThrowing()
{
    var context = new ScriptContext();
    context.SetVariable("payload", new string('a', 6000) + "!");

    var evaluator = new ExpressionEvaluator(context);
    var stopwatch = Stopwatch.StartNew();

    var result = evaluator.Evaluate("payload matches '(a+)+$'");

    stopwatch.Stop();
    result.Should().BeFalse();
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(7));
}
```

**Dynamic Timestamp / Time-Dependent Tests:**
```csharp
[Fact]
public void TimestampVariable_ResolvesDynamically()
{
    var context = new ScriptContext();

    var first = context.SubstituteVariables("${_timestamp}");
    Thread.Sleep(1100);
    var second = context.SubstituteVariables("${_timestamp}");

    second.Should().NotBe(first);
}
```

## GDI+ Font Testing Gotcha

When two `System.Drawing.Font` objects are created with identical parameters (family, size, style), GDI+ may share the underlying native handle. Disposing one can invalidate the other's `Font.Height` (throws `ArgumentException: Parameter is not valid`). When writing tests that dispose fonts and then access properties, use different font sizes between the two batches to ensure independent GDI+ handles. See `SSH_Helper.Tests/UI/ApplyFontSettingsTests.cs`.

## Adding New Tests

**For a new utility class:**
1. Create `SSH_Helper.Tests/Utilities/{ClassName}Tests.cs`
2. Use file-scoped namespace `SSH_Helper.Tests.Utilities;`
3. Group tests by method with `#region` blocks
4. Use `[Theory]`/`[InlineData]` for parameterized boundary testing
5. Use FluentAssertions for all assertions

**For a new service:**
1. Create `SSH_Helper.Tests/Services/{ServiceName}Tests.cs`
2. Create temp directory in constructor, clean up in `Dispose()`
3. Use real `ConfigurationService` with temp config path for isolation
4. Test event raising with boolean flag pattern
5. For large services, split into feature-focused test files: `{ServiceName}{Feature}Tests.cs`

**For a new scripting command:**
1. Create `SSH_Helper.Tests/Scripting/{CommandName}Tests.cs`
2. Instantiate command directly: `private readonly SetCommand _command = new();`
3. Build `ScriptStep` and `ScriptContext` per test
4. Use `CancellationToken.None` for non-cancellation tests
5. Assert on `CommandResult.Success` and context variable state

**For a new parser (under Scripting/Parsers):**
1. Create `SSH_Helper.Tests/Scripting/Parsers/{ParserName}Tests.cs`
2. Use `[Theory]`/`[InlineData]` for multiple input samples
3. Follow existing `FortiGateParserTests.cs` as template

**For WinForms UI:**
1. Create `SSH_Helper.Tests/UI/{FeatureName}Tests.cs`
2. Use `[WinFormsFact]`/`[WinFormsTheory]` attributes
3. Prefer test harness over full Form1 instantiation
4. Access `internal` members via `InternalsVisibleTo`

**For async/debounced services:**
1. Use `TaskCompletionSource` for waiting on async events
2. Use `.WaitAsync(TimeSpan)` to prevent test hangs
3. Use short debounce intervals in tests (e.g., `DebounceMilliseconds = 40`)

---

*Testing analysis: 2026-03-07*
