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
                var defaultYes = step.Confirm.Default;

                var confirmed = await ScriptPromptDialogRunner
                    .ShowAsync<ScriptConfirmDialog, bool>(
                        () => new ScriptConfirmDialog(prompt, defaultYes),
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

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return CommandResult.Suppressed(errorMsg);

                return CommandResult.Fail(errorMsg);
            }
        }
    }

    /// <summary>
    /// Simple yes/no confirmation dialog for script prompts.
    /// </summary>
    internal sealed class ScriptConfirmDialog : Form
    {
        public ScriptConfirmDialog(string prompt, bool defaultYes)
        {
            Text = "Script Confirmation";
            Size = new Size(400, 150);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var lblPrompt = new Label
            {
                Text = prompt,
                Location = new Point(15, 15),
                Size = new Size(355, 50),
                AutoSize = false
            };

            var btnYes = new Button
            {
                Text = "Yes",
                Size = new Size(80, 28),
                Location = new Point(205, 75),
                DialogResult = DialogResult.Yes
            };

            var btnNo = new Button
            {
                Text = "No",
                Size = new Size(80, 28),
                Location = new Point(290, 75),
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
