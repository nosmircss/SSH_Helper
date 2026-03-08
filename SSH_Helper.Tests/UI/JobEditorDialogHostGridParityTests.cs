using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Services;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class JobEditorDialogHostGridParityTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;

    public JobEditorDialogHostGridParityTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"JobEditorDialogParity_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [WinFormsFact]
    public void Constructor_ConfiguresHostsGridForMainGridParity()
    {
        using var dialog = CreateDialog();
        var grid = GetField<DataGridView>(dialog, "_gridHosts");

        grid.SelectionMode.Should().Be(DataGridViewSelectionMode.CellSelect);
        grid.EditMode.Should().Be(DataGridViewEditMode.EditProgrammatically);
        grid.AllowUserToOrderColumns.Should().BeTrue();
        grid.ClipboardCopyMode.Should().Be(DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText);
        grid.ColumnHeadersHeight.Should().Be(HostGridUtilities.DefaultColumnHeaderHeight);
        grid.RowTemplate.Height.Should().Be(HostGridUtilities.DefaultRowHeight);
        grid.RowHeadersWidth.Should().Be(HostGridUtilities.DefaultRowHeaderWidth);
        grid.ContextMenuStrip.Should().NotBeNull();
    }

    [WinFormsFact]
    public void HostCount_RefreshesWhenHostIpValueChanges()
    {
        using var dialog = CreateDialog();
        var grid = GetField<DataGridView>(dialog, "_gridHosts");
        var label = GetField<Label>(dialog, "_lblHostCount");

        var rowIndex = grid.Rows.Add();
        grid.Rows[rowIndex].Cells[CsvManager.HostColumnName].Value = "10.0.0.1";
        Application.DoEvents();

        label.Text.Should().Be("1 host(s)");

        grid.Rows[rowIndex].Cells[CsvManager.HostColumnName].Value = string.Empty;
        Application.DoEvents();

        label.Text.Should().Be("0 host(s)");
    }

    [WinFormsFact]
    public void CopyFromMainGridButton_LoadsProvidedColumnsAndRows()
    {
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase)
            {
                [CsvManager.HostColumnName] = "10.0.0.1",
                ["username"] = "admin"
            },
            new(StringComparer.OrdinalIgnoreCase)
            {
                [CsvManager.HostColumnName] = "10.0.0.2",
                ["username"] = "root"
            }
        };

        using var dialog = CreateDialog(
            () => rows,
            () => new List<string> { CsvManager.HostColumnName, "username" });

        var toolStrip = GetField<ToolStrip>(dialog, "_hostsToolStrip");
        var copyButton = toolStrip.Items
            .OfType<ToolStripButton>()
            .Single(item => string.Equals(item.Text, "Copy from Main Grid", StringComparison.Ordinal));

        copyButton.PerformClick();

        var grid = GetField<DataGridView>(dialog, "_gridHosts");
        grid.Columns.Cast<DataGridViewColumn>()
            .Select(column => column.Name)
            .Should()
            .ContainInOrder(CsvManager.HostColumnName, "username");
        grid.Rows.Cast<DataGridViewRow>()
            .Count(row => !row.IsNewRow)
            .Should()
            .Be(2);
        grid.Rows[0].Cells[CsvManager.HostColumnName].Value.Should().Be("10.0.0.1");
        grid.Rows[1].Cells["username"].Value.Should().Be("root");
    }

    [WinFormsFact]
    public void ExtractHostColumnsFromGrid_UsesDisplayOrder()
    {
        using var dialog = CreateDialog();
        var grid = GetField<DataGridView>(dialog, "_gridHosts");

        grid.Columns.Add(HostGridUtilities.CreateTextColumn("username"));
        grid.Columns.Add(HostGridUtilities.CreateTextColumn("password"));
        grid.Columns["password"]!.DisplayIndex = 1;
        grid.Columns["username"]!.DisplayIndex = 2;

        var orderedColumns = InvokeMethod<List<string>>(dialog, "ExtractHostColumnsFromGrid");

        orderedColumns.Should().Equal(CsvManager.HostColumnName, "password", "username");
    }

    private JobEditorDialog CreateDialog(
        Func<IReadOnlyList<Dictionary<string, string>>>? getRows = null,
        Func<IReadOnlyList<string>>? getColumns = null)
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);

        return new JobEditorDialog(
            null,
            presetManager,
            new SchedulingService(),
            credentialProvider: null,
            getRows,
            getColumns,
            darkMode: false);
    }

    private static T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"field '{fieldName}' should exist on {obj.GetType().Name}");
        return (T)field!.GetValue(obj)!;
    }

    private static T InvokeMethod<T>(object obj, string methodName, params object[]? args)
    {
        var method = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"method '{methodName}' should exist on {obj.GetType().Name}");
        return (T)method!.Invoke(obj, args)!;
    }
}
