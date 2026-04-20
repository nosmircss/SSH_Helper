using System.Collections.Generic;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.UI;

/// <summary>
/// Verifies script prompt dialogs honor the font size parameter and scale their
/// layout proportionally. These dialogs previously ignored every font setting.
/// </summary>
public class ScriptPromptDialogFontTests
{
    [WinFormsTheory]
    [InlineData(9f)]
    [InlineData(14f)]
    [InlineData(20f)]
    public void ScriptInputDialog_AppliesFontSize(float fontSize)
    {
        using var dialog = new ScriptInputDialog(
            prompt: "Enter:",
            defaultValue: "",
            password: false,
            validationRegex: null,
            validationError: "err",
            title: null,
            fontSize: fontSize);

        dialog.Font.Size.Should().BeApproximately(fontSize, 0.01f);
    }

    [WinFormsTheory]
    [InlineData(9f, 400)]
    [InlineData(14f, 622)]
    [InlineData(20f, 889)]
    public void ScriptConfirmDialog_WidthScalesWithFontSize(float fontSize, int expectedWidth)
    {
        using var dialog = new ScriptConfirmDialog("Continue?", defaultYes: false, title: null, fontSize: fontSize);

        dialog.Font.Size.Should().BeApproximately(fontSize, 0.01f);
        dialog.Width.Should().Be(expectedWidth);
    }

    [WinFormsFact]
    public void ScriptChooseDialog_ListItemHeightAndWidthScaleWithFontSize()
    {
        var options = new List<ChoiceOption>
        {
            new() { Label = "a", Value = "a" },
            new() { Label = "b", Value = "b" },
        };

        using var small = new ScriptChooseDialog("Pick", options, defaultValue: null, title: null, fontSize: 9f);
        using var large = new ScriptChooseDialog("Pick", options, defaultValue: null, title: null, fontSize: 20f);

        var smallList = FindControl<ListBox>(small)!;
        var largeList = FindControl<ListBox>(large)!;

        largeList.ItemHeight.Should().BeGreaterThan(smallList.ItemHeight,
            "larger font should produce taller list rows so text isn't clipped");
        large.Width.Should().BeGreaterThan(small.Width);
        large.Font.Size.Should().BeApproximately(20f, 0.01f);
    }

    [WinFormsFact]
    public void ScriptMultiselectDialog_WidthScalesWithFontSize()
    {
        var options = new List<ChoiceOption>
        {
            new() { Label = "a", Value = "a" },
            new() { Label = "b", Value = "b" },
        };

        using var small = new ScriptMultiselectDialog("Pick many", options, min: null, max: null, title: null, fontSize: 9f);
        using var large = new ScriptMultiselectDialog("Pick many", options, min: null, max: null, title: null, fontSize: 18f);

        large.Font.Size.Should().BeApproximately(18f, 0.01f);
        large.Width.Should().BeGreaterThan(small.Width);
    }

    [WinFormsFact]
    public void ScriptPromptDialogRunner_DefaultPromptFontSize_RoundTrips()
    {
        var prior = ScriptPromptDialogRunner.DefaultPromptFontSize;
        try
        {
            ScriptPromptDialogRunner.DefaultPromptFontSize = 13f;
            ScriptPromptDialogRunner.DefaultPromptFontSize.Should().Be(13f);

            ScriptPromptDialogRunner.DefaultPromptFontSize = null;
            ScriptPromptDialogRunner.DefaultPromptFontSize.Should().BeNull();
        }
        finally
        {
            ScriptPromptDialogRunner.DefaultPromptFontSize = prior;
        }
    }

    private static T? FindControl<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match) return match;
            var nested = FindControl<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }
}
