using SSH_Helper.Services.Scripting.Models;

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
        public bool WasCancelled { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<InteractiveTerminalSessionDetails> InteractiveSessions { get; set; } = new();
        public string? HistoryLabel { get; set; }
        public bool HistoryLabelReplacesAddress { get; set; }
        public bool HistoryLabelTouched { get; set; }
        public List<HistoryLabelOperation> HistoryLabelOperations { get; set; } = new();
    }
}
