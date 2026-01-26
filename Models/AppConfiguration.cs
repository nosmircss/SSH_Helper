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
        public bool UseConnectionPooling { get; set; } = false;

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

        /// <summary>
        /// Manual ordering for root-level items on the Favorites tab.
        /// Contains preset names (prefixed with "preset:") and folder names (prefixed with "folder:").
        /// </summary>
        public List<string> ManualFavoriteOrder { get; set; } = new();

        // Update settings
        public UpdateSettings UpdateSettings { get; set; } = new();

        // Remember state settings
        public bool RememberState { get; set; } = true;
        public ApplicationState? SavedState { get; set; }

        // History settings
        public int MaxHistoryEntries { get; set; } = 30;

        // Theme settings
        /// <summary>
        /// When true, the application uses dark theme. Output window is always dark.
        /// </summary>
        public bool DarkMode { get; set; } = false;

        // Host grid settings
        /// <summary>
        /// When true, columns in the hosts DataGridView auto-resize to fit content.
        /// </summary>
        public bool AutoResizeHostColumns { get; set; } = false;

        // Font settings
        /// <summary>
        /// Font customization settings for UI elements.
        /// </summary>
        public FontSettings FontSettings { get; set; } = new();

        // SSH config settings
        /// <summary>
        /// Settings for SSH config file integration.
        /// </summary>
        public SshConfigSettings SshConfig { get; set; } = new();
    }

    /// <summary>
    /// Icon size options for toolbar and UI elements.
    /// </summary>
    public enum IconSize
    {
        Small = 16,
        Medium = 24,
        Large = 32
    }

    /// <summary>
    /// Font customization settings for different UI element categories.
    /// </summary>
    public class FontSettings
    {
        // === Font Families ===

        /// <summary>
        /// Font family for UI elements (e.g., "Segoe UI").
        /// </summary>
        public string UIFontFamily { get; set; } = "Segoe UI";

        /// <summary>
        /// Font family for code/monospace elements (e.g., "Cascadia Code").
        /// </summary>
        public string CodeFontFamily { get; set; } = "Cascadia Code";

        // === Font Sizes ===

        /// <summary>
        /// Font size for section titles (e.g., "Hosts", "Presets", "Commands").
        /// </summary>
        public float SectionTitleFontSize { get; set; } = 9.5f;

        /// <summary>
        /// Font size for tree views (preset list, favorites).
        /// </summary>
        public float TreeViewFontSize { get; set; } = 9.5f;

        /// <summary>
        /// Font size for placeholder/empty labels.
        /// </summary>
        public float EmptyLabelFontSize { get; set; } = 9.5f;

        /// <summary>
        /// Font size for execute buttons.
        /// </summary>
        public float ExecuteButtonFontSize { get; set; } = 9.5f;

        /// <summary>
        /// Font size for code editor (command input).
        /// </summary>
        public float CodeEditorFontSize { get; set; } = 9.75f;

        /// <summary>
        /// Font size for output area.
        /// </summary>
        public float OutputAreaFontSize { get; set; } = 9.75f;

        /// <summary>
        /// Font size for tab headers.
        /// </summary>
        public float TabFontSize { get; set; } = 9f;

        /// <summary>
        /// Font size for general buttons (Save, Browse, etc.).
        /// </summary>
        public float ButtonFontSize { get; set; } = 9f;

        /// <summary>
        /// Font size for host list items.
        /// </summary>
        public float HostListFontSize { get; set; } = 9f;

        /// <summary>
        /// Font size for context menus.
        /// </summary>
        public float MenuFontSize { get; set; } = 9f;

        /// <summary>
        /// Font size for tooltips.
        /// </summary>
        public float TooltipFontSize { get; set; } = 9f;

        /// <summary>
        /// Font size for status bar text.
        /// </summary>
        public float StatusBarFontSize { get; set; } = 9f;

        // === Global Scaling ===

        /// <summary>
        /// Global scale factor for all fonts (0.8 = 80%, 1.5 = 150%). Applied on top of individual sizes.
        /// </summary>
        public float GlobalScaleFactor { get; set; } = 1.0f;

        // === Layout Settings ===

        /// <summary>
        /// Line spacing multiplier for code editor (1.0 = normal, 1.5 = 150% line height).
        /// </summary>
        public float CodeEditorLineSpacing { get; set; } = 1.0f;

        /// <summary>
        /// Line spacing multiplier for output area.
        /// </summary>
        public float OutputAreaLineSpacing { get; set; } = 1.0f;

        /// <summary>
        /// Tab width in spaces for code editor indentation.
        /// </summary>
        public int TabWidth { get; set; } = 4;

        /// <summary>
        /// Enable word wrap in code editor.
        /// </summary>
        public bool CodeEditorWordWrap { get; set; } = false;

        /// <summary>
        /// Enable word wrap in output area.
        /// </summary>
        public bool OutputAreaWordWrap { get; set; } = false;

        /// <summary>
        /// Row height for tree views in pixels (0 = auto based on font).
        /// </summary>
        public int TreeViewRowHeight { get; set; } = 0;

        /// <summary>
        /// Row height for host list in pixels.
        /// </summary>
        public int HostListRowHeight { get; set; } = 28;

        // === Icon Settings ===

        /// <summary>
        /// Size of icons in the UI.
        /// </summary>
        public IconSize IconSize { get; set; } = IconSize.Small;

        // === Accent Color ===

        /// <summary>
        /// Custom accent color in ARGB format. Null uses system/theme default.
        /// </summary>
        public int? CustomAccentColor { get; set; } = null;

        /// <summary>
        /// Creates a copy of the current settings with default values.
        /// </summary>
        public static FontSettings CreateDefault() => new FontSettings();

        /// <summary>
        /// Applies the global scale factor to a font size.
        /// </summary>
        public float ScaledSize(float baseSize) => baseSize * GlobalScaleFactor;
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
        /// Indices of selected (checked) hosts in the grid.
        /// </summary>
        public List<int> SelectedHostIndices { get; set; } = new();

        /// <summary>
        /// The path to the last loaded CSV file (if any).
        /// </summary>
        public string? LastCsvPath { get; set; }

        /// <summary>
        /// The currently selected preset name.
        /// </summary>
        public string? SelectedPreset { get; set; }

        /// <summary>
        /// The currently selected folder name (if a folder is selected instead of a preset).
        /// </summary>
        public string? SelectedFolder { get; set; }

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

        /// <summary>
        /// Per-host results for folder executions. Null for single preset executions.
        /// </summary>
        public List<HostHistoryEntry>? HostResults { get; set; }
    }

    /// <summary>
    /// Per-host execution data stored within a folder history entry.
    /// </summary>
    public class HostHistoryEntry
    {
        public string HostAddress { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
        public DateTime Timestamp { get; set; }
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
        public int? HistorySplitterDistance { get; set; } = 137;
    }

    /// <summary>
    /// Settings for SSH config file integration.
    /// </summary>
    public class SshConfigSettings
    {
        /// <summary>
        /// When true, reads SSH config from %USERPROFILE%\.ssh\config
        /// and applies settings (IdentityFile, algorithms, etc.) to connections.
        /// </summary>
        public bool EnableSshConfig { get; set; } = false;
    }
}
