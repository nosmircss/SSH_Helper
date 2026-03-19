using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class CollectionFunctionTests
{
    private readonly SetCommand _command = new();

    private async Task<object?> EvalRaw(string expression, ScriptContext context)
    {
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariable("result");
    }

    private async Task<string> Eval(string expression, ScriptContext? context = null)
    {
        context ??= new ScriptContext();
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariableString("result");
    }

    private static ScriptContext WithList(string name, params string[] items)
    {
        var context = new ScriptContext();
        context.SetVariable(name, new List<string>(items));
        return context;
    }

    // --- map ---

    [Fact]
    public async Task Map_TransformItems()
    {
        var context = WithList("names", "alice", "bob", "charlie");
        var result = await EvalRaw("map(names, x => upper(x))", context) as List<string>;
        result.Should().NotBeNull();
        result.Should().Equal("ALICE", "BOB", "CHARLIE");
    }

    [Fact]
    public async Task Map_EmptyList()
    {
        var context = WithList("items");
        var result = await EvalRaw("map(items, x => upper(x))", context) as List<string>;
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Map_WithArithmetic()
    {
        var context = WithList("nums", "1", "2", "3");
        var result = await EvalRaw("map(nums, x => x + 10)", context) as List<string>;
        result.Should().NotBeNull();
        result.Should().Equal("11", "12", "13");
    }

    // --- filter ---

    [Fact]
    public async Task Filter_KeepsMatchingItems()
    {
        var context = WithList("items", "hello", "", "world", "");
        var result = await EvalRaw("filter(items, x => x)", context) as List<string>;
        result.Should().NotBeNull();
        result.Should().Equal("hello", "world");
    }

    [Fact]
    public async Task Filter_WithFunction()
    {
        var context = WithList("words", "hi", "hello", "hey", "greetings");
        var result = await EvalRaw("filter(words, w => startswith(w, \"he\"))", context) as List<string>;
        result.Should().NotBeNull();
        result.Should().Equal("hello", "hey");
    }

    // --- reduce ---

    [Fact]
    public async Task Reduce_SumNumbers()
    {
        var context = WithList("nums", "1", "2", "3", "4");
        var result = await Eval("reduce(nums, (acc, x) => acc + x, 0)", context);
        result.Should().Be("10");
    }

    [Fact]
    public async Task Reduce_ConcatStrings()
    {
        var context = WithList("words", "hello", " ", "world");
        var result = await Eval("reduce(words, (acc, x) => acc + x, \"\")", context);
        result.Should().Be("hello world");
    }

    // --- find ---

    [Fact]
    public async Task Find_ReturnsFirstMatch()
    {
        var context = WithList("nums", "1", "5", "10", "15");
        var result = await Eval("find(nums, x => x + 0 > 7)", context);
        result.Should().Be("10");
    }

    [Fact]
    public async Task Find_NoMatch_ReturnsEmpty()
    {
        var context = WithList("nums", "1", "2", "3");
        var result = await Eval("find(nums, x => x + 0 > 100)", context);
        result.Should().BeEmpty();
    }

    // --- any / all ---

    [Fact]
    public async Task Any_SomeMatch_ReturnsTrue()
    {
        var context = WithList("items", "a", "", "b");
        var result = await Eval("any(items, x => x)", context);
        result.Should().Be("True");
    }

    [Fact]
    public async Task Any_NoneMatch_ReturnsFalse()
    {
        var context = WithList("items", "", "", "");
        var result = await Eval("any(items, x => x)", context);
        result.Should().Be("False");
    }

    [Fact]
    public async Task All_AllMatch_ReturnsTrue()
    {
        var context = WithList("items", "a", "b", "c");
        var result = await Eval("all(items, x => x)", context);
        result.Should().Be("True");
    }

    [Fact]
    public async Task All_SomeFail_ReturnsFalse()
    {
        var context = WithList("items", "a", "", "c");
        var result = await Eval("all(items, x => x)", context);
        result.Should().Be("False");
    }

    // --- count ---

    [Fact]
    public async Task Count_AllItems()
    {
        var context = WithList("items", "a", "b", "c");
        var result = await Eval("count(items)", context);
        result.Should().Be("3");
    }

    [Fact]
    public async Task Count_WithLambda()
    {
        var context = WithList("items", "hello", "", "world", "");
        var result = await Eval("count(items, x => x)", context);
        result.Should().Be("2");
    }

    // --- range ---

    [Fact]
    public async Task Range_BasicSequence()
    {
        var result = await EvalRaw("range(0, 5)", new ScriptContext()) as List<string>;
        result.Should().NotBeNull();
        result.Should().Equal("0", "1", "2", "3", "4");
    }

    [Fact]
    public async Task Range_WithStep()
    {
        var result = await EvalRaw("range(0, 10, 3)", new ScriptContext()) as List<string>;
        result.Should().NotBeNull();
        result.Should().Equal("0", "3", "6", "9");
    }

    [Fact]
    public async Task Range_Descending()
    {
        var result = await EvalRaw("range(5, 0, -1)", new ScriptContext()) as List<string>;
        result.Should().NotBeNull();
        result.Should().Equal("5", "4", "3", "2", "1");
    }

    // --- slice ---

    [Fact]
    public async Task Slice_BasicSublist()
    {
        var context = WithList("items", "a", "b", "c", "d", "e");
        var result = await EvalRaw("slice(items, 1, 3)", context) as List<string>;
        result.Should().NotBeNull();
        result.Should().Equal("b", "c");
    }

    [Fact]
    public async Task Slice_NegativeIndex()
    {
        var context = WithList("items", "a", "b", "c", "d", "e");
        var result = await EvalRaw("slice(items, -2)", context) as List<string>;
        result.Should().NotBeNull();
        result.Should().Equal("d", "e");
    }

    // --- flatten ---

    [Fact]
    public async Task Flatten_NestedJsonArrays()
    {
        var context = new ScriptContext();
        context.SetVariable("nested", "[\"a\",[\"b\",\"c\"],\"d\"]");
        var result = await EvalRaw("flatten(nested)", context) as List<string>;
        result.Should().NotBeNull();
        result.Should().Equal("a", "b", "c", "d");
    }

    // --- zip ---

    [Fact]
    public async Task Zip_PairsElements()
    {
        var context = new ScriptContext();
        context.SetVariable("keys", new List<string> { "a", "b", "c" });
        context.SetVariable("vals", new List<string> { "1", "2", "3" });
        var result = await EvalRaw("zip(keys, vals)", context) as List<string>;
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result![0].Should().Contain("\"a\"").And.Contain("\"1\"");
    }

    // --- Lambda scoping ---

    [Fact]
    public async Task Lambda_DoesNotLeakVariable()
    {
        var context = WithList("items", "a", "b");
        context.SetVariable("x", "original");

        await EvalRaw("map(items, x => upper(x))", context);

        // x should be restored to its original value
        context.GetVariableString("x").Should().Be("original");
    }

    [Fact]
    public async Task Lambda_UndefinedVarStaysUndefined()
    {
        var context = WithList("items", "a", "b");

        await EvalRaw("map(items, z => upper(z))", context);

        // z should not exist after the lambda finishes
        context.HasVariable("z").Should().BeFalse();
    }
}
