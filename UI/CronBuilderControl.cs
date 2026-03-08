using SSH_Helper.Services;

namespace SSH_Helper.UI
{
    /// <summary>
    /// Visual cron expression builder UserControl with preset template buttons,
    /// per-field ComboBox dropdowns, editable raw text field with bidirectional sync,
    /// inline human-readable description, and next-run preview.
    /// </summary>
    public sealed class CronBuilderControl : UserControl
    {
        #region Constants

        private const string CustomIndicator = "Custom";

        /// <summary>
        /// Preset templates: (label, cron expression).
        /// Locked by user decision in RESEARCH.md.
        /// </summary>
        private static readonly (string Label, string Expression)[] Presets =
        {
            ("Every 5 min",    "*/5 * * * *"),
            ("Every 15 min",   "*/15 * * * *"),
            ("Every 30 min",   "*/30 * * * *"),
            ("Hourly",         "0 * * * *"),
            ("Daily midnight", "0 0 * * *"),
            ("Daily 3 AM",     "0 3 * * *"),
            ("Weekdays 9 AM",  "0 9 * * 1-5"),
            ("Weekly Monday",  "0 0 * * 1"),
            ("Monthly 1st",    "0 0 1 * *"),
            ("Quarterly",      "0 0 1 1,4,7,10 *"),
        };

        /// <summary>Minute dropdown items.</summary>
        internal static readonly string[] MinuteItems = BuildMinuteItems();

        /// <summary>Hour dropdown items.</summary>
        internal static readonly string[] HourItems = BuildHourItems();

        /// <summary>Day of Month dropdown items.</summary>
        internal static readonly string[] DayOfMonthItems = BuildDayOfMonthItems();

        /// <summary>Month dropdown items.</summary>
        internal static readonly string[] MonthItems = BuildMonthItems();

        /// <summary>Day of Week dropdown items.</summary>
        internal static readonly string[] DayOfWeekItems = BuildDayOfWeekItems();

        #endregion

        #region Fields

        private SchedulingService? _schedulingService;
        private bool _suppressSyncEvents;
        private bool _isRefreshingLayout;

        // Controls
        private readonly Label _lblPresets;
        private readonly FlowLayoutPanel _presetPanel;
        private readonly Label _lblFields;
        private readonly TableLayoutPanel _dropdownPanel;
        private readonly ComboBox _cboMinute;
        private readonly ComboBox _cboHour;
        private readonly ComboBox _cboDayOfMonth;
        private readonly ComboBox _cboMonth;
        private readonly ComboBox _cboDayOfWeek;
        private readonly Label _lblExpression;
        private readonly TextBox _txtRawExpression;
        private readonly Label _lblDescription;
        private readonly Label _lblNextRun;
        private readonly Label _lblValidation;

        #endregion

        #region Events

        /// <summary>
        /// Fires when the cron expression changes via any path (preset, dropdown, raw text).
        /// </summary>
        public event EventHandler? CronExpressionChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the current cron expression.
        /// Setting triggers full UI update (dropdowns, description, next-run).
        /// </summary>
        public string CronExpression
        {
            get => _txtRawExpression.Text.Trim();
            set
            {
                if (_suppressSyncEvents) return;
                _suppressSyncEvents = true;
                try
                {
                    _txtRawExpression.Text = value ?? string.Empty;
                    SyncDropdownsFromRawText();
                    UpdateDescriptionAndNextRun();
                }
                finally
                {
                    _suppressSyncEvents = false;
                }
                CronExpressionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Constructor

        public CronBuilderControl()
        {
            // Initialize controls
            _lblPresets = new Label();
            _presetPanel = new FlowLayoutPanel();
            _lblFields = new Label();
            _dropdownPanel = new TableLayoutPanel();
            _cboMinute = new ComboBox();
            _cboHour = new ComboBox();
            _cboDayOfMonth = new ComboBox();
            _cboMonth = new ComboBox();
            _cboDayOfWeek = new ComboBox();
            _lblExpression = new Label();
            _txtRawExpression = new TextBox();
            _lblDescription = new Label();
            _lblNextRun = new Label();
            _lblValidation = new Label();

            SuspendLayout();
            BuildLayout();
            WireEvents();
            ResumeLayout(true);
            RefreshMeasuredLayout();
        }

        #endregion

        #region Layout

        private void BuildLayout()
        {
            AutoScroll = true;
            Padding = new Padding(8);

            var yPos = 8;

            // 1. Preset buttons row
            BuildPresetPanel(ref yPos);

            // 2. Dropdown selectors row
            BuildDropdownPanel(ref yPos);

            // 3. Raw expression row
            BuildRawExpressionRow(ref yPos);

            // 4. Description label
            BuildDescriptionLabel(ref yPos);

            // 5. Next-run preview label
            BuildNextRunLabel(ref yPos);

            // 6. Validation label
            BuildValidationLabel(ref yPos);

            MinimumSize = new Size(460, yPos + 8);
        }

        private void BuildPresetPanel(ref int yPos)
        {
            _lblPresets.Text = "Presets:";
            _lblPresets.AutoSize = true;
            _lblPresets.Location = new Point(8, yPos);
            _lblPresets.Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);
            Controls.Add(_lblPresets);
            yPos += _lblPresets.Height + 4;

            _presetPanel.Location = new Point(8, yPos);
            _presetPanel.Size = new Size(Width - 24, 32);
            _presetPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _presetPanel.WrapContents = true;
            _presetPanel.AutoSize = false;
            _presetPanel.AutoScroll = false;
            _presetPanel.Margin = Padding.Empty;
            _presetPanel.Padding = Padding.Empty;

            foreach (var (label, _) in Presets)
            {
                var btn = new Button
                {
                    Text = label,
                    AutoSize = true,
                    Padding = new Padding(4, 2, 4, 2),
                    Margin = new Padding(2),
                    Tag = label,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                };
                btn.FlatAppearance.BorderSize = 1;
                btn.Click += PresetButton_Click;
                _presetPanel.Controls.Add(btn);
            }

            Controls.Add(_presetPanel);
            yPos += _presetPanel.Height + 8;
        }

        private void BuildDropdownPanel(ref int yPos)
        {
            _lblFields.Text = "Fields:";
            _lblFields.AutoSize = true;
            _lblFields.Location = new Point(8, yPos);
            _lblFields.Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);
            Controls.Add(_lblFields);
            yPos += _lblFields.Height + 4;

            _dropdownPanel.Location = new Point(8, yPos);
            _dropdownPanel.Size = new Size(Width - 24, 52);
            _dropdownPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _dropdownPanel.ColumnCount = 5;
            _dropdownPanel.RowCount = 2;
            _dropdownPanel.Margin = Padding.Empty;
            _dropdownPanel.Padding = Padding.Empty;

            // Equal column widths
            for (var i = 0; i < 5; i++)
            {
                _dropdownPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            }
            _dropdownPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _dropdownPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Labels
            string[] labels = { "Minute", "Hour", "Day (Month)", "Month", "Day (Week)" };
            for (var i = 0; i < labels.Length; i++)
            {
                var lbl = new Label
                {
                    Text = labels[i],
                    AutoSize = true,
                    Margin = new Padding(2),
                    TextAlign = ContentAlignment.BottomLeft,
                };
                _dropdownPanel.Controls.Add(lbl, i, 0);
            }

            // ComboBoxes
            ConfigureDropdown(_cboMinute, MinuteItems);
            ConfigureDropdown(_cboHour, HourItems);
            ConfigureDropdown(_cboDayOfMonth, DayOfMonthItems);
            ConfigureDropdown(_cboMonth, MonthItems);
            ConfigureDropdown(_cboDayOfWeek, DayOfWeekItems);

            _dropdownPanel.Controls.Add(_cboMinute, 0, 1);
            _dropdownPanel.Controls.Add(_cboHour, 1, 1);
            _dropdownPanel.Controls.Add(_cboDayOfMonth, 2, 1);
            _dropdownPanel.Controls.Add(_cboMonth, 3, 1);
            _dropdownPanel.Controls.Add(_cboDayOfWeek, 4, 1);

            Controls.Add(_dropdownPanel);
            yPos += _dropdownPanel.Height + 8;
        }

        private static void ConfigureDropdown(ComboBox cbo, string[] items)
        {
            cbo.DropDownStyle = ComboBoxStyle.DropDownList;
            cbo.Items.AddRange(items);
            cbo.Items.Add(CustomIndicator);
            cbo.SelectedIndex = 0; // default to "*"
            cbo.Dock = DockStyle.Fill;
            cbo.Margin = new Padding(2);
        }

        private void BuildRawExpressionRow(ref int yPos)
        {
            _lblExpression.Text = "Expression:";
            _lblExpression.AutoSize = true;
            _lblExpression.Location = new Point(8, yPos);
            _lblExpression.Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);
            Controls.Add(_lblExpression);
            yPos += _lblExpression.Height + 4;

            _txtRawExpression.Location = new Point(8, yPos);
            _txtRawExpression.Size = new Size(Width - 24, 23);
            _txtRawExpression.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtRawExpression.Font = new Font("Consolas", 10f);
            _txtRawExpression.PlaceholderText = "e.g. */5 * * * *";
            Controls.Add(_txtRawExpression);
            yPos += _txtRawExpression.Height + 8;
        }

        private void BuildDescriptionLabel(ref int yPos)
        {
            _lblDescription.Location = new Point(8, yPos);
            _lblDescription.Size = new Size(Width - 24, 20);
            _lblDescription.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _lblDescription.AutoEllipsis = true;
            _lblDescription.ForeColor = Color.FromArgb(108, 117, 125);
            _lblDescription.Text = string.Empty;
            Controls.Add(_lblDescription);
            yPos += _lblDescription.Height + 4;
        }

        private void BuildNextRunLabel(ref int yPos)
        {
            _lblNextRun.Location = new Point(8, yPos);
            _lblNextRun.Size = new Size(Width - 24, 20);
            _lblNextRun.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _lblNextRun.AutoEllipsis = true;
            _lblNextRun.ForeColor = Color.FromArgb(108, 117, 125);
            _lblNextRun.Text = "Next run: --";
            Controls.Add(_lblNextRun);
            yPos += _lblNextRun.Height + 4;
        }

        private void BuildValidationLabel(ref int yPos)
        {
            _lblValidation.Location = new Point(8, yPos);
            _lblValidation.Size = new Size(Width - 24, 20);
            _lblValidation.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _lblValidation.AutoEllipsis = true;
            _lblValidation.ForeColor = Color.Red;
            _lblValidation.Text = string.Empty;
            _lblValidation.Visible = false;
            Controls.Add(_lblValidation);
            yPos += _lblValidation.Height + 4;
        }

        private void RefreshMeasuredLayout()
        {
            if (_isRefreshingLayout || IsDisposed)
            {
                return;
            }

            var contentWidth = ClientSize.Width - Padding.Horizontal;
            if (contentWidth <= 0)
            {
                return;
            }

            _isRefreshingLayout = true;
            SuspendLayout();

            try
            {
                var x = Padding.Left;
                var y = Padding.Top;

                y = LayoutSectionLabel(_lblPresets, x, y, contentWidth);

                _presetPanel.Location = new Point(x, y);
                _presetPanel.Width = contentWidth;
                _presetPanel.Height = 1;
                _presetPanel.PerformLayout();
                _presetPanel.Height = MeasureFlowLayoutHeight(_presetPanel);
                y += _presetPanel.Height + 8;

                y = LayoutSectionLabel(_lblFields, x, y, contentWidth);

                _dropdownPanel.Location = new Point(x, y);
                _dropdownPanel.Width = contentWidth;
                _dropdownPanel.Height = Math.Max(_dropdownPanel.GetPreferredSize(new Size(contentWidth, 0)).Height, 52);
                y += _dropdownPanel.Height + 8;

                y = LayoutSectionLabel(_lblExpression, x, y, contentWidth);

                _txtRawExpression.Location = new Point(x, y);
                _txtRawExpression.Width = contentWidth;
                _txtRawExpression.Height = Math.Max(_txtRawExpression.PreferredHeight, 23);
                y += _txtRawExpression.Height + 8;

                y = LayoutSingleLineLabel(_lblDescription, x, y, contentWidth, reserveSpaceWhenHidden: true);
                y = LayoutSingleLineLabel(_lblNextRun, x, y, contentWidth, reserveSpaceWhenHidden: true);
                y = LayoutSingleLineLabel(_lblValidation, x, y, contentWidth, reserveSpaceWhenHidden: false);

                var contentHeight = y + Padding.Bottom + 8;
                if (AutoScrollMinSize.Height != contentHeight)
                {
                    AutoScrollMinSize = new Size(0, contentHeight);
                }

                if (MinimumSize.Width != 460 || MinimumSize.Height != contentHeight)
                {
                    MinimumSize = new Size(460, contentHeight);
                }

                if (Height != contentHeight)
                {
                    Height = contentHeight;
                }
            }
            finally
            {
                ResumeLayout(true);
                _isRefreshingLayout = false;
            }
        }

        private static int LayoutSectionLabel(Label label, int x, int y, int width)
        {
            label.Location = new Point(x, y);
            var size = label.GetPreferredSize(new Size(width, 0));
            label.Size = size;
            return y + size.Height + 4;
        }

        private static int LayoutSingleLineLabel(
            Label label,
            int x,
            int y,
            int width,
            bool reserveSpaceWhenHidden)
        {
            label.Location = new Point(x, y);

            if (!label.Visible && !reserveSpaceWhenHidden)
            {
                label.Size = new Size(width, 0);
                return y;
            }

            var height = Math.Max(TextRenderer.MeasureText("Ag", label.Font).Height + 2, 20);
            label.Size = new Size(width, height);
            return y + height + 4;
        }

        private static int MeasureFlowLayoutHeight(FlowLayoutPanel flow)
        {
            var contentBottom = 0;
            foreach (Control control in flow.Controls)
            {
                if (!control.Visible)
                {
                    continue;
                }

                var candidateBottom = control.Bottom + control.Margin.Bottom;
                if (candidateBottom > contentBottom)
                {
                    contentBottom = candidateBottom;
                }
            }

            return Math.Max(contentBottom, 0);
        }

        #endregion

        #region Event Wiring

        private void WireEvents()
        {
            _cboMinute.SelectedIndexChanged += Dropdown_SelectedIndexChanged;
            _cboHour.SelectedIndexChanged += Dropdown_SelectedIndexChanged;
            _cboDayOfMonth.SelectedIndexChanged += Dropdown_SelectedIndexChanged;
            _cboMonth.SelectedIndexChanged += Dropdown_SelectedIndexChanged;
            _cboDayOfWeek.SelectedIndexChanged += Dropdown_SelectedIndexChanged;

            _txtRawExpression.TextChanged += RawExpression_TextChanged;
        }

        #endregion

        #region Event Handlers

        private void PresetButton_Click(object? sender, EventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string presetName) return;

            var expression = GetPresetExpression(presetName);
            if (expression == null) return;

            // Setting via property handles all sync
            CronExpression = expression;
        }

        private void Dropdown_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressSyncEvents) return;

            // If any dropdown is "Custom", we can't build expression from dropdowns
            var min = _cboMinute.SelectedItem?.ToString() ?? "*";
            var hour = _cboHour.SelectedItem?.ToString() ?? "*";
            var dom = _cboDayOfMonth.SelectedItem?.ToString() ?? "*";
            var month = _cboMonth.SelectedItem?.ToString() ?? "*";
            var dow = _cboDayOfWeek.SelectedItem?.ToString() ?? "*";

            var expression = BuildExpressionFromDropdowns(min, hour, dom, month, dow);
            if (expression == null) return; // Has "Custom" field, can't rebuild

            _suppressSyncEvents = true;
            try
            {
                _txtRawExpression.Text = expression;
                UpdateDescriptionAndNextRun();
            }
            finally
            {
                _suppressSyncEvents = false;
            }
            CronExpressionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RawExpression_TextChanged(object? sender, EventArgs e)
        {
            if (_suppressSyncEvents) return;

            _suppressSyncEvents = true;
            try
            {
                SyncDropdownsFromRawText();
                UpdateDescriptionAndNextRun();
            }
            finally
            {
                _suppressSyncEvents = false;
            }
            CronExpressionChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Sync Logic

        private void SyncDropdownsFromRawText()
        {
            var text = _txtRawExpression.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                SetDropdownValue(_cboMinute, "*");
                SetDropdownValue(_cboHour, "*");
                SetDropdownValue(_cboDayOfMonth, "*");
                SetDropdownValue(_cboMonth, "*");
                SetDropdownValue(_cboDayOfWeek, "*");
                return;
            }

            if (TryParseToDropdowns(text, out var min, out var hour, out var dom, out var month, out var dow))
            {
                SetDropdownValue(_cboMinute, min);
                SetDropdownValue(_cboHour, hour);
                SetDropdownValue(_cboDayOfMonth, dom);
                SetDropdownValue(_cboMonth, month);
                SetDropdownValue(_cboDayOfWeek, dow);
            }
            else
            {
                // Invalid expression - set all to Custom
                SetDropdownValue(_cboMinute, CustomIndicator);
                SetDropdownValue(_cboHour, CustomIndicator);
                SetDropdownValue(_cboDayOfMonth, CustomIndicator);
                SetDropdownValue(_cboMonth, CustomIndicator);
                SetDropdownValue(_cboDayOfWeek, CustomIndicator);
            }
        }

        private static void SetDropdownValue(ComboBox cbo, string value)
        {
            var index = cbo.Items.IndexOf(value);
            if (index >= 0)
            {
                cbo.SelectedIndex = index;
            }
            else
            {
                // Value not in dropdown items, set to Custom
                var customIndex = cbo.Items.IndexOf(CustomIndicator);
                if (customIndex >= 0)
                    cbo.SelectedIndex = customIndex;
            }
        }

        private void UpdateDescriptionAndNextRun()
        {
            var expression = _txtRawExpression.Text.Trim();

            if (string.IsNullOrEmpty(expression))
            {
                _lblDescription.Text = string.Empty;
                _lblNextRun.Text = "Next run: --";
                _lblValidation.Text = "Cron expression cannot be empty.";
                _lblValidation.Visible = true;
                RefreshMeasuredLayout();
                return;
            }

            if (_schedulingService == null)
            {
                // No service injected -- show raw field values only
                _lblDescription.Text = expression;
                _lblNextRun.Text = "Next run: --";
                _lblValidation.Text = string.Empty;
                _lblValidation.Visible = false;
                RefreshMeasuredLayout();
                return;
            }

            var validationError = _schedulingService.ValidateCronExpression(expression);
            if (validationError != null)
            {
                _lblDescription.Text = string.Empty;
                _lblNextRun.Text = "Next run: --";
                _lblValidation.Text = validationError;
                _lblValidation.Visible = true;
                RefreshMeasuredLayout();
                return;
            }

            // Valid expression
            _lblValidation.Text = string.Empty;
            _lblValidation.Visible = false;

            var description = _schedulingService.GetDescription(expression);
            _lblDescription.Text = description != null
                ? $"{expression} -- {description}"
                : expression;

            var nextRun = _schedulingService.GetNextRunLocal(expression);
            _lblNextRun.Text = nextRun.HasValue
                ? $"Next run: {nextRun.Value:g}"
                : "Next run: --";

            RefreshMeasuredLayout();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Injects the scheduling service dependency for validation, description, and next-run.
        /// </summary>
        public void SetSchedulingService(SchedulingService service)
        {
            _schedulingService = service;
            // Re-evaluate current expression with the new service
            UpdateDescriptionAndNextRun();
        }

        /// <summary>
        /// Applies DialogTheme colors for dark or light mode.
        /// </summary>
        public void ApplyTheme(bool darkMode)
        {
            DialogTheme.ApplyTo(this, darkMode);

            // Style preset buttons with accent color
            foreach (Control ctrl in _presetPanel.Controls)
            {
                if (ctrl is Button btn)
                {
                    DialogTheme.StyleButton(btn, darkMode);
                    if (darkMode)
                    {
                        btn.FlatAppearance.BorderColor = DialogTheme.DarkAccent;
                    }
                }
            }

            // Validation label stays red regardless of theme
            _lblValidation.ForeColor = Color.Red;
            RefreshMeasuredLayout();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            RefreshMeasuredLayout();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RefreshMeasuredLayout();
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            RefreshMeasuredLayout();
        }

        #endregion

        #region Static Logic Methods (testable without UI)

        /// <summary>
        /// Builds a 5-field cron expression from individual dropdown values.
        /// Returns null if any field is "Custom" (cannot be assembled from dropdowns).
        /// </summary>
        internal static string? BuildExpressionFromDropdowns(
            string minute, string hour, string dayOfMonth, string month, string dayOfWeek)
        {
            if (minute == CustomIndicator || hour == CustomIndicator ||
                dayOfMonth == CustomIndicator || month == CustomIndicator ||
                dayOfWeek == CustomIndicator)
            {
                return null;
            }

            return $"{minute} {hour} {dayOfMonth} {month} {dayOfWeek}";
        }

        /// <summary>
        /// Tries to parse a cron expression string into individual dropdown-compatible values.
        /// Fields that don't match any dropdown item are returned as "Custom".
        /// Returns false if the expression is fundamentally invalid (wrong field count, etc.).
        /// </summary>
        internal static bool TryParseToDropdowns(
            string? expression,
            out string minute, out string hour,
            out string dayOfMonth, out string month, out string dayOfWeek)
        {
            minute = hour = dayOfMonth = month = dayOfWeek = "*";

            if (string.IsNullOrWhiteSpace(expression))
                return false;

            var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
                return false;

            minute = MapFieldToDropdown(parts[0], MinuteItems);
            hour = MapFieldToDropdown(parts[1], HourItems);
            dayOfMonth = MapFieldToDropdown(parts[2], DayOfMonthItems);
            month = MapFieldToDropdown(parts[3], MonthItems);
            dayOfWeek = MapFieldToDropdown(parts[4], DayOfWeekItems);

            return true;
        }

        /// <summary>
        /// Returns the cron expression for a preset name, or null if unknown.
        /// </summary>
        internal static string? GetPresetExpression(string presetName)
        {
            foreach (var (label, expr) in Presets)
            {
                if (string.Equals(label, presetName, StringComparison.Ordinal))
                    return expr;
            }
            return null;
        }

        /// <summary>
        /// Returns the list of preset names (in display order).
        /// </summary>
        internal static IReadOnlyList<string> GetPresetNames()
        {
            return Presets.Select(p => p.Label).ToArray();
        }

        #endregion

        #region Dropdown Item Builders

        private static string[] BuildMinuteItems()
        {
            var items = new List<string> { "*", "0", "*/5", "*/10", "*/15", "*/30" };
            for (var i = 1; i <= 59; i++)
            {
                var val = i.ToString();
                if (!items.Contains(val))
                    items.Add(val);
            }
            return items.ToArray();
        }

        private static string[] BuildHourItems()
        {
            var items = new List<string> { "*" };
            for (var i = 0; i <= 23; i++)
                items.Add(i.ToString());
            items.AddRange(new[] { "*/2", "*/3", "*/4", "*/6", "*/8", "*/12" });
            return items.ToArray();
        }

        private static string[] BuildDayOfMonthItems()
        {
            var items = new List<string> { "*" };
            for (var i = 1; i <= 31; i++)
                items.Add(i.ToString());
            items.AddRange(new[] { "*/2", "L" });
            return items.ToArray();
        }

        private static string[] BuildMonthItems()
        {
            var items = new List<string> { "*" };
            for (var i = 1; i <= 12; i++)
                items.Add(i.ToString());
            items.AddRange(new[] { "*/2", "*/3", "*/6" });
            return items.ToArray();
        }

        private static string[] BuildDayOfWeekItems()
        {
            var items = new List<string> { "*" };
            for (var i = 0; i <= 6; i++)
                items.Add(i.ToString());
            items.AddRange(new[] { "1-5", "0,6" });
            return items.ToArray();
        }

        /// <summary>
        /// Maps a cron field value to the closest dropdown item.
        /// Returns the value if it's in the dropdown items, or "Custom" if not.
        /// </summary>
        private static string MapFieldToDropdown(string fieldValue, string[] dropdownItems)
        {
            // Check if the field value is directly in the dropdown items
            foreach (var item in dropdownItems)
            {
                if (string.Equals(item, fieldValue, StringComparison.Ordinal))
                    return fieldValue;
            }

            return CustomIndicator;
        }

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cboMinute.SelectedIndexChanged -= Dropdown_SelectedIndexChanged;
                _cboHour.SelectedIndexChanged -= Dropdown_SelectedIndexChanged;
                _cboDayOfMonth.SelectedIndexChanged -= Dropdown_SelectedIndexChanged;
                _cboMonth.SelectedIndexChanged -= Dropdown_SelectedIndexChanged;
                _cboDayOfWeek.SelectedIndexChanged -= Dropdown_SelectedIndexChanged;
                _txtRawExpression.TextChanged -= RawExpression_TextChanged;

                foreach (Control ctrl in _presetPanel.Controls)
                {
                    if (ctrl is Button btn)
                        btn.Click -= PresetButton_Click;
                }
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
