using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using System.Text;
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
    public void CompletionPopup_OutsideClickDismissesSuggestions()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "st";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var popup = GetCompletionPopup(control);
        popup.Visible.Should().BeTrue();

        using var outside = new TextBox();
        _ = outside.Handle;
        InvokeNonPublic(control, "DismissCompletionOnExternalClick", outside.Handle);

        popup.Visible.Should().BeFalse();
    }

    [WinFormsFact]
    public void CompletionPopup_UpdateUISelection_RepositionsToCaret()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "steps:\n  - sen";
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
    public void CompletionPopup_BlankHeaderLine_AutoRequestShowsSuggestions()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "name: demo\nversion: 1\n";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");

        GetCompletionPopup(control).Visible.Should().BeTrue();
    }

    [WinFormsFact]
    public void CompletionPopup_BlankTopLevelLine_AfterSteps_AutoRequestStaysHidden()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "name: demo\nvars:\n  token: abc\nsteps:\n  - send: ok\n";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");

        GetCompletionPopup(control).Visible.Should().BeFalse();
    }

    [WinFormsFact]
    public void CompletionPopup_BlankTopLevelLine_AfterSteps_CtrlSpaceShowsStepCommands()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "name: demo\nvars:\n  token: abc\nsteps:\n  - send: ok\n";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "Editor_KeyDown", null, new KeyEventArgs(Keys.Control | Keys.Space));

        var popup = GetCompletionPopup(control);
        var list = GetCompletionList(control);
        popup.Visible.Should().BeTrue();
        list.Items.OfType<CompletionItem>().Select(item => item.Label).Should().Contain("send");
    }

    [WinFormsFact]
    public void CompletionPopup_BlankLine_AfterIndentlessStepsSequence_CtrlSpaceShowsStepCommands()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "steps:\n- send:\n    command: df\n\n- extract:\n    from: ${Host_IP}\n    into: foo\n    pattern: .*\n\n";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "Editor_KeyDown", null, new KeyEventArgs(Keys.Control | Keys.Space));

        var popup = GetCompletionPopup(control);
        var list = GetCompletionList(control);
        popup.Visible.Should().BeTrue();
        list.Items.OfType<CompletionItem>().Select(item => item.Label).Should().Contain("send");
        list.Items.OfType<CompletionItem>().Should().OnlyContain(item => item.InsertText.StartsWith("- ", StringComparison.Ordinal));
    }

    [WinFormsFact]
    public void CompletionPopup_PrintScreenKeyUp_DoesNotTriggerSuggestions()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "steps:\n  - dns:\n      ";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "Editor_KeyUp", null, new KeyEventArgs(Keys.Snapshot));

        GetCompletionPopup(control).Visible.Should().BeFalse();
    }

    [WinFormsFact]
    public void CompletionPopup_UpdateUISelection_HidesRootSuggestionsWhenCaretMovesBelowSteps()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "name: demo\n\nvars:\n  token: abc\nsteps:\n  - send:\n      command: ok\n";
        control.SelectionStart = "name: demo\n".Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var popup = GetCompletionPopup(control);
        popup.Visible.Should().BeTrue();

        var editor = GetInnerEditor(control);
        editor.GotoPosition(control.Text.Length);
        InvokeNonPublic(control, "Editor_UpdateUI", null, new UpdateUIEventArgs(UpdateChange.Selection));

        popup.Visible.Should().BeFalse();
    }

    [WinFormsFact]
    public void Tab_OnTrailingBlankLine_IndentsCurrentBlankLine()
    {
        using var control = new ScintillaScriptEditorControl();
        control.ApplyCommandEditorSettings(new CommandEditorSettings
        {
            IndentSize = 2,
            UseSpacesForTab = true
        });
        control.Text = "steps:\n  - print:\n      message: \"test\"\n";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "Editor_KeyDown", null, new KeyEventArgs(Keys.Tab));

        NormalizeLineEndings(control.Text).Should().Be("steps:\n  - print:\n      message: \"test\"\n  ");
        control.SelectionStart.Should().Be(control.Text.Length);
        control.SelectionLength.Should().Be(0);
    }

    [WinFormsFact]
    public void ScrollPastEnd_IsEnabledByDefault()
    {
        using var control = new ScintillaScriptEditorControl();
        var editor = GetInnerEditor(control);
        editor.EndAtLastLine.Should().BeFalse();
    }

    [WinFormsFact]
    public void LineNumberMargin_IsEnabledByDefault()
    {
        using var control = new ScintillaScriptEditorControl();
        var editor = GetInnerEditor(control);

        editor.Margins[0].Type.Should().Be(MarginType.Number);
        editor.Margins[0].Width.Should().BeGreaterThan(0);
        editor.Margins[1].Width.Should().BeGreaterThan(0);
    }

    [WinFormsFact]
    public void LineNumberMargin_ExpandsWhenLineCountReachesThreeDigits()
    {
        using var control = new ScintillaScriptEditorControl();
        control.Text = "line 1";
        var editor = GetInnerEditor(control);
        var smallMarginWidth = editor.Margins[0].Width;

        var sb = new StringBuilder();
        for (var i = 1; i <= 120; i++)
        {
            sb.Append("line ").Append(i).Append('\n');
        }

        control.Text = sb.ToString();
        editor.Margins[0].Width.Should().BeGreaterThan(smallMarginWidth);
    }

    [WinFormsFact]
    public void LineNumberMargin_ProvidesVisibleGapAfterDigits()
    {
        using var control = new ScintillaScriptEditorControl();
        var sb = new StringBuilder();
        for (var i = 1; i <= 120; i++)
        {
            sb.Append("line ").Append(i).Append('\n');
        }

        control.Text = sb.ToString();
        var editor = GetInnerEditor(control);
        var lineDigitsWidth = editor.TextWidth(Style.LineNumber, "999");
        var combinedMarginWidth = editor.Margins[0].Width + editor.Margins[1].Width;

        editor.Margins[1].Width.Should().BeGreaterThanOrEqualTo(8);
        combinedMarginWidth.Should().BeGreaterThan(lineDigitsWidth + 8);
    }

    [WinFormsFact]
    public void TextSetter_WhenReadOnly_StillAppliesProgrammaticUpdates()
    {
        using var control = new ScintillaScriptEditorControl();
        control.Text = "first folder";
        control.ReadOnly = true;

        control.Text = "second folder";

        control.Text.Should().Be("second folder");
        control.ReadOnly.Should().BeTrue();
    }

    [WinFormsFact]
    public void Clear_WhenReadOnly_ClearsProgrammaticallyAndPreservesReadOnly()
    {
        using var control = new ScintillaScriptEditorControl();
        control.Text = "folder summary";
        control.ReadOnly = true;

        control.Clear();

        control.Text.Should().BeEmpty();
        control.ReadOnly.Should().BeTrue();
    }

    [WinFormsFact]
    public void ApplyCommandEditorSettings_VisualOptions_EnableScintillaVisualAids()
    {
        using var control = new ScintillaScriptEditorControl();
        control.ApplyCommandEditorSettings(new CommandEditorSettings
        {
            EnableCurrentLineHighlight = true,
            EnableIndentGuides = true,
            ShowWhitespace = true,
            EnableLongLineGuide = true,
            LongLineColumn = 132,
            EnableCodeFolding = true,
            EnableBraceMatching = true
        });

        var editor = GetInnerEditor(control);
        editor.CaretLineBackColor.A.Should().BeGreaterThan(0);
        editor.IndentationGuides.Should().Be(IndentView.LookBoth);
        editor.ViewWhitespace.Should().Be(WhitespaceMode.VisibleAlways);
        editor.EdgeMode.Should().Be(EdgeMode.Line);
        editor.EdgeColumn.Should().Be(132);
        editor.Margins[2].Type.Should().Be(MarginType.Symbol);
        editor.Margins[2].Width.Should().BeGreaterThan(0);
    }

    [WinFormsFact]
    public void ApplyCommandEditorSettings_VisualOptions_DisableScintillaVisualAids()
    {
        using var control = new ScintillaScriptEditorControl();
        control.ApplyCommandEditorSettings(new CommandEditorSettings
        {
            EnableCurrentLineHighlight = false,
            EnableIndentGuides = false,
            ShowWhitespace = false,
            EnableLongLineGuide = false,
            EnableCodeFolding = false,
            EnableBraceMatching = false
        });

        var editor = GetInnerEditor(control);
        editor.CaretLineBackColor.A.Should().Be(0);
        editor.IndentationGuides.Should().Be(IndentView.None);
        editor.ViewWhitespace.Should().Be(WhitespaceMode.Invisible);
        editor.EdgeMode.Should().Be(EdgeMode.None);
        editor.Margins[2].Width.Should().Be(0);
    }

    [WinFormsFact]
    public void CodeFolding_YamlStructure_MarksFoldHeaders()
    {
        using var control = new ScintillaScriptEditorControl();
        control.ApplyCommandEditorSettings(new CommandEditorSettings
        {
            EnableCodeFolding = true
        });

        control.Text = "name: demo\nsteps:\n  - send:\n      command: show version\n      capture: out";

        var editor = GetInnerEditor(control);
        var stepsLine = editor.Lines[1];
        var sendLine = editor.Lines[2];

        (stepsLine.FoldLevelFlags & FoldLevelFlags.Header).Should().NotBe(0);
        (sendLine.FoldLevelFlags & FoldLevelFlags.Header).Should().NotBe(0);
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
        NormalizeLineEndings(control.Text).Should().StartWith("steps: ");

        control.Text = "st";
        control.SelectionStart = control.Text.Length;
        InvokeNonPublic(control, "ShowCompletionPopup");
        var escapeHandled = (bool)InvokeNonPublic(control, "HandleCompletionNavigation", new KeyEventArgs(Keys.Escape))!;
        escapeHandled.Should().BeTrue();
        control.Text.Should().Be("st");
    }

    [WinFormsFact]
    public void CompletionPopup_MouseClickOnSuggestion_CommitsClickedItem()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "steps:\n  - ";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var popup = GetCompletionPopup(control);
        var list = GetCompletionList(control);
        popup.Visible.Should().BeTrue();
        list.Items.Count.Should().BeGreaterThan(1, "step-command completion should provide multiple choices");

        var clickedIndex = 1;
        var clickedItem = (CompletionItem)list.Items[clickedIndex]!;
        var clickX = 6;
        var clickY = (clickedIndex * list.ItemHeight) + Math.Max(2, list.ItemHeight / 2);
        var lParam = NativeMethods.MakeLParam(clickX, clickY);

        NativeMethods.SendMessage(list.Handle, NativeMethods.WM_LBUTTONDOWN, (IntPtr)NativeMethods.MK_LBUTTON, lParam);
        Application.DoEvents();
        NativeMethods.SendMessage(list.Handle, NativeMethods.WM_LBUTTONUP, IntPtr.Zero, lParam);

        NormalizeLineEndings(control.Text).Should().EndWith($"\n  - {clickedItem.InsertText}: ");
    }

    [WinFormsFact]
    public void CompletionPopup_ListFocus_DoesNotDismissSuggestions()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "st";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var popup = GetCompletionPopup(control);
        var list = GetCompletionList(control);
        popup.Visible.Should().BeTrue();

        list.Focus();
        Application.DoEvents();

        popup.Visible.Should().BeTrue();
    }

    [WinFormsFact]
    public void CompletionPopup_NativeChildHandleClick_IsNotTreatedAsExternal()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "st";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var popup = GetCompletionPopup(control);
        var list = GetCompletionList(control);
        popup.Visible.Should().BeTrue();

        var nativeChild = NativeMethods.CreateWindowEx(
            0,
            "STATIC",
            string.Empty,
            NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE,
            0,
            0,
            8,
            8,
            list.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        nativeChild.Should().NotBe(IntPtr.Zero);

        try
        {
            InvokeNonPublic(control, "DismissCompletionOnExternalClick", nativeChild);
            popup.Visible.Should().BeTrue();
        }
        finally
        {
            _ = NativeMethods.DestroyWindow(nativeChild);
        }
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
        NormalizeLineEndings(control.Text).Should().StartWith("name: ");
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
        NormalizeLineEndings(control.Text).Should().Contain("  - send: ");
    }

    [WinFormsFact]
    public void CompletionCommit_StepCommandWithoutDash_PrependsDashAndAppendsColonAndSpace()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.Text = "steps:\n  sen";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        InvokeNonPublic(control, "ShowCompletionPopup");
        var handled = (bool)InvokeNonPublic(control, "HandleCompletionNavigation", new KeyEventArgs(Keys.Enter))!;

        handled.Should().BeTrue();
        NormalizeLineEndings(control.Text).Should().Contain("  - send: ");
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
    public void ApplyTheme_DarkMode_UsesDarkLineNumberPalette()
    {
        using var control = new ScintillaScriptEditorControl();
        control.ApplyTheme(true);

        var editor = GetInnerEditor(control);
        var lineNumberStyle = editor.Styles[Style.LineNumber];

        lineNumberStyle.ForeColor.Should().Be(Color.FromArgb(160, 160, 160));
        lineNumberStyle.BackColor.Should().Be(Color.FromArgb(30, 30, 30));
        editor.Margins[1].Type.Should().Be(MarginType.Color);
        editor.Margins[1].BackColor.Should().Be(Color.FromArgb(30, 30, 30));
    }

    [WinFormsFact]
    public void ApplyTheme_DarkMode_WithSyntaxHighlighting_KeepsLineNumberPalette()
    {
        using var control = new ScintillaScriptEditorControl();
        control.SetSyntaxHighlighter(new YamlSshSyntaxHighlighter());
        control.Text = "name: qa\nsteps:\n  - send:\n      command: show version";
        control.ApplyTheme(true);

        var editor = GetInnerEditor(control);
        var lineNumberStyle = editor.Styles[Style.LineNumber];

        lineNumberStyle.ForeColor.Should().Be(Color.FromArgb(160, 160, 160));
        lineNumberStyle.BackColor.Should().Be(Color.FromArgb(30, 30, 30));
        editor.Margins[1].BackColor.Should().Be(Color.FromArgb(30, 30, 30));
    }

    [WinFormsFact]
    public void ApplyTheme_LightMode_UsesGreyLineNumberText()
    {
        using var control = new ScintillaScriptEditorControl();
        control.ApplyTheme(false);

        var editor = GetInnerEditor(control);
        var lineNumberStyle = editor.Styles[Style.LineNumber];

        lineNumberStyle.ForeColor.Should().Be(Color.FromArgb(115, 115, 115));
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
    public void SmartEnter_OnScalarStepOptionKey_DoesNotOverIndentNextLine()
    {
        using var control = new ScintillaScriptEditorControl();
        control.Text = "steps:\n  - extract:\n      from:";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        var handled = (bool)InvokeNonPublic(control, "HandleSmartEnter", new KeyEventArgs(Keys.Enter))!;

        handled.Should().BeTrue();
        NormalizeLineEndings(control.Text).Should().EndWith("\n      ");
    }

    [WinFormsFact]
    public void SmartEnter_OnEmptyIndentedRootCommandPayloadLine_DedentsToCommandIndent()
    {
        using var control = new ScintillaScriptEditorControl();
        control.Text = "- dns:\n    host: 1.2.3.4\n    into:\n    ";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        var handled = (bool)InvokeNonPublic(control, "HandleSmartEnter", new KeyEventArgs(Keys.Enter))!;

        handled.Should().BeTrue();
        NormalizeLineEndings(control.Text).Should().EndWith("\n    \n");
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

    private static class NativeMethods
    {
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int MK_LBUTTON = 0x0001;
        public const int WS_CHILD = 0x40000000;
        public const int WS_VISIBLE = 0x10000000;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            int exStyle,
            string className,
            string windowName,
            int style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parentHandle,
            IntPtr menuHandle,
            IntPtr instanceHandle,
            IntPtr param);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(IntPtr handle);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static IntPtr MakeLParam(int x, int y)
        {
            var packed = (y << 16) | (x & 0xFFFF);
            return (IntPtr)packed;
        }
    }
}
