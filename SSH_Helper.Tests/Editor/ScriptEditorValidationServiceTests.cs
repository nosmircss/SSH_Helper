using FluentAssertions;
using SSH_Helper.Services.Editor;
using Xunit;
using System.Diagnostics;

namespace SSH_Helper.Tests.Editor;

public class ScriptEditorValidationServiceTests
{
    [Fact]
    public async Task ValidateNowAsync_NonYamlText_ReturnsNoDiagnostics()
    {
        using var service = new ScriptEditorValidationService();

        var diagnostics = await service.ValidateNowAsync("show version");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestValidation_DebouncedLastEditWins_PublishesLatestDiagnostics()
    {
        using var service = new ScriptEditorValidationService
        {
            DebounceMilliseconds = 40,
            EnableYamlHygieneWarnings = false
        };

        var published = new List<IReadOnlyList<EditorDiagnostic>>();
        var completion = new TaskCompletionSource<IReadOnlyList<EditorDiagnostic>>(TaskCreationOptions.RunContinuationsAsynchronously);

        service.DiagnosticsUpdated += (_, diagnostics) =>
        {
            lock (published)
            {
                published.Add(diagnostics.ToList());
            }

            if (diagnostics.Any(d => d.Message.Contains("then", StringComparison.OrdinalIgnoreCase)))
            {
                completion.TrySetResult(diagnostics);
            }
        };

        service.RequestValidation("steps:\n  - send:\n      command: show version");
        await Task.Delay(10);
        service.RequestValidation("steps:\n  - if:\n      condition: condition");

        var finalDiagnostics = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
        finalDiagnostics.Should().Contain(d => d.Message.Contains("then", StringComparison.OrdinalIgnoreCase));

        await Task.Delay(120);
        lock (published)
        {
            published.Should().NotBeEmpty();
            published.Last().Should().Contain(d => d.Message.Contains("then", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task ValidateNowAsync_ParserMessage_MapsToColumnSpan()
    {
        using var service = new ScriptEditorValidationService
        {
            EnableYamlHygieneWarnings = false
        };

        var text = """
                   steps:
                     - http:
                         url: https://example.com
                         method: FETCH
                   """;

        var diagnostics = await service.ValidateNowAsync(text);

        diagnostics.Should().NotBeEmpty();
        diagnostics.Should().Contain(d =>
            d.Severity == DiagnosticSeverity.Error &&
            d.ColumnStart >= 1 &&
            d.ColumnEnd >= d.ColumnStart &&
            d.LineNumber >= 1);
    }

    [Fact]
    public async Task ValidateNowAsync_HygieneWarnings_IncludeTabsAndDuplicateKeys()
    {
        using var service = new ScriptEditorValidationService
        {
            ShowInlineWarnings = true,
            EnableYamlHygieneWarnings = true
        };

        var text = "steps:\n\t- send: test\nvars:\n  key: one\n  key: two";

        var diagnostics = await service.ValidateNowAsync(text);

        diagnostics.Should().Contain(d => d.Message.Contains("Tab indentation", StringComparison.OrdinalIgnoreCase));
        diagnostics.Should().Contain(d => d.Message.Contains("Duplicate key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateNowAsync_HygieneWarnings_DuplicateKeysAcrossDifferentSteps_NotReported()
    {
        using var service = new ScriptEditorValidationService
        {
            ShowInlineWarnings = true,
            EnableYamlHygieneWarnings = true
        };

        var text = """
                   steps:
                     - send:
                         command: whoami
                         suppress: true
                         capture: current_user
                     - send:
                         command: uname -a
                         timeout: 15
                         capture: system_info
                   """;

        var diagnostics = await service.ValidateNowAsync(text);

        diagnostics.Should().NotContain(d =>
            d.Message.Contains("Duplicate key 'capture'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateNowAsync_ShowInlineWarningsDisabled_SuppressesParserWarnings()
    {
        using var service = new ScriptEditorValidationService
        {
            ShowInlineWarnings = false,
            EnableYamlHygieneWarnings = false
        };

        var text = """
                   steps:
                     - send:
                         command: show version
                         typoo: true
                   """;

        var diagnostics = await service.ValidateNowAsync(text);

        diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task ValidateNowAsync_LargeYamlScript_CompletesWithinBoundedTime()
    {
        using var service = new ScriptEditorValidationService
        {
            ShowInlineWarnings = false,
            EnableYamlHygieneWarnings = false
        };

        var steps = Enumerable.Range(1, 550)
            .Select(index => $"  - send:\n      command: show version {index}");
        var text = "steps:\n" + string.Join("\n", steps);

        var stopwatch = Stopwatch.StartNew();
        var diagnostics = await service.ValidateNowAsync(text);
        stopwatch.Stop();

        diagnostics.Should().BeEmpty();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000);
    }
}
