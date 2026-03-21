using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class StringFunctionTests
{
    private readonly SetCommand _command = new();

    private async Task<string> Eval(string expression, ScriptContext? context = null)
    {
        context ??= new ScriptContext();
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariableString("result");
    }

    // --- contains ---

    [Fact]
    public async Task Contains_Found_ReturnsTrue()
    {
        var result = await Eval("contains(\"hello world\", \"world\")");
        result.Should().Be("True");
    }

    [Fact]
    public async Task Contains_NotFound_ReturnsFalse()
    {
        var result = await Eval("contains(\"hello world\", \"xyz\")");
        result.Should().Be("False");
    }

    [Fact]
    public async Task Contains_CaseInsensitive()
    {
        var result = await Eval("contains(\"Hello World\", \"hello\")");
        result.Should().Be("True");
    }

    // --- startswith ---

    [Fact]
    public async Task StartsWith_Match_ReturnsTrue()
    {
        var result = await Eval("startswith(\"hello world\", \"hello\")");
        result.Should().Be("True");
    }

    [Fact]
    public async Task StartsWith_NoMatch_ReturnsFalse()
    {
        var result = await Eval("startswith(\"hello world\", \"world\")");
        result.Should().Be("False");
    }

    // --- endswith ---

    [Fact]
    public async Task EndsWith_Match_ReturnsTrue()
    {
        var result = await Eval("endswith(\"hello world\", \"world\")");
        result.Should().Be("True");
    }

    [Fact]
    public async Task EndsWith_NoMatch_ReturnsFalse()
    {
        var result = await Eval("endswith(\"hello world\", \"hello\")");
        result.Should().Be("False");
    }

    // --- pad_left / pad_right ---

    [Fact]
    public async Task PadLeft_DefaultChar()
    {
        var result = await Eval("pad_left(\"42\", 5)");
        result.Should().Be("   42");
    }

    [Fact]
    public async Task PadLeft_CustomChar()
    {
        var result = await Eval("pad_left(\"42\", 5, \"0\")");
        result.Should().Be("00042");
    }

    [Fact]
    public async Task PadRight_DefaultChar()
    {
        var result = await Eval("pad_right(\"42\", 5)");
        result.Should().Be("42   ");
    }

    [Fact]
    public async Task PadRight_CustomChar()
    {
        var result = await Eval("pad_right(\"hi\", 5, \".\")");
        result.Should().Be("hi...");
    }

    // --- repeat ---

    [Fact]
    public async Task Repeat_Normal()
    {
        var result = await Eval("repeat(\"ab\", 3)");
        result.Should().Be("ababab");
    }

    [Fact]
    public async Task Repeat_Zero_ReturnsEmpty()
    {
        var result = await Eval("repeat(\"ab\", 0)");
        result.Should().BeEmpty();
    }

    // --- reverse ---

    [Fact]
    public async Task Reverse_String()
    {
        var result = await Eval("reverse(\"hello\")");
        result.Should().Be("olleh");
    }

    [Fact]
    public async Task Reverse_List()
    {
        var context = new ScriptContext();
        context.SetVariable("items", new System.Collections.Generic.List<string> { "a", "b", "c" });

        var step = new ScriptStep { Set = "result = reverse(items)" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        var list = context.GetVariable("result") as System.Collections.Generic.List<string>;
        list.Should().NotBeNull();
        list.Should().Equal("c", "b", "a");
    }

    // --- regex_replace ---

    [Fact]
    public async Task RegexReplace_BasicPattern()
    {
        var result = await Eval("regex_replace(\"abc123def\", \"\\d+\", \"NUM\")");
        result.Should().Be("abcNUMdef");
    }

    [Fact]
    public async Task RegexReplace_NoMatch_ReturnsOriginal()
    {
        var result = await Eval("regex_replace(\"hello\", \"\\d+\", \"NUM\")");
        result.Should().Be("hello");
    }

    // --- format ---

    [Fact]
    public async Task Format_PositionalArgs()
    {
        var result = await Eval("format(\"{0} has {1} items\", \"server\", \"42\")");
        result.Should().Be("server has 42 items");
    }

    [Fact]
    public async Task Format_WithVariables()
    {
        var context = new ScriptContext();
        context.SetVariable("name", "router1");
        context.SetVariable("count", "5");

        var result = await Eval("format(\"{0}: {1} ports\", name, count)", context);
        result.Should().Be("router1: 5 ports");
    }

    // --- char_at ---

    [Fact]
    public async Task CharAt_ValidIndex()
    {
        var result = await Eval("char_at(\"hello\", 1)");
        result.Should().Be("e");
    }

    [Fact]
    public async Task CharAt_OutOfBounds_ReturnsEmpty()
    {
        var result = await Eval("char_at(\"hi\", 5)");
        result.Should().BeEmpty();
    }

    // --- index_of ---

    [Fact]
    public async Task IndexOf_Found()
    {
        var result = await Eval("index_of(\"hello world\", \"world\")");
        result.Should().Be("6");
    }

    [Fact]
    public async Task IndexOf_NotFound()
    {
        var result = await Eval("index_of(\"hello\", \"xyz\")");
        result.Should().Be("-1");
    }

    // --- String concat via ExpressionParser ---

    [Fact]
    public async Task StringConcat_TwoStrings()
    {
        var context = new ScriptContext();
        context.SetVariable("greeting", "Hello");
        context.SetVariable("name", "World");

        var step = new ScriptStep { Set = "result = greeting + \" \" + name" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        context.GetVariableString("result").Should().Be("Hello World");
    }

    [Fact]
    public async Task StringConcat_StringAndNumber()
    {
        var context = new ScriptContext();
        context.SetVariable("prefix", "Port");
        context.SetVariable("num", "22");

        // num resolves to 22.0 (a double) and prefix is non-numeric, so + concatenates
        var step = new ScriptStep { Set = "result = prefix + num" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        context.GetVariableString("result").Should().Be("Port22");
    }
}
