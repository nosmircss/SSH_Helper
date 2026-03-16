namespace SSH_Helper.Models
{
    /// <summary>
    /// Captures execution metadata associated with a history entry.
    /// </summary>
    public class ExecutionDetails
    {
        public string PresetName { get; set; } = string.Empty;
        public string Commands { get; set; } = string.Empty;
        public string PresetType { get; set; } = string.Empty;
        public bool WasCancelled { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public string EnvironmentName { get; set; } = EnvironmentConfig.DefaultName;
        public string Username { get; set; } = string.Empty;
        public int CommandTimeoutSeconds { get; set; }
        public int ConnectionTimeoutSeconds { get; set; }
        public bool UseConnectionPooling { get; set; }
        public string RunMode { get; set; } = string.Empty;
        public bool IsFolderExecution { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public List<string> ExecutedPresetNames { get; set; } = new();
        public List<HostExecutionContext> Hosts { get; set; } = new();
        public List<InteractiveTerminalSessionDetails> InteractiveSessions { get; set; } = new();
    }

    /// <summary>
    /// Per-host context captured at execution time.
    /// </summary>
    public class HostExecutionContext
    {
        public string HostAddress { get; set; } = string.Empty;
        public bool Success { get; set; }
        public bool WasCancelled { get; set; }
        public DateTime TimestampUtc { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Captures a single interactive terminal session launched during script execution.
    /// </summary>
    public class InteractiveTerminalSessionDetails
    {
        public int SessionNumber { get; set; }
        public string HostAddress { get; set; } = string.Empty;
        public string SessionMode { get; set; } = string.Empty;
        public string EmulationMode { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; }
        public DateTime EndedAtUtc { get; set; }
        public string CloseReason { get; set; } = string.Empty;
        public bool Completed { get; set; }
        public string Transcript { get; set; } = string.Empty;
    }
}
