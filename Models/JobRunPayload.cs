namespace SSH_Helper.Models
{
    /// <summary>
    /// Full run payload containing metadata and per-host output for a single job execution.
    /// Stored as an individual JSON file and loaded on demand when viewing run details.
    /// </summary>
    public sealed class JobRunPayload
    {
        /// <summary>
        /// Unique identifier for this run (matches <see cref="JobRunRecord.Id"/>).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The ID of the job that was executed.
        /// </summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// The display name of the job at execution time.
        /// </summary>
        public string JobName { get; set; } = string.Empty;

        /// <summary>
        /// When execution started (UTC).
        /// </summary>
        public DateTime StartedUtc { get; set; }

        /// <summary>
        /// When execution completed (UTC).
        /// </summary>
        public DateTime CompletedUtc { get; set; }

        /// <summary>
        /// Whether the overall job execution succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Whether the overall job execution ended due to cancellation.
        /// </summary>
        public bool WasCancelled { get; set; }

        /// <summary>
        /// Number of hosts that completed successfully.
        /// </summary>
        public int HostsSucceeded { get; set; }

        /// <summary>
        /// Number of hosts that failed during execution.
        /// </summary>
        public int HostsFailed { get; set; }

        /// <summary>
        /// Optional error message if the job failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of consecutive identical failures collapsed into this payload.
        /// Zero indicates legacy data that predates failure-streak tracking.
        /// </summary>
        public int ConsecutiveFailureCount { get; set; }

        /// <summary>
        /// Whether this payload represents a skipped run recorded during scheduler startup.
        /// </summary>
        public bool WasSkipped { get; set; }

        /// <summary>
        /// Number of skipped recurring runs summarized by this payload.
        /// Zero indicates a legacy single skipped record without summary metadata.
        /// </summary>
        public int SkippedRunCount { get; set; }

        /// <summary>
        /// Earliest missed scheduled time covered by this skipped summary, in UTC.
        /// </summary>
        public DateTime? SkippedWindowStartUtc { get; set; }

        /// <summary>
        /// Latest missed scheduled time covered by this skipped summary, in UTC.
        /// </summary>
        public DateTime? SkippedWindowEndUtc { get; set; }

        /// <summary>
        /// Per-host execution outputs with individual success/failure status.
        /// </summary>
        public List<JobHostOutput> HostOutputs { get; set; } = new();
    }
}
