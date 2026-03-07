using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class PresetManagerFolderBaseEnvironmentTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly ConfigurationService _configService;
    private readonly PresetManager _presetManager;

    public PresetManagerFolderBaseEnvironmentTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PresetManagerFolderBaseEnvironmentTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
        _configService = new ConfigurationService(_configPath);
        _configService.Load();
        _presetManager = new PresetManager(_configService);
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
    public void SetFolderBaseEnvironment_PersistsAcrossReload()
    {
        SeedEnvironments("prod");
        _presetManager.Load();
        _presetManager.CreateFolder("Network/Prod");

        _presetManager.SetFolderBaseEnvironment("Network/Prod", "prod").Should().BeTrue();

        _presetManager.Folders["Network/Prod"].BaseEnvironment.Should().Be("prod");

        var reloadedConfig = new ConfigurationService(_configPath);
        var reloadedManager = new PresetManager(reloadedConfig);
        reloadedConfig.Load();
        reloadedManager.Load();

        reloadedManager.Folders["Network/Prod"].BaseEnvironment.Should().Be("prod");
    }

    [Fact]
    public void Load_WhenFolderBaseEnvironmentIsInvalid_ClearsOverride()
    {
        _configService.Update(config =>
        {
            config.Environments = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)
            {
                [EnvironmentConfig.DefaultName] = new() { Name = EnvironmentConfig.DefaultName }
            };
            config.ActiveEnvironment = EnvironmentConfig.DefaultName;
            config.BaseEnvironment = EnvironmentConfig.DefaultName;
            config.PresetFolders = new Dictionary<string, FolderInfo>
            {
                ["Network/Prod"] = new() { BaseEnvironment = "missing" }
            };
        });

        _presetManager.Load();

        _presetManager.Folders["Network/Prod"].BaseEnvironment.Should().BeNull();
        _configService.GetCurrent().PresetFolders["Network/Prod"].BaseEnvironment.Should().BeNull();
    }

    [Fact]
    public void RenameFolderBaseEnvironment_UpdatesAssignedFolders()
    {
        SeedEnvironments("prod", "production");
        _presetManager.Load();
        _presetManager.CreateFolder("Network/Prod");
        _presetManager.SetFolderBaseEnvironment("Network/Prod", "prod");

        _presetManager.RenameFolderBaseEnvironment("prod", "production").Should().Be(1);

        _presetManager.Folders["Network/Prod"].BaseEnvironment.Should().Be("production");
    }

    [Fact]
    public void ClearFolderBaseEnvironment_ClearsAssignedFolders()
    {
        SeedEnvironments("prod");
        _presetManager.Load();
        _presetManager.CreateFolder("Network/Prod");
        _presetManager.SetFolderBaseEnvironment("Network/Prod", "prod");

        _presetManager.ClearFolderBaseEnvironment("prod").Should().Be(1);

        _presetManager.Folders["Network/Prod"].BaseEnvironment.Should().BeNull();
    }

    [Fact]
    public void RenameFolder_PreservesBaseEnvironment()
    {
        SeedEnvironments("prod");
        _presetManager.Load();
        _presetManager.CreateFolder("Network/Prod");
        _presetManager.SetFolderBaseEnvironment("Network/Prod", "prod");

        _presetManager.RenameFolder("Network/Prod", "Network/Production").Should().BeTrue();

        _presetManager.Folders["Network/Production"].BaseEnvironment.Should().Be("prod");
        _presetManager.Folders.Should().NotContainKey("Network/Prod");
    }

    private void SeedEnvironments(params string[] additionalEnvironmentNames)
    {
        var environments = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)
        {
            [EnvironmentConfig.DefaultName] = new() { Name = EnvironmentConfig.DefaultName }
        };

        foreach (var environmentName in additionalEnvironmentNames)
        {
            environments[environmentName] = new EnvironmentConfig { Name = environmentName };
        }

        _configService.SaveEnvironmentState(environments, EnvironmentConfig.DefaultName, EnvironmentConfig.DefaultName);
    }
}
