using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace SSH_Helper
{

    public partial class Form1 : Form
    {
        private const string ApplicationVersion = "0.32-Beta";
        private const string ApplicationName = "SSH_Helper";

        private const int DefaultPollIntervalMs = 60; 

        public class ConfigObject
        {
            public Dictionary<string, PresetInfo>? Presets { get; set; }
            public string? Username { get; set; }
            public int Delay { get; set; }
            public int Timeout { get; set; }
        }

        public class PresetInfo
        {
            public string Commands { get; set; } = "";
            public int? Delay { get; set; }
            public int? Timeout { get; set; }
        }

        private bool _exitConfirmed = false;
        private string? _activePresetName;                 // Currently loaded (saved) preset name
        private bool _suppressPresetSelectionChange = false; // Prevent recursive prompting during programmatic selection changes
        private int rightClickedColumnIndex = -1; // Field to store the index of the right-clicked column
        private int rightClickedRowIndex = -1; // Field to store the index of the right-clicked row
        private string loadedFilePath;
        private Dictionary<string, PresetInfo> presets = new Dictionary<string, PresetInfo>();
        private BindingList<KeyValuePair<string, string>> outputHistoryList = new BindingList<KeyValuePair<string, string>>();
        private bool isRunning = false;
        CancellationTokenSource cts = new CancellationTokenSource();
        private bool _initialPresetDefaultsSaved = false;
        private bool _csvDirty = false;
        private string configFilePath;

        private bool _debugCaptureRaw = false;
        private StringBuilder? _rawBuffer;

        public Form1()
        {
            InitializeComponent();
            this.Text = $"{ApplicationName} {ApplicationVersion}";

            InitializeConfiguration();
            InitializeDataGridView();

            lstOutput.DataSource = outputHistoryList;
            lstOutput.DisplayMember = "Key";

            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            this.Click += Form_Click;
            dgv_variables.ContextMenuStrip = contextMenuStrip1;
            dgv_variables.MouseDown += dgv_variables_MouseDown;
            dgv_variables.RowPostPaint += dgv_variables_RowPostPaint;
            dgv_variables.CellClick += dgv_variables_CellClick;
            dgv_variables.ColumnAdded += dgv_variables_ColumnAdded;
            dgv_variables.CellLeave += dgv_variables_CellLeave;

            // Track edits to mark CSV as dirty
            dgv_variables.CellValueChanged += dgv_variables_CellValueChanged;
            dgv_variables.RowsAdded += dgv_variables_RowsAdded;
            dgv_variables.RowsRemoved += dgv_variables_RowsRemoved;
            dgv_variables.ColumnRemoved += dgv_variables_ColumnRemoved;

            AttachClickEventHandlers();

            //presets
            lstPreset.MouseDown += lstPreset_MouseDown;

            txtDelay.KeyPress += txtDelay_KeyPress;
            txtTimeout.KeyPress += txtTimeout_KeyPress;

            InitializePresetExportImportMenuItems();
        }

        private void lstPreset_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = lstPreset.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    lstPreset.SelectedIndex = index; // Select the item under the mouse
                    contextPresetLst.Show(Cursor.Position); // Show the context menu at the cursor position
                }
                else
                {
                    contextPresetLstAdd.Show(Cursor.Position); // Show the context menu at the cursor position
                }
            }
        }


        private void InitializeConfiguration()
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string specificFolder = Path.Combine(folder, "SSH_Helper");
            if (!Directory.Exists(specificFolder))
            {
                Directory.CreateDirectory(specificFolder);
            }

            configFilePath = Path.Combine(specificFolder, "config.json");
            if (!File.Exists(configFilePath))
            {
                CreateDefaultConfigFile();
            }

            LoadConfiguration();

            if (string.IsNullOrEmpty(txtTimeout.Text))
            {
                txtTimeout.Text = "10";
            }

            ApplyDefaultDelayTimeoutToPresetsAndSave();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If closing wasn’t initiated via Exit menu (or not yet confirmed), confirm now
            if (!_exitConfirmed)
            {
                // Optionally skip prompts on system shutdown
                if (e.CloseReason != CloseReason.WindowsShutDown &&
                    e.CloseReason != CloseReason.TaskManagerClosing)
                {
                    if (!ConfirmExitWorkflow())
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            Savevariables(); // persist settings on exit
        }

        private void Savevariables()
        {
            try
            {
                // Load the current configuration from the file
                var json = File.ReadAllText(configFilePath);
                var config = JsonConvert.DeserializeObject<ConfigObject>(json) ?? new ConfigObject();

                // Update the username in the configuration
                config.Username = txtUsername.Text;
                config.Delay = int.Parse(txtDelay.Text);
                config.Timeout = int.Parse(txtTimeout.Text);
                config.Presets = presets; // preserve presets if present

                // Serialize the updated configuration and write it back to the file
                json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save variables: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyDefaultDelayTimeoutToPresetsAndSave()
        {
            if (_initialPresetDefaultsSaved) return; // Run only once

            if (!int.TryParse(txtDelay.Text, out var globalDelay)) return;
            if (!int.TryParse(txtTimeout.Text, out var globalTimeout)) return;

            bool changed = false;
            foreach (var key in presets.Keys.ToList())
            {
                var p = presets[key];
                if (p.Delay == null)
                {
                    p.Delay = globalDelay;
                    changed = true;
                }
                if (p.Timeout == null)
                {
                    p.Timeout = globalTimeout;
                    changed = true;
                }
            }

            if (changed)
            {
                SavePresets(); // Persist upgraded presets with concrete Delay/Timeout values
            }
            _initialPresetDefaultsSaved = true;
        }

        private void LoadConfiguration()
        {
            try
            {
                string json = File.ReadAllText(configFilePath);
                var rootObj = JObject.Parse(json);

                presets.Clear();
                var presetsToken = rootObj["Presets"] as JObject;
                if (presetsToken != null)
                {
                    foreach (var prop in presetsToken.Properties())
                    {
                        if (prop.Value.Type == JTokenType.String)
                        {
                            // Legacy format: value is just a command string
                            presets[prop.Name] = new PresetInfo { Commands = prop.Value.ToString() };
                        }
                        else
                        {
                            var info = prop.Value.ToObject<PresetInfo>() ?? new PresetInfo();
                            info.Commands ??= "";
                            presets[prop.Name] = info;
                        }
                    }
                }

                lstPreset.Items.Clear();
                foreach (var key in presets.Keys)
                    lstPreset.Items.Add(key);

                // Avoid rootObj.ToObject<ConfigObject>() because legacy Presets entries may be strings
                string? usernameVal = rootObj["Username"]?.Type == JTokenType.String ? rootObj["Username"]!.ToString() : null;
                if (!string.IsNullOrWhiteSpace(usernameVal))
                    txtUsername.Text = usernameVal;

                if (rootObj["Delay"]?.Type == JTokenType.Integer)
                {
                    int delayVal = rootObj["Delay"]!.ToObject<int>();
                    if (delayVal > 0) txtDelay.Text = delayVal.ToString();
                }

                if (rootObj["Timeout"]?.Type == JTokenType.Integer)
                {
                    int timeoutVal = rootObj["Timeout"]!.ToObject<int>();
                    if (timeoutVal > 0) txtTimeout.Text = timeoutVal.ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void lstPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressPresetSelectionChange) return;
            if (lstPreset.SelectedItem == null) return;

            string newPresetName = lstPreset.SelectedItem.ToString();

            // If switching away from the active preset and current edits are dirty, prompt
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
                    // Revert selection back to previously active preset
                    _suppressPresetSelectionChange = true;
                    int revertIndex = lstPreset.Items.IndexOf(_activePresetName);
                    if (revertIndex >= 0)
                        lstPreset.SelectedIndex = revertIndex;
                    _suppressPresetSelectionChange = false;
                    return;
                }
                if (result == DialogResult.Yes)
                {
                    // Save current edits before switching
                    btnSave_Click(this, EventArgs.Empty);
                }
                // If No -> discard edits and continue loading the newly selected preset
            }

            if (presets.TryGetValue(newPresetName, out var info))
            {
                txtCommand.Text = info.Commands;
                txtPreset.Text = newPresetName;
                if (info.Delay.HasValue) txtDelay.Text = info.Delay.Value.ToString();
                if (info.Timeout.HasValue) txtTimeout.Text = info.Timeout.Value.ToString();
            }

            _activePresetName = newPresetName; // Now this is the active (clean) preset
        }

        private void dgv_variables_CellLeave(object sender, DataGridViewCellEventArgs e)
        {
            // Commit the edit when the cell loses focus
            dgv_variables.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dgv_variables_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            _csvDirty = true;
        }
        private void dgv_variables_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            _csvDirty = true;
        }
        private void dgv_variables_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            _csvDirty = true;
        }
        private void dgv_variables_ColumnRemoved(object sender, DataGridViewColumnEventArgs e)
        {
            _csvDirty = true;
        }

        private void dgv_variables_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                dgv_variables.SelectAll();
                e.Handled = true;  // Prevent further processing of this key combination
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopyDataGridViewToClipboard();
                e.Handled = true;  // Prevent further processing of this key combination
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                PasteClipboardData();
                //increment selected cell
                int rowIndex = dgv_variables.CurrentCell.RowIndex;
                int colIndex = dgv_variables.CurrentCell.ColumnIndex;
                if (rowIndex < dgv_variables.Rows.Count - 1)
                {
                    dgv_variables.CurrentCell = dgv_variables[colIndex, rowIndex + 1];
                }
                dgv_variables.ClearSelection();
                e.Handled = true;  // Prevent further processing of this key combination
            }
            else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
            {
                // Handle Delete or Backspace key to clear the contents of selected cells
                DeleteSelectedCellsContents();
                e.Handled = true;  // Prevent further processing of this key combination
            }
        }

        private void dgv_variables_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1) // This checks if the click is on a column header
            {
                dgv_variables.ClearSelection();
                foreach (DataGridViewRow row in dgv_variables.Rows)
                {
                    row.Cells[e.ColumnIndex].Selected = true;
                }
            }
        }

        private void dgv_variables_ColumnAdded(object sender, DataGridViewColumnEventArgs e)
        {
            e.Column.SortMode = DataGridViewColumnSortMode.NotSortable;  // Disable sorting for the added column
        }

        private void DeleteSelectedCellsContents()
        {
            foreach (DataGridViewCell cell in dgv_variables.SelectedCells)
            {
                if (!cell.ReadOnly)  // Check if the cell is not read-only
                {
                    cell.Value = null;  // Clear the content of the cell
                }
            }
            dgv_variables.Refresh();  // Optionally refresh the DataGridView to update the UI immediately
            _csvDirty = true;
        }

        private void CopyDataGridViewToClipboard()
        {
            // Determine if the entire table is selected
            bool allCellsSelected = (dgv_variables.SelectedCells.Count == dgv_variables.RowCount * dgv_variables.ColumnCount);

            if (allCellsSelected)
            {
                // Manually construct the data for clipboard to exclude row headers and the new row
                StringBuilder buffer = new StringBuilder();
                // Add column headers first
                for (int j = 0; j < dgv_variables.ColumnCount; j++)
                {
                    buffer.Append(dgv_variables.Columns[j].HeaderText);
                    if (j < dgv_variables.ColumnCount - 1)
                        buffer.Append("\t");
                }
                buffer.AppendLine();

                // Add rows of data, skipping the last row if it's the new row and skipping empty rows
                int rowCount = dgv_variables.AllowUserToAddRows ? dgv_variables.Rows.Count - 1 : dgv_variables.Rows.Count;
                for (int i = 0; i < rowCount; i++)
                {
                    bool isEmptyRow = true;
                    StringBuilder rowBuffer = new StringBuilder();
                    for (int j = 0; j < dgv_variables.Columns.Count; j++)
                    {
                        string cellValue = dgv_variables.Rows[i].Cells[j].Value?.ToString() ?? "";
                        rowBuffer.Append(cellValue);
                        if (j < dgv_variables.Columns.Count - 1)
                            rowBuffer.Append("\t");

                        if (!string.IsNullOrEmpty(cellValue))
                            isEmptyRow = false;
                    }

                    // Only append the row if it is not empty
                    if (!isEmptyRow)
                    {
                        buffer.AppendLine(rowBuffer.ToString());
                    }
                }

                // Copy the constructed string to the clipboard
                Clipboard.SetText(buffer.ToString());
            }
            else
            {
                // Manual copying for partial selection, ensuring new lines are formatted for Excel
                StringBuilder buffer = new StringBuilder();
                // Sort the selected cells by row index, then by column index to maintain order
                var sortedCells = dgv_variables.SelectedCells.Cast<DataGridViewCell>()
                                     .OrderBy(c => c.RowIndex)
                                     .ThenBy(c => c.ColumnIndex)
                                     .ToList();

                int lastRowIndex = -1;
                foreach (var cell in sortedCells)
                {
                    // Add a newline when starting a new row
                    if (cell.RowIndex != lastRowIndex)
                    {
                        if (lastRowIndex != -1)
                            buffer.AppendLine();
                        lastRowIndex = cell.RowIndex;
                    }
                    else
                    {
                        buffer.Append("\t");
                    }

                    // Append the current cell's value
                    buffer.Append(cell.Value?.ToString() ?? "");
                }

                // Set the final string to the clipboard, ensuring to use Windows newline
                Clipboard.SetText(buffer.ToString());
            }
        }

        private void dgv_variables_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                dgv_variables.BeginEdit(true);
            }
        }

        private void EndEditAndClearSelection()
        {
            if (dgv_variables.IsCurrentCellInEditMode)
            {
                dgv_variables.EndEdit(); // Commits any edits that are currently in progress

            }
            dgv_variables.ClearSelection();
        }

        private void Form_Click(object sender, EventArgs e)
        {
            EndEditAndClearSelection();
        }

        private void AttachClickEventHandlers()
        {
            foreach (Control control in this.Controls)
            {
                if (control != dgv_variables) // Exclude the DataGridView itself
                {
                    control.Click += Control_Click;
                }
            }
        }

        private void Control_Click(object sender, EventArgs e)
        {
            EndEditAndClearSelection();
        }

        private void dgv_variables_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Check if the current cell is not in edit mode and the character is not a control character
            if (!dgv_variables.IsCurrentCellInEditMode && !char.IsControl(e.KeyChar))
            {
                dgv_variables.BeginEdit(true);
                // Send the character to the editing control, simulating user input
                if (dgv_variables.EditingControl is System.Windows.Forms.TextBox editingTextBox)
                {
                    editingTextBox.Text = e.KeyChar.ToString();  // Set the text directly to handle first key
                    editingTextBox.SelectionStart = editingTextBox.Text.Length;  // Move the caret to the end
                }
                e.Handled = true; // Indicate that the key press has been handled
            }
        }

        private void PasteClipboardData()
        {
            if (Clipboard.ContainsText())
            {
                Point startCell = GetCurrentCellLocation();
                string clipboardString = Clipboard.GetText();
                string[] rows = clipboardString.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                dgv_variables.AllowUserToAddRows = false;  // Temporarily disable to manage row addition manually

                for (int i = 0; i < rows.Length; i++)
                {
                    string[] columns = rows[i].Split('\t');
                    for (int j = 0; j < columns.Length; j++)
                    {
                        int rowIndex = startCell.Y + i;
                        // Ensure the row exists or add new rows as necessary
                        while (rowIndex >= dgv_variables.Rows.Count)
                        {
                            dgv_variables.Rows.Add(new DataGridViewRow());  // Add a new row if necessary
                        }

                        int columnIndex = startCell.X + j;
                        // Ensure the column exists or add new columns as necessary
                        while (columnIndex >= dgv_variables.Columns.Count)
                        {
                            int nextColumnNumber = dgv_variables.Columns.Count + 1;
                            string defaultColumnName = $"Column{nextColumnNumber}";
                            dgv_variables.Columns.Add(defaultColumnName, defaultColumnName);
                        }

                        if (!dgv_variables.Columns[columnIndex].ReadOnly)
                        {
                            dgv_variables.Rows[rowIndex].Cells[columnIndex].Value = columns[j];
                        }
                    }
                }

                dgv_variables.AllowUserToAddRows = true;  // Re-enable the ability to add rows
                _csvDirty = true;

            }
        }

        private Point GetCurrentCellLocation()
        {
            int column = dgv_variables.CurrentCell?.ColumnIndex ?? 0; // Fallback to 0 if no cell is selected
            int row = dgv_variables.CurrentCell?.RowIndex ?? 0; // Fallback to 0 if no cell is selected
            return new Point(column, row);
        }

        private void InitializeDataGridView()
        {
            dgv_variables.Columns.Add($"Host_IP", $"Host_IP");
            dgv_variables.KeyPress += dgv_variables_KeyPress;
            dgv_variables.KeyDown += dgv_variables_KeyDown;

            // Basic DataGridView properties for better appearance
            dgv_variables.EnableHeadersVisualStyles = false;
            dgv_variables.BackgroundColor = Color.White;
            dgv_variables.GridColor = Color.Gainsboro; // Soft gray lines, less pronounced than older styles

            // Column header styles
            dgv_variables.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv_variables.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 242, 242); // Light gray background
            dgv_variables.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            dgv_variables.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            dgv_variables.ColumnHeadersHeight = 32; // Taller headers to match modern Excel
            dgv_variables.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Row header styles
            dgv_variables.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv_variables.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 242, 242);
            dgv_variables.RowHeadersDefaultCellStyle.ForeColor = Color.Black;
            dgv_variables.RowHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            dgv_variables.RowHeadersWidth = 60; // Ample width for row headers

            // Cell default styles
            dgv_variables.DefaultCellStyle.BackColor = Color.White;
            dgv_variables.DefaultCellStyle.ForeColor = Color.Black;
            dgv_variables.DefaultCellStyle.Font = new Font("Segoe UI", 9);
            dgv_variables.DefaultCellStyle.SelectionBackColor = Color.LightBlue; // Excel-like selection color
            dgv_variables.DefaultCellStyle.SelectionForeColor = Color.Black;

            // Alternating row style for better readability
            dgv_variables.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);

            // Ensuring the grid lines are visible but unobtrusive

            dgv_variables.ColumnHeadersVisible = true;
            dgv_variables.RowHeadersVisible = true;

        }

        private void dgv_variables_MouseDown(object sender, MouseEventArgs e)
        {
            DataGridView.HitTestInfo hit = dgv_variables.HitTest(e.X, e.Y);

            if (hit.Type == DataGridViewHitTestType.RowHeader)
            {
                // Ensure the row is selected when the row header is clicked
                dgv_variables.ClearSelection();
                dgv_variables.CurrentCell = dgv_variables.Rows[hit.RowIndex].Cells[0];

                foreach (DataGridViewCell cell in dgv_variables.Rows[hit.RowIndex].Cells)
                {
                    cell.Selected = true;  // Select each cell in the row to visually highlight the entire row
                }
            }

            // Handling clicks outside of cell areas but not outside the grid itself.
            if (hit.Type != DataGridViewHitTestType.Cell && hit.Type != DataGridViewHitTestType.ColumnHeader && hit.Type != DataGridViewHitTestType.RowHeader)
            {
                EndEditAndClearSelection();
            }

            if (e.Button == MouseButtons.Right)
            {
                if (hit.Type == DataGridViewHitTestType.Cell || hit.Type == DataGridViewHitTestType.ColumnHeader)
                {
                    rightClickedColumnIndex = hit.ColumnIndex;  // Save the clicked column index

                    // Check if the right-click is on a cell, and not on the header
                    if (hit.Type == DataGridViewHitTestType.Cell)
                    {
                        rightClickedRowIndex = hit.RowIndex; // Save the row index only if it's a cell
                        dgv_variables.CurrentCell = dgv_variables[hit.ColumnIndex, hit.RowIndex]; // Set the current cell
                    }
                    else
                    {
                        rightClickedRowIndex = -1; // There is no row index for header clicks
                    }

                    // Enable/disable "Delete Column" based on Host_IP protection
                    if (rightClickedColumnIndex >= 0 && rightClickedColumnIndex < dgv_variables.Columns.Count)
                    {
                        var col = dgv_variables.Columns[rightClickedColumnIndex];
                        bool isHostIp =
                            string.Equals(col.Name, "Host_IP", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(col.HeaderText, "Host_IP", StringComparison.OrdinalIgnoreCase);
                        deleteColumnToolStripMenuItem.Enabled = !isHostIp;
                        renameColumnToolStripMenuItem.Enabled = !isHostIp;
                    }
                    else
                    {
                        deleteColumnToolStripMenuItem.Enabled = true;
                        renameColumnToolStripMenuItem.Enabled = true;
                    }

                    dgv_variables.ContextMenuStrip = contextMenuStrip1; // Assign the custom context menu
                    contextMenuStrip1.Show(dgv_variables, e.Location); // Show the context menu at the cursor location
                }
                else
                {
                    rightClickedColumnIndex = -1;
                    rightClickedRowIndex = -1;
                    dgv_variables.ContextMenuStrip = contextMenuStrip1; // Ensure the custom menu is enabled elsewhere
                    deleteColumnToolStripMenuItem.Enabled = true;       // Default to enabled when not on a column
                }
            }
        }

        private void btnOpenCSV_Click(object sender, EventArgs e)
        {
            if (!EnsureCsvChangesSavedBeforeReplacing())
                return;

            OpenCsvInteractive();
        }

        private DataTable LoadCsvIntoDataTable(string filePath)
        {
            DataTable dt = new DataTable();

            using (StreamReader sr = new StreamReader(filePath))
            {
                // Read the header line
                string[] headers = sr.ReadLine().Split(',');

                // Manually add the Host_IP column to match the first column of data
                dt.Columns.Add("Host_IP");

                // Process headers starting from the second CSV column
                for (int i = 1; i < headers.Length; i++)
                {
                    string headerName = string.IsNullOrEmpty(headers[i].Trim()) ? $"column{i}" : headers[i].Trim().Replace(" ", "_");
                    dt.Columns.Add(headerName);
                }

                // Read and add the data rows
                while (!sr.EndOfStream)
                {
                    string[] rowValues = sr.ReadLine().Split(',');

                    // Skip completely empty rows
                    if (rowValues.All(string.IsNullOrWhiteSpace))
                        continue;

                    // Ensure the data array fits the DataTable's column structure
                    if (rowValues.Length > headers.Length)
                        Array.Resize(ref rowValues, headers.Length);

                    // Add row to the DataTable
                    dt.Rows.Add(rowValues);
                }
            }
            return dt;
        }

        private void addColumnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Calculate the next column number
            int nextColumnNumber = dgv_variables.Columns.Count + 1;
            string defaultColumnName = $"Column{nextColumnNumber}";

            // Prompting the user to enter the name of the new column, using the calculated default name
            string columnName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the name of the new column:",
                "Add Column",
                defaultColumnName
            );

            // Replace spaces with underscores in the column name entered by the user
            columnName = columnName.Replace(" ", "_");

            if (!string.IsNullOrEmpty(columnName))
            {
                // Check if the column already exists to avoid duplicates
                if (dgv_variables.Columns.Contains(columnName))
                {
                    MessageBox.Show("Column name already exists!");
                    return;
                }

                // Adding the new column
                dgv_variables.Columns.Add(columnName, columnName);
                _csvDirty = true;
            }
        }

        private void deleteColumnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Check if the index is valid
            if (rightClickedColumnIndex >= 0 && rightClickedColumnIndex < dgv_variables.Columns.Count)
            {
                var col = dgv_variables.Columns[rightClickedColumnIndex];

                if (string.Equals(col.Name, "Host_IP", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(col.HeaderText, "Host_IP", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("The Host_IP column cannot be deleted.", "Delete Column", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                dgv_variables.Columns.RemoveAt(rightClickedColumnIndex);
                _csvDirty = true;
            }
        }

        private void deleteRowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Check if the right-clicked row index is valid
            if (rightClickedRowIndex >= 0 && rightClickedRowIndex < dgv_variables.Rows.Count)
            {
                DataGridViewRow row = dgv_variables.Rows[rightClickedRowIndex];
                if (!row.IsNewRow) // Ensure it's not the new row
                {
                    dgv_variables.Rows.RemoveAt(rightClickedRowIndex);
                    _csvDirty = true;

                    if (dgv_variables.Rows.Count > 0)
                    {
                        int newSelectedIndex = rightClickedRowIndex < dgv_variables.Rows.Count ? rightClickedRowIndex : dgv_variables.Rows.Count - 1;
                        dgv_variables.Rows[newSelectedIndex].Selected = true;
                        dgv_variables.CurrentCell = dgv_variables.Rows[newSelectedIndex].Cells[0]; // Ensure the focus moves to the new row
                    }
                }
                else
                {
                    MessageBox.Show("Cannot delete the new row placeholder.");
                }
            }
            else
            {
                MessageBox.Show("No valid row selected.");
            }
        }

        private void renameColumnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (rightClickedColumnIndex >= 0 && rightClickedColumnIndex < dgv_variables.Columns.Count)
            {
                DataGridViewColumn columnToRename = dgv_variables.Columns[rightClickedColumnIndex];
                string currentName = columnToRename.HeaderText;
                string newName = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter a new name for the column '{currentName}':",
                    "Rename Column",
                    currentName
                );

                // Replace spaces with underscores in the new column name
                newName = newName.Replace(" ", "_");

                if (!string.IsNullOrEmpty(newName) && newName != currentName)
                {
                    // Ensure the new name doesn't already exist to avoid duplicates
                    foreach (DataGridViewColumn column in dgv_variables.Columns)
                    {
                        if (column.HeaderText.Equals(newName, StringComparison.OrdinalIgnoreCase))
                        {
                            MessageBox.Show("This column name already exists. Please choose a different name.", "Rename Column Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }

                    // Update both the HeaderText and Name properties of the column
                    columnToRename.HeaderText = newName;
                    columnToRename.Name = newName; // Assuming you also use Name in your logic
                    _csvDirty = true;
                }
            }
        }

        private void btnSaveAs_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV files (*.csv)|*.csv";
                sfd.Title = "Save as CSV";
                if (!string.IsNullOrEmpty(loadedFilePath))
                    sfd.FileName = Path.GetFileName(loadedFilePath);

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    SaveDataGridViewToCSV(sfd.FileName);
                    loadedFilePath = sfd.FileName; // ensure future 'Save' works
                }
            }
        }

        private void SaveDataGridViewToCSV(string filename)
        {
            using (StreamWriter sw = new StreamWriter(filename))
            {
                // Write column headers based on display order
                var columnOrder = dgv_variables.Columns
                    .Cast<DataGridViewColumn>()
                    .OrderBy(c => c.DisplayIndex)
                    .ToList();

                for (int i = 0; i < columnOrder.Count; i++)
                {
                    sw.Write(columnOrder[i].HeaderText);
                    if (i < columnOrder.Count - 1)
                        sw.Write(",");
                }
                sw.WriteLine();

                // Write data rows based on display order
                foreach (DataGridViewRow row in dgv_variables.Rows)
                {
                    if (!row.IsNewRow) // Skip the new row placeholder
                    {
                        for (int i = 0; i < columnOrder.Count; i++)
                        {
                            // Retrieve cell value based on the column's display order
                            sw.Write(row.Cells[columnOrder[i].Index].Value?.ToString());
                            if (i < columnOrder.Count - 1)
                                sw.Write(",");
                        }
                        sw.WriteLine();
                    }
                }
            }
            _csvDirty = false; // Saved
        }

        private void dgv_variables_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            // Use the RowIndex plus 1 to display a 1-based row number like in Excel
            var grid = sender as DataGridView;
            var rowIdx = (e.RowIndex + 1).ToString();

            // Get the center of the header cell to align the row number correctly
            var centerFormat = new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            // Get the header bounds
            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);

            // Draw the row number
            e.Graphics.DrawString(rowIdx, grid.Font, SystemBrushes.ControlText, headerBounds, centerFormat);
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            // Check if the DataGridView is bound to a DataTable
            if (dgv_variables.DataSource is DataTable)
            {
                DataTable dt = (DataTable)dgv_variables.DataSource;
                dt.Rows.Clear(); // Clears the rows
                dt.Columns.Clear(); // Clears the columns, if needed

                // Re-add the necessary column after clearing
                dt.Columns.Add("Host_IP", typeof(string));
            }
            else
            {
                // Handle other types of data sources or unbound scenarios
                dgv_variables.Rows.Clear();
                dgv_variables.Columns.Clear();

                // Re-add the Host_IP column
                dgv_variables.Columns.Add("Host_IP", "Host IP");
            }
            _csvDirty = true;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string presetName = txtPreset.Text.Trim();
            string commands = txtCommand.Text;

            if (!string.IsNullOrEmpty(presetName) && !string.IsNullOrWhiteSpace(commands))
            {
                int? perDelay = int.TryParse(txtDelay.Text, out var dVal) ? dVal : null;
                int? perTimeout = int.TryParse(txtTimeout.Text, out var tVal) ? tVal : null;

                if (!presets.ContainsKey(presetName))
                {
                    presets.Add(presetName, new PresetInfo
                    {
                        Commands = commands,
                        Delay = perDelay,
                        Timeout = perTimeout
                    });
                    if (!lstPreset.Items.Contains(presetName))
                        lstPreset.Items.Add(presetName);
                }
                else
                {
                    presets[presetName].Commands = commands;
                    presets[presetName].Delay = perDelay;
                    presets[presetName].Timeout = perTimeout;
                }

                SavePresets();
                _activePresetName = presetName; // Mark as clean
            }
            else
            {
                MessageBox.Show("Preset name or command is empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //execute
        private void ExecuteCommands(IEnumerable<DataGridViewRow> rows, CancellationToken token)
        {
            string[] commands = txtCommand.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            this.Invoke(new Action(() =>
            {
                txtOutput.Clear();  // Clear previous output
            }));

            bool isFirst = true;
            foreach (DataGridViewRow row in rows)
            {
                if (token.IsCancellationRequested)
                    break;
                if (row.IsNewRow) continue;

                string ipAddressWithPort = row.Cells["Host_IP"].Value?.ToString();
                if (string.IsNullOrEmpty(ipAddressWithPort) || !IsValidIPAddress(ipAddressWithPort))
                {
                    continue; // Skip this row in execution phase too
                }

                ExecuteRowCommands(row, commands, ref isFirst);
            }

            //store history
            string key = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {txtPreset.Text}";
            var entry = new KeyValuePair<string, string>(key, txtOutput.Text);
            this.Invoke(new Action(() =>
            {
                outputHistoryList.Insert(0, entry);  // Insert at the start of the list
                lstOutput.SelectedIndex = 0;  // Select the newest entry automatically              
                Savevariables(); // Save the variables after each execution
            }));

        }

        private void ExecuteRowCommands(DataGridViewRow row, string[] commands, ref bool isFirst)
        {
            string ipAddressWithPort = row.Cells["Host_IP"].Value?.ToString();
            string[] parts = ipAddressWithPort.Split(':');
            string ipAddress = parts[0];
            int port = parts.Length > 1 && int.TryParse(parts[1], out int customPort) ? customPort : 22;

            string username = dgv_variables.Columns.Contains("username") && row.Cells["username"].Value != null && !string.IsNullOrWhiteSpace(row.Cells["username"].Value.ToString())
                ? row.Cells["username"].Value.ToString() : txtUsername.Text;

            string password = dgv_variables.Columns.Contains("password") && row.Cells["password"].Value != null && !string.IsNullOrWhiteSpace(row.Cells["password"].Value.ToString())
                ? row.Cells["password"].Value.ToString() : txtPassword.Text;

            try
            {
                using (var client = new SshClient(ipAddress, port, username, password))
                {
                    client.ErrorOccurred += (s, a) =>
                    {
                        if (a.Exception != null && isRunning)
                        {
                            this.Invoke(new Action(() =>
                            {
                                txtOutput.AppendText(Environment.NewLine +
                                                     $"[SSH.NET Error] {a.Exception.GetType().Name}: {a.Exception.Message}" +
                                                     Environment.NewLine);
                            }));
                        }
                    };

                    if (!int.TryParse(txtTimeout.Text, out var timeoutSec)) timeoutSec = 10;
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(timeoutSec);

                    client.Connect();

                    using (var shellStream = client.CreateShellStream("xterm", 200, 48, 1200, 800, 16384))
                    {
                        try
                        {
                            HandleCommandExecution(shellStream, commands, row, ref isFirst, ipAddress, port);
                        }
                        catch (Exception ex)
                        {
                            // Execution/read-stage error (not an actual connect failure)
                            HandleException(false, ex, ipAddress, port);
                        }
                    }

                    client.Disconnect();
                }
            }
            catch (SshAuthenticationException authEx)
            {
                HandleException(true, authEx, ipAddress, port);
            }
            catch (Renci.SshNet.Common.SshConnectionException ex)
            {
                HandleException(false, ex, ipAddress, port);
            }
            catch (Renci.SshNet.Common.SshOperationTimeoutException ex)
            {
                HandleException(false, ex, ipAddress, port);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                HandleException(false, ex, ipAddress, port);
            }
            catch (Exception ex)
            {
                HandleException(false, ex, ipAddress, port);
            }
        }

        private static string NormalizeTerminalOutput(string input, int tabSize = 8)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sbOut = new StringBuilder(input.Length + 64);
            var line = new StringBuilder(256);
            int cursor = 0;
            int savedCursor = -1;

            void EnsureLen(int len)
            {
                if (line.Length < len)
                    line.Append(' ', len - line.Length);
            }

            void CommitLine()
            {
                // Trim trailing spaces (keeps leading indent intact)
                int end = line.Length;
                while (end > 0 && line[end - 1] == ' ') end--;
                sbOut.Append(line.ToString(0, end));
                sbOut.Append("\r\n");
                line.Clear();
                cursor = 0;
            }

            int ParseIntDefault(string s, int def)
            {
                return int.TryParse(s, out var v) && v > 0 ? v : def;
            }

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '\r')
                {
                    cursor = 0; // CR: return to column 0
                }
                else if (c == '\n')
                {
                    CommitLine();
                }
                else if (c == '\t')
                {
                    int nextStop = ((cursor / tabSize) + 1) * tabSize;
                    EnsureLen(nextStop);
                    cursor = nextStop;
                }
                else if (c == '\b')
                {
                    if (cursor > 0) cursor--;
                }
                else if (c == (char)0x1B) // ESC
                {
                    // Support ESC[s (save), ESC[u (restore)
                    if (i + 1 < input.Length && input[i + 1] == 's')
                    {
                        savedCursor = cursor; i += 1; continue;
                    }
                    if (i + 1 < input.Length && input[i + 1] == 'u')
                    {
                        if (savedCursor >= 0) cursor = Math.Min(savedCursor, line.Length);
                        i += 1; continue;
                    }

                    // CSI: ESC[
                    if (i + 1 < input.Length && input[i + 1] == '[')
                    {
                        i += 2;
                        var param = new StringBuilder();
                        while (i < input.Length)
                        {
                            char ch = input[i];
                            if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
                            {
                                char cmd = ch;
                                string p = param.ToString(); // e.g. "2K", "10C", "12;34H"
                                                             // Split params by ';'
                                string[] parts = p.Split(';', StringSplitOptions.RemoveEmptyEntries);

                                switch (cmd)
                                {
                                    case 'X': // ECH: erase n chars from cursor (replace with spaces, keep length)
                                        {
                                            int n = parts.Length > 0 ? ParseIntDefault(parts[0], 1) : 1;
                                            if (cursor < line.Length)
                                            {
                                                int cnt = Math.Min(n, line.Length - cursor);
                                                EnsureLen(cursor + cnt);
                                                for (int j = 0; j < cnt; j++)
                                                    line[cursor + j] = ' ';
                                            }
                                        }
                                        break;

                                    case 's': // CSI save cursor
                                        savedCursor = cursor;
                                        break;

                                    case 'u': // CSI restore cursor
                                        if (savedCursor >= 0) cursor = Math.Min(savedCursor, line.Length);
                                        break;

                                    case 'K':
                                        {
                                            int mode = parts.Length > 0 ? ParseIntDefault(parts[0], 0) : 0;
                                            if (mode == 2)
                                            {
                                                line.Clear();
                                                cursor = 0;
                                            }
                                            else if (mode == 0)
                                            {
                                                if (cursor < line.Length)
                                                    line.Remove(cursor, line.Length - cursor);
                                            }
                                            else if (mode == 1)
                                            {
                                                // Erase from start to cursor (retain tail)
                                                if (cursor > 0)
                                                {
                                                    int keep = line.Length - cursor;
                                                    var tail = keep > 0 ? line.ToString(cursor, keep) : string.Empty;
                                                    line.Clear();
                                                    line.Append(new string(' ', cursor));
                                                    if (keep > 0)
                                                    {
                                                        EnsureLen(cursor + keep);
                                                        for (int j = 0; j < keep; j++)
                                                            line[cursor + j] = tail[j];
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    case 'C': // CUF: forward n
                                        {
                                            int n = parts.Length > 0 ? ParseIntDefault(parts[0], 1) : 1;
                                            cursor += n;
                                        }
                                        break;
                                    case 'D': // CUB: back n
                                        {
                                            int n = parts.Length > 0 ? ParseIntDefault(parts[0], 1) : 1;
                                            cursor = Math.Max(0, cursor - n);
                                        }
                                        break;
                                    case 'G': // CHA: to column n (1-based)
                                        {
                                            int n = parts.Length > 0 ? ParseIntDefault(parts[0], 1) : 1;
                                            cursor = Math.Max(0, n - 1);
                                        }
                                        break;
                                    case 'H': // CUP row;col (1-based) – honor col, ignore row
                                    case 'f': // HVP row;col (1-based)
                                        {
                                            int col = 1;
                                            if (parts.Length >= 2)
                                                col = ParseIntDefault(parts[1], 1);
                                            else if (parts.Length == 1)
                                                col = ParseIntDefault(parts[0], 1);
                                            cursor = Math.Max(0, col - 1);
                                        }
                                        break;
                                    case '@': // ICH: insert n spaces at cursor
                                        {
                                            int n = parts.Length > 0 ? ParseIntDefault(parts[0], 1) : 1;
                                            EnsureLen(cursor);
                                            line.Insert(cursor, new string(' ', n));
                                            // cursor stays
                                        }
                                        break;
                                    case 'P': // DCH: delete n chars at cursor
                                        {
                                            int n = parts.Length > 0 ? ParseIntDefault(parts[0], 1) : 1;
                                            if (cursor < line.Length)
                                            {
                                                int del = Math.Min(n, line.Length - cursor);
                                                line.Remove(cursor, del);
                                            }
                                        }
                                        break;
                                    case 'm':
                                        // SGR (colors/styles) – ignore for plain-text normalization
                                        break;
                                    default:
                                        // Ignore other CSI commands
                                        break;
                                }
                                break; // exit CSI loop
                            }
                            else
                            {
                                param.Append(ch);
                                i++;
                            }
                        }
                    }
                    // else: unknown ESC sequence – ignore
                }
                else if (c >= ' ' && c <= '~')
                {
                    EnsureLen(cursor + 1);
                    line[cursor] = c;
                    cursor++;
                }
                else
                {
                    // Ignore other control chars
                }
            }

            if (line.Length > 0)
            {
                // Keep last line without forcing a trailing CRLF
                sbOut.Append(line);
            }

            return sbOut.ToString();
        }

        private void HandleCommandExecution(ShellStream shellStream, string[] commands, DataGridViewRow row, ref bool isFirst, string ipAddress, int port)
        {
            if (!isRunning) return;

            if (_debugCaptureRaw) _rawBuffer = new StringBuilder(256 * 1024);

            // Use fixed poll interval; keep Timeout user-configurable
            int delayMs = DefaultPollIntervalMs;
            if (!int.TryParse(txtTimeout.Text, out var timeoutSec)) timeoutSec = 10;
            int idleTimeoutMs = Math.Max(1000, timeoutSec * 1000);

            // Trigger prompt and read initial banner/prompt
            shellStream.WriteLine("");
            shellStream.Flush();

            string banner = ReadAvailable(shellStream, delayMs, 800, maxOverallMs: 1500, stopOnLikelyPrompt: true);
            banner = Regex.Replace(banner, @"[^\u0020-\u007E\r\n\t\b\u001B]", "");
            banner = NormalizeTerminalOutput(banner);

            if (!TryDetectPromptFromBuffer(banner, out var promptText))
            {
                var lines = banner.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                promptText = lines.LastOrDefault()?.TrimEnd() ?? "";
            }

            var promptRegex = BuildPromptRegex(promptText);

            string header = $"{new string('#', 20)} CONNECTED TO!!! {ipAddress}:{port} {promptText} {new string('#', 20)}";
            string foo = new string('#', header.Length);

            if (!isFirst)
            {
                this.Invoke(new Action(() => txtOutput.AppendText(Environment.NewLine)));
            }
            isFirst = false;

            if (isRunning)
            {
                this.Invoke(new Action(() =>
                {
                    txtOutput.AppendText(foo + Environment.NewLine + header + Environment.NewLine + foo + Environment.NewLine + promptText);
                }));
            }

            foreach (string commandTemplate in commands)
            {
                if (!isRunning) break;
                if (string.IsNullOrWhiteSpace(commandTemplate) || commandTemplate.StartsWith("#")) continue;

                string commandToExecute = commandTemplate;

                // Safe ${var} substitution (no exception if column missing)
                foreach (Match match in Regex.Matches(commandTemplate, @"\$\{([^}]+)\}"))
                {
                    string variableName = match.Groups[1].Value;
                    string columnValue = "";
                    if (row?.DataGridView?.Columns.Contains(variableName) == true)
                    {
                        columnValue = row.Cells[variableName].Value?.ToString() ?? "";
                    }
                    commandToExecute = commandToExecute.Replace($"${{{variableName}}}", columnValue);
                }

                if (string.IsNullOrWhiteSpace(commandToExecute)) continue;

                shellStream.WriteLine(commandToExecute);
                shellStream.Flush();

                bool matchedPrompt;
                string? updatedPromptLiteral;
                var output = ReadUntilPromptWithPager(shellStream, promptRegex, delayMs, idleTimeoutMs, out matchedPrompt, out updatedPromptLiteral);

                if (!string.IsNullOrEmpty(updatedPromptLiteral) && !string.Equals(updatedPromptLiteral, promptText, StringComparison.Ordinal))
                {
                    promptText = updatedPromptLiteral;
                    promptRegex = BuildPromptRegex(promptText);
                }

                if (output.StartsWith(commandToExecute + "\r\r\n", StringComparison.Ordinal))
                {
                    output = Regex.Replace(output, Regex.Escape(commandToExecute) + "\r\r\n", commandToExecute + "\r\n");
                }

                if (isRunning)
                {
                    this.Invoke(new Action(() =>
                    {
                        txtOutput.AppendText(output);
                    }));
                }
            }

            // Add this here to capture raw input/output if enabled
            if (_debugCaptureRaw && _rawBuffer is { Length: > 0 })
            {
                File.WriteAllText(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"ssh_raw_{DateTime.Now:yyyyMMdd_HHmmss}.txt"),
                    _rawBuffer.ToString(), new UTF8Encoding(false));
                _rawBuffer.Clear();
            }
        }

        private static string FlattenException(Exception ex)
        {
            var sb = new StringBuilder();
            for (var e = ex; e != null; e = e.InnerException)
            {
                sb.AppendLine($"{e.GetType().Name}: {e.Message}");
            }
            return sb.ToString();
        }

        // Reads available data until inactivity (maxInactivityMs). Sanitizes and handles pager tokens.
        private string ReadAvailable(ShellStream shellStream, int pollIntervalMs, int maxInactivityMs, int maxOverallMs = 2000, bool stopOnLikelyPrompt = true)
        {
            var sb = new StringBuilder();
            var sw = Stopwatch.StartNew();
            long lastDataMs = sw.ElapsedMilliseconds;
            int idleQuietMs = Math.Clamp(pollIntervalMs * 3, 100, 400);

            while (isRunning)
            {
                if (sw.ElapsedMilliseconds >= maxOverallMs) break;
                if (sw.ElapsedMilliseconds - lastDataMs >= Math.Min(maxInactivityMs, maxOverallMs)) break;

                if (shellStream.DataAvailable)
                {
                    string chunk = shellStream.Read();

                    // Capture raw before any processing
                    if (_debugCaptureRaw) _rawBuffer?.Append(chunk);

                    if (!string.IsNullOrEmpty(chunk))
                    {
                        lastDataMs = sw.ElapsedMilliseconds;

                        // Sanitize
                        chunk = Regex.Replace(chunk, @"[^\u0020-\u007E\r\n\t\b\u001B]", "");

                        bool sawPager;
                        chunk = StripPagerArtifacts(chunk, out sawPager);
                        sb.Append(chunk);

                        if (sawPager)
                        {
                            shellStream.Write(" ");
                            shellStream.Flush();
                        }

                        if (stopOnLikelyPrompt)
                        {
                            if (TryDetectPromptFromBufferTail(sb.ToString(), out _))
                            {
                                if (sw.ElapsedMilliseconds - lastDataMs >= idleQuietMs)
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    Thread.Sleep(Math.Min(50, Math.Max(10, pollIntervalMs / 2)));
                }
            }

            // Add this to capture raw input/output if enabled
            if (_debugCaptureRaw)
            {
                _rawBuffer?.Append(sb.ToString());
            }

            return sb.ToString();
        }

        // Read until the known prompt reappears (pager-aware). Will return earlier only if inactivity timeout elapses.
        private string ReadUntilPromptWithPager(ShellStream shellStream, Regex promptRegex, int pollIntervalMs, int maxInactivityMs, out bool matchedPrompt, out string? updatedPromptLiteral)
        {
            matchedPrompt = false;
            updatedPromptLiteral = null;

            var sb = new StringBuilder();
            var sw = Stopwatch.StartNew();
            long lastActivityMs = 0;
            int pageCount = 0;
            const int maxPages = 50000;

            while (isRunning && pageCount < maxPages)
            {
                if (sw.ElapsedMilliseconds - lastActivityMs > maxInactivityMs)
                    break;

                if (shellStream.DataAvailable)
                {
                    string chunk = shellStream.Read();

                    // Capture raw before any processing
                    if (_debugCaptureRaw) _rawBuffer?.Append(chunk);

                    if (!string.IsNullOrEmpty(chunk))
                    {
                        lastActivityMs = sw.ElapsedMilliseconds;

                        // Sanitize
                        chunk = Regex.Replace(chunk, @"[^\u0020-\u007E\r\n\t\b\u001B]", "");

                        bool sawPager;
                        chunk = StripPagerArtifacts(chunk, out sawPager);
                        sb.Append(chunk);

                        if (sawPager)
                        {
                            lastActivityMs = sw.ElapsedMilliseconds;
                            shellStream.Write(" ");
                            shellStream.Flush();
                            pageCount++;
                            continue;
                        }

                        if (BufferEndsWithPrompt(sb, promptRegex))
                        {
                            matchedPrompt = true;
                            break;
                        }

                        if (!matchedPrompt)
                        {
                            if (TryDetectDifferentPromptTail(sb, promptRegex, out var differentPrompt))
                            {
                                // We discovered a new prompt form (e.g. entered/exited a config mode).
                                matchedPrompt = true;             // Treat as end of command output.
                                updatedPromptLiteral = differentPrompt; // Signal caller to rebuild regex adaptively.
                                break;
                            }
                        }
                    }
                }
                else
                {
                    Thread.Sleep(Math.Min(50, Math.Max(10, pollIntervalMs / 2)));
                }
            }

            var resultRaw = sb.ToString();

            // Update prompt if possible (tail heuristic)
            if (!matchedPrompt && TryDetectPromptFromBufferTail(resultRaw, out var tailPrompt) && !string.IsNullOrWhiteSpace(tailPrompt))
            {
                updatedPromptLiteral = tailPrompt;
            }

            // Normalize CR/LF at the end (prevents overwrites like "set wifi-certificate" -> "ate ...")
            var result = NormalizeTerminalOutput(resultRaw);
            return result;
        }

        private static Regex BuildPromptRegex(string promptLiteral)
        {
            // Generic fallback if nothing known yet
            if (string.IsNullOrWhiteSpace(promptLiteral))
                return new Regex(@"^.*(?:[#>$%])[ \t]*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

            // Trim trailing whitespace
            var trimmed = Regex.Replace(promptLiteral, @"\s+$", "");

            // Remove trailing ANSI (already mostly sanitized earlier, but be safe)
            trimmed = Regex.Replace(trimmed, @"\x1B\[[0-9;]*[A-Za-z]", "");

            // Ensure it ends with a typical prompt terminator; if not, fall back
            if (!Regex.IsMatch(trimmed, @"[#>$%]\s*$"))
                return new Regex(@"^.*(?:[#>$%])[ \t]*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

            // Strip final prompt char for base extraction
            char terminator = trimmed[^1];
            string body = trimmed[..^1].TrimEnd();

            // Split off any mode/context portion (parenthetical) but allow it to vary
            // e.g. "MSD903-DFWB (setting)" => baseHost = "MSD903-DFWB"
            string baseHost;
            int parenIdx = body.IndexOf('(');
            if (parenIdx > 0)
                baseHost = body[..parenIdx].TrimEnd();
            else
                baseHost = body;

            if (string.IsNullOrWhiteSpace(baseHost))
                baseHost = body; // fallback

            string baseEsc = Regex.Escape(baseHost);

            // Build adaptive pattern:
            // ^<base>(\s*\([^)]+\))?\s*[#>$%]\s*$
            string pattern = $"^{baseEsc}(?:\\s*\\([^)]+\\))?\\s*[{Regex.Escape(terminator.ToString())}#>$%]\\s*$";

            return new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        }

        private static bool TryDetectDifferentPromptTail(StringBuilder sb, Regex currentPromptRegex, out string newPrompt)
        {
            newPrompt = "";
            if (sb.Length == 0) return false;

            int lookback = Math.Min(4096, sb.Length);
            string tail = sb.ToString(sb.Length - lookback, lookback);
            var lines = tail.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].TrimEnd();
                if (IsLikelyPrompt(line) && !currentPromptRegex.IsMatch(line))
                {
                    newPrompt = line;
                    return true;
                }
                if (line.Length > 0) break;
            }
            return false;
        }

        private static bool BufferEndsWithPrompt(StringBuilder sb, Regex promptRegex)
        {
            if (sb.Length == 0) return false;

            // Look back only at the tail to reduce cost
            int lookback = Math.Min(4096, sb.Length);
            string tail = sb.ToString(sb.Length - lookback, lookback);

            // Grab last non-empty line and test the prompt regex
            var lines = tail.Split(new[] { "\r\n" }, StringSplitOptions.None);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (line.Length == 0) continue;
                if (promptRegex.IsMatch(line))
                    return true;
                // Only test the last non-empty line
                break;
            }
            return false;
        }

        private static bool IsLikelyPrompt(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            line = line.TrimEnd();

            // Heuristic: common CLI prompt endings
            char last = line[^1];
            if (last is '#' or '>' or '$' or '%')
                return true;

            return false;
        }

        private static bool TryDetectPromptFromBuffer(string buffer, out string prompt)
        {
            prompt = "";
            if (string.IsNullOrEmpty(buffer)) return false;

            var lines = buffer.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var candidate = lines[i].TrimEnd();
                if (IsLikelyPrompt(candidate))
                {
                    prompt = candidate;
                    return true;
                }
            }
            return false;
        }

        private static bool TryDetectPromptFromBufferTail(string buffer, out string prompt)
        {
            // Similar to TryDetectPromptFromBuffer but optimized for tail-only checks
            prompt = "";
            if (string.IsNullOrEmpty(buffer)) return false;

            int lookback = Math.Min(4096, buffer.Length);
            string tail = buffer.Substring(buffer.Length - lookback);
            var lines = tail.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string candidate = lines[i].TrimEnd();
                if (IsLikelyPrompt(candidate))
                {
                    prompt = candidate;
                    return true;
                }
            }
            return false;
        }

        // Remove pager artifacts and tells us if a pager token was present this chunk.
        private string StripPagerArtifacts(string chunk, out bool sawPager)
        {
            sawPager = false;

            // Match common pager prompts:
            //  - "-- More --", "--More--", "----More----", etc. (case-insensitive)
            var pagerRegex = new Regex(@"(?:(?:--\s*More\s*--)|(?:-+\s*More\s*-+))",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (pagerRegex.IsMatch(chunk))
            {
                sawPager = true;
                chunk = pagerRegex.Replace(chunk, string.Empty);
            }

            return chunk;
        }

        private void HandleException(bool AuthException, Exception ex, string ipAddress, int port)
        {
            if (isRunning == false)
            {
                return;
            }
            if (!string.IsNullOrEmpty(txtOutput.Text))
            {
                this.Invoke(new Action(() =>
                {
                    txtOutput.AppendText(Environment.NewLine);
                }));
            }

            string title = AuthException
                ? $"{new string('#', 20)} ERROR AUTHENTICATING TO!!! {ipAddress}:{port} {new string('#', 20)}"
                : $"{new string('#', 20)} ERROR CONNECTING TO!!! {ipAddress}:{port} {new string('#', 20)}";

            string preheader = new string('#', title.Length);
            string details = FlattenException(ex);

            this.Invoke(new Action(() =>
            {
                txtOutput.AppendText(preheader + Environment.NewLine + title + Environment.NewLine + preheader + Environment.NewLine);
                if (!string.IsNullOrWhiteSpace(details))
                {
                    txtOutput.AppendText(details + Environment.NewLine);
                }
            }));
        }

        private void btnExecuteAll_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                // Set the wait cursor for the entire form and each control individually
                this.Cursor = Cursors.WaitCursor;
                foreach (Control ctrl in this.Controls)
                {
                    ctrl.Cursor = Cursors.WaitCursor; // Ensure wait cursor is set for each control
                }

                lstOutput.Enabled = false;
                isRunning = true;
                btnStopAll.Visible = true;

                Task.Run(() =>
                {
                    ExecuteCommands(dgv_variables.Rows.Cast<DataGridViewRow>(), cts.Token);
                })
                .ContinueWith(task =>
                {
                    this.Invoke(new Action(() =>
                    {
                        // Reset the cursor for the entire form and each control when the task completes
                        this.Cursor = Cursors.Default;
                        foreach (Control ctrl in this.Controls)
                        {
                            ctrl.Cursor = Cursors.Default; // Reset cursor for each control
                        }

                        btnStopAll.Visible = false;
                        isRunning = false;
                        lstOutput.Enabled = true;
                        if (task.Exception != null)
                            MessageBox.Show("An error occurred: " + task.Exception.InnerException.Message);
                    }));
                });
            }
        }

        private void btnExecuteSelected_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            lstOutput.Enabled = false;

            if (dgv_variables.CurrentCell == null)
            {
                txtOutput.Clear();
                txtOutput.AppendText("No host selected");
                lstOutput.Enabled = true;
                Cursor.Current = Cursors.Default;
                return;
            }

            DataGridViewRow selectedRow = dgv_variables.Rows[dgv_variables.CurrentCell.RowIndex];

            bool isNew = selectedRow.IsNewRow;

            // Fixed line:
            string host = dgv_variables.Columns.Contains("Host_IP")
                ? (selectedRow.Cells["Host_IP"].Value?.ToString() ?? "")
                : "";

            if (isNew || string.IsNullOrWhiteSpace(host) || !IsValidIPAddress(host))
            {
                txtOutput.Clear();
                txtOutput.AppendText("No host selected");
                lstOutput.Enabled = true;
                Cursor.Current = Cursors.Default;
                return;
            }

            isRunning = true;
            try
            {
                ExecuteCommands(new[] { selectedRow }, cts.Token);
            }
            finally
            {
                isRunning = false;
                lstOutput.Enabled = true;
                Cursor.Current = Cursors.Default;
            }
        }

        private bool IsValidIPAddress(string ipAddressWithPort)
        {
            string[] parts = ipAddressWithPort.Split(':');
            string ipAddress = parts[0];
            string[] octets = ipAddress.Split('.');

            if (octets.Length != 4)
            {
                return false;
            }

            foreach (string octet in octets)
            {
                if (!int.TryParse(octet, out int value) || value < 0 || value > 255)
                {
                    return false;
                }
            }

            if (parts.Length > 1) // Check if port is specified and valid
            {
                if (!int.TryParse(parts[1], out int port) || port <= 0 || port > 65535)
                {
                    return false;
                }
            }

            return true;
        }

        private void SavePresets()
        {
            ConfigObject existing;
            try
            {
                existing = JsonConvert.DeserializeObject<ConfigObject>(File.ReadAllText(configFilePath)) ?? new ConfigObject();
            }
            catch
            {
                existing = new ConfigObject();
            }

            existing.Presets = presets;
            // Keep last global delay/timeout & username synced
            if (int.TryParse(txtDelay.Text, out int d)) existing.Delay = d;
            if (int.TryParse(txtTimeout.Text, out int t)) existing.Timeout = t;
            existing.Username = txtUsername.Text;

            string json = JsonConvert.SerializeObject(existing, Formatting.Indented);
            File.WriteAllText(configFilePath, json);
        }

        private void CreateDefaultConfigFile()
        {
            var defaultPresets = new Dictionary<string, PresetInfo>
    {
        { "Custom", new PresetInfo { Commands = "get system status" } },
        { "Get external-address-resource list", new PresetInfo { Commands = "dia sys external-address-resource list" } }
    };

            // Set a default username
            var defaultUsername = "";

            // Combine both username and presets into a single object for serialization
            var settings = new
            {
                Timeout = 10,
                Delay = 500,
                Username = defaultUsername,
                Presets = defaultPresets
            };

            // Serialize the settings object to JSON
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            // Write to the configuration file
            File.WriteAllText(configFilePath, json);

            // Assign the default presets to the global presets variable
            presets = defaultPresets;

            // Load presets into the list box
            foreach (var preset in defaultPresets)
            {
                lstPreset.Items.Add(preset.Key);
            }

        }
        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Check if there is a selected item to delete
            if (lstPreset.SelectedItem == null) return;

            // Get the current index of the selected item
            int selectedIndex = lstPreset.SelectedIndex;
            string selectedPreset = lstPreset.SelectedItem.ToString();

            if (presets.ContainsKey(selectedPreset))
            {
                // Remove the item from the dictionary and save the changes
                presets.Remove(selectedPreset);
                SavePresets();

                // Remove the item from the ListBox
                lstPreset.Items.Remove(selectedPreset);

                // Determine the new index to select after deletion
                if (lstPreset.Items.Count > 0)  // Check if there are any items left
                {
                    if (selectedIndex > 0) // If not the first item was deleted
                    {
                        lstPreset.SelectedIndex = selectedIndex - 1; // Select the previous item
                    }
                    else
                    {
                        lstPreset.SelectedIndex = 0; // Select the new first item if the first item was deleted
                    }
                }
            }
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //rename selected lstpreset
            if (lstPreset.SelectedItem == null) return;
            string selectedPreset = lstPreset.SelectedItem.ToString();
            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                               $"Enter a new name for the preset '{selectedPreset}':",
                                              "Rename Preset",
                                                             selectedPreset
                                                                        );

            if (!string.IsNullOrEmpty(newName) && newName != selectedPreset)
            {
                // Ensure the new name doesn't already exist to avoid duplicates
                foreach (var preset in presets)
                {
                    if (preset.Key.Equals(newName, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("This preset name already exists. Please choose a different name.", "Rename Preset Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                // Set the new header text
                presets[newName] = presets[selectedPreset];
                presets.Remove(selectedPreset);
                SavePresets();
                lstPreset.Items.Remove(selectedPreset);
                lstPreset.Items.Add(newName);
                lstPreset.SelectedItem = newName;
                txtPreset.Text = newName;
                _activePresetName = newName;
            }


        }

        private void addPresetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Add new preset
            string presetName = Microsoft.VisualBasic.Interaction.InputBox(
                               "Enter the name of the new preset:",
                                              "Add Preset",
                                                             "New Preset"
                                                                        );

            if (!string.IsNullOrEmpty(presetName))
            {
                if (!presets.ContainsKey(presetName))
                {
                    presets.Add(presetName, new PresetInfo());
                    lstPreset.Items.Add(presetName);
                }
                else
                {
                    MessageBox.Show("Preset name already exists!");
                }
            }

            // Save presets
            SavePresets();
        }

        private void contextPresetLstAdd_Click(object sender, EventArgs e)
        {
            // call addPresetToolStripMenuItem_Click
            addPresetToolStripMenuItem_Click(sender, e);
        }

        private void toggleSortingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Toggle the Sorted property
            lstPreset.Sorted = !lstPreset.Sorted;
        }

        private void toggleSortingToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            toggleSortingToolStripMenuItem_Click(sender, e);
        }

        private void txtDelay_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Allow only digits and control characters
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void txtTimeout_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Allow only digits and control characters
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void lstOutput_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstOutput.SelectedItem != null)
            {
                var selectedEntry = (KeyValuePair<string, string>)lstOutput.SelectedItem;
                txtOutput.Text = selectedEntry.Value;  // Load the associated output into txtOutput
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (lstOutput.SelectedItem == null)
            {
                MessageBox.Show("Please select an item from the list to save.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                // Retrieve the selected item's value
                var selectedItem = (KeyValuePair<string, string>)lstOutput.SelectedItem;
                string outputText = selectedItem.Value;

                saveFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                saveFileDialog.DefaultExt = "txt";
                saveFileDialog.AddExtension = true;
                saveFileDialog.Title = "Save As";
                var filename = selectedItem.Key;
                filename = filename.Replace(":", "_");
                saveFileDialog.FileName = filename;


                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Write the output to the selected file path
                    try
                    {
                        File.WriteAllText(saveFileDialog.FileName, outputText);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to save the file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

        }

        private void deleteEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //delete selected entry in lstOutput
            if (lstOutput.SelectedItem == null)
            {
                MessageBox.Show("Please select an item from the list to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            //get the key for the lstOutput.SelectedItem
            var selectedEntry = (KeyValuePair<string, string>)lstOutput.SelectedItem;

            //get confirmation they want to delete
            DialogResult dialogResult = MessageBox.Show("Are you sure you want to delete " + selectedEntry.Key + "?", "Delete Entry", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.No)
            {
                return;
            }
            // Remove the selected entry from the list
            outputHistoryList.Remove((KeyValuePair<string, string>)lstOutput.SelectedItem);
            //select next history if available otherwise clear txtOutput
            if (lstOutput.Items.Count > 0)
            {
                lstOutput.SelectedIndex = 0;
            }
            else
            {
                txtOutput.Clear();
            }

        }

        private void deleteAllHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //get confirmation they want to delete
            DialogResult dialogResult = MessageBox.Show("Are you sure you want to delete all history?", "Delete History", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.No)
            {
                return;
            }
            //clear all entries in lstOutput and outputHistoryList
            outputHistoryList.Clear();
            txtOutput.Clear();

        }

        private void btnStopAll_Click(object sender, EventArgs e)
        {
            isRunning = false;
            cts.Cancel();
            this.Invoke(new Action(() =>
            {
                Thread.Sleep(300);
                btnStopAll.Visible = false;
                txtOutput.AppendText(Environment.NewLine + Environment.NewLine + "Execution Stopped by User" + Environment.NewLine);
            }));
            //reset cancel token
            cts.Dispose();
            cts = new CancellationTokenSource();

        }

        private string GetUniquePresetName(string baseName)
        {
            string candidate = baseName;
            int i = 1;
            while (presets.ContainsKey(candidate))
            {
                candidate = $"{baseName}_{i++}";
            }
            return candidate;
        }

        private void duplicatePresetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstPreset.SelectedItem == null) return;

            string sourceName = lstPreset.SelectedItem.ToString();
            if (!presets.TryGetValue(sourceName, out var commandText)) return;

            string suggested = GetUniquePresetName(sourceName + "_Copy");

            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter name for the copied preset (from '{sourceName}'):",
                "Copy Preset",
                suggested
            );

            if (string.IsNullOrWhiteSpace(newName)) return;

            if (presets.ContainsKey(newName))
            {
                MessageBox.Show("A preset with that name already exists.", "Copy Preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            presets.Add(newName, new PresetInfo
            {
                Commands = commandText.Commands,
                Delay = presets[sourceName].Delay,
                Timeout = presets[sourceName].Timeout
            });

            if (lstPreset.Sorted)
            {
                lstPreset.Items.Add(newName);
            }
            else
            {
                int insertIndex = lstPreset.SelectedIndex + 1;
                if (insertIndex > lstPreset.Items.Count) insertIndex = lstPreset.Items.Count;
                lstPreset.Items.Insert(insertIndex, newName);
            }

            SavePresets();
            lstPreset.SelectedItem = newName;
            txtPreset.Text = newName;
            txtCommand.Text = commandText.Commands;
            _activePresetName = newName;
        }

        // === Find Support ===
        private FindDialog? _findDialog;
        private string _lastFindTerm = "";
        private bool _lastFindMatchCase = false;
        private bool _lastFindWrap = true;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Global shortcuts
            if (keyData == (Keys.Control | Keys.F))
            {
                ShowFindDialog();
                return true;
            }
            if (keyData == Keys.F3)
            {
                // Global F3 should move focus to the output area (so we pass initiatedFromFindDialog: false)
                PerformFind(_lastFindTerm, _lastFindMatchCase, forward: true, _lastFindWrap, initiatedFromFindDialog: false);
                return true;
            }
            if (keyData == (Keys.Shift | Keys.F3))
            {
                PerformFind(_lastFindTerm, _lastFindMatchCase, forward: false, _lastFindWrap, initiatedFromFindDialog: false);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ShowFindDialog()
        {
            string seed = txtOutput.SelectedText;
            if (string.IsNullOrWhiteSpace(seed))
            {
                // If no selection, try last term
                seed = string.IsNullOrWhiteSpace(_lastFindTerm) ? "" : _lastFindTerm;
            }

            if (_findDialog == null || _findDialog.IsDisposed)
            {
                _findDialog = new FindDialog(this, seed, _lastFindMatchCase, _lastFindWrap);
                // Position near txtOutput (basic positioning)
                var screenPoint = txtOutput.PointToScreen(Point.Empty);
                _findDialog.StartPosition = FormStartPosition.Manual;
                _findDialog.Left = screenPoint.X + 40;
                _findDialog.Top = screenPoint.Y + 40;
            }
            else
            {
                _findDialog.Show();
                _findDialog.BringToFront();
            }

            _findDialog.Show();
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

            bool found = PerformFind(term, matchCase, forward: true, wrap, initiatedFromFindDialog: true);
            if (!found)
                _findDialog?.SetStatus("Not found.", true);
            else
                _findDialog?.SetStatus("Found.");
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

            bool found = PerformFind(term, matchCase, forward: false, wrap, initiatedFromFindDialog: true);
            if (!found)
                _findDialog?.SetStatus("Not found.", true);
            else
                _findDialog?.SetStatus("Found.");
        }

        private bool PerformFind(string term, bool matchCase, bool forward, bool wrap, bool initiatedFromFindDialog)
        {
            if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(txtOutput.Text))
                return false;

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string text = txtOutput.Text;
            int startIndex;

            if (forward)
            {
                startIndex = txtOutput.SelectionStart + txtOutput.SelectionLength;
                if (startIndex > text.Length) startIndex = text.Length;

                int idx = text.IndexOf(term, startIndex, comparison);
                if (idx == -1 && wrap)
                {
                    idx = text.IndexOf(term, 0, comparison);
                }
                if (idx == -1)
                    return false;

                HighlightAndScroll(idx, term.Length, initiatedFromFindDialog);
                return true;
            }
            else
            {
                startIndex = txtOutput.SelectionStart - 1;
                if (startIndex < 0) startIndex = text.Length - 1;

                int idx = LastIndexOf(text, term, startIndex, comparison);
                if (idx == -1 && wrap)
                {
                    idx = LastIndexOf(text, term, text.Length - 1, comparison);
                }
                if (idx == -1)
                    return false;

                HighlightAndScroll(idx, term.Length, initiatedFromFindDialog);
                return true;
            }
        }

        private static int LastIndexOf(string source, string term, int startIndex, StringComparison comparison)
        {
            if (startIndex < 0) return -1;
            if (string.IsNullOrEmpty(term)) return -1;
            // Walk backwards manually (supports case-insensitive)
            int lastPossible = startIndex - term.Length + 1;
            for (int i = lastPossible; i >= 0; i--)
            {
                if (string.Compare(source, i, term, 0, term.Length, comparison) == 0)
                    return i;
            }
            return -1;
        }

        private void HighlightAndScroll(int index, int length, bool initiatedFromFindDialog)
        {
            try
            {
                txtOutput.SelectionStart = index;
                txtOutput.SelectionLength = length;
                txtOutput.ScrollToCaret();

                if (initiatedFromFindDialog)
                {
                    // Keep the Find dialog active & ready for another Enter
                    if (_findDialog != null && !_findDialog.IsDisposed && _findDialog.Visible)
                    {
                        _findDialog.Activate();
                        // Re-focus textbox for quick repeated Enter
                        var findBox = _findDialog.ActiveControl as TextBox;
                        // Fallback: explicitly focus the find textbox if we have a reference
                        // (in this simple dialog ActiveControl will usually be txtFind already)
                    }
                }
                else
                {
                    // Global F3 navigation puts focus in output
                    txtOutput.Focus();
                }
            }
            catch
            {
                // Ignore UI race conditions
            }
        }

        private void contextHistoryLst_Opening(object sender, CancelEventArgs e)
        {

        }

        private void InitializePresetExportImportMenuItems()
        {
            if (contextPresetLst != null)
            {
                // Avoid duplicates if called defensively
                if (!contextPresetLst.Items.OfType<ToolStripMenuItem>().Any(i => i.Name == "importPresetToolStripMenuItem"))
                {
                    var importItem = new ToolStripMenuItem("Import Preset", null, importPresetToolStripMenuItem_Click)
                    {
                        Name = "importPresetToolStripMenuItem"
                    };
                    contextPresetLst.Items.Add(new ToolStripSeparator());
                    contextPresetLst.Items.Add(importItem);
                }
                if (!contextPresetLst.Items.OfType<ToolStripMenuItem>().Any(i => i.Name == "exportPresetToolStripMenuItem"))
                {
                    var exportItem = new ToolStripMenuItem("Export Preset", null, exportPresetToolStripMenuItem_Click)
                    {
                        Name = "exportPresetToolStripMenuItem"
                    };

                    contextPresetLst.Items.Add(exportItem);
                }

            }

            if (contextPresetLstAdd != null)
            {
                if (!contextPresetLstAdd.Items.OfType<ToolStripMenuItem>().Any(i => i.Name == "importPresetToolStripMenuItem"))
                {
                    var importItem2 = new ToolStripMenuItem("Import Preset", null, importPresetToolStripMenuItem_Click)
                    {
                        Name = "importPresetToolStripMenuItem"
                    };
                    contextPresetLstAdd.Items.Add(new ToolStripSeparator());
                    contextPresetLstAdd.Items.Add(importItem2);
                }
            }
        }

        private void exportPresetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstPreset.SelectedItem == null)
            {
                MessageBox.Show("No preset selected to export.", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string presetName = lstPreset.SelectedItem.ToString();
            if (!presets.TryGetValue(presetName, out var info))
            {
                MessageBox.Show("Preset not found in memory.", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string exportString = CreatePresetExportString(presetName, info);
                Clipboard.SetText(exportString);
                MessageBox.Show("Preset exported to clipboard.", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to export preset: " + ex.Message, "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void importPresetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Paste the encoded preset string:\r\nFormat: <name>_<encoded>",
                "Import Preset",
                ""
            );

            if (string.IsNullOrWhiteSpace(input))
                return;

            int lastUnderscore = input.LastIndexOf('_');
            if (lastUnderscore <= 0 || lastUnderscore >= input.Length - 1)
            {
                MessageBox.Show("Invalid format. Expected <name>_<encoded>.", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string importedName = input.Substring(0, lastUnderscore);
            string encoded = input.Substring(lastUnderscore + 1);

            try
            {
                var importedInfo = ParseImportedPresetPayload(encoded);

                // Ensure unique name if collision
                string finalName = importedName;
                if (presets.ContainsKey(finalName))
                {
                    finalName = GetUniquePresetName(finalName);
                }

                presets[finalName] = importedInfo;

                if (!lstPreset.Items.Contains(finalName))
                {
                    lstPreset.Items.Add(finalName);
                }

                lstPreset.SelectedItem = finalName;
                txtPreset.Text = finalName;
                txtCommand.Text = importedInfo.Commands;
                if (importedInfo.Delay.HasValue) txtDelay.Text = importedInfo.Delay.Value.ToString();
                if (importedInfo.Timeout.HasValue) txtTimeout.Text = importedInfo.Timeout.Value.ToString();

                SavePresets();
                MessageBox.Show($"Preset '{finalName}' imported.", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (FormatException)
            {
                MessageBox.Show("Encoded section is not valid Base64.", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (InvalidDataException)
            {
                MessageBox.Show("Failed to decompress data. The string may be corrupted.", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to import preset: " + ex.Message, "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string CreatePresetExportString(string presetName, PresetInfo info)
        {
            var payload = new
            {
                v = 1,
                commands = info.Commands ?? "",
                delay = info.Delay,
                timeout = info.Timeout
            };
            string json = JsonConvert.SerializeObject(payload);
            string encoded = CompressAndEncode(json);
            return $"{presetName}_{encoded}";
        }

        private PresetInfo ParseImportedPresetPayload(string encoded)
        {
            string decompressed = DecompressEncoded(encoded);

            // If payload looks like JSON attempt to parse structured export
            if (decompressed.Length > 0 && decompressed.TrimStart().StartsWith("{"))
            {
                try
                {
                    var obj = JObject.Parse(decompressed);
                    // commands key (case-insensitive fallback)
                    string commands =
                        obj["commands"]?.ToString() ??
                        obj["Commands"]?.ToString() ??
                        "";
                    int? delay = obj["delay"]?.Type == JTokenType.Null ? null : obj["delay"]?.Value<int?>();
                    int? timeout = obj["timeout"]?.Type == JTokenType.Null ? null : obj["timeout"]?.Value<int?>();

                    return new PresetInfo
                    {
                        Commands = commands,
                        Delay = delay,
                        Timeout = timeout
                    };
                }
                catch
                {
                    // Fall back to treating decompressed text as raw commands
                }
            }

            return new PresetInfo
            {
                Commands = decompressed,
                Delay = int.TryParse(txtDelay.Text, out var dVal) ? dVal : null,
                Timeout = int.TryParse(txtTimeout.Text, out var tVal) ? tVal : null
            };
        }
        private string CompressAndEncode(string text)
        {
            byte[] raw = Encoding.UTF8.GetBytes(text);
            using var ms = new MemoryStream();
            using (var gzip = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(raw, 0, raw.Length);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        private string DecompressEncoded(string encoded)
        {
            byte[] compressed = Convert.FromBase64String(encoded);
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        private void saveAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (outputHistoryList.Count == 0)
            {
                MessageBox.Show("There is no history to save.", "No History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                saveFileDialog.DefaultExt = "txt";
                saveFileDialog.AddExtension = true;
                saveFileDialog.Title = "Save All History";
                saveFileDialog.FileName = $"SSH_Helper_History_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var sw = new StreamWriter(saveFileDialog.FileName, false, new UTF8Encoding(false)))
                        {
                            for (int i = 0; i < outputHistoryList.Count; i++)
                            {
                                var entry = outputHistoryList[i];

                                string header = $"===== {entry.Key} =====";
                                sw.WriteLine(header);
                                sw.WriteLine();

                                // Normalize newlines to Windows CRLF for consistency in the saved file
                                string body = (entry.Value ?? string.Empty).Replace("\r\n", "\n").Replace("\n", "\r\n");
                                if (!string.IsNullOrEmpty(body))
                                    sw.WriteLine(body);

                                // Separate entries with a blank line (add an extra line between blocks)
                                if (i < outputHistoryList.Count - 1)
                                {
                                    sw.WriteLine();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to save the file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var dlg = new AboutDialog(ApplicationName, ApplicationVersion);
            dlg.ShowDialog(this);
        }

        private void openCSVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!EnsureCsvChangesSavedBeforeReplacing())
                return;

            OpenCsvInteractive();
        }

        private void saveAsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV files (*.csv)|*.csv";
                sfd.Title = "Save as CSV";
                if (!string.IsNullOrEmpty(loadedFilePath))
                    sfd.FileName = Path.GetFileName(loadedFilePath);

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    SaveDataGridViewToCSV(sfd.FileName);
                    loadedFilePath = sfd.FileName; // capture chosen path
                }
            }
        }

        private bool SaveCurrentCsv(bool promptIfNoPath = true)
        {
            // Commit any in-progress edit so the latest cell value is persisted
            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            if (string.IsNullOrWhiteSpace(loadedFilePath))
            {
                if (!promptIfNoPath) return false;
                saveAsToolStripMenuItem1_Click(this, EventArgs.Empty);
                return !string.IsNullOrWhiteSpace(loadedFilePath);
            }

            try
            {
                SaveDataGridViewToCSV(loadedFilePath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file:\r\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveCurrentCsv(promptIfNoPath: true);
        }

        private bool ConfirmExitWorkflow()
        {
            // If commands are running, confirm stop before exiting
            if (isRunning)
            {
                var result = MessageBox.Show(
                    "Execution is currently running. Stop and exit?",
                    "Exit",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                    return false;

                btnStopAll_Click(this, EventArgs.Empty);
            }

            // Commit any in-progress grid edit
            if (dgv_variables.IsCurrentCellInEditMode)
            {
                dgv_variables.EndEdit();
            }

            bool presetDirty = IsPresetDirty();

            // Offer to save CSV changes first
            if (_csvDirty)
            {
                var saveCsv = MessageBox.Show(
                    "You have unsaved CSV changes. Do you want to save before exiting?",
                    "Save Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (saveCsv == DialogResult.Cancel)
                    return false;

                if (saveCsv == DialogResult.Yes)
                {
                    if (!SaveCurrentCsv(promptIfNoPath: true))
                    {
                        // Save failed or cancelled in Save As
                        return false;
                    }
                }
            }

            // Offer to save preset changes
            if (presetDirty)
            {
                var savePreset = MessageBox.Show(
                    "You have unsaved preset changes. Do you want to save the preset before exiting?",
                    "Save Preset",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (savePreset == DialogResult.Cancel)
                    return false;

                if (savePreset == DialogResult.Yes)
                {
                    btnSave_Click(this, EventArgs.Empty);
                }
            }

            return true;
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            if (!ConfirmExitWorkflow())
                return;

            _exitConfirmed = true;
            this.Close(); // FormClosing will persist variables
        }

        private bool EnsureCsvChangesSavedBeforeReplacing()
        {
            // Commit any active edit so dirty flag is accurate
            if (dgv_variables.IsCurrentCellInEditMode)
                dgv_variables.EndEdit();

            if (!_csvDirty)
                return true;

            var result = MessageBox.Show(
                "You have unsaved CSV changes. Save before opening another file?",
                "Unsaved CSV",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
                return false;

            if (result == DialogResult.Yes)
            {
                if (!SaveCurrentCsv(promptIfNoPath: true))
                    return false; // Save failed or user cancelled Save As
            }

            // No = discard changes
            return true;
        }

        private void OpenCsvInteractive()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                ofd.Multiselect = false;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    loadedFilePath = ofd.FileName;
                    DataTable dataTable = LoadCsvIntoDataTable(ofd.FileName);
                    dgv_variables.Columns.Clear();
                    dgv_variables.DataSource = dataTable;
                    _csvDirty = false; // Freshly loaded
                }
            }
        }

        private bool IsPresetDirty()
        {
            string active = _activePresetName ?? "";
            string currentName = txtPreset.Text?.Trim() ?? "";
            string currentCommands = txtCommand.Text ?? "";

            bool anyFieldsEntered = !string.IsNullOrWhiteSpace(currentName) || !string.IsNullOrWhiteSpace(currentCommands);

            // If nothing has been loaded yet, any entered data counts as dirty (unsaved new preset)
            if (string.IsNullOrEmpty(active))
                return anyFieldsEntered;

            if (!presets.TryGetValue(active, out var info))
                return anyFieldsEntered;

            bool nameChanged = !string.Equals(currentName, active, StringComparison.Ordinal);
            bool commandsChanged = !string.Equals(currentCommands, info.Commands ?? "", StringComparison.Ordinal);

            bool delayDiffers;
            if (int.TryParse(txtDelay.Text, out var dVal))
                delayDiffers = info.Delay != dVal;
            else
                delayDiffers = info.Delay.HasValue;

            bool timeoutDiffers;
            if (int.TryParse(txtTimeout.Text, out var tVal))
                timeoutDiffers = info.Timeout != tVal;
            else
                timeoutDiffers = info.Timeout.HasValue;

            return nameChanged || commandsChanged || delayDiffers || timeoutDiffers;
        }

    }
}