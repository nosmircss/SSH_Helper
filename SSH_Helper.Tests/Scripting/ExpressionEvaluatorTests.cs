using FluentAssertions;
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
}
