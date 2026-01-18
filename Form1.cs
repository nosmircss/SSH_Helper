using System.ComponentModel;
using System.Data;
using System.Text;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Utilities;

namespace SSH_Helper
{
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

        private const string ApplicationVersion = "0.50.0";
        private const string ApplicationName = "SSH Helper";

        #endregion

        #region Services

        private readonly ConfigurationService _configService;
        private readonly PresetManager _presetManager;
        private readonly CsvManager _csvManager;
        private readonly SshExecutionService _sshService;
        private readonly UpdateService _updateService;

        #endregion

        #region State

        private string? _loadedFilePath;
        private string? _activePresetName;
        private bool _csvDirty;
        private bool _exitConfirmed;
        private bool _suppressPresetSelectionChange;
        private bool _suppressExpandCollapseEvents;
        private int _rightClickedColumnIndex = -1;
        private int _rightClickedRowIndex = -1;
        private readonly BindingList<KeyValuePair<string, string>> _outputHistory = new();

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

        // Track which TreeView triggered the context menu
        private TreeView? _contextMenuSourceTreeView;

        #endregion

        #region Constructor

        public Form1()
        {
            InitializeComponent();
            Text = $"{ApplicationName} {ApplicationVersion}";

            // Initialize services
            _configService = new ConfigurationService();
            _presetManager = new PresetManager(_configService);
            _csvManager = new CsvManager();
            _sshService = new SshExecutionService();

            // Wire up SSH service events
            _sshService.OutputReceived += SshService_OutputReceived;
            _sshService.ColumnUpdateRequested += SshService_ColumnUpdateRequested;

            // Initialize update service
            var config = _configService.Load();
            _updateService = new UpdateService(
                config.UpdateSettings.GitHubOwner,
                config.UpdateSettings.GitHubRepo,
                ApplicationVersion);

            InitializeFromConfiguration();
            InitializeDataGridView();
            InitializeOutputHistory();
            InitializeEventHandlers();
            InitializeToolbarSync();
            InitializePasswordMasking();
            RestoreWindowState();
            UpdateHostCount();
            UpdateSortModeIndicator();
            UpdateStatusBar("Ready");

            // Check for updates on startup (after form is shown)
            Shown += Form1_Shown;
        }

        private async void Form1_Shown(object? sender, EventArgs e)
        {
            // Remove handler to only run once
            Shown -= Form1_Shown;

            // Restore folder expand/collapse state after form is fully shown
            RestoreFolderExpandState();

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
            RefreshPresetList();

            // Apply defaults to presets that don't have them
            _presetManager.ApplyDefaults(config.Timeout);
        }

        private void InitializeDataGridView()
        {
            dgv_variables.Columns.Add(CsvManager.HostColumnName, CsvManager.HostColumnName);

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
            dgv_variables.RowTemplate.Height = 28;

            dgv_variables.ColumnHeadersVisible = true;
            dgv_variables.RowHeadersVisible = true;
        }

        private void InitializeOutputHistory()
        {
            lstOutput.DataSource = _outputHistory;
            lstOutput.DisplayMember = "Key";
        }

        private void InitializeEventHandlers()
        {
            // Form events
            FormClosing += Form1_FormClosing;

            // DataGridView events
            dgv_variables.MouseDown += Dgv_Variables_MouseDown;
            dgv_variables.RowPostPaint += Dgv_Variables_RowPostPaint;
            dgv_variables.CellClick += Dgv_Variables_CellClick;
            dgv_variables.ColumnAdded += Dgv_Variables_ColumnAdded;
            dgv_variables.CellLeave += Dgv_Variables_CellLeave;
            dgv_variables.CellValueChanged += Dgv_Variables_CellValueChanged;
            dgv_variables.RowsAdded += Dgv_Variables_RowsAdded;
            dgv_variables.RowsRemoved += Dgv_Variables_RowsRemoved;
            dgv_variables.ColumnRemoved += Dgv_Variables_ColumnRemoved;
            dgv_variables.KeyPress += Dgv_Variables_KeyPress;
            dgv_variables.KeyDown += Dgv_Variables_KeyDown;

            // Preset TreeView events are wired up in Designer
            trvPresets.NodeMouseClick += TrvPresets_NodeMouseClick;
            contextPresetLst.Opening += ContextPresetLst_Opening;

            // History and host list right-click selection
            lstOutput.MouseDown += LstOutput_MouseDown;
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
                EndEditAndClearSelection();
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

                // Enable/disable delete/rename based on Host_IP protection
                bool isProtected = IsHostIpColumn(_rightClickedColumnIndex);
                deleteColumnToolStripMenuItem.Enabled = !isProtected;
                renameColumnToolStripMenuItem.Enabled = !isProtected;

                contextMenuStrip1.Show(dgv_variables, location);
            }
            else
            {
                _rightClickedColumnIndex = -1;
                _rightClickedRowIndex = -1;
                deleteColumnToolStripMenuItem.Enabled = true;
                renameColumnToolStripMenuItem.Enabled = true;
            }
        }

        private bool IsHostIpColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= dgv_variables.Columns.Count)
                return false;

            var col = dgv_variables.Columns[columnIndex];
            return string.Equals(col.Name, CsvManager.HostColumnName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(col.HeaderText, CsvManager.HostColumnName, StringComparison.OrdinalIgnoreCase);
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

        private void Dgv_Variables_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1) // Column header click
            {
                dgv_variables.ClearSelection();
                foreach (DataGridViewRow row in dgv_variables.Rows)
                {
                    row.Cells[e.ColumnIndex].Selected = true;
                }
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
            _csvDirty = true;
            UpdateHostCount();
        }

        private void Dgv_Variables_RowsAdded(object? sender, DataGridViewRowsAddedEventArgs e)
        {
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
                txtPreset.Text = $"📁 {tag.Name}";
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
                RestoreFolderExpandState();
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
                    var folderNode = new TreeNode($"📁 {folderName}")
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
                txtPreset.Text = $"📁 {tag.Name}";
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
            if (e.Button == MouseButtons.Right)
            {
                var hitInfo = trvFavorites.HitTest(e.Location);
                if (hitInfo.Node != null)
                {
                    trvFavorites.SelectedNode = hitInfo.Node;
                }
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
                        RestoreFolderExpandState();
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
                ExecuteFolderPresetsOnSelectedHost(folderName);
            }
            else
            {
                ExecuteOnSelectedHost();
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
            using var dialog = new SettingsDialog(_configService);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // Settings saved - default timeout only applies to new presets
                // Don't update the current timeout field as it's preset-specific
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

        private void debugModeToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            _sshService.DebugMode = debugModeToolStripMenuItem.Checked;
            UpdateStatusBar(debugModeToolStripMenuItem.Checked ? "Debug mode enabled" : "Debug mode disabled");
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
            RestoreFolderExpandState();
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
            if (lstOutput.SelectedItem is KeyValuePair<string, string> entry)
            {
                // Check if this is a folder history entry (contains folder emoji)
                bool isFolderEntry = entry.Key.Contains("📁");

                if (isFolderEntry)
                {
                    // Try to get per-host results from in-memory cache or from saved state
                    _currentHostResults = GetHostResultsForEntry(entry.Key);

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
                }

                // For non-folder entries or folder entries without per-host data,
                // hide the host list and show combined output
                historySplitContainer.Panel2Collapsed = true;
                lstHosts.Items.Clear();
                _currentHostResults = null;
                txtOutput.Text = entry.Value;
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
                txtOutput.Text = hostEntry.Output;
            }
        }

        private void lstHosts_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();

            var item = lstHosts.Items[e.Index];
            if (item is HostHistoryEntry hostEntry)
            {
                // Draw status icon
                var iconRect = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top + 2, 16, 16);
                var iconColor = hostEntry.Success ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69);
                var iconText = hostEntry.Success ? "✓" : "✗";

                using var iconFont = new Font("Segoe UI", 10F, FontStyle.Bold);
                using var iconBrush = new SolidBrush(iconColor);
                e.Graphics.DrawString(iconText, iconFont, iconBrush, iconRect.Left, iconRect.Top - 1);

                // Draw host address
                var textRect = new Rectangle(e.Bounds.Left + 24, e.Bounds.Top, e.Bounds.Width - 28, e.Bounds.Height);
                var textColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                    ? SystemColors.HighlightText
                    : e.ForeColor;
                using var textBrush = new SolidBrush(textColor);
                e.Graphics.DrawString(hostEntry.HostAddress, e.Font ?? lstHosts.Font, textBrush, textRect, StringFormat.GenericDefault);
            }

            e.DrawFocusRectangle();
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
                    _csvDirty = false;
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
            int nextNumber = dgv_variables.Columns.Count + 1;
            string defaultName = $"Column{nextNumber}";

            string columnName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the name of the new column:",
                "Add Column",
                defaultName);

            columnName = InputValidator.SanitizeColumnName(columnName);

            if (string.IsNullOrEmpty(columnName)) return;

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

            newName = InputValidator.SanitizeColumnName(newName);

            if (string.IsNullOrEmpty(newName) || newName == currentName) return;

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

            if (IsHostIpColumn(columnIndex))
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
            dgv_variables.ClearSelection();
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
            if (presetName.StartsWith("📁"))
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
                RestoreFolderExpandState();
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
            RestoreFolderExpandState();
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
                RestoreFolderExpandState();
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
            RestoreFolderExpandState();
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
                RestoreFolderExpandState();

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
                RestoreFolderExpandState();
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
                RestoreFolderExpandState();
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

        private void RefreshPresetList()
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
                string folderDisplay = folderInfo?.IsFavorite == true ? $"★ 📁 {folderName}" : $"📁 {folderName}";
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
                    string displayName = preset?.IsFavorite == true ? $"★ {presetName}" : presetName;
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
                var preset = _presetManager.Get(presetName);
                string displayName = preset?.IsFavorite == true ? $"★ {presetName}" : presetName;
                var presetNode = new TreeNode(displayName)
                {
                    Tag = new PresetNodeTag { IsFolder = false, Name = presetName }
                };
                trvPresets.Nodes.Add(presetNode);
            }

            trvPresets.EndUpdate();

            // Note: Folder expand/collapse state is restored in Form1_Shown -> RestoreFolderExpandState()
            // because expanding nodes before the form is visible doesn't work reliably in WinForms

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
            sb.AppendLine($"═══════════════════════════════════════════");
            sb.AppendLine($"  FOLDER: {folderName}");
            sb.AppendLine($"═══════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"  Presets: {presetNames.Count}");
            sb.AppendLine();

            if (presetNames.Count > 0)
            {
                sb.AppendLine("  Contents:");
                sb.AppendLine("  ─────────");
                foreach (var name in presetNames)
                {
                    var preset = _presetManager.Get(name);
                    var favorite = preset?.IsFavorite == true ? "★ " : "  ";
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
            if (!string.IsNullOrEmpty(_selectedFolderName))
            {
                int count = _presetManager.GetPresetsInFolder(_selectedFolderName).Count();
                btnExecuteAll.Text = $"Run 📁 {_selectedFolderName} ({count})";
                btnExecuteSelected.Text = $"Run Selected 📁";
            }
            else
            {
                btnExecuteAll.Text = "Run All";
                btnExecuteSelected.Text = "Run Selected";
            }

            // Reposition buttons based on text width
            using (var g = btnExecuteSelected.CreateGraphics())
            {
                var selectedSize = g.MeasureString(btnExecuteSelected.Text, btnExecuteSelected.Font);
                btnExecuteSelected.Width = (int)selectedSize.Width + 40;

                var allSize = g.MeasureString(btnExecuteAll.Text, btnExecuteAll.Font);
                btnExecuteAll.Width = (int)allSize.Width + 40;
                btnExecuteAll.Left = btnExecuteSelected.Right + 8;
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
            RestoreFolderExpandState();
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
            RestoreFolderExpandState();
            RefreshFavoritesList();
            SelectFolderByName(folderName);

            UpdateStatusBar(newFavoriteState ? $"Folder '{folderName}' added to favorites" : $"Folder '{folderName}' removed from favorites");
        }

        private string GetPresetNameFromDisplay(string displayName)
        {
            return displayName.StartsWith("★ ") ? displayName.Substring(2) : displayName;
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

        private async void ExecuteOnAllHosts()
        {
            if (_sshService.IsRunning) return;

            SetExecutionMode(true);
            txtOutput.Clear();

            var hosts = GetHostConnections(dgv_variables.Rows.Cast<DataGridViewRow>()).ToList();
            int commandTimeout = InputValidator.ParseIntOrDefault(txtTimeoutHeader.Text, 10);
            int connectionTimeout = _configService.GetCurrent().ConnectionTimeout;
            var timeouts = SshTimeoutOptions.Create(commandTimeout, connectionTimeout);

            // Create a preset from the current command text (supports both simple commands and YAML scripts)
            var preset = new PresetInfo { Commands = txtCommand.Text };

            UpdateStatusBar($"Executing on {hosts.Count} hosts...", true, 0, hosts.Count);

            try
            {
                var results = await _sshService.ExecutePresetAsync(hosts, preset, tsbUsername.Text, tsbPassword.Text, timeouts);
                StoreExecutionHistory(results);
                UpdateStatusBar($"Completed execution on {results.Count} hosts");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
                UpdateStatusBar("Execution failed");
            }
            finally
            {
                SetExecutionMode(false);
            }
        }

        private async void ExecuteOnSelectedHost()
        {
            if (dgv_variables.CurrentCell == null)
            {
                txtOutput.Clear();
                txtOutput.AppendText("No host selected");
                return;
            }

            var row = dgv_variables.Rows[dgv_variables.CurrentCell.RowIndex];
            string host = GetCellValue(row, CsvManager.HostColumnName);

            if (row.IsNewRow || string.IsNullOrWhiteSpace(host) || !InputValidator.IsValidIpAddress(host))
            {
                txtOutput.Clear();
                txtOutput.AppendText("No valid host selected");
                return;
            }

            SetExecutionMode(true);
            txtOutput.Clear();

            var hosts = GetHostConnections(new[] { row }).ToList();
            int commandTimeout = InputValidator.ParseIntOrDefault(txtTimeoutHeader.Text, 10);
            int connectionTimeout = _configService.GetCurrent().ConnectionTimeout;
            var timeouts = SshTimeoutOptions.Create(commandTimeout, connectionTimeout);

            // Create a preset from the current command text (supports both simple commands and YAML scripts)
            var preset = new PresetInfo { Commands = txtCommand.Text };

            UpdateStatusBar($"Executing on {host}...", true, 0, 1);

            try
            {
                var results = await _sshService.ExecutePresetAsync(hosts, preset, tsbUsername.Text, tsbPassword.Text, timeouts);
                StoreExecutionHistory(results);
                UpdateStatusBar($"Completed execution on {host}");
            }
            finally
            {
                SetExecutionMode(false);
            }
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
                txtOutput.Clear();
                txtOutput.AppendText("No host selected");
                return;
            }

            var row = dgv_variables.Rows[dgv_variables.CurrentCell.RowIndex];
            string host = GetCellValue(row, CsvManager.HostColumnName);

            if (row.IsNewRow || string.IsNullOrWhiteSpace(host) || !InputValidator.IsValidIpAddress(host))
            {
                txtOutput.Clear();
                txtOutput.AppendText("No valid host selected");
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

        private async Task ExecuteFolderWithOptionsAsync(string folderName, FolderExecutionOptions options, List<DataGridViewRow> hostRows)
        {
            var config = _configService.Load();
            int connectionTimeout = config.ConnectionTimeout;

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
            txtOutput.Clear();

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

            foreach (var result in results)
            {
                hostResults.Add(new HostHistoryEntry
                {
                    HostAddress = result.Host.ToString(),
                    Output = result.Output,
                    Success = result.Success,
                    Timestamp = result.Timestamp
                });
                combinedOutput.Append(result.Output);
            }

            string key = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 📁 {folderName}";
            var entry = new KeyValuePair<string, string>(key, combinedOutput.ToString());

            Invoke(() =>
            {
                _outputHistory.Insert(0, entry);

                // Store host results in a way that can be retrieved when this entry is selected
                // We'll use a parallel list that matches _outputHistory indices
                StoreHostResultsForEntry(0, hostResults);

                lstOutput.SelectedIndex = 0;
                SaveConfiguration();
            });
        }

        // Dictionary to store host results by history entry key
        private readonly Dictionary<string, List<HostHistoryEntry>> _hostResultsByHistoryKey = [];

        private void StoreHostResultsForEntry(int index, List<HostHistoryEntry> hostResults)
        {
            if (index >= 0 && index < _outputHistory.Count)
            {
                var key = _outputHistory[index].Key;
                _hostResultsByHistoryKey[key] = hostResults;
            }
        }

        private List<HostHistoryEntry>? GetHostResultsForEntry(string key)
        {
            return _hostResultsByHistoryKey.TryGetValue(key, out var results) ? results : null;
        }

        private void StopExecution()
        {
            _sshService.Stop();
            Invoke(() =>
            {
                Thread.Sleep(300);
                btnStopAll.Visible = false;
                txtOutput.AppendText(Environment.NewLine + Environment.NewLine + "Execution Stopped by User" + Environment.NewLine);
                UpdateStatusBar("Execution stopped by user");
            });
        }

        private IEnumerable<HostConnection> GetHostConnections(IEnumerable<DataGridViewRow> rows)
        {
            foreach (var row in rows)
            {
                if (row.IsNewRow) continue;

                string hostIp = GetCellValue(row, CsvManager.HostColumnName);
                if (string.IsNullOrWhiteSpace(hostIp) || !InputValidator.IsValidIpAddress(hostIp))
                    continue;

                var host = HostConnection.Parse(hostIp);
                host.Username = GetCellValue(row, "username");
                host.Password = GetCellValue(row, "password");

                // Collect all variables from the row
                foreach (DataGridViewColumn col in dgv_variables.Columns)
                {
                    host.Variables[col.Name] = row.Cells[col.Index].Value?.ToString() ?? "";
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
            Cursor = executing ? Cursors.WaitCursor : Cursors.Default;
            btnExecuteAll.Enabled = !executing;
            btnExecuteSelected.Enabled = !executing;
            btnStopAll.Visible = executing;
            lstOutput.Enabled = !executing;
            tsbOpenCsv.Enabled = !executing;
            tsbSaveCsv.Enabled = !executing;
            tsbSaveCsvAs.Enabled = !executing;
            tsbClearGrid.Enabled = !executing;

            if (!executing)
            {
                statusProgress.Visible = false;
            }
        }

        private void SshService_OutputReceived(object? sender, SshOutputEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(() => txtOutput.AppendText(e.Output));
            }
            else
            {
                txtOutput.AppendText(e.Output);
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
            var combinedOutput = new StringBuilder();
            foreach (var result in results)
            {
                combinedOutput.Append(result.Output);
            }

            string key = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {txtPreset.Text}";
            var entry = new KeyValuePair<string, string>(key, combinedOutput.ToString());

            Invoke(() =>
            {
                _outputHistory.Insert(0, entry);
                lstOutput.SelectedIndex = 0;
                SaveConfiguration();
            });
        }

        #endregion

        #region History Operations

        private void SaveHistoryEntry()
        {
            if (lstOutput.SelectedItem is not KeyValuePair<string, string> entry)
            {
                MessageBox.Show("Please select an item from the list to save.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = entry.Key.Replace(":", "_")
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, entry.Value);
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
                        sw.WriteLine($"===== {entry.Key} =====");
                        sw.WriteLine();
                        string body = (entry.Value ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
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
            if (lstOutput.SelectedItem is not KeyValuePair<string, string> entry)
            {
                MessageBox.Show("Please select an item from the list to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to delete {entry.Key}?", "Delete Entry", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            _outputHistory.Remove(entry);
            if (lstOutput.Items.Count > 0)
                lstOutput.SelectedIndex = 0;
            else
                txtOutput.Clear();
        }

        private void DeleteAllHistory()
        {
            if (MessageBox.Show("Are you sure you want to delete all history?", "Delete History", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            _outputHistory.Clear();
            txtOutput.Clear();
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
                        MessageBox.Show($"Saving folder states: {folderStates}", "Debug - SaveConfiguration");
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ApplicationState BuildApplicationState(int maxHistoryEntries = 30)
        {
            var state = new ApplicationState();

            // Save hosts data
            state.HostColumns = new List<string>();
            for (int i = 0; i < dgv_variables.Columns.Count; i++)
            {
                state.HostColumns.Add(dgv_variables.Columns[i].Name);
            }

            state.Hosts = new List<Dictionary<string, string>>();
            for (int row = 0; row < dgv_variables.Rows.Count; row++)
            {
                if (dgv_variables.Rows[row].IsNewRow) continue;

                var rowData = new Dictionary<string, string>();
                for (int col = 0; col < dgv_variables.Columns.Count; col++)
                {
                    var colName = dgv_variables.Columns[col].Name;
                    var value = dgv_variables.Rows[row].Cells[col].Value?.ToString() ?? "";
                    rowData[colName] = value;
                }
                state.Hosts.Add(rowData);
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
            foreach (var kvp in historyToSave)
            {
                var historyEntry = new HistoryEntry
                {
                    Timestamp = kvp.Key,
                    Output = kvp.Value
                };

                // Include per-host results if this is a folder entry
                if (_hostResultsByHistoryKey.TryGetValue(kvp.Key, out var hostResults))
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
                    dgv_variables.Columns.Add(colName, colName);
                }

                if (state.Hosts != null)
                {
                    foreach (var rowData in state.Hosts)
                    {
                        var rowIndex = dgv_variables.Rows.Add();
                        foreach (var kvp in rowData)
                        {
                            if (dgv_variables.Columns.Contains(kvp.Key))
                            {
                                dgv_variables.Rows[rowIndex].Cells[kvp.Key].Value = kvp.Value;
                            }
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
                _hostResultsByHistoryKey.Clear();

                foreach (var entry in state.History)
                {
                    _outputHistory.Add(new KeyValuePair<string, string>(entry.Timestamp, entry.Output));

                    // Restore per-host results if available
                    if (entry.HostResults != null && entry.HostResults.Count > 0)
                    {
                        _hostResultsByHistoryKey[entry.Timestamp] = entry.HostResults;
                    }
                }

                // Select first history entry and display its output
                if (_outputHistory.Count > 0)
                {
                    lstOutput.SelectedIndex = 0;
                    // The SelectedIndexChanged handler will take care of showing output
                    // and showing the host list if applicable
                }
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

        #region Helpers

        private void EndEditAndClearSelection()
        {
            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();
            dgv_variables.ClearSelection();
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
