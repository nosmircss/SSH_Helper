namespace SSH_Helper.Models
{
    /// <summary>
    /// Options for executing all presets in a folder.
    /// </summary>
    public class FolderExecutionOptions
    {
        /// <summary>
        /// List of preset names to execute (user can uncheck some in the dialog).
        /// </summary>
        public List<string> SelectedPresets { get; set; } = new();

        /// <summary>
        /// If true, run all presets simultaneously on each host.
        /// If false, run presets sequentially (one at a time).
        /// </summary>
        public bool RunPresetsInParallel { get; set; }

        /// <summary>
        /// If true, stop execution on the first error encountered.
        /// </summary>
        public bool StopOnFirstError { get; set; }

        /// <summary>
        /// Number of hosts to process in parallel. Default is 1 (sequential).
        /// </summary>
        public int ParallelHostCount { get; set; } = 1;

        /// <summary>
        /// If true, suppress preset name separators from output.
        /// </summary>
        public bool SuppressPresetNames { get; set; }

        /// <summary>
        /// Indices of the selected hosts from the original host list.
        /// Used to filter which hosts to execute against.
        /// </summary>
        public List<int> SelectedHostIndices { get; set; } = new();
    }
}
