namespace SSH_Helper.Models
{
    /// <summary>
    /// Full persisted payload for a single history run.
    /// </summary>
    public sealed class HistoryRunPayload
    {
        public string Id { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public List<HostHistoryEntry>? HostResults { get; set; }
        public ExecutionDetails? Details { get; set; }
    }
}
