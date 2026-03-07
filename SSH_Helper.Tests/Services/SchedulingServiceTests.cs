using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

/// <summary>
/// Tests for SchedulingService covering cron validation, description,
/// next-run calculation, missed-run detection, and one-time completion.
/// </summary>
public class SchedulingServiceTests
{
    private readonly SchedulingService _sut = new();

    #region ValidateCronExpression Tests

    [Fact]
    public void ValidateCronExpression_ValidDailyAt3AM_ReturnsNull()
    {
        _sut.ValidateCronExpression("0 3 * * *").Should().BeNull();
    }

    [Fact]
    public void ValidateCronExpression_ValidEvery5Minutes_ReturnsNull()
    {
        _sut.ValidateCronExpression("*/5 * * * *").Should().BeNull();
    }

    [Fact]
    public void ValidateCronExpression_InvalidExpression_ReturnsError()
    {
        var result = _sut.ValidateCronExpression("bad");
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateCronExpression_SixFieldExpression_ReturnsError()
    {
        // 6-field expressions (with seconds) should be rejected
        var result = _sut.ValidateCronExpression("* * * * * *");
        result.Should().NotBeNull();
    }

    [Fact]
    public void ValidateCronExpression_EmptyString_ReturnsError()
    {
        var result = _sut.ValidateCronExpression("");
        result.Should().NotBeNull();
    }

    #endregion

    #region GetDescription Tests

    [Fact]
    public void GetDescription_DailyAt3AM_ReturnsStringContaining3()
    {
        var desc = _sut.GetDescription("0 3 * * *");
        desc.Should().NotBeNull();
        // Should contain "03:00" or "3:00" depending on locale
        desc.Should().MatchRegex("3:00");
    }

    [Fact]
    public void GetDescription_InvalidExpression_ReturnsNull()
    {
        _sut.GetDescription("invalid").Should().BeNull();
    }

    [Fact]
    public void GetDescription_Every15Minutes_ReturnsStringContaining15()
    {
        var desc = _sut.GetDescription("*/15 * * * *");
        desc.Should().NotBeNull();
        desc.Should().Contain("15");
    }

    [Fact]
    public void GetDescription_EveryMinute_ReturnsNonEmptyString()
    {
        var desc = _sut.GetDescription("* * * * *");
        desc.Should().NotBeNull();
        desc.Should().NotBeEmpty();
    }

    #endregion

    #region GetNextRunLocal Tests

    [Fact]
    public void GetNextRunLocal_ValidDailyExpression_ReturnsFutureDateTime()
    {
        var next = _sut.GetNextRunLocal("0 3 * * *");
        next.Should().NotBeNull();
        // The next run should be in the future from the local perspective
        next!.Value.Should().BeAfter(DateTime.Now.AddMinutes(-1));
    }

    [Fact]
    public void GetNextRunLocal_InvalidExpression_ReturnsNull()
    {
        _sut.GetNextRunLocal("invalid").Should().BeNull();
    }

    [Fact]
    public void GetNextRunLocal_EveryMinute_ReturnsNearFuture()
    {
        var next = _sut.GetNextRunLocal("* * * * *");
        next.Should().NotBeNull();
        // Every-minute should fire within the next ~2 minutes
        next!.Value.Should().BeBefore(DateTime.Now.AddMinutes(2));
    }

    #endregion

    #region GetNextRunUtc Tests

    [Fact]
    public void GetNextRunUtc_ValidExpression_ReturnsUtcKind()
    {
        var next = _sut.GetNextRunUtc("0 3 * * *");
        next.Should().NotBeNull();
        next!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void GetNextRunUtc_InvalidExpression_ReturnsNull()
    {
        _sut.GetNextRunUtc("invalid").Should().BeNull();
    }

    [Fact]
    public void GetNextRunUtc_EveryMinute_ReturnsFutureUtc()
    {
        var next = _sut.GetNextRunUtc("* * * * *");
        next.Should().NotBeNull();
        next!.Value.Should().BeAfter(DateTime.UtcNow.AddSeconds(-1));
    }

    #endregion

    #region GetMissedOccurrences Tests

    [Fact]
    public void GetMissedOccurrences_EveryMinute5MinAgo_ReturnsApproximately4Entries()
    {
        // Every minute, 5 minutes ago: exclusive bounds means ~4 occurrences
        var missed = _sut.GetMissedOccurrences("*/1 * * * *", DateTime.UtcNow.AddMinutes(-5));
        missed.Should().NotBeEmpty();
        // With exclusive bounds on a 5-minute window at per-minute frequency:
        // Should get approximately 4 entries (minute -4, -3, -2, -1)
        missed.Count.Should().BeInRange(3, 5);
    }

    [Fact]
    public void GetMissedOccurrences_InvalidExpression_ReturnsEmptyList()
    {
        var missed = _sut.GetMissedOccurrences("invalid", DateTime.UtcNow.AddMinutes(-5));
        missed.Should().BeEmpty();
    }

    [Fact]
    public void GetMissedOccurrences_YearlyCronIn5MinWindow_ReturnsEmptyList()
    {
        // Yearly cron (Jan 1 midnight) in a 5-minute window: no missed runs
        var missed = _sut.GetMissedOccurrences("0 0 1 1 *", DateTime.UtcNow.AddMinutes(-5));
        missed.Should().BeEmpty();
    }

    [Fact]
    public void GetMissedOccurrences_AllResultsAreUtc()
    {
        var missed = _sut.GetMissedOccurrences("*/1 * * * *", DateTime.UtcNow.AddMinutes(-5));
        foreach (var dt in missed)
        {
            dt.Kind.Should().Be(DateTimeKind.Utc);
        }
    }

    #endregion

    #region DetectMissedRuns Tests

    [Fact]
    public void DetectMissedRuns_EnabledRecurringJob_ReturnsSkippedEntries()
    {
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

        var skipped = _sut.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));
        skipped.Should().NotBeEmpty();
        skipped[0].JobId.Should().Be("job1");
        skipped[0].JobName.Should().Be("Test Job");
    }

    [Fact]
    public void DetectMissedRuns_DisabledJob_ReturnsEmptyList()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "Disabled Job",
                IsEnabled = false,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = "*/1 * * * *"
            }
        };

        var skipped = _sut.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));
        skipped.Should().BeEmpty();
    }

    [Fact]
    public void DetectMissedRuns_OneTimeScheduleType_ReturnsEmptyList()
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

        var skipped = _sut.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));
        skipped.Should().BeEmpty();
    }

    [Fact]
    public void DetectMissedRuns_NullCronExpression_ReturnsEmptyList()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "No Cron Job",
                IsEnabled = true,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = null
            }
        };

        var skipped = _sut.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));
        skipped.Should().BeEmpty();
    }

    [Fact]
    public void DetectMissedRuns_EmptyCronExpression_ReturnsEmptyList()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "Empty Cron Job",
                IsEnabled = true,
                ScheduleType = ScheduleType.Recurring,
                CronExpression = ""
            }
        };

        var skipped = _sut.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));
        skipped.Should().BeEmpty();
    }

    [Fact]
    public void DetectMissedRuns_NoneScheduleType_ReturnsEmptyList()
    {
        var jobs = new Dictionary<string, JobDefinition>
        {
            ["job1"] = new JobDefinition
            {
                Id = "job1",
                Name = "None Type Job",
                IsEnabled = true,
                ScheduleType = ScheduleType.None,
                CronExpression = "*/1 * * * *"
            }
        };

        var skipped = _sut.DetectMissedRuns(jobs, DateTime.UtcNow.AddMinutes(-5));
        skipped.Should().BeEmpty();
    }

    #endregion

    #region MarkOneTimeCompleted Tests

    [Fact]
    public void MarkOneTimeCompleted_SetsIsEnabledToFalse()
    {
        var job = new JobDefinition
        {
            IsEnabled = true,
            ScheduleType = ScheduleType.OneTime,
            OneTimeScheduleUtc = DateTime.UtcNow.AddHours(-1)
        };

        _sut.MarkOneTimeCompleted(job);

        job.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void MarkOneTimeCompleted_SetsDisabledReason()
    {
        var job = new JobDefinition
        {
            IsEnabled = true,
            ScheduleType = ScheduleType.OneTime,
            OneTimeScheduleUtc = DateTime.UtcNow.AddHours(-1)
        };

        _sut.MarkOneTimeCompleted(job);

        job.DisabledReason.Should().Be("One-time schedule completed");
    }

    [Fact]
    public void MarkOneTimeCompleted_PreservesOneTimeScheduleUtc()
    {
        var scheduleTime = DateTime.UtcNow.AddHours(-1);
        var job = new JobDefinition
        {
            IsEnabled = true,
            ScheduleType = ScheduleType.OneTime,
            OneTimeScheduleUtc = scheduleTime
        };

        _sut.MarkOneTimeCompleted(job);

        job.OneTimeScheduleUtc.Should().Be(scheduleTime);
    }

    #endregion
}
