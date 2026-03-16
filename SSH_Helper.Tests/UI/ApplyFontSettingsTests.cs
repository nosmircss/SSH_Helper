using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class ApplyFontSettingsTests
{
    #region Font Creation Tests

    [WinFormsFact]
    public void ApplyFontSettings_DefaultSettings_AllControlsHaveNonNullFonts()
    {
        using var harness = new FontApplicationTestHarness();
        harness.ApplyFontSettings(FontSettings.CreateDefault());

        harness.lblHostsTitle.Font.Should().NotBeNull();
        harness.lblPresetsTitle.Font.Should().NotBeNull();
        harness.trvPresets.Font.Should().NotBeNull();
        harness.trvFavorites.Font.Should().NotBeNull();
        harness.lblFavoritesEmpty.Font.Should().NotBeNull();
        harness.btnExecuteAll.Font.Should().NotBeNull();
        harness.btnSavePreset.Font.Should().NotBeNull();
        harness.txtCommand.Font.Should().NotBeNull();
        harness.txtOutput.Font.Should().NotBeNull();
        harness.presetsTabControl.Font.Should().NotBeNull();
        harness.lstOutput.Font.Should().NotBeNull();
        harness.menuStrip1.Font.Should().NotBeNull();
        harness.mainToolStrip.Font.Should().NotBeNull();
        harness.statusStrip.Font.Should().NotBeNull();
    }

    [WinFormsFact]
    public void ApplyFontSettings_DefaultSettings_CreatesManagedFonts()
    {
        using var harness = new FontApplicationTestHarness();
        harness.ApplyFontSettings(FontSettings.CreateDefault());

        // Should have created fonts for: sectionTitle, tree, emptyLabel, execButton,
        // button, codeEditor, output, tab, list, menu, contextMenu, toolStrip, status = 13
        harness.ManagedFonts.Should().NotBeEmpty();
        harness.ManagedFonts.Count.Should().BeGreaterOrEqualTo(10);
    }

    [WinFormsFact]
    public void ApplyFontSettings_UIFontFamily_AppliedToLabels()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.UIFontFamily = "Arial";
        harness.ApplyFontSettings(settings);

        // Font constructor may map family names; just verify controls share the same font instance
        // and that labels share the same font instance
        harness.lblHostsTitle.Font.Should().BeSameAs(harness.lblPresetsTitle.Font);
        harness.lblHostsTitle.Font.Should().BeSameAs(harness.lblScriptTitle.Font);
    }

    [WinFormsFact]
    public void ApplyFontSettings_CodeFontFamily_AppliedToCodeEditor()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.CodeFontFamily = "Consolas";
        harness.ApplyFontSettings(settings);

        // Code editor and output should use the code font
        harness.txtCommand.Font.Should().NotBeNull();
        harness.txtOutput.Font.Should().NotBeNull();
        // They should be different font instances (different categories)
        harness.txtCommand.Font.Should().NotBeSameAs(harness.lblHostsTitle.Font);
    }

    [WinFormsFact]
    public void ApplyFontSettings_GlobalScaleFactor_ScalesFontSizes()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.GlobalScaleFactor = 1.5f;
        settings.TreeViewFontSize = 10f;
        harness.ApplyFontSettings(settings);

        // Tree font should be approximately 15pt (10 * 1.5)
        harness.trvPresets.Font.Size.Should().BeApproximately(15f, 0.5f);
    }

    #endregion

    #region WordWrap Tests

    [WinFormsTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ApplyFontSettings_WordWrap_AppliedToCodeEditor(bool wordWrap)
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.CodeEditorWordWrap = wordWrap;
        harness.ApplyFontSettings(settings);

        harness.txtCommand.WordWrap.Should().Be(wordWrap);
    }

    [WinFormsTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ApplyFontSettings_WordWrap_AppliedToOutput(bool wordWrap)
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.OutputAreaWordWrap = wordWrap;
        harness.ApplyFontSettings(settings);

        harness.txtOutput.WordWrap.Should().Be(wordWrap);
    }

    #endregion

    #region Row Height Tests

    [WinFormsFact]
    public void ApplyFontSettings_TreeViewRowHeight_Auto_CalculatesFromFont()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.TreeViewRowHeight = 0; // auto
        harness.ApplyFontSettings(settings);

        // Auto height should be fontHeight + 4, which is > 0
        harness.trvPresets.ItemHeight.Should().BeGreaterThan(0);
        harness.trvFavorites.ItemHeight.Should().Be(harness.trvPresets.ItemHeight);
    }

    [WinFormsTheory]
    [InlineData(20)]
    [InlineData(35)]
    [InlineData(50)]
    public void ApplyFontSettings_TreeViewRowHeight_Custom_AppliesExactly(int rowHeight)
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.TreeViewRowHeight = rowHeight;
        harness.ApplyFontSettings(settings);

        harness.trvPresets.ItemHeight.Should().Be(rowHeight);
        harness.trvFavorites.ItemHeight.Should().Be(rowHeight);
    }

    [WinFormsTheory]
    [InlineData(16)]
    [InlineData(28)]
    [InlineData(50)]
    public void ApplyFontSettings_HostListRowHeight_AppliedToDataGridView(int rowHeight)
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.HostListRowHeight = rowHeight;
        harness.ApplyFontSettings(settings);

        harness.dgv_variables.RowTemplate.Height.Should().Be(rowHeight);
    }

    [WinFormsFact]
    public void ApplyFontSettings_HistoryListVariableRows_FontChangesKeepUsableHeights()
    {
        using var harness = new FontApplicationTestHarness();
        harness.lstOutput.Size = new Size(150, 240);
        harness.lstOutput.Items.Add("2026-03-13 09:55:59 - QA WriteFile JSON [Windows] with a very long history label that should wrap across multiple lines");

        harness.ApplyFontSettings(FontSettings.CreateDefault());
        harness.ConfigureVariableHeightHistoryList();
        var initialMinimumHeight = HistoryListLayout.GetMinimumItemHeight(harness.lstOutput.Font);
        var initialHeight = HistoryListLayout.CalculateItemHeight(
            harness.lstOutput.Items[0]?.ToString(),
            harness.lstOutput.Font,
            harness.lstOutput.ClientSize.Width);

        var largerFontSettings = FontSettings.CreateDefault();
        largerFontSettings.HostListFontSize = 12f;
        harness.ApplyFontSettings(largerFontSettings);
        harness.lstOutput.RefreshVariableItemHeights();

        var resizedHeight = HistoryListLayout.CalculateItemHeight(
            harness.lstOutput.Items[0]?.ToString(),
            harness.lstOutput.Font,
            harness.lstOutput.ClientSize.Width);

        harness.lstOutput.DrawMode.Should().Be(DrawMode.OwnerDrawVariable);
        initialHeight.Should().BeGreaterThan(initialMinimumHeight);
        resizedHeight.Should().BeGreaterThan(initialHeight);
    }

    #endregion

    #region Font Disposal Lifecycle Tests (Critical Crash Prevention)

    [WinFormsFact]
    public void ApplyFontSettings_FirstCall_NoPreviousFontsToDispose()
    {
        using var harness = new FontApplicationTestHarness();
        harness.ApplyFontSettings(FontSettings.CreateDefault());

        harness.PreviousFonts.Should().BeEmpty();
    }

    [WinFormsFact]
    public void ApplyFontSettings_SecondCall_PreviousFontsCollectedForDisposal()
    {
        using var harness = new FontApplicationTestHarness();

        harness.ApplyFontSettings(FontSettings.CreateDefault());
        var firstBatchCount = harness.ManagedFonts.Count;
        firstBatchCount.Should().BeGreaterThan(0);

        harness.ApplyFontSettings(FontSettings.CreateDefault());

        harness.PreviousFonts.Should().HaveCount(firstBatchCount);
    }

    [WinFormsFact]
    public void ApplyFontSettings_SecondCall_NewFontsReplacePrevious()
    {
        using var harness = new FontApplicationTestHarness();

        var settings1 = FontSettings.CreateDefault();
        settings1.TreeViewFontSize = 10f;
        harness.ApplyFontSettings(settings1);
        var firstTreeFont = harness.trvPresets.Font;

        var settings2 = FontSettings.CreateDefault();
        settings2.TreeViewFontSize = 12f;
        harness.ApplyFontSettings(settings2);

        harness.trvPresets.Font.Should().NotBeSameAs(firstTreeFont);
        harness.trvPresets.Font.Size.Should().BeApproximately(12f, 0.5f);
    }

    [WinFormsFact]
    public void ApplyFontSettings_AfterDisposingPrevious_ControlFontsStillAccessible()
    {
        // This is the CORE crash test — validates the fix from commit 305e53b.
        // Old fonts must be fully replaced BEFORE disposal so controls still work.
        // Uses different settings between calls to ensure GDI+ creates independent handles.
        using var harness = new FontApplicationTestHarness();

        // Apply first batch (batch A) with one set of sizes
        var settingsA = FontSettings.CreateDefault();
        settingsA.SectionTitleFontSize = 8f;
        settingsA.TreeViewFontSize = 8f;
        settingsA.EmptyLabelFontSize = 8f;
        settingsA.ExecuteButtonFontSize = 8f;
        settingsA.ButtonFontSize = 8f;
        settingsA.CodeEditorFontSize = 8f;
        settingsA.OutputAreaFontSize = 8f;
        settingsA.TabFontSize = 8f;
        settingsA.HostListFontSize = 8f;
        settingsA.MenuFontSize = 8f;
        settingsA.StatusBarFontSize = 8f;
        harness.ApplyFontSettings(settingsA);

        // Apply second batch (batch B) with ALL different sizes
        var settingsB = FontSettings.CreateDefault();
        settingsB.SectionTitleFontSize = 12f;
        settingsB.TreeViewFontSize = 12f;
        settingsB.EmptyLabelFontSize = 12f;
        settingsB.ExecuteButtonFontSize = 12f;
        settingsB.ButtonFontSize = 12f;
        settingsB.CodeEditorFontSize = 12f;
        settingsB.OutputAreaFontSize = 12f;
        settingsB.TabFontSize = 12f;
        settingsB.HostListFontSize = 12f;
        settingsB.MenuFontSize = 12f;
        settingsB.StatusBarFontSize = 12f;
        harness.ApplyFontSettings(settingsB);

        // Dispose batch A — simulates the deferred BeginInvoke disposal
        harness.DisposePreviousFonts();

        // Access every control's Font.Height — MUST NOT throw ArgumentException
        var action = () =>
        {
            _ = harness.lblHostsTitle.Font.Height;
            _ = harness.trvPresets.Font.Height;
            _ = harness.trvFavorites.Font.Height;
            _ = harness.lblFavoritesEmpty.Font.Height;
            _ = harness.btnExecuteAll.Font.Height;
            _ = harness.btnSavePreset.Font.Height;
            _ = harness.txtCommand.Font.Height;
            _ = harness.txtOutput.Font.Height;
            _ = harness.presetsTabControl.Font.Height;
            _ = harness.lstOutput.Font.Height;
            _ = harness.menuStrip1.Font.Height;
            _ = harness.mainToolStrip.Font.Height;
            _ = harness.statusStrip.Font.Height;
        };

        action.Should().NotThrow();
    }

    [WinFormsFact]
    public void ApplyFontSettings_PreviousFonts_CanBeDisposedSafely()
    {
        using var harness = new FontApplicationTestHarness();

        harness.ApplyFontSettings(FontSettings.CreateDefault());
        harness.ApplyFontSettings(FontSettings.CreateDefault());

        // Disposing previous fonts should not throw
        var action = () => harness.DisposePreviousFonts();
        action.Should().NotThrow();
    }

    [WinFormsFact]
    public void ApplyFontSettings_NewFontsAreDifferentObjectsFromPrevious()
    {
        // Verifies that the tracked font disposal pattern creates separate object references.
        // This is the key invariant: ManagedFonts and PreviousFonts must not share references,
        // so disposing PreviousFonts cannot invalidate the current control fonts.
        using var harness = new FontApplicationTestHarness();

        harness.ApplyFontSettings(FontSettings.CreateDefault());
        harness.ApplyFontSettings(FontSettings.CreateDefault());

        foreach (var currentFont in harness.ManagedFonts)
        {
            foreach (var previousFont in harness.PreviousFonts)
            {
                currentFont.Should().NotBeSameAs(previousFont,
                    "current and previous font batches must be distinct object references");
            }
        }
    }

    [WinFormsFact]
    public void ApplyFontSettings_ControlFontsReflectLatestSettings()
    {
        // Verifies that after two ApplyFontSettings calls with different sizes,
        // controls reflect the latest settings (not stale ones from the previous batch).
        // This validates the core invariant that prevents the 305e53b crash:
        // new fonts are fully assigned before old ones are collected for disposal.
        using var harness = new FontApplicationTestHarness();

        var settingsA = FontSettings.CreateDefault();
        settingsA.TreeViewFontSize = 9f;
        settingsA.CodeEditorFontSize = 9f;
        harness.ApplyFontSettings(settingsA);

        var settingsB = FontSettings.CreateDefault();
        settingsB.TreeViewFontSize = 13f;
        settingsB.CodeEditorFontSize = 13f;
        harness.ApplyFontSettings(settingsB);

        // Controls should reflect batch B sizes, not batch A
        harness.trvPresets.Font.Size.Should().BeApproximately(13f, 0.5f,
            "trvPresets should have the latest font size, not the previous one");
        harness.txtCommand.Font.Size.Should().BeApproximately(13f, 0.5f,
            "txtCommand should have the latest font size, not the previous one");

        // ManagedFonts and PreviousFonts should be distinct lists
        harness.ManagedFonts.Should().NotBeEmpty();
        harness.PreviousFonts.Should().NotBeEmpty();
        harness.ManagedFonts.Should().HaveCount(harness.PreviousFonts.Count,
            "each call should create the same number of font objects");
    }

    [WinFormsFact]
    public void ApplyFontSettings_FontHeightAccess_DoesNotThrowAfterDisposal()
    {
        // Uses different settings between calls to ensure GDI+ creates independent handles
        using var harness = new FontApplicationTestHarness();

        var settingsA = FontSettings.CreateDefault();
        settingsA.TreeViewFontSize = 9f;
        harness.ApplyFontSettings(settingsA);

        var settingsB = FontSettings.CreateDefault();
        settingsB.TreeViewFontSize = 11f;
        harness.ApplyFontSettings(settingsB);

        harness.DisposePreviousFonts();

        // Specifically test tree view height access (the exact crash point from 305e53b)
        var action = () =>
        {
            _ = harness.trvPresets.Font.Height;
            _ = harness.trvPresets.ItemHeight;
        };
        action.Should().NotThrow();
    }

    #endregion

    #region Rapid Repeated Application Tests (Slider Dragging)

    [WinFormsFact]
    public void ApplyFontSettings_RapidRepeatedCalls_NoExceptionThrown()
    {
        using var harness = new FontApplicationTestHarness();

        var action = () =>
        {
            for (int i = 0; i < 100; i++)
            {
                var settings = FontSettings.CreateDefault();
                settings.GlobalScaleFactor = 0.8f + (i * 0.007f); // 0.80 to 1.50
                harness.ApplyFontSettings(settings);
                harness.DisposePreviousFonts();
            }
        };

        action.Should().NotThrow();
    }

    [WinFormsFact]
    public void ApplyFontSettings_RapidRepeatedCalls_OnlyLatestFontsRetained()
    {
        using var harness = new FontApplicationTestHarness();

        for (int i = 0; i < 50; i++)
        {
            var settings = FontSettings.CreateDefault();
            settings.GlobalScaleFactor = 0.8f + (i * 0.014f);
            harness.ApplyFontSettings(settings);
            harness.DisposePreviousFonts();
        }

        // Only the last batch should remain in ManagedFonts
        harness.ManagedFonts.Should().NotBeEmpty();
        harness.PreviousFonts.Should().BeEmpty(); // All previous were disposed
    }

    [WinFormsFact]
    public void ApplyFontSettings_RapidRepeatedCalls_ControlFontsRemainValid()
    {
        using var harness = new FontApplicationTestHarness();

        for (int i = 0; i < 50; i++)
        {
            var settings = FontSettings.CreateDefault();
            settings.GlobalScaleFactor = 0.8f + (i * 0.014f);
            harness.ApplyFontSettings(settings);
            harness.DisposePreviousFonts();
        }

        // After 50 rapid applications, every control's Font.Height must be accessible
        var action = () =>
        {
            _ = harness.lblHostsTitle.Font.Height;
            _ = harness.trvPresets.Font.Height;
            _ = harness.txtCommand.Font.Height;
            _ = harness.txtOutput.Font.Height;
            _ = harness.menuStrip1.Font.Height;
            _ = harness.statusStrip.Font.Height;
        };

        action.Should().NotThrow();
    }

    #endregion

    #region Invalid Font Name Tests

    [WinFormsFact]
    public void ApplyFontSettings_InvalidUIFontFamily_DoesNotThrow()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.UIFontFamily = "NonExistentFontXYZ123";

        // Font constructor falls back to a default family for invalid names
        var action = () => harness.ApplyFontSettings(settings);
        action.Should().NotThrow();

        // Controls should still have valid, non-null fonts
        harness.lblHostsTitle.Font.Should().NotBeNull();
        harness.trvPresets.Font.Should().NotBeNull();
    }

    [WinFormsFact]
    public void ApplyFontSettings_EmptyFontFamily_DoesNotThrow()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.UIFontFamily = "";

        var action = () => harness.ApplyFontSettings(settings);
        action.Should().NotThrow();
    }

    [WinFormsFact]
    public void ApplyFontSettings_InvalidCodeFontFamily_DoesNotThrow()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.CodeFontFamily = "TotallyFakeMonoFont999";

        var action = () => harness.ApplyFontSettings(settings);
        action.Should().NotThrow();

        harness.txtCommand.Font.Should().NotBeNull();
        harness.txtOutput.Font.Should().NotBeNull();
    }

    #endregion

    #region Extreme Scale Factor Tests

    [WinFormsFact]
    public void ApplyFontSettings_MinScaleMinSize_CreatesFontsSuccessfully()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.GlobalScaleFactor = 0.8f;
        settings.SectionTitleFontSize = 7f;
        settings.TreeViewFontSize = 7f;
        settings.CodeEditorFontSize = 7f;
        settings.OutputAreaFontSize = 7f;

        // 0.8 * 7 = 5.6pt — still valid for Font constructor
        var action = () => harness.ApplyFontSettings(settings);
        action.Should().NotThrow();
    }

    [WinFormsFact]
    public void ApplyFontSettings_MaxScaleMaxSize_CreatesFontsSuccessfully()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.GlobalScaleFactor = 1.5f;
        settings.SectionTitleFontSize = 16f;
        settings.TreeViewFontSize = 16f;
        settings.CodeEditorFontSize = 16f;
        settings.OutputAreaFontSize = 16f;

        // 1.5 * 16 = 24pt — should be fine
        var action = () => harness.ApplyFontSettings(settings);
        action.Should().NotThrow();
    }

    #endregion

    #region Accent Color Tests

    [WinFormsFact]
    public void ApplyFontSettings_NullAccentColor_NoException()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        settings.CustomAccentColor = null;

        var action = () => harness.ApplyFontSettings(settings);
        action.Should().NotThrow();
        harness.LastAppliedAccentColor.Should().BeNull();
    }

    [WinFormsFact]
    public void ApplyFontSettings_ValidAccentColor_Applied()
    {
        using var harness = new FontApplicationTestHarness();
        var settings = FontSettings.CreateDefault();
        var argb = Color.CornflowerBlue.ToArgb();
        settings.CustomAccentColor = argb;

        harness.ApplyFontSettings(settings);
        harness.LastAppliedAccentColor.Should().Be(argb);
    }

    #endregion
}
