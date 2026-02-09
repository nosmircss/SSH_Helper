using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using FluentAssertions;
using ScintillaNET;
using SSH_Helper.Models;
using SSH_Helper.Services.Editor;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class ScintillaScriptEditorControlTests
{
    [WinFormsFact]
    public void CompletionPopup_MouseUpClosesSuggestions()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "st";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var popup = GetCompletionPopup(control);
        popup.Visible.Should().BeTrue();

        InvokeNonPublic(control, "Editor_MouseUp", null, new MouseEventArgs(MouseButtons.Left, 1, 8, 8, 0));
        popup.Visible.Should().BeFalse();
    }

    [WinFormsFact]
    public void CompletionPopup_UpdateUISelection_RepositionsToCaret()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "na";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var popup = GetCompletionPopup(control);
        popup.Visible.Should().BeTrue();
        var initialLocation = popup.Location;

        var editor = GetInnerEditor(control);
        editor.GotoPosition(0);
        InvokeNonPublic(control, "Editor_UpdateUI", null, new UpdateUIEventArgs(UpdateChange.Selection));

        popup.Visible.Should().BeTrue();
        popup.Location.Should().NotBe(initialLocation);
    }

    [WinFormsFact]
    public void CompletionPopup_UpdateUIVScroll_HidesWhenCaretMovesOutOfViewport()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        var text = "na";
        for (var i = 0; i < 80; i++)
        {
            text += "\nline";
        }

        control.Text = text;
        control.SelectionStart = 2;
        control.SelectionLength = 0;

        var editor = GetInnerEditor(control);
        _ = editor.Handle;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var popup = GetCompletionPopup(control);
        popup.Visible.Should().BeTrue();

        editor.FirstVisibleLine = 40;
        InvokeNonPublic(control, "Editor_UpdateUI", null, new UpdateUIEventArgs(UpdateChange.VScroll));

        popup.Visible.Should().BeFalse();
    }

    [WinFormsFact]
    public void ScrollPastEnd_IsEnabledByDefault()
    {
        using var control = new ScintillaScriptEditorControl();
        var editor = GetInnerEditor(control);
        editor.EndAtLastLine.Should().BeFalse();
    }

    [WinFormsFact]
    public void CompletionNavigation_EnterCommitsAndEscapeDismissesWithoutMutation()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));

        control.Text = "st";
        control.SelectionStart = control.Text.Length;
        InvokeNonPublic(control, "ShowCompletionPopup");
        var enterHandled = (bool)InvokeNonPublic(control, "HandleCompletionNavigation", new KeyEventArgs(Keys.Enter))!;
        enterHandled.Should().BeTrue();
        control.Text.Should().NotBe("st");
        control.Text.Should().NotContain("\n");

        control.Text = "st";
        control.SelectionStart = control.Text.Length;
        InvokeNonPublic(control, "ShowCompletionPopup");
        var escapeHandled = (bool)InvokeNonPublic(control, "HandleCompletionNavigation", new KeyEventArgs(Keys.Escape))!;
        escapeHandled.Should().BeTrue();
        control.Text.Should().Be("st");
    }

    [WinFormsFact]
    public void CompletionCommit_TopLevelKey_AppendsColonAndSpace()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "na";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var handled = (bool)InvokeNonPublic(control, "HandleCompletionNavigation", new KeyEventArgs(Keys.Enter))!;

        handled.Should().BeTrue();
        control.Text.Should().Be("name: ");
    }

    [WinFormsFact]
    public void CompletionCommit_StepCommand_AppendsColonAndSpace()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "steps:\n  - sen";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var handled = (bool)InvokeNonPublic(control, "HandleCompletionNavigation", new KeyEventArgs(Keys.Enter))!;

        handled.Should().BeTrue();
        NormalizeLineEndings(control.Text).Should().EndWith("\n  - send: ");
    }

    [WinFormsFact]
    public void CompletionCommit_StepOptionKey_AppendsColonAndSpace()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "steps:\n  - send:\n      command: hostname\n      capt";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var handled = (bool)InvokeNonPublic(control, "HandleCompletionNavigation", new KeyEventArgs(Keys.Enter))!;

        handled.Should().BeTrue();
        NormalizeLineEndings(control.Text).Should().EndWith("\n      capture: ");
    }

    [WinFormsFact]
    public void CompletionCommit_OptionValue_DoesNotAppendColonAndSpace()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "steps:\n  - http:\n      method: P";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var handled = (bool)InvokeNonPublic(control, "HandleCompletionNavigation", new KeyEventArgs(Keys.Enter))!;

        handled.Should().BeTrue();
        NormalizeLineEndings(control.Text).Should().Contain("method: ");
        NormalizeLineEndings(control.Text).Should().NotContain("method: P:");
    }

    [WinFormsFact]
    public void ApplyTheme_DarkMode_UsesDarkCompletionPalette()
    {
        using var control = new ScintillaScriptEditorControl();
        control.ApplyTheme(true);

        var popup = GetCompletionPopup(control);
        var list = GetCompletionList(control);

        popup.BackColor.Should().Be(Color.FromArgb(88, 88, 91));
        list.BackColor.Should().Be(Color.FromArgb(45, 45, 46));
        list.ForeColor.Should().Be(Color.FromArgb(220, 220, 220));
    }

    [WinFormsFact]
    public void SetDiagnostics_RespectsShowInlineWarningsSetting()
    {
        using var control = new ScintillaScriptEditorControl();
        control.Text = "steps:\n  - send: value";
        var editor = GetInnerEditor(control);
        var warningPosition = control.Text.IndexOf("value", StringComparison.Ordinal);

        var warning = new EditorDiagnostic
        {
            LineNumber = 2,
            ColumnStart = 11,
            ColumnEnd = 15,
            Severity = DiagnosticSeverity.Warning,
            Message = "warning"
        };

        control.ApplyCommandEditorSettings(new CommandEditorSettings { ShowInlineWarnings = false });
        control.SetDiagnostics([warning]);
        editor.IndicatorAllOnFor(warningPosition).Should().Be(0u);

        control.ApplyCommandEditorSettings(new CommandEditorSettings { ShowInlineWarnings = true });
        control.SetDiagnostics([warning]);
        editor.IndicatorAllOnFor(warningPosition).Should().NotBe(0u);
    }

    [WinFormsFact]
    public void SmartEnter_IsSingleUndoableEditUnit()
    {
        using var control = new ScintillaScriptEditorControl();
        var originalText = "steps:\n  - send: hi";
        control.Text = originalText;
        control.SelectionStart = originalText.Length;
        control.SelectionLength = 0;

        var handled = (bool)InvokeNonPublic(control, "HandleSmartEnter", new KeyEventArgs(Keys.Enter))!;
        handled.Should().BeTrue();
        control.Text.Should().NotBe(originalText);

        var editor = GetInnerEditor(control);
        editor.CanUndo.Should().BeTrue();
        editor.Undo();
        control.Text.Should().Be(originalText);
    }

    [WinFormsFact]
    public void SmartEnter_CtrlEnter_InsertsSiblingStepPrefix()
    {
        using var control = new ScintillaScriptEditorControl();
        control.Text = "steps:\n  - send:\n      command: hi";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        var handled = (bool)InvokeNonPublic(control, "HandleSmartEnter", new KeyEventArgs(Keys.Control | Keys.Enter))!;

        handled.Should().BeTrue();
        NormalizeLineEndings(control.Text).Should().EndWith("\n  - ");
    }

    [WinFormsFact]
    public void GetCaretPosition_ReturnsOneBasedLineAndColumn()
    {
        using var control = new ScintillaScriptEditorControl();
        control.Text = "line1\nline2";
        var editor = GetInnerEditor(control);
        var target = control.Text.IndexOf("line2", StringComparison.Ordinal) + 3;
        editor.GotoPosition(target);

        var (line, column) = control.GetCaretPosition();
        line.Should().Be(2);
        column.Should().Be(4);
    }

    private static Scintilla GetInnerEditor(ScintillaScriptEditorControl control)
    {
        var field = typeof(ScintillaScriptEditorControl).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (Scintilla)field!.GetValue(control)!;
    }

    private static Panel GetCompletionPopup(ScintillaScriptEditorControl control)
    {
        var field = typeof(ScintillaScriptEditorControl).GetField("_completionPopup", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (Panel)field!.GetValue(control)!;
    }

    private static ListBox GetCompletionList(ScintillaScriptEditorControl control)
    {
        var field = typeof(ScintillaScriptEditorControl).GetField("_completionList", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (ListBox)field!.GetValue(control)!;
    }

    private static object? InvokeNonPublic(object instance, string methodName, params object?[]? parameters)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"private method '{methodName}' should exist for this test");
        return method!.Invoke(instance, parameters);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
