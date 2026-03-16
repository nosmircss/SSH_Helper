namespace SSH_Helper.Models
{
    /// <summary>
    /// Records a job run that was missed while the application was closed.
    /// Created during startup missed-run detection; never auto-executed.
    /// </summary>
    public class SkippedRunEntry
    {
        /// <summary>
        /// The ID of the job that missed this run.
        /// </summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// The display name of the job at the time the miss was detected.
        /// </summary>
        public string JobName { get; set; } = string.Empty;

        /// <summary>
        /// The UTC time when the job should have run according to its cron schedule.
        /// </summary>
        public DateTime ScheduledTimeUtc { get; set; }

        /// <summary>
        /// The UTC time when this missed run was detected (defaults to now).
        /// </summary>
        public DateTime DetectedUtc { get; set; } = DateTime.UtcNow;
    }
}
