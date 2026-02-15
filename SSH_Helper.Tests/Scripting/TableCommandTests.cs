using System.Collections.Generic;
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
}
