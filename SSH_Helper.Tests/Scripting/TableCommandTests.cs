using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class TableCommandTests
{
    private readonly TableCommand _command = new();

    [Fact]
    public async Task ExecuteAsync_ListDataFromVariable_AutoHeaderUsesVariableName()
    {
        var step = new ScriptStep
        {
            Table = new TableOptions
            {
                Data = "${items}"
            }
        };
        var context = new ScriptContext();
        context.SetVariable("items", new List<string> { "server-01", "server-02", "server-03" });

        string? output = null;
        context.OutputReceived += (_, args) =>
        {
            if (args.Type == ScriptOutputType.Info)
                output = args.Message;
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        output.Should().NotBeNullOrEmpty();
        var lines = output!.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        lines[0].Trim().Should().Be("items");
        lines[1].Trim().Should().Be(new string('-', "server-03".Length));
        lines[2].Trim().Should().Be("server-01");
        lines[3].Trim().Should().Be("server-02");
        lines[4].Trim().Should().Be("server-03");
    }

    [Fact]
    public async Task ExecuteAsync_ListDataWithExplicitColumns_UsesConfiguredHeaderAndField()
    {
        var step = new ScriptStep
        {
            Table = new TableOptions
            {
                Data = "${items}",
                Columns = new List<TableColumn>
                {
                    new()
                    {
                        Header = "Host",
                        Field = "Value"
                    }
                }
            }
        };
        var context = new ScriptContext();
        context.SetVariable("items", new List<string> { "server-01" });

        string? output = null;
        context.OutputReceived += (_, args) =>
        {
            if (args.Type == ScriptOutputType.Info)
                output = args.Message;
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        output.Should().NotBeNullOrEmpty();
        output.Should().Contain("Host");
        output.Should().Contain("server-01");
    }

    [Fact]
    public async Task ExecuteAsync_JsonElementArrayOfObjects_RendersObjectRows()
    {
        var step = new ScriptStep
        {
            Table = new TableOptions
            {
                Data = "${rows}"
            }
        };

        var context = new ScriptContext();
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(
            "[{\"host\":\"server-01\",\"status\":\"up\"},{\"host\":\"server-02\",\"status\":\"down\"}]");
        context.SetVariable("rows", jsonElement);

        string? output = null;
        context.OutputReceived += (_, args) =>
        {
            if (args.Type == ScriptOutputType.Info)
                output = args.Message;
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        output.Should().NotBeNullOrEmpty();
        output.Should().Contain("server-01");
        output.Should().Contain("server-02");
        output.Should().Contain("up");
        output.Should().Contain("down");
    }

    [Fact]
    public async Task ExecuteAsync_JsonNodeArrayOfObjects_RendersObjectRows()
    {
        var step = new ScriptStep
        {
            Table = new TableOptions
            {
                Data = "${rows}"
            }
        };

        var context = new ScriptContext();
        context.SetVariable("rows", JsonNode.Parse("[{\"host\":\"edge-1\",\"latency\":12},{\"host\":\"edge-2\",\"latency\":9}]"));

        string? output = null;
        context.OutputReceived += (_, args) =>
        {
            if (args.Type == ScriptOutputType.Info)
                output = args.Message;
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        output.Should().NotBeNullOrEmpty();
        output.Should().Contain("edge-1");
        output.Should().Contain("edge-2");
        output.Should().Contain("12");
        output.Should().Contain("9");
    }

    [Fact]
    public async Task ExecuteAsync_JsonObjectString_RendersSingleDataRow()
    {
        var step = new ScriptStep
        {
            Table = new TableOptions
            {
                Data = "${row}"
            }
        };

        var context = new ScriptContext();
        context.SetVariable("row", "{\"host\":\"single-host\",\"status\":\"up\"}");

        string? output = null;
        context.OutputReceived += (_, args) =>
        {
            if (args.Type == ScriptOutputType.Info)
                output = args.Message;
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        output.Should().NotBeNullOrEmpty();
        output.Should().Contain("single-host");
        output.Should().Contain("up");
    }

    [Fact]
    public async Task ExecuteAsync_ListOfDictionaries_RendersEachDictionaryAsRow()
    {
        var step = new ScriptStep
        {
            Table = new TableOptions
            {
                Data = "${rows}"
            }
        };

        var context = new ScriptContext();
        context.SetVariable("rows", new List<object>
        {
            new Dictionary<string, object>
            {
                ["host"] = "core-1",
                ["status"] = "up"
            },
            new Dictionary<string, object>
            {
                ["host"] = "core-2",
                ["status"] = "down"
            }
        });

        string? output = null;
        context.OutputReceived += (_, args) =>
        {
            if (args.Type == ScriptOutputType.Info)
                output = args.Message;
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        output.Should().NotBeNullOrEmpty();
        output.Should().Contain("core-1");
        output.Should().Contain("core-2");
        output.Should().Contain("up");
        output.Should().Contain("down");
    }
}
