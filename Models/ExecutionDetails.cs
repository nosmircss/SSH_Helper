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
    }

    /// <summary>
    /// Per-host context captured at execution time.
    /// </summary>
    public class HostExecutionContext
    {
        public string HostAddress { get; set; } = string.Empty;
        public bool Success { get; set; }
        public DateTime TimestampUtc { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
