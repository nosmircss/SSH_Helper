using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Services.Editor;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.UI;
using SSH_Helper.Utilities;

namespace SSH_Helper
{
    /// <summary>
    /// Native methods for dark mode scrollbar support on Windows 10/11.
    /// </summary>
    internal static class NativeMethods
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Dark mode APIs for scrollbars (Windows 10 1903+)
        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int SetPreferredAppMode(int mode);

        [DllImport("uxtheme.dll", EntryPoint = "#133", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool AllowDarkModeForWindow(IntPtr hWnd, bool allow);

        [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern void FlushMenuThemes();

        // Child window enumeration for applying theme to scrollbars
        public delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr hwndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        // Window message constants
        public const int WM_THEMECHANGED = 0x031A;
        public const int WM_SETREDRAW = 0x000B;

        // SetWindowPos flags for forcing frame/non-client area redraw
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_FRAMECHANGED = 0x0020;

        // App mode constants
        public const int AppModeDefault = 0;
        public const int AppModeAllowDark = 1;
        public const int AppModeForceDark = 2;
        public const int AppModeForceLight = 3;
    }

    /// <summary>
    /// Tag object for TreeView nodes to identify presets vs folders.
    /// </summary>
    internal class PresetNodeTag
    {
        public bool IsFolder { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    internal enum DropPosition { None, Above, Inside, Below }

    public partial class Form1 : Form
    {
        private enum ConnectionTestVisualState
        {
            None,
            Testing,
            Success,
            Failure
        }

        private sealed class ConnectionTestRowVisualStateInfo
        {
            public ConnectionTestRowVisualStateInfo(ConnectionTestVisualState state, string toolTipText)
            {
                State = state;
                ToolTipText = toolTipText ?? string.Empty;
            }

            public ConnectionTestVisualState State { get; }

            public string ToolTipText { get; }
        }

        #region Constants

        private const string ApplicationVersion = "0.51.10";
        private const string ApplicationName = "SSH Helper";
        private const string SelectColumnName = "";
        private const int UiOutputThrottleMs = 50;
        private const string FolderIcon = "\U0001F4C1";
        private const string StarIcon = "\u2605";
        private const string FolderBaseEnvironmentInheritChoiceValue = "__SSH_HELPER_FOLDER_BASE_INHERIT__";
        private const string FavoriteKeyFolderPrefix = "folder:";
        private const string FavoriteKeyPresetPrefix = "preset:";
        private const int LargeHistoryPayloadCharThreshold = 10_000_000;
        private const int SmallHistoryPayloadCharThreshold = 500_000;
        private const int OutputTextRecreateThresholdChars = 500_000;
        private const int OutputTextRecreateTargetChars = 100_000;
        private const int HiddenPresetsTabHeaderFallbackHeight = 24;
        private static readonly TimeSpan AutomaticHistoryCompactionCooldown = TimeSpan.FromSeconds(2);
        private static readonly string FolderSummarySeparator = new string('=', 60);
        private static readonly string FolderSummarySubSeparator = new string('=', 9);

        #endregion

        #region Services

        private readonly ConfigurationService _configService;
        private readonly EnvironmentService _environmentService;
        private readonly PresetManager _presetManager;
        private readonly CsvManager _csvManager;
        private readonly SshExecutionService _sshService;
        private readonly ExecutionCoordinator _executionCoordinator;
        private readonly UpdateService _updateService;
        private readonly SshConfigService _sshConfigService;
        private readonly HistoryStorageService _historyStorage;
        private readonly PresetDeleteUndoService _presetDeleteUndoService = new();

        // Scheduler services
        private JobStorageService? _jobStorage;
        private SchedulingService? _schedulingService;
        private JobExecutionService? _jobExecutionService;
        private JobHistoryService? _jobHistoryService;
        private JobExportService? _jobExportService;
        private ToolStripStatusLabel? _statusScheduler;
        private System.Windows.Forms.Timer? _statusBarTimer;
        private readonly ModelessDialogManager<JobListDialog> _jobListDialogManager = new();
        private readonly HashSet<string> _runNowJobIds = new();

        #endregion

        #region State

        private FlowCanvasForm? _flowCanvasForm;
        private string? _loadedFilePath;
        private CsvFileFingerprint? _loadedFileFingerprint;
        private HostGridSnapshot? _loadedFileSnapshot;
        private CsvFileSyncStatus _loadedFileSyncStatus = CsvFileSyncStatus.NotTracked;
        private string? _activePresetName;
        private string _activeEnvironmentName = EnvironmentConfig.DefaultName;
        private string _baseEnvironmentName = EnvironmentConfig.DefaultName;
        private bool _csvDirty;
        private bool _exitConfirmed;
        private bool _suppressPresetSelectionChange;
        private bool _suppressEnvironmentSelectionChange;
        private bool _suppressExpandCollapseEvents;
        private bool _pendingColumnAutoSize;
        private int _rightClickedColumnIndex = -1;
        private int _rightClickedRowIndex = -1;
        private readonly BindingList<HistoryListItem> _outputHistory = new();
        private bool _deferredSchedulerBootstrapStarted;

        // Recent files menu
        private ToolStripMenuItem? _recentFilesMenuItem;
        private ToolStripSeparator? _recentFilesSeparator;

        // Connection testing
        private ToolStripMenuItem? _testConnectionMenuItem;
        private ToolStripSeparator? _testConnectionSeparator;
        private CancellationTokenSource? _connectionTestCts;
        private bool _isTestingConnections;
        private int _connectionTestProgressRunId;
        private readonly ConditionalWeakTable<DataGridViewRow, ConnectionTestRowVisualStateInfo> _connectionTestRowStates = new();

        // Preset search/filter
        private BufferedPanel? _presetSearchPanel;
        private TextBox? _txtPresetSearch;
        private Label? _btnPresetSearchClear;
        private System.Windows.Forms.Timer? _presetSearchDebounceTimer;

        // Find dialog state
        private FindDialog? _findDialog;
        private string _lastFindTerm = "";
        private bool _lastFindMatchCase;
        private List<int> _findMatches = new();
        private int _currentMatchIndex = -1;

        // Preset sorting
        private PresetSortMode _currentSortMode = PresetSortMode.Manual;
        private readonly List<string> _manualPresetOrder = new();

        // Preset TreeView drag-drop state
        private TreeNode? _draggedNode;
        private TreeNode? _dropTargetNode;
        private DropPosition _dropPosition = DropPosition.None;
        private TreeNode? _favLastHighlightedNode;
        private int _lastPresetsTabIndex;
        private bool _restoringPresetsTabSelection;
        private PresetNodeTag? _lastPresetsTreeSelection;
        private PresetNodeTag? _lastFavoritesTreeSelection;
        private Func<string, string, string, string>? _inputBoxPromptOverrideForTests = null;

        // Track selected folder for Run button (TreeView selection can be unreliable on button click)
        private string? _selectedFolderName;

        // Per-host history data for the currently selected folder history entry
        private List<HostHistoryEntry>? _currentHostResults;
        private readonly Dictionary<string, HistoryIndexEntry> _historyIndexEntries = new(StringComparer.Ordinal);
        private bool _suppressHistorySelectionChanged;
        private bool _suppressHostSelectionChanged;
        private bool _historySelectionHandlingEnabled;
        private bool _historySelectionArmPending;
        private DateTime _historySelectionArmedAtUtc = DateTime.MaxValue;
        private string _selectedHistoryOutput = string.Empty;
        private int _manualExecutionCompletedOperations;
        private int _manualExecutionProgressRunId;
        private string? _loadedHistoryPayloadId;
        private HistoryRunPayload? _loadedHistoryPayload;
        private bool _loadedHistoryPayloadHasDetails;
        private bool _loadedHistoryPayloadHasHostOutputs;
        private DateTime _lastAutomaticHistoryCompactionUtc = DateTime.MinValue;
        private string? _lastHistorySelectionGcEntryId;
        // Output state
        private readonly StringBuilder _outputBuffer = new();
        private readonly object _outputBufferLock = new();
        private readonly OutputThrottler _uiOutputThrottler;
        private bool _manualCancellationRequested;

        // Credential provider
        private ICredentialProvider? _credentialProvider;

        // Track which TreeView triggered the context menu
        private TreeView? _contextMenuSourceTreeView;
        private readonly ToolStripMenuItem _ctxFolderBaseEnvironment = new();
        private readonly ToolStripSeparator _ctxFolderExpandCollapseSeparator = new();
        private readonly ToolStripMenuItem _ctxExpandAllSubfolders = new();
        private readonly ToolStripMenuItem _ctxCollapseAllSubfolders = new();

        // Script editor services
        private ScriptAutocompleteProvider? _scriptAutocompleteProvider;
        private readonly YamlSshSyntaxHighlighter _scriptSyntaxHighlighter = new();
        private readonly ScriptEditorValidationService _scriptValidationService = new();
        private CommandEditorSettings _commandEditorSettings = new();

        // Custom scrollbars for DataGridView (to support dark mode theming)
        private VScrollBar? _dgvVScrollBar;
        private HScrollBar? _dgvHScrollBar;
        private Panel? _dgvScrollCorner;
        private readonly HostGridRestoreBatcher _hostGridRestoreBatcher;

        // Multi-host selection state
        private bool _selectAllChecked;
        private Rectangle _selectAllCheckboxBounds;

        #endregion

        #region Constructor

        public Form1()
        {
            // Enable dark mode support for scrollbars (must be called before creating windows)
            NativeMethods.SetPreferredAppMode(NativeMethods.AppModeAllowDark);

            // Enable form-level double buffering to reduce flicker
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            InitializeComponent();
            InitializeFlowCanvasMenuItem();
            _hostGridRestoreBatcher = new HostGridRestoreBatcher(
                onScrollbarRefresh: UpdateDataGridViewScrollbars,
                onHostCountRefresh: UpdateHostCount,
                onMarkDirty: MarkHostGridDirty);
            InitializeFolderBaseEnvironmentContextMenuItem();
            InitializeFolderExpandCollapseContextMenuItems();
            Text = $"{ApplicationName} {ApplicationVersion}";

            var uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _uiOutputThrottler = new OutputThrottler(TimeSpan.FromMilliseconds(UiOutputThrottleMs), AppendOutputToUi, uiContext);
            FormClosed += (_, __) =>
            {
                _uiOutputThrottler.Dispose();
                _scriptValidationService.Dispose();
            };

            // Initialize services
            _configService = new ConfigurationService();
            _historyStorage = new HistoryStorageService(_configService.ConfigFilePath);
            _environmentService = new EnvironmentService(_configService);
            _presetManager = new PresetManager(_configService);
            _csvManager = new CsvManager();
            var config = _configService.Load();
            if (_configService.ConfigLoadError != null)
            {
                DialogTheme.Show(_configService.ConfigLoadError, "Configuration Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            var poolTimeouts = SshTimeoutOptions.Create(config.Timeout, config.ConnectionTimeout);
            _sshService = new SshExecutionService(enablePooling: true, poolTimeouts);
            _sshService.UseConnectionPooling = config.UseConnectionPooling;
            _sshService.PreferSshAgent = config.Credentials.PreferSshAgent;
            _sshConfigService = new SshConfigService();
            _executionCoordinator = new ExecutionCoordinator(_sshService, _configService);

            // Wire up SSH service events
            _sshService.OutputReceived += SshService_OutputReceived;
            _sshService.ColumnUpdateRequested += SshService_ColumnUpdateRequested;
            _sshService.EnvironmentVariableUpdateRequested += SshService_EnvironmentVariableUpdateRequested;
            _sshService.CommandCompleted += SshService_CommandCompleted;
            _sshService.ExecutionCompleted += SshService_ExecutionCompleted;
            _environmentService.EnvironmentChanged += EnvironmentService_EnvironmentChanged;

            // Initialize update service
            _updateService = new UpdateService(
                config.UpdateSettings.GitHubOwner,
                config.UpdateSettings.GitHubRepo,
                ApplicationVersion);

            InitializeFromConfiguration(config);
            InitializeCredentials();
            InitializeDataGridView();
            InitializeScriptEditor();
            InitializeOutputHistory();
            InitializeHistoryPersistence();
            InitializeEventHandlers();
            InitializeToolbarSync();
            InitializeEnvironmentToolbar();
            InitializePasswordMasking();
            EnableDoubleBuffering();
            RestoreWindowState(config);
            UpdateHostCount();
            UpdatePresetHeaderIndicator();
            UpdateSortModeIndicator();
            RebuildRecentFilesMenu();
            InitializePresetSearchFilter();
            InitializePresetsTabChrome();
            InitializeConnectionTesting();
            InitializeSchedulerStatusBar();
            RefreshPresetDeleteUndoUi();
            UpdateStatusBar("Ready");

            // Apply saved theme and fonts
            var currentConfig = _configService.GetCurrent();
            ApplyTheme(currentConfig.DarkMode);
            ApplyFontSettings(currentConfig.FontSettings);
            ApplyColumnAutoResize(currentConfig.AutoResizeHostColumns);

            // Check for updates on startup (after form is shown)
            Shown += Form1_Shown;
        }

        private async void Form1_Shown(object? sender, EventArgs e)
        {
            // Remove handler to only run once
            Shown -= Form1_Shown;
            _historySelectionArmedAtUtc = DateTime.MaxValue;
            _historySelectionArmPending = true;
            Application.Idle -= ArmHistorySelectionOnIdle;
            Application.Idle += ArmHistorySelectionOnIdle;

            // Restore folder expand/collapse state after form is fully shown
            RestoreFolderExpandState();

            // Auto-size columns to content if state was restored (must happen after form is visible)
            if (_pendingColumnAutoSize)
            {
                _pendingColumnAutoSize = false;
                AutoSizeColumnsToContent();
            }

            // After startup restore/layout, ensure selected preset is actually in viewport.
            BeginInvoke((Action)EnsureSelectedPresetNodeVisible);
            Application.Idle -= BootstrapSchedulerAfterStartupRestoreOnIdle;
            Application.Idle += BootstrapSchedulerAfterStartupRestoreOnIdle;

            var config = _configService.GetCurrent();
            if (config.UpdateSettings.CheckOnStartup)
            {
                await CheckForUpdatesAsync(silent: true);
            }
        }

        private void BootstrapSchedulerAfterStartupRestoreOnIdle(object? sender, EventArgs e)
        {
            Application.Idle -= BootstrapSchedulerAfterStartupRestoreOnIdle;
            RunDeferredSchedulerBootstrap();
        }

        private bool TryBeginDeferredSchedulerBootstrap()
        {
            if (_deferredSchedulerBootstrapStarted)
            {
                return false;
            }

            _deferredSchedulerBootstrapStarted = true;
            return true;
        }

        private void RunDeferredSchedulerBootstrap()
        {
            if (!TryBeginDeferredSchedulerBootstrap())
            {
                return;
            }

            InitializeSchedulerServices();
            UpdateSchedulerStatusBar();
        }

        private void RestoreFolderExpandState()
        {
            _suppressExpandCollapseEvents = true;
            foreach (TreeNode node in trvPresets.Nodes)
            {
                if (node.Tag is PresetNodeTag tag && tag.IsFolder)
                {
                    if (_presetManager.Folders.TryGetValue(tag.Name, out var folderInfo))
                    {
                        if (folderInfo.IsExpanded)
                            node.Expand();
                        else
                            node.Collapse();
                    }
                }
            }
            _suppressExpandCollapseEvents = false;
        }

        private void EnsureSelectedPresetNodeVisible()
        {
            if (trvPresets.IsDisposed)
                return;

            var selectedNode = trvPresets.SelectedNode;
            if (selectedNode == null)
                return;

            EnsureTreeNodeFullyVisible(trvPresets, selectedNode);
        }

        private void EnsureTreeNodeFullyVisible(TreeView treeView, TreeNode node)
        {
            if (treeView.IsDisposed || node.TreeView != treeView)
                return;

            // Preserve saved collapse state: do not auto-expand collapsed branches.
            if (HasCollapsedAncestor(node))
                return;

            treeView.Update();
            if (IsTreeNodeFullyVisible(treeView, node))
                return;

            var previousSuppressExpand = _suppressExpandCollapseEvents;
            _suppressExpandCollapseEvents = true;
            try
            {
                node.EnsureVisible();
            }
            finally
            {
                _suppressExpandCollapseEvents = previousSuppressExpand;
            }
        }

        private static bool IsTreeNodeFullyVisible(TreeView treeView, TreeNode node)
        {
            var bounds = node.Bounds;
            return bounds.Height > 0 &&
                bounds.Top >= 0 &&
                bounds.Bottom <= treeView.ClientSize.Height;
        }

        private static bool HasCollapsedAncestor(TreeNode node)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                if (!parent.IsExpanded)
                    return true;

                parent = parent.Parent;
            }

            return false;
        }

        #endregion

        #region Initialization

        private void InitializeFromConfiguration(AppConfiguration config)
        {
            _activeEnvironmentName = string.IsNullOrWhiteSpace(config.ActiveEnvironment)
                ? EnvironmentConfig.DefaultName
                : config.ActiveEnvironment;
            _presetManager.Load(config);

            // Populate UI from config
            tsbUsername.Text = config.Username;
            txtUsername.Text = config.Username;

            // Load sort mode and manual order
            _currentSortMode = config.PresetSortMode;
            _manualPresetOrder.Clear();
            _manualPresetOrder.AddRange(config.ManualPresetOrder);

            // Populate preset list with proper sorting
            // Don't restore expand state here - Form1_Shown will do it after the form is visible
            RefreshPresetList(restoreExpandState: false, configOverride: config);

            // Show global default timeout as placeholder when preset has no override
            txtTimeoutHeader.PlaceholderText = config.Timeout.ToString();
        }

        private static DataGridViewCheckBoxColumn CreateSelectColumn()
        {
            return new DataGridViewCheckBoxColumn
            {
                Name = SelectColumnName,
                HeaderText = "",
                Width = 40,
                Resizable = DataGridViewTriState.False,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = false,
                FalseValue = false,
                TrueValue = true,
                ValueType = typeof(bool)
            };
        }

        private void InitializeDataGridView()
        {
            // Add checkbox column for multi-host selection (first column)
            dgv_variables.Columns.Add(CreateSelectColumn());

            dgv_variables.Columns.Add(CsvManager.HostColumnName, CsvManager.HostColumnName);
            dgv_variables.Columns[CsvManager.HostColumnName].Width = 150;

            // Modern styling
            dgv_variables.EnableHeadersVisualStyles = false;
            dgv_variables.BackgroundColor = Color.White;
            dgv_variables.GridColor = Color.FromArgb(222, 226, 230);

            // Column headers
            dgv_variables.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv_variables.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            dgv_variables.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(33, 37, 41);
            dgv_variables.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            dgv_variables.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
            dgv_variables.ColumnHeadersHeight = 36;
            dgv_variables.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Row headers
            dgv_variables.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv_variables.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            dgv_variables.RowHeadersDefaultCellStyle.ForeColor = Color.FromArgb(108, 117, 125);
            dgv_variables.RowHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgv_variables.RowHeadersWidth = 50;

            // Cell styles
            dgv_variables.DefaultCellStyle.BackColor = Color.White;
            dgv_variables.DefaultCellStyle.ForeColor = Color.FromArgb(33, 37, 41);
            dgv_variables.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgv_variables.DefaultCellStyle.SelectionBackColor = Color.FromArgb(13, 110, 253);
            dgv_variables.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv_variables.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            dgv_variables.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);

            // Explicitly disable auto row sizing and set fixed row height
            dgv_variables.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgv_variables.RowTemplate.Height = 28;

            dgv_variables.ColumnHeadersVisible = true;
            dgv_variables.RowHeadersVisible = true;

            // Set up custom scrollbars for dark mode support
            SetupDataGridViewScrollbars();
        }

        private void InitializeScriptEditor()
        {
            _scriptAutocompleteProvider = new ScriptAutocompleteProvider(GetEditorHostColumns);
            txtCommand.SetAutocompleteProvider(_scriptAutocompleteProvider);
            txtCommand.SetSyntaxHighlighter(_scriptSyntaxHighlighter);
            txtCommand.SetValidationService(_scriptValidationService);
            txtCommand.SetVariableTooltipResolvers(ResolveEditorVariableValue, ResolveEditorColumnValue);
            ApplyCommandEditorSettings(_configService.GetCurrent().CommandEditor);
        }

        private void ApplyCommandEditorSettings(CommandEditorSettings? settings)
        {
            _commandEditorSettings = (settings ?? new CommandEditorSettings()).CloneNormalized();
            txtCommand.ApplyCommandEditorSettings(_commandEditorSettings);
        }

        private IReadOnlyCollection<string> GetEditorHostColumns()
        {
            return dgv_variables.Columns
                .Cast<DataGridViewColumn>()
                .Select(column => column.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name) && !string.Equals(name, SelectColumnName, StringComparison.Ordinal))
                .ToList();
        }

        private string? ResolveEditorVariableValue(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                return null;

            var key = variableName.Trim();
            if (TryGetBuiltInEditorVariable(key, out var builtIn))
            {
                return builtIn;
            }

            if (ScriptParser.IsYamlScript(txtCommand.Text))
            {
                try
                {
                    var parser = new ScriptParser();
                    var script = parser.Parse(txtCommand.Text);
                    if (script.Vars.TryGetValue(key, out var value) && value != null)
                    {
                        return value is IEnumerable<string> values
                            ? string.Join(", ", values)
                            : value.ToString();
                    }
                }
                catch
                {
                    // Ignore parser issues for hover preview.
                }
            }

            var environmentVariables = _environmentService.GetActiveEnvironmentVariables();
            if (environmentVariables.TryGetValue(key, out var environmentValue))
            {
                return environmentValue;
            }

            if (_scriptAutocompleteProvider != null)
            {
                var symbols = _scriptAutocompleteProvider.ExtractDynamicSymbols(txtCommand.Text);
                if (symbols.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    return "[declared in script]";
                }
            }

            return null;
        }

        private bool TryGetBuiltInEditorVariable(string key, out string? value)
        {
            value = key.ToLowerInvariant() switch
            {
                "_timestamp" => DateTime.Now.ToString("O"),
                "_iteration" => "0",
                "_last_error" => string.Empty,
                "_output" => string.Empty,
                "_host" => ResolveEditorColumnValue(CsvManager.HostColumnName),
                "_port" => ResolveEditorColumnValue("port"),
                "_username" => ResolveEditorColumnValue("username") ?? tsbUsername.Text,
                "_password" => ResolveEditorColumnValue("password"),
                _ => null
            };

            return value != null;
        }

        private string? ResolveEditorColumnValue(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName) || !dgv_variables.Columns.Contains(columnName))
                return null;

            var row = GetSelectedHostPreviewRow();
            if (row == null)
                return null;

            return row.Cells[columnName].Value?.ToString();
        }

        private DataGridViewRow? GetSelectedHostPreviewRow()
        {
            if (dgv_variables.CurrentRow != null && !dgv_variables.CurrentRow.IsNewRow)
            {
                return dgv_variables.CurrentRow;
            }

            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                if (!row.IsNewRow)
                    return row;
            }

            return null;
        }

        private void SetupDataGridViewScrollbars()
        {
            // Hide the built-in scrollbars
            dgv_variables.ScrollBars = ScrollBars.None;

            // Change DataGridView from Dock.Fill to manual positioning so we can add scrollbars
            dgv_variables.Dock = DockStyle.None;
            dgv_variables.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            // Create vertical scrollbar
            _dgvVScrollBar = new VScrollBar
            {
                Width = SystemInformation.VerticalScrollBarWidth,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom
            };

            // Create horizontal scrollbar
            _dgvHScrollBar = new HScrollBar
            {
                Height = SystemInformation.HorizontalScrollBarHeight,
                Visible = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            // Create corner panel (fills the gap when both scrollbars are visible)
            _dgvScrollCorner = new Panel
            {
                Width = SystemInformation.VerticalScrollBarWidth,
                Height = SystemInformation.HorizontalScrollBarHeight,
                Visible = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            // Add controls to the hosts panel
            hostsPanel.Controls.Add(_dgvScrollCorner);
            hostsPanel.Controls.Add(_dgvHScrollBar);
            hostsPanel.Controls.Add(_dgvVScrollBar);

            // Bring scrollbars to front
            _dgvVScrollBar.BringToFront();
            _dgvHScrollBar.BringToFront();
            _dgvScrollCorner.BringToFront();

            // Wire up scrollbar events
            _dgvVScrollBar.Scroll += DgvVScrollBar_Scroll;
            _dgvHScrollBar.Scroll += DgvHScrollBar_Scroll;

            // Wire up DataGridView events to update scrollbar state
            dgv_variables.RowsAdded += (s, e) => RequestHostGridScrollbarRefresh();
            dgv_variables.RowsRemoved += (s, e) => RequestHostGridScrollbarRefresh();
            dgv_variables.ColumnAdded += (s, e) => RequestHostGridScrollbarRefresh();
            dgv_variables.ColumnRemoved += (s, e) => RequestHostGridScrollbarRefresh();
            dgv_variables.ColumnWidthChanged += (s, e) => RequestHostGridScrollbarRefresh();
            dgv_variables.Resize += (s, e) => RequestHostGridScrollbarRefresh();
            dgv_variables.Scroll += DgvVariables_Scroll;
            dgv_variables.MouseWheel += DgvVariables_MouseWheel;
            hostsPanel.Resize += (s, e) => RequestHostGridScrollbarRefresh();

            // Initial update
            UpdateDataGridViewScrollbars();
        }

        private void DgvVScrollBar_Scroll(object? sender, ScrollEventArgs e)
        {
            if (e.NewValue >= 0 && e.NewValue < dgv_variables.RowCount)
            {
                dgv_variables.FirstDisplayedScrollingRowIndex = e.NewValue;
            }
        }

        private void DgvHScrollBar_Scroll(object? sender, ScrollEventArgs e)
        {
            dgv_variables.HorizontalScrollingOffset = e.NewValue;
        }

        private void DgvVariables_Scroll(object? sender, ScrollEventArgs e)
        {
            // Sync custom scrollbars with DataGridView's internal scroll position
            if (_dgvVScrollBar != null && e.ScrollOrientation == ScrollOrientation.VerticalScroll)
            {
                _dgvVScrollBar.Value = Math.Min(e.NewValue, _dgvVScrollBar.Maximum);
            }
            else if (_dgvHScrollBar != null && e.ScrollOrientation == ScrollOrientation.HorizontalScroll)
            {
                _dgvHScrollBar.Value = Math.Min(e.NewValue, _dgvHScrollBar.Maximum);
            }
        }

        private void DgvVariables_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_dgvVScrollBar == null || !_dgvVScrollBar.Visible || dgv_variables.RowCount == 0)
                return;

            // Calculate scroll amount (typically 3 rows per wheel notch)
            int scrollLines = SystemInformation.MouseWheelScrollLines;
            int delta = e.Delta > 0 ? -scrollLines : scrollLines;

            // Calculate new row index
            int currentRow = dgv_variables.FirstDisplayedScrollingRowIndex;
            int newRow = Math.Max(0, Math.Min(dgv_variables.RowCount - 1, currentRow + delta));

            if (newRow != currentRow && newRow >= 0)
            {
                dgv_variables.FirstDisplayedScrollingRowIndex = newRow;
                _dgvVScrollBar.Value = Math.Min(newRow, _dgvVScrollBar.Maximum);
            }

            // Mark the event as handled to prevent default behavior
            if (e is HandledMouseEventArgs handled)
            {
                handled.Handled = true;
            }
        }

        private void UpdateDataGridViewScrollbars()
        {
            if (_dgvVScrollBar == null || _dgvHScrollBar == null || _dgvScrollCorner == null)
                return;

            // Get the available area within hostsPanel (accounting for padding and header)
            int headerHeight = hostsHeaderPanel.Height;
            int padding = hostsPanel.Padding.All;
            int availableWidth = hostsPanel.ClientSize.Width - padding * 2;
            int availableHeight = hostsPanel.ClientSize.Height - headerHeight - padding;

            // Calculate total content dimensions
            int totalRowHeight = dgv_variables.RowCount * dgv_variables.RowTemplate.Height + dgv_variables.ColumnHeadersHeight;
            int totalColumnWidth = dgv_variables.Columns.Cast<DataGridViewColumn>().Sum(c => c.Width) + dgv_variables.RowHeadersWidth;

            // Determine if scrollbars are needed (iteratively since they affect available space)
            bool needVScroll = totalRowHeight > availableHeight;
            bool needHScroll = totalColumnWidth > availableWidth;

            // If vertical scrollbar is shown, it reduces horizontal space
            if (needVScroll)
                needHScroll = totalColumnWidth > (availableWidth - _dgvVScrollBar.Width);
            // If horizontal scrollbar is shown, it reduces vertical space
            if (needHScroll)
                needVScroll = totalRowHeight > (availableHeight - _dgvHScrollBar.Height);

            // Calculate DataGridView size
            int dgvWidth = availableWidth - (needVScroll ? _dgvVScrollBar.Width : 0);
            int dgvHeight = availableHeight - (needHScroll ? _dgvHScrollBar.Height : 0);

            // Position and size the DataGridView
            dgv_variables.Location = new Point(padding, headerHeight);
            dgv_variables.Size = new Size(dgvWidth, dgvHeight);

            // Position vertical scrollbar
            _dgvVScrollBar.Visible = needVScroll;
            if (needVScroll)
            {
                _dgvVScrollBar.Location = new Point(padding + dgvWidth, headerHeight);
                _dgvVScrollBar.Height = dgvHeight;

                int displayedRows = dgv_variables.DisplayedRowCount(false);
                _dgvVScrollBar.Minimum = 0;
                _dgvVScrollBar.Maximum = Math.Max(0, dgv_variables.RowCount - 1);
                _dgvVScrollBar.LargeChange = Math.Max(1, displayedRows);
                _dgvVScrollBar.SmallChange = 1;
                if (dgv_variables.FirstDisplayedScrollingRowIndex >= 0)
                {
                    _dgvVScrollBar.Value = Math.Min(dgv_variables.FirstDisplayedScrollingRowIndex, _dgvVScrollBar.Maximum);
                }
            }

            // Position horizontal scrollbar
            _dgvHScrollBar.Visible = needHScroll;
            if (needHScroll)
            {
                _dgvHScrollBar.Location = new Point(padding, headerHeight + dgvHeight);
                _dgvHScrollBar.Width = dgvWidth;

                _dgvHScrollBar.Minimum = 0;
                _dgvHScrollBar.Maximum = Math.Max(0, totalColumnWidth - dgvWidth + _dgvHScrollBar.LargeChange);
                _dgvHScrollBar.LargeChange = Math.Max(1, dgvWidth / 4);
                _dgvHScrollBar.SmallChange = 20;
                _dgvHScrollBar.Value = Math.Min(dgv_variables.HorizontalScrollingOffset, Math.Max(0, _dgvHScrollBar.Maximum - _dgvHScrollBar.LargeChange + 1));
            }

            // Position corner panel only when both scrollbars are visible
            _dgvScrollCorner.Visible = needVScroll && needHScroll;
            if (_dgvScrollCorner.Visible)
            {
                _dgvScrollCorner.Location = new Point(padding + dgvWidth, headerHeight + dgvHeight);
                _dgvScrollCorner.BringToFront();
            }

            // Apply current theme colors to scrollbars
            ApplyScrollbarColors();
        }

        private void ApplyScrollbarColors()
        {
            if (_dgvVScrollBar == null || _dgvHScrollBar == null || _dgvScrollCorner == null)
                return;

            if (_isDarkMode)
            {
                _dgvScrollCorner.BackColor = DarkSurface0;
                // Apply dark mode theme to scrollbars
                ApplyDarkScrollbars(_dgvVScrollBar);
                ApplyDarkScrollbars(_dgvHScrollBar);
            }
            else
            {
                _dgvScrollCorner.BackColor = Color.FromArgb(248, 249, 250);
                ApplyLightScrollbars(_dgvVScrollBar);
                ApplyLightScrollbars(_dgvHScrollBar);
            }
        }

        private IDisposable BeginHostGridRestoreScope() => _hostGridRestoreBatcher.BeginRestoreScope();

        private IDisposable BeginHostGridMutationScope() => _hostGridRestoreBatcher.BeginMutationScope();

        private void RequestHostGridScrollbarRefresh() => _hostGridRestoreBatcher.RequestScrollbarRefresh();

        private void RequestHostGridHostCountRefresh() => _hostGridRestoreBatcher.RequestHostCountRefresh();

        private void RequestHostGridDirtyMark() => _hostGridRestoreBatcher.RequestMarkDirty();

        private void MarkHostGridDirty()
        {
            _csvDirty = true;
        }

        private void InitializeOutputHistory()
        {
            lstOutput.DataSource = _outputHistory;
            lstOutput.DisplayMember = nameof(HistoryListItem.Label);
            ConfigureHistoryListLayout();
        }

        private void InitializeHistoryPersistence()
        {
            var config = _configService.GetCurrent();
            var indexEntries = _historyStorage.LoadIndex();

            if (indexEntries.Count == 0 && config.SavedState?.History != null && config.SavedState.History.Count > 0)
            {
                var imported = _historyStorage.ImportLegacyHistory(config.SavedState.History, config.MaxHistoryEntries);
                if (imported > 0)
                {
                    config.SavedState.History.Clear();
                    _configService.Save(config);
                    indexEntries = _historyStorage.LoadIndex();
                }
            }

            LoadHistoryIndexIntoList(indexEntries);
        }

        private void LoadHistoryIndexIntoList(IReadOnlyList<HistoryIndexEntry>? indexEntries = null, string? selectEntryId = null)
        {
            var entries = indexEntries ?? _historyStorage.LoadIndex();
            var selectedIndexToApply = -1;

            _suppressHistorySelectionChanged = true;
            lstOutput.BeginUpdate();
            try
            {
                _historyIndexEntries.Clear();
                _outputHistory.Clear();
                foreach (var entry in entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                        continue;

                    _historyIndexEntries[entry.Id] = entry;
                    _outputHistory.Add(HistoryListCollectionUpdater.CreateListItem(entry));
                }

                _loadedHistoryPayloadId = null;
                _loadedHistoryPayload = null;
                _loadedHistoryPayloadHasDetails = false;
                _loadedHistoryPayloadHasHostOutputs = false;
                _selectedHistoryOutput = string.Empty;

                if (!string.IsNullOrWhiteSpace(selectEntryId))
                {
                    selectedIndexToApply = _outputHistory
                        .Select((item, index) => new { item.Id, index })
                        .FirstOrDefault(item => string.Equals(item.Id, selectEntryId, StringComparison.Ordinal))?.index ?? -1;
                }

                lstOutput.ClearSelected();
                historySplitContainer.Panel2Collapsed = true;
                lstHosts.Items.Clear();
                _currentHostResults = null;

                if (_outputHistory.Count == 0)
                {
                    ClearOutput();
                }
            }
            finally
            {
                lstOutput.EndUpdate();
                _suppressHistorySelectionChanged = false;
            }

            if (selectedIndexToApply >= 0)
            {
                EnableHistorySelectionHandling();
                lstOutput.SelectedIndex = selectedIndexToApply;
            }
        }

        private void InsertHistoryEntryIntoList(HistoryIndexEntry indexEntry, HistoryRunPayload payload)
        {
            ArgumentNullException.ThrowIfNull(indexEntry);
            ArgumentNullException.ThrowIfNull(payload);

            CacheLoadedHistoryPayload(
                indexEntry.Id,
                payload,
                hasDetails: payload.Details != null,
                hasHostOutputs: payload.HostResults != null && payload.HostResults.Count > 0);

            lstOutput.BeginUpdate();
            _suppressHistorySelectionChanged = true;
            try
            {
                var (_, removedIds) = HistoryListCollectionUpdater.InsertNewest(
                    _outputHistory,
                    _historyIndexEntries,
                    indexEntry,
                    _configService.GetCurrent().MaxHistoryEntries);

                lstOutput.ClearSelected();

                foreach (var removedId in removedIds)
                {
                    ClearLoadedHistoryPayload(removedId);
                }
            }
            finally
            {
                _suppressHistorySelectionChanged = false;
                lstOutput.EndUpdate();
            }

            EnableHistorySelectionHandling();
            if (_outputHistory.Count > 0)
            {
                lstOutput.SelectedIndex = 0;
            }
        }

        private void CacheLoadedHistoryPayload(
            string entryId,
            HistoryRunPayload payload,
            bool hasDetails,
            bool hasHostOutputs)
        {
            var previousPayloadChars = EstimatePayloadTextChars(_loadedHistoryPayload);
            _loadedHistoryPayloadId = entryId;
            _loadedHistoryPayload = payload;
            _loadedHistoryPayloadHasDetails = hasDetails;
            _loadedHistoryPayloadHasHostOutputs = hasHostOutputs;
            MaybeCompactAfterPayloadSwap(previousPayloadChars, payload);
        }

        private void EnableHistorySelectionHandling()
        {
            if (_historySelectionHandlingEnabled)
                return;

            _historySelectionHandlingEnabled = true;
        }

        private void ArmHistorySelectionOnIdle(object? sender, EventArgs e)
        {
            if (!_historySelectionArmPending)
            {
                Application.Idle -= ArmHistorySelectionOnIdle;
                return;
            }

            // Ignore any carried-over launch click; arm only after input fully settles.
            if (Control.MouseButtons != MouseButtons.None)
                return;

            _historySelectionArmedAtUtc = DateTime.UtcNow;
            _historySelectionArmPending = false;
            Application.Idle -= ArmHistorySelectionOnIdle;

            if (HistoryStartupSelectionHydration.ShouldHydrateSelectedEntry(
                    _historySelectionHandlingEnabled,
                    lstOutput.SelectedItem is HistoryListItem))
            {
                // A carried-over launch click can update the ListBox selection before
                // history handling is armed. Once input settles, hydrate the visible selection.
                EnableHistorySelectionHandling();
                ApplySelectedHistoryEntry();
            }
        }

        private bool IsHistorySelectionArmed()
        {
            return !_historySelectionArmPending && DateTime.UtcNow >= _historySelectionArmedAtUtc;
        }

        private bool TryLoadHistoryPayload(
            string entryId,
            out HistoryRunPayload payload,
            bool showError = true,
            bool requireDetails = false,
            bool requireHostOutputs = false)
        {
            payload = new HistoryRunPayload();
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            var hasPersistedDetails = _historyIndexEntries.TryGetValue(entryId, out var indexEntry) && indexEntry.HasDetails;
            var hasPersistedHostResults = indexEntry?.HasHostResults ?? false;
            if (string.Equals(_loadedHistoryPayloadId, entryId, StringComparison.Ordinal) &&
                _loadedHistoryPayload != null)
            {
                if (requireDetails && hasPersistedDetails && !_loadedHistoryPayloadHasDetails)
                {
                    // Cached payload was loaded in lightweight mode; reload with details on demand.
                }
                else if (requireHostOutputs && hasPersistedHostResults && !_loadedHistoryPayloadHasHostOutputs)
                {
                    // Cached payload was loaded without host output bodies; reload host outputs on demand.
                }
                else
                {
                    payload = _loadedHistoryPayload;
                    return true;
                }
            }

            if (_historyStorage.TryLoadRunPayload(
                    entryId,
                    out var loadedPayload,
                    includeDetails: requireDetails,
                    includeHostOutputs: requireHostOutputs) && loadedPayload != null)
            {
                CacheLoadedHistoryPayload(
                    entryId,
                    loadedPayload,
                    hasDetails: !hasPersistedDetails || requireDetails,
                    hasHostOutputs: !hasPersistedHostResults || requireHostOutputs);
                payload = loadedPayload;
                return true;
            }

            if (showError)
            {
                DialogTheme.Show(
                    this,
                    "History payload for this run could not be loaded.",
                    "History Load Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return false;
        }

        private static long EstimatePayloadTextChars(HistoryRunPayload? payload)
        {
            if (payload == null)
                return 0;

            long chars = payload.Output?.Length ?? 0;

            if (payload.HostResults != null)
            {
                foreach (var host in payload.HostResults)
                {
                    chars += host?.Output?.Length ?? 0;
                }
            }

            if (payload.Details != null)
            {
                chars += payload.Details.Commands?.Length ?? 0;
                if (payload.Details.Hosts != null)
                {
                    foreach (var host in payload.Details.Hosts)
                    {
                        if (host?.Variables == null)
                            continue;

                        foreach (var kvp in host.Variables)
                        {
                            chars += kvp.Key?.Length ?? 0;
                            chars += kvp.Value?.Length ?? 0;
                        }
                    }
                }

                if (payload.Details.InteractiveSessions != null)
                {
                    foreach (var session in payload.Details.InteractiveSessions)
                    {
                        chars += session?.Transcript?.Length ?? 0;
                    }
                }
            }

            return chars;
        }

        private void MaybeCompactAfterPayloadSwap(long previousPayloadChars, HistoryRunPayload nextPayload)
        {
            var nextPayloadChars = EstimatePayloadTextChars(nextPayload);
            var releasedLargePayload =
                previousPayloadChars >= LargeHistoryPayloadCharThreshold &&
                nextPayloadChars <= SmallHistoryPayloadCharThreshold;
            MaybeRunAutomaticHistoryCompaction(releasedLargePayload);
        }

        private void MaybeCompactAfterPayloadClear(long releasedPayloadChars)
        {
            MaybeRunAutomaticHistoryCompaction(releasedPayloadChars >= LargeHistoryPayloadCharThreshold);
        }

        private void MaybeRunAutomaticHistoryCompaction(bool shouldCompact)
        {
            if (!shouldCompact)
                return;

            var nowUtc = DateTime.UtcNow;
            if (nowUtc - _lastAutomaticHistoryCompactionUtc < AutomaticHistoryCompactionCooldown)
                return;

            _lastAutomaticHistoryCompactionUtc = nowUtc;
            RunGcCompaction();
        }

        private static void RunHistorySwitchGc()
        {
            RunGcCompaction();
        }

        private static void RunGcCompaction()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static HistoryIndexEntry BuildHistoryIndexEntry(string id, string label, HistoryRunPayload payload)
        {
            return new HistoryIndexEntry
            {
                Id = id,
                Label = label,
                CreatedAtUtc = DateTime.UtcNow,
                HasHostResults = payload.HostResults != null && payload.HostResults.Count > 0,
                HasDetails = payload.Details != null,
                RunFileName = $"{id}.json"
            };
        }

        private void InitializeCredentials()
        {
            var config = _configService.GetCurrent();
            _credentialProvider = config.Credentials.UseCredentialManager
                ? new CredentialManagerProvider()
                : null;

            if (_credentialProvider?.IsAvailable == true)
            {
                TryLoadDefaultPassword();
            }
        }

        private bool IsCredentialManagerAvailable => _credentialProvider?.IsAvailable == true;

        private void TryLoadDefaultPassword()
        {
            if (!IsCredentialManagerAvailable)
                return;

            if (_credentialProvider!.TryGetPassword(CredentialTargets.DefaultPasswordTarget, out _, out var password))
            {
                tsbPassword.Text = password;
                txtPassword.Text = password;
            }
        }

        private void StoreDefaultPassword()
        {
            if (!IsCredentialManagerAvailable)
                return;

            _credentialProvider!.SavePassword(CredentialTargets.DefaultPasswordTarget, tsbUsername.Text, tsbPassword.Text);
        }

        private bool TryResolveHostPassword(string hostKey, string username, out string password)
        {
            password = string.Empty;
            if (!IsCredentialManagerAvailable)
                return false;

            var target = CredentialTargets.HostPasswordTarget(hostKey, username);
            return _credentialProvider!.TryGetPassword(target, out _, out password);
        }

        private void StoreHostPassword(string hostKey, string username, string password)
        {
            if (!IsCredentialManagerAvailable)
                return;

            var target = CredentialTargets.HostPasswordTarget(hostKey, username);
            _credentialProvider!.SavePassword(target, username, password);
        }

        private void MigratePasswordsToCredentialManager()
        {
            if (!IsCredentialManagerAvailable)
                return;

            StoreDefaultPassword();

            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                if (row.IsNewRow) continue;

                var hostValue = GetCellValue(row, CsvManager.HostColumnName);
                if (string.IsNullOrWhiteSpace(hostValue))
                    continue;

                var usernameValue = GetCellValue(row, "username");
                var resolvedUsername = string.IsNullOrWhiteSpace(usernameValue) ? tsbUsername.Text : usernameValue;
                var passwordValue = GetCellValue(row, "password");

                if (!string.IsNullOrWhiteSpace(passwordValue))
                {
                    StoreHostPassword(hostValue, resolvedUsername, passwordValue);
                }
            }
        }

        private void InitializeEventHandlers()
        {
            // Form events
            FormClosing += Form1_FormClosing;
            Resize += (_, _) => ReflowTopSectionHeaders();
            topSplitContainer.Resize += (_, _) => ReflowTopSectionHeaders();
            commandSplitContainer.Resize += (_, _) => ReflowTopSectionHeaders();
            scriptPanel.Resize += (_, _) => ReflowScriptHeader();
            scriptHeaderPanel.Resize += (_, _) => ReflowScriptHeader();

            // DataGridView events
            dgv_variables.MouseDown += Dgv_Variables_MouseDown;
            dgv_variables.RowPostPaint += Dgv_Variables_RowPostPaint;
            dgv_variables.CellPainting += Dgv_Variables_CellPainting;
            dgv_variables.CellClick += Dgv_Variables_CellClick;
            dgv_variables.ColumnAdded += Dgv_Variables_ColumnAdded;
            dgv_variables.CellLeave += Dgv_Variables_CellLeave;
            dgv_variables.Leave += Dgv_Variables_Leave;
            dgv_variables.CellValueChanged += Dgv_Variables_CellValueChanged;
            dgv_variables.RowsAdded += Dgv_Variables_RowsAdded;
            dgv_variables.RowsRemoved += Dgv_Variables_RowsRemoved;
            dgv_variables.ColumnRemoved += Dgv_Variables_ColumnRemoved;
            dgv_variables.KeyPress += Dgv_Variables_KeyPress;
            dgv_variables.KeyDown += Dgv_Variables_KeyDown;
            dgv_variables.ColumnHeaderMouseClick += Dgv_Variables_ColumnHeaderMouseClick;
            dgv_variables.CurrentCellDirtyStateChanged += Dgv_Variables_CurrentCellDirtyStateChanged;

            // Preset TreeView events are wired up in Designer
            trvPresets.NodeMouseClick += TrvPresets_NodeMouseClick;
            contextPresetLst.Opening += ContextPresetLst_Opening;
            tsbEnvironment.DropDownItemClicked += TsbEnvironment_DropDownItemClicked;
            tsbManageEnvironments.Click += TsbManageEnvironments_Click;

            // History and host list right-click selection and custom drawing
            lstOutput.MouseDown += LstOutput_MouseDown;
            lstOutput.KeyDown += LstOutput_KeyDown;
            lstOutput.MeasureItem += LstOutput_MeasureItem;
            lstOutput.DrawItem += LstOutput_DrawItem;
            lstHosts.MouseDown += LstHosts_MouseDown;

            // Script editor cursor position tracking
            txtCommand.Click += TxtCommand_CursorPositionChanged;
            txtCommand.KeyUp += TxtCommand_CursorPositionChanged;
            txtCommand.MouseUp += TxtCommand_CursorPositionChanged;
            txtCommand.TextChanged += (_, _) => UpdatePresetHeaderIndicator();
            txtPreset.TextChanged += (_, _) => UpdatePresetHeaderIndicator();
            txtTimeoutHeader.TextChanged += (_, _) => UpdatePresetHeaderIndicator();
            UpdateLinePosition();
        }

        private void InitializeToolbarSync()
        {
            // Sync toolbar username/password with hidden textboxes
            tsbUsername.TextChanged += (s, e) => txtUsername.Text = tsbUsername.Text;
            tsbPassword.TextChanged += (s, e) => txtPassword.Text = tsbPassword.Text;

            // Only allow numeric input in timeout
            txtTimeoutHeader.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                {
                    e.Handled = true;
                }
            };
        }

        private void InitializeEnvironmentToolbar()
        {
            _activeEnvironmentName = _environmentService.GetActiveEnvironmentName();
            _baseEnvironmentName = _environmentService.GetBaseEnvironmentName();
            RefreshEnvironmentSelector(_activeEnvironmentName);
        }

        private void InitializePasswordMasking()
        {
            // Access the internal TextBox of ToolStripTextBox to set password char
            if (tsbPassword.TextBox != null)
            {
                tsbPassword.TextBox.UseSystemPasswordChar = true;
            }
        }

        private void EnableDoubleBuffering()
        {
            // Enable double buffering on controls to reduce flicker during owner-draw
            EnableControlDoubleBuffering(trvPresets);
            EnableControlDoubleBuffering(trvFavorites);
            EnableControlDoubleBuffering(lstOutput);
            EnableControlDoubleBuffering(lstHosts);
        }

        private static void EnableControlDoubleBuffering(Control control)
        {
            // Use reflection to set the protected DoubleBuffered property
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                .SetValue(control, true, null);
        }

        private void RestoreWindowState(AppConfiguration config)
        {
            var ws = config.WindowState;
            var restoreMaximized = ws.IsMaximized;

            if (!restoreMaximized && ws.Width.HasValue && ws.Height.HasValue && ws.Left.HasValue && ws.Top.HasValue)
            {
                // Ensure window is on screen
                var screen = Screen.FromPoint(new Point(ws.Left.Value, ws.Top.Value));
                if (screen != null)
                {
                    StartPosition = FormStartPosition.Manual;
                    Left = Math.Max(screen.WorkingArea.Left, Math.Min(ws.Left.Value, screen.WorkingArea.Right - 100));
                    Top = Math.Max(screen.WorkingArea.Top, Math.Min(ws.Top.Value, screen.WorkingArea.Bottom - 100));
                    Width = Math.Min(ws.Width.Value, screen.WorkingArea.Width);
                    Height = Math.Min(ws.Height.Value, screen.WorkingArea.Height);
                }
            }

            if (restoreMaximized)
            {
                WindowState = FormWindowState.Maximized;
            }

            // Restore splitter positions after load
            Load += (s, e) =>
            {
                if (ws.MainSplitterDistance.HasValue && ws.MainSplitterDistance.Value > 0)
                {
                    try { mainSplitContainer.SplitterDistance = Math.Min(ws.MainSplitterDistance.Value, mainSplitContainer.Height - mainSplitContainer.Panel2MinSize); }
                    catch { /* Ignore invalid splitter distances */ }
                }
                if (ws.TopSplitterDistance.HasValue && ws.TopSplitterDistance.Value > 0)
                {
                    try { topSplitContainer.SplitterDistance = Math.Min(ws.TopSplitterDistance.Value, topSplitContainer.Width - topSplitContainer.Panel2MinSize); }
                    catch { /* Ignore invalid splitter distances */ }
                }
                if (ws.CommandSplitterDistance.HasValue && ws.CommandSplitterDistance.Value > 0)
                {
                    try { commandSplitContainer.SplitterDistance = Math.Min(ws.CommandSplitterDistance.Value, commandSplitContainer.Width - commandSplitContainer.Panel2MinSize); }
                    catch { /* Ignore invalid splitter distances */ }
                }
                if (ws.OutputSplitterDistance.HasValue && ws.OutputSplitterDistance.Value > 0)
                {
                    try { outputSplitContainer.SplitterDistance = Math.Min(ws.OutputSplitterDistance.Value, outputSplitContainer.Width - outputSplitContainer.Panel2MinSize); }
                    catch { /* Ignore invalid splitter distances */ }
                }
                if (ws.HistorySplitterDistance.HasValue && ws.HistorySplitterDistance.Value > 0)
                {
                    try { historySplitContainer.SplitterDistance = Math.Min(ws.HistorySplitterDistance.Value, historySplitContainer.Height - historySplitContainer.Panel2MinSize); }
                    catch { /* Ignore invalid splitter distances */ }
                }

                RestoreInitialEnvironmentState(config);
                ConfigureHistoryListLayout();
            };
        }

        private void RestoreInitialEnvironmentState(AppConfiguration config)
        {
            if (config.Environments != null && config.Environments.Count > 0)
            {
                _activeEnvironmentName = _environmentService.GetActiveEnvironmentName();
                var environment = _environmentService.GetEnvironment(_activeEnvironmentName);
                if (config.RememberState && config.SavedState != null)
                {
                    var mergedState = MergeEnvironmentIntoSavedState(config.SavedState, environment);
                    RestoreApplicationState(mergedState);
                }
                else
                {
                    LoadEnvironmentIntoGrid(environment);
                }
            }
            else if (config.RememberState && config.SavedState != null)
            {
                RestoreApplicationState(config.SavedState);
            }

            RefreshEnvironmentSelector(_activeEnvironmentName);
        }

        private static ApplicationState MergeEnvironmentIntoSavedState(ApplicationState savedState, EnvironmentConfig environment)
        {
            return new ApplicationState
            {
                HostColumns = environment.HostColumns?.ToList() ?? new List<string>(),
                Hosts = environment.Hosts?
                    .Select(row => new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase))
                    .ToList()
                    ?? new List<Dictionary<string, string>>(),
                SelectedHostIndices = environment.SelectedHostIndices?.ToList() ?? new List<int>(),
                LastCsvPath = environment.LastCsvPath,
                LastCsvFingerprint = environment.LastCsvFingerprint?.Clone(),
                SelectedPreset = savedState.SelectedPreset,
                SelectedFolder = savedState.SelectedFolder,
                Username = savedState.Username,
                History = new List<HistoryEntry>()
            };
        }

        private static ExecutionDetails CloneExecutionDetails(ExecutionDetails details)
        {
            return new ExecutionDetails
            {
                PresetName = details.PresetName ?? string.Empty,
                Commands = details.Commands ?? string.Empty,
                PresetType = details.PresetType ?? string.Empty,
                WasCancelled = details.WasCancelled,
                StartTimeUtc = details.StartTimeUtc,
                EndTimeUtc = details.EndTimeUtc,
                EnvironmentName = details.EnvironmentName ?? EnvironmentConfig.DefaultName,
                Username = details.Username ?? string.Empty,
                CommandTimeoutSeconds = details.CommandTimeoutSeconds,
                ConnectionTimeoutSeconds = details.ConnectionTimeoutSeconds,
                UseConnectionPooling = details.UseConnectionPooling,
                RunMode = details.RunMode ?? string.Empty,
                IsFolderExecution = details.IsFolderExecution,
                FolderName = details.FolderName ?? string.Empty,
                ExecutedPresetNames = details.ExecutedPresetNames?.ToList() ?? new List<string>(),
                Hosts = details.Hosts?
                    .Select(host => new SSH_Helper.Models.HostExecutionContext
                    {
                        HostAddress = host.HostAddress ?? string.Empty,
                        Success = host.Success,
                        WasCancelled = host.WasCancelled,
                        TimestampUtc = host.TimestampUtc,
                        Variables = CloneDetailVariables(host.Variables)
                    })
                    .ToList()
                    ?? new List<SSH_Helper.Models.HostExecutionContext>(),
                InteractiveSessions = details.InteractiveSessions?
                    .Select(session => new InteractiveTerminalSessionDetails
                    {
                        SessionNumber = session.SessionNumber,
                        HostAddress = session.HostAddress ?? string.Empty,
                        SessionMode = session.SessionMode ?? string.Empty,
                        EmulationMode = session.EmulationMode ?? string.Empty,
                        StartedAtUtc = session.StartedAtUtc,
                        EndedAtUtc = session.EndedAtUtc,
                        CloseReason = session.CloseReason ?? string.Empty,
                        Completed = session.Completed,
                        Transcript = session.Transcript ?? string.Empty
                    })
                    .ToList()
                    ?? new List<InteractiveTerminalSessionDetails>()
            };
        }

        private static Dictionary<string, string> CloneDetailVariables(Dictionary<string, string>? source)
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source == null || source.Count == 0)
                return variables;

            foreach (var kvp in source)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;

                variables[kvp.Key] = kvp.Value ?? string.Empty;
            }

            return variables;
        }

        private void RefreshEnvironmentSelector(string? preferredEnvironment = null)
        {
            var names = _environmentService.GetEnvironmentNames();
            var target = string.IsNullOrWhiteSpace(preferredEnvironment)
                ? _environmentService.GetActiveEnvironmentName()
                : preferredEnvironment;

            _suppressEnvironmentSelectionChange = true;
            tsbEnvironment.DropDownItems.Clear();
            tsbEnvironment.DropDown.MinimumSize = new Size(tsbEnvironment.Width, 0);
            foreach (var name in names)
            {
                var item = new ToolStripMenuItem(name)
                {
                    Tag = name,
                    Checked = string.Equals(name, target, StringComparison.OrdinalIgnoreCase)
                };
                ApplyEnvironmentMenuItemColor(item, GetEnvironmentLabelColor(name));
                tsbEnvironment.DropDownItems.Add(item);
            }

            int index = names.FindIndex(name => string.Equals(name, target, StringComparison.OrdinalIgnoreCase));
            _activeEnvironmentName = index >= 0 ? names[index] : EnvironmentConfig.DefaultName;
            foreach (ToolStripItem item in tsbEnvironment.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is string name)
                {
                    menuItem.Checked = string.Equals(name, _activeEnvironmentName, StringComparison.OrdinalIgnoreCase);
                }
            }
            tsbEnvironment.Text = _activeEnvironmentName;
            ApplyActiveEnvironmentLabelColor();
            RefreshBaseEnvironmentIndicator();
            _suppressEnvironmentSelectionChange = false;

            UpdateWindowTitle();
        }

        private void RefreshBaseEnvironmentIndicator()
        {
            _baseEnvironmentName = _environmentService.GetBaseEnvironmentName();
            var indicator = BaseEnvironmentIndicatorFormatter.Format(_activeEnvironmentName, _baseEnvironmentName);
            toolStripLabelBaseEnvironment.Text = indicator.Text;
            toolStripLabelBaseEnvironment.Visible = indicator.Visible;
        }

        private PresetBaseEnvironmentResolution ResolveEffectiveBaseEnvironment(string? folderPath)
        {
            _baseEnvironmentName = _environmentService.GetBaseEnvironmentName();
            return PresetBaseEnvironmentResolver.Resolve(_baseEnvironmentName, folderPath, _presetManager.Folders);
        }

        private bool TryApplyFolderEnvironment(string folderPath)
        {
            var resolution = ResolveEffectiveBaseEnvironment(folderPath);
            if (string.Equals(_activeEnvironmentName, resolution.EnvironmentName, StringComparison.OrdinalIgnoreCase))
                return true;

            return TrySwitchEnvironment(resolution.EnvironmentName, out _);
        }

        private int? GetEnvironmentLabelColor(string environmentName)
        {
            try
            {
                return _environmentService.GetEnvironment(environmentName).LabelColor;
            }
            catch
            {
                return null;
            }
        }

        private void ApplyEnvironmentMenuItemColor(ToolStripMenuItem item, int? labelColorArgb)
        {
            if (!labelColorArgb.HasValue)
                return;

            var color = Color.FromArgb(labelColorArgb.Value);
            item.ForeColor = Color.FromArgb(255, color.R, color.G, color.B);
        }

        private void ApplyActiveEnvironmentLabelColor()
        {
            var defaultColor = _isDarkMode ? DarkTextPrimary : LightTextColor;
            var labelColor = GetEnvironmentLabelColor(_activeEnvironmentName);
            var swatchColor = labelColor.HasValue
                ? Color.FromArgb(labelColor.Value)
                : (_isDarkMode ? DarkSurface2 : LightControlBackground);

            tsbEnvironment.Tag = swatchColor.ToArgb();
            tsbEnvironment.Invalidate();
            if (labelColor.HasValue)
            {
                tsbEnvironment.ForeColor = GetContrastColor(swatchColor);
                return;
            }

            tsbEnvironment.ForeColor = defaultColor;
        }

        private void UpdateWindowTitle()
        {
            Text = $"{ApplicationName} {ApplicationVersion} - [{_activeEnvironmentName}]";
        }

        private void EnvironmentService_EnvironmentChanged(object? sender, EnvironmentChangedEventArgs e)
        {
            _activeEnvironmentName = e.CurrentEnvironment;
            _baseEnvironmentName = _environmentService.GetBaseEnvironmentName();
            ApplyActiveEnvironmentLabelColor();
            RefreshBaseEnvironmentIndicator();
            UpdateWindowTitle();

            if (!string.IsNullOrWhiteSpace(_selectedFolderName))
            {
                RefreshSelectedFolderSummary();
            }
        }

        private void TsbEnvironment_DropDownItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            if (_suppressEnvironmentSelectionChange)
                return;

            var clickedItem = e.ClickedItem;
            if (clickedItem?.Tag is not string targetEnvironment)
                return;

            if (string.Equals(targetEnvironment, _activeEnvironmentName, StringComparison.OrdinalIgnoreCase))
                return;

            if (!TrySwitchEnvironment(targetEnvironment, out var switchStatusMessage, updateBaseEnvironment: true))
            {
                RefreshEnvironmentSelector(_activeEnvironmentName);
                return;
            }

            UpdateStatusBar(switchStatusMessage ?? BuildEnvironmentSwitchStatusMessage(includeBaseEnvironment: true));
        }

        private void TsbManageEnvironments_Click(object? sender, EventArgs e)
        {
            EnsureDefaultEnvironmentForFirstAdoption();

            using var dialog = new EnvironmentDialog(_environmentService, _configService, _presetManager, _isDarkMode);
            DialogTheme.SetDialogFont(dialog, _dialogFont);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var targetEnvironment = dialog.SelectedEnvironmentName ?? _environmentService.GetActiveEnvironmentName();
            if (!string.Equals(targetEnvironment, _activeEnvironmentName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TrySwitchEnvironment(targetEnvironment, out var switchStatusMessage, updateBaseEnvironment: true))
                {
                    RefreshEnvironmentSelector(_activeEnvironmentName);
                    return;
                }

                UpdateStatusBar(switchStatusMessage ?? BuildEnvironmentSwitchStatusMessage(includeBaseEnvironment: true));
                return;
            }

            RefreshEnvironmentSelector(_activeEnvironmentName);

            if (!string.IsNullOrWhiteSpace(_selectedFolderName))
            {
                RefreshSelectedFolderSummary();
            }

            UpdateStatusBar($"Active environment: {_activeEnvironmentName}");
        }

        private bool TrySwitchEnvironment(
            string targetEnvironment,
            out string? statusMessage,
            bool updateBaseEnvironment = false)
        {
            statusMessage = null;

            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            SaveCurrentGridToEnvironment(_activeEnvironmentName);

            var environment = _environmentService.GetEnvironment(targetEnvironment);
            var loadedFingerprint = environment.LastCsvFingerprint?.Clone();
            var syncStatus = string.IsNullOrWhiteSpace(environment.LastCsvPath)
                ? CsvFileSyncStatus.NotTracked
                : CsvFileSyncStatus.Current;
            string? syncDetailMessage = null;

            ResolveEnvironmentCsvSyncBeforeSwitch(
                targetEnvironment,
                ref environment,
                ref loadedFingerprint,
                ref syncStatus,
                ref syncDetailMessage);

            environment = _environmentService.SwitchEnvironment(targetEnvironment);
            _activeEnvironmentName = environment.Name;
            if (updateBaseEnvironment)
            {
                _environmentService.SetBaseEnvironment(environment.Name);
            }

            _baseEnvironmentName = _environmentService.GetBaseEnvironmentName();
            var environmentToLoad = syncStatus == CsvFileSyncStatus.Current && loadedFingerprint != null
                ? _environmentService.GetEnvironment(targetEnvironment)
                : environment;
            LoadEnvironmentIntoGrid(environmentToLoad, loadedFingerprint, syncStatus);
            RefreshEnvironmentSelector(_activeEnvironmentName);

            if (updateBaseEnvironment && !string.IsNullOrWhiteSpace(_selectedFolderName))
            {
                RefreshSelectedFolderSummary();
            }

            if (!string.IsNullOrWhiteSpace(syncDetailMessage))
            {
                statusMessage = BuildEnvironmentSwitchStatusMessage(updateBaseEnvironment, syncDetailMessage);
            }

            return true;
        }

        private void ResolveEnvironmentCsvSyncBeforeSwitch(
            string targetEnvironment,
            ref EnvironmentConfig environment,
            ref CsvFileFingerprint? loadedFingerprint,
            ref CsvFileSyncStatus syncStatus,
            ref string? syncDetailMessage)
        {
            var evaluation = CsvFileSyncEvaluator.EvaluateEnvironment(environment, _csvManager);
            var fileName = Path.GetFileName(environment.LastCsvPath ?? string.Empty);

            switch (evaluation.Status)
            {
                case CsvFileSyncStatus.NotTracked:
                    syncStatus = CsvFileSyncStatus.NotTracked;
                    loadedFingerprint = null;
                    return;

                case CsvFileSyncStatus.Current:
                    syncStatus = CsvFileSyncStatus.Current;
                    loadedFingerprint = evaluation.CurrentFingerprint?.Clone() ?? environment.LastCsvFingerprint?.Clone();
                    return;

                case CsvFileSyncStatus.ChangedOnDisk:
                    syncStatus = CsvFileSyncStatus.ChangedOnDisk;
                    loadedFingerprint = environment.LastCsvFingerprint?.Clone();

                    var reloadResult = DialogTheme.Show(
                        this,
                        $"The CSV file '{fileName}' changed on disk since environment '{targetEnvironment}' captured its hosts. Reload hosts from disk now?",
                        "Reload Hosts",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (reloadResult != DialogResult.Yes)
                    {
                        syncDetailMessage = $"Using the saved host snapshot; '{fileName}' changed on disk.";
                        return;
                    }

                    if (TryReloadEnvironmentHostsFromDisk(targetEnvironment, environment.LastCsvPath!, out var reloadedEnvironment, out var reloadError))
                    {
                        environment = reloadedEnvironment;
                        loadedFingerprint = reloadedEnvironment.LastCsvFingerprint?.Clone();
                        syncStatus = CsvFileSyncStatus.Current;
                        syncDetailMessage = $"Reloaded hosts from '{fileName}' because the file changed on disk.";
                        return;
                    }

                    syncDetailMessage = $"Could not reload '{fileName}' from disk ({reloadError}). Using the saved host snapshot.";
                    return;

                case CsvFileSyncStatus.MissingOnDisk:
                    syncStatus = CsvFileSyncStatus.MissingOnDisk;
                    loadedFingerprint = environment.LastCsvFingerprint?.Clone();
                    syncDetailMessage = $"Remembered CSV '{fileName}' is missing on disk.";
                    return;

                default:
                    syncStatus = environment.LastCsvFingerprint != null
                        ? CsvFileSyncStatus.Current
                        : CsvFileSyncStatus.Unknown;
                    loadedFingerprint = environment.LastCsvFingerprint?.Clone();
                    if (!string.IsNullOrWhiteSpace(evaluation.ErrorMessage))
                    {
                        syncDetailMessage = $"Could not verify '{fileName}' against disk ({evaluation.ErrorMessage}).";
                    }
                    return;
            }
        }

        private bool TryReloadEnvironmentHostsFromDisk(
            string environmentName,
            string csvPath,
            out EnvironmentConfig reloadedEnvironment,
            out string? errorMessage)
        {
            reloadedEnvironment = _environmentService.GetEnvironment(environmentName);
            errorMessage = null;

            try
            {
                var dataTable = _csvManager.LoadFromFile(csvPath);
                var hostColumns = dataTable.Columns.Cast<DataColumn>()
                    .Select(column => column.ColumnName)
                    .ToList();
                var hosts = dataTable.Rows.Cast<DataRow>()
                    .Select(row =>
                    {
                        var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var columnName in hostColumns)
                        {
                            rowData[columnName] = row[columnName]?.ToString() ?? string.Empty;
                        }

                        return rowData;
                    })
                    .ToList();
                var fingerprint = CsvFileSyncEvaluator.Capture(csvPath);

                _environmentService.SaveCurrentGridToEnvironment(
                    environmentName,
                    hostColumns,
                    hosts,
                    new List<int>(),
                    csvPath,
                    fingerprint);

                reloadedEnvironment = _environmentService.GetEnvironment(environmentName);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private string BuildEnvironmentSwitchStatusMessage(bool includeBaseEnvironment, string? detail = null)
        {
            var message = includeBaseEnvironment
                ? $"Active environment switched to '{_activeEnvironmentName}'. Base environment set to '{_baseEnvironmentName}'."
                : $"Active environment switched to '{_activeEnvironmentName}'.";

            return string.IsNullOrWhiteSpace(detail)
                ? message
                : $"{message} {detail}";
        }

        private void LoadPresetIntoEditor(string presetName, PresetInfo preset)
        {
            txtCommand.ReadOnly = false;
            txtCommand.Text = preset.Commands;
            txtPreset.Text = presetName;
            txtTimeoutHeader.Text = preset.Timeout.HasValue
                ? preset.Timeout.Value.ToString()
                : string.Empty;
            _activePresetName = presetName;
            _selectedFolderName = null;
            UpdateRunButtonText();
            UpdatePresetHeaderIndicator();

            ApplyPresetEnvironmentOnPresetLoad(presetName, preset);
        }

        private void ApplyPresetEnvironmentOnPresetLoad(string presetName, PresetInfo preset)
        {
            var requestedEnvironment = TryGetScriptDeclaredEnvironment(preset.Commands);
            var effectiveBaseEnvironment = ResolveEffectiveBaseEnvironment(preset.Folder);
            var transition = PresetEnvironmentLoadPlanner.Plan(
                _activeEnvironmentName,
                effectiveBaseEnvironment.EnvironmentName,
                requestedEnvironment);

            if (transition.Kind == PresetEnvironmentLoadActionKind.None)
                return;

            if (transition.Kind == PresetEnvironmentLoadActionKind.RestoreBaseEnvironment)
            {
                if (TrySwitchEnvironment(transition.TargetEnvironment!, out _))
                {
                    UpdateStatusBar(PresetEnvironmentStatusFormatter.FormatRestoreMessage(presetName, effectiveBaseEnvironment));
                }
                return;
            }

            var matchingEnvironment = _environmentService.GetEnvironmentNames()
                .FirstOrDefault(name => string.Equals(name, transition.TargetEnvironment, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(matchingEnvironment))
            {
                UpdateStatusBar(PresetEnvironmentStatusFormatter.FormatMissingEnvironmentMessage(presetName, transition.TargetEnvironment!));
                return;
            }

            if (TrySwitchEnvironment(matchingEnvironment, out _))
            {
                UpdateStatusBar(PresetEnvironmentStatusFormatter.FormatSwitchMessage(presetName, matchingEnvironment));
            }
        }

        private static string? TryGetScriptDeclaredEnvironment(string commandText)
        {
            if (!ScriptParser.IsYamlScript(commandText))
                return null;

            try
            {
                var script = new ScriptParser().Parse(commandText);
                return string.IsNullOrWhiteSpace(script.Environment)
                    ? null
                    : script.Environment.Trim();
            }
            catch (ScriptParseException)
            {
                return null;
            }
        }

        private void EnsureDefaultEnvironmentForFirstAdoption()
        {
            var config = _configService.GetCurrent();
            if (config.Environments.Count > 0)
                return;

            SaveCurrentGridToEnvironment(EnvironmentConfig.DefaultName);
            _activeEnvironmentName = EnvironmentConfig.DefaultName;
            RefreshEnvironmentSelector(_activeEnvironmentName);
        }

        private void SaveCurrentGridToEnvironment(string environmentName)
        {
            var state = BuildApplicationState();
            _environmentService.SaveCurrentGridToEnvironment(
                environmentName,
                state.HostColumns,
                state.Hosts,
                state.SelectedHostIndices,
                state.LastCsvPath,
                state.LastCsvFingerprint);
        }

        private void LoadEnvironmentIntoGrid(
            EnvironmentConfig environment,
            CsvFileFingerprint? loadedFingerprint = null,
            CsvFileSyncStatus syncStatus = CsvFileSyncStatus.Current)
        {
            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            if (dgv_variables.DataSource != null)
            {
                dgv_variables.DataSource = null;
            }

            dgv_variables.Rows.Clear();
            dgv_variables.Columns.Clear();

            _loadedFilePath = environment.LastCsvPath;
            _loadedFileFingerprint = loadedFingerprint?.Clone() ?? environment.LastCsvFingerprint?.Clone();
            _loadedFileSyncStatus = string.IsNullOrWhiteSpace(_loadedFilePath)
                ? CsvFileSyncStatus.NotTracked
                : syncStatus;

            var columns = (environment.HostColumns ?? new List<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name) && name != SelectColumnName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            using (BeginHostGridRestoreScope())
            {
                if (!columns.Contains(CsvManager.HostColumnName, StringComparer.OrdinalIgnoreCase))
                {
                    columns.Insert(0, CsvManager.HostColumnName);
                }

                foreach (var column in columns)
                {
                    dgv_variables.Columns.Add(column, column);
                }

                EnsureSelectColumn();
                dgv_variables.RowTemplate.Height = 28;

                var useCredentialManager = _credentialProvider?.IsAvailable == true &&
                                           _configService.GetCurrent().Credentials.UseCredentialManager;

                foreach (var rowData in environment.Hosts ?? new List<Dictionary<string, string>>())
                {
                    var rowCopy = rowData != null
                        ? new Dictionary<string, string>(rowData, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    if (useCredentialManager)
                    {
                        rowCopy.TryGetValue(CsvManager.HostColumnName, out var hostValue);
                        rowCopy.TryGetValue("username", out var usernameValue);
                        rowCopy.TryGetValue("password", out var passwordValue);

                        var resolvedUsername = string.IsNullOrWhiteSpace(usernameValue) ? tsbUsername.Text : usernameValue;
                        if (!string.IsNullOrWhiteSpace(passwordValue) && !string.IsNullOrWhiteSpace(hostValue))
                        {
                            StoreHostPassword(hostValue, resolvedUsername, passwordValue);
                            rowCopy["password"] = string.Empty;
                        }
                    }

                    var rowIndex = dgv_variables.Rows.Add();
                    dgv_variables.Rows[rowIndex].Height = 28;

                    foreach (var kvp in rowCopy)
                    {
                        if (dgv_variables.Columns.Contains(kvp.Key))
                        {
                            dgv_variables.Rows[rowIndex].Cells[kvp.Key].Value = kvp.Value;
                        }
                    }
                }

                if (environment.SelectedHostIndices != null && dgv_variables.Columns.Contains(SelectColumnName))
                {
                    foreach (var index in environment.SelectedHostIndices)
                    {
                        if (index >= 0 && index < dgv_variables.Rows.Count && !dgv_variables.Rows[index].IsNewRow)
                        {
                            dgv_variables.Rows[index].Cells[SelectColumnName].Value = true;
                        }
                    }
                }

                RequestHostGridHostCountRefresh();
                RequestHostGridScrollbarRefresh();
            }

            _pendingColumnAutoSize = true;
            _csvDirty = false;
            CaptureLoadedFileSnapshotFromGrid();
            UpdateHostsFileIndicator();
        }

        #endregion

        #region UI Helpers

        private CsvFileSyncStatus ResolveLoadedFileSyncStatus()
        {
            if (string.IsNullOrWhiteSpace(_loadedFilePath))
            {
                _loadedFileSyncStatus = CsvFileSyncStatus.NotTracked;
                return _loadedFileSyncStatus;
            }

            if (_loadedFileFingerprint == null)
            {
                return _loadedFileSyncStatus;
            }

            var evaluation = CsvFileSyncEvaluator.Evaluate(_loadedFilePath, _loadedFileFingerprint);
            if (evaluation.Status == CsvFileSyncStatus.Current && evaluation.CurrentFingerprint != null)
            {
                _loadedFileFingerprint = evaluation.CurrentFingerprint.Clone();
            }

            if (evaluation.Status != CsvFileSyncStatus.Unknown)
            {
                _loadedFileSyncStatus = evaluation.Status;
            }

            return _loadedFileSyncStatus;
        }

        private void UpdateHostsFileIndicator()
        {
            lblHostsTitle.Text = $"Hosts: {HostsFileIndicatorFormatter.Format(_loadedFilePath, IsHostsGridUnsaved(), ResolveLoadedFileSyncStatus())}";
        }

        private bool IsHostsGridUnsaved()
        {
            if (string.IsNullOrWhiteSpace(_loadedFilePath))
            {
                return true;
            }

            if (!_csvDirty)
            {
                return false;
            }

            if (_loadedFileSnapshot == null)
            {
                return true;
            }

            return !HostGridUtilities.SnapshotsMatch(HostGridUtilities.BuildSnapshot(dgv_variables), _loadedFileSnapshot);
        }

        private void CaptureLoadedFileSnapshotFromGrid()
        {
            _loadedFileSnapshot = string.IsNullOrWhiteSpace(_loadedFilePath)
                ? null
                : HostGridUtilities.BuildSnapshot(dgv_variables);
        }

        private void UpdatePresetHeaderIndicator()
        {
            var isDirty = IsPresetDirty();
            var currentPresetName = string.IsNullOrWhiteSpace(txtPreset.Text)
                ? _activePresetName
                : txtPreset.Text.Trim();

            lblPresetsTitle.Text = PresetHeaderIndicatorFormatter.Format(
                _selectedFolderName,
                currentPresetName,
                isDirty);
            lblScriptTitle.Text = PresetHeaderIndicatorFormatter.FormatCommandSectionTitle(isDirty);
            btnSavePreset.Text = PresetHeaderIndicatorFormatter.FormatSaveButtonLabel(isDirty);
            ReflowScriptHeader();
        }

        private void UpdateHostCount()
        {
            int count = dgv_variables.Rows.Cast<DataGridViewRow>()
                .Count(r => !r.IsNewRow && !string.IsNullOrWhiteSpace(GetCellValue(r, CsvManager.HostColumnName)));

            string text = count == 1 ? "1 host" : $"{count} hosts";
            lblHostCount.Text = text;
            statusHostCount.Text = text;
            UpdateHostsFileIndicator();
        }

        private int GetCheckedHostCount()
        {
            // Guard: column may not exist during initialization or when loading saved state
            if (!dgv_variables.Columns.Contains(SelectColumnName))
                return 0;

            return dgv_variables.Rows.Cast<DataGridViewRow>()
                .Count(r => !r.IsNewRow &&
                            r.Cells[SelectColumnName].Value is true);
        }

        private void UpdateSelectionCount()
        {
            int checkedCount = GetCheckedHostCount();
            int totalCount = dgv_variables.Rows.Cast<DataGridViewRow>()
                .Count(r => !r.IsNewRow && !string.IsNullOrWhiteSpace(GetCellValue(r, CsvManager.HostColumnName)));

            if (checkedCount > 0)
            {
                string text = $"{checkedCount} of {totalCount} selected";
                lblHostCount.Text = text;
                statusHostCount.Text = text;
            }
            else
            {
                string text = totalCount == 1 ? "1 host" : $"{totalCount} hosts";
                lblHostCount.Text = text;
                statusHostCount.Text = text;
            }

            UpdateHostsFileIndicator();
            UpdateRunButtonText();
        }

        private void SetAllCheckboxes(bool value)
        {
            if (!dgv_variables.Columns.Contains(SelectColumnName))
                return;

            _selectAllChecked = value;
            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                if (!row.IsNewRow)
                {
                    row.Cells[SelectColumnName].Value = value;
                }
            }
            dgv_variables.InvalidateColumn(dgv_variables.Columns[SelectColumnName]!.Index);
            UpdateSelectionCount();
        }

        private void EnsureSelectColumn()
        {
            // Add checkbox column if it doesn't exist (e.g., after loading CSV or restoring state)
            if (dgv_variables.Columns.Contains(SelectColumnName))
                return;

            dgv_variables.Columns.Insert(0, CreateSelectColumn());
            _selectAllChecked = false;
        }

        private void UpdateStatusBar(string message, bool showProgress = false, int progress = 0, int total = 0)
        {
            statusLabel.Text = message;
            statusProgress.Visible = showProgress;
            if (showProgress && total > 0)
            {
                statusProgress.Maximum = total;
                statusProgress.Value = Math.Min(progress, total);
            }
        }

        private int BeginConnectionTestProgress(int totalConnections)
        {
            int runId = unchecked(++_connectionTestProgressRunId);
            statusProgress.Visible = true;
            statusProgress.Maximum = totalConnections;
            statusProgress.Value = 0;
            return runId;
        }

        private void UpdateConnectionTestProgress(int runId, int completedConnections, int totalConnections)
        {
            if (!_isTestingConnections || runId != _connectionTestProgressRunId)
            {
                return;
            }

            statusProgress.Value = Math.Min(completedConnections, totalConnections);
            UpdateStatusBar($"Testing connections... {completedConnections} of {totalConnections}");
        }

        private void InvalidateConnectionTestProgress()
        {
            unchecked
            {
                _connectionTestProgressRunId++;
            }
        }

        private static Color GetConnectionTestHeaderTextColor(Color backgroundColor)
        {
            int weightedBrightness = ((backgroundColor.R * 299) + (backgroundColor.G * 587) + (backgroundColor.B * 114)) / 1000;
            return weightedBrightness >= 140 ? LightTextColor : Color.White;
        }

        private static (Color CellBackColor, Color CellForeColor, Color HeaderBackColor, Color HeaderForeColor) GetConnectionTestPalette(
            ConnectionTestVisualState state,
            bool darkMode)
        {
            Color cellBackColor;
            Color cellForeColor;

            switch (state)
            {
                case ConnectionTestVisualState.Testing:
                    cellBackColor = darkMode ? Color.FromArgb(92, 67, 0) : Color.FromArgb(255, 243, 205);
                    cellForeColor = darkMode ? Color.FromArgb(255, 231, 153) : Color.FromArgb(102, 77, 3);
                    break;
                case ConnectionTestVisualState.Success:
                    cellBackColor = darkMode ? Color.FromArgb(30, 70, 40) : Color.FromArgb(212, 237, 218);
                    cellForeColor = darkMode ? Color.FromArgb(180, 230, 180) : Color.Empty;
                    break;
                case ConnectionTestVisualState.Failure:
                    cellBackColor = darkMode ? Color.FromArgb(80, 30, 30) : Color.FromArgb(248, 215, 218);
                    cellForeColor = darkMode ? Color.FromArgb(230, 150, 150) : Color.Empty;
                    break;
                default:
                    return (Color.Empty, Color.Empty, Color.Empty, Color.Empty);
            }

            var headerBackColor = cellBackColor;
            var headerForeColor = GetConnectionTestHeaderTextColor(headerBackColor);
            return (cellBackColor, cellForeColor, headerBackColor, headerForeColor);
        }

        private int GetHostIpColumnIndex()
        {
            return dgv_variables.Columns["Host_IP"]?.Index ?? -1;
        }

        private void SetConnectionTestVisualState(
            DataGridViewRow row,
            int hostIpColIndex,
            ConnectionTestVisualState state,
            string toolTipText)
        {
            if (hostIpColIndex < 0 || row.IsNewRow)
            {
                return;
            }

            _connectionTestRowStates.Remove(row);
            if (state != ConnectionTestVisualState.None)
            {
                _connectionTestRowStates.Add(row, new ConnectionTestRowVisualStateInfo(state, toolTipText));
            }

            ApplyConnectionTestVisualState(row, hostIpColIndex);
        }

        private void ApplyConnectionTestVisualState(DataGridViewRow row, int hostIpColIndex)
        {
            if (hostIpColIndex < 0 || row.IsNewRow)
            {
                return;
            }

            var cell = row.Cells[hostIpColIndex];
            if (!_connectionTestRowStates.TryGetValue(row, out var visualState) ||
                visualState.State == ConnectionTestVisualState.None)
            {
                cell.Style.BackColor = Color.Empty;
                cell.Style.ForeColor = Color.Empty;
                cell.ToolTipText = string.Empty;
                row.HeaderCell.Style.BackColor = Color.Empty;
                row.HeaderCell.Style.ForeColor = Color.Empty;
                if (row.Index >= 0)
                {
                    dgv_variables.InvalidateRow(row.Index);
                }
                return;
            }

            var palette = GetConnectionTestPalette(visualState.State, _isDarkMode);
            cell.Style.BackColor = palette.CellBackColor;
            cell.Style.ForeColor = palette.CellForeColor;
            cell.ToolTipText = visualState.ToolTipText;
            row.HeaderCell.Style.BackColor = palette.HeaderBackColor;
            row.HeaderCell.Style.ForeColor = palette.HeaderForeColor;

            if (row.Index >= 0)
            {
                dgv_variables.InvalidateRow(row.Index);
            }
        }

        private void ClearConnectionTestVisualState(DataGridViewRow row, int hostIpColIndex)
        {
            SetConnectionTestVisualState(row, hostIpColIndex, ConnectionTestVisualState.None, string.Empty);
        }

        private void ReapplyConnectionTestVisualStates()
        {
            int hostIpColIndex = GetHostIpColumnIndex();
            if (hostIpColIndex < 0)
            {
                return;
            }

            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                ApplyConnectionTestVisualState(row, hostIpColIndex);
            }
        }

        private void ApplyConnectionTestCellResult(DataGridViewRow row, int hostIpColIndex, Models.ConnectionTestResult result)
        {
            if (hostIpColIndex < 0)
            {
                return;
            }

            if (result.Success)
            {
                SetConnectionTestVisualState(
                    row,
                    hostIpColIndex,
                    ConnectionTestVisualState.Success,
                    $"Reachable ({result.LatencyMs}ms)");
            }
            else
            {
                string errorCategory = string.IsNullOrWhiteSpace(result.ErrorCategory) ? "Error" : result.ErrorCategory;
                string errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Connection test failed" : result.ErrorMessage;
                SetConnectionTestVisualState(
                    row,
                    hostIpColIndex,
                    ConnectionTestVisualState.Failure,
                    $"{errorCategory}: {errorMessage}");
            }
        }

        private IProgress<FolderExecutionProgress>? BeginManualExecutionProgress(int totalOperations)
        {
            _manualExecutionCompletedOperations = 0;

            if (!ManualExecutionStatusProgress.ShouldShowProgress(totalOperations))
                return null;

            int runId = unchecked(++_manualExecutionProgressRunId);
            UpdateStatusBar("Running... 0%", true, 0, totalOperations);
            return new Progress<FolderExecutionProgress>(progress => UpdateManualExecutionProgress(runId, progress));
        }

        private void UpdateManualExecutionProgress(int runId, FolderExecutionProgress progress)
        {
            if (runId != _manualExecutionProgressRunId ||
                !ManualExecutionStatusProgress.ShouldShowProgress(progress.TotalOperations))
            {
                return;
            }

            var state = ManualExecutionStatusProgress.Advance(_manualExecutionCompletedOperations, progress);
            _manualExecutionCompletedOperations = state.CompletedOperations;

            UpdateStatusBar(
                state.StatusText,
                showProgress: true,
                progress: state.CompletedOperations,
                total: state.TotalOperations);
        }

        private void EndManualExecutionProgress()
        {
            _manualExecutionCompletedOperations = 0;
            unchecked
            {
                _manualExecutionProgressRunId++;
            }
        }

        /// <summary>
        /// Logs a timestamped debug message to the output window when SSH Debug mode is enabled.
        /// </summary>
        private void SshDebugLog(string phase, string message, System.Diagnostics.Stopwatch? stopwatch = null)
        {
            if (!debugModeToolStripMenuItem.Checked) return;

            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var elapsed = stopwatch != null ? $" (+{stopwatch.ElapsedMilliseconds}ms)" : "";
            var debugLine = $"[DEBUG {timestamp}]{elapsed} {phase}: {message}\r\n";
            AppendOutputText(debugLine);
        }

        #region Theme

        // Light theme colors (clean, modern light theme)
        private static readonly Color LightBackground = Color.FromArgb(248, 249, 250);
        private static readonly Color LightPanelBackground = Color.White;
        private static readonly Color LightTextColor = Color.FromArgb(33, 37, 41);
        private static readonly Color LightSecondaryText = Color.FromArgb(108, 117, 125);
        private static readonly Color LightBorderColor = Color.FromArgb(222, 226, 230);
        private static readonly Color LightControlBackground = Color.FromArgb(253, 253, 253);
        private static readonly Color LightAlternateRow = Color.FromArgb(248, 249, 250);
        private static readonly Color LightFormBackground = Color.FromArgb(233, 236, 239);
        private static readonly Color LightAccent = DialogTheme.GridLightSelection;
        private static readonly Color LightSelectionBorder = Color.FromArgb(10, 88, 202);  // Darker accent for border
        private static readonly Color SchedulerStatusLinkLight = Color.FromArgb(36, 120, 214);
        private static readonly Color SchedulerStatusLinkLightActive = Color.FromArgb(58, 138, 226);

        // Dark theme colors (VS Code inspired - professional and easy on the eyes)
        private static readonly Color DarkSurface0 = Color.FromArgb(24, 24, 24);      // Deepest background
        private static readonly Color DarkSurface1 = Color.FromArgb(30, 30, 30);      // Main panel background
        private static readonly Color DarkSurface2 = Color.FromArgb(37, 37, 38);      // Elevated surfaces
        private static readonly Color DarkSurface3 = Color.FromArgb(45, 45, 46);      // Headers, toolbars
        private static readonly Color DarkTextPrimary = Color.FromArgb(204, 204, 204);    // Primary text
        private static readonly Color DarkTextSecondary = Color.FromArgb(128, 128, 128);  // Secondary/muted text
        private static readonly Color DarkBorder = Color.FromArgb(48, 48, 48);            // Subtle borders
        private static readonly Color DarkSelectionBg = DialogTheme.GridDarkSelection;     // Shared selection color
        private static readonly Color DarkSelectionBorder = Color.FromArgb(0, 122, 204); // Selection border accent
        private static readonly Color SchedulerStatusLinkDark = Color.FromArgb(92, 171, 226);
        private static readonly Color SchedulerStatusLinkDarkActive = Color.FromArgb(115, 186, 236);
        private static readonly Color DarkInputBackground = Color.FromArgb(60, 60, 60);   // Input fields
        private static readonly Color DarkInputText = Color.FromArgb(220, 220, 220);      // Input text

        // Track current theme for owner-draw methods
        private bool _isDarkMode;

        private void ApplyTheme(bool darkMode)
        {
            _isDarkMode = darkMode;
            SuspendLayout();

            if (darkMode)
            {
                ApplyDarkTheme();
            }
            else
            {
                ApplyLightTheme();
            }

            ConfigureHistoryListLayout();

            // Set up owner-draw for TreeViews (both light and dark mode for consistent selection visibility)
            trvPresets.DrawMode = TreeViewDrawMode.OwnerDrawAll;
            trvPresets.DrawNode -= TreeView_DrawNode;
            trvPresets.DrawNode += TreeView_DrawNode;

            trvFavorites.DrawMode = TreeViewDrawMode.OwnerDrawAll;
            trvFavorites.DrawNode -= TreeView_DrawNode;
            trvFavorites.DrawNode += TreeView_DrawNode;

            // Update custom DataGridView scrollbar colors
            ApplyScrollbarColors();
            ApplyActiveEnvironmentLabelColor();
            ReapplyConnectionTestVisualStates();

            ResumeLayout(true);
        }

        // Fonts created by ApplyFontSettings — disposed on next call or in Form1.Dispose
        private List<Font> _managedFonts = new();
        private Font? _dialogFont;

        private static string ResolveSemiboldFontFamily(string? uiFontFamily)
        {
            if (string.IsNullOrWhiteSpace(uiFontFamily))
            {
                return Models.FontSettings.DefaultUIFontFamily;
            }

            return uiFontFamily.EndsWith("Semibold", StringComparison.OrdinalIgnoreCase)
                ? uiFontFamily
                : $"{uiFontFamily} Semibold";
        }

        private void ApplyFontSettings(Models.FontSettings fontSettings)
        {
            SuspendLayout();

            // Collect previous fonts for disposal after all controls are reassigned
            var previousFonts = _managedFonts;
            _managedFonts = new List<Font>();

            var uiFont = fontSettings.UIFontFamily;
            var codeFont = fontSettings.CodeFontFamily;
            var scale = fontSettings.GlobalScaleFactor;
            var semiboldUiFont = ResolveSemiboldFontFamily(uiFont);

            // Helper to apply scaling
            float Scaled(float size) => size * scale;

            // Section titles (Semibold)
            var sectionTitleFont = new Font(semiboldUiFont, Scaled(fontSettings.SectionTitleFontSize), FontStyle.Bold);
            _managedFonts.Add(sectionTitleFont);
            lblHostsTitle.Font = sectionTitleFont;
            lblPresetsTitle.Font = sectionTitleFont;
            lblScriptTitle.Font = sectionTitleFont;
            lblHistoryTitle.Font = sectionTitleFont;
            lblHostsListTitle.Font = sectionTitleFont;

            // Tree views
            var treeFont = new Font(uiFont, Scaled(fontSettings.TreeViewFontSize));
            _managedFonts.Add(treeFont);
            trvPresets.Font = treeFont;
            trvFavorites.Font = treeFont;

            // Apply custom row height for tree views if specified (0 = auto based on font)
            if (fontSettings.TreeViewRowHeight > 0)
            {
                trvPresets.ItemHeight = fontSettings.TreeViewRowHeight;
                trvFavorites.ItemHeight = fontSettings.TreeViewRowHeight;
            }
            else
            {
                // Calculate height based on font (font height + padding)
                int fontHeight;
                try
                {
                    fontHeight = treeFont.Height;
                }
                catch (ArgumentException)
                {
                    // GDI+ can fail with "Parameter is not valid" during font transitions;
                    // approximate from point size (1pt ≈ 1.333px at 96 DPI, plus internal leading)
                    fontHeight = (int)Math.Ceiling(Scaled(fontSettings.TreeViewFontSize) * 1.6f);
                }
                var autoHeight = fontHeight + 4;
                trvPresets.ItemHeight = autoHeight;
                trvFavorites.ItemHeight = autoHeight;
            }

            // Empty labels
            var emptyLabelFont = new Font(uiFont, Scaled(fontSettings.EmptyLabelFontSize));
            _managedFonts.Add(emptyLabelFont);
            lblFavoritesEmpty.Font = emptyLabelFont;

            // Execute buttons (Semibold)
            var execButtonFont = new Font(semiboldUiFont, Scaled(fontSettings.ExecuteButtonFontSize), FontStyle.Bold);
            _managedFonts.Add(execButtonFont);
            btnExecuteAll.Font = execButtonFont;
            btnExecuteSelected.Font = execButtonFont;
            btnStopAll.Font = execButtonFont;

            // General buttons
            var buttonFont = new Font(uiFont, Scaled(fontSettings.ButtonFontSize));
            _managedFonts.Add(buttonFont);
            btnSavePreset.Font = buttonFont;

            // Code editor
            var codeEditorFont = new Font(codeFont, Scaled(fontSettings.CodeEditorFontSize));
            _managedFonts.Add(codeEditorFont);
            txtCommand.Font = codeEditorFont;
            txtCommand.WordWrap = fontSettings.CodeEditorWordWrap;

            // Output area
            var outputFont = new Font(codeFont, Scaled(fontSettings.OutputAreaFontSize));
            _managedFonts.Add(outputFont);
            txtOutput.Font = outputFont;
            txtOutput.WordWrap = fontSettings.OutputAreaWordWrap;


            // Tab controls
            var tabFont = new Font(uiFont, Scaled(fontSettings.TabFontSize));
            _managedFonts.Add(tabFont);
            presetsTabControl.Font = tabFont;
            presetsTabHeaderStrip.Font = tabFont;
            presetsTabHeaderStrip.Height = Math.Max(24, tabFont.Height + 10);
            UpdatePresetsTabViewportLayout();

            // Host list (DataGridView) - apply row height setting
            // Don't change font on DataGridView as it interferes with existing styling
            var hostRowHeight = fontSettings.HostListRowHeight > 0 ? fontSettings.HostListRowHeight : 28;
            dgv_variables.RowTemplate.Height = hostRowHeight;
            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                row.Height = hostRowHeight;
            }

            // History list boxes
            var listFont = new Font(uiFont, Scaled(fontSettings.HostListFontSize));
            _managedFonts.Add(listFont);
            lstOutput.Font = listFont;
            lstHosts.Font = listFont;
            ConfigureHistoryListLayout();

            // Menu strip
            var menuFont = new Font(uiFont, Scaled(fontSettings.MenuFontSize));
            _managedFonts.Add(menuFont);
            menuStrip1.Font = menuFont;
            ApplyMenuFontRecursive(menuStrip1.Items, menuFont);

            // Context menus
            ApplyContextMenuFont(contextMenuStrip1, uiFont, Scaled(fontSettings.MenuFontSize));
            ApplyContextMenuFont(contextPresetLst, uiFont, Scaled(fontSettings.MenuFontSize));
            ApplyContextMenuFont(contextPresetLstAdd, uiFont, Scaled(fontSettings.MenuFontSize));
            ApplyContextMenuFont(contextHistoryLst, uiFont, Scaled(fontSettings.MenuFontSize));
            ApplyContextMenuFont(contextHostLst, uiFont, Scaled(fontSettings.MenuFontSize));

            // Toolstrips
            var toolStripFont = new Font(uiFont, Scaled(fontSettings.ButtonFontSize));
            _managedFonts.Add(toolStripFont);
            mainToolStrip.Font = toolStripFont;
            presetsToolStrip.Font = toolStripFont;
            ReflowTopBarsForCurrentFont();

            // Status bar
            var statusFont = new Font(uiFont, Scaled(fontSettings.StatusBarFontSize));
            _managedFonts.Add(statusFont);
            statusStrip.Font = statusFont;

            // Dialog font (used by themed confirmation dialogs)
            _dialogFont = new Font(uiFont, Scaled(fontSettings.DialogFontSize));
            _managedFonts.Add(_dialogFont);

            // Apply accent color if custom
            ApplyAccentColor(fontSettings.CustomAccentColor);

            ResumeLayout(true);

            // Defer disposal so any pending WM_PAINT messages (queued by ApplyTheme's
            // Refresh or by ResumeLayout) are processed while old fonts are still valid.
            if (previousFonts.Count > 0)
            {
                var fontsToDispose = previousFonts;
                var currentFonts = _managedFonts;
                BeginInvoke(() =>
                {
                    foreach (var font in fontsToDispose)
                    {
                        // GDI+ may share native handles between Font objects with identical
                        // parameters. Disposing an old font would invalidate any new font
                        // that shares the same handle (e.g. when resetting to defaults
                        // produces the same font settings that were already active).
                        bool sharedHandle = currentFonts.Any(f =>
                            f.Name == font.Name &&
                            f.Size == font.Size &&
                            f.Style == font.Style);
                        if (!sharedHandle)
                        {
                            try { font.Dispose(); } catch { }
                        }
                    }
                });
            }
        }

        private void ReflowTopBarsForCurrentFont()
        {
            static int CalculateStripHeight(Font font, int minHeight, int verticalPadding)
            {
                var textHeight = TextRenderer.MeasureText("Hg", font).Height;
                return Math.Max(minHeight, textHeight + verticalPadding);
            }

            menuStrip1.AutoSize = false;
            menuStrip1.Height = CalculateStripHeight(menuStrip1.Font, minHeight: 24, verticalPadding: 8);

            mainToolStrip.AutoSize = false;
            mainToolStrip.Height = CalculateStripHeight(mainToolStrip.Font, minHeight: 25, verticalPadding: 10);
            var mainItemHeight = Math.Max(22, mainToolStrip.Height - mainToolStrip.Padding.Vertical - 2);

            tsbUsername.AutoSize = false;
            tsbUsername.Size = new Size(tsbUsername.Width, mainItemHeight);
            tsbPassword.AutoSize = false;
            tsbPassword.Size = new Size(tsbPassword.Width, mainItemHeight);

            tsbEnvironment.AutoSize = false;
            tsbEnvironment.Size = new Size(tsbEnvironment.Width, mainItemHeight);

            toolStripSeparator1.AutoSize = false;
            toolStripSeparator1.Size = new Size(toolStripSeparator1.Width, mainItemHeight);
            toolStripSeparator2.AutoSize = false;
            toolStripSeparator2.Size = new Size(toolStripSeparator2.Width, mainItemHeight);
            toolStripSeparatorEnv.AutoSize = false;
            toolStripSeparatorEnv.Size = new Size(toolStripSeparatorEnv.Width, mainItemHeight);

            presetsToolStrip.AutoSize = false;
            presetsToolStrip.Height = CalculateStripHeight(presetsToolStrip.Font, minHeight: 25, verticalPadding: 10);
            var presetItemHeight = Math.Max(22, presetsToolStrip.Height - presetsToolStrip.Padding.Vertical - 2);
            tsbSeparatorFolders.AutoSize = false;
            tsbSeparatorFolders.Size = new Size(tsbSeparatorFolders.Width, presetItemHeight);

            ReflowMainChromeBounds();
            ReflowTopSectionHeaders();
        }

        private void ReflowMainChromeBounds()
        {
            menuStrip1.Dock = DockStyle.None;
            mainToolStrip.Dock = DockStyle.None;
            mainSplitContainer.Dock = DockStyle.None;

            menuStrip1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            mainToolStrip.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            mainSplitContainer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Size = new Size(ClientSize.Width, menuStrip1.Height);

            mainToolStrip.Location = new Point(0, menuStrip1.Bottom);
            mainToolStrip.Size = new Size(ClientSize.Width, mainToolStrip.Height);

            var statusHeight = statusStrip.Visible ? statusStrip.Height : 0;
            var contentTop = mainToolStrip.Bottom;
            mainSplitContainer.Location = new Point(0, contentTop);
            mainSplitContainer.Size = new Size(
                ClientSize.Width,
                Math.Max(0, ClientSize.Height - statusHeight - contentTop));

            menuStrip1.BringToFront();
            mainToolStrip.BringToFront();
            statusStrip.BringToFront();
        }

        private void ReflowTopSectionHeaders()
        {
            ReflowHostsHeader();
            ReflowTitleOnlyHeader(presetsHeaderPanel, lblPresetsTitle, minHeight: 32);
            ReflowScriptHeader();
        }

        private void ReflowHostsHeader()
        {
            lblHostCount.AutoSize = true;

            var titleHeight = lblHostsTitle.PreferredHeight;
            var countHeight = lblHostCount.PreferredHeight;
            var contentHeight = Math.Max(titleHeight, countHeight);
            hostsHeaderPanel.Height = Math.Max(36, hostsHeaderPanel.Padding.Vertical + contentHeight);

            var contentTop = hostsHeaderPanel.Padding.Top +
                             Math.Max(0, (hostsHeaderPanel.ClientSize.Height - hostsHeaderPanel.Padding.Vertical - contentHeight) / 2);
            var countY = contentTop + Math.Max(0, (contentHeight - lblHostCount.Height) / 2);
            lblHostCount.Location = new Point(
                Math.Max(hostsHeaderPanel.Padding.Left, hostsHeaderPanel.ClientSize.Width - hostsHeaderPanel.Padding.Right - lblHostCount.Width),
                countY);

            var titleWidth = Math.Max(0, lblHostCount.Left - hostsHeaderPanel.Padding.Left - 8);
            var titleY = contentTop + Math.Max(0, (contentHeight - titleHeight) / 2);
            lblHostsTitle.AutoSize = false;
            lblHostsTitle.Location = new Point(hostsHeaderPanel.Padding.Left, titleY);
            lblHostsTitle.Size = new Size(titleWidth, titleHeight);
        }

        private void ReflowTitleOnlyHeader(Panel panel, Label titleLabel, int minHeight)
        {
            var titleHeight = titleLabel.PreferredHeight;
            panel.Height = Math.Max(minHeight, panel.Padding.Vertical + titleHeight);

            var titleY = panel.Padding.Top +
                         Math.Max(0, (panel.ClientSize.Height - panel.Padding.Vertical - titleHeight) / 2);
            titleLabel.AutoSize = false;
            titleLabel.Location = new Point(panel.Padding.Left, titleY);
            titleLabel.Size = new Size(
                Math.Max(0, panel.ClientSize.Width - panel.Padding.Horizontal),
                titleHeight);
        }

        private void ReflowScriptHeader()
        {
            var titleHeight = lblScriptTitle.PreferredHeight;
            var contentLeft = scriptHeaderPanel.Padding.Left;
            var contentRight = Math.Max(contentLeft, scriptHeaderPanel.ClientSize.Width - scriptHeaderPanel.Padding.Right);

            // Single top row: Name + preset box + timeout + save.
            var rowY = scriptHeaderPanel.Padding.Top + 3;

            btnSavePreset.AutoSize = false;
            btnSavePreset.Location = new Point(
                Math.Max(contentLeft, contentRight - btnSavePreset.Width),
                rowY - 1);

            txtTimeoutHeader.Location = new Point(
                Math.Max(contentLeft, btnSavePreset.Left - txtTimeoutHeader.Width - 6),
                rowY);

            lblTimeoutHeader.AutoSize = true;
            lblTimeoutHeader.Location = new Point(
                Math.Max(contentLeft, txtTimeoutHeader.Left - lblTimeoutHeader.Width - 6),
                rowY + Math.Max(0, (txtTimeoutHeader.Height - lblTimeoutHeader.Height) / 2));

            lblPresetName.AutoSize = true;
            lblPresetName.Location = new Point(contentLeft, rowY + Math.Max(0, (txtPreset.Height - lblPresetName.Height) / 2));

            var presetLeft = lblPresetName.Right + 6;
            var presetRight = lblTimeoutHeader.Left - 8;
            txtPreset.Location = new Point(presetLeft, rowY);
            txtPreset.Width = Math.Max(20, presetRight - presetLeft);

            var firstRowBottom = new Control[] { lblPresetName, txtPreset, lblTimeoutHeader, txtTimeoutHeader, btnSavePreset }
                .Where(control => control.Visible)
                .Select(control => control.Bottom)
                .DefaultIfEmpty(scriptHeaderPanel.Padding.Top)
                .Max();

            lblScriptTitle.AutoSize = false;
            lblScriptTitle.Location = new Point(scriptHeaderPanel.Padding.Left, firstRowBottom + 6);
            lblScriptTitle.Size = new Size(
                Math.Max(0, scriptHeaderPanel.ClientSize.Width - scriptHeaderPanel.Padding.Horizontal),
                titleHeight);

            scriptHeaderPanel.Height = Math.Max(60, lblScriptTitle.Bottom + scriptHeaderPanel.Padding.Bottom);
            scriptHeaderPanel.Invalidate();
        }

        private void ApplyColumnAutoResize(bool autoResize)
        {
            if (autoResize)
            {
                dgv_variables.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            }
            else
            {
                // Capture current column widths before disabling auto-resize
                var columnWidths = new Dictionary<string, int>();
                foreach (DataGridViewColumn column in dgv_variables.Columns)
                {
                    columnWidths[column.Name] = column.Width;
                }

                dgv_variables.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                // Restore the widths that were set during auto-resize
                foreach (DataGridViewColumn column in dgv_variables.Columns)
                {
                    if (columnWidths.TryGetValue(column.Name, out int width))
                    {
                        column.Width = width;
                    }
                }
            }
        }

        /// <summary>
        /// Performs a one-time auto-size of columns to fit their content.
        /// This is called when loading data (CSV import or state restore) to size columns appropriately,
        /// regardless of the AutoResizeHostColumns setting.
        /// </summary>
        private void AutoSizeColumnsToContent()
        {
            // Skip if auto-resize is already enabled (it will handle sizing automatically)
            if (dgv_variables.AutoSizeColumnsMode == DataGridViewAutoSizeColumnsMode.AllCells)
                return;

            // Skip if no columns
            if (dgv_variables.Columns.Count == 0)
                return;

            // Auto-resize each column individually to fit content
            foreach (DataGridViewColumn column in dgv_variables.Columns)
            {
                dgv_variables.AutoResizeColumn(column.Index, DataGridViewAutoSizeColumnMode.AllCells);
            }
        }

        private void ApplyMenuFontRecursive(ToolStripItemCollection items, Font font)
        {
            foreach (ToolStripItem item in items)
            {
                item.Font = font;
                if (item is ToolStripMenuItem menuItem && menuItem.DropDownItems.Count > 0)
                {
                    ApplyMenuFontRecursive(menuItem.DropDownItems, font);
                }
            }
        }

        private void ApplyContextMenuFont(ContextMenuStrip? menu, string fontFamily, float fontSize)
        {
            if (menu == null) return;
            var font = new Font(fontFamily, fontSize);
            _managedFonts.Add(font);
            menu.Font = font;
            foreach (ToolStripItem item in menu.Items)
            {
                item.Font = font;
            }
        }

        private void ApplyAccentColor(int? accentColorArgb)
        {
            if (!accentColorArgb.HasValue) return;

            var accentColor = Color.FromArgb(accentColorArgb.Value);
            var contrastColor = GetContrastColor(accentColor);

            // Apply accent to execute buttons
            btnExecuteAll.BackColor = accentColor;
            btnExecuteAll.ForeColor = contrastColor;
            btnExecuteAll.FlatStyle = FlatStyle.Flat;
            btnExecuteAll.FlatAppearance.BorderSize = 0;

            btnExecuteSelected.BackColor = accentColor;
            btnExecuteSelected.ForeColor = contrastColor;
            btnExecuteSelected.FlatStyle = FlatStyle.Flat;
            btnExecuteSelected.FlatAppearance.BorderSize = 0;
        }

        private static Color GetContrastColor(Color color)
        {
            var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return luminance > 0.5 ? Color.Black : Color.White;
        }

        private void ApplyLightTheme()
        {
            // Apply light title bar (Windows 10 1809+ / Windows 11)
            int value = 0;
            _ = NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));

            // Form
            BackColor = LightFormBackground;

            // Menu and toolbar
            menuStrip1.BackColor = LightBackground;
            menuStrip1.ForeColor = LightTextColor;
            mainToolStrip.BackColor = LightBackground;
            mainToolStrip.ForeColor = LightTextColor;
            statusStrip.BackColor = LightBackground;
            statusStrip.ForeColor = LightTextColor;
            statusLabel.ForeColor = LightTextColor;
            statusHostCount.ForeColor = LightSecondaryText;
            ApplySchedulerStatusBarTheme();

            // Hosts panel
            hostsPanel.BackColor = LightPanelBackground;
            hostsHeaderPanel.BackColor = LightBackground;
            lblHostsTitle.ForeColor = LightTextColor;
            lblHostCount.ForeColor = LightSecondaryText;

            // DataGridView
            dgv_variables.BackgroundColor = LightPanelBackground;
            dgv_variables.GridColor = LightBorderColor;
            dgv_variables.ColumnHeadersDefaultCellStyle.BackColor = LightBackground;
            dgv_variables.ColumnHeadersDefaultCellStyle.ForeColor = LightTextColor;
            dgv_variables.RowHeadersDefaultCellStyle.BackColor = LightBackground;
            dgv_variables.RowHeadersDefaultCellStyle.ForeColor = LightSecondaryText;
            dgv_variables.DefaultCellStyle.BackColor = LightPanelBackground;
            dgv_variables.DefaultCellStyle.ForeColor = LightTextColor;
            dgv_variables.DefaultCellStyle.SelectionBackColor = LightAccent;
            dgv_variables.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv_variables.AlternatingRowsDefaultCellStyle.BackColor = LightAlternateRow;
            dgv_variables.AlternatingRowsDefaultCellStyle.SelectionBackColor = LightAccent;
            dgv_variables.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;

            // Command panel
            commandPanel.BackColor = LightPanelBackground;

            // Presets panel
            presetsPanel.BackColor = LightBackground;
            presetsTabViewportPanel.BackColor = LightBackground;
            presetsHeaderPanel.BackColor = LightBackground;
            presetsToolStrip.BackColor = LightBackground;
            lblPresetsTitle.ForeColor = LightTextColor;
            presetsTabControl.BackColor = LightBackground;
            presetsTabHeaderStrip.BackColor = LightBackground;
            presetsTabHeaderStrip.HeaderBackgroundColor = LightBackground;
            presetsTabHeaderStrip.SelectedTabBackgroundColor = LightPanelBackground;
            presetsTabHeaderStrip.HoverTabBackgroundColor = LightControlBackground;
            presetsTabHeaderStrip.SelectedTextColor = LightTextColor;
            presetsTabHeaderStrip.UnselectedTextColor = LightSecondaryText;
            presetsTabHeaderStrip.SelectedAccentColor = LightAccent;
            presetsTabHeaderStrip.BorderColor = LightBorderColor;
            tabPresets.BackColor = LightPanelBackground;
            tabFavorites.BackColor = LightPanelBackground;
            trvPresets.BackColor = LightPanelBackground;
            trvPresets.ForeColor = LightTextColor;
            trvFavorites.BackColor = LightPanelBackground;
            trvFavorites.ForeColor = LightTextColor;
            lblFavoritesEmpty.ForeColor = LightSecondaryText;
            if (_presetSearchPanel != null)
            {
                _presetSearchPanel.BackColor = LightBackground;
                _txtPresetSearch!.BackColor = LightPanelBackground;
                _txtPresetSearch.ForeColor = LightTextColor;
                _btnPresetSearchClear!.ForeColor = LightSecondaryText;
            }

            // Script panel
            scriptPanel.BackColor = LightPanelBackground;
            scriptHeaderPanel.BackColor = LightBackground;
            scriptFooterPanel.BackColor = LightBackground;
            lblScriptTitle.ForeColor = LightTextColor;
            lblPresetName.ForeColor = LightSecondaryText;
            lblTimeoutHeader.ForeColor = LightSecondaryText;
            lblLinePosition.ForeColor = LightSecondaryText;
            txtPreset.BackColor = LightControlBackground;
            txtPreset.ForeColor = LightTextColor;
            txtTimeoutHeader.BackColor = LightControlBackground;
            txtTimeoutHeader.ForeColor = LightTextColor;
            txtCommand.BackColor = LightControlBackground;
            txtCommand.ForeColor = LightTextColor;
            txtCommand.ApplyTheme(false);
            btnSavePreset.BackColor = LightAccent;
            btnSavePreset.FlatAppearance.BorderColor = LightSelectionBorder;

            
            // Execute panel
            executePanel.BackColor = LightBackground;

            // History panel (NOT the output - that stays dark)
            outputPanel.BackColor = LightPanelBackground;
            historyPanel.BackColor = LightPanelBackground;
            historyHeaderPanel.BackColor = LightBackground;
            lblHistoryTitle.ForeColor = LightTextColor;
            lstOutput.BackColor = LightPanelBackground;
            lstOutput.ForeColor = LightTextColor;
            hostListPanel.BackColor = LightPanelBackground;
            hostHeaderPanel.BackColor = LightBackground;
            lblHostsListTitle.ForeColor = LightTextColor;
            lstHosts.BackColor = LightPanelBackground;
            lstHosts.ForeColor = LightTextColor;

            // Output tools (light)

            // Toolstrip styling
            ApplyToolStripTheme(mainToolStrip, false);
            ApplyToolStripTheme(presetsToolStrip, false);
            mainToolStrip.Renderer = new ModernToolStripRenderer();
            presetsToolStrip.Renderer = new ModernToolStripRenderer();
            menuStrip1.Renderer = new ModernToolStripRenderer();

            // Splitter styling - light theme
            mainSplitContainer.BackColor = LightFormBackground;
            topSplitContainer.BackColor = LightFormBackground;
            commandSplitContainer.BackColor = LightFormBackground;
            outputSplitContainer.BackColor = LightFormBackground;
            historySplitContainer.BackColor = LightFormBackground;

            // Input field borders - standard for light mode
            txtPreset.BorderStyle = BorderStyle.Fixed3D;
            txtTimeoutHeader.BorderStyle = BorderStyle.Fixed3D;

            // Reset scrollbars to light theme
            ApplyLightScrollbars(dgv_variables);
            ApplyLightScrollbars(trvPresets);
            ApplyLightScrollbars(trvFavorites);
            ApplyLightScrollbars(lstOutput);
            ApplyLightScrollbars(lstHosts);
            ApplyLightScrollbars(txtCommand);
            ApplyLightScrollbars(txtOutput);

            // Reset TabControl to default drawing
            ApplyLightTabControl(presetsTabControl);
            presetsTabHeaderStrip.Invalidate();
            UpdatePresetsTabViewportLayout();
        }

        private void ApplyDarkTheme()
        {
            // Apply dark title bar (Windows 10 1809+ / Windows 11)
            int value = 1;
            _ = NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));

            // Form - deep background
            BackColor = DarkSurface0;

            // Menu and toolbar - elevated surface
            menuStrip1.BackColor = DarkSurface3;
            menuStrip1.ForeColor = DarkTextPrimary;
            mainToolStrip.BackColor = DarkSurface3;
            mainToolStrip.ForeColor = DarkTextPrimary;
            statusStrip.BackColor = DarkSurface3;
            statusStrip.ForeColor = DarkTextPrimary;
            statusLabel.ForeColor = DarkTextPrimary;
            statusHostCount.ForeColor = DarkTextSecondary;
            ApplySchedulerStatusBarTheme();

            // Hosts panel
            hostsPanel.BackColor = DarkSurface1;
            hostsHeaderPanel.BackColor = DarkSurface2;
            lblHostsTitle.ForeColor = DarkTextPrimary;
            lblHostCount.ForeColor = DarkTextSecondary;

            // DataGridView - refined dark styling with subtle selection
            dgv_variables.BackgroundColor = DarkSurface1;
            dgv_variables.GridColor = DarkBorder;
            dgv_variables.ColumnHeadersDefaultCellStyle.BackColor = DarkSurface2;
            dgv_variables.ColumnHeadersDefaultCellStyle.ForeColor = DarkTextPrimary;
            dgv_variables.RowHeadersDefaultCellStyle.BackColor = DarkSurface2;
            dgv_variables.RowHeadersDefaultCellStyle.ForeColor = DarkTextSecondary;
            dgv_variables.DefaultCellStyle.BackColor = DarkSurface1;
            dgv_variables.DefaultCellStyle.ForeColor = DarkTextPrimary;
            dgv_variables.DefaultCellStyle.SelectionBackColor = DarkSelectionBg;
            dgv_variables.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv_variables.AlternatingRowsDefaultCellStyle.BackColor = DarkSurface2;
            dgv_variables.AlternatingRowsDefaultCellStyle.SelectionBackColor = DarkSelectionBg;
            dgv_variables.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;

            // Command panel
            commandPanel.BackColor = DarkSurface1;

            // Presets panel
            presetsPanel.BackColor = DarkSurface1;
            presetsTabViewportPanel.BackColor = DarkSurface1;
            presetsHeaderPanel.BackColor = DarkSurface2;
            presetsToolStrip.BackColor = DarkSurface2;
            lblPresetsTitle.ForeColor = DarkTextPrimary;
            presetsTabControl.BackColor = DarkSurface1;
            presetsTabHeaderStrip.BackColor = DarkSurface2;
            presetsTabHeaderStrip.HeaderBackgroundColor = DarkSurface2;
            presetsTabHeaderStrip.SelectedTabBackgroundColor = DarkSurface1;
            presetsTabHeaderStrip.HoverTabBackgroundColor = DarkSurface3;
            presetsTabHeaderStrip.SelectedTextColor = Color.White;
            presetsTabHeaderStrip.UnselectedTextColor = DarkTextSecondary;
            presetsTabHeaderStrip.SelectedAccentColor = DarkSelectionBorder;
            presetsTabHeaderStrip.BorderColor = DarkBorder;
            tabPresets.BackColor = DarkSurface1;
            tabFavorites.BackColor = DarkSurface1;
            trvPresets.BackColor = DarkSurface1;
            trvPresets.ForeColor = DarkTextPrimary;
            trvFavorites.BackColor = DarkSurface1;
            trvFavorites.ForeColor = DarkTextPrimary;
            lblFavoritesEmpty.ForeColor = DarkTextSecondary;
            if (_presetSearchPanel != null)
            {
                _presetSearchPanel.BackColor = DarkSurface2;
                _txtPresetSearch!.BackColor = DarkInputBackground;
                _txtPresetSearch.ForeColor = DarkInputText;
                _btnPresetSearchClear!.ForeColor = DarkTextSecondary;
            }

            // Script panel
            scriptPanel.BackColor = DarkSurface1;
            scriptHeaderPanel.BackColor = DarkSurface2;
            scriptFooterPanel.BackColor = DarkSurface2;
            lblScriptTitle.ForeColor = DarkTextPrimary;
            lblPresetName.ForeColor = DarkTextSecondary;
            lblTimeoutHeader.ForeColor = DarkTextSecondary;
            lblLinePosition.ForeColor = DarkTextSecondary;
            txtPreset.BackColor = DarkInputBackground;
            txtPreset.ForeColor = DarkInputText;
            txtTimeoutHeader.BackColor = DarkInputBackground;
            txtTimeoutHeader.ForeColor = DarkInputText;
            txtCommand.BackColor = DarkSurface2;
            txtCommand.ForeColor = DarkInputText;
            txtCommand.ApplyTheme(true);
            btnSavePreset.BackColor = DarkSelectionBg;
            btnSavePreset.FlatAppearance.BorderColor = DarkSelectionBorder;

            // Execute panel
            executePanel.BackColor = DarkSurface2;

            // History panel (NOT the output - that stays dark)
            outputPanel.BackColor = DarkSurface1;
            historyPanel.BackColor = DarkSurface1;
            historyHeaderPanel.BackColor = DarkSurface2;
            lblHistoryTitle.ForeColor = DarkTextPrimary;
            lstOutput.BackColor = DarkSurface1;
            lstOutput.ForeColor = DarkTextPrimary;
            hostListPanel.BackColor = DarkSurface1;
            hostHeaderPanel.BackColor = DarkSurface2;
            lblHostsListTitle.ForeColor = DarkTextPrimary;
            lstHosts.BackColor = DarkSurface1;
            lstHosts.ForeColor = DarkTextPrimary;

            // Output tools (dark)

            // Toolstrip styling with dark theme
            ApplyToolStripTheme(mainToolStrip, true);
            ApplyToolStripTheme(presetsToolStrip, true);
            mainToolStrip.Renderer = new DarkToolStripRenderer();
            presetsToolStrip.Renderer = new DarkToolStripRenderer();
            menuStrip1.Renderer = new DarkToolStripRenderer();

            // Splitter styling
            mainSplitContainer.BackColor = DarkSurface0;
            topSplitContainer.BackColor = DarkSurface0;
            commandSplitContainer.BackColor = DarkSurface0;
            outputSplitContainer.BackColor = DarkSurface0;
            historySplitContainer.BackColor = DarkSurface0;

            // Input field borders - use BorderStyle.FixedSingle for dark visibility
            txtPreset.BorderStyle = BorderStyle.FixedSingle;
            txtTimeoutHeader.BorderStyle = BorderStyle.FixedSingle;

            // Apply dark scrollbars to scrollable controls
            ApplyDarkScrollbars(dgv_variables);
            ApplyDarkScrollbars(trvPresets);
            ApplyDarkScrollbars(trvFavorites);
            ApplyDarkScrollbars(lstOutput);
            ApplyDarkScrollbars(lstHosts);
            ApplyDarkScrollbars(txtCommand);
            ApplyDarkScrollbars(txtOutput);

            // Style TabControl for dark mode
            ApplyDarkTabControl(presetsTabControl);
            presetsTabHeaderStrip.Invalidate();
            UpdatePresetsTabViewportLayout();
        }

        private void ApplyToolStripTheme(ToolStrip strip, bool darkMode)
        {
            var textColor = darkMode ? DarkTextPrimary : LightTextColor;
            var inputBg = darkMode ? DarkInputBackground : LightControlBackground;
            var inputText = darkMode ? DarkInputText : LightTextColor;

            foreach (ToolStripItem item in strip.Items)
            {
                item.ForeColor = textColor;
                if (item is ToolStripTextBox textBox)
                {
                    textBox.BackColor = inputBg;
                    textBox.ForeColor = inputText;
                    if (textBox.TextBox != null)
                    {
                        textBox.TextBox.BackColor = inputBg;
                        textBox.TextBox.ForeColor = inputText;
                    }
                }
                else if (item is ToolStripComboBox comboBox)
                {
                    comboBox.BackColor = inputBg;
                    comboBox.ForeColor = inputText;
                }
                else if (item is ToolStripLabel label)
                {
                    label.ForeColor = darkMode ? DarkTextSecondary : LightSecondaryText;
                }
                else if (item is ToolStripButton button)
                {
                    button.Margin = new Padding(2, 0, 2, 0);
                }
            }
        }

        // Track controls that have had scrollbar theme handlers attached
        private readonly HashSet<Control> _scrollbarThemedControls = new();

        /// <summary>
        /// Applies dark scrollbars to a control using Windows 10/11 dark mode theme.
        /// </summary>
        private void ApplyDarkScrollbars(Control control)
        {
            ApplyScrollbarTheme(control, "DarkMode_Explorer");
        }

        /// <summary>
        /// Resets scrollbars to default light theme.
        /// </summary>
        private void ApplyLightScrollbars(Control control)
        {
            ApplyScrollbarTheme(control, "Explorer");
        }

        /// <summary>
        /// Applies scrollbar theme to a control, handling both immediate and deferred scenarios.
        /// </summary>
        private void ApplyScrollbarTheme(Control control, string theme)
        {
            bool isDark = theme == "DarkMode_Explorer";

            if (control.IsHandleCreated)
            {
                ApplyScrollbarThemeToHandle(control.Handle, isDark);
            }

            // Only attach the HandleCreated handler once per control
            if (!_scrollbarThemedControls.Contains(control))
            {
                _scrollbarThemedControls.Add(control);
                control.HandleCreated += (s, e) =>
                {
                    if (s is Control c)
                    {
                        ApplyScrollbarThemeToHandle(c.Handle, _isDarkMode);
                    }
                };
            }
        }

        /// <summary>
        /// Applies dark/light scrollbar theme to a window handle using Windows dark mode APIs.
        /// Also applies the theme to child windows (scrollbars in complex controls like DataGridView).
        /// </summary>
        private static void ApplyScrollbarThemeToHandle(IntPtr handle, bool dark)
        {
            // Allow dark mode for this specific window
            NativeMethods.AllowDarkModeForWindow(handle, dark);

            // Set the visual theme
            var theme = dark ? "DarkMode_Explorer" : "Explorer";
            NativeMethods.SetWindowTheme(handle, theme, null);

            // Also apply theme to child windows (scrollbars are child windows in complex controls)
            NativeMethods.EnumChildWindows(handle, (childHwnd, lParam) =>
            {
                NativeMethods.AllowDarkModeForWindow(childHwnd, dark);
                NativeMethods.SetWindowTheme(childHwnd, theme, null);
                return true; // Continue enumeration
            }, IntPtr.Zero);

            // Send theme changed message to force the control to refresh its scrollbars
            NativeMethods.SendMessage(handle, NativeMethods.WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);

            // Force the non-client area (including scrollbars) to be recalculated and redrawn
            NativeMethods.SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
        }

        /// <summary>
        /// Applies dark mode styling to a TabControl.
        /// </summary>
        private void ApplyDarkTabControl(TabControl tabControl)
        {
            // Keep normal appearance but use owner draw
            tabControl.Appearance = TabAppearance.Normal;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.DrawItem -= TabControl_DrawItem;
            tabControl.DrawItem += TabControl_DrawItem;

            tabControl.Paint -= TabControl_Paint;

            // Use the custom BorderlessTabControl properties if available
            if (tabControl is BorderlessTabControl borderlessTab)
            {
                borderlessTab.HideBorder = true;
                borderlessTab.BorderBackgroundColor = DarkSurface1;
                borderlessTab.HiddenBorderHeaderColor = DarkSurface2;
                borderlessTab.HiddenBorderInactiveTabColor = DarkSurface3;
                borderlessTab.HiddenBorderInactiveTabTopLineColor = DarkSurface2;
            }
            else
            {
                // Non-borderless tabs still rely on the managed paint overlay to hide native artifacts.
                tabControl.Paint += TabControl_Paint;
            }

            // Style the parent panel
            if (tabControl.Parent is Panel parentPanel)
            {
                parentPanel.BackColor = DarkSurface1;
            }

            // Style the tab pages themselves
            foreach (TabPage page in tabControl.TabPages)
            {
                page.BackColor = DarkSurface1;
                page.ForeColor = DarkTextPrimary;
            }

            tabControl.Invalidate();
            tabControl.Parent?.Invalidate();
        }

        /// <summary>
        /// Resets TabControl to default drawing.
        /// </summary>
        private void ApplyLightTabControl(TabControl tabControl)
        {
            // Reset to normal tab appearance
            tabControl.Appearance = TabAppearance.Normal;
            tabControl.DrawMode = TabDrawMode.Normal;
            tabControl.DrawItem -= TabControl_DrawItem;
            tabControl.Paint -= TabControl_Paint;

            // Disable border hiding on custom TabControl
            if (tabControl is BorderlessTabControl borderlessTab)
            {
                borderlessTab.HideBorder = false;
            }

            // Reset tab page colors
            foreach (TabPage page in tabControl.TabPages)
            {
                page.BackColor = SystemColors.Control;
                page.ForeColor = SystemColors.ControlText;
            }

            tabControl.Invalidate();
        }

        private void TabControl_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not TabControl tabControl) return;

            using var bgBrush = new SolidBrush(DarkSurface1);
            using var headerBrush = new SolidBrush(DarkSurface2);

            var tabHeight = tabControl.ItemSize.Height + 4;

            // Fill the entire content area (everything below the tabs)
            var contentRect = new Rectangle(0, tabHeight - 2, tabControl.Width, tabControl.Height - tabHeight + 2);
            e.Graphics.FillRectangle(bgBrush, contentRect);

            // Paint thick borders to cover all default 3D effects
            // Left edge (extra wide to ensure coverage)
            e.Graphics.FillRectangle(bgBrush, 0, tabHeight - 2, 4, tabControl.Height - tabHeight + 4);
            // Right edge
            e.Graphics.FillRectangle(bgBrush, tabControl.Width - 4, tabHeight - 2, 4, tabControl.Height - tabHeight + 4);
            // Bottom edge
            e.Graphics.FillRectangle(bgBrush, 0, tabControl.Height - 4, tabControl.Width, 4);

            // Fill the area to the right of the last tab (header area)
            if (tabControl.TabCount > 0)
            {
                var lastTabRect = tabControl.GetTabRect(tabControl.TabCount - 1);
                var fillRect = new Rectangle(lastTabRect.Right, 0, tabControl.Width - lastTabRect.Right, tabHeight - 2);
                e.Graphics.FillRectangle(headerBrush, fillRect);

                // Also fill above the tabs to cover any top border
                e.Graphics.FillRectangle(headerBrush, 0, 0, tabControl.Width, 2);
            }

            // Draw a subtle separator line between tabs and content
            using var borderPen = new Pen(DarkBorder);
            e.Graphics.DrawLine(borderPen, 0, tabHeight - 2, tabControl.Width, tabHeight - 2);
        }

        private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabControl) return;

            var tabPage = tabControl.TabPages[e.Index];
            var tabRect = tabControl.GetTabRect(e.Index);
            var isSelected = tabControl.SelectedIndex == e.Index;

            // First, fill the header background area to eliminate any white artifacts
            using (var headerBrush = new SolidBrush(DarkSurface2))
            {
                // Paint the entire row above and around this tab
                e.Graphics.FillRectangle(headerBrush, tabRect.X - 4, 0, tabRect.Width + 8, tabRect.Y + 2);
            }

            // Draw tab background
            var bgColor = isSelected ? DarkSurface1 : DarkSurface3;
            using (var bgBrush = new SolidBrush(bgColor))
            {
                // Fill the actual tab area (not the expanded rect for non-selected)
                var fillRect = new Rectangle(tabRect.X, tabRect.Y, tabRect.Width, tabRect.Height);
                e.Graphics.FillRectangle(bgBrush, fillRect);
            }

            // For selected tab: draw accent line at top and blend bottom with content
            if (isSelected)
            {
                // Blue accent line at top
                using var accentPen = new Pen(DarkSelectionBorder, 2);
                e.Graphics.DrawLine(accentPen, tabRect.Left, tabRect.Top + 1, tabRect.Right - 1, tabRect.Top + 1);

                // Make sure bottom blends with content (no border)
                using var contentBrush = new SolidBrush(DarkSurface1);
                e.Graphics.FillRectangle(contentBrush, tabRect.Left - 2, tabRect.Bottom - 2, tabRect.Width + 4, 6);
            }
            else
            {
                // For unselected tabs: paint over any edge highlights
                // Cover the right edge where white highlight appears
                using var edgeBrush = new SolidBrush(DarkSurface2);
                e.Graphics.FillRectangle(edgeBrush, tabRect.Right - 1, tabRect.Y, 4, tabRect.Height);
                // Cover the left edge
                e.Graphics.FillRectangle(edgeBrush, tabRect.Left - 3, tabRect.Y, 4, tabRect.Height);
                // Cover the top edge highlight with a darker line
                using var topPen = new Pen(DarkSurface2, 2);
                e.Graphics.DrawLine(topPen, tabRect.Left, tabRect.Top + 1, tabRect.Right - 1, tabRect.Top + 1);

                // Draw bottom border line
                using var borderBrush = new SolidBrush(DarkSurface1);
                e.Graphics.FillRectangle(borderBrush, tabRect.Left - 2, tabRect.Bottom - 1, tabRect.Width + 4, 5);

                using var borderPen = new Pen(DarkBorder);
                e.Graphics.DrawLine(borderPen, tabRect.Left - 2, tabRect.Bottom - 1, tabRect.Right + 2, tabRect.Bottom - 1);
            }

            // Draw tab text
            var textColor = isSelected ? Color.White : DarkTextSecondary;
            using (var textBrush = new SolidBrush(textColor))
            {
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                SafeDrawString(e.Graphics, tabPage.Text, tabControl.Font, textBrush, tabRect, sf);
            }
        }

        #endregion

        /// <summary>
        /// Draws text safely, catching ArgumentException from disposed fonts during
        /// font-settings transitions. Silently skips the draw on failure — the control
        /// will repaint correctly on the next cycle with valid fonts.
        /// </summary>
        private static void SafeDrawString(Graphics g, string? s, Font font, Brush brush, RectangleF rect, StringFormat? format)
        {
            try
            {
                g.DrawString(s, font, brush, rect, format);
            }
            catch (ArgumentException)
            {
                // Font disposed during a settings transition; skip this frame
            }
        }

        #endregion

        #region Form Events

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_exitConfirmed &&
                e.CloseReason != CloseReason.WindowsShutDown &&
                e.CloseReason != CloseReason.TaskManagerClosing)
            {
                if (!ConfirmExitWorkflow())
                {
                    e.Cancel = true;
                    return;
                }
            }

            _jobExecutionService?.Stop();
            _configService.GetCurrent().LastAppShutdownUtc = DateTime.UtcNow;
            SaveConfiguration();
        }

        #endregion

        #region DataGridView Events

        private void Dgv_Variables_MouseDown(object? sender, MouseEventArgs e)
        {
            var hit = dgv_variables.HitTest(e.X, e.Y);

            if (hit.Type == DataGridViewHitTestType.RowHeader)
            {
                dgv_variables.ClearSelection();
                dgv_variables.CurrentCell = dgv_variables.Rows[hit.RowIndex].Cells[0];
                foreach (DataGridViewCell cell in dgv_variables.Rows[hit.RowIndex].Cells)
                {
                    cell.Selected = true;
                }
            }

            if (hit.Type != DataGridViewHitTestType.Cell &&
                hit.Type != DataGridViewHitTestType.ColumnHeader &&
                hit.Type != DataGridViewHitTestType.RowHeader)
            {
                SelectHostIpColumnOnly();
            }

            if (e.Button == MouseButtons.Right)
            {
                HandleRightClick(hit, e.Location);
            }
        }

        private void HandleRightClick(DataGridView.HitTestInfo hit, Point location)
        {
            if (hit.Type == DataGridViewHitTestType.Cell || hit.Type == DataGridViewHitTestType.ColumnHeader || hit.Type == DataGridViewHitTestType.RowHeader)
            {
                _rightClickedColumnIndex = hit.ColumnIndex;
                _rightClickedRowIndex = (hit.Type == DataGridViewHitTestType.Cell || hit.Type == DataGridViewHitTestType.RowHeader) ? hit.RowIndex : -1;

                if (hit.Type == DataGridViewHitTestType.Cell)
                {
                    dgv_variables.CurrentCell = dgv_variables[hit.ColumnIndex, hit.RowIndex];
                }

                // Hide column operations when clicking on row header
                bool isRowHeader = hit.Type == DataGridViewHitTestType.RowHeader;
                deleteColumnToolStripMenuItem.Visible = !isRowHeader;
                renameColumnToolStripMenuItem.Visible = !isRowHeader;

                // Hide row operations when clicking on column header
                bool isColumnHeader = hit.Type == DataGridViewHitTestType.ColumnHeader;
                deleteRowToolStripMenuItem.Visible = !isColumnHeader;

                // Enable/disable delete/rename based on Host_IP protection
                bool isProtected = IsProtectedColumn(_rightClickedColumnIndex);
                deleteColumnToolStripMenuItem.Enabled = !isProtected;
                renameColumnToolStripMenuItem.Enabled = !isProtected;

                // Show connection testing only for data rows
                bool isDataRow = !isColumnHeader;
                if (_testConnectionMenuItem != null)
                    _testConnectionMenuItem.Visible = isDataRow;
                if (_clearTestResultsMenuItem != null)
                    _clearTestResultsMenuItem.Visible = isDataRow;
                if (_testConnectionSeparator != null)
                    _testConnectionSeparator.Visible = isDataRow;

                UpdateHostGridContextMenuSeparators();
                contextMenuStrip1.Show(dgv_variables, location);
            }
            else
            {
                _rightClickedColumnIndex = -1;
                _rightClickedRowIndex = -1;
                deleteColumnToolStripMenuItem.Visible = true;
                deleteColumnToolStripMenuItem.Enabled = true;
                renameColumnToolStripMenuItem.Visible = true;
                renameColumnToolStripMenuItem.Enabled = true;
                deleteRowToolStripMenuItem.Visible = true;
                UpdateHostGridContextMenuSeparators();
            }
        }

        private void UpdateHostGridContextMenuSeparators()
        {
            bool hasColumnActions = addColumnToolStripMenuItem.Available ||
                                    renameColumnToolStripMenuItem.Available ||
                                    deleteColumnToolStripMenuItem.Available;
            bool hasRowAction = deleteRowToolStripMenuItem.Available;
            bool hasSelectionActions = selectAllHostsToolStripMenuItem.Available ||
                                       deselectAllHostsToolStripMenuItem.Available ||
                                       invertSelectionToolStripMenuItem.Available;

            // Between column actions and row action.
            toolStripSeparator5.Visible = hasColumnActions && hasRowAction;
            // Between selection actions and whatever action group comes before them.
            toolStripSeparatorSelection.Visible = hasSelectionActions && (hasColumnActions || hasRowAction);
        }

        private bool IsProtectedColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= dgv_variables.Columns.Count)
                return false;

            var col = dgv_variables.Columns[columnIndex];
            return string.Equals(col.Name, CsvManager.HostColumnName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(col.HeaderText, CsvManager.HostColumnName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(col.Name, SelectColumnName, StringComparison.OrdinalIgnoreCase);
        }

        private void Dgv_Variables_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null) return;

            var row = grid.Rows[e.RowIndex];
            var rowIdx = (e.RowIndex + 1).ToString();
            using var centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
            var headerBackColor = row.HeaderCell.Style.BackColor.IsEmpty
                ? grid.RowHeadersDefaultCellStyle.BackColor
                : row.HeaderCell.Style.BackColor;
            using (var backgroundBrush = new SolidBrush(headerBackColor))
            {
                e.Graphics.FillRectangle(backgroundBrush, headerBounds);
            }

            using (var borderPen = new Pen(grid.GridColor))
            {
                e.Graphics.DrawLine(borderPen, headerBounds.Right - 1, headerBounds.Top, headerBounds.Right - 1, headerBounds.Bottom - 1);
                e.Graphics.DrawLine(borderPen, headerBounds.Left, headerBounds.Bottom - 1, headerBounds.Right - 1, headerBounds.Bottom - 1);
            }

            var headerForeColor = row.HeaderCell.Style.ForeColor.IsEmpty
                ? grid.RowHeadersDefaultCellStyle.ForeColor
                : row.HeaderCell.Style.ForeColor;
            using var brush = new SolidBrush(headerForeColor);
            SafeDrawString(e.Graphics, rowIdx, grid.Font, brush, headerBounds, centerFormat);
        }

        private void Dgv_Variables_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;

            // Paint header checkbox for Select column
            if (e.RowIndex == -1 && e.ColumnIndex >= 0 &&
                dgv_variables.Columns[e.ColumnIndex].Name == SelectColumnName)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All);

                // Draw checkbox in center of header
                var checkboxSize = 14;
                var x = e.CellBounds.X + (e.CellBounds.Width - checkboxSize) / 2;
                var y = e.CellBounds.Y + (e.CellBounds.Height - checkboxSize) / 2;
                _selectAllCheckboxBounds = new Rectangle(x, y, checkboxSize, checkboxSize);

                var state = _selectAllChecked ? ButtonState.Checked : ButtonState.Normal;
                if (_isDarkMode)
                {
                    // Dark mode: draw a custom checkbox
                    using var pen = new Pen(Color.FromArgb(128, 128, 128), 1);
                    using var brush = new SolidBrush(_selectAllChecked ? DarkSelectionBorder : Color.FromArgb(45, 45, 48));
                    e.Graphics.FillRectangle(brush, _selectAllCheckboxBounds);
                    e.Graphics.DrawRectangle(pen, _selectAllCheckboxBounds);

                    if (_selectAllChecked)
                    {
                        // Draw checkmark
                        using var checkPen = new Pen(Color.White, 2);
                        var checkX = _selectAllCheckboxBounds.X + 3;
                        var checkY = _selectAllCheckboxBounds.Y + 7;
                        e.Graphics.DrawLine(checkPen, checkX, checkY, checkX + 3, checkY + 3);
                        e.Graphics.DrawLine(checkPen, checkX + 3, checkY + 3, checkX + 9, checkY - 3);
                    }
                }
                else
                {
                    ControlPaint.DrawCheckBox(e.Graphics, _selectAllCheckboxBounds, state);
                }

                e.Handled = true;
                return;
            }

            // Paint data row checkboxes for Select column in dark mode
            if (_isDarkMode && e.RowIndex >= 0 && e.ColumnIndex >= 0 &&
                dgv_variables.Columns[e.ColumnIndex].Name == SelectColumnName)
            {
                // Paint background
                e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

                // Get checkbox state
                var checkboxCell = dgv_variables.Rows[e.RowIndex].Cells[e.ColumnIndex];
                var isChecked = checkboxCell.Value is true;

                // Draw custom checkbox in center
                var checkboxSize = 14;
                var x = e.CellBounds.X + (e.CellBounds.Width - checkboxSize) / 2;
                var y = e.CellBounds.Y + (e.CellBounds.Height - checkboxSize) / 2;
                var checkboxBounds = new Rectangle(x, y, checkboxSize, checkboxSize);

                using var pen = new Pen(Color.FromArgb(128, 128, 128), 1);
                using var brush = new SolidBrush(isChecked ? DarkSelectionBorder : Color.FromArgb(45, 45, 48));
                e.Graphics.FillRectangle(brush, checkboxBounds);
                e.Graphics.DrawRectangle(pen, checkboxBounds);

                if (isChecked)
                {
                    // Draw checkmark
                    using var checkPen = new Pen(Color.White, 2);
                    var checkX = checkboxBounds.X + 3;
                    var checkY = checkboxBounds.Y + 7;
                    e.Graphics.DrawLine(checkPen, checkX, checkY, checkX + 3, checkY + 3);
                    e.Graphics.DrawLine(checkPen, checkX + 3, checkY + 3, checkX + 9, checkY - 3);
                }

                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var cell = dgv_variables.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (!cell.Selected) return;

            // Paint selected cells with consistent color regardless of focus state
            var selectionColor = _isDarkMode ? DarkSelectionBg : LightAccent;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background);

            using (var brush = new SolidBrush(selectionColor))
            {
                e.Graphics.FillRectangle(brush, e.CellBounds);
            }

            // Paint the rest (content, border)
            e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground | DataGridViewPaintParts.Border);

            e.Handled = true;
        }

        private void Dgv_Variables_Leave(object? sender, EventArgs e)
        {
            SelectHostIpColumnOnly();
        }

        private void SelectHostIpColumnOnly()
        {
            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            // Select only the Host_IP column of selected rows
            if (dgv_variables.Columns.Contains(CsvManager.HostColumnName) && dgv_variables.SelectedCells.Count > 0)
            {
                var selectedRows = dgv_variables.SelectedCells
                    .Cast<DataGridViewCell>()
                    .Select(c => c.RowIndex)
                    .Distinct()
                    .Where(r => r >= 0 && r < dgv_variables.Rows.Count)
                    .ToList();

                dgv_variables.ClearSelection();

                foreach (var rowIndex in selectedRows)
                {
                    dgv_variables.Rows[rowIndex].Cells[CsvManager.HostColumnName].Selected = true;
                }
            }
            else
            {
                dgv_variables.ClearSelection();
            }
        }

        private void Dgv_Variables_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            // Column header click (but not top-left corner where ColumnIndex is also -1)
            if (e.RowIndex == -1 && e.ColumnIndex >= 0)
            {
                dgv_variables.ClearSelection();
                foreach (DataGridViewRow row in dgv_variables.Rows)
                {
                    row.Cells[e.ColumnIndex].Selected = true;
                }
                return;
            }

            // Single-click checkbox toggle (since EditMode is EditProgrammatically)
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 &&
                dgv_variables.Columns[e.ColumnIndex].Name == SelectColumnName &&
                !dgv_variables.Rows[e.RowIndex].IsNewRow)
            {
                var cell = dgv_variables.Rows[e.RowIndex].Cells[e.ColumnIndex];
                bool currentValue = cell.Value is true;
                cell.Value = !currentValue;
            }
        }

        private void Dgv_Variables_ColumnAdded(object? sender, DataGridViewColumnEventArgs e)
        {
            e.Column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        private void Dgv_Variables_CellLeave(object? sender, DataGridViewCellEventArgs e)
        {
            dgv_variables.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void Dgv_Variables_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            // Don't mark dirty for checkbox changes (they're not persisted to CSV)
            if (e.ColumnIndex >= 0 && dgv_variables.Columns[e.ColumnIndex].Name == SelectColumnName)
            {
                UpdateSelectionCount();
                return;
            }

            _csvDirty = true;
            RequestHostGridHostCountRefresh();

            // Clear connection test indicator when Host_IP is edited
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0 &&
                dgv_variables.Columns[e.ColumnIndex].Name == "Host_IP")
            {
                ClearConnectionTestVisualState(dgv_variables.Rows[e.RowIndex], e.ColumnIndex);
            }
        }

        private void Dgv_Variables_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex >= 0 && dgv_variables.Columns[e.ColumnIndex].Name == SelectColumnName)
            {
                _selectAllChecked = !_selectAllChecked;
                SetAllCheckboxes(_selectAllChecked);
            }
        }

        private void Dgv_Variables_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            // Commit checkbox changes immediately so CellValueChanged fires right away
            if (dgv_variables.CurrentCell is DataGridViewCheckBoxCell)
            {
                dgv_variables.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void Dgv_Variables_RowsAdded(object? sender, DataGridViewRowsAddedEventArgs e)
        {
            // Ensure consistent row height for all added rows (including the new row placeholder)
            for (int i = 0; i < e.RowCount; i++)
            {
                dgv_variables.Rows[e.RowIndex + i].Height = 28;
            }

            RequestHostGridDirtyMark();
            RequestHostGridHostCountRefresh();
        }

        private void Dgv_Variables_RowsRemoved(object? sender, DataGridViewRowsRemovedEventArgs e)
        {
            RequestHostGridDirtyMark();
            RequestHostGridHostCountRefresh();
        }

        private void Dgv_Variables_ColumnRemoved(object? sender, DataGridViewColumnEventArgs e)
        {
            RequestHostGridDirtyMark();
            RequestHostGridHostCountRefresh();
        }

        private void Dgv_Variables_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!dgv_variables.IsCurrentCellInEditMode && !char.IsControl(e.KeyChar))
            {
                dgv_variables.BeginEdit(true);
                if (dgv_variables.EditingControl is TextBox editingTextBox)
                {
                    editingTextBox.Text = e.KeyChar.ToString();
                    editingTextBox.SelectionStart = editingTextBox.Text.Length;
                }
                e.Handled = true;
            }
        }

        private void Dgv_Variables_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                dgv_variables.SelectAll();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopyToClipboard();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                PasteFromClipboard();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
            {
                DeleteSelectedCells();
                e.Handled = true;
            }
        }

        private void dgv_variables_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                dgv_variables.BeginEdit(true);
            }
        }

        #endregion

        #region Script Editor Events

        private void TxtCommand_CursorPositionChanged(object? sender, EventArgs e)
        {
            UpdateLinePosition();
        }

        private void UpdateLinePosition()
        {
            int selectionStart = txtCommand.SelectionStart;
            int line = txtCommand.GetLineFromCharIndex(selectionStart) + 1;
            int firstCharIndex = txtCommand.GetFirstCharIndexOfCurrentLine();
            int col = selectionStart - firstCharIndex + 1;
            lblLinePosition.Text = $"Ln {line}, Col {col}";
        }

        #endregion

        #region Preset Events

        #region TreeView Preset Handlers

        private void TrvPresets_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            // Select the node on right-click so context menu shows for correct node
            if (e.Button == MouseButtons.Right)
            {
                trvPresets.SelectedNode = e.Node;
            }
        }

        private void trvPresets_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (_suppressPresetSelectionChange || e.Node == null)
                return;

            TryApplySelectedPresetNode(
                trvPresets,
                e.Node,
                onCancel: () =>
                {
                    _suppressPresetSelectionChange = true;
                    SelectPresetByName(_activePresetName);
                    _suppressPresetSelectionChange = false;
                });
        }

        private void trvPresets_AfterCollapse(object? sender, TreeViewEventArgs e)
        {
            if (e.Node != null)
                SetFolderExpandedFromEvent(e.Node, false);
        }

        private void trvPresets_AfterExpand(object? sender, TreeViewEventArgs e)
        {
            if (e.Node != null)
                SetFolderExpandedFromEvent(e.Node, true);
        }

        private void SetFolderExpandedFromEvent(TreeNode node, bool expanded)
        {
            if (_suppressExpandCollapseEvents) return;
            if (node.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                _presetManager.SetFolderExpanded(tag.Name, expanded);
                if (debugModeToolStripMenuItem.Checked)
                {
                    // Verify the state was actually saved
                    var currentState = _presetManager.Folders.TryGetValue(tag.Name, out var info) ? info.IsExpanded : (bool?)null;
                    var action = expanded ? "expanded" : "collapsed";
                    UpdateStatusBar($"Folder '{tag.Name}' {action}. Verified state: {currentState}");
                }
            }
        }

        // Track if click was on +/- glyph to allow expand/collapse
        private bool _clickedOnPlusMinus;

        private void trvPresets_MouseDown(object? sender, MouseEventArgs e)
        {
            // Use HitTest to determine what was clicked
            var hitInfo = trvPresets.HitTest(e.Location);
            _clickedOnPlusMinus = hitInfo.Location == TreeViewHitTestLocations.PlusMinus;

            // If clicked on +/- for a folder, manually toggle expand/collapse
            if (_clickedOnPlusMinus && hitInfo.Node?.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                if (hitInfo.Node.IsExpanded)
                    hitInfo.Node.Collapse();
                else
                    hitInfo.Node.Expand();
            }

            // Full-row selection: select node when clicking anywhere on the row
            if (!_clickedOnPlusMinus && e.Button == MouseButtons.Left)
            {
                var node = trvPresets.GetNodeAt(0, e.Y);
                if (node != null)
                {
                    trvPresets.SelectedNode = node;
                }
            }
        }

        private void trvPresets_BeforeCollapse(object? sender, TreeViewCancelEventArgs e)
        {
            // Allow collapse during programmatic restoration
            if (_suppressExpandCollapseEvents) return;

            // Only allow collapse if clicked on +/- glyph
            if (!_clickedOnPlusMinus && e.Node?.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                e.Cancel = true;
            }
        }

        private void trvPresets_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            // Allow expand during programmatic restoration
            if (_suppressExpandCollapseEvents) return;

            // Only allow expand if clicked on +/- glyph
            if (!_clickedOnPlusMinus && e.Node?.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                e.Cancel = true;
            }
        }

        private void trvPresets_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            // Single-click on folder label (not +/-): select it
            if (!_clickedOnPlusMinus && e.Node?.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                trvPresets.SelectedNode = e.Node;

                if (e.Button == MouseButtons.Left)
                {
                    EnsureFolderSummaryCurrent(tag.Name);
                }
            }
            // Reset flag after click is processed
            _clickedOnPlusMinus = false;
        }

        private void trvPresets_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            // Double-click on folder label: toggle expand/collapse
            if (e.Node?.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                // Set flag to allow the expand/collapse in Before handlers
                _clickedOnPlusMinus = true;
                if (e.Node.IsExpanded)
                    e.Node.Collapse();
                else
                    e.Node.Expand();
                _clickedOnPlusMinus = false;
            }
        }

        private void trvPresets_ItemDrag(object? sender, ItemDragEventArgs e)
        {
            if (e.Item is TreeNode node)
            {
                _draggedNode = node;
                DoDragDrop(node, DragDropEffects.Move);
            }
        }

        private void trvPresets_DragOver(object? sender, DragEventArgs e)
        {
            if (_draggedNode == null)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            var pt = trvPresets.PointToClient(new Point(e.X, e.Y));
            var targetNode = trvPresets.GetNodeAt(pt);

            if (targetNode != null)
            {
                var position = GetDropPosition(targetNode, pt);
                if (CanDropAt(_draggedNode, targetNode, position))
                {
                    e.Effect = DragDropEffects.Move;
                    if (_dropTargetNode != targetNode || _dropPosition != position)
                    {
                        _dropTargetNode = targetNode;
                        _dropPosition = position;
                        trvPresets.Invalidate();
                    }
                }
                else
                {
                    e.Effect = DragDropEffects.None;
                    ClearDropIndicator();
                }
            }
            else
            {
                // Dropping on empty area = move to root
                e.Effect = DragDropEffects.Move;
                ClearDropIndicator();
            }
        }

        private void trvPresets_DragDrop(object? sender, DragEventArgs e)
        {
            // Capture and clear visual indicator state
            var dropTarget = _dropTargetNode;
            var dropPos = _dropPosition;
            ClearDropIndicator();

            if (_draggedNode == null)
                return;

            var pt = trvPresets.PointToClient(new Point(e.X, e.Y));
            var targetNode = dropTarget ?? trvPresets.GetNodeAt(pt);
            var draggedTag = _draggedNode.Tag as PresetNodeTag;

            if (draggedTag == null)
            {
                _draggedNode = null;
                return;
            }

            // If we don't have a position from the indicator, calculate it
            if (dropPos == DropPosition.None && targetNode != null)
                dropPos = GetDropPosition(targetNode, pt);

            try
            {
                string finalName = draggedTag.Name;

                if (targetNode == null)
                {
                    finalName = HandleDropOnEmptySpace(draggedTag);
                }
                else if (dropPos == DropPosition.Inside)
                {
                    finalName = HandleDropInside(draggedTag, targetNode);
                }
                else if (dropPos == DropPosition.Above || dropPos == DropPosition.Below)
                {
                    finalName = HandleDropAdjacentTo(draggedTag, targetNode, dropPos);
                }

                RefreshPresetList();
                SelectTreeNodeByTagName(finalName, draggedTag.IsFolder);
            }
            finally
            {
                _draggedNode = null;
            }
        }

        private string HandleDropOnEmptySpace(PresetNodeTag draggedTag)
        {
            if (draggedTag.IsFolder)
            {
                var currentParent = FolderPathUtility.GetParentPath(draggedTag.Name);
                if (currentParent != null)
                {
                    var folderName = FolderPathUtility.GetFolderName(draggedTag.Name);
                    var newPath = _presetManager.GetUniqueFolderName(folderName);
                    _presetManager.RenameFolder(draggedTag.Name, newPath);
                    ClearPresetDeleteUndoHistory();
                    return newPath;
                }
                return draggedTag.Name;
            }
            else
            {
                var currentPreset = _presetManager.Get(draggedTag.Name);
                if (!string.IsNullOrEmpty(currentPreset?.Folder))
                {
                    _presetManager.MovePresetToFolder(draggedTag.Name, null);
                    ClearPresetDeleteUndoHistory();
                }
                return draggedTag.Name;
            }
        }

        private string HandleDropInside(PresetNodeTag draggedTag, TreeNode targetNode)
        {
            var targetTag = targetNode.Tag as PresetNodeTag;
            if (targetTag == null || !targetTag.IsFolder)
                return draggedTag.Name;

            if (draggedTag.IsFolder)
            {
                var folderName = FolderPathUtility.GetFolderName(draggedTag.Name);
                var newPath = FolderPathUtility.CombinePath(targetTag.Name, folderName);
                newPath = _presetManager.GetUniqueFolderName(newPath);
                _presetManager.RenameFolder(draggedTag.Name, newPath);
                ClearPresetDeleteUndoHistory();
                return newPath;
            }
            else
            {
                var currentPreset = _presetManager.Get(draggedTag.Name);
                if (!string.Equals(currentPreset?.Folder, targetTag.Name, StringComparison.Ordinal))
                {
                    _presetManager.MovePresetToFolder(draggedTag.Name, targetTag.Name);
                    ClearPresetDeleteUndoHistory();
                }
                return draggedTag.Name;
            }
        }

        private string HandleDropAdjacentTo(PresetNodeTag draggedTag, TreeNode targetNode, DropPosition position)
        {
            var targetTag = targetNode.Tag as PresetNodeTag;
            if (targetTag == null) return draggedTag.Name;

            // Determine the target's parent level — this is where the dragged item will end up
            string? targetParentPath;
            if (targetTag.IsFolder)
                targetParentPath = FolderPathUtility.GetParentPath(targetTag.Name);
            else
                targetParentPath = _presetManager.Get(targetTag.Name)?.Folder;

            if (draggedTag.IsFolder)
            {
                // Move folder to the target's parent level
                var folderName = FolderPathUtility.GetFolderName(draggedTag.Name);
                var newPath = FolderPathUtility.CombinePath(targetParentPath, folderName);

                if (newPath != draggedTag.Name)
                {
                    newPath = _presetManager.GetUniqueFolderName(newPath);
                    _presetManager.RenameFolder(draggedTag.Name, newPath);
                    ClearPresetDeleteUndoHistory();
                }

                // Position in manual order relative to the target
                if (targetTag.IsFolder)
                    InsertIntoFolderOrder(newPath, targetTag.Name, position);
                else
                    InsertIntoFolderOrder(newPath, null, position);

                return newPath;
            }
            else
            {
                // Move preset to the target's parent folder
                string? targetFolder;
                if (targetTag.IsFolder)
                    targetFolder = targetParentPath;
                else
                    targetFolder = _presetManager.Get(targetTag.Name)?.Folder;

                var currentPreset = _presetManager.Get(draggedTag.Name);
                if (currentPreset?.Folder != targetFolder)
                {
                    _presetManager.MovePresetToFolder(draggedTag.Name, targetFolder);
                    ClearPresetDeleteUndoHistory();
                }

                // Position in manual order
                if (!targetTag.IsFolder)
                    InsertIntoPresetOrder(draggedTag.Name, targetFolder, targetTag.Name, position);

                return draggedTag.Name;
            }
        }

        private bool CanDropOn(TreeNode draggedNode, TreeNode targetNode)
        {
            if (draggedNode == targetNode)
                return false;

            var draggedTag = draggedNode.Tag as PresetNodeTag;
            var targetTag = targetNode.Tag as PresetNodeTag;

            if (draggedTag == null || targetTag == null)
                return false;

            if (draggedTag.IsFolder)
            {
                if (!targetTag.IsFolder)
                    return false;

                // Prevent dropping folder into itself or its descendants (cycle prevention)
                if (draggedTag.Name == targetTag.Name ||
                    FolderPathUtility.IsDescendantOf(targetTag.Name, draggedTag.Name))
                    return false;

                // Can drop folder onto another folder (to make it a subfolder) or reorder in Manual mode
                return true;
            }
            else
            {
                // Presets can drop on folders or other presets (for reordering)
                return targetTag.IsFolder || _currentSortMode == PresetSortMode.Manual;
            }
        }

        private DropPosition GetDropPosition(TreeNode targetNode, Point clientPoint)
        {
            var bounds = targetNode.Bounds;
            if (bounds.Height == 0) return DropPosition.Inside;

            float relativeY = clientPoint.Y - bounds.Top;
            float ratio = relativeY / bounds.Height;

            bool isFolder = targetNode.Tag is PresetNodeTag tag && tag.IsFolder;

            if (isFolder)
            {
                if (ratio < 0.25f) return DropPosition.Above;
                if (ratio > 0.75f) return DropPosition.Below;
                return DropPosition.Inside;
            }
            else
            {
                return ratio < 0.5f ? DropPosition.Above : DropPosition.Below;
            }
        }

        private bool CanDropAt(TreeNode draggedNode, TreeNode targetNode, DropPosition position)
        {
            if (draggedNode == targetNode)
                return false;

            var draggedTag = draggedNode.Tag as PresetNodeTag;
            var targetTag = targetNode.Tag as PresetNodeTag;

            if (draggedTag == null || targetTag == null)
                return false;

            if (draggedTag.IsFolder)
            {
                // Prevent dropping folder into itself or its descendants
                if (draggedTag.Name == targetTag.Name ||
                    FolderPathUtility.IsDescendantOf(targetTag.Name, draggedTag.Name))
                    return false;

                if (position == DropPosition.Inside)
                {
                    // Can only drop inside another folder
                    return targetTag.IsFolder;
                }
                else
                {
                    // Above/Below: can drop next to any folder
                    // Next to a preset: only if the folder would be at same level
                    return true;
                }
            }
            else
            {
                if (position == DropPosition.Inside)
                {
                    return targetTag.IsFolder;
                }
                else
                {
                    // Above/Below: always allowed (moves preset to target's level)
                    return true;
                }
            }
        }

        private void ClearDropIndicator()
        {
            if (_dropTargetNode != null || _dropPosition != DropPosition.None)
            {
                _dropTargetNode = null;
                _dropPosition = DropPosition.None;
                trvPresets.Invalidate();
            }
        }

        private static void ReorderInList(List<string> list, string source, string target)
        {
            if (!list.Contains(source))
                list.Add(source);
            if (!list.Contains(target))
                list.Add(target);

            int sourceIndex = list.IndexOf(source);
            int targetIndex = list.IndexOf(target);

            list.RemoveAt(sourceIndex);
            list.Insert(targetIndex, source);
        }

        private void ReorderFolders(string sourceFolderName, string targetFolderName)
        {
            var config = _configService.Load();
            ReorderInList(config.ManualFolderOrder, sourceFolderName, targetFolderName);
            _configService.Save(config);
            ClearPresetDeleteUndoHistory();
        }

        private void ReorderPresetsInFolder(string sourcePresetName, string targetPresetName, string? folder)
        {
            var config = _configService.Load();
            string folderKey = folder ?? "";

            if (!config.ManualPresetOrderByFolder.TryGetValue(folderKey, out var presetOrder))
            {
                presetOrder = _presetManager.GetPresetsInFolder(folder).ToList();
                config.ManualPresetOrderByFolder[folderKey] = presetOrder;
            }

            ReorderInList(presetOrder, sourcePresetName, targetPresetName);

            config.ManualPresetOrderByFolder[folderKey] = presetOrder;
            _configService.Save(config);
            ClearPresetDeleteUndoHistory();
        }

        private void InsertIntoFolderOrder(string folderToInsert, string? referenceItem, DropPosition position)
        {
            var config = _configService.Load();
            var folderOrder = config.ManualFolderOrder;

            // Ensure all existing folders are in the manual order list
            foreach (var folder in _presetManager.GetFolders())
            {
                if (!folderOrder.Contains(folder))
                    folderOrder.Add(folder);
            }

            folderOrder.Remove(folderToInsert);

            if (referenceItem != null)
            {
                int refIndex = folderOrder.IndexOf(referenceItem);
                if (refIndex >= 0)
                {
                    int insertIndex = position == DropPosition.Below ? refIndex + 1 : refIndex;
                    folderOrder.Insert(insertIndex, folderToInsert);
                }
                else
                {
                    folderOrder.Add(folderToInsert);
                }
            }
            else
            {
                folderOrder.Add(folderToInsert);
            }

            config.ManualFolderOrder = folderOrder;
            _configService.Save(config);
            ClearPresetDeleteUndoHistory();
        }

        private void InsertIntoPresetOrder(string presetToInsert, string? folder, string referenceItem, DropPosition position)
        {
            var config = _configService.Load();
            string folderKey = folder ?? "";

            if (!config.ManualPresetOrderByFolder.TryGetValue(folderKey, out var presetOrder))
            {
                presetOrder = _presetManager.GetPresetsInFolder(folder).ToList();
                config.ManualPresetOrderByFolder[folderKey] = presetOrder;
            }

            // Ensure all presets in this folder are tracked
            foreach (var p in _presetManager.GetPresetsInFolder(folder))
            {
                if (!presetOrder.Contains(p))
                    presetOrder.Add(p);
            }

            presetOrder.Remove(presetToInsert);

            int refIndex = presetOrder.IndexOf(referenceItem);
            if (refIndex >= 0)
            {
                int insertIndex = position == DropPosition.Below ? refIndex + 1 : refIndex;
                presetOrder.Insert(insertIndex, presetToInsert);
            }
            else
            {
                presetOrder.Add(presetToInsert);
            }

            config.ManualPresetOrderByFolder[folderKey] = presetOrder;
            _configService.Save(config);
            ClearPresetDeleteUndoHistory();
        }

        #endregion

        #region Favorites TreeView Handlers

        private void presetsTabHeaderStrip_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (presetsTabControl.SelectedIndex != presetsTabHeaderStrip.SelectedIndex)
            {
                presetsTabControl.SelectedIndex = presetsTabHeaderStrip.SelectedIndex;
            }
        }

        private void presetsTabControl_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (presetsTabHeaderStrip.SelectedIndex != presetsTabControl.SelectedIndex)
            {
                presetsTabHeaderStrip.SelectedIndex = presetsTabControl.SelectedIndex;
            }

            if (_restoringPresetsTabSelection)
            {
                return;
            }

            var previousTabIndex = _lastPresetsTabIndex;

            if (presetsTabControl.SelectedTab == tabFavorites)
            {
                RefreshFavoritesList();

                if (!TryApplySelectedPresetNode(
                        trvFavorites,
                        onCancel: () => RestorePresetTabSelection(previousTabIndex)))
                {
                    return;
                }
            }
            else if (presetsTabControl.SelectedTab == tabPresets)
            {
                if (!TryApplySelectedPresetNode(
                        trvPresets,
                        onCancel: () => RestorePresetTabSelection(previousTabIndex)))
                {
                    return;
                }
            }

            _lastPresetsTabIndex = presetsTabControl.SelectedIndex;
        }

        private void RefreshFavoritesList(string? filterText = null)
        {
            var filter = string.IsNullOrWhiteSpace(filterText) ? null : filterText.Trim();
            var previousSelection = CaptureSelectedPresetNodeTag(trvFavorites) ?? ClonePresetNodeTag(_lastFavoritesTreeSelection);
            var previousSuppressSelectionChange = _suppressPresetSelectionChange;
            _suppressPresetSelectionChange = true;
            trvFavorites.Nodes.Clear();

            // Get favorite folders
            var favoriteFolders = _presetManager.Folders
                .Where(kvp => kvp.Value.IsFavorite)
                .Select(kvp => kvp.Key)
                .ToHashSet();

            // Get favorite presets
            var favoritePresets = _presetManager.Presets
                .Where(kvp => kvp.Value.IsFavorite)
                .Select(kvp => kvp.Key)
                .ToHashSet();

            // Apply filter to favorites
            if (filter != null)
            {
                favoritePresets.RemoveWhere(p => !PresetMatchesFilter(p, filter));
                favoriteFolders.RemoveWhere(f => !FolderHasMatchingPresets(f, filter));
            }

            if (favoriteFolders.Count == 0 && favoritePresets.Count == 0)
            {
                lblFavoritesEmpty.Visible = true;
                trvFavorites.Visible = false;
                _lastFavoritesTreeSelection = null;
                _suppressPresetSelectionChange = previousSuppressSelectionChange;
                return;
            }

            lblFavoritesEmpty.Visible = false;
            trvFavorites.Visible = true;

            var config = _configService.Load();

            // Build ordered list of root-level favorite items
            var orderedItems = GetOrderedFavoriteItems(favoriteFolders, favoritePresets, config);

            // Add items in order
            foreach (var item in orderedItems)
            {
                if (TryParseFavoriteKey(item, out var parsedName, out var parsedIsFolder) && parsedIsFolder)
                {
                    var folderName = parsedName;
                    var folderNode = new TreeNode($"{FolderIcon} {folderName}")
                    {
                        Tag = new PresetNodeTag { IsFolder = true, Name = folderName }
                    };
                    trvFavorites.Nodes.Add(folderNode);

                    // Add presets in this folder
                    var presetsInFolder = GetSortedPresetsInFolder(folderName, config);
                    foreach (var presetName in presetsInFolder)
                    {
                        if (filter != null && !PresetMatchesFilter(presetName, filter))
                            continue;

                        var presetNode = new TreeNode(presetName)
                        {
                            Tag = new PresetNodeTag { IsFolder = false, Name = presetName }
                        };
                        folderNode.Nodes.Add(presetNode);
                    }

                    // Expand folder by default
                    folderNode.Expand();
                }
                else if (TryParseFavoriteKey(item, out var presetParsedName, out var isPresetFolder) && !isPresetFolder)
                {
                    var presetName = presetParsedName;
                    var node = new TreeNode(presetName)
                    {
                        Tag = new PresetNodeTag { IsFolder = false, Name = presetName }
                    };
                    trvFavorites.Nodes.Add(node);
                }
            }

            if (previousSelection != null)
            {
                var restoredNode = FindNodeByTag(trvFavorites.Nodes, previousSelection.Name, previousSelection.IsFolder);
                if (restoredNode != null)
                {
                    trvFavorites.SelectedNode = restoredNode;
                    _lastFavoritesTreeSelection = ClonePresetNodeTag(previousSelection);
                }
                else
                {
                    _lastFavoritesTreeSelection = null;
                }
            }

            _suppressPresetSelectionChange = previousSuppressSelectionChange;
        }

        private static string BuildFavoriteKey(PresetNodeTag tag) =>
            tag.IsFolder ? $"{FavoriteKeyFolderPrefix}{tag.Name}" : $"{FavoriteKeyPresetPrefix}{tag.Name}";

        private static bool TryParseFavoriteKey(string key, out string name, out bool isFolder)
        {
            if (key.StartsWith(FavoriteKeyFolderPrefix, StringComparison.Ordinal))
            {
                name = key.Substring(FavoriteKeyFolderPrefix.Length);
                isFolder = true;
                return true;
            }
            if (key.StartsWith(FavoriteKeyPresetPrefix, StringComparison.Ordinal))
            {
                name = key.Substring(FavoriteKeyPresetPrefix.Length);
                isFolder = false;
                return true;
            }
            name = string.Empty;
            isFolder = false;
            return false;
        }

        private List<string> GetOrderedFavoriteItems(HashSet<string> favoriteFolders, HashSet<string> favoritePresets, AppConfiguration config)
        {
            var result = new List<string>();
            var remainingFolders = new HashSet<string>(favoriteFolders);
            var remainingPresets = new HashSet<string>(favoritePresets);

            // First, add items in the saved manual order
            foreach (var item in config.ManualFavoriteOrder)
            {
                if (!TryParseFavoriteKey(item, out var name, out var isFolder))
                    continue;

                if (isFolder)
                {
                    if (remainingFolders.Contains(name))
                    {
                        result.Add(item);
                        remainingFolders.Remove(name);
                    }
                }
                else
                {
                    if (remainingPresets.Contains(name))
                    {
                        result.Add(item);
                        remainingPresets.Remove(name);
                    }
                }
            }

            // Add any remaining folders (new favorites not yet in the order)
            foreach (var folder in remainingFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                result.Add($"{FavoriteKeyFolderPrefix}{folder}");
            }

            // Add any remaining presets (new favorites not yet in the order)
            foreach (var preset in remainingPresets.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                result.Add($"{FavoriteKeyPresetPrefix}{preset}");
            }

            return result;
        }

        private void trvFavorites_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (_suppressPresetSelectionChange || e.Node == null)
                return;

            TryApplySelectedPresetNode(trvFavorites, e.Node);
        }

        private void trvFavorites_MouseDown(object? sender, MouseEventArgs e)
        {
            // Full-row selection: select node when clicking anywhere on the row
            var node = trvFavorites.GetNodeAt(0, e.Y);
            if (node != null)
            {
                trvFavorites.SelectedNode = node;
            }
        }

        private void trvFavorites_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.Node != null)
            {
                trvFavorites.SelectedNode = e.Node;
                return;
            }

            if (e.Button == MouseButtons.Left && e.Node?.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                EnsureFolderSummaryCurrent(tag.Name);
            }
        }

        private void trvFavorites_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            // Double-click loads the preset (same as select, but confirms action)
            if (e.Node?.Tag is PresetNodeTag tag && !tag.IsFolder)
            {
                UpdateStatusBar($"Loaded favorite preset: {tag.Name}");
            }
        }

        private void trvFavorites_ItemDrag(object? sender, ItemDragEventArgs e)
        {
            if (e.Item is TreeNode node)
            {
                _draggedNode = node;
                DoDragDrop(node, DragDropEffects.Move);
            }
        }

        private void trvFavorites_DragOver(object? sender, DragEventArgs e)
        {
            if (_draggedNode == null)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            var pt = trvFavorites.PointToClient(new Point(e.X, e.Y));
            var targetNode = trvFavorites.GetNodeAt(pt);

            // Reset previous highlight
            if (_favLastHighlightedNode != null && _favLastHighlightedNode != targetNode)
            {
                _favLastHighlightedNode.BackColor = trvFavorites.BackColor;
            }

            if (targetNode != null && CanDropOnFavorites(_draggedNode, targetNode))
            {
                e.Effect = DragDropEffects.Move;
                targetNode.BackColor = Color.LightBlue;
                _favLastHighlightedNode = targetNode;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void trvFavorites_DragDrop(object? sender, DragEventArgs e)
        {
            // Reset highlight
            if (_favLastHighlightedNode != null)
            {
                _favLastHighlightedNode.BackColor = trvFavorites.BackColor;
                _favLastHighlightedNode = null;
            }

            if (_draggedNode == null)
                return;

            var pt = trvFavorites.PointToClient(new Point(e.X, e.Y));
            var targetNode = trvFavorites.GetNodeAt(pt);
            var draggedTag = _draggedNode.Tag as PresetNodeTag;

            if (draggedTag == null || targetNode == null)
            {
                _draggedNode = null;
                return;
            }

            var targetTag = targetNode.Tag as PresetNodeTag;
            if (targetTag == null)
            {
                _draggedNode = null;
                return;
            }

            try
            {
                // Check if both nodes are at root level of the favorites tree
                bool draggedIsRootLevel = _draggedNode.Parent == null;
                bool targetIsRootLevel = targetNode.Parent == null;

                if (draggedIsRootLevel && targetIsRootLevel)
                {
                    // Reorder root-level favorites (folders and presets)
                    ReorderFavoriteItems(draggedTag, targetTag);
                    RefreshFavoritesList();
                    SelectItemInFavoritesTree(draggedTag);
                    UpdateStatusBar($"Reordered favorite '{draggedTag.Name}'");
                }
                else if (!draggedIsRootLevel && !targetIsRootLevel &&
                         _draggedNode.Parent == targetNode.Parent &&
                         !draggedTag.IsFolder && !targetTag.IsFolder)
                {
                    // Reorder presets within the same folder
                    var sourcePreset = _presetManager.Get(draggedTag.Name);
                    var targetPreset = _presetManager.Get(targetTag.Name);

                    if (sourcePreset?.Folder == targetPreset?.Folder)
                    {
                        ReorderPresetsInFolder(draggedTag.Name, targetTag.Name, sourcePreset?.Folder);

                        // Refresh both tabs since they share the same folder data
                        RefreshPresetList();
                        RefreshFavoritesList();
                        SelectPresetInFavoritesTree(draggedTag.Name);

                        UpdateStatusBar($"Reordered preset '{draggedTag.Name}'");
                    }
                }
            }
            finally
            {
                _draggedNode = null;
            }
        }

        private void ReorderFavoriteItems(PresetNodeTag sourceTag, PresetNodeTag targetTag)
        {
            var config = _configService.Load();

            string sourceKey = BuildFavoriteKey(sourceTag);
            string targetKey = BuildFavoriteKey(targetTag);

            // Get current favorite items
            var favoriteFolders = _presetManager.Folders
                .Where(kvp => kvp.Value.IsFavorite)
                .Select(kvp => kvp.Key)
                .ToHashSet();

            var favoritePresets = _presetManager.Presets
                .Where(kvp => kvp.Value.IsFavorite)
                .Select(kvp => kvp.Key)
                .ToHashSet();

            // Build current order
            var currentOrder = GetOrderedFavoriteItems(favoriteFolders, favoritePresets, config);

            // Remove source from current position
            currentOrder.Remove(sourceKey);

            // Find target position and insert before it
            int targetIndex = currentOrder.IndexOf(targetKey);
            if (targetIndex >= 0)
            {
                currentOrder.Insert(targetIndex, sourceKey);
            }
            else
            {
                currentOrder.Add(sourceKey);
            }

            // Save new order
            config.ManualFavoriteOrder = currentOrder;
            _configService.Save(config);
            ClearPresetDeleteUndoHistory();
        }

        private bool CanDropOnFavorites(TreeNode draggedNode, TreeNode targetNode)
        {
            if (draggedNode == targetNode)
                return false;

            var draggedTag = draggedNode.Tag as PresetNodeTag;
            var targetTag = targetNode.Tag as PresetNodeTag;

            if (draggedTag == null || targetTag == null)
                return false;

            // Check if both nodes are at root level of the favorites tree
            bool draggedIsRootLevel = draggedNode.Parent == null;
            bool targetIsRootLevel = targetNode.Parent == null;

            // Allow reordering root-level items with each other
            if (draggedIsRootLevel && targetIsRootLevel)
            {
                return true;
            }

            // Allow reordering presets within the same folder (non-root level)
            if (!draggedIsRootLevel && !targetIsRootLevel &&
                draggedNode.Parent == targetNode.Parent &&
                !draggedTag.IsFolder && !targetTag.IsFolder)
            {
                return true;
            }

            return false;
        }

        private void SelectItemInFavoritesTree(PresetNodeTag tag)
        {
            foreach (TreeNode node in trvFavorites.Nodes)
            {
                if (node.Tag is PresetNodeTag nodeTag &&
                    nodeTag.IsFolder == tag.IsFolder &&
                    nodeTag.Name == tag.Name)
                {
                    trvFavorites.SelectedNode = node;
                    return;
                }
            }
        }

        private void SelectPresetInFavoritesTree(string presetName)
        {
            foreach (TreeNode node in trvFavorites.Nodes)
            {
                if (node.Tag is PresetNodeTag tag && !tag.IsFolder && tag.Name == presetName)
                {
                    trvFavorites.SelectedNode = node;
                    return;
                }

                // Check child nodes (presets in folders)
                foreach (TreeNode childNode in node.Nodes)
                {
                    if (childNode.Tag is PresetNodeTag childTag && !childTag.IsFolder && childTag.Name == presetName)
                    {
                        trvFavorites.SelectedNode = childNode;
                        return;
                    }
                }
            }
        }

        #endregion

        #endregion

        #region Button Click Handlers

        private void btnOpenCSV_Click(object sender, EventArgs e)
        {
            if (!EnsureCsvChangesSaved()) return;
            OpenCsvFile();
        }

        private void btnSaveAs_Click(object sender, EventArgs e)
        {
            SaveCsvAs();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            if (!DialogTheme.Confirm(this, "Are you sure you want to clear all hosts?", "Clear Grid", _isDarkMode, _dialogFont))
                return;

            ClearGrid();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveCurrentPreset();
        }

        private void btnExecuteAll_Click(object sender, EventArgs e)
        {
            // Check if a folder is selected - use tracked folder name as fallback
            // (TreeView selection can be unreliable when clicking buttons)
            string? folderName = null;

            // Check both trvPresets and trvFavorites based on current tab
            if (presetsTabControl.SelectedTab == tabFavorites)
            {
                if (trvFavorites.SelectedNode?.Tag is PresetNodeTag favTag && favTag.IsFolder)
                {
                    folderName = favTag.Name;
                }
                else if (!string.IsNullOrEmpty(_selectedFolderName))
                {
                    folderName = _selectedFolderName;
                }
            }
            else
            {
                if (trvPresets.SelectedNode?.Tag is PresetNodeTag tag && tag.IsFolder)
                {
                    folderName = tag.Name;
                }
                else if (!string.IsNullOrEmpty(_selectedFolderName))
                {
                    folderName = _selectedFolderName;
                }
            }

            if (folderName != null)
            {
                ExecuteFolderPresetsOnAllHosts(folderName);
            }
            else
            {
                ExecuteOnAllHosts();
            }
        }

        private void btnExecuteSelected_Click(object sender, EventArgs e)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SshDebugLog("CLICK", "btnExecuteSelected_Click entered");

            // Check if a folder is selected - use tracked folder name as fallback
            // (TreeView selection can be unreliable when clicking buttons)
            string? folderName = null;

            // Check both trvPresets and trvFavorites based on current tab
            if (presetsTabControl.SelectedTab == tabFavorites)
            {
                SshDebugLog("CLICK", "Checking Favorites tab for folder selection", sw);
                if (trvFavorites.SelectedNode?.Tag is PresetNodeTag favTag && favTag.IsFolder)
                {
                    folderName = favTag.Name;
                }
                else if (!string.IsNullOrEmpty(_selectedFolderName))
                {
                    folderName = _selectedFolderName;
                }
            }
            else
            {
                SshDebugLog("CLICK", "Checking Presets tab for folder selection", sw);
                if (trvPresets.SelectedNode?.Tag is PresetNodeTag tag && tag.IsFolder)
                {
                    folderName = tag.Name;
                }
                else if (!string.IsNullOrEmpty(_selectedFolderName))
                {
                    folderName = _selectedFolderName;
                }
            }

            SshDebugLog("CLICK", $"Folder selection check complete. Folder: {folderName ?? "(none)"}", sw);

            if (folderName != null)
            {
                int checkedCount = GetCheckedHostCount();
                if (checkedCount > 0)
                {
                    SshDebugLog("CLICK", $"Dispatching to ExecuteFolderPresetsOnCheckedHosts ({checkedCount} hosts)", sw);
                    ExecuteFolderPresetsOnCheckedHosts(folderName);
                }
                else
                {
                    SshDebugLog("CLICK", $"Dispatching to ExecuteFolderPresetsOnSelectedHost", sw);
                    ExecuteFolderPresetsOnSelectedHost(folderName);
                }
            }
            else
            {
                // Check if any hosts are checkbox-selected
                int checkedCount = GetCheckedHostCount();
                if (checkedCount > 0)
                {
                    SshDebugLog("CLICK", $"Dispatching to ExecuteOnCheckedHosts ({checkedCount} hosts)", sw);
                    ExecuteOnCheckedHosts();
                }
                else
                {
                    SshDebugLog("CLICK", $"Dispatching to ExecuteOnSelectedHost", sw);
                    ExecuteOnSelectedHost();
                }
            }
        }

        private void btnStopAll_Click(object sender, EventArgs e)
        {
            StopExecution();
        }

        #endregion

        #region Menu Item Handlers

        private void openCSVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!EnsureCsvChangesSaved()) return;
            OpenCsvFile();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveCurrentCsv(promptIfNoPath: true);
        }

        private void saveAsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SaveCsvAs();
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            if (!ConfirmExitWorkflow()) return;
            _exitConfirmed = true;
            Close();
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var previousCredentialManager = _configService.GetCurrent().Credentials.UseCredentialManager;
            using var dialog = new SettingsDialog(_configService, _presetManager, _isDarkMode);
            DialogTheme.SetDialogFont(dialog, _dialogFont);
            var dialogResult = dialog.ShowDialog(this);
            if (dialogResult == DialogResult.OK)
            {
                // Apply saved settings
                var config = _configService.GetCurrent();

                // Update timeout placeholder to reflect new global default
                txtTimeoutHeader.PlaceholderText = config.Timeout.ToString();
                ApplyTheme(config.DarkMode);
                ApplyFontSettings(config.FontSettings);
                ApplyCommandEditorSettings(config.CommandEditor);
                ApplyColumnAutoResize(config.AutoResizeHostColumns);
                _sshService.UseConnectionPooling = config.UseConnectionPooling;
                _sshService.PreferSshAgent = config.Credentials.PreferSshAgent;
                UpdateStatusBar(config.UseConnectionPooling ? "Connection pooling enabled" : "Connection pooling disabled");

                if (previousCredentialManager != config.Credentials.UseCredentialManager)
                {
                    InitializeCredentials();
                    if (config.Credentials.UseCredentialManager)
                    {
                        MigratePasswordsToCredentialManager();
                    }
                }
            }

            // A timeout reset is persisted immediately in the settings dialog.
            // Keep the active editor timeout field in sync to avoid reintroducing overrides on save.
            if (dialog.PresetTimeoutsWereCleared && !string.IsNullOrEmpty(_activePresetName))
            {
                var activePreset = _presetManager.Get(_activePresetName);
                txtTimeoutHeader.Text = activePreset?.Timeout?.ToString() ?? string.Empty;
            }
        }

        private void exportAllPresetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportAllPresets();
        }

        private void importAllPresetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ImportAllPresets();
        }

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowFindDialog();
        }

        private void validateScriptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_selectedFolderName != null)
                return;

            var scriptText = txtCommand.Text ?? string.Empty;

            if (!Services.Scripting.ScriptParser.IsYamlScript(scriptText))
                return;

            var parser = new Services.Scripting.ScriptParser();

            try
            {
                var script = parser.Parse(scriptText);
                var errors = parser.Validate(script, scriptText, enforceCanonicalSyntax: true);
                var warnings = parser.Warnings;

                if (errors.Count == 0 && warnings.Count == 0)
                {
                    var successMessage = ScriptValidationFormatter.FormatSuccessMessage();
                    AppendOutputText(Environment.NewLine + successMessage + Environment.NewLine);
                    DialogTheme.ShowMessage(this, successMessage, "Validate Script", MessageBoxIcon.Information, _isDarkMode, _dialogFont);
                }
                else if (errors.Count == 0)
                {
                    var warningMessage = "Script validation succeeded with warnings:" + Environment.NewLine + string.Join(Environment.NewLine, warnings);
                    AppendOutputText(Environment.NewLine + warningMessage + Environment.NewLine);
                    DialogTheme.ShowMessage(this, warningMessage, "Validate Script", MessageBoxIcon.Warning, _isDarkMode, _dialogFont);
                }
                else
                {
                    var message = ScriptValidationFormatter.FormatFailureMessage(errors);
                    if (warnings.Count > 0)
                        message += Environment.NewLine + Environment.NewLine + "Warnings:" + Environment.NewLine + string.Join(Environment.NewLine, warnings);
                    AppendOutputText(Environment.NewLine + message + Environment.NewLine);
                    DialogTheme.ShowMessage(this, message, "Validate Script", MessageBoxIcon.Warning, _isDarkMode, _dialogFont);
                }
            }
            catch (Exception ex)
            {
                var message = ScriptValidationFormatter.FormatExceptionMessage(ex);
                AppendOutputText(Environment.NewLine + message + Environment.NewLine);
                DialogTheme.ShowMessage(this, message, "Validate Script", MessageBoxIcon.Error, _isDarkMode, _dialogFont);
            }
        }

#if DEBUG
        private void memoryDebuggerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var dialog = new MemoryDebuggerDialog(
                CaptureMemoryDebuggerSnapshot,
                TrimMemoryPressureNow,
                AggressiveTrimMemoryNow,
                _isDarkMode);
            DialogTheme.SetDialogFont(dialog, _dialogFont);
            dialog.ShowDialog(this);
        }

        private void viewAllPopupsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var demoSteps = new List<(string Name, Func<DialogResult> Show)>
            {
                (
                    "No Icon (OK)",
                    () => DialogTheme.Show(
                        this,
                        "Sample popup with no icon and OK button.",
                        "Popup Gallery",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.None)
                ),
                (
                    "Information (OK)",
                    () => DialogTheme.Show(
                        this,
                        "Sample informational popup.\n\nUse this to review spacing, icon alignment, and button styling.",
                        "Popup Gallery",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information)
                ),
                (
                    "Warning (OK)",
                    () => DialogTheme.Show(
                        this,
                        "Sample warning popup with wrapped text to verify multi-line readability in the themed dialog.",
                        "Popup Gallery",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning)
                ),
                (
                    "Error (OK)",
                    () => DialogTheme.Show(
                        this,
                        "Sample error popup.",
                        "Popup Gallery",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error)
                ),
                (
                    "Question (Yes/No)",
                    () => DialogTheme.Show(
                        this,
                        "Sample confirmation popup using Yes/No buttons.",
                        "Popup Gallery",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question)
                ),
                (
                    "Question (Yes/No/Cancel)",
                    () => DialogTheme.Show(
                        this,
                        "Sample confirmation popup using Yes/No/Cancel buttons.",
                        "Popup Gallery",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question)
                ),
                (
                    "Unsaved Preset Diff",
                    () =>
                    {
                        var savedCommands = "- print: Starting backup\n- send: show version\n- wait: 1000\n- send: show interfaces status";
                        var currentCommands = "- print: Starting backup\n- send: show version\n- wait: 2500\n- send: show interfaces status\n- print: Backup completed";

                        using var dialog = new UnsavedPresetDiffDialog(
                            "Sample/QA Preset",
                            "Sample/QA Preset",
                            30,
                            "45",
                            savedCommands,
                            currentCommands,
                            _isDarkMode);
                        DialogTheme.SetDialogFont(dialog, _dialogFont);
                        return dialog.ShowDialog(this);
                    }
                )
            };

            var intro = DialogTheme.Show(
                this,
                $"Popup gallery will walk through {demoSteps.Count} popup styles.\n\nAfter each sample, you can continue or stop.",
                "View All Popups",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);

            if (intro != DialogResult.OK)
                return;

            for (var i = 0; i < demoSteps.Count; i++)
            {
                var step = demoSteps[i];
                step.Show();

                if (i == demoSteps.Count - 1)
                    break;

                var continueResult = DialogTheme.Show(
                    this,
                    $"Shown {i + 1} of {demoSteps.Count}: {step.Name}\n\nContinue to the next popup?",
                    "View All Popups",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (continueResult != DialogResult.Yes)
                    break;
            }

            DialogTheme.Show(
                this,
                "Popup gallery complete.",
                "View All Popups",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private (long WorkingSetBytes, long PrivateBytes, long ManagedHeapBytes, string Summary) CaptureMemoryDebuggerSnapshot()
        {
            var process = Process.GetCurrentProcess();
            process.Refresh();

            long historyLabelChars = 0;
            foreach (var entry in _outputHistory)
            {
                historyLabelChars += entry.Label?.Length ?? 0;
            }

            int selectedHostCacheCount = _currentHostResults?.Count ?? 0;
            long selectedHostCacheChars = 0;
            if (_currentHostResults != null)
            {
                foreach (var host in _currentHostResults)
                {
                    selectedHostCacheChars += host?.Output?.Length ?? 0;
                }
            }

            var loadedPayloadId = _loadedHistoryPayloadId;
            var loadedPayload = _loadedHistoryPayload;
            var loadedPayloadOutputChars = loadedPayload?.Output?.Length ?? 0;
            var loadedPayloadHostOutputChars = 0L;
            var loadedPayloadHostCount = loadedPayload?.HostResults?.Count ?? 0;
            if (loadedPayload?.HostResults != null)
            {
                foreach (var host in loadedPayload.HostResults)
                {
                    loadedPayloadHostOutputChars += host?.Output?.Length ?? 0;
                }
            }

            var loadedDetailsCommandChars = loadedPayload?.Details?.Commands?.Length ?? 0;
            var loadedDetailsVariableChars = 0L;
            var loadedDetailsTranscriptChars = 0L;
            var loadedDetailsTranscriptCount = 0;
            if (loadedPayload?.Details?.Hosts != null)
            {
                foreach (var host in loadedPayload.Details.Hosts)
                {
                    if (host?.Variables == null)
                        continue;

                    foreach (var kvp in host.Variables)
                    {
                        loadedDetailsVariableChars += (kvp.Key?.Length ?? 0) + (kvp.Value?.Length ?? 0);
                    }
                }
            }

            if (loadedPayload?.Details?.InteractiveSessions != null)
            {
                foreach (var session in loadedPayload.Details.InteractiveSessions)
                {
                    var transcriptLength = session?.Transcript?.Length ?? 0;
                    if (transcriptLength <= 0)
                        continue;

                    loadedDetailsTranscriptCount++;
                    loadedDetailsTranscriptChars += transcriptLength;
                }
            }

            long presetChars = 0;
            foreach (var preset in _presetManager.Presets.Values)
            {
                presetChars += preset?.Commands?.Length ?? 0;
            }

            long hostGridChars = 0;
            int hostGridCellCount = 0;
            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                if (row.IsNewRow)
                    continue;

                foreach (DataGridViewCell cell in row.Cells)
                {
                    var value = cell.Value?.ToString();
                    if (string.IsNullOrEmpty(value))
                        continue;

                    hostGridCellCount++;
                    hostGridChars += value.Length;
                }
            }

            var scriptChars = txtCommand.Text?.Length ?? 0;
            var visibleOutputChars = txtOutput.TextLength;
            var pooledConnectionCount = _sshService.ConnectionPool?.Count ?? 0;
            var terminalHistoryLimit = SshTerminalOptionsFactory.DefaultHistoryMaxLength;
            int outputBufferChars;
            lock (_outputBufferLock)
            {
                outputBufferChars = _outputBuffer.Length;
            }

            var configPath = _configService.ConfigFilePath;
            long configSizeBytes = 0;
            var historyIndexPath = Path.Combine(Path.GetDirectoryName(configPath) ?? string.Empty, "history.index.json");
            var historyRunFolderPath = Path.Combine(Path.GetDirectoryName(configPath) ?? string.Empty, "history");
            long historyIndexSizeBytes = 0;
            long historyRunFilesSizeBytes = 0;
            int historyRunFileCount = 0;
            try
            {
                if (File.Exists(configPath))
                {
                    configSizeBytes = new FileInfo(configPath).Length;
                }

                if (File.Exists(historyIndexPath))
                {
                    historyIndexSizeBytes = new FileInfo(historyIndexPath).Length;
                }

                if (Directory.Exists(historyRunFolderPath))
                {
                    var runFiles = Directory.GetFiles(historyRunFolderPath, "*.json", SearchOption.TopDirectoryOnly);
                    historyRunFileCount = runFiles.Length;
                    foreach (var runFile in runFiles)
                    {
                        historyRunFilesSizeBytes += new FileInfo(runFile).Length;
                    }
                }
            }
            catch
            {
                configSizeBytes = 0;
                historyIndexSizeBytes = 0;
                historyRunFilesSizeBytes = 0;
                historyRunFileCount = 0;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Captured: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Config path: {configPath}");
            sb.AppendLine($"Config size: {FormatBytes(configSizeBytes)}");
            sb.AppendLine($"History index path: {historyIndexPath}");
            sb.AppendLine($"History index size: {FormatBytes(historyIndexSizeBytes)}");
            sb.AppendLine($"History run files: {historyRunFileCount:N0} ({FormatBytes(historyRunFilesSizeBytes)})");
            sb.AppendLine($"SSH pooling: {(_sshService.UseConnectionPooling ? "enabled" : "disabled")} ({pooledConnectionCount:N0} pooled connection(s))");
            sb.AppendLine($"Terminal history limit: {terminalHistoryLimit:N0} lines ({SshTerminalOptionsFactory.DefaultColumns}x{SshTerminalOptionsFactory.DefaultRows})");
            sb.AppendLine($"Loaded history payload id: {loadedPayloadId ?? "[none]"}");
            sb.AppendLine();
            sb.AppendLine("Large managed text buckets (estimated):");
            AppendMemoryBucket(sb, "History metadata labels", historyLabelChars, _outputHistory.Count);
            AppendMemoryBucket(sb, "Selected host output cache", selectedHostCacheChars, selectedHostCacheCount);
            AppendMemoryBucket(sb, "Loaded history payload output", loadedPayloadOutputChars, loadedPayload == null ? 0 : 1);
            AppendMemoryBucket(sb, "Loaded history payload host output", loadedPayloadHostOutputChars, loadedPayloadHostCount);
            AppendMemoryBucket(sb, "Loaded detail command snapshot", loadedDetailsCommandChars, loadedPayload?.Details == null ? 0 : 1);
            AppendMemoryBucket(sb, "Loaded detail variable text", loadedDetailsVariableChars, loadedPayload?.Details?.Hosts?.Count ?? 0);
            AppendMemoryBucket(sb, "Loaded detail interactive transcripts", loadedDetailsTranscriptChars, loadedDetailsTranscriptCount);
            AppendMemoryBucket(sb, "Preset command text", presetChars, _presetManager.Presets.Count);
            AppendMemoryBucket(sb, "Host grid cell text", hostGridChars, hostGridCellCount);
            AppendMemoryBucket(sb, "Script editor text", scriptChars, 1);
            AppendMemoryBucket(sb, "Output panel text", visibleOutputChars, 1);
            AppendMemoryBucket(sb, "Live output buffer", outputBufferChars, 1);

            sb.AppendLine();
            sb.AppendLine("Notes:");
            sb.AppendLine("- Estimates assume UTF-16 string storage (~2 bytes per char).");
            sb.AppendLine("- History payload files are stored on disk and loaded lazily per selected run.");
            sb.AppendLine("- Loaded interactive transcripts can dominate managed memory when an entry with large details is selected.");
            sb.AppendLine("- Terminal emulation buffers (cells/colors/scrollback) can consume large managed memory outside these text estimates.");
            sb.AppendLine("- Private bytes include native allocations from WinForms controls and SSH libraries.");

            var managedHeapBytes = GC.GetTotalMemory(true);
            return (process.WorkingSet64, process.PrivateMemorySize64, managedHeapBytes, sb.ToString());
        }

        private string TrimMemoryPressureNow()
        {
            long removedChars = 0;
            int trimmedOutputBuffer = 0;

            var trimmedItemCount = trimmedOutputBuffer;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var sb = new StringBuilder();
            sb.AppendLine($"Trim complete. Estimated reduction: {FormatBytes(removedChars * 2L)}");
            sb.AppendLine($"Trimmed items: {trimmedItemCount:N0}");
            if (trimmedOutputBuffer > 0)
            {
                sb.AppendLine($"- Live output buffer: {trimmedOutputBuffer:N0}");
            }

            if (trimmedItemCount == 0)
            {
                sb.AppendLine("No oversized payloads needed trimming.");
            }
            sb.AppendLine("- Forced full GC cycle executed.");

            return sb.ToString();
        }

        private string AggressiveTrimMemoryNow()
        {
            var process = Process.GetCurrentProcess();
            process.Refresh();

            var beforeWorkingSet = process.WorkingSet64;
            var beforePrivate = process.PrivateMemorySize64;
            var beforeManaged = GC.GetTotalMemory(false);

            var visibleText = txtOutput.Text;
            RecreateOutputTextBoxIfNeeded(visibleText.Length, force: true);
            txtOutput.Text = visibleText;
            txtOutput.ClearUndo();
            ScrollOutputToEnd();

            ClearLoadedHistoryPayload();

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var workingSetTrimApplied = false;
            try
            {
                workingSetTrimApplied = NativeMethods.EmptyWorkingSet(process.Handle);
            }
            catch
            {
                // Best effort trim.
            }

            process.Refresh();
            var afterWorkingSet = process.WorkingSet64;
            var afterPrivate = process.PrivateMemorySize64;
            var afterManaged = GC.GetTotalMemory(false);

            var workingSetDelta = beforeWorkingSet - afterWorkingSet;
            var privateDelta = beforePrivate - afterPrivate;
            var managedDelta = beforeManaged - afterManaged;

            var sb = new StringBuilder();
            sb.AppendLine("Aggressive trim complete.");
            sb.AppendLine($"Working set: {FormatBytes(beforeWorkingSet)} -> {FormatBytes(afterWorkingSet)}");
            sb.AppendLine($"Private bytes: {FormatBytes(beforePrivate)} -> {FormatBytes(afterPrivate)}");
            sb.AppendLine($"Managed heap: {FormatBytes(beforeManaged)} -> {FormatBytes(afterManaged)}");
            sb.AppendLine($"Working set delta: {(workingSetDelta >= 0 ? "-" : "+")}{FormatBytes(Math.Abs(workingSetDelta))}");
            sb.AppendLine($"Private bytes delta: {(privateDelta >= 0 ? "-" : "+")}{FormatBytes(Math.Abs(privateDelta))}");
            sb.AppendLine($"Managed heap delta: {(managedDelta >= 0 ? "-" : "+")}{FormatBytes(Math.Abs(managedDelta))}");
            sb.AppendLine(workingSetTrimApplied
                ? "- Requested OS working-set trim."
                : "- Could not apply OS working-set trim.");
            sb.AppendLine("- Note: private bytes can remain high when native heaps keep committed pages for reuse.");
            return sb.ToString();
        }

        private static void AppendMemoryBucket(StringBuilder sb, string name, long charCount, int entryCount)
        {
            var estimatedBytes = Math.Max(0, charCount) * 2L;
            sb.AppendLine($"- {name}: {FormatBytes(estimatedBytes)} ({entryCount:N0} item(s), {charCount:N0} chars)");
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes:N0} B";

            string[] units = ["KB", "MB", "GB", "TB"];
            var unitIndex = -1;
            double value = bytes;
            do
            {
                value /= 1024d;
                unitIndex++;
            }
            while (value >= 1024d && unitIndex < units.Length - 1);

            return $"{value:N2} {units[unitIndex]}";
        }
#endif

        private void debugModeToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            _sshService.DebugMode = debugModeToolStripMenuItem.Checked;
            UpdateStatusBar(debugModeToolStripMenuItem.Checked ? "Debug mode enabled" : "Debug mode disabled");
        }


        private void documentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/nosmircss/SSH_Helper",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void scriptingDocumentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/nosmircss/SSH_Helper/blob/master/SCRIPTING.md",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var dlg = new AboutDialog(ApplicationName, ApplicationVersion, _isDarkMode);
            DialogTheme.SetDialogFont(dlg, _dialogFont);
            dlg.ShowDialog(this);
        }

        private void addColumnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddColumn();
        }

        private void renameColumnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RenameColumn(_rightClickedColumnIndex);
        }

        private void deleteColumnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteColumn(_rightClickedColumnIndex);
        }

        private void deleteRowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteRow(_rightClickedRowIndex);
        }

        private void selectAllHostsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetAllCheckboxes(true);
        }

        private void deselectAllHostsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetAllCheckboxes(false);
        }

        private void invertSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                if (!row.IsNewRow)
                {
                    bool current = row.Cells[SelectColumnName].Value is true;
                    row.Cells[SelectColumnName].Value = !current;
                }
            }
            _selectAllChecked = false; // Reset since invert breaks the "all selected" state
            dgv_variables.InvalidateColumn(dgv_variables.Columns[SelectColumnName]!.Index);
            UpdateSelectionCount();
        }

        private void addPresetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddPreset();
        }

        private void contextPresetLstAdd_Click(object sender, EventArgs e)
        {
            AddPreset();
        }

        private void duplicatePresetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DuplicatePreset(preferContextSource: sender == ctxDuplicatePreset);
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RenamePreset(preferContextSource: sender == ctxRenamePreset);
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeletePreset(preferContextSource: sender == ctxDeletePreset);
        }

        private void toggleSortingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _currentSortMode = _currentSortMode switch
            {
                PresetSortMode.Ascending => PresetSortMode.Descending,
                PresetSortMode.Descending => PresetSortMode.Manual,
                PresetSortMode.Manual => PresetSortMode.Ascending,
                _ => PresetSortMode.Ascending
            };

            // When switching to manual mode, initialize the order from current presets
            if (_currentSortMode == PresetSortMode.Manual && _manualPresetOrder.Count == 0)
            {
                // Build order from current presets
                foreach (var presetName in _presetManager.Presets.Keys)
                {
                    if (!string.IsNullOrEmpty(presetName))
                        _manualPresetOrder.Add(presetName);
                }
            }

            RefreshPresetList();
            UpdateSortModeIndicator();
            UpdateStatusBar($"Sort mode: {_currentSortMode}");
            ClearPresetDeleteUndoHistory();
        }

        private void UpdateSortModeIndicator()
        {
            ctxToggleSorting.Text = $"Toggle Sorting ({_currentSortMode})";
        }

        private void InitializeFlowCanvasMenuItem()
        {
            var flowCanvasItem = new ToolStripMenuItem("Flow Canvas...");
            flowCanvasItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.F;
            flowCanvasItem.Click += (_, _) => OpenFlowCanvas();
            // Insert before the last item (Debug Mode) with a separator
            var editMenu = editToolStripMenuItem;
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(flowCanvasItem);
        }

        private void OpenFlowCanvas()
        {
            // Reuse existing window if still open
            if (_flowCanvasForm != null && !_flowCanvasForm.IsDisposed)
            {
                _flowCanvasForm.BringToFront();
                _flowCanvasForm.Activate();
                return;
            }

            var config = _configService.GetCurrent();
            _flowCanvasForm = new FlowCanvasForm(config.DarkMode);
            _flowCanvasForm.FormClosed += (_, _) => _flowCanvasForm = null;
            _flowCanvasForm.Show(this);
        }

        private void InitializeFolderExpandCollapseContextMenuItems()
        {
            _ctxFolderExpandCollapseSeparator.Name = "ctxFolderExpandCollapseSeparator";
            _ctxExpandAllSubfolders.Name = "ctxExpandAllSubfolders";
            _ctxExpandAllSubfolders.Text = "E&xpand All Subfolders";
            _ctxExpandAllSubfolders.Click += CtxExpandAllSubfolders_Click;

            _ctxCollapseAllSubfolders.Name = "ctxCollapseAllSubfolders";
            _ctxCollapseAllSubfolders.Text = "C&ollapse All Subfolders";
            _ctxCollapseAllSubfolders.Click += CtxCollapseAllSubfolders_Click;

            if (contextPresetLst.Items.Contains(_ctxExpandAllSubfolders))
            {
                return;
            }

            contextPresetLst.Items.Add(_ctxFolderExpandCollapseSeparator);
            contextPresetLst.Items.Add(_ctxExpandAllSubfolders);
            contextPresetLst.Items.Add(_ctxCollapseAllSubfolders);
        }

        private void InitializeFolderBaseEnvironmentContextMenuItem()
        {
            _ctxFolderBaseEnvironment.Name = "ctxFolderBaseEnvironment";
            _ctxFolderBaseEnvironment.Text = "Folder Base En&vironment...";
            _ctxFolderBaseEnvironment.Click += CtxFolderBaseEnvironment_Click;

            if (contextPresetLst.Items.Contains(_ctxFolderBaseEnvironment))
            {
                return;
            }

            var deleteFolderIndex = contextPresetLst.Items.IndexOf(ctxDeleteFolder);
            if (deleteFolderIndex >= 0)
            {
                contextPresetLst.Items.Insert(deleteFolderIndex, _ctxFolderBaseEnvironment);
                return;
            }

            contextPresetLst.Items.Add(_ctxFolderBaseEnvironment);
        }

        private void CtxFolderBaseEnvironment_Click(object? sender, EventArgs e)
        {
            var folderNode = ResolveContextMenuFolderNode();
            if (folderNode?.Tag is not PresetNodeTag folderTag || !folderTag.IsFolder)
                return;

            var folderPath = folderTag.Name;

            BeginInvoke((Action)(() =>
            {
                if (IsDisposed)
                    return;

                ShowFolderBaseEnvironmentDialog(folderPath);
            }));
        }

        private void CtxExpandAllSubfolders_Click(object? sender, EventArgs e)
        {
            ExpandCollapseFolderSubtree(expand: true);
        }

        private void CtxCollapseAllSubfolders_Click(object? sender, EventArgs e)
        {
            ExpandCollapseFolderSubtree(expand: false);
        }

        private void ExpandCollapseFolderSubtree(bool expand)
        {
            var folderNode = ResolveContextMenuFolderNode();
            if (folderNode?.Tag is not PresetNodeTag folderTag || !folderTag.IsFolder)
            {
                return;
            }

            var treeView = folderNode.TreeView;
            var topNodeBefore = PresetTreeViewportRestorer.Capture(treeView?.TopNode);

            var folderPaths = new List<string>();
            CollectFolderPaths(folderNode, folderPaths);
            if (folderPaths.Count == 0)
            {
                return;
            }

            _suppressExpandCollapseEvents = true;
            treeView?.BeginUpdate();
            try
            {
                if (expand)
                {
                    folderNode.ExpandAll();
                }
                else
                {
                    CollapseFolderNodeRecursive(folderNode);
                }

                // Keep viewport anchoring while redraw is still suspended
                // so users never see the temporary scroll jump.
                if (treeView != null)
                {
                    PresetTreeViewportRestorer.TryRestoreTopNode(
                        treeView,
                        treeView.Nodes,
                        topNodeBefore,
                        PresetTreeViewportRestorer.Capture(folderNode));
                }
            }
            finally
            {
                treeView?.EndUpdate();
                _suppressExpandCollapseEvents = false;
            }

            foreach (var folderPath in folderPaths)
            {
                if (_presetManager.Folders.TryGetValue(folderPath, out var folderInfo) &&
                    folderInfo.IsExpanded == expand)
                {
                    continue;
                }

                _presetManager.SetFolderExpanded(folderPath, expand);
            }

            UpdateStatusBar(expand
                ? $"Expanded all folders under '{folderTag.Name}'"
                : $"Collapsed all folders under '{folderTag.Name}'");
        }

        private TreeNode? ResolveContextMenuFolderNode()
        {
            if (_contextMenuSourceTreeView?.SelectedNode?.Tag is PresetNodeTag sourceTag && sourceTag.IsFolder)
            {
                return _contextMenuSourceTreeView.SelectedNode;
            }

            if (trvPresets.SelectedNode?.Tag is PresetNodeTag presetTag && presetTag.IsFolder)
            {
                return trvPresets.SelectedNode;
            }

            return null;
        }

        private static bool HasFolderDescendants(TreeNode? folderNode)
        {
            if (folderNode == null)
            {
                return false;
            }

            foreach (TreeNode child in folderNode.Nodes)
            {
                if (child.Tag is PresetNodeTag childTag && childTag.IsFolder)
                {
                    return true;
                }

                if (HasFolderDescendants(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CollectFolderPaths(TreeNode node, List<string> folderPaths)
        {
            if (node.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                folderPaths.Add(tag.Name);
            }

            foreach (TreeNode child in node.Nodes)
            {
                CollectFolderPaths(child, folderPaths);
            }
        }

        private static void CollapseFolderNodeRecursive(TreeNode node)
        {
            foreach (TreeNode child in node.Nodes)
            {
                CollapseFolderNodeRecursive(child);
            }

            if (node.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                node.Collapse();
            }
        }

        private void ExportPreset_Click(object? sender, EventArgs e)
        {
            ExportPreset(preferContextSource: sender == ctxExportPreset);
        }

        private void ImportPreset_Click(object? sender, EventArgs e)
        {
            ImportPreset();
        }

        private void ctxToggleFavorite_Click(object? sender, EventArgs e)
        {
            ToggleFavorite();
        }

        private void ContextPresetLst_Opening(object? sender, CancelEventArgs e)
        {
            // Determine which TreeView triggered the context menu
            if (sender is ContextMenuStrip cms && cms.SourceControl is TreeView sourceTreeView)
            {
                _contextMenuSourceTreeView = sourceTreeView;
            }
            else
            {
                // Fallback: check which TreeView has focus or was most recently clicked
                _contextMenuSourceTreeView = trvFavorites.Focused ? trvFavorites : trvPresets;
            }

            var tag = _contextMenuSourceTreeView.SelectedNode?.Tag as PresetNodeTag;
            bool isFolder = tag?.IsFolder == true;
            bool isPreset = tag != null && !tag.IsFolder;
            bool hasSelection = tag != null;
            bool isFavoritesTab = _contextMenuSourceTreeView == trvFavorites;
            bool hasSubfolders = isFolder && HasFolderDescendants(_contextMenuSourceTreeView?.SelectedNode);

            // On Favorites tab, only show limited options: Rename and Toggle Favorite
            if (isFavoritesTab)
            {
                // Determine if Toggle Favorite should be shown
                // Only show for folders (which are directly favorited) or presets that are themselves favorited
                bool showToggleFavorite = false;
                if (isFolder)
                {
                    showToggleFavorite = true;
                }
                else if (isPreset)
                {
                    var preset = _presetManager.Get(tag!.Name);
                    showToggleFavorite = preset?.IsFavorite == true;
                }

                // Hide most items on Favorites tab
                ctxAddPreset.Visible = false;
                ctxDuplicatePreset.Visible = false;
                ctxRenamePreset.Visible = isPreset;
                ctxDeletePreset.Visible = false;
                ctxToggleFavorite.Visible = showToggleFavorite;
                ctxExportPreset.Visible = false;
                ctxImportPreset.Visible = false;
                ctxToggleSorting.Visible = false;
                ctxAddFolder.Visible = false;
                ctxRenameFolder.Visible = isFolder;
                ctxDeleteFolder.Visible = false;
                _ctxFolderBaseEnvironment.Visible = false;
                ctxMoveToFolder.Visible = false;
                _ctxFolderExpandCollapseSeparator.Visible = false;
                _ctxExpandAllSubfolders.Visible = false;
                _ctxCollapseAllSubfolders.Visible = false;

                // Hide all separators on Favorites tab
                toolStripSeparator6.Visible = false;
                toolStripSeparator7.Visible = false;
                toolStripSeparatorFolders.Visible = false;
                return;
            }

            // Presets tab - show full context menu
            // Preset-specific items
            ctxAddPreset.Visible = true;
            ctxDuplicatePreset.Visible = isPreset;
            ctxRenamePreset.Visible = isPreset;
            ctxDeletePreset.Visible = isPreset;
            ctxToggleFavorite.Visible = hasSelection;
            ctxExportPreset.Visible = isPreset;
            ctxImportPreset.Visible = true;
            ctxToggleSorting.Visible = true;

            // Folder-specific items
            ctxAddFolder.Visible = true;
            ctxRenameFolder.Visible = isFolder;
            ctxDeleteFolder.Visible = isFolder;
            _ctxFolderBaseEnvironment.Visible = isFolder && _contextMenuSourceTreeView == trvPresets;
            _ctxFolderExpandCollapseSeparator.Visible = hasSubfolders;
            _ctxExpandAllSubfolders.Visible = hasSubfolders;
            _ctxCollapseAllSubfolders.Visible = hasSubfolders;

            // Move to folder - only for presets
            ctxMoveToFolder.Visible = isPreset;
            if (isPreset)
            {
                BuildMoveToFolderSubmenu(tag!.Name);
            }

            // Show separators appropriately
            toolStripSeparator6.Visible = isPreset;
            toolStripSeparator7.Visible = true;
            toolStripSeparatorFolders.Visible = true;
        }

        private void ShowFolderBaseEnvironmentDialog(string folderPath)
        {
            _baseEnvironmentName = _environmentService.GetBaseEnvironmentName();
            _presetManager.Folders.TryGetValue(folderPath, out var folderInfo);
            var explicitBaseEnvironment = folderInfo?.BaseEnvironment;
            var inheritedResolution = ResolveEffectiveBaseEnvironment(FolderPathUtility.GetParentPath(folderPath));

            var options = new List<ChoiceOption>
            {
                new()
                {
                    Label = FolderBaseEnvironmentSummaryFormatter.FormatInheritChoiceLabel(inheritedResolution),
                    Value = FolderBaseEnvironmentInheritChoiceValue
                }
            };

            foreach (var environmentName in _environmentService.GetEnvironmentNames())
            {
                options.Add(new ChoiceOption
                {
                    Label = environmentName,
                    Value = environmentName
                });
            }

            var defaultValue = string.IsNullOrWhiteSpace(explicitBaseEnvironment)
                ? FolderBaseEnvironmentInheritChoiceValue
                : explicitBaseEnvironment;

            using var dialog = new ScriptChooseDialog(
                $"Select the base environment for folder '{folderPath}'.",
                options,
                defaultValue,
                "Folder Base Environment");
            DialogTheme.SetDialogFont(dialog, _dialogFont);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var selectedValue = dialog.SelectedValue;
            if (string.IsNullOrWhiteSpace(selectedValue))
                return;

            if (string.Equals(selectedValue, FolderBaseEnvironmentInheritChoiceValue, StringComparison.Ordinal))
            {
                ApplyFolderBaseEnvironmentSelection(folderPath, null, inheritedResolution.EnvironmentName);
                return;
            }

            ApplyFolderBaseEnvironmentSelection(folderPath, selectedValue, selectedValue);
        }

        private void ApplyFolderBaseEnvironmentSelection(string folderPath, string? explicitEnvironmentName, string statusEnvironmentName)
        {
            if (!_presetManager.SetFolderBaseEnvironment(folderPath, explicitEnvironmentName))
                return;

            TryApplyFolderEnvironment(folderPath);
            DisplayFolderSummary(folderPath);
            ClearPresetDeleteUndoHistory();

            if (string.IsNullOrWhiteSpace(explicitEnvironmentName))
            {
                UpdateStatusBar($"Folder '{folderPath}' now inherits base environment '{statusEnvironmentName}'.");
                return;
            }

            UpdateStatusBar($"Folder '{folderPath}' base environment set to '{statusEnvironmentName}'.");
        }

        private void BuildMoveToFolderSubmenu(string presetName)
        {
            ctxMoveToFolder.DropDownItems.Clear();

            var preset = _presetManager.Get(presetName);
            string? currentFolder = preset?.Folder;

            // Add "Root" option
            var rootItem = new ToolStripMenuItem("(Root Level)")
            {
                Checked = string.IsNullOrEmpty(currentFolder),
                Tag = (string?)null
            };
            rootItem.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(currentFolder))
                {
                    return;
                }

                var preActionExpandState = CapturePresetTreeExpandState();
                var movingNode = FindPresetNodeByName(trvPresets.Nodes, presetName);
                _presetManager.MovePresetToFolder(presetName, null);

                bool usedIncrementalMutation = false;
                if (CanMutatePresetTreeIncrementally() && movingNode != null)
                {
                    ApplyIncrementalPresetTreeMutation(
                        () => { usedIncrementalMutation = TryReinsertExistingPresetNodeIncrementally(movingNode, presetName); },
                        () => movingNode);
                }

                if (!usedIncrementalMutation)
                {
                    RefreshPresetListPreservingCurrentFilter(expandStatesOverride: preActionExpandState);
                    SelectPresetByName(presetName, ensureVisible: false);
                }

                if (ShouldRefreshFavoritesForPreset(presetName, currentFolder))
                {
                    RefreshFavoritesListPreservingCurrentFilter();
                }

                EnsurePresetLoadedInEditor(presetName);
                ClearPresetDeleteUndoHistory();
            };
            ctxMoveToFolder.DropDownItems.Add(rootItem);

            // Build hierarchical folder menu
            var allFolders = _presetManager.GetFolders().OrderBy(f => f).ToList();
            if (allFolders.Count > 0)
            {
                ctxMoveToFolder.DropDownItems.Add(new ToolStripSeparator());
            }

            // Dictionary to track menu items by path for nesting
            var menuItems = new Dictionary<string, ToolStripMenuItem>();

            foreach (var folderPath in allFolders)
            {
                var folderName = FolderPathUtility.GetFolderName(folderPath);
                var parentPath = FolderPathUtility.GetParentPath(folderPath);

                var folderItem = new ToolStripMenuItem(folderName)
                {
                    Checked = string.Equals(currentFolder, folderPath, StringComparison.Ordinal),
                    Tag = folderPath
                };

                // Capture folderPath for closure
                var targetPath = folderPath;
                folderItem.Click += (s, e) =>
                {
                    if (string.Equals(currentFolder, targetPath, StringComparison.Ordinal))
                    {
                        return;
                    }

                    var preActionExpandState = CapturePresetTreeExpandState();
                    var movingNode = FindPresetNodeByName(trvPresets.Nodes, presetName);
                    _presetManager.MovePresetToFolder(presetName, targetPath);

                    bool usedIncrementalMutation = false;
                    if (CanMutatePresetTreeIncrementally() && movingNode != null)
                    {
                        ApplyIncrementalPresetTreeMutation(
                            () => { usedIncrementalMutation = TryReinsertExistingPresetNodeIncrementally(movingNode, presetName); },
                            () => movingNode);
                    }

                    if (!usedIncrementalMutation)
                    {
                        RefreshPresetListPreservingCurrentFilter(expandStatesOverride: preActionExpandState);
                        SelectPresetByName(presetName, ensureVisible: false);
                    }

                    if (ShouldRefreshFavoritesForPreset(presetName, currentFolder))
                    {
                        RefreshFavoritesListPreservingCurrentFilter();
                    }

                    EnsurePresetLoadedInEditor(presetName);
                    ClearPresetDeleteUndoHistory();
                };

                // Add to parent menu or root
                if (parentPath != null && menuItems.TryGetValue(parentPath, out var parentItem))
                {
                    parentItem.DropDownItems.Add(folderItem);
                }
                else
                {
                    ctxMoveToFolder.DropDownItems.Add(folderItem);
                }

                menuItems[folderPath] = folderItem;
            }
        }

        private void ctxAddFolder_Click(object? sender, EventArgs e)
        {
            // Check if a folder is selected - if so, create as subfolder
            string? parentPath = null;
            if (trvPresets.SelectedNode?.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                parentPath = tag.Name;
            }

            string prompt = parentPath != null
                ? $"Enter a name for the new subfolder in '{parentPath}':"
                : "Enter a name for the new folder (use / for nested paths, e.g., 'Network/Cisco'):";

            string folderName = ShowInputBox(
                prompt,
                "New Folder",
                "New Folder");

            if (string.IsNullOrWhiteSpace(folderName)) return;

            // Build full path
            string fullPath = parentPath != null
                ? FolderPathUtility.CombinePath(parentPath, folderName)
                : folderName;

            fullPath = _presetManager.GetUniqueFolderName(fullPath);

            if (_presetManager.CreateFolder(fullPath))
            {
                bool usedIncrementalMutation = false;
                if (CanMutatePresetTreeIncrementally())
                {
                    ApplyIncrementalPresetTreeMutation(
                        () => { usedIncrementalMutation = TryInsertFolderNodeIncrementally(fullPath, out _); });
                }

                if (!usedIncrementalMutation)
                {
                    RefreshPresetList();
                }

                UpdateStatusBar($"Folder '{fullPath}' created");
                ClearPresetDeleteUndoHistory();
            }
        }

        private void ctxRenameFolder_Click(object? sender, EventArgs e)
        {
            if (trvPresets.SelectedNode?.Tag is not PresetNodeTag tag || !tag.IsFolder)
                return;

            string oldPath = tag.Name;
            string oldFolderName = FolderPathUtility.GetFolderName(oldPath);
            string? parentPath = FolderPathUtility.GetParentPath(oldPath);

            string prompt = parentPath != null
                ? $"Enter a new name for the folder '{oldFolderName}' (in {parentPath}):"
                : $"Enter a new name for the folder '{oldFolderName}':";

            string newFolderName = Microsoft.VisualBasic.Interaction.InputBox(
                prompt,
                "Rename Folder",
                oldFolderName);

            if (string.IsNullOrWhiteSpace(newFolderName) || newFolderName == oldFolderName) return;

            // Build new full path
            string newPath = FolderPathUtility.CombinePath(parentPath, newFolderName);

            if (_presetManager.RenameFolder(oldPath, newPath))
            {
                RefreshPresetList();
                UpdateStatusBar($"Folder renamed to '{newPath}'");
                ClearPresetDeleteUndoHistory();
            }
            else
            {
                DialogTheme.Show(this, "A folder with that name already exists.", "Rename Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ctxDeleteFolder_Click(object? sender, EventArgs e)
        {
            if (trvPresets.SelectedNode?.Tag is not PresetNodeTag tag || !tag.IsFolder)
                return;

            string folderPath = tag.Name;
            string folderName = FolderPathUtility.GetFolderName(folderPath);
            string? parentPath = FolderPathUtility.GetParentPath(folderPath);

            // Count presets and subfolders
            int presetCount = _presetManager.CountPresetsInFolderAndDescendants(folderPath);
            int subfolderCount = _presetManager.CountDescendantFolders(folderPath);

            // Build message based on contents
            var messageParts = new List<string>();
            if (subfolderCount > 0)
                messageParts.Add($"{subfolderCount} subfolder(s)");
            if (presetCount > 0)
                messageParts.Add($"{presetCount} preset(s)");

            string contentsDescription = messageParts.Count > 0
                ? $" containing {string.Join(" and ", messageParts)}"
                : " (empty)";

            // If folder has presets, offer choice to delete or move them
            if (presetCount > 0)
            {
                string targetLocation = parentPath != null ? $"parent folder '{parentPath}'" : "root level";
                string message = $"Delete folder '{folderName}'{contentsDescription}?\n\n" +
                               $"Yes = Delete folder AND all presets\n" +
                               $"No = Delete folder but move presets to {targetLocation}\n" +
                               $"Cancel = Abort";

                var result = DialogTheme.Show(this, message, "Delete Folder", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Cancel)
                    return;

                bool deletePresets = result == DialogResult.Yes;

                if (deletePresets)
                {
                    // Clear editor before deleting to avoid "save changes?" prompt for deleted presets
                    _activePresetName = null;
                    txtPreset.Clear();
                    txtCommand.Clear();
                    txtTimeoutHeader.Clear();
                }

                if (TryDeleteFolderWithUndo(folderPath, deletePresets))
                {
                    RefreshPresetList();
                    RefreshFavoritesList();

                    string action = deletePresets ? "and its presets deleted" : "deleted (presets moved)";
                    UpdateStatusBar($"Folder '{folderPath}' {action}");
                }
            }
            else
            {
                // No presets, just confirm deletion
                string message = $"Delete folder '{folderName}'{contentsDescription}?";

                if (DialogTheme.Show(this, message, "Delete Folder", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (TryDeleteFolderWithUndo(folderPath, deletePresets: false))
                    {
                        RefreshPresetList();
                        RefreshFavoritesList();
                        UpdateStatusBar($"Folder '{folderPath}' deleted");
                    }
                }
            }
        }

        private void tsbAddFolder_Click(object? sender, EventArgs e)
        {
            // Reuse the context menu handler
            ctxAddFolder_Click(sender, e);
        }

        private void tsbDeleteFolder_Click(object? sender, EventArgs e)
        {
            if (trvPresets.SelectedNode?.Tag is not PresetNodeTag tag || !tag.IsFolder)
            {
                DialogTheme.Show(this, "Please select a folder to delete.", "Delete Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string folderPath = tag.Name;
            string folderName = FolderPathUtility.GetFolderName(folderPath);

            // Count presets and subfolders
            int presetCount = _presetManager.CountPresetsInFolderAndDescendants(folderPath);
            int subfolderCount = _presetManager.CountDescendantFolders(folderPath);

            // Build message based on contents
            var messageParts = new List<string>();
            if (subfolderCount > 0)
                messageParts.Add($"{subfolderCount} subfolder(s)");
            if (presetCount > 0)
                messageParts.Add($"{presetCount} preset(s)");

            string contentsDescription = messageParts.Count > 0
                ? $" and ALL its contents ({string.Join(" and ", messageParts)})"
                : "";

            string message = $"Are you sure you want to delete the folder '{folderName}'{contentsDescription}?";

            if (DialogTheme.Show(this, message, "Delete Folder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                // Clear editor before deleting to avoid "save changes?" prompt for deleted presets
                _activePresetName = null;
                txtPreset.Clear();
                txtCommand.Clear();
                txtTimeoutHeader.Clear();

                if (TryDeleteFolderWithUndo(folderPath, deletePresets: true))
                {
                    RefreshPresetList();
                    RefreshFavoritesList();
                    UpdateStatusBar($"Folder '{folderName}' and its presets deleted");
                }
            }
        }

        private void lstOutput_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressHistorySelectionChanged || !_historySelectionHandlingEnabled)
                return;

            ApplySelectedHistoryEntry();
        }

        private void ApplySelectedHistoryEntry()
        {
            var selectedEntryId = (lstOutput.SelectedItem as HistoryListItem)?.Id;
            var shouldRunHistorySwitchGc = !string.Equals(
                _lastHistorySelectionGcEntryId,
                selectedEntryId,
                StringComparison.Ordinal);

            try
            {
                if (lstOutput.SelectedItem is not HistoryListItem entry)
                {
                    _currentHostResults = null;
                    _selectedHistoryOutput = string.Empty;
                    lstHosts.Items.Clear();
                    historySplitContainer.Panel2Collapsed = true;
                    return;
                }

                if (!TryLoadHistoryPayload(entry.Id, out var payload))
                {
                    historySplitContainer.Panel2Collapsed = true;
                    lstHosts.Items.Clear();
                    _currentHostResults = null;
                    _selectedHistoryOutput = string.Empty;
                    SetOutputText(string.Empty);
                    return;
                }

                _selectedHistoryOutput = payload.Output ?? string.Empty;
                _currentHostResults = payload.HostResults != null && payload.HostResults.Count > 0
                    ? payload.HostResults
                    : null;
                entry.HasHostResults = _currentHostResults != null;
                var hasDetails = _historyIndexEntries.TryGetValue(entry.Id, out var existingIndexEntry)
                    ? existingIndexEntry.HasDetails
                    : payload.Details != null;
                entry.HasDetails = hasDetails;

                if (_historyIndexEntries.TryGetValue(entry.Id, out var indexEntry))
                {
                    indexEntry.HasHostResults = entry.HasHostResults;
                    indexEntry.HasDetails = hasDetails;
                }

                if (_currentHostResults != null)
                {
                    _suppressHostSelectionChanged = true;
                    lstHosts.BeginUpdate();
                    try
                    {
                        lstHosts.Items.Clear();
                        foreach (var hostResult in _currentHostResults)
                        {
                            lstHosts.Items.Add(hostResult);
                        }

                        lstHosts.ClearSelected();
                    }
                    finally
                    {
                        lstHosts.EndUpdate();
                        _suppressHostSelectionChanged = false;
                    }

                    historySplitContainer.Panel2Collapsed = false;
                    SetOutputText(_selectedHistoryOutput);
                    return;
                }

                historySplitContainer.Panel2Collapsed = true;
                lstHosts.Items.Clear();
                SetOutputText(_selectedHistoryOutput);
            }
            finally
            {
                _lastHistorySelectionGcEntryId = selectedEntryId;
                if (shouldRunHistorySwitchGc)
                {
                    RunHistorySwitchGc();
                }
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveHistoryEntry();
        }

        private void saveAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveAllHistory();
        }

        private void deleteEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteHistoryEntry();
        }

        private void deleteAllHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteAllHistory();
        }

        private void contextHistoryLst_Opening(object sender, CancelEventArgs e)
        {
            var hasSelection = lstOutput.SelectedItem is HistoryListItem;
            saveAsToolStripMenuItem.Enabled = hasSelection;
            deleteEntryToolStripMenuItem.Enabled = hasSelection;

            if (hasSelection && lstOutput.SelectedItem is HistoryListItem entry && entry.HasDetails)
            {
                viewDetailsToolStripMenuItem.Enabled = true;
                viewDetailsToolStripMenuItem.Text = "View Details...";
            }
            else
            {
                viewDetailsToolStripMenuItem.Enabled = false;
                viewDetailsToolStripMenuItem.Text = "View Details (not available)";
            }
        }

        private void contextHostLst_Opening(object sender, CancelEventArgs e)
        {
            if (lstOutput.SelectedItem is HistoryListItem entry &&
                lstHosts.SelectedItem is HostHistoryEntry hostEntry &&
                entry.HasDetails)
            {
                viewHostDetailsToolStripMenuItem.Enabled = true;
                viewHostDetailsToolStripMenuItem.Text = $"View Details ({hostEntry.HostAddress})...";
                return;
            }

            viewHostDetailsToolStripMenuItem.Enabled = false;
            viewHostDetailsToolStripMenuItem.Text = "View Details (not available)";
        }

        private void viewDetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ViewExecutionDetails();
        }

        private void viewHostDetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ViewExecutionDetailsForSelectedHost();
        }

        private void LstOutput_MouseDown(object? sender, MouseEventArgs e)
        {
            int index = lstOutput.IndexFromPoint(e.Location);
            if (index < 0 || index >= lstOutput.Items.Count)
                return;

            if (!IsHistorySelectionArmed())
                return;

            EnableHistorySelectionHandling();

            if (e.Button == MouseButtons.Right)
            {
                lstOutput.SelectedIndex = index;
            }
        }

        private void LstOutput_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                    if (!IsHistorySelectionArmed())
                        return;
                    EnableHistorySelectionHandling();
                    break;
            }
        }

        private void LstHosts_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = lstHosts.IndexFromPoint(e.Location);
                if (index >= 0 && index < lstHosts.Items.Count)
                {
                    lstHosts.SelectedIndex = index;
                }
            }
        }

        private void lstHosts_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressHostSelectionChanged)
                return;

            if (lstHosts.SelectedIndices.Count == 0)
            {
                SetOutputText(_selectedHistoryOutput);
                return;
            }

            if (!EnsureHostOutputsLoadedForSelection())
            {
                SetOutputText(_selectedHistoryOutput);
                return;
            }

            SetOutputText(BuildSelectedHostOutput());
        }

        private bool EnsureHostOutputsLoadedForSelection()
        {
            if (_loadedHistoryPayloadHasHostOutputs)
                return true;

            if (lstOutput.SelectedItem is not HistoryListItem entry)
                return false;

            var selectedIndices = lstHosts.SelectedIndices.Cast<int>().ToArray();
            if (selectedIndices.Length == 0)
                return false;

            if (!TryLoadHistoryPayload(entry.Id, out var payload, requireHostOutputs: true))
                return false;

            _currentHostResults = payload.HostResults != null && payload.HostResults.Count > 0
                ? payload.HostResults
                : null;
            if (_currentHostResults == null)
                return false;

            _suppressHostSelectionChanged = true;
            lstHosts.BeginUpdate();
            try
            {
                lstHosts.Items.Clear();
                foreach (var hostResult in _currentHostResults)
                {
                    lstHosts.Items.Add(hostResult);
                }

                foreach (var index in selectedIndices)
                {
                    if (index >= 0 && index < lstHosts.Items.Count)
                    {
                        lstHosts.SetSelected(index, true);
                    }
                }
            }
            finally
            {
                lstHosts.EndUpdate();
                _suppressHostSelectionChanged = false;
            }

            return true;
        }

        private string BuildSelectedHostOutput()
        {
            if (lstHosts.SelectedIndices.Count == 0)
                return _selectedHistoryOutput;

            var combinedOutput = new StringBuilder();
            bool isFirstSelectedHost = true;

            for (int i = 0; i < lstHosts.Items.Count; i++)
            {
                if (!lstHosts.GetSelected(i) || lstHosts.Items[i] is not HostHistoryEntry hostEntry)
                    continue;

                var output = hostEntry.Output ?? string.Empty;
                if (isFirstSelectedHost)
                {
                    output = output.TrimStart('\r', '\n');
                    isFirstSelectedHost = false;
                }

                combinedOutput.Append(output);
            }

            return combinedOutput.ToString();
        }

        private void lstHosts_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            // Draw background with theme-aware colors
            var bgColor = isSelected
                ? (_isDarkMode ? DarkSelectionBg : LightAccent)
                : (_isDarkMode ? DarkSurface1 : e.BackColor);
            using var bgBrush = new SolidBrush(bgColor);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            // Draw selection border
            if (isSelected)
            {
                using var borderPen = new Pen(_isDarkMode ? DarkSelectionBorder : LightSelectionBorder, 1);
                e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }

            var item = lstHosts.Items[e.Index];
            if (item is HostHistoryEntry hostEntry)
            {
                // Draw status icon
                var iconRect = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top + 2, 16, 16);
                var iconColor = hostEntry.WasCancelled
                    ? Color.FromArgb(255, 193, 7)
                    : hostEntry.Success ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69);
                var iconText = hostEntry.WasCancelled
                    ? "\u25A0"
                    : hostEntry.Success ? "\u2713" : "\u2717";

                using var iconFont = new Font("Segoe UI", 10F, FontStyle.Bold);
                using var iconBrush = new SolidBrush(iconColor);
                e.Graphics.DrawString(iconText, iconFont, iconBrush, iconRect.Left, iconRect.Top - 1);

                // Draw host address with theme-aware text color
                var textRect = new Rectangle(e.Bounds.Left + 24, e.Bounds.Top, e.Bounds.Width - 28, e.Bounds.Height);
                var textColor = _isDarkMode ? DarkTextPrimary : (isSelected ? Color.White : e.ForeColor);
                using var textBrush = new SolidBrush(textColor);
                SafeDrawString(e.Graphics, hostEntry.HostAddress, e.Font ?? lstHosts.Font, textBrush, textRect, StringFormat.GenericDefault);
            }
        }

        private void LstOutput_MeasureItem(object? sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstOutput.Items.Count)
            {
                e.ItemHeight = HistoryListLayout.GetMinimumItemHeight(lstOutput.Font);
                return;
            }

            var item = lstOutput.Items[e.Index];
            var text = item is HistoryListItem historyItem
                ? historyItem.Label
                : item?.ToString() ?? string.Empty;

            e.ItemHeight = HistoryListLayout.CalculateItemHeight(text, lstOutput.Font, lstOutput.ClientSize.Width);
        }

        private void LstOutput_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            // Draw background with theme-aware colors
            var bgColor = isSelected
                ? (_isDarkMode ? DarkSelectionBg : LightAccent)
                : (_isDarkMode ? DarkSurface1 : e.BackColor);
            using var bgBrush = new SolidBrush(bgColor);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            // Draw selection border for visual clarity
            if (isSelected)
            {
                using var borderPen = new Pen(_isDarkMode ? DarkSelectionBorder : LightSelectionBorder, 1);
                e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }

            // Get the item text
            var item = lstOutput.Items[e.Index];
            string text = item is HistoryListItem historyItem ? historyItem.Label : item?.ToString() ?? "";

            // Draw text with theme-aware color
            var textColor = _isDarkMode ? DarkTextPrimary : (isSelected ? Color.White : e.ForeColor);
            var textBounds = HistoryListLayout.GetTextBounds(e.Bounds);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                lstOutput.Font,
                textBounds,
                textColor,
                HistoryListLayout.TextDrawFlags);
        }

        private void ConfigureHistoryListLayout()
        {
            lstOutput.DrawMode = DrawMode.OwnerDrawVariable;
            lstOutput.ItemHeight = HistoryListLayout.GetMinimumItemHeight(lstOutput.Font);
            lstOutput.RefreshVariableItemHeights();
        }

        private void TreeView_DrawNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null || e.Bounds.IsEmpty) return;

            if (sender is not TreeView treeView) return;

            bool isSelected = e.Node == treeView.SelectedNode;

            // Draw background with theme-aware colors
            var rowBounds = new Rectangle(0, e.Bounds.Y, treeView.ClientSize.Width, e.Bounds.Height);

            if (isSelected)
            {
                // Use a prominent selection color whether focused or not (matches hosts grid)
                var selectionColor = _isDarkMode ? DarkSelectionBg : LightAccent;
                using var bgBrush = new SolidBrush(selectionColor);
                e.Graphics.FillRectangle(bgBrush, rowBounds);

                // Draw selection border for visual clarity
                using var borderPen = new Pen(_isDarkMode ? DarkSelectionBorder : LightSelectionBorder, 1);
                e.Graphics.DrawRectangle(borderPen, rowBounds.X, rowBounds.Y, rowBounds.Width - 1, rowBounds.Height - 1);
            }
            else
            {
                // Non-selected: fill with background color
                using var bgBrush = new SolidBrush(treeView.BackColor);
                e.Graphics.FillRectangle(bgBrush, rowBounds);
            }

            // Calculate text position (account for indentation and expand/collapse button)
            int indent = e.Node.Level * treeView.Indent + 19; // 19 pixels for the expand/collapse area

            // Theme-aware colors
            var lineColor = _isDarkMode ? DarkTextSecondary : Color.FromArgb(128, 128, 128);
            var arrowColor = _isDarkMode ? DarkTextSecondary : Color.FromArgb(96, 96, 96);
            var textColor = _isDarkMode ? DarkTextPrimary : (isSelected ? Color.White : treeView.ForeColor);

            // Draw tree lines
            if (treeView.ShowLines)
            {
                using var linePen = new Pen(lineColor, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
                int midY = e.Bounds.Y + e.Bounds.Height / 2;
                bool hasChildren = e.Node.Nodes.Count > 0;

                // Only draw lines for non-root nodes, or root nodes when ShowRootLines is enabled
                bool shouldDrawNodeLines = e.Node.Level > 0 || treeView.ShowRootLines;

                if (shouldDrawNodeLines && !hasChildren)
                {
                    // Draw horizontal line to leaf nodes only (folders have expand/collapse indicator)
                    int lineStartX = e.Node.Level * treeView.Indent + 8;
                    int lineEndX = indent - 2;
                    e.Graphics.DrawLine(linePen, lineStartX, midY, lineEndX, midY);

                    // Draw vertical line segment at this node's level
                    int vertX = e.Node.Level * treeView.Indent + 8;
                    bool isLastSibling = e.Node.NextNode == null;
                    int vertTop = e.Bounds.Y;
                    int vertBottom = isLastSibling ? midY : e.Bounds.Y + e.Bounds.Height;
                    e.Graphics.DrawLine(linePen, vertX, vertTop, vertX, vertBottom);
                }

                // Draw vertical continuation lines for ancestor levels
                var ancestor = e.Node.Parent;
                int ancestorLevel = e.Node.Level - 1;
                while (ancestor != null && ancestorLevel >= 0)
                {
                    // Draw continuation line if this ancestor has more siblings below
                    if (ancestor.NextNode != null)
                    {
                        // Only draw if it's not root level, or ShowRootLines is enabled
                        if (ancestorLevel > 0 || treeView.ShowRootLines)
                        {
                            int ancestorX = ancestorLevel * treeView.Indent + 8;
                            e.Graphics.DrawLine(linePen, ancestorX, e.Bounds.Y, ancestorX, e.Bounds.Y + e.Bounds.Height);
                        }
                    }
                    ancestor = ancestor.Parent;
                    ancestorLevel--;
                }
            }

            // Draw expand/collapse indicator if node has children
            if (e.Node.Nodes.Count > 0)
            {
                int arrowX = e.Node.Level * treeView.Indent + 4;
                int arrowY = e.Bounds.Y + (e.Bounds.Height / 2);
                using var arrowPen = new Pen(arrowColor, 1.5f);

                if (e.Node.IsExpanded)
                {
                    // Down arrow for expanded
                    e.Graphics.DrawLine(arrowPen, arrowX, arrowY - 2, arrowX + 4, arrowY + 2);
                    e.Graphics.DrawLine(arrowPen, arrowX + 4, arrowY + 2, arrowX + 8, arrowY - 2);
                }
                else
                {
                    // Right arrow for collapsed
                    e.Graphics.DrawLine(arrowPen, arrowX + 2, arrowY - 4, arrowX + 6, arrowY);
                    e.Graphics.DrawLine(arrowPen, arrowX + 6, arrowY, arrowX + 2, arrowY + 4);
                }
            }

            // Check if this is a folder node
            bool isFolder = e.Node.Tag is PresetNodeTag nodeTag && nodeTag.IsFolder;
            int iconWidth = 0;

            // Get text and strip any folder emoji characters
            string nodeText = e.Node.Text;
            if (isFolder)
            {
                // Remove folder emoji and any leading space
                nodeText = nodeText.Replace("\U0001F4C1", "").Replace("\uD83D\uDCC1", "").TrimStart();

                // Draw folder icon using Segoe UI Symbol (same as history section)
                iconWidth = 18;
                var iconColor = _isDarkMode ? Color.FromArgb(220, 180, 80) : Color.FromArgb(180, 140, 60);
                using var iconFont = new Font("Segoe UI Symbol", 9F);
                using var iconBrush = new SolidBrush(iconColor);
                var iconRect = new RectangleF(indent, e.Bounds.Y, iconWidth, e.Bounds.Height);
                using var iconSf = new StringFormat { LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("\U0001F4C1", iconFont, iconBrush, iconRect, iconSf);
            }

            // Draw text
            var textBounds = new Rectangle(indent + iconWidth, e.Bounds.Y, treeView.ClientSize.Width - indent - iconWidth, e.Bounds.Height);
            using var textBrush = new SolidBrush(textColor);
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            SafeDrawString(e.Graphics, nodeText, treeView.Font, textBrush, textBounds, sf);

            // Draw drop position indicator
            if (treeView == trvPresets && _dropTargetNode == e.Node && _dropPosition != DropPosition.None)
            {
                var indicatorColor = _isDarkMode
                    ? Color.FromArgb(0, 122, 204)
                    : Color.FromArgb(10, 88, 202);

                int lineLeft = e.Node.Level * treeView.Indent + 4;
                int lineRight = treeView.ClientSize.Width - 4;

                if (_dropPosition == DropPosition.Above)
                {
                    int y = rowBounds.Top + 1;
                    using var pen = new Pen(indicatorColor, 2f);
                    using var brush = new SolidBrush(indicatorColor);
                    e.Graphics.FillEllipse(brush, lineLeft - 3, y - 3, 6, 6);
                    e.Graphics.DrawLine(pen, lineLeft + 3, y, lineRight, y);
                }
                else if (_dropPosition == DropPosition.Below)
                {
                    int y = rowBounds.Bottom - 2;
                    using var pen = new Pen(indicatorColor, 2f);
                    using var brush = new SolidBrush(indicatorColor);
                    e.Graphics.FillEllipse(brush, lineLeft - 3, y - 3, 6, 6);
                    e.Graphics.DrawLine(pen, lineLeft + 3, y, lineRight, y);
                }
                else if (_dropPosition == DropPosition.Inside)
                {
                    var highlightColor = _isDarkMode
                        ? Color.FromArgb(50, 0, 122, 204)
                        : Color.FromArgb(50, 10, 88, 202);
                    using var highlightBrush = new SolidBrush(highlightColor);
                    e.Graphics.FillRectangle(highlightBrush, rowBounds);

                    using var borderPen = new Pen(indicatorColor, 1f);
                    e.Graphics.DrawRectangle(borderPen, rowBounds.X, rowBounds.Y, rowBounds.Width - 1, rowBounds.Height - 1);
                }
            }
        }

        private void exportHostOutputToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (lstHosts.SelectedItem is not HostHistoryEntry hostEntry)
            {
                DialogTheme.Show(this, "Please select a host to export.", "No Host Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"{hostEntry.HostAddress.Replace(":", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, hostEntry.Output);
                    UpdateStatusBar($"Output exported to {Path.GetFileName(sfd.FileName)}");
                }
                catch (Exception ex)
                {
                    DialogTheme.Show(this, $"Failed to export: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region Connection Testing

        private ToolStripMenuItem? _clearTestResultsMenuItem;

        private void InitializeConnectionTesting()
        {
            _testConnectionSeparator = new ToolStripSeparator();

            _clearTestResultsMenuItem = new ToolStripMenuItem("Clear Test Results");
            _clearTestResultsMenuItem.Click += (_, _) => ClearConnectionTestIndicators();

            _testConnectionMenuItem = new ToolStripMenuItem("Test Connection(s)");
            _testConnectionMenuItem.Click += async (_, _) => await TestSelectedConnections();

            contextMenuStrip1.Items.Add(_testConnectionSeparator);
            contextMenuStrip1.Items.Add(_clearTestResultsMenuItem);
            contextMenuStrip1.Items.Add(_testConnectionMenuItem);
        }

        private void ClearConnectionTestIndicators()
        {
            var colIndex = GetHostIpColumnIndex();
            if (colIndex < 0) return;

            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                if (row.IsNewRow) continue;
                ClearConnectionTestVisualState(row, colIndex);
            }
        }

        private async Task TestSelectedConnections()
        {
            if (_isTestingConnections) return;

            var rows = new List<DataGridViewRow>();

            // Use checked (selected) rows if any, otherwise just the right-clicked row
            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells.Count > 0 && row.Cells[0] is DataGridViewCheckBoxCell chk && chk.Value is true)
                    rows.Add(row);
            }

            if (rows.Count == 0 && _rightClickedRowIndex >= 0 && _rightClickedRowIndex < dgv_variables.Rows.Count)
            {
                var row = dgv_variables.Rows[_rightClickedRowIndex];
                if (!row.IsNewRow)
                    rows.Add(row);
            }

            if (rows.Count == 0) return;

            _isTestingConnections = true;
            _connectionTestCts = new CancellationTokenSource();
            var ct = _connectionTestCts.Token;

            var config = _configService.GetCurrent();
            var timeoutMs = config.ConnectionTimeout * 1000;
            var completed = 0;
            var progressRunId = BeginConnectionTestProgress(rows.Count);

            try
            {
                // Test in parallel with concurrency limit
                var semaphore = new SemaphoreSlim(5);
                var tasks = rows.Select(async row =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var hosts = GetHostConnections(new[] { row }).ToList();
                        if (hosts.Count == 0) return;
                        var host = hosts[0];

                        // Show "Testing..." in Host_IP cell
                        var hostIpColIndex = GetHostIpColumnIndex();
                        if (hostIpColIndex >= 0)
                        {
                            BeginInvoke(() =>
                            {
                                SetConnectionTestVisualState(row, hostIpColIndex, ConnectionTestVisualState.Testing, "Testing...");
                            });
                        }

                        var result = await _sshService.TestConnectionAsync(host, timeoutMs, ct);
                        var done = Interlocked.Increment(ref completed);

                        BeginInvoke(() =>
                        {
                            ApplyConnectionTestCellResult(row, hostIpColIndex, result);
                            UpdateConnectionTestProgress(progressRunId, done, rows.Count);
                        });
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
                InvalidateConnectionTestProgress();
                UpdateStatusBar($"Connection test complete ({completed} hosts)");
            }
            catch (OperationCanceledException)
            {
                InvalidateConnectionTestProgress();
                UpdateStatusBar("Connection test cancelled");
            }
            finally
            {
                statusProgress.Visible = false;
                _isTestingConnections = false;
                _connectionTestCts?.Dispose();
                _connectionTestCts = null;
            }
        }

        #endregion

        #region Preset Search/Filter

        private void InitializePresetSearchFilter()
        {
            _presetSearchPanel = new BufferedPanel
            {
                Height = 28,
                Dock = DockStyle.Top,
                Padding = new Padding(4, 4, 4, 2),
                BackColor = presetsPanel.BackColor
            };

            _txtPresetSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = trvPresets.Font,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Filter presets..."
            };

            _btnPresetSearchClear = new Label
            {
                Text = "\u2715", // X character
                Dock = DockStyle.Right,
                Width = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Visible = false,
                Font = new Font("Segoe UI", 8f)
            };

            _btnPresetSearchClear.Click += (_, _) =>
            {
                _txtPresetSearch.Clear();
                _txtPresetSearch.Focus();
            };

            _presetSearchDebounceTimer = new System.Windows.Forms.Timer { Interval = 150 };
            _presetSearchDebounceTimer.Tick += (_, _) =>
            {
                _presetSearchDebounceTimer.Stop();
                var filter = _txtPresetSearch.Text.Trim();
                RefreshPresetList(filterText: filter);
                if (presetsTabControl.SelectedIndex == 1)
                    RefreshFavoritesList(filterText: filter);
            };

            _txtPresetSearch.TextChanged += (_, _) =>
            {
                _btnPresetSearchClear!.Visible = _txtPresetSearch.TextLength > 0;
                _presetSearchDebounceTimer!.Stop();
                _presetSearchDebounceTimer.Start();
            };

            _txtPresetSearch.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape && _txtPresetSearch.TextLength > 0)
                {
                    _txtPresetSearch.Clear();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            _presetSearchPanel.Controls.Add(_txtPresetSearch);
            _presetSearchPanel.Controls.Add(_btnPresetSearchClear);

            // Add inside the Presets tab page. The TreeView (DockStyle.Fill) must be higher in
            // z-order than the search panel (DockStyle.Top) so the Top panel is laid out first
            // and the Fill control occupies the remaining space without overlap.
            tabPresets.Controls.Add(_presetSearchPanel);
            trvPresets.BringToFront();
        }

        private void InitializePresetsTabChrome()
        {
            presetsTabHeaderStrip.SelectedIndex = presetsTabControl.SelectedIndex;
            _lastPresetsTabIndex = presetsTabControl.SelectedIndex;
            presetsTabViewportPanel.Resize += (_, _) => UpdatePresetsTabViewportLayout();
            presetsTabControl.HandleCreated += (_, _) => UpdatePresetsTabViewportLayout();
            presetsTabControl.FontChanged += (_, _) => UpdatePresetsTabViewportLayout();
            UpdatePresetsTabViewportLayout();
        }

        private void UpdatePresetsTabViewportLayout()
        {
            if (presetsTabViewportPanel.ClientSize.Width <= 0 || presetsTabViewportPanel.ClientSize.Height <= 0)
            {
                return;
            }

            var nativeHeaderHeight = GetNativePresetsTabHeaderHeight();
            presetsTabControl.Bounds = new Rectangle(
                0,
                -nativeHeaderHeight,
                presetsTabViewportPanel.ClientSize.Width,
                presetsTabViewportPanel.ClientSize.Height + nativeHeaderHeight);
        }

        private int GetNativePresetsTabHeaderHeight()
        {
            if (presetsTabControl.TabCount > 0 && presetsTabControl.IsHandleCreated)
            {
                var tabRect = presetsTabControl.GetTabRect(0);
                if (tabRect.Height > 0)
                {
                    return Math.Max(HiddenPresetsTabHeaderFallbackHeight, tabRect.Bottom + 1);
                }
            }

            return Math.Max(HiddenPresetsTabHeaderFallbackHeight, presetsTabHeaderStrip.Height);
        }

        private string? _activePresetFilter;

        private bool PresetMatchesFilter(string presetName, string? filterText)
        {
            if (string.IsNullOrEmpty(filterText))
                return true;
            return presetName.Contains(filterText, StringComparison.OrdinalIgnoreCase);
        }

        private bool FolderHasMatchingPresets(string folderPath, string? filterText)
        {
            if (string.IsNullOrEmpty(filterText))
                return true;

            // Check direct presets in this folder
            var presetsInFolder = _presetManager.GetPresetsInFolder(folderPath);
            if (presetsInFolder.Any(p => PresetMatchesFilter(p, filterText)))
                return true;

            // Check descendant folders recursively
            var subfolders = _presetManager.GetSubfolders(folderPath);
            return subfolders.Any(sf => FolderHasMatchingPresets(sf, filterText));
        }

        #endregion

        #region Recent Files

        private void AddToRecentFiles(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);
            _configService.Update(c =>
            {
                c.RecentFiles.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
                c.RecentFiles.Insert(0, fullPath);
                while (c.RecentFiles.Count > c.MaxRecentFiles)
                    c.RecentFiles.RemoveAt(c.RecentFiles.Count - 1);
            });
            RebuildRecentFilesMenu();
        }

        private void RebuildRecentFilesMenu()
        {
            var config = _configService.GetCurrent();
            var recentFiles = config.RecentFiles;

            // Remove existing recent files menu items
            if (_recentFilesMenuItem != null)
            {
                fileToolStripMenuItem.DropDownItems.Remove(_recentFilesMenuItem);
                _recentFilesMenuItem.Dispose();
                _recentFilesMenuItem = null;
            }
            if (_recentFilesSeparator != null)
            {
                fileToolStripMenuItem.DropDownItems.Remove(_recentFilesSeparator);
                _recentFilesSeparator.Dispose();
                _recentFilesSeparator = null;
            }

            if (recentFiles.Count == 0)
                return;

            _recentFilesMenuItem = new ToolStripMenuItem("Recent Files");
            _recentFilesSeparator = new ToolStripSeparator();

            foreach (var path in recentFiles)
            {
                var fileName = Path.GetFileName(path);
                var parentDir = Path.GetDirectoryName(path);
                var parentName = string.IsNullOrEmpty(parentDir) ? "" : Path.GetFileName(parentDir);
                var displayText = string.IsNullOrEmpty(parentName) ? fileName : $"{fileName}  ({parentName})";

                var item = new ToolStripMenuItem(displayText);
                item.ToolTipText = path;

                if (!File.Exists(path))
                {
                    item.Enabled = false;
                    item.ToolTipText = $"{path} (file not found)";
                }

                var capturedPath = path;
                item.Click += (_, _) => OpenCsvFile(capturedPath);
                _recentFilesMenuItem.DropDownItems.Add(item);
            }

            _recentFilesMenuItem.DropDownItems.Add(new ToolStripSeparator());
            var clearItem = new ToolStripMenuItem("Clear Recent Files");
            clearItem.Click += (_, _) =>
            {
                _configService.Update(c => c.RecentFiles.Clear());
                RebuildRecentFilesMenu();
            };
            _recentFilesMenuItem.DropDownItems.Add(clearItem);

            // Insert after "Open CSV..." (index 0)
            var insertIndex = fileToolStripMenuItem.DropDownItems.IndexOf(openCSVToolStripMenuItem) + 1;
            fileToolStripMenuItem.DropDownItems.Insert(insertIndex, _recentFilesMenuItem);
            fileToolStripMenuItem.DropDownItems.Insert(insertIndex + 1, _recentFilesSeparator);
        }

        #endregion

        #region CSV Operations

        private enum CsvSaveAttemptResult
        {
            Saved,
            Cancelled,
            Failed
        }

        private void OpenCsvFile(string? filePath = null)
        {
            if (filePath == null)
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    Multiselect = false
                };

                if (ofd.ShowDialog(this) != DialogResult.OK)
                    return;

                filePath = ofd.FileName;
            }

            try
            {
                var dataTable = _csvManager.LoadFromFile(filePath);
                _loadedFilePath = filePath;
                using (BeginHostGridMutationScope())
                {
                    dgv_variables.Columns.Clear();
                    dgv_variables.DataSource = dataTable;
                    EnsureSelectColumn();

                    // Apply row template height to all rows (DataSource binding doesn't use RowTemplate)
                    foreach (DataGridViewRow row in dgv_variables.Rows)
                    {
                        if (!row.IsNewRow)
                            row.Height = dgv_variables.RowTemplate.Height;
                    }

                    _csvDirty = false;
                    _loadedFileFingerprint = CsvFileSyncEvaluator.Capture(_loadedFilePath);
                    _loadedFileSyncStatus = string.IsNullOrWhiteSpace(_loadedFilePath)
                        ? CsvFileSyncStatus.NotTracked
                        : CsvFileSyncStatus.Current;
                    CaptureLoadedFileSnapshotFromGrid();
                    AutoSizeColumnsToContent();
                    RequestHostGridHostCountRefresh();
                    RequestHostGridScrollbarRefresh();
                }

                UpdateStatusBar($"Loaded: {Path.GetFileName(filePath)}");

                AddToRecentFiles(filePath);
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Failed to load CSV: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveCsvAs()
        {
            _ = SaveCsvAsInternal();
        }

        private CsvSaveAttemptResult SaveCsvAsInternal()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Save as CSV"
            };

            if (!string.IsNullOrEmpty(_loadedFilePath))
                sfd.FileName = Path.GetFileName(_loadedFilePath);

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return CsvSaveAttemptResult.Cancelled;

            return TrySaveCsvToPath(sfd.FileName);
        }

        private bool SaveCurrentCsv(bool promptIfNoPath)
        {
            return SaveCurrentCsvInternal(promptIfNoPath) == CsvSaveAttemptResult.Saved;
        }

        private CsvSaveAttemptResult SaveCurrentCsvInternal(bool promptIfNoPath)
        {
            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            if (string.IsNullOrWhiteSpace(_loadedFilePath))
            {
                if (!promptIfNoPath)
                    return CsvSaveAttemptResult.Cancelled;

                return SaveCsvAsInternal();
            }

            return TrySaveCsvToPath(_loadedFilePath);
        }

        private CsvSaveAttemptResult TrySaveCsvToPath(string filename)
        {
            try
            {
                SaveCsvToFile(filename);
                _loadedFilePath = filename;
                _loadedFileFingerprint = CsvFileSyncEvaluator.Capture(_loadedFilePath);
                _loadedFileSyncStatus = string.IsNullOrWhiteSpace(_loadedFilePath)
                    ? CsvFileSyncStatus.NotTracked
                    : CsvFileSyncStatus.Current;
                CaptureLoadedFileSnapshotFromGrid();
                UpdateHostsFileIndicator();
                UpdateStatusBar($"Saved: {Path.GetFileName(filename)}");
                return CsvSaveAttemptResult.Saved;
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Failed to save file:\r\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return CsvSaveAttemptResult.Failed;
            }
        }

        private void SaveCsvToFile(string filename)
        {
            var columns = dgv_variables.Columns
                .Cast<DataGridViewColumn>()
                .Where(c => c.Name != SelectColumnName) // Exclude checkbox column from CSV
                .OrderBy(c => c.DisplayIndex)
                .Select(c => (c.Name, c.HeaderText));

            var rows = dgv_variables.Rows
                .Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow)
                .Select(r => columns.Select(c => r.Cells[dgv_variables.Columns[c.Name].Index].Value?.ToString()));

            _csvManager.SaveToFile(filename, columns, rows);
            _csvDirty = false;
        }

        private void ClearGrid()
        {
            using (BeginHostGridMutationScope())
            {
                if (dgv_variables.DataSource is DataTable dt)
                {
                    dt.Rows.Clear();
                    dt.Columns.Clear();
                    dt.Columns.Add(CsvManager.HostColumnName, typeof(string));
                }
                else
                {
                    dgv_variables.Rows.Clear();
                    dgv_variables.Columns.Clear();
                    dgv_variables.Columns.Add(CsvManager.HostColumnName, CsvManager.HostColumnName);
                }
                EnsureSelectColumn();
                _csvDirty = true;
                RequestHostGridHostCountRefresh();
                RequestHostGridScrollbarRefresh();
            }

            _loadedFilePath = null;
            _loadedFileFingerprint = null;
            _loadedFileSnapshot = null;
            _loadedFileSyncStatus = CsvFileSyncStatus.NotTracked;
            UpdateHostsFileIndicator();
            UpdateStatusBar("Grid cleared");
        }

        private bool EnsureCsvChangesSaved()
        {
            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            if (!_csvDirty) return true;

            var result = DialogTheme.Show(
                this,
                "You have unsaved CSV changes. Save before opening another file?",
                "Unsaved CSV",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.Yes && !SaveCurrentCsv(promptIfNoPath: true)) return false;
            return true;
        }

        #endregion

        #region Column Operations

        private void AddColumn()
        {
            // Find a unique column name by checking existing columns
            int nextNumber = dgv_variables.Columns.Count + 1;
            string defaultName = $"Column{nextNumber}";
            while (dgv_variables.Columns.Contains(defaultName))
            {
                nextNumber++;
                defaultName = $"Column{nextNumber}";
            }

            string columnName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the name of the new column:",
                "Add Column",
                defaultName);

            // Check raw input first - if user cleared the box or cancelled, return early
            if (string.IsNullOrWhiteSpace(columnName)) return;

            columnName = InputValidator.SanitizeColumnName(columnName);

            if (dgv_variables.Columns.Contains(columnName))
            {
                DialogTheme.Show(this, "Column name already exists!", Application.ProductName ?? "Message", MessageBoxButtons.OK, MessageBoxIcon.None);
                return;
            }

            dgv_variables.Columns.Add(columnName, columnName);
            _csvDirty = true;
            UpdateHostsFileIndicator();
        }

        private void RenameColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= dgv_variables.Columns.Count) return;

            var column = dgv_variables.Columns[columnIndex];
            string currentName = column.HeaderText;

            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter a new name for the column '{currentName}':",
                "Rename Column",
                currentName);

            // Check raw input first - if user cleared the box or cancelled, return early
            if (string.IsNullOrWhiteSpace(newName) || newName == currentName) return;

            newName = InputValidator.SanitizeColumnName(newName);

            if (dgv_variables.Columns.Cast<DataGridViewColumn>()
                .Any(c => c.HeaderText.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            {
                DialogTheme.Show(this, "This column name already exists.", "Rename Column Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            column.HeaderText = newName;
            column.Name = newName;
            _csvDirty = true;
            UpdateHostsFileIndicator();
        }

        private void DeleteColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= dgv_variables.Columns.Count) return;

            if (IsProtectedColumn(columnIndex))
            {
                DialogTheme.Show(this, "The Host_IP column cannot be deleted.", "Delete Column", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            dgv_variables.Columns.RemoveAt(columnIndex);
            _csvDirty = true;
            UpdateHostsFileIndicator();
        }

        private void DeleteRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgv_variables.Rows.Count)
            {
                DialogTheme.Show(this, "No valid row selected.", Application.ProductName ?? "Message", MessageBoxButtons.OK, MessageBoxIcon.None);
                return;
            }

            var row = dgv_variables.Rows[rowIndex];
            if (row.IsNewRow)
            {
                DialogTheme.Show(this, "Cannot delete the new row placeholder.", Application.ProductName ?? "Message", MessageBoxButtons.OK, MessageBoxIcon.None);
                return;
            }

            dgv_variables.Rows.RemoveAt(rowIndex);

            if (dgv_variables.Rows.Count > 0)
            {
                int newIndex = rowIndex < dgv_variables.Rows.Count ? rowIndex : dgv_variables.Rows.Count - 1;
                dgv_variables.Rows[newIndex].Selected = true;
                dgv_variables.CurrentCell = dgv_variables.Rows[newIndex].Cells[0];
            }

            _csvDirty = true;
            UpdateHostCount();
        }

        #endregion

        #region Clipboard Operations

        private void CopyToClipboard()
        {
            bool allSelected = dgv_variables.SelectedCells.Count == dgv_variables.RowCount * dgv_variables.ColumnCount;
            var buffer = new StringBuilder();

            if (allSelected)
            {
                // Copy with headers
                for (int j = 0; j < dgv_variables.ColumnCount; j++)
                {
                    buffer.Append(dgv_variables.Columns[j].HeaderText);
                    if (j < dgv_variables.ColumnCount - 1) buffer.Append("\t");
                }
                buffer.AppendLine();

                int rowCount = dgv_variables.AllowUserToAddRows ? dgv_variables.Rows.Count - 1 : dgv_variables.Rows.Count;
                for (int i = 0; i < rowCount; i++)
                {
                    bool isEmpty = true;
                    var rowBuffer = new StringBuilder();

                    for (int j = 0; j < dgv_variables.Columns.Count; j++)
                    {
                        string value = dgv_variables.Rows[i].Cells[j].Value?.ToString() ?? "";
                        rowBuffer.Append(value);
                        if (j < dgv_variables.Columns.Count - 1) rowBuffer.Append("\t");
                        if (!string.IsNullOrEmpty(value)) isEmpty = false;
                    }

                    if (!isEmpty) buffer.AppendLine(rowBuffer.ToString());
                }
            }
            else
            {
                var sortedCells = dgv_variables.SelectedCells
                    .Cast<DataGridViewCell>()
                    .OrderBy(c => c.RowIndex)
                    .ThenBy(c => c.ColumnIndex)
                    .ToList();

                int lastRowIndex = -1;
                foreach (var cell in sortedCells)
                {
                    if (cell.RowIndex != lastRowIndex)
                    {
                        if (lastRowIndex != -1) buffer.AppendLine();
                        lastRowIndex = cell.RowIndex;
                    }
                    else
                    {
                        buffer.Append("\t");
                    }
                    buffer.Append(cell.Value?.ToString() ?? "");
                }
            }

            Clipboard.SetText(buffer.ToString());
        }

        private void PasteFromClipboard()
        {
            if (!Clipboard.ContainsText()) return;

            var startCell = dgv_variables.CurrentCell;
            int startCol = startCell?.ColumnIndex ?? 0;
            int startRow = startCell?.RowIndex ?? 0;

            string[] rows = Clipboard.GetText().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            using (BeginHostGridMutationScope())
            {
                dgv_variables.AllowUserToAddRows = false;

                for (int i = 0; i < rows.Length; i++)
                {
                    string[] columns = rows[i].Split('\t');
                    for (int j = 0; j < columns.Length; j++)
                    {
                        int rowIndex = startRow + i;
                        while (rowIndex >= dgv_variables.Rows.Count)
                        {
                            dgv_variables.Rows.Add(new DataGridViewRow());
                        }

                        int columnIndex = startCol + j;
                        while (columnIndex >= dgv_variables.Columns.Count)
                        {
                            int nextNum = dgv_variables.Columns.Count + 1;
                            dgv_variables.Columns.Add($"Column{nextNum}", $"Column{nextNum}");
                        }

                        if (!dgv_variables.Columns[columnIndex].ReadOnly)
                        {
                            dgv_variables.Rows[rowIndex].Cells[columnIndex].Value = columns[j];
                        }
                    }
                }

                dgv_variables.AllowUserToAddRows = true;

                // Select the new empty row so user can continue pasting
                dgv_variables.ClearSelection();
                int newRowIndex = dgv_variables.Rows.Count - 1; // The new empty row
                if (newRowIndex >= 0 && dgv_variables.Rows[newRowIndex].IsNewRow)
                {
                    dgv_variables.CurrentCell = dgv_variables.Rows[newRowIndex].Cells[startCol];
                }

                _csvDirty = true;
                RequestHostGridHostCountRefresh();
                RequestHostGridScrollbarRefresh();
            }

        }

        private void DeleteSelectedCells()
        {
            using (BeginHostGridMutationScope())
            {
                var cellsToInvalidate = new List<DataGridViewCell>();
                foreach (DataGridViewCell cell in dgv_variables.SelectedCells)
                {
                    if (!cell.ReadOnly)
                    {
                        cell.Value = null;
                        cellsToInvalidate.Add(cell);
                    }
                }

                foreach (var cell in cellsToInvalidate)
                {
                    dgv_variables.InvalidateCell(cell);
                }

                _csvDirty = true;
                UpdateHostsFileIndicator();
            }
        }

        #endregion

        #region Preset Operations

        private void RefreshPresetDeleteUndoUi()
        {
            undoDeleteToolStripMenuItem.Enabled = _presetDeleteUndoService.CanUndo;
            undoDeleteToolStripMenuItem.Text = _presetDeleteUndoService.PendingActionText;
        }

        private void ClearPresetDeleteUndoHistory()
        {
            _presetDeleteUndoService.Clear();
            RefreshPresetDeleteUndoUi();
        }

        private AppConfiguration CapturePresetDeleteUndoConfigSnapshot()
        {
            var currentConfig = _configService.GetCurrent();
            var snapshot = JsonConvert.DeserializeObject<AppConfiguration>(JsonConvert.SerializeObject(currentConfig)) ?? new AppConfiguration();
            snapshot.ManualPresetOrder = new List<string>(_manualPresetOrder);
            return snapshot;
        }

        private IReadOnlyCollection<JobDefinition> CaptureAffectedJobsForPresetDelete(string presetName)
        {
            if (_jobStorage == null)
            {
                return Array.Empty<JobDefinition>();
            }

            return _jobStorage.GetJobsReferencingPreset(presetName)
                .Select(CloneJobDefinition)
                .ToArray();
        }

        private IReadOnlyCollection<JobDefinition> CaptureAffectedJobsForFolderDelete(string folderPath, bool deletePresets)
        {
            if (_jobStorage == null)
            {
                return Array.Empty<JobDefinition>();
            }

            var affectedJobs = new Dictionary<string, JobDefinition>(StringComparer.Ordinal);

            foreach (var job in _jobStorage.GetJobsReferencingFolder(folderPath))
            {
                affectedJobs[job.Id] = CloneJobDefinition(job);
            }

            if (deletePresets)
            {
                foreach (var presetName in GetPresetNamesInFolderSubtree(folderPath))
                {
                    foreach (var job in _jobStorage.GetJobsReferencingPreset(presetName))
                    {
                        affectedJobs[job.Id] = CloneJobDefinition(job);
                    }
                }
            }

            return affectedJobs.Values.ToArray();
        }

        private IEnumerable<string> GetPresetNamesInFolderSubtree(string folderPath)
        {
            return _presetManager.Presets
                .Where(kvp =>
                    string.Equals(kvp.Value.Folder, folderPath, StringComparison.Ordinal) ||
                    (!string.IsNullOrEmpty(kvp.Value.Folder) && FolderPathUtility.IsDescendantOf(kvp.Value.Folder, folderPath)))
                .Select(kvp => kvp.Key)
                .ToList();
        }

        private void RecordPresetDeleteUndo(
            string targetName,
            bool isFolder,
            AppConfiguration configSnapshot,
            IReadOnlyCollection<JobDefinition> affectedJobs)
        {
            _presetDeleteUndoService.RecordDelete(targetName, isFolder, configSnapshot, affectedJobs);
            RefreshPresetDeleteUndoUi();
        }

        private void UndoLatestPresetDelete()
        {
            var undoResult = _presetDeleteUndoService.UndoLatest(_presetManager, _jobStorage);
            if (undoResult == null)
            {
                RefreshPresetDeleteUndoUi();
                return;
            }

            var restoredConfig = _configService.GetCurrent();
            _manualPresetOrder.Clear();
            _manualPresetOrder.AddRange(restoredConfig.ManualPresetOrder);

            bool usedIncrementalMutation = false;
            TreeNode? restoredNode = null;
            if (!undoResult.IsFolder && CanMutatePresetTreeIncrementally())
            {
                ApplyIncrementalPresetTreeMutation(
                    () => { usedIncrementalMutation = TryInsertPresetNodeIncrementally(undoResult.TargetName, out restoredNode); },
                    () => restoredNode);
            }

            if (!usedIncrementalMutation)
            {
                RefreshPresetListPreservingCurrentFilter();
            }

            if (undoResult.IsFolder || ShouldRefreshFavoritesForPreset(undoResult.TargetName))
            {
                RefreshFavoritesListPreservingCurrentFilter();
            }

            if (presetsTabControl.SelectedTab != tabPresets)
            {
                presetsTabControl.SelectedTab = tabPresets;
            }

            _suppressPresetSelectionChange = true;
            try
            {
                if (undoResult.IsFolder)
                {
                    SelectFolderByName(undoResult.TargetName);
                    LoadFolderIntoSummary(undoResult.TargetName);
                }
                else
                {
                    if (!usedIncrementalMutation)
                    {
                        SelectPresetByName(undoResult.TargetName, ensureVisible: true);
                    }
                    else if (restoredNode != null)
                    {
                        EnsureTreeNodeFullyVisible(trvPresets, restoredNode);
                    }

                    var restoredPreset = _presetManager.Get(undoResult.TargetName);
                    if (restoredPreset != null)
                    {
                        LoadPresetIntoEditor(undoResult.TargetName, restoredPreset);
                    }
                }
            }
            finally
            {
                _suppressPresetSelectionChange = false;
            }

            RefreshPresetDeleteUndoUi();
        }

        private void undoDeleteToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            UndoLatestPresetDelete();
        }

        private bool ShouldHandlePresetDeleteShortcut()
        {
            if (!_presetDeleteUndoService.CanUndo)
            {
                return false;
            }

            if (txtCommand.ContainsFocus ||
                dgv_variables.IsCurrentCellInEditMode ||
                tsbUsername.TextBox.Focused ||
                tsbPassword.TextBox.Focused)
            {
                return false;
            }

            return !ContainsFocusedEditableTextControl(this);
        }

        private static bool ContainsFocusedEditableTextControl(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (!child.ContainsFocus)
                {
                    continue;
                }

                if (child is TextBoxBase)
                {
                    return true;
                }

                if (ContainsFocusedEditableTextControl(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static JobDefinition CloneJobDefinition(JobDefinition job)
        {
            return JsonConvert.DeserializeObject<JobDefinition>(JsonConvert.SerializeObject(job))!;
        }

        private bool TryDeletePresetWithUndo(string presetName)
        {
            var configSnapshot = CapturePresetDeleteUndoConfigSnapshot();
            var affectedJobs = CaptureAffectedJobsForPresetDelete(presetName);
            if (!_presetManager.Delete(presetName))
            {
                return false;
            }

            RecordPresetDeleteUndo(presetName, isFolder: false, configSnapshot, affectedJobs);
            return true;
        }

        private bool TryDeleteFolderWithUndo(string folderPath, bool deletePresets)
        {
            var configSnapshot = CapturePresetDeleteUndoConfigSnapshot();
            var affectedJobs = CaptureAffectedJobsForFolderDelete(folderPath, deletePresets);
            if (!_presetManager.DeleteFolder(folderPath, deletePresets))
            {
                return false;
            }

            RecordPresetDeleteUndo(folderPath, isFolder: true, configSnapshot, affectedJobs);
            return true;
        }

        private bool SaveCurrentPreset(PresetSaveImpactAction? selectedAction = null)
        {
            // Don't save when a folder is selected (folder summary is displayed)
            if (!string.IsNullOrEmpty(_selectedFolderName))
            {
                return false;
            }

            string presetName = txtPreset.Text.Trim();
            string commands = txtCommand.Text;
            int? timeout = int.TryParse(txtTimeoutHeader.Text, out var parsedTimeout) ? parsedTimeout : null;

            if (string.IsNullOrEmpty(presetName))
            {
                DialogTheme.Show(this, "Preset name is required.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // Prevent saving preset with folder icon prefix (safety check)
            if (presetName.StartsWith(FolderIcon, StringComparison.Ordinal))
            {
                return false;
            }

            var preActionExpandState = CapturePresetTreeExpandState();
            bool refreshPresetList = false;

            string? originalPresetName = _activePresetName;
            bool hasActivePreset = !string.IsNullOrWhiteSpace(originalPresetName) &&
                _presetManager.Get(originalPresetName) != null;
            bool nameChanged = hasActivePreset &&
                !string.Equals(presetName, originalPresetName, StringComparison.Ordinal);
            string? originalFolder = hasActivePreset && !string.IsNullOrWhiteSpace(originalPresetName)
                ? _presetManager.Get(originalPresetName!)?.Folder
                : null;
            TreeNode? renamedNode = null;

            var action = selectedAction ?? ShowPresetSavePrompt(
                presetName,
                commands,
                txtTimeoutHeader.Text ?? string.Empty,
                timeout,
                allowDiscard: false);

            if (action is PresetSaveImpactAction.Cancel or PresetSaveImpactAction.Discard)
            {
                return false;
            }

            if (nameChanged)
            {
                if (action == PresetSaveImpactAction.RenameExisting)
                {
                    if (_presetManager.Presets.ContainsKey(presetName))
                    {
                        DialogTheme.Show(this, "This preset name already exists.", "Rename Preset Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    if (!_presetManager.Rename(originalPresetName!, presetName))
                    {
                        DialogTheme.Show(this, "Unable to rename preset.", "Rename Preset Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    renamedNode = FindPresetNodeByName(trvPresets.Nodes, originalPresetName!);

                    int orderIndex = _manualPresetOrder.IndexOf(originalPresetName!);
                    if (orderIndex >= 0)
                    {
                        _manualPresetOrder[orderIndex] = presetName;
                    }

                    refreshPresetList = true;
                }
                else if (action == PresetSaveImpactAction.CreateNew)
                {
                    if (_presetManager.Presets.ContainsKey(presetName))
                    {
                        DialogTheme.Show(
                            this,
                            "A preset with this name already exists. Choose a different name to create a new preset.",
                            "Save Preset",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            // Preserve IsFavorite/Folder from the target preset, or from the currently active preset
            // when user chooses "Create new" from a renamed active preset.
            var existingPreset = _presetManager.Get(presetName);
            if (existingPreset == null && hasActivePreset && !string.IsNullOrWhiteSpace(originalPresetName))
            {
                existingPreset = _presetManager.Get(originalPresetName);
            }

            var preset = new PresetInfo
            {
                Commands = commands,
                Timeout = timeout,
                IsFavorite = existingPreset?.IsFavorite ?? false,
                Folder = existingPreset?.Folder
            };

            bool isNew = !_presetManager.Presets.ContainsKey(presetName);
            _presetManager.Save(presetName, preset);

            if (isNew)
            {
                // Add to manual order list for Manual sort mode
                if (!_manualPresetOrder.Contains(presetName))
                {
                    _manualPresetOrder.Add(presetName);
                }
                refreshPresetList = true;
            }

            if (refreshPresetList)
            {
                // Align active preset before programmatic selection to avoid false
                // unsaved prompts in AfterSelect handlers.
                _activePresetName = presetName;

                bool usedIncrementalMutation = false;
                TreeNode? mutatedNode = null;
                if (CanMutatePresetTreeIncrementally())
                {
                    if (isNew)
                    {
                        ApplyIncrementalPresetTreeMutation(
                            () => { usedIncrementalMutation = TryInsertPresetNodeIncrementally(presetName, out mutatedNode); },
                            () => mutatedNode);
                    }
                    else if (nameChanged && action == PresetSaveImpactAction.RenameExisting && renamedNode != null)
                    {
                        ApplyIncrementalPresetTreeMutation(
                            () => { usedIncrementalMutation = TryReinsertExistingPresetNodeIncrementally(renamedNode, presetName); },
                            () => renamedNode);
                    }
                }

                if (!usedIncrementalMutation)
                {
                    RefreshPresetListPreservingCurrentFilter(expandStatesOverride: preActionExpandState);

                    _suppressPresetSelectionChange = true;
                    try
                    {
                        SelectPresetByName(presetName, ensureVisible: false);
                    }
                    finally
                    {
                        _suppressPresetSelectionChange = false;
                    }
                }

                if ((isNew || (nameChanged && action == PresetSaveImpactAction.RenameExisting)) &&
                    ShouldRefreshFavoritesForPreset(presetName, originalFolder))
                {
                    RefreshFavoritesListPreservingCurrentFilter();
                }
            }

            _activePresetName = presetName;
            UpdatePresetHeaderIndicator();
            UpdateStatusBar($"Preset '{presetName}' saved");
            ClearPresetDeleteUndoHistory();
            return true;
        }

        private void AddPreset()
        {
            // Check for unsaved changes first
            if (!string.IsNullOrEmpty(_activePresetName) && IsPresetDirty())
            {
                if (!TryResolvePendingPresetChanges())
                {
                    return;
                }
            }

            string presetName = ShowInputBox(
                "Enter the name of the new preset:",
                "Add Preset",
                "New Preset");

            if (string.IsNullOrEmpty(presetName)) return;

            if (_presetManager.Presets.ContainsKey(presetName))
            {
                DialogTheme.Show(this, "Preset name already exists!", Application.ProductName ?? "Message", MessageBoxButtons.OK, MessageBoxIcon.None);
                return;
            }

            // Determine folder: if a folder is selected, create preset in it
            // If a preset inside a folder is selected, create in that folder
            string? targetFolder = null;
            if (trvPresets.SelectedNode?.Tag is PresetNodeTag tag)
            {
                if (tag.IsFolder)
                {
                    targetFolder = tag.Name;
                }
                else
                {
                    // Selected a preset - check if it's in a folder
                    var selectedPreset = _presetManager.Get(tag.Name);
                    targetFolder = selectedPreset?.Folder;
                }
            }

            // New presets inherit the global default timeout unless an explicit override is entered.
            var newPreset = new PresetInfo
            {
                Timeout = null,
                Folder = targetFolder
            };

            _presetManager.Save(presetName, newPreset);

            // Add to manual order
            if (!_manualPresetOrder.Contains(presetName))
            {
                _manualPresetOrder.Add(presetName);
            }

            bool usedIncrementalMutation = false;
            TreeNode? insertedNode = null;
            if (CanMutatePresetTreeIncrementally())
            {
                ApplyIncrementalPresetTreeMutation(
                    () => { usedIncrementalMutation = TryInsertPresetNodeIncrementally(presetName, out insertedNode); },
                    () => insertedNode);
            }

            if (!usedIncrementalMutation)
            {
                RefreshPresetListPreservingCurrentFilter();
                SelectPresetByName(presetName, ensureVisible: true);
            }
            else if (insertedNode != null)
            {
                EnsureTreeNodeFullyVisible(trvPresets, insertedNode);
            }

            if (ShouldRefreshFavoritesForPreset(presetName))
            {
                RefreshFavoritesListPreservingCurrentFilter();
            }

            EnsurePresetLoadedInEditor(presetName);
            ClearPresetDeleteUndoHistory();
        }

        private string? ResolvePresetNameForActions(bool preferContextSource)
        {
            // Context-menu actions should follow the tree where the menu was opened.
            if (preferContextSource &&
                _contextMenuSourceTreeView?.SelectedNode?.Tag is PresetNodeTag contextTag &&
                !contextTag.IsFolder)
            {
                return contextTag.Name;
            }

            // Toolbar actions follow the active tab selection first.
            if (presetsTabControl.SelectedTab == tabFavorites)
            {
                if (trvFavorites.SelectedNode?.Tag is PresetNodeTag favoritesTag && !favoritesTag.IsFolder)
                {
                    return favoritesTag.Name;
                }
            }
            else
            {
                if (trvPresets.SelectedNode?.Tag is PresetNodeTag presetsTag && !presetsTag.IsFolder)
                {
                    return presetsTag.Name;
                }
            }

            // Fallback to active preset loaded in the editor.
            if (!string.IsNullOrWhiteSpace(_activePresetName) && _presetManager.Get(_activePresetName) != null)
            {
                return _activePresetName;
            }

            return null;
        }

        private TreeView ResolvePresetTreeViewForActions(bool preferContextSource)
        {
            if (preferContextSource && _contextMenuSourceTreeView != null)
            {
                return _contextMenuSourceTreeView;
            }

            return presetsTabControl.SelectedTab == tabFavorites ? trvFavorites : trvPresets;
        }

        private TreeNode? FindPresetNodeByName(TreeNodeCollection nodes, string presetName)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is PresetNodeTag tag &&
                    !tag.IsFolder &&
                    string.Equals(tag.Name, presetName, StringComparison.Ordinal))
                {
                    return node;
                }

                var found = FindPresetNodeByName(node.Nodes, presetName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private string? GetSelectionTargetAfterDeletedPreset(string presetName, bool preferContextSource)
        {
            var sourceTree = ResolvePresetTreeViewForActions(preferContextSource);
            var displayedNodes = PresetTreeDisplayOrderBuilder.Build(sourceTree.Nodes);
            return PresetDeletionSelectionResolver.GetAdjacentPresetName(displayedNodes, presetName);
        }

        private void DuplicatePreset(bool preferContextSource = false)
        {
            string? sourceName = ResolvePresetNameForActions(preferContextSource);
            if (string.IsNullOrWhiteSpace(sourceName))
                return;

            var sourcePreset = _presetManager.Get(sourceName);
            if (sourcePreset == null)
                return;

            var preActionExpandState = CapturePresetTreeExpandState();
            string suggested = _presetManager.GetUniqueName(sourceName + "_Copy");

            string newName = ShowInputBox(
                $"Enter name for the copied preset (from '{sourceName}'):",
                "Copy Preset",
                suggested);

            if (string.IsNullOrWhiteSpace(newName)) return;

            if (_presetManager.Presets.ContainsKey(newName))
            {
                DialogTheme.Show(this, "A preset with that name already exists.", "Copy Preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string finalName = _presetManager.Duplicate(sourceName, newName);
                var duplicatedPreset = _presetManager.Get(finalName);

                // Defensive guard: duplicate should stay in the source folder.
                if (duplicatedPreset != null &&
                    !string.Equals(duplicatedPreset.Folder, sourcePreset.Folder, StringComparison.Ordinal))
                {
                    duplicatedPreset.Folder = sourcePreset.Folder;
                    _presetManager.Save(finalName, duplicatedPreset);
                }

                // Add to manual order after the source
                int sourceIndex = _manualPresetOrder.IndexOf(sourceName);
                if (sourceIndex >= 0)
                {
                    _manualPresetOrder.Insert(sourceIndex + 1, finalName);
                }
                else
                {
                    _manualPresetOrder.Add(finalName);
                }

                bool usedIncrementalMutation = false;
                TreeNode? duplicatedNode = null;
                if (CanMutatePresetTreeIncrementally())
                {
                    ApplyIncrementalPresetTreeMutation(
                        () => { usedIncrementalMutation = TryInsertPresetNodeIncrementally(finalName, out duplicatedNode); },
                        () => duplicatedNode);
                }

                if (!usedIncrementalMutation)
                {
                    RefreshPresetListPreservingCurrentFilter(expandStatesOverride: preActionExpandState);
                    SelectPresetByName(finalName, ensureVisible: false);
                }

                if (ShouldRefreshFavoritesForPreset(finalName, sourcePreset.Folder))
                {
                    RefreshFavoritesListPreservingCurrentFilter();
                }

                EnsurePresetLoadedInEditor(finalName);
                ClearPresetDeleteUndoHistory();
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenamePreset(bool preferContextSource = false)
        {
            string? selectedPreset = ResolvePresetNameForActions(preferContextSource);
            if (string.IsNullOrWhiteSpace(selectedPreset))
                return;

            string newName = ShowInputBox(
                $"Enter a new name for the preset '{selectedPreset}':",
                "Rename Preset",
                selectedPreset);

            if (string.IsNullOrEmpty(newName) || newName == selectedPreset) return;

            var renamedNode = FindPresetNodeByName(trvPresets.Nodes, selectedPreset);
            var preActionExpandState = CapturePresetTreeExpandState();
            var originalFolder = _presetManager.Get(selectedPreset)?.Folder;

            if (!_presetManager.Rename(selectedPreset, newName))
            {
                DialogTheme.Show(this, "This preset name already exists.", "Rename Preset Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Update manual order list
            int orderIndex = _manualPresetOrder.IndexOf(selectedPreset);
            if (orderIndex >= 0)
            {
                _manualPresetOrder[orderIndex] = newName;
            }

            bool usedIncrementalMutation = false;
            if (CanMutatePresetTreeIncrementally() && renamedNode != null)
            {
                ApplyIncrementalPresetTreeMutation(
                    () => { usedIncrementalMutation = TryReinsertExistingPresetNodeIncrementally(renamedNode, newName); },
                    () => renamedNode);
            }

            if (!usedIncrementalMutation)
            {
                RefreshPresetListPreservingCurrentFilter(expandStatesOverride: preActionExpandState);
                SelectPresetByName(newName, ensureVisible: false);
            }

            if (ShouldRefreshFavoritesForPreset(newName, originalFolder))
            {
                RefreshFavoritesListPreservingCurrentFilter();
            }

            txtPreset.Text = newName;
            _activePresetName = newName;
            UpdatePresetHeaderIndicator();
            ClearPresetDeleteUndoHistory();
        }

        private void DeletePreset(bool preferContextSource = false)
        {
            string? selectedPreset = ResolvePresetNameForActions(preferContextSource);
            if (string.IsNullOrWhiteSpace(selectedPreset))
                return;

            var deleteNode = FindPresetNodeByName(trvPresets.Nodes, selectedPreset);
            var preActionExpandState = CapturePresetTreeExpandState();
            var selectionTargetPresetName = GetSelectionTargetAfterDeletedPreset(selectedPreset, preferContextSource);
            var selectionTargetNode = string.IsNullOrEmpty(selectionTargetPresetName)
                ? null
                : FindPresetNodeByName(trvPresets.Nodes, selectionTargetPresetName);
            var canDeleteInPlace =
                string.IsNullOrWhiteSpace(_activePresetFilter) &&
                deleteNode != null &&
                deleteNode.TreeView == trvPresets;

            // Check if this is the currently active preset being deleted
            bool isDeletingActivePreset = string.Equals(selectedPreset, _activePresetName, StringComparison.Ordinal);

            if (TryDeletePresetWithUndo(selectedPreset))
            {
                _manualPresetOrder.Remove(selectedPreset);

                // Clear active preset if we deleted it (prevents "save changes?" prompt)
                if (isDeletingActivePreset)
                {
                    _activePresetName = null;
                    txtPreset.Clear();
                    txtCommand.Clear();
                    txtTimeoutHeader.Clear();
                }

                if (canDeleteInPlace)
                {
                    PresetTreeDeleteMutation.RemoveNodeAndSelectReplacement(trvPresets, deleteNode!, selectionTargetNode);
                }
                else
                {
                    RefreshPresetListPreservingCurrentFilter(expandStatesOverride: preActionExpandState);
                }

                RefreshFavoritesListPreservingCurrentFilter();

                if (!string.IsNullOrEmpty(selectionTargetPresetName))
                {
                    if (trvPresets.SelectedNode?.Tag is PresetNodeTag selectedTag &&
                        !selectedTag.IsFolder &&
                        string.Equals(selectedTag.Name, selectionTargetPresetName, StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                // Select another preset if any exist
                if (_presetManager.Presets.Count > 0)
                {
                    var firstPreset = PresetTreeDisplayOrderBuilder.Build(trvPresets.Nodes)
                        .FirstOrDefault(tag => !tag.IsFolder)
                        ?.Name;

                    if (!string.IsNullOrEmpty(firstPreset) &&
                        !string.Equals(_activePresetName, firstPreset, StringComparison.Ordinal))
                    {
                        SelectPresetByName(firstPreset, ensureVisible: false);
                        if (!string.Equals(_activePresetName, firstPreset, StringComparison.Ordinal))
                        {
                            var preset = _presetManager.Get(firstPreset);
                            if (preset != null)
                            {
                                LoadPresetIntoEditor(firstPreset, preset);
                            }
                        }
                    }
                }
            }
        }

        private void ExportPreset(bool preferContextSource = false)
        {
            string? presetName = ResolvePresetNameForActions(preferContextSource);
            if (string.IsNullOrWhiteSpace(presetName))
            {
                DialogTheme.Show(this, "No preset selected to export.", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string exportString = _presetManager.Export(presetName);
                Clipboard.SetText(exportString);
                DialogTheme.Show(this, "Preset exported to clipboard.", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Failed to export preset: {ex.Message}", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImportPreset()
        {
            string input = ShowInputBox(
                "Paste the encoded preset string:\r\nFormat: <name>_<encoded>",
                "Import Preset",
                "");

            if (string.IsNullOrWhiteSpace(input)) return;

            try
            {
                int? defaultTimeout = int.TryParse(txtTimeoutHeader.Text, out var t) ? t : null;

                string finalName = _presetManager.Import(input, defaultTimeout);

                bool usedIncrementalMutation = false;
                TreeNode? importedNode = null;
                if (CanMutatePresetTreeIncrementally())
                {
                    ApplyIncrementalPresetTreeMutation(
                        () => { usedIncrementalMutation = TryInsertPresetNodeIncrementally(finalName, out importedNode); },
                        () => importedNode);
                }

                if (!usedIncrementalMutation)
                {
                    RefreshPresetListPreservingCurrentFilter();
                    SelectPresetByName(finalName, ensureVisible: false);
                }

                if (ShouldRefreshFavoritesForPreset(finalName))
                {
                    RefreshFavoritesListPreservingCurrentFilter();
                }

                EnsurePresetLoadedInEditor(finalName);
                DialogTheme.Show(this, $"Preset '{finalName}' imported.", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearPresetDeleteUndoHistory();
            }
            catch (FormatException)
            {
                DialogTheme.Show(this, "Invalid format or Base64 encoding.", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Failed to import preset: {ex.Message}", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportAllPresets()
        {
            if (_presetManager.Presets.Count == 0)
            {
                DialogTheme.Show(this, "No presets to export.", "Export All Presets", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Title = "Export All Presets",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = "json",
                FileName = "presets_export.json"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                _presetManager.ExportAllToFile(dialog.FileName);
                DialogTheme.Show(this, $"Exported {_presetManager.Presets.Count} presets to:\n{dialog.FileName}",
                    "Export All Presets", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Failed to export presets: {ex.Message}", "Export All Presets", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImportAllPresets()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Import All Presets",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = "json"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            // Ask user where to import the presets
            string? targetFolder = PromptForImportDestination();
            if (targetFolder == "\x1B") // ESC marker for cancelled
                return;

            try
            {
                int count = _presetManager.ImportAllFromFile(dialog.FileName, targetFolder);
                RefreshPresetList();

                string locationMsg = targetFolder == null
                    ? "Presets were imported with their original folder structure."
                    : string.IsNullOrEmpty(targetFolder)
                        ? "Presets were imported to root level."
                        : $"Presets were imported to folder \"{targetFolder}\".";

                DialogTheme.Show(this, $"Imported {count} presets.\n\n{locationMsg}\n\nNote: If any preset names already existed, '_imported' was appended to avoid overwriting.",
                    "Import All Presets", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (count > 0)
                {
                    ClearPresetDeleteUndoHistory();
                }
            }
            catch (FormatException ex)
            {
                DialogTheme.Show(this, $"Invalid preset file format: {ex.Message}", "Import All Presets", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Failed to import presets: {ex.Message}", "Import All Presets", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Prompts the user to select a destination folder for importing presets.
        /// </summary>
        /// <returns>
        /// null = keep original structure,
        /// empty string = import to root,
        /// folder name = import to that folder,
        /// "\x1B" = cancelled
        /// </returns>
        private string? PromptForImportDestination()
        {
            // Ask if user wants to import to a specific folder
            var result = DialogTheme.Show(
                this,
                "Would you like to import these presets into a specific folder?\n\n" +
                "• Yes - Choose a destination folder\n" +
                "• No - Keep the original folder structure from the export",
                "Import Destination",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
                return "\x1B";

            if (result == DialogResult.No)
                return null; // Keep original structure

            // Get list of existing folders
            var existingFolders = _presetManager.GetFolders().OrderBy(f => f).ToList();

            // Build the prompt message with existing folders
            string folderList = existingFolders.Count > 0
                ? "\n\nExisting folders:\n• " + string.Join("\n• ", existingFolders)
                : "";

            string prompt = $"Enter the folder name to import presets into:{folderList}\n\n" +
                           "(Leave empty to import to root level, or type a new name to create a folder)";

            string folderName = Microsoft.VisualBasic.Interaction.InputBox(
                prompt,
                "Import Destination Folder",
                existingFolders.Count > 0 ? existingFolders[0] : "");

            // Empty result from InputBox means Cancel was pressed OR user left it empty
            // We need to distinguish - if user presses Cancel, we abort
            // InputBox returns empty string for both Cancel and empty input
            // So we'll treat empty as "root level" and provide a way to cancel earlier

            return folderName; // Empty string = root, non-empty = folder name
        }

        private Dictionary<string, bool> CapturePresetTreeExpandState()
        {
            var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            void Capture(TreeNodeCollection nodes)
            {
                foreach (TreeNode node in nodes)
                {
                    if (node.Tag is PresetNodeTag tag && tag.IsFolder)
                    {
                        states[tag.Name] = node.IsExpanded;
                        Capture(node.Nodes);
                    }
                }
            }

            Capture(trvPresets.Nodes);
            return states;
        }

        private void RefreshPresetList(
            bool restoreExpandState = true,
            IReadOnlyDictionary<string, bool>? expandStatesOverride = null,
            string? filterText = null,
            AppConfiguration? configOverride = null)
        {
            _activePresetFilter = string.IsNullOrWhiteSpace(filterText) ? null : filterText.Trim();
            var isFiltering = _activePresetFilter != null;
            string? currentSelection = null;
            if (trvPresets.SelectedNode?.Tag is PresetNodeTag selectedTag && !selectedTag.IsFolder)
            {
                currentSelection = selectedTag.Name;
            }

            IReadOnlyDictionary<string, bool>? runtimeExpandStates = null;
            if (restoreExpandState)
            {
                if (expandStatesOverride != null && expandStatesOverride.Count > 0)
                {
                    runtimeExpandStates = expandStatesOverride;
                }
                else if (trvPresets.Nodes.Count > 0)
                {
                    runtimeExpandStates = CapturePresetTreeExpandState();
                }
            }

            _suppressPresetSelectionChange = true;
            _suppressExpandCollapseEvents = true;
            trvPresets.BeginUpdate();
            trvPresets.Nodes.Clear();

            var config = configOverride ?? _configService.Load();

            // Dictionary to track nodes by full path for nested folder building
            var folderNodes = new Dictionary<string, TreeNode>();

            // Get all folders sorted
            var allFolders = GetSortedFolders(config).ToList();

            // Build nested folder hierarchy
            foreach (var folderPath in allFolders)
            {
                // Skip folders with no matching presets when filtering
                if (isFiltering && !FolderHasMatchingPresets(folderPath, _activePresetFilter))
                    continue;

                var folderName = FolderPathUtility.GetFolderName(folderPath);
                var parentPath = FolderPathUtility.GetParentPath(folderPath);
                var folderInfo = _presetManager.Folders.GetValueOrDefault(folderPath);

                string folderDisplay = folderInfo?.IsFavorite == true
                    ? $"{StarIcon} {FolderIcon} {folderName}"
                    : $"{FolderIcon} {folderName}";

                var folderNode = new TreeNode(folderDisplay)
                {
                    Tag = new PresetNodeTag { IsFolder = true, Name = folderPath }
                };

                // Add to parent node or tree root
                if (parentPath != null && folderNodes.TryGetValue(parentPath, out var parentNode))
                {
                    parentNode.Nodes.Add(folderNode);
                }
                else
                {
                    // Root-level folder
                    trvPresets.Nodes.Add(folderNode);
                }

                folderNodes[folderPath] = folderNode;

                // Add presets in this folder (exact match only, not descendants)
                var presetsInFolder = GetSortedPresetsInFolder(folderPath, config);
                foreach (var presetName in presetsInFolder)
                {
                    if (isFiltering && !PresetMatchesFilter(presetName, _activePresetFilter))
                        continue;

                    var preset = _presetManager.Get(presetName);
                    string displayName = preset?.IsFavorite == true ? $"{StarIcon} {presetName}" : presetName;
                    var presetNode = new TreeNode(displayName)
                    {
                        Tag = new PresetNodeTag { IsFolder = false, Name = presetName }
                    };
                    folderNode.Nodes.Add(presetNode);
                }

            }

            // Add root-level presets (no folder)
            var rootPresets = GetSortedPresetsInFolder(null, config);
            foreach (var presetName in rootPresets)
            {
                if (isFiltering && !PresetMatchesFilter(presetName, _activePresetFilter))
                    continue;

                var preset = _presetManager.Get(presetName);
                string displayName = preset?.IsFavorite == true ? $"{StarIcon} {presetName}" : presetName;
                var presetNode = new TreeNode(displayName)
                {
                    Tag = new PresetNodeTag { IsFolder = false, Name = presetName }
                };
                trvPresets.Nodes.Add(presetNode);
            }

            // Final pass for expand/collapse state after the full hierarchy exists.
            // This avoids branch state drift when nodes are added after earlier Expand calls.
            // When filtering, expand all nodes to show matches.
            if (isFiltering)
            {
                trvPresets.ExpandAll();
            }
            else if (restoreExpandState)
            {
                foreach (var folderPath in allFolders)
                {
                    if (!folderNodes.TryGetValue(folderPath, out var folderNode))
                    {
                        continue;
                    }

                    bool shouldExpand = false;
                    if (runtimeExpandStates != null &&
                        runtimeExpandStates.TryGetValue(folderPath, out var expandedFromRuntime))
                    {
                        shouldExpand = expandedFromRuntime;
                    }
                    else if (_presetManager.Folders.TryGetValue(folderPath, out var folderInfo))
                    {
                        shouldExpand = folderInfo.IsExpanded;
                    }

                    if (shouldExpand)
                    {
                        folderNode.Expand();
                    }
                    else
                    {
                        folderNode.Collapse();
                    }
                }
            }

            trvPresets.EndUpdate();

            // Disable drag-drop during filtering to prevent reordering a filtered subset
            trvPresets.AllowDrop = !isFiltering;

            // Restore selection
            if (!string.IsNullOrEmpty(currentSelection))
            {
                SelectPresetByName(currentSelection, ensureVisible: false);
            }

            _suppressPresetSelectionChange = false;
            _suppressExpandCollapseEvents = false;
        }

        private IEnumerable<string> GetSortedFolders(AppConfiguration config)
        {
            var folders = _presetManager.GetFolders().ToList();

            // Sort folders so parents always come before their children
            // This is critical for building the nested tree hierarchy correctly
            IEnumerable<string> sortedFolders = _currentSortMode switch
            {
                PresetSortMode.Ascending => folders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase),
                PresetSortMode.Descending => folders
                    .OrderBy(f => FolderPathUtility.GetDepth(f))  // Depth first to ensure parents before children
                    .ThenByDescending(f => f, StringComparer.OrdinalIgnoreCase),
                PresetSortMode.Manual => GetManualOrderedFolders(folders, config),
                _ => folders
            };

            // Ensure parents come before children by sorting by depth, then by the sort order
            // Ascending alphabetical naturally handles this because "A" < "A/B"
            // For descending and manual, we need to be more careful
            if (_currentSortMode == PresetSortMode.Manual)
            {
                // For manual ordering, sort by depth first to ensure parent folders exist
                sortedFolders = sortedFolders.OrderBy(f => FolderPathUtility.GetDepth(f)).ToList();
            }

            return sortedFolders;
        }

        private static IEnumerable<string> GetManualOrdered(IEnumerable<string> all, IReadOnlyList<string> manualOrder)
        {
            var allList = all as IList<string> ?? all.ToList();
            var result = new List<string>();
            foreach (var name in manualOrder)
            {
                if (allList.Contains(name))
                {
                    result.Add(name);
                }
            }
            // Add any items not in manual order
            foreach (var name in allList)
            {
                if (!result.Contains(name))
                {
                    result.Add(name);
                }
            }
            return result;
        }

        private IEnumerable<string> GetManualOrderedFolders(List<string> folders, AppConfiguration config)
        {
            return GetManualOrdered(folders, config.ManualFolderOrder);
        }

        private IEnumerable<string> GetSortedPresetsInFolder(string? folder, AppConfiguration config)
        {
            var presets = _presetManager.GetPresetsInFolder(folder).ToList();

            if (_currentSortMode == PresetSortMode.Manual)
            {
                return GetManualOrderedPresets(presets, folder, config);
            }

            // Separate favorites and non-favorites
            var favorites = presets.Where(p => _presetManager.Get(p)?.IsFavorite == true);
            var nonFavorites = presets.Where(p => _presetManager.Get(p)?.IsFavorite != true);

            return _currentSortMode switch
            {
                PresetSortMode.Ascending =>
                    favorites.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .Concat(nonFavorites.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)),
                PresetSortMode.Descending =>
                    favorites.OrderByDescending(n => n, StringComparer.OrdinalIgnoreCase)
                        .Concat(nonFavorites.OrderByDescending(n => n, StringComparer.OrdinalIgnoreCase)),
                _ => favorites.Concat(nonFavorites)
            };
        }

        private IEnumerable<string> GetManualOrderedPresets(List<string> presets, string? folder, AppConfiguration config)
        {
            string folderKey = folder ?? "";
            IReadOnlyList<string> manualOrder = config.ManualPresetOrderByFolder.TryGetValue(folderKey, out var order)
                ? order
                : Array.Empty<string>();
            return GetManualOrdered(presets, manualOrder);
        }

        private void DisplayFolderSummary(string folderPath)
        {
            var config = _configService.Load();
            var presetNames = GetSortedPresetsInFolder(folderPath, config).ToList();
            var subfolders = _presetManager.GetSubfolders(folderPath).ToList();
            var folderName = FolderPathUtility.GetFolderName(folderPath);
            var effectiveBaseEnvironment = ResolveEffectiveBaseEnvironment(folderPath);
            _presetManager.Folders.TryGetValue(folderPath, out var folderInfo);

            var sb = new StringBuilder();
            sb.AppendLine(FolderSummarySeparator);
            sb.AppendLine($"  FOLDER: {folderName}");
            if (folderPath != folderName)
            {
                sb.AppendLine($"  PATH: {folderPath}");
            }
            sb.AppendLine(FolderSummarySeparator);
            sb.AppendLine();
            sb.AppendLine(FolderBaseEnvironmentSummaryFormatter.FormatSummaryLine(folderInfo?.BaseEnvironment, effectiveBaseEnvironment));
            sb.AppendLine($"  Presets: {presetNames.Count}");
            if (subfolders.Count > 0)
            {
                sb.AppendLine($"  Subfolders: {subfolders.Count}");
            }
            sb.AppendLine();

            if (subfolders.Count > 0)
            {
                sb.AppendLine("  Subfolders:");
                sb.AppendLine($"  {FolderSummarySubSeparator}");
                foreach (var subfolder in subfolders.OrderBy(f => f))
                {
                    var subfolderName = FolderPathUtility.GetFolderName(subfolder);
                    sb.AppendLine($"    {FolderIcon} {subfolderName}");
                }
                sb.AppendLine();
            }

            if (presetNames.Count > 0)
            {
                sb.AppendLine("  Presets:");
                sb.AppendLine($"  {FolderSummarySubSeparator}");
                foreach (var name in presetNames)
                {
                    var preset = _presetManager.Get(name);
                    var favorite = preset?.IsFavorite == true ? $"{StarIcon} " : "  ";
                    var type = preset?.IsScript == true ? "[Script]" : "";
                    sb.AppendLine($"  {favorite}{name} {type}");
                }
            }
            else if (subfolders.Count == 0)
            {
                sb.AppendLine("  (Empty folder)");
            }

            txtCommand.Text = sb.ToString();
            txtCommand.ReadOnly = true;
        }

        private void LoadFolderIntoSummary(string folderPath)
        {
            _activePresetName = null;
            _selectedFolderName = folderPath;
            TryApplyFolderEnvironment(folderPath);
            txtTimeoutHeader.Clear();
            RefreshSelectedFolderSummary();
        }

        private void EnsureFolderSummaryCurrent(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            if (string.IsNullOrEmpty(_activePresetName) &&
                string.Equals(_selectedFolderName, folderPath, StringComparison.Ordinal))
            {
                return;
            }

            HandleFolderSelection(folderPath);
        }

        private void HandleFolderSelection(string folderPath, Action? onCancel = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_activePresetName) && IsPresetDirty())
            {
                if (!TryResolvePendingPresetChanges(onCancel))
                {
                    return;
                }
            }

            LoadFolderIntoSummary(folderPath);
        }

        private void RefreshSelectedFolderSummary()
        {
            if (string.IsNullOrWhiteSpace(_selectedFolderName))
            {
                return;
            }

            txtPreset.Text = $"{FolderIcon} {_selectedFolderName}";
            DisplayFolderSummary(_selectedFolderName);
            UpdateRunButtonText();
            UpdatePresetHeaderIndicator();
        }

        private void UpdateRunButtonText()
        {
            int checkedCount = GetCheckedHostCount();

            if (!string.IsNullOrEmpty(_selectedFolderName))
            {
                int count = _presetManager.GetPresetsInFolder(_selectedFolderName).Count();
                btnExecuteAll.Text = $"Run {FolderIcon} {_selectedFolderName} ({count})";
                btnExecuteSelected.Text = checkedCount > 0
                    ? $"Run Checked ({checkedCount}) {FolderIcon}"
                    : $"Run Selected {FolderIcon}";
            }
            else
            {
                btnExecuteAll.Text = "Run All";
                btnExecuteSelected.Text = checkedCount > 0
                    ? $"Run Checked ({checkedCount})"
                    : "Run Selected";
            }

            // Reposition buttons based on text width
            try
            {
                using var g = btnExecuteSelected.CreateGraphics();
                var selectedSize = g.MeasureString(btnExecuteSelected.Text, btnExecuteSelected.Font);
                btnExecuteSelected.Width = (int)selectedSize.Width + 40;

                var allSize = g.MeasureString(btnExecuteAll.Text, btnExecuteAll.Font);
                btnExecuteAll.Width = (int)allSize.Width + 40;
                btnExecuteAll.Left = btnExecuteSelected.Right + 8;

                // Position Stop button with same spacing and ensure matching height
                btnStopAll.Left = btnExecuteAll.Right + 8;
                btnStopAll.Height = btnExecuteAll.Height;
            }
            catch (ArgumentException)
            {
                // Font may have an invalidated GDI+ handle during font transitions;
                // skip measurement — layout will correct on the next stable call.
            }
        }

        private string ShowInputBox(string prompt, string title, string defaultResponse)
        {
            var promptOverride = _inputBoxPromptOverrideForTests;
            if (promptOverride != null)
            {
                return promptOverride(prompt, title, defaultResponse);
            }

            return Microsoft.VisualBasic.Interaction.InputBox(prompt, title, defaultResponse);
        }

        private bool CanMutatePresetTreeIncrementally()
        {
            return string.IsNullOrWhiteSpace(_activePresetFilter) && !trvPresets.IsDisposed;
        }

        private string BuildPresetTreeNodeText(string presetName)
        {
            var preset = _presetManager.Get(presetName);
            return preset?.IsFavorite == true ? $"{StarIcon} {presetName}" : presetName;
        }

        private string BuildFolderTreeNodeText(string folderPath)
        {
            var folderName = FolderPathUtility.GetFolderName(folderPath);
            var folderInfo = _presetManager.Folders.GetValueOrDefault(folderPath);
            return folderInfo?.IsFavorite == true
                ? $"{StarIcon} {FolderIcon} {folderName}"
                : $"{FolderIcon} {folderName}";
        }

        private TreeNode CreatePresetTreeNode(string presetName)
        {
            return new TreeNode(BuildPresetTreeNodeText(presetName))
            {
                Tag = new PresetNodeTag { IsFolder = false, Name = presetName }
            };
        }

        private TreeNode CreateFolderTreeNode(string folderPath)
        {
            return new TreeNode(BuildFolderTreeNodeText(folderPath))
            {
                Tag = new PresetNodeTag { IsFolder = true, Name = folderPath }
            };
        }

        private void UpdatePresetTreeNodeDisplay(TreeNode node)
        {
            if (node.Tag is not PresetNodeTag tag)
            {
                return;
            }

            node.Text = tag.IsFolder
                ? BuildFolderTreeNodeText(tag.Name)
                : BuildPresetTreeNodeText(tag.Name);
        }

        private void ApplyIncrementalPresetTreeMutation(Action mutate, Func<TreeNode?>? preferredSelectionNodeResolver = null)
        {
            var topNodeBefore = PresetTreeViewportRestorer.Capture(trvPresets.TopNode);
            var selectionBefore = CaptureSelectedPresetNodeTag(trvPresets) ?? ClonePresetNodeTag(_lastPresetsTreeSelection);
            var previousSuppressSelection = _suppressPresetSelectionChange;
            var previousSuppressExpand = _suppressExpandCollapseEvents;

            _suppressPresetSelectionChange = true;
            _suppressExpandCollapseEvents = true;
            PresetNodeTag? selectionTag = null;
            trvPresets.BeginUpdate();
            try
            {
                mutate();

                var preferredSelectionNode = preferredSelectionNodeResolver?.Invoke();
                TreeNode? selectionNode = null;
                if (preferredSelectionNode != null && preferredSelectionNode.TreeView == trvPresets)
                {
                    selectionNode = preferredSelectionNode;
                }
                else if (selectionBefore != null)
                {
                    selectionNode = FindNodeByTag(trvPresets.Nodes, selectionBefore.Name, selectionBefore.IsFolder);
                }

                selectionTag = selectionNode?.Tag as PresetNodeTag ?? selectionBefore;
                if (selectionNode != null)
                {
                    trvPresets.SelectedNode = selectionNode;
                }

                RememberPresetTreeSelection(trvPresets, selectionTag);
                PresetTreeViewportRestorer.TryRestoreTopNode(
                    trvPresets,
                    trvPresets.Nodes,
                    topNodeBefore,
                    selectionTag);
            }
            finally
            {
                trvPresets.EndUpdate();
                _suppressExpandCollapseEvents = previousSuppressExpand;
                _suppressPresetSelectionChange = previousSuppressSelection;
            }

            PresetTreeViewportRestorer.TryRestoreTopNode(
                trvPresets,
                trvPresets.Nodes,
                topNodeBefore,
                selectionTag);
        }

        private bool TryInsertPresetNodeIncrementally(string presetName, out TreeNode? insertedNode)
        {
            insertedNode = null;
            var preset = _presetManager.Get(presetName);
            if (preset == null)
            {
                return false;
            }

            var parentNode = string.IsNullOrEmpty(preset.Folder)
                ? null
                : FindNodeByTag(trvPresets.Nodes, preset.Folder, isFolder: true);
            if (!string.IsNullOrEmpty(preset.Folder) && parentNode == null)
            {
                return false;
            }

            var node = CreatePresetTreeNode(presetName);
            InsertPresetNode(node, presetName, preset.Folder, _configService.GetCurrent());
            insertedNode = node;
            return true;
        }

        private bool TryInsertFolderNodeIncrementally(string folderPath, out TreeNode? insertedNode)
        {
            insertedNode = null;
            var parentPath = FolderPathUtility.GetParentPath(folderPath);
            var parentNode = string.IsNullOrEmpty(parentPath)
                ? null
                : FindNodeByTag(trvPresets.Nodes, parentPath, isFolder: true);
            if (!string.IsNullOrEmpty(parentPath) && parentNode == null)
            {
                return false;
            }

            var node = CreateFolderTreeNode(folderPath);
            InsertFolderNode(node, folderPath, _configService.GetCurrent());
            insertedNode = node;
            return true;
        }

        private bool TryReinsertExistingPresetNodeIncrementally(TreeNode node, string presetName)
        {
            var preset = _presetManager.Get(presetName);
            if (preset == null)
            {
                return false;
            }

            var parentNode = string.IsNullOrEmpty(preset.Folder)
                ? null
                : FindNodeByTag(trvPresets.Nodes, preset.Folder, isFolder: true);
            if (!string.IsNullOrEmpty(preset.Folder) && parentNode == null)
            {
                return false;
            }

            if (node.Tag is PresetNodeTag tag)
            {
                tag.Name = presetName;
                tag.IsFolder = false;
            }

            DetachTreeNode(node);
            UpdatePresetTreeNodeDisplay(node);
            InsertPresetNode(node, presetName, preset.Folder, _configService.GetCurrent());
            return true;
        }

        private void InsertPresetNode(TreeNode node, string presetName, string? folder, AppConfiguration config)
        {
            var parentNode = string.IsNullOrEmpty(folder)
                ? null
                : FindNodeByTag(trvPresets.Nodes, folder, isFolder: true);
            var targetNodes = parentNode?.Nodes ?? trvPresets.Nodes;
            var orderedPresets = GetSortedPresetsInFolder(folder, config).ToList();
            var desiredPresetIndex = orderedPresets.IndexOf(presetName);
            if (desiredPresetIndex < 0)
            {
                desiredPresetIndex = orderedPresets.Count;
            }

            var insertIndex = ResolvePresetInsertIndex(targetNodes, desiredPresetIndex, insideFolder: parentNode != null);
            targetNodes.Insert(insertIndex, node);
        }

        private void InsertFolderNode(TreeNode node, string folderPath, AppConfiguration config)
        {
            var parentPath = FolderPathUtility.GetParentPath(folderPath);
            var parentNode = string.IsNullOrEmpty(parentPath)
                ? null
                : FindNodeByTag(trvPresets.Nodes, parentPath, isFolder: true);
            var targetNodes = parentNode?.Nodes ?? trvPresets.Nodes;
            var siblingFolders = GetSortedFolders(config)
                .Where(path => string.Equals(FolderPathUtility.GetParentPath(path), parentPath, StringComparison.Ordinal))
                .ToList();
            var desiredFolderIndex = siblingFolders.IndexOf(folderPath);
            if (desiredFolderIndex < 0)
            {
                desiredFolderIndex = siblingFolders.Count;
            }

            var insertIndex = ResolveFolderInsertIndex(targetNodes, desiredFolderIndex, insideFolder: parentNode != null);
            targetNodes.Insert(insertIndex, node);
        }

        private static int ResolvePresetInsertIndex(TreeNodeCollection nodes, int desiredPresetIndex, bool insideFolder)
        {
            var seenPresets = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Tag is not PresetNodeTag tag)
                {
                    continue;
                }

                if (!tag.IsFolder)
                {
                    if (seenPresets == desiredPresetIndex)
                    {
                        return i;
                    }

                    seenPresets++;
                    continue;
                }

                if (insideFolder)
                {
                    return i;
                }
            }

            return nodes.Count;
        }

        private static int ResolveFolderInsertIndex(TreeNodeCollection nodes, int desiredFolderIndex, bool insideFolder)
        {
            var seenFolders = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Tag is not PresetNodeTag tag)
                {
                    continue;
                }

                if (tag.IsFolder)
                {
                    if (seenFolders == desiredFolderIndex)
                    {
                        return i;
                    }

                    seenFolders++;
                    continue;
                }

                if (!insideFolder)
                {
                    return i;
                }
            }

            return nodes.Count;
        }

        private static void DetachTreeNode(TreeNode node)
        {
            if (node.Parent != null)
            {
                node.Parent.Nodes.Remove(node);
            }
            else
            {
                node.TreeView?.Nodes.Remove(node);
            }
        }

        private bool IsFavoriteFolder(string? folderPath)
        {
            return !string.IsNullOrEmpty(folderPath) &&
                _presetManager.Folders.TryGetValue(folderPath, out var folderInfo) &&
                folderInfo.IsFavorite;
        }

        private bool ShouldRefreshFavoritesForPreset(string presetName, string? previousFolder = null)
        {
            var preset = _presetManager.Get(presetName);
            if (preset == null)
            {
                return IsFavoriteFolder(previousFolder);
            }

            return preset.IsFavorite ||
                IsFavoriteFolder(previousFolder) ||
                IsFavoriteFolder(preset.Folder);
        }

        private void EnsurePresetLoadedInEditor(string presetName)
        {
            if (string.Equals(_activePresetName, presetName, StringComparison.Ordinal) &&
                string.IsNullOrEmpty(_selectedFolderName))
            {
                return;
            }

            var preset = _presetManager.Get(presetName);
            if (preset != null)
            {
                LoadPresetIntoEditor(presetName, preset);
            }
        }

        private void RefreshPresetListPreservingCurrentFilter(
            bool restoreExpandState = true,
            IReadOnlyDictionary<string, bool>? expandStatesOverride = null,
            AppConfiguration? configOverride = null)
        {
            RefreshPresetList(
                restoreExpandState: restoreExpandState,
                expandStatesOverride: expandStatesOverride,
                filterText: _activePresetFilter,
                configOverride: configOverride);
        }

        private void RefreshFavoritesListPreservingCurrentFilter()
        {
            RefreshFavoritesList(_activePresetFilter);
        }

        private static TreeNode? FindNodeByTag(TreeNodeCollection nodes, string name, bool isFolder)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is PresetNodeTag tag && tag.IsFolder == isFolder && tag.Name == name)
                    return node;

                var found = FindNodeByTag(node.Nodes, name, isFolder);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static PresetNodeTag? CaptureSelectedPresetNodeTag(TreeView treeView)
        {
            if (treeView.SelectedNode?.Tag is not PresetNodeTag tag)
            {
                return null;
            }

            return ClonePresetNodeTag(tag);
        }

        private static PresetNodeTag? ClonePresetNodeTag(PresetNodeTag? tag)
        {
            if (tag == null)
            {
                return null;
            }

            return new PresetNodeTag
            {
                IsFolder = tag.IsFolder,
                Name = tag.Name
            };
        }

        private PresetNodeTag? GetRememberedPresetTreeSelection(TreeView treeView)
        {
            if (ReferenceEquals(treeView, trvFavorites))
            {
                return ClonePresetNodeTag(_lastFavoritesTreeSelection);
            }

            if (ReferenceEquals(treeView, trvPresets))
            {
                return ClonePresetNodeTag(_lastPresetsTreeSelection);
            }

            return null;
        }

        private void RememberPresetTreeSelection(TreeView treeView, PresetNodeTag? tag)
        {
            var clone = ClonePresetNodeTag(tag);
            if (ReferenceEquals(treeView, trvFavorites))
            {
                _lastFavoritesTreeSelection = clone;
            }
            else if (ReferenceEquals(treeView, trvPresets))
            {
                _lastPresetsTreeSelection = clone;
            }
        }

        private bool TryApplySelectedPresetNode(TreeView treeView, Action? onCancel = null)
        {
            return TryApplySelectedPresetNode(treeView, treeView.SelectedNode, onCancel);
        }

        private bool TryApplySelectedPresetNode(TreeView treeView, TreeNode? node, Action? onCancel = null)
        {
            var tag = node?.Tag as PresetNodeTag ?? GetRememberedPresetTreeSelection(treeView);
            if (tag == null)
            {
                return true;
            }

            if (!TryApplySelectedPresetNodeTag(tag, onCancel))
            {
                return false;
            }

            RememberPresetTreeSelection(treeView, tag);
            return true;
        }

        private bool TryApplySelectedPresetNodeTag(PresetNodeTag tag, Action? onCancel = null)
        {
            if (tag.IsFolder)
            {
                if (string.IsNullOrEmpty(_activePresetName) &&
                    string.Equals(_selectedFolderName, tag.Name, StringComparison.Ordinal))
                {
                    return true;
                }

                HandleFolderSelection(tag.Name, onCancel);
                return string.IsNullOrEmpty(_activePresetName) &&
                    string.Equals(_selectedFolderName, tag.Name, StringComparison.Ordinal);
            }

            if (string.Equals(_activePresetName, tag.Name, StringComparison.Ordinal) &&
                string.IsNullOrEmpty(_selectedFolderName))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(_activePresetName) &&
                !string.Equals(tag.Name, _activePresetName, StringComparison.Ordinal) &&
                IsPresetDirty())
            {
                if (!TryResolvePendingPresetChanges(onCancel))
                {
                    return false;
                }
            }

            var preset = _presetManager.Get(tag.Name);
            if (preset == null)
            {
                return false;
            }

            LoadPresetIntoEditor(tag.Name, preset);
            return true;
        }

        private void RestorePresetTabSelection(int tabIndex)
        {
            if (tabIndex < 0 ||
                tabIndex >= presetsTabControl.TabCount ||
                presetsTabControl.SelectedIndex == tabIndex)
            {
                return;
            }

            _restoringPresetsTabSelection = true;
            try
            {
                presetsTabControl.SelectedIndex = tabIndex;
            }
            finally
            {
                _restoringPresetsTabSelection = false;
            }
        }

        private void SelectPresetByName(string? presetName, bool ensureVisible = true)
        {
            if (string.IsNullOrEmpty(presetName))
                return;

            var targetNode = FindNodeByTag(trvPresets.Nodes, presetName, isFolder: false);
            if (targetNode != null)
            {
                // In state-preserving flows, avoid selecting hidden nodes because
                // SelectedNode itself can auto-expand collapsed ancestor folders.
                if (!ensureVisible && !PresetTreeSelectionGuard.CanSelectWithoutEnsuringVisible(targetNode))
                {
                    return;
                }

                // Suppress expand events while making the node visible
                // to avoid overwriting saved expand/collapse state
                _suppressExpandCollapseEvents = true;
                trvPresets.SelectedNode = targetNode;
                if (ensureVisible)
                {
                    targetNode.EnsureVisible();
                }
                _suppressExpandCollapseEvents = false;
                RememberPresetTreeSelection(trvPresets, targetNode.Tag as PresetNodeTag);
            }
        }

        private void SelectTreeNodeByTagName(string? name, bool isFolder, bool ensureVisible = true)
        {
            if (isFolder)
                SelectFolderByName(name, ensureVisible);
            else
                SelectPresetByName(name, ensureVisible);
        }

        private void SelectFolderByName(string? folderName, bool ensureVisible = true)
        {
            if (string.IsNullOrEmpty(folderName))
                return;

            var targetNode = FindNodeByTag(trvPresets.Nodes, folderName, isFolder: true);
            if (targetNode != null)
            {
                if (!ensureVisible && !PresetTreeSelectionGuard.CanSelectWithoutEnsuringVisible(targetNode))
                {
                    return;
                }

                _suppressExpandCollapseEvents = true;
                trvPresets.SelectedNode = targetNode;
                if (ensureVisible)
                {
                    targetNode.EnsureVisible();
                }
                _suppressExpandCollapseEvents = false;
                RememberPresetTreeSelection(trvPresets, targetNode.Tag as PresetNodeTag);
            }
        }

        private void ToggleFavorite()
        {
            // Use the TreeView that triggered the context menu, or fall back to checking both
            PresetNodeTag? tag = null;
            if (_contextMenuSourceTreeView?.SelectedNode?.Tag is PresetNodeTag sourceTag)
            {
                tag = sourceTag;
            }
            else if (trvPresets.SelectedNode?.Tag is PresetNodeTag presetsTag)
            {
                tag = presetsTag;
            }
            else if (trvFavorites.SelectedNode?.Tag is PresetNodeTag favoritesTag)
            {
                tag = favoritesTag;
            }

            if (tag == null)
                return;

            if (tag.IsFolder)
            {
                // Toggle folder favorite
                ToggleFolderFavorite(tag.Name);
            }
            else
            {
                // Toggle preset favorite
                TogglePresetFavorite(tag.Name);
            }
        }

        private void TogglePresetFavorite(string presetName)
        {
            var preset = _presetManager.Get(presetName);
            if (preset == null) return;
            var presetNode = FindPresetNodeByName(trvPresets.Nodes, presetName);
            var preActionExpandState = CapturePresetTreeExpandState();

            preset.IsFavorite = !preset.IsFavorite;
            _presetManager.Save(presetName, preset);

            bool usedIncrementalMutation = false;
            if (CanMutatePresetTreeIncrementally() && presetNode != null)
            {
                ApplyIncrementalPresetTreeMutation(
                    () => { usedIncrementalMutation = TryReinsertExistingPresetNodeIncrementally(presetNode, presetName); },
                    () => presetNode);
            }

            if (!usedIncrementalMutation)
            {
                RefreshPresetListPreservingCurrentFilter(expandStatesOverride: preActionExpandState);
                SelectPresetByName(presetName, ensureVisible: false);
            }

            RefreshFavoritesListPreservingCurrentFilter();

            UpdateStatusBar(preset.IsFavorite ? $"'{presetName}' added to favorites" : $"'{presetName}' removed from favorites");
            ClearPresetDeleteUndoHistory();
        }

        private void ToggleFolderFavorite(string folderName)
        {
            if (!_presetManager.Folders.TryGetValue(folderName, out var folderInfo))
                return;
            var folderNode = FindNodeByTag(trvPresets.Nodes, folderName, isFolder: true);
            var preActionExpandState = CapturePresetTreeExpandState();

            bool newFavoriteState = !folderInfo.IsFavorite;
            _presetManager.SetFolderFavorite(folderName, newFavoriteState);

            bool usedIncrementalMutation = false;
            if (CanMutatePresetTreeIncrementally() && folderNode != null)
            {
                ApplyIncrementalPresetTreeMutation(
                    () =>
                    {
                        UpdatePresetTreeNodeDisplay(folderNode);
                        usedIncrementalMutation = true;
                    },
                    () => folderNode);
            }

            if (!usedIncrementalMutation)
            {
                RefreshPresetListPreservingCurrentFilter(expandStatesOverride: preActionExpandState);
                SelectFolderByName(folderName, ensureVisible: false);
            }

            RefreshFavoritesListPreservingCurrentFilter();

            UpdateStatusBar(newFavoriteState ? $"Folder '{folderName}' added to favorites" : $"Folder '{folderName}' removed from favorites");
            ClearPresetDeleteUndoHistory();
        }

        private string GetPresetNameFromDisplay(string displayName)
        {
            return displayName.StartsWith($"{StarIcon} ", StringComparison.Ordinal) ? displayName.Substring(2) : displayName;
        }

        private static string NormalizeCommandTextForComparison(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // Compare command text independent of newline representation (LF/CRLF/CR).
            return value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
        }

        private PresetInfo? GetExistingPresetForPendingSave(string presetName, out string? existingPresetName)
        {
            if (!string.IsNullOrWhiteSpace(_activePresetName))
            {
                var activePreset = _presetManager.Get(_activePresetName);
                if (activePreset != null)
                {
                    existingPresetName = _activePresetName;
                    return activePreset;
                }
            }

            var matchingPreset = _presetManager.Get(presetName);
            if (matchingPreset != null)
            {
                existingPresetName = presetName;
                return matchingPreset;
            }

            existingPresetName = null;
            return null;
        }

        private static bool HasPendingPresetChanges(
            string presetName,
            string commands,
            int? timeout,
            PresetInfo? existingPreset,
            string? existingPresetName)
        {
            if (existingPreset == null || string.IsNullOrWhiteSpace(existingPresetName))
            {
                return true;
            }

            bool nameChanged = !string.Equals(presetName, existingPresetName, StringComparison.Ordinal);
            bool commandsChanged = !string.Equals(
                NormalizeCommandTextForComparison(commands),
                NormalizeCommandTextForComparison(existingPreset.Commands),
                StringComparison.Ordinal);
            bool timeoutChanged = existingPreset.Timeout != timeout;

            return nameChanged || commandsChanged || timeoutChanged;
        }

        private PresetSaveImpactAction ShowPresetSavePrompt(
            string presetName,
            string commands,
            string currentTimeoutText,
            int? timeout,
            bool allowDiscard)
        {
            var existingPreset = GetExistingPresetForPendingSave(presetName, out var existingPresetName);
            if (existingPreset == null || string.IsNullOrWhiteSpace(existingPresetName))
            {
                return allowDiscard
                    ? PresetSaveImpactAction.Discard
                    : PresetSaveImpactAction.SaveExisting;
            }

            var nameChanged = !string.Equals(presetName, existingPresetName, StringComparison.Ordinal);
            var hasPendingChanges = HasPendingPresetChanges(
                presetName,
                commands,
                timeout,
                existingPreset,
                existingPresetName);

            if (!hasPendingChanges)
            {
                return PresetSaveImpactAction.SaveExisting;
            }

            var impact = PresetSaveImpactResolver.Resolve(
                _presetManager,
                existingPresetName!,
                existingPreset.Folder);

            if (!impact.HasAffectedJobs && !nameChanged && !allowDiscard)
            {
                return PresetSaveImpactAction.SaveExisting;
            }

            using var dialog = new UnsavedPresetDiffDialog(
                existingPresetName!,
                presetName,
                existingPreset.Timeout,
                currentTimeoutText,
                existingPreset.Commands,
                commands,
                _isDarkMode,
                impact,
                GetPresetSavePromptMode(nameChanged, allowDiscard));
            DialogTheme.SetDialogFont(dialog, _dialogFont);
            dialog.ShowDialog(this);
            return dialog.SelectedAction;
        }

        private bool TryResolvePendingPresetChanges(Action? onCancel = null)
        {
            if (!IsPresetDirty())
            {
                return true;
            }

            string presetName = txtPreset.Text.Trim();
            string commands = txtCommand.Text;
            int? timeout = int.TryParse(txtTimeoutHeader.Text, out var parsedTimeout) ? parsedTimeout : null;

            var action = ShowPresetSavePrompt(
                presetName,
                commands,
                txtTimeoutHeader.Text ?? string.Empty,
                timeout,
                allowDiscard: true);

            if (action == PresetSaveImpactAction.Cancel)
            {
                onCancel?.Invoke();
                return false;
            }

            if (action == PresetSaveImpactAction.Discard)
            {
                return true;
            }

            if (!SaveCurrentPreset(action))
            {
                onCancel?.Invoke();
                return false;
            }

            return true;
        }

        private static PresetSavePromptMode GetPresetSavePromptMode(bool nameChanged, bool allowDiscard)
        {
            if (nameChanged)
            {
                return allowDiscard
                    ? PresetSavePromptMode.RenameExistingCreateNewDiscardCancel
                    : PresetSavePromptMode.RenameExistingCreateNewCancel;
            }

            return allowDiscard
                ? PresetSavePromptMode.SaveDiscardCancel
                : PresetSavePromptMode.SaveCancel;
        }

        private bool IsPresetDirty()
        {
            // When viewing a folder (not a preset), there's nothing to save
            if (!string.IsNullOrEmpty(_selectedFolderName)) return false;

            if (string.IsNullOrEmpty(_activePresetName)) return InputValidator.IsNotEmpty(txtPreset.Text) || InputValidator.IsNotEmpty(txtCommand.Text);

            var preset = _presetManager.Get(_activePresetName);
            if (preset == null) return InputValidator.IsNotEmpty(txtPreset.Text) || InputValidator.IsNotEmpty(txtCommand.Text);

            bool nameChanged = !string.Equals(txtPreset.Text?.Trim(), _activePresetName, StringComparison.Ordinal);
            var currentCommands = NormalizeCommandTextForComparison(txtCommand.Text);
            var savedCommands = NormalizeCommandTextForComparison(preset.Commands);
            bool commandsChanged = !string.Equals(currentCommands, savedCommands, StringComparison.Ordinal);

            bool timeoutDiffers = int.TryParse(txtTimeoutHeader.Text, out var t)
                ? preset.Timeout != t
                : preset.Timeout.HasValue;

            return nameChanged || commandsChanged || timeoutDiffers;
        }

        private string GetPresetDirtyDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Unsaved preset changes detected. Save before exiting?");
            sb.AppendLine();
            sb.AppendLine("=== Debug Info ===");
            sb.AppendLine($"Selected folder: \"{_selectedFolderName ?? "(none)"}\"");

            if (string.IsNullOrEmpty(_activePresetName))
            {
                sb.AppendLine($"No active preset loaded.");
                sb.AppendLine($"Preset name field: \"{txtPreset.Text}\"");
                sb.AppendLine($"Command field has content: {InputValidator.IsNotEmpty(txtCommand.Text)}");
                return sb.ToString();
            }

            var preset = _presetManager.Get(_activePresetName);
            if (preset == null)
            {
                sb.AppendLine($"Active preset \"{_activePresetName}\" not found in preset manager.");
                sb.AppendLine($"Preset name field: \"{txtPreset.Text}\"");
                sb.AppendLine($"Command field has content: {InputValidator.IsNotEmpty(txtCommand.Text)}");
                return sb.ToString();
            }

            sb.AppendLine($"Active preset: \"{_activePresetName}\"");
            sb.AppendLine();

            bool nameChanged = !string.Equals(txtPreset.Text?.Trim(), _activePresetName, StringComparison.Ordinal);
            sb.AppendLine($"[{(nameChanged ? "X" : " ")}] Name changed:");
            if (nameChanged)
            {
                sb.AppendLine($"    Saved: \"{_activePresetName}\"");
                sb.AppendLine($"    Current: \"{txtPreset.Text?.Trim()}\"");
            }

            var savedCmd = preset.Commands ?? string.Empty;
            var currentCmd = txtCommand.Text ?? string.Empty;
            var normalizedSavedCmd = NormalizeCommandTextForComparison(savedCmd);
            var normalizedCurrentCmd = NormalizeCommandTextForComparison(currentCmd);
            bool commandsChanged = !string.Equals(normalizedCurrentCmd, normalizedSavedCmd, StringComparison.Ordinal);
            sb.AppendLine($"[{(commandsChanged ? "X" : " ")}] Commands changed:");
            if (commandsChanged)
            {
                sb.AppendLine($"    Saved length: {savedCmd.Length} chars");
                sb.AppendLine($"    Current length: {currentCmd.Length} chars");
                sb.AppendLine($"    Saved normalized length: {normalizedSavedCmd.Length} chars");
                sb.AppendLine($"    Current normalized length: {normalizedCurrentCmd.Length} chars");
                if (savedCmd.Length < 100 && currentCmd.Length < 100)
                {
                    sb.AppendLine($"    Saved: \"{savedCmd.Replace("\r\n", "\\n").Replace("\n", "\\n")}\"");
                    sb.AppendLine($"    Current: \"{currentCmd.Replace("\r\n", "\\n").Replace("\n", "\\n")}\"");
                }
            }

            bool timeoutDiffers = int.TryParse(txtTimeoutHeader.Text, out var t)
                ? preset.Timeout != t
                : preset.Timeout.HasValue;
            sb.AppendLine($"[{(timeoutDiffers ? "X" : " ")}] Timeout changed:");
            if (timeoutDiffers)
            {
                sb.AppendLine($"    Saved: {preset.Timeout?.ToString() ?? "(null)"}");
                sb.AppendLine($"    Current: \"{txtTimeoutHeader.Text}\"");
            }

            return sb.ToString();
        }

        #endregion

        #region SSH Execution

        private async Task ExecutePresetOnRowsAsync(
            List<DataGridViewRow> hostRows,
            Func<int, string> startStatus,
            Func<int, string> completionStatus,
            Stopwatch sw,
            bool includeCommandPreview = false)
        {
            SshDebugLog("EXEC", "ExecutePresetOnRowsAsync entered");

            if (_executionCoordinator.IsRunning)
            {
                SshDebugLog("EXEC", "Aborted - SSH service already running", sw);
                return;
            }

            SshDebugLog("EXEC", "Building host connections", sw);
            var hosts = GetHostConnections(hostRows).ToList();
            SshDebugLog("EXEC", $"Host connections built: {hosts.Count} host(s)", sw);

            string presetDisplayName = string.IsNullOrWhiteSpace(txtPreset.Text) ? "Current Preset" : txtPreset.Text.Trim();
            FolderExecutionOptions? dialogOptions = null;

            if (ExecutionDialogPolicy.ShouldPromptForPresetExecutionOptions(hosts.Count))
            {
                var hostAddresses = hosts.Select(h => h.ToString()).ToList();
                using var dialog = new FolderExecutionDialog(presetDisplayName, new List<string> { presetDisplayName }, hostAddresses, _isDarkMode);
                DialogTheme.SetDialogFont(dialog, _dialogFont);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                dialogOptions = dialog.Options;
                if (dialogOptions.SelectedPresets.Count == 0)
                    return;
                if (dialogOptions.SelectedHostIndices.Count == 0)
                    return;

                hosts = dialogOptions.SelectedHostIndices
                    .Where(i => i >= 0 && i < hosts.Count)
                    .Select(i => hosts[i])
                    .ToList();
            }

            if (hosts.Count == 0)
                return;

            SshDebugLog("EXEC", "Preparing execution options", sw);
            int commandTimeout = InputValidator.ParseIntOrDefault(txtTimeoutHeader.Text, _configService.GetCurrent().Timeout);
            var preparation = _executionCoordinator.PrepareExecution(txtCommand.Text, commandTimeout);
            SshDebugLog("EXEC", $"Timeouts configured - command: {preparation.CommandTimeoutSeconds}s, connection: {preparation.ConnectionTimeoutSeconds}s", sw);

            var preset = preparation.Preset;

            // Validate column dependencies before entering execution mode
            if (!ValidateColumnDependencies(preset))
                return;

            SshDebugLog("EXEC", "Calling SetExecutionMode(true)", sw);
            SetExecutionMode(true);
            ClearOutput();
            if (includeCommandPreview)
            {
                var commandPreview = txtCommand.Text.Length > 50 ? txtCommand.Text.Substring(0, 50) + "..." : txtCommand.Text;
                SshDebugLog("EXEC", $"Preset created. IsScript: {preset.IsScript}, Commands: {commandPreview.Replace("\r", "\\r").Replace("\n", "\\n")}", sw);
            }
            else
            {
                SshDebugLog("EXEC", $"Preset created. IsScript: {preset.IsScript}", sw);
            }

            if (dialogOptions != null)
            {
                var totalOperations = hosts.Count * Math.Max(1, dialogOptions.SelectedPresets.Count);
                var manualProgress = BeginManualExecutionProgress(totalOperations);
                if (manualProgress == null)
                {
                    UpdateStatusBar(startStatus(hosts.Count));
                }
                var branchExecutionStartUtc = DateTime.UtcNow;

                try
                {
                    List<ExecutionResult> results;
                    var presets = new Dictionary<string, PresetInfo>(StringComparer.Ordinal)
                    {
                        [presetDisplayName] = preparation.Preset
                    };

                    SshDebugLog("EXEC", "Calling ExecuteFolderAsync for multi-host preset execution", sw);
                    results = await _sshService.ExecuteFolderAsync(
                        hosts,
                        presets,
                        tsbUsername.Text,
                        tsbPassword.Text,
                        preparation.Timeouts,
                        dialogOptions,
                        manualProgress);
                    SshDebugLog("EXEC", $"ExecuteFolderAsync completed. Results: {results.Count}", sw);

                    var wasCancelled = WasManualExecutionCancelled(results);
                    if (wasCancelled)
                    {
                        AppendOutputText(Environment.NewLine + Environment.NewLine + "Execution cancelled." + Environment.NewLine);
                    }

                    var executionDetails = BuildExecutionDetails(
                        presetDisplayName,
                        preset.Commands,
                        preset.Type.ToString(),
                        branchExecutionStartUtc,
                        DateTime.UtcNow,
                        tsbUsername.Text,
                        preparation.CommandTimeoutSeconds,
                        preparation.ConnectionTimeoutSeconds,
                        _sshService.UseConnectionPooling,
                        BuildRunModeDescription(dialogOptions, isFolderExecution: false),
                        wasCancelled,
                        isFolderExecution: false,
                        string.Empty,
                        new[] { presetDisplayName },
                        hosts,
                        results);

                    StoreExecutionHistory(results, executionDetails);
                    UpdateStatusBar(wasCancelled
                        ? BuildCancelledPresetStatus(hosts)
                        : completionStatus(results.Count));
                }
                catch (Exception ex)
                {
                    SshDebugLog("EXEC", $"Exception: {ex.GetType().Name}: {ex.Message}", sw);
                    DialogTheme.Show(this, $"An error occurred: {ex.Message}", Application.ProductName ?? "Message", MessageBoxButtons.OK, MessageBoxIcon.None);
                    UpdateStatusBar("Execution failed");
                }
                finally
                {
                    SshDebugLog("EXEC", "Execution complete, calling SetExecutionMode(false)", sw);
                    SetExecutionMode(false);
                }
                return;
            }
            else
            {
                UpdateStatusBar(startStatus(hosts.Count));
            }
            var executionStartUtc = DateTime.UtcNow;

            try
            {
                List<ExecutionResult> results;
                SshDebugLog("EXEC", "Calling ExecutePresetAsync - SSH connection starting", sw);
                results = await _executionCoordinator.ExecutePresetAsync(
                    hosts,
                    preparation,
                    tsbUsername.Text,
                    tsbPassword.Text);
                SshDebugLog("EXEC", $"ExecutePresetAsync completed. Results: {results.Count}", sw);
                var wasCancelled = WasManualExecutionCancelled(results);
                if (wasCancelled)
                {
                    AppendOutputText(Environment.NewLine + Environment.NewLine + "Execution cancelled." + Environment.NewLine);
                }

                var executionDetails = BuildExecutionDetails(
                    presetDisplayName,
                    preset.Commands,
                    preset.Type.ToString(),
                    executionStartUtc,
                    DateTime.UtcNow,
                    tsbUsername.Text,
                    preparation.CommandTimeoutSeconds,
                    preparation.ConnectionTimeoutSeconds,
                    _sshService.UseConnectionPooling,
                    BuildRunModeDescription(dialogOptions, isFolderExecution: false),
                    wasCancelled,
                    isFolderExecution: false,
                    string.Empty,
                    new[] { presetDisplayName },
                    hosts,
                    results);

                StoreExecutionHistory(results, executionDetails);
                UpdateStatusBar(wasCancelled
                    ? BuildCancelledPresetStatus(hosts)
                    : completionStatus(results.Count));
            }
            catch (Exception ex)
            {
                SshDebugLog("EXEC", $"Exception: {ex.GetType().Name}: {ex.Message}", sw);
                DialogTheme.Show(this, $"An error occurred: {ex.Message}", Application.ProductName ?? "Message", MessageBoxButtons.OK, MessageBoxIcon.None);
                UpdateStatusBar("Execution failed");
            }
            finally
            {
                SshDebugLog("EXEC", "Execution complete, calling SetExecutionMode(false)", sw);
                SetExecutionMode(false);
            }
        }

        /// <summary>
        /// Validates that grid columns referenced by the preset exist before execution.
        /// Returns true to proceed, false to cancel.
        /// </summary>
        private bool ValidateColumnDependencies(PresetInfo preset)
        {
            return ValidateColumnDependencies(new[] { preset });
        }

        /// <summary>
        /// Validates that grid columns referenced by the presets exist before execution.
        /// Returns true to proceed, false to cancel.
        /// </summary>
        private bool ValidateColumnDependencies(IEnumerable<PresetInfo> presets)
        {
            var analyzer = new ScriptDependencyAnalyzer();

            // Collect existing grid column names
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewColumn col in dgv_variables.Columns)
            {
                existingColumns.Add(col.Name);
            }

            var missingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var environmentVariableNames = _environmentService.GetActiveEnvironmentVariables().Keys;
                foreach (var preset in presets)
                {
                    var result = analyzer.AnalyzePresetDetails(preset, environmentVariableNames);
                    if (result.SuppressMissingColumnWarning)
                        continue;

                    foreach (var referencedColumn in result.ReferencedColumns)
                    {
                        if (!string.IsNullOrWhiteSpace(referencedColumn) &&
                            !existingColumns.Contains(referencedColumn))
                        {
                            missingColumns.Add(referencedColumn);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If analysis fails (e.g., parse error), don't block execution.
                // The actual execution path will report parse errors properly.
                SshDebugLog("VALIDATE", $"Column dependency analysis failed: {ex.Message}");
                return true;
            }

            if (missingColumns.Count == 0)
                return true;

            var columnList = string.Join(
                "\n",
                missingColumns
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .Select(c => $"  \u2022 {c}"));
            var message = $"The following column(s) are referenced but do not exist in the grid:\n\n" +
                          columnList +
                          "\n\nThese references will resolve to empty values.\n\nContinue with execution?";

            var dialogResult = DialogTheme.Show(
                this,
                message,
                "Missing Column References",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return dialogResult == DialogResult.Yes;
        }

        private bool ValidateFolderInteractiveRestrictions(IReadOnlyDictionary<string, PresetInfo> presets)
        {
            var interactivePresetNames = GetInteractiveFolderPresetNames(presets);
            if (interactivePresetNames.Count == 0)
                return true;

            var presetList = string.Join("\n", interactivePresetNames.Select(name => $"  \u2022 {name}"));
            var message =
                "Folder execution cannot include presets that use the 'interactive' step.\n\n" +
                "Blocked preset(s):\n" +
                presetList +
                "\n\nRun those presets directly against a single current host instead.";

            DialogTheme.Show(
                this,
                message,
                "Interactive Presets Not Allowed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return false;
        }

        private static List<string> GetInteractiveFolderPresetNames(IReadOnlyDictionary<string, PresetInfo> presets)
        {
            var parser = new ScriptParser();
            var analyzer = new ScriptDependencyAnalyzer();
            var interactivePresetNames = new List<string>();

            foreach (var entry in presets)
            {
                if (!entry.Value.IsScript)
                    continue;

                SSH_Helper.Services.Scripting.Models.Script script;
                try
                {
                    script = parser.Parse(entry.Value.Commands);
                    var validationErrors = parser.Validate(script, entry.Value.Commands, enforceCanonicalSyntax: true);
                    if (validationErrors.Count > 0)
                        continue;
                }
                catch (Exception)
                {
                    continue;
                }

                if (analyzer.AnalyzeSshRequirements(script).UsesInteractive)
                    interactivePresetNames.Add(entry.Key);
            }

            return interactivePresetNames;
        }

        private async void ExecuteOnAllHosts()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SshDebugLog("EXEC", "ExecuteOnAllHosts entered");

            var hostRows = dgv_variables.Rows.Cast<DataGridViewRow>().ToList();
            await ExecutePresetOnRowsAsync(
                hostRows,
                hostCount => $"Executing on {hostCount} hosts...",
                resultCount => $"Completed execution on {resultCount} hosts",
                sw);
        }

        private async void ExecuteOnSelectedHost()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SshDebugLog("EXEC", "ExecuteOnSelectedHost entered");

            if (dgv_variables.CurrentCell == null)
            {
                ClearOutput();
                AppendOutputText("No host selected");
                return;
            }

            var row = dgv_variables.Rows[dgv_variables.CurrentCell.RowIndex];
            string host = GetCellValue(row, CsvManager.HostColumnName);
            SshDebugLog("EXEC", $"Host from grid: {host}", sw);

            if (row.IsNewRow || string.IsNullOrWhiteSpace(host) || !InputValidator.IsValidHostOrIp(host))
            {
                ClearOutput();
                AppendOutputText("No valid host selected");
                return;
            }

            await ExecutePresetOnRowsAsync(
                new List<DataGridViewRow> { row },
                _ => $"Executing on {host}...",
                _ => $"Completed execution on {host}",
                sw,
                includeCommandPreview: true);
        }

        private async void ExecuteOnCheckedHosts()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SshDebugLog("EXEC", "ExecuteOnCheckedHosts entered");

            // Get rows with checkbox checked
            var checkedRows = dgv_variables.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow &&
                            r.Cells[SelectColumnName].Value is true)
                .ToList();

            if (checkedRows.Count == 0)
            {
                DialogTheme.Show(this, "No hosts selected. Check the boxes next to hosts you want to execute on.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await ExecutePresetOnRowsAsync(
                checkedRows,
                hostCount => $"Executing on {hostCount} selected hosts...",
                resultCount => $"Completed execution on {resultCount} hosts",
                sw);
        }

        private async void ExecuteFolderPresetsOnAllHosts(string folderName)
        {
            var hostRows = dgv_variables.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow && !string.IsNullOrWhiteSpace(GetCellValue(r, CsvManager.HostColumnName)))
                .ToList();

            if (hostRows.Count == 0)
            {
                DialogTheme.Show(this, "No hosts available.", "Run Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await ExecuteFolderPresetsAsync(folderName, hostRows);
        }

        private async void ExecuteFolderPresetsOnSelectedHost(string folderName)
        {
            if (dgv_variables.CurrentCell == null)
            {
                ClearOutput();
                AppendOutputText("No host selected");
                return;
            }

            var row = dgv_variables.Rows[dgv_variables.CurrentCell.RowIndex];
            string host = GetCellValue(row, CsvManager.HostColumnName);

            if (row.IsNewRow || string.IsNullOrWhiteSpace(host) || !InputValidator.IsValidHostOrIp(host))
            {
                ClearOutput();
                AppendOutputText("No valid host selected");
                return;
            }

            await ExecuteFolderPresetsAsync(folderName, new List<DataGridViewRow> { row });
        }

        private async void ExecuteFolderPresetsOnCheckedHosts(string folderName)
        {
            // Get checked host rows with valid hosts
            var checkedRows = dgv_variables.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow &&
                            r.Cells[SelectColumnName].Value is true &&
                            !string.IsNullOrWhiteSpace(GetCellValue(r, CsvManager.HostColumnName)))
                .ToList();

            if (checkedRows.Count == 0)
            {
                ClearOutput();
                AppendOutputText("No valid hosts checked");
                return;
            }

            await ExecuteFolderPresetsAsync(folderName, checkedRows);
        }

        private async Task ExecuteFolderPresetsAsync(string folderName, IEnumerable<DataGridViewRow> rows)
        {
            var config = _configService.Load();
            var presetNames = GetSortedPresetsInFolder(folderName, config).ToList();
            if (presetNames.Count == 0)
            {
                DialogTheme.Show(this, $"Folder '{folderName}' contains no presets.", "Run Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var hostRows = rows.ToList();
            var hostAddresses = hostRows
                .Select(r => GetCellValue(r, CsvManager.HostColumnName))
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .ToList();

            using var dialog = new FolderExecutionDialog(folderName, presetNames, hostAddresses, _isDarkMode);
            DialogTheme.SetDialogFont(dialog, _dialogFont);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var options = dialog.Options;
            if (options.SelectedPresets.Count == 0)
                return;

            if (_sshService.IsRunning) return;

            await ExecuteFolderWithOptionsAsync(folderName, options, hostRows);
        }

        private async Task ExecuteFolderWithOptionsAsync(string folderName, FolderExecutionOptions options, List<DataGridViewRow> hostRows)
        {
            var config = _configService.Load();
            int connectionTimeout = config.ConnectionTimeout;

            // Filter hostRows by selected indices if specified
            if (options.SelectedHostIndices.Count > 0)
            {
                hostRows = options.SelectedHostIndices
                    .Where(i => i >= 0 && i < hostRows.Count)
                    .Select(i => hostRows[i])
                    .ToList();
            }

            if (!TryApplyFolderEnvironment(folderName))
                return;

            // Build preset dictionary
            var presets = new Dictionary<string, PresetInfo>();
            foreach (var presetName in options.SelectedPresets)
            {
                var preset = _presetManager.Get(presetName);
                if (preset != null)
                    presets[presetName] = preset;
            }

            if (presets.Count == 0)
                return;

            if (!ValidateFolderInteractiveRestrictions(presets))
                return;

            // Validate column dependencies before entering execution mode
            if (!ValidateColumnDependencies(presets.Values))
                return;

            var hosts = GetHostConnections(hostRows).ToList();
            if (hosts.Count == 0)
                return;

            SetExecutionMode(true);
            ClearOutput();

            int totalOperations = hosts.Count * presets.Count;
            var progress = BeginManualExecutionProgress(totalOperations);
            if (progress == null)
            {
                UpdateStatusBar($"Executing folder '{folderName}' on {hosts.Count} hosts...");
            }

            // Use default timeout from first preset or config
            int commandTimeout = presets.Values.FirstOrDefault()?.Timeout ?? config.Timeout;
            var timeouts = SshTimeoutOptions.Create(commandTimeout, connectionTimeout);
            var executionStartUtc = DateTime.UtcNow;

            try
            {
                var results = await _sshService.ExecuteFolderAsync(
                    hosts,
                    presets,
                    tsbUsername.Text,
                    tsbPassword.Text,
                    timeouts,
                    options,
                    progress);

                var wasCancelled = WasManualExecutionCancelled(results);
                if (wasCancelled)
                {
                    AppendOutputText(Environment.NewLine + Environment.NewLine + $"Folder '{folderName}' cancelled." + Environment.NewLine);
                }

                var executionDetails = BuildExecutionDetails(
                    folderName,
                    BuildFolderCommandSnapshot(options.SelectedPresets, presets),
                    "Folder",
                    executionStartUtc,
                    DateTime.UtcNow,
                    tsbUsername.Text,
                    commandTimeout,
                    connectionTimeout,
                    _sshService.UseConnectionPooling,
                    BuildRunModeDescription(options, isFolderExecution: true),
                    wasCancelled,
                    isFolderExecution: true,
                    folderName,
                    options.SelectedPresets,
                    hosts,
                    results);

                // Store single history entry for the entire folder execution
                StoreFolderExecutionHistory(folderName, results, executionDetails);

                int successCount = results.Count(r => r.Success);
                int failCount = results.Count(r => !r.Success && !r.WasCancelled);
                string status = wasCancelled
                    ? BuildCancelledFolderStatus(folderName)
                    : failCount > 0
                        ? $"Completed folder '{folderName}': {successCount} succeeded, {failCount} failed"
                        : $"Completed folder '{folderName}' on {hosts.Count} hosts";
                UpdateStatusBar(status);
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusBar("Execution failed");
            }
            finally
            {
                SetExecutionMode(false);
            }
        }

        private string StoreFolderExecutionHistory(string folderName, List<ExecutionResult> results, ExecutionDetails? details = null)
        {
            var hostResults = BuildHostHistoryEntries(results);
            var output = GetBufferedOutputSnapshot();
            if (string.IsNullOrEmpty(output))
            {
                var combinedOutput = new StringBuilder();
                foreach (var hostResult in hostResults)
                {
                    combinedOutput.Append(hostResult.Output);
                }

                output = combinedOutput.ToString();
            }

            string label = BuildExecutionHistoryLabel(folderName, isFolder: true, details?.WasCancelled == true);
            var entryId = HistoryIdGenerator.NewId();
            var payload = new HistoryRunPayload
            {
                Id = entryId,
                Output = output,
                HostResults = hostResults.Count > 0 ? hostResults : null,
                Details = details == null ? null : CloneExecutionDetails(details)
            };
            var indexEntry = BuildHistoryIndexEntry(entryId, label, payload);

            Invoke(() =>
            {
                try
                {
                    _historyStorage.SaveRun(indexEntry, payload, _configService.GetCurrent().MaxHistoryEntries);
                    InsertHistoryEntryIntoList(indexEntry, payload);
                    SaveConfiguration();
                }
                catch (Exception ex)
                {
                    DialogTheme.Show(this, $"Failed to persist history run: {ex.Message}", "History Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });

            return entryId;
        }

        private string GetBufferedOutputSnapshot()
        {
            lock (_outputBufferLock)
            {
                return _outputBuffer.ToString();
            }
        }

        private string BuildExecutionHistoryLabel(string? executionName, bool isFolder, bool wasCancelled)
        {
            var name = string.IsNullOrWhiteSpace(executionName)
                ? "(unnamed)"
                : executionName.Trim();
            var suffix = isFolder ? $"{FolderIcon} {name}" : name;
            var prefix = wasCancelled ? "CANCELLED - " : string.Empty;
            return $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {prefix}{suffix}";
        }

        private bool WasManualExecutionCancelled(IReadOnlyList<ExecutionResult> results)
        {
            return _manualCancellationRequested || (results?.Any(result => result.WasCancelled) ?? false);
        }

        private static string BuildCancelledPresetStatus(IReadOnlyList<HostConnection> hosts)
        {
            if (hosts.Count == 1)
                return $"Cancelled execution on {hosts[0]}";

            return $"Cancelled execution on {hosts.Count} hosts";
        }

        private static string BuildCancelledFolderStatus(string folderName)
            => $"Cancelled folder '{folderName}'";

        private ExecutionDetails BuildExecutionDetails(
            string presetName,
            string commands,
            string presetType,
            DateTime startTimeUtc,
            DateTime endTimeUtc,
            string username,
            int commandTimeoutSeconds,
            int connectionTimeoutSeconds,
            bool useConnectionPooling,
            string runMode,
            bool wasCancelled,
            bool isFolderExecution,
            string folderName,
            IEnumerable<string> executedPresetNames,
            IReadOnlyList<HostConnection> hosts,
            IReadOnlyList<ExecutionResult> results)
        {
            return new ExecutionDetails
            {
                PresetName = presetName,
                Commands = commands ?? string.Empty,
                PresetType = presetType,
                WasCancelled = wasCancelled,
                StartTimeUtc = startTimeUtc,
                EndTimeUtc = endTimeUtc,
                EnvironmentName = string.IsNullOrWhiteSpace(_activeEnvironmentName)
                    ? EnvironmentConfig.DefaultName
                    : _activeEnvironmentName,
                Username = username ?? string.Empty,
                CommandTimeoutSeconds = commandTimeoutSeconds,
                ConnectionTimeoutSeconds = connectionTimeoutSeconds,
                UseConnectionPooling = useConnectionPooling,
                RunMode = runMode,
                IsFolderExecution = isFolderExecution,
                FolderName = folderName ?? string.Empty,
                ExecutedPresetNames = executedPresetNames?.ToList() ?? new List<string>(),
                Hosts = BuildHostExecutionContexts(hosts, results, endTimeUtc, wasCancelled),
                InteractiveSessions = BuildInteractiveSessionDetails(results)
            };
        }

        private static List<SSH_Helper.Models.HostExecutionContext> BuildHostExecutionContexts(
            IReadOnlyList<HostConnection> hosts,
            IReadOnlyList<ExecutionResult> results,
            DateTime fallbackTimestampUtc,
            bool runWasCancelled)
        {
            var hostResultLookup = (results ?? Array.Empty<ExecutionResult>())
                .GroupBy(r => r.Host.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var contexts = new List<SSH_Helper.Models.HostExecutionContext>();
            if (hosts == null || hosts.Count == 0)
                return contexts;

            foreach (var host in hosts)
            {
                hostResultLookup.TryGetValue(host.ToString(), out var hostResults);
                var hostSuccess = hostResults != null && hostResults.Count > 0 && hostResults.All(r => r.Success);
                var hostWasCancelled = hostResults != null && hostResults.Any(r => r.WasCancelled);
                if (!hostWasCancelled && runWasCancelled && (hostResults == null || hostResults.Count == 0))
                {
                    hostWasCancelled = true;
                }

                var hostTimestampUtc = hostResults != null && hostResults.Count > 0
                    ? hostResults.Max(r => r.Timestamp.ToUniversalTime())
                    : fallbackTimestampUtc;

                var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in host.Variables)
                {
                    if (string.Equals(kvp.Key, "password", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.IsNullOrWhiteSpace(kvp.Key))
                        continue;

                    variables[kvp.Key] = kvp.Value ?? string.Empty;
                }

                contexts.Add(new SSH_Helper.Models.HostExecutionContext
                {
                    HostAddress = host.ToString(),
                    Success = hostSuccess,
                    WasCancelled = hostWasCancelled,
                    TimestampUtc = hostTimestampUtc,
                    Variables = variables
                });
            }

            return contexts;
        }

        private static List<InteractiveTerminalSessionDetails> BuildInteractiveSessionDetails(
            IReadOnlyList<ExecutionResult> results)
        {
            var sessions = new List<InteractiveTerminalSessionDetails>();
            if (results == null || results.Count == 0)
                return sessions;

            var nextSessionNumber = 1;
            foreach (var result in results)
            {
                if (result?.InteractiveSessions == null || result.InteractiveSessions.Count == 0)
                    continue;

                foreach (var session in result.InteractiveSessions)
                {
                    sessions.Add(new InteractiveTerminalSessionDetails
                    {
                        SessionNumber = nextSessionNumber++,
                        HostAddress = string.IsNullOrWhiteSpace(session.HostAddress)
                            ? result.Host?.ToString() ?? string.Empty
                            : session.HostAddress,
                        SessionMode = session.SessionMode ?? string.Empty,
                        EmulationMode = session.EmulationMode ?? string.Empty,
                        StartedAtUtc = session.StartedAtUtc,
                        EndedAtUtc = session.EndedAtUtc,
                        CloseReason = session.CloseReason ?? string.Empty,
                        Completed = session.Completed,
                        Transcript = session.Transcript ?? string.Empty
                    });
                }
            }

            return sessions;
        }

        private static string BuildRunModeDescription(FolderExecutionOptions? options, bool isFolderExecution)
        {
            if (options == null)
            {
                return isFolderExecution ? "Sequential presets, 1 host at a time" : "Single preset";
            }

            var presetMode = options.RunPresetsInParallel ? "Parallel presets" : "Sequential presets";
            var hostMode = options.ParallelHostCount > 1
                ? $"{options.ParallelHostCount} hosts in parallel"
                : "1 host at a time";
            var errorBehavior = options.StopOnFirstError ? "Stop on first error" : "Continue on error";
            return $"{presetMode}, {hostMode}, {errorBehavior}";
        }

        private static string BuildFolderCommandSnapshot(
            IReadOnlyList<string> selectedPresetNames,
            IReadOnlyDictionary<string, PresetInfo> presets)
        {
            if (selectedPresetNames == null || selectedPresetNames.Count == 0 || presets == null || presets.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var presetName in selectedPresetNames)
            {
                if (!presets.TryGetValue(presetName, out var preset))
                    continue;

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                }

                sb.AppendLine($"[{presetName}]");
                sb.Append(preset.Commands ?? string.Empty);
            }

            return sb.ToString();
        }

        private static List<HostHistoryEntry> BuildHostHistoryEntries(List<ExecutionResult> results)
        {
            var hostResults = new List<HostHistoryEntry>();
            if (results == null || results.Count == 0)
                return hostResults;

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var output = result.Output;
                if (i == 0)
                    output = output.TrimStart('\r', '\n');

                hostResults.Add(new HostHistoryEntry
                {
                    HostAddress = result.Host.ToString(),
                    Output = output,
                    Success = result.Success,
                    WasCancelled = result.WasCancelled,
                    Timestamp = result.Timestamp
                });
            }

            return hostResults;
        }

        private void ClearLoadedHistoryPayload(string? entryId = null)
        {
            if (!string.IsNullOrWhiteSpace(entryId) &&
                !string.Equals(_loadedHistoryPayloadId, entryId, StringComparison.Ordinal))
            {
                return;
            }

            var releasedPayloadChars = EstimatePayloadTextChars(_loadedHistoryPayload);
            _loadedHistoryPayloadId = null;
            _loadedHistoryPayload = null;
            _loadedHistoryPayloadHasDetails = false;
            _loadedHistoryPayloadHasHostOutputs = false;
            MaybeCompactAfterPayloadClear(releasedPayloadChars);
        }

        private void StopExecution()
        {
            if (!_sshService.IsRunning || _manualCancellationRequested)
                return;

            // Immediate visual feedback - disable button and change text
            btnStopAll.Enabled = false;
            btnStopAll.Text = "Cancelling...";
            UpdateStatusBar("Cancellation requested...");
            _manualCancellationRequested = true;

            // Request cancellation
            _sshService.Stop();

            // Append stop message to output
            AppendOutputText(Environment.NewLine + Environment.NewLine + "Cancellation requested by user" + Environment.NewLine);
        }

        private IEnumerable<HostConnection> GetHostConnections(IEnumerable<DataGridViewRow> rows)
        {
            // Check if SSH config is enabled
            var sshConfigEnabled = _configService.GetCurrent().SshConfig.EnableSshConfig;

            foreach (var row in rows)
            {
                if (row.IsNewRow) continue;

                string hostIp = GetCellValue(row, CsvManager.HostColumnName);
                if (string.IsNullOrWhiteSpace(hostIp) || !InputValidator.IsValidHostOrIp(hostIp))
                    continue;

                var host = HostConnection.Parse(hostIp);
                host.Username = GetCellValue(row, "username");
                var resolvedUsername = string.IsNullOrWhiteSpace(host.Username) ? tsbUsername.Text : host.Username;
                var passwordValue = GetCellValue(row, "password");

                var useCredentialManager = _credentialProvider?.IsAvailable == true &&
                                           _configService.GetCurrent().Credentials.UseCredentialManager;
                if (useCredentialManager)
                {
                    if (!string.IsNullOrWhiteSpace(passwordValue))
                    {
                        StoreHostPassword(host.ToString(), resolvedUsername, passwordValue);
                    }
                    else if (TryResolveHostPassword(host.ToString(), resolvedUsername, out var storedPassword))
                    {
                        passwordValue = storedPassword;
                    }
                }

                host.Password = passwordValue;

                // Collect all variables from the row
                foreach (DataGridViewColumn col in dgv_variables.Columns)
                {
                    host.Variables[col.Name] = row.Cells[col.Index].Value?.ToString() ?? "";
                }

                if (!string.IsNullOrEmpty(host.Password))
                {
                    host.Variables["password"] = host.Password;
                }

                var environmentVariables = _environmentService.GetActiveEnvironmentVariables();
                foreach (var kvp in environmentVariables)
                {
                    if (!host.Variables.TryGetValue(kvp.Key, out var currentValue) ||
                        string.IsNullOrWhiteSpace(currentValue))
                    {
                        host.Variables[kvp.Key] = kvp.Value;
                    }
                }

                // Apply SSH config settings if enabled (grid values take precedence)
                if (sshConfigEnabled)
                {
                    var sshConfig = _sshConfigService.GetHostConfig(host.IpAddress);
                    host.ApplySshConfig(sshConfig);
                }

                yield return host;
            }
        }

        private string GetCellValue(DataGridViewRow row, string columnName)
        {
            if (!dgv_variables.Columns.Contains(columnName))
                return "";
            return row.Cells[columnName].Value?.ToString() ?? "";
        }

        private void SetExecutionMode(bool executing)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SshDebugLog("UI", $"SetExecutionMode({executing}) entered");

            executePanel.SuspendLayout();
            try
            {
                var targetCursor = executing ? Cursors.WaitCursor : Cursors.Default;
                if (Cursor != targetCursor)
                    Cursor = targetCursor;

                var runButtonsEnabled = !executing;
                if (btnExecuteAll.Enabled != runButtonsEnabled)
                    btnExecuteAll.Enabled = runButtonsEnabled;
                if (btnExecuteSelected.Enabled != runButtonsEnabled)
                    btnExecuteSelected.Enabled = runButtonsEnabled;
                if (btnStopAll.Visible != executing)
                    btnStopAll.Visible = executing;
            }
            finally
            {
                executePanel.ResumeLayout(performLayout: false);
                executePanel.Invalidate();
            }

            lstOutput.Enabled = !executing;
            tsbOpenCsv.Enabled = !executing;
            tsbSaveCsv.Enabled = !executing;
            tsbSaveCsvAs.Enabled = !executing;
            tsbClearGrid.Enabled = !executing;

            if (executing)
            {
                // Reset stop button to initial state when starting execution
                _manualCancellationRequested = false;
                btnStopAll.Enabled = true;
                btnStopAll.Text = "Stop";
            }
            else
            {
                _manualCancellationRequested = false;
                btnStopAll.Enabled = true;
                btnStopAll.Text = "Stop";
                statusProgress.Visible = false;
                EndManualExecutionProgress();
            }

            SshDebugLog("UI", $"SetExecutionMode({executing}) completed", sw);
        }

        private void SshService_OutputReceived(object? sender, SshOutputEventArgs e)
        {
            AppendOutputText(e.Output);
        }

        private void SshService_CommandCompleted(object? sender, SshCommandCompletedEventArgs e)
        {
            _uiOutputThrottler.Flush();
        }

        private void SshService_ExecutionCompleted(object? sender, EventArgs e)
        {
            _uiOutputThrottler.Flush();
        }

        private void AppendOutputText(string output)
        {
            HandleOutputReceived(output);
        }

        private void HandleOutputReceived(string output)
        {
            if (string.IsNullOrEmpty(output))
                return;

            var appendedOutput = AppendToHistoryBuffer(output);
            if (string.IsNullOrEmpty(appendedOutput))
                return;

            _uiOutputThrottler.Enqueue(appendedOutput);

            if (IsDebugOutput(appendedOutput))
            {
                _uiOutputThrottler.Flush();
            }
        }

        private string AppendToHistoryBuffer(string output)
        {
            lock (_outputBufferLock)
            {
                // Trim leading newlines if output buffer is empty (first banner)
                if (_outputBuffer.Length == 0)
                {
                    output = output.TrimStart('\r', '\n');
                }

                _outputBuffer.Append(output);

                return output;
            }
        }

        private static bool IsDebugOutput(string output)
        {
            var trimmed = output.TrimStart('\r', '\n');
            return trimmed.StartsWith("[DEBUG ", StringComparison.Ordinal);
        }

        private void AppendOutputToUi(string output)
        {
            if (string.IsNullOrEmpty(output))
                return;

            if (InvokeRequired)
            {
                BeginInvoke(() => AppendOutputToUi(output));
                return;
            }

            txtOutput.AppendText(output);
            ScrollOutputToEnd();
        }

        private void SetOutputText(string text)
        {
            var sourceText = text ?? string.Empty;
            RecreateOutputTextBoxIfNeeded(sourceText.Length);

            // Suspend drawing to prevent flicker during text replacement
            NativeMethods.SendMessage(txtOutput.Handle, NativeMethods.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                _uiOutputThrottler.Clear();
                lock (_outputBufferLock)
                {
                    _outputBuffer.Clear();
                    if (!string.IsNullOrEmpty(sourceText))
                        _outputBuffer.Append(sourceText);

                    ShrinkOutputBufferCapacityIfNeeded_NoLock();
                }

                txtOutput.Text = sourceText;
                txtOutput.ClearUndo();
            }
            finally
            {
                // Resume drawing and force repaint
                NativeMethods.SendMessage(txtOutput.Handle, NativeMethods.WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                txtOutput.Invalidate();
            }
            ScrollOutputToEnd();
        }

        private void ClearOutput()
        {
            RecreateOutputTextBoxIfNeeded(0);

            // Suspend drawing to prevent flicker during clear
            NativeMethods.SendMessage(txtOutput.Handle, NativeMethods.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                _uiOutputThrottler.Clear();
                lock (_outputBufferLock)
                {
                    _outputBuffer.Clear();
                    ShrinkOutputBufferCapacityIfNeeded_NoLock();
                }
                txtOutput.Clear();
                txtOutput.ClearUndo();
            }
            finally
            {
                // Resume drawing and force repaint
                NativeMethods.SendMessage(txtOutput.Handle, NativeMethods.WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                txtOutput.Invalidate();
            }
        }

        private void ScrollOutputToEnd()
        {
            txtOutput.SelectionStart = txtOutput.TextLength;
            txtOutput.SelectionLength = 0;
            txtOutput.ScrollToCaret();
        }

        private void RecreateOutputTextBoxIfNeeded(int incomingTextLength, bool force = false)
        {
            if (!force &&
                (txtOutput.TextLength < OutputTextRecreateThresholdChars || incomingTextLength > OutputTextRecreateTargetChars))
                return;

            var oldTextBox = txtOutput;
            var parent = oldTextBox.Parent;
            if (parent == null)
                return;

            var insertIndex = parent.Controls.GetChildIndex(oldTextBox);
            var replacement = new TextBox
            {
                BackColor = oldTextBox.BackColor,
                BorderStyle = oldTextBox.BorderStyle,
                Dock = oldTextBox.Dock,
                Font = oldTextBox.Font,
                ForeColor = oldTextBox.ForeColor,
                HideSelection = oldTextBox.HideSelection,
                MaxLength = oldTextBox.MaxLength,
                Multiline = oldTextBox.Multiline,
                Name = oldTextBox.Name,
                ReadOnly = oldTextBox.ReadOnly,
                ScrollBars = oldTextBox.ScrollBars,
                ShortcutsEnabled = oldTextBox.ShortcutsEnabled,
                TabIndex = oldTextBox.TabIndex,
                WordWrap = oldTextBox.WordWrap
            };

            parent.SuspendLayout();
            try
            {
                parent.Controls.Remove(oldTextBox);
                _scrollbarThemedControls.Remove(oldTextBox);
                txtOutput = replacement;
                parent.Controls.Add(replacement);
                parent.Controls.SetChildIndex(replacement, insertIndex);

                if (_isDarkMode)
                {
                    ApplyDarkScrollbars(replacement);
                }
                else
                {
                    ApplyLightScrollbars(replacement);
                }
            }
            finally
            {
                parent.ResumeLayout();
                oldTextBox.Dispose();
            }
        }

        private void ShrinkOutputBufferCapacityIfNeeded_NoLock()
        {
            const int retainedCapacityWhenEmpty = 16_384;
            if (_outputBuffer.Length == 0 &&
                _outputBuffer.Capacity > retainedCapacityWhenEmpty)
            {
                _outputBuffer.Capacity = retainedCapacityWhenEmpty;
            }
        }

        private void SshService_ColumnUpdateRequested(object? sender, SshColumnUpdateEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(() => UpdateHostColumn(e.Host, e.ColumnName, e.Value));
            }
            else
            {
                UpdateHostColumn(e.Host, e.ColumnName, e.Value);
            }
        }

        private void SshService_EnvironmentVariableUpdateRequested(object? sender, SshEnvironmentVariableUpdateEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(() => UpdateActiveEnvironmentVariable(e.Variable, e.Value));
            }
            else
            {
                UpdateActiveEnvironmentVariable(e.Variable, e.Value);
            }
        }

        private void UpdateActiveEnvironmentVariable(string variable, string value)
        {
            try
            {
                _environmentService.UpdateActiveEnvironmentVariable(variable, value);
            }
            catch (Exception ex)
            {
                AppendOutputText($"[WARNING] Failed to persist environment variable '{variable}': {ex.Message}{Environment.NewLine}");
            }
        }

        private void UpdateHostColumn(HostConnection host, string columnName, string value)
        {
            // Find the row for this host
            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                if (row.IsNewRow) continue;

                var hostIp = row.Cells[CsvManager.HostColumnName]?.Value?.ToString();
                if (string.IsNullOrEmpty(hostIp)) continue;

                // Match by IP address (with optional port)
                var rowHost = HostConnection.Parse(hostIp);
                if (rowHost.IpAddress == host.IpAddress && rowHost.Port == host.Port)
                {
                    // Check if column exists, create if it doesn't
                    if (!dgv_variables.Columns.Contains(columnName))
                    {
                        dgv_variables.Columns.Add(columnName, columnName);
                    }

                    // Update the cell value
                    row.Cells[columnName].Value = value;
                    break;
                }
            }
        }

        private string StoreExecutionHistory(List<ExecutionResult> results, ExecutionDetails? details = null)
        {
            // Use output buffer as the source of truth - includes all debug output
            var output = GetBufferedOutputSnapshot();
            string label = BuildExecutionHistoryLabel(
                details?.PresetName ?? txtPreset.Text,
                isFolder: false,
                details?.WasCancelled == true);
            var entryId = HistoryIdGenerator.NewId();
            var hostResults = BuildHostHistoryEntries(results);
            var payload = new HistoryRunPayload
            {
                Id = entryId,
                Output = output ?? string.Empty,
                HostResults = hostResults.Count > 0 ? hostResults : null,
                Details = details == null ? null : CloneExecutionDetails(details)
            };
            var indexEntry = BuildHistoryIndexEntry(entryId, label, payload);

            Invoke(() =>
            {
                try
                {
                    _historyStorage.SaveRun(indexEntry, payload, _configService.GetCurrent().MaxHistoryEntries);
                    InsertHistoryEntryIntoList(indexEntry, payload);
                    SaveConfiguration();
                }
                catch (Exception ex)
                {
                    DialogTheme.Show(this, $"Failed to persist history run: {ex.Message}", "History Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });

            return entryId;
        }

        #endregion

        #region History Operations

        private void SaveHistoryEntry()
        {
            if (lstOutput.SelectedItem is not HistoryListItem entry)
            {
                DialogTheme.Show(this, "Please select an item from the list to save.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = entry.Label.Replace(":", "_")
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    if (!TryLoadHistoryPayload(entry.Id, out var payload))
                        return;

                    File.WriteAllText(sfd.FileName, payload.Output ?? string.Empty);
                }
                catch (Exception ex)
                {
                    DialogTheme.Show(this, $"Failed to save the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveAllHistory()
        {
            if (_outputHistory.Count == 0)
            {
                DialogTheme.Show(this, "There is no history to save.", "No History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"SSH_Helper_History_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    int missingPayloads = 0;
                    using var sw = new StreamWriter(sfd.FileName, false, new UTF8Encoding(false));
                    for (int i = 0; i < _outputHistory.Count; i++)
                    {
                        var entry = _outputHistory[i];
                        sw.WriteLine($"===== {entry.Label} =====");
                        sw.WriteLine();
                        if (!TryLoadHistoryPayload(entry.Id, out var payload, showError: false))
                        {
                            missingPayloads++;
                            sw.WriteLine("[history payload unavailable]");
                            if (i < _outputHistory.Count - 1) sw.WriteLine();
                            continue;
                        }

                        string body = (payload.Output ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
                        if (!string.IsNullOrEmpty(body)) sw.WriteLine(body);
                        if (i < _outputHistory.Count - 1) sw.WriteLine();
                    }

                    if (missingPayloads > 0)
                    {
                        DialogTheme.Show(
                            this,
                            $"{missingPayloads} history item(s) could not be loaded and were exported as placeholders.",
                            "History Export Warning",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    DialogTheme.Show(this, $"Failed to save the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteHistoryEntry()
        {
            if (lstOutput.SelectedItem is not HistoryListItem entry)
            {
                DialogTheme.Show(this, "Please select an item from the list to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (DialogTheme.Show(this, $"Are you sure you want to delete {entry.Label}?", "Delete Entry", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            _historyStorage.DeleteRun(entry.Id);
            _historyIndexEntries.Remove(entry.Id);
            ClearLoadedHistoryPayload(entry.Id);
            _suppressHistorySelectionChanged = true;
            try
            {
                _outputHistory.Remove(entry);
                lstOutput.ClearSelected();
            }
            finally
            {
                _suppressHistorySelectionChanged = false;
            }

            _currentHostResults = null;
            _selectedHistoryOutput = string.Empty;
            lstHosts.Items.Clear();
            historySplitContainer.Panel2Collapsed = true;
            ClearOutput();
        }

        private void DeleteAllHistory()
        {
            if (DialogTheme.Show(this, "Are you sure you want to delete all history?", "Delete History", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            _historyStorage.DeleteAll();
            _outputHistory.Clear();
            _historyIndexEntries.Clear();
            _currentHostResults = null;
            _selectedHistoryOutput = string.Empty;
            lstHosts.Items.Clear();
            historySplitContainer.Panel2Collapsed = true;
            ClearLoadedHistoryPayload();
            ClearOutput();
        }

        private void ViewExecutionDetails(string? hostAddressFilter = null, string? scopedOutput = null)
        {
            if (lstOutput.SelectedItem is not HistoryListItem entry)
                return;

            if (!TryLoadHistoryPayload(entry.Id, out var payload, requireDetails: true))
                return;

            var details = payload.Details;
            if (details == null)
            {
                DialogTheme.Show(this, "Execution details are not available for this history entry.",
                    "Details Not Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var dialog = new ExecutionDetailsDialog(
                details,
                scopedOutput ?? payload.Output ?? string.Empty,
                hostAddressFilter,
                _isDarkMode);
            DialogTheme.SetDialogFont(dialog, _dialogFont);
            dialog.ShowDialog(this);
            ClearLoadedHistoryPayload(entry.Id);
        }

        private void ViewExecutionDetailsForSelectedHost()
        {
            if (lstHosts.SelectedItem is not HostHistoryEntry hostEntry)
            {
                DialogTheme.Show(this, "Please select a host first.",
                    "No Host Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!EnsureHostOutputsLoadedForSelection())
            {
                DialogTheme.Show(
                    this,
                    "Host output for this selection could not be loaded.",
                    "History Load Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var scopedOutput = BuildSelectedHostOutput();
            ViewExecutionDetails(hostEntry.HostAddress, scopedOutput);
        }

        #endregion

        #region Find Support

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.S:
                    // Save preset when Ctrl+S is pressed and script editor has focus
                    if (txtCommand.Focused || txtPreset.Focused)
                    {
                        SaveCurrentPreset();
                        return true;
                    }
                    break;
                case Keys.Control | Keys.F:
                    ShowFindDialog();
                    return true;
                case Keys.Control | Keys.Z:
                    if (ShouldHandlePresetDeleteShortcut())
                    {
                        UndoLatestPresetDelete();
                        return true;
                    }
                    break;
                case Keys.F3:
                    NavigateToMatch(forward: true);
                    return true;
                case Keys.Shift | Keys.F3:
                    NavigateToMatch(forward: false);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ShowFindDialog()
        {
            string seed = txtOutput.SelectedText;
            if (string.IsNullOrWhiteSpace(seed))
                seed = _lastFindTerm ?? "";

            if (_findDialog == null || _findDialog.IsDisposed)
            {
                _findDialog = new FindDialog(this, seed, _lastFindMatchCase);
                DialogTheme.SetDialogFont(_findDialog, _dialogFont);
                _findDialog.AnchorTo(txtOutput);
            }

            _findDialog.Show();
            _findDialog.BringToFront();
        }

        internal void FindFromDialog(string term, bool matchCase, bool forward, bool highlightFirst)
        {
            _lastFindTerm = term;
            _lastFindMatchCase = matchCase;

            BuildMatchList(term, matchCase);

            if (_findMatches.Count == 0)
            {
                _currentMatchIndex = -1;
                _findDialog?.SetMatchInfo(0, 0);
                return;
            }

            if (highlightFirst)
            {
                // Find match at or after current cursor position
                int cursorPos = txtOutput.SelectionStart;
                _currentMatchIndex = _findMatches.FindIndex(m => m >= cursorPos);
                if (_currentMatchIndex == -1)
                    _currentMatchIndex = 0;
            }
            else if (forward)
            {
                _currentMatchIndex = (_currentMatchIndex + 1) % _findMatches.Count;
            }
            else
            {
                _currentMatchIndex = (_currentMatchIndex - 1 + _findMatches.Count) % _findMatches.Count;
            }

            HighlightCurrentMatch(term.Length);
            _findDialog?.SetMatchInfo(_currentMatchIndex + 1, _findMatches.Count);
        }

        internal void UpdateFindStatus(string term, bool matchCase)
        {
            _lastFindTerm = term;
            _lastFindMatchCase = matchCase;

            BuildMatchList(term, matchCase);

            if (_findMatches.Count == 0)
            {
                _currentMatchIndex = -1;
                _findDialog?.SetMatchInfo(0, 0);
            }
            else
            {
                // Find which match contains the current selection
                int cursorPos = txtOutput.SelectionStart;
                _currentMatchIndex = _findMatches.FindIndex(m => m >= cursorPos);
                if (_currentMatchIndex == -1)
                    _currentMatchIndex = 0;

                _findDialog?.SetMatchInfo(_currentMatchIndex + 1, _findMatches.Count);
            }
        }

        private void NavigateToMatch(bool forward)
        {
            if (string.IsNullOrEmpty(_lastFindTerm))
                return;

            if (_findMatches.Count == 0)
            {
                BuildMatchList(_lastFindTerm, _lastFindMatchCase);
                if (_findMatches.Count == 0)
                    return;
                _currentMatchIndex = 0;
            }
            else if (forward)
            {
                _currentMatchIndex = (_currentMatchIndex + 1) % _findMatches.Count;
            }
            else
            {
                _currentMatchIndex = (_currentMatchIndex - 1 + _findMatches.Count) % _findMatches.Count;
            }

            HighlightCurrentMatch(_lastFindTerm.Length);
            _findDialog?.SetMatchInfo(_currentMatchIndex + 1, _findMatches.Count);
        }

        private void BuildMatchList(string term, bool matchCase)
        {
            _findMatches.Clear();

            if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(txtOutput.Text))
                return;

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string text = txtOutput.Text;
            int index = 0;

            while ((index = text.IndexOf(term, index, comparison)) != -1)
            {
                _findMatches.Add(index);
                index += term.Length;
            }
        }

        private void HighlightCurrentMatch(int length)
        {
            if (_currentMatchIndex < 0 || _currentMatchIndex >= _findMatches.Count)
                return;

            try
            {
                txtOutput.SelectionStart = _findMatches[_currentMatchIndex];
                txtOutput.SelectionLength = length;
                txtOutput.ScrollToCaret();

                if (_findDialog is { IsDisposed: false, Visible: true })
                    _findDialog.Activate();
            }
            catch
            {
                // Ignore UI race conditions
            }
        }

        #endregion

        #region Configuration

        private void SaveConfiguration()
        {
            try
            {
                var currentConfig = _configService.GetCurrent();
                if (currentConfig.Environments.Count > 0)
                {
                    SaveCurrentGridToEnvironment(_activeEnvironmentName);
                }

                _configService.Update(config =>
                {
                    config.Username = tsbUsername.Text;
                    config.ActiveEnvironment = config.Environments.Count > 0
                        ? _activeEnvironmentName
                        : null;

                    // Save sort mode and manual order
                    config.PresetSortMode = _currentSortMode;
                    config.ManualPresetOrder = new List<string>(_manualPresetOrder);

                    // Ensure preset folders state is saved (includes expand/collapse state)
                    config.PresetFolders = new Dictionary<string, FolderInfo>();
                    foreach (var kvp in _presetManager.Folders)
                    {
                        config.PresetFolders[kvp.Key] = kvp.Value;
                    }

                    // DEBUG: Show what we're about to save
                    if (debugModeToolStripMenuItem.Checked)
                    {
                        var folderStates = string.Join(", ", _presetManager.Folders.Select(f => $"{f.Key}={f.Value.IsExpanded}"));
                        //DialogTheme.Show($"Saving folder states: {folderStates}", "Debug - SaveConfiguration");
                    }

                    // Save window state
                    config.WindowState.IsMaximized = WindowState == FormWindowState.Maximized;

                    if (WindowState == FormWindowState.Normal)
                    {
                        config.WindowState.Left = Left;
                        config.WindowState.Top = Top;
                        config.WindowState.Width = Width;
                        config.WindowState.Height = Height;
                    }

                    // Save splitter positions
                    config.WindowState.MainSplitterDistance = mainSplitContainer.SplitterDistance;
                    config.WindowState.TopSplitterDistance = topSplitContainer.SplitterDistance;
                    config.WindowState.CommandSplitterDistance = commandSplitContainer.SplitterDistance;
                    config.WindowState.OutputSplitterDistance = outputSplitContainer.SplitterDistance;
                    config.WindowState.HistorySplitterDistance = historySplitContainer.SplitterDistance;

                    // Save application state if enabled
                    if (config.RememberState)
                    {
                        config.SavedState = BuildApplicationState();
                    }
                    else
                    {
                        config.SavedState = null;
                    }

                });

                var config = _configService.GetCurrent();
                if (config.Credentials.UseCredentialManager)
                {
                    StoreDefaultPassword();
                    MigratePasswordsToCredentialManager();
                }
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Failed to save configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ApplicationState BuildApplicationState()
        {
            var state = new ApplicationState();

            // Save hosts data (exclude checkbox column and whitespace-only column names)
            state.HostColumns = new List<string>();
            for (int i = 0; i < dgv_variables.Columns.Count; i++)
            {
                var colName = dgv_variables.Columns[i].Name;
                if (colName == SelectColumnName || string.IsNullOrWhiteSpace(colName))
                    continue;
                state.HostColumns.Add(colName);
            }

            var useCredentialManager = _credentialProvider?.IsAvailable == true &&
                                       _configService.GetCurrent().Credentials.UseCredentialManager;

            state.Hosts = new List<Dictionary<string, string>>();
            for (int row = 0; row < dgv_variables.Rows.Count; row++)
            {
                if (dgv_variables.Rows[row].IsNewRow) continue;

                var rowData = new Dictionary<string, string>();
                var hostValue = GetCellValue(dgv_variables.Rows[row], CsvManager.HostColumnName);
                var usernameValue = GetCellValue(dgv_variables.Rows[row], "username");
                var resolvedUsername = string.IsNullOrWhiteSpace(usernameValue) ? tsbUsername.Text : usernameValue;
                var passwordValue = GetCellValue(dgv_variables.Rows[row], "password");

                if (useCredentialManager && !string.IsNullOrWhiteSpace(passwordValue) && !string.IsNullOrWhiteSpace(hostValue))
                {
                    StoreHostPassword(hostValue, resolvedUsername, passwordValue);
                }

                for (int col = 0; col < dgv_variables.Columns.Count; col++)
                {
                    var colName = dgv_variables.Columns[col].Name;
                    // Skip checkbox column and whitespace-only column names
                    if (colName == SelectColumnName || string.IsNullOrWhiteSpace(colName))
                        continue;
                    var value = dgv_variables.Rows[row].Cells[col].Value?.ToString() ?? "";

                    if (useCredentialManager && string.Equals(colName, "password", StringComparison.OrdinalIgnoreCase))
                    {
                        rowData[colName] = string.Empty;
                    }
                    else
                    {
                        rowData[colName] = value;
                    }
                }
                state.Hosts.Add(rowData);
            }

            // Save selected (checked) host indices
            state.SelectedHostIndices = new List<int>();
            for (int row = 0; row < dgv_variables.Rows.Count; row++)
            {
                if (dgv_variables.Rows[row].IsNewRow) continue;
                if (dgv_variables.Columns.Contains(SelectColumnName) &&
                    dgv_variables.Rows[row].Cells[SelectColumnName].Value is true)
                {
                    state.SelectedHostIndices.Add(row);
                }
            }

            // Save CSV path
            state.LastCsvPath = _loadedFilePath;
            state.LastCsvFingerprint = _loadedFileFingerprint?.Clone();

            // Save selected preset or folder
            state.SelectedPreset = _activePresetName;
            state.SelectedFolder = _selectedFolderName;

            // Save username (not password)
            state.Username = tsbUsername.Text;

            return state;
        }

        private void RestoreApplicationState(ApplicationState state)
        {
            if (state == null) return;

            if (dgv_variables.DataSource != null)
            {
                dgv_variables.DataSource = null;
            }

            // Restore hosts data
            dgv_variables.Rows.Clear();
            dgv_variables.Columns.Clear();

            _loadedFilePath = state.LastCsvPath;
            _loadedFileFingerprint = state.LastCsvFingerprint?.Clone();
            _loadedFileSyncStatus = string.IsNullOrWhiteSpace(_loadedFilePath)
                ? CsvFileSyncStatus.NotTracked
                : CsvFileSyncStatus.Current;

            using (BeginHostGridRestoreScope())
            {
                if (state.HostColumns != null && state.HostColumns.Count > 0)
                {
                    foreach (var colName in state.HostColumns)
                    {
                        // Skip checkbox column name and whitespace-only names (will be added by EnsureSelectColumn)
                        if (colName == SelectColumnName || string.IsNullOrWhiteSpace(colName))
                            continue;
                        dgv_variables.Columns.Add(colName, colName);
                    }
                    EnsureSelectColumn();

                    if (state.Hosts != null)
                    {
                        // Ensure row template height is set before adding rows
                        dgv_variables.RowTemplate.Height = 28;
                        var useCredentialManager = _credentialProvider?.IsAvailable == true &&
                                                   _configService.GetCurrent().Credentials.UseCredentialManager;

                        foreach (var rowData in state.Hosts)
                        {
                            if (useCredentialManager)
                            {
                                rowData.TryGetValue(CsvManager.HostColumnName, out var hostValue);
                                rowData.TryGetValue("username", out var usernameValue);
                                rowData.TryGetValue("password", out var passwordValue);

                                var resolvedUsername = string.IsNullOrWhiteSpace(usernameValue) ? tsbUsername.Text : usernameValue;
                                if (!string.IsNullOrWhiteSpace(passwordValue) && !string.IsNullOrWhiteSpace(hostValue))
                                {
                                    StoreHostPassword(hostValue, resolvedUsername, passwordValue);
                                    rowData["password"] = string.Empty;
                                }
                            }

                            var rowIndex = dgv_variables.Rows.Add();
                            dgv_variables.Rows[rowIndex].Height = 28;
                            foreach (var kvp in rowData)
                            {
                                if (dgv_variables.Columns.Contains(kvp.Key))
                                {
                                    dgv_variables.Rows[rowIndex].Cells[kvp.Key].Value = kvp.Value;
                                }
                            }
                        }
                    }

                    // Restore selected (checked) host indices
                    if (state.SelectedHostIndices != null && dgv_variables.Columns.Contains(SelectColumnName))
                    {
                        foreach (var index in state.SelectedHostIndices)
                        {
                            if (index >= 0 && index < dgv_variables.Rows.Count && !dgv_variables.Rows[index].IsNewRow)
                            {
                                dgv_variables.Rows[index].Cells[SelectColumnName].Value = true;
                            }
                        }
                    }
                }

                RequestHostGridHostCountRefresh();
                RequestHostGridScrollbarRefresh();
            }

            // Restore username (not password)
            if (!string.IsNullOrEmpty(state.Username))
            {
                tsbUsername.Text = state.Username;
                txtUsername.Text = state.Username;
            }

            // Restore selected preset or folder (do this last so it loads properly)
            if (!string.IsNullOrEmpty(state.SelectedPreset))
            {
                SelectPresetByName(state.SelectedPreset);
            }
            else if (!string.IsNullOrEmpty(state.SelectedFolder))
            {
                SelectFolderByName(state.SelectedFolder);
            }

            // Reset dirty flag since we're restoring saved state, not making new changes
            _csvDirty = false;
            CaptureLoadedFileSnapshotFromGrid();
            UpdateHostsFileIndicator();

            // Flag for auto-sizing after the form is fully visible (handled in Form1_Shown)
            _pendingColumnAutoSize = true;
        }

        private bool ConfirmExitWorkflow()
        {
            if (_sshService.IsRunning)
            {
                if (DialogTheme.Show(this, "Execution is currently running. Stop and exit?", "Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                    return false;
                StopExecution();
            }

            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            if (!TryResolvePendingCsvChangesForExit())
            {
                return false;
            }

            if (IsPresetDirty())
            {
                if (!TryResolvePendingPresetChanges())
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryResolvePendingCsvChangesForExit()
        {
            if (!_csvDirty)
                return true;

            var result = DialogTheme.Show(
                this,
                "You have unsaved CSV changes. Save before exiting?",
                "Save Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
                return false;

            if (result != DialogResult.Yes)
                return true;

            var saveResult = SaveCurrentCsvInternal(promptIfNoPath: true);
            return saveResult switch
            {
                CsvSaveAttemptResult.Saved => true,
                CsvSaveAttemptResult.Cancelled => ConfirmExitWithoutSavingCsv("CSV save was canceled. Exit without saving your CSV changes?"),
                CsvSaveAttemptResult.Failed => ConfirmExitWithoutSavingCsv("CSV save failed. Exit without saving your CSV changes?"),
                _ => false
            };
        }

        private bool ConfirmExitWithoutSavingCsv(string message)
        {
            return DialogTheme.Show(
                this,
                message,
                "Exit Without Saving",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        #endregion

        #region Update Check

        /// <summary>
        /// Checks for application updates.
        /// </summary>
        /// <param name="silent">If true, only shows dialog when update is available. If false, shows result even when up-to-date.</param>
        private async Task CheckForUpdatesAsync(bool silent)
        {
            if (_updateService == null) return;

            var config = _configService.GetCurrent();

            // Update status bar
            if (!silent)
            {
                UpdateStatusBar("Checking for updates...");
                checkForUpdatesToolStripMenuItem.Enabled = false;
            }

            try
            {
                var result = await _updateService.CheckForUpdatesAsync();

                // Update last check time
                _configService.Update(c => c.UpdateSettings.LastCheckTime = DateTime.UtcNow);

                if (result.ErrorMessage != null)
                {
                    if (!silent)
                    {
                        using var errorDialog = new UpdateErrorDialog(result.ErrorMessage, _isDarkMode);
                        errorDialog.ShowDialog(this);
                    }
                    UpdateStatusBar("Update check failed");
                    return;
                }

                if (result.UpdateAvailable)
                {
                    // Check if user has skipped this version
                    if (silent && config.UpdateSettings.SkippedVersion == result.LatestVersion)
                    {
                        UpdateStatusBar("Ready");
                        return;
                    }

                    using var updateDialog = new UpdateDialog(result, _updateService, skippedVersion =>
                    {
                        _configService.Update(c => c.UpdateSettings.SkippedVersion = skippedVersion);
                    }, config.UpdateSettings.EnableUpdateLog, _isDarkMode, ConfirmExitWorkflow);
                    DialogTheme.SetDialogFont(updateDialog, _dialogFont);
                    updateDialog.ShowDialog(this);
                }
                else
                {
                    if (!silent)
                    {
                        using var noUpdateDialog = new NoUpdateDialog(ApplicationVersion, _isDarkMode);
                        noUpdateDialog.ShowDialog(this);
                    }
                }

                UpdateStatusBar("Ready");
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    using var errorDialog = new UpdateErrorDialog(ex.Message, _isDarkMode);
                    errorDialog.ShowDialog(this);
                }
                UpdateStatusBar("Update check failed");
            }
            finally
            {
                if (!silent)
                {
                    checkForUpdatesToolStripMenuItem.Enabled = true;
                }
            }
        }

        private async void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await CheckForUpdatesAsync(silent: false);
        }

        #endregion

        #region Scheduler

        /// <summary>
        /// Creates and wires all scheduler services with the correct dependency chain.
        /// </summary>
        private void InitializeSchedulerServices()
        {
            if (_credentialProvider == null)
                return;

            _schedulingService = new SchedulingService();
            _jobStorage = new JobStorageService(_credentialProvider);
            _jobStorage.Load();
            _jobExportService = new JobExportService();
            _jobExecutionService = new JobExecutionService(
                _jobStorage, _schedulingService, _configService, _presetManager, _credentialProvider);
            _jobHistoryService = new JobHistoryService();
            _jobHistoryService.SubscribeTo(_jobExecutionService, ResolveSchedulerHistoryRetention);

            // Subscribe to execution events for output panel notifications
            _jobExecutionService.JobCompleted += OnSchedulerJobCompleted;
            _jobExecutionService.JobStateChanged += OnSchedulerJobStateChanged;

            // Refresh status bar when jobs change
            _jobStorage.JobsChanged += (s, e) =>
            {
                if (InvokeRequired) { BeginInvoke(() => UpdateSchedulerStatusBar()); return; }
                UpdateSchedulerStatusBar();
            };

            // Set up reference integrity with preset manager
            _presetManager.SetJobStorageService(_jobStorage);

            // Register cleanup on form close
            FormClosed += (_, __) => CleanupSchedulerServices();

            // Crash recovery and start timer
            _jobExecutionService.Initialize();
            RecordMissedSchedulerRunsOnStartup();
            _jobExecutionService.Start();
        }

        /// <summary>
        /// Sets up the scheduler status bar label and refresh timer.
        /// </summary>
        private void InitializeSchedulerStatusBar()
        {
            // Add scheduler status label to status strip
            var statusScheduler = new ToolStripStatusLabel
            {
                Name = "_statusScheduler",
                Spring = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Scheduler: 0 active",
                Visible = false,
                IsLink = true,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            statusScheduler.Click += (s, e) => ShowJobListDialog();
            statusStrip.Items.Add(statusScheduler);
            _statusScheduler = statusScheduler;
            ApplySchedulerStatusBarTheme();

            // Add Scheduler menu item to menu bar (before Help)
            var menuScheduler = new ToolStripMenuItem
            {
                Name = "_menuScheduler",
                Text = "&Scheduler",
                Size = new Size(69, 20)
            };
            menuScheduler.Click += (s, e) => ShowJobListDialog();
            var helpIndex = menuStrip1.Items.IndexOf(helpToolStripMenuItem);
            if (helpIndex >= 0)
                menuStrip1.Items.Insert(helpIndex, menuScheduler);
            else
                menuStrip1.Items.Add(menuScheduler);

            // Start status bar refresh timer
            _statusBarTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _statusBarTimer.Tick += (s, e) => UpdateSchedulerStatusBar();
            _statusBarTimer.Start();
            UpdateSchedulerStatusBar(); // initial update
        }

        private void ApplySchedulerStatusBarTheme()
        {
            if (_statusScheduler == null)
            {
                return;
            }

            _statusScheduler.LinkColor = _isDarkMode ? SchedulerStatusLinkDark : SchedulerStatusLinkLight;
            _statusScheduler.ActiveLinkColor = _isDarkMode ? SchedulerStatusLinkDarkActive : SchedulerStatusLinkLightActive;
            _statusScheduler.VisitedLinkColor = _statusScheduler.LinkColor;
        }

        /// <summary>
        /// Shows the job list dialog (modeless, single-instance pattern).
        /// </summary>
        private void ShowJobListDialog()
        {
            if (_jobStorage == null || _jobExecutionService == null ||
                _schedulingService == null || _jobHistoryService == null || _jobExportService == null)
                return;

            var config = _configService.GetCurrent();
            _jobListDialogManager.ShowOrActivate(
                () => new JobListDialog(
                    _jobStorage,
                    _jobExecutionService,
                    _jobHistoryService,
                    _schedulingService,
                    _presetManager,
                    _jobExportService,
                    _credentialProvider,
                    RunTrackedJobNowAsync,
                    getMainGridRows: () =>
                    {
                        return HostGridUtilities.BuildSchedulerCopySnapshot(dgv_variables).Rows
                            .ToList();
                    },
                    getMainGridColumns: () =>
                    {
                        return HostGridUtilities.BuildSchedulerCopySnapshot(dgv_variables).Columns
                            .ToList();
                    },
                    darkMode: _isDarkMode,
                    fontFamily: config.FontSettings.UIFontFamily,
                    fontSize: config.FontSettings.MenuFontSize),
                this);
        }

        /// <summary>
        /// Updates the scheduler status bar label with active job count and next-run countdown.
        /// </summary>
        private void UpdateSchedulerStatusBar()
        {
            if (_statusScheduler == null || _jobStorage == null || _schedulingService == null)
                return;

            var activeCount = _jobStorage.Jobs.Values.Count(j => j.IsEnabled);
            var showStatusBar = SchedulerNotificationFormatter.ShouldShowStatusBar(activeCount);
            _statusScheduler.Visible = showStatusBar;

            if (!showStatusBar)
            {
                return;
            }

            // Find next scheduled run across all enabled recurring jobs
            string? nextJobName = null;
            TimeSpan? timeUntilNext = null;

            foreach (var job in _jobStorage.Jobs.Values)
            {
                if (!job.IsEnabled || job.ScheduleType != ScheduleType.Recurring ||
                    string.IsNullOrEmpty(job.CronExpression))
                    continue;

                var nextRun = _schedulingService.GetNextRunLocal(job.CronExpression);
                if (nextRun == null) continue;

                var remaining = nextRun.Value - DateTime.Now;
                if (remaining <= TimeSpan.Zero) continue;

                if (timeUntilNext == null || remaining < timeUntilNext.Value)
                {
                    timeUntilNext = remaining;
                    nextJobName = job.Name;
                }
            }

            _statusScheduler.Text = SchedulerNotificationFormatter.FormatStatusBar(
                activeCount, nextJobName, timeUntilNext);
        }

        /// <summary>
        /// Handles job completion events and refreshes scheduler UI state.
        /// </summary>
        private void OnSchedulerJobCompleted(object? sender, JobRunResult result)
        {
            if (InvokeRequired) { BeginInvoke(() => OnSchedulerJobCompleted(sender, result)); return; }

            _runNowJobIds.Remove(result.JobId);
            UpdateSchedulerStatusBar();
        }

        private void RecordMissedSchedulerRunsOnStartup()
        {
            if (_jobStorage == null || _schedulingService == null || _jobHistoryService == null)
            {
                return;
            }

            var lastShutdownUtc = _configService.GetCurrent().LastAppShutdownUtc;
            if (!lastShutdownUtc.HasValue)
            {
                return;
            }

            var skippedSummaries = _schedulingService
                .DetectMissedRunSummaries(_jobStorage.Jobs, lastShutdownUtc.Value)
                .OrderBy(entry => entry.LastScheduledTimeUtc)
                .ToList();

            foreach (var skippedSummary in skippedSummaries)
            {
                var job = _jobStorage.Get(skippedSummary.JobId);
                _jobHistoryService.SaveSkippedRunSummary(
                    skippedSummary,
                    ResolveSchedulerHistoryRetention(job));
            }
        }

        private JobHistoryRetentionOptions ResolveSchedulerHistoryRetention(JobRunResult result)
        {
            var job = _jobStorage?.Get(result.JobId);
            return ResolveSchedulerHistoryRetention(job);
        }

        private JobHistoryRetentionOptions ResolveSchedulerHistoryRetention(JobDefinition? job)
        {
            return SchedulerHistoryPolicyResolver.Resolve(_configService.GetCurrent(), job);
        }

        /// <summary>
        /// Handles job state change events and refreshes scheduler UI state.
        /// </summary>
        private void OnSchedulerJobStateChanged(object? sender, JobExecutionService.JobStateChangedEventArgs e)
        {
            if (InvokeRequired) { BeginInvoke(() => OnSchedulerJobStateChanged(sender, e)); return; }

            UpdateSchedulerStatusBar();
        }

        /// <summary>
        /// Registers a job ID as a "Run Now" trigger so notifications use the correct prefix.
        /// </summary>
        internal void TrackRunNow(string jobId) => _runNowJobIds.Add(jobId);

        internal async Task<bool> RunTrackedJobNowAsync(string jobId)
        {
            if (_jobExecutionService == null)
            {
                return false;
            }

            TrackRunNow(jobId);
            var result = await _jobExecutionService.RunNowAsync(jobId);
            if (!result)
            {
                _runNowJobIds.Remove(jobId);
            }

            return result;
        }

        /// <summary>
        /// Stops and disposes scheduler services, unsubscribes event handlers.
        /// Called from Dispose override in Form1.Designer.cs.
        /// </summary>
        private void CleanupSchedulerServices()
        {
            _statusBarTimer?.Stop();
            _statusBarTimer?.Dispose();
            if (_jobExecutionService != null)
            {
                _jobExecutionService.JobCompleted -= OnSchedulerJobCompleted;
                _jobExecutionService.JobStateChanged -= OnSchedulerJobStateChanged;
                _jobExecutionService.Stop();
                _jobExecutionService.Dispose();
            }
        }

        #endregion

        private void tsbPassword_Click(object sender, EventArgs e)
        {

        }
    }
}




