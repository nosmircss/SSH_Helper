using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class ExecutionCoordinatorTests : IDisposable
{
    private readonly string _testDirectory;

    public ExecutionCoordinatorTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ExecutionCoordinatorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void PrepareExecution_UsesConfiguredTimeouts()
    {
        var configPath = Path.Combine(_testDirectory, "config.json");
        var configService = new ConfigurationService(configPath);
        configService.Update(config => { config.ConnectionTimeout = 45; });

        var coordinator = new ExecutionCoordinator(new SshExecutionService(), configService);

        var preparation = coordinator.PrepareExecution("echo test", 12);

        preparation.CommandTimeoutSeconds.Should().Be(12);
        preparation.ConnectionTimeoutSeconds.Should().Be(45);
        preparation.Preset.Commands.Should().Be("echo test");
    }
}
