namespace SSH_Helper.Models
{
    /// <summary>
    /// Preset sort modes for the preset list.
    /// </summary>
    public enum PresetSortMode
    {
        Ascending,
        Descending,
        Manual
    }

    /// <summary>
    /// Root configuration object persisted to config.json
    /// </summary>
    public class AppConfiguration
    {
        public Dictionary<string, PresetInfo> Presets { get; set; } = new();
        public string Username { get; set; } = string.Empty;
        public int Delay { get; set; } = 500;
        public int Timeout { get; set; } = 10;

        // Window state
        public WindowState WindowState { get; set; } = new();

        // Preset sorting
        public PresetSortMode PresetSortMode { get; set; } = PresetSortMode.Ascending;
        public List<string> ManualPresetOrder { get; set; } = new();

        // Update settings
        public UpdateSettings UpdateSettings { get; set; } = new();
    }

    /// <summary>
    /// Settings for auto-update functionality.
    /// </summary>
    public class UpdateSettings
    {
        /// <summary>
        /// GitHub repository owner (username or organization).
        /// </summary>
        public string GitHubOwner { get; set; } = "nosmircss";

        /// <summary>
        /// GitHub repository name.
        /// </summary>
        public string GitHubRepo { get; set; } = "SSH_Helper";

        /// <summary>
        /// Whether to check for updates on application startup.
        /// </summary>
        public bool CheckOnStartup { get; set; } = true;

        /// <summary>
        /// Last time an update check was performed (UTC).
        /// </summary>
        public DateTime? LastCheckTime { get; set; }

        /// <summary>
        /// Version that the user chose to skip (won't be prompted again).
        /// </summary>
        public string? SkippedVersion { get; set; }
    }

    /// <summary>
    /// Stores window position and splitter settings.
    /// </summary>
    public class WindowState
    {
        public int? Left { get; set; } = 50;
        public int? Top { get; set; } = 50;
        public int? Width { get; set; } = 1850;
        public int? Height { get; set; } = 1050;
        public bool IsMaximized { get; set; }

        // Splitter positions
        public int? MainSplitterDistance { get; set; } = 400;
        public int? TopSplitterDistance { get; set; } = 800;
        public int? CommandSplitterDistance { get; set; } = 350;
        public int? OutputSplitterDistance { get; set; } = 300;
    }
}
