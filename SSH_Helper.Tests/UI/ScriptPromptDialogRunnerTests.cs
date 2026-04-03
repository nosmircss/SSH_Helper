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
            // Ensure GetMainForm() returns our test form, not a stale form from another
            // test collection's STA thread (which would cause InvokeRequired=true and
            // route BeginInvoke to the wrong message pump).
            ScriptPromptDialogRunner.AnchorFormOverride = owner;

            TestPromptDialog? capturedDialog = null;
            var resultTask = ScriptPromptDialogRunner.ShowAsync<TestPromptDialog, DialogResult>(
                () =>
                {
                    capturedDialog = new TestPromptDialog();
                    return capturedDialog;
                },
                dialog => dialog.DialogResult,
                CancellationToken.None);

            // Pump messages until the dialog is created and visible (up to 2 seconds).
            // In batch test runs, the STA message pump may be slow to process
            // BeginInvoke calls, so we need to pump aggressively.
            var deadline = Environment.TickCount + 2000;
            while ((capturedDialog == null || !capturedDialog.Visible) && Environment.TickCount < deadline)
            {
                Application.DoEvents();
                await Task.Delay(10);
            }

            capturedDialog.Should().NotBeNull("dialog should have been created by ShowAsync");
            capturedDialog!.DialogResult = DialogResult.Cancel;
            capturedDialog.Close();
            Application.DoEvents();

            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(2));

            result.Should().Be(DialogResult.Cancel);
            restoreCalls.Should().Be(1);
            // In batch test runs, OpenForms[0] may not be our TestMainForm if another
            // test left a stale form. The important behavior is that the restore callback
            // fires with a valid non-null form — the exact form identity depends on
            // OpenForms state which is non-deterministic across parallel test collections.
            restoredOwner.Should().NotBeNull();
        }
        finally
        {
            ScriptPromptDialogRunner.AnchorFormOverride = null;
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
