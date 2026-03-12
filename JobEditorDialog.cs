using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Services.Editor;
using SSH_Helper.UI;
using SSH_Helper.Utilities;

namespace SSH_Helper
{
    /// <summary>
    /// Tabbed modal dialog for creating and editing job definitions.
    /// Provides General, Hosts, Credentials, and Advanced tabs with
    /// cron preview, host list management, and credential configuration.
    /// Works on a deep clone so cancellation
    /// discards all changes.
    /// </summary>
    internal sealed class JobEditorDialog : Form
    {
        #region Fields

        private const int GeneralTabIndex = 0;
        private const int ContentTabIndex = 1;
        private const int HostsTabIndex = 2;
        private const int CredentialsTabIndex = 3;
        private const int AdvancedTabIndex = 4;

        private readonly PresetManager _presetManager;
        private readonly SchedulingService _schedulingService;
        private readonly CsvManager _csvManager;
        private readonly ICredentialProvider? _credentialProvider;
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

        // General tab controls
        private readonly TextBox _txtName;
        private readonly RadioButton _rbPreset;
        private readonly RadioButton _rbFolder;
        private readonly RadioButton _rbCustomPreset;
        private readonly Label _lblTargetSelect;
        private readonly Label _lblCustomTargetInfo;
        private readonly Label _lblSchedule;
        private readonly ComboBox _cboTarget;
        private readonly ComboBox _cboScheduleType;
        private readonly Panel _panelCron;
        private readonly CronBuilderControl _cronBuilder;
        private readonly Panel _panelOneTime;
        private readonly DateTimePicker _dtpOneTime;

        // Content tab controls
        private readonly ScintillaScriptEditorControl _txtCustomPresetCommands;
        private readonly Label _lblContentHelp;
        private readonly ScriptAutocompleteProvider _customPresetAutocompleteProvider;
        private readonly YamlSshSyntaxHighlighter _customPresetSyntaxHighlighter = new();
        private readonly ScriptEditorValidationService _customPresetValidationService = new();
        private readonly CommandEditorSettings _customPresetEditorSettings = new();

        // Hosts tab controls
        private readonly ToolStrip _hostsToolStrip;
        private readonly ContextMenuStrip _hostsContextMenu;
        private readonly DataGridView _gridHosts;
        private readonly Label _lblHostCount;
        private int _hostsRightClickedColumnIndex = -1;
        private int _hostsRightClickedRowIndex = -1;

        // Credentials tab controls
        private readonly RadioButton _rbInheritFromApp;
        private readonly RadioButton _rbStored;
        private readonly RadioButton _rbPerHostColumn;
        private readonly Panel _panelStoredCreds;
        private readonly TextBox _txtUsername;
        private readonly TextBox _txtPassword;
        private readonly Label _lblStoredCredNote;
        private readonly Panel _panelPerHost;
        private bool _hasStoredPassword;

        // Advanced tab controls
        private readonly GroupBox _grpFolderExecution;
        private readonly RadioButton _rbSequential;
        private readonly RadioButton _rbParallel;
        private readonly CheckBox _chkStopOnError;
        private readonly GroupBox _grpTimeoutOverrides;
        private readonly CheckBox _chkOverrideCommandTimeout;
        private readonly NumericUpDown _numCommandTimeoutOverride;
        private readonly Label _lblCommandTimeoutSource;
        private readonly CheckBox _chkOverrideConnectionTimeout;
        private readonly NumericUpDown _numConnectionTimeoutOverride;
        private readonly Label _lblConnectionTimeoutSource;
        private readonly GroupBox _grpHistoryRetention;
        private readonly CheckBox _chkOverrideMaxRuns;
        private readonly NumericUpDown _numMaxRuns;
        private readonly CheckBox _chkOverrideRetention;
        private readonly NumericUpDown _numRetentionDays;
        private bool _syncingCronPanelLayout;
        private bool _hasInitializedCommandTimeoutOverrideValue;
        private bool _hasInitializedConnectionTimeoutOverrideValue;

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
            ICredentialProvider? credentialProvider,
            Func<IReadOnlyList<Dictionary<string, string>>>? getMainGridRows,
            Func<IReadOnlyList<string>>? getMainGridColumns,
            bool darkMode,
            string? fontFamily = null,
            float fontSize = 9f)
        {
            _presetManager = presetManager;
            _schedulingService = schedulingService;
            _csvManager = new CsvManager();
            _credentialProvider = credentialProvider;
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

            _txtName = new TextBox();
            _rbPreset = new RadioButton();
            _rbFolder = new RadioButton();
            _rbCustomPreset = new RadioButton();
            _lblTargetSelect = new Label();
            _lblCustomTargetInfo = new Label();
            _lblSchedule = new Label();
            _cboTarget = new ComboBox();
            _cboScheduleType = new ComboBox();
            _panelCron = new Panel();
            _cronBuilder = new CronBuilderControl();
            _panelOneTime = new Panel();
            _dtpOneTime = new DateTimePicker();
            _txtCustomPresetCommands = new ScintillaScriptEditorControl();
            _lblContentHelp = new Label();
            _customPresetAutocompleteProvider = new ScriptAutocompleteProvider(GetCustomPresetHostColumns);

            _hostsToolStrip = new ToolStrip();
            _hostsContextMenu = new ContextMenuStrip();
            _gridHosts = new DataGridView();
            _lblHostCount = new Label();

            _rbInheritFromApp = new RadioButton();
            _rbStored = new RadioButton();
            _rbPerHostColumn = new RadioButton();
            _panelStoredCreds = new Panel();
            _txtUsername = new TextBox();
            _txtPassword = new TextBox();
            _lblStoredCredNote = new Label();
            _panelPerHost = new Panel();

            _grpFolderExecution = new GroupBox();
            _rbSequential = new RadioButton();
            _rbParallel = new RadioButton();
            _chkStopOnError = new CheckBox();
            _grpTimeoutOverrides = new GroupBox();
            _chkOverrideCommandTimeout = new CheckBox();
            _numCommandTimeoutOverride = new NumericUpDown();
            _lblCommandTimeoutSource = new Label();
            _chkOverrideConnectionTimeout = new CheckBox();
            _numConnectionTimeoutOverride = new NumericUpDown();
            _lblConnectionTimeoutSource = new Label();
            _grpHistoryRetention = new GroupBox();
            _chkOverrideMaxRuns = new CheckBox();
            _numMaxRuns = new NumericUpDown();
            _chkOverrideRetention = new CheckBox();
            _numRetentionDays = new NumericUpDown();

            SuspendLayout();
            BuildDialogChrome();
            BuildGeneralTab();
            BuildContentTab();
            BuildHostsTab();
            BuildCredentialsTab();
            BuildAdvancedTab();
            BuildBottomPanel();
            InitializeCustomPresetEditor();
            WireEvents();
            PrepopulateFromJob();
            ResumeLayout(true);

            // Apply theming
            var font = fontFamily != null ? new Font(fontFamily, fontSize) : new Font("Segoe UI", fontSize);
            DialogTheme.SetDialogFont(this, font);
            _txtCustomPresetCommands.Font = font;
            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.SetDarkTitleBar(this, darkMode);
            DialogTheme.StyleTabControl(_tabControl, darkMode);
            DialogTheme.StyleDataGridView(_gridHosts, darkMode);
            ApplyHostGridParityStyle();
            DialogTheme.StyleButton(_btnSave, darkMode, isPrimary: true);
            DialogTheme.StyleButton(_btnCancel, darkMode);
            _cronBuilder.ApplyTheme(darkMode);
            _cronBuilder.SetSchedulingService(schedulingService);
            _txtCustomPresetCommands.ApplyTheme(darkMode);
            RefreshCronPanelLayout();

            // Style toolstrip for dark mode
            if (darkMode)
            {
                _hostsToolStrip.BackColor = DialogTheme.DarkSurface1;
                _hostsToolStrip.ForeColor = DialogTheme.DarkText;
                _hostsToolStrip.Renderer = new ToolStripProfessionalRenderer(
                    new DarkToolStripColorTable());
                _hostsContextMenu.BackColor = DialogTheme.DarkSurface2;
                _hostsContextMenu.ForeColor = DialogTheme.DarkText;
                _hostsContextMenu.Renderer = new ToolStripProfessionalRenderer(
                    new DarkToolStripColorTable());
            }

            Load += (_, _) =>
            {
                DialogTheme.ApplyNativeTheme(this, darkMode);
                RefreshCronPanelLayout();

            };

            if (darkMode)
            {
                // Re-apply native theme when switching tabs. Controls on
                // non-visible tab pages can lose their native dark rendering
                // until they are actually shown.
                _tabControl.Selected += (_, e) =>
                {
                    if (e.TabPage != null)
                        BeginInvoke(() => DialogTheme.ApplyNativeTheme(e.TabPage, darkMode));
                };
            }
        }

        #endregion

        #region Dialog Chrome

        private void BuildDialogChrome()
        {
            Text = _isNew ? "New Job" : $"Edit Job - {_editingJob.Name}";
            Size = new Size(750, 600);
            MinimumSize = new Size(750, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;

            _tabControl.Dock = DockStyle.Fill;

            var tabGeneral = new TabPage("General");
            var tabContent = new TabPage("Content");
            var tabHosts = new TabPage("Hosts");
            var tabCredentials = new TabPage("Credentials");
            var tabAdvanced = new TabPage("Advanced");

            _tabControl.TabPages.Add(tabGeneral);
            _tabControl.TabPages.Add(tabContent);
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

            _rbCustomPreset.Text = "Custom Preset";
            _rbCustomPreset.Location = new Point(controlLeft + 280, yPos);
            _rbCustomPreset.AutoSize = true;

            tab.Controls.Add(lblTarget);
            tab.Controls.Add(_rbPreset);
            tab.Controls.Add(_rbFolder);
            tab.Controls.Add(_rbCustomPreset);
            yPos += 30;

            // Target Selector
            _lblTargetSelect.Text = "Target:";
            _lblTargetSelect.Location = new Point(16, yPos + 3);
            _lblTargetSelect.Size = new Size(labelWidth, 20);
            _lblTargetSelect.TextAlign = ContentAlignment.MiddleLeft;
            _cboTarget.Location = new Point(controlLeft, yPos);
            _cboTarget.Size = new Size(300, 23);
            _cboTarget.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _cboTarget.DropDownStyle = ComboBoxStyle.DropDownList;
            _lblCustomTargetInfo.Text = "Custom preset content is stored with this job only. Author it on the Content tab.";
            _lblCustomTargetInfo.Location = new Point(controlLeft, yPos + 3);
            _lblCustomTargetInfo.Size = new Size(tab.Width - controlLeft - 32, 36);
            _lblCustomTargetInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _lblCustomTargetInfo.Visible = false;
            tab.Controls.Add(_lblTargetSelect);
            tab.Controls.Add(_cboTarget);
            tab.Controls.Add(_lblCustomTargetInfo);
            yPos += 32;

            // Schedule Type
            _lblSchedule.Text = "Schedule:";
            _lblSchedule.Location = new Point(16, yPos + 3);
            _lblSchedule.Size = new Size(labelWidth, 20);
            _lblSchedule.TextAlign = ContentAlignment.MiddleLeft;
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
            tab.Controls.Add(_lblSchedule);
            tab.Controls.Add(_cboScheduleType);
            yPos += 32;

            // Cron panel (visible only when Recurring selected)
            BuildCronPanel(tab, ref yPos);

            // One-time panel (visible only when One-Time selected)
            BuildOneTimePanel(tab, ref yPos);
        }

        private void BuildContentTab()
        {
            var tab = _tabControl.TabPages[ContentTabIndex];
            tab.Padding = new Padding(16);

            _lblContentHelp.Dock = DockStyle.Top;
            _lblContentHelp.Height = 28;
            _lblContentHelp.TextAlign = ContentAlignment.MiddleLeft;
            _lblContentHelp.Text = "Switch Target Type to Custom Preset to author job-local content. Plain commands and YAML scripts are supported.";

            var editorPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0)
            };

            _txtCustomPresetCommands.Dock = DockStyle.Fill;
            editorPanel.Controls.Add(_txtCustomPresetCommands);

            tab.Controls.Add(editorPanel);
            tab.Controls.Add(_lblContentHelp);
        }

        private void BuildCronPanel(TabPage tab, ref int yPos)
        {
            _panelCron.Location = new Point(16, yPos);
            _panelCron.Size = new Size(tab.Width - 48, Math.Max(_cronBuilder.MinimumSize.Height, _cronBuilder.Height));
            _panelCron.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _panelCron.Visible = false;

            _cronBuilder.Location = new Point(0, 0);
            _cronBuilder.Size = new Size(_panelCron.Width, Math.Max(_cronBuilder.MinimumSize.Height, _cronBuilder.Height));
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

        private void RefreshCronPanelLayout()
        {
            if (_syncingCronPanelLayout || IsDisposed || _panelCron.IsDisposed || _cronBuilder.IsDisposed)
            {
                return;
            }

            // Explicitly compute panel width from parent tab page to avoid
            // stale anchor distances caused by early layout during SuspendLayout.
            if (_panelCron.Parent is Control parentTab && parentTab.ClientSize.Width > 0)
            {
                var expectedWidth = parentTab.ClientSize.Width - _panelCron.Left - 32;
                if (expectedWidth > 0 && _panelCron.Width != expectedWidth)
                {
                    _panelCron.Width = expectedWidth;
                }
            }

            var panelWidth = _panelCron.ClientSize.Width;
            if (panelWidth <= 0)
            {
                return;
            }

            _syncingCronPanelLayout = true;

            try
            {
                _cronBuilder.Location = Point.Empty;

                if (_cronBuilder.Width != panelWidth)
                {
                    _cronBuilder.Width = panelWidth;
                }

                _cronBuilder.PerformLayout();

                var desiredHeight = Math.Max(_cronBuilder.Height, _cronBuilder.MinimumSize.Height);
                if (_cronBuilder.Height != desiredHeight)
                {
                    _cronBuilder.Height = desiredHeight;
                }

                if (_panelCron.Height != desiredHeight)
                {
                    _panelCron.Height = desiredHeight;
                }

                _panelCron.Parent?.PerformLayout();
            }
            finally
            {
                _syncingCronPanelLayout = false;
            }
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

        private void InitializeCustomPresetEditor()
        {
            _txtCustomPresetCommands.SetAutocompleteProvider(_customPresetAutocompleteProvider);
            _txtCustomPresetCommands.SetSyntaxHighlighter(_customPresetSyntaxHighlighter);
            _txtCustomPresetCommands.SetValidationService(_customPresetValidationService);
            _txtCustomPresetCommands.SetVariableTooltipResolvers(
                variableResolver: null,
                columnResolver: ResolveCustomPresetColumnValue);
            _txtCustomPresetCommands.ApplyCommandEditorSettings(_customPresetEditorSettings);
        }

        private IReadOnlyCollection<string> GetCustomPresetHostColumns()
        {
            return _gridHosts.Columns
                .Cast<DataGridViewColumn>()
                .Select(column => column.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }

        private string? ResolveCustomPresetColumnValue(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName) || !_gridHosts.Columns.Contains(columnName))
            {
                return null;
            }

            var previewRow = GetPreviewHostRow();
            return previewRow?.Cells[columnName].Value?.ToString();
        }

        private DataGridViewRow? GetPreviewHostRow()
        {
            if (_gridHosts.CurrentRow != null && !_gridHosts.CurrentRow.IsNewRow)
            {
                return _gridHosts.CurrentRow;
            }

            return _gridHosts.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(row => !row.IsNewRow);
        }

        private JobTargetType GetSelectedTargetType()
        {
            if (_rbFolder.Checked)
                return JobTargetType.Folder;

            if (_rbCustomPreset.Checked)
                return JobTargetType.CustomPreset;

            return JobTargetType.Preset;
        }

        private void UpdateTargetModeUi()
        {
            var targetType = GetSelectedTargetType();
            var usesNamedTarget = targetType != JobTargetType.CustomPreset;

            _lblTargetSelect.Visible = usesNamedTarget;
            _cboTarget.Visible = usesNamedTarget;
            _cboTarget.Enabled = usesNamedTarget;
            _lblCustomTargetInfo.Visible = !usesNamedTarget;

            if (usesNamedTarget)
            {
                PopulateTargetCombo();
            }
            else
            {
                _cboTarget.SelectedIndex = -1;
            }

            UpdateFolderExecutionState();
            UpdateCustomPresetEditorState();
            UpdateGeneralTabLayout();
            UpdateTimeoutOverrideState();
        }

        private void UpdateCustomPresetEditorState()
        {
            var isCustomPreset = GetSelectedTargetType() == JobTargetType.CustomPreset;
            _txtCustomPresetCommands.ReadOnly = !isCustomPreset;
            _lblContentHelp.Text = isCustomPreset
                ? "This content is stored with the job only. Plain commands and YAML scripts are supported."
                : "Switch Target Type to Custom Preset to author job-local content. Saved preset and folder jobs ignore this tab.";
        }

        private void UpdateGeneralTabLayout()
        {
            const int rowSpacing = 9;
            const int labelOffsetY = 3;

            var customInfoWidth = Math.Max(_lblCustomTargetInfo.Width, 1);
            var measuredCustomHeight = TextRenderer.MeasureText(
                _lblCustomTargetInfo.Text,
                _lblCustomTargetInfo.Font,
                new Size(customInfoWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;

            _lblCustomTargetInfo.Height = Math.Max(36, measuredCustomHeight);

            var scheduleTop = GetSelectedTargetType() == JobTargetType.CustomPreset
                ? _lblCustomTargetInfo.Bottom + rowSpacing
                : _cboTarget.Bottom + rowSpacing;

            _lblSchedule.Location = new Point(_lblSchedule.Left, scheduleTop + labelOffsetY);
            _cboScheduleType.Location = new Point(_cboScheduleType.Left, scheduleTop);

            var panelTop = _cboScheduleType.Bottom + rowSpacing;
            _panelCron.Location = new Point(_panelCron.Left, panelTop);
            _panelOneTime.Location = new Point(_panelOneTime.Left, panelTop);

            RefreshCronPanelLayout();
        }

        #endregion

        #region Hosts Tab

        private void BuildHostsTab()
        {
            var tab = _tabControl.TabPages[HostsTabIndex];

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
            var btnAddColumn = new ToolStripButton("Add Column")
            {
                ToolTipText = "Add a host column"
            };
            var btnRenameColumn = new ToolStripButton("Rename Column")
            {
                ToolTipText = "Rename the current host column"
            };
            var btnDeleteColumn = new ToolStripButton("Delete Column")
            {
                ToolTipText = "Delete the current host column"
            };

            btnImportCsv.Click += BtnImportCsv_Click;
            btnCopyFromMain.Click += BtnCopyFromMain_Click;
            btnAddRow.Click += BtnAddRow_Click;
            btnRemoveSelected.Click += BtnRemoveSelected_Click;
            btnAddColumn.Click += (_, _) => AddHostColumn();
            btnRenameColumn.Click += (_, _) => RenameHostColumn(GetActiveHostColumnIndex());
            btnDeleteColumn.Click += (_, _) => DeleteHostColumn(GetActiveHostColumnIndex());

            _hostsToolStrip.Items.Add(btnImportCsv);
            _hostsToolStrip.Items.Add(new ToolStripSeparator());
            _hostsToolStrip.Items.Add(btnCopyFromMain);
            _hostsToolStrip.Items.Add(new ToolStripSeparator());
            _hostsToolStrip.Items.Add(btnAddRow);
            _hostsToolStrip.Items.Add(btnRemoveSelected);
            _hostsToolStrip.Items.Add(new ToolStripSeparator());
            _hostsToolStrip.Items.Add(btnAddColumn);
            _hostsToolStrip.Items.Add(btnRenameColumn);
            _hostsToolStrip.Items.Add(btnDeleteColumn);

            tab.Controls.Add(_hostsToolStrip);

            // Host count label at bottom
            _lblHostCount.Text = "0 host(s)";
            _lblHostCount.Dock = DockStyle.Bottom;
            _lblHostCount.Height = 24;
            _lblHostCount.TextAlign = ContentAlignment.MiddleLeft;
            _lblHostCount.Padding = new Padding(8, 0, 0, 0);
            tab.Controls.Add(_lblHostCount);

            _gridHosts.Dock = DockStyle.Fill;
            _gridHosts.AllowUserToAddRows = true;
            _gridHosts.AllowUserToDeleteRows = true;
            _gridHosts.AllowUserToResizeRows = false;
            _gridHosts.AllowUserToOrderColumns = true;
            _gridHosts.ReadOnly = false;
            _gridHosts.MultiSelect = true;
            _gridHosts.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _gridHosts.EditMode = DataGridViewEditMode.EditProgrammatically;
            _gridHosts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _gridHosts.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            _gridHosts.RowHeadersVisible = true;
            _gridHosts.ContextMenuStrip = _hostsContextMenu;

            BuildHostsContextMenu();
            ApplyHostGridSnapshot(new List<string> { CsvManager.HostColumnName }, Array.Empty<Dictionary<string, string>>());

            tab.Controls.Add(_gridHosts);
            _gridHosts.BringToFront();
        }

        private void UpdateHostCount()
        {
            _lblHostCount.Text = $"{HostGridUtilities.CountHosts(_gridHosts)} host(s)";
        }

        private void PopulateHostGrid()
        {
            var columns = _editingJob.HostColumns.Count > 0
                ? _editingJob.HostColumns
                : new List<string> { CsvManager.HostColumnName };

            ApplyHostGridSnapshot(columns, _editingJob.Hosts);
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
            return _gridHosts.Columns
                .Cast<DataGridViewColumn>()
                .OrderBy(column => column.DisplayIndex)
                .Select(column => column.Name)
                .ToList();
        }

        private void BuildHostsContextMenu()
        {
            _hostsContextMenu.Items.Clear();

            var addColumnItem = new ToolStripMenuItem("Add Column");
            addColumnItem.Click += (_, _) => AddHostColumn();

            var renameColumnItem = new ToolStripMenuItem("Rename Column");
            renameColumnItem.Click += (_, _) => RenameHostColumn(_hostsRightClickedColumnIndex);

            var deleteColumnItem = new ToolStripMenuItem("Delete Column");
            deleteColumnItem.Click += (_, _) => DeleteHostColumn(_hostsRightClickedColumnIndex);

            var deleteRowItem = new ToolStripMenuItem("Delete Row");
            deleteRowItem.Click += (_, _) => DeleteHostRow(_hostsRightClickedRowIndex);

            _hostsContextMenu.Items.Add(addColumnItem);
            _hostsContextMenu.Items.Add(renameColumnItem);
            _hostsContextMenu.Items.Add(deleteColumnItem);
            _hostsContextMenu.Items.Add(new ToolStripSeparator());
            _hostsContextMenu.Items.Add(deleteRowItem);

            _hostsContextMenu.Opening += (_, _) =>
            {
                var renameEnabled = _hostsRightClickedColumnIndex >= 0 &&
                    _hostsRightClickedColumnIndex < _gridHosts.Columns.Count &&
                    !HostGridUtilities.IsProtectedHostColumn(_gridHosts.Columns[_hostsRightClickedColumnIndex]);
                var deleteEnabled = renameEnabled;

                renameColumnItem.Visible = _hostsRightClickedColumnIndex >= 0;
                renameColumnItem.Enabled = renameEnabled;
                deleteColumnItem.Visible = _hostsRightClickedColumnIndex >= 0;
                deleteColumnItem.Enabled = deleteEnabled;

                deleteRowItem.Visible = _hostsRightClickedRowIndex >= 0;
                deleteRowItem.Enabled = _hostsRightClickedRowIndex >= 0;
            };
        }

        private void ApplyHostGridParityStyle()
        {
            _gridHosts.ColumnHeadersHeight = HostGridUtilities.DefaultColumnHeaderHeight;
            _gridHosts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _gridHosts.RowHeadersWidth = HostGridUtilities.DefaultRowHeaderWidth;
            _gridHosts.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            _gridHosts.RowTemplate.Height = HostGridUtilities.DefaultRowHeight;
            _gridHosts.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _gridHosts.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
            _gridHosts.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);

            if (_darkMode)
            {
                _gridHosts.RowHeadersDefaultCellStyle.BackColor = DialogTheme.GridDarkHeader;
                _gridHosts.RowHeadersDefaultCellStyle.ForeColor = DialogTheme.DarkSecondaryText;
            }
            else
            {
                _gridHosts.RowHeadersDefaultCellStyle.BackColor = DialogTheme.LightBackground;
                _gridHosts.RowHeadersDefaultCellStyle.ForeColor = DialogTheme.LightSecondaryText;
            }
        }

        private void ApplyHostGridSnapshot(
            IReadOnlyList<string> columns,
            IEnumerable<Dictionary<string, string>> rows)
        {
            var normalizedColumns = columns
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!normalizedColumns.Contains(CsvManager.HostColumnName, StringComparer.OrdinalIgnoreCase))
            {
                normalizedColumns.Insert(0, CsvManager.HostColumnName);
            }

            if (normalizedColumns.Count == 0)
            {
                normalizedColumns.Add(CsvManager.HostColumnName);
            }

            _gridHosts.Rows.Clear();
            _gridHosts.Columns.Clear();

            foreach (var columnName in normalizedColumns)
            {
                _gridHosts.Columns.Add(HostGridUtilities.CreateTextColumn(columnName));
            }

            foreach (var hostRow in rows)
            {
                var rowIndex = _gridHosts.Rows.Add();
                var gridRow = _gridHosts.Rows[rowIndex];
                foreach (var columnName in normalizedColumns)
                {
                    if (hostRow.TryGetValue(columnName, out var value))
                    {
                        gridRow.Cells[columnName].Value = value;
                    }
                }
            }

            UpdateHostCount();
        }

        private int GetActiveHostColumnIndex()
        {
            if (_gridHosts.CurrentCell != null)
            {
                return _gridHosts.CurrentCell.ColumnIndex;
            }

            if (_gridHosts.SelectedCells.Count > 0)
            {
                return _gridHosts.SelectedCells[0].ColumnIndex;
            }

            return -1;
        }

        private void AddHostColumn()
        {
            string defaultName = HostGridUtilities.GetNextGeneratedColumnName(_gridHosts.Columns);
            string columnName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the name of the new column:",
                "Add Column",
                defaultName);

            if (string.IsNullOrWhiteSpace(columnName))
            {
                return;
            }

            columnName = InputValidator.SanitizeColumnName(columnName);
            if (_gridHosts.Columns.Cast<DataGridViewColumn>()
                .Any(column => string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase)))
            {
                DialogTheme.Show("Column name already exists!", "Add Column");
                return;
            }

            _gridHosts.Columns.Add(HostGridUtilities.CreateTextColumn(columnName));
        }

        private void RenameHostColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= _gridHosts.Columns.Count)
            {
                return;
            }

            var column = _gridHosts.Columns[columnIndex];
            if (HostGridUtilities.IsProtectedHostColumn(column))
            {
                DialogTheme.Show("The Host_IP column cannot be renamed.", "Rename Column");
                return;
            }

            var currentName = column.HeaderText;
            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter a new name for the column '{currentName}':",
                "Rename Column",
                currentName);

            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, currentName, StringComparison.Ordinal))
            {
                return;
            }

            newName = InputValidator.SanitizeColumnName(newName);
            if (_gridHosts.Columns.Cast<DataGridViewColumn>()
                .Any(existing => !ReferenceEquals(existing, column) &&
                                 string.Equals(existing.Name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                DialogTheme.Show("This column name already exists.", "Rename Column Error");
                return;
            }

            column.HeaderText = newName;
            column.Name = newName;
        }

        private void DeleteHostColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= _gridHosts.Columns.Count)
            {
                return;
            }

            if (HostGridUtilities.IsProtectedHostColumn(_gridHosts.Columns[columnIndex]))
            {
                DialogTheme.Show("The Host_IP column cannot be deleted.", "Delete Column");
                return;
            }

            _gridHosts.Columns.RemoveAt(columnIndex);
            UpdateHostCount();
        }

        private void DeleteHostRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _gridHosts.Rows.Count)
            {
                return;
            }

            var row = _gridHosts.Rows[rowIndex];
            if (row.IsNewRow)
            {
                return;
            }

            _gridHosts.Rows.RemoveAt(rowIndex);
            UpdateHostCount();
        }

        #endregion

        #region Credentials Tab

        private void BuildCredentialsTab()
        {
            var tab = _tabControl.TabPages[CredentialsTabIndex];
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

            _lblStoredCredNote.Text = SchedulerJobIntegrityUtilities.FormatStoredCredentialNote(hasStoredPassword: false);
            _lblStoredCredNote.Location = new Point(0, 66);
            _lblStoredCredNote.AutoSize = true;
            _lblStoredCredNote.ForeColor = Color.FromArgb(108, 117, 125);
            _txtPassword.PlaceholderText = "Leave blank to keep the stored password";

            _panelStoredCreds.Controls.Add(lblUsername);
            _panelStoredCreds.Controls.Add(_txtUsername);
            _panelStoredCreds.Controls.Add(lblPassword);
            _panelStoredCreds.Controls.Add(_txtPassword);
            _panelStoredCreds.Controls.Add(_lblStoredCredNote);
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
            var tab = _tabControl.TabPages[AdvancedTabIndex];
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

            // Timeout Overrides group
            _grpTimeoutOverrides.Text = "Timeouts (Per-Job Overrides)";
            _grpTimeoutOverrides.Location = new Point(16, yPos);
            _grpTimeoutOverrides.Size = new Size(tab.Width - 48, 128);
            _grpTimeoutOverrides.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _chkOverrideCommandTimeout.Text = "Override command timeout";
            _chkOverrideCommandTimeout.Location = new Point(16, 24);
            _chkOverrideCommandTimeout.AutoSize = true;

            _numCommandTimeoutOverride.Location = new Point(250, 22);
            _numCommandTimeoutOverride.Size = new Size(80, 23);
            _numCommandTimeoutOverride.Minimum = JobEditorValidator.MinCommandTimeoutOverrideSeconds;
            _numCommandTimeoutOverride.Maximum = JobEditorValidator.MaxCommandTimeoutOverrideSeconds;
            _numCommandTimeoutOverride.Value = JobEditorValidator.MinCommandTimeoutOverrideSeconds;
            _numCommandTimeoutOverride.Enabled = false;

            _lblCommandTimeoutSource.Location = new Point(32, 50);
            _lblCommandTimeoutSource.Size = new Size(_grpTimeoutOverrides.Width - 48, 18);
            _lblCommandTimeoutSource.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _lblCommandTimeoutSource.ForeColor = Color.FromArgb(108, 117, 125);

            _chkOverrideConnectionTimeout.Text = "Override connection timeout";
            _chkOverrideConnectionTimeout.Location = new Point(16, 76);
            _chkOverrideConnectionTimeout.AutoSize = true;

            _numConnectionTimeoutOverride.Location = new Point(250, 74);
            _numConnectionTimeoutOverride.Size = new Size(80, 23);
            _numConnectionTimeoutOverride.Minimum = JobEditorValidator.MinConnectionTimeoutOverrideSeconds;
            _numConnectionTimeoutOverride.Maximum = JobEditorValidator.MaxConnectionTimeoutOverrideSeconds;
            _numConnectionTimeoutOverride.Value = JobEditorValidator.MinConnectionTimeoutOverrideSeconds;
            _numConnectionTimeoutOverride.Enabled = false;

            _lblConnectionTimeoutSource.Location = new Point(32, 102);
            _lblConnectionTimeoutSource.Size = new Size(_grpTimeoutOverrides.Width - 48, 18);
            _lblConnectionTimeoutSource.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _lblConnectionTimeoutSource.ForeColor = Color.FromArgb(108, 117, 125);

            _grpTimeoutOverrides.Controls.Add(_chkOverrideCommandTimeout);
            _grpTimeoutOverrides.Controls.Add(_numCommandTimeoutOverride);
            _grpTimeoutOverrides.Controls.Add(_lblCommandTimeoutSource);
            _grpTimeoutOverrides.Controls.Add(_chkOverrideConnectionTimeout);
            _grpTimeoutOverrides.Controls.Add(_numConnectionTimeoutOverride);
            _grpTimeoutOverrides.Controls.Add(_lblConnectionTimeoutSource);
            tab.Controls.Add(_grpTimeoutOverrides);
            yPos += _grpTimeoutOverrides.Height + 16;

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
                    UpdateTargetModeUi();
                }
            };
            _rbFolder.CheckedChanged += (_, _) =>
            {
                if (_rbFolder.Checked)
                {
                    UpdateTargetModeUi();
                }
            };
            _rbCustomPreset.CheckedChanged += (_, _) =>
            {
                if (_rbCustomPreset.Checked)
                {
                    UpdateTargetModeUi();
                }
            };

            // Schedule type changes show/hide cron and one-time panels
            _cboScheduleType.SelectedIndexChanged += (_, _) =>
            {
                var index = _cboScheduleType.SelectedIndex;
                _panelCron.Visible = index == 1;    // Recurring (Cron)
                _panelOneTime.Visible = index == 2; // One-Time
                UpdateGeneralTabLayout();
            };

            _panelCron.SizeChanged += (_, _) => RefreshCronPanelLayout();
            _cronBuilder.SizeChanged += (_, _) => RefreshCronPanelLayout();
            _tabControl.TabPages[GeneralTabIndex].Resize += (_, _) => UpdateGeneralTabLayout();

            // Credential mode radio buttons
            _rbInheritFromApp.CheckedChanged += (_, _) => UpdateCredentialPanels();
            _rbStored.CheckedChanged += (_, _) => UpdateCredentialPanels();
            _rbPerHostColumn.CheckedChanged += (_, _) => UpdateCredentialPanels();
            _cboTarget.SelectedIndexChanged += (_, _) => UpdateTimeoutOverrideGuidance();

            // Timeout override toggles
            _chkOverrideCommandTimeout.CheckedChanged += (_, _) => UpdateTimeoutOverrideState();
            _chkOverrideConnectionTimeout.CheckedChanged += (_, _) => UpdateTimeoutOverrideState();

            // History retention overrides
            _chkOverrideMaxRuns.CheckedChanged += (_, _) =>
                _numMaxRuns.Enabled = _chkOverrideMaxRuns.Checked;
            _chkOverrideRetention.CheckedChanged += (_, _) =>
                _numRetentionDays.Enabled = _chkOverrideRetention.Checked;

            _gridHosts.MouseDown += GridHosts_MouseDown;
            _gridHosts.RowPostPaint += GridHosts_RowPostPaint;
            _gridHosts.CellPainting += GridHosts_CellPainting;
            _gridHosts.CellClick += GridHosts_CellClick;
            _gridHosts.ColumnAdded += GridHosts_ColumnAdded;
            _gridHosts.CellLeave += GridHosts_CellLeave;
            _gridHosts.CellValueChanged += GridHosts_CellValueChanged;
            _gridHosts.RowsAdded += GridHosts_RowsAdded;
            _gridHosts.RowsRemoved += GridHosts_RowsRemoved;
            _gridHosts.KeyPress += GridHosts_KeyPress;
            _gridHosts.KeyDown += GridHosts_KeyDown;
            _gridHosts.CellDoubleClick += GridHosts_CellDoubleClick;

        }

        private void UpdateCredentialPanels()
        {
            _panelStoredCreds.Visible = _rbStored.Checked;
            _panelPerHost.Visible = _rbPerHostColumn.Checked;
            UpdateStoredCredentialNote();
        }

        private void LoadStoredCredentials()
        {
            _hasStoredPassword = false;

            if (_credentialProvider?.IsAvailable != true || _editingJob.CredentialMode != CredentialMode.Stored)
            {
                UpdateStoredCredentialNote();
                return;
            }

            if (_credentialProvider.TryGetPassword(
                    CredentialTargets.JobPasswordTarget(_editingJob.Id),
                    out var storedUsername,
                    out var storedPassword))
            {
                if (!string.IsNullOrWhiteSpace(storedUsername))
                {
                    _txtUsername.Text = storedUsername;
                }

                _hasStoredPassword = !string.IsNullOrEmpty(storedPassword);
            }

            _txtPassword.Clear();
            UpdateStoredCredentialNote();
        }

        private void UpdateStoredCredentialNote()
        {
            _lblStoredCredNote.Text = SchedulerJobIntegrityUtilities.FormatStoredCredentialNote(_hasStoredPassword);
        }

        private void UpdateTimeoutOverrideState()
        {
            _numCommandTimeoutOverride.Enabled = _chkOverrideCommandTimeout.Checked;
            _numConnectionTimeoutOverride.Enabled = _chkOverrideConnectionTimeout.Checked;

            if (_chkOverrideCommandTimeout.Checked && !_hasInitializedCommandTimeoutOverrideValue)
            {
                _numCommandTimeoutOverride.Value = ClampNumericValue(
                    _numCommandTimeoutOverride,
                    GetInheritedCommandTimeoutInfo().TimeoutSeconds);
                _hasInitializedCommandTimeoutOverrideValue = true;
            }

            if (_chkOverrideConnectionTimeout.Checked && !_hasInitializedConnectionTimeoutOverrideValue)
            {
                _numConnectionTimeoutOverride.Value = ClampNumericValue(
                    _numConnectionTimeoutOverride,
                    _presetManager.GetCurrentConfiguration().ConnectionTimeout);
                _hasInitializedConnectionTimeoutOverrideValue = true;
            }

            UpdateTimeoutOverrideGuidance();
        }

        private void UpdateTimeoutOverrideGuidance()
        {
            var (commandSource, commandTimeoutSeconds) = GetInheritedCommandTimeoutInfo();
            _lblCommandTimeoutSource.Text = _chkOverrideCommandTimeout.Checked
                ? "Using per-job command timeout override."
                : $"Inherited: {commandSource} ({commandTimeoutSeconds} sec)";

            var connectionTimeoutSeconds = _presetManager.GetCurrentConfiguration().ConnectionTimeout;
            _lblConnectionTimeoutSource.Text = _chkOverrideConnectionTimeout.Checked
                ? "Using per-job connection timeout override."
                : $"Inherited: app connection timeout ({connectionTimeoutSeconds} sec)";
        }

        private (string SourceLabel, int TimeoutSeconds) GetInheritedCommandTimeoutInfo()
        {
            var config = _presetManager.GetCurrentConfiguration();
            if (GetSelectedTargetType() == JobTargetType.CustomPreset)
            {
                return ("app default command timeout", config.Timeout);
            }

            var targetName = _cboTarget.SelectedItem?.ToString();
            if (GetSelectedTargetType() == JobTargetType.Preset &&
                !string.IsNullOrWhiteSpace(targetName) &&
                _presetManager.Get(targetName)?.Timeout is int presetTimeout)
            {
                return ($"preset '{targetName}' timeout", presetTimeout);
            }

            return ("app default command timeout", config.Timeout);
        }

        private static decimal ClampNumericValue(NumericUpDown control, int value)
        {
            var decimalValue = Convert.ToDecimal(value);
            if (decimalValue < control.Minimum)
            {
                return control.Minimum;
            }

            if (decimalValue > control.Maximum)
            {
                return control.Maximum;
            }

            return decimalValue;
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
            _rbCustomPreset.Checked = _editingJob.TargetType == JobTargetType.CustomPreset;
            _txtCustomPresetCommands.Text = _editingJob.CustomPresetCommands;
            UpdateTargetModeUi();

            // Select target in combo
            if (_editingJob.TargetType != JobTargetType.CustomPreset &&
                !string.IsNullOrEmpty(_editingJob.TargetName))
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
            LoadStoredCredentials();
            UpdateCredentialPanels();

            // Advanced - Folder execution
            _rbSequential.Checked = _editingJob.FolderExecutionMode == FolderExecutionMode.Sequential;
            _rbParallel.Checked = _editingJob.FolderExecutionMode == FolderExecutionMode.Parallel;
            _chkStopOnError.Checked = _editingJob.StopOnError;
            UpdateFolderExecutionState();

            // Advanced - Timeout overrides
            if (_editingJob.CommandTimeoutOverrideSeconds.HasValue)
            {
                _hasInitializedCommandTimeoutOverrideValue = true;
                _numCommandTimeoutOverride.Value = ClampNumericValue(
                    _numCommandTimeoutOverride,
                    _editingJob.CommandTimeoutOverrideSeconds.Value);
                _chkOverrideCommandTimeout.Checked = true;
            }
            else
            {
                _hasInitializedCommandTimeoutOverrideValue = false;
                _chkOverrideCommandTimeout.Checked = false;
            }

            if (_editingJob.ConnectionTimeoutOverrideSeconds.HasValue)
            {
                _hasInitializedConnectionTimeoutOverrideValue = true;
                _numConnectionTimeoutOverride.Value = ClampNumericValue(
                    _numConnectionTimeoutOverride,
                    _editingJob.ConnectionTimeoutOverrideSeconds.Value);
                _chkOverrideConnectionTimeout.Checked = true;
            }
            else
            {
                _hasInitializedConnectionTimeoutOverrideValue = false;
                _chkOverrideConnectionTimeout.Checked = false;
            }

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

            UpdateTimeoutOverrideState();
            RefreshCronPanelLayout();
        }

        #endregion

        #region Host Grid Events

        private void GridHosts_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            var hit = _gridHosts.HitTest(e.X, e.Y);
            _hostsRightClickedColumnIndex = hit.ColumnIndex;
            _hostsRightClickedRowIndex = hit.Type == DataGridViewHitTestType.Cell ||
                                         hit.Type == DataGridViewHitTestType.RowHeader
                ? hit.RowIndex
                : -1;

            if (hit.Type == DataGridViewHitTestType.Cell && hit.RowIndex >= 0 && hit.ColumnIndex >= 0)
            {
                _gridHosts.CurrentCell = _gridHosts[hit.ColumnIndex, hit.RowIndex];
            }
        }

        private void GridHosts_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (sender is not DataGridView grid)
            {
                return;
            }

            var rowIndexText = (e.RowIndex + 1).ToString();
            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
            var rowNumberColor = _darkMode ? DialogTheme.DarkSecondaryText : DialogTheme.LightSecondaryText;

            TextRenderer.DrawText(
                e.Graphics,
                rowIndexText,
                grid.Font,
                headerBounds,
                rowNumberColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void GridHosts_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics == null)
            {
                return;
            }

            var cell = _gridHosts.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (!cell.Selected)
            {
                return;
            }

            var selectionColor = _darkMode ? DialogTheme.GridDarkSelection : DialogTheme.GridLightSelection;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background);
            using var brush = new SolidBrush(selectionColor);
            e.Graphics.FillRectangle(brush, e.CellBounds);
            e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground | DataGridViewPaintParts.Border);
            e.Handled = true;
        }

        private void GridHosts_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1 && e.ColumnIndex >= 0)
            {
                _gridHosts.ClearSelection();
                foreach (DataGridViewRow row in _gridHosts.Rows)
                {
                    if (!row.IsNewRow)
                    {
                        row.Cells[e.ColumnIndex].Selected = true;
                    }
                }
            }
        }

        private void GridHosts_ColumnAdded(object? sender, DataGridViewColumnEventArgs e)
        {
            e.Column.SortMode = DataGridViewColumnSortMode.NotSortable;
            e.Column.Width = string.Equals(e.Column.Name, CsvManager.HostColumnName, StringComparison.OrdinalIgnoreCase)
                ? HostGridUtilities.DefaultHostColumnWidth
                : HostGridUtilities.DefaultAdditionalColumnWidth;
        }

        private void GridHosts_CellLeave(object? sender, DataGridViewCellEventArgs e)
        {
            _gridHosts.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void GridHosts_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            UpdateHostCount();
        }

        private void GridHosts_RowsAdded(object? sender, DataGridViewRowsAddedEventArgs e)
        {
            for (int i = 0; i < e.RowCount; i++)
            {
                _gridHosts.Rows[e.RowIndex + i].Height = HostGridUtilities.DefaultRowHeight;
            }

            UpdateHostCount();
        }

        private void GridHosts_RowsRemoved(object? sender, DataGridViewRowsRemovedEventArgs e)
        {
            UpdateHostCount();
        }

        private void GridHosts_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (_gridHosts.IsCurrentCellInEditMode || char.IsControl(e.KeyChar) || _gridHosts.CurrentCell == null)
            {
                return;
            }

            _gridHosts.BeginEdit(true);
            if (_gridHosts.EditingControl is TextBox editingTextBox)
            {
                editingTextBox.Text = e.KeyChar.ToString();
                editingTextBox.SelectionStart = editingTextBox.Text.Length;
            }

            e.Handled = true;
        }

        private void GridHosts_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                _gridHosts.SelectAll();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                Clipboard.SetText(HostGridUtilities.BuildClipboardText(_gridHosts));
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                if (Clipboard.ContainsText())
                {
                    HostGridUtilities.PasteClipboardText(_gridHosts, Clipboard.GetText());
                    UpdateHostCount();
                }

                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
            {
                HostGridUtilities.ClearSelectedCells(_gridHosts);
                _gridHosts.Refresh();
                UpdateHostCount();
                e.Handled = true;
            }
        }

        private void GridHosts_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                _gridHosts.BeginEdit(true);
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
                var dataTable = _csvManager.LoadFromFile(ofd.FileName);
                var snapshot = HostGridUtilities.BuildSnapshot(dataTable);
                if (!snapshot.Columns.Contains(CsvManager.HostColumnName, StringComparer.OrdinalIgnoreCase))
                {
                    DialogTheme.Show("CSV must contain a 'Host_IP' column.", "Import Error");
                    return;
                }

                ApplyHostGridSnapshot(snapshot.Columns, snapshot.Rows);
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

            ApplyHostGridSnapshot(columns, rows);
        }

        private void BtnAddRow_Click(object? sender, EventArgs e)
        {
            _gridHosts.Rows.Add();
            UpdateHostCount();
        }

        private void BtnRemoveSelected_Click(object? sender, EventArgs e)
        {
            var selectedRowIndices = _gridHosts.SelectedCells
                .Cast<DataGridViewCell>()
                .Select(cell => cell.RowIndex)
                .Concat(_gridHosts.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index))
                .Where(index => index >= 0 && index < _gridHosts.Rows.Count && !_gridHosts.Rows[index].IsNewRow)
                .Distinct()
                .OrderByDescending(index => index)
                .ToList();

            if (selectedRowIndices.Count == 0 &&
                _gridHosts.CurrentCell != null &&
                !_gridHosts.Rows[_gridHosts.CurrentCell.RowIndex].IsNewRow)
            {
                selectedRowIndices.Add(_gridHosts.CurrentCell.RowIndex);
            }

            foreach (var rowIndex in selectedRowIndices)
            {
                _gridHosts.Rows.RemoveAt(rowIndex);
            }

            UpdateHostCount();
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

        private bool TryPersistStoredCredentials(string username, string passwordText)
        {
            if (_credentialProvider?.IsAvailable != true)
            {
                DialogTheme.Show(
                    "Windows Credential Manager is not available, so stored job credentials cannot be saved.",
                    "Stored Credentials");
                return false;
            }

            var target = CredentialTargets.JobPasswordTarget(_editingJob.Id);
            string passwordToStore = passwordText;

            if (string.IsNullOrEmpty(passwordToStore))
            {
                if (!_hasStoredPassword ||
                    !_credentialProvider.TryGetPassword(target, out _, out passwordToStore))
                {
                    DialogTheme.Show(
                        "Password is required the first time you save stored credentials for this job.",
                        "Stored Credentials");
                    return false;
                }
            }

            if (!_credentialProvider.SavePassword(target, username, passwordToStore, "Scheduler job credential"))
            {
                DialogTheme.Show(
                    "Failed to save the job password to Windows Credential Manager.",
                    "Stored Credentials");
                return false;
            }

            _hasStoredPassword = true;
            _txtPassword.Clear();
            UpdateStoredCredentialNote();
            return true;
        }

        private void ValidateAndSave()
        {
            // Extract current form values
            var name = _txtName.Text;
            var targetType = GetSelectedTargetType();
            var targetName = targetType == JobTargetType.CustomPreset
                ? null
                : _cboTarget.SelectedItem?.ToString();
            var customPresetCommands = _txtCustomPresetCommands.Text;
            var scheduleType = GetSelectedScheduleType();
            var cronExpression = scheduleType == ScheduleType.Recurring
                ? _cronBuilder.CronExpression : null;
            var oneTimeUtc = scheduleType == ScheduleType.OneTime
                ? _dtpOneTime.Value.ToUniversalTime() : (DateTime?)null;
            var hosts = ExtractHostsFromGrid();
            var hostColumns = ExtractHostColumnsFromGrid();
            var credentialMode = GetSelectedCredentialMode();
            var storedUsername = credentialMode == CredentialMode.Stored
                ? _txtUsername.Text : null;
            var storedPassword = credentialMode == CredentialMode.Stored
                ? _txtPassword.Text : string.Empty;
            var commandTimeoutOverrideSeconds = _chkOverrideCommandTimeout.Checked
                ? (int)_numCommandTimeoutOverride.Value
                : (int?)null;
            var connectionTimeoutOverrideSeconds = _chkOverrideConnectionTimeout.Checked
                ? (int)_numConnectionTimeoutOverride.Value
                : (int?)null;

            // Delegate ALL validation to JobEditorValidator
            var error = JobEditorValidator.ValidateAll(
                name, targetName, scheduleType, cronExpression,
                oneTimeUtc, hosts, hostColumns, credentialMode, storedUsername, targetType, customPresetCommands,
                commandTimeoutOverrideSeconds, connectionTimeoutOverrideSeconds);

            if (error != null)
            {
                DialogTheme.Show(error, "Validation Error");
                return;
            }

            if (credentialMode == CredentialMode.Stored &&
                !TryPersistStoredCredentials(storedUsername!.Trim(), storedPassword))
            {
                return;
            }

            // Populate Result from all controls
            _editingJob.Name = name!.Trim();
            _editingJob.TargetType = targetType;
            _editingJob.TargetName = targetType == JobTargetType.CustomPreset
                ? string.Empty
                : targetName!;
            _editingJob.CustomPresetCommands = targetType == JobTargetType.CustomPreset
                ? customPresetCommands
                : string.Empty;

            // Compute TargetContentHash from preset content
            if (_editingJob.TargetType == JobTargetType.Preset &&
                _presetManager.Presets.TryGetValue(targetName!, out var presetInfo))
            {
                _editingJob.TargetContentHash = ContentHasher.ComputeHash(presetInfo.Commands);
                _editingJob.FolderPresetHashes = null;
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
            else
            {
                _editingJob.TargetContentHash = string.Empty;
                _editingJob.FolderPresetHashes = null;
            }

            // Schedule
            _editingJob.ScheduleType = scheduleType;
            _editingJob.CronExpression = cronExpression;
            _editingJob.OneTimeScheduleUtc = oneTimeUtc;

            // Hosts
            _editingJob.Hosts = hosts;
            _editingJob.HostColumns = hostColumns;

            // Credentials
            _editingJob.CredentialMode = credentialMode;

            // Advanced tab
            _editingJob.FolderExecutionMode = _rbParallel.Checked
                ? FolderExecutionMode.Parallel
                : FolderExecutionMode.Sequential;
            _editingJob.StopOnError = _chkStopOnError.Checked;
            _editingJob.CommandTimeoutOverrideSeconds = commandTimeoutOverrideSeconds;
            _editingJob.ConnectionTimeoutOverrideSeconds = connectionTimeoutOverrideSeconds;
            _editingJob.MaxHistoryRuns = _chkOverrideMaxRuns.Checked
                ? (int)_numMaxRuns.Value : null;
            _editingJob.HistoryRetentionDays = _chkOverrideRetention.Checked
                ? (int)_numRetentionDays.Value : null;

            // Metadata
            _editingJob.ModifiedUtc = DateTime.UtcNow;
            _editingJob.HasDriftWarning = false; // Legacy compatibility: new saves do not carry an active drift flag.

            Result = _editingJob;
            DialogResult = DialogResult.OK;
            Close();
        }

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cronBuilder.Dispose();
                _customPresetValidationService.Dispose();
                _txtCustomPresetCommands.Dispose();
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
