using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1MenuInitializationTests
{
    [WinFormsFact]
    public void Constructor_PlacesFlowCanvasAsTopLevelMenu_BeforeScheduler()
    {
        using var form = new global::SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var menuStrip = GetField<MenuStrip>(form, "menuStrip1");
        var editMenu = GetField<ToolStripMenuItem>(form, "editToolStripMenuItem");

        var topLevelItems = menuStrip.Items.OfType<ToolStripMenuItem>().ToList();
        var flowCanvasIndex = topLevelItems.FindIndex(item => item.Text == "Flow Canvas");
        var schedulerIndex = topLevelItems.FindIndex(item => item.Text == "&Scheduler");

        flowCanvasIndex.Should().BeGreaterThanOrEqualTo(0,
            "Flow Canvas should be a top-level menu item on the main menu strip");
        schedulerIndex.Should().BeGreaterThanOrEqualTo(0,
            "Scheduler should still be present as a top-level menu item");
        flowCanvasIndex.Should().BeLessThan(schedulerIndex,
            "Flow Canvas should appear before Scheduler on the top-level menu strip");

        editMenu.DropDownItems
            .OfType<ToolStripItem>()
            .Should()
            .NotContain(item => item.Text == "Flow Canvas",
                "Flow Canvas should no longer live under the Edit menu");
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }
}
