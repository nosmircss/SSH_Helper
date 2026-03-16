using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Services.Scripting.Commands;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class ScriptPromptDialogRunnerTests
{
    [WinFormsFact]
    public async Task ShowAsync_WhenPromptCloses_RequestsMainFormReactivation()
    {
        using var owner = new TestMainForm();

        owner.Show();
        Application.DoEvents();

        Form? restoredOwner = null;
        var restoreCalls = 0;
        ScriptPromptDialogRunner.RestoreMainFormActivationOverrideForTests = form =>
        {
            restoreCalls++;
            restoredOwner = form;
        };

        try
        {
            var resultTask = ScriptPromptDialogRunner.ShowAsync<TestPromptDialog, DialogResult>(
                () => new TestPromptDialog(),
                dialog => dialog.DialogResult,
                CancellationToken.None);

            Application.DoEvents();

            var prompt = Application.OpenForms.OfType<TestPromptDialog>().Single();
            prompt.DialogResult = DialogResult.Cancel;
            prompt.Close();
            Application.DoEvents();

            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(2));

            result.Should().Be(DialogResult.Cancel);
            restoreCalls.Should().Be(1);
            restoredOwner.Should().BeSameAs(owner);
        }
        finally
        {
            ScriptPromptDialogRunner.RestoreMainFormActivationOverrideForTests = null;
        }
    }

    private sealed class TestMainForm : Form
    {
        public TestMainForm()
        {
            Controls.Add(new Button
            {
                Name = "btnStopAll",
                Text = "Stop",
                Location = new System.Drawing.Point(10, 10)
            });
        }
    }

    private sealed class TestPromptDialog : Form
    {
    }
}
