using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Moq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1ExitWorkflowTests
{
    [WinFormsTheory]
    [InlineData(DialogResult.No, true, false)]
    [InlineData(DialogResult.Cancel, false, false)]
    [InlineData(DialogResult.No, true, true)]
    [InlineData(DialogResult.Cancel, false, true)]
    [InlineData(DialogResult.Yes, true, true)]
    public void ClosingWithUnsavedCsv_RespectsPromptChoice(DialogResult choice, bool shouldClose, bool validationCancelled)
    {
        AssertCloseBehavior(choice, shouldClose, validationCancelled);
    }

    [WinFormsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosingWithSavedCsv_OverridesValidationCancellation(bool viaMenu)
    {
        AssertCloseBehavior(null, shouldClose: true, validationCancelled: true, viaMenu);
    }

    private static void AssertCloseBehavior(DialogResult? choice, bool shouldClose, bool validationCancelled, bool viaMenu = false)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"Form1ExitWorkflowTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var form = new Form1();
            var configService = GetField<ConfigurationService>(form, "_configService");
            typeof(ConfigurationService).GetField("_configFilePath", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(configService, Path.Combine(directory, "config.json"));
            configService.Save(new AppConfiguration());
            SetField(form, "_credentialProvider", Mock.Of<ICredentialProvider>());
            SetField(form, "_selectedFolderName", "test-folder");
            SetField(form, "_deferredSchedulerBootstrapStarted", true);
            form.Show();
            Application.DoEvents();
            var grid = GetField<DataGridView>(form, "dgv_variables");
            if (!grid.Columns.Contains("Host_IP"))
                grid.Columns.Add("Host_IP", "Host_IP");
            var row = grid.Rows.Add();
            grid.Rows[row].Cells["Host_IP"].Value = "127.0.0.1";
            grid.CurrentCell = grid.Rows[row].Cells["Host_IP"];
            grid.Focus();
            grid.BeginEdit(true);
            ((TextBox)grid.EditingControl!).Text = "127.0.0.2";
            if (choice == null)
                grid.EndEdit();
            SetField(form, "_csvDirty", choice != null);
            var csvPath = Path.Combine(directory, "hosts.csv");
            SetField(form, "_loadedFilePath", csvPath);
            if (validationCancelled)
                grid.Validating += (_, e) => e.Cancel = true;

            var prompts = new List<string>();
            using var timer = new System.Windows.Forms.Timer { Interval = 20 };
            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f != form && f.Modal);
                if (dialog == null)
                    return;
                prompts.Add(dialog.Text);
                var button = dialog.Controls.OfType<Button>().SingleOrDefault(b => b.DialogResult == choice);
                if (button != null && prompts.Count == 1)
                    button.PerformClick();
                else
                    dialog.DialogResult = DialogResult.Cancel;
            };
            timer.Start();

            if (viaMenu)
                typeof(Form1).GetMethod("ExitMenuItem_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, new object[] { form, EventArgs.Empty });
            else
                form.Close();

            if (choice != null)
                prompts.Should().Equal("Save Changes");
            else
                prompts.Should().BeEmpty();
            form.IsDisposed.Should().Be(shouldClose);
            File.Exists(csvPath).Should().Be(choice == DialogResult.Yes);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static T GetField<T>(object instance, string name) =>
        (T)typeof(Form1).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void SetField(object instance, string name, object value) =>
        typeof(Form1).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
}
