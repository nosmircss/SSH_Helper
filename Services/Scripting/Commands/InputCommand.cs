using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Prompts the user for input during script execution.
    /// </summary>
    public class InputCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Input == null)
                return Task.FromResult(CommandResult.Fail("Input command has no options"));

            if (string.IsNullOrEmpty(step.Input.Into))
                return Task.FromResult(CommandResult.Fail("Input command requires an 'into' property"));

            try
            {
                // Substitute variables in prompt and default
                var prompt = context.SubstituteVariables(step.Input.Prompt ?? "Enter value:");
                var defaultValue = step.Input.Default != null
                    ? context.SubstituteVariables(step.Input.Default)
                    : string.Empty;

                // Compile validation regex if provided
                Regex? validationRegex = null;
                if (!string.IsNullOrEmpty(step.Input.Validate))
                {
                    try
                    {
                        validationRegex = new Regex(step.Input.Validate, RegexOptions.Compiled);
                    }
                    catch (ArgumentException ex)
                    {
                        return Task.FromResult(CommandResult.Fail($"Invalid validation pattern: {ex.Message}"));
                    }
                }

                var validationError = step.Input.ValidationError ?? "Input does not match required format.";

                string? userInput = null;

                // Show input dialog on UI thread
                var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                if (mainForm != null && mainForm.InvokeRequired)
                {
                    mainForm.Invoke(() =>
                    {
                        userInput = ShowInputDialogWithValidation(prompt, defaultValue, step.Input.Password, validationRegex, validationError);
                    });
                }
                else
                {
                    userInput = ShowInputDialogWithValidation(prompt, defaultValue, step.Input.Password, validationRegex, validationError);
                }

                // Check for cancellation
                if (userInput == null)
                {
                    context.EmitOutput("Input cancelled by user", ScriptOutputType.Warning);
                    return Task.FromResult(CommandResult.Fail("Input cancelled by user"));
                }

                // Store the input
                context.SetVariable(step.Input.Into, userInput);
                context.EmitOutput($"Set {step.Input.Into} from user input", ScriptOutputType.Debug);

                return Task.FromResult(CommandResult.Ok());
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error getting user input: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return Task.FromResult(CommandResult.Ok(errorMsg));

                return Task.FromResult(CommandResult.Fail(errorMsg));
            }
        }

        private static string? ShowInputDialogWithValidation(string prompt, string defaultValue, bool password, Regex? validationRegex, string validationError)
        {
            using var dialog = new ScriptInputDialog(prompt, defaultValue, password, validationRegex, validationError);
            var result = dialog.ShowDialog();

            if (result == DialogResult.OK)
                return dialog.InputValue;

            return null;
        }
    }

    /// <summary>
    /// Simple input dialog for script user prompts with optional validation.
    /// </summary>
    internal sealed class ScriptInputDialog : Form
    {
        private readonly TextBox _txtInput;
        private readonly Label _lblError;
        private readonly Regex? _validationRegex;
        private readonly string _validationError;

        public string InputValue => _txtInput.Text;

        public ScriptInputDialog(string prompt, string defaultValue, bool password, Regex? validationRegex = null, string validationError = "Invalid input.")
        {
            _validationRegex = validationRegex;
            _validationError = validationError;

            Text = "Script Input";
            Size = new System.Drawing.Size(400, 185);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var lblPrompt = new Label
            {
                Text = prompt,
                Location = new System.Drawing.Point(15, 15),
                Size = new System.Drawing.Size(355, 40),
                AutoSize = false
            };

            _txtInput = new TextBox
            {
                Text = defaultValue,
                Location = new System.Drawing.Point(15, 58),
                Size = new System.Drawing.Size(355, 23),
                UseSystemPasswordChar = password
            };

            _lblError = new Label
            {
                Text = string.Empty,
                Location = new System.Drawing.Point(15, 84),
                Size = new System.Drawing.Size(355, 20),
                ForeColor = System.Drawing.Color.Red,
                Visible = false
            };

            var btnOk = new Button
            {
                Text = "OK",
                Size = new System.Drawing.Size(80, 28),
                Location = new System.Drawing.Point(205, 110),
            };
            btnOk.Click += BtnOk_Click;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new System.Drawing.Size(80, 28),
                Location = new System.Drawing.Point(290, 110),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(lblPrompt);
            Controls.Add(_txtInput);
            Controls.Add(_lblError);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Load += (_, _) =>
            {
                _txtInput.Focus();
                _txtInput.SelectAll();
            };

            // Clear error when user types
            _txtInput.TextChanged += (_, _) =>
            {
                _lblError.Visible = false;
            };
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            // Validate input if regex is provided
            if (_validationRegex != null)
            {
                if (!_validationRegex.IsMatch(_txtInput.Text))
                {
                    _lblError.Text = _validationError;
                    _lblError.Visible = true;
                    _txtInput.Focus();
                    _txtInput.SelectAll();
                    return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
