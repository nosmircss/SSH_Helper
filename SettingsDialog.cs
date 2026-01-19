using SSH_Helper.Models;
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
        private readonly CheckBox _chkDarkMode;

        // Updates tab controls
        private readonly CheckBox _chkCheckForUpdatesOnStartup;
        private readonly CheckBox _chkEnableUpdateLog;

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
        private NumericUpDown _numTooltipFontSize = null!;
        private NumericUpDown _numStatusBarFontSize = null!;

        // Appearance tab controls - Global Scale
        private TrackBar _trkGlobalScale = null!;
        private Label _lblGlobalScaleValue = null!;

        // Appearance tab controls - Layout
        private NumericUpDown _numCodeEditorLineSpacing = null!;
        private NumericUpDown _numOutputAreaLineSpacing = null!;
        private NumericUpDown _numTabWidth = null!;
        private CheckBox _chkCodeEditorWordWrap = null!;
        private CheckBox _chkOutputAreaWordWrap = null!;
        private NumericUpDown _numTreeViewRowHeight = null!;
        private NumericUpDown _numHostListRowHeight = null!;

        // Appearance tab controls - Icons
        private ComboBox _cboIconSize = null!;

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

        // Reset button
        private readonly Button _btnResetDefaults;

        private readonly Button _btnSave;
        private readonly Button _btnCancel;

        private Color _customAccentColor = Color.FromArgb(0, 120, 215);

        public SettingsDialog(ConfigurationService configService)
        {
            _configService = configService;

            Text = "Settings";
            Size = new Size(520, 620);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _tabControl = new TabControl
            {
                Location = new Point(12, 12),
                Size = new Size(480, 520),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            // === General Tab ===
            var tabGeneral = CreateGeneralTab();
            _tabControl.TabPages.Add(tabGeneral);

            // === Updates Tab ===
            var tabUpdates = CreateUpdatesTab();
            _tabControl.TabPages.Add(tabUpdates);

            // === Appearance Tab (with scrollable panel) ===
            var tabAppearance = CreateAppearanceTab();
            _tabControl.TabPages.Add(tabAppearance);

            // Buttons
            _btnResetDefaults = new Button
            {
                Text = "Reset to Defaults",
                Size = new Size(110, 28),
                Location = new Point(12, 545)
            };
            _btnResetDefaults.Click += BtnResetDefaults_Click;

            _btnSave = new Button
            {
                Text = "Save",
                Size = new Size(80, 28),
                Location = new Point(321, 545),
                DialogResult = DialogResult.OK
            };
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 28),
                Location = new Point(407, 545),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(_tabControl);
            Controls.Add(_btnResetDefaults);
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            // Initialize controls that need constructor
            _chkRememberState = (CheckBox)tabGeneral.Controls["chkRememberState"]!;
            _numMaxHistory = (NumericUpDown)tabGeneral.Controls["numMaxHistory"]!;
            _numDefaultTimeout = (NumericUpDown)tabGeneral.Controls["numDefaultTimeout"]!;
            _numConnectionTimeout = (NumericUpDown)tabGeneral.Controls["numConnectionTimeout"]!;
            _chkDarkMode = (CheckBox)tabGeneral.Controls["chkDarkMode"]!;

            _chkCheckForUpdatesOnStartup = (CheckBox)tabUpdates.Controls["chkCheckForUpdatesOnStartup"]!;
            _chkEnableUpdateLog = (CheckBox)tabUpdates.Controls["chkEnableUpdateLog"]!;

            LoadSettings();
            UpdatePreview();
        }

        private TabPage CreateGeneralTab()
        {
            var tabGeneral = new TabPage("General");

            var lblStateSection = new Label
            {
                Text = "Application State",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, 15),
                AutoSize = true
            };

            var chkRememberState = new CheckBox
            {
                Name = "chkRememberState",
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

            var numMaxHistory = new NumericUpDown
            {
                Name = "numMaxHistory",
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

            var numDefaultTimeout = new NumericUpDown
            {
                Name = "numDefaultTimeout",
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

            var numConnectionTimeout = new NumericUpDown
            {
                Name = "numConnectionTimeout",
                Location = new Point(250, 163),
                Size = new Size(80, 23),
                Minimum = 5,
                Maximum = 120,
                Value = 30
            };

            var lblAppearanceSection = new Label
            {
                Text = "Theme",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, 200),
                AutoSize = true
            };

            var chkDarkMode = new CheckBox
            {
                Name = "chkDarkMode",
                Text = "Dark mode (output window is always dark)",
                Location = new Point(15, 225),
                AutoSize = true
            };

            tabGeneral.Controls.Add(lblStateSection);
            tabGeneral.Controls.Add(chkRememberState);
            tabGeneral.Controls.Add(lblMaxHistory);
            tabGeneral.Controls.Add(numMaxHistory);
            tabGeneral.Controls.Add(lblDefaultsSection);
            tabGeneral.Controls.Add(lblDefaultTimeout);
            tabGeneral.Controls.Add(numDefaultTimeout);
            tabGeneral.Controls.Add(lblConnectionTimeout);
            tabGeneral.Controls.Add(numConnectionTimeout);
            tabGeneral.Controls.Add(lblAppearanceSection);
            tabGeneral.Controls.Add(chkDarkMode);

            return tabGeneral;
        }

        private TabPage CreateUpdatesTab()
        {
            var tabUpdates = new TabPage("Updates");

            var lblUpdateSection = new Label
            {
                Text = "Automatic Updates",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, 15),
                AutoSize = true
            };

            var chkCheckForUpdatesOnStartup = new CheckBox
            {
                Name = "chkCheckForUpdatesOnStartup",
                Text = "Check for updates when application starts",
                Location = new Point(15, 40),
                AutoSize = true
            };

            var lblTroubleshooting = new Label
            {
                Text = "Troubleshooting",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, 80),
                AutoSize = true
            };

            var chkEnableUpdateLog = new CheckBox
            {
                Name = "chkEnableUpdateLog",
                Text = "Enable update log file (for troubleshooting update failures)",
                Location = new Point(15, 105),
                AutoSize = true
            };

            var lblLogPath = new Label
            {
                Text = "Log file: %TEMP%\\SSH_Helper_Update\\update.log",
                Location = new Point(32, 128),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8f)
            };

            tabUpdates.Controls.Add(lblUpdateSection);
            tabUpdates.Controls.Add(chkCheckForUpdatesOnStartup);
            tabUpdates.Controls.Add(lblTroubleshooting);
            tabUpdates.Controls.Add(chkEnableUpdateLog);
            tabUpdates.Controls.Add(lblLogPath);

            return tabUpdates;
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

            // Row 1: Section titles, Tree views
            AddFontSizeRow(scrollPanel, ref y, "Section titles:", out _numSectionTitleSize, "Tree views:", out _numTreeViewSize);

            // Row 2: Empty labels, Execute buttons
            AddFontSizeRow(scrollPanel, ref y, "Empty labels:", out _numEmptyLabelSize, "Execute buttons:", out _numExecuteButtonSize);

            // Row 3: Code editor, Output area
            AddFontSizeRow(scrollPanel, ref y, "Code editor:", out _numCodeEditorSize, "Output area:", out _numOutputAreaSize);

            // Row 4: Tab headers, General buttons
            AddFontSizeRow(scrollPanel, ref y, "Tab headers:", out _numTabFontSize, "Buttons:", out _numButtonFontSize);

            // Row 5: Host list, Menus
            AddFontSizeRow(scrollPanel, ref y, "Host list:", out _numHostListFontSize, "Menus:", out _numMenuFontSize);

            // Row 6: Tooltips, Status bar
            AddFontSizeRow(scrollPanel, ref y, "Tooltips:", out _numTooltipFontSize, "Status bar:", out _numStatusBarFontSize);

            y += 10;

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

            // Line spacing row
            var lblCodeLineSpacing = new Label { Text = "Code line spacing:", Location = new Point(15, y), AutoSize = true };
            _numCodeEditorLineSpacing = CreateNumericUpDown(120, y - 2, 1.0m, 1.0m, 2.0m, 0.1m, 1);
            _numCodeEditorLineSpacing.ValueChanged += (s, e) => UpdatePreview();

            var lblOutputLineSpacing = new Label { Text = "Output line spacing:", Location = new Point(220, y), AutoSize = true };
            _numOutputAreaLineSpacing = CreateNumericUpDown(345, y - 2, 1.0m, 1.0m, 2.0m, 0.1m, 1);
            _numOutputAreaLineSpacing.ValueChanged += (s, e) => UpdatePreview();

            scrollPanel.Controls.Add(lblCodeLineSpacing);
            scrollPanel.Controls.Add(_numCodeEditorLineSpacing);
            scrollPanel.Controls.Add(lblOutputLineSpacing);
            scrollPanel.Controls.Add(_numOutputAreaLineSpacing);
            y += 30;

            // Tab width
            var lblTabWidth = new Label { Text = "Tab width (spaces):", Location = new Point(15, y), AutoSize = true };
            _numTabWidth = new NumericUpDown
            {
                Location = new Point(135, y - 2),
                Size = new Size(50, 23),
                Minimum = 1,
                Maximum = 8,
                Value = 4
            };
            scrollPanel.Controls.Add(lblTabWidth);
            scrollPanel.Controls.Add(_numTabWidth);
            y += 30;

            // Word wrap checkboxes
            _chkCodeEditorWordWrap = new CheckBox
            {
                Text = "Word wrap in code editor",
                Location = new Point(15, y),
                AutoSize = true
            };
            _chkCodeEditorWordWrap.CheckedChanged += (s, e) => UpdatePreview();

            _chkOutputAreaWordWrap = new CheckBox
            {
                Text = "Word wrap in output area",
                Location = new Point(220, y),
                AutoSize = true,
                Checked = false
            };
            _chkOutputAreaWordWrap.CheckedChanged += (s, e) => UpdatePreview();

            scrollPanel.Controls.Add(_chkCodeEditorWordWrap);
            scrollPanel.Controls.Add(_chkOutputAreaWordWrap);
            y += 30;

            // Row heights
            var lblTreeViewRowHeight = new Label { Text = "Tree row height (0=auto):", Location = new Point(15, y), AutoSize = true };
            _numTreeViewRowHeight = new NumericUpDown
            {
                Location = new Point(155, y - 2),
                Size = new Size(50, 23),
                Minimum = 0,
                Maximum = 50,
                Value = 0
            };

            var lblHostListRowHeight = new Label { Text = "Host list row height:", Location = new Point(220, y), AutoSize = true };
            _numHostListRowHeight = new NumericUpDown
            {
                Location = new Point(350, y - 2),
                Size = new Size(50, 23),
                Minimum = 0,
                Maximum = 50,
                Value = 0
            };

            scrollPanel.Controls.Add(lblTreeViewRowHeight);
            scrollPanel.Controls.Add(_numTreeViewRowHeight);
            scrollPanel.Controls.Add(lblHostListRowHeight);
            scrollPanel.Controls.Add(_numHostListRowHeight);
            y += 35;

            // === Icons Section ===
            var lblIconsSection = new Label
            {
                Text = "Icons",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(15, y),
                AutoSize = true
            };
            scrollPanel.Controls.Add(lblIconsSection);
            y += 25;

            var lblIconSize = new Label { Text = "Icon size:", Location = new Point(15, y), AutoSize = true };
            _cboIconSize = new ComboBox
            {
                Location = new Point(80, y - 3),
                Size = new Size(100, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cboIconSize.Items.AddRange(new object[] { "Small (16px)", "Medium (24px)", "Large (32px)" });
            _cboIconSize.SelectedIndex = 0;
            scrollPanel.Controls.Add(lblIconSize);
            scrollPanel.Controls.Add(_cboIconSize);
            y += 35;

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

            tabAppearance.Controls.Add(scrollPanel);

            return tabAppearance;
        }

        private void AddFontSizeRow(Panel panel, ref int y, string label1, out NumericUpDown num1, string label2, out NumericUpDown num2)
        {
            var lbl1 = new Label { Text = label1, Location = new Point(15, y), AutoSize = true };
            num1 = CreateNumericUpDown(120, y - 2, 9.5m, 7, 16, 0.5m, 1);
            num1.ValueChanged += (s, e) => UpdatePreview();

            var lbl2 = new Label { Text = label2, Location = new Point(220, y), AutoSize = true };
            num2 = CreateNumericUpDown(345, y - 2, 9.5m, 7, 16, 0.5m, 1);
            num2.ValueChanged += (s, e) => UpdatePreview();

            panel.Controls.Add(lbl1);
            panel.Controls.Add(num1);
            panel.Controls.Add(lbl2);
            panel.Controls.Add(num2);

            y += 28;
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

        private void UpdatePreview()
        {
            if (_pnlPreview == null || _lblPreviewTitle == null) return;

            try
            {
                var scale = _trkGlobalScale?.Value / 100f ?? 1f;
                var uiFont = _cboUIFont?.SelectedItem?.ToString() ?? "Segoe UI";
                var codeFont = _cboCodeFont?.SelectedItem?.ToString() ?? "Cascadia Code";

                var titleSize = (float)(_numSectionTitleSize?.Value ?? 9.5m) * scale;
                var treeSize = (float)(_numTreeViewSize?.Value ?? 9.5m) * scale;
                var codeSize = (float)(_numCodeEditorSize?.Value ?? 9.75m) * scale;
                var buttonSize = (float)(_numButtonFontSize?.Value ?? 9m) * scale;

                _lblPreviewTitle.Font = new Font(uiFont + " Semibold", Math.Max(7f, titleSize), FontStyle.Bold);
                _trvPreview.Font = new Font(uiFont, Math.Max(7f, treeSize));
                _txtPreviewCode.Font = new Font(codeFont, Math.Max(7f, codeSize));
                _txtPreviewCode.WordWrap = _chkCodeEditorWordWrap?.Checked ?? false;
                _btnPreviewButton.Font = new Font(uiFont, Math.Max(7f, buttonSize));

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
            catch
            {
                // Ignore font errors during preview
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

            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                _customAccentColor = colorDialog.Color;
                _pnlAccentColor.BackColor = _customAccentColor;
                UpdatePreview();
            }
        }

        private void BtnResetDefaults_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
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
            _numTooltipFontSize.Value = (decimal)settings.TooltipFontSize;
            _numStatusBarFontSize.Value = (decimal)settings.StatusBarFontSize;

            _trkGlobalScale.Value = (int)(settings.GlobalScaleFactor * 100);
            _lblGlobalScaleValue.Text = $"{_trkGlobalScale.Value}%";

            _numCodeEditorLineSpacing.Value = (decimal)settings.CodeEditorLineSpacing;
            _numOutputAreaLineSpacing.Value = (decimal)settings.OutputAreaLineSpacing;
            _numTabWidth.Value = settings.TabWidth;
            _chkCodeEditorWordWrap.Checked = settings.CodeEditorWordWrap;
            _chkOutputAreaWordWrap.Checked = settings.OutputAreaWordWrap;
            _numTreeViewRowHeight.Value = settings.TreeViewRowHeight;
            _numHostListRowHeight.Value = settings.HostListRowHeight;

            _cboIconSize.SelectedIndex = settings.IconSize switch
            {
                IconSize.Small => 0,
                IconSize.Medium => 1,
                IconSize.Large => 2,
                _ => 0
            };

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

            // Updates
            _chkCheckForUpdatesOnStartup.Checked = config.UpdateSettings.CheckOnStartup;
            _chkEnableUpdateLog.Checked = config.UpdateSettings.EnableUpdateLog;

            // Appearance
            ApplyFontSettingsToControls(config.FontSettings);
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
            _configService.Update(config =>
            {
                // General
                config.RememberState = _chkRememberState.Checked;
                config.MaxHistoryEntries = (int)_numMaxHistory.Value;
                config.Timeout = (int)_numDefaultTimeout.Value;
                config.ConnectionTimeout = (int)_numConnectionTimeout.Value;
                config.DarkMode = _chkDarkMode.Checked;

                // Updates
                config.UpdateSettings.CheckOnStartup = _chkCheckForUpdatesOnStartup.Checked;
                config.UpdateSettings.EnableUpdateLog = _chkEnableUpdateLog.Checked;

                // Appearance - Font Families
                config.FontSettings.UIFontFamily = _cboUIFont.SelectedItem?.ToString() ?? "Segoe UI";
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
                config.FontSettings.TooltipFontSize = (float)_numTooltipFontSize.Value;
                config.FontSettings.StatusBarFontSize = (float)_numStatusBarFontSize.Value;

                // Appearance - Global Scale
                config.FontSettings.GlobalScaleFactor = _trkGlobalScale.Value / 100f;

                // Appearance - Layout
                config.FontSettings.CodeEditorLineSpacing = (float)_numCodeEditorLineSpacing.Value;
                config.FontSettings.OutputAreaLineSpacing = (float)_numOutputAreaLineSpacing.Value;
                config.FontSettings.TabWidth = (int)_numTabWidth.Value;
                config.FontSettings.CodeEditorWordWrap = _chkCodeEditorWordWrap.Checked;
                config.FontSettings.OutputAreaWordWrap = _chkOutputAreaWordWrap.Checked;
                config.FontSettings.TreeViewRowHeight = (int)_numTreeViewRowHeight.Value;
                config.FontSettings.HostListRowHeight = (int)_numHostListRowHeight.Value;

                // Appearance - Icons
                config.FontSettings.IconSize = _cboIconSize.SelectedIndex switch
                {
                    0 => IconSize.Small,
                    1 => IconSize.Medium,
                    2 => IconSize.Large,
                    _ => IconSize.Small
                };

                // Appearance - Accent Color
                config.FontSettings.CustomAccentColor = _chkUseCustomAccent.Checked
                    ? _customAccentColor.ToArgb()
                    : null;
            });
        }
    }
}
