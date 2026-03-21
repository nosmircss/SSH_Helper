using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1ConnectionTestStatusTests
{
    [WinFormsFact]
    public async Task TestSelectedConnections_WhenQueuedProgressCallbacksDrainAfterCompletion_KeepsCompletionStatus()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = AcceptSingleClientAsync(listener);

        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var hostGrid = GetField<DataGridView>(form, "dgv_variables");
        var statusLabel = GetField<ToolStripStatusLabel>(form, "statusLabel");
        var statusProgress = GetField<ToolStripProgressBar>(form, "statusProgress");

        var rowIndex = hostGrid.Rows.Add();
        var row = hostGrid.Rows[rowIndex];
        row.Cells[0].Value = true;
        row.Cells["Host_IP"].Value = $"127.0.0.1:{port}";

        var testTask = (Task)InvokePrivateMethod(form, "TestSelectedConnections")!;
        await testTask.WaitAsync(TimeSpan.FromSeconds(5));

        await PumpUiAsync();
        await acceptTask.WaitAsync(TimeSpan.FromSeconds(2));

        statusLabel.Text.Should().Be("Connection test complete (1 hosts)",
            "queued per-host progress callbacks should not overwrite the final completion status after the test run finishes");
        statusProgress.Visible.Should().BeFalse();
    }

    private static async Task AcceptSingleClientAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
    }

    private static async Task PumpUiAsync()
    {
        for (int i = 0; i < 4; i++)
        {
            Application.DoEvents();
            await Task.Delay(10);
        }
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }

    private static object? InvokePrivateMethod(object instance, string methodName, params object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on {instance.GetType().Name}");
        return method!.Invoke(instance, args);
    }
}
