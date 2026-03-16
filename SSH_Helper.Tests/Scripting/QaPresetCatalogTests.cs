using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class QaPresetCatalogTests
{
    [Fact]
    public void Every_QaPresetDescription_DeclaresRequirements_AndExpectedOutcome()
    {
        foreach (var preset in LoadQaPresets())
        {
            preset.Script.Description.Should().NotBeNullOrWhiteSpace($"{preset.Name} should keep a descriptive summary");
            preset.Script.Description.Should().Contain("Requires:", $"{preset.Name} should declare its prerequisites");
            preset.Script.Description.Should().Contain("Expected:", $"{preset.Name} should declare its expected final result");
        }
    }

    [Fact]
    public void QaPresetCatalog_ResultContracts_AreExplicit()
    {
        foreach (var preset in LoadQaPresets())
        {
            var description = preset.Script.Description ?? string.Empty;
            var topLevelExitSteps = preset.Script.Steps.Where(step => step.GetStepType() == StepType.Exit).ToList();

            if (description.Contains("Expected: intentional validation failure.", StringComparison.Ordinal))
            {
                preset.Name.Should().Contain("[Expected Fail]");
                preset.ValidationErrors.Should().NotBeEmpty($"{preset.Name} is intentionally invalid");
                continue;
            }

            preset.ValidationErrors.Should().BeEmpty($"{preset.Name} should validate cleanly");
            preset.Script.Steps.Should().NotBeEmpty($"{preset.Name} should have executable steps");
            var lastStep = preset.Script.Steps[^1];

            if (description.Contains("Expected: intentional failure stop.", StringComparison.Ordinal))
            {
                lastStep.GetStepType().Should().Be(StepType.Assert, $"{preset.Name} should stop on an error-severity assert");
                lastStep.Assert.Should().NotBeNull();
                lastStep.Assert!.Severity.Should().Be("error");
                continue;
            }

            topLevelExitSteps.Should().ContainSingle($"{preset.Name} should have one top-level terminal exit marker");
            topLevelExitSteps[0].Should().BeSameAs(lastStep, $"{preset.Name} should not hide an earlier top-level exit before its final result");
            lastStep.GetStepType().Should().Be(StepType.Exit, $"{preset.Name} should end with an explicit terminal exit marker");
            lastStep.Exit.Should().NotBeNullOrWhiteSpace();

            if (description.Contains("Expected: intentional failure exit.", StringComparison.Ordinal))
            {
                lastStep.Exit!.Should().StartWith("failure ", $"{preset.Name} should terminate with a failure exit");
                continue;
            }

            if (description.Contains("Expected: intentional error exit.", StringComparison.Ordinal))
            {
                lastStep.Exit!.Should().StartWith("error ", $"{preset.Name} should terminate with an error exit");
                continue;
            }

            if (description.Contains("Expected: pass.", StringComparison.Ordinal))
            {
                lastStep.Exit!.Should().StartWith("success PASS - ", $"{preset.Name} should terminate with an explicit pass marker");
                continue;
            }

            throw new Xunit.Sdk.XunitException($"Unsupported Expected clause in preset '{preset.Name}': {description}");
        }
    }

    [Fact]
    public void QaPresetCatalog_AndFixtures_CoverRequiredScriptingFeatures()
    {
        var fixtureCommands = LoadCoverageFixtures().Select(fixture => fixture.Commands).ToList();
        var combinedText = string.Join("\n\n", LoadQaPresets().Select(preset => preset.Commands).Concat(fixtureCommands));

        Regex.IsMatch(combinedText, @"(?m)^environment:\s*\S+").Should().BeTrue("coverage should include the top-level environment header");
        Regex.IsMatch(combinedText, @"(?m)^suppress_missing_column_warning:\s*true").Should().BeTrue("coverage should include missing-column warning suppression");
        Regex.IsMatch(combinedText, @"(?m)^library:\s*true").Should().BeTrue("coverage should include library files");
        Regex.IsMatch(combinedText, @"(?m)^imports:\s*$").Should().BeTrue("coverage should include file-backed imports");
        Regex.IsMatch(combinedText, @"(?m)^subroutines:\s*$").Should().BeTrue("coverage should include subroutine declarations");
        Regex.IsMatch(combinedText, @"(?m)^\s*-\s*call:\s*$").Should().BeTrue("coverage should include call steps");
        Regex.IsMatch(combinedText, @"(?m)^\s*-\s*return:\s*true\s*$").Should().BeTrue("coverage should include return steps");
        Regex.IsMatch(combinedText, @"(?ms)-\s*send:\s*.*?^\s+expect:\s").Should().BeTrue("coverage should include send.expect");
        Regex.IsMatch(combinedText, @"(?ms)-\s*readfile:\s*.*?^\s+select_file:\s*true").Should().BeTrue("coverage should include readfile.select_file");
        Regex.IsMatch(combinedText, @"(?ms)-\s*readfile:\s*.*?^\s+message:\s").Should().BeTrue("coverage should include readfile.message");
        Regex.IsMatch(combinedText, @"(?ms)-\s*readfile:\s*.*?^\s+file_ext:\s").Should().BeTrue("coverage should include readfile.file_ext");
        Regex.IsMatch(combinedText, @"(?ms)-\s*readfile:\s*.*?^\s+encoding:\s").Should().BeTrue("coverage should include readfile.encoding");
        Regex.IsMatch(combinedText, @"(?ms)-\s*http:\s*.*?^\s+follow_redirects:\s*false").Should().BeTrue("coverage should include http.follow_redirects");
        Regex.IsMatch(combinedText, @"(?ms)-\s*interactive:\s*.*?^\s+show_window:\s*false").Should().BeTrue("coverage should include interactive.show_window");
        Regex.IsMatch(combinedText, @"(?ms)-\s*interactive:\s*.*?^\s+max_lines:\s*\d+").Should().BeTrue("coverage should include interactive.max_lines");
        Regex.IsMatch(combinedText, @"(?ms)-\s*interactive:\s*.*?^\s+width:\s*\d+").Should().BeTrue("coverage should include interactive.width");
        Regex.IsMatch(combinedText, @"(?ms)-\s*interactive:\s*.*?^\s+height:\s*\d+").Should().BeTrue("coverage should include interactive.height");
        combinedText.Should().Contain("_writefile", "coverage should include the writefile runtime path variable");
    }

    [Fact]
    public void QaCoverageFixtures_Parse_AndValidate()
    {
        var parser = new ScriptParser();
        var fixtures = LoadCoverageFixtures().ToList();
        fixtures.Should().HaveCount(2);

        var libraryFixture = fixtures.Single(fixture => fixture.Name == "catalog_library.yaml");
        var libraryScript = parser.Parse(libraryFixture.Commands);
        parser.Validate(libraryScript, libraryFixture.Commands, allowLibraryDefinitions: true).Should().BeEmpty();
        libraryScript.Library.Should().BeTrue();

        var runnerFixture = fixtures.Single(fixture => fixture.Name == "catalog_runner.yaml");
        var runnerScript = parser.Parse(runnerFixture.Commands);
        parser.Validate(runnerScript, runnerFixture.Commands).Should().BeEmpty();
    }

    private static IReadOnlyList<LoadedQaPreset> LoadQaPresets()
    {
        var parser = new ScriptParser();
        var repoRoot = FindRepositoryRoot();
        var qaPresetsPath = Path.Combine(repoRoot, "qa_presets.json");
        using var document = JsonDocument.Parse(File.ReadAllText(qaPresetsPath));

        document.RootElement.TryGetProperty("presets", out var presetsElement).Should().BeTrue();
        presetsElement.ValueKind.Should().Be(JsonValueKind.Object);

        var presets = new List<LoadedQaPreset>();
        foreach (var presetProperty in presetsElement.EnumerateObject())
        {
            if (!presetProperty.Value.TryGetProperty("commands", out var commandsElement) ||
                commandsElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var commands = commandsElement.GetString() ?? string.Empty;
            if (!ScriptParser.IsYamlScript(commands))
            {
                continue;
            }

            var script = parser.Parse(commands);
            var validationErrors = parser.Validate(script, commands);
            presets.Add(new LoadedQaPreset(presetProperty.Name, commands, script, validationErrors));
        }

        presets.Should().NotBeEmpty("the QA catalog should contain YAML presets");
        return presets;
    }

    private static IReadOnlyList<LoadedFixtureScript> LoadCoverageFixtures()
    {
        var repoRoot = FindRepositoryRoot();
        var libraryPath = Path.Combine(repoRoot, "ScriptSamples", "qa", "catalog_library.yaml");
        var runnerPath = Path.Combine(repoRoot, "ScriptSamples", "qa", "catalog_runner.yaml");

        File.Exists(libraryPath).Should().BeTrue("the QA catalog library fixture should exist");
        File.Exists(runnerPath).Should().BeTrue("the QA catalog runner fixture should exist");

        var libraryCommands = File.ReadAllText(libraryPath);
        var runnerCommands = File.ReadAllText(runnerPath)
            .Replace("__QA_CATALOG_LIBRARY_PATH__", libraryPath.Replace("\\", "\\\\"));

        return
        [
            new LoadedFixtureScript(Path.GetFileName(libraryPath), libraryCommands),
            new LoadedFixtureScript(Path.GetFileName(runnerPath), runnerCommands)
        ];
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? search = new(AppContext.BaseDirectory);
        while (search != null && !File.Exists(Path.Combine(search.FullName, "qa_presets.json")))
        {
            search = search.Parent;
        }

        search.Should().NotBeNull("test should run from within the repository tree");
        return search!.FullName;
    }

    private sealed record LoadedQaPreset(string Name, string Commands, Script Script, List<string> ValidationErrors);
    private sealed record LoadedFixtureScript(string Name, string Commands);
}
