using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Utilities;
using System.Runtime.CompilerServices;
using System.Reflection;
using Xunit;

namespace SSH_Helper.Tests.UI
{
    public class SchedulerNotificationTests
    {
        #region FormatCompletion

        [Fact]
        public void FormatCompletion_Success_Scheduled_FormatsCorrectly()
        {
            var timestamp = new DateTime(2026, 3, 7, 14, 30, 0, DateTimeKind.Utc);
            var duration = TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(15);

            var result = SchedulerNotificationFormatter.FormatCompletion(
                "BackupJob", isRunNow: false, success: true,
                hostsSucceeded: 5, hostsFailed: 0, duration, timestamp);

            result.Should().Be("[14:30:00] [Scheduled: BackupJob] Completed -- 5/5 hosts succeeded (02:15)");
        }

        [Fact]
        public void FormatCompletion_Failure_RunNow_FormatsCorrectly()
        {
            var timestamp = new DateTime(2026, 3, 7, 9, 5, 30, DateTimeKind.Utc);
            var duration = TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(5);

            var result = SchedulerNotificationFormatter.FormatCompletion(
                "DeployJob", isRunNow: true, success: false,
                hostsSucceeded: 2, hostsFailed: 3, duration, timestamp);

            result.Should().Be("[09:05:30] [Run Now: DeployJob] Failed -- 3/5 hosts failed (01:05)");
        }

        #endregion

        #region FormatStateChange

        [Fact]
        public void FormatStateChange_Skipped_FormatsWithMessage()
        {
            var timestamp = new DateTime(2026, 3, 7, 10, 0, 0, DateTimeKind.Utc);

            var result = SchedulerNotificationFormatter.FormatStateChange(
                "MyJob", JobExecutionState.Skipped, isRunNow: false,
                "Job is disabled", timestamp);

            result.Should().Be("[10:00:00] [Skipped: MyJob] Job is disabled");
        }

        [Fact]
        public void FormatStateChange_Started_FormatsWithoutMessage()
        {
            var timestamp = new DateTime(2026, 3, 7, 12, 15, 45, DateTimeKind.Utc);

            var result = SchedulerNotificationFormatter.FormatStateChange(
                "CheckJob", JobExecutionState.Started, isRunNow: false,
                null, timestamp);

            result.Should().Be("[12:15:45] [Scheduled: CheckJob] Started");
        }

        [Fact]
        public void FormatStateChange_Queued_FormatsCorrectly()
        {
            var timestamp = new DateTime(2026, 3, 7, 8, 0, 0, DateTimeKind.Utc);

            var result = SchedulerNotificationFormatter.FormatStateChange(
                "QueuedJob", JobExecutionState.Queued, isRunNow: true,
                null, timestamp);

            result.Should().Be("[08:00:00] [Run Now: QueuedJob] Queued");
        }

        [Fact]
        public void FormatStateChange_Cancelled_FormatsCorrectly()
        {
            var timestamp = new DateTime(2026, 3, 7, 8, 30, 0, DateTimeKind.Utc);

            var result = SchedulerNotificationFormatter.FormatStateChange(
                "CancelledJob", JobExecutionState.Cancelled, isRunNow: false,
                null, timestamp);

            result.Should().Be("[08:30:00] [Scheduled: CancelledJob] Cancelled");
        }

        #endregion

        #region FormatDuration

        [Fact]
        public void FormatDuration_SubHour_FormatsAsMinutesSeconds()
        {
            var duration = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(30);
            SchedulerNotificationFormatter.FormatDuration(duration).Should().Be("05:30");
        }

        [Fact]
        public void FormatDuration_OverHour_FormatsAsHoursMinutesSeconds()
        {
            var duration = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(23) + TimeSpan.FromSeconds(45);
            SchedulerNotificationFormatter.FormatDuration(duration).Should().Be("01:23:45");
        }

        [Fact]
        public void FormatDuration_ZeroDuration_FormatsAsZeroMinutesSeconds()
        {
            SchedulerNotificationFormatter.FormatDuration(TimeSpan.Zero).Should().Be("00:00");
        }

        #endregion

        #region FormatStatusBar

        [Fact]
        public void DeferredSchedulerBootstrapGuard_AllowsOnlyFirstRun()
        {
            var form = (global::SSH_Helper.Form1)RuntimeHelpers.GetUninitializedObject(typeof(global::SSH_Helper.Form1));
            var method = typeof(global::SSH_Helper.Form1).GetMethod(
                "TryBeginDeferredSchedulerBootstrap",
                BindingFlags.Instance | BindingFlags.NonPublic);

            method.Should().NotBeNull();
            method!.Invoke(form, null).Should().Be(true);
            method.Invoke(form, null).Should().Be(false);
        }

        [Fact]
        public void ShouldShowStatusBar_ZeroActive_ReturnsFalse()
        {
            SchedulerNotificationFormatter.ShouldShowStatusBar(0).Should().BeFalse();
        }

        [Fact]
        public void ShouldShowStatusBar_PositiveActive_ReturnsTrue()
        {
            SchedulerNotificationFormatter.ShouldShowStatusBar(1).Should().BeTrue();
        }

        [Fact]
        public void FormatStatusBar_ZeroActive_ReturnsSimple()
        {
            var result = SchedulerNotificationFormatter.FormatStatusBar(0, null, null);
            result.Should().Be("Scheduler: 0 active");
        }

        [Fact]
        public void FormatStatusBar_ActiveWithNextJob_IncludesNextInfo()
        {
            var result = SchedulerNotificationFormatter.FormatStatusBar(
                3, "Backups", TimeSpan.FromMinutes(135));

            result.Should().Be("Scheduler: 3 active -- Next: Backups in 2h 15m");
        }

        [Fact]
        public void FormatStatusBar_ActiveWithoutNextJob_OmitsNextInfo()
        {
            var result = SchedulerNotificationFormatter.FormatStatusBar(2, null, null);
            result.Should().Be("Scheduler: 2 active");
        }

        #endregion

        #region FormatTimeRemaining

        [Fact]
        public void FormatTimeRemaining_HoursAndMinutes_FormatsCorrectly()
        {
            var remaining = TimeSpan.FromHours(2) + TimeSpan.FromMinutes(15);
            SchedulerNotificationFormatter.FormatTimeRemaining(remaining).Should().Be("2h 15m");
        }

        [Fact]
        public void FormatTimeRemaining_MinutesOnly_FormatsCorrectly()
        {
            var remaining = TimeSpan.FromMinutes(45);
            SchedulerNotificationFormatter.FormatTimeRemaining(remaining).Should().Be("45m");
        }

        [Fact]
        public void FormatTimeRemaining_SubMinute_FormatsAsLessThanOneMinute()
        {
            var remaining = TimeSpan.FromSeconds(30);
            SchedulerNotificationFormatter.FormatTimeRemaining(remaining).Should().Be("< 1m");
        }

        [Fact]
        public void FormatTimeRemaining_ExactlyOneHour_FormatsCorrectly()
        {
            var remaining = TimeSpan.FromHours(1);
            SchedulerNotificationFormatter.FormatTimeRemaining(remaining).Should().Be("1h 0m");
        }

        #endregion
    }
}
