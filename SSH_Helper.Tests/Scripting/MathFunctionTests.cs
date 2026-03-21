using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class MathFunctionTests
{
    private readonly SetCommand _command = new();

    private async Task<string> Eval(string expression, ScriptContext? context = null)
    {
        context ??= new ScriptContext();
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariableString("result");
    }

    // --- abs ---

    [Fact]
    public async Task Abs_Positive() => (await Eval("abs(5)")).Should().Be("5");

    [Fact]
    public async Task Abs_Negative() => (await Eval("abs(-5)")).Should().Be("5");

    [Fact]
    public async Task Abs_Zero() => (await Eval("abs(0)")).Should().Be("0");

    [Fact]
    public async Task Abs_Float() => (await Eval("abs(-3.14)")).Should().Be("3.14");

    // --- min / max ---

    [Fact]
    public async Task Min_TwoValues() => (await Eval("min(3, 7)")).Should().Be("3");

    [Fact]
    public async Task Min_MultipleValues() => (await Eval("min(5, 2, 8, 1)")).Should().Be("1");

    [Fact]
    public async Task Max_TwoValues() => (await Eval("max(3, 7)")).Should().Be("7");

    [Fact]
    public async Task Max_MultipleValues() => (await Eval("max(5, 2, 8, 1)")).Should().Be("8");

    [Fact]
    public async Task Min_WithVariables()
    {
        var context = new ScriptContext();
        context.SetVariable("a", "10");
        context.SetVariable("b", "3");
        (await Eval("min(a, b)", context)).Should().Be("3");
    }

    // --- round ---

    [Fact]
    public async Task Round_Default() => (await Eval("round(3.7)")).Should().Be("4");

    [Fact]
    public async Task Round_Down() => (await Eval("round(3.2)")).Should().Be("3");

    [Fact]
    public async Task Round_WithDecimals() => (await Eval("round(3.14159, 2)")).Should().Be("3.14");

    // --- floor / ceil ---

    [Fact]
    public async Task Floor_Positive() => (await Eval("floor(3.7)")).Should().Be("3");

    [Fact]
    public async Task Floor_Negative() => (await Eval("floor(-3.2)")).Should().Be("-4");

    [Fact]
    public async Task Ceil_Positive() => (await Eval("ceil(3.2)")).Should().Be("4");

    [Fact]
    public async Task Ceil_Negative() => (await Eval("ceil(-3.7)")).Should().Be("-3");

    // --- pow ---

    [Fact]
    public async Task Pow_IntegerResult() => (await Eval("pow(2, 3)")).Should().Be("8");

    [Fact]
    public async Task Pow_FloatResult() => (await Eval("pow(2, 0.5)")).Should().StartWith("1.41");

    // --- sqrt ---

    [Fact]
    public async Task Sqrt_PerfectSquare() => (await Eval("sqrt(9)")).Should().Be("3");

    [Fact]
    public async Task Sqrt_NonPerfect() => (await Eval("sqrt(2)")).Should().StartWith("1.41");

    // --- clamp ---

    [Fact]
    public async Task Clamp_InRange() => (await Eval("clamp(5, 0, 10)")).Should().Be("5");

    [Fact]
    public async Task Clamp_BelowMin() => (await Eval("clamp(-5, 0, 10)")).Should().Be("0");

    [Fact]
    public async Task Clamp_AboveMax() => (await Eval("clamp(15, 0, 10)")).Should().Be("10");

    // --- random ---

    [Fact]
    public async Task Random_ReturnsIntegerInRange()
    {
        var result = await Eval("random(1, 10)");
        int.TryParse(result, out var num).Should().BeTrue();
        num.Should().BeInRange(1, 10);
    }

    [Fact]
    public async Task Random_DefaultRange()
    {
        var result = await Eval("random()");
        int.TryParse(result, out var num).Should().BeTrue();
        num.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task Random_SingleValueRange_ReturnsThatValue()
    {
        (await Eval("random(5, 5)")).Should().Be("5");
    }

    [Fact]
    public async Task Random_ReversedBounds_AutoSwaps()
    {
        var result = await Eval("random(10, 1)");
        int.TryParse(result, out var num).Should().BeTrue();
        num.Should().BeInRange(1, 10);
    }

    [Fact]
    public async Task Random_NegativeRange_Supported()
    {
        var result = await Eval("random(-10, -1)");
        int.TryParse(result, out var num).Should().BeTrue();
        num.Should().BeInRange(-10, -1);
    }

    [Fact]
    public async Task Random_InvalidMax_UsesDefault()
    {
        var result = await Eval("random(50, 'oops')");
        int.TryParse(result, out var num).Should().BeTrue();
        num.Should().BeInRange(50, 100);
    }

    [Fact]
    public async Task Random_IntMaxBoundary_DoesNotOverflow()
    {
        var result = await Eval($"random({int.MaxValue - 1}, {int.MaxValue})");
        int.TryParse(result, out var num).Should().BeTrue();
        num.Should().BeInRange(int.MaxValue - 1, int.MaxValue);
    }
}
