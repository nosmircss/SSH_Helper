using SSH_Helper.Models;

namespace SSH_Helper
{
    /// <summary>
    /// Dialog for configuring folder preset execution options.
    /// </summary>
    internal sealed class FolderExecutionDialog : Form
    {
        private readonly string _folderName;
        private readonly List<string> _presetNames;
        private readonly List<string> _hostAddresses;

        private readonly CheckedListBox _lstPresets;
        private readonly ListBox _lstHosts;
        private readonly RadioButton _rbSequential;
        private readonly RadioButton _rbParallel;
        private readonly CheckBox _chkStopOnError;
        private readonly CheckBox _chkSuppressPresetNames;
        private readonly TextBox _txtParallelHosts;
        private readonly Button _btnRun;
        private readonly Button _btnCancel;

        /// <summary>
        /// Gets the configured execution options after the dialog is closed with OK.
        /// </summary>
        public FolderExecutionOptions Options { get; private set; } = new();

        public FolderExecutionDialog(string folderName, List<string> presetNames, List<string> hostAddresses)
        {
            _folderName = folderName;
            _presetNames = presetNames;
            _hostAddresses = hostAddresses;

            Text = $"Run Folder: {folderName}";
            Size = new Size(420, 580);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            // Presets section
            var lblPresets = new Label
            {
                Text = "Presets to run:",
                Location = new Point(15, 15),
                AutoSize = true
            };

            _lstPresets = new CheckedListBox
            {
                Location = new Point(15, 38),
                Size = new Size(375, 94),
                CheckOnClick = true
            };

            foreach (var preset in _presetNames)
            {
                _lstPresets.Items.Add(preset, true);
            }
            _lstPresets.ItemCheck += LstPresets_ItemCheck;

            // Hosts section
            var lblHosts = new Label
            {
                Text = $"Target hosts ({_hostAddresses.Count}):",
                Location = new Point(15, 140),
                AutoSize = true
            };

            _lstHosts = new ListBox
            {
                Location = new Point(15, 163),
                Size = new Size(375, 94),
                SelectionMode = SelectionMode.None,
                BackColor = SystemColors.Control
            };

            foreach (var host in _hostAddresses)
            {
                _lstHosts.Items.Add(host);
            }

            // Preset Execution section
            var lblPresetSection = new Label
            {
                Text = "Preset Execution",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, 268),
                AutoSize = true
            };

            var lblRunMode = new Label
            {
                Text = "Run Mode:",
                Location = new Point(15, 296),
                AutoSize = true
            };

            _rbSequential = new RadioButton
            {
                Text = "Sequential (one preset at a time)",
                Location = new Point(30, 318),
                AutoSize = true,
                Checked = true
            };

            _rbParallel = new RadioButton
            {
                Text = "Parallel (all presets simultaneously)",
                Location = new Point(30, 341),
                AutoSize = true
            };

            _chkStopOnError = new CheckBox
            {
                Text = "Stop on first error",
                Location = new Point(15, 371),
                AutoSize = true
            };

            _chkSuppressPresetNames = new CheckBox
            {
                Text = "Suppress preset names from output",
                Location = new Point(15, 394),
                AutoSize = true,
                Checked = true
            };

            // Host Execution section
            var lblHostSection = new Label
            {
                Text = "Host Execution",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, 426),
                AutoSize = true
            };

            var lblParallelHosts = new Label
            {
                Text = "Parallel hosts:",
                Location = new Point(15, 456),
                AutoSize = true
            };

            _txtParallelHosts = new TextBox
            {
                Text = "1",
                Location = new Point(105, 453),
                Size = new Size(50, 23),
                TextAlign = HorizontalAlignment.Right
            };
            _txtParallelHosts.KeyPress += TxtParallelHosts_KeyPress;

            var lblHostCount = new Label
            {
                Text = $"(of {_hostAddresses.Count} selected)",
                Location = new Point(162, 456),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            // Buttons
            _btnRun = new Button
            {
                Text = $"Run {_presetNames.Count} Presets",
                AutoSize = true,
                MinimumSize = new Size(0, 28),
                Location = new Point(230, 498),
                DialogResult = DialogResult.OK
            };
            _btnRun.Click += BtnRun_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 28),
                Location = new Point(320, 498),
                DialogResult = DialogResult.Cancel
            };

            // Add controls
            Controls.Add(lblPresets);
            Controls.Add(_lstPresets);
            Controls.Add(lblHosts);
            Controls.Add(_lstHosts);
            Controls.Add(lblPresetSection);
            Controls.Add(lblRunMode);
            Controls.Add(_rbSequential);
            Controls.Add(_rbParallel);
            Controls.Add(_chkStopOnError);
            Controls.Add(_chkSuppressPresetNames);
            Controls.Add(lblHostSection);
            Controls.Add(lblParallelHosts);
            Controls.Add(_txtParallelHosts);
            Controls.Add(lblHostCount);
            Controls.Add(_btnCancel);
            Controls.Add(_btnRun);

            AcceptButton = _btnRun;
            CancelButton = _btnCancel;
        }

        private void LstPresets_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            // Calculate new count after the check change is applied
            int count = _lstPresets.CheckedItems.Count;
            if (e.NewValue == CheckState.Checked)
                count++;
            else if (e.NewValue == CheckState.Unchecked)
                count--;

            _btnRun.Text = $"Run {count} Presets";
            _btnRun.Enabled = count > 0;
        }

        private void TxtParallelHosts_KeyPress(object? sender, KeyPressEventArgs e)
        {
            // Only allow digits and control characters (backspace, etc.)
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void BtnRun_Click(object? sender, EventArgs e)
        {
            // Validate parallel hosts input
            if (!int.TryParse(_txtParallelHosts.Text, out int parallelHosts) || parallelHosts < 1)
            {
                MessageBox.Show(
                    "Please enter a valid number for parallel hosts (minimum 1).",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            // Build the options
            Options = new FolderExecutionOptions
            {
                SelectedPresets = _lstPresets.CheckedItems.Cast<string>().ToList(),
                RunPresetsInParallel = _rbParallel.Checked,
                StopOnFirstError = _chkStopOnError.Checked,
                ParallelHostCount = parallelHosts,
                SuppressPresetNames = _chkSuppressPresetNames.Checked
            };
        }
    }
}
