using Cronos;
using CronExpressionDescriptor;
using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Pure logic service for cron scheduling operations.
    /// Provides validation, description, next-run calculation, missed-run detection,
    /// and one-time job completion. Does NOT run a timer or execute jobs (that is Phase 3).
    /// </summary>
    public sealed class SchedulingService
    {
        /// <summary>
        /// Validates a 5-field cron expression.
        /// Returns null if valid, error message if invalid.
        /// </summary>
        /// <param name="expression">The cron expression to validate.</param>
        /// <returns>Null if valid; error message string if invalid.</returns>
        public string? ValidateCronExpression(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return "Cron expression cannot be empty.";

            // 5-field only -- do NOT pass CronFormat.IncludeSeconds
            if (!CronExpression.TryParse(expression.Trim(), out _))
                return "Invalid cron expression format. Expected 5 fields: minute hour day-of-month month day-of-week.";

            return null;
        }

        /// <summary>
        /// Returns a human-readable description of a cron expression.
        /// Returns null if the expression is invalid.
        /// </summary>
        /// <param name="expression">The cron expression to describe.</param>
        /// <returns>Human-readable description, or null if invalid.</returns>
        public string? GetDescription(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return null;

            if (!CronExpression.TryParse(expression.Trim(), out _))
                return null;

            return ExpressionDescriptor.GetDescription(expression.Trim(), new Options
            {
                Use24HourTimeFormat = false,
                ThrowExceptionOnParseError = false
            });
        }

        /// <summary>
        /// Gets the next occurrence of a cron expression in local time.
        /// Returns null if the expression is invalid or has no next occurrence.
        /// </summary>
        /// <param name="cronExpression">The cron expression.</param>
        /// <returns>Next occurrence as local DateTime, or null.</returns>
        public DateTime? GetNextRunLocal(string? cronExpression)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
                return null;

            if (!CronExpression.TryParse(cronExpression.Trim(), out var cron))
                return null;

            var next = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
            return next?.LocalDateTime;
        }

        /// <summary>
        /// Gets the next occurrence of a cron expression in UTC.
        /// Returns null if the expression is invalid or has no next occurrence.
        /// </summary>
        /// <param name="cronExpression">The cron expression.</param>
        /// <returns>Next occurrence as UTC DateTime, or null.</returns>
        public DateTime? GetNextRunUtc(string? cronExpression)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
                return null;

            if (!CronExpression.TryParse(cronExpression.Trim(), out var cron))
                return null;

            return cron.GetNextOccurrence(DateTime.UtcNow);
        }

        /// <summary>
        /// Returns all cron occurrences that were missed between lastCheckedUtc and now.
        /// Uses exclusive bounds on both ends.
        /// </summary>
        /// <param name="cronExpression">The cron expression.</param>
        /// <param name="lastCheckedUtc">The UTC time of the last check (exclusive).</param>
        /// <returns>List of missed occurrence times in UTC, or empty list if invalid.</returns>
        public IReadOnlyList<DateTime> GetMissedOccurrences(string? cronExpression, DateTime lastCheckedUtc)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
                return Array.Empty<DateTime>();

            if (!CronExpression.TryParse(cronExpression.Trim(), out var cron))
                return Array.Empty<DateTime>();

            return cron.GetOccurrences(lastCheckedUtc, DateTime.UtcNow, fromInclusive: false, toInclusive: false)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Detects missed runs for all enabled recurring jobs since the last application shutdown.
        /// Returns a list of SkippedRunEntry objects for each missed occurrence.
        /// </summary>
        /// <param name="jobs">All known job definitions.</param>
        /// <param name="lastAppShutdownUtc">The UTC time the application was last shut down.</param>
        /// <returns>List of skipped run entries.</returns>
        public IReadOnlyList<SkippedRunEntry> DetectMissedRuns(
            IReadOnlyDictionary<string, JobDefinition> jobs,
            DateTime lastAppShutdownUtc)
        {
            var skipped = new List<SkippedRunEntry>();

            foreach (var job in jobs.Values)
            {
                if (!job.IsEnabled)
                    continue;

                if (job.ScheduleType != ScheduleType.Recurring)
                    continue;

                if (string.IsNullOrEmpty(job.CronExpression))
                    continue;

                var missed = GetMissedOccurrences(job.CronExpression, lastAppShutdownUtc);
                foreach (var time in missed)
                {
                    skipped.Add(new SkippedRunEntry
                    {
                        JobId = job.Id,
                        JobName = job.Name,
                        ScheduledTimeUtc = time
                    });
                }
            }

            return skipped.AsReadOnly();
        }

        /// <summary>
        /// Marks a one-time job as completed by disabling it with a reason.
        /// Preserves the OneTimeScheduleUtc value as a visible record.
        /// </summary>
        /// <param name="job">The job to mark as completed.</param>
        public void MarkOneTimeCompleted(JobDefinition job)
        {
            job.IsEnabled = false;
            job.DisabledReason = "One-time schedule completed";
        }
    }
}
