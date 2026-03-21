using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class BrowserCallbackWebViewDialogTests
{
    private static T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"field '{fieldName}' should exist on {obj.GetType().Name}");
        return (T)field!.GetValue(obj)!;
    }

    [WinFormsFact]
    public void SetCompletionState_UpdatesInstructionsAndActionText()
    {
        using var dialog = new BrowserCallbackWebViewDialog(
            "https://example.com/start",
            Path.Combine(Path.GetTempPath(), $"BrowserCallbackWebViewDialogTests_{Guid.NewGuid():N}"),
            darkMode: true);

        var closeButton = GetField<Button>(dialog, "_btnClose");
        var instructions = GetField<Label>(dialog, "_lblInstructions");

        closeButton.Text.Should().Be("Cancel");
        instructions.Text.Should().Contain("cancel");

        var completionMethod = dialog.GetType().GetMethod("SetCompletionState", BindingFlags.NonPublic | BindingFlags.Instance);
        completionMethod.Should().NotBeNull("the dialog should expose a completion-state transition for keep-open callback flows");

        completionMethod!.Invoke(dialog, Array.Empty<object>());

        closeButton.Text.Should().Be("Close");
        instructions.Text.Should().Contain("complete");
        instructions.Text.Should().NotContain("cancel");
    }
}
