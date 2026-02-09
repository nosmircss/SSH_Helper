using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;

namespace SSH_Helper
{
    /// <summary>
    /// Settings dialog for application preferences.
    /// </summary>
    internal sealed class SettingsDialog : Form
    {
        private readonly ConfigurationService _configService;

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

        // Reset button (lives inside the Appearance tab)
        private Button _btnResetDefaults = null!;

        private readonly Button _btnSave;
        private readonly Button _btnCancel;

        private Color _customAccentColor = Color.FromArgb(0, 120, 215);
        private List<Font> _previewFonts = new();

        public SettingsDialog(ConfigurationService configService, bool darkMode = false)
        {
            _configService = configService;

            // Enable DPI scaling - must be set before any Size/Location values
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;

            Text = "Settings";
            Size = new Size(520, 620);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _tabControl = new BorderlessTabControl
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

            // === Command Editor Tab ===
            var tabCommandEditor = CreateCommandEditorTab();
            _tabControl.TabPages.Add(tabCommandEditor);

            // === Appearance Tab (with scrollable panel) ===
            var tabAppearance = CreateAppearanceTab();
            _tabControl.TabPages.Add(tabAppearance);

            // Buttons
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
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            // Initialize controls — use recursive find since controls are nested in layout panels
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

            LoadSettings();
            UpdatePreview();
            ApplyDialogTheme(darkMode);
        }

        private void ApplyDialogTheme(bool darkMode)
        {
            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.StyleButton(_btnSave, darkMode, isPrimary: true);
            DialogTheme.StyleButton(_btnCancel, darkMode);
            DialogTheme.StyleButton(_btnResetDefaults, darkMode);
            DialogTheme.StyleButton(_btnChooseAccentColor, darkMode);
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
            flow.Controls.Add(new CheckBox { Name = "chkUseCredentialManager", Text = "Store passwords in Windows Credential Manager", AutoSize = true });
            flow.Controls.Add(new CheckBox { Name = "chkPreferSshAgent", Text = "Prefer SSH agent when available", AutoSize = true });

            tabGeneral.Controls.Add(flow);
            return tabGeneral;
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

            var fontSizesTable = CreateLabelSpinnerTable(6, scrollPanel.ClientSize.Width - 30);
            fontSizesTable.Location = new Point(15, y);
            AddTableRow(fontSizesTable, 0, "Section titles:", out _numSectionTitleSize, 9.5m, "Tree views:", out _numTreeViewSize, 9.5m);
            AddTableRow(fontSizesTable, 1, "Empty labels:", out _numEmptyLabelSize, 9.5m, "Execute buttons:", out _numExecuteButtonSize, 9.5m);
            AddTableRow(fontSizesTable, 2, "Code editor:", out _numCodeEditorSize, 9.75m, "Output area:", out _numOutputAreaSize, 9.75m);
            AddTableRow(fontSizesTable, 3, "Tab headers:", out _numTabFontSize, 9m, "Buttons:", out _numButtonFontSize, 9m);
            AddTableRow(fontSizesTable, 4, "Host list:", out _numHostListFontSize, 9m, "Menus:", out _numMenuFontSize, 9m);
            AddTableRow(fontSizesTable, 5, "Status bar:", out _numStatusBarFontSize, 9m, "Dialogs:", out _numDialogFontSize, 9m);
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

            // Reset appearance defaults button — inside the Appearance tab
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

                // Track fonts for disposal when the dialog closes.
                // Do NOT dispose previous fonts here — GDI+ may share native
                // handles between Font objects with identical parameters, so
                // disposing one can invalidate another that is still assigned
                // to a control whose window handle hasn't been created yet.
                var titleFont = new Font(uiFont + " Semibold", Math.Max(7f, titleSize), FontStyle.Bold);
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
            _numStatusBarFontSize.Value = (decimal)settings.StatusBarFontSize;
            _numDialogFontSize.Value = (decimal)settings.DialogFontSize;

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
                config.CommandEditor.Normalize();

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
                config.FontSettings.StatusBarFontSize = (float)_numStatusBarFontSize.Value;
                config.FontSettings.DialogFontSize = (float)_numDialogFontSize.Value;

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
            });
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
