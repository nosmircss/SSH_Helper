namespace SSH_Helper.Models
{
    /// <summary>
    /// Root document for persisted history index metadata.
    /// </summary>
    public sealed class HistoryIndexDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public List<HistoryIndexEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// Lightweight metadata for a persisted run payload.
    /// </summary>
    public sealed class HistoryIndexEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public bool HasHostResults { get; set; }
        public bool HasDetails { get; set; }
        public string RunFileName { get; set; } = string.Empty;
    }
}
