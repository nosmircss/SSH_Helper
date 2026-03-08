using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using SSH_Helper.Utilities;

namespace SSH_Helper
{
    /// <summary>
    /// Tabbed modal dialog for creating and editing job definitions.
    /// Provides General, Hosts, Credentials, and Advanced tabs with
    /// cron preview, host list management, credential configuration,
    /// and drift warning banner. Works on a deep clone so cancellation
    /// discards all changes.
    /// </summary>
    internal sealed class JobEditorDialog : Form
    {
        #region Fields

        private readonly PresetManager _presetManager;
        private readonly SchedulingService _schedulingService;
        private readonly Func<IReadOnlyList<Dictionary<string, string>>>? _getMainGridRows;
        private readonly Func<IReadOnlyList<string>>? _getMainGridColumns;
        private readonly bool _darkMode;
        private readonly bool _isNew;
        private readonly JobDefinition _editingJob;

        // Dialog chrome
        private readonly BorderlessTabControl _tabControl;
        private readonly Panel _bottomPanel;
        private readonly Button _btnSave;
        private readonly Button _btnCancel;

        // Drift warning banner
        private readonly Panel _driftBanner;
        private readonly Label _lblDriftWarning;
        private readonly Button _btnAcknowledgeDrift;

        // General tab controls
        private readonly TextBox _txtName;
        private readonly RadioButton _rbPreset;
        private readonly RadioButton _rbFolder;
        private readonly ComboBox _cboTarget;
        private readonly ComboBox _cboScheduleType;
        private readonly Panel _panelCron;
        private readonly CronBuilderControl _cronBuilder;
        private readonly Panel _panelOneTime;
        private readonly DateTimePicker _dtpOneTime;

        // Hosts tab controls
        private readonly ToolStrip _hostsToolStrip;
        private readonly DataGridView _gridHosts;
        private readonly Label _lblHostCount;

        // Credentials tab controls
        private readonly RadioButton _rbInheritFromApp;
        private readonly RadioButton _rbStored;
        private readonly RadioButton _rbPerHostColumn;
        private readonly Panel _panelStoredCreds;
        private readonly TextBox _txtUsername;
        private readonly TextBox _txtPassword;
        private readonly Panel _panelPerHost;

        // Advanced tab controls
        private readonly GroupBox _grpFolderExecution;
        private readonly RadioButton _rbSequential;
        private readonly RadioButton _rbParallel;
        private readonly CheckBox _chkStopOnError;
        private readonly GroupBox _grpHistoryRetention;
        private readonly CheckBox _chkOverrideMaxRuns;
        private readonly NumericUpDown _numMaxRuns;
        private readonly CheckBox _chkOverrideRetention;
        private readonly NumericUpDown _numRetentionDays;

        #endregion

        #region Properties

        /// <summary>
        /// The resulting job definition after save. Null if cancelled.
        /// </summary>
        public JobDefinition? Result { get; private set; }

        #endregion

        #region Constructor

        public JobEditorDialog(
            JobDefinition? existingJob,
            PresetManager presetManager,
            SchedulingService schedulingService,
            Func<IReadOnlyList<Dictionary<string, string>>>? getMainGridRows,
            Func<IReadOnlyList<string>>? getMainGridColumns,
            bool darkMode,
            string? fontFamily = null,
            float fontSize = 9f)
        {
            _presetManager = presetManager;
            _schedulingService = schedulingService;
            _getMainGridRows = getMainGridRows;
            _getMainGridColumns = getMainGridColumns;
            _darkMode = darkMode;
            _isNew = existingJob == null;

            // Deep clone for editing or create new
            if (existingJob != null)
            {
                var json = JsonConvert.SerializeObject(existingJob);
                _editingJob = JsonConvert.DeserializeObject<JobDefinition>(json)!;
            }
            else
            {
                _editingJob = new JobDefinition();
            }

            // Initialize all controls
            _tabControl = new BorderlessTabControl();
            _bottomPanel = new Panel();
            _btnSave = new Button();
            _btnCancel = new Button();

            _driftBanner = new Panel();
            _lblDriftWarning = new Label();
            _btnAcknowledgeDrift = new Button();

            _txtName = new TextBox();
            _rbPreset = new RadioButton();
            _rbFolder = new RadioButton();
            _cboTarget = new ComboBox();
            _cboScheduleType = new ComboBox();
            _panelCron = new Panel();
            _cronBuilder = new CronBuilderControl();
            _panelOneTime = new Panel();
            _dtpOneTime = new DateTimePicker();

            _hostsToolStrip = new ToolStrip();
            _gridHosts = new DataGridView();
            _lblHostCount = new Label();

            _rbInheritFromApp = new RadioButton();
            _rbStored = new RadioButton();
            _rbPerHostColumn = new RadioButton();
            _panelStoredCreds = new Panel();
            _txtUsername = new TextBox();
            _txtPassword = new TextBox();
            _panelPerHost = new Panel();

            _grpFolderExecution = new GroupBox();
            _rbSequential = new RadioButton();
            _rbParallel = new RadioButton();
            _chkStopOnError = new CheckBox();
            _grpHistoryRetention = new GroupBox();
            _chkOverrideMaxRuns = new CheckBox();
            _numMaxRuns = new NumericUpDown();
            _chkOverrideRetention = new CheckBox();
            _numRetentionDays = new NumericUpDown();

            SuspendLayout();
            BuildDialogChrome();
            BuildDriftBanner();
            BuildGeneralTab();
            BuildHostsTab();
            BuildCredentialsTab();
            BuildAdvancedTab();
            BuildBottomPanel();
            WireEvents();
            PrepopulateFromJob();
            ResumeLayout(true);

            // Apply theming
            var font = fontFamily != null ? new Font(fontFamily, fontSize) : new Font("Segoe UI", fontSize);
            DialogTheme.SetDialogFont(this, font);
            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.SetDarkTitleBar(this, darkMode);
            DialogTheme.StyleTabControl(_tabControl, darkMode);
            DialogTheme.StyleDataGridView(_gridHosts, darkMode);
            DialogTheme.StyleButton(_btnSave, darkMode, isPrimary: true);
            DialogTheme.StyleButton(_btnCancel, darkMode);
            DialogTheme.StyleButton(_btnAcknowledgeDrift, darkMode);
            _cronBuilder.ApplyTheme(darkMode);
            _cronBuilder.SetSchedulingService(schedulingService);

            // Style toolstrip for dark mode
            if (darkMode)
            {
                _hostsToolStrip.BackColor = DialogTheme.DarkSurface1;
                _hostsToolStrip.ForeColor = DialogTheme.DarkText;
                _hostsToolStrip.Renderer = new ToolStripProfessionalRenderer(
                    new DarkToolStripColorTable());
            }

            // Drift banner theming override (always yellow regardless of mode)
            _driftBanner.BackColor = Color.FromArgb(255, 243, 205);
            _lblDriftWarning.ForeColor = Color.FromArgb(133, 100, 4);
            _lblDriftWarning.BackColor = Color.FromArgb(255, 243, 205);

            Load += (_, _) => DialogTheme.ApplyNativeTheme(this, darkMode);
        }

        #endregion

        #region Dialog Chrome

        private void BuildDialogChrome()
        {
            Text = _isNew ? "New Job" : $"Edit Job - {_editingJob.Name}";
            Size = new Size(750, 600);
            MinimumSize = new Size(650, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;

            _tabControl.Dock = DockStyle.Fill;

            var tabGeneral = new TabPage("General");
            var tabHosts = new TabPage("Hosts");
            var tabCredentials = new TabPage("Credentials");
            var tabAdvanced = new TabPage("Advanced");

            _tabControl.TabPages.Add(tabGeneral);
            _tabControl.TabPages.Add(tabHosts);
            _tabControl.TabPages.Add(tabCredentials);
            _tabControl.TabPages.Add(tabAdvanced);

            Controls.Add(_tabControl);
        }

        private void BuildBottomPanel()
        {
            _bottomPanel.Dock = DockStyle.Bottom;
            _bottomPanel.Height = 50;
            _bottomPanel.Padding = new Padding(8);

            _btnCancel.Text = "Cancel";
            _btnCancel.Size = new Size(90, 30);
            _btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            _btnCancel.DialogResult = DialogResult.Cancel;

            _btnSave.Text = "Save";
            _btnSave.Size = new Size(90, 30);
            _btnSave.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

            // Position buttons right-aligned
            _btnCancel.Location = new Point(_bottomPanel.Width - _btnCancel.Width - 12,
                (_bottomPanel.Height - _btnCancel.Height) / 2);
            _btnSave.Location = new Point(_btnCancel.Left - _btnSave.Width - 8,
                (_bottomPanel.Height - _btnSave.Height) / 2);

            _bottomPanel.Controls.Add(_btnSave);
            _bottomPanel.Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            Controls.Add(_bottomPanel);
        }

        #endregion

        #region Drift Warning Banner

        private void BuildDriftBanner()
        {
            _driftBanner.Dock = DockStyle.Top;
            _driftBanner.Height = 40;
            _driftBanner.BackColor = Color.FromArgb(255, 243, 205);
            _driftBanner.Visible = false;
            _driftBanner.Padding = new Padding(8, 4, 8, 4);

            _lblDriftWarning.Text = "Target preset has changed since this job was saved. Execution blocked until reviewed.";
            _lblDriftWarning.AutoSize = false;
            _lblDriftWarning.TextAlign = ContentAlignment.MiddleLeft;
            _lblDriftWarning.Dock = DockStyle.Fill;
            _lblDriftWarning.ForeColor = Color.FromArgb(133, 100, 4);

            _btnAcknowledgeDrift.Text = "Review && Acknowledge";
            _btnAcknowledgeDrift.AutoSize = true;
            _btnAcknowledgeDrift.Dock = DockStyle.Right;
            _btnAcknowledgeDrift.Padding = new Padding(4, 0, 4, 0);

            _driftBanner.Controls.Add(_lblDriftWarning);
            _driftBanner.Controls.Add(_btnAcknowledgeDrift);

            Controls.Add(_driftBanner);
        }

        #endregion

        #region General Tab

        private void BuildGeneralTab()
        {
            var tab = _tabControl.TabPages[0];
            tab.AutoScroll = true;
            tab.Padding = new Padding(16);

            var yPos = 16;
            const int labelWidth = 100;
            const int controlLeft = 120;

            // Job Name
            var lblName = new Label
            {
                Text = "Job Name:",
                Location = new Point(16, yPos + 3),
                Size = new Size(labelWidth, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _txtName.Location = new Point(controlLeft, yPos);
            _txtName.Size = new Size(tab.Width - controlLeft - 32, 23);
            _txtName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtName.PlaceholderText = "Enter job name...";
            tab.Controls.Add(lblName);
            tab.Controls.Add(_txtName);
            yPos += 32;

            // Target Type
            var lblTarget = new Label
            {
                Text = "Target Type:",
                Location = new Point(16, yPos + 3),
                Size = new Size(labelWidth, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _rbPreset.Text = "Single Preset";
            _rbPreset.Location = new Point(controlLeft, yPos);
            _rbPreset.AutoSize = true;
            _rbPreset.Checked = true;

            _rbFolder.Text = "Preset Folder";
            _rbFolder.Location = new Point(controlLeft + 140, yPos);
            _rbFolder.AutoSize = true;

            tab.Controls.Add(lblTarget);
            tab.Controls.Add(_rbPreset);
            tab.Controls.Add(_rbFolder);
            yPos += 30;

            // Target Selector
            var lblTargetSelect = new Label
            {
                Text = "Target:",
                Location = new Point(16, yPos + 3),
                Size = new Size(labelWidth, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _cboTarget.Location = new Point(controlLeft, yPos);
            _cboTarget.Size = new Size(tab.Width - controlLeft - 32, 23);
            _cboTarget.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _cboTarget.DropDownStyle = ComboBoxStyle.DropDownList;
            tab.Controls.Add(lblTargetSelect);
            tab.Controls.Add(_cboTarget);
            yPos += 32;

            // Schedule Type
            var lblSchedule = new Label
            {
                Text = "Schedule:",
                Location = new Point(16, yPos + 3),
                Size = new Size(labelWidth, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _cboScheduleType.Location = new Point(controlLeft, yPos);
            _cboScheduleType.Size = new Size(220, 23);
            _cboScheduleType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboScheduleType.Items.AddRange(new object[]
            {
                "None (Manual Only)",
                "Recurring (Cron)",
                "One-Time"
            });
            _cboScheduleType.SelectedIndex = 0;
            tab.Controls.Add(lblSchedule);
            tab.Controls.Add(_cboScheduleType);
            yPos += 32;

            // Cron panel (visible only when Recurring selected)
            BuildCronPanel(tab, ref yPos);

            // One-time panel (visible only when One-Time selected)
            BuildOneTimePanel(tab, ref yPos);
        }

        private void BuildCronPanel(TabPage tab, ref int yPos)
        {
            _panelCron.Location = new Point(16, yPos);
            _panelCron.Size = new Size(tab.Width - 48, 280);
            _panelCron.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _panelCron.Visible = false;

            _cronBuilder.Location = new Point(0, 0);
            _cronBuilder.Size = new Size(_panelCron.Width, 260);
            _cronBuilder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _panelCron.Controls.Add(_cronBuilder);

            tab.Controls.Add(_panelCron);
            yPos += _panelCron.Height + 8;
        }

        private void BuildOneTimePanel(TabPage tab, ref int yPos)
        {
            // Place at same vertical position as cron panel (overlapping, only one visible at a time)
            _panelOneTime.Location = new Point(16, yPos - _panelCron.Height - 8);
            _panelOneTime.Size = new Size(tab.Width - 48, 50);
            _panelOneTime.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _panelOneTime.Visible = false;

            var lblOneTime = new Label
            {
                Text = "Scheduled Date/Time:",
                Location = new Point(0, 6),
                AutoSize = true
            };

            _dtpOneTime.Location = new Point(150, 3);
            _dtpOneTime.Size = new Size(200, 23);
            _dtpOneTime.Format = DateTimePickerFormat.Custom;
            _dtpOneTime.CustomFormat = "yyyy-MM-dd HH:mm";
            _dtpOneTime.ShowUpDown = true;
            _dtpOneTime.Value = DateTime.Now.AddHours(1);

            _panelOneTime.Controls.Add(lblOneTime);
            _panelOneTime.Controls.Add(_dtpOneTime);

            tab.Controls.Add(_panelOneTime);
        }

        private void PopulateTargetCombo()
        {
            _cboTarget.Items.Clear();

            if (_rbPreset.Checked)
            {
                var presets = _presetManager.Presets.Keys
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                _cboTarget.Items.AddRange(presets);
            }
            else
            {
                var folders = _presetManager.Folders.Keys
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                _cboTarget.Items.AddRange(folders);
            }
        }

        #endregion

        #region Hosts Tab

        private void BuildHostsTab()
        {
            var tab = _tabControl.TabPages[1];

            // ToolStrip
            _hostsToolStrip.Dock = DockStyle.Top;
            _hostsToolStrip.GripStyle = ToolStripGripStyle.Hidden;

            var btnImportCsv = new ToolStripButton("Import CSV")
            {
                ToolTipText = "Import hosts from CSV file"
            };
            var btnCopyFromMain = new ToolStripButton("Copy from Main Grid")
            {
                ToolTipText = "Copy hosts from the main application grid"
            };
            var btnAddRow = new ToolStripButton("Add Row")
            {
                ToolTipText = "Add a new empty host row"
            };
            var btnRemoveSelected = new ToolStripButton("Remove Selected")
            {
                ToolTipText = "Remove selected rows"
            };

            btnImportCsv.Click += BtnImportCsv_Click;
            btnCopyFromMain.Click += BtnCopyFromMain_Click;
            btnAddRow.Click += BtnAddRow_Click;
            btnRemoveSelected.Click += BtnRemoveSelected_Click;

            _hostsToolStrip.Items.Add(btnImportCsv);
            _hostsToolStrip.Items.Add(new ToolStripSeparator());
            _hostsToolStrip.Items.Add(btnCopyFromMain);
            _hostsToolStrip.Items.Add(new ToolStripSeparator());
            _hostsToolStrip.Items.Add(btnAddRow);
            _hostsToolStrip.Items.Add(btnRemoveSelected);

            tab.Controls.Add(_hostsToolStrip);

            // Host count label at bottom
            _lblHostCount.Text = "0 host(s)";
            _lblHostCount.Dock = DockStyle.Bottom;
            _lblHostCount.Height = 24;
            _lblHostCount.TextAlign = ContentAlignment.MiddleLeft;
            _lblHostCount.Padding = new Padding(8, 0, 0, 0);
            tab.Controls.Add(_lblHostCount);

            // DataGridView fills remaining space
            _gridHosts.Dock = DockStyle.Fill;
            _gridHosts.AllowUserToAddRows = true;
            _gridHosts.AllowUserToDeleteRows = true;
            _gridHosts.ReadOnly = false;
            _gridHosts.MultiSelect = true;
            _gridHosts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridHosts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridHosts.RowHeadersVisible = true;
            _gridHosts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

            // Default Host_IP column
            var colHostIp = new DataGridViewTextBoxColumn
            {
                Name = "Host_IP",
                HeaderText = "Host_IP",
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            _gridHosts.Columns.Add(colHostIp);

            tab.Controls.Add(_gridHosts);

            // Correct z-order: grid fills between toolbar and status label
            _gridHosts.BringToFront();
        }

        private void UpdateHostCount()
        {
            var count = 0;
            foreach (DataGridViewRow row in _gridHosts.Rows)
            {
                if (row.IsNewRow) continue;
                var hostIp = row.Cells["Host_IP"]?.Value?.ToString();
                if (!string.IsNullOrWhiteSpace(hostIp))
                    count++;
            }
            _lblHostCount.Text = $"{count} host(s)";
        }

        private void PopulateHostGrid()
        {
            _gridHosts.Rows.Clear();
            _gridHosts.Columns.Clear();

            // Build columns from job.HostColumns or default to Host_IP
            var columns = _editingJob.HostColumns.Count > 0
                ? _editingJob.HostColumns
                : new List<string> { "Host_IP" };

            foreach (var colName in columns)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = colName,
                    HeaderText = colName,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                _gridHosts.Columns.Add(col);
            }

            // Populate rows
            foreach (var hostRow in _editingJob.Hosts)
            {
                var rowIndex = _gridHosts.Rows.Add();
                var gridRow = _gridHosts.Rows[rowIndex];
                foreach (var colName in columns)
                {
                    if (hostRow.TryGetValue(colName, out var value))
                    {
                        gridRow.Cells[colName].Value = value;
                    }
                }
            }

            UpdateHostCount();
        }

        private List<Dictionary<string, string>> ExtractHostsFromGrid()
        {
            var hosts = new List<Dictionary<string, string>>();
            foreach (DataGridViewRow row in _gridHosts.Rows)
            {
                if (row.IsNewRow) continue;

                var hostData = new Dictionary<string, string>();
                var hasData = false;

                foreach (DataGridViewColumn col in _gridHosts.Columns)
                {
                    var value = row.Cells[col.Name]?.Value?.ToString() ?? string.Empty;
                    hostData[col.Name] = value;
                    if (!string.IsNullOrWhiteSpace(value))
                        hasData = true;
                }

                if (hasData)
                    hosts.Add(hostData);
            }
            return hosts;
        }

        private List<string> ExtractHostColumnsFromGrid()
        {
            var columns = new List<string>();
            foreach (DataGridViewColumn col in _gridHosts.Columns)
            {
                columns.Add(col.Name);
            }
            return columns;
        }

        #endregion

        #region Credentials Tab

        private void BuildCredentialsTab()
        {
            var tab = _tabControl.TabPages[2];
            tab.AutoScroll = true;
            tab.Padding = new Padding(16);

            var yPos = 16;

            // Credential mode group
            var grpCredMode = new GroupBox
            {
                Text = "Credential Mode",
                Location = new Point(16, yPos),
                Size = new Size(tab.Width - 48, 110),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _rbInheritFromApp.Text = "Use application default credentials";
            _rbInheritFromApp.Location = new Point(16, 24);
            _rbInheritFromApp.AutoSize = true;
            _rbInheritFromApp.Checked = true;

            _rbStored.Text = "Use stored credentials for this job";
            _rbStored.Location = new Point(16, 50);
            _rbStored.AutoSize = true;

            _rbPerHostColumn.Text = "Credentials from host grid columns (username/password columns)";
            _rbPerHostColumn.Location = new Point(16, 76);
            _rbPerHostColumn.AutoSize = true;

            grpCredMode.Controls.Add(_rbInheritFromApp);
            grpCredMode.Controls.Add(_rbStored);
            grpCredMode.Controls.Add(_rbPerHostColumn);
            tab.Controls.Add(grpCredMode);
            yPos += grpCredMode.Height + 12;

            // Stored credentials panel
            _panelStoredCreds.Location = new Point(16, yPos);
            _panelStoredCreds.Size = new Size(tab.Width - 48, 120);
            _panelStoredCreds.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _panelStoredCreds.Visible = false;

            var lblUsername = new Label
            {
                Text = "Username:",
                Location = new Point(0, 6),
                AutoSize = true
            };
            _txtUsername.Location = new Point(100, 3);
            _txtUsername.Size = new Size(250, 23);

            var lblPassword = new Label
            {
                Text = "Password:",
                Location = new Point(0, 36),
                AutoSize = true
            };
            _txtPassword.Location = new Point(100, 33);
            _txtPassword.Size = new Size(250, 23);
            _txtPassword.UseSystemPasswordChar = true;

            var lblCredNote = new Label
            {
                Text = "Credentials are stored in Windows Credential Manager",
                Location = new Point(0, 66),
                AutoSize = true,
                ForeColor = Color.FromArgb(108, 117, 125)
            };

            _panelStoredCreds.Controls.Add(lblUsername);
            _panelStoredCreds.Controls.Add(_txtUsername);
            _panelStoredCreds.Controls.Add(lblPassword);
            _panelStoredCreds.Controls.Add(_txtPassword);
            _panelStoredCreds.Controls.Add(lblCredNote);
            tab.Controls.Add(_panelStoredCreds);
            yPos += _panelStoredCreds.Height + 8;

            // Per-host panel (positioned over stored creds, only one visible at a time)
            _panelPerHost.Location = new Point(16, grpCredMode.Bottom + 12);
            _panelPerHost.Size = new Size(tab.Width - 48, 40);
            _panelPerHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _panelPerHost.Visible = false;

            var lblPerHostInfo = new Label
            {
                Text = "Each host row must have 'username' and 'password' columns in the Hosts tab",
                Location = new Point(0, 6),
                AutoSize = true,
                ForeColor = Color.FromArgb(108, 117, 125)
            };
            _panelPerHost.Controls.Add(lblPerHostInfo);
            tab.Controls.Add(_panelPerHost);
        }

        #endregion

        #region Advanced Tab

        private void BuildAdvancedTab()
        {
            var tab = _tabControl.TabPages[3];
            tab.AutoScroll = true;
            tab.Padding = new Padding(16);

            var yPos = 16;

            // Folder Execution group
            _grpFolderExecution.Text = "Folder Execution";
            _grpFolderExecution.Location = new Point(16, yPos);
            _grpFolderExecution.Size = new Size(tab.Width - 48, 110);
            _grpFolderExecution.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _rbSequential.Text = "Sequential (one preset at a time)";
            _rbSequential.Location = new Point(16, 24);
            _rbSequential.AutoSize = true;
            _rbSequential.Checked = true;

            _rbParallel.Text = "Parallel (all presets concurrently)";
            _rbParallel.Location = new Point(16, 50);
            _rbParallel.AutoSize = true;

            _chkStopOnError.Text = "Stop on first preset failure";
            _chkStopOnError.Location = new Point(16, 78);
            _chkStopOnError.AutoSize = true;

            _grpFolderExecution.Controls.Add(_rbSequential);
            _grpFolderExecution.Controls.Add(_rbParallel);
            _grpFolderExecution.Controls.Add(_chkStopOnError);
            tab.Controls.Add(_grpFolderExecution);
            yPos += _grpFolderExecution.Height + 16;

            // History Retention group
            _grpHistoryRetention.Text = "History Retention (Per-Job Overrides)";
            _grpHistoryRetention.Location = new Point(16, yPos);
            _grpHistoryRetention.Size = new Size(tab.Width - 48, 100);
            _grpHistoryRetention.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _chkOverrideMaxRuns.Text = "Override maximum runs";
            _chkOverrideMaxRuns.Location = new Point(16, 24);
            _chkOverrideMaxRuns.AutoSize = true;

            _numMaxRuns.Location = new Point(220, 22);
            _numMaxRuns.Size = new Size(80, 23);
            _numMaxRuns.Minimum = 1;
            _numMaxRuns.Maximum = 1000;
            _numMaxRuns.Value = 50;
            _numMaxRuns.Enabled = false;

            _chkOverrideRetention.Text = "Override retention days";
            _chkOverrideRetention.Location = new Point(16, 56);
            _chkOverrideRetention.AutoSize = true;

            _numRetentionDays.Location = new Point(220, 54);
            _numRetentionDays.Size = new Size(80, 23);
            _numRetentionDays.Minimum = 1;
            _numRetentionDays.Maximum = 365;
            _numRetentionDays.Value = 30;
            _numRetentionDays.Enabled = false;

            _grpHistoryRetention.Controls.Add(_chkOverrideMaxRuns);
            _grpHistoryRetention.Controls.Add(_numMaxRuns);
            _grpHistoryRetention.Controls.Add(_chkOverrideRetention);
            _grpHistoryRetention.Controls.Add(_numRetentionDays);
            tab.Controls.Add(_grpHistoryRetention);
        }

        private void UpdateFolderExecutionState()
        {
            var isFolder = _rbFolder.Checked;
            _grpFolderExecution.Enabled = isFolder;
            _chkStopOnError.Enabled = isFolder;
        }

        #endregion

        #region Event Wiring

        private void WireEvents()
        {
            _btnSave.Click += (_, _) => ValidateAndSave();
            _btnCancel.Click += (_, _) =>
            {
                Result = null;
                DialogResult = DialogResult.Cancel;
                Close();
            };

            // Target type radio buttons
            _rbPreset.CheckedChanged += (_, _) =>
            {
                if (_rbPreset.Checked)
                {
                    PopulateTargetCombo();
                    UpdateFolderExecutionState();
                }
            };
            _rbFolder.CheckedChanged += (_, _) =>
            {
                if (_rbFolder.Checked)
                {
                    PopulateTargetCombo();
                    UpdateFolderExecutionState();
                }
            };

            // Schedule type changes show/hide cron and one-time panels
            _cboScheduleType.SelectedIndexChanged += (_, _) =>
            {
                var index = _cboScheduleType.SelectedIndex;
                _panelCron.Visible = index == 1;    // Recurring (Cron)
                _panelOneTime.Visible = index == 2; // One-Time
            };

            // Credential mode radio buttons
            _rbInheritFromApp.CheckedChanged += (_, _) => UpdateCredentialPanels();
            _rbStored.CheckedChanged += (_, _) => UpdateCredentialPanels();
            _rbPerHostColumn.CheckedChanged += (_, _) => UpdateCredentialPanels();

            // History retention overrides
            _chkOverrideMaxRuns.CheckedChanged += (_, _) =>
                _numMaxRuns.Enabled = _chkOverrideMaxRuns.Checked;
            _chkOverrideRetention.CheckedChanged += (_, _) =>
                _numRetentionDays.Enabled = _chkOverrideRetention.Checked;

            // Host grid row changes update count
            _gridHosts.RowsAdded += (_, _) => UpdateHostCount();
            _gridHosts.RowsRemoved += (_, _) => UpdateHostCount();

            // Drift banner acknowledge
            _btnAcknowledgeDrift.Click += BtnAcknowledgeDrift_Click;
        }

        private void UpdateCredentialPanels()
        {
            _panelStoredCreds.Visible = _rbStored.Checked;
            _panelPerHost.Visible = _rbPerHostColumn.Checked;
        }

        #endregion

        #region Prepopulate

        private void PrepopulateFromJob()
        {
            // Name
            _txtName.Text = _editingJob.Name;

            // Target type
            _rbPreset.Checked = _editingJob.TargetType == JobTargetType.Preset;
            _rbFolder.Checked = _editingJob.TargetType == JobTargetType.Folder;
            PopulateTargetCombo();

            // Select target in combo
            if (!string.IsNullOrEmpty(_editingJob.TargetName))
            {
                var idx = _cboTarget.Items.IndexOf(_editingJob.TargetName);
                if (idx >= 0)
                    _cboTarget.SelectedIndex = idx;
            }

            // Schedule type
            _cboScheduleType.SelectedIndex = (int)_editingJob.ScheduleType;
            _panelCron.Visible = _editingJob.ScheduleType == ScheduleType.Recurring;
            _panelOneTime.Visible = _editingJob.ScheduleType == ScheduleType.OneTime;

            // Cron expression
            if (!string.IsNullOrEmpty(_editingJob.CronExpression))
            {
                _cronBuilder.CronExpression = _editingJob.CronExpression;
            }

            // One-time date
            if (_editingJob.OneTimeScheduleUtc.HasValue)
            {
                _dtpOneTime.Value = _editingJob.OneTimeScheduleUtc.Value.ToLocalTime();
            }

            // Hosts grid
            PopulateHostGrid();

            // Credential mode
            switch (_editingJob.CredentialMode)
            {
                case CredentialMode.InheritFromApp:
                    _rbInheritFromApp.Checked = true;
                    break;
                case CredentialMode.Stored:
                    _rbStored.Checked = true;
                    break;
                case CredentialMode.PerHostColumn:
                    _rbPerHostColumn.Checked = true;
                    break;
            }
            UpdateCredentialPanels();

            // Advanced - Folder execution
            _rbSequential.Checked = _editingJob.FolderExecutionMode == FolderExecutionMode.Sequential;
            _rbParallel.Checked = _editingJob.FolderExecutionMode == FolderExecutionMode.Parallel;
            _chkStopOnError.Checked = _editingJob.StopOnError;
            UpdateFolderExecutionState();

            // Advanced - History overrides
            if (_editingJob.MaxHistoryRuns.HasValue)
            {
                _chkOverrideMaxRuns.Checked = true;
                _numMaxRuns.Value = _editingJob.MaxHistoryRuns.Value;
                _numMaxRuns.Enabled = true;
            }
            if (_editingJob.HistoryRetentionDays.HasValue)
            {
                _chkOverrideRetention.Checked = true;
                _numRetentionDays.Value = _editingJob.HistoryRetentionDays.Value;
                _numRetentionDays.Enabled = true;
            }

            // Drift warning
            if (!_isNew && _editingJob.HasDriftWarning)
            {
                _driftBanner.Visible = true;
            }
        }

        #endregion

        #region Host Toolbar Actions

        private void BtnImportCsv_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Import Hosts from CSV",
                Filter = "CSV Files|*.csv|All Files|*.*",
                FilterIndex = 1
            };

            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var lines = File.ReadAllLines(ofd.FileName);
                if (lines.Length == 0) return;

                // Parse header line
                var headers = ParseCsvLine(lines[0]);
                if (headers.Count == 0) return;

                // Require Host_IP column
                if (!headers.Contains("Host_IP", StringComparer.OrdinalIgnoreCase))
                {
                    DialogTheme.Show("CSV must contain a 'Host_IP' column.", "Import Error");
                    return;
                }

                // Rebuild grid columns from CSV
                _gridHosts.Rows.Clear();
                _gridHosts.Columns.Clear();
                foreach (var h in headers)
                {
                    _gridHosts.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        Name = h,
                        HeaderText = h,
                        SortMode = DataGridViewColumnSortMode.NotSortable
                    });
                }

                // Parse data rows
                for (var i = 1; i < lines.Length; i++)
                {
                    var values = ParseCsvLine(lines[i]);
                    if (values.Count == 0) continue;

                    var rowIndex = _gridHosts.Rows.Add();
                    var row = _gridHosts.Rows[rowIndex];
                    for (var c = 0; c < Math.Min(values.Count, headers.Count); c++)
                    {
                        row.Cells[headers[c]].Value = values[c];
                    }
                }

                DialogTheme.StyleDataGridView(_gridHosts, _darkMode);
                UpdateHostCount();
            }
            catch (Exception ex)
            {
                DialogTheme.Show($"Error importing CSV: {ex.Message}", "Import Error");
            }
        }

        private void BtnCopyFromMain_Click(object? sender, EventArgs e)
        {
            if (_getMainGridRows == null || _getMainGridColumns == null)
            {
                DialogTheme.Show("Main grid data is not available.", "Copy Error");
                return;
            }

            var rows = _getMainGridRows();
            var columns = _getMainGridColumns();

            if (rows.Count == 0)
            {
                DialogTheme.Show("No hosts found in the main grid.", "Copy");
                return;
            }

            // Rebuild grid columns from main grid
            _gridHosts.Rows.Clear();
            _gridHosts.Columns.Clear();
            foreach (var colName in columns)
            {
                _gridHosts.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = colName,
                    HeaderText = colName,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }

            // Populate rows
            foreach (var hostRow in rows)
            {
                var rowIndex = _gridHosts.Rows.Add();
                var row = _gridHosts.Rows[rowIndex];
                foreach (var colName in columns)
                {
                    if (hostRow.TryGetValue(colName, out var value))
                    {
                        row.Cells[colName].Value = value;
                    }
                }
            }

            DialogTheme.StyleDataGridView(_gridHosts, _darkMode);
            UpdateHostCount();
        }

        private void BtnAddRow_Click(object? sender, EventArgs e)
        {
            _gridHosts.Rows.Add();
            UpdateHostCount();
        }

        private void BtnRemoveSelected_Click(object? sender, EventArgs e)
        {
            var selectedRows = _gridHosts.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow)
                .OrderByDescending(r => r.Index)
                .ToList();

            foreach (var row in selectedRows)
            {
                _gridHosts.Rows.Remove(row);
            }
            UpdateHostCount();
        }

        #endregion

        #region Drift Banner

        private void BtnAcknowledgeDrift_Click(object? sender, EventArgs e)
        {
            // Show diff between saved and current content hash
            var currentContent = string.Empty;
            if (_editingJob.TargetType == JobTargetType.Preset &&
                _presetManager.Presets.TryGetValue(_editingJob.TargetName, out var preset))
            {
                currentContent = preset.Commands;
            }

            var currentHash = ContentHasher.ComputeHash(currentContent);
            var savedHash = _editingJob.TargetContentHash;

            var message = $"Saved content hash: {savedHash}\n" +
                          $"Current content hash: {currentHash}\n\n" +
                          "The target preset has been modified since this job was saved.\n" +
                          "Click Yes to acknowledge and clear the drift warning.";

            if (DialogTheme.Confirm(this, message, "Review Preset Changes", _darkMode))
            {
                _editingJob.HasDriftWarning = false;
                _driftBanner.Visible = false;
            }
        }

        #endregion

        #region Save Validation

        private ScheduleType GetSelectedScheduleType()
        {
            return _cboScheduleType.SelectedIndex switch
            {
                1 => ScheduleType.Recurring,
                2 => ScheduleType.OneTime,
                _ => ScheduleType.None
            };
        }

        private CredentialMode GetSelectedCredentialMode()
        {
            if (_rbStored.Checked) return CredentialMode.Stored;
            if (_rbPerHostColumn.Checked) return CredentialMode.PerHostColumn;
            return CredentialMode.InheritFromApp;
        }

        private void ValidateAndSave()
        {
            // Extract current form values
            var name = _txtName.Text;
            var targetName = _cboTarget.SelectedItem?.ToString();
            var scheduleType = GetSelectedScheduleType();
            var cronExpression = scheduleType == ScheduleType.Recurring
                ? _cronBuilder.CronExpression : null;
            var oneTimeUtc = scheduleType == ScheduleType.OneTime
                ? _dtpOneTime.Value.ToUniversalTime() : (DateTime?)null;
            var hosts = ExtractHostsFromGrid();
            var credentialMode = GetSelectedCredentialMode();
            var storedUsername = credentialMode == CredentialMode.Stored
                ? _txtUsername.Text : null;

            // Delegate ALL validation to JobEditorValidator
            var error = JobEditorValidator.ValidateAll(
                name, targetName, scheduleType, cronExpression,
                oneTimeUtc, hosts, credentialMode, storedUsername);

            if (error != null)
            {
                DialogTheme.Show(error, "Validation Error");
                return;
            }

            // Populate Result from all controls
            _editingJob.Name = name!.Trim();
            _editingJob.TargetType = _rbFolder.Checked ? JobTargetType.Folder : JobTargetType.Preset;
            _editingJob.TargetName = targetName!;

            // Compute TargetContentHash from preset content
            if (_editingJob.TargetType == JobTargetType.Preset &&
                _presetManager.Presets.TryGetValue(targetName!, out var presetInfo))
            {
                _editingJob.TargetContentHash = ContentHasher.ComputeHash(presetInfo.Commands);
            }
            else if (_editingJob.TargetType == JobTargetType.Folder)
            {
                _editingJob.FolderPresetHashes = new Dictionary<string, string>();
                foreach (var presetName in _presetManager.GetPresetsInFolder(targetName))
                {
                    if (_presetManager.Presets.TryGetValue(presetName, out var fp))
                    {
                        _editingJob.FolderPresetHashes[presetName] = ContentHasher.ComputeHash(fp.Commands);
                    }
                }
                _editingJob.TargetContentHash = string.Empty;
            }

            // Schedule
            _editingJob.ScheduleType = scheduleType;
            _editingJob.CronExpression = cronExpression;
            _editingJob.OneTimeScheduleUtc = oneTimeUtc;

            // Hosts
            _editingJob.Hosts = hosts;
            _editingJob.HostColumns = ExtractHostColumnsFromGrid();

            // Credentials
            _editingJob.CredentialMode = credentialMode;

            // Advanced tab
            _editingJob.FolderExecutionMode = _rbParallel.Checked
                ? FolderExecutionMode.Parallel
                : FolderExecutionMode.Sequential;
            _editingJob.StopOnError = _chkStopOnError.Checked;
            _editingJob.MaxHistoryRuns = _chkOverrideMaxRuns.Checked
                ? (int)_numMaxRuns.Value : null;
            _editingJob.HistoryRetentionDays = _chkOverrideRetention.Checked
                ? (int)_numRetentionDays.Value : null;

            // Metadata
            _editingJob.ModifiedUtc = DateTime.UtcNow;
            _editingJob.HasDriftWarning = false; // Fresh save clears drift

            Result = _editingJob;
            DialogResult = DialogResult.OK;
            Close();
        }

        #endregion

        #region CSV Parsing

        /// <summary>
        /// Simple CSV line parser that handles quoted fields with escaped double-quotes.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            if (string.IsNullOrEmpty(line)) return fields;

            var inQuotes = false;
            var field = new System.Text.StringBuilder();

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            field.Append('"');
                            i++; // skip escaped quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(field.ToString().Trim());
                        field.Clear();
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
            }

            fields.Add(field.ToString().Trim());
            return fields;
        }

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cronBuilder.Dispose();
                _gridHosts.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Dark ToolStrip Support

        /// <summary>
        /// Dark mode color table for the hosts toolbar.
        /// </summary>
        private sealed class DarkToolStripColorTable : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin => DialogTheme.DarkSurface1;
            public override Color ToolStripGradientMiddle => DialogTheme.DarkSurface1;
            public override Color ToolStripGradientEnd => DialogTheme.DarkSurface1;
            public override Color MenuStripGradientBegin => DialogTheme.DarkSurface1;
            public override Color MenuStripGradientEnd => DialogTheme.DarkSurface1;
            public override Color ToolStripBorder => DialogTheme.DarkBorder;
            public override Color SeparatorDark => DialogTheme.DarkBorder;
            public override Color SeparatorLight => DialogTheme.DarkSurface2;
            public override Color ButtonSelectedHighlight => DialogTheme.DarkSurface2;
            public override Color ButtonSelectedGradientBegin => DialogTheme.DarkSurface2;
            public override Color ButtonSelectedGradientEnd => DialogTheme.DarkSurface2;
            public override Color ButtonPressedGradientBegin => DialogTheme.DarkAccent;
            public override Color ButtonPressedGradientEnd => DialogTheme.DarkAccent;
            public override Color ImageMarginGradientBegin => DialogTheme.DarkSurface1;
            public override Color ImageMarginGradientMiddle => DialogTheme.DarkSurface1;
            public override Color ImageMarginGradientEnd => DialogTheme.DarkSurface1;
        }

        #endregion
    }
}
