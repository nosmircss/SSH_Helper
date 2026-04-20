using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using ScintillaNET;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1ExecutionCursorTests
{
    [WinFormsFact]
    public void SetExecutionMode_TogglesBusyCursorAcrossEditorAndOutputControls()
    {
        using var form = CreateForm();
        var commandEditor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");
        var outputTextBox = GetField<TextBox>(form, "txtOutput");
        var innerEditor = GetInnerEditor(commandEditor);

        InvokePrivateMethod(form, "SetExecutionMode", true);

        form.UseWaitCursor.Should().BeTrue();
        commandEditor.UseWaitCursor.Should().BeTrue();
        innerEditor.UseWaitCursor.Should().BeTrue();
        outputTextBox.UseWaitCursor.Should().BeTrue();

        InvokePrivateMethod(form, "SetExecutionMode", false);

        form.UseWaitCursor.Should().BeFalse();
        commandEditor.UseWaitCursor.Should().BeFalse();
        innerEditor.UseWaitCursor.Should().BeFalse();
        outputTextBox.UseWaitCursor.Should().BeFalse();
    }

    [WinFormsFact]
    public void SetExecutionMode_OverridesInnerCommandEditorCursorTypeWhileRunning_AndRestoresItAfterward()
    {
        const int scCursorWait = 4;

        using var form = CreateForm();
        var commandEditor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");
        var innerEditor = GetInnerEditor(commandEditor);
        _ = commandEditor.Handle;
        _ = innerEditor.Handle;
        var initialCommandCursor = commandEditor.Cursor;
        var initialInnerCursorType = GetScintillaCursorType(innerEditor);

        InvokePrivateMethod(form, "SetExecutionMode", true);

        commandEditor.Cursor.Should().Be(Cursors.WaitCursor);
        GetScintillaCursorType(innerEditor).Should().Be(scCursorWait);

        InvokePrivateMethod(form, "SetExecutionMode", false);

        commandEditor.Cursor.Should().Be(initialCommandCursor);
        GetScintillaCursorType(innerEditor).Should().Be(initialInnerCursorType);
    }

    private static SSH_Helper.Form1 CreateForm()
    {
        var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();
        return form;
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }

    private static Scintilla GetInnerEditor(ScintillaScriptEditorControl control)
    {
        var field = typeof(ScintillaScriptEditorControl).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull("_editor should exist on ScintillaScriptEditorControl");

        var value = field!.GetValue(control) as Scintilla;
        value.Should().NotBeNull("ScintillaScriptEditorControl should initialize its inner Scintilla editor");
        return value!;
    }

    private static int GetScintillaCursorType(Scintilla editor)
    {
        return editor.DirectMessage(2387).ToInt32();
    }

    private static object? InvokePrivateMethod(object instance, string methodName, params object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on {instance.GetType().Name}");
        return method!.Invoke(instance, args);
    }
}
