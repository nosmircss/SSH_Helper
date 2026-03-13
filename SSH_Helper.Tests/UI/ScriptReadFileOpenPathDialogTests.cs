using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Services.Scripting.Commands;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class ScriptReadFileOpenPathDialogTests
{
    [WinFormsFact]
    public void Constructor_WithLongCustomMessage_ReflowsControlsBelowPrompt()
    {
        using var defaultDialog = new ScriptReadFileOpenPathDialog("seed.txt");
        using var dialog = new ScriptReadFileOpenPathDialog(
            "seed.txt",
            "Select the configuration export to import into this run.\r\nThe chosen file should contain one entry per line and match the approved import types for this workflow.\r\nReview the contents before continuing.");

        var defaultPrompt = GetField<Label>(defaultDialog, "_lblPrompt");
        var defaultPathBox = GetField<TextBox>(defaultDialog, "_txtPath");
        var prompt = GetField<Label>(dialog, "_lblPrompt");
        var pathBox = GetField<TextBox>(dialog, "_txtPath");
        var okButton = GetField<Button>(dialog, "_btnOk");
        var errorLabel = GetField<Label>(dialog, "_lblError");

        prompt.Height.Should().BeGreaterThan(defaultPrompt.Height);
        pathBox.Top.Should().BeGreaterThan(defaultPathBox.Top);
        pathBox.Top.Should().BeGreaterThan(prompt.Bottom);
        okButton.Top.Should().BeGreaterThan(errorLabel.Bottom);
        dialog.ClientSize.Height.Should().BeGreaterThan(defaultDialog.ClientSize.Height);
    }

    [WinFormsFact]
    public void OkClick_WithDisallowedExtension_ShowsValidationError()
    {
        using var dialog = new ScriptReadFileOpenPathDialog(
            "seed.txt",
            "Pick the file to read.",
            [".txt", ".json"]);

        var pathBox = GetField<TextBox>(dialog, "_txtPath");
        var errorLabel = GetField<Label>(dialog, "_lblError");
        var okButton = GetField<Button>(dialog, "_btnOk");

        dialog.Show();
        Application.DoEvents();

        pathBox.Text = @"C:\temp\blocked.csv";
        okButton.PerformClick();
        Application.DoEvents();

        dialog.DialogResult.Should().NotBe(DialogResult.OK);
        errorLabel.Visible.Should().BeTrue();
        errorLabel.Text.Should().Contain(".txt");
        errorLabel.Text.Should().Contain(".json");

        dialog.Close();
    }

    private static T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"field '{fieldName}' should exist on {obj.GetType().Name}");
        return (T)field!.GetValue(obj)!;
    }
}
