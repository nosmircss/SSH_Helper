using SSH_Helper.Models;
using SSH_Helper.UI;

namespace SSH_Helper
{
    /// <summary>
    /// Dialog for configuring folder preset execution options.
    /// </summary>
    internal sealed class FolderExecutionDialog : Form
    {
        private const string UnsupportedInteractiveSuffix = " (unsupported interactive command)";
        private const int MaxParallelHosts = 100;

        private readonly string _folderName;
        private readonly List<string> _presetNames;
        private readonly List<string> _hostAddresses;
        private readonly HashSet<string> _unsupportedInteractivePresets;

        private readonly Panel _pnlPresets;
        private readonly Panel _pnlHosts;
        private readonly CheckedListBox _lstPresets;
        private readonly CheckedListBox _lstHosts;
        private readonly Label _lblHosts;
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

        public FolderExecutionDialog(
            string folderName,
            List<string> presetNames,
            List<string> hostAddresses,
            bool darkMode = false,
            IEnumerable<string>? unsupportedInteractivePresets = null)
        {
            _folderName = folderName;
            _presetNames = presetNames;
            _hostAddresses = hostAddresses;
            _unsupportedInteractivePresets = new HashSet<string>(
                unsupportedInteractivePresets ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

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

            _pnlPresets = new Panel
            {
                Location = new Point(15, 38),
                Size = new Size(375, 94),
                Padding = new Padding(1)
            };

            _lstPresets = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                BorderStyle = BorderStyle.None
            };
            _pnlPresets.Controls.Add(_lstPresets);

            foreach (var preset in _presetNames)
            {
                var isUnsupportedInteractive = _unsupportedInteractivePresets.Contains(preset);
                _lstPresets.Items.Add(new PresetListItem(preset, isUnsupportedInteractive), !isUnsupportedInteractive);
            }
            _lstPresets.ItemCheck += LstPresets_ItemCheck;

            // Hosts section
            _lblHosts = new Label
            {
                Text = $"Target hosts ({_hostAddresses.Count}):",
                Location = new Point(15, 140),
                AutoSize = true
            };

            _pnlHosts = new Panel
            {
                Location = new Point(15, 163),
                Size = new Size(375, 94),
                Padding = new Padding(1)
            };

            _lstHosts = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                BorderStyle = BorderStyle.None
            };
            _pnlHosts.Controls.Add(_lstHosts);

            foreach (var host in _hostAddresses)
            {
                _lstHosts.Items.Add(host, true);
            }
            _lstHosts.ItemCheck += LstHosts_ItemCheck;

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
            _txtParallelHosts.Leave += TxtParallelHosts_Leave;
            _txtParallelHosts.TextChanged += TxtParallelHosts_TextChanged;

            // Buttons
            _btnRun = new Button
            {
                Text = $"Run {_presetNames.Count} Presets",
                AutoSize = true,
                MinimumSize = new Size(0, 28),
                Location = new Point(185, 500),
                DialogResult = DialogResult.OK
            };
            _btnRun.Click += BtnRun_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 28),
                Location = new Point(305, 500),
                DialogResult = DialogResult.Cancel
            };

            // Add controls
            Controls.Add(lblPresets);
            Controls.Add(_pnlPresets);
            Controls.Add(_lblHosts);
            Controls.Add(_pnlHosts);
            Controls.Add(lblPresetSection);
            Controls.Add(lblRunMode);
            Controls.Add(_rbSequential);
            Controls.Add(_rbParallel);
            Controls.Add(_chkStopOnError);
            Controls.Add(_chkSuppressPresetNames);
            Controls.Add(lblHostSection);
            Controls.Add(lblParallelHosts);
            Controls.Add(_txtParallelHosts);
            Controls.Add(_btnCancel);
            Controls.Add(_btnRun);

            AcceptButton = _btnRun;
            CancelButton = _btnCancel;

            UpdateRunButtonState(_lstPresets.CheckedItems.Count, _lstHosts.CheckedItems.Count);
            UpdateExecutionModeConstraints(_lstHosts.CheckedItems.Count);
            ApplyTheme(darkMode);
        }

        private void ApplyTheme(bool darkMode)
        {
            DialogTheme.ApplyTo(this, darkMode);

            // Panel BackColor acts as the 1px border around each list
            var borderColor = darkMode ? DialogTheme.DarkBorder : DialogTheme.LightBorder;
            _pnlPresets.BackColor = borderColor;
            _pnlHosts.BackColor = borderColor;

            DialogTheme.StyleButton(_btnRun, darkMode);
            DialogTheme.StyleButton(_btnCancel, darkMode);
            DialogTheme.SetDarkTitleBar(this, darkMode);

            if (darkMode)
            {
                Load += (_, _) => DialogTheme.ApplyNativeTheme(this, true);
            }
        }

        private void LstPresets_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            int presetCount = _lstPresets.CheckedItems.Count;
            if (e.NewValue == CheckState.Checked) presetCount++;
            else if (e.NewValue == CheckState.Unchecked) presetCount--;

            UpdateRunButtonState(presetCount, _lstHosts.CheckedItems.Count);
        }

        private void LstHosts_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            int hostCount = _lstHosts.CheckedItems.Count;
            if (e.NewValue == CheckState.Checked) hostCount++;
            else if (e.NewValue == CheckState.Unchecked) hostCount--;

            _lblHosts.Text = $"Target hosts ({hostCount} of {_hostAddresses.Count}):";
            UpdateRunButtonState(_lstPresets.CheckedItems.Count, hostCount);
            NormalizeParallelHosts(hostCount);
            UpdateExecutionModeConstraints(hostCount);
        }

        private void TxtParallelHosts_KeyPress(object? sender, KeyPressEventArgs e)
        {
            // Only allow digits and control characters (backspace, etc.)
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void TxtParallelHosts_Leave(object? sender, EventArgs e)
        {
            NormalizeParallelHosts(_lstHosts.CheckedItems.Count);
            UpdateExecutionModeConstraints(_lstHosts.CheckedItems.Count);
        }

        private void TxtParallelHosts_TextChanged(object? sender, EventArgs e)
        {
            UpdateExecutionModeConstraints(_lstHosts.CheckedItems.Count);
        }

        private void NormalizeParallelHosts(int selectedHostCount)
        {
            int maxParallelHosts = Math.Max(1, Math.Min(selectedHostCount, MaxParallelHosts));

            if (!int.TryParse(_txtParallelHosts.Text, out int parallelHosts))
            {
                parallelHosts = 1;
            }

            parallelHosts = Math.Clamp(parallelHosts, 1, maxParallelHosts);
            _txtParallelHosts.Text = parallelHosts.ToString();
        }

        private void UpdateExecutionModeConstraints(int selectedHostCount)
        {
            if (!int.TryParse(_txtParallelHosts.Text, out int parallelHosts))
            {
                parallelHosts = 1;
            }

            parallelHosts = Math.Clamp(parallelHosts, 1, Math.Max(1, Math.Min(selectedHostCount, MaxParallelHosts)));
            var hostParallelEnabled = parallelHosts > 1;

            _rbParallel.Enabled = !hostParallelEnabled;
            if (hostParallelEnabled && _rbParallel.Checked)
            {
                _rbSequential.Checked = true;
            }

            _rbParallel.Text = hostParallelEnabled
                ? "Parallel (disabled when running multiple hosts in parallel)"
                : "Parallel (all presets simultaneously)";
        }

        private void BtnRun_Click(object? sender, EventArgs e)
        {
            int selectedHostCount = _lstHosts.CheckedItems.Count;
            NormalizeParallelHosts(selectedHostCount);
            int parallelHosts = int.Parse(_txtParallelHosts.Text);
            bool runPresetsInParallel = _rbParallel.Checked && parallelHosts == 1;

            // Build the options
            Options = new FolderExecutionOptions
            {
                SelectedPresets = _lstPresets.CheckedItems
                    .Cast<PresetListItem>()
                    .Select(item => item.Name)
                    .ToList(),
                SelectedHostIndices = _lstHosts.CheckedIndices.Cast<int>().ToList(),
                RunPresetsInParallel = runPresetsInParallel,
                StopOnFirstError = _chkStopOnError.Checked,
                ParallelHostCount = parallelHosts,
                SuppressPresetNames = _chkSuppressPresetNames.Checked
            };
        }

        private void UpdateRunButtonState(int checkedPresetCount, int checkedHostCount)
        {
            _btnRun.Text = $"Run {checkedPresetCount} Presets";
            _btnRun.Enabled = checkedPresetCount > 0 && checkedHostCount > 0;
        }

        private sealed class PresetListItem
        {
            public PresetListItem(string name, bool unsupportedInteractive)
            {
                Name = name;
                UnsupportedInteractive = unsupportedInteractive;
            }

            public string Name { get; }
            public bool UnsupportedInteractive { get; }

            public override string ToString()
            {
                if (!UnsupportedInteractive)
                    return Name;

                return Name + FolderExecutionDialog.UnsupportedInteractiveSuffix;
            }
        }
    }
}
