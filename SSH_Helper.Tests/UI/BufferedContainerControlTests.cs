using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public class BufferedContainerControlTests
{
    private const int WmEraseBkgnd = 0x0014;

    [WinFormsFact]
    public void BufferedPanel_Constructor_EnablesBufferedPaintingStyles()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var control = CreateControl("BufferedPanel");

        GetStyle(control, ControlStyles.OptimizedDoubleBuffer).Should().BeTrue();
        GetStyle(control, ControlStyles.AllPaintingInWmPaint).Should().BeTrue();
        GetStyle(control, ControlStyles.ResizeRedraw).Should().BeTrue();
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void BufferedSplitContainer_Constructor_EnablesBufferedPaintingStyles()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var control = CreateControl("BufferedSplitContainer");

        GetStyle(control, ControlStyles.OptimizedDoubleBuffer).Should().BeTrue();
        GetStyle(control, ControlStyles.AllPaintingInWmPaint).Should().BeTrue();
        GetStyle(control, ControlStyles.ResizeRedraw).Should().BeTrue();
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void BufferedSplitContainer_WndProc_WhenErasingBackground_SuppressesNativeErase()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var control = CreateControl("BufferedSplitContainer");
        control.Size = new System.Drawing.Size(320, 200);

        DispatchEraseBackground(control).Should().Be((IntPtr)1);
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    private static Control CreateControl(string typeName)
    {
        var assembly = typeof(SSH_Helper.Form1).Assembly;
        var type = assembly.GetType($"SSH_Helper.{typeName}", throwOnError: false);
        type.Should().NotBeNull($"SSH_Helper.{typeName} should exist for buffered container coverage");

        var control = Activator.CreateInstance(type!, nonPublic: true);
        control.Should().BeAssignableTo<Control>();
        return (Control)control!;
    }

    private static bool GetStyle(Control control, ControlStyles style)
    {
        var method = typeof(Control).GetMethod("GetStyle", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("Control should expose GetStyle for WinForms style inspection");
        return (bool)method!.Invoke(control, new object[] { style })!;
    }

    private static IntPtr DispatchEraseBackground(Control control)
    {
        var method = control.GetType().GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("Control should expose WndProc for WinForms message inspection");

        var message = Message.Create(IntPtr.Zero, WmEraseBkgnd, IntPtr.Zero, IntPtr.Zero);
        var args = new object[] { message };
        method!.Invoke(control, args);
        return ((Message)args[0]).Result;
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
