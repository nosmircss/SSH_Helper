using FluentAssertions;
using Newtonsoft.Json;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.Models;

public class MaxConcurrentJobsTests
{
    [Fact]
    public void AppConfiguration_MaxConcurrentJobs_DefaultsToThree()
    {
        var config = new AppConfiguration();

        config.MaxConcurrentJobs.Should().Be(3);
    }

    [Fact]
    public void AppConfiguration_MaxConcurrentJobs_CanBeSet()
    {
        var config = new AppConfiguration();
        config.MaxConcurrentJobs = 10;

        config.MaxConcurrentJobs.Should().Be(10);
    }

    [Fact]
    public void AppConfiguration_MaxConcurrentJobs_AllowsZero()
    {
        // Validation happens at service level, not model level
        var config = new AppConfiguration();
        config.MaxConcurrentJobs = 0;

        config.MaxConcurrentJobs.Should().Be(0);
    }

    [Fact]
    public void AppConfiguration_MaxConcurrentJobs_AllowsNegative()
    {
        // Validation happens at service level, not model level
        var config = new AppConfiguration();
        config.MaxConcurrentJobs = -1;

        config.MaxConcurrentJobs.Should().Be(-1);
    }

    [Fact]
    public void AppConfiguration_MaxConcurrentJobs_RoundTripsJson()
    {
        var config = new AppConfiguration { MaxConcurrentJobs = 5 };

        var json = JsonConvert.SerializeObject(config);
        var deserialized = JsonConvert.DeserializeObject<AppConfiguration>(json);

        deserialized.Should().NotBeNull();
        deserialized!.MaxConcurrentJobs.Should().Be(5);
    }

    [Fact]
    public void AppConfiguration_MaxConcurrentJobs_DefaultsInJsonWithoutProperty()
    {
        // Simulates loading a config.json that doesn't have MaxConcurrentJobs
        // (e.g., pre-Phase 3 config file)
        var json = "{}";
        var deserialized = JsonConvert.DeserializeObject<AppConfiguration>(json);

        deserialized.Should().NotBeNull();
        deserialized!.MaxConcurrentJobs.Should().Be(3);
    }

    [Fact]
    public void AppConfiguration_ExistingProperties_UnchangedAfterAddingMaxConcurrentJobs()
    {
        var config = new AppConfiguration();

        // Verify key existing properties still have correct defaults
        config.Timeout.Should().Be(10);
        config.ConnectionTimeout.Should().Be(30);
        config.Username.Should().BeEmpty();
        config.Presets.Should().NotBeNull();
        config.LastAppShutdownUtc.Should().BeNull();
    }
}
