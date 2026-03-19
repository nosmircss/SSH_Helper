using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class TypeFunctionTests
{
    private readonly SetCommand _command = new();

    private async Task<string> Eval(string expression, ScriptContext? context = null)
    {
        context ??= new ScriptContext();
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariableString("result");
    }

    // --- int ---

    [Fact]
    public async Task Int_FromFloat() => (await Eval("int(\"3.7\")")).Should().Be("3");

    [Fact]
    public async Task Int_FromString() => (await Eval("int(\"42\")")).Should().Be("42");

    [Fact]
    public async Task Int_FromBoolTrue() => (await Eval("int(\"true\")")).Should().Be("1");

    // --- float ---

    [Fact]
    public async Task Float_FromInt() => (await Eval("float(\"42\")")).Should().Be("42");

    [Fact]
    public async Task Float_FromString() => (await Eval("float(\"3.14\")")).Should().Be("3.14");

    // --- str ---

    [Fact]
    public async Task Str_FromNumber()
    {
        var context = new ScriptContext();
        context.SetVariable("num", 42);
        (await Eval("str(num)", context)).Should().Be("42");
    }

    // --- bool ---

    [Fact]
    public async Task Bool_Truthy() => (await Eval("bool(\"hello\")")).Should().Be("True");

    [Fact]
    public async Task Bool_Falsy() => (await Eval("bool(\"\")")).Should().Be("False");

    // --- typeof ---

    [Fact]
    public async Task TypeOf_String()
    {
        var context = new ScriptContext();
        context.SetVariable("val", "hello");
        (await Eval("typeof(val)", context)).Should().Be("string");
    }

    [Fact]
    public async Task TypeOf_Number()
    {
        var context = new ScriptContext();
        context.SetVariable("val", "42");
        (await Eval("typeof(val)", context)).Should().Be("number");
    }

    [Fact]
    public async Task TypeOf_List()
    {
        var context = new ScriptContext();
        context.SetVariable("val", new List<string> { "a", "b" });
        (await Eval("typeof(val)", context)).Should().Be("list");
    }

    [Fact]
    public async Task TypeOf_Null()
    {
        (await Eval("typeof(undefined_xyz)")).Should().Be("null");
    }

    // --- is_number ---

    [Fact]
    public async Task IsNumber_True() => (await Eval("is_number(\"42\")")).Should().Be("True");

    [Fact]
    public async Task IsNumber_False() => (await Eval("is_number(\"hello\")")).Should().Be("False");

    // --- is_list ---

    [Fact]
    public async Task IsList_True()
    {
        var context = new ScriptContext();
        context.SetVariable("items", new List<string> { "a" });
        (await Eval("is_list(items)", context)).Should().Be("True");
    }

    [Fact]
    public async Task IsList_False()
    {
        var context = new ScriptContext();
        context.SetVariable("val", "hello");
        (await Eval("is_list(val)", context)).Should().Be("False");
    }

    // --- is_json ---

    [Fact]
    public async Task IsJson_Object()
    {
        var context = new ScriptContext();
        context.SetVariable("data", "{\"key\":\"value\"}");
        (await Eval("is_json(data)", context)).Should().Be("True");
    }

    [Fact]
    public async Task IsJson_NotJson()
    {
        var context = new ScriptContext();
        context.SetVariable("val", "hello");
        (await Eval("is_json(val)", context)).Should().Be("False");
    }

    // --- is_empty ---

    [Fact]
    public async Task IsEmpty_EmptyString() => (await Eval("is_empty(\"\")")).Should().Be("True");

    [Fact]
    public async Task IsEmpty_NonEmpty()
    {
        var context = new ScriptContext();
        context.SetVariable("val", "hello");
        (await Eval("is_empty(val)", context)).Should().Be("False");
    }

    [Fact]
    public async Task IsEmpty_EmptyList()
    {
        var context = new ScriptContext();
        context.SetVariable("items", new List<string>());
        (await Eval("is_empty(items)", context)).Should().Be("True");
    }
}
