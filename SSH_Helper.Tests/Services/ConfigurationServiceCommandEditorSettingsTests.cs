using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class ConfigurationServiceCommandEditorSettingsTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly ConfigurationService _configurationService;

    public ConfigurationServiceCommandEditorSettingsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"CommandEditorSettings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
        _configurationService = new ConfigurationService(_configPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void Load_MissingCommandEditorSettings_UsesDefaults()
    {
        File.WriteAllText(_configPath, """{"Presets":{},"Username":"tester"}""");

        var config = _configurationService.Load();

        config.CommandEditor.Should().NotBeNull();
        config.CommandEditor.EnableSyntaxHighlighting.Should().BeTrue();
        config.CommandEditor.EnableAutocomplete.Should().BeTrue();
        config.CommandEditor.EnableInlineValidation.Should().BeTrue();
        config.CommandEditor.ValidationDebounceMs.Should().Be(400);
        config.CommandEditor.IndentSize.Should().Be(2);
    }

    [Fact]
    public void SaveAndLoad_CommandEditorSettings_RoundTrips()
    {
        _configurationService.Update(config =>
        {
            config.CommandEditor.EnableSyntaxHighlighting = false;
            config.CommandEditor.EnableAutocomplete = false;
            config.CommandEditor.AutocompleteShowOnTyping = false;
            config.CommandEditor.EnableInlineValidation = false;
            config.CommandEditor.ValidationDebounceMs = 900;
            config.CommandEditor.ShowInlineWarnings = false;
            config.CommandEditor.EnableDiagnosticTooltips = false;
            config.CommandEditor.EnableVariableInspectorTooltips = false;
            config.CommandEditor.EnableYamlHygieneWarnings = false;
            config.CommandEditor.UseSpacesForTab = false;
            config.CommandEditor.IndentSize = 4;
            config.CommandEditor.EnableSmartEnter = false;
            config.CommandEditor.PreserveBlankLineBetweenSteps = false;
        });

        var reloaded = new ConfigurationService(_configPath).Load();
        var editor = reloaded.CommandEditor;

        editor.EnableSyntaxHighlighting.Should().BeFalse();
        editor.EnableAutocomplete.Should().BeFalse();
        editor.AutocompleteShowOnTyping.Should().BeFalse();
        editor.EnableInlineValidation.Should().BeFalse();
        editor.ValidationDebounceMs.Should().Be(900);
        editor.ShowInlineWarnings.Should().BeFalse();
        editor.EnableDiagnosticTooltips.Should().BeFalse();
        editor.EnableVariableInspectorTooltips.Should().BeFalse();
        editor.EnableYamlHygieneWarnings.Should().BeFalse();
        editor.UseSpacesForTab.Should().BeFalse();
        editor.IndentSize.Should().Be(4);
        editor.EnableSmartEnter.Should().BeFalse();
        editor.PreserveBlankLineBetweenSteps.Should().BeFalse();
    }

    [Fact]
    public void Load_OutOfRangeValues_ClampsToSafeBounds()
    {
        var json = $$"""
                     {
                       "Presets": {},
                       "CommandEditor": {
                         "ValidationDebounceMs": 5,
                         "IndentSize": 99
                       }
                     }
                     """;
        File.WriteAllText(_configPath, json);

        var config = _configurationService.Load();

        config.CommandEditor.ValidationDebounceMs.Should().Be(CommandEditorSettings.MinValidationDebounceMs);
        config.CommandEditor.IndentSize.Should().Be(CommandEditorSettings.MaxIndentSize);
    }

    [Fact]
    public void Update_CommandEditorSettings_NormalizesBeforePersisting()
    {
        _configurationService.Update(config =>
        {
            config.CommandEditor.ValidationDebounceMs = -1;
            config.CommandEditor.IndentSize = 0;
        });

        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.CommandEditor.ValidationDebounceMs.Should().Be(CommandEditorSettings.MinValidationDebounceMs);
        reloaded.CommandEditor.IndentSize.Should().Be(CommandEditorSettings.MinIndentSize);
    }
}
