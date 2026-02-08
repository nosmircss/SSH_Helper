using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class ConfigurationServiceWindowStateTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly ConfigurationService _configService;

    public ConfigurationServiceWindowStateTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"WindowStateTests_{Guid.NewGuid():N}");
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
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void SaveAndLoad_EnvironmentDialogLayout_PreservesValues()
    {
        _configService.Load();
        _configService.Update(config =>
        {
            config.WindowState.EnvironmentDialogWidth = 1110;
            config.WindowState.EnvironmentDialogHeight = 710;
            config.WindowState.EnvironmentDialogSplitterDistance = 345;
        });

        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.WindowState.EnvironmentDialogWidth.Should().Be(1110);
        reloaded.WindowState.EnvironmentDialogHeight.Should().Be(710);
        reloaded.WindowState.EnvironmentDialogSplitterDistance.Should().Be(345);
    }
}
