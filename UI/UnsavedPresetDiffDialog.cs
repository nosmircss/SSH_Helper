using System.Globalization;
using SSH_Helper.Utilities;

namespace SSH_Helper.UI
{
    internal sealed class UnsavedPresetDiffDialog : Form
    {
        private readonly RichTextBox _txtDiff;
        private readonly bool _darkMode;

        public UnsavedPresetDiffDialog(
            string savedPresetName,
            string currentPresetName,
            int? savedTimeout,
            string currentTimeoutText,
            string savedCommands,
            string currentCommands,
            bool darkMode)
        {
            _darkMode = darkMode;

            Text = "Unsaved Preset";
            Size = new Size(920, 620);
            MinimumSize = new Size(760, 480);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var trimmedCurrentPresetName = (currentPresetName ?? string.Empty).Trim();
            var timeoutParsed = int.TryParse(currentTimeoutText, out var parsedTimeout);
            var timeoutChanged = timeoutParsed ? savedTimeout != parsedTimeout : savedTimeout.HasValue;
            var nameChanged = !string.Equals(trimmedCurrentPresetName, savedPresetName, StringComparison.Ordinal);
            var commandChanged = !string.Equals(
                NormalizeCommandText(savedCommands),
                NormalizeCommandText(currentCommands),
                StringComparison.Ordinal);
            var summaryText = BuildSummaryLine(nameChanged, timeoutChanged);

            var promptLabel = new Label
            {
                Text = $"Save changes to preset '{savedPresetName}'?",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            var headerLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = string.IsNullOrEmpty(summaryText) ? 1 : 2,
                Dock = DockStyle.Fill,
                AutoSize = true
            };
            headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headerLayout.Controls.Add(promptLabel, 0, 0);
            if (!string.IsNullOrEmpty(summaryText))
            {
                var summaryLabel = new Label
                {
                    Text = summaryText,
                    AutoSize = true,
                    ForeColor = darkMode ? DialogTheme.DarkSecondaryText : DialogTheme.LightSecondaryText,
                    Margin = new Padding(0, 0, 0, 0)
                };

                headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                headerLayout.Controls.Add(summaryLabel, 0, 1);
            }

            _txtDiff = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = false,
                DetectUrls = false,
                HideSelection = false,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = RichTextBoxScrollBars.Both,
                Font = new Font(FontFamily.GenericMonospace, 9.0f),
                Margin = new Padding(0, 8, 0, 8)
            };

            var btnSave = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.Yes,
                Size = new Size(96, 30),
                Margin = new Padding(8, 0, 0, 0)
            };

            var btnDiscard = new Button
            {
                Text = "Discard",
                DialogResult = DialogResult.No,
                Size = new Size(96, 30),
                Margin = new Padding(8, 0, 0, 0)
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(96, 30),
                Margin = new Padding(8, 0, 0, 0)
            };

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 4, 0, 0)
            };
            buttonPanel.Controls.Add(btnSave);
            buttonPanel.Controls.Add(btnDiscard);
            buttonPanel.Controls.Add(btnCancel);

            var root = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 3,
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(headerLayout, 0, 0);
            root.Controls.Add(_txtDiff, 0, 1);
            root.Controls.Add(buttonPanel, 0, 2);

            Controls.Add(root);
            AcceptButton = btnSave;
            CancelButton = btnCancel;

            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.StyleButton(btnSave, darkMode, isPrimary: true);
            DialogTheme.StyleButton(btnDiscard, darkMode);
            DialogTheme.StyleButton(btnCancel, darkMode);
            DialogTheme.SetDarkTitleBar(this, darkMode);

            if (darkMode)
            {
                _txtDiff.BackColor = DialogTheme.DarkInput;
                _txtDiff.ForeColor = DialogTheme.DarkText;
            }
            else
            {
                _txtDiff.BackColor = DialogTheme.LightInput;
                _txtDiff.ForeColor = DialogTheme.LightText;
            }

            PopulateDiff(
                savedPresetName,
                trimmedCurrentPresetName,
                savedTimeout,
                currentTimeoutText,
                timeoutParsed,
                parsedTimeout,
                savedCommands,
                currentCommands,
                nameChanged,
                timeoutChanged,
                commandChanged);

            Load += (_, _) => DialogTheme.ApplyNativeTheme(this, darkMode);
        }

        private void PopulateDiff(
            string savedPresetName,
            string currentPresetName,
            int? savedTimeout,
            string currentTimeoutText,
            bool timeoutParsed,
            int parsedTimeout,
            string savedCommands,
            string currentCommands,
            bool nameChanged,
            bool timeoutChanged,
            bool commandChanged)
        {
            var lines = new List<InlineDiffLine>();

            if (nameChanged)
            {
                lines.Add(new InlineDiffLine(
                    InlineDiffLineKind.Meta,
                    $"~ Name: \"{DisplayText(savedPresetName)}\" -> \"{DisplayText(currentPresetName)}\""));
            }

            if (timeoutChanged)
            {
                lines.Add(new InlineDiffLine(
                    InlineDiffLineKind.Meta,
                    $"~ Timeout: {FormatTimeout(savedTimeout)} -> {FormatCurrentTimeout(currentTimeoutText, timeoutParsed, parsedTimeout)}"));
            }

            if (commandChanged)
            {
                if (lines.Count > 0)
                {
                    lines.Add(new InlineDiffLine(InlineDiffLineKind.Meta, string.Empty));
                }

                lines.AddRange(
                    InlineDiffBuilder.Build(
                        savedCommands,
                        currentCommands,
                        contextLines: 0,
                        maxOutputLines: EstimateCommandDiffLineBudget(savedCommands, currentCommands),
                        includeAllLines: true));
            }
            else if (lines.Count == 0)
            {
                lines.Add(new InlineDiffLine(InlineDiffLineKind.Meta, "~ No command text changes."));
            }

            RenderLines(lines);
        }

        private void RenderLines(IReadOnlyList<InlineDiffLine> lines)
        {
            _txtDiff.SuspendLayout();
            _txtDiff.Clear();

            foreach (var line in lines)
            {
                var start = _txtDiff.TextLength;
                _txtDiff.AppendText(line.Text);
                _txtDiff.AppendText(Environment.NewLine);

                if (line.Text.Length == 0)
                    continue;

                _txtDiff.Select(start, line.Text.Length);
                _txtDiff.SelectionColor = ResolveLineColor(line.Kind);
            }

            _txtDiff.Select(0, 0);
            _txtDiff.SelectionColor = ResolveLineColor(InlineDiffLineKind.Context);
            _txtDiff.ResumeLayout();
        }

        private Color ResolveLineColor(InlineDiffLineKind kind)
        {
            return kind switch
            {
                InlineDiffLineKind.Added => _darkMode ? Color.FromArgb(140, 220, 140) : Color.FromArgb(0, 110, 0),
                InlineDiffLineKind.Removed => _darkMode ? Color.FromArgb(255, 150, 150) : Color.FromArgb(170, 0, 0),
                InlineDiffLineKind.Meta => _darkMode ? Color.FromArgb(255, 210, 120) : Color.FromArgb(120, 90, 0),
                _ => _darkMode ? DialogTheme.DarkText : DialogTheme.LightText
            };
        }

        private static string BuildSummaryLine(bool nameChanged, bool timeoutChanged)
        {
            var parts = new List<string>();
            if (nameChanged) parts.Add("name");
            if (timeoutChanged) parts.Add("timeout");
            return parts.Count == 0 ? string.Empty : $"Changed fields: {string.Join(", ", parts)}";
        }

        private static string DisplayText(string value)
        {
            return string.IsNullOrEmpty(value) ? "(empty)" : value;
        }

        private static string FormatTimeout(int? timeout)
        {
            return timeout.HasValue
                ? timeout.Value.ToString(CultureInfo.InvariantCulture)
                : "(default)";
        }

        private static string FormatCurrentTimeout(string currentTimeoutText, bool timeoutParsed, int parsedTimeout)
        {
            if (timeoutParsed)
                return parsedTimeout.ToString(CultureInfo.InvariantCulture);

            if (string.IsNullOrWhiteSpace(currentTimeoutText))
                return "(default)";

            return $"\"{currentTimeoutText.Trim()}\" (invalid)";
        }

        private static string NormalizeCommandText(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
        }

        private static int EstimateCommandDiffLineBudget(string savedCommands, string currentCommands)
        {
            var savedLines = CountLines(savedCommands);
            var currentLines = CountLines(currentCommands);
            var estimate = Math.Max(savedLines, currentLines) + 20;
            return Math.Clamp(estimate, 200, 10_000);
        }

        private static int CountLines(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            var normalized = NormalizeCommandText(value);
            return normalized.Length == 0 ? 0 : normalized.Split('\n').Length;
        }
    }
}
