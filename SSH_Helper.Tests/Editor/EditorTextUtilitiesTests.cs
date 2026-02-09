using FluentAssertions;
using SSH_Helper.Services.Editor;
using Xunit;

namespace SSH_Helper.Tests.Editor;

public class EditorTextUtilitiesTests
{
    [Fact]
    public void ApplyIndentation_MultiLineSelection_IndentsEachLineByTwoSpaces()
    {
        var text = "steps:\n  - send: one\n  - send: two";
        var selectionStart = text.IndexOf("  - send: one", StringComparison.Ordinal);
        var selectionLength = text.Length - selectionStart;

        var edit = EditorTextUtilities.ApplyIndentation(
            text,
            selectionStart,
            selectionLength,
            indentSize: 2,
            outdent: false,
            useSpacesForTab: true);

        NormalizeLineEndings(edit.Text).Should().Contain("    - send: one");
        NormalizeLineEndings(edit.Text).Should().Contain("    - send: two");
    }

    [Fact]
    public void ApplyIndentation_MultiLineSelection_OutdentsByConfiguredWidth()
    {
        var text = "steps:\n    - send: one\n    - send: two";
        var selectionStart = text.IndexOf("    - send: one", StringComparison.Ordinal);
        var selectionLength = text.Length - selectionStart;

        var edit = EditorTextUtilities.ApplyIndentation(
            text,
            selectionStart,
            selectionLength,
            indentSize: 2,
            outdent: true,
            useSpacesForTab: true);

        NormalizeLineEndings(edit.Text).Should().Contain("  - send: one");
        NormalizeLineEndings(edit.Text).Should().Contain("  - send: two");
    }

    [Fact]
    public void ApplySmartEnter_AfterStepLine_ContinuesOptionIndentation()
    {
        var text = "steps:\n  - send: show version";

        var edit = EditorTextUtilities.ApplySmartEnter(
            text,
            text.Length,
            selectionLength: 0,
            indentSize: 2,
            preserveBlankLineBetweenSteps: true);

        NormalizeLineEndings(edit.Text).Should().EndWith("\n    ");
    }

    [Fact]
    public void ApplySmartEnter_AfterMappingKey_InsertsIndentedLine()
    {
        var text = "steps:\n  - http:";

        var edit = EditorTextUtilities.ApplySmartEnter(
            text,
            text.Length,
            selectionLength: 0,
            indentSize: 2,
            preserveBlankLineBetweenSteps: true);

        NormalizeLineEndings(edit.Text).Should().EndWith("\n    ");
    }

    [Fact]
    public void ApplySmartEnter_OnBlankLineBetweenSteps_PreservesBlankSeparator()
    {
        var text = "steps:\n  - send: first\n\n  - send: second";
        var caret = text.IndexOf("\n\n", StringComparison.Ordinal) + 1;

        var edit = EditorTextUtilities.ApplySmartEnter(
            text,
            caret,
            selectionLength: 0,
            indentSize: 2,
            preserveBlankLineBetweenSteps: true);

        NormalizeLineEndings(edit.Text).Should().Contain("\n\n\n  - send: second");
    }

    [Fact]
    public void ApplySmartEnter_AfterTopLevelScalarBeforeSectionHeader_KeepsCaretOnInsertedLine()
    {
        var text = "name: QA Send Basic\ndebug: true\n\nsteps:\n  - send: whoami";
        var caret = text.IndexOf("debug: true", StringComparison.Ordinal) + "debug: true".Length;

        var edit = EditorTextUtilities.ApplySmartEnter(
            text,
            caret,
            selectionLength: 0,
            indentSize: 2,
            preserveBlankLineBetweenSteps: true);

        NormalizeLineEndings(edit.Text).Should().Contain("debug: true\n\n\nsteps:");

        var beforeCaret = NormalizeLineEndings(edit.Text.Substring(0, edit.SelectionStart));
        beforeCaret.Should().EndWith("debug: true\n");

        var afterCaret = NormalizeLineEndings(edit.Text.Substring(edit.SelectionStart));
        afterCaret.Should().StartWith("\n\nsteps:");
    }

    [Fact]
    public void ApplySmartEnter_OnStepOptionLine_KeepsSameIndentBeforeNextStep()
    {
        var text = "steps:\n  - send: whoami\n    capture: current_user\n  - print: done";
        var caret = text.IndexOf("capture: current_user", StringComparison.Ordinal) + "capture: current_user".Length;

        var edit = EditorTextUtilities.ApplySmartEnter(
            text,
            caret,
            selectionLength: 0,
            indentSize: 2,
            preserveBlankLineBetweenSteps: true);

        NormalizeLineEndings(edit.Text).Should().Contain("capture: current_user\n    \n  - print: done");

        var beforeCaret = edit.Text.Substring(0, edit.SelectionStart);
        NormalizeLineEndings(beforeCaret).Should().EndWith("\n    ");

        var afterCaret = edit.Text.Substring(edit.SelectionStart);
        NormalizeLineEndings(afterCaret).Should().StartWith("\n  - print: done");
    }

    [Fact]
    public void ApplyIndentation_OnBlankLine_DoesNotIndentFollowingStep()
    {
        var text = "steps:\n  - send: hostname\n    capture: host_result\n\n  - print: \"Captured hostname\"";
        var blankLineStart = text.IndexOf("\n\n", StringComparison.Ordinal) + 1;

        var edit = EditorTextUtilities.ApplyIndentation(
            text,
            blankLineStart,
            selectionLength: 0,
            indentSize: 2,
            outdent: false,
            useSpacesForTab: true);

        NormalizeLineEndings(edit.Text).Should().Contain("capture: host_result\n  \n  - print:");
    }

    [Fact]
    public void ApplySiblingStepEnter_FromCommandMapPayload_InsertsSiblingStepPrefix()
    {
        var text = "steps:\n  - send:\n      command: show version";

        var edit = EditorTextUtilities.ApplySiblingStepEnter(
            text,
            text.Length,
            selectionLength: 0,
            indentSize: 2);

        NormalizeLineEndings(edit.Text).Should().EndWith("\n  - ");
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
