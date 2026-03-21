using System.Text.Json;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public sealed class BrowserCallbackSelfContainedPresetTests
{
    [Fact]
    public void SelfContainedBrowserCallbackPresetBundle_Exists_AndValidates()
    {
        var repoRoot = FindRepositoryRoot();
        var bundlePath = Path.Combine(repoRoot, "ScriptSamples", "browser_callback_self_contained_presets.json");

        File.Exists(bundlePath).Should().BeTrue("the repo should ship an importable browser-callback demo preset bundle");

        using var document = JsonDocument.Parse(File.ReadAllText(bundlePath));
        document.RootElement.TryGetProperty("presets", out var presetsElement).Should().BeTrue();
        presetsElement.ValueKind.Should().Be(JsonValueKind.Object);

        var presetEntries = presetsElement.EnumerateObject().ToList();
        presetEntries.Should().ContainSingle("the bundle should stay focused on one self-contained demo preset");

        var presetEntry = presetEntries[0];
        presetEntry.Name.Should().Be("Browser Callback Self-Contained Demo");
        presetEntry.Value.TryGetProperty("commands", out var commandsElement).Should().BeTrue();
        commandsElement.ValueKind.Should().Be(JsonValueKind.String);

        var commands = commandsElement.GetString();
        commands.Should().NotBeNullOrWhiteSpace();
        ScriptParser.IsYamlScript(commands!).Should().BeTrue();

        var parser = new ScriptParser();
        var script = parser.Parse(commands!);
        parser.Validate(script, commands!, enforceCanonicalSyntax: true).Should().BeEmpty();

        script.Description.Should().Contain("Requires:");
        script.Description.Should().Contain("Expected:");

        var callbackSteps = script.Steps.Where(step => step.GetStepType() == StepType.BrowserCallbackCapture).ToList();
        callbackSteps.Should().HaveCount(2, "the self-contained preset should cover both query and fragment callback capture");

        var requirement = new ScriptDependencyAnalyzer().AnalyzeSshRequirements(script);
        requirement.RequiresSshSession.Should().BeFalse("the demo should not require a real SSH session");
        requirement.UsesBrowserCallbackCapture.Should().BeTrue();

        var columnDependencies = new ScriptDependencyAnalyzer().AnalyzeScript(script);
        columnDependencies.ReferencedColumns.Should().BeEmpty("the demo should not depend on external grid columns or environment variables");

        script.Steps.Should().NotBeEmpty();
        script.Steps[^1].GetStepType().Should().Be(StepType.Exit);
        script.Steps[^1].Exit.Should().StartWith("success PASS - ");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? search = new(AppContext.BaseDirectory);
        while (search != null && !Directory.Exists(Path.Combine(search.FullName, "ScriptSamples")))
        {
            search = search.Parent;
        }

        search.Should().NotBeNull("test should run from within the repository tree");
        return search!.FullName;
    }
}
