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
        public int Timeout { get; set; } = 10;
        public int ConnectionTimeout { get; set; } = 30;

        // Window state
        public WindowState WindowState { get; set; } = new();

        // Preset sorting
        public PresetSortMode PresetSortMode { get; set; } = PresetSortMode.Manual;
        public List<string> ManualPresetOrder { get; set; } = new();

        // Preset folders
        /// <summary>
        /// Folder metadata keyed by folder name.
        /// </summary>
        public Dictionary<string, FolderInfo> PresetFolders { get; set; } = new();

        /// <summary>
        /// Manual ordering for presets within each folder.
        /// Key: folder name (empty string for root level), Value: ordered preset names.
        /// </summary>
        public Dictionary<string, List<string>> ManualPresetOrderByFolder { get; set; } = new();

        /// <summary>
        /// Manual ordering for folders.
        /// </summary>
        public List<string> ManualFolderOrder { get; set; } = new();

        // Update settings
        public UpdateSettings UpdateSettings { get; set; } = new();

        // Remember state settings
        public bool RememberState { get; set; } = true;
        public ApplicationState? SavedState { get; set; }

        // History settings
        public int MaxHistoryEntries { get; set; } = 30;
    }

    /// <summary>
    /// Saved application state for restore on startup.
    /// </summary>
    public class ApplicationState
    {
        /// <summary>
        /// The hosts data (CSV content as list of rows).
        /// </summary>
        public List<Dictionary<string, string>> Hosts { get; set; } = new();

        /// <summary>
        /// Column names for the hosts grid.
        /// </summary>
        public List<string> HostColumns { get; set; } = new();

        /// <summary>
        /// The path to the last loaded CSV file (if any).
        /// </summary>
        public string? LastCsvPath { get; set; }

        /// <summary>
        /// The currently selected preset name.
        /// </summary>
        public string? SelectedPreset { get; set; }

        /// <summary>
        /// The saved username (not password for security).
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Execution history entries (timestamp -> output).
        /// </summary>
        public List<HistoryEntry> History { get; set; } = new();
    }

    /// <summary>
    /// A single history entry.
    /// </summary>
    public class HistoryEntry
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
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

        /// <summary>
        /// Enable logging for the update process to help troubleshoot failures.
        /// </summary>
        public bool EnableUpdateLog { get; set; } = false;
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
