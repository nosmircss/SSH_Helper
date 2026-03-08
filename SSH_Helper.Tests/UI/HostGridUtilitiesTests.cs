using System.Data;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Services;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class HostGridUtilitiesTests
{
    [Fact]
    public void BuildSnapshot_FromDataTable_PreservesColumnOrderAndValues()
    {
        var table = new DataTable();
        table.Columns.Add(CsvManager.HostColumnName);
        table.Columns.Add("username");
        table.Rows.Add("10.0.0.1", "admin");

        var snapshot = HostGridUtilities.BuildSnapshot(table);

        snapshot.Columns.Should().Equal(CsvManager.HostColumnName, "username");
        snapshot.Rows.Should().HaveCount(1);
        snapshot.Rows[0][CsvManager.HostColumnName].Should().Be("10.0.0.1");
        snapshot.Rows[0]["username"].Should().Be("admin");
    }

    [WinFormsFact]
    public void BuildSchedulerCopySnapshot_WhenCheckedRowsExist_UsesOnlyCheckedEligibleRows()
    {
        using var grid = new DataGridView { AllowUserToAddRows = true };
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = string.Empty, HeaderText = string.Empty });
        grid.Columns.Add(HostGridUtilities.CreateTextColumn(CsvManager.HostColumnName));
        grid.Columns.Add(HostGridUtilities.CreateTextColumn("role"));

        var checkedRowIndex = grid.Rows.Add();
        grid.Rows[checkedRowIndex].Cells[0].Value = true;
        grid.Rows[checkedRowIndex].Cells[CsvManager.HostColumnName].Value = "10.0.0.1";
        grid.Rows[checkedRowIndex].Cells["role"].Value = "edge";

        var uncheckedRowIndex = grid.Rows.Add();
        grid.Rows[uncheckedRowIndex].Cells[0].Value = false;
        grid.Rows[uncheckedRowIndex].Cells[CsvManager.HostColumnName].Value = "10.0.0.2";
        grid.Rows[uncheckedRowIndex].Cells["role"].Value = "core";

        var blankCheckedRowIndex = grid.Rows.Add();
        grid.Rows[blankCheckedRowIndex].Cells[0].Value = true;
        grid.Rows[blankCheckedRowIndex].Cells[CsvManager.HostColumnName].Value = string.Empty;
        grid.Rows[blankCheckedRowIndex].Cells["role"].Value = "ignored";

        var snapshot = HostGridUtilities.BuildSchedulerCopySnapshot(grid);

        snapshot.Columns.Should().Equal(CsvManager.HostColumnName, "role");
        snapshot.Rows.Should().HaveCount(1);
        snapshot.Rows[0][CsvManager.HostColumnName].Should().Be("10.0.0.1");
        snapshot.Rows[0]["role"].Should().Be("edge");
    }

    [WinFormsFact]
    public void PasteClipboardText_WhenMatrixExceedsGrid_AddsRowsAndColumns()
    {
        using var grid = new DataGridView { AllowUserToAddRows = true };
        grid.Columns.Add(HostGridUtilities.CreateTextColumn(CsvManager.HostColumnName));
        grid.Rows.Add();
        grid.CurrentCell = grid.Rows[0].Cells[0];

        HostGridUtilities.PasteClipboardText(
            grid,
            "10.0.0.1\tadmin\r\n10.0.0.2\troot");

        grid.Columns.Cast<DataGridViewColumn>()
            .Select(column => column.Name)
            .Should()
            .ContainInOrder(CsvManager.HostColumnName, "Column2");
        grid.Rows.Cast<DataGridViewRow>()
            .Count(row => !row.IsNewRow)
            .Should()
            .Be(2);
        grid.Rows[0].Cells[CsvManager.HostColumnName].Value.Should().Be("10.0.0.1");
        grid.Rows[0].Cells["Column2"].Value.Should().Be("admin");
        grid.Rows[1].Cells[CsvManager.HostColumnName].Value.Should().Be("10.0.0.2");
        grid.Rows[1].Cells["Column2"].Value.Should().Be("root");
    }
}
