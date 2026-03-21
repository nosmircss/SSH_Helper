using System;
using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class DateTimeFunctionTests
{
    private readonly SetCommand _command = new();

    private async Task<string> Eval(string expression, ScriptContext? context = null)
    {
        context ??= new ScriptContext();
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariableString("result");
    }

    // --- now ---

    [Fact]
    public async Task Now_DefaultFormat()
    {
        var result = await Eval("now()");
        result.Should().MatchRegex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
    }

    [Fact]
    public async Task Now_CustomFormat()
    {
        var result = await Eval("now(\"yyyy-MM-dd\")");
        result.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
    }

    // --- epoch ---

    [Fact]
    public async Task Epoch_ReturnsCurrentUnixTime()
    {
        var result = await Eval("epoch()");
        long.TryParse(result, out var epoch).Should().BeTrue();
        var expected = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        epoch.Should().BeCloseTo(expected, 2);
    }

    [Fact]
    public async Task Epoch_CanRoundTripWithEpochToDate()
    {
        var context = new ScriptContext();
        (await _command.ExecuteAsync(new ScriptStep { Set = "unix_ts = epoch()" }, context, CancellationToken.None))
            .Success.Should().BeTrue();

        (await _command.ExecuteAsync(
            new ScriptStep { Set = "result = epoch_to_date(unix_ts, 'yyyy-MM-dd')" },
            context,
            CancellationToken.None))
            .Success.Should().BeTrue();

        context.GetVariableString("result").Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
    }

    // --- epoch_to_date ---

    [Fact]
    public async Task EpochToDate_KnownValue()
    {
        // 1700000000 = 2023-11-14 22:13:20 UTC
        var result = await Eval("epoch_to_date(1700000000, \"yyyy-MM-dd\")");
        result.Should().Be("2023-11-14");
    }

    // --- date_add ---

    [Fact]
    public async Task DateAdd_AddDays()
    {
        var result = await Eval("date_add(\"2024-01-01 00:00:00\", 5, \"days\")");
        result.Should().Be("2024-01-06 00:00:00");
    }

    [Fact]
    public async Task DateAdd_AddHours()
    {
        var result = await Eval("date_add(\"2024-01-01 00:00:00\", 3, \"hours\")");
        result.Should().Be("2024-01-01 03:00:00");
    }

    [Fact]
    public async Task DateAdd_AddMinutes()
    {
        var result = await Eval("date_add(\"2024-01-01 00:00:00\", 90, \"minutes\")");
        result.Should().Be("2024-01-01 01:30:00");
    }

    // --- date_diff ---

    [Fact]
    public async Task DateDiff_Days()
    {
        var result = await Eval("date_diff(\"2024-01-10 00:00:00\", \"2024-01-01 00:00:00\", \"days\")");
        result.Should().Be("9");
    }

    [Fact]
    public async Task DateDiff_Hours()
    {
        var result = await Eval("date_diff(\"2024-01-01 12:00:00\", \"2024-01-01 00:00:00\", \"hours\")");
        result.Should().Be("12");
    }

    // --- date_format ---

    [Fact]
    public async Task DateFormat_Reformat()
    {
        var result = await Eval("date_format(\"2024-01-15 14:30:00\", \"MM/dd/yyyy\")");
        result.Should().Be("01/15/2024");
    }

    [Fact]
    public async Task DateFormat_TimeOnly()
    {
        var result = await Eval("date_format(\"2024-01-15 14:30:45\", \"HH:mm\")");
        result.Should().Be("14:30");
    }
}
