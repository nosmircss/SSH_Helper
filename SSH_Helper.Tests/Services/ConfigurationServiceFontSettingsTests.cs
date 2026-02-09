using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class ConfigurationServiceFontSettingsTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly ConfigurationService _configService;

    public ConfigurationServiceFontSettingsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(),
            $"FontSettingsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
        _configService = new ConfigurationService(_configPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch { /* best-effort cleanup */ }
    }

    #region Round-Trip Tests

    [Fact]
    public void SaveAndLoad_DefaultFontSettings_RoundTrips()
    {
        var config = _configService.Load();
        var defaults = FontSettings.CreateDefault();

        // Save and reload from a fresh service instance
        _configService.Save(config);
        var reloaded = new ConfigurationService(_configPath).Load();

        reloaded.FontSettings.UIFontFamily.Should().Be(defaults.UIFontFamily);
        reloaded.FontSettings.CodeFontFamily.Should().Be(defaults.CodeFontFamily);
        reloaded.FontSettings.SectionTitleFontSize.Should().Be(defaults.SectionTitleFontSize);
        reloaded.FontSettings.TreeViewFontSize.Should().Be(defaults.TreeViewFontSize);
        reloaded.FontSettings.EmptyLabelFontSize.Should().Be(defaults.EmptyLabelFontSize);
        reloaded.FontSettings.ExecuteButtonFontSize.Should().Be(defaults.ExecuteButtonFontSize);
        reloaded.FontSettings.CodeEditorFontSize.Should().Be(defaults.CodeEditorFontSize);
        reloaded.FontSettings.OutputAreaFontSize.Should().Be(defaults.OutputAreaFontSize);
        reloaded.FontSettings.TabFontSize.Should().Be(defaults.TabFontSize);
        reloaded.FontSettings.ButtonFontSize.Should().Be(defaults.ButtonFontSize);
        reloaded.FontSettings.HostListFontSize.Should().Be(defaults.HostListFontSize);
        reloaded.FontSettings.MenuFontSize.Should().Be(defaults.MenuFontSize);
        reloaded.FontSettings.StatusBarFontSize.Should().Be(defaults.StatusBarFontSize);
        reloaded.FontSettings.GlobalScaleFactor.Should().Be(defaults.GlobalScaleFactor);
        reloaded.FontSettings.CodeEditorWordWrap.Should().Be(defaults.CodeEditorWordWrap);
        reloaded.FontSettings.OutputAreaWordWrap.Should().Be(defaults.OutputAreaWordWrap);
        reloaded.FontSettings.TreeViewRowHeight.Should().Be(defaults.TreeViewRowHeight);
        reloaded.FontSettings.HostListRowHeight.Should().Be(defaults.HostListRowHeight);
        reloaded.FontSettings.CustomAccentColor.Should().Be(defaults.CustomAccentColor);
    }

    [Fact]
    public void SaveAndLoad_CustomFontSettings_AllPropertiesRoundTrip()
    {
        var config = _configService.Load();
        var fs = config.FontSettings;

        // Set every property to a non-default value
        fs.UIFontFamily = "Arial";
        fs.CodeFontFamily = "Consolas";
        fs.SectionTitleFontSize = 12f;
        fs.TreeViewFontSize = 11f;
        fs.EmptyLabelFontSize = 10f;
        fs.ExecuteButtonFontSize = 13f;
        fs.CodeEditorFontSize = 14f;
        fs.OutputAreaFontSize = 11.5f;
        fs.TabFontSize = 10.5f;
        fs.ButtonFontSize = 10f;
        fs.HostListFontSize = 11f;
        fs.MenuFontSize = 10f;
        fs.StatusBarFontSize = 8.5f;
        fs.GlobalScaleFactor = 1.3f;
        fs.CodeEditorWordWrap = true;
        fs.OutputAreaWordWrap = true;
        fs.TreeViewRowHeight = 30;
        fs.HostListRowHeight = 35;
        fs.CustomAccentColor = System.Drawing.Color.CornflowerBlue.ToArgb();

        _configService.Save(config);
        var reloaded = new ConfigurationService(_configPath).Load();

        reloaded.FontSettings.UIFontFamily.Should().Be("Arial");
        reloaded.FontSettings.CodeFontFamily.Should().Be("Consolas");
        reloaded.FontSettings.SectionTitleFontSize.Should().Be(12f);
        reloaded.FontSettings.TreeViewFontSize.Should().Be(11f);
        reloaded.FontSettings.EmptyLabelFontSize.Should().Be(10f);
        reloaded.FontSettings.ExecuteButtonFontSize.Should().Be(13f);
        reloaded.FontSettings.CodeEditorFontSize.Should().Be(14f);
        reloaded.FontSettings.OutputAreaFontSize.Should().Be(11.5f);
        reloaded.FontSettings.TabFontSize.Should().Be(10.5f);
        reloaded.FontSettings.ButtonFontSize.Should().Be(10f);
        reloaded.FontSettings.HostListFontSize.Should().Be(11f);
        reloaded.FontSettings.MenuFontSize.Should().Be(10f);
        reloaded.FontSettings.StatusBarFontSize.Should().Be(8.5f);
        reloaded.FontSettings.GlobalScaleFactor.Should().Be(1.3f);
        reloaded.FontSettings.CodeEditorWordWrap.Should().BeTrue();
        reloaded.FontSettings.OutputAreaWordWrap.Should().BeTrue();
        reloaded.FontSettings.TreeViewRowHeight.Should().Be(30);
        reloaded.FontSettings.HostListRowHeight.Should().Be(35);
        reloaded.FontSettings.CustomAccentColor.Should().Be(System.Drawing.Color.CornflowerBlue.ToArgb());
    }

    [Theory]
    [InlineData(7f)]
    [InlineData(16f)]
    public void SaveAndLoad_AllFontSizes_AtBoundaryValues(float fontSize)
    {
        _configService.Update(c =>
        {
            c.FontSettings.SectionTitleFontSize = fontSize;
            c.FontSettings.TreeViewFontSize = fontSize;
            c.FontSettings.EmptyLabelFontSize = fontSize;
            c.FontSettings.ExecuteButtonFontSize = fontSize;
            c.FontSettings.CodeEditorFontSize = fontSize;
            c.FontSettings.OutputAreaFontSize = fontSize;
            c.FontSettings.TabFontSize = fontSize;
            c.FontSettings.ButtonFontSize = fontSize;
            c.FontSettings.HostListFontSize = fontSize;
            c.FontSettings.MenuFontSize = fontSize;
            c.FontSettings.StatusBarFontSize = fontSize;
        });

        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.FontSettings.SectionTitleFontSize.Should().Be(fontSize);
        reloaded.FontSettings.TreeViewFontSize.Should().Be(fontSize);
        reloaded.FontSettings.CodeEditorFontSize.Should().Be(fontSize);
        reloaded.FontSettings.OutputAreaFontSize.Should().Be(fontSize);
        reloaded.FontSettings.ButtonFontSize.Should().Be(fontSize);
        reloaded.FontSettings.StatusBarFontSize.Should().Be(fontSize);
    }

    [Theory]
    [InlineData(0.8f)]
    [InlineData(1.0f)]
    [InlineData(1.5f)]
    public void SaveAndLoad_GlobalScaleFactor_PreservesValue(float scale)
    {
        _configService.Update(c => c.FontSettings.GlobalScaleFactor = scale);
        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.FontSettings.GlobalScaleFactor.Should().Be(scale);
    }

    [Fact]
    public void SaveAndLoad_CustomAccentColor_PreservesArgbValue()
    {
        var argb = System.Drawing.Color.CornflowerBlue.ToArgb();
        _configService.Update(c => c.FontSettings.CustomAccentColor = argb);
        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.FontSettings.CustomAccentColor.Should().Be(argb);
    }

    [Fact]
    public void SaveAndLoad_NullCustomAccentColor_PreservesNull()
    {
        _configService.Update(c => c.FontSettings.CustomAccentColor = null);
        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.FontSettings.CustomAccentColor.Should().BeNull();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SaveAndLoad_BooleanSettings_AllCombinations(bool codeWrap, bool outputWrap)
    {
        _configService.Update(c =>
        {
            c.FontSettings.CodeEditorWordWrap = codeWrap;
            c.FontSettings.OutputAreaWordWrap = outputWrap;
        });

        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.FontSettings.CodeEditorWordWrap.Should().Be(codeWrap);
        reloaded.FontSettings.OutputAreaWordWrap.Should().Be(outputWrap);
    }

    #endregion

    #region Resilience Tests

    [Fact]
    public void Load_MissingFontSettingsKey_ReturnsDefaults()
    {
        // Write minimal JSON without FontSettings
        File.WriteAllText(_configPath, """{"Username":"","Timeout":10,"Presets":{}}""");

        var service = new ConfigurationService(_configPath);
        var config = service.Load();

        // Should use default FontSettings values
        config.FontSettings.Should().NotBeNull();
        config.FontSettings.UIFontFamily.Should().Be("Segoe UI Semibold");
        config.FontSettings.GlobalScaleFactor.Should().Be(1.0f);
    }

    [Fact]
    public void Load_PartialFontSettings_FillsRemainingWithDefaults()
    {
        // Write JSON with only UIFontFamily set
        File.WriteAllText(_configPath, """{"FontSettings":{"UIFontFamily":"Arial"},"Presets":{}}""");

        var service = new ConfigurationService(_configPath);
        var config = service.Load();

        config.FontSettings.UIFontFamily.Should().Be("Arial");
        // All other properties should be their defaults
        config.FontSettings.CodeFontFamily.Should().Be("Cascadia Code");
        config.FontSettings.GlobalScaleFactor.Should().Be(1.0f);
        config.FontSettings.SectionTitleFontSize.Should().Be(9.5f);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaultConfig()
    {
        File.WriteAllText(_configPath, "{{invalid json");

        var service = new ConfigurationService(_configPath);
        var config = service.Load();

        config.Should().NotBeNull();
        config.FontSettings.Should().NotBeNull();
        service.ConfigLoadError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Update_FontSettings_PersistsAndReloads()
    {
        _configService.Load();
        _configService.Update(c => c.FontSettings.GlobalScaleFactor = 1.3f);

        var freshService = new ConfigurationService(_configPath);
        var reloaded = freshService.Load();
        reloaded.FontSettings.GlobalScaleFactor.Should().Be(1.3f);
    }

    #endregion
}
