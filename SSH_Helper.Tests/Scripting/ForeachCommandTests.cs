using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ForeachCommandTests
{
    [Fact]
    public async Task ExecuteAsync_JsonStringArrayItems_AreNotQuoted()
    {
        var executor = new ScriptExecutor();
        var command = new ForeachCommand(executor);
        var context = new ScriptContext();
        context.SetVariable("arr", "[\"one\",\"two\"]");

        var step = new ScriptStep
        {
            Foreach = "item in arr",
            Do = new List<ScriptStep>
            {
                new() { Set = "collected = push(collected, item)" }
            }
        };

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var collected = context.GetVariable("collected").Should().BeOfType<List<string>>().Subject;
        collected.Should().Equal("one", "two");
    }

    [Fact]
    public async Task ExecuteAsync_SplitExpression_IteratesResolvedValues()
    {
        var executor = new ScriptExecutor();
        var command = new ForeachCommand(executor);
        var context = new ScriptContext();
        context.SetVariable("csv_ports", "22,80,443");

        var step = new ScriptStep
        {
            Foreach = "port in split(csv_ports, ',')",
            Do = new List<ScriptStep>
            {
                new() { Set = "ports = push(ports, trim(port))" }
            }
        };

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("ports").Should().BeEquivalentTo(new List<string> { "22", "80", "443" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecuteAsync_JsonItemsExpression_IteratesResolvedValues()
    {
        var executor = new ScriptExecutor();
        var command = new ForeachCommand(executor);
        var context = new ScriptContext();
        context.SetVariable("response", "{\"data\":{\"tags\":[\"malware\",\"phishing\"]}}");

        var step = new ScriptStep
        {
            Foreach = "tag in json.items(response, 'data.tags')",
            Do = new List<ScriptStep>
            {
                new() { Set = "tags = push(tags, tag)" }
            }
        };

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("tags").Should().BeEquivalentTo(new List<string> { "malware", "phishing" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecuteAsync_MissingVariableCollection_DoesNotTreatIdentifierAsLiteral()
    {
        var executor = new ScriptExecutor();
        var command = new ForeachCommand(executor);
        var context = new ScriptContext();

        var step = new ScriptStep
        {
            Foreach = "item in missing_collection",
            Do = new List<ScriptStep>
            {
                new() { Set = "collected = push(collected, item)" }
            }
        };

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.HasVariable("collected").Should().BeFalse();
    }
}
