using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.UI;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Prompts the user with a yes/no confirmation during script execution.
    /// </summary>
    public class ConfirmCommand : IScriptCommand
    {
        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Confirm == null)
                return CommandResult.Fail("Confirm command has no options");

            if (string.IsNullOrEmpty(step.Confirm.Into))
                return CommandResult.Fail("Confirm command requires an 'into' property");

            try
            {
                var prompt = context.SubstituteVariables(step.Confirm.Prompt ?? "Are you sure?");
                var title = string.IsNullOrWhiteSpace(step.Confirm.Title)
                    ? null
                    : context.SubstituteVariables(step.Confirm.Title);
                var defaultYes = step.Confirm.Default;

                var fontSize = step.Confirm.FontSize ?? ScriptPromptDialogRunner.DefaultPromptFontSize ?? 9f;

                var confirmed = await ScriptPromptDialogRunner
                    .ShowAsync<ScriptConfirmDialog, bool>(
                        () => new ScriptConfirmDialog(prompt, defaultYes, title, fontSize),
                        dialog => dialog.DialogResult == DialogResult.Yes,
                        cancellationToken)
                    .ConfigureAwait(false);

                var result = confirmed ? "true" : "false";
                context.SetVariable(step.Confirm.Into, result);
                context.EmitOutput($"Set {step.Confirm.Into} = {result} from user confirmation", ScriptOutputType.Debug);

                return CommandResult.Ok();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error getting user confirmation: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                return CommandResult.ApplyOnError(step, errorMsg);
            }
        }
    }

    /// <summary>
    /// Simple yes/no confirmation dialog for script prompts.
    /// </summary>
    internal sealed class ScriptConfirmDialog : Form
    {
        public ScriptConfirmDialog(string prompt, bool defaultYes, string? title = null, float fontSize = 9f)
        {
            var clampedFontSize = Math.Clamp(fontSize, 7f, 36f);
            var scale = Math.Max(1f, clampedFontSize / 9f);
            int S(int v) => (int)Math.Round(v * scale);

            Text = string.IsNullOrWhiteSpace(title) ? "Script Confirmation" : title.Trim();
            Size = new Size(S(400), S(150));
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var ambientForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            var baseFontFamily = ambientForm?.Font.FontFamily.Name ?? "Segoe UI";
            DialogTheme.SetDialogFont(this, new Font(baseFontFamily, clampedFontSize));

            var lblPrompt = new Label
            {
                Text = prompt,
                Location = new Point(S(15), S(15)),
                Size = new Size(S(355), S(50)),
                AutoSize = false
            };

            var btnYes = new Button
            {
                Text = "Yes",
                Size = new Size(S(80), S(28)),
                Location = new Point(S(205), S(75)),
                DialogResult = DialogResult.Yes
            };

            var btnNo = new Button
            {
                Text = "No",
                Size = new Size(S(80), S(28)),
                Location = new Point(S(290), S(75)),
                DialogResult = DialogResult.No
            };

            Controls.Add(lblPrompt);
            Controls.Add(btnYes);
            Controls.Add(btnNo);

            AcceptButton = defaultYes ? btnYes : btnNo;
            CancelButton = btnNo;

            // Apply dark mode if the app is in dark mode
            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            var isDark = mainForm != null && mainForm.BackColor.GetBrightness() < 0.2f;
            if (isDark)
            {
                DialogTheme.ApplyTo(this, true);
                DialogTheme.StyleButton(btnYes, true, isPrimary: defaultYes);
                DialogTheme.StyleButton(btnNo, true, isPrimary: !defaultYes);
                DialogTheme.SetDarkTitleBar(this, true);
            }

            Load += (_, _) =>
            {
                if (isDark)
                    DialogTheme.ApplyNativeTheme(this, true);
                if (defaultYes)
                    btnYes.Focus();
                else
                    btnNo.Focus();
            };
        }
    }
}
