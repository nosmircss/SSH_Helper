using SSH_Helper.Models;

namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Pure formatting logic for scheduler notifications and status bar text.
    /// Produces human-readable strings with prefixes: [Scheduled:], [Run Now:], [Skipped:].
    /// All methods are static and side-effect-free for testability.
    /// </summary>
    internal static class SchedulerNotificationFormatter
    {
        /// <summary>
        /// Formats a job completion notification with success/failure details and host counts.
        /// Format: [HH:mm:ss] [Prefix: JobName] Status -- N/Total hosts succeeded/failed (duration)
        /// </summary>
        internal static string FormatCompletion(
            string jobName,
            bool isRunNow,
            bool success,
            int hostsSucceeded,
            int hostsFailed,
            TimeSpan duration,
            DateTime timestamp)
        {
            var prefix = isRunNow ? "Run Now" : "Scheduled";
            var time = timestamp.ToString("HH:mm:ss");
            var dur = FormatDuration(duration);
            var totalHosts = hostsSucceeded + hostsFailed;

            if (success)
            {
                return $"[{time}] [{prefix}: {jobName}] Completed -- {hostsSucceeded}/{totalHosts} hosts succeeded ({dur})";
            }
            else
            {
                return $"[{time}] [{prefix}: {jobName}] Failed -- {hostsFailed}/{totalHosts} hosts failed ({dur})";
            }
        }

        /// <summary>
        /// Formats a job state change notification.
        /// Returns null for states that don't produce user-visible notifications.
        /// Format: [HH:mm:ss] [Prefix: JobName] State [message]
        /// </summary>
        internal static string? FormatStateChange(
            string jobName,
            JobExecutionState state,
            bool isRunNow,
            string? message,
            DateTime timestamp)
        {
            var prefix = isRunNow ? "Run Now" : "Scheduled";
            var time = timestamp.ToString("HH:mm:ss");

            return state switch
            {
                JobExecutionState.Started => $"[{time}] [{prefix}: {jobName}] Started",
                JobExecutionState.Queued => $"[{time}] [{prefix}: {jobName}] Queued",
                JobExecutionState.Skipped => $"[{time}] [Skipped: {jobName}] {message ?? "Skipped"}",
                JobExecutionState.Cancelled => $"[{time}] [{prefix}: {jobName}] Cancelled",
                // Completed and Failed are handled by FormatCompletion via the JobCompleted event
                _ => null,
            };
        }

        /// <summary>
        /// Returns whether the scheduler status bar segment should be shown.
        /// </summary>
        internal static bool ShouldShowStatusBar(int activeJobCount) => activeJobCount > 0;

        /// <summary>
        /// Formats the status bar text showing active job count and next-run countdown.
        /// </summary>
        internal static string FormatStatusBar(int activeJobCount, string? nextJobName, TimeSpan? timeUntilNext)
        {
            var countText = $"Scheduler: {activeJobCount} active";

            if (nextJobName != null && timeUntilNext.HasValue)
            {
                var remaining = FormatTimeRemaining(timeUntilNext.Value);
                return $"{countText} -- Next: {nextJobName} in {remaining}";
            }

            return countText;
        }

        /// <summary>
        /// Formats a TimeSpan as a duration string in MM:SS or HH:MM:SS format.
        /// </summary>
        internal static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            }

            return $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
        }

        /// <summary>
        /// Formats a TimeSpan as a countdown string for the status bar.
        /// </summary>
        internal static string FormatTimeRemaining(TimeSpan remaining)
        {
            if (remaining.TotalSeconds < 60)
                return "< 1m";
            if (remaining.TotalMinutes < 60)
                return $"{(int)remaining.TotalMinutes}m";
            if (remaining.TotalHours < 24)
                return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";

            return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
        }
    }
}
