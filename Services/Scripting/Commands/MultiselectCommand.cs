using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CheckBoxState = System.Windows.Forms.VisualStyles.CheckBoxState;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.UI;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Prompts the user to select multiple options from a list during script execution.
    /// </summary>
    public class MultiselectCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Multiselect == null)
                return Task.FromResult(CommandResult.Fail("Multiselect command has no options"));

            if (string.IsNullOrEmpty(step.Multiselect.Into))
                return Task.FromResult(CommandResult.Fail("Multiselect command requires an 'into' property"));

            try
            {
                var prompt = context.SubstituteVariables(step.Multiselect.Prompt ?? "Select options:");

                var resolvedOptions = ChoiceOptionResolver.Resolve(
                    step.Multiselect.Options,
                    step.Multiselect.OptionsFrom,
                    context,
                    out var optionResolveError);

                if (resolvedOptions.Count == 0)
                {
                    var error = string.IsNullOrWhiteSpace(optionResolveError)
                        ? "Multiselect command requires at least one option"
                        : $"Multiselect command requires at least one option ({optionResolveError})";
                    return Task.FromResult(CommandResult.Fail(error));
                }

                List<string>? selectedValues = null;

                var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                if (mainForm != null && mainForm.InvokeRequired)
                {
                    mainForm.Invoke(() =>
                    {
                        selectedValues = ShowMultiselectDialog(prompt, resolvedOptions, step.Multiselect.Min, step.Multiselect.Max);
                    });
                }
                else
                {
                    selectedValues = ShowMultiselectDialog(prompt, resolvedOptions, step.Multiselect.Min, step.Multiselect.Max);
                }

                if (selectedValues == null)
                {
                    context.EmitOutput("Selection cancelled by user", ScriptOutputType.Warning);
                    return Task.FromResult(CommandResult.Fail("Selection cancelled by user"));
                }

                context.SetVariable(step.Multiselect.Into, selectedValues);
                context.SetVariable($"{step.Multiselect.Into}_count", selectedValues.Count.ToString());
                context.EmitOutput($"Set {step.Multiselect.Into} with {selectedValues.Count} selection(s)", ScriptOutputType.Debug);

                return Task.FromResult(CommandResult.Ok());
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error getting user selection: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return Task.FromResult(CommandResult.Suppressed(errorMsg));

                return Task.FromResult(CommandResult.Fail(errorMsg));
            }
        }

        private static List<string>? ShowMultiselectDialog(string prompt, List<ChoiceOption> options, int? min, int? max)
        {
            using var dialog = new ScriptMultiselectDialog(prompt, options, min, max);
            var result = dialog.ShowDialog();

            if (result == DialogResult.OK)
                return dialog.SelectedValues;

            return null;
        }
    }

    /// <summary>
    /// Dialog for selecting multiple options from a checklist.
    /// </summary>
    internal sealed class ScriptMultiselectDialog : Form
    {
        private readonly ThemedCheckedListBox _checkedListBox;
        private readonly List<ChoiceOption> _options;
        private readonly Label _lblError;
        private readonly Label _lblCount;
        private readonly int? _min;
        private readonly int? _max;

        public List<string> SelectedValues
        {
            get
            {
                var values = new List<string>();
                foreach (int index in _checkedListBox.CheckedIndices)
                {
                    values.Add(_options[index].Value);
                }
                return values;
            }
        }

        public ScriptMultiselectDialog(string prompt, List<ChoiceOption> options, int? min, int? max)
        {
            _options = options;
            _min = min;
            _max = max;

            var visibleItems = Math.Min(options.Count, 10);
            var listHeight = Math.Max(visibleItems * 20, 40);
            var formHeight = 165 + listHeight;

            Text = "Script Selection";
            Size = new Size(400, formHeight);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var lblPrompt = new Label
            {
                Text = prompt,
                Location = new Point(15, 15),
                Size = new Size(355, 40),
                AutoSize = false
            };

            _checkedListBox = new ThemedCheckedListBox
            {
                Location = new Point(15, 58),
                Size = new Size(355, listHeight),
                CheckOnClick = true,
                IntegralHeight = false
            };

            foreach (var opt in options)
            {
                _checkedListBox.Items.Add(opt.Label);
            }

            _lblCount = new Label
            {
                Text = BuildCountText(0),
                Location = new Point(15, 62 + listHeight),
                Size = new Size(170, 20),
                ForeColor = SystemColors.GrayText
            };

            _lblError = new Label
            {
                Text = string.Empty,
                Location = new Point(185, 62 + listHeight),
                Size = new Size(185, 20),
                ForeColor = Color.Red,
                TextAlign = ContentAlignment.TopRight,
                Visible = false
            };

            var btnOk = new Button
            {
                Text = "OK",
                Size = new Size(80, 28),
                Location = new Point(205, formHeight - 70)
            };
            btnOk.Click += BtnOk_Click;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 28),
                Location = new Point(290, formHeight - 70),
                DialogResult = DialogResult.Cancel
            };

            _checkedListBox.ItemCheck += (_, e) =>
            {
                // ItemCheck fires before the check state changes, so we compute the new count
                var currentCount = _checkedListBox.CheckedItems.Count;
                var newCount = e.NewValue == CheckState.Checked ? currentCount + 1 : currentCount - 1;
                _lblCount.Text = BuildCountText(newCount);
                _lblError.Visible = false;
            };

            Controls.Add(lblPrompt);
            Controls.Add(_checkedListBox);
            Controls.Add(_lblCount);
            Controls.Add(_lblError);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            // Apply dark mode if the app is in dark mode
            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            var isDark = mainForm != null && mainForm.BackColor.GetBrightness() < 0.2f;
            if (isDark)
            {
                DialogTheme.ApplyTo(this, true);
                DialogTheme.StyleButton(btnOk, true, isPrimary: true);
                DialogTheme.StyleButton(btnCancel, true);
                DialogTheme.SetDarkTitleBar(this, true);
                _checkedListBox.BorderStyle = BorderStyle.None;
                _checkedListBox.UseDarkSelection = true;
            }

            Load += (_, _) =>
            {
                if (isDark)
                    DialogTheme.ApplyNativeTheme(this, true);
                _checkedListBox.Focus();
            };
        }

        private string BuildCountText(int count)
        {
            var text = $"Selected: {count}";
            if (_min.HasValue || _max.HasValue)
            {
                var parts = new List<string>();
                if (_min.HasValue) parts.Add($"min {_min.Value}");
                if (_max.HasValue) parts.Add($"max {_max.Value}");
                text += $" ({string.Join(", ", parts)})";
            }
            return text;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            var checkedCount = _checkedListBox.CheckedItems.Count;

            if (_min.HasValue && checkedCount < _min.Value)
            {
                _lblError.Text = $"Select at least {_min.Value}";
                _lblError.Visible = true;
                return;
            }

            if (_max.HasValue && checkedCount > _max.Value)
            {
                _lblError.Text = $"Select at most {_max.Value}";
                _lblError.Visible = true;
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// CheckedListBox subclass that overrides OnDrawItem for custom selection colors.
        /// CheckedListBox.OnDrawItem does not call base, so the DrawItem event never fires.
        /// </summary>
        private sealed class ThemedCheckedListBox : CheckedListBox
    {
        public bool UseDarkSelection { get; set; }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (!UseDarkSelection || e.Index < 0)
            {
                base.OnDrawItem(e);
                return;
            }

            var isSelected = (e.State & DrawItemState.Selected) != 0;
            var bgColor = isSelected ? DialogTheme.GridDarkSelection : BackColor;
            var fgColor = isSelected ? Color.White : ForeColor;

            // Background
            using var bgBrush = new SolidBrush(bgColor);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            // Checkbox
            var checkSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, CheckBoxState.UncheckedNormal);
            var checkX = e.Bounds.X + 2;
            var checkY = e.Bounds.Y + (e.Bounds.Height - checkSize.Height) / 2;
            var isChecked = GetItemChecked(e.Index);
            var checkState = isChecked ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
            CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(checkX, checkY), checkState);

            // Text
            var textX = checkX + checkSize.Width + 4;
            var textRect = new RectangleF(textX, e.Bounds.Y, e.Bounds.Width - textX + e.Bounds.X, e.Bounds.Height);
            using var textBrush = new SolidBrush(fgColor);
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(Items[e.Index]?.ToString() ?? "", e.Font ?? Font, textBrush, textRect, sf);

            // Selection border
            if (isSelected)
            {
                using var borderPen = new Pen(Color.FromArgb(0, 122, 204));
                e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }
        }
        }
    }
}
