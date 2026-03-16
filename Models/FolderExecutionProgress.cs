namespace SSH_Helper.Models
{
    /// <summary>
    /// Progress information for folder preset execution.
    /// </summary>
    public class FolderExecutionProgress
    {
        /// <summary>
        /// Number of completed host-task operations for the current run.
        /// One operation is one preset finished on one host.
        /// </summary>
        public int CompletedOperations { get; set; }

        /// <summary>
        /// Total number of host-task operations for the current run.
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// The current host being processed.
        /// </summary>
        public string CurrentHost { get; set; } = string.Empty;

        /// <summary>
        /// The current preset being executed.
        /// </summary>
        public string CurrentPreset { get; set; } = string.Empty;

        /// <summary>
        /// Number of presets completed on the current host.
        /// </summary>
        public int CompletedPresets { get; set; }

        /// <summary>
        /// Total number of presets to run on each host.
        /// </summary>
        public int TotalPresets { get; set; }

        /// <summary>
        /// Number of hosts that have completed all presets.
        /// </summary>
        public int CompletedHosts { get; set; }

        /// <summary>
        /// Total number of hosts to process.
        /// </summary>
        public int TotalHosts { get; set; }
    }
}
