using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1ScriptContextMenuTests
{
    [WinFormsFact]
    public void CommentAndUncommentMenuClick_MultiLineSelection_TogglesIndentAwareHashPrefixes()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var editor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");
        var commentMenuItem = GetField<ToolStripMenuItem>(form, "ctxCommentSelectedLines");
        var uncommentMenuItem = GetField<ToolStripMenuItem>(form, "ctxUncommentSelectedLines");

        editor.Text = "steps:\n  - send:\n      command: hi\nprint: done";
        var selectionStart = editor.Text.IndexOf("- send:", StringComparison.Ordinal);
        var selectionEnd = editor.Text.Length;
        editor.SelectionStart = selectionStart;
        editor.SelectionLength = selectionEnd - selectionStart;

        commentMenuItem.PerformClick();

        editor.Text.Should().Be("steps:\n  #- send:\n      #command: hi\n#print: done");

        editor.SelectionStart = selectionStart;
        editor.SelectionLength = editor.Text.Length - selectionStart;

        uncommentMenuItem.PerformClick();

        editor.Text.Should().Be("steps:\n  - send:\n      command: hi\nprint: done");
    }

    [WinFormsFact]
    public void PathBrowserMenuClick_InsideDoubleQuotes_ConvertsToSingleQuotedYamlPath()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var editor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");
        var pathBrowserMenuItem = GetField<ToolStripMenuItem>(form, "ctxPathBrowser");

        SetField(
            form,
            "_filePathPickerOverrideForTests",
            new Func<IWin32Window?, string?>(_ => @"C:\samples\foo.wav"));

        var initialText = "path: \"\"";
        var insertIndex = initialText.IndexOf("\"\"", StringComparison.Ordinal) + 1;
        editor.Text = initialText;
        editor.SelectionStart = insertIndex;
        editor.SelectionLength = 0;

        pathBrowserMenuItem.PerformClick();

        editor.Text.Should().Be("path: 'C:\\samples\\foo.wav'");
        editor.SelectionStart.Should().Be(editor.Text.Length);
        editor.SelectionLength.Should().Be(0);
    }

    [WinFormsFact]
    public void PathBrowserMenuClick_OutsideDoubleQuotes_InsertsRawPathAtCaret()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var editor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");
        var pathBrowserMenuItem = GetField<ToolStripMenuItem>(form, "ctxPathBrowser");

        SetField(
            form,
            "_filePathPickerOverrideForTests",
            new Func<IWin32Window?, string?>(_ => @"C:\samples\foo.wav"));

        editor.Text = "path: ";
        editor.SelectionStart = editor.Text.Length;
        editor.SelectionLength = 0;

        pathBrowserMenuItem.PerformClick();

        editor.Text.Should().Be("path: C:\\samples\\foo.wav");
        editor.SelectionStart.Should().Be(editor.Text.Length);
        editor.SelectionLength.Should().Be(0);
    }

    [WinFormsFact]
    public void PathBrowserMenuClick_AfterLoneDoubleQuote_ConvertsToSingleQuotedYamlPath()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var editor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");
        var pathBrowserMenuItem = GetField<ToolStripMenuItem>(form, "ctxPathBrowser");

        SetField(
            form,
            "_filePathPickerOverrideForTests",
            new Func<IWin32Window?, string?>(_ => @"C:\samples\foo.wav"));

        editor.Text = "path: \"";
        editor.SelectionStart = editor.Text.Length;
        editor.SelectionLength = 0;

        pathBrowserMenuItem.PerformClick();

        editor.Text.Should().Be("path: 'C:\\samples\\foo.wav'");
        editor.SelectionStart.Should().Be(editor.Text.Length);
        editor.SelectionLength.Should().Be(0);
    }

    [WinFormsFact]
    public void PathBrowserMenuClick_AfterLoneSingleQuote_ConvertsToSingleQuotedYamlPath()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var editor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");
        var pathBrowserMenuItem = GetField<ToolStripMenuItem>(form, "ctxPathBrowser");

        SetField(
            form,
            "_filePathPickerOverrideForTests",
            new Func<IWin32Window?, string?>(_ => @"C:\samples\foo.wav"));

        editor.Text = "path: '";
        editor.SelectionStart = editor.Text.Length;
        editor.SelectionLength = 0;

        pathBrowserMenuItem.PerformClick();

        editor.Text.Should().Be("path: 'C:\\samples\\foo.wav'");
        editor.SelectionStart.Should().Be(editor.Text.Length);
        editor.SelectionLength.Should().Be(0);
    }

    [WinFormsFact]
    public void PathBrowserMenuClick_AfterClosingDoubleQuoteOfEmptyPair_ConvertsToSingleQuotedYamlPath()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var editor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");
        var pathBrowserMenuItem = GetField<ToolStripMenuItem>(form, "ctxPathBrowser");

        SetField(
            form,
            "_filePathPickerOverrideForTests",
            new Func<IWin32Window?, string?>(_ => @"C:\samples\foo.wav"));

        editor.Text = "path: \"\"";
        editor.SelectionStart = editor.Text.Length;
        editor.SelectionLength = 0;

        pathBrowserMenuItem.PerformClick();

        editor.Text.Should().Be("path: 'C:\\samples\\foo.wav'");
        editor.SelectionStart.Should().Be(editor.Text.Length);
        editor.SelectionLength.Should().Be(0);
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }

    private static void SetField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");
        field!.SetValue(instance, value);
    }
}
