using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.UI;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Prompts the user to select one option from a list during script execution.
    /// </summary>
    public class ChooseCommand : IScriptCommand
    {
        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Choose == null)
                return CommandResult.Fail("Choose command has no options");

            if (string.IsNullOrEmpty(step.Choose.Into))
                return CommandResult.Fail("Choose command requires an 'into' property");

            try
            {
                var prompt = context.SubstituteVariables(step.Choose.Prompt ?? "Select an option:");
                var defaultValue = step.Choose.Default != null
                    ? context.SubstituteVariables(step.Choose.Default)
                    : null;

                var resolvedOptions = ChoiceOptionResolver.Resolve(
                    step.Choose.Options,
                    step.Choose.OptionsFrom,
                    context,
                    out var optionResolveError);

                if (resolvedOptions.Count == 0)
                {
                    var error = string.IsNullOrWhiteSpace(optionResolveError)
                        ? "Choose command requires at least one option"
                        : $"Choose command requires at least one option ({optionResolveError})";
                    return CommandResult.Fail(error);
                }

                var selectedValue = await ScriptPromptDialogRunner
                    .ShowAsync<ScriptChooseDialog, string?>(
                        () => new ScriptChooseDialog(prompt, resolvedOptions, defaultValue),
                        dialog => dialog.DialogResult == DialogResult.OK ? dialog.SelectedValue : null,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (selectedValue == null)
                {
                    context.EmitOutput("Selection cancelled by user", ScriptOutputType.Warning);
                    return CommandResult.Fail("Selection cancelled by user");
                }

                context.SetVariable(step.Choose.Into, selectedValue);
                context.EmitOutput($"Set {step.Choose.Into} from user selection", ScriptOutputType.Debug);

                return CommandResult.Ok();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error getting user selection: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return CommandResult.Suppressed(errorMsg);

                return CommandResult.Fail(errorMsg);
            }
        }

    }

    /// <summary>
    /// Dialog for selecting one option from a list.
    /// </summary>
    internal sealed class ScriptChooseDialog : Form
    {
        private readonly ListBox _listBox;
        private readonly List<ChoiceOption> _options;
        private bool _isDark;

        public string? SelectedValue => _listBox.SelectedIndex >= 0
            ? _options[_listBox.SelectedIndex].Value
            : null;

        public ScriptChooseDialog(string prompt, List<ChoiceOption> options, string? defaultValue)
        {
            _options = options;

            var visibleItems = Math.Min(options.Count, 10);
            var listHeight = Math.Max(visibleItems * 20, 40);
            var formHeight = 130 + listHeight;

            Text = "Script Choice";
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

            _listBox = new ListBox
            {
                Location = new Point(15, 58),
                Size = new Size(355, listHeight),
                SelectionMode = SelectionMode.One,
                IntegralHeight = false
            };

            foreach (var opt in options)
            {
                _listBox.Items.Add(opt.Label);
            }

            // Pre-select default
            if (defaultValue != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    if (string.Equals(options[i].Value, defaultValue, StringComparison.OrdinalIgnoreCase))
                    {
                        _listBox.SelectedIndex = i;
                        break;
                    }
                }
            }

            var btnOk = new Button
            {
                Text = "OK",
                Size = new Size(80, 28),
                Location = new Point(205, formHeight - 70),
                Enabled = _listBox.SelectedIndex >= 0
            };
            btnOk.Click += (_, _) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 28),
                Location = new Point(290, formHeight - 70),
                DialogResult = DialogResult.Cancel
            };

            _listBox.SelectedIndexChanged += (_, _) =>
            {
                btnOk.Enabled = _listBox.SelectedIndex >= 0;
            };

            _listBox.DoubleClick += (_, _) =>
            {
                if (_listBox.SelectedIndex >= 0)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            Controls.Add(lblPrompt);
            Controls.Add(_listBox);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            // Apply dark mode if the app is in dark mode
            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            _isDark = mainForm != null && mainForm.BackColor.GetBrightness() < 0.2f;
            if (_isDark)
            {
                DialogTheme.ApplyTo(this, true);
                DialogTheme.StyleButton(btnOk, true, isPrimary: true);
                DialogTheme.StyleButton(btnCancel, true);
                DialogTheme.SetDarkTitleBar(this, true);
                _listBox.BorderStyle = BorderStyle.None;
                _listBox.DrawMode = DrawMode.OwnerDrawFixed;
                _listBox.DrawItem += ListBox_DrawItem;
            }

            Load += (_, _) =>
            {
                if (_isDark)
                    DialogTheme.ApplyNativeTheme(this, true);
                _listBox.Focus();
            };
        }

        private void ListBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var isSelected = (e.State & DrawItemState.Selected) != 0;
            var bgColor = isSelected ? DialogTheme.GridDarkSelection : _listBox.BackColor;
            var fgColor = isSelected ? Color.White : _listBox.ForeColor;

            using var bgBrush = new SolidBrush(bgColor);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            using var textBrush = new SolidBrush(fgColor);
            var text = _listBox.Items[e.Index]?.ToString() ?? string.Empty;
            var textRect = new RectangleF(e.Bounds.X + 2, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height);
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(text, e.Font ?? _listBox.Font, textBrush, textRect, sf);

            if (isSelected)
            {
                using var borderPen = new Pen(Color.FromArgb(0, 122, 204));
                e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }
        }
    }
}
