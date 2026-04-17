namespace SSH_Helper.Models
{
    /// <summary>
    /// Per-host execution output captured for job history recording.
    /// Created from <see cref="ExecutionResult"/> during the JobCompleted event handoff.
    /// </summary>
    public sealed class JobHostOutput
    {
        /// <summary>
        /// The host address (IP or hostname) that was targeted.
        /// </summary>
        public string HostAddress { get; set; } = string.Empty;

        /// <summary>
        /// The raw command output captured from this host.
        /// </summary>
        public string Output { get; set; } = string.Empty;

        /// <summary>
        /// Whether command execution on this host succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Whether execution on this host was cancelled by user request.
        /// </summary>
        public bool WasCancelled { get; set; }

        /// <summary>
        /// Optional error message if execution on this host failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Optional label attached via the sethistorylabel script command.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// When true, display should show only Label (hide HostAddress).
        /// </summary>
        public bool LabelReplacesAddress { get; set; }
    }
}
