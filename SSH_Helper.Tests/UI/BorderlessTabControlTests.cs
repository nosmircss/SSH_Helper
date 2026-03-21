using System.Reflection;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public class BorderlessTabControlTests
{
    [WinFormsFact]
    public void Constructor_EnablesBufferedPaintingStyles()
    {
        using var control = new SSH_Helper.BorderlessTabControl();

        GetStyle(control, ControlStyles.OptimizedDoubleBuffer).Should().BeTrue();
        GetStyle(control, ControlStyles.AllPaintingInWmPaint).Should().BeTrue();
        GetStyle(control, ControlStyles.ResizeRedraw).Should().BeTrue();
    }

    [WinFormsFact]
    public void WndProc_WhenHideBorderAndEraseBackground_SuppressesNativeErase()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var control = new BorderlessTabControlProbe
        {
            HideBorder = true,
            Size = new System.Drawing.Size(320, 200)
        };

        control.TabPages.Add(new TabPage("Presets"));
        control.DispatchEraseBackground().Should().Be((IntPtr)1);
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void WndProc_WhenHideBorderAndEraseBackground_PaintsTrailingHeaderGapDark()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var control = new BorderlessTabControlProbe
        {
            HideBorder = true,
            BorderBackgroundColor = Color.FromArgb(30, 30, 30),
            HiddenBorderHeaderColor = Color.FromArgb(37, 37, 38),
            Size = new Size(320, 200)
        };

        control.TabPages.Add(new TabPage("Presets"));
        control.TabPages.Add(new TabPage("Favorites"));

        _ = control.Handle;
        control.PerformLayout();

        using var bitmap = new Bitmap(control.Width, control.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);

        var hdc = graphics.GetHdc();
        try
        {
            control.DispatchEraseBackground(hdc).Should().Be((IntPtr)1);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }

        var lastTab = control.GetTabRect(control.TabCount - 1);
        var samplePoint = new Point(
            Math.Clamp(lastTab.Right + 4, 0, control.Width - 1),
            Math.Clamp(lastTab.Top + Math.Max(2, lastTab.Height / 2), 0, control.Height - 1));

        bitmap.GetPixel(samplePoint.X, samplePoint.Y).Should().Be(Color.FromArgb(37, 37, 38),
            "the header gap beside the last tab should already be painted with the dark header color during background erase so it does not flash before the later seam overlay runs");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void ApplyDarkTabControl_WhenBorderlessTabControl_DoesNotAttachManagedPaintOverlay()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = new SSH_Helper.Form1();
        using var control = new SSH_Helper.BorderlessTabControl();

        CountEventListEntries(control).Should().Be(0, "a fresh borderless tab control should not have custom event wiring yet");
        InvokeApplyDarkTabControl(form, control);

        CountEventListEntries(control).Should().Be(0,
            "dark borderless tabs should keep their seam cleanup on the buffered post-native overlay path instead of wiring a managed Paint handler");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void TabControlPaint_WhenBorderlessDarkTabControl_PaintsDisplayRightEdgeDark()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = new SSH_Helper.Form1();
        using var host = new Panel { Size = new Size(360, 240) };
        using var control = new SSH_Helper.BorderlessTabControl
        {
            Size = new Size(320, 200)
        };

        control.TabPages.Add(new TabPage("Presets"));
        control.TabPages.Add(new TabPage("Favorites"));
        host.Controls.Add(control);
        form.Controls.Add(host);

        _ = form.Handle;
        _ = host.Handle;
        _ = control.Handle;
        form.PerformLayout();
        control.PerformLayout();

        InvokeApplyDarkTabControl(form, control);

        using var bitmap = new Bitmap(control.Width, control.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        InvokeTabControlPaint(form, control, graphics);

        var displayRect = control.DisplayRectangle;
        displayRect.Width.Should().BeGreaterThan(0, "the hosted tab control should compute a display rectangle once its handle exists");
        displayRect.Height.Should().BeGreaterThan(0, "the hosted tab control should compute a display rectangle once its handle exists");

        var samplePoint = new Point(
            Math.Clamp(displayRect.Right - 1, 0, control.Width - 1),
            Math.Clamp(displayRect.Top + Math.Min(12, Math.Max(1, displayRect.Height / 4)), 0, control.Height - 1));

        bitmap.GetPixel(samplePoint.X, samplePoint.Y).Should().Be(Color.FromArgb(30, 30, 30),
            "borderless dark tabs should overpaint the native page edge on the display rectangle border instead of leaving a white startup seam");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void HiddenBorderOverlay_WhenRendered_PaintsTrailingHeaderGapDark()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var control = new SSH_Helper.BorderlessTabControl
        {
            HideBorder = true,
            BorderBackgroundColor = Color.FromArgb(30, 30, 30),
            Size = new Size(320, 200)
        };

        control.TabPages.Add(new TabPage("Presets"));
        control.TabPages.Add(new TabPage("Favorites"));

        _ = control.Handle;
        control.PerformLayout();

        using var bitmap = new Bitmap(control.Width, control.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        InvokeHiddenBorderOverlay(control, graphics);

        var lastTab = control.GetTabRect(control.TabCount - 1);
        var samplePoint = new Point(
            Math.Clamp(lastTab.Right + 4, 0, control.Width - 1),
            Math.Clamp(lastTab.Top + Math.Max(2, lastTab.Height / 2), 0, control.Height - 1));

        bitmap.GetPixel(samplePoint.X, samplePoint.Y).Should().Be(Color.FromArgb(37, 37, 38),
            "the hidden-border overlay should repaint the trailing header gap with the dark header surface instead of leaving a light native seam");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    private static bool GetStyle(Control control, ControlStyles style)
    {
        var method = typeof(Control).GetMethod("GetStyle", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("Control should expose GetStyle for WinForms style inspection");
        return (bool)method!.Invoke(control, new object[] { style })!;
    }

    private static int CountEventListEntries(Control control)
    {
        var eventsProperty = typeof(Component).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic);
        eventsProperty.Should().NotBeNull("Component should expose the WinForms event list");

        var eventList = (EventHandlerList?)eventsProperty!.GetValue(control);
        eventList.Should().NotBeNull("control should have an event list");

        var headField = typeof(EventHandlerList)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .FirstOrDefault(field => field.FieldType.Name.Contains("ListEntry", StringComparison.Ordinal));
        headField.Should().NotBeNull("EventHandlerList should expose its linked-list head for WinForms event inspection");

        var current = headField!.GetValue(eventList);
        var nextField = current?.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .FirstOrDefault(field => field.FieldType == current.GetType());

        var count = 0;
        while (current != null)
        {
            count++;
            current = nextField?.GetValue(current);
        }

        return count;
    }

    private static void InvokeApplyDarkTabControl(SSH_Helper.Form1 form, TabControl control)
    {
        var method = typeof(SSH_Helper.Form1).GetMethod("ApplyDarkTabControl", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("Form1 should expose ApplyDarkTabControl for dark-mode tab styling");
        method!.Invoke(form, new object[] { control });
    }

    private static void InvokeTabControlPaint(SSH_Helper.Form1 form, TabControl control, Graphics graphics)
    {
        var method = typeof(SSH_Helper.Form1).GetMethod("TabControl_Paint", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("Form1 should expose TabControl_Paint for dark-mode tab overlay painting");
        method!.Invoke(form, new object[] { control, new PaintEventArgs(graphics, new Rectangle(Point.Empty, control.Size)) });
    }

    private static void InvokeHiddenBorderOverlay(SSH_Helper.BorderlessTabControl control, Graphics graphics)
    {
        var method = typeof(SSH_Helper.BorderlessTabControl).GetMethod("PaintHiddenBorderOverlay", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("BorderlessTabControl should expose a dedicated hidden-border overlay renderer for buffered post-native painting");
        method!.Invoke(control, new object[] { graphics });
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

    private sealed class BorderlessTabControlProbe : SSH_Helper.BorderlessTabControl
    {
        private const int WmEraseBkgnd = 0x0014;

        public IntPtr DispatchEraseBackground()
        {
            if (!IsHandleCreated)
            {
                _ = Handle;
            }

            var message = Message.Create(Handle, WmEraseBkgnd, IntPtr.Zero, IntPtr.Zero);
            WndProc(ref message);
            return message.Result;
        }

        public IntPtr DispatchEraseBackground(IntPtr hdc)
        {
            if (!IsHandleCreated)
            {
                _ = Handle;
            }

            var message = Message.Create(Handle, WmEraseBkgnd, hdc, IntPtr.Zero);
            WndProc(ref message);
            return message.Result;
        }
    }
}
