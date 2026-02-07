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
        _configService.SaveEnvironmentState(map, EnvironmentConfig.DefaultName);

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
}
