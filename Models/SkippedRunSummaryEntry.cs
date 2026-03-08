namespace SSH_Helper.Models
{
    /// <summary>
    /// Aggregated summary of recurring scheduler runs missed while the application was closed.
    /// One entry represents one job across one startup downtime window.
    /// </summary>
    public sealed class SkippedRunSummaryEntry
    {
        /// <summary>
        /// The ID of the job that missed one or more runs.
        /// </summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// The display name of the job when the summary was created.
        /// </summary>
        public string JobName { get; set; } = string.Empty;

        /// <summary>
        /// The number of recurring runs that were missed during downtime.
        /// </summary>
        public int MissedRunCount { get; set; }

        /// <summary>
        /// The earliest missed scheduled time in UTC for the downtime window.
        /// </summary>
        public DateTime FirstScheduledTimeUtc { get; set; }

        /// <summary>
        /// The latest missed scheduled time in UTC for the downtime window.
        /// </summary>
        public DateTime LastScheduledTimeUtc { get; set; }
    }
}
