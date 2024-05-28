using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Renci.SshNet;
using Renci.SshNet.Common;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace SSH_Helper
{

    public partial class Form1 : Form
    {
        public class ConfigObject
        {
            public Dictionary<string, string> Presets { get; set; }
            public string Username { get; set; }
            public int Delay { get; set; }
            public int Timeout { get; set; }
        }

        private int rightClickedColumnIndex = -1; // Field to store the index of the right-clicked column
        private int rightClickedRowIndex = -1; // Field to store the index of the right-clicked row
        private string loadedFilePath;
        private Dictionary<string, string> presets = new Dictionary<string, string>();
        private BindingList<KeyValuePair<string, string>> outputHistoryList = new BindingList<KeyValuePair<string, string>>();

        private string configFilePath;

        public Form1()
        {
            InitializeComponent();
            InitializeConfiguration();
            InitializeDataGridView();

            lstOutput.DataSource = outputHistoryList;
            lstOutput.DisplayMember = "Key";

            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            this.Click += Form_Click; // Handle clicks on the form area
            dgv_variables.ContextMenuStrip = contextMenuStrip1;
            dgv_variables.MouseDown += dgv_variables_MouseDown;
            dgv_variables.RowPostPaint += dgv_variables_RowPostPaint;
            dgv_variables.CellClick += dgv_variables_CellClick;
            dgv_variables.ColumnAdded += dgv_variables_ColumnAdded;
            dgv_variables.CellLeave += dgv_variables_CellLeave;
            AttachClickEventHandlers();

            // Set edit mode to programmatically only
            dgv_variables.EditMode = DataGridViewEditMode.EditProgrammatically;

            //presets
            lstPreset.MouseDown += lstPreset_MouseDown;
            txtDelay.KeyPress += txtDelay_KeyPress;

            txtTimeout.KeyPress += txtTimeout_KeyPress;
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
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Savevariables();  // Call this to save settings when the form is closing
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

                // Serialize the updated configuration and write it back to the file
                json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save variables: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void LoadConfiguration()
        {
            try
            {
                string json = File.ReadAllText(configFilePath);
                var rootObject = JsonConvert.DeserializeObject<ConfigObject>(json);

                // Set the loaded presets
                presets = rootObject.Presets;
                lstPreset.Items.Clear();
                foreach (var key in presets.Keys)
                {
                    lstPreset.Items.Add(key);
                }

                // Set the loaded username
                if (!string.IsNullOrEmpty(rootObject.Username))
                {
                    txtUsername.Text = rootObject.Username;
                }

                // Set the loaded delay
                if (rootObject.Delay > 0)
                {
                    txtDelay.Text = rootObject.Delay.ToString();
                }

                // Set the loaded timeout
                if (rootObject.Timeout > 0)
                {
                    txtTimeout.Text = rootObject.Timeout.ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void lstPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstPreset.SelectedItem != null)
            {
                string presetName = lstPreset.SelectedItem.ToString();
                if (presets.ContainsKey(presetName))
                {
                    txtCommand.Text = presets[presetName]; // Load the command associated with the preset
                    txtPreset.Text = presetName; // Display the selected preset name
                }
            }
        }

        private void dgv_variables_CellLeave(object sender, DataGridViewCellEventArgs e)
        {
            // Commit the edit when the cell loses focus
            dgv_variables.CommitEdit(DataGridViewDataErrorContexts.Commit);
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

                    dgv_variables.ContextMenuStrip = contextMenuStrip1; // Assign the custom context menu
                    contextMenuStrip1.Show(dgv_variables, e.Location); // Show the context menu at the cursor location
                }
                else
                {
                    rightClickedColumnIndex = -1;
                    rightClickedRowIndex = -1;
                    dgv_variables.ContextMenuStrip = contextMenuStrip1; // Ensure the custom menu is enabled elsewhere
                }
            }
        }

        private void btnOpenCSV_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                ofd.Multiselect = false;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    loadedFilePath = ofd.FileName;  // Store the loaded file path
                    DataTable dataTable = LoadCsvIntoDataTable(ofd.FileName);
                    dgv_variables.Columns.Clear();
                    dgv_variables.DataSource = dataTable;
                }
            }
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
            }
        }


        private void deleteColumnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Check if the index is valid
            if (rightClickedColumnIndex >= 0 && rightClickedColumnIndex < dgv_variables.Columns.Count)
            {
                dgv_variables.Columns.RemoveAt(rightClickedColumnIndex);
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
                    dgv_variables.Rows.RemoveAt(rightClickedRowIndex); // Remove the row
                                                                       // Adjust the selection after deletion
                    if (dgv_variables.Rows.Count > 0) // Check if there are any data rows left
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
                {
                    sfd.FileName = Path.GetFileName(loadedFilePath);  // Use the loaded file's name as the default
                }

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    SaveDataGridViewToCSV(sfd.FileName);
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
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string presetName = txtPreset.Text.Trim();
            string commands = txtCommand.Text;

            if (!string.IsNullOrEmpty(presetName) && !string.IsNullOrWhiteSpace(commands))
            {
                if (!presets.ContainsKey(presetName))
                {
                    presets.Add(presetName, commands);
                    lstPreset.Items.Add(presetName);
                }
                else
                {
                    presets[presetName] = commands; // Optionally update the preset
                }
                SavePresets(); // Save presets after updating
            }
            else
            {
                MessageBox.Show("Preset name or command is empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //execute
        private void ExecuteCommands(IEnumerable<DataGridViewRow> rows)
        {
            string[] commands = txtCommand.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            txtOutput.Font = new Font("Consolas", 10);
            txtOutput.Multiline = true;
            txtOutput.Clear(); // Clear previous output

            bool isFirst = true;
            foreach (DataGridViewRow row in rows)
            {
                if (row.IsNewRow) continue; // Skip new row placeholder

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
            outputHistoryList.Insert(0, entry);  // Insert at the start of the list
            lstOutput.SelectedIndex = 0;  // Select the newest entry automatically

            // Save the variables after each execution
            Savevariables();
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
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(int.Parse(txtTimeout.Text));
                    client.Connect();
                    using (var shellStream = client.CreateShellStream("xterm", 80, 24, 800, 600, 1024))
                    {
                        HandleCommandExecution(shellStream, commands, row, ref isFirst, ipAddress, port);
                    }
                    client.Disconnect();
                }
            }
            catch (SshAuthenticationException authEx)
            {
                HandleException(true, authEx, ipAddress, port);
            }
            catch (Exception ex)
            {
                HandleException(false, ex, ipAddress, port);
            }

        }

        private void HandleCommandExecution(ShellStream shellStream, string[] commands, DataGridViewRow row, ref bool isFirst, string ipAddress, int port)
        {
            shellStream.WriteLine(""); // Send a carriage return to trigger the prompt
            shellStream.Flush();
            Thread.Sleep(int.Parse(txtDelay.Text));
            var response = shellStream.Read();
            response = Regex.Replace(response, @"\x1B\[[0-?]*[ -/]*[@-~]", "");  // Remove ANSI escape codes
            response = response.Replace("\n", "\r\n");  // Make sure newlines are compatible with Windows
            string[] lines = response.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            string prompt = lines.LastOrDefault()?.Trim() ?? "";

            string header = $"{new string('#', 20)} CONNECTED TO!!! {ipAddress}:{port} {prompt} {new string('#', 20)}";
            string foo = new string('#', header.Length);

            if (!isFirst)
            {
                txtOutput.AppendText(Environment.NewLine);
            }
            isFirst = false;

            txtOutput.AppendText(foo + Environment.NewLine + header + Environment.NewLine + foo + Environment.NewLine + prompt);

            foreach (string commandTemplate in commands)
            {
                if (!string.IsNullOrWhiteSpace(commandTemplate) && !commandTemplate.StartsWith("#"))
                {
                    string commandToExecute = commandTemplate;
                    foreach (Match match in Regex.Matches(commandTemplate, @"\$\{([^}]+)\}"))
                    {
                        string variableName = match.Groups[1].Value;
                        string columnValue = row.Cells[variableName].Value?.ToString() ?? "";
                        commandToExecute = commandToExecute.Replace($"${{{variableName}}}", columnValue);
                    }

                    if (string.IsNullOrWhiteSpace(commandToExecute))
                    {
                        continue;
                    }

                    shellStream.WriteLine(commandToExecute);
                    Thread.Sleep(int.Parse(txtDelay.Text));
                    var output = shellStream.Read();

                    //if output starts with \r\n , remove it
                    if (output.StartsWith(commandToExecute + "\r\r\n"))
                    {
                        output = Regex.Replace(output, commandToExecute + "\r\r\n", commandToExecute + "\r\n");
                    }
                    output = Regex.Replace(output, @"[^\u0020-\u007E\r\n\t]", "");
                    //output = Regex.Replace(output, @"\x1B\[[0-?]*[ -/]*[@-~]", "");  // Remove ANSI escape codes

                    txtOutput.AppendText($"{output}");

                }
            }

        }

        private void HandleException(bool AuthException, Exception ex, string ipAddress, int port)
        {
            // only add new line if there is not already text in txtOutput
            if (!string.IsNullOrEmpty(txtOutput.Text))
            {
                txtOutput.AppendText(Environment.NewLine);
            }
            if (AuthException)
            {

                string errorMessage = $"{new string('#', 20)} ERROR AUTHENTICATING TO!!! {ipAddress}:{port} {new string('#', 20)}";
                string preheader = new string('#', errorMessage.Length);
                txtOutput.AppendText(preheader + Environment.NewLine + errorMessage + Environment.NewLine + preheader + Environment.NewLine + Environment.NewLine);
            }
            else
            {
                string errorMessage = $"{new string('#', 20)} ERROR CONNECTING TO!!! {ipAddress}:{port} {new string('#', 20)}";
                string preheader = new string('#', errorMessage.Length);
                txtOutput.AppendText(preheader + Environment.NewLine + errorMessage + Environment.NewLine + preheader + Environment.NewLine + Environment.NewLine);
            }
        }

        private void btnExecuteAll_Click(object sender, EventArgs e)
        {
            ExecuteCommands(dgv_variables.Rows.Cast<DataGridViewRow>());
        }

        private void btnExecuteSelected_Click(object sender, EventArgs e)
        {
            if (dgv_variables.CurrentCell != null)
            {
                DataGridViewRow selectedRow = dgv_variables.Rows[dgv_variables.CurrentCell.RowIndex];
                ExecuteCommands(new[] { selectedRow });
            }
            else
            {
                MessageBox.Show("No cell selected.");
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
            var wrapper = new { Presets = presets };
            string json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
            File.WriteAllText(configFilePath, json);
        }

        private void CreateDefaultConfigFile()
        {
            var defaultPresets = new Dictionary<string, string>
    {
        {"Custom", "" },
        {"Get external-address-resource list", "dia sys external-address-resource list"}

    };

            // Set a default username
            var defaultUsername = "";

            // Combine both username and presets into a single object for serialization
            var settings = new
            {
                Timeout = 10,
                Delay = 300,
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
                    presets.Add(presetName, "");
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
    }
}
