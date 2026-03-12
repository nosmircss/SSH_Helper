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
        /// <summary>
        /// Optional compressed representation of SavedState used for persistence size reduction.
        /// This is decompressed into SavedState on load and cleared in memory afterward.
        /// </summary>
        public string? SavedStateCompressed { get; set; }
        public Dictionary<string, EnvironmentConfig> Environments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? ActiveEnvironment { get; set; }
        /// <summary>
        /// The operator-selected base environment used when presets without an explicit environment restore context.
        /// </summary>
        public string? BaseEnvironment { get; set; }

        // History settings
        public int MaxHistoryEntries { get; set; } = 30;

        // Theme settings
        /// <summary>
        /// When true, the application uses dark theme. Output window is always dark.
        /// </summary>
        public bool DarkMode { get; set; } = true;

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

        /// <summary>
        /// Script editor behavior and diagnostics settings.
        /// </summary>
        public CommandEditorSettings CommandEditor { get; set; } = new();

        // SSH config settings
        /// <summary>
        /// Settings for SSH config file integration.
        /// </summary>
        public SshConfigSettings SshConfig { get; set; } = new();

        /// <summary>
        /// Credential and agent preferences.
        /// </summary>
        public CredentialSettings Credentials { get; set; } = new();

        // Recent files
        /// <summary>
        /// Most recently opened CSV file paths, newest first.
        /// </summary>
        public List<string> RecentFiles { get; set; } = new();

        /// <summary>
        /// Maximum number of recent files to remember.
        /// </summary>
        public int MaxRecentFiles { get; set; } = 10;

        /// <summary>
        /// UTC timestamp of the last application shutdown. Used by SchedulingService.DetectMissedRuns
        /// to anchor the missed-run detection window. Null on first install (means clean slate, no missed runs).
        /// </summary>
        public DateTime? LastAppShutdownUtc { get; set; }

        /// <summary>
        /// Maximum number of scheduled jobs that can execute concurrently.
        /// Default: 3. User-configurable via settings.
        /// </summary>
        public int MaxConcurrentJobs { get; set; } = 3;

        /// <summary>
        /// Default maximum number of history entries retained per job.
        /// Individual jobs can override via JobDefinition.MaxHistoryRuns.
        /// </summary>
        public int DefaultMaxHistoryRuns { get; set; } = 50;

        /// <summary>
        /// Default maximum age in days for history entries.
        /// Individual jobs can override via JobDefinition.HistoryRetentionDays.
        /// </summary>
        public int DefaultHistoryRetentionDays { get; set; } = 30;

        /// <summary>
        /// Maximum number of characters to retain per host in job history output.
        /// Output exceeding this limit is truncated with a marker.
        /// </summary>
        public int MaxJobOutputCharsPerHost { get; set; } = 1_048_576;

    }

    /// <summary>
    /// Persisted settings for the script editor experience.
    /// </summary>
    public class CommandEditorSettings
    {
        public const int MinValidationDebounceMs = 150;
        public const int MaxValidationDebounceMs = 2000;
        public const int MinIndentSize = 2;
        public const int MaxIndentSize = 8;
        public const int MinLongLineColumn = 80;
        public const int MaxLongLineColumn = 200;

        public bool EnableSyntaxHighlighting { get; set; } = true;
        public bool EnableAutocomplete { get; set; } = true;
        public bool AutocompleteShowOnTyping { get; set; } = true;
        public bool EnableInlineValidation { get; set; } = true;
        public int ValidationDebounceMs { get; set; } = 400;
        public bool ShowInlineWarnings { get; set; } = true;
        public bool EnableDiagnosticTooltips { get; set; } = true;
        public bool EnableVariableInspectorTooltips { get; set; } = true;
        public bool EnableYamlHygieneWarnings { get; set; } = true;
        public bool UseSpacesForTab { get; set; } = true;
        public int IndentSize { get; set; } = 2;
        public bool EnableSmartEnter { get; set; } = true;
        public bool PreserveBlankLineBetweenSteps { get; set; } = true;
        public bool EnableCurrentLineHighlight { get; set; } = true;
        public bool EnableIndentGuides { get; set; } = false;
        public bool ShowWhitespace { get; set; } = false;
        public bool EnableLongLineGuide { get; set; } = false;
        public int LongLineColumn { get; set; } = 120;
        public bool EnableCodeFolding { get; set; } = false;
        public bool EnableBraceMatching { get; set; } = true;

        public void Normalize()
        {
            ValidationDebounceMs = Math.Clamp(ValidationDebounceMs, MinValidationDebounceMs, MaxValidationDebounceMs);
            IndentSize = Math.Clamp(IndentSize, MinIndentSize, MaxIndentSize);
            LongLineColumn = Math.Clamp(LongLineColumn, MinLongLineColumn, MaxLongLineColumn);
        }

        public CommandEditorSettings CloneNormalized()
        {
            var clone = (CommandEditorSettings)MemberwiseClone();
            clone.Normalize();
            return clone;
        }
    }

    /// <summary>
    /// Font customization settings for different UI element categories.
    /// </summary>
    public class FontSettings
    {
        public const string DefaultUIFontFamily = "Segoe UI Semibold";

        // === Font Families ===

        /// <summary>
        /// Font family for UI elements (e.g., "Segoe UI Semibold").
        /// </summary>
        public string UIFontFamily { get; set; } = DefaultUIFontFamily;

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
        /// Font size for status bar text.
        /// </summary>
        public float StatusBarFontSize { get; set; } = 9f;

        /// <summary>
        /// Font size for dialog windows (confirmations, environment manager, etc.).
        /// </summary>
        public float DialogFontSize { get; set; } = 9f;

        // === Global Scaling ===

        /// <summary>
        /// Global scale factor for all fonts (0.8 = 80%, 1.5 = 150%). Applied on top of individual sizes.
        /// </summary>
        public float GlobalScaleFactor { get; set; } = 1.0f;

        // === Layout Settings ===

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
        /// Lightweight file identity for the last loaded CSV file.
        /// </summary>
        public CsvFileFingerprint? LastCsvFingerprint { get; set; }

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
        /// Legacy in-config execution history entries used only for migration.
        /// New history persistence is stored in external per-run files.
        /// </summary>
        public List<HistoryEntry> History { get; set; } = new();
    }

    /// <summary>
    /// A single history entry.
    /// </summary>
    public class HistoryEntry
    {
        /// <summary>
        /// Stable unique identifier for the history entry.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Display label for the history entry (legacy field name: Timestamp).
        /// </summary>
        public string Timestamp { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;

        /// <summary>
        /// Per-host results for folder executions. Null for single preset executions.
        /// </summary>
        public List<HostHistoryEntry>? HostResults { get; set; }

        /// <summary>
        /// Optional execution metadata used by the "View Details" dialog.
        /// Null for legacy entries and entries created before details capture.
        /// </summary>
        public ExecutionDetails? Details { get; set; }
    }

    /// <summary>
    /// Per-host execution data stored within a folder history entry.
    /// </summary>
    public class HostHistoryEntry
    {
        public string HostAddress { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
        public bool WasCancelled { get; set; }
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

        // Manage Environments dialog layout
        public int? EnvironmentDialogWidth { get; set; } = 920;
        public int? EnvironmentDialogHeight { get; set; } = 620;
        public int? EnvironmentDialogSplitterDistance { get; set; } = 270;
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

    /// <summary>
    /// Credential storage and SSH agent preferences.
    /// </summary>
    public class CredentialSettings
    {
        /// <summary>
        /// Store and retrieve passwords using Windows Credential Manager.
        /// </summary>
        public bool UseCredentialManager { get; set; } = false;

        /// <summary>
        /// Prefer SSH agent authentication when available.
        /// </summary>
        public bool PreferSshAgent { get; set; } = false;
    }

}
