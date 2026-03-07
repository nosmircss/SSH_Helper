using FluentAssertions;
using Moq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

/// <summary>
/// Integration tests for missed-run detection and one-time completion with real persistence.
/// Uses SchedulingService + ConfigurationService + JobStorageService against temp directories.
/// </summary>
public class SchedulingServiceMissedRunIntegrationTests : IDisposable
{
    private readonly SchedulingService _scheduling = new();
    private readonly string _tempDir;

    public SchedulingServiceMissedRunIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ssh_helper_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    #region Missed-Run Detection Tests

    [Fact]
    public void DetectMissedRuns_EnabledRecurringJob_5MinAgo_ProducesSkippedEntries()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "Every Minute Job",
                IsEnabled = true,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = "*/1 * * * *"
            }
        };

        var lastShutdown = DateTime.UtcNow.AddMinutes(-5);
        var skipped = _scheduling.DetectMissedRuns(jobs, lastShutdown);

        skipped.Should().NotBeEmpty();
        // With exclusive bounds on 5-min window at per-minute: ~4 entries
        skipped.Count.Should().BeInRange(3, 5);
    }

    [Fact]
    public void DetectMissedRuns_MultipleEnabledRecurringJobs_ProducesEntriesFromAll()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "Job A",
                IsEnabled = true,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = "*/1 * * * *"
            },
            ["job2"] = new JobDefinition
            {
                Id = "job2",
                Name = "Job B",
                IsEnabled = true,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = "*/1 * * * *"
            }
        };

        var lastShutdown = DateTime.UtcNow.AddMinutes(-5);
        var skipped = _scheduling.DetectMissedRuns(jobs, lastShutdown);

        skipped.Should().NotBeEmpty();
        skipped.Select(s => s.JobId).Distinct().Should().Contain("job1").And.Contain("job2");
    }

    [Fact]
    public void DetectMissedRuns_DisabledRecurringJob_IsSkipped()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "Disabled Recurring",
                IsEnabled = false,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = "*/1 * * * *"
            }
        };

        var skipped = _scheduling.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));

        skipped.Should().BeEmpty();
    }

    [Fact]
    public void DetectMissedRuns_OneTimeJob_IsSkipped()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "One-Time Job",
                IsEnabled = true,
                ScheduleType = ScheduleType.OneTime,
                CronExpression = "*/1 * * * *"
            }
        };

        var skipped = _scheduling.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));

        skipped.Should().BeEmpty();
    }

    [Fact]
    public void DetectMissedRuns_NoneScheduleType_IsSkipped()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "No Schedule",
                IsEnabled = true,
                ScheduleType = ScheduleType.None,
                CronExpression = "*/1 * * * *"
            }
        };

        var skipped = _scheduling.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));

        skipped.Should().BeEmpty();
    }

    [Fact]
    public void DetectMissedRuns_NullCronExpression_IsSkipped()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "Null Cron",
                IsEnabled = true,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = null
            }
        };

        var skipped = _scheduling.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));

        skipped.Should().BeEmpty();
    }

    [Fact]
    public void DetectMissedRuns_SkippedRunEntry_HasCorrectJobIdAndName()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["abc123"] = new JobDefinition
            {
                Id = "abc123",
                Name = "My Important Job",
                IsEnabled = true,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = "*/1 * * * *"
            }
        };

        var skipped = _scheduling.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));

        skipped.Should().NotBeEmpty();
        skipped.Should().AllSatisfy(entry =>
        {
            entry.JobId.Should().Be("abc123");
            entry.JobName.Should().Be("My Important Job");
        });
    }

    [Fact]
    public void DetectMissedRuns_SkippedRunEntry_ScheduledTimesBetweenShutdownAndNow()
    {
        var lastShutdown = DateTime.UtcNow.AddMinutes(-5);
        var beforeDetect = DateTime.UtcNow;

        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "Test Job",
                IsEnabled = true,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = "*/1 * * * *"
            }
        };

        var skipped = _scheduling.DetectMissedRuns(jobs, lastShutdown);

        skipped.Should().NotBeEmpty();
        skipped.Should().AllSatisfy(entry =>
        {
            entry.ScheduledTimeUtc.Should().BeAfter(lastShutdown);
            entry.ScheduledTimeUtc.Should().BeBefore(beforeDetect.AddSeconds(1));
        });
    }

    [Fact]
    public void DetectMissedRuns_CleanSlateSimulation_ReturnsEmpty()
    {
        // Simulating first install: caller uses DateTime.UtcNow as lastShutdown
        // With exclusive bounds, no occurrences between now and now
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "Every Minute",
                IsEnabled = true,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = "*/1 * * * *"
            }
        };

        var skipped = _scheduling.DetectMissedRuns(jobs, DateTime.UtcNow);

        skipped.Should().BeEmpty();
    }

    #endregion

    #region One-Time Completion Tests

    [Fact]
    public void MarkOneTimeCompleted_SetsIsEnabledFalseAndDisabledReason()
    {
        var job = new JobDefinition
        {
            IsEnabled = true,
            ScheduleType = ScheduleType.OneTime,
            OneTimeScheduleUtc = DateTime.UtcNow.AddHours(-1)
        };

        _scheduling.MarkOneTimeCompleted(job);

        job.IsEnabled.Should().BeFalse();
        job.DisabledReason.Should().Be("One-time schedule completed");
    }

    [Fact]
    public void MarkOneTimeCompleted_PreservesOneTimeScheduleUtc()
    {
        var scheduleTime = new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Utc);
        var job = new JobDefinition
        {
            IsEnabled = true,
            ScheduleType = ScheduleType.OneTime,
            OneTimeScheduleUtc = scheduleTime
        };

        _scheduling.MarkOneTimeCompleted(job);

        job.OneTimeScheduleUtc.Should().Be(scheduleTime);
    }

    [Fact]
    public void MarkOneTimeCompleted_PersistsThroughJobStorageSaveReload()
    {
        var credentialMock = new Mock<ICredentialProvider>();
        credentialMock.Setup(c => c.IsAvailable).Returns(true);

        var jobsFilePath = Path.Combine(_tempDir, "jobs.json");
        var storage = new JobStorageService(credentialMock.Object, jobsFilePath);
        storage.Load();

        var scheduleTime = new DateTime(2026, 3, 7, 14, 30, 0, DateTimeKind.Utc);
        var job = new JobDefinition
        {
            Name = "Persist Test Job",
            IsEnabled = true,
            ScheduleType = ScheduleType.OneTime,
            OneTimeScheduleUtc = scheduleTime,
            TargetName = "TestPreset"
        };

        // Mark completed and save
        _scheduling.MarkOneTimeCompleted(job);
        storage.Save(job);

        // Reload from disk
        var storage2 = new JobStorageService(credentialMock.Object, jobsFilePath);
        storage2.Load();

        var reloaded = storage2.Get(job.Id);

        reloaded.Should().NotBeNull();
        reloaded!.IsEnabled.Should().BeFalse();
        reloaded.DisabledReason.Should().Be("One-time schedule completed");
        reloaded.OneTimeScheduleUtc.Should().Be(scheduleTime);
    }

    #endregion

    #region LastAppShutdownUtc Persistence Tests

    [Fact]
    public void LastAppShutdownUtc_RoundTripsThroughConfigurationService()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var configService = new ConfigurationService(configPath);

        var shutdownTime = new DateTime(2026, 3, 7, 16, 0, 0, DateTimeKind.Utc);
        var config = configService.Load();
        config.LastAppShutdownUtc = shutdownTime;
        configService.Save(config);

        // Create a new ConfigurationService instance to force disk reload
        var configService2 = new ConfigurationService(configPath);
        var reloaded = configService2.Load();

        reloaded.LastAppShutdownUtc.Should().NotBeNull();
        reloaded.LastAppShutdownUtc!.Value.Should().Be(shutdownTime);
    }

    [Fact]
    public void LastAppShutdownUtc_NullOnFirstInstall()
    {
        var configPath = Path.Combine(_tempDir, "firstinstall_config.json");
        var configService = new ConfigurationService(configPath);

        // First load creates a default config (no LastAppShutdownUtc key in JSON)
        var config = configService.Load();

        config.LastAppShutdownUtc.Should().BeNull();
    }

    #endregion
}
