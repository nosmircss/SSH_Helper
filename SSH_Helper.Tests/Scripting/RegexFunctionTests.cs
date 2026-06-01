using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class RegexFunctionTests
{
    private readonly SetCommand _command = new();

    private async Task<string> Eval(string expression, ScriptContext? context = null)
    {
        context ??= new ScriptContext();
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariableString("result");
    }

    private async Task<List<string>> EvalList(string expression)
    {
        var context = new ScriptContext();
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariable("result").Should().BeAssignableTo<List<string>>().Subject;
    }

    [Fact]
    public async Task RegexMatch_CapturesRequestedGroup()
    {
        (await Eval("regex_match(\"inet 10.0.0.5\", \"inet (\\d+\\.\\d+\\.\\d+\\.\\d+)\", 1)"))
            .Should().Be("10.0.0.5");
    }

    [Fact]
    public async Task RegexMatch_DefaultReturnsFullMatch()
    {
        (await Eval("regex_match(\"abc123\", \"[a-z]+\")")).Should().Be("abc");
    }

    [Fact]
    public async Task RegexMatch_NoMatch_ReturnsEmpty()
    {
        (await Eval("regex_match(\"abc\", \"x+\")")).Should().BeEmpty();
    }

    [Fact]
    public async Task RegexMatch_InvalidPattern_ReturnsEmpty()
    {
        (await Eval("regex_match(\"abc\", \"(\")")).Should().BeEmpty();
    }

    [Fact]
    public async Task RegexMatchAll_ReturnsAllMatches()
    {
        (await EvalList("regex_match_all(\"a1 b2 c3\", \"\\d\")"))
            .Should().Equal("1", "2", "3");
    }

    [Fact]
    public async Task RegexGroups_ReturnsGroupsOfFirstMatch()
    {
        (await EvalList("regex_groups(\"2026-01-15\", \"(\\d+)-(\\d+)-(\\d+)\")"))
            .Should().Equal("2026", "01", "15");
    }
}
