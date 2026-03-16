namespace SSH_Helper.Models
{
    /// <summary>
    /// Retention and truncation settings applied when persisting job history.
    /// </summary>
    public sealed class JobHistoryRetentionOptions
    {
        /// <summary>
        /// Default maximum number of runs retained per job.
        /// </summary>
        public const int DefaultMaxRuns = 50;

        /// <summary>
        /// Default maximum age in days for retained runs.
        /// </summary>
        public const int DefaultRetentionDays = 30;

        /// <summary>
        /// Default maximum number of characters retained per host output.
        /// </summary>
        public const int DefaultMaxOutputChars = 1_048_576;

        /// <summary>
        /// Maximum number of runs retained per job.
        /// </summary>
        public int MaxRuns { get; set; } = DefaultMaxRuns;

        /// <summary>
        /// Maximum age in days for retained runs.
        /// </summary>
        public int RetentionDays { get; set; } = DefaultRetentionDays;

        /// <summary>
        /// Maximum number of characters retained per host output.
        /// </summary>
        public int MaxOutputChars { get; set; } = DefaultMaxOutputChars;
    }
}
