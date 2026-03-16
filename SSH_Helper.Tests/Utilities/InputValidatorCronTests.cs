using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

/// <summary>
/// Tests for cron expression and future-date validation extensions in InputValidator.
/// </summary>
public class InputValidatorCronTests
{
    #region ValidateCronExpression Tests

    [Fact]
    public void ValidateCronExpression_Null_ReturnsEmptyError()
    {
        var result = InputValidator.ValidateCronExpression(null);
        result.Should().Be("Cron expression cannot be empty.");
    }

    [Fact]
    public void ValidateCronExpression_EmptyString_ReturnsEmptyError()
    {
        var result = InputValidator.ValidateCronExpression("");
        result.Should().Be("Cron expression cannot be empty.");
    }

    [Fact]
    public void ValidateCronExpression_WhitespaceOnly_ReturnsEmptyError()
    {
        var result = InputValidator.ValidateCronExpression("   ");
        result.Should().Be("Cron expression cannot be empty.");
    }

    [Theory]
    [InlineData("0 3 * * *")]       // Daily at 3 AM
    [InlineData("*/5 * * * *")]     // Every 5 minutes
    [InlineData("0 0 1 * *")]       // Monthly on 1st
    [InlineData("0 9 * * 1-5")]     // Weekdays at 9 AM
    [InlineData("*/15 * * * *")]    // Every 15 minutes
    public void ValidateCronExpression_ValidExpressions_ReturnsNull(string expression)
    {
        var result = InputValidator.ValidateCronExpression(expression);
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateCronExpression_InvalidExpression_ReturnsErrorContainingInvalid()
    {
        var result = InputValidator.ValidateCronExpression("invalid");
        result.Should().NotBeNull();
        result.Should().Contain("Invalid");
    }

    [Fact]
    public void ValidateCronExpression_SixFieldExpression_ReturnsError()
    {
        // 6-field expressions (with seconds) should be rejected -- 5-field only
        var result = InputValidator.ValidateCronExpression("* * * * * *");
        result.Should().NotBeNull();
        result.Should().Contain("Invalid");
    }

    #endregion

    #region IsFutureDate Tests

    [Fact]
    public void IsFutureDate_FutureDate_ReturnsTrue()
    {
        var futureDate = DateTime.UtcNow.AddHours(1);
        InputValidator.IsFutureDate(futureDate).Should().BeTrue();
    }

    [Fact]
    public void IsFutureDate_PastDate_ReturnsFalse()
    {
        var pastDate = DateTime.UtcNow.AddHours(-1);
        InputValidator.IsFutureDate(pastDate).Should().BeFalse();
    }

    [Fact]
    public void IsFutureDate_FarFuture_ReturnsTrue()
    {
        var farFuture = DateTime.UtcNow.AddDays(365);
        InputValidator.IsFutureDate(farFuture).Should().BeTrue();
    }

    #endregion

    #region ScheduleType Enum Tests

    [Fact]
    public void ScheduleType_HasExpectedValues()
    {
        ((int)SSH_Helper.Models.ScheduleType.None).Should().Be(0);
        ((int)SSH_Helper.Models.ScheduleType.Recurring).Should().Be(1);
        ((int)SSH_Helper.Models.ScheduleType.OneTime).Should().Be(2);
    }

    [Fact]
    public void JobDefinition_ScheduleType_DefaultsToNone()
    {
        var job = new SSH_Helper.Models.JobDefinition();
        job.ScheduleType.Should().Be(SSH_Helper.Models.ScheduleType.None);
    }

    #endregion

    #region SkippedRunEntry Tests

    [Fact]
    public void SkippedRunEntry_HasExpectedProperties()
    {
        var entry = new SSH_Helper.Models.SkippedRunEntry
        {
            JobId = "abc123",
            JobName = "Test Job",
            ScheduledTimeUtc = new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Utc)
        };

        entry.JobId.Should().Be("abc123");
        entry.JobName.Should().Be("Test Job");
        entry.ScheduledTimeUtc.Should().Be(new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Utc));
        entry.DetectedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion
}
