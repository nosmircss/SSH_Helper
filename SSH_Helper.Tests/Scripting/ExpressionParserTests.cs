using System;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ExpressionParserTests
{
    private static object? Eval(string expression, ScriptContext? context = null)
    {
        context ??= new ScriptContext();
        var parser = new ExpressionParser(expression, context);
        return parser.Parse();
    }

    private static double EvalDouble(string expression, ScriptContext? context = null)
    {
        var result = Eval(expression, context);
        return Convert.ToDouble(result);
    }

    // --- Basic arithmetic ---

    [Theory]
    [InlineData("1 + 2", 3.0)]
    [InlineData("10 - 3", 7.0)]
    [InlineData("4 * 5", 20.0)]
    [InlineData("15 / 3", 5.0)]
    [InlineData("10 % 3", 1.0)]
    public void Arithmetic_BasicOperations(string expr, double expected)
    {
        EvalDouble(expr).Should().Be(expected);
    }

    [Theory]
    [InlineData("2 + 3 * 4", 14.0)]
    [InlineData("(2 + 3) * 4", 20.0)]
    [InlineData("10 - 2 * 3", 4.0)]
    [InlineData("10 / 2 + 3", 8.0)]
    public void Arithmetic_OperatorPrecedence(string expr, double expected)
    {
        EvalDouble(expr).Should().Be(expected);
    }

    [Theory]
    [InlineData("-5", -5.0)]
    [InlineData("+5", 5.0)]
    [InlineData("-(3 + 2)", -5.0)]
    [InlineData("--5", 5.0)]
    public void Arithmetic_UnaryOperators(string expr, double expected)
    {
        EvalDouble(expr).Should().Be(expected);
    }

    [Fact]
    public void Arithmetic_DivisionByZero_ReturnsZero()
    {
        EvalDouble("10 / 0").Should().Be(0.0);
    }

    [Fact]
    public void Arithmetic_ModuloByZero_ReturnsZero()
    {
        EvalDouble("10 % 0").Should().Be(0.0);
    }

    // --- Variable resolution ---

    [Fact]
    public void Variables_ResolvesNumericVariable()
    {
        var context = new ScriptContext();
        context.SetVariable("x", "10");
        EvalDouble("x + 5", context).Should().Be(15.0);
    }

    [Fact]
    public void Variables_ResolvesMultipleVariables()
    {
        var context = new ScriptContext();
        context.SetVariable("a", "3");
        context.SetVariable("b", "7");
        EvalDouble("a * b", context).Should().Be(21.0);
    }

    [Fact]
    public void Variables_ResolvesLength()
    {
        var context = new ScriptContext();
        context.SetVariable("items", new System.Collections.Generic.List<string> { "a", "b", "c" });
        EvalDouble("items.length", context).Should().Be(3.0);
    }

    // --- Nested parentheses ---

    [Theory]
    [InlineData("((1 + 2) * (3 + 4))", 21.0)]
    [InlineData("(((5)))", 5.0)]
    public void Arithmetic_NestedParentheses(string expr, double expected)
    {
        EvalDouble(expr).Should().Be(expected);
    }

    // --- Floating point ---

    [Theory]
    [InlineData("1.5 + 2.5", 4.0)]
    [InlineData("3.14 * 2", 6.28)]
    public void Arithmetic_FloatingPoint(string expr, double expected)
    {
        EvalDouble(expr).Should().BeApproximately(expected, 0.001);
    }

    // --- Function calls inside expressions ---

    [Fact]
    public void Functions_LengthInExpression()
    {
        var context = new ScriptContext();
        context.SetVariable("name", "hello");
        EvalDouble("length(name) + 1", context).Should().Be(6.0);
    }

    // --- Error handling ---

    [Fact]
    public void Error_UnmatchedParenthesis_Throws()
    {
        Action act = () => Eval("(1 + 2");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Error_TrailingTokens_Throws()
    {
        Action act = () => Eval("1 + 2 3");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void StringConcat_NonNumericVariable_Concatenates()
    {
        var context = new ScriptContext();
        context.SetVariable("name", "hello");

        var result = Eval("name + 1", context);
        result.Should().Be("hello1");
    }

    // --- Complex expressions matching ArithmeticParser behavior ---

    [Fact]
    public void Complex_VariableArithmetic()
    {
        var context = new ScriptContext();
        context.SetVariable("count", "10");
        context.SetVariable("offset", "3");
        EvalDouble("count - offset * 2 + 1", context).Should().Be(5.0);
    }

    [Fact]
    public void Complex_NestedWithVariables()
    {
        var context = new ScriptContext();
        context.SetVariable("x", "5");
        context.SetVariable("y", "2");
        EvalDouble("(x + y) * (x - y)", context).Should().Be(21.0);
    }

    // --- Ternary operator ---

    [Fact]
    public void Ternary_TrueCondition()
    {
        Eval("1 ? \"yes\" : \"no\"").Should().Be("yes");
    }

    [Fact]
    public void Ternary_FalseCondition()
    {
        Eval("0 ? \"yes\" : \"no\"").Should().Be("no");
    }

    [Fact]
    public void Ternary_WithVariables()
    {
        var context = new ScriptContext();
        context.SetVariable("status", "up");
        Eval("status ? \"Online\" : \"Offline\"", context).Should().Be("Online");
    }

    [Fact]
    public void Ternary_Nested()
    {
        // 1 ? (0 ? "a" : "b") : "c" => "b"
        Eval("1 ? 0 ? \"a\" : \"b\" : \"c\"").Should().Be("b");
    }

    [Fact]
    public void Ternary_NumericResult()
    {
        EvalDouble("1 ? 42 : 0").Should().Be(42.0);
    }

    [Fact]
    public void Ternary_WithArithmetic()
    {
        EvalDouble("1 ? 2 + 3 : 10").Should().Be(5.0);
    }

    [Fact]
    public void Ternary_EmptyStringIsFalsy()
    {
        var context = new ScriptContext();
        context.SetVariable("val", "");
        Eval("val ? \"has value\" : \"empty\"", context).Should().Be("empty");
    }

    // --- Null coalescing ---

    [Fact]
    public void NullCoalesce_LeftNotNull_ReturnsLeft()
    {
        var context = new ScriptContext();
        context.SetVariable("name", "Alice");
        Eval("name ?? \"default\"", context).Should().Be("Alice");
    }

    [Fact]
    public void NullCoalesce_LeftNull_ReturnsRight()
    {
        var context = new ScriptContext();
        // undefined_var is not set
        Eval("undefined_var ?? \"fallback\"", context).Should().Be("fallback");
    }

    [Fact]
    public void NullCoalesce_LeftEmpty_ReturnsRight()
    {
        var context = new ScriptContext();
        context.SetVariable("val", "");
        Eval("val ?? \"fallback\"", context).Should().Be("fallback");
    }

    [Fact]
    public void NullCoalesce_Chain()
    {
        var context = new ScriptContext();
        // a and b not set, c is set
        context.SetVariable("c", "found");
        Eval("a ?? b ?? c ?? \"none\"", context).Should().Be("found");
    }

    // --- iif() function ---

    [Fact]
    public void Iif_TrueCondition()
    {
        var context = new ScriptContext();
        context.SetVariable("x", "5");
        var step = new SSH_Helper.Services.Scripting.Models.ScriptStep { Set = "result = iif(x, \"yes\", \"no\")" };
        new SSH_Helper.Services.Scripting.Commands.SetCommand().ExecuteAsync(step, context, System.Threading.CancellationToken.None).Wait();
        context.GetVariableString("result").Should().Be("yes");
    }

    [Fact]
    public void Iif_FalseCondition()
    {
        var context = new ScriptContext();
        context.SetVariable("x", "false");
        var step = new SSH_Helper.Services.Scripting.Models.ScriptStep { Set = "result = iif(x, \"yes\", \"no\")" };
        new SSH_Helper.Services.Scripting.Commands.SetCommand().ExecuteAsync(step, context, System.Threading.CancellationToken.None).Wait();
        context.GetVariableString("result").Should().Be("no");
    }

    // --- String concatenation in ExpressionParser ---

    [Fact]
    public void StringConcat_QuotedStrings()
    {
        Eval("\"hello\" + \" \" + \"world\"").Should().Be("hello world");
    }

    [Fact]
    public void StringConcat_MixedTypes()
    {
        var context = new ScriptContext();
        context.SetVariable("name", "server");
        Eval("name + \"-01\"", context).Should().Be("server-01");
    }
}
