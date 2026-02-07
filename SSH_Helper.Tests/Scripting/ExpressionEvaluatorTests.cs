using FluentAssertions;
using System.Diagnostics;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ExpressionEvaluatorTests
{
    [Fact]
    public void Evaluate_ParenthesizedGroupWithOr_ReturnsTrue()
    {
        var context = new ScriptContext();
        context.SetVariable("x", 42);
        context.SetVariable("name", "TestHost");

        var evaluator = new ExpressionEvaluator(context);

        var result = evaluator.Evaluate("(x > 10 and x < 50) or name == 'Other'");

        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NestedGroupInRightOperand_RespectsGrouping()
    {
        var context = new ScriptContext();
        context.SetVariable("x", 5);
        context.SetVariable("name", "TestHost");

        var evaluator = new ExpressionEvaluator(context);

        var result = evaluator.Evaluate("x > 10 or (x < 10 and name == 'TestHost')");

        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ParenthesizedGroupWithOr_ReturnsFalseWhenAllFalse()
    {
        var context = new ScriptContext();
        context.SetVariable("x", 5);
        context.SetVariable("name", "Nope");

        var evaluator = new ExpressionEvaluator(context);

        var result = evaluator.Evaluate("(x > 10 and x < 50) or name == 'Other'");

        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_MatchesCatastrophicPattern_CompletesWithoutThrowing()
    {
        var context = new ScriptContext();
        context.SetVariable("payload", new string('a', 6000) + "!");

        var evaluator = new ExpressionEvaluator(context);
        var stopwatch = Stopwatch.StartNew();

        var result = evaluator.Evaluate("payload matches '(a+)+$'");

        stopwatch.Stop();
        result.Should().BeFalse();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void Evaluate_VariableWithSpaces_ResolvesCorrectly()
    {
        var context = new ScriptContext();
        context.SetVariable("name", "John Doe");

        var evaluator = new ExpressionEvaluator(context);

        var result = evaluator.Evaluate("${name} == \"John Doe\"");

        result.Should().BeTrue();
    }
}
