using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
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

        // Window message constants
        public const int WM_THEMECHANGED = 0x031A;

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

    public partial class Form1 : Form
    {
        #region Constants

        private const string ApplicationVersion = "0.50.14";
        private const string ApplicationName = "SSH Helper";
        private const string SelectColumnName = "";
        private const string FolderIcon = "\U0001F4C1";
        private const string StarIcon = "\u2605";
        private static readonly string FolderSummarySeparator = new string('-', 60);
        private static readonly string FolderSummarySubSeparator = new string('-', 9);

        #endregion

        #region Services

        private readonly ConfigurationService _configService;
        private readonly PresetManager _presetManager;
        private readonly CsvManager _csvManager;
        private readonly SshExecutionService _sshService;
        private readonly ExecutionCoordinator _executionCoordinator;
        private readonly UpdateService _updateService;
        private readonly SshConfigService _sshConfigService;

        #endregion

        #region State

        private string? _loadedFilePath;
        private string? _activePresetName;
        private bool _csvDirty;
        private bool _exitConfirmed;
        private bool _suppressPresetSelectionChange;
        private bool _suppressExpandCollapseEvents;
        private bool _pendingColumnAutoSize;
        private int _rightClickedColumnIndex = -1;
        private int _rightClickedRowIndex = -1;
        private readonly BindingList<HistoryListItem> _outputHistory = new();

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
        private TreeNode? _lastHighlightedNode;

        // Track selected folder for Run button (TreeView selection can be unreliable on button click)
        private string? _selectedFolderName;

        // Per-host history data for the currently selected folder history entry
        private List<HostHistoryEntry>? _currentHostResults;

        // Output state
        private readonly StringBuilder _outputBuffer = new();

        // Credential provider
        private ICredentialProvider? _credentialProvider;

        // Track which TreeView triggered the context menu
        private TreeView? _contextMenuSourceTreeView;

        // SSH startup debug mode - logs timing from button click through SSH connection
        private bool _sshDebugMode;

        // Custom scrollbars for DataGridView (to support dark mode theming)
        private VScrollBar? _dgvVScrollBar;
        private HScrollBar? _dgvHScrollBar;
        private Panel? _dgvScrollCorner;

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
            Text = $"{ApplicationName} {ApplicationVersion}";

            // Initialize services
            _configService = new ConfigurationService();
            _presetManager = new PresetManager(_configService);
            _csvManager = new CsvManager();
            var config = _configService.Load();
            var poolTimeouts = SshTimeoutOptions.Create(config.Timeout, config.ConnectionTimeout);
            _sshService = new SshExecutionService(enablePooling: true, poolTimeouts);
            _sshService.UseConnectionPooling = config.UseConnectionPooling;
            _sshService.PreferSshAgent = config.Credentials.PreferSshAgent;
            _sshConfigService = new SshConfigService();
            _executionCoordinator = new ExecutionCoordinator(_sshService, _configService);

            // Wire up SSH service events
            _sshService.OutputReceived += SshService_OutputReceived;
            _sshService.ColumnUpdateRequested += SshService_ColumnUpdateRequested;

            // Initialize update service
            _updateService = new UpdateService(
                config.UpdateSettings.GitHubOwner,
                config.UpdateSettings.GitHubRepo,
                ApplicationVersion);

            InitializeFromConfiguration();
            InitializeCredentials();
            InitializeDataGridView();
            InitializeOutputHistory();
            InitializeEventHandlers();
            InitializeToolbarSync();
            InitializePasswordMasking();
            EnableDoubleBuffering();
            RestoreWindowState();
            UpdateHostCount();
            UpdateSortModeIndicator();
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

            // Restore folder expand/collapse state after form is fully shown
            RestoreFolderExpandState();

            // Auto-size columns to content if state was restored (must happen after form is visible)
            if (_pendingColumnAutoSize)
            {
                _pendingColumnAutoSize = false;
                AutoSizeColumnsToContent();
            }

            var config = _configService.GetCurrent();
            if (config.UpdateSettings.CheckOnStartup)
            {
                await CheckForUpdatesAsync(silent: true);
            }
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

        #endregion

        #region Initialization

        private void InitializeFromConfiguration()
        {
            var config = _configService.Load();
            _presetManager.Load();

            // Populate UI from config
            tsbUsername.Text = config.Username;
            txtUsername.Text = config.Username;

            // Load sort mode and manual order
            _currentSortMode = config.PresetSortMode;
            _manualPresetOrder.Clear();
            _manualPresetOrder.AddRange(config.ManualPresetOrder);

            // Populate preset list with proper sorting
            // Don't restore expand state here - Form1_Shown will do it after the form is visible
            RefreshPresetList(restoreExpandState: false);

            // Apply defaults to presets that don't have them
            _presetManager.ApplyDefaults(config.Timeout);
        }

        private void InitializeDataGridView()
        {
            // Add checkbox column for multi-host selection (first column)
            var selectColumn = new DataGridViewCheckBoxColumn
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
            dgv_variables.Columns.Add(selectColumn);

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
            dgv_variables.RowsAdded += (s, e) => UpdateDataGridViewScrollbars();
            dgv_variables.RowsRemoved += (s, e) => UpdateDataGridViewScrollbars();
            dgv_variables.ColumnAdded += (s, e) => UpdateDataGridViewScrollbars();
            dgv_variables.ColumnRemoved += (s, e) => UpdateDataGridViewScrollbars();
            dgv_variables.ColumnWidthChanged += (s, e) => UpdateDataGridViewScrollbars();
            dgv_variables.Resize += (s, e) => UpdateDataGridViewScrollbars();
            dgv_variables.Scroll += DgvVariables_Scroll;
            dgv_variables.MouseWheel += DgvVariables_MouseWheel;
            hostsPanel.Resize += (s, e) => UpdateDataGridViewScrollbars();

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

        private void InitializeOutputHistory()
        {
            lstOutput.DataSource = _outputHistory;
            lstOutput.DisplayMember = nameof(HistoryListItem.Label);
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

        private void TryLoadDefaultPassword()
        {
            if (_credentialProvider == null || !_credentialProvider.IsAvailable)
                return;

            if (_credentialProvider.TryGetPassword(CredentialTargets.DefaultPasswordTarget, out _, out var password))
            {
                tsbPassword.Text = password;
                txtPassword.Text = password;
            }
        }

        private void StoreDefaultPassword()
        {
            if (_credentialProvider == null || !_credentialProvider.IsAvailable)
                return;

            _credentialProvider.SavePassword(CredentialTargets.DefaultPasswordTarget, tsbUsername.Text, tsbPassword.Text);
        }

        private bool TryResolveHostPassword(string hostKey, string username, out string password)
        {
            password = string.Empty;
            if (_credentialProvider == null || !_credentialProvider.IsAvailable)
                return false;

            var target = CredentialTargets.HostPasswordTarget(hostKey, username);
            return _credentialProvider.TryGetPassword(target, out _, out password);
        }

        private void StoreHostPassword(string hostKey, string username, string password)
        {
            if (_credentialProvider == null || !_credentialProvider.IsAvailable)
                return;

            var target = CredentialTargets.HostPasswordTarget(hostKey, username);
            _credentialProvider.SavePassword(target, username, password);
        }

        private void MigratePasswordsToCredentialManager()
        {
            if (_credentialProvider == null || !_credentialProvider.IsAvailable)
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

            // History and host list right-click selection and custom drawing
            lstOutput.MouseDown += LstOutput_MouseDown;
            lstOutput.DrawItem += LstOutput_DrawItem;
            lstHosts.MouseDown += LstHosts_MouseDown;

            // Script editor cursor position tracking
            txtCommand.Click += TxtCommand_CursorPositionChanged;
            txtCommand.KeyUp += TxtCommand_CursorPositionChanged;
            txtCommand.MouseUp += TxtCommand_CursorPositionChanged;
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

        private void RestoreWindowState()
        {
            var config = _configService.GetCurrent();
            var ws = config.WindowState;

            if (ws.Width.HasValue && ws.Height.HasValue && ws.Left.HasValue && ws.Top.HasValue)
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

            if (ws.IsMaximized)
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

                // Restore application state if enabled
                if (config.RememberState && config.SavedState != null)
                {
                    RestoreApplicationState(config.SavedState);
                }
            };
        }

        #endregion

        #region UI Helpers

        private void UpdateHostCount()
        {
            int count = dgv_variables.Rows.Cast<DataGridViewRow>()
                .Count(r => !r.IsNewRow && !string.IsNullOrWhiteSpace(GetCellValue(r, CsvManager.HostColumnName)));

            string text = count == 1 ? "1 host" : $"{count} hosts";
            lblHostCount.Text = text;
            statusHostCount.Text = text;
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

            var selectColumn = new DataGridViewCheckBoxColumn
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
            dgv_variables.Columns.Insert(0, selectColumn);
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

        /// <summary>
        /// Logs a timestamped debug message to the output window when SSH Debug mode is enabled.
        /// </summary>
        private void SshDebugLog(string phase, string message, System.Diagnostics.Stopwatch? stopwatch = null)
        {
            if (!_sshDebugMode) return;

            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var elapsed = stopwatch != null ? $" (+{stopwatch.ElapsedMilliseconds}ms)" : "";
            var debugLine = $"[SSH DEBUG {timestamp}]{elapsed} {phase}: {message}\r\n";
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
        private static readonly Color LightAccent = Color.FromArgb(13, 110, 253);
        private static readonly Color LightSelectionBorder = Color.FromArgb(10, 88, 202);  // Darker accent for border

        // Dark theme colors (VS Code inspired - professional and easy on the eyes)
        private static readonly Color DarkSurface0 = Color.FromArgb(24, 24, 24);      // Deepest background
        private static readonly Color DarkSurface1 = Color.FromArgb(30, 30, 30);      // Main panel background
        private static readonly Color DarkSurface2 = Color.FromArgb(37, 37, 38);      // Elevated surfaces
        private static readonly Color DarkSurface3 = Color.FromArgb(45, 45, 46);      // Headers, toolbars
        private static readonly Color DarkTextPrimary = Color.FromArgb(204, 204, 204);    // Primary text
        private static readonly Color DarkTextSecondary = Color.FromArgb(128, 128, 128);  // Secondary/muted text
        private static readonly Color DarkBorder = Color.FromArgb(48, 48, 48);            // Subtle borders
        private static readonly Color DarkSelectionBg = Color.FromArgb(4, 57, 94);        // Subtle selection (VS Code style)
        private static readonly Color DarkSelectionBorder = Color.FromArgb(0, 122, 204); // Selection border accent
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

            // Set up owner-draw for history listbox
            lstOutput.DrawMode = DrawMode.OwnerDrawFixed;
            lstOutput.ItemHeight = 22;

            // Set up owner-draw for TreeViews only in dark mode
            if (darkMode)
            {
                trvPresets.DrawMode = TreeViewDrawMode.OwnerDrawAll;
                trvPresets.DrawNode -= TreeView_DrawNode;
                trvPresets.DrawNode += TreeView_DrawNode;

                trvFavorites.DrawMode = TreeViewDrawMode.OwnerDrawAll;
                trvFavorites.DrawNode -= TreeView_DrawNode;
                trvFavorites.DrawNode += TreeView_DrawNode;
            }
            else
            {
                // Light mode: also use owner draw for consistent selection visibility
                trvPresets.DrawMode = TreeViewDrawMode.OwnerDrawAll;
                trvPresets.DrawNode -= TreeView_DrawNode;
                trvPresets.DrawNode += TreeView_DrawNode;

                trvFavorites.DrawMode = TreeViewDrawMode.OwnerDrawAll;
                trvFavorites.DrawNode -= TreeView_DrawNode;
                trvFavorites.DrawNode += TreeView_DrawNode;
            }

            // Update custom DataGridView scrollbar colors
            ApplyScrollbarColors();

            ResumeLayout(true);
            Refresh();
        }

        private void ApplyFontSettings(Models.FontSettings fontSettings)
        {
            SuspendLayout();

            var uiFont = fontSettings.UIFontFamily;
            var codeFont = fontSettings.CodeFontFamily;
            var scale = fontSettings.GlobalScaleFactor;

            // Helper to apply scaling
            float Scaled(float size) => size * scale;

            // Section titles (Semibold)
            lblHostsTitle.Font = new Font(uiFont + " Semibold", Scaled(fontSettings.SectionTitleFontSize), FontStyle.Bold);
            lblPresetsTitle.Font = new Font(uiFont + " Semibold", Scaled(fontSettings.SectionTitleFontSize), FontStyle.Bold);
            lblScriptTitle.Font = new Font(uiFont + " Semibold", Scaled(fontSettings.SectionTitleFontSize), FontStyle.Bold);
            lblHistoryTitle.Font = new Font(uiFont + " Semibold", Scaled(fontSettings.SectionTitleFontSize), FontStyle.Bold);
            lblHostsListTitle.Font = new Font(uiFont + " Semibold", Scaled(fontSettings.SectionTitleFontSize), FontStyle.Bold);

            // Tree views
            trvPresets.Font = new Font(uiFont, Scaled(fontSettings.TreeViewFontSize));
            trvFavorites.Font = new Font(uiFont, Scaled(fontSettings.TreeViewFontSize));

            // Apply custom row height for tree views if specified (0 = auto based on font)
            if (fontSettings.TreeViewRowHeight > 0)
            {
                trvPresets.ItemHeight = fontSettings.TreeViewRowHeight;
                trvFavorites.ItemHeight = fontSettings.TreeViewRowHeight;
            }
            else
            {
                // Calculate height based on font (font height + padding)
                var autoHeight = trvPresets.Font.Height + 4;
                trvPresets.ItemHeight = autoHeight;
                trvFavorites.ItemHeight = autoHeight;
            }

            // Empty labels
            lblFavoritesEmpty.Font = new Font(uiFont, Scaled(fontSettings.EmptyLabelFontSize));

            // Execute buttons (Semibold)
            btnExecuteAll.Font = new Font(uiFont + " Semibold", Scaled(fontSettings.ExecuteButtonFontSize), FontStyle.Bold);
            btnExecuteSelected.Font = new Font(uiFont + " Semibold", Scaled(fontSettings.ExecuteButtonFontSize), FontStyle.Bold);
            btnStopAll.Font = new Font(uiFont + " Semibold", Scaled(fontSettings.ExecuteButtonFontSize), FontStyle.Bold);

            // General buttons
            btnSavePreset.Font = new Font(uiFont, Scaled(fontSettings.ButtonFontSize));

            // Code editor
            txtCommand.Font = new Font(codeFont, Scaled(fontSettings.CodeEditorFontSize));
            txtCommand.WordWrap = fontSettings.CodeEditorWordWrap;

            // Output area
            txtOutput.Font = new Font(codeFont, Scaled(fontSettings.OutputAreaFontSize));
            txtOutput.WordWrap = fontSettings.OutputAreaWordWrap;


            // Tab controls
            var tabFont = new Font(uiFont, Scaled(fontSettings.TabFontSize));
            presetsTabControl.Font = tabFont;

            // Host list (DataGridView) - apply row height setting
            // Don't change font on DataGridView as it interferes with existing styling
            var hostRowHeight = fontSettings.HostListRowHeight > 0 ? fontSettings.HostListRowHeight : 28;
            dgv_variables.RowTemplate.Height = hostRowHeight;
            foreach (DataGridViewRow row in dgv_variables.Rows)
            {
                row.Height = hostRowHeight;
            }

            // History list boxes
            lstOutput.Font = new Font(uiFont, Scaled(fontSettings.HostListFontSize));
            lstHosts.Font = new Font(uiFont, Scaled(fontSettings.HostListFontSize));

            // Menu strip
            menuStrip1.Font = new Font(uiFont, Scaled(fontSettings.MenuFontSize));
            ApplyMenuFontRecursive(menuStrip1.Items, new Font(uiFont, Scaled(fontSettings.MenuFontSize)));

            // Context menus
            ApplyContextMenuFont(contextMenuStrip1, uiFont, Scaled(fontSettings.MenuFontSize));
            ApplyContextMenuFont(contextPresetLst, uiFont, Scaled(fontSettings.MenuFontSize));
            ApplyContextMenuFont(contextPresetLstAdd, uiFont, Scaled(fontSettings.MenuFontSize));
            ApplyContextMenuFont(contextHistoryLst, uiFont, Scaled(fontSettings.MenuFontSize));
            ApplyContextMenuFont(contextHostLst, uiFont, Scaled(fontSettings.MenuFontSize));

            // Toolstrips
            mainToolStrip.Font = new Font(uiFont, Scaled(fontSettings.ButtonFontSize));
            presetsToolStrip.Font = new Font(uiFont, Scaled(fontSettings.ButtonFontSize));

            // Status bar
            statusStrip.Font = new Font(uiFont, Scaled(fontSettings.StatusBarFontSize));

            // Apply accent color if custom
            ApplyAccentColor(fontSettings.CustomAccentColor);

            ResumeLayout(true);
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
            presetsHeaderPanel.BackColor = LightBackground;
            presetsToolStrip.BackColor = LightBackground;
            lblPresetsTitle.ForeColor = LightTextColor;
            presetsTabControl.BackColor = LightBackground;
            tabPresets.BackColor = LightPanelBackground;
            tabFavorites.BackColor = LightPanelBackground;
            trvPresets.BackColor = LightPanelBackground;
            trvPresets.ForeColor = LightTextColor;
            trvFavorites.BackColor = LightPanelBackground;
            trvFavorites.ForeColor = LightTextColor;
            lblFavoritesEmpty.ForeColor = LightSecondaryText;

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
            presetsHeaderPanel.BackColor = DarkSurface2;
            presetsToolStrip.BackColor = DarkSurface2;
            lblPresetsTitle.ForeColor = DarkTextPrimary;
            presetsTabControl.BackColor = DarkSurface1;
            tabPresets.BackColor = DarkSurface1;
            tabFavorites.BackColor = DarkSurface1;
            trvPresets.BackColor = DarkSurface1;
            trvPresets.ForeColor = DarkTextPrimary;
            trvFavorites.BackColor = DarkSurface1;
            trvFavorites.ForeColor = DarkTextPrimary;
            lblFavoritesEmpty.ForeColor = DarkTextSecondary;

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
                else if (item is ToolStripLabel label)
                {
                    label.ForeColor = darkMode ? DarkTextSecondary : LightSecondaryText;
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

            // Handle painting to cover any remaining artifacts
            tabControl.Paint -= TabControl_Paint;
            tabControl.Paint += TabControl_Paint;

            // Use the custom BorderlessTabControl properties if available
            if (tabControl is BorderlessTabControl borderlessTab)
            {
                borderlessTab.HideBorder = true;
                borderlessTab.BorderBackgroundColor = DarkSurface1;
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
                using var topPen = new Pen(Color.Red, 2);
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
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(tabPage.Text, tabControl.Font, textBrush, tabRect, sf);
            }
        }

        #endregion

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
            }
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

            var rowIdx = (e.RowIndex + 1).ToString();
            var centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
            using var brush = new SolidBrush(Color.FromArgb(108, 117, 125));
            e.Graphics.DrawString(rowIdx, grid.Font, brush, headerBounds, centerFormat);
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
            if (dgv_variables.Columns.Contains("Host_IP") && dgv_variables.SelectedCells.Count > 0)
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
                    dgv_variables.Rows[rowIndex].Cells["Host_IP"].Selected = true;
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
            UpdateHostCount();
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

            _csvDirty = true;
            UpdateHostCount();
        }

        private void Dgv_Variables_RowsRemoved(object? sender, DataGridViewRowsRemovedEventArgs e)
        {
            _csvDirty = true;
            UpdateHostCount();
        }

        private void Dgv_Variables_ColumnRemoved(object? sender, DataGridViewColumnEventArgs e) => _csvDirty = true;

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

            var tag = e.Node.Tag as PresetNodeTag;
            if (tag == null)
                return;

            // If a folder is selected, clear the editor to avoid confusion
            if (tag.IsFolder)
            {
                // Check for unsaved changes first
                if (!string.IsNullOrEmpty(_activePresetName) && IsPresetDirty())
                {
                    var result = MessageBox.Show(
                        $"Save changes to preset '{_activePresetName}'?",
                        "Unsaved Preset",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Cancel)
                    {
                        _suppressPresetSelectionChange = true;
                        SelectPresetByName(_activePresetName);
                        _suppressPresetSelectionChange = false;
                        return;
                    }

                    if (result == DialogResult.Yes)
                    {
                        SaveCurrentPreset();
                    }
                }

                // Display folder summary and track selected folder
                _activePresetName = null;
                _selectedFolderName = tag.Name;
                txtPreset.Text = $"{FolderIcon} {tag.Name}";
                txtTimeoutHeader.Clear();
                DisplayFolderSummary(tag.Name);
                UpdateRunButtonText();
                return;
            }

            // Clear folder selection when a preset is selected
            _selectedFolderName = null;
            UpdateRunButtonText();

            string newPresetName = tag.Name;

            if (!string.IsNullOrEmpty(_activePresetName) &&
                !string.Equals(newPresetName, _activePresetName, StringComparison.Ordinal) &&
                IsPresetDirty())
            {
                var result = MessageBox.Show(
                    $"Save changes to preset '{_activePresetName}'?",
                    "Unsaved Preset",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Cancel)
                {
                    _suppressPresetSelectionChange = true;
                    SelectPresetByName(_activePresetName);
                    _suppressPresetSelectionChange = false;
                    return;
                }

                if (result == DialogResult.Yes)
                {
                    SaveCurrentPreset();
                }
            }

            var preset = _presetManager.Get(newPresetName);
            if (preset != null)
            {
                txtCommand.ReadOnly = false;
                txtCommand.Text = preset.Commands;
                txtPreset.Text = newPresetName;
                if (preset.Timeout.HasValue)
                {
                    txtTimeoutHeader.Text = preset.Timeout.Value.ToString();
                }
                else
                {
                    txtTimeoutHeader.Text = string.Empty;
                }
            }

            _activePresetName = newPresetName;
        }

        private void trvPresets_AfterCollapse(object? sender, TreeViewEventArgs e)
        {
            if (_suppressExpandCollapseEvents) return;
            if (e.Node?.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                _presetManager.SetFolderExpanded(tag.Name, false);
                if (debugModeToolStripMenuItem.Checked)
                {
                    // Verify the state was actually saved
                    var currentState = _presetManager.Folders.TryGetValue(tag.Name, out var info) ? info.IsExpanded : (bool?)null;
                    UpdateStatusBar($"Folder '{tag.Name}' collapsed. Verified state: {currentState}");
                }
            }
        }

        private void trvPresets_AfterExpand(object? sender, TreeViewEventArgs e)
        {
            if (_suppressExpandCollapseEvents) return;
            if (e.Node?.Tag is PresetNodeTag tag && tag.IsFolder)
            {
                _presetManager.SetFolderExpanded(tag.Name, true);
                if (debugModeToolStripMenuItem.Checked)
                {
                    // Verify the state was actually saved
                    var currentState = _presetManager.Folders.TryGetValue(tag.Name, out var info) ? info.IsExpanded : (bool?)null;
                    UpdateStatusBar($"Folder '{tag.Name}' expanded. Verified state: {currentState}");
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

            // Reset previous highlight
            if (_lastHighlightedNode != null && _lastHighlightedNode != targetNode)
            {
                _lastHighlightedNode.BackColor = trvPresets.BackColor;
            }

            if (targetNode != null && CanDropOn(_draggedNode, targetNode))
            {
                e.Effect = DragDropEffects.Move;
                targetNode.BackColor = Color.LightBlue;
                _lastHighlightedNode = targetNode;
            }
            else if (targetNode == null)
            {
                // Dropping on empty area = move to root
                e.Effect = DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void trvPresets_DragDrop(object? sender, DragEventArgs e)
        {
            // Reset highlight
            if (_lastHighlightedNode != null)
            {
                _lastHighlightedNode.BackColor = trvPresets.BackColor;
                _lastHighlightedNode = null;
            }

            if (_draggedNode == null)
                return;

            var pt = trvPresets.PointToClient(new Point(e.X, e.Y));
            var targetNode = trvPresets.GetNodeAt(pt);
            var draggedTag = _draggedNode.Tag as PresetNodeTag;

            if (draggedTag == null)
            {
                _draggedNode = null;
                return;
            }

            try
            {
                if (draggedTag.IsFolder)
                {
                    // Folder reordering in Manual mode (folders can't be nested)
                    if (_currentSortMode == PresetSortMode.Manual && targetNode?.Tag is PresetNodeTag targetTag && targetTag.IsFolder)
                    {
                        ReorderFolders(draggedTag.Name, targetTag.Name);
                    }
                }
                else
                {
                    // Preset being dragged
                    if (targetNode == null)
                    {
                        // Drop on empty area = move to root
                        _presetManager.MovePresetToFolder(draggedTag.Name, null);
                    }
                    else if (targetNode.Tag is PresetNodeTag targetTag)
                    {
                        if (targetTag.IsFolder)
                        {
                            // Drop on folder = move into folder
                            _presetManager.MovePresetToFolder(draggedTag.Name, targetTag.Name);
                        }
                        else if (_currentSortMode == PresetSortMode.Manual)
                        {
                            // Drop on preset = reorder within same folder
                            var sourcePreset = _presetManager.Get(draggedTag.Name);
                            var targetPreset = _presetManager.Get(targetTag.Name);
                            if (sourcePreset?.Folder == targetPreset?.Folder)
                            {
                                ReorderPresetsInFolder(draggedTag.Name, targetTag.Name, sourcePreset?.Folder);
                            }
                        }
                    }
                }

                RefreshPresetList();
                SelectPresetByName(draggedTag.IsFolder ? null : draggedTag.Name);
            }
            finally
            {
                _draggedNode = null;
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
                // Folders can only be reordered with other folders in Manual mode
                return _currentSortMode == PresetSortMode.Manual && targetTag.IsFolder;
            }
            else
            {
                // Presets can drop on folders or other presets (for reordering)
                return targetTag.IsFolder || _currentSortMode == PresetSortMode.Manual;
            }
        }

        private void ReorderFolders(string sourceFolderName, string targetFolderName)
        {
            var config = _configService.Load();
            var folderOrder = config.ManualFolderOrder;

            if (!folderOrder.Contains(sourceFolderName))
                folderOrder.Add(sourceFolderName);
            if (!folderOrder.Contains(targetFolderName))
                folderOrder.Add(targetFolderName);

            int sourceIndex = folderOrder.IndexOf(sourceFolderName);
            int targetIndex = folderOrder.IndexOf(targetFolderName);

            folderOrder.RemoveAt(sourceIndex);
            folderOrder.Insert(targetIndex, sourceFolderName);

            config.ManualFolderOrder = folderOrder;
            _configService.Save(config);
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

            if (!presetOrder.Contains(sourcePresetName))
                presetOrder.Add(sourcePresetName);
            if (!presetOrder.Contains(targetPresetName))
                presetOrder.Add(targetPresetName);

            int sourceIndex = presetOrder.IndexOf(sourcePresetName);
            int targetIndex = presetOrder.IndexOf(targetPresetName);

            presetOrder.RemoveAt(sourceIndex);
            presetOrder.Insert(targetIndex, sourcePresetName);

            config.ManualPresetOrderByFolder[folderKey] = presetOrder;
            _configService.Save(config);
        }

        #endregion

        #region Favorites TreeView Handlers

        private void presetsTabControl_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (presetsTabControl.SelectedTab == tabFavorites)
            {
                RefreshFavoritesList();
            }
        }

        private void RefreshFavoritesList()
        {
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

            if (favoriteFolders.Count == 0 && favoritePresets.Count == 0)
            {
                lblFavoritesEmpty.Visible = true;
                trvFavorites.Visible = false;
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
                if (item.StartsWith("folder:"))
                {
                    var folderName = item.Substring(7);
                    var folderNode = new TreeNode($"{FolderIcon} {folderName}")
                    {
                        Tag = new PresetNodeTag { IsFolder = true, Name = folderName }
                    };
                    trvFavorites.Nodes.Add(folderNode);

                    // Add presets in this folder
                    var presetsInFolder = GetSortedPresetsInFolder(folderName, config);
                    foreach (var presetName in presetsInFolder)
                    {
                        var presetNode = new TreeNode(presetName)
                        {
                            Tag = new PresetNodeTag { IsFolder = false, Name = presetName }
                        };
                        folderNode.Nodes.Add(presetNode);
                    }

                    // Expand folder by default
                    folderNode.Expand();
                }
                else if (item.StartsWith("preset:"))
                {
                    var presetName = item.Substring(7);
                    var node = new TreeNode(presetName)
                    {
                        Tag = new PresetNodeTag { IsFolder = false, Name = presetName }
                    };
                    trvFavorites.Nodes.Add(node);
                }
            }
        }

        private List<string> GetOrderedFavoriteItems(HashSet<string> favoriteFolders, HashSet<string> favoritePresets, AppConfiguration config)
        {
            var result = new List<string>();
            var remainingFolders = new HashSet<string>(favoriteFolders);
            var remainingPresets = new HashSet<string>(favoritePresets);

            // First, add items in the saved manual order
            foreach (var item in config.ManualFavoriteOrder)
            {
                if (item.StartsWith("folder:"))
                {
                    var folderName = item.Substring(7);
                    if (remainingFolders.Contains(folderName))
                    {
                        result.Add(item);
                        remainingFolders.Remove(folderName);
                    }
                }
                else if (item.StartsWith("preset:"))
                {
                    var presetName = item.Substring(7);
                    if (remainingPresets.Contains(presetName))
                    {
                        result.Add(item);
                        remainingPresets.Remove(presetName);
                    }
                }
            }

            // Add any remaining folders (new favorites not yet in the order)
            foreach (var folder in remainingFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                result.Add($"folder:{folder}");
            }

            // Add any remaining presets (new favorites not yet in the order)
            foreach (var preset in remainingPresets.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                result.Add($"preset:{preset}");
            }

            return result;
        }

        private void trvFavorites_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (_suppressPresetSelectionChange || e.Node == null)
                return;

            var tag = e.Node.Tag as PresetNodeTag;
            if (tag == null)
                return;

            // Handle folder selection
            if (tag.IsFolder)
            {
                // Check for unsaved changes first
                if (!string.IsNullOrEmpty(_activePresetName) && IsPresetDirty())
                {
                    var result = MessageBox.Show(
                        $"Save changes to preset '{_activePresetName}'?",
                        "Unsaved Preset",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Cancel)
                    {
                        return;
                    }

                    if (result == DialogResult.Yes)
                    {
                        SaveCurrentPreset();
                    }
                }

                // Display folder summary
                _activePresetName = null;
                _selectedFolderName = tag.Name;
                txtPreset.Text = $"{FolderIcon} {tag.Name}";
                txtTimeoutHeader.Clear();
                DisplayFolderSummary(tag.Name);
                UpdateRunButtonText();
                return;
            }

            string newPresetName = tag.Name;

            // Check for unsaved changes
            if (!string.IsNullOrEmpty(_activePresetName) &&
                !string.Equals(newPresetName, _activePresetName, StringComparison.Ordinal) &&
                IsPresetDirty())
            {
                var result = MessageBox.Show(
                    $"Save changes to preset '{_activePresetName}'?",
                    "Unsaved Preset",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Cancel)
                {
                    return;
                }

                if (result == DialogResult.Yes)
                {
                    SaveCurrentPreset();
                }
            }

            var preset = _presetManager.Get(newPresetName);
            if (preset != null)
            {
                txtCommand.ReadOnly = false;
                txtCommand.Text = preset.Commands;
                txtPreset.Text = newPresetName;
                if (preset.Timeout.HasValue)
                {
                    txtTimeoutHeader.Text = preset.Timeout.Value.ToString();
                }
                else
                {
                    txtTimeoutHeader.Text = string.Empty;
                }
            }

            _activePresetName = newPresetName;
            _selectedFolderName = null;
            UpdateRunButtonText();
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
            if (_lastHighlightedNode != null && _lastHighlightedNode != targetNode)
            {
                _lastHighlightedNode.BackColor = trvFavorites.BackColor;
            }

            if (targetNode != null && CanDropOnFavorites(_draggedNode, targetNode))
            {
                e.Effect = DragDropEffects.Move;
                targetNode.BackColor = Color.LightBlue;
                _lastHighlightedNode = targetNode;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void trvFavorites_DragDrop(object? sender, DragEventArgs e)
        {
            // Reset highlight
            if (_lastHighlightedNode != null)
            {
                _lastHighlightedNode.BackColor = trvFavorites.BackColor;
                _lastHighlightedNode = null;
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

            string sourceKey = sourceTag.IsFolder ? $"folder:{sourceTag.Name}" : $"preset:{sourceTag.Name}";
            string targetKey = targetTag.IsFolder ? $"folder:{targetTag.Name}" : $"preset:{targetTag.Name}";

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
            using var dialog = new SettingsDialog(_configService);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // Settings saved - default timeout only applies to new presets
                // Don't update the current timeout field as it's preset-specific

                // Apply theme and font settings if changed
                var config = _configService.GetCurrent();
                ApplyTheme(config.DarkMode);
                ApplyFontSettings(config.FontSettings);
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
            var scriptText = txtCommand.Text ?? string.Empty;

            if (!Services.Scripting.ScriptParser.IsYamlScript(scriptText))
            {
                MessageBox.Show("Current commands are not a YAML script.", "Validate Script", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var parser = new Services.Scripting.ScriptParser();

            try
            {
                var script = parser.Parse(scriptText);
                var errors = parser.Validate(script, scriptText);

                if (errors.Count == 0)
                {
                    var successMessage = ScriptValidationFormatter.FormatSuccessMessage();
                    AppendOutputText(Environment.NewLine + successMessage + Environment.NewLine);
                    MessageBox.Show(successMessage, "Validate Script", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    var message = ScriptValidationFormatter.FormatFailureMessage(errors);
                    AppendOutputText(Environment.NewLine + message + Environment.NewLine);
                    MessageBox.Show(message, "Validate Script", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                var message = ScriptValidationFormatter.FormatExceptionMessage(ex);
                AppendOutputText(Environment.NewLine + message + Environment.NewLine);
                MessageBox.Show(message, "Validate Script", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void debugModeToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            _sshService.DebugMode = debugModeToolStripMenuItem.Checked;
            UpdateStatusBar(debugModeToolStripMenuItem.Checked ? "Debug mode enabled" : "Debug mode disabled");
        }

        private void sshDebugModeToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            _sshDebugMode = sshDebugModeToolStripMenuItem.Checked;
            _sshService.SshDebugMode = _sshDebugMode;
            UpdateStatusBar(_sshDebugMode ? "SSH Debug enabled - timing info will be logged" : "SSH Debug disabled");
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
            using var dlg = new AboutDialog(ApplicationName, ApplicationVersion);
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
            DuplicatePreset();
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RenamePreset();
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeletePreset();
        }

        private void toggleSortingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Cycle through sort modes: Ascending -> Descending -> Manual -> Ascending
            var previousMode = _currentSortMode;
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
        }

        private void UpdateSortModeIndicator()
        {
            ctxToggleSorting.Text = $"Toggle Sorting ({_currentSortMode})";
        }

        private void ExportPreset_Click(object? sender, EventArgs e)
        {
            ExportPreset();
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
                ctxMoveToFolder.Visible = false;

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
                _presetManager.MovePresetToFolder(presetName, null);
                RefreshPresetList();
                SelectPresetByName(presetName);
            };
            ctxMoveToFolder.DropDownItems.Add(rootItem);

            // Add separator if there are folders
            var folders = _presetManager.GetFolders().ToList();
            if (folders.Count > 0)
            {
                ctxMoveToFolder.DropDownItems.Add(new ToolStripSeparator());
            }

            // Add folder options
            foreach (var folderName in folders.OrderBy(f => f))
            {
                var folderItem = new ToolStripMenuItem(folderName)
                {
                    Checked = string.Equals(currentFolder, folderName, StringComparison.Ordinal),
                    Tag = folderName
                };
                folderItem.Click += (s, e) =>
                {
                    _presetManager.MovePresetToFolder(presetName, folderName);
                    RefreshPresetList();
                    SelectPresetByName(presetName);
                };
                ctxMoveToFolder.DropDownItems.Add(folderItem);
            }
        }

        private void ctxAddFolder_Click(object? sender, EventArgs e)
        {
            string folderName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a name for the new folder:",
                "New Folder",
                "New Folder");

            if (string.IsNullOrWhiteSpace(folderName)) return;

            folderName = _presetManager.GetUniqueFolderName(folderName);

            if (_presetManager.CreateFolder(folderName))
            {
                RefreshPresetList();
                UpdateStatusBar($"Folder '{folderName}' created");
            }
        }

        private void ctxRenameFolder_Click(object? sender, EventArgs e)
        {
            if (trvPresets.SelectedNode?.Tag is not PresetNodeTag tag || !tag.IsFolder)
                return;

            string oldName = tag.Name;
            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter a new name for the folder '{oldName}':",
                "Rename Folder",
                oldName);

            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;

            if (_presetManager.RenameFolder(oldName, newName))
            {
                RefreshPresetList();
                UpdateStatusBar($"Folder renamed to '{newName}'");
            }
            else
            {
                MessageBox.Show("A folder with that name already exists.", "Rename Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ctxDeleteFolder_Click(object? sender, EventArgs e)
        {
            if (trvPresets.SelectedNode?.Tag is not PresetNodeTag tag || !tag.IsFolder)
                return;

            string folderName = tag.Name;
            var presetsInFolder = _presetManager.GetPresetsInFolder(folderName).ToList();

            string message = presetsInFolder.Count > 0
                ? $"Delete folder '{folderName}' and move its {presetsInFolder.Count} preset(s) to root level?"
                : $"Delete empty folder '{folderName}'?";

            if (MessageBox.Show(message, "Delete Folder", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _presetManager.DeleteFolder(folderName, deletePresets: false);
                RefreshPresetList();
                UpdateStatusBar($"Folder '{folderName}' deleted");
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
                MessageBox.Show("Please select a folder to delete.", "Delete Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string folderName = tag.Name;
            var presetsInFolder = _presetManager.GetPresetsInFolder(folderName).ToList();

            string message = presetsInFolder.Count > 0
                ? $"Are you sure you want to delete the folder '{folderName}' and ALL {presetsInFolder.Count} preset(s) inside it?\n\nThis action cannot be undone."
                : $"Delete empty folder '{folderName}'?";

            if (MessageBox.Show(message, "Delete Folder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                // Clear editor before deleting to avoid "save changes?" prompt for deleted presets
                _activePresetName = null;
                txtPreset.Clear();
                txtCommand.Clear();
                txtTimeoutHeader.Clear();

                _presetManager.DeleteFolder(folderName, deletePresets: true);
                RefreshPresetList();
                UpdateStatusBar($"Folder '{folderName}' and its presets deleted");
            }
        }

        private void lstOutput_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstOutput.SelectedItem is HistoryListItem entry)
            {
                // Try to get per-host results from in-memory cache or from saved state
                _currentHostResults = GetHostResultsForEntry(entry.Id);

                if (_currentHostResults != null && _currentHostResults.Count > 0)
                {
                    // Populate and show the host list
                    lstHosts.Items.Clear();
                    foreach (var hostResult in _currentHostResults)
                    {
                        lstHosts.Items.Add(hostResult);
                    }

                    // Show the host list panel
                    historySplitContainer.Panel2Collapsed = false;

                    // Select the first host to show its output
                    if (lstHosts.Items.Count > 0)
                    {
                        lstHosts.SelectedIndex = 0;
                    }
                    return;
                }

                // For entries without per-host data, hide the host list and show combined output
                historySplitContainer.Panel2Collapsed = true;
                lstHosts.Items.Clear();
                _currentHostResults = null;
                SetOutputText(entry.Output);
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
            // Can be used for dynamic menu state
        }

        private void LstOutput_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = lstOutput.IndexFromPoint(e.Location);
                if (index >= 0 && index < lstOutput.Items.Count)
                {
                    lstOutput.SelectedIndex = index;
                }
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
            if (lstHosts.SelectedItem is HostHistoryEntry hostEntry)
            {
                // Trim leading blank lines for cleaner display
                SetOutputText(hostEntry.Output.TrimStart('\r', '\n'));
            }
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
                var iconColor = hostEntry.Success ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69);
                var iconText = hostEntry.Success ? "\u2713" : "\u2717";

                using var iconFont = new Font("Segoe UI", 10F, FontStyle.Bold);
                using var iconBrush = new SolidBrush(iconColor);
                e.Graphics.DrawString(iconText, iconFont, iconBrush, iconRect.Left, iconRect.Top - 1);

                // Draw host address with theme-aware text color
                var textRect = new Rectangle(e.Bounds.Left + 24, e.Bounds.Top, e.Bounds.Width - 28, e.Bounds.Height);
                var textColor = _isDarkMode ? DarkTextPrimary : (isSelected ? Color.White : e.ForeColor);
                using var textBrush = new SolidBrush(textColor);
                e.Graphics.DrawString(hostEntry.HostAddress, e.Font ?? lstHosts.Font, textBrush, textRect, StringFormat.GenericDefault);
            }
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

            // Check if this is a folder entry (contains folder emoji)
            bool isFolderEntry = text.Contains(FolderIcon); // folder icon

            // Draw text with theme-aware color
            var textColor = _isDarkMode ? DarkTextPrimary : (isSelected ? Color.White : e.ForeColor);
            using var textBrush = new SolidBrush(textColor);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

            if (isFolderEntry)
            {
                // Remove the folder emoji from text
                text = text.Replace("\U0001F4C1 ", "").Replace("\U0001F4C1", "");

                // Parse the text to extract date/time and folder name
                // Format is "2026-01-18 17:39:04 - FolderName"
                int separatorIndex = text.IndexOf(" - ");
                string dateTimePart = separatorIndex > 0 ? text.Substring(0, separatorIndex) : "";
                string folderName = separatorIndex > 0 ? text.Substring(separatorIndex + 3) : text;

                int currentX = e.Bounds.Left + 4;

                // Draw date/time first
                if (!string.IsNullOrEmpty(dateTimePart))
                {
                    var dateTimeSize = e.Graphics.MeasureString(dateTimePart + " - ", e.Font ?? lstOutput.Font);
                    var dateTimeRect = new RectangleF(currentX, e.Bounds.Top, dateTimeSize.Width, e.Bounds.Height);
                    e.Graphics.DrawString(dateTimePart + " - ", e.Font ?? lstOutput.Font, textBrush, dateTimeRect, sf);
                    currentX += (int)dateTimeSize.Width;
                }

                // Draw folder icon after date/time
                var iconColor = _isDarkMode ? Color.FromArgb(220, 180, 80) : Color.FromArgb(180, 140, 60);
                using var iconFont = new Font("Segoe UI Symbol", 9F);
                using var iconBrush = new SolidBrush(iconColor);
                var iconRect = new RectangleF(currentX, e.Bounds.Top, 18, e.Bounds.Height);
                var iconSf = new StringFormat { LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("\U0001F4C1", iconFont, iconBrush, iconRect, iconSf);
                currentX += 18;

                // Draw folder name
                var nameRect = new Rectangle(currentX, e.Bounds.Top, e.Bounds.Width - currentX - 4, e.Bounds.Height);
                e.Graphics.DrawString(folderName, e.Font ?? lstOutput.Font, textBrush, nameRect, sf);
            }
            else
            {
                // Regular entry - just draw the text
                var textRect = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height);
                e.Graphics.DrawString(text, e.Font ?? lstOutput.Font, textBrush, textRect, sf);
            }
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
                var iconSf = new StringFormat { LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("\U0001F4C1", iconFont, iconBrush, iconRect, iconSf);
            }

            // Draw text
            var textBounds = new Rectangle(indent + iconWidth, e.Bounds.Y, treeView.ClientSize.Width - indent - iconWidth, e.Bounds.Height);
            using var textBrush = new SolidBrush(textColor);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            e.Graphics.DrawString(nodeText, treeView.Font, textBrush, textBounds, sf);
        }

        private void exportHostOutputToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (lstHosts.SelectedItem is not HostHistoryEntry hostEntry)
            {
                MessageBox.Show("Please select a host to export.", "No Host Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"{hostEntry.HostAddress.Replace(":", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, hostEntry.Output);
                    UpdateStatusBar($"Output exported to {Path.GetFileName(sfd.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region CSV Operations

        private void OpenCsvFile()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Multiselect = false
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var dataTable = _csvManager.LoadFromFile(ofd.FileName);
                    _loadedFilePath = ofd.FileName;
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
                    AutoSizeColumnsToContent();
                    UpdateHostCount();
                    UpdateStatusBar($"Loaded: {Path.GetFileName(ofd.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load CSV: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveCsvAs()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Save as CSV"
            };

            if (!string.IsNullOrEmpty(_loadedFilePath))
                sfd.FileName = Path.GetFileName(_loadedFilePath);

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                SaveCsvToFile(sfd.FileName);
                _loadedFilePath = sfd.FileName;
                UpdateStatusBar($"Saved: {Path.GetFileName(sfd.FileName)}");
            }
        }

        private bool SaveCurrentCsv(bool promptIfNoPath)
        {
            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            if (string.IsNullOrWhiteSpace(_loadedFilePath))
            {
                if (!promptIfNoPath) return false;
                SaveCsvAs();
                return !string.IsNullOrWhiteSpace(_loadedFilePath);
            }

            try
            {
                SaveCsvToFile(_loadedFilePath);
                UpdateStatusBar($"Saved: {Path.GetFileName(_loadedFilePath)}");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file:\r\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
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
            _loadedFilePath = null;
            UpdateHostCount();
            UpdateStatusBar("Grid cleared");
        }

        private bool EnsureCsvChangesSaved()
        {
            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            if (!_csvDirty) return true;

            var result = MessageBox.Show(
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
                MessageBox.Show("Column name already exists!");
                return;
            }

            dgv_variables.Columns.Add(columnName, columnName);
            _csvDirty = true;
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
                MessageBox.Show("This column name already exists.", "Rename Column Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            column.HeaderText = newName;
            column.Name = newName;
            _csvDirty = true;
        }

        private void DeleteColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= dgv_variables.Columns.Count) return;

            if (IsProtectedColumn(columnIndex))
            {
                MessageBox.Show("The Host_IP column cannot be deleted.", "Delete Column", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            dgv_variables.Columns.RemoveAt(columnIndex);
            _csvDirty = true;
        }

        private void DeleteRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgv_variables.Rows.Count)
            {
                MessageBox.Show("No valid row selected.");
                return;
            }

            var row = dgv_variables.Rows[rowIndex];
            if (row.IsNewRow)
            {
                MessageBox.Show("Cannot delete the new row placeholder.");
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
            UpdateHostCount();
        }

        private void DeleteSelectedCells()
        {
            foreach (DataGridViewCell cell in dgv_variables.SelectedCells)
            {
                if (!cell.ReadOnly) cell.Value = null;
            }
            dgv_variables.Refresh();
            _csvDirty = true;
        }

        #endregion

        #region Preset Operations

        private void SaveCurrentPreset()
        {
            // Don't save when a folder is selected (folder summary is displayed)
            if (!string.IsNullOrEmpty(_selectedFolderName))
            {
                return;
            }

            string presetName = txtPreset.Text.Trim();
            string commands = txtCommand.Text;

            if (string.IsNullOrEmpty(presetName))
            {
                MessageBox.Show("Preset name is required.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Prevent saving preset with folder icon prefix (safety check)
            if (presetName.StartsWith(FolderIcon, StringComparison.Ordinal))
            {
                return;
            }

            // Preserve existing IsFavorite and Folder status if updating
            var existingPreset = _presetManager.Get(presetName);
            var preset = new PresetInfo
            {
                Commands = commands,
                Timeout = int.TryParse(txtTimeoutHeader.Text, out var t) ? t : null,
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
                RefreshPresetList();
            }

            _activePresetName = presetName;
            UpdateStatusBar($"Preset '{presetName}' saved");
        }

        private void AddPreset()
        {
            // Check for unsaved changes first
            if (!string.IsNullOrEmpty(_activePresetName) && IsPresetDirty())
            {
                var saveResult = MessageBox.Show(
                    $"Save changes to preset '{_activePresetName}'?",
                    "Unsaved Preset",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (saveResult == DialogResult.Cancel)
                    return;

                if (saveResult == DialogResult.Yes)
                {
                    SaveCurrentPreset();
                }
            }

            string presetName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the name of the new preset:",
                "Add Preset",
                "New Preset");

            if (string.IsNullOrEmpty(presetName)) return;

            if (_presetManager.Presets.ContainsKey(presetName))
            {
                MessageBox.Show("Preset name already exists!");
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

            // Get default timeout from config
            var config = _configService.Load();
            var newPreset = new PresetInfo
            {
                Timeout = config.Timeout,
                Folder = targetFolder
            };

            _presetManager.Save(presetName, newPreset);

            // Add to manual order
            if (!_manualPresetOrder.Contains(presetName))
            {
                _manualPresetOrder.Add(presetName);
            }

            RefreshPresetList();
            SelectPresetByName(presetName);

            // Load the new preset into the editor
            txtPreset.Text = presetName;
            txtCommand.Clear();
            txtTimeoutHeader.Text = config.Timeout.ToString();
            _activePresetName = presetName;
        }

        private void DuplicatePreset()
        {
            if (trvPresets.SelectedNode?.Tag is not PresetNodeTag tag || tag.IsFolder)
                return;

            string sourceName = tag.Name;
            string suggested = _presetManager.GetUniqueName(sourceName + "_Copy");

            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter name for the copied preset (from '{sourceName}'):",
                "Copy Preset",
                suggested);

            if (string.IsNullOrWhiteSpace(newName)) return;

            if (_presetManager.Presets.ContainsKey(newName))
            {
                MessageBox.Show("A preset with that name already exists.", "Copy Preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string finalName = _presetManager.Duplicate(sourceName, newName);

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

                RefreshPresetList();
                SelectPresetByName(finalName);

                var preset = _presetManager.Get(finalName);
                txtPreset.Text = finalName;
                txtCommand.Text = preset?.Commands ?? "";
                _activePresetName = finalName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenamePreset()
        {
            if (trvPresets.SelectedNode?.Tag is not PresetNodeTag tag || tag.IsFolder)
                return;

            string selectedPreset = tag.Name;
            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter a new name for the preset '{selectedPreset}':",
                "Rename Preset",
                selectedPreset);

            if (string.IsNullOrEmpty(newName) || newName == selectedPreset) return;

            if (!_presetManager.Rename(selectedPreset, newName))
            {
                MessageBox.Show("This preset name already exists.", "Rename Preset Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Update manual order list
            int orderIndex = _manualPresetOrder.IndexOf(selectedPreset);
            if (orderIndex >= 0)
            {
                _manualPresetOrder[orderIndex] = newName;
            }

            RefreshPresetList();
            SelectPresetByName(newName);
            txtPreset.Text = newName;
            _activePresetName = newName;
        }

        private void DeletePreset()
        {
            if (trvPresets.SelectedNode?.Tag is not PresetNodeTag tag || tag.IsFolder)
                return;

            string selectedPreset = tag.Name;

            // Check if this is the currently active preset being deleted
            bool isDeletingActivePreset = string.Equals(selectedPreset, _activePresetName, StringComparison.Ordinal);

            if (_presetManager.Delete(selectedPreset))
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

                RefreshPresetList();

                // Select another preset if any exist
                if (_presetManager.Presets.Count > 0)
                {
                    var firstPreset = _presetManager.Presets.Keys.FirstOrDefault();
                    if (!string.IsNullOrEmpty(firstPreset))
                    {
                        SelectPresetByName(firstPreset);
                        var preset = _presetManager.Get(firstPreset);
                        if (preset != null)
                        {
                            txtPreset.Text = firstPreset;
                            txtCommand.Text = preset.Commands;
                            txtTimeoutHeader.Text = preset.Timeout.HasValue
                                ? preset.Timeout.Value.ToString()
                                : string.Empty;
                            _activePresetName = firstPreset;
                        }
                    }
                }
            }
        }

        private void ExportPreset()
        {
            if (trvPresets.SelectedNode?.Tag is not PresetNodeTag tag || tag.IsFolder)
            {
                MessageBox.Show("No preset selected to export.", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string presetName = tag.Name;
                string exportString = _presetManager.Export(presetName);
                Clipboard.SetText(exportString);
                MessageBox.Show("Preset exported to clipboard.", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export preset: {ex.Message}", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImportPreset()
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Paste the encoded preset string:\r\nFormat: <name>_<encoded>",
                "Import Preset",
                "");

            if (string.IsNullOrWhiteSpace(input)) return;

            try
            {
                int? defaultTimeout = int.TryParse(txtTimeoutHeader.Text, out var t) ? t : null;

                string finalName = _presetManager.Import(input, defaultTimeout);

                RefreshPresetList();
                SelectPresetByName(finalName);

                var preset = _presetManager.Get(finalName);
                if (preset != null)
                {
                    txtPreset.Text = finalName;
                    txtCommand.Text = preset.Commands;
                    if (preset.Timeout.HasValue) txtTimeoutHeader.Text = preset.Timeout.Value.ToString();
                }
                _activePresetName = finalName;

                MessageBox.Show($"Preset '{finalName}' imported.", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (FormatException)
            {
                MessageBox.Show("Invalid format or Base64 encoding.", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import preset: {ex.Message}", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportAllPresets()
        {
            if (_presetManager.Presets.Count == 0)
            {
                MessageBox.Show("No presets to export.", "Export All Presets", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Title = "Export All Presets",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = "json",
                FileName = "presets_export.json"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                _presetManager.ExportAllToFile(dialog.FileName);
                MessageBox.Show($"Exported {_presetManager.Presets.Count} presets to:\n{dialog.FileName}",
                    "Export All Presets", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export presets: {ex.Message}", "Export All Presets", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                int count = _presetManager.ImportAllFromFile(dialog.FileName);
                RefreshPresetList();
                MessageBox.Show($"Imported {count} presets.\n\nNote: If any preset names already existed, '_imported' was appended to avoid overwriting.",
                    "Import All Presets", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (FormatException ex)
            {
                MessageBox.Show($"Invalid preset file format: {ex.Message}", "Import All Presets", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import presets: {ex.Message}", "Import All Presets", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshPresetList(bool restoreExpandState = true)
        {
            string? currentSelection = null;
            if (trvPresets.SelectedNode?.Tag is PresetNodeTag selectedTag && !selectedTag.IsFolder)
            {
                currentSelection = selectedTag.Name;
            }

            _suppressPresetSelectionChange = true;
            _suppressExpandCollapseEvents = true;
            trvPresets.BeginUpdate();
            trvPresets.Nodes.Clear();

            var config = _configService.Load();

            // Get sorted folders
            var folders = GetSortedFolders(config);

            // Add folder nodes
            var folderNodes = new Dictionary<string, TreeNode>();
            foreach (var folderName in folders)
            {
                var folderInfo = _presetManager.Folders.GetValueOrDefault(folderName);
                string folderDisplay = folderInfo?.IsFavorite == true
                    ? $"{StarIcon} {FolderIcon} {folderName}"
                    : $"{FolderIcon} {folderName}";
                var folderNode = new TreeNode(folderDisplay)
                {
                    Tag = new PresetNodeTag { IsFolder = true, Name = folderName }
                };
                trvPresets.Nodes.Add(folderNode);
                folderNodes[folderName] = folderNode;

                // Add presets in this folder
                var presetsInFolder = GetSortedPresetsInFolder(folderName, config);
                foreach (var presetName in presetsInFolder)
                {
                    var preset = _presetManager.Get(presetName);
                    string displayName = preset?.IsFavorite == true ? $"{StarIcon} {presetName}" : presetName;
                    var presetNode = new TreeNode(displayName)
                    {
                        Tag = new PresetNodeTag { IsFolder = false, Name = presetName }
                    };
                    folderNode.Nodes.Add(presetNode);
                }

                // Restore expand state immediately while still in BeginUpdate block
                // This prevents flicker by doing all visual changes before EndUpdate
                if (restoreExpandState && folderInfo?.IsExpanded == true)
                {
                    folderNode.Expand();
                }
            }

            // Add root-level presets (no folder)
            var rootPresets = GetSortedPresetsInFolder(null, config);
            foreach (var presetName in rootPresets)
            {
                var preset = _presetManager.Get(presetName);
                string displayName = preset?.IsFavorite == true ? $"{StarIcon} {presetName}" : presetName;
                var presetNode = new TreeNode(displayName)
                {
                    Tag = new PresetNodeTag { IsFolder = false, Name = presetName }
                };
                trvPresets.Nodes.Add(presetNode);
            }

            trvPresets.EndUpdate();

            // Restore selection
            if (!string.IsNullOrEmpty(currentSelection))
            {
                SelectPresetByName(currentSelection);
            }

            _suppressPresetSelectionChange = false;
            _suppressExpandCollapseEvents = false;
        }

        private IEnumerable<string> GetSortedFolders(AppConfiguration config)
        {
            var folders = _presetManager.GetFolders().ToList();

            return _currentSortMode switch
            {
                PresetSortMode.Ascending => folders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase),
                PresetSortMode.Descending => folders.OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase),
                PresetSortMode.Manual => GetManualOrderedFolders(folders, config),
                _ => folders
            };
        }

        private IEnumerable<string> GetManualOrderedFolders(List<string> folders, AppConfiguration config)
        {
            var result = new List<string>();
            foreach (var name in config.ManualFolderOrder)
            {
                if (folders.Contains(name))
                {
                    result.Add(name);
                }
            }
            // Add any folders not in manual order
            foreach (var name in folders)
            {
                if (!result.Contains(name))
                {
                    result.Add(name);
                }
            }
            return result;
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
            var result = new List<string>();

            if (config.ManualPresetOrderByFolder.TryGetValue(folderKey, out var manualOrder))
            {
                foreach (var name in manualOrder)
                {
                    if (presets.Contains(name))
                    {
                        result.Add(name);
                    }
                }
            }
            // Add any presets not in manual order
            foreach (var name in presets)
            {
                if (!result.Contains(name))
                {
                    result.Add(name);
                }
            }
            return result;
        }

        private void DisplayFolderSummary(string folderName)
        {
            var config = _configService.Load();
            var presetNames = GetSortedPresetsInFolder(folderName, config).ToList();

            var sb = new StringBuilder();
            sb.AppendLine(FolderSummarySeparator);
            sb.AppendLine($"  FOLDER: {folderName}");
            sb.AppendLine(FolderSummarySeparator);
            sb.AppendLine();
            sb.AppendLine($"  Presets: {presetNames.Count}");
            sb.AppendLine();

            if (presetNames.Count > 0)
            {
                sb.AppendLine("  Contents:");
                sb.AppendLine($"  {FolderSummarySubSeparator}");
                foreach (var name in presetNames)
                {
                    var preset = _presetManager.Get(name);
                    var favorite = preset?.IsFavorite == true ? $"{StarIcon} " : "  ";
                    var type = preset?.IsScript == true ? "[Script]" : "";
                    sb.AppendLine($"  {favorite}{name} {type}");
                }
            }
            else
            {
                sb.AppendLine("  (Empty folder)");
            }

            txtCommand.Text = sb.ToString();
            txtCommand.ReadOnly = true;
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
            using (var g = btnExecuteSelected.CreateGraphics())
            {
                var selectedSize = g.MeasureString(btnExecuteSelected.Text, btnExecuteSelected.Font);
                btnExecuteSelected.Width = (int)selectedSize.Width + 40;

                var allSize = g.MeasureString(btnExecuteAll.Text, btnExecuteAll.Font);
                btnExecuteAll.Width = (int)allSize.Width + 40;
                btnExecuteAll.Left = btnExecuteSelected.Right + 8;

                // Position Stop button with same spacing and ensure matching height
                btnStopAll.Left = btnExecuteAll.Right + 8;
                btnStopAll.Height = btnExecuteAll.Height;
            }
        }

        private void SelectPresetByName(string? presetName)
        {
            if (string.IsNullOrEmpty(presetName))
                return;

            TreeNode? FindNode(TreeNodeCollection nodes)
            {
                foreach (TreeNode node in nodes)
                {
                    if (node.Tag is PresetNodeTag tag && !tag.IsFolder && tag.Name == presetName)
                        return node;

                    var found = FindNode(node.Nodes);
                    if (found != null)
                        return found;
                }
                return null;
            }

            var targetNode = FindNode(trvPresets.Nodes);
            if (targetNode != null)
            {
                // Suppress expand events while making the node visible
                // to avoid overwriting saved expand/collapse state
                _suppressExpandCollapseEvents = true;
                trvPresets.SelectedNode = targetNode;
                targetNode.EnsureVisible();
                _suppressExpandCollapseEvents = false;
            }
        }

        private void SelectFolderByName(string? folderName)
        {
            if (string.IsNullOrEmpty(folderName))
                return;

            foreach (TreeNode node in trvPresets.Nodes)
            {
                if (node.Tag is PresetNodeTag tag && tag.IsFolder && tag.Name == folderName)
                {
                    _suppressExpandCollapseEvents = true;
                    trvPresets.SelectedNode = node;
                    node.EnsureVisible();
                    _suppressExpandCollapseEvents = false;
                    return;
                }
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

            preset.IsFavorite = !preset.IsFavorite;
            _presetManager.Save(presetName, preset);

            RefreshPresetList();
            RefreshFavoritesList();
            SelectPresetByName(presetName);

            UpdateStatusBar(preset.IsFavorite ? $"'{presetName}' added to favorites" : $"'{presetName}' removed from favorites");
        }

        private void ToggleFolderFavorite(string folderName)
        {
            if (!_presetManager.Folders.TryGetValue(folderName, out var folderInfo))
                return;

            bool newFavoriteState = !folderInfo.IsFavorite;
            _presetManager.SetFolderFavorite(folderName, newFavoriteState);

            RefreshPresetList();
            RefreshFavoritesList();
            SelectFolderByName(folderName);

            UpdateStatusBar(newFavoriteState ? $"Folder '{folderName}' added to favorites" : $"Folder '{folderName}' removed from favorites");
        }

        private string GetPresetNameFromDisplay(string displayName)
        {
            return displayName.StartsWith($"{StarIcon} ", StringComparison.Ordinal) ? displayName.Substring(2) : displayName;
        }

        private bool IsPresetDirty()
        {
            // When viewing a folder (not a preset), there's nothing to save
            if (!string.IsNullOrEmpty(_selectedFolderName)) return false;

            if (string.IsNullOrEmpty(_activePresetName)) return InputValidator.IsNotEmpty(txtPreset.Text) || InputValidator.IsNotEmpty(txtCommand.Text);

            var preset = _presetManager.Get(_activePresetName);
            if (preset == null) return InputValidator.IsNotEmpty(txtPreset.Text) || InputValidator.IsNotEmpty(txtCommand.Text);

            bool nameChanged = !string.Equals(txtPreset.Text?.Trim(), _activePresetName, StringComparison.Ordinal);
            bool commandsChanged = !string.Equals(txtCommand.Text, preset.Commands ?? "", StringComparison.Ordinal);

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

            bool commandsChanged = !string.Equals(txtCommand.Text, preset.Commands ?? "", StringComparison.Ordinal);
            sb.AppendLine($"[{(commandsChanged ? "X" : " ")}] Commands changed:");
            if (commandsChanged)
            {
                var savedCmd = preset.Commands ?? "";
                var currentCmd = txtCommand.Text ?? "";
                sb.AppendLine($"    Saved length: {savedCmd.Length} chars");
                sb.AppendLine($"    Current length: {currentCmd.Length} chars");
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
                using var dialog = new FolderExecutionDialog(presetDisplayName, new List<string> { presetDisplayName }, hostAddresses);
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

            SshDebugLog("EXEC", "Calling SetExecutionMode(true)", sw);
            SetExecutionMode(true);
            ClearOutput();

            SshDebugLog("EXEC", "Preparing execution options", sw);
            int commandTimeout = InputValidator.ParseIntOrDefault(txtTimeoutHeader.Text, 10);
            var preparation = _executionCoordinator.PrepareExecution(txtCommand.Text, commandTimeout);
            SshDebugLog("EXEC", $"Timeouts configured - command: {preparation.CommandTimeoutSeconds}s, connection: {preparation.ConnectionTimeoutSeconds}s", sw);

            var preset = preparation.Preset;
            if (includeCommandPreview)
            {
                var commandPreview = txtCommand.Text.Length > 50 ? txtCommand.Text.Substring(0, 50) + "..." : txtCommand.Text;
                SshDebugLog("EXEC", $"Preset created. IsScript: {preset.IsScript}, Commands: {commandPreview.Replace("\r", "\\r").Replace("\n", "\\n")}", sw);
            }
            else
            {
                SshDebugLog("EXEC", $"Preset created. IsScript: {preset.IsScript}", sw);
            }

            UpdateStatusBar(startStatus(hosts.Count), true, 0, hosts.Count);

            try
            {
                List<ExecutionResult> results;
                if (dialogOptions != null)
                {
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
                        dialogOptions);
                    SshDebugLog("EXEC", $"ExecuteFolderAsync completed. Results: {results.Count}", sw);
                }
                else
                {
                    SshDebugLog("EXEC", "Calling ExecutePresetAsync - SSH connection starting", sw);
                    results = await _executionCoordinator.ExecutePresetAsync(
                        hosts,
                        preparation,
                        tsbUsername.Text,
                        tsbPassword.Text);
                    SshDebugLog("EXEC", $"ExecutePresetAsync completed. Results: {results.Count}", sw);
                }
                StoreExecutionHistory(results);
                UpdateStatusBar(completionStatus(results.Count));
            }
            catch (Exception ex)
            {
                SshDebugLog("EXEC", $"Exception: {ex.GetType().Name}: {ex.Message}", sw);
                MessageBox.Show($"An error occurred: {ex.Message}");
                UpdateStatusBar("Execution failed");
            }
            finally
            {
                SshDebugLog("EXEC", "Execution complete, calling SetExecutionMode(false)", sw);
                SetExecutionMode(false);
            }
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
                MessageBox.Show("No hosts selected. Check the boxes next to hosts you want to execute on.",
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
            var config = _configService.Load();
            var presetNames = GetSortedPresetsInFolder(folderName, config).ToList();
            if (presetNames.Count == 0)
            {
                MessageBox.Show($"Folder '{folderName}' contains no presets.", "Run Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var hostRows = dgv_variables.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow && !string.IsNullOrWhiteSpace(GetCellValue(r, CsvManager.HostColumnName)))
                .ToList();

            if (hostRows.Count == 0)
            {
                MessageBox.Show("No hosts available.", "Run Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Show folder execution dialog
            var hostAddresses = hostRows
                .Select(r => GetCellValue(r, CsvManager.HostColumnName))
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .ToList();
            using var dialog = new FolderExecutionDialog(folderName, presetNames, hostAddresses);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var options = dialog.Options;
            if (options.SelectedPresets.Count == 0)
                return;

            if (_sshService.IsRunning) return;

            await ExecuteFolderWithOptionsAsync(folderName, options, hostRows);
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

            var config = _configService.Load();
            var presetNames = GetSortedPresetsInFolder(folderName, config).ToList();
            if (presetNames.Count == 0)
            {
                MessageBox.Show($"Folder '{folderName}' contains no presets.", "Run Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Show folder execution dialog (single host selected)
            using var dialog = new FolderExecutionDialog(folderName, presetNames, new List<string> { host });
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var options = dialog.Options;
            if (options.SelectedPresets.Count == 0)
                return;

            if (_sshService.IsRunning) return;

            await ExecuteFolderWithOptionsAsync(folderName, options, new List<DataGridViewRow> { row });
        }

        private async void ExecuteFolderPresetsOnCheckedHosts(string folderName)
        {
            var config = _configService.Load();
            var presetNames = GetSortedPresetsInFolder(folderName, config).ToList();
            if (presetNames.Count == 0)
            {
                MessageBox.Show($"Folder '{folderName}' contains no presets.", "Run Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

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

            // Show folder execution dialog with all checked hosts
            var hostAddresses = checkedRows
                .Select(r => GetCellValue(r, CsvManager.HostColumnName))
                .ToList();
            using var dialog = new FolderExecutionDialog(folderName, presetNames, hostAddresses);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var options = dialog.Options;
            if (options.SelectedPresets.Count == 0)
                return;

            if (_sshService.IsRunning) return;

            await ExecuteFolderWithOptionsAsync(folderName, options, checkedRows);
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

            var hosts = GetHostConnections(hostRows).ToList();
            if (hosts.Count == 0)
                return;

            SetExecutionMode(true);
            ClearOutput();

            int totalOperations = hosts.Count * presets.Count;
            UpdateStatusBar($"Executing folder '{folderName}' on {hosts.Count} hosts...", true, 0, totalOperations);

            // Use default timeout from first preset or config
            int commandTimeout = presets.Values.FirstOrDefault()?.Timeout ?? config.Timeout;
            var timeouts = SshTimeoutOptions.Create(commandTimeout, connectionTimeout);

            // Progress reporter
            var progress = new Progress<FolderExecutionProgress>(p =>
            {
                if (!string.IsNullOrEmpty(p.CurrentHost) && !string.IsNullOrEmpty(p.CurrentPreset))
                {
                    int completed = p.CompletedHosts * presets.Count + p.CompletedPresets;
                    UpdateStatusBar($"Running {folderName}: {p.CurrentPreset} ({p.CompletedPresets + 1}/{p.TotalPresets}) on {p.CurrentHost}", true, completed, totalOperations);
                }
            });

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

                // Store single history entry for the entire folder execution
                StoreFolderExecutionHistory(folderName, results);

                int successCount = results.Count(r => r.Success);
                int failCount = results.Count - successCount;
                string status = failCount > 0
                    ? $"Completed folder '{folderName}': {successCount} succeeded, {failCount} failed"
                    : $"Completed folder '{folderName}' on {hosts.Count} hosts";
                UpdateStatusBar(status);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusBar("Execution failed");
            }
            finally
            {
                SetExecutionMode(false);
            }
        }

        private void StoreFolderExecutionHistory(string folderName, List<ExecutionResult> results)
        {
            // Build per-host history entries
            var hostResults = new List<HostHistoryEntry>();
            var combinedOutput = new StringBuilder();

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var output = result.Output;
                // Trim leading newlines only from first result
                if (i == 0)
                    output = output.TrimStart('\r', '\n');

                hostResults.Add(new HostHistoryEntry
                {
                    HostAddress = result.Host.ToString(),
                    Output = output,
                    Success = result.Success,
                    Timestamp = result.Timestamp
                });
                combinedOutput.Append(output);
            }

            string label = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {FolderIcon} {folderName}";
            var entryId = HistoryIdGenerator.NewId();
            var entry = new HistoryListItem(entryId, label, combinedOutput.ToString());

            Invoke(() =>
            {
                _outputHistory.Insert(0, entry);

                // Store host results by entry ID
                StoreHostResultsForEntry(entryId, hostResults);

                lstOutput.SelectedIndex = 0;
                SaveConfiguration();
            });
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
                    Timestamp = result.Timestamp
                });
            }

            return hostResults;
        }

        // Store host results by history entry ID
        private readonly HistoryResultStore _historyResults = new();

        private void StoreHostResultsForEntry(string entryId, List<HostHistoryEntry> hostResults)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return;

            _historyResults.SetResults(entryId, hostResults);
        }

        private List<HostHistoryEntry>? GetHostResultsForEntry(string entryId)
        {
            return _historyResults.TryGetResults(entryId, out var results) ? results : null;
        }

        private void StopExecution()
        {
            // Immediate visual feedback - disable button and change text
            btnStopAll.Enabled = false;
            btnStopAll.Text = "Stopping...";
            UpdateStatusBar("Stopping execution...");

            // Request cancellation
            _sshService.Stop();

            // Append stop message to output
            AppendOutputText(Environment.NewLine + Environment.NewLine + "Execution Stopped by User" + Environment.NewLine);
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

            Cursor = executing ? Cursors.WaitCursor : Cursors.Default;
            btnExecuteAll.Enabled = !executing;
            btnExecuteSelected.Enabled = !executing;
            btnStopAll.Visible = executing;
            lstOutput.Enabled = !executing;
            tsbOpenCsv.Enabled = !executing;
            tsbSaveCsv.Enabled = !executing;
            tsbSaveCsvAs.Enabled = !executing;
            tsbClearGrid.Enabled = !executing;

            if (executing)
            {
                // Reset stop button to initial state when starting execution
                btnStopAll.Enabled = true;
                btnStopAll.Text = "Stop";
            }
            else
            {
                statusProgress.Visible = false;
            }

            SshDebugLog("UI", $"SetExecutionMode({executing}) completed", sw);
        }

        private void SshService_OutputReceived(object? sender, SshOutputEventArgs e)
        {
            var output = e.Output;

            if (InvokeRequired)
            {
                Invoke(() => AppendOutputText(output));
            }
            else
            {
                AppendOutputText(output);
            }
        }

        private void AppendOutputText(string output)
        {
            if (string.IsNullOrEmpty(output))
                return;

            // Trim leading newlines if output buffer is empty (first banner)
            if (_outputBuffer.Length == 0)
                output = output.TrimStart('\r', '\n');

            _outputBuffer.Append(output);
            txtOutput.AppendText(output);
            ScrollOutputToEnd();
        }

        private void SetOutputText(string text)
        {
            _outputBuffer.Clear();
            if (!string.IsNullOrEmpty(text))
                _outputBuffer.Append(text);

            txtOutput.Text = text ?? string.Empty;
            ScrollOutputToEnd();
        }

        private void ClearOutput()
        {
            _outputBuffer.Clear();
            txtOutput.Clear();
        }

        private void ScrollOutputToEnd()
        {
            txtOutput.SelectionStart = txtOutput.TextLength;
            txtOutput.SelectionLength = 0;
            txtOutput.ScrollToCaret();
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

        private void StoreExecutionHistory(List<ExecutionResult> results)
        {
            // Use output buffer as the source of truth - includes all debug output
            var output = _outputBuffer.ToString();

            string label = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {txtPreset.Text}";
            var entryId = HistoryIdGenerator.NewId();
            var entry = new HistoryListItem(entryId, label, output);

            Invoke(() =>
            {
                _outputHistory.Insert(0, entry);
                var hostResults = BuildHostHistoryEntries(results);
                if (hostResults.Count > 0)
                {
                    StoreHostResultsForEntry(entryId, hostResults);
                }
                lstOutput.SelectedIndex = 0;
                SaveConfiguration();
            });
        }

        #endregion

        #region History Operations

        private void SaveHistoryEntry()
        {
            if (lstOutput.SelectedItem is not HistoryListItem entry)
            {
                MessageBox.Show("Please select an item from the list to save.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = entry.Label.Replace(":", "_")
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, entry.Output);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveAllHistory()
        {
            if (_outputHistory.Count == 0)
            {
                MessageBox.Show("There is no history to save.", "No History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"SSH_Helper_History_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using var sw = new StreamWriter(sfd.FileName, false, new UTF8Encoding(false));
                    for (int i = 0; i < _outputHistory.Count; i++)
                    {
                        var entry = _outputHistory[i];
                        sw.WriteLine($"===== {entry.Label} =====");
                        sw.WriteLine();
                        string body = (entry.Output ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
                        if (!string.IsNullOrEmpty(body)) sw.WriteLine(body);
                        if (i < _outputHistory.Count - 1) sw.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteHistoryEntry()
        {
            if (lstOutput.SelectedItem is not HistoryListItem entry)
            {
                MessageBox.Show("Please select an item from the list to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to delete {entry.Label}?", "Delete Entry", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            _outputHistory.Remove(entry);
            _historyResults.RemoveResults(entry.Id);
            if (lstOutput.Items.Count > 0)
                lstOutput.SelectedIndex = 0;
            else
                ClearOutput();
        }

        private void DeleteAllHistory()
        {
            if (MessageBox.Show("Are you sure you want to delete all history?", "Delete History", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            _outputHistory.Clear();
            _historyResults.Clear();
            ClearOutput();
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
                _configService.Update(config =>
                {
                    config.Username = tsbUsername.Text;
                    config.Timeout = InputValidator.ParseIntOrDefault(txtTimeoutHeader.Text, 10);

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
                        //MessageBox.Show($"Saving folder states: {folderStates}", "Debug - SaveConfiguration");
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
                        config.SavedState = BuildApplicationState(config.MaxHistoryEntries);
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
                MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ApplicationState BuildApplicationState(int maxHistoryEntries = 30)
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

            // Save selected preset or folder
            state.SelectedPreset = _activePresetName;
            state.SelectedFolder = _selectedFolderName;

            // Save username (not password)
            state.Username = tsbUsername.Text;

            // Save history (limited to maxHistoryEntries, keeping most recent)
            state.History = new List<HistoryEntry>();
            var historyToSave = _outputHistory.Take(maxHistoryEntries);
            foreach (var entry in historyToSave)
            {
                var entryId = string.IsNullOrWhiteSpace(entry.Id)
                    ? HistoryIdGenerator.NewId()
                    : entry.Id;

                var historyEntry = new HistoryEntry
                {
                    Id = entryId,
                    Timestamp = entry.Label,
                    Output = entry.Output
                };

                // Include per-host results if this is a folder entry
                if (_historyResults.TryGetResults(entryId, out var hostResults))
                {
                    historyEntry.HostResults = hostResults;
                }

                state.History.Add(historyEntry);
            }

            return state;
        }

        private void RestoreApplicationState(ApplicationState state)
        {
            if (state == null) return;

            // Restore hosts data
            dgv_variables.Rows.Clear();
            dgv_variables.Columns.Clear();

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

            // Restore CSV path
            _loadedFilePath = state.LastCsvPath;

            // Restore username (not password)
            if (!string.IsNullOrEmpty(state.Username))
            {
                tsbUsername.Text = state.Username;
                txtUsername.Text = state.Username;
            }

            // Restore history
            if (state.History != null && state.History.Count > 0)
            {
                _outputHistory.Clear();
                _historyResults.Clear();

                foreach (var entry in state.History)
                {
                    var entryId = string.IsNullOrWhiteSpace(entry.Id)
                        ? HistoryIdGenerator.NewId()
                        : entry.Id;
                    var label = string.IsNullOrWhiteSpace(entry.Timestamp)
                        ? entryId
                        : entry.Timestamp;

                    _outputHistory.Add(new HistoryListItem(entryId, label, entry.Output));

                    // Restore per-host results if available
                    if (entry.HostResults != null && entry.HostResults.Count > 0)
                    {
                        _historyResults.SetResults(entryId, entry.HostResults);
                    }
                }

                // Clear selection - don't auto-select any history item on load
                lstOutput.ClearSelected();
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

            UpdateHostCount();

            // Flag for auto-sizing after the form is fully visible (handled in Form1_Shown)
            _pendingColumnAutoSize = true;

            // Reset dirty flag since we're restoring saved state, not making new changes
            _csvDirty = false;
        }

        private bool ConfirmExitWorkflow()
        {
            if (_sshService.IsRunning)
            {
                if (MessageBox.Show("Execution is currently running. Stop and exit?", "Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                    return false;
                StopExecution();
            }

            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            if (_csvDirty)
            {
                var result = MessageBox.Show("You have unsaved CSV changes. Save before exiting?", "Save Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel) return false;
                if (result == DialogResult.Yes && !SaveCurrentCsv(promptIfNoPath: true)) return false;
            }

            if (IsPresetDirty())
            {
                var message = "You have unsaved preset changes. Save before exiting?";

                if (debugModeToolStripMenuItem.Checked)
                {
                    message = GetPresetDirtyDebugInfo();
                }

                var result = MessageBox.Show(message, "Save Preset", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel) return false;
                if (result == DialogResult.Yes) SaveCurrentPreset();
            }

            return true;
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
                        using var errorDialog = new UpdateErrorDialog(result.ErrorMessage);
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
                    }, config.UpdateSettings.EnableUpdateLog);
                    updateDialog.ShowDialog(this);
                }
                else
                {
                    if (!silent)
                    {
                        using var noUpdateDialog = new NoUpdateDialog(ApplicationVersion);
                        noUpdateDialog.ShowDialog(this);
                    }
                }

                UpdateStatusBar("Ready");
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    using var errorDialog = new UpdateErrorDialog(ex.Message);
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

        private void tsbPassword_Click(object sender, EventArgs e)
        {

        }
    }
}



