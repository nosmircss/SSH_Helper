using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class DateTimeFunctionEnhancementTests
{
    private readonly SetCommand _command = new();

    private async Task<string> Eval(string expression, ScriptContext? context = null)
    {
        context ??= new ScriptContext();
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariableString("result");
    }

    [Fact]
    public async Task DateAdd_Months()
    {
        (await Eval("date_add(\"2026-01-15 00:00:00\", 3, \"months\")"))
            .Should().Be("2026-04-15 00:00:00");
    }

    [Fact]
    public async Task DateAdd_Month_ClampsEndOfMonth()
    {
        (await Eval("date_add(\"2026-01-31 00:00:00\", 1, \"month\")"))
            .Should().Be("2026-02-28 00:00:00");
    }

    [Fact]
    public async Task DateAdd_Years()
    {
        (await Eval("date_add(\"2026-01-15 00:00:00\", 1, \"year\")"))
            .Should().Be("2027-01-15 00:00:00");
    }

    [Fact]
    public async Task DateAdd_Weeks()
    {
        (await Eval("date_add(\"2026-01-15 00:00:00\", 2, \"weeks\")"))
            .Should().Be("2026-01-29 00:00:00");
    }

    [Fact]
    public async Task DateDiff_Weeks()
    {
        (await Eval("date_diff(\"2026-01-29 00:00:00\", \"2026-01-15 00:00:00\", \"weeks\")"))
            .Should().Be("2");
    }

    [Fact]
    public async Task NowUtc_ReturnsTimestampFormat()
    {
        (await Eval("now_utc()")).Should().MatchRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$");
    }

    [Fact]
    public async Task NowLocal_ReturnsTimestampFormat()
    {
        (await Eval("now_local()")).Should().MatchRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$");
    }

    [Fact]
    public async Task ParseDate_ExplicitFormat()
    {
        (await Eval("parse_date(\"15-01-2026\", \"dd-MM-yyyy\")"))
            .Should().Be("2026-01-15 00:00:00");
    }
}
