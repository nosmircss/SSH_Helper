using System.Data;
using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

/// <summary>
/// Tests for the CsvManager service.
/// </summary>
public class CsvManagerTests : IDisposable
{
    private readonly CsvManager _csvManager;
    private readonly string _testDirectory;

    public CsvManagerTests()
    {
        _csvManager = new CsvManager();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"CsvManagerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region CreateEmptyTable Tests

    [Fact]
    public void CreateEmptyTable_ReturnsTableWithHostColumn()
    {
        var table = _csvManager.CreateEmptyTable();

        table.Should().NotBeNull();
        table.Columns.Count.Should().Be(1);
        table.Columns[0].ColumnName.Should().Be("Host_IP");
        table.Rows.Count.Should().Be(0);
    }

    #endregion

    #region LoadFromFile Tests

    [Fact]
    public void LoadFromFile_FileNotFound_ThrowsFileNotFoundException()
    {
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.csv");

        var action = () => _csvManager.LoadFromFile(nonExistentPath);

        action.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void LoadFromFile_EmptyFile_ThrowsInvalidDataException()
    {
        var filePath = Path.Combine(_testDirectory, "empty.csv");
        File.WriteAllText(filePath, "");

        var action = () => _csvManager.LoadFromFile(filePath);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void LoadFromFile_ValidCsv_ReturnsDataTable()
    {
        var filePath = Path.Combine(_testDirectory, "valid.csv");
        File.WriteAllText(filePath, "Host_IP,port,username\n192.168.1.1,22,admin\n192.168.1.2,2222,root");

        var table = _csvManager.LoadFromFile(filePath);

        table.Columns.Count.Should().Be(3);
        table.Columns[0].ColumnName.Should().Be("Host_IP");
        table.Columns[1].ColumnName.Should().Be("port");
        table.Columns[2].ColumnName.Should().Be("username");
        table.Rows.Count.Should().Be(2);
        table.Rows[0]["Host_IP"].Should().Be("192.168.1.1");
        table.Rows[0]["port"].Should().Be("22");
        table.Rows[1]["Host_IP"].Should().Be("192.168.1.2");
    }

    [Fact]
    public void LoadFromFile_QuotedValues_ParsesCorrectly()
    {
        var filePath = Path.Combine(_testDirectory, "quoted.csv");
        File.WriteAllText(filePath, "Host_IP,description\n192.168.1.1,\"Server, Main\"\n192.168.1.2,\"Has \"\"quotes\"\"\"");

        var table = _csvManager.LoadFromFile(filePath);

        table.Rows[0]["description"].Should().Be("Server, Main");
        table.Rows[1]["description"].Should().Be("Has \"quotes\"");
    }

    [Fact]
    public void LoadFromFile_EmptyColumnName_GeneratesDefault()
    {
        var filePath = Path.Combine(_testDirectory, "empty_header.csv");
        File.WriteAllText(filePath, "Host_IP,,port\n192.168.1.1,value,22");

        var table = _csvManager.LoadFromFile(filePath);

        table.Columns[1].ColumnName.Should().Be("Column1");
    }

    [Fact]
    public void LoadFromFile_SpacesInColumnNames_ReplacedWithUnderscores()
    {
        var filePath = Path.Combine(_testDirectory, "spaces.csv");
        File.WriteAllText(filePath, "Host_IP,Custom Column\n192.168.1.1,value");

        var table = _csvManager.LoadFromFile(filePath);

        table.Columns[1].ColumnName.Should().Be("Custom_Column");
    }

    [Fact]
    public void LoadFromFile_SkipsEmptyRows()
    {
        var filePath = Path.Combine(_testDirectory, "empty_rows.csv");
        File.WriteAllText(filePath, "Host_IP,port\n192.168.1.1,22\n\n192.168.1.2,23\n");

        var table = _csvManager.LoadFromFile(filePath);

        table.Rows.Count.Should().Be(2);
    }

    [Fact]
    public void LoadFromFile_ExtraColumnsInRow_TruncatesToHeaderLength()
    {
        var filePath = Path.Combine(_testDirectory, "extra_columns.csv");
        File.WriteAllText(filePath, "Host_IP,port\n192.168.1.1,22,extra,values");

        var table = _csvManager.LoadFromFile(filePath);

        table.Columns.Count.Should().Be(2);
        table.Rows[0].ItemArray.Length.Should().Be(2);
    }

    #endregion

    #region SaveToFile Tests

    [Fact]
    public void SaveToFile_ValidData_WritesCorrectCsv()
    {
        var filePath = Path.Combine(_testDirectory, "output.csv");
        var columns = new List<(string Name, string Header)>
        {
            ("Host_IP", "Host_IP"),
            ("port", "port")
        };
        var rows = new List<List<string?>>
        {
            new() { "192.168.1.1", "22" },
            new() { "192.168.1.2", "2222" }
        };

        _csvManager.SaveToFile(filePath, columns, rows);

        var content = File.ReadAllText(filePath);
        content.Should().Contain("Host_IP,port");
        content.Should().Contain("192.168.1.1,22");
        content.Should().Contain("192.168.1.2,2222");
    }

    [Fact]
    public void SaveToFile_ValuesWithCommas_QuotesValues()
    {
        var filePath = Path.Combine(_testDirectory, "comma_values.csv");
        var columns = new List<(string Name, string Header)>
        {
            ("Host_IP", "Host_IP"),
            ("description", "description")
        };
        var rows = new List<List<string?>>
        {
            new() { "192.168.1.1", "Server, Main" }
        };

        _csvManager.SaveToFile(filePath, columns, rows);

        var content = File.ReadAllText(filePath);
        content.Should().Contain("\"Server, Main\"");
    }

    [Fact]
    public void SaveToFile_ValuesWithQuotes_EscapesQuotes()
    {
        var filePath = Path.Combine(_testDirectory, "quote_values.csv");
        var columns = new List<(string Name, string Header)>
        {
            ("Host_IP", "Host_IP"),
            ("description", "description")
        };
        var rows = new List<List<string?>>
        {
            new() { "192.168.1.1", "Has \"quotes\"" }
        };

        _csvManager.SaveToFile(filePath, columns, rows);

        var content = File.ReadAllText(filePath);
        content.Should().Contain("\"Has \"\"quotes\"\"\"");
    }

    [Fact]
    public void SaveToFile_NullValues_WritesEmptyString()
    {
        var filePath = Path.Combine(_testDirectory, "null_values.csv");
        var columns = new List<(string Name, string Header)>
        {
            ("Host_IP", "Host_IP"),
            ("port", "port")
        };
        var rows = new List<List<string?>>
        {
            new() { "192.168.1.1", null }
        };

        _csvManager.SaveToFile(filePath, columns, rows);

        var content = File.ReadAllText(filePath);
        content.Should().Contain("192.168.1.1,");
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void RoundTrip_PreservesData()
    {
        var originalPath = Path.Combine(_testDirectory, "original.csv");
        var savedPath = Path.Combine(_testDirectory, "saved.csv");
        File.WriteAllText(originalPath, "Host_IP,port,username\n192.168.1.1,22,admin\n192.168.1.2,2222,root");

        // Load
        var table = _csvManager.LoadFromFile(originalPath);

        // Convert to save format
        var columns = new List<(string Name, string Header)>();
        foreach (DataColumn col in table.Columns)
        {
            columns.Add((col.ColumnName, col.ColumnName));
        }

        var rows = new List<List<string?>>();
        foreach (DataRow row in table.Rows)
        {
            var rowValues = new List<string?>();
            foreach (var item in row.ItemArray)
            {
                rowValues.Add(item?.ToString());
            }
            rows.Add(rowValues);
        }

        // Save
        _csvManager.SaveToFile(savedPath, columns, rows);

        // Reload
        var reloadedTable = _csvManager.LoadFromFile(savedPath);

        // Verify
        reloadedTable.Columns.Count.Should().Be(table.Columns.Count);
        reloadedTable.Rows.Count.Should().Be(table.Rows.Count);
        reloadedTable.Rows[0]["Host_IP"].Should().Be("192.168.1.1");
        reloadedTable.Rows[0]["port"].Should().Be("22");
    }

    #endregion
}
