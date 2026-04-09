using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SSH_Helper.Services.Scripting.Commands;

namespace SSH_Helper.UI
{
    internal sealed class LocalCmdConfirmationDialog : ILocalCmdConfirmation
    {
        public Task<LocalCmdConfirmResult> ConfirmAsync(string resolvedCommand, string shell, string workingDir, CancellationToken cancellationToken)
        {
            return ScriptPromptDialogRunner.ShowAsync<LocalCmdConfirmationForm, LocalCmdConfirmResult>(
                () => new LocalCmdConfirmationForm(resolvedCommand, shell, workingDir),
                dialog => dialog.SelectedResult,
                cancellationToken);
        }
    }

    internal sealed class LocalCmdConfirmationForm : Form
    {
        public LocalCmdConfirmResult SelectedResult { get; private set; } = LocalCmdConfirmResult.Cancel;

        public LocalCmdConfirmationForm(string resolvedCommand, string shell, string workingDir)
        {
            Text = "Local Command Confirmation";
            Size = new Size(540, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var headerLabel = new Label
            {
                Text = "A script wants to run a local command:",
                AutoSize = true,
                Location = new Point(16, 16),
                Font = new Font(Font, FontStyle.Bold),
            };
            Controls.Add(headerLabel);

            var commandBox = new TextBox
            {
                Text = resolvedCommand,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(16, 48),
                Size = new Size(492, 112),
                Font = new Font("Consolas", 9.5f),
            };
            Controls.Add(commandBox);

            var shellLabel = new Label
            {
                Text = $"Shell: {shell}",
                AutoSize = true,
                Location = new Point(16, 172),
            };
            Controls.Add(shellLabel);

            var dirLabel = new Label
            {
                Text = $"Directory: {workingDir}",
                AutoSize = true,
                Location = new Point(16, 196),
            };
            Controls.Add(dirLabel);

            var scopeLabel = new Label
            {
                Text = "Run Same Command approves this resolved command for the current host for the rest of this run.",
                AutoSize = false,
                Location = new Point(16, 220),
                Size = new Size(492, 32),
            };
            Controls.Add(scopeLabel);

            var btnRun = new Button
            {
                Text = "Run",
                Size = new Size(90, 32),
                Location = new Point(90, 266),
                DialogResult = DialogResult.OK,
            };
            btnRun.Click += (_, _) =>
            {
                SelectedResult = LocalCmdConfirmResult.Run;
                Close();
            };
            Controls.Add(btnRun);

            var btnRunAll = new Button
            {
                Text = "Run Same Command",
                Size = new Size(140, 32),
                Location = new Point(195, 266),
                DialogResult = DialogResult.Yes,
            };
            btnRunAll.Click += (_, _) =>
            {
                SelectedResult = LocalCmdConfirmResult.RunAll;
                Close();
            };
            Controls.Add(btnRunAll);

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(90, 32),
                Location = new Point(350, 266),
                DialogResult = DialogResult.Cancel,
            };
            btnCancel.Click += (_, _) =>
            {
                SelectedResult = LocalCmdConfirmResult.Cancel;
                Close();
            };
            Controls.Add(btnCancel);

            AcceptButton = btnRun;
            CancelButton = btnCancel;

            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            var isDark = mainForm != null && mainForm.BackColor.GetBrightness() < 0.2f;
            if (isDark)
            {
                DialogTheme.ApplyTo(this, true);
                DialogTheme.StyleButton(btnRun, true, isPrimary: true);
                DialogTheme.StyleButton(btnRunAll, true);
                DialogTheme.StyleButton(btnCancel, true);
                DialogTheme.SetDarkTitleBar(this, true);
            }

            FormClosed += (_, _) =>
            {
                if (DialogResult != DialogResult.OK && DialogResult != DialogResult.Yes)
                    SelectedResult = LocalCmdConfirmResult.Cancel;
            };

            Load += (_, _) =>
            {
                if (isDark)
                    DialogTheme.ApplyNativeTheme(this, true);
            };
        }
    }
}
