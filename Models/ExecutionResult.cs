namespace SSH_Helper.Models
{
    /// <summary>
    /// Result of executing commands on a single host.
    /// </summary>
    public class ExecutionResult
    {
        public HostConnection Host { get; set; } = new();
        public string Output { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
