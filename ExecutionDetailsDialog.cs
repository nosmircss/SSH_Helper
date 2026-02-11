using System.Text;
using SSH_Helper.Models;
using SSH_Helper.UI;

namespace SSH_Helper
{
    /// <summary>
    /// Displays captured execution metadata for a history entry.
    /// </summary>
    internal sealed class ExecutionDetailsDialog : Form
    {
        private readonly ExecutionDetails _details;
        private readonly string _historyOutput;

        private readonly BorderlessTabControl _tabControl;
        private readonly TextBox _txtSummary;
        private readonly DataGridView _gridHosts;
        private readonly TextBox _txtSettings;
        private readonly DataGridView _gridContext;
        private readonly Button _btnCopyToClipboard;
        private readonly Button _btnSaveToFile;
        private readonly Button _btnClose;

        public ExecutionDetailsDialog(ExecutionDetails details, bool darkMode = false)
            : this(details, string.Empty, darkMode)
        {
        }

        public ExecutionDetailsDialog(ExecutionDetails details, string historyOutput, bool darkMode = false)
        {
            _details = details ?? throw new ArgumentNullException(nameof(details));
            _historyOutput = historyOutput ?? string.Empty;

            Text = "Execution Details";
            Size = new Size(980, 680);
            MinimumSize = new Size(760, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _tabControl = new BorderlessTabControl
            {
                Location = new Point(12, 12),
                Size = new Size(940, 580),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var summaryTab = new TabPage("Summary");
            var hostsTab = new TabPage("Hosts");
            var settingsTab = new TabPage("Settings");
            var contextTab = new TabPage("Context");

            _txtSummary = CreateReadOnlyTextBox();
            summaryTab.Controls.Add(_txtSummary);

            _gridHosts = CreateReadOnlyGrid();
            _gridHosts.Columns.Add("HostAddress", "Host");
            _gridHosts.Columns.Add("Status", "Status");
            _gridHosts.Columns.Add("Timestamp", "Timestamp");
            _gridHosts.Columns["HostAddress"]!.FillWeight = 45;
            _gridHosts.Columns["Status"]!.FillWeight = 20;
            _gridHosts.Columns["Timestamp"]!.FillWeight = 35;
            hostsTab.Controls.Add(_gridHosts);

            _txtSettings = CreateReadOnlyTextBox();
            settingsTab.Controls.Add(_txtSettings);

            _gridContext = CreateReadOnlyGrid();
            _gridContext.Columns.Add("HostAddress", "Host");
            _gridContext.Columns.Add("Variable", "Variable");
            _gridContext.Columns.Add("Value", "Value");
            _gridContext.Columns["HostAddress"]!.FillWeight = 25;
            _gridContext.Columns["Variable"]!.FillWeight = 25;
            _gridContext.Columns["Value"]!.FillWeight = 50;
            contextTab.Controls.Add(_gridContext);

            _tabControl.TabPages.Add(summaryTab);
            _tabControl.TabPages.Add(hostsTab);
            _tabControl.TabPages.Add(settingsTab);
            _tabControl.TabPages.Add(contextTab);

            _btnCopyToClipboard = new Button
            {
                Text = "Copy to Clipboard",
                Size = new Size(130, 30),
                Location = new Point(18, 604),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            _btnCopyToClipboard.Click += BtnCopyToClipboard_Click;

            _btnSaveToFile = new Button
            {
                Text = "Save to File...",
                Size = new Size(110, 30),
                Location = new Point(156, 604),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            _btnSaveToFile.Click += BtnSaveToFile_Click;

            _btnClose = new Button
            {
                Text = "Close",
                Size = new Size(80, 30),
                Location = new Point(866, 604),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                DialogResult = DialogResult.OK
            };

            Controls.Add(_tabControl);
            Controls.Add(_btnCopyToClipboard);
            Controls.Add(_btnSaveToFile);
            Controls.Add(_btnClose);

            AcceptButton = _btnClose;
            CancelButton = _btnClose;

            PopulateSummaryTab();
            PopulateHostsTab();
            PopulateSettingsTab();
            PopulateContextTab();
            ApplyTheme(darkMode);
        }

        private static TextBox CreateReadOnlyTextBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f)
            };
        }

        private static DataGridView CreateReadOnlyGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                // Keep values non-editable while still allowing in-cell text selection for copy.
                ReadOnly = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                EditMode = DataGridViewEditMode.EditOnEnter,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText
            };

            grid.CellMouseDown += ReadOnlyGrid_CellMouseDown;
            grid.EditingControlShowing += ReadOnlyGrid_EditingControlShowing;
            grid.KeyDown += ReadOnlyGrid_KeyDown;
            grid.ContextMenuStrip = CreateGridContextMenu(grid);
            return grid;
        }

        private static ContextMenuStrip CreateGridContextMenu(DataGridView grid)
        {
            var contextMenu = new ContextMenuStrip();
            var copyMenuItem = new ToolStripMenuItem("Copy");
            copyMenuItem.Click += (_, _) => CopyGridSelection(grid);
            contextMenu.Items.Add(copyMenuItem);
            contextMenu.Opening += (_, _) =>
            {
                copyMenuItem.Enabled = grid.CurrentCell is not null;
            };
            return contextMenu;
        }

        private static void ReadOnlyGrid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is not DataGridView grid || e.Button != MouseButtons.Right)
                return;

            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var clickedCell = grid[e.ColumnIndex, e.RowIndex];
            if (clickedCell.Selected)
                return;

            grid.ClearSelection();
            grid.CurrentCell = clickedCell;
            clickedCell.Selected = true;
        }

        private static void ReadOnlyGrid_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is not TextBox textBox)
                return;

            // Read-only textbox keeps data immutable while still allowing mouse text selection + Ctrl+C.
            textBox.ReadOnly = true;
            textBox.ShortcutsEnabled = true;
            textBox.KeyDown -= EditingTextBox_KeyDown;
            textBox.KeyDown += EditingTextBox_KeyDown;
            if (sender is DataGridView grid)
            {
                textBox.ContextMenuStrip = grid.ContextMenuStrip;
            }
        }

        private static void EditingTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!e.Control || e.KeyCode != Keys.A || sender is not TextBox textBox)
                return;

            textBox.SelectAll();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private static void ReadOnlyGrid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not DataGridView grid)
                return;

            if (e.Control && e.KeyCode == Keys.C)
            {
                CopyGridSelection(grid);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // Keep grid data immutable.
            if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back || (e.Control && (e.KeyCode == Keys.V || e.KeyCode == Keys.X)))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private static void CopyGridSelection(DataGridView grid)
        {
            if (grid.EditingControl is TextBox textBox && textBox.SelectionLength > 0)
            {
                Clipboard.SetText(textBox.SelectedText);
                return;
            }

            var clipboardData = grid.GetClipboardContent();
            if (clipboardData is not null)
            {
                Clipboard.SetDataObject(clipboardData);
                return;
            }

            if (grid.CurrentCell?.Value is not null)
            {
                Clipboard.SetText(Convert.ToString(grid.CurrentCell.Value) ?? string.Empty);
            }
        }

        private void PopulateSummaryTab()
        {
            int hostCount = _details.Hosts?.Count ?? 0;
            int successCount = _details.Hosts?.Count(h => h.Success) ?? 0;
            int failedCount = hostCount - successCount;
            var duration = _details.EndTimeUtc > _details.StartTimeUtc
                ? _details.EndTimeUtc - _details.StartTimeUtc
                : TimeSpan.Zero;

            var sb = new StringBuilder();
            sb.AppendLine($"Preset Name: {_details.PresetName}");
            sb.AppendLine($"Preset Type: {_details.PresetType}");
            sb.AppendLine($"Folder Execution: {_details.IsFolderExecution}");
            if (_details.IsFolderExecution)
            {
                sb.AppendLine($"Folder Name: {_details.FolderName}");
            }

            sb.AppendLine($"Start Time: {FormatTimestamp(_details.StartTimeUtc)}");
            sb.AppendLine($"End Time: {FormatTimestamp(_details.EndTimeUtc)}");
            sb.AppendLine($"Duration: {duration:hh\\:mm\\:ss}");
            sb.AppendLine($"Environment: {_details.EnvironmentName}");
            sb.AppendLine($"Hosts: {hostCount} total ({successCount} succeeded, {failedCount} failed)");
            sb.AppendLine();
            sb.AppendLine("Executed Presets:");
            if (_details.ExecutedPresetNames.Count == 0)
            {
                sb.AppendLine("  (none)");
            }
            else
            {
                foreach (var preset in _details.ExecutedPresetNames)
                {
                    sb.AppendLine($"  - {preset}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Commands:");
            sb.AppendLine(_details.Commands);
            _txtSummary.Text = sb.ToString();
            _txtSummary.SelectionStart = 0;
            _txtSummary.SelectionLength = 0;
        }

        private void PopulateHostsTab()
        {
            _gridHosts.Rows.Clear();

            foreach (var host in _details.Hosts.OrderBy(h => h.HostAddress, StringComparer.OrdinalIgnoreCase))
            {
                _gridHosts.Rows.Add(
                    host.HostAddress,
                    host.Success ? "Success" : "Failed",
                    FormatTimestamp(host.TimestampUtc));
            }
        }

        private void PopulateSettingsTab()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Environment: {_details.EnvironmentName}");
            sb.AppendLine($"Username: {_details.Username}");
            sb.AppendLine($"Command Timeout: {_details.CommandTimeoutSeconds} sec");
            sb.AppendLine($"Connection Timeout: {_details.ConnectionTimeoutSeconds} sec");
            sb.AppendLine($"Connection Pooling: {(_details.UseConnectionPooling ? "Enabled" : "Disabled")}");
            sb.AppendLine($"Run Mode: {_details.RunMode}");
            _txtSettings.Text = sb.ToString();
            _txtSettings.SelectionStart = 0;
            _txtSettings.SelectionLength = 0;
        }

        private void PopulateContextTab()
        {
            _gridContext.Rows.Clear();

            foreach (var host in _details.Hosts.OrderBy(h => h.HostAddress, StringComparer.OrdinalIgnoreCase))
            {
                if (host.Variables.Count == 0)
                {
                    _gridContext.Rows.Add(host.HostAddress, "(no variables)", string.Empty);
                    continue;
                }

                foreach (var kvp in host.Variables.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
                {
                    _gridContext.Rows.Add(host.HostAddress, kvp.Key, kvp.Value);
                }
            }
        }

        private void ApplyTheme(bool darkMode)
        {
            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.StyleTabControl(_tabControl, darkMode);
            DialogTheme.StyleButton(_btnCopyToClipboard, darkMode);
            DialogTheme.StyleButton(_btnSaveToFile, darkMode);
            DialogTheme.StyleButton(_btnClose, darkMode, isPrimary: true);
            DialogTheme.SetDarkTitleBar(this, darkMode);

            ApplyGridTheme(_gridHosts, darkMode);
            ApplyGridTheme(_gridContext, darkMode);

            if (darkMode)
            {
                Load += (_, _) => DialogTheme.ApplyNativeTheme(this, true);
            }
        }

        private static void ApplyGridTheme(DataGridView grid, bool darkMode)
        {
            DialogTheme.StyleDataGridView(grid, darkMode, flattenHeaderBevel: true);
        }

        private void BtnCopyToClipboard_Click(object? sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(FormatDetailsAsText(includeOutputWindow: false));
                MessageBox.Show("Execution details copied to clipboard.",
                    "Copied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy details: {ex.Message}",
                    "Copy Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BtnSaveToFile_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"ExecutionDetails_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                File.WriteAllText(sfd.FileName, FormatDetailsAsText(includeOutputWindow: true));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save details: {ex.Message}",
                    "Save Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private string FormatDetailsAsText(bool includeOutputWindow)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Execution Details");
            sb.AppendLine(new string('=', 80));
            sb.AppendLine($"Preset Name: {_details.PresetName}");
            sb.AppendLine($"Preset Type: {_details.PresetType}");
            sb.AppendLine($"Folder Execution: {_details.IsFolderExecution}");
            if (_details.IsFolderExecution)
            {
                sb.AppendLine($"Folder Name: {_details.FolderName}");
            }

            sb.AppendLine($"Start Time: {FormatTimestamp(_details.StartTimeUtc)}");
            sb.AppendLine($"End Time: {FormatTimestamp(_details.EndTimeUtc)}");
            sb.AppendLine($"Environment: {_details.EnvironmentName}");
            sb.AppendLine($"Username: {_details.Username}");
            sb.AppendLine($"Command Timeout: {_details.CommandTimeoutSeconds} sec");
            sb.AppendLine($"Connection Timeout: {_details.ConnectionTimeoutSeconds} sec");
            sb.AppendLine($"Connection Pooling: {(_details.UseConnectionPooling ? "Enabled" : "Disabled")}");
            sb.AppendLine($"Run Mode: {_details.RunMode}");
            sb.AppendLine();
            sb.AppendLine("Executed Presets:");
            if (_details.ExecutedPresetNames.Count == 0)
            {
                sb.AppendLine("  (none)");
            }
            else
            {
                foreach (var preset in _details.ExecutedPresetNames)
                {
                    sb.AppendLine($"  - {preset}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Commands");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine(_details.Commands);
            sb.AppendLine();
            sb.AppendLine("Host Results");
            sb.AppendLine(new string('-', 80));
            foreach (var host in _details.Hosts.OrderBy(h => h.HostAddress, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"{host.HostAddress} | {(host.Success ? "Success" : "Failed")} | {FormatTimestamp(host.TimestampUtc)}");
            }

            sb.AppendLine();
            sb.AppendLine("Host Context Variables");
            sb.AppendLine(new string('-', 80));
            foreach (var host in _details.Hosts.OrderBy(h => h.HostAddress, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"[{host.HostAddress}]");
                if (host.Variables.Count == 0)
                {
                    sb.AppendLine("  (no variables)");
                    continue;
                }

                foreach (var variable in host.Variables.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"  {variable.Key} = {variable.Value}");
                }
            }

            if (includeOutputWindow)
            {
                sb.AppendLine();
                sb.AppendLine("Output Window (All Hosts Top to Bottom)");
                sb.AppendLine(new string('-', 80));
                if (string.IsNullOrWhiteSpace(_historyOutput))
                {
                    sb.AppendLine("(no output)");
                }
                else
                {
                    sb.Append(_historyOutput);
                    if (!_historyOutput.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                    {
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }

        private static string FormatTimestamp(DateTime timestampUtc)
        {
            if (timestampUtc == default)
                return "(not recorded)";

            return timestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
