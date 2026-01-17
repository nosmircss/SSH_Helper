using System.ComponentModel;
using System.Data;
using System.Text;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Utilities;

namespace SSH_Helper
{
    public partial class Form1 : Form
    {
        #region Constants

        private const string ApplicationVersion = "0.42";
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
        private int _rightClickedColumnIndex = -1;
        private int _rightClickedRowIndex = -1;
        private readonly BindingList<KeyValuePair<string, string>> _outputHistory = new();

        // Find dialog state
        private FindDialog? _findDialog;
        private string _lastFindTerm = "";
        private bool _lastFindMatchCase;
        private bool _lastFindWrap = true;

        // Preset sorting
        private PresetSortMode _currentSortMode = PresetSortMode.Ascending;
        private readonly List<string> _manualPresetOrder = new();

        // Preset drag-drop state
        private int _presetDragIndex = -1;
        private Point _presetDragStartPoint;

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

            var config = _configService.GetCurrent();
            if (config.UpdateSettings.CheckOnStartup)
            {
                await CheckForUpdatesAsync(silent: true);
            }
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
            txtDelay.Text = config.Delay.ToString();
            tsbTimeout.Text = config.Timeout > 0 ? config.Timeout.ToString() : "10";
            txtTimeout.Text = tsbTimeout.Text;

            // Load sort mode and manual order
            _currentSortMode = config.PresetSortMode;
            _manualPresetOrder.Clear();
            _manualPresetOrder.AddRange(config.ManualPresetOrder);

            // Populate preset list with proper sorting
            RefreshPresetList();

            // Apply defaults to presets that don't have them
            _presetManager.ApplyDefaults(config.Delay, config.Timeout);
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

            // Preset events
            lstPreset.MouseDown += LstPreset_MouseDown;
            lstPreset.MouseMove += LstPreset_MouseMove;
            lstPreset.DragOver += LstPreset_DragOver;
            lstPreset.DragDrop += LstPreset_DragDrop;
            lstPreset.AllowDrop = true;
        }

        private void InitializeToolbarSync()
        {
            // Sync toolbar username/password with hidden textboxes
            tsbUsername.TextChanged += (s, e) => txtUsername.Text = tsbUsername.Text;
            tsbPassword.TextChanged += (s, e) => txtPassword.Text = tsbPassword.Text;
            tsbTimeout.TextChanged += (s, e) => txtTimeout.Text = tsbTimeout.Text;

            // Only allow numeric input in timeout
            tsbTimeout.KeyPress += (s, e) =>
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
            if (hit.Type == DataGridViewHitTestType.Cell || hit.Type == DataGridViewHitTestType.ColumnHeader)
            {
                _rightClickedColumnIndex = hit.ColumnIndex;
                _rightClickedRowIndex = hit.Type == DataGridViewHitTestType.Cell ? hit.RowIndex : -1;

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

        #region Preset Events

        private void LstPreset_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = lstPreset.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    lstPreset.SelectedIndex = index;
                    contextPresetLst.Show(Cursor.Position);
                }
                else
                {
                    contextPresetLstAdd.Show(Cursor.Position);
                }
            }
            else if (e.Button == MouseButtons.Left && _currentSortMode == PresetSortMode.Manual)
            {
                _presetDragIndex = lstPreset.IndexFromPoint(e.Location);
                _presetDragStartPoint = e.Location;
            }
        }

        private void LstPreset_MouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _presetDragIndex < 0 || _currentSortMode != PresetSortMode.Manual)
                return;

            // Check if mouse has moved enough to start drag
            if (Math.Abs(e.X - _presetDragStartPoint.X) > SystemInformation.DragSize.Width ||
                Math.Abs(e.Y - _presetDragStartPoint.Y) > SystemInformation.DragSize.Height)
            {
                if (_presetDragIndex < lstPreset.Items.Count)
                {
                    lstPreset.DoDragDrop(lstPreset.Items[_presetDragIndex], DragDropEffects.Move);
                }
            }
        }

        private void LstPreset_DragOver(object? sender, DragEventArgs e)
        {
            if (_currentSortMode != PresetSortMode.Manual || !e.Data?.GetDataPresent(typeof(string)) == true)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            e.Effect = DragDropEffects.Move;

            // Get the item under the cursor and highlight it
            var point = lstPreset.PointToClient(new Point(e.X, e.Y));
            int targetIndex = lstPreset.IndexFromPoint(point);
            if (targetIndex >= 0 && targetIndex < lstPreset.Items.Count)
            {
                lstPreset.SelectedIndex = targetIndex;
            }
        }

        private void LstPreset_DragDrop(object? sender, DragEventArgs e)
        {
            if (_currentSortMode != PresetSortMode.Manual || _presetDragIndex < 0)
            {
                _presetDragIndex = -1;
                return;
            }

            var point = lstPreset.PointToClient(new Point(e.X, e.Y));
            int targetIndex = lstPreset.IndexFromPoint(point);

            if (targetIndex < 0 || targetIndex == _presetDragIndex)
            {
                _presetDragIndex = -1;
                return;
            }

            // Get the preset name being moved (strip star if favorite)
            string draggedDisplayName = lstPreset.Items[_presetDragIndex].ToString() ?? "";
            string draggedPresetName = GetPresetNameFromDisplay(draggedDisplayName);

            // Build a list from the current visual order
            var currentOrder = new List<string>();
            for (int i = 0; i < lstPreset.Items.Count; i++)
            {
                currentOrder.Add(GetPresetNameFromDisplay(lstPreset.Items[i].ToString() ?? ""));
            }

            // Remove from current position and insert at new position
            currentOrder.RemoveAt(_presetDragIndex);
            currentOrder.Insert(targetIndex > _presetDragIndex ? targetIndex : targetIndex, draggedPresetName);

            // Update manual order list
            _manualPresetOrder.Clear();
            _manualPresetOrder.AddRange(currentOrder);

            _presetDragIndex = -1;
            RefreshPresetList();

            // Re-select the moved item
            for (int i = 0; i < lstPreset.Items.Count; i++)
            {
                if (GetPresetNameFromDisplay(lstPreset.Items[i].ToString() ?? "") == draggedPresetName)
                {
                    lstPreset.SelectedIndex = i;
                    break;
                }
            }

            // Save the new order
            SaveConfiguration();
        }

        private void lstPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressPresetSelectionChange || lstPreset.SelectedItem == null)
                return;

            string displayName = lstPreset.SelectedItem.ToString() ?? "";
            string newPresetName = GetPresetNameFromDisplay(displayName);

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
                    // Find the previous active preset in the list
                    for (int i = 0; i < lstPreset.Items.Count; i++)
                    {
                        if (GetPresetNameFromDisplay(lstPreset.Items[i].ToString() ?? "") == _activePresetName)
                        {
                            lstPreset.SelectedIndex = i;
                            break;
                        }
                    }
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
                txtCommand.Text = preset.Commands;
                txtPreset.Text = newPresetName;
                if (preset.Delay.HasValue) txtDelay.Text = preset.Delay.Value.ToString();
                if (preset.Timeout.HasValue)
                {
                    txtTimeout.Text = preset.Timeout.Value.ToString();
                    tsbTimeout.Text = preset.Timeout.Value.ToString();
                }
            }

            _activePresetName = newPresetName;
        }

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
            ExecuteOnAllHosts();
        }

        private void btnExecuteSelected_Click(object sender, EventArgs e)
        {
            ExecuteOnSelectedHost();
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
            dialog.ShowDialog(this);
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

            // When switching to manual mode, initialize the order from current visual order
            if (_currentSortMode == PresetSortMode.Manual && _manualPresetOrder.Count == 0)
            {
                // Build order from current list display
                for (int i = 0; i < lstPreset.Items.Count; i++)
                {
                    string name = GetPresetNameFromDisplay(lstPreset.Items[i].ToString() ?? "");
                    if (!string.IsNullOrEmpty(name))
                        _manualPresetOrder.Add(name);
                }
            }

            RefreshPresetList();
            UpdateSortModeIndicator();
            UpdateStatusBar($"Sort mode: {_currentSortMode}");
        }

        private void UpdateSortModeIndicator()
        {
            string indicator = _currentSortMode switch
            {
                PresetSortMode.Ascending => "Sort \u2191",   // ↑
                PresetSortMode.Descending => "Sort \u2193", // ↓
                PresetSortMode.Manual => "Sort \u2195",     // ↕
                _ => "Sort"
            };
            tsbSortPresets.Text = indicator;
            tsbSortPresets.ToolTipText = $"Sort mode: {_currentSortMode}";
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

        private void lstOutput_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstOutput.SelectedItem is KeyValuePair<string, string> entry)
            {
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
            string presetName = txtPreset.Text.Trim();
            string commands = txtCommand.Text;

            if (string.IsNullOrEmpty(presetName))
            {
                MessageBox.Show("Preset name is required.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Preserve existing IsFavorite status if updating
            var existingPreset = _presetManager.Get(presetName);
            var preset = new PresetInfo
            {
                Commands = commands,
                Delay = int.TryParse(txtDelay.Text, out var d) ? d : null,
                Timeout = int.TryParse(tsbTimeout.Text, out var t) ? t : null,
                IsFavorite = existingPreset?.IsFavorite ?? false
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

            _presetManager.Save(presetName, new PresetInfo());

            // Add to manual order
            if (!_manualPresetOrder.Contains(presetName))
            {
                _manualPresetOrder.Add(presetName);
            }

            RefreshPresetList();
            lstPreset.SelectedItem = presetName;
        }

        private void DuplicatePreset()
        {
            if (lstPreset.SelectedItem == null) return;

            string sourceName = lstPreset.SelectedItem.ToString() ?? "";
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
                lstPreset.SelectedItem = finalName;

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
            if (lstPreset.SelectedItem == null) return;

            string selectedPreset = lstPreset.SelectedItem.ToString() ?? "";
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
            lstPreset.SelectedItem = newName;
            txtPreset.Text = newName;
            _activePresetName = newName;
        }

        private void DeletePreset()
        {
            if (lstPreset.SelectedItem == null) return;

            int selectedIndex = lstPreset.SelectedIndex;
            string selectedPreset = lstPreset.SelectedItem.ToString() ?? "";

            // Check if this is the currently active preset being deleted
            bool isDeletingActivePreset = string.Equals(selectedPreset, _activePresetName, StringComparison.Ordinal);

            if (_presetManager.Delete(selectedPreset))
            {
                lstPreset.Items.Remove(selectedPreset);
                _manualPresetOrder.Remove(selectedPreset);

                // Clear active preset if we deleted it (prevents "save changes?" prompt)
                if (isDeletingActivePreset)
                {
                    _activePresetName = null;
                    txtPreset.Clear();
                    txtCommand.Clear();
                }

                if (lstPreset.Items.Count > 0)
                {
                    _suppressPresetSelectionChange = true;
                    lstPreset.SelectedIndex = selectedIndex > 0 ? selectedIndex - 1 : 0;
                    _suppressPresetSelectionChange = false;

                    // Load the newly selected preset
                    var newSelection = lstPreset.SelectedItem?.ToString();
                    if (!string.IsNullOrEmpty(newSelection))
                    {
                        var preset = _presetManager.Get(newSelection);
                        if (preset != null)
                        {
                            txtPreset.Text = newSelection;
                            txtCommand.Text = preset.Commands;
                            _activePresetName = newSelection;
                        }
                    }
                }
            }
        }

        private void ExportPreset()
        {
            if (lstPreset.SelectedItem == null)
            {
                MessageBox.Show("No preset selected to export.", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string presetName = lstPreset.SelectedItem.ToString() ?? "";
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
                int? defaultDelay = int.TryParse(txtDelay.Text, out var d) ? d : null;
                int? defaultTimeout = int.TryParse(tsbTimeout.Text, out var t) ? t : null;

                string finalName = _presetManager.Import(input, defaultDelay, defaultTimeout);

                lstPreset.Items.Add(finalName);
                lstPreset.SelectedItem = finalName;

                var preset = _presetManager.Get(finalName);
                if (preset != null)
                {
                    txtPreset.Text = finalName;
                    txtCommand.Text = preset.Commands;
                    if (preset.Delay.HasValue) txtDelay.Text = preset.Delay.Value.ToString();
                    if (preset.Timeout.HasValue) tsbTimeout.Text = preset.Timeout.Value.ToString();
                }

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

        private void RefreshPresetList()
        {
            string? currentSelection = lstPreset.SelectedItem?.ToString();
            _suppressPresetSelectionChange = true;

            lstPreset.Sorted = false;
            lstPreset.Items.Clear();

            // Get all presets with their info
            var presets = _presetManager.Presets.ToList();

            // Sort presets based on current mode, always with favorites first
            IEnumerable<string> sortedPresets;

            // Separate favorites and non-favorites
            var favorites = presets.Where(p => p.Value.IsFavorite).Select(p => p.Key);
            var nonFavorites = presets.Where(p => !p.Value.IsFavorite).Select(p => p.Key);

            switch (_currentSortMode)
            {
                case PresetSortMode.Ascending:
                    sortedPresets = favorites.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .Concat(nonFavorites.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
                    break;

                case PresetSortMode.Descending:
                    sortedPresets = favorites.OrderByDescending(n => n, StringComparer.OrdinalIgnoreCase)
                        .Concat(nonFavorites.OrderByDescending(n => n, StringComparer.OrdinalIgnoreCase));
                    break;

                case PresetSortMode.Manual:
                    // Use manual order exactly as specified (no favorites separation in manual mode)
                    var orderedList = new List<string>();

                    foreach (var name in _manualPresetOrder)
                    {
                        if (_presetManager.Presets.ContainsKey(name))
                        {
                            orderedList.Add(name);
                        }
                    }

                    // Add any presets not in manual order at the end
                    foreach (var kvp in presets)
                    {
                        if (!_manualPresetOrder.Contains(kvp.Key))
                        {
                            orderedList.Add(kvp.Key);
                        }
                    }

                    sortedPresets = orderedList;
                    break;

                default:
                    sortedPresets = presets.Select(p => p.Key);
                    break;
            }

            foreach (var name in sortedPresets)
            {
                var preset = _presetManager.Get(name);
                string displayName = preset?.IsFavorite == true ? $"★ {name}" : name;
                lstPreset.Items.Add(displayName);
            }

            // Restore selection
            if (!string.IsNullOrEmpty(currentSelection))
            {
                // Find the item (may have star prefix now)
                for (int i = 0; i < lstPreset.Items.Count; i++)
                {
                    string item = lstPreset.Items[i].ToString() ?? "";
                    string itemName = item.StartsWith("★ ") ? item.Substring(2) : item;
                    if (itemName == currentSelection || item == currentSelection)
                    {
                        lstPreset.SelectedIndex = i;
                        break;
                    }
                }
            }

            _suppressPresetSelectionChange = false;
        }

        private void ToggleFavorite()
        {
            if (lstPreset.SelectedItem == null) return;

            string displayName = lstPreset.SelectedItem.ToString() ?? "";
            string presetName = displayName.StartsWith("★ ") ? displayName.Substring(2) : displayName;

            var preset = _presetManager.Get(presetName);
            if (preset == null) return;

            preset.IsFavorite = !preset.IsFavorite;
            _presetManager.Save(presetName, preset);

            RefreshPresetList();

            // Re-select the item
            for (int i = 0; i < lstPreset.Items.Count; i++)
            {
                string item = lstPreset.Items[i].ToString() ?? "";
                string itemName = item.StartsWith("★ ") ? item.Substring(2) : item;
                if (itemName == presetName)
                {
                    lstPreset.SelectedIndex = i;
                    break;
                }
            }

            UpdateStatusBar(preset.IsFavorite ? $"'{presetName}' added to favorites" : $"'{presetName}' removed from favorites");
        }

        private string GetPresetNameFromDisplay(string displayName)
        {
            return displayName.StartsWith("★ ") ? displayName.Substring(2) : displayName;
        }

        private bool IsPresetDirty()
        {
            if (string.IsNullOrEmpty(_activePresetName)) return InputValidator.IsNotEmpty(txtPreset.Text) || InputValidator.IsNotEmpty(txtCommand.Text);

            var preset = _presetManager.Get(_activePresetName);
            if (preset == null) return InputValidator.IsNotEmpty(txtPreset.Text) || InputValidator.IsNotEmpty(txtCommand.Text);

            bool nameChanged = !string.Equals(txtPreset.Text?.Trim(), _activePresetName, StringComparison.Ordinal);
            bool commandsChanged = !string.Equals(txtCommand.Text, preset.Commands ?? "", StringComparison.Ordinal);

            bool delayDiffers = int.TryParse(txtDelay.Text, out var d)
                ? preset.Delay != d
                : preset.Delay.HasValue;

            bool timeoutDiffers = int.TryParse(tsbTimeout.Text, out var t)
                ? preset.Timeout != t
                : preset.Timeout.HasValue;

            return nameChanged || commandsChanged || delayDiffers || timeoutDiffers;
        }

        #endregion

        #region SSH Execution

        private async void ExecuteOnAllHosts()
        {
            if (_sshService.IsRunning) return;

            SetExecutionMode(true);
            txtOutput.Clear();

            var hosts = GetHostConnections(dgv_variables.Rows.Cast<DataGridViewRow>()).ToList();
            string[] commands = txtCommand.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            int timeout = InputValidator.ParseIntOrDefault(tsbTimeout.Text, 10);

            UpdateStatusBar($"Executing on {hosts.Count} hosts...", true, 0, hosts.Count);

            try
            {
                var results = await _sshService.ExecuteAsync(hosts, commands, tsbUsername.Text, tsbPassword.Text, timeout);
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
            string[] commands = txtCommand.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            int timeout = InputValidator.ParseIntOrDefault(tsbTimeout.Text, 10);

            UpdateStatusBar($"Executing on {host}...", true, 0, 1);

            try
            {
                var results = await _sshService.ExecuteAsync(hosts, commands, tsbUsername.Text, tsbPassword.Text, timeout);
                StoreExecutionHistory(results);
                UpdateStatusBar($"Completed execution on {host}");
            }
            finally
            {
                SetExecutionMode(false);
            }
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
                case Keys.Control | Keys.F:
                    ShowFindDialog();
                    return true;
                case Keys.F3:
                    PerformFind(_lastFindTerm, _lastFindMatchCase, true, _lastFindWrap, false);
                    return true;
                case Keys.Shift | Keys.F3:
                    PerformFind(_lastFindTerm, _lastFindMatchCase, false, _lastFindWrap, false);
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
                _findDialog = new FindDialog(this, seed, _lastFindMatchCase, _lastFindWrap);
                var screenPoint = txtOutput.PointToScreen(Point.Empty);
                _findDialog.StartPosition = FormStartPosition.Manual;
                _findDialog.Left = screenPoint.X + 40;
                _findDialog.Top = screenPoint.Y + 40;
            }

            _findDialog.Show();
            _findDialog.BringToFront();
        }

        internal void FindNextFromDialog(string term, bool matchCase, bool wrap)
        {
            if (string.IsNullOrEmpty(term))
            {
                _findDialog?.SetStatus("Enter text to find.", true);
                return;
            }
            _lastFindTerm = term;
            _lastFindMatchCase = matchCase;
            _lastFindWrap = wrap;

            bool found = PerformFind(term, matchCase, true, wrap, true);
            _findDialog?.SetStatus(found ? "Found." : "Not found.", !found);
        }

        internal void FindPreviousFromDialog(string term, bool matchCase, bool wrap)
        {
            if (string.IsNullOrEmpty(term))
            {
                _findDialog?.SetStatus("Enter text to find.", true);
                return;
            }
            _lastFindTerm = term;
            _lastFindMatchCase = matchCase;
            _lastFindWrap = wrap;

            bool found = PerformFind(term, matchCase, false, wrap, true);
            _findDialog?.SetStatus(found ? "Found." : "Not found.", !found);
        }

        private bool PerformFind(string term, bool matchCase, bool forward, bool wrap, bool fromDialog)
        {
            if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(txtOutput.Text))
                return false;

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string text = txtOutput.Text;

            int idx;
            if (forward)
            {
                int start = txtOutput.SelectionStart + txtOutput.SelectionLength;
                if (start > text.Length) start = text.Length;

                idx = text.IndexOf(term, start, comparison);
                if (idx == -1 && wrap)
                    idx = text.IndexOf(term, 0, comparison);
            }
            else
            {
                int start = txtOutput.SelectionStart - 1;
                if (start < 0) start = text.Length - 1;

                idx = LastIndexOf(text, term, start, comparison);
                if (idx == -1 && wrap)
                    idx = LastIndexOf(text, term, text.Length - 1, comparison);
            }

            if (idx == -1) return false;

            HighlightAndScroll(idx, term.Length, fromDialog);
            return true;
        }

        private static int LastIndexOf(string source, string term, int startIndex, StringComparison comparison)
        {
            if (startIndex < 0 || string.IsNullOrEmpty(term)) return -1;
            int lastPossible = startIndex - term.Length + 1;
            for (int i = lastPossible; i >= 0; i--)
            {
                if (string.Compare(source, i, term, 0, term.Length, comparison) == 0)
                    return i;
            }
            return -1;
        }

        private void HighlightAndScroll(int index, int length, bool fromDialog)
        {
            try
            {
                txtOutput.SelectionStart = index;
                txtOutput.SelectionLength = length;
                txtOutput.ScrollToCaret();

                if (fromDialog && _findDialog is { IsDisposed: false, Visible: true })
                {
                    _findDialog.Activate();
                }
                else
                {
                    txtOutput.Focus();
                }
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
                    config.Delay = InputValidator.ParseIntOrDefault(txtDelay.Text, 500);
                    config.Timeout = InputValidator.ParseIntOrDefault(tsbTimeout.Text, 10);

                    // Save sort mode and manual order
                    config.PresetSortMode = _currentSortMode;
                    config.ManualPresetOrder = new List<string>(_manualPresetOrder);

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
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                var result = MessageBox.Show("You have unsaved preset changes. Save before exiting?", "Save Preset", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
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
                    });
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
    }
}
