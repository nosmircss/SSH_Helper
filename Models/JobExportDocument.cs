namespace SSH_Helper.Models
{
    /// <summary>
    /// Wrapper document for job export files (.sshjobs).
    /// Contains version information and a list of exported job definitions.
    /// </summary>
    public sealed class JobExportDocument
    {
        /// <summary>
        /// Schema version for forward compatibility.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// When the export was created (UTC).
        /// </summary>
        public DateTime ExportedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The exported job definitions.
        /// </summary>
        public List<JobDefinition> Jobs { get; set; } = new();
    }
}
