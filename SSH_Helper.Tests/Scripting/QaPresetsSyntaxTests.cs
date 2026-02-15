using System.Text.Json;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class QaPresetsSyntaxTests
{
    [Fact]
    public void Parse_AllQaPresetYamlScripts_DoNotThrow()
    {
        var parser = new ScriptParser();
        var repoRoot = FindRepositoryRoot();
        var qaPresetsPath = Path.Combine(repoRoot, "qa_presets.json");

        File.Exists(qaPresetsPath).Should().BeTrue("qa preset catalog should exist in repository root");

        using var document = JsonDocument.Parse(File.ReadAllText(qaPresetsPath));
        document.RootElement.TryGetProperty("presets", out var presetsElement).Should().BeTrue();
        presetsElement.ValueKind.Should().Be(JsonValueKind.Object);

        var parsedYamlPresetCount = 0;
        var errors = new List<string>();

        foreach (var presetProperty in presetsElement.EnumerateObject())
        {
            if (!presetProperty.Value.TryGetProperty("commands", out var commandsElement) ||
                commandsElement.ValueKind != JsonValueKind.String)
            {
                errors.Add($"Preset '{presetProperty.Name}' is missing string 'commands'.");
                continue;
            }

            var commands = commandsElement.GetString() ?? string.Empty;
            if (!ScriptParser.IsYamlScript(commands))
            {
                continue;
            }

            parsedYamlPresetCount++;
            try
            {
                parser.Parse(commands);
            }
            catch (ScriptParseException ex)
            {
                errors.Add($"{presetProperty.Name}: {ex.Message}");
            }
        }

        parsedYamlPresetCount.Should().BeGreaterThan(0, "qa presets should contain YAML scripts");
        errors.Should().BeEmpty("all QA preset YAML scripts should parse successfully");
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
}
