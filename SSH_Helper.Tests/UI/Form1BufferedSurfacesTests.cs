using System.Reflection;
using FluentAssertions;
using Xunit;
using System.Windows.Forms;
using System.Drawing;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public class Form1BufferedSurfacesTests
{
    [Theory]
    [InlineData("hostsHeaderPanel")]
    [InlineData("presetsHeaderPanel")]
    [InlineData("scriptHeaderPanel")]
    [InlineData("scriptFooterPanel")]
    [InlineData("historyHeaderPanel")]
    [InlineData("executePanel")]
    public void Form1_HeaderPanels_UseBufferedPanelTypes(string fieldName)
    {
        var field = typeof(SSH_Helper.Form1).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on Form1");
        field!.FieldType.Should().Be(typeof(SSH_Helper.BufferedPanel),
            $"{fieldName} should use BufferedPanel to reduce activation flicker around labels and tab-adjacent surfaces");
    }

    [WinFormsFact]
    public void Form1_PresetSearchPanel_UsesBufferedPanelType()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = new SSH_Helper.Form1();

        var field = typeof(SSH_Helper.Form1).GetField("_presetSearchPanel", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull("Form1 should create the preset search panel during initialization");

        var panel = field!.GetValue(form);
        panel.Should().NotBeNull("preset search panel should be initialized once Form1 is constructed");
        panel.Should().BeOfType<SSH_Helper.BufferedPanel>("the preset search strip lives directly under the Presets/Favorites tabs and should repaint through the buffered container wrapper");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void Form1_PresetTabPages_DisableVisualStyleBackColor()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = new SSH_Helper.Form1();

        var presetsField = typeof(SSH_Helper.Form1).GetField("tabPresets", BindingFlags.Instance | BindingFlags.NonPublic);
        var favoritesField = typeof(SSH_Helper.Form1).GetField("tabFavorites", BindingFlags.Instance | BindingFlags.NonPublic);
        presetsField.Should().NotBeNull("Form1 should expose the Presets tab page");
        favoritesField.Should().NotBeNull("Form1 should expose the Favorites tab page");

        var presetsPage = (TabPage)presetsField!.GetValue(form)!;
        var favoritesPage = (TabPage)favoritesField!.GetValue(form)!;

        presetsPage.UseVisualStyleBackColor.Should().BeFalse("dark-mode tab pages should rely on explicit themed backgrounds instead of native visual-style erase");
        favoritesPage.UseVisualStyleBackColor.Should().BeFalse("dark-mode tab pages should rely on explicit themed backgrounds instead of native visual-style erase");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void Form1_UsesCustomBufferedPresetsHeaderStrip()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = new SSH_Helper.Form1();

        var field = typeof(SSH_Helper.Form1).GetField("presetsTabHeaderStrip", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull("Form1 should expose a dedicated visible header strip for Presets/Favorites instead of relying on the native tab chrome");

        var header = field!.GetValue(form);
        header.Should().NotBeNull("the custom presets header strip should be initialized during Form1 construction");
        header!.GetType().Name.Should().Be("PresetTabHeaderStrip",
            "the visible Presets/Favorites header should be fully custom-painted so native tab chrome is no longer part of the repaint path");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void Form1_PresetsTabControl_IsHostedInsideViewportAndShiftedAboveIt()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = new SSH_Helper.Form1();

        var viewportField = typeof(SSH_Helper.Form1).GetField("presetsTabViewportPanel", BindingFlags.Instance | BindingFlags.NonPublic);
        viewportField.Should().NotBeNull("Form1 should host the tab control inside a clipping viewport so the native tab header is hidden");

        var tabField = typeof(SSH_Helper.Form1).GetField("presetsTabControl", BindingFlags.Instance | BindingFlags.NonPublic);
        tabField.Should().NotBeNull("Form1 should expose the backing tab control");

        var viewport = viewportField!.GetValue(form) as Control;
        var tabControl = tabField!.GetValue(form) as TabControl;
        viewport.Should().NotBeNull("the clipping viewport should be initialized");
        tabControl.Should().NotBeNull("the backing tab control should be initialized");

        _ = form.Handle;
        form.PerformLayout();
        _ = tabControl!.Handle;

        tabControl.Parent.Should().BeSameAs(viewport, "the tab control should render inside the clipping viewport rather than directly into the presets panel");
        tabControl.Top.Should().BeLessThan(0, "the native tab header should be shifted above the clipping viewport so only the custom header strip is visible");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void Form1_CustomPresetsHeaderStrip_TracksSelectedTab()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = new SSH_Helper.Form1();

        var headerField = typeof(SSH_Helper.Form1).GetField("presetsTabHeaderStrip", BindingFlags.Instance | BindingFlags.NonPublic);
        var tabField = typeof(SSH_Helper.Form1).GetField("presetsTabControl", BindingFlags.Instance | BindingFlags.NonPublic);
        headerField.Should().NotBeNull("Form1 should expose the custom presets header strip");
        tabField.Should().NotBeNull("Form1 should expose the backing tab control");

        var header = headerField!.GetValue(form);
        var tabControl = (TabControl?)tabField!.GetValue(form);
        header.Should().NotBeNull("the custom header strip should be initialized");
        tabControl.Should().NotBeNull("the backing tab control should be initialized");

        var selectedIndexProperty = header!.GetType().GetProperty("SelectedIndex", BindingFlags.Instance | BindingFlags.Public);
        selectedIndexProperty.Should().NotBeNull("the custom header strip should expose the selected tab index for synchronization");

        _ = form.Handle;
        form.PerformLayout();
        _ = tabControl!.Handle;

        tabControl.SelectedIndex = 1;
        selectedIndexProperty!.GetValue(header).Should().Be(1,
            "switching the backing tab control should update the visible custom header strip so the active tab looks unchanged to the user");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    private static Form[] SnapshotVisibleOpenForms()
    {
        return Application.OpenForms.Cast<Form>().Where(form => form.Visible).ToArray();
    }

    private static void AssertNoNewVisibleOpenForms(IEnumerable<Form> openFormsBefore)
    {
        Application.OpenForms.Cast<Form>()
            .Where(form => form.Visible)
            .Except(openFormsBefore)
            .Should()
            .BeEmpty();
    }
}
