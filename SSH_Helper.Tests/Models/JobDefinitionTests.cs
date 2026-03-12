using FluentAssertions;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.Models;

public class JobDefinitionTests
{
    [Fact]
    public void NewJobDefinition_HasUniqueGuidId()
    {
        var job = new JobDefinition();

        job.Id.Should().NotBeNullOrEmpty();
        job.Id.Should().HaveLength(32, "GUID ToString(\"N\") produces 32 hex chars");
        job.Id.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void NewJobDefinition_TwoInstances_HaveDifferentIds()
    {
        var job1 = new JobDefinition();
        var job2 = new JobDefinition();

        job1.Id.Should().NotBe(job2.Id);
    }

    [Fact]
    public void NewJobDefinition_IsEnabledByDefault()
    {
        var job = new JobDefinition();

        job.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void NewJobDefinition_CredentialModeDefaultsToInheritFromApp()
    {
        var job = new JobDefinition();

        job.CredentialMode.Should().Be(CredentialMode.InheritFromApp);
    }

    [Fact]
    public void NewJobDefinition_HostsIsEmptyList()
    {
        var job = new JobDefinition();

        job.Hosts.Should().NotBeNull();
        job.Hosts.Should().BeEmpty();
    }

    [Fact]
    public void NewJobDefinition_HostColumnsIsEmptyList()
    {
        var job = new JobDefinition();

        job.HostColumns.Should().NotBeNull();
        job.HostColumns.Should().BeEmpty();
    }

    [Fact]
    public void NewJobDefinition_HasTimestamps()
    {
        var before = DateTime.UtcNow;
        var job = new JobDefinition();
        var after = DateTime.UtcNow;

        job.CreatedUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        job.ModifiedUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void NewJobDefinition_NullableFieldsAreNull()
    {
        var job = new JobDefinition();

        job.CronExpression.Should().BeNull();
        job.OneTimeScheduleUtc.Should().BeNull();
        job.DisabledReason.Should().BeNull();
        job.FolderPresetHashes.Should().BeNull();
        job.CustomPresetCommands.Should().BeEmpty();
        job.CommandTimeoutOverrideSeconds.Should().BeNull();
        job.ConnectionTimeoutOverrideSeconds.Should().BeNull();
    }

    [Fact]
    public void NewJobDefinition_HasDriftWarningIsFalse()
    {
        var job = new JobDefinition();

        job.HasDriftWarning.Should().BeFalse();
    }

    [Fact]
    public void NewJobDefinition_HasAllRequiredProperties()
    {
        var job = new JobDefinition();

        // Verify all properties exist and are settable
        job.Name = "Test Job";
        job.TargetType = JobTargetType.CustomPreset;
        job.TargetName = string.Empty;
        job.CustomPresetCommands = "echo custom";
        job.TargetContentHash = "abc123";
        job.FolderPresetHashes = new Dictionary<string, string> { { "preset1", "hash1" } };
        job.CronExpression = "0 * * * *";
        job.OneTimeScheduleUtc = DateTime.UtcNow;
        job.HasDriftWarning = true;
        job.DisabledReason = "maintenance";
        job.CommandTimeoutOverrideSeconds = 45;
        job.ConnectionTimeoutOverrideSeconds = 30;

        job.Name.Should().Be("Test Job");
        job.TargetType.Should().Be(JobTargetType.CustomPreset);
        job.TargetName.Should().BeEmpty();
        job.CustomPresetCommands.Should().Be("echo custom");
        job.TargetContentHash.Should().Be("abc123");
        job.FolderPresetHashes.Should().ContainKey("preset1");
        job.CronExpression.Should().Be("0 * * * *");
        job.OneTimeScheduleUtc.Should().NotBeNull();
        job.HasDriftWarning.Should().BeTrue();
        job.DisabledReason.Should().Be("maintenance");
        job.CommandTimeoutOverrideSeconds.Should().Be(45);
        job.ConnectionTimeoutOverrideSeconds.Should().Be(30);
    }

    [Fact]
    public void CredentialMode_HasExpectedValues()
    {
        ((int)CredentialMode.InheritFromApp).Should().Be(0);
        ((int)CredentialMode.Stored).Should().Be(1);
        ((int)CredentialMode.PerHostColumn).Should().Be(2);
    }

    [Fact]
    public void JobTargetType_HasExpectedValues()
    {
        ((int)JobTargetType.Preset).Should().Be(0);
        ((int)JobTargetType.Folder).Should().Be(1);
        ((int)JobTargetType.CustomPreset).Should().Be(2);
    }
}
