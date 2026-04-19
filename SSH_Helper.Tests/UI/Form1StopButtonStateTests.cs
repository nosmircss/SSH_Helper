using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1StopButtonStateTests
{
    [WinFormsFact]
    public void StopExecution_WhenCancellationRequestedInDarkMode_DisablesStopButtonWithoutChangingItsRenderStyle()
    {
        using var form = CreateForm();
        InvokePrivateMethod(form, "ApplyTheme", true);

        var stopButton = GetField<Button>(form, "btnStopAll");
        var sshService = GetField<SshExecutionService>(form, "_sshService");
        var initialWidth = stopButton.Width;

        stopButton.Visible = true;
        stopButton.ForeColor.Should().Be(Color.White, "the active Stop button starts with white text");
        stopButton.FlatStyle.Should().Be(FlatStyle.Flat);

        SetPrivateField(sshService, "_isRunning", true);

        InvokePrivateMethod(form, "StopExecution");

        stopButton.Text.Should().Be("Canceling...");
        stopButton.Enabled.Should().BeFalse(
            "the cancelling state should still block further clicks through the real Enabled state");
        stopButton.ForeColor.Should().Be(Color.White);
        stopButton.TextAlign.Should().Be(ContentAlignment.MiddleCenter);
        stopButton.Width.Should().BeGreaterThan(initialWidth,
            "the cancelling label should widen the stop button so the full caption fits");
        stopButton.Width.Should().BeGreaterThanOrEqualTo(
            TextRenderer.MeasureText(stopButton.Text, stopButton.Font).Width + 20,
            "the cancelling label should have enough horizontal space to render without clipping");
        GetPrivateField<bool>(form, "_manualCancellationRequested").Should().BeTrue();

        using var disabledBitmap = RenderControl(stopButton);
        stopButton.Enabled = true;
        using var enabledBitmap = RenderControl(stopButton);

        BitmapsEqual(disabledBitmap, enabledBitmap).Should().BeTrue(
            "the disabled cancelling state should render with the same centered white caption treatment as the enabled stop button");
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

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        return (T)field!.GetValue(instance)!;
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");
        field!.SetValue(instance, value);
    }

    private static object? InvokePrivateMethod(object instance, string methodName, params object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on {instance.GetType().Name}");
        return method!.Invoke(instance, args);
    }

    private static Bitmap RenderControl(Control control)
    {
        using var host = new Form
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            Size = new Size(control.Width + 20, control.Height + 20)
        };

        control.Location = new Point(8, 8);
        host.Controls.Add(control);
        host.CreateControl();
        control.CreateControl();
        host.Show();
        Application.DoEvents();

        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));

        host.Controls.Remove(control);
        host.Close();
        return bitmap;
    }

    private static bool BitmapsEqual(Bitmap left, Bitmap right)
    {
        if (left.Size != right.Size)
        {
            return false;
        }

        for (int y = 0; y < left.Height; y++)
        {
            for (int x = 0; x < left.Width; x++)
            {
                if (left.GetPixel(x, y) != right.GetPixel(x, y))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
