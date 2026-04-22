using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Drawing;
using System.Linq;
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

    [WinFormsFact]
    public async Task TestSelectedConnections_WhenSelectedRowSucceeds_ColorsRowHeader()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = AcceptSingleClientAsync(listener);

        using var form = CreateForm();
        var hostGrid = GetField<DataGridView>(form, "dgv_variables");
        var row = AddCheckedHostRow(hostGrid, $"127.0.0.1:{port}");

        var testTask = (Task)InvokePrivateMethod(form, "TestSelectedConnections")!;
        await testTask.WaitAsync(TimeSpan.FromSeconds(5));

        await PumpUiAsync();
        await acceptTask.WaitAsync(TimeSpan.FromSeconds(2));

        row.Cells["Host_IP"].Selected.Should().BeTrue("the row should remain selected during the test scenario");
        row.HeaderCell.Style.BackColor.Should().NotBe(Color.Empty,
            "successful selected rows should surface connection status in the row header even when the Host_IP cell uses selection colors");
    }

    [WinFormsFact]
    public async Task TestSelectedConnections_WhenSelectedRowFails_ColorsRowHeader()
    {
        var unusedPort = ReserveUnusedLoopbackPort();

        using var form = CreateForm();
        var hostGrid = GetField<DataGridView>(form, "dgv_variables");
        var row = AddCheckedHostRow(hostGrid, $"127.0.0.1:{unusedPort}");

        var testTask = (Task)InvokePrivateMethod(form, "TestSelectedConnections")!;
        await testTask.WaitAsync(TimeSpan.FromSeconds(5));

        await PumpUiAsync();

        row.Cells["Host_IP"].Selected.Should().BeTrue("the row should remain selected during the test scenario");
        row.HeaderCell.Style.BackColor.Should().NotBe(Color.Empty,
            "failed selected rows should surface connection status in the row header even when the Host_IP cell uses selection colors");
    }

    [WinFormsFact]
    public void ClearConnectionTestIndicators_ClearsRowHeaderStatus()
    {
        using var form = CreateForm();
        var hostGrid = GetField<DataGridView>(form, "dgv_variables");
        var row = AddCheckedHostRow(hostGrid, "10.0.0.5");
        var hostIpColIndex = hostGrid.Columns["Host_IP"].Index;

        InvokePrivateMethod(
            form,
            "ApplyConnectionTestCellResult",
            row,
            hostIpColIndex,
            new SSH_Helper.Models.ConnectionTestResult(true, null, null, 25));

        row.HeaderCell.Style.BackColor.Should().NotBe(Color.Empty,
            "the row header should show connection-test state before clearing");

        InvokePrivateMethod(form, "ClearConnectionTestIndicators");

        row.HeaderCell.Style.BackColor.Should().Be(Color.Empty);
        row.HeaderCell.Style.ForeColor.Should().Be(Color.Empty);
        row.Cells["Host_IP"].Style.BackColor.Should().Be(Color.Empty);
        row.Cells["Host_IP"].Style.ForeColor.Should().Be(Color.Empty);
        row.Cells["Host_IP"].ToolTipText.Should().BeEmpty();
    }

    [WinFormsFact]
    public void HostIpEdit_ClearsRowHeaderStatus()
    {
        using var form = CreateForm();
        var hostGrid = GetField<DataGridView>(form, "dgv_variables");
        var row = AddCheckedHostRow(hostGrid, "10.0.0.5");
        var hostIpColIndex = hostGrid.Columns["Host_IP"].Index;

        InvokePrivateMethod(
            form,
            "ApplyConnectionTestCellResult",
            row,
            hostIpColIndex,
            new SSH_Helper.Models.ConnectionTestResult(false, "Network", "Connection refused", 0));

        row.HeaderCell.Style.BackColor.Should().NotBe(Color.Empty,
            "the row header should show connection-test state before the host value changes");

        row.Cells["Host_IP"].Value = "10.0.0.6";
        InvokePrivateMethod(
            form,
            "Dgv_Variables_CellValueChanged",
            hostGrid,
            new DataGridViewCellEventArgs(hostIpColIndex, row.Index));

        row.HeaderCell.Style.BackColor.Should().Be(Color.Empty);
        row.HeaderCell.Style.ForeColor.Should().Be(Color.Empty);
        row.Cells["Host_IP"].Style.BackColor.Should().Be(Color.Empty);
        row.Cells["Host_IP"].Style.ForeColor.Should().Be(Color.Empty);
        row.Cells["Host_IP"].ToolTipText.Should().BeEmpty();
    }

    [WinFormsFact]
    public void ApplyTheme_ReappliesConnectionTestColorsForCurrentTheme()
    {
        using var form = CreateForm();
        var hostGrid = GetField<DataGridView>(form, "dgv_variables");
        var row = AddCheckedHostRow(hostGrid, "10.0.0.5");
        var hostIpColIndex = hostGrid.Columns["Host_IP"].Index;

        InvokePrivateMethod(form, "ApplyTheme", false);

        InvokePrivateMethod(
            form,
            "ApplyConnectionTestCellResult",
            row,
            hostIpColIndex,
            new SSH_Helper.Models.ConnectionTestResult(true, null, null, 25));

        var lightHeaderColor = row.HeaderCell.Style.BackColor;
        var lightCellColor = row.Cells["Host_IP"].Style.BackColor;

        lightHeaderColor.Should().NotBe(Color.Empty, "the row header should expose connection-test state in light mode first");
        lightCellColor.Should().NotBe(Color.Empty, "the Host_IP cell should still be tinted before the theme changes");

        InvokePrivateMethod(form, "ApplyTheme", true);

        row.HeaderCell.Style.BackColor.Should().NotBe(Color.Empty);
        row.Cells["Host_IP"].Style.BackColor.Should().NotBe(Color.Empty);
        row.HeaderCell.Style.BackColor.Should().NotBe(lightHeaderColor,
            "theme changes should regenerate row-header colors from the logical connection-test state");
        row.Cells["Host_IP"].Style.BackColor.Should().NotBe(lightCellColor,
            "theme changes should regenerate Host_IP colors from the logical connection-test state");
    }

    [WinFormsFact]
    public void GetHostConnections_WhenHostIpHasNoExplicitPort_UsesPortColumn()
    {
        using var form = CreateForm();
        var hostGrid = GetField<DataGridView>(form, "dgv_variables");
        EnsurePortColumn(hostGrid);

        var row = AddCheckedHostRow(hostGrid, "127.0.0.1");
        row.Cells["port"].Value = "2222";

        var host = ResolveSingleHost(form, row);

        host.Port.Should().Be(2222);
    }

    [WinFormsFact]
    public void GetHostConnections_WhenHostIpHasExplicitPort_OverridesPortColumn()
    {
        using var form = CreateForm();
        var hostGrid = GetField<DataGridView>(form, "dgv_variables");
        EnsurePortColumn(hostGrid);

        var row = AddCheckedHostRow(hostGrid, "127.0.0.1:2022");
        row.Cells["port"].Value = "2222";

        var host = ResolveSingleHost(form, row);

        host.Port.Should().Be(2022);
    }

    [WinFormsFact]
    public void GetHostConnections_WhenPortColumnInvalid_FallsBackToDefaultPort()
    {
        using var form = CreateForm();
        var hostGrid = GetField<DataGridView>(form, "dgv_variables");
        EnsurePortColumn(hostGrid);

        var row = AddCheckedHostRow(hostGrid, "127.0.0.1");
        row.Cells["port"].Value = "abc";

        var host = ResolveSingleHost(form, row);

        host.Port.Should().Be(22);
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

    private static SSH_Helper.Form1 CreateForm()
    {
        var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();
        return form;
    }

    private static DataGridViewRow AddCheckedHostRow(DataGridView hostGrid, string host)
    {
        var rowIndex = hostGrid.Rows.Add();
        var row = hostGrid.Rows[rowIndex];
        row.Cells[0].Value = true;
        row.Cells["Host_IP"].Value = host;
        hostGrid.ClearSelection();
        hostGrid.CurrentCell = row.Cells["Host_IP"];
        row.Cells["Host_IP"].Selected = true;
        return row;
    }

    private static int ReserveUnusedLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void EnsurePortColumn(DataGridView hostGrid)
    {
        if (hostGrid.Columns.Contains("port"))
        {
            return;
        }

        hostGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "port",
            HeaderText = "port"
        });
    }

    private static SSH_Helper.Models.HostConnection ResolveSingleHost(SSH_Helper.Form1 form, DataGridViewRow row)
    {
        var result = InvokePrivateMethod(form, "GetHostConnections", (IEnumerable<DataGridViewRow>)new[] { row });
        result.Should().NotBeNull();

        var hosts = ((IEnumerable<SSH_Helper.Models.HostConnection>)result!).ToList();
        hosts.Should().ContainSingle();
        return hosts.Single();
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
