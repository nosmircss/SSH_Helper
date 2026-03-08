using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class SchedulerHistoryPolicyResolverTests
{
    [Fact]
    public void Resolve_UsesPerJobOverridesWhenPresent()
    {
        var config = new AppConfiguration
        {
            DefaultMaxHistoryRuns = 50,
            DefaultHistoryRetentionDays = 30,
            MaxJobOutputCharsPerHost = 1000
        };
        var job = new JobDefinition
        {
            MaxHistoryRuns = 7,
            HistoryRetentionDays = 14
        };

        var options = SchedulerHistoryPolicyResolver.Resolve(config, job);

        options.MaxRuns.Should().Be(7);
        options.RetentionDays.Should().Be(14);
        options.MaxOutputChars.Should().Be(1000);
    }

    [Fact]
    public void Resolve_UsesGlobalDefaultsWhenOverridesAreMissing()
    {
        var config = new AppConfiguration
        {
            DefaultMaxHistoryRuns = 60,
            DefaultHistoryRetentionDays = 45,
            MaxJobOutputCharsPerHost = 2048
        };

        var options = SchedulerHistoryPolicyResolver.Resolve(config, new JobDefinition());

        options.MaxRuns.Should().Be(60);
        options.RetentionDays.Should().Be(45);
        options.MaxOutputChars.Should().Be(2048);
    }
}
