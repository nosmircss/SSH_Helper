using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Vault;
using SSH_Helper.UI;

namespace SSH_Helper
{
    /// <summary>
    /// Settings dialog for application preferences.
    /// </summary>
    internal sealed class SettingsDialog : Form
    {
        private readonly ConfigurationService _configService;
        private readonly PresetManager? _presetManager;
        private readonly IBrowserCallbackWebViewProfileManager _browserCallbackProfileManager;
        private readonly ISettingsDialogPromptService _promptService;
        private readonly ICredentialProvider? _credentialProvider;

        private readonly BorderlessTabControl _tabControl;

        // General tab controls
        private readonly CheckBox _chkRememberState;
        private readonly NumericUpDown _numMaxHistory;
        private readonly NumericUpDown _numDefaultTimeout;
        private readonly NumericUpDown _numConnectionTimeout;
        private readonly CheckBox _chkDarkMode;
        private readonly CheckBox _chkAutoResizeHostColumns;
        private readonly CheckBox _chkEnableSshConfig;
        private readonly CheckBox _chkUseConnectionPooling;
        private readonly CheckBox _chkUseCredentialManager;
        private readonly CheckBox _chkPreferSshAgent;

        // Updates tab controls
        private readonly CheckBox _chkCheckForUpdatesOnStartup;
        private readonly CheckBox _chkEnableUpdateLog;

        // Command Editor tab controls
        private readonly CheckBox _chkEnableSyntaxHighlighting;
        private readonly CheckBox _chkEnableAutocomplete;
        private readonly CheckBox _chkAutocompleteShowOnTyping;
        private readonly CheckBox _chkEnableInlineValidation;
        private readonly NumericUpDown _numValidationDebounceMs;
        private readonly CheckBox _chkShowInlineWarnings;
        private readonly CheckBox _chkEnableDiagnosticTooltips;
        private readonly CheckBox _chkEnableVariableInspectorTooltips;
        private readonly CheckBox _chkEnableYamlHygieneWarnings;
        private readonly CheckBox _chkUseSpacesForTab;
        private readonly NumericUpDown _numIndentSize;
        private readonly CheckBox _chkEnableSmartEnter;
        private readonly CheckBox _chkPreserveBlankLineBetweenSteps;
        private readonly CheckBox _chkEnableCurrentLineHighlight;
        private readonly CheckBox _chkEnableIndentGuides;
        private readonly CheckBox _chkShowWhitespace;
        private readonly CheckBox _chkEnableLongLineGuide;
        private readonly NumericUpDown _numLongLineColumn;
        private readonly CheckBox _chkEnableCodeFolding;
        private readonly CheckBox _chkEnableBraceMatching;

        // Appearance tab controls - Font Families
        private ComboBox _cboUIFont = null!;
        private ComboBox _cboCodeFont = null!;

        // Appearance tab controls - Font Sizes (existing)
        private NumericUpDown _numSectionTitleSize = null!;
        private NumericUpDown _numTreeViewSize = null!;
        private NumericUpDown _numEmptyLabelSize = null!;
        private NumericUpDown _numExecuteButtonSize = null!;
        private NumericUpDown _numCodeEditorSize = null!;
        private NumericUpDown _numOutputAreaSize = null!;

        // Appearance tab controls - Font Sizes (new)
        private NumericUpDown _numTabFontSize = null!;
        private NumericUpDown _numButtonFontSize = null!;
        private NumericUpDown _numHostListFontSize = null!;
        private NumericUpDown _numMenuFontSize = null!;
        private NumericUpDown _numStatusBarFontSize = null!;
        private NumericUpDown _numDialogFontSize = null!;
        private NumericUpDown _numScriptPromptFontSize = null!;

        // Appearance tab controls - Global Scale
        private TrackBar _trkGlobalScale = null!;
        private Label _lblGlobalScaleValue = null!;

        // Appearance tab controls - Layout
        private CheckBox _chkCodeEditorWordWrap = null!;
        private CheckBox _chkOutputAreaWordWrap = null!;
        private NumericUpDown _numTreeViewRowHeight = null!;
        private NumericUpDown _numHostListRowHeight = null!;

        // Appearance tab controls - Accent Color
        private Panel _pnlAccentColor = null!;
        private Button _btnChooseAccentColor = null!;
        private CheckBox _chkUseCustomAccent = null!;

        // Appearance tab controls - Preview
        private Panel _pnlPreview = null!;
        private Label _lblPreviewTitle = null!;
        private TreeView _trvPreview = null!;
        private TextBox _txtPreviewCode = null!;
        private Button _btnPreviewButton = null!;

        // Vault tab controls
        private CheckBox _chkVaultEnabled = null!;
        private ListBox _lstVaultProfiles = null!;
        private Button _btnVaultAdd = null!;
        private Button _btnVaultRemove = null!;
        private TextBox _txtVaultProfileName = null!;
        private TextBox _txtVaultAddress = null!;
        private TextBox _txtVaultNamespace = null!;
        private TextBox _txtVaultMountPath = null!;
        private ComboBox _cmbVaultKvVersion = null!;
        private ComboBox _cmbVaultAuthMethod = null!;
        private TextBox _txtVaultToken = null!;
        private TextBox _txtVaultAppRoleId = null!;
        private TextBox _txtVaultAppRoleSecret = null!;
        private TextBox _txtVaultLdapUsername = null!;
        private TextBox _txtVaultLdapPassword = null!;
        private TextBox _txtVaultUserpassUsername = null!;
        private TextBox _txtVaultUserpassPassword = null!;
        private TextBox _txtVaultOidcAuthMountPath = null!;
        private TextBox _txtVaultOidcRole = null!;
        private TextBox _txtVaultOidcCallbackHost = null!;
        private NumericUpDown _numVaultOidcCallbackPort = null!;
        private TextBox _txtVaultOidcCallbackPath = null!;
        private NumericUpDown _numVaultOidcTimeoutSeconds = null!;
        private TextBox _txtVaultCaCertPath = null!;
        private Button _btnVaultBrowseCaCert = null!;
        private CheckBox _chkVaultSkipTls = null!;
        private NumericUpDown _numVaultCacheTtl = null!;
        private CheckBox _chkVaultDefault = null!;
        private Button _btnVaultTestConnection = null!;
        private Panel _pnlVaultAuthToken = null!;
        private Panel _pnlVaultAuthAppRole = null!;
        private Panel _pnlVaultAuthLdap = null!;
        private Panel _pnlVaultAuthUserpass = null!;
        private Panel _pnlVaultAuthOidc = null!;
        private readonly List<VaultProfileConfig> _vaultProfiles = new();
        private bool _suppressVaultProfileSelection;
        private bool _suppressVaultDefaultToggle;
        private int _activeVaultProfileIndex = -1;
        private string? _vaultDefaultProfileName;

        // Notifications tab controls
        private CheckBox _chkNotificationsEnabled = null!;
        private ListBox _lstNotificationProfiles = null!;
        private Button _btnNotificationAdd = null!;
        private Button _btnNotificationRemove = null!;
        private TextBox _txtNotificationProfileName = null!;
        private ComboBox _cmbNotificationKind = null!;
        private TextBox _txtNotificationDefaultTitle = null!;
        private Panel _pnlNotificationWebhook = null!;
        private TextBox _txtNotificationWebhookUrl = null!;
        private Panel _pnlNotificationSmtp = null!;
        private TextBox _txtNotificationSmtpHost = null!;
        private NumericUpDown _numNotificationSmtpPort = null!;
        private TextBox _txtNotificationSmtpFrom = null!;
        private TextBox _txtNotificationSmtpTo = null!;
        private TextBox _txtNotificationSmtpUsername = null!;
        private TextBox _txtNotificationSmtpPassword = null!;
        private CheckBox _chkNotificationSmtpUseStartTls = null!;
        private CheckBox _chkNotificationDefault = null!;
        private readonly List<NotificationProfile> _notificationProfiles = new();
        private bool _suppressNotificationProfileSelection;
        private bool _suppressNotificationDefaultToggle;
        private int _activeNotificationProfileIndex = -1;
        private string? _notificationDefaultProfileName;

        // Reset buttons
        private Button _btnResetDefaults = null!;
        private Button _btnResetPresetTimeouts = null!;
        private Button _btnClearEmbeddedBrowserData = null!;

        private readonly Button _btnSave;
        private readonly Button _btnCancel;
        private readonly List<FlowLayoutPanel> _scrollableFlowPanels = new();

        private Color _customAccentColor = Color.FromArgb(0, 120, 215);
        private List<Font> _previewFonts = new();
        public bool PresetTimeoutsWereCleared { get; private set; }

        public SettingsDialog(ConfigurationService configService, PresetManager? presetManager = null, bool darkMode = false, ICredentialProvider? credentialProvider = null)
            : this(
                configService,
                presetManager,
                darkMode,
                BrowserCallbackWebViewProfileManager.Shared,
                new SettingsDialogPromptService(),
                credentialProvider)
        {
        }

        internal SettingsDialog(
            ConfigurationService configService,
            PresetManager? presetManager,
            bool darkMode,
            IBrowserCallbackWebViewProfileManager browserCallbackProfileManager,
            ISettingsDialogPromptService promptService,
            ICredentialProvider? credentialProvider = null)
        {
            _configService = configService;
            _presetManager = presetManager;
            _browserCallbackProfileManager = browserCallbackProfileManager ?? throw new ArgumentNullException(nameof(browserCallbackProfileManager));
            _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
            _credentialProvider = credentialProvider;

            // Enable DPI scaling - must be set before any Size/Location values
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;

            Text = "Settings";
            Size = new Size(544, 620);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _tabControl = new BorderlessTabControl
            {
                Location = new Point(12, 12),
                Size = new Size(504, 520),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            // === General Tab ===
            var tabGeneral = CreateGeneralTab();
            _tabControl.TabPages.Add(tabGeneral);

            // === Updates Tab ===
            var tabUpdates = CreateUpdatesTab();
            _tabControl.TabPages.Add(tabUpdates);

            // === Command Editor Tab ===
            var tabCommandEditor = CreateCommandEditorTab();
            _tabControl.TabPages.Add(tabCommandEditor);

            // === Appearance Tab (with scrollable panel) ===
            var tabAppearance = CreateAppearanceTab();
            _tabControl.TabPages.Add(tabAppearance);

            // === Vault Tab ===
            var tabVault = CreateVaultTab();
            _tabControl.TabPages.Add(tabVault);

            // === Notifications Tab ===
            var tabNotifications = CreateNotificationsTab();
            _tabControl.TabPages.Add(tabNotifications);

            // Buttons
            _btnSave = new Button
            {
                Text = "Save",
                Size = new Size(80, 28),
                Location = new Point(345, 545),
                DialogResult = DialogResult.OK
            };
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 28),
                Location = new Point(431, 545),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(_tabControl);
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            // Initialize controls â€” use recursive find since controls are nested in layout panels
            _chkRememberState = FindControl<CheckBox>(tabGeneral, "chkRememberState");
            _numMaxHistory = FindControl<NumericUpDown>(tabGeneral, "numMaxHistory");
            _numDefaultTimeout = FindControl<NumericUpDown>(tabGeneral, "numDefaultTimeout");
            _numConnectionTimeout = FindControl<NumericUpDown>(tabGeneral, "numConnectionTimeout");
            _chkDarkMode = FindControl<CheckBox>(tabGeneral, "chkDarkMode");
            _chkAutoResizeHostColumns = FindControl<CheckBox>(tabGeneral, "chkAutoResizeHostColumns");
            _chkEnableSshConfig = FindControl<CheckBox>(tabGeneral, "chkEnableSshConfig");
            _chkUseConnectionPooling = FindControl<CheckBox>(tabGeneral, "chkUseConnectionPooling");
            _chkUseCredentialManager = FindControl<CheckBox>(tabGeneral, "chkUseCredentialManager");
            _chkPreferSshAgent = FindControl<CheckBox>(tabGeneral, "chkPreferSshAgent");

            _chkCheckForUpdatesOnStartup = FindControl<CheckBox>(tabUpdates, "chkCheckForUpdatesOnStartup");
            _chkEnableUpdateLog = FindControl<CheckBox>(tabUpdates, "chkEnableUpdateLog");
            _chkEnableSyntaxHighlighting = FindControl<CheckBox>(tabCommandEditor, "chkEnableSyntaxHighlighting");
            _chkEnableAutocomplete = FindControl<CheckBox>(tabCommandEditor, "chkEnableAutocomplete");
            _chkAutocompleteShowOnTyping = FindControl<CheckBox>(tabCommandEditor, "chkAutocompleteShowOnTyping");
            _chkEnableInlineValidation = FindControl<CheckBox>(tabCommandEditor, "chkEnableInlineValidation");
            _numValidationDebounceMs = FindControl<NumericUpDown>(tabCommandEditor, "numValidationDebounceMs");
            _chkShowInlineWarnings = FindControl<CheckBox>(tabCommandEditor, "chkShowInlineWarnings");
            _chkEnableDiagnosticTooltips = FindControl<CheckBox>(tabCommandEditor, "chkEnableDiagnosticTooltips");
            _chkEnableVariableInspectorTooltips = FindControl<CheckBox>(tabCommandEditor, "chkEnableVariableInspectorTooltips");
            _chkEnableYamlHygieneWarnings = FindControl<CheckBox>(tabCommandEditor, "chkEnableYamlHygieneWarnings");
            _chkUseSpacesForTab = FindControl<CheckBox>(tabCommandEditor, "chkUseSpacesForTab");
            _numIndentSize = FindControl<NumericUpDown>(tabCommandEditor, "numIndentSize");
            _chkEnableSmartEnter = FindControl<CheckBox>(tabCommandEditor, "chkEnableSmartEnter");
            _chkPreserveBlankLineBetweenSteps = FindControl<CheckBox>(tabCommandEditor, "chkPreserveBlankLineBetweenSteps");
            _chkEnableCurrentLineHighlight = FindControl<CheckBox>(tabCommandEditor, "chkEnableCurrentLineHighlight");
            _chkEnableIndentGuides = FindControl<CheckBox>(tabCommandEditor, "chkEnableIndentGuides");
            _chkShowWhitespace = FindControl<CheckBox>(tabCommandEditor, "chkShowWhitespace");
            _chkEnableLongLineGuide = FindControl<CheckBox>(tabCommandEditor, "chkEnableLongLineGuide");
            _numLongLineColumn = FindControl<NumericUpDown>(tabCommandEditor, "numLongLineColumn");
            _chkEnableCodeFolding = FindControl<CheckBox>(tabCommandEditor, "chkEnableCodeFolding");
            _chkEnableBraceMatching = FindControl<CheckBox>(tabCommandEditor, "chkEnableBraceMatching");

            LoadSettings();
            UpdatePreview();
            ApplyDialogTheme(darkMode);

            FontChanged += (_, _) => RefreshScrollableFlowExtents();
            Shown += (_, _) => RefreshScrollableFlowExtents();
            _tabControl.SelectedIndexChanged += (_, _) => BeginInvoke(new Action(RefreshScrollableFlowExtents));
            RefreshScrollableFlowExtents();
        }

        private void ApplyDialogTheme(bool darkMode)
        {
            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.StyleButton(_btnSave, darkMode, isPrimary: true);
            DialogTheme.StyleButton(_btnCancel, darkMode);
            DialogTheme.StyleButton(_btnResetDefaults, darkMode);
            DialogTheme.StyleButton(_btnResetPresetTimeouts, darkMode);
            DialogTheme.StyleButton(_btnClearEmbeddedBrowserData, darkMode);
            DialogTheme.StyleButton(_btnChooseAccentColor, darkMode);
            DialogTheme.StyleButton(_btnVaultAdd, darkMode);
            DialogTheme.StyleButton(_btnVaultRemove, darkMode);
            DialogTheme.StyleButton(_btnVaultBrowseCaCert, darkMode);
            DialogTheme.StyleButton(_btnVaultTestConnection, darkMode);
            DialogTheme.StyleButton(_btnNotificationAdd, darkMode);
            DialogTheme.StyleButton(_btnNotificationRemove, darkMode);
            DialogTheme.SetDarkTitleBar(this, darkMode);
            DialogTheme.StyleTabControl(_tabControl, darkMode);

            if (darkMode)
            {
                Load += (_, _) => DialogTheme.ApplyNativeTheme(this, true);

                // Re-apply native theme when switching tabs. Controls on
                // non-visible tab pages can lose their native dark rendering
                // until they are actually shown.
                _tabControl.Selected += (_, e) =>
                {
                    if (e.TabPage != null)
                        BeginInvoke(() => DialogTheme.ApplyNativeTheme(e.TabPage, true));
                };
            }

            // The preview panel should reflect theme
            if (darkMode)
            {
                _pnlPreview.BackColor = DialogTheme.DarkSurface1;
                _pnlPreview.ForeColor = DialogTheme.DarkText;
            }
            else
            {
                _pnlPreview.BackColor = Color.White;
                _pnlPreview.ForeColor = DialogTheme.LightText;
            }
        }

        private TabPage CreateGeneralTab()
        {
            var tabGeneral = new TabPage("General") { AutoScroll = true };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12, 12, 12, 12),
            };
            _scrollableFlowPanels.Add(flow);

            var sectionFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            var noteFont = new Font("Segoe UI", 8f);

            // Helper: section header with top margin
            Label SectionHeader(string text, int topMargin = 8) => new()
            {
                Text = text, Font = sectionFont, AutoSize = true,
                Margin = new Padding(0, topMargin, 0, 4)
            };

            // Helper: label+spinner row using a 2-column table
            TableLayoutPanel LabelSpinnerRow(string labelText, string numName, decimal value, decimal min, decimal max)
            {
                var row = new TableLayoutPanel
                {
                    AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 2, 0, 2),
                };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
                row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var lbl = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) };
                var num = new NumericUpDown
                {
                    Name = numName, Size = new Size(70, 23),
                    Minimum = min, Maximum = max, Value = value,
                    TextAlign = HorizontalAlignment.Right
                };
                row.Controls.Add(lbl, 0, 0);
                row.Controls.Add(num, 1, 0);
                return row;
            }

            // Helper: indented note label
            Label NoteLabel(string text) => new()
            {
                Text = text, AutoSize = true, ForeColor = Color.Gray, Font = noteFont,
                Margin = new Padding(17, 0, 0, 2)
            };

            // === Application State ===
            flow.Controls.Add(SectionHeader("Application State", 0));
            flow.Controls.Add(new CheckBox { Name = "chkRememberState", Text = "Remember state on exit (hosts, preset, history)", AutoSize = true });
            flow.Controls.Add(LabelSpinnerRow("Maximum history entries to keep:", "numMaxHistory", 30, 1, 500));

            // === Default Values ===
            flow.Controls.Add(SectionHeader("Default Values"));
            flow.Controls.Add(LabelSpinnerRow("Default command timeout (seconds):", "numDefaultTimeout", 10, 1, 300));
            flow.Controls.Add(LabelSpinnerRow("Connection timeout (seconds):", "numConnectionTimeout", 30, 5, 120));
            _btnResetPresetTimeouts = new Button
            {
                Text = "Reset All Preset Timeouts to Default",
                AutoSize = true,
                Margin = new Padding(15, 8, 0, 0)
            };
            _btnResetPresetTimeouts.Click += BtnResetPresetTimeouts_Click;
            flow.Controls.Add(_btnResetPresetTimeouts);

            // === Theme ===
            flow.Controls.Add(SectionHeader("Theme"));
            flow.Controls.Add(new CheckBox { Name = "chkDarkMode", Text = "Dark mode (output window is always dark)", AutoSize = true });

            // === Host Grid ===
            flow.Controls.Add(SectionHeader("Host Grid"));
            flow.Controls.Add(new CheckBox { Name = "chkAutoResizeHostColumns", Text = "Auto-resize columns to fit content", AutoSize = true });

            // === SSH Config ===
            flow.Controls.Add(SectionHeader("SSH Config"));
            flow.Controls.Add(new CheckBox { Name = "chkEnableSshConfig", Text = "Use SSH config file (~/.ssh/config)", AutoSize = true });
            var sshConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "config");
            flow.Controls.Add(NoteLabel($"Path: {sshConfigPath}"));

            // === Connection Pooling ===
            flow.Controls.Add(SectionHeader("Connection Pooling"));
            flow.Controls.Add(new CheckBox { Name = "chkUseConnectionPooling", Text = "Reuse SSH connections across executions", AutoSize = true });
            flow.Controls.Add(NoteLabel("May improve performance for repeated runs on the same hosts."));

            // === Credentials ===
            flow.Controls.Add(SectionHeader("Credentials"));
            flow.Controls.Add(new CheckBox { Name = "chkUseCredentialManager", Text = "Store main form password in Windows Credential Manager", AutoSize = true });
            flow.Controls.Add(new CheckBox { Name = "chkPreferSshAgent", Text = "Prefer SSH agent when available", AutoSize = true });

            // === Browser Callback ===
            flow.Controls.Add(SectionHeader("Browser Callback"));
            _btnClearEmbeddedBrowserData = new Button
            {
                Text = "Clear Embedded Browser Data...",
                AutoSize = true,
                Margin = new Padding(15, 8, 0, 0)
            };
            _btnClearEmbeddedBrowserData.Click += BtnClearEmbeddedBrowserData_Click;
            flow.Controls.Add(_btnClearEmbeddedBrowserData);
            flow.Controls.Add(NoteLabel("Resets SSH Helper's embedded-browser cookies, cache, local storage, IndexedDB, and related site data."));

            tabGeneral.Controls.Add(flow);
            return tabGeneral;
        }

        private TabPage CreateVaultTab()
        {
            var tabVault = new TabPage("Vault");
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 160,
                FixedPanel = FixedPanel.Panel1
            };

            // Left panel: enable checkbox + profile list + add/remove
            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            _chkVaultEnabled = new CheckBox
            {
                Name = "chkVaultEnabled",
                Text = "Enable Vault",
                AutoSize = true,
                Dock = DockStyle.Top
            };
            _chkVaultEnabled.CheckedChanged += (_, _) => UpdateVaultControlStates();

            _lstVaultProfiles = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            _lstVaultProfiles.SelectedIndexChanged += LstVaultProfiles_SelectedIndexChanged;

            var profileButtonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 0)
            };
            _btnVaultAdd = new Button { Text = "Add", Width = 70, Height = 26 };
            _btnVaultAdd.Click += BtnVaultAdd_Click;
            _btnVaultRemove = new Button { Text = "Remove", Width = 70, Height = 26 };
            _btnVaultRemove.Click += BtnVaultRemove_Click;
            profileButtonPanel.Controls.Add(_btnVaultAdd);
            profileButtonPanel.Controls.Add(_btnVaultRemove);

            leftPanel.Controls.Add(_lstVaultProfiles);
            leftPanel.Controls.Add(profileButtonPanel);
            leftPanel.Controls.Add(_chkVaultEnabled);

            // Right panel: profile detail fields
            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(8, 8, 8, 8)
            };
            _scrollableFlowPanels.Add(rightFlow);

            var sectionFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            const int vaultLabelColumnWidth = 120;
            const int vaultInputMinWidth = 205;
            const int vaultPathInputMinWidth = 145;

            TableLayoutPanel LabeledTextBox(string labelText, string textBoxName, out TextBox textBox, bool isPassword = false)
            {
                var row = new TableLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 2,
                    RowCount = 1,
                    Margin = new Padding(0, 2, 0, 2),
                    Width = 430
                };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, vaultLabelColumnWidth));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var lbl = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) };
                textBox = new TextBox
                {
                    Name = textBoxName,
                    Dock = DockStyle.Fill,
                    MinimumSize = new Size(vaultInputMinWidth, 0),
                    UseSystemPasswordChar = isPassword
                };
                row.Controls.Add(lbl, 0, 0);
                row.Controls.Add(textBox, 1, 0);
                return row;
            }

            // Connection
            rightFlow.Controls.Add(new Label { Text = "Connection", Font = sectionFont, AutoSize = true, Margin = new Padding(0, 0, 0, 4) });
            rightFlow.Controls.Add(LabeledTextBox("Profile Name:", "txtVaultProfileName", out _txtVaultProfileName));
            rightFlow.Controls.Add(LabeledTextBox("Address:", "txtVaultAddress", out _txtVaultAddress));
            rightFlow.Controls.Add(LabeledTextBox("Namespace:", "txtVaultNamespace", out _txtVaultNamespace));
            rightFlow.Controls.Add(LabeledTextBox("Mount Path:", "txtVaultMountPath", out _txtVaultMountPath));

            // KV Version combo
            var kvVersionRow = new TableLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 2, 0, 2), Width = 430
            };
            kvVersionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, vaultLabelColumnWidth));
            kvVersionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            kvVersionRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            kvVersionRow.Controls.Add(new Label { Text = "KV Version:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) }, 0, 0);
            _cmbVaultKvVersion = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, MinimumSize = new Size(vaultInputMinWidth, 0) };
            _cmbVaultKvVersion.Items.AddRange(new object[] { "Auto-detect", "v1", "v2" });
            _cmbVaultKvVersion.SelectedIndex = 0;
            kvVersionRow.Controls.Add(_cmbVaultKvVersion, 1, 0);
            rightFlow.Controls.Add(kvVersionRow);

            // Authentication
            rightFlow.Controls.Add(new Label { Text = "Authentication", Font = sectionFont, AutoSize = true, Margin = new Padding(0, 10, 0, 4) });

            var authMethodRow = new TableLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 2, 0, 2), Width = 430
            };
            authMethodRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, vaultLabelColumnWidth));
            authMethodRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            authMethodRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            authMethodRow.Controls.Add(new Label { Text = "Auth Method:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) }, 0, 0);
            _cmbVaultAuthMethod = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, MinimumSize = new Size(vaultInputMinWidth, 0) };
            _cmbVaultAuthMethod.Items.AddRange(new object[] { "Token", "AppRole", "LDAP", "Userpass", "OIDC" });
            _cmbVaultAuthMethod.SelectedIndex = 0;
            _cmbVaultAuthMethod.SelectedIndexChanged += (_, _) => UpdateVaultAuthFieldVisibility();
            authMethodRow.Controls.Add(_cmbVaultAuthMethod, 1, 0);
            rightFlow.Controls.Add(authMethodRow);

            // Token auth panel
            _pnlVaultAuthToken = new Panel { AutoSize = true, Width = 430 };
            var tokenLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
            tokenLayout.Controls.Add(LabeledTextBox("Token:", "txtVaultToken", out _txtVaultToken, isPassword: true));
            _pnlVaultAuthToken.Controls.Add(tokenLayout);
            rightFlow.Controls.Add(_pnlVaultAuthToken);

            // AppRole auth panel
            _pnlVaultAuthAppRole = new Panel { AutoSize = true, Width = 430, Visible = false };
            var appRoleLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
            appRoleLayout.Controls.Add(LabeledTextBox("Role ID:", "txtVaultAppRoleId", out _txtVaultAppRoleId));
            appRoleLayout.Controls.Add(LabeledTextBox("Secret ID:", "txtVaultAppRoleSecret", out _txtVaultAppRoleSecret, isPassword: true));
            _pnlVaultAuthAppRole.Controls.Add(appRoleLayout);
            rightFlow.Controls.Add(_pnlVaultAuthAppRole);

            // LDAP auth panel
            _pnlVaultAuthLdap = new Panel { AutoSize = true, Width = 430, Visible = false };
            var ldapLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
            ldapLayout.Controls.Add(LabeledTextBox("Username:", "txtVaultLdapUsername", out _txtVaultLdapUsername));
            ldapLayout.Controls.Add(LabeledTextBox("Password:", "txtVaultLdapPassword", out _txtVaultLdapPassword, isPassword: true));
            _pnlVaultAuthLdap.Controls.Add(ldapLayout);
            rightFlow.Controls.Add(_pnlVaultAuthLdap);

            // Userpass auth panel
            _pnlVaultAuthUserpass = new Panel { AutoSize = true, Width = 430, Visible = false };
            var userpassLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
            userpassLayout.Controls.Add(LabeledTextBox("Username:", "txtVaultUserpassUsername", out _txtVaultUserpassUsername));
            userpassLayout.Controls.Add(LabeledTextBox("Password:", "txtVaultUserpassPassword", out _txtVaultUserpassPassword, isPassword: true));
            _pnlVaultAuthUserpass.Controls.Add(userpassLayout);
            rightFlow.Controls.Add(_pnlVaultAuthUserpass);

            // OIDC auth panel
            _pnlVaultAuthOidc = new Panel { AutoSize = true, Width = 430, Visible = false };
            var oidcLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
            oidcLayout.Controls.Add(LabeledTextBox("Auth Mount:", "txtVaultOidcAuthMountPath", out _txtVaultOidcAuthMountPath));
            oidcLayout.Controls.Add(LabeledTextBox("Role:", "txtVaultOidcRole", out _txtVaultOidcRole));
            oidcLayout.Controls.Add(LabeledTextBox("Callback Host:", "txtVaultOidcCallbackHost", out _txtVaultOidcCallbackHost));

            var oidcPortRow = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 2, 0, 2),
                Width = 430
            };
            oidcPortRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, vaultLabelColumnWidth));
            oidcPortRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            oidcPortRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            oidcPortRow.Controls.Add(new Label { Text = "Callback Port:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) }, 0, 0);
            _numVaultOidcCallbackPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 8250, Width = 100, TextAlign = HorizontalAlignment.Right };
            oidcPortRow.Controls.Add(_numVaultOidcCallbackPort, 1, 0);
            oidcLayout.Controls.Add(oidcPortRow);

            oidcLayout.Controls.Add(LabeledTextBox("Callback Path:", "txtVaultOidcCallbackPath", out _txtVaultOidcCallbackPath));

            var oidcTimeoutRow = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 2, 0, 2),
                Width = 430
            };
            oidcTimeoutRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, vaultLabelColumnWidth));
            oidcTimeoutRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            oidcTimeoutRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            oidcTimeoutRow.Controls.Add(new Label { Text = "Timeout (sec):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) }, 0, 0);
            _numVaultOidcTimeoutSeconds = new NumericUpDown { Minimum = 15, Maximum = 3600, Value = 180, Width = 100, TextAlign = HorizontalAlignment.Right };
            oidcTimeoutRow.Controls.Add(_numVaultOidcTimeoutSeconds, 1, 0);
            oidcLayout.Controls.Add(oidcTimeoutRow);

            _pnlVaultAuthOidc.Controls.Add(oidcLayout);
            rightFlow.Controls.Add(_pnlVaultAuthOidc);

            // TLS
            rightFlow.Controls.Add(new Label { Text = "TLS", Font = sectionFont, AutoSize = true, Margin = new Padding(0, 10, 0, 4) });

            // CA cert path with browse button
            var caCertRow = new TableLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 2, 0, 2), Width = 430
            };
            caCertRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, vaultLabelColumnWidth));
            caCertRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            caCertRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            caCertRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            caCertRow.Controls.Add(new Label { Text = "CA Certificate:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) }, 0, 0);
            _txtVaultCaCertPath = new TextBox { Dock = DockStyle.Fill, MinimumSize = new Size(vaultPathInputMinWidth, 0) };
            caCertRow.Controls.Add(_txtVaultCaCertPath, 1, 0);
            _btnVaultBrowseCaCert = new Button { Text = "Browse...", Width = 75, Height = 23 };
            _btnVaultBrowseCaCert.Click += BtnVaultBrowseCaCert_Click;
            caCertRow.Controls.Add(_btnVaultBrowseCaCert, 2, 0);
            rightFlow.Controls.Add(caCertRow);

            _chkVaultSkipTls = new CheckBox { Text = "Skip TLS verification (development only)", AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
            rightFlow.Controls.Add(_chkVaultSkipTls);

            // Cache
            rightFlow.Controls.Add(new Label { Text = "Cache", Font = sectionFont, AutoSize = true, Margin = new Padding(0, 10, 0, 4) });
            var cacheTtlRow = new TableLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 2, 0, 2), Width = 430
            };
            cacheTtlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            cacheTtlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            cacheTtlRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cacheTtlRow.Controls.Add(new Label { Text = "Cache TTL (sec):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) }, 0, 0);
            _numVaultCacheTtl = new NumericUpDown { Minimum = 0, Maximum = 86400, Value = 300, Width = 90, TextAlign = HorizontalAlignment.Right };
            cacheTtlRow.Controls.Add(_numVaultCacheTtl, 1, 0);
            rightFlow.Controls.Add(cacheTtlRow);

            // Default + Test
            _chkVaultDefault = new CheckBox { Text = "Set as default profile", AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
            _chkVaultDefault.CheckedChanged += ChkVaultDefault_CheckedChanged;
            rightFlow.Controls.Add(_chkVaultDefault);

            _btnVaultTestConnection = new Button { Text = "Test Connection", AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            _btnVaultTestConnection.Click += BtnVaultTestConnection_Click;
            rightFlow.Controls.Add(_btnVaultTestConnection);

            mainSplit.Panel1.Controls.Add(leftPanel);
            mainSplit.Panel2.Controls.Add(rightFlow);
            tabVault.Controls.Add(mainSplit);

            return tabVault;
        }

        private void UpdateVaultControlStates()
        {
            bool enabled = _chkVaultEnabled.Checked;
            _lstVaultProfiles.Enabled = enabled;
            _btnVaultAdd.Enabled = enabled;
            _btnVaultRemove.Enabled = enabled && _lstVaultProfiles.SelectedIndex >= 0;
            SetVaultDetailFieldsEnabled(enabled && _lstVaultProfiles.SelectedIndex >= 0);
        }

        private void SetVaultDetailFieldsEnabled(bool enabled)
        {
            _txtVaultProfileName.Enabled = enabled;
            _txtVaultAddress.Enabled = enabled;
            _txtVaultNamespace.Enabled = enabled;
            _txtVaultMountPath.Enabled = enabled;
            _cmbVaultKvVersion.Enabled = enabled;
            _cmbVaultAuthMethod.Enabled = enabled;
            _txtVaultToken.Enabled = enabled;
            _txtVaultAppRoleId.Enabled = enabled;
            _txtVaultAppRoleSecret.Enabled = enabled;
            _txtVaultLdapUsername.Enabled = enabled;
            _txtVaultLdapPassword.Enabled = enabled;
            _txtVaultUserpassUsername.Enabled = enabled;
            _txtVaultUserpassPassword.Enabled = enabled;
            _txtVaultOidcAuthMountPath.Enabled = enabled;
            _txtVaultOidcRole.Enabled = enabled;
            _txtVaultOidcCallbackHost.Enabled = enabled;
            _numVaultOidcCallbackPort.Enabled = enabled;
            _txtVaultOidcCallbackPath.Enabled = enabled;
            _numVaultOidcTimeoutSeconds.Enabled = enabled;
            _txtVaultCaCertPath.Enabled = enabled;
            _btnVaultBrowseCaCert.Enabled = enabled;
            _chkVaultSkipTls.Enabled = enabled;
            _numVaultCacheTtl.Enabled = enabled;
            _chkVaultDefault.Enabled = enabled;
            _btnVaultTestConnection.Enabled = enabled;
        }

        private void UpdateVaultAuthFieldVisibility()
        {
            var method = _cmbVaultAuthMethod.SelectedIndex;
            _pnlVaultAuthToken.Visible = method == 0;
            _pnlVaultAuthAppRole.Visible = method == 1;
            _pnlVaultAuthLdap.Visible = method == 2;
            _pnlVaultAuthUserpass.Visible = method == 3;
            _pnlVaultAuthOidc.Visible = method == 4;
        }

        private void LstVaultProfiles_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressVaultProfileSelection)
                return;

            PersistVaultProfileByIndex(_activeVaultProfileIndex);
            _activeVaultProfileIndex = _lstVaultProfiles.SelectedIndex;

            if (_lstVaultProfiles.SelectedIndex >= 0 && _lstVaultProfiles.SelectedIndex < _vaultProfiles.Count)
            {
                LoadVaultProfileDetails(_vaultProfiles[_lstVaultProfiles.SelectedIndex]);
            }

            UpdateVaultControlStates();
        }

        private void ChkVaultDefault_CheckedChanged(object? sender, EventArgs e)
        {
            if (_suppressVaultDefaultToggle || !_chkVaultDefault.Checked)
                return;

            var index = _activeVaultProfileIndex;
            if (index < 0 || index >= _vaultProfiles.Count)
                return;

            _vaultDefaultProfileName = _vaultProfiles[index].Name;
        }

        private void BtnVaultAdd_Click(object? sender, EventArgs e)
        {
            PersistCurrentVaultProfile();
            var name = $"profile-{_vaultProfiles.Count + 1}";
            var profile = new VaultProfileConfig { Name = name, MountPath = "secret" };
            _vaultProfiles.Add(profile);
            _suppressVaultProfileSelection = true;
            _lstVaultProfiles.Items.Add(name);
            _lstVaultProfiles.SelectedIndex = _lstVaultProfiles.Items.Count - 1;
            _suppressVaultProfileSelection = false;
            _activeVaultProfileIndex = _lstVaultProfiles.SelectedIndex;
            LoadVaultProfileDetails(profile);

            if (string.IsNullOrWhiteSpace(_vaultDefaultProfileName) && _vaultProfiles.Count == 1)
                _vaultDefaultProfileName = profile.Name;

            UpdateVaultControlStates();
        }

        private void BtnVaultRemove_Click(object? sender, EventArgs e)
        {
            var index = _lstVaultProfiles.SelectedIndex;
            if (index < 0 || index >= _vaultProfiles.Count)
                return;

            PersistCurrentVaultProfile();

            var profileName = _vaultProfiles[index].Name;
            var removedWasDefault = string.Equals(
                profileName,
                _vaultDefaultProfileName,
                StringComparison.OrdinalIgnoreCase);

            // Remove stored credentials for this profile
            if (_credentialProvider != null)
            {
                _credentialProvider.DeletePassword(CredentialTargets.VaultAuthTarget(profileName, "token"));
                _credentialProvider.DeletePassword(CredentialTargets.VaultAuthTarget(profileName, "approle_secret"));
                _credentialProvider.DeletePassword(CredentialTargets.VaultAuthTarget(profileName, "ldap_password"));
                _credentialProvider.DeletePassword(CredentialTargets.VaultAuthTarget(profileName, "userpass_password"));
            }

            _vaultProfiles.RemoveAt(index);
            _suppressVaultProfileSelection = true;
            _lstVaultProfiles.Items.RemoveAt(index);

            if (_lstVaultProfiles.Items.Count > 0)
            {
                _lstVaultProfiles.SelectedIndex = Math.Min(index, _lstVaultProfiles.Items.Count - 1);
                _suppressVaultProfileSelection = false;
                _activeVaultProfileIndex = _lstVaultProfiles.SelectedIndex;
                LoadVaultProfileDetails(_vaultProfiles[_lstVaultProfiles.SelectedIndex]);
            }
            else
            {
                _suppressVaultProfileSelection = false;
                _activeVaultProfileIndex = -1;
                ClearVaultProfileDetails();
            }

            if (removedWasDefault)
            {
                _vaultDefaultProfileName = _activeVaultProfileIndex >= 0
                    ? _vaultProfiles[_activeVaultProfileIndex].Name
                    : null;
            }

            UpdateVaultControlStates();
        }

        private void LoadVaultProfileDetails(VaultProfileConfig profile)
        {
            _txtVaultProfileName.Text = profile.Name;
            _txtVaultAddress.Text = profile.Address;
            _txtVaultNamespace.Text = profile.Namespace;
            _txtVaultMountPath.Text = profile.MountPath;
            _cmbVaultKvVersion.SelectedIndex = (int)profile.KvVersion;
            _cmbVaultAuthMethod.SelectedIndex = (int)profile.AuthMethod;
            _txtVaultAppRoleId.Text = profile.AppRoleRoleId;
            _txtVaultLdapUsername.Text = profile.LdapUsername;
            _txtVaultUserpassUsername.Text = profile.UserpassUsername;
            _txtVaultOidcAuthMountPath.Text = string.IsNullOrWhiteSpace(profile.OidcAuthMountPath) ? "oidc" : profile.OidcAuthMountPath;
            _txtVaultOidcRole.Text = profile.OidcRole;
            _txtVaultOidcCallbackHost.Text = string.IsNullOrWhiteSpace(profile.OidcCallbackHost) ? "127.0.0.1" : profile.OidcCallbackHost;
            _numVaultOidcCallbackPort.Value = Math.Clamp(profile.OidcCallbackPort <= 0 ? 8250 : profile.OidcCallbackPort, 1, 65535);
            _txtVaultOidcCallbackPath.Text = string.IsNullOrWhiteSpace(profile.OidcCallbackPath) ? "/oidc/callback" : profile.OidcCallbackPath;
            _numVaultOidcTimeoutSeconds.Value = Math.Clamp(profile.OidcTimeoutSeconds <= 0 ? 180 : profile.OidcTimeoutSeconds, 15, 3600);
            _numVaultCacheTtl.Value = Math.Clamp(profile.CacheTtlSeconds, 0, 86400);
            _txtVaultCaCertPath.Text = profile.CaCertificatePath;
            _chkVaultSkipTls.Checked = profile.SkipTlsVerification;

            _suppressVaultDefaultToggle = true;
            _chkVaultDefault.Checked = string.Equals(
                profile.Name,
                _vaultDefaultProfileName,
                StringComparison.OrdinalIgnoreCase);
            _suppressVaultDefaultToggle = false;

            // Load secrets from credential manager
            _txtVaultToken.Text = string.Empty;
            _txtVaultAppRoleSecret.Text = string.Empty;
            _txtVaultLdapPassword.Text = string.Empty;
            _txtVaultUserpassPassword.Text = string.Empty;

            if (_credentialProvider != null)
            {
                if (_credentialProvider.TryGetPassword(CredentialTargets.VaultAuthTarget(profile.Name, "token"), out _, out var token))
                    _txtVaultToken.Text = token;
                if (_credentialProvider.TryGetPassword(CredentialTargets.VaultAuthTarget(profile.Name, "approle_secret"), out _, out var secret))
                    _txtVaultAppRoleSecret.Text = secret;
                if (_credentialProvider.TryGetPassword(CredentialTargets.VaultAuthTarget(profile.Name, "ldap_password"), out _, out var ldapPass))
                    _txtVaultLdapPassword.Text = ldapPass;
                if (_credentialProvider.TryGetPassword(CredentialTargets.VaultAuthTarget(profile.Name, "userpass_password"), out _, out var userpass))
                    _txtVaultUserpassPassword.Text = userpass;
            }

            UpdateVaultAuthFieldVisibility();
        }

        private void ClearVaultProfileDetails()
        {
            _txtVaultProfileName.Text = string.Empty;
            _txtVaultAddress.Text = string.Empty;
            _txtVaultNamespace.Text = string.Empty;
            _txtVaultMountPath.Text = "secret";
            _cmbVaultKvVersion.SelectedIndex = 0;
            _cmbVaultAuthMethod.SelectedIndex = 0;
            _txtVaultToken.Text = string.Empty;
            _txtVaultAppRoleId.Text = string.Empty;
            _txtVaultAppRoleSecret.Text = string.Empty;
            _txtVaultLdapUsername.Text = string.Empty;
            _txtVaultLdapPassword.Text = string.Empty;
            _txtVaultUserpassUsername.Text = string.Empty;
            _txtVaultUserpassPassword.Text = string.Empty;
            _txtVaultOidcAuthMountPath.Text = "oidc";
            _txtVaultOidcRole.Text = string.Empty;
            _txtVaultOidcCallbackHost.Text = "127.0.0.1";
            _numVaultOidcCallbackPort.Value = 8250;
            _txtVaultOidcCallbackPath.Text = "/oidc/callback";
            _numVaultOidcTimeoutSeconds.Value = 180;
            _txtVaultCaCertPath.Text = string.Empty;
            _chkVaultSkipTls.Checked = false;
            _numVaultCacheTtl.Value = 300;
            _chkVaultDefault.Checked = false;
            UpdateVaultAuthFieldVisibility();
        }

        private void PersistCurrentVaultProfile()
        {
            PersistVaultProfileByIndex(_activeVaultProfileIndex);
        }

        private void PersistVaultProfileByIndex(int index)
        {
            if (index < 0 || index >= _vaultProfiles.Count)
                return;

            var profile = _vaultProfiles[index];
            var oldName = profile.Name;
            profile.Name = _txtVaultProfileName.Text.Trim();
            profile.Address = _txtVaultAddress.Text.Trim();
            profile.Namespace = _txtVaultNamespace.Text.Trim();
            profile.MountPath = string.IsNullOrWhiteSpace(_txtVaultMountPath.Text) ? "secret" : _txtVaultMountPath.Text.Trim();
            profile.KvVersion = (VaultKvVersion)_cmbVaultKvVersion.SelectedIndex;
            profile.AuthMethod = (VaultAuthMethod)_cmbVaultAuthMethod.SelectedIndex;
            profile.AppRoleRoleId = _txtVaultAppRoleId.Text.Trim();
            profile.LdapUsername = _txtVaultLdapUsername.Text.Trim();
            profile.UserpassUsername = _txtVaultUserpassUsername.Text.Trim();
            profile.OidcAuthMountPath = string.IsNullOrWhiteSpace(_txtVaultOidcAuthMountPath.Text) ? "oidc" : _txtVaultOidcAuthMountPath.Text.Trim();
            profile.OidcRole = _txtVaultOidcRole.Text.Trim();
            profile.OidcCallbackHost = string.IsNullOrWhiteSpace(_txtVaultOidcCallbackHost.Text) ? "127.0.0.1" : _txtVaultOidcCallbackHost.Text.Trim();
            profile.OidcCallbackPort = (int)_numVaultOidcCallbackPort.Value;
            profile.OidcCallbackPath = string.IsNullOrWhiteSpace(_txtVaultOidcCallbackPath.Text) ? "/oidc/callback" : _txtVaultOidcCallbackPath.Text.Trim();
            profile.OidcTimeoutSeconds = (int)_numVaultOidcTimeoutSeconds.Value;
            profile.CacheTtlSeconds = (int)_numVaultCacheTtl.Value;
            profile.CaCertificatePath = _txtVaultCaCertPath.Text.Trim();
            profile.SkipTlsVerification = _chkVaultSkipTls.Checked;

            // Update list display if name changed
            if (!string.Equals(oldName, profile.Name, StringComparison.Ordinal))
            {
                _suppressVaultProfileSelection = true;
                _lstVaultProfiles.Items[index] = profile.Name;
                _suppressVaultProfileSelection = false;

                if (string.Equals(_vaultDefaultProfileName, oldName, StringComparison.OrdinalIgnoreCase))
                    _vaultDefaultProfileName = profile.Name;
            }

            if (_chkVaultDefault.Checked)
                _vaultDefaultProfileName = profile.Name;

            // Store secrets in credential manager
            if (_credentialProvider != null && !string.IsNullOrEmpty(profile.Name))
            {
                SaveVaultCredential(profile.Name, "token", _txtVaultToken.Text);
                SaveVaultCredential(profile.Name, "approle_secret", _txtVaultAppRoleSecret.Text);
                SaveVaultCredential(profile.Name, "ldap_password", _txtVaultLdapPassword.Text);
                SaveVaultCredential(profile.Name, "userpass_password", _txtVaultUserpassPassword.Text);
            }
        }

        private void SaveVaultCredential(string profileName, string authType, string value)
        {
            var target = CredentialTargets.VaultAuthTarget(profileName, authType);
            if (string.IsNullOrEmpty(value))
                _credentialProvider?.DeletePassword(target);
            else
                _credentialProvider?.SavePassword(target, string.Empty, value);
        }

        private void BtnVaultBrowseCaCert_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select CA Certificate",
                Filter = "Certificate files (*.pem;*.crt;*.cer)|*.pem;*.crt;*.cer|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                _txtVaultCaCertPath.Text = dialog.FileName;
        }

        private async void BtnVaultTestConnection_Click(object? sender, EventArgs e)
        {
            PersistCurrentVaultProfile();
            var index = _lstVaultProfiles.SelectedIndex;
            if (index < 0 || index >= _vaultProfiles.Count)
                return;

            var profile = _vaultProfiles[index];
            _btnVaultTestConnection.Enabled = false;
            _btnVaultTestConnection.Text = "Testing...";
            try
            {
                var testSettings = new VaultSettings
                {
                    Enabled = true,
                    Profiles = new List<VaultProfileConfig> { profile },
                    DefaultProfileName = profile.Name
                };

                using var testService = new VaultService(
                    testSettings,
                    tokenProvider: (name, _) => !string.IsNullOrEmpty(_txtVaultToken.Text) ? _txtVaultToken.Text : null,
                    secretIdProvider: (name, _) => !string.IsNullOrEmpty(_txtVaultAppRoleSecret.Text) ? _txtVaultAppRoleSecret.Text : null,
                    ldapPasswordProvider: (name, _) => !string.IsNullOrEmpty(_txtVaultLdapPassword.Text) ? _txtVaultLdapPassword.Text : null,
                    userpassPasswordProvider: (name, _) => !string.IsNullOrEmpty(_txtVaultUserpassPassword.Text) ? _txtVaultUserpassPassword.Text : null,
                    tokenSaver: (name, token) => SaveVaultCredential(name, "token", token));

                await testService.TestConnectionAsync(profile.Name);
                MessageBox.Show(this, "Connection successful.", "Vault Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Connection failed:\n{ex.Message}", "Vault Test", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _btnVaultTestConnection.Text = "Test Connection";
                _btnVaultTestConnection.Enabled = true;
            }
        }

        private void LoadVaultSettings()
        {
            var config = _configService.GetCurrent();
            _vaultDefaultProfileName = string.IsNullOrWhiteSpace(config.Vault.DefaultProfileName)
                ? null
                : config.Vault.DefaultProfileName.Trim();
            _chkVaultEnabled.Checked = config.Vault.Enabled;
            _vaultProfiles.Clear();

            _suppressVaultProfileSelection = true;
            _lstVaultProfiles.Items.Clear();

            foreach (var p in config.Vault.Profiles)
            {
                var clone = new VaultProfileConfig
                {
                    Name = p.Name,
                    Address = p.Address,
                    Namespace = p.Namespace,
                    MountPath = p.MountPath,
                    KvVersion = p.KvVersion,
                    AuthMethod = p.AuthMethod,
                    AppRoleRoleId = p.AppRoleRoleId,
                    LdapUsername = p.LdapUsername,
                    UserpassUsername = p.UserpassUsername,
                    OidcAuthMountPath = p.OidcAuthMountPath,
                    OidcRole = p.OidcRole,
                    OidcCallbackHost = p.OidcCallbackHost,
                    OidcCallbackPort = p.OidcCallbackPort,
                    OidcCallbackPath = p.OidcCallbackPath,
                    OidcTimeoutSeconds = p.OidcTimeoutSeconds,
                    CacheTtlSeconds = p.CacheTtlSeconds,
                    CaCertificatePath = p.CaCertificatePath,
                    SkipTlsVerification = p.SkipTlsVerification
                };
                _vaultProfiles.Add(clone);
                _lstVaultProfiles.Items.Add(clone.Name);
            }

            if (_lstVaultProfiles.Items.Count > 0)
            {
                var initialIndex = _vaultProfiles.FindIndex(p =>
                    string.Equals(p.Name, _vaultDefaultProfileName, StringComparison.OrdinalIgnoreCase));
                if (initialIndex < 0)
                    initialIndex = 0;

                _lstVaultProfiles.SelectedIndex = initialIndex;
                _suppressVaultProfileSelection = false;
                _activeVaultProfileIndex = initialIndex;
                LoadVaultProfileDetails(_vaultProfiles[initialIndex]);
            }
            else
            {
                _suppressVaultProfileSelection = false;
                _activeVaultProfileIndex = -1;
                ClearVaultProfileDetails();
            }

            UpdateVaultControlStates();
        }

        private void SaveVaultSettings(AppConfiguration config)
        {
            PersistCurrentVaultProfile();
            config.Vault.Enabled = _chkVaultEnabled.Checked;
            config.Vault.Profiles = _vaultProfiles.Select(p => new VaultProfileConfig
            {
                Name = p.Name,
                Address = p.Address,
                Namespace = p.Namespace,
                MountPath = p.MountPath,
                KvVersion = p.KvVersion,
                AuthMethod = p.AuthMethod,
                AppRoleRoleId = p.AppRoleRoleId,
                LdapUsername = p.LdapUsername,
                UserpassUsername = p.UserpassUsername,
                OidcAuthMountPath = p.OidcAuthMountPath,
                OidcRole = p.OidcRole,
                OidcCallbackHost = p.OidcCallbackHost,
                OidcCallbackPort = p.OidcCallbackPort,
                OidcCallbackPath = p.OidcCallbackPath,
                OidcTimeoutSeconds = p.OidcTimeoutSeconds,
                CacheTtlSeconds = p.CacheTtlSeconds,
                CaCertificatePath = p.CaCertificatePath,
                SkipTlsVerification = p.SkipTlsVerification
            }).ToList();

            var resolvedDefaultProfile = _vaultProfiles
                .FirstOrDefault(p => string.Equals(p.Name, _vaultDefaultProfileName, StringComparison.OrdinalIgnoreCase))
                ?.Name;

            if (string.IsNullOrWhiteSpace(resolvedDefaultProfile) && config.Vault.Profiles.Count > 0)
                resolvedDefaultProfile = config.Vault.Profiles[0].Name;

            _vaultDefaultProfileName = resolvedDefaultProfile;
            config.Vault.DefaultProfileName = resolvedDefaultProfile ?? "";
        }

        private TabPage CreateNotificationsTab()
        {
            var tab = new TabPage("Notifications");
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 160,
                FixedPanel = FixedPanel.Panel1
            };

            // Left panel: enable + profile list + add/remove
            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            _chkNotificationsEnabled = new CheckBox
            {
                Name = "chkNotificationsEnabled",
                Text = "Enable Notifications",
                AutoSize = true,
                Dock = DockStyle.Top
            };
            _chkNotificationsEnabled.CheckedChanged += (_, _) => UpdateNotificationControlStates();

            _lstNotificationProfiles = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            _lstNotificationProfiles.SelectedIndexChanged += LstNotificationProfiles_SelectedIndexChanged;

            var profileButtonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 0)
            };
            _btnNotificationAdd = new Button { Text = "Add", Width = 70, Height = 26 };
            _btnNotificationAdd.Click += BtnNotificationAdd_Click;
            _btnNotificationRemove = new Button { Text = "Remove", Width = 70, Height = 26 };
            _btnNotificationRemove.Click += BtnNotificationRemove_Click;
            profileButtonPanel.Controls.Add(_btnNotificationAdd);
            profileButtonPanel.Controls.Add(_btnNotificationRemove);

            leftPanel.Controls.Add(_lstNotificationProfiles);
            leftPanel.Controls.Add(profileButtonPanel);
            leftPanel.Controls.Add(_chkNotificationsEnabled);

            // Right panel: detail fields
            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(8, 8, 8, 8)
            };
            _scrollableFlowPanels.Add(rightFlow);

            var sectionFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            const int labelColumnWidth = 120;
            const int inputMinWidth = 205;

            TableLayoutPanel LabeledTextBox(string labelText, out TextBox textBox, bool isPassword = false)
            {
                var row = new TableLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 2,
                    RowCount = 1,
                    Margin = new Padding(0, 2, 0, 2),
                    Width = 430
                };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelColumnWidth));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                row.Controls.Add(new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) }, 0, 0);
                textBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    MinimumSize = new Size(inputMinWidth, 0),
                    UseSystemPasswordChar = isPassword
                };
                row.Controls.Add(textBox, 1, 0);
                return row;
            }

            // Profile header
            rightFlow.Controls.Add(new Label { Text = "Profile", Font = sectionFont, AutoSize = true, Margin = new Padding(0, 0, 0, 4) });
            rightFlow.Controls.Add(LabeledTextBox("Profile Name:", out _txtNotificationProfileName));

            // Kind combo
            var kindRow = new TableLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 2, 0, 2), Width = 430
            };
            kindRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelColumnWidth));
            kindRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            kindRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            kindRow.Controls.Add(new Label { Text = "Channel:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) }, 0, 0);
            _cmbNotificationKind = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, MinimumSize = new Size(inputMinWidth, 0) };
            _cmbNotificationKind.Items.AddRange(new object[] { "Slack", "Teams", "Discord", "SMTP Email" });
            _cmbNotificationKind.SelectedIndex = 0;
            _cmbNotificationKind.SelectedIndexChanged += (_, _) => UpdateNotificationKindVisibility();
            kindRow.Controls.Add(_cmbNotificationKind, 1, 0);
            rightFlow.Controls.Add(kindRow);

            rightFlow.Controls.Add(LabeledTextBox("Default Title:", out _txtNotificationDefaultTitle));

            // Webhook section (Slack/Teams/Discord)
            _pnlNotificationWebhook = new Panel { AutoSize = true, Width = 430 };
            var webhookLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
            webhookLayout.Controls.Add(new Label { Text = "Webhook", Font = sectionFont, AutoSize = true, Margin = new Padding(0, 10, 0, 4) });
            webhookLayout.Controls.Add(LabeledTextBox("Webhook URL:", out _txtNotificationWebhookUrl, isPassword: true));
            _pnlNotificationWebhook.Controls.Add(webhookLayout);
            rightFlow.Controls.Add(_pnlNotificationWebhook);

            // SMTP section
            _pnlNotificationSmtp = new Panel { AutoSize = true, Width = 430, Visible = false };
            var smtpLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
            smtpLayout.Controls.Add(new Label { Text = "SMTP Server", Font = sectionFont, AutoSize = true, Margin = new Padding(0, 10, 0, 4) });
            smtpLayout.Controls.Add(LabeledTextBox("Host:", out _txtNotificationSmtpHost));

            var smtpPortRow = new TableLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 2, 0, 2), Width = 430
            };
            smtpPortRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelColumnWidth));
            smtpPortRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            smtpPortRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            smtpPortRow.Controls.Add(new Label { Text = "Port:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) }, 0, 0);
            _numNotificationSmtpPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 587, Width = 100, TextAlign = HorizontalAlignment.Right };
            smtpPortRow.Controls.Add(_numNotificationSmtpPort, 1, 0);
            smtpLayout.Controls.Add(smtpPortRow);

            smtpLayout.Controls.Add(LabeledTextBox("From Address:", out _txtNotificationSmtpFrom));
            smtpLayout.Controls.Add(LabeledTextBox("To Addresses:", out _txtNotificationSmtpTo));
            _txtNotificationSmtpTo.Multiline = true;
            _txtNotificationSmtpTo.Height = 44;
            _txtNotificationSmtpTo.ScrollBars = ScrollBars.Vertical;
            smtpLayout.Controls.Add(new Label { Text = "(One per line, or comma/semicolon-separated)", AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f), Margin = new Padding(labelColumnWidth, 0, 0, 4) });
            smtpLayout.Controls.Add(LabeledTextBox("Username:", out _txtNotificationSmtpUsername));
            smtpLayout.Controls.Add(LabeledTextBox("Password:", out _txtNotificationSmtpPassword, isPassword: true));
            _chkNotificationSmtpUseStartTls = new CheckBox { Text = "Use STARTTLS / SSL", AutoSize = true, Margin = new Padding(labelColumnWidth, 4, 0, 2), Checked = true };
            smtpLayout.Controls.Add(_chkNotificationSmtpUseStartTls);
            _pnlNotificationSmtp.Controls.Add(smtpLayout);
            rightFlow.Controls.Add(_pnlNotificationSmtp);

            _chkNotificationDefault = new CheckBox { Text = "Set as default profile", AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
            _chkNotificationDefault.CheckedChanged += ChkNotificationDefault_CheckedChanged;
            rightFlow.Controls.Add(_chkNotificationDefault);

            mainSplit.Panel1.Controls.Add(leftPanel);
            mainSplit.Panel2.Controls.Add(rightFlow);
            tab.Controls.Add(mainSplit);

            return tab;
        }

        private void UpdateNotificationControlStates()
        {
            bool enabled = _chkNotificationsEnabled.Checked;
            _lstNotificationProfiles.Enabled = enabled;
            _btnNotificationAdd.Enabled = enabled;
            _btnNotificationRemove.Enabled = enabled && _lstNotificationProfiles.SelectedIndex >= 0;
            SetNotificationDetailFieldsEnabled(enabled && _lstNotificationProfiles.SelectedIndex >= 0);
        }

        private void SetNotificationDetailFieldsEnabled(bool enabled)
        {
            _txtNotificationProfileName.Enabled = enabled;
            _cmbNotificationKind.Enabled = enabled;
            _txtNotificationDefaultTitle.Enabled = enabled;
            _txtNotificationWebhookUrl.Enabled = enabled;
            _txtNotificationSmtpHost.Enabled = enabled;
            _numNotificationSmtpPort.Enabled = enabled;
            _txtNotificationSmtpFrom.Enabled = enabled;
            _txtNotificationSmtpTo.Enabled = enabled;
            _txtNotificationSmtpUsername.Enabled = enabled;
            _txtNotificationSmtpPassword.Enabled = enabled;
            _chkNotificationSmtpUseStartTls.Enabled = enabled;
            _chkNotificationDefault.Enabled = enabled;
        }

        private void UpdateNotificationKindVisibility()
        {
            var kind = GetSelectedNotificationKind();
            _pnlNotificationWebhook.Visible = kind != NotificationChannelKind.Smtp;
            _pnlNotificationSmtp.Visible = kind == NotificationChannelKind.Smtp;
        }

        private NotificationChannelKind GetSelectedNotificationKind()
        {
            return _cmbNotificationKind.SelectedIndex switch
            {
                1 => NotificationChannelKind.Teams,
                2 => NotificationChannelKind.Discord,
                3 => NotificationChannelKind.Smtp,
                _ => NotificationChannelKind.Slack
            };
        }

        private void SetSelectedNotificationKind(NotificationChannelKind kind)
        {
            _cmbNotificationKind.SelectedIndex = kind switch
            {
                NotificationChannelKind.Teams => 1,
                NotificationChannelKind.Discord => 2,
                NotificationChannelKind.Smtp => 3,
                _ => 0
            };
        }

        private void LstNotificationProfiles_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressNotificationProfileSelection)
                return;

            PersistNotificationProfileByIndex(_activeNotificationProfileIndex);
            _activeNotificationProfileIndex = _lstNotificationProfiles.SelectedIndex;

            if (_lstNotificationProfiles.SelectedIndex >= 0 && _lstNotificationProfiles.SelectedIndex < _notificationProfiles.Count)
                LoadNotificationProfileDetails(_notificationProfiles[_lstNotificationProfiles.SelectedIndex]);
            else
                ClearNotificationProfileDetails();

            UpdateNotificationControlStates();
        }

        private void ChkNotificationDefault_CheckedChanged(object? sender, EventArgs e)
        {
            if (_suppressNotificationDefaultToggle) return;
            var index = _activeNotificationProfileIndex;
            if (index < 0 || index >= _notificationProfiles.Count)
                return;
            if (_chkNotificationDefault.Checked)
                _notificationDefaultProfileName = _notificationProfiles[index].Name;
            else if (string.Equals(_notificationDefaultProfileName, _notificationProfiles[index].Name, StringComparison.OrdinalIgnoreCase))
                _notificationDefaultProfileName = null;
        }

        private void BtnNotificationAdd_Click(object? sender, EventArgs e)
        {
            var name = $"profile-{_notificationProfiles.Count + 1}";
            var profile = new NotificationProfile { Name = name, Kind = NotificationChannelKind.Slack };
            _notificationProfiles.Add(profile);
            _suppressNotificationProfileSelection = true;
            _lstNotificationProfiles.Items.Add(name);
            _lstNotificationProfiles.SelectedIndex = _lstNotificationProfiles.Items.Count - 1;
            _suppressNotificationProfileSelection = false;
            _activeNotificationProfileIndex = _lstNotificationProfiles.SelectedIndex;
            LoadNotificationProfileDetails(profile);

            if (string.IsNullOrWhiteSpace(_notificationDefaultProfileName) && _notificationProfiles.Count == 1)
                _notificationDefaultProfileName = profile.Name;

            UpdateNotificationControlStates();
        }

        private void BtnNotificationRemove_Click(object? sender, EventArgs e)
        {
            var index = _lstNotificationProfiles.SelectedIndex;
            if (index < 0 || index >= _notificationProfiles.Count)
                return;

            var profileName = _notificationProfiles[index].Name;

            // Purge stored secrets
            if (_credentialProvider != null && !string.IsNullOrEmpty(profileName))
            {
                _credentialProvider.DeletePassword(CredentialTargets.NotifyWebhookTarget(profileName));
                _credentialProvider.DeletePassword(CredentialTargets.NotifySmtpPasswordTarget(profileName));
            }

            _notificationProfiles.RemoveAt(index);
            _suppressNotificationProfileSelection = true;
            _lstNotificationProfiles.Items.RemoveAt(index);

            if (_lstNotificationProfiles.Items.Count > 0)
            {
                _lstNotificationProfiles.SelectedIndex = Math.Min(index, _lstNotificationProfiles.Items.Count - 1);
                _suppressNotificationProfileSelection = false;
                _activeNotificationProfileIndex = _lstNotificationProfiles.SelectedIndex;
                LoadNotificationProfileDetails(_notificationProfiles[_lstNotificationProfiles.SelectedIndex]);
            }
            else
            {
                _suppressNotificationProfileSelection = false;
                _activeNotificationProfileIndex = -1;
                ClearNotificationProfileDetails();
            }

            if (string.Equals(_notificationDefaultProfileName, profileName, StringComparison.OrdinalIgnoreCase))
            {
                _notificationDefaultProfileName = _activeNotificationProfileIndex >= 0
                    ? _notificationProfiles[_activeNotificationProfileIndex].Name
                    : null;
            }

            UpdateNotificationControlStates();
        }

        private void LoadNotificationProfileDetails(NotificationProfile profile)
        {
            _txtNotificationProfileName.Text = profile.Name;
            SetSelectedNotificationKind(profile.Kind);
            _txtNotificationDefaultTitle.Text = profile.DefaultTitle;
            _txtNotificationSmtpHost.Text = profile.SmtpHost;
            _numNotificationSmtpPort.Value = Math.Clamp(profile.SmtpPort, 1, 65535);
            _txtNotificationSmtpFrom.Text = profile.SmtpFromAddress;
            _txtNotificationSmtpTo.Text = string.Join(Environment.NewLine, profile.SmtpToAddresses ?? new List<string>());
            _txtNotificationSmtpUsername.Text = profile.SmtpUsername;
            _chkNotificationSmtpUseStartTls.Checked = profile.UseStartTls;

            _txtNotificationWebhookUrl.Text = "";
            _txtNotificationSmtpPassword.Text = "";
            if (_credentialProvider != null && !string.IsNullOrEmpty(profile.Name))
            {
                if (_credentialProvider.TryGetPassword(CredentialTargets.NotifyWebhookTarget(profile.Name), out _, out var url))
                    _txtNotificationWebhookUrl.Text = url ?? "";
                if (_credentialProvider.TryGetPassword(CredentialTargets.NotifySmtpPasswordTarget(profile.Name), out _, out var pw))
                    _txtNotificationSmtpPassword.Text = pw ?? "";
            }

            _suppressNotificationDefaultToggle = true;
            _chkNotificationDefault.Checked = !string.IsNullOrEmpty(_notificationDefaultProfileName)
                && string.Equals(_notificationDefaultProfileName, profile.Name, StringComparison.OrdinalIgnoreCase);
            _suppressNotificationDefaultToggle = false;

            UpdateNotificationKindVisibility();
        }

        private void ClearNotificationProfileDetails()
        {
            _txtNotificationProfileName.Text = "";
            _cmbNotificationKind.SelectedIndex = 0;
            _txtNotificationDefaultTitle.Text = "";
            _txtNotificationWebhookUrl.Text = "";
            _txtNotificationSmtpHost.Text = "";
            _numNotificationSmtpPort.Value = 587;
            _txtNotificationSmtpFrom.Text = "";
            _txtNotificationSmtpTo.Text = "";
            _txtNotificationSmtpUsername.Text = "";
            _txtNotificationSmtpPassword.Text = "";
            _chkNotificationSmtpUseStartTls.Checked = true;
            _suppressNotificationDefaultToggle = true;
            _chkNotificationDefault.Checked = false;
            _suppressNotificationDefaultToggle = false;
            UpdateNotificationKindVisibility();
        }

        private void PersistCurrentNotificationProfile()
        {
            PersistNotificationProfileByIndex(_activeNotificationProfileIndex);
        }

        private void PersistNotificationProfileByIndex(int index)
        {
            if (index < 0 || index >= _notificationProfiles.Count)
                return;

            var profile = _notificationProfiles[index];
            var oldName = profile.Name;
            var newName = (_txtNotificationProfileName.Text ?? "").Trim();

            profile.Kind = GetSelectedNotificationKind();
            profile.DefaultTitle = (_txtNotificationDefaultTitle.Text ?? "").Trim();
            profile.SmtpHost = (_txtNotificationSmtpHost.Text ?? "").Trim();
            profile.SmtpPort = (int)_numNotificationSmtpPort.Value;
            profile.SmtpFromAddress = (_txtNotificationSmtpFrom.Text ?? "").Trim();
            profile.SmtpToAddresses = SplitSmtpRecipients(_txtNotificationSmtpTo.Text);
            profile.SmtpUsername = (_txtNotificationSmtpUsername.Text ?? "").Trim();
            profile.UseStartTls = _chkNotificationSmtpUseStartTls.Checked;

            if (!string.IsNullOrEmpty(newName) && !string.Equals(newName, oldName, StringComparison.Ordinal))
            {
                // Rename: migrate credentials from old target to new
                if (_credentialProvider != null && !string.IsNullOrEmpty(oldName))
                {
                    MigrateNotificationCredential(oldName, newName, CredentialTargets.NotifyWebhookTarget);
                    MigrateNotificationCredential(oldName, newName, CredentialTargets.NotifySmtpPasswordTarget);
                }
                profile.Name = newName;
                _suppressNotificationProfileSelection = true;
                _lstNotificationProfiles.Items[index] = profile.Name;
                _suppressNotificationProfileSelection = false;

                if (string.Equals(_notificationDefaultProfileName, oldName, StringComparison.OrdinalIgnoreCase))
                    _notificationDefaultProfileName = profile.Name;
            }

            if (_chkNotificationDefault.Checked)
                _notificationDefaultProfileName = profile.Name;

            if (_credentialProvider != null && !string.IsNullOrEmpty(profile.Name))
            {
                SaveNotificationSecret(CredentialTargets.NotifyWebhookTarget(profile.Name), _txtNotificationWebhookUrl.Text);
                SaveNotificationSecret(CredentialTargets.NotifySmtpPasswordTarget(profile.Name), _txtNotificationSmtpPassword.Text);
            }
        }

        private void MigrateNotificationCredential(string oldName, string newName, Func<string, string> targetBuilder)
        {
            if (_credentialProvider == null) return;
            var oldTarget = targetBuilder(oldName);
            if (_credentialProvider.TryGetPassword(oldTarget, out _, out var value) && !string.IsNullOrEmpty(value))
            {
                _credentialProvider.SavePassword(targetBuilder(newName), string.Empty, value);
                _credentialProvider.DeletePassword(oldTarget);
            }
        }

        private void SaveNotificationSecret(string target, string value)
        {
            if (_credentialProvider == null) return;
            if (string.IsNullOrEmpty(value))
                _credentialProvider.DeletePassword(target);
            else
                _credentialProvider.SavePassword(target, string.Empty, value);
        }

        private static List<string> SplitSmtpRecipients(string? raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
                return result;
            foreach (var token in raw.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = token.Trim();
                if (trimmed.Length > 0)
                    result.Add(trimmed);
            }
            return result;
        }

        private void LoadNotificationSettings()
        {
            var config = _configService.GetCurrent();
            _notificationDefaultProfileName = string.IsNullOrWhiteSpace(config.Notifications.DefaultProfileName)
                ? null
                : config.Notifications.DefaultProfileName.Trim();
            _chkNotificationsEnabled.Checked = config.Notifications.Enabled;
            _notificationProfiles.Clear();

            _suppressNotificationProfileSelection = true;
            _lstNotificationProfiles.Items.Clear();

            foreach (var p in config.Notifications.Profiles)
            {
                var clone = new NotificationProfile
                {
                    Name = p.Name,
                    Kind = p.Kind,
                    DefaultTitle = p.DefaultTitle,
                    SmtpHost = p.SmtpHost,
                    SmtpPort = p.SmtpPort,
                    SmtpFromAddress = p.SmtpFromAddress,
                    SmtpToAddresses = new List<string>(p.SmtpToAddresses ?? new List<string>()),
                    SmtpUsername = p.SmtpUsername,
                    UseStartTls = p.UseStartTls
                };
                _notificationProfiles.Add(clone);
                _lstNotificationProfiles.Items.Add(clone.Name);
            }

            if (_lstNotificationProfiles.Items.Count > 0)
            {
                var initialIndex = _notificationProfiles.FindIndex(p =>
                    string.Equals(p.Name, _notificationDefaultProfileName, StringComparison.OrdinalIgnoreCase));
                if (initialIndex < 0) initialIndex = 0;
                _lstNotificationProfiles.SelectedIndex = initialIndex;
                _suppressNotificationProfileSelection = false;
                _activeNotificationProfileIndex = initialIndex;
                LoadNotificationProfileDetails(_notificationProfiles[initialIndex]);
            }
            else
            {
                _suppressNotificationProfileSelection = false;
                _activeNotificationProfileIndex = -1;
                ClearNotificationProfileDetails();
            }

            UpdateNotificationControlStates();
        }

        private void SaveNotificationSettings(AppConfiguration config)
        {
            PersistCurrentNotificationProfile();
            config.Notifications.Enabled = _chkNotificationsEnabled.Checked;
            config.Notifications.Profiles = _notificationProfiles.Select(p => new NotificationProfile
            {
                Name = p.Name,
                Kind = p.Kind,
                DefaultTitle = p.DefaultTitle,
                SmtpHost = p.SmtpHost,
                SmtpPort = p.SmtpPort,
                SmtpFromAddress = p.SmtpFromAddress,
                SmtpToAddresses = new List<string>(p.SmtpToAddresses ?? new List<string>()),
                SmtpUsername = p.SmtpUsername,
                UseStartTls = p.UseStartTls
            }).ToList();

            var resolvedDefault = _notificationProfiles
                .FirstOrDefault(p => string.Equals(p.Name, _notificationDefaultProfileName, StringComparison.OrdinalIgnoreCase))
                ?.Name;
            if (string.IsNullOrWhiteSpace(resolvedDefault) && config.Notifications.Profiles.Count > 0)
                resolvedDefault = config.Notifications.Profiles[0].Name;

            _notificationDefaultProfileName = resolvedDefault;
            config.Notifications.DefaultProfileName = resolvedDefault ?? "";
        }

        private TabPage CreateUpdatesTab()
        {
            var tabUpdates = new TabPage("Updates");

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12, 12, 12, 12),
            };
            _scrollableFlowPanels.Add(flow);

            var sectionFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            var noteFont = new Font("Segoe UI", 8f);

            flow.Controls.Add(new Label
            {
                Text = "Automatic Updates", Font = sectionFont, AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkCheckForUpdatesOnStartup",
                Text = "Check for updates when application starts",
                AutoSize = true
            });

            flow.Controls.Add(new Label
            {
                Text = "Troubleshooting", Font = sectionFont, AutoSize = true,
                Margin = new Padding(0, 12, 0, 4)
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableUpdateLog",
                Text = "Enable update log file (for troubleshooting update failures)",
                AutoSize = true
            });
            flow.Controls.Add(new Label
            {
                Text = "Log file: %TEMP%\\SSH_Helper_Update\\update.log",
                AutoSize = true, ForeColor = Color.Gray, Font = noteFont,
                Margin = new Padding(17, 0, 0, 2)
            });

            tabUpdates.Controls.Add(flow);
            return tabUpdates;
        }

        private TabPage CreateCommandEditorTab()
        {
            var tabCommandEditor = new TabPage("Command Editor");

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12, 12, 12, 12)
            };
            _scrollableFlowPanels.Add(flow);

            var sectionFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            Label SectionHeader(string text, int topMargin = 10) => new()
            {
                Text = text,
                Font = sectionFont,
                AutoSize = true,
                Margin = new Padding(0, topMargin, 0, 4)
            };

            TableLayoutPanel LabeledNumeric(string label, string name, decimal min, decimal max, decimal value)
            {
                var row = new TableLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 2,
                    RowCount = 1,
                    Margin = new Padding(0, 2, 0, 2)
                };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
                row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                row.Controls.Add(new Label
                {
                    Text = label,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(0, 4, 4, 0)
                }, 0, 0);

                row.Controls.Add(new NumericUpDown
                {
                    Name = name,
                    Minimum = min,
                    Maximum = max,
                    Value = value,
                    TextAlign = HorizontalAlignment.Right,
                    Size = new Size(80, 23)
                }, 1, 0);

                return row;
            }

            // Features
            flow.Controls.Add(SectionHeader("Features", 0));
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableSyntaxHighlighting",
                Text = "Enable syntax highlighting",
                AutoSize = true
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableAutocomplete",
                Text = "Enable autocomplete",
                AutoSize = true
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkAutocompleteShowOnTyping",
                Text = "Show autocomplete while typing",
                AutoSize = true,
                Margin = new Padding(18, 0, 0, 2)
            });

            // Validation and diagnostics
            flow.Controls.Add(SectionHeader("Validation & Diagnostics"));
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableInlineValidation",
                Text = "Enable inline validation",
                AutoSize = true
            });
            flow.Controls.Add(LabeledNumeric(
                "Inline validation debounce (ms):",
                "numValidationDebounceMs",
                CommandEditorSettings.MinValidationDebounceMs,
                CommandEditorSettings.MaxValidationDebounceMs,
                400));
            flow.Controls.Add(new CheckBox
            {
                Name = "chkShowInlineWarnings",
                Text = "Show inline warnings",
                AutoSize = true
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableDiagnosticTooltips",
                Text = "Enable diagnostic hover tooltips",
                AutoSize = true
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableVariableInspectorTooltips",
                Text = "Enable variable inspector tooltips",
                AutoSize = true
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableYamlHygieneWarnings",
                Text = "Enable YAML hygiene warnings",
                AutoSize = true
            });

            // Indentation/newline behavior
            flow.Controls.Add(SectionHeader("Indentation & Newline"));
            flow.Controls.Add(new CheckBox
            {
                Name = "chkUseSpacesForTab",
                Text = "Use spaces for Tab indentation",
                AutoSize = true
            });
            flow.Controls.Add(LabeledNumeric(
                "Indent size (spaces):",
                "numIndentSize",
                CommandEditorSettings.MinIndentSize,
                CommandEditorSettings.MaxIndentSize,
                2));
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableSmartEnter",
                Text = "Enable smart Enter indentation",
                AutoSize = true
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkPreserveBlankLineBetweenSteps",
                Text = "Preserve blank lines between YAML steps",
                AutoSize = true,
                Margin = new Padding(18, 0, 0, 2)
            });

            // Visual aids
            flow.Controls.Add(SectionHeader("Visual Aids"));
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableCurrentLineHighlight",
                Text = "Highlight current line",
                AutoSize = true
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableIndentGuides",
                Text = "Show indentation guides",
                AutoSize = true
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkShowWhitespace",
                Text = "Show whitespace markers",
                AutoSize = true
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableLongLineGuide",
                Text = "Show long-line guide",
                AutoSize = true
            });
            flow.Controls.Add(LabeledNumeric(
                "Long-line guide column:",
                "numLongLineColumn",
                CommandEditorSettings.MinLongLineColumn,
                CommandEditorSettings.MaxLongLineColumn,
                120));
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableCodeFolding",
                Text = "Enable code folding margin",
                AutoSize = true
            });
            flow.Controls.Add(new CheckBox
            {
                Name = "chkEnableBraceMatching",
                Text = "Highlight matching braces",
                AutoSize = true
            });

            tabCommandEditor.Controls.Add(flow);
            return tabCommandEditor;
        }

        private TabPage CreateAppearanceTab()
        {
            var tabAppearance = new TabPage("Appearance");

            // Create scrollable panel for all appearance controls
            var scrollPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(472, 490),
                AutoScroll = true,
                Dock = DockStyle.Fill
            };

            int y = 15;

            // === Font Families Section ===
            var lblFontsSection = new Label
            {
                Text = "Font Families",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, y),
                AutoSize = true
            };
            scrollPanel.Controls.Add(lblFontsSection);
            y += 28;

            var lblUIFont = new Label { Text = "UI Font:", Location = new Point(15, y), AutoSize = true };
            _cboUIFont = new ComboBox
            {
                Location = new Point(120, y - 3),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            PopulateFontComboBox(_cboUIFont, false);
            _cboUIFont.SelectedIndexChanged += (s, e) => UpdatePreview();
            scrollPanel.Controls.Add(lblUIFont);
            scrollPanel.Controls.Add(_cboUIFont);
            y += 30;

            var lblCodeFont = new Label { Text = "Code Font:", Location = new Point(15, y), AutoSize = true };
            _cboCodeFont = new ComboBox
            {
                Location = new Point(120, y - 3),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            PopulateFontComboBox(_cboCodeFont, true);
            _cboCodeFont.SelectedIndexChanged += (s, e) => UpdatePreview();
            scrollPanel.Controls.Add(lblCodeFont);
            scrollPanel.Controls.Add(_cboCodeFont);
            y += 35;

            // === Global Scale Section ===
            var lblScaleSection = new Label
            {
                Text = "Global Scale",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, y),
                AutoSize = true
            };
            scrollPanel.Controls.Add(lblScaleSection);
            y += 25;

            var lblScale = new Label { Text = "Scale Factor:", Location = new Point(15, y + 5), AutoSize = true };
            _trkGlobalScale = new TrackBar
            {
                Location = new Point(100, y),
                Size = new Size(200, 45),
                Minimum = 80,
                Maximum = 150,
                Value = 100,
                TickFrequency = 10,
                SmallChange = 5,
                LargeChange = 10
            };
            _lblGlobalScaleValue = new Label
            {
                Text = "100%",
                Location = new Point(305, y + 5),
                Size = new Size(50, 20)
            };
            _trkGlobalScale.ValueChanged += (s, e) =>
            {
                _lblGlobalScaleValue.Text = $"{_trkGlobalScale.Value}%";
                UpdatePreview();
            };
            scrollPanel.Controls.Add(lblScale);
            scrollPanel.Controls.Add(_trkGlobalScale);
            scrollPanel.Controls.Add(_lblGlobalScaleValue);
            y += 50;

            // === Font Sizes Section ===
            var lblSizesSection = new Label
            {
                Text = "Font Sizes",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, y),
                AutoSize = true
            };
            scrollPanel.Controls.Add(lblSizesSection);
            y += 25;

            var fontSizesTable = CreateLabelSpinnerTable(7, scrollPanel.ClientSize.Width - 30);
            fontSizesTable.Location = new Point(15, y);
            AddTableRow(fontSizesTable, 0, "Section titles:", out _numSectionTitleSize, 9.5m, "Tree views:", out _numTreeViewSize, 9.5m);
            AddTableRow(fontSizesTable, 1, "Empty labels:", out _numEmptyLabelSize, 9.5m, "Execute buttons:", out _numExecuteButtonSize, 9.5m);
            AddTableRow(fontSizesTable, 2, "Code editor:", out _numCodeEditorSize, 9.75m, "Output area:", out _numOutputAreaSize, 9.75m);
            AddTableRow(fontSizesTable, 3, "Tab headers:", out _numTabFontSize, 9m, "Buttons:", out _numButtonFontSize, 9m);
            AddTableRow(fontSizesTable, 4, "Host list:", out _numHostListFontSize, 9m, "Menus:", out _numMenuFontSize, 9m);
            AddTableRow(fontSizesTable, 5, "Status bar:", out _numStatusBarFontSize, 9m, "Dialogs:", out _numDialogFontSize, 9m);

            // Script prompt font size (left column only; right column empty)
            var lblScriptPrompt = new Label { Text = "Script prompts:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 0) };
            _numScriptPromptFontSize = CreateNumericUpDown(0, 0, 9m, 7, 24, 0.5m, 1);
            _numScriptPromptFontSize.ValueChanged += (s, e) => UpdatePreview();
            fontSizesTable.Controls.Add(lblScriptPrompt, 0, 6);
            fontSizesTable.Controls.Add(_numScriptPromptFontSize, 1, 6);

            scrollPanel.Controls.Add(fontSizesTable);
            y += fontSizesTable.PreferredSize.Height + 10;

            // === Layout Section ===
            var lblLayoutSection = new Label
            {
                Text = "Layout",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, y),
                AutoSize = true
            };
            scrollPanel.Controls.Add(lblLayoutSection);
            y += 25;

            // Word wrap checkboxes
            _chkCodeEditorWordWrap = new CheckBox { Text = "Word wrap in code editor", AutoSize = true };
            _chkCodeEditorWordWrap.CheckedChanged += (s, e) => UpdatePreview();
            _chkOutputAreaWordWrap = new CheckBox { Text = "Word wrap in output area", AutoSize = true, Checked = false };
            _chkOutputAreaWordWrap.CheckedChanged += (s, e) => UpdatePreview();

            var layoutTable = CreateLabelSpinnerTable(2, scrollPanel.ClientSize.Width - 30);
            layoutTable.Location = new Point(15, y);

            // Word wrap row
            layoutTable.Controls.Add(_chkCodeEditorWordWrap, 0, 0);
            layoutTable.SetColumnSpan(_chkCodeEditorWordWrap, 2);
            layoutTable.Controls.Add(_chkOutputAreaWordWrap, 2, 0);
            layoutTable.SetColumnSpan(_chkOutputAreaWordWrap, 2);

            // Row heights
            _numTreeViewRowHeight = CreateNumericUpDown(0, 0, 0, 0, 50, 1, 0);
            _numHostListRowHeight = CreateNumericUpDown(0, 0, 28, 16, 50, 1, 0);
            _numTreeViewRowHeight.Size = new Size(50, 23);
            _numHostListRowHeight.Size = new Size(50, 23);
            var lblTreeRowHeight = new Label { Text = "Tree row height (0=auto):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 0) };
            var lblHostListRowHeight = new Label { Text = "Host list row height:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 0) };
            layoutTable.Controls.Add(lblTreeRowHeight, 0, 1);
            layoutTable.Controls.Add(_numTreeViewRowHeight, 1, 1);
            layoutTable.Controls.Add(lblHostListRowHeight, 2, 1);
            layoutTable.Controls.Add(_numHostListRowHeight, 3, 1);

            scrollPanel.Controls.Add(layoutTable);
            y += layoutTable.PreferredSize.Height + 10;

            // === Accent Color Section ===
            var lblAccentSection = new Label
            {
                Text = "Accent Color",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, y),
                AutoSize = true
            };
            scrollPanel.Controls.Add(lblAccentSection);
            y += 25;

            _chkUseCustomAccent = new CheckBox
            {
                Text = "Use custom accent color",
                Location = new Point(15, y),
                AutoSize = true
            };
            _chkUseCustomAccent.CheckedChanged += (s, e) =>
            {
                _btnChooseAccentColor.Enabled = _chkUseCustomAccent.Checked;
                UpdatePreview();
            };

            _pnlAccentColor = new Panel
            {
                Location = new Point(200, y - 2),
                Size = new Size(30, 22),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _customAccentColor
            };

            _btnChooseAccentColor = new Button
            {
                Text = "Choose...",
                Location = new Point(240, y - 3),
                Size = new Size(70, 24),
                Enabled = false
            };
            _btnChooseAccentColor.Click += BtnChooseAccentColor_Click;

            scrollPanel.Controls.Add(_chkUseCustomAccent);
            scrollPanel.Controls.Add(_pnlAccentColor);
            scrollPanel.Controls.Add(_btnChooseAccentColor);
            y += 35;

            // === Preview Section ===
            var lblPreviewSection = new Label
            {
                Text = "Preview",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, y),
                AutoSize = true
            };
            scrollPanel.Controls.Add(lblPreviewSection);
            y += 25;

            _pnlPreview = new Panel
            {
                Location = new Point(15, y),
                Size = new Size(420, 120),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            _lblPreviewTitle = new Label
            {
                Text = "Section Title",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(10, 8),
                AutoSize = true
            };

            _trvPreview = new TreeView
            {
                Location = new Point(10, 30),
                Size = new Size(120, 55),
                BorderStyle = BorderStyle.FixedSingle
            };
            _trvPreview.Nodes.Add("Preset 1");
            _trvPreview.Nodes.Add("Preset 2");
            _trvPreview.Nodes[0].Nodes.Add("Sub item");

            _txtPreviewCode = new TextBox
            {
                Location = new Point(140, 30),
                Size = new Size(180, 55),
                Multiline = true,
                Font = new Font("Cascadia Code", 9.75f),
                Text = "echo \"Hello\"\nls -la",
                BorderStyle = BorderStyle.FixedSingle
            };

            _btnPreviewButton = new Button
            {
                Text = "Execute",
                Location = new Point(330, 30),
                Size = new Size(80, 28)
            };

            _pnlPreview.Controls.Add(_lblPreviewTitle);
            _pnlPreview.Controls.Add(_trvPreview);
            _pnlPreview.Controls.Add(_txtPreviewCode);
            _pnlPreview.Controls.Add(_btnPreviewButton);

            scrollPanel.Controls.Add(_pnlPreview);
            y += 130;

            // Reset appearance defaults button â€” inside the Appearance tab
            _btnResetDefaults = new Button
            {
                Text = "Reset Appearance to Defaults",
                Size = new Size(190, 28),
                Location = new Point(15, y)
            };
            _btnResetDefaults.Click += BtnResetDefaults_Click;
            scrollPanel.Controls.Add(_btnResetDefaults);

            tabAppearance.Controls.Add(scrollPanel);

            return tabAppearance;
        }

        private void RefreshScrollableFlowExtents()
        {
            foreach (var flow in _scrollableFlowPanels)
            {
                RefreshScrollableFlowExtent(flow);
            }
        }

        private static void RefreshScrollableFlowExtent(FlowLayoutPanel flow)
        {
            if (flow.IsDisposed)
            {
                return;
            }

            flow.PerformLayout();

            // AutoScrollPosition is negative when scrolled; normalize so extents
            // are stable regardless of current scroll offset.
            var scrollOffsetY = -flow.AutoScrollPosition.Y;

            int contentBottom = flow.Padding.Top;
            foreach (Control control in flow.Controls)
            {
                if (!control.Visible)
                {
                    continue;
                }

                int candidateBottom = control.Bottom + control.Margin.Bottom + scrollOffsetY;
                if (candidateBottom > contentBottom)
                {
                    contentBottom = candidateBottom;
                }
            }

            // Reserve a small buffer so the last row is never clipped by
            // rounding, DPI scaling, or scrollbar metrics.
            var targetHeight = contentBottom + flow.Padding.Bottom + 8;
            if (flow.AutoScrollMinSize.Height != targetHeight)
            {
                flow.AutoScrollMinSize = new Size(0, targetHeight);
            }
        }

        private static TableLayoutPanel CreateLabelSpinnerTable(int rows, int width)
        {
            var table = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            // Label columns auto-size to content, spinner columns are fixed width
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            for (int i = 0; i < rows; i++)
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return table;
        }

        private void AddTableRow(TableLayoutPanel table, int row,
            string label1, out NumericUpDown num1, decimal default1,
            string label2, out NumericUpDown num2, decimal default2)
        {
            var lbl1 = new Label { Text = label1, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 0) };
            num1 = CreateNumericUpDown(0, 0, default1, 7, 16, 0.5m, 1);
            num1.ValueChanged += (s, e) => UpdatePreview();
            table.Controls.Add(lbl1, 0, row);
            table.Controls.Add(num1, 1, row);

            var lbl2 = new Label { Text = label2, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 4, 0, 0) };
            num2 = CreateNumericUpDown(0, 0, default2, 7, 16, 0.5m, 1);
            num2.ValueChanged += (s, e) => UpdatePreview();
            table.Controls.Add(lbl2, 2, row);
            table.Controls.Add(num2, 3, row);
        }

        private static T FindControl<T>(Control parent, string name) where T : Control
        {
            if (parent.Controls[name] is T direct) return direct;
            foreach (Control child in parent.Controls)
            {
                var found = FindControl<T>(child, name);
                if (found != null) return found;
            }
            return null!;
        }

        private static NumericUpDown CreateNumericUpDown(int x, int y, decimal value, decimal min, decimal max, decimal increment, int decimalPlaces)
        {
            return new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(60, 23),
                Minimum = min,
                Maximum = max,
                Value = value,
                Increment = increment,
                DecimalPlaces = decimalPlaces
            };
        }

        private static string ResolveSemiboldFontFamily(string? uiFontFamily)
        {
            if (string.IsNullOrWhiteSpace(uiFontFamily))
            {
                return FontSettings.DefaultUIFontFamily;
            }

            return uiFontFamily.EndsWith("Semibold", StringComparison.OrdinalIgnoreCase)
                ? uiFontFamily
                : $"{uiFontFamily} Semibold";
        }

        private void UpdatePreview()
        {
            if (_pnlPreview == null || _lblPreviewTitle == null) return;

            try
            {
                var scale = _trkGlobalScale?.Value / 100f ?? 1f;
                var uiFont = _cboUIFont?.SelectedItem?.ToString() ?? FontSettings.DefaultUIFontFamily;
                var codeFont = _cboCodeFont?.SelectedItem?.ToString() ?? "Cascadia Code";
                var semiboldUiFont = ResolveSemiboldFontFamily(uiFont);

                var titleSize = (float)(_numSectionTitleSize?.Value ?? 9.5m) * scale;
                var treeSize = (float)(_numTreeViewSize?.Value ?? 9.5m) * scale;
                var codeSize = (float)(_numCodeEditorSize?.Value ?? 9.75m) * scale;
                var buttonSize = (float)(_numButtonFontSize?.Value ?? 9m) * scale;

                // Track fonts for disposal when the dialog closes.
                // Do NOT dispose previous fonts here â€” GDI+ may share native
                // handles between Font objects with identical parameters, so
                // disposing one can invalidate another that is still assigned
                // to a control whose window handle hasn't been created yet.
                var titleFont = new Font(semiboldUiFont, Math.Max(7f, titleSize), FontStyle.Bold);
                _previewFonts.Add(titleFont);
                _lblPreviewTitle.Font = titleFont;

                var treeFont = new Font(uiFont, Math.Max(7f, treeSize));
                _previewFonts.Add(treeFont);
                _trvPreview.Font = treeFont;

                var codePreviewFont = new Font(codeFont, Math.Max(7f, codeSize));
                _previewFonts.Add(codePreviewFont);
                _txtPreviewCode.Font = codePreviewFont;
                _txtPreviewCode.WordWrap = _chkCodeEditorWordWrap?.Checked ?? false;

                var buttonFont = new Font(uiFont, Math.Max(7f, buttonSize));
                _previewFonts.Add(buttonFont);
                _btnPreviewButton.Font = buttonFont;

                if (_chkUseCustomAccent?.Checked == true)
                {
                    _btnPreviewButton.BackColor = _customAccentColor;
                    _btnPreviewButton.ForeColor = GetContrastColor(_customAccentColor);
                }
                else
                {
                    _btnPreviewButton.BackColor = SystemColors.Control;
                    _btnPreviewButton.ForeColor = SystemColors.ControlText;
                }
            }
            catch (ArgumentException)
            {
                // Font creation can fail with invalid family names or extreme sizes;
                // safe to ignore since this only affects the live preview
            }
        }

        private static Color GetContrastColor(Color color)
        {
            var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return luminance > 0.5 ? Color.Black : Color.White;
        }

        private void BtnChooseAccentColor_Click(object? sender, EventArgs e)
        {
            using var colorDialog = new ColorDialog
            {
                Color = _customAccentColor,
                FullOpen = true
            };

            if (colorDialog.ShowDialog(this) == DialogResult.OK)
            {
                _customAccentColor = colorDialog.Color;
                _pnlAccentColor.BackColor = _customAccentColor;
                UpdatePreview();
            }
        }

        private void BtnResetDefaults_Click(object? sender, EventArgs e)
        {
            var result = DialogTheme.Show(
                this,
                "Reset all appearance settings to their default values?",
                "Reset to Defaults",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                var defaults = FontSettings.CreateDefault();
                ApplyFontSettingsToControls(defaults);
                UpdatePreview();
            }
        }

        private void BtnResetPresetTimeouts_Click(object? sender, EventArgs e)
        {
            if (_presetManager == null)
                return;

            var result = _promptService.Show(
                this,
                "Clear custom timeout values from all presets?\n\n" +
                "Presets will inherit the global default timeout instead.",
                "Reset Preset Timeouts",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                int count = _presetManager.ClearAllTimeouts();
                PresetTimeoutsWereCleared |= count > 0;
                _promptService.Show(
                    this,
                    $"Cleared timeout overrides from {count} preset(s).",
                    "Reset Preset Timeouts",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void BtnClearEmbeddedBrowserData_Click(object? sender, EventArgs e)
        {
            var result = _promptService.Show(
                this,
                "Clear SSH Helper's embedded browser data?\n\n" +
                "This resets SSH Helper's embedded-browser cookies, cache, local storage, IndexedDB, and related site data only.",
                "Clear Embedded Browser Data",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            var clearResult = _browserCallbackProfileManager.ClearEmbeddedBrowserData();
            switch (clearResult)
            {
                case EmbeddedBrowserDataClearResult.Cleared:
                    _promptService.Show(
                        this,
                        "Embedded browser data was cleared.",
                        "Clear Embedded Browser Data",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;

                case EmbeddedBrowserDataClearResult.ActiveSessionBlocked:
                    _promptService.Show(
                        this,
                        "Embedded browser data cannot be cleared while an embedded browser callback window is open.",
                        "Clear Embedded Browser Data",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;
            }
        }

        private void ApplyFontSettingsToControls(FontSettings settings)
        {
            SelectFontInComboBox(_cboUIFont, settings.UIFontFamily);
            SelectFontInComboBox(_cboCodeFont, settings.CodeFontFamily);

            _numSectionTitleSize.Value = (decimal)settings.SectionTitleFontSize;
            _numTreeViewSize.Value = (decimal)settings.TreeViewFontSize;
            _numEmptyLabelSize.Value = (decimal)settings.EmptyLabelFontSize;
            _numExecuteButtonSize.Value = (decimal)settings.ExecuteButtonFontSize;
            _numCodeEditorSize.Value = (decimal)settings.CodeEditorFontSize;
            _numOutputAreaSize.Value = (decimal)settings.OutputAreaFontSize;
            _numTabFontSize.Value = (decimal)settings.TabFontSize;
            _numButtonFontSize.Value = (decimal)settings.ButtonFontSize;
            _numHostListFontSize.Value = (decimal)settings.HostListFontSize;
            _numMenuFontSize.Value = (decimal)settings.MenuFontSize;
            _numStatusBarFontSize.Value = (decimal)settings.StatusBarFontSize;
            _numDialogFontSize.Value = (decimal)settings.DialogFontSize;
            _numScriptPromptFontSize.Value = (decimal)Math.Clamp(settings.ScriptPromptFontSize, 7f, 24f);

            _trkGlobalScale.Value = (int)(settings.GlobalScaleFactor * 100);
            _lblGlobalScaleValue.Text = $"{_trkGlobalScale.Value}%";

            _chkCodeEditorWordWrap.Checked = settings.CodeEditorWordWrap;
            _chkOutputAreaWordWrap.Checked = settings.OutputAreaWordWrap;
            _numTreeViewRowHeight.Value = settings.TreeViewRowHeight;
            _numHostListRowHeight.Value = settings.HostListRowHeight;

            _chkUseCustomAccent.Checked = settings.CustomAccentColor.HasValue;
            if (settings.CustomAccentColor.HasValue)
            {
                _customAccentColor = Color.FromArgb(settings.CustomAccentColor.Value);
                _pnlAccentColor.BackColor = _customAccentColor;
            }
            else
            {
                _customAccentColor = Color.FromArgb(0, 120, 215);
                _pnlAccentColor.BackColor = _customAccentColor;
            }
        }

        private void LoadSettings()
        {
            var config = _configService.GetCurrent();

            // General
            _chkRememberState.Checked = config.RememberState;
            _numMaxHistory.Value = Math.Clamp(config.MaxHistoryEntries, 1, 500);
            _numDefaultTimeout.Value = Math.Clamp(config.Timeout, 1, 300);
            _numConnectionTimeout.Value = Math.Clamp(config.ConnectionTimeout, 5, 120);
            _chkDarkMode.Checked = config.DarkMode;
            _chkAutoResizeHostColumns.Checked = config.AutoResizeHostColumns;
            _chkEnableSshConfig.Checked = config.SshConfig.EnableSshConfig;
            _chkUseConnectionPooling.Checked = config.UseConnectionPooling;
            _chkUseCredentialManager.Checked = config.Credentials.UseCredentialManager;
            _chkPreferSshAgent.Checked = config.Credentials.PreferSshAgent;

            // Updates
            _chkCheckForUpdatesOnStartup.Checked = config.UpdateSettings.CheckOnStartup;
            _chkEnableUpdateLog.Checked = config.UpdateSettings.EnableUpdateLog;

            // Command editor
            var editor = config.CommandEditor?.CloneNormalized() ?? new CommandEditorSettings();
            _chkEnableSyntaxHighlighting.Checked = editor.EnableSyntaxHighlighting;
            _chkEnableAutocomplete.Checked = editor.EnableAutocomplete;
            _chkAutocompleteShowOnTyping.Checked = editor.AutocompleteShowOnTyping;
            _chkEnableInlineValidation.Checked = editor.EnableInlineValidation;
            _numValidationDebounceMs.Value = editor.ValidationDebounceMs;
            _chkShowInlineWarnings.Checked = editor.ShowInlineWarnings;
            _chkEnableDiagnosticTooltips.Checked = editor.EnableDiagnosticTooltips;
            _chkEnableVariableInspectorTooltips.Checked = editor.EnableVariableInspectorTooltips;
            _chkEnableYamlHygieneWarnings.Checked = editor.EnableYamlHygieneWarnings;
            _chkUseSpacesForTab.Checked = editor.UseSpacesForTab;
            _numIndentSize.Value = editor.IndentSize;
            _chkEnableSmartEnter.Checked = editor.EnableSmartEnter;
            _chkPreserveBlankLineBetweenSteps.Checked = editor.PreserveBlankLineBetweenSteps;
            _chkEnableCurrentLineHighlight.Checked = editor.EnableCurrentLineHighlight;
            _chkEnableIndentGuides.Checked = editor.EnableIndentGuides;
            _chkShowWhitespace.Checked = editor.ShowWhitespace;
            _chkEnableLongLineGuide.Checked = editor.EnableLongLineGuide;
            _numLongLineColumn.Value = editor.LongLineColumn;
            _chkEnableCodeFolding.Checked = editor.EnableCodeFolding;
            _chkEnableBraceMatching.Checked = editor.EnableBraceMatching;

            // Appearance
            ApplyFontSettingsToControls(config.FontSettings);

            // Vault
            LoadVaultSettings();

            // Notifications
            LoadNotificationSettings();
        }

        private void PopulateFontComboBox(ComboBox comboBox, bool monospacedOnly)
        {
            var fonts = new List<string>();

            foreach (var family in System.Drawing.FontFamily.Families)
            {
                if (monospacedOnly)
                {
                    if (IsLikelyMonospaced(family.Name))
                    {
                        fonts.Add(family.Name);
                    }
                }
                else
                {
                    fonts.Add(family.Name);
                }
            }

            fonts.Sort();
            comboBox.Items.AddRange(fonts.ToArray());
        }

        private static bool IsLikelyMonospaced(string fontName)
        {
            var monoPatterns = new[] { "mono", "courier", "consolas", "cascadia", "fira code",
                "source code", "jetbrains", "hack", "menlo", "monaco", "lucida console",
                "dejavu sans mono", "ubuntu mono", "droid sans mono", "roboto mono",
                "inconsolata", "anonymous", "liberation mono", "noto mono", "sf mono" };

            var lowerName = fontName.ToLowerInvariant();
            return monoPatterns.Any(p => lowerName.Contains(p));
        }

        private static void SelectFontInComboBox(ComboBox comboBox, string fontName)
        {
            var index = comboBox.Items.IndexOf(fontName);
            if (index >= 0)
            {
                comboBox.SelectedIndex = index;
            }
            else if (comboBox.Items.Count > 0)
            {
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    if (comboBox.Items[i]?.ToString()?.StartsWith(fontName.Split(' ')[0], StringComparison.OrdinalIgnoreCase) == true)
                    {
                        comboBox.SelectedIndex = i;
                        return;
                    }
                }
                comboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Gets the current dark mode setting from the checkbox (for live preview).
        /// </summary>
        public bool IsDarkModeEnabled => _chkDarkMode.Checked;

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            PersistCurrentVaultProfile();
            if (!ValidateVaultProfiles())
                return;

            _configService.Update(config =>
            {
                // General
                config.RememberState = _chkRememberState.Checked;
                config.MaxHistoryEntries = (int)_numMaxHistory.Value;
                config.Timeout = (int)_numDefaultTimeout.Value;
                config.ConnectionTimeout = (int)_numConnectionTimeout.Value;
                config.DarkMode = _chkDarkMode.Checked;
                config.AutoResizeHostColumns = _chkAutoResizeHostColumns.Checked;
                config.SshConfig.EnableSshConfig = _chkEnableSshConfig.Checked;
                config.UseConnectionPooling = _chkUseConnectionPooling.Checked;
                config.Credentials.UseCredentialManager = _chkUseCredentialManager.Checked;
                config.Credentials.PreferSshAgent = _chkPreferSshAgent.Checked;

                // Updates
                config.UpdateSettings.CheckOnStartup = _chkCheckForUpdatesOnStartup.Checked;
                config.UpdateSettings.EnableUpdateLog = _chkEnableUpdateLog.Checked;

                // Command editor
                config.CommandEditor ??= new CommandEditorSettings();
                config.CommandEditor.EnableSyntaxHighlighting = _chkEnableSyntaxHighlighting.Checked;
                config.CommandEditor.EnableAutocomplete = _chkEnableAutocomplete.Checked;
                config.CommandEditor.AutocompleteShowOnTyping = _chkAutocompleteShowOnTyping.Checked;
                config.CommandEditor.EnableInlineValidation = _chkEnableInlineValidation.Checked;
                config.CommandEditor.ValidationDebounceMs = Math.Clamp(
                    (int)_numValidationDebounceMs.Value,
                    CommandEditorSettings.MinValidationDebounceMs,
                    CommandEditorSettings.MaxValidationDebounceMs);
                config.CommandEditor.ShowInlineWarnings = _chkShowInlineWarnings.Checked;
                config.CommandEditor.EnableDiagnosticTooltips = _chkEnableDiagnosticTooltips.Checked;
                config.CommandEditor.EnableVariableInspectorTooltips = _chkEnableVariableInspectorTooltips.Checked;
                config.CommandEditor.EnableYamlHygieneWarnings = _chkEnableYamlHygieneWarnings.Checked;
                config.CommandEditor.UseSpacesForTab = _chkUseSpacesForTab.Checked;
                config.CommandEditor.IndentSize = Math.Clamp(
                    (int)_numIndentSize.Value,
                    CommandEditorSettings.MinIndentSize,
                    CommandEditorSettings.MaxIndentSize);
                config.CommandEditor.EnableSmartEnter = _chkEnableSmartEnter.Checked;
                config.CommandEditor.PreserveBlankLineBetweenSteps = _chkPreserveBlankLineBetweenSteps.Checked;
                config.CommandEditor.EnableCurrentLineHighlight = _chkEnableCurrentLineHighlight.Checked;
                config.CommandEditor.EnableIndentGuides = _chkEnableIndentGuides.Checked;
                config.CommandEditor.ShowWhitespace = _chkShowWhitespace.Checked;
                config.CommandEditor.EnableLongLineGuide = _chkEnableLongLineGuide.Checked;
                config.CommandEditor.LongLineColumn = Math.Clamp(
                    (int)_numLongLineColumn.Value,
                    CommandEditorSettings.MinLongLineColumn,
                    CommandEditorSettings.MaxLongLineColumn);
                config.CommandEditor.EnableCodeFolding = _chkEnableCodeFolding.Checked;
                config.CommandEditor.EnableBraceMatching = _chkEnableBraceMatching.Checked;
                config.CommandEditor.Normalize();

                // Appearance - Font Families
                config.FontSettings.UIFontFamily = _cboUIFont.SelectedItem?.ToString() ?? FontSettings.DefaultUIFontFamily;
                config.FontSettings.CodeFontFamily = _cboCodeFont.SelectedItem?.ToString() ?? "Cascadia Code";

                // Appearance - Font Sizes (existing)
                config.FontSettings.SectionTitleFontSize = (float)_numSectionTitleSize.Value;
                config.FontSettings.TreeViewFontSize = (float)_numTreeViewSize.Value;
                config.FontSettings.EmptyLabelFontSize = (float)_numEmptyLabelSize.Value;
                config.FontSettings.ExecuteButtonFontSize = (float)_numExecuteButtonSize.Value;
                config.FontSettings.CodeEditorFontSize = (float)_numCodeEditorSize.Value;
                config.FontSettings.OutputAreaFontSize = (float)_numOutputAreaSize.Value;

                // Appearance - Font Sizes (new)
                config.FontSettings.TabFontSize = (float)_numTabFontSize.Value;
                config.FontSettings.ButtonFontSize = (float)_numButtonFontSize.Value;
                config.FontSettings.HostListFontSize = (float)_numHostListFontSize.Value;
                config.FontSettings.MenuFontSize = (float)_numMenuFontSize.Value;
                config.FontSettings.StatusBarFontSize = (float)_numStatusBarFontSize.Value;
                config.FontSettings.DialogFontSize = (float)_numDialogFontSize.Value;
                config.FontSettings.ScriptPromptFontSize = (float)_numScriptPromptFontSize.Value;

                // Appearance - Global Scale
                config.FontSettings.GlobalScaleFactor = _trkGlobalScale.Value / 100f;

                // Appearance - Layout
                config.FontSettings.CodeEditorWordWrap = _chkCodeEditorWordWrap.Checked;
                config.FontSettings.OutputAreaWordWrap = _chkOutputAreaWordWrap.Checked;
                config.FontSettings.TreeViewRowHeight = (int)_numTreeViewRowHeight.Value;
                config.FontSettings.HostListRowHeight = (int)_numHostListRowHeight.Value;

                // Appearance - Accent Color
                config.FontSettings.CustomAccentColor = _chkUseCustomAccent.Checked
                    ? _customAccentColor.ToArgb()
                    : null;

                // Vault
                SaveVaultSettings(config);

                // Notifications
                SaveNotificationSettings(config);
            });
        }

        private bool ValidateVaultProfiles()
        {
            if (!_chkVaultEnabled.Checked)
                return true;

            foreach (var profile in _vaultProfiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Name))
                {
                    _promptService.Show(this, "Vault profile name is required.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (profile.AuthMethod != VaultAuthMethod.Oidc)
                    continue;

                if (string.IsNullOrWhiteSpace(profile.OidcRole))
                {
                    _promptService.Show(this, $"Vault profile '{profile.Name}' requires an OIDC role.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (!VaultOidcCallbackSettings.TryCreate(
                        profile.OidcCallbackHost,
                        profile.OidcCallbackPort,
                        profile.OidcCallbackPath,
                        profile.Name,
                        out _,
                        out var validationError))
                {
                    _promptService.Show(
                        this,
                        validationError ?? "Vault profile has invalid OIDC callback settings.",
                        "Settings",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var font in _previewFonts)
                {
                    try { font.Dispose(); } catch { }
                }
            }
            base.Dispose(disposing);
        }
    }
}
