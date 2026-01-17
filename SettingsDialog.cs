using SSH_Helper.Services;

namespace SSH_Helper
{
    /// <summary>
    /// Settings dialog for application preferences.
    /// </summary>
    internal sealed class SettingsDialog : Form
    {
        private readonly ConfigurationService _configService;

        private readonly TabControl _tabControl;

        // General tab controls
        private readonly CheckBox _chkRememberState;
        private readonly NumericUpDown _numMaxHistory;
        private readonly NumericUpDown _numDefaultTimeout;
        private readonly NumericUpDown _numConnectionTimeout;

        // Updates tab controls
        private readonly CheckBox _chkCheckForUpdatesOnStartup;

        private readonly Button _btnSave;
        private readonly Button _btnCancel;

        public SettingsDialog(ConfigurationService configService)
        {
            _configService = configService;

            Text = "Settings";
            Size = new Size(450, 350);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _tabControl = new TabControl
            {
                Location = new Point(12, 12),
                Size = new Size(410, 250),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // === General Tab ===
            var tabGeneral = new TabPage("General");

            var lblStateSection = new Label
            {
                Text = "Application State",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, 15),
                AutoSize = true
            };

            _chkRememberState = new CheckBox
            {
                Text = "Remember state on exit (hosts, preset, history)",
                Location = new Point(15, 40),
                AutoSize = true
            };

            var lblMaxHistory = new Label
            {
                Text = "Maximum history entries to keep:",
                Location = new Point(15, 70),
                AutoSize = true
            };

            _numMaxHistory = new NumericUpDown
            {
                Location = new Point(220, 68),
                Size = new Size(80, 23),
                Minimum = 1,
                Maximum = 500,
                Value = 30
            };

            var lblDefaultsSection = new Label
            {
                Text = "Default Values",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, 105),
                AutoSize = true
            };

            var lblDefaultTimeout = new Label
            {
                Text = "Default command timeout (seconds):",
                Location = new Point(15, 135),
                AutoSize = true
            };

            _numDefaultTimeout = new NumericUpDown
            {
                Location = new Point(250, 133),
                Size = new Size(80, 23),
                Minimum = 1,
                Maximum = 300,
                Value = 10
            };

            var lblConnectionTimeout = new Label
            {
                Text = "Connection timeout (seconds):",
                Location = new Point(15, 165),
                AutoSize = true
            };

            _numConnectionTimeout = new NumericUpDown
            {
                Location = new Point(250, 163),
                Size = new Size(80, 23),
                Minimum = 5,
                Maximum = 120,
                Value = 30
            };

            tabGeneral.Controls.Add(lblStateSection);
            tabGeneral.Controls.Add(_chkRememberState);
            tabGeneral.Controls.Add(lblMaxHistory);
            tabGeneral.Controls.Add(_numMaxHistory);
            tabGeneral.Controls.Add(lblDefaultsSection);
            tabGeneral.Controls.Add(lblDefaultTimeout);
            tabGeneral.Controls.Add(_numDefaultTimeout);
            tabGeneral.Controls.Add(lblConnectionTimeout);
            tabGeneral.Controls.Add(_numConnectionTimeout);

            _tabControl.TabPages.Add(tabGeneral);

            // === Updates Tab ===
            var tabUpdates = new TabPage("Updates");

            var lblUpdateSection = new Label
            {
                Text = "Automatic Updates",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, 15),
                AutoSize = true
            };

            _chkCheckForUpdatesOnStartup = new CheckBox
            {
                Text = "Check for updates when application starts",
                Location = new Point(15, 40),
                AutoSize = true
            };

            tabUpdates.Controls.Add(lblUpdateSection);
            tabUpdates.Controls.Add(_chkCheckForUpdatesOnStartup);

            _tabControl.TabPages.Add(tabUpdates);

            // Buttons
            _btnSave = new Button
            {
                Text = "Save",
                Size = new Size(80, 28),
                Location = new Point(261, 275),
                DialogResult = DialogResult.OK
            };
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 28),
                Location = new Point(347, 275),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(_tabControl);
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            LoadSettings();
        }

        private void LoadSettings()
        {
            var config = _configService.GetCurrent();

            // General
            _chkRememberState.Checked = config.RememberState;
            _numMaxHistory.Value = Math.Clamp(config.MaxHistoryEntries, 1, 500);
            _numDefaultTimeout.Value = Math.Clamp(config.Timeout, 1, 300);
            _numConnectionTimeout.Value = Math.Clamp(config.ConnectionTimeout, 5, 120);

            // Updates
            _chkCheckForUpdatesOnStartup.Checked = config.UpdateSettings.CheckOnStartup;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            _configService.Update(config =>
            {
                // General
                config.RememberState = _chkRememberState.Checked;
                config.MaxHistoryEntries = (int)_numMaxHistory.Value;
                config.Timeout = (int)_numDefaultTimeout.Value;
                config.ConnectionTimeout = (int)_numConnectionTimeout.Value;

                // Updates
                config.UpdateSettings.CheckOnStartup = _chkCheckForUpdatesOnStartup.Checked;
            });
        }
    }
}
