using System.Globalization;
using SSH_Helper.Models;
using SSH_Helper.Utilities;

namespace SSH_Helper.UI
{
    internal enum PresetSaveImpactAction
    {
        Cancel = 0,
        SaveExisting,
        RenameExisting,
        CreateNew,
        Discard
    }

    internal enum PresetSavePromptMode
    {
        SaveCancel = 0,
        SaveDiscardCancel,
        RenameExistingCreateNewCancel,
        RenameExistingCreateNewDiscardCancel
    }

    internal sealed class UnsavedPresetDiffDialog : Form
    {
        private readonly RichTextBox _txtDiff;
        private readonly bool _darkMode;
        private readonly Label _lblImpactCount;
        private readonly Label _lblImpactSummary;
        private readonly Button _btnToggleAffectedJobs;
        private readonly Panel _panelAffectedJobs;
        private readonly ListBox _lstAffectedJobs;
        private bool _affectedJobsExpanded;

        public UnsavedPresetDiffDialog(
            string savedPresetName,
            string currentPresetName,
            int? savedTimeout,
            string currentTimeoutText,
            string savedCommands,
            string currentCommands,
            bool darkMode,
            PresetSaveImpact? impact = null,
            PresetSavePromptMode promptMode = PresetSavePromptMode.SaveDiscardCancel,
            string? impactSummaryOverride = null)
        {
            _darkMode = darkMode;

            Text = "Save Preset";
            Size = new Size(920, 620);
            MinimumSize = new Size(760, 480);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            SelectedAction = PresetSaveImpactAction.Cancel;

            var trimmedCurrentPresetName = (currentPresetName ?? string.Empty).Trim();
            var timeoutParsed = int.TryParse(currentTimeoutText, out var parsedTimeout);
            var timeoutChanged = timeoutParsed ? savedTimeout != parsedTimeout : savedTimeout.HasValue;
            var nameChanged = !string.Equals(trimmedCurrentPresetName, savedPresetName, StringComparison.Ordinal);
            var commandChanged = !string.Equals(
                NormalizeCommandText(savedCommands),
                NormalizeCommandText(currentCommands),
                StringComparison.Ordinal);
            var summaryText = BuildSummaryLine(nameChanged, timeoutChanged);
            var hasImpact = impact?.HasAffectedJobs == true;

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

            _lblImpactCount = new Label
            {
                AutoSize = true,
                Visible = hasImpact
            };

            _lblImpactSummary = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(860, 0),
                Margin = new Padding(0, 2, 0, 0),
                Visible = hasImpact
            };

            _btnToggleAffectedJobs = new Button
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 8, 0, 0),
                Padding = new Padding(8, 2, 8, 2),
                Visible = hasImpact
            };
            _btnToggleAffectedJobs.Click += (_, _) => ToggleAffectedJobsVisibility();

            _lstAffectedJobs = new ListBox
            {
                Dock = DockStyle.Fill,
                HorizontalScrollbar = true,
                IntegralHeight = false
            };

            _panelAffectedJobs = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                Margin = new Padding(0, 6, 0, 0),
                Padding = new Padding(0),
                Visible = false
            };
            _panelAffectedJobs.Controls.Add(_lstAffectedJobs);

            var impactLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 4,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Visible = hasImpact
            };
            impactLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            impactLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            impactLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            impactLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            impactLayout.Controls.Add(_lblImpactCount, 0, 0);
            impactLayout.Controls.Add(_lblImpactSummary, 0, 1);
            impactLayout.Controls.Add(_btnToggleAffectedJobs, 0, 2);
            impactLayout.Controls.Add(_panelAffectedJobs, 0, 3);

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

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 4, 0, 0)
            };

            ConfigureActionButtons(buttonPanel, promptMode);

            var root = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 4,
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(headerLayout, 0, 0);
            root.Controls.Add(impactLayout, 0, 1);
            root.Controls.Add(_txtDiff, 0, 2);
            root.Controls.Add(buttonPanel, 0, 3);

            Controls.Add(root);

            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.SetDarkTitleBar(this, darkMode);

            if (darkMode)
            {
                _txtDiff.BackColor = DialogTheme.DarkInput;
                _txtDiff.ForeColor = DialogTheme.DarkText;
                _lstAffectedJobs.BackColor = DialogTheme.DarkInput;
                _lstAffectedJobs.ForeColor = DialogTheme.DarkText;
            }
            else
            {
                _txtDiff.BackColor = DialogTheme.LightInput;
                _txtDiff.ForeColor = DialogTheme.LightText;
                _lstAffectedJobs.BackColor = DialogTheme.LightInput;
                _lstAffectedJobs.ForeColor = DialogTheme.LightText;
            }

            if (!string.IsNullOrEmpty(summaryText))
            {
                headerLayout.Controls[1].ForeColor = darkMode
                    ? DialogTheme.DarkSecondaryText
                    : DialogTheme.LightSecondaryText;
            }

            if (hasImpact)
            {
                _lblImpactCount.Text = BuildImpactCountText(savedPresetName, impact!.AffectedJobs.Count);
                _lblImpactSummary.Text = impactSummaryOverride ?? BuildImpactSummaryText(
                    savedPresetName,
                    trimmedCurrentPresetName,
                    nameChanged);
                _lblImpactSummary.ForeColor = darkMode
                    ? DialogTheme.DarkSecondaryText
                    : DialogTheme.LightSecondaryText;

                foreach (var job in impact.AffectedJobs)
                {
                    _lstAffectedJobs.Items.Add(FormatAffectedJobText(job));
                }

                UpdateAffectedJobsToggleText();
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

        public PresetSaveImpactAction SelectedAction { get; private set; }

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
                        maxOutputLines: CalculateFullCommandDiffLineBudget(savedCommands, currentCommands),
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

        private void ConfigureActionButtons(
            FlowLayoutPanel buttonPanel,
            PresetSavePromptMode promptMode)
        {
            switch (promptMode)
            {
                case PresetSavePromptMode.RenameExistingCreateNewDiscardCancel:
                    var btnRenameDiscard = CreateActionButton(
                        "Rename Existing",
                        PresetSaveImpactAction.RenameExisting,
                        isPrimary: true);
                    buttonPanel.Controls.Add(btnRenameDiscard);
                    AcceptButton = btnRenameDiscard;
                    buttonPanel.Controls.Add(CreateActionButton(
                        "Create New",
                        PresetSaveImpactAction.CreateNew,
                        isPrimary: false));
                    buttonPanel.Controls.Add(CreateActionButton(
                        "Discard",
                        PresetSaveImpactAction.Discard,
                        isPrimary: false));
                    break;

                case PresetSavePromptMode.RenameExistingCreateNewCancel:
                    var btnRename = CreateActionButton(
                        "Rename Existing",
                        PresetSaveImpactAction.RenameExisting,
                        isPrimary: true);
                    buttonPanel.Controls.Add(btnRename);
                    AcceptButton = btnRename;
                    buttonPanel.Controls.Add(CreateActionButton(
                        "Create New",
                        PresetSaveImpactAction.CreateNew,
                        isPrimary: false));
                    break;

                case PresetSavePromptMode.SaveDiscardCancel:
                    var btnSaveDiscard = CreateActionButton(
                        "Save",
                        PresetSaveImpactAction.SaveExisting,
                        isPrimary: true);
                    buttonPanel.Controls.Add(btnSaveDiscard);
                    AcceptButton = btnSaveDiscard;
                    buttonPanel.Controls.Add(CreateActionButton(
                        "Discard",
                        PresetSaveImpactAction.Discard,
                        isPrimary: false));
                    break;

                default:
                    var btnSave = CreateActionButton(
                        "Save",
                        PresetSaveImpactAction.SaveExisting,
                        isPrimary: true);
                    buttonPanel.Controls.Add(btnSave);
                    AcceptButton = btnSave;
                    break;
            }

            var btnCancel = CreateActionButton(
                "Cancel",
                PresetSaveImpactAction.Cancel,
                isPrimary: false);
            btnCancel.DialogResult = DialogResult.Cancel;
            buttonPanel.Controls.Add(btnCancel);
            CancelButton = btnCancel;
        }

        private Button CreateActionButton(
            string text,
            PresetSaveImpactAction action,
            bool isPrimary)
        {
            var button = new Button
            {
                Text = text,
                Size = new Size(120, 30),
                Margin = new Padding(8, 0, 0, 0)
            };

            DialogTheme.StyleButton(button, _darkMode, isPrimary: isPrimary);
            button.Click += (_, _) =>
            {
                SelectedAction = action;
                DialogResult = action == PresetSaveImpactAction.Cancel
                    ? DialogResult.Cancel
                    : DialogResult.OK;
                Close();
            };

            return button;
        }

        private void ToggleAffectedJobsVisibility()
        {
            _affectedJobsExpanded = !_affectedJobsExpanded;
            _panelAffectedJobs.Visible = _affectedJobsExpanded;
            UpdateAffectedJobsToggleText();
        }

        private void UpdateAffectedJobsToggleText()
        {
            var jobCount = _lstAffectedJobs.Items.Count;
            var label = jobCount == 1 ? "job" : "jobs";
            _btnToggleAffectedJobs.Text = _affectedJobsExpanded
                ? $"Hide affected scheduled {label} ({jobCount})"
                : $"Show affected scheduled {label} ({jobCount})";
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

        private static string BuildImpactCountText(string savedPresetName, int affectedJobCount)
        {
            var jobLabel = affectedJobCount == 1 ? "scheduled job" : "scheduled jobs";
            return $"Preset '{savedPresetName}' is used by {affectedJobCount} {jobLabel}.";
        }

        private static string BuildImpactSummaryText(
            string savedPresetName,
            string currentPresetName,
            bool nameChanged)
        {
            if (nameChanged)
            {
                return $"Rename Existing will update '{savedPresetName}' to '{currentPresetName}', and future scheduled or Run Now executions will use the renamed preset. " +
                       $"Create New saves '{currentPresetName}' as a separate preset instead.";
            }

            return $"Future scheduled and Run Now executions will use the updated preset '{currentPresetName}'.";
        }

        private static string FormatAffectedJobText(JobDefinition job)
        {
            var targetSuffix = job.TargetType == JobTargetType.Folder
                ? $" [Folder: {job.TargetName}]"
                : string.Empty;
            return $"{job.Name}{targetSuffix}";
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

        private static int CalculateFullCommandDiffLineBudget(string savedCommands, string currentCommands)
        {
            var savedLines = CountLines(savedCommands);
            var currentLines = CountLines(currentCommands);
            var maxOperationCount = (long)savedLines + currentLines + 2;
            return maxOperationCount >= int.MaxValue
                ? int.MaxValue - 1
                : Math.Max(1, (int)maxOperationCount);
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
