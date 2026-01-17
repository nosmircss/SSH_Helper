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
        private readonly CheckBox _chkCheckForUpdatesOnStartup;
        private readonly Button _btnSave;
        private readonly Button _btnCancel;

        public SettingsDialog(ConfigurationService configService)
        {
            _configService = configService;

            Text = "Settings";
            Size = new Size(450, 200);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _tabControl = new TabControl
            {
                Location = new Point(12, 12),
                Size = new Size(410, 100),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Updates Tab
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
                Location = new Point(261, 125),
                DialogResult = DialogResult.OK
            };
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 28),
                Location = new Point(347, 125),
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
            _chkCheckForUpdatesOnStartup.Checked = config.UpdateSettings.CheckOnStartup;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            // Save settings
            _configService.Update(config =>
            {
                config.UpdateSettings.CheckOnStartup = _chkCheckForUpdatesOnStartup.Checked;
            });
        }
    }
}
