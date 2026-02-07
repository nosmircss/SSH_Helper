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
}
