using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class EnvironmentServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly ConfigurationService _configService;
    private readonly EnvironmentService _environmentService;

    public EnvironmentServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"EnvironmentServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
        _configService = new ConfigurationService(_configPath);
        _configService.Load();
        _environmentService = new EnvironmentService(_configService);
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
    public void GetEnvironmentNames_WhenNoExplicitEnvironments_ReturnsDefaultOnly()
    {
        var names = _environmentService.GetEnvironmentNames();
        names.Should().Equal(EnvironmentConfig.DefaultName);
    }

    [Fact]
    public void GetEnvironment_Default_UsesLegacySavedStateWhenNoExplicitEnvironments()
    {
        _configService.Update(config =>
        {
            config.SavedState = new ApplicationState
            {
                HostColumns = new List<string> { CsvManager.HostColumnName, "username" },
                Hosts = new List<Dictionary<string, string>>
                {
                    new()
                    {
                        [CsvManager.HostColumnName] = "10.0.0.1",
                        ["username"] = "admin"
                    }
                },
                SelectedHostIndices = new List<int> { 0 },
                LastCsvPath = @"C:\tmp\legacy.csv"
            };
        });

        var legacyDefault = _environmentService.GetEnvironment(EnvironmentConfig.DefaultName);
        legacyDefault.HostColumns.Should().Contain(CsvManager.HostColumnName);
        legacyDefault.Hosts.Should().HaveCount(1);
        legacyDefault.Hosts[0][CsvManager.HostColumnName].Should().Be("10.0.0.1");
        legacyDefault.SelectedHostIndices.Should().ContainSingle().Which.Should().Be(0);
        legacyDefault.LastCsvPath.Should().Be(@"C:\tmp\legacy.csv");
    }

    [Fact]
    public void CreateEnvironment_InLegacyProfile_CreatesDefaultSnapshotAndNewEnvironment()
    {
        _configService.Update(config =>
        {
            config.SavedState = new ApplicationState
            {
                HostColumns = new List<string> { CsvManager.HostColumnName },
                Hosts = new List<Dictionary<string, string>>
                {
                    new() { [CsvManager.HostColumnName] = "192.0.2.10" }
                }
            };
        });

        var created = _environmentService.CreateEnvironment("prod");

        created.Name.Should().Be("prod");
        var persisted = _configService.GetCurrent();
        persisted.Environments.Should().ContainKey(EnvironmentConfig.DefaultName);
        persisted.Environments.Should().ContainKey("prod");
        persisted.Environments[EnvironmentConfig.DefaultName].Hosts.Should().HaveCount(1);
        persisted.Environments[EnvironmentConfig.DefaultName].Hosts[0][CsvManager.HostColumnName].Should().Be("192.0.2.10");
    }

    [Fact]
    public void SwitchEnvironment_SetsActiveAndRaisesEvent()
    {
        var map = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)
        {
            [EnvironmentConfig.DefaultName] = new EnvironmentConfig { Name = EnvironmentConfig.DefaultName },
            ["prod"] = new EnvironmentConfig
            {
                Name = "prod",
                Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["site"] = "dc-1"
                }
            }
        };
        _configService.SaveEnvironmentState(map, EnvironmentConfig.DefaultName, EnvironmentConfig.DefaultName);

        EnvironmentChangedEventArgs? raised = null;
        _environmentService.EnvironmentChanged += (_, args) => raised = args;

        var active = _environmentService.SwitchEnvironment("prod");

        active.Name.Should().Be("prod");
        _environmentService.GetActiveEnvironmentName().Should().Be("prod");
        raised.Should().NotBeNull();
        raised!.PreviousEnvironment.Should().Be(EnvironmentConfig.DefaultName);
        raised.CurrentEnvironment.Should().Be("prod");
    }

    [Fact]
    public void DeleteEnvironment_Default_Throws()
    {
        Action act = () => _environmentService.DeleteEnvironment(EnvironmentConfig.DefaultName);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetBaseEnvironmentName_WhenPersistedBaseIsMissing_ReturnsActiveEnvironment()
    {
        _configService.Update(config =>
        {
            config.Environments = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)
            {
                [EnvironmentConfig.DefaultName] = new EnvironmentConfig { Name = EnvironmentConfig.DefaultName },
                ["prod"] = new EnvironmentConfig { Name = "prod" }
            };
            config.ActiveEnvironment = "prod";
            config.BaseEnvironment = null;
        });

        _environmentService.GetBaseEnvironmentName().Should().Be("prod");
        _configService.GetCurrent().BaseEnvironment.Should().Be("prod");
    }

    [Fact]
    public void SetBaseEnvironment_PersistsAcrossReload()
    {
        var map = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)
        {
            [EnvironmentConfig.DefaultName] = new EnvironmentConfig { Name = EnvironmentConfig.DefaultName },
            ["prod"] = new EnvironmentConfig { Name = "prod" }
        };
        _configService.SaveEnvironmentState(map, EnvironmentConfig.DefaultName, EnvironmentConfig.DefaultName);

        _environmentService.SetBaseEnvironment("prod");

        _environmentService.GetBaseEnvironmentName().Should().Be("prod");

        var reloadedConfig = new ConfigurationService(_configPath);
        var reloadedService = new EnvironmentService(reloadedConfig);
        reloadedConfig.Load();
        reloadedService.GetBaseEnvironmentName().Should().Be("prod");
    }

    [Fact]
    public void SwitchEnvironment_DoesNotChangeBaseEnvironment()
    {
        var map = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)
        {
            [EnvironmentConfig.DefaultName] = new EnvironmentConfig { Name = EnvironmentConfig.DefaultName },
            ["prod"] = new EnvironmentConfig { Name = "prod" }
        };
        _configService.SaveEnvironmentState(map, EnvironmentConfig.DefaultName, EnvironmentConfig.DefaultName);

        _environmentService.SwitchEnvironment("prod");

        _environmentService.GetActiveEnvironmentName().Should().Be("prod");
        _environmentService.GetBaseEnvironmentName().Should().Be(EnvironmentConfig.DefaultName);
    }

    [Fact]
    public void SaveCurrentGridToEnvironment_PreservesLabelColor()
    {
        _environmentService.CreateEnvironment("prod");
        const int labelColor = unchecked((int)0xFF1E90FF);

        _environmentService.UpdateEnvironmentDetails(
            "prod",
            "Production",
            labelColor,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["region"] = "us-east-1"
            });

        _environmentService.SaveCurrentGridToEnvironment(
            "prod",
            new List<string> { CsvManager.HostColumnName },
            new List<Dictionary<string, string>>
            {
                new() { [CsvManager.HostColumnName] = "203.0.113.10" }
            },
            new List<int> { 0 },
            @"C:\tmp\prod.csv");

        var environment = _environmentService.GetEnvironment("prod");
        environment.LabelColor.Should().Be(labelColor);
        environment.Hosts.Should().HaveCount(1);
        environment.Hosts[0][CsvManager.HostColumnName].Should().Be("203.0.113.10");
    }

    [Fact]
    public void SaveCurrentGridToEnvironment_PersistsCsvFingerprint()
    {
        _environmentService.CreateEnvironment("prod");
        var fingerprint = new CsvFileFingerprint
        {
            LastWriteTimeUtc = new DateTime(2026, 3, 6, 18, 0, 0, DateTimeKind.Utc),
            FileSizeBytes = 128
        };

        _environmentService.SaveCurrentGridToEnvironment(
            "prod",
            new List<string> { CsvManager.HostColumnName },
            new List<Dictionary<string, string>>
            {
                new() { [CsvManager.HostColumnName] = "203.0.113.11" }
            },
            new List<int>(),
            @"C:\tmp\prod.csv",
            fingerprint);

        var environment = _environmentService.GetEnvironment("prod");
        environment.LastCsvFingerprint.Should().NotBeNull();
        environment.LastCsvFingerprint!.LastWriteTimeUtc.Should().Be(fingerprint.LastWriteTimeUtc);
        environment.LastCsvFingerprint.FileSizeBytes.Should().Be(fingerprint.FileSizeBytes);
    }

    [Fact]
    public void SaveCurrentGridToEnvironment_PreservesVaultProfileName()
    {
        _environmentService.CreateEnvironment("prod");
        _environmentService.UpdateEnvironmentDetails(
            "prod",
            description: "Production",
            labelColor: null,
            variables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            vaultProfileName: "vault-prod");

        _environmentService.SaveCurrentGridToEnvironment(
            "prod",
            new List<string> { CsvManager.HostColumnName, "username" },
            new List<Dictionary<string, string>>
            {
                new()
                {
                    [CsvManager.HostColumnName] = "203.0.113.90",
                    ["username"] = "admin"
                }
            },
            new List<int> { 0 },
            @"C:\tmp\prod.csv");

        var environment = _environmentService.GetEnvironment("prod");
        environment.VaultProfileName.Should().Be("vault-prod");
    }

    [Fact]
    public void ImportEnvironment_WhenEnvironmentExistsWithoutOverwrite_Throws()
    {
        _environmentService.CreateEnvironment("prod");

        var imported = new EnvironmentConfig
        {
            Name = "prod",
            Description = "Imported profile"
        };

        Action act = () => _environmentService.ImportEnvironment(imported, overwriteExisting: false);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public void ImportEnvironment_WhenOverwriteEnabled_ReplacesEnvironmentSnapshot()
    {
        _environmentService.CreateEnvironment("prod");
        _environmentService.SaveCurrentGridToEnvironment(
            "prod",
            new List<string> { CsvManager.HostColumnName, "username" },
            new List<Dictionary<string, string>>
            {
                new()
                {
                    [CsvManager.HostColumnName] = "198.51.100.10",
                    ["username"] = "before"
                }
            },
            new List<int> { 0 },
            @"C:\tmp\before.csv");

        var imported = new EnvironmentConfig
        {
            Name = "prod",
            Description = "Imported production",
            HostColumns = new List<string> { CsvManager.HostColumnName, "port" },
            Hosts = new List<Dictionary<string, string>>
            {
                new()
                {
                    [CsvManager.HostColumnName] = "198.51.100.77",
                    ["port"] = "2222"
                }
            },
            SelectedHostIndices = new List<int> { 0 },
            LastCsvPath = @"C:\tmp\imported.csv",
            Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["region"] = "us-west-2"
            }
        };

        var saved = _environmentService.ImportEnvironment(imported, overwriteExisting: true);

        saved.Name.Should().Be("prod");
        saved.Description.Should().Be("Imported production");

        var current = _environmentService.GetEnvironment("prod");
        current.HostColumns.Should().ContainInOrder(CsvManager.HostColumnName, "port");
        current.Hosts.Should().ContainSingle();
        current.Hosts[0][CsvManager.HostColumnName].Should().Be("198.51.100.77");
        current.Hosts[0]["port"].Should().Be("2222");
        current.LastCsvPath.Should().Be(@"C:\tmp\imported.csv");
        current.Variables.Should().ContainKey("region").WhoseValue.Should().Be("us-west-2");
    }

    [Fact]
    public void ImportEnvironment_InLegacyProfile_CapturesDefaultSnapshotBeforeImport()
    {
        _configService.Update(config =>
        {
            config.SavedState = new ApplicationState
            {
                HostColumns = new List<string> { CsvManager.HostColumnName },
                Hosts = new List<Dictionary<string, string>>
                {
                    new() { [CsvManager.HostColumnName] = "203.0.113.50" }
                }
            };
            config.Environments.Clear();
            config.ActiveEnvironment = null;
        });

        _environmentService.ImportEnvironment(new EnvironmentConfig
        {
            Name = "staging",
            HostColumns = new List<string> { CsvManager.HostColumnName },
            Hosts = new List<Dictionary<string, string>>
            {
                new() { [CsvManager.HostColumnName] = "203.0.113.60" }
            }
        });

        var persisted = _configService.GetCurrent();
        persisted.Environments.Should().ContainKey(EnvironmentConfig.DefaultName);
        persisted.Environments.Should().ContainKey("staging");
        persisted.Environments[EnvironmentConfig.DefaultName].Hosts.Should().ContainSingle();
        persisted.Environments[EnvironmentConfig.DefaultName].Hosts[0][CsvManager.HostColumnName].Should().Be("203.0.113.50");
        persisted.BaseEnvironment.Should().Be(EnvironmentConfig.DefaultName);
    }

    [Fact]
    public void RenameEnvironment_WhenBaseEnvironmentMatches_UpdatesBaseEnvironment()
    {
        var map = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)
        {
            [EnvironmentConfig.DefaultName] = new EnvironmentConfig { Name = EnvironmentConfig.DefaultName },
            ["prod"] = new EnvironmentConfig { Name = "prod" }
        };
        _configService.SaveEnvironmentState(map, EnvironmentConfig.DefaultName, "prod");

        _environmentService.RenameEnvironment("prod", "production");

        _environmentService.GetBaseEnvironmentName().Should().Be("production");
    }

    [Fact]
    public void DeleteEnvironment_WhenBaseEnvironmentMatches_FallsBackToActiveEnvironment()
    {
        var map = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)
        {
            [EnvironmentConfig.DefaultName] = new EnvironmentConfig { Name = EnvironmentConfig.DefaultName },
            ["prod"] = new EnvironmentConfig { Name = "prod" },
            ["staging"] = new EnvironmentConfig { Name = "staging" }
        };
        _configService.SaveEnvironmentState(map, "staging", "prod");

        _environmentService.DeleteEnvironment("prod");

        _environmentService.GetActiveEnvironmentName().Should().Be("staging");
        _environmentService.GetBaseEnvironmentName().Should().Be("staging");
    }

    [Fact]
    public void UpdateActiveEnvironmentVariable_UpdatesOnlyActiveEnvironment()
    {
        var map = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)
        {
            [EnvironmentConfig.DefaultName] = new EnvironmentConfig
            {
                Name = EnvironmentConfig.DefaultName,
                Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["api_token"] = "default-token"
                }
            },
            ["prod"] = new EnvironmentConfig
            {
                Name = "prod",
                Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["api_token"] = "old-token"
                }
            }
        };
        _configService.SaveEnvironmentState(map, "prod", EnvironmentConfig.DefaultName);

        _environmentService.UpdateActiveEnvironmentVariable("api_token", "new-token");

        _environmentService.GetEnvironment("prod")
            .Variables.Should().ContainKey("api_token").WhoseValue.Should().Be("new-token");
        _environmentService.GetEnvironment(EnvironmentConfig.DefaultName)
            .Variables.Should().ContainKey("api_token").WhoseValue.Should().Be("default-token");
    }

    [Fact]
    public void UpdateActiveEnvironmentVariable_InLegacyProfile_CreatesDefaultAndPersistsVariable()
    {
        _configService.Update(config =>
        {
            config.SavedState = new ApplicationState
            {
                HostColumns = new List<string> { CsvManager.HostColumnName },
                Hosts = new List<Dictionary<string, string>>
                {
                    new() { [CsvManager.HostColumnName] = "198.51.100.20" }
                }
            };
            config.Environments.Clear();
            config.ActiveEnvironment = null;
        });

        _environmentService.UpdateActiveEnvironmentVariable("api_token", "legacy-token");

        var persisted = _configService.GetCurrent();
        persisted.Environments.Should().ContainKey(EnvironmentConfig.DefaultName);
        persisted.Environments[EnvironmentConfig.DefaultName].Hosts.Should().ContainSingle();
        persisted.Environments[EnvironmentConfig.DefaultName].Hosts[0][CsvManager.HostColumnName].Should().Be("198.51.100.20");
        persisted.Environments[EnvironmentConfig.DefaultName]
            .Variables.Should().ContainKey("api_token").WhoseValue.Should().Be("legacy-token");
        persisted.BaseEnvironment.Should().Be(EnvironmentConfig.DefaultName);
    }
}
