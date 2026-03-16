namespace SSH_Helper.Models
{
    /// <summary>
    /// Query filter for retrieving job run history entries.
    /// All properties are optional; null values are treated as "no filter".
    /// </summary>
    public sealed class JobRunFilter
    {
        /// <summary>
        /// Filter by success status. Null = all, true = success only, false = failed only.
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// Only include runs that started at or after this UTC time.
        /// </summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>
        /// Only include runs that started at or before this UTC time.
        /// </summary>
        public DateTime? ToUtc { get; set; }

        /// <summary>
        /// Maximum number of results to return. Default: 50.
        /// </summary>
        public int MaxResults { get; set; } = 50;
    }
}
