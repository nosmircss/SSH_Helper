namespace SSH_Helper.Models
{
    /// <summary>
    /// Lightweight index entry for a single job run in the per-job history index.
    /// Contains only metadata needed for listing; full output is in the payload file.
    /// </summary>
    public sealed class JobRunRecord
    {
        /// <summary>
        /// Unique identifier for this run (GUID hex, no dashes).
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
        /// File name of the corresponding payload file (e.g., "abc123.json").
        /// </summary>
        public string RunFileName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Root document for the per-job run history index file.
    /// Wraps the list of run records with a schema version for forward compatibility.
    /// </summary>
    public sealed class JobRunIndexDocument
    {
        /// <summary>
        /// Schema version for forward compatibility.
        /// </summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// Ordered list of run records, newest first.
        /// </summary>
        public List<JobRunRecord> Entries { get; set; } = new();
    }
}
