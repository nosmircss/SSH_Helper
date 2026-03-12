using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;

namespace SSH_Helper
{
    /// <summary>
    /// Shows a preview of jobs to import with conflict resolution.
    /// Users can review import entries, see conflict warnings, and select which jobs to import.
    /// </summary>
    internal sealed class ImportPreviewDialog : Form
    {
        private readonly IReadOnlyList<JobExportService.ImportJobEntry> _entries;
        private readonly DataGridView _gridPreview;
        private readonly Label _lblSummary;
        private readonly Button _btnImport;
        private readonly Button _btnCancel;

        private const int ColImport = 0;
        private const int ColName = 1;
        private const int ColSchedule = 2;
        private const int ColTarget = 3;
        private const int ColStatus = 4;

        /// <summary>
        /// The entries the user accepted for import. Null if the dialog was cancelled.
        /// </summary>
        public IReadOnlyList<JobExportService.ImportJobEntry>? AcceptedEntries { get; private set; }

        public ImportPreviewDialog(
            IReadOnlyList<JobExportService.ImportJobEntry> entries,
            bool darkMode,
            string? fontFamily = null,
            float fontSize = 9f)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));

            Text = "Import Jobs";
            Size = new Size(700, 450);
            MinimumSize = new Size(500, 300);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            // --- Top label ---
            var lblHeader = new Label
            {
                Text = "The following jobs will be imported:",
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(10, 8, 10, 0),
                AutoSize = false
            };

            // --- Preview grid ---
            _gridPreview = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnEnter
            };

            // Define columns
            var colCheckBox = new DataGridViewCheckBoxColumn
            {
                Name = "Import",
                HeaderText = "Import",
                Width = 50,
                FillWeight = 10,
                FlatStyle = FlatStyle.Standard
            };

            var colJobName = new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "Name",
                ReadOnly = true,
                FillWeight = 30
            };

            var colSchedule = new DataGridViewTextBoxColumn
            {
                Name = "Schedule",
                HeaderText = "Schedule",
                ReadOnly = true,
                FillWeight = 20
            };

            var colTarget = new DataGridViewTextBoxColumn
            {
                Name = "Target",
                HeaderText = "Target",
                ReadOnly = true,
                FillWeight = 20
            };

            var colStatusText = new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "Status",
                ReadOnly = true,
                FillWeight = 20
            };

            _gridPreview.Columns.AddRange(colCheckBox, colJobName, colSchedule, colTarget, colStatusText);

            // Wire up checkbox change events
            _gridPreview.CellValueChanged += GridPreview_CellValueChanged;
            _gridPreview.CurrentCellDirtyStateChanged += GridPreview_CurrentCellDirtyStateChanged;

            // --- Bottom panel with summary and buttons ---
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                Padding = new Padding(10, 6, 10, 6)
            };

            _lblSummary = new Label
            {
                AutoSize = true,
                Location = new Point(10, 14),
                Text = ""
            };

            _btnImport = new Button
            {
                Text = "Import",
                Size = new Size(80, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK
            };
            _btnImport.Click += BtnImport_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };

            // Position buttons on the right side
            _btnCancel.Location = new Point(
                bottomPanel.ClientSize.Width - _btnCancel.Width - 10,
                8);
            _btnImport.Location = new Point(
                _btnCancel.Left - _btnImport.Width - 8,
                8);

            bottomPanel.Resize += (_, _) =>
            {
                _btnCancel.Left = bottomPanel.ClientSize.Width - _btnCancel.Width - 10;
                _btnImport.Left = _btnCancel.Left - _btnImport.Width - 8;
            };

            bottomPanel.Controls.Add(_lblSummary);
            bottomPanel.Controls.Add(_btnImport);
            bottomPanel.Controls.Add(_btnCancel);

            // --- Add controls in correct dock order ---
            Controls.Add(_gridPreview);
            Controls.Add(lblHeader);
            Controls.Add(bottomPanel);

            AcceptButton = _btnImport;
            CancelButton = _btnCancel;

            // --- Populate grid ---
            PopulateGrid();
            UpdateSummaryLabel();

            // --- Apply theming ---
            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.StyleDataGridView(_gridPreview, darkMode, flattenHeaderBevel: true);
            DialogTheme.StyleButton(_btnImport, darkMode, isPrimary: true);
            DialogTheme.StyleButton(_btnCancel, darkMode);
            DialogTheme.SetDarkTitleBar(this, darkMode);

            if (!string.IsNullOrEmpty(fontFamily))
            {
                DialogTheme.SetDialogFont(this, new Font(fontFamily, fontSize));
            }

            if (darkMode)
            {
                Load += (_, _) => DialogTheme.ApplyNativeTheme(this, true);
            }
        }

        private void PopulateGrid()
        {
            _gridPreview.Rows.Clear();

            foreach (var entry in _entries)
            {
                var job = entry.Job;

                // Schedule column
                var schedule = job.ScheduleType.ToString();
                if (job.ScheduleType == ScheduleType.Recurring && !string.IsNullOrEmpty(job.CronExpression))
                {
                    schedule = $"Recurring ({job.CronExpression})";
                }

                // Target column
                var target = job.TargetType switch
                {
                    JobTargetType.Folder => $"[Folder] {job.TargetName}",
                    JobTargetType.CustomPreset => "[Custom] Scheduler-local content",
                    _ => job.TargetName
                };

                // Status column
                string statusText;
                Color statusColor;

                if (entry.HasConflict)
                {
                    statusText = $"Renamed (original: {job.Name})";
                    statusColor = Color.FromArgb(255, 165, 0); // Orange/amber
                }
                else if (entry.MissingTarget)
                {
                    statusText = job.TargetType == JobTargetType.Folder
                        ? "Target folder not found - will be disabled"
                        : "Target preset not found - will be disabled";
                    statusColor = Color.FromArgb(220, 50, 50); // Red
                }
                else
                {
                    statusText = "OK";
                    statusColor = Color.FromArgb(50, 180, 50); // Green
                }

                var rowIndex = _gridPreview.Rows.Add(
                    true, // Checked by default
                    entry.ResolvedName,
                    schedule,
                    target,
                    statusText);

                // Apply status color to the Status cell
                _gridPreview.Rows[rowIndex].Cells[ColStatus].Style.ForeColor = statusColor;
                _gridPreview.Rows[rowIndex].Cells[ColStatus].Style.SelectionForeColor = statusColor;

                // Store the entry reference in the row's Tag
                _gridPreview.Rows[rowIndex].Tag = entry;
            }
        }

        private void GridPreview_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            // Commit checkbox changes immediately so CellValueChanged fires
            if (_gridPreview.IsCurrentCellDirty)
            {
                _gridPreview.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void GridPreview_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == ColImport && e.RowIndex >= 0)
            {
                UpdateSummaryLabel();
            }
        }

        private void UpdateSummaryLabel()
        {
            int checkedCount = 0;
            int totalCount = _gridPreview.Rows.Count;

            foreach (DataGridViewRow row in _gridPreview.Rows)
            {
                if (row.Cells[ColImport].Value is true)
                {
                    checkedCount++;
                }
            }

            _lblSummary.Text = $"{checkedCount} of {totalCount} jobs selected";
        }

        private void BtnImport_Click(object? sender, EventArgs e)
        {
            var accepted = new List<JobExportService.ImportJobEntry>();

            foreach (DataGridViewRow row in _gridPreview.Rows)
            {
                if (row.Cells[ColImport].Value is true && row.Tag is JobExportService.ImportJobEntry entry)
                {
                    accepted.Add(entry);
                }
            }

            AcceptedEntries = accepted;
            DialogResult = DialogResult.OK;
        }
    }
}
