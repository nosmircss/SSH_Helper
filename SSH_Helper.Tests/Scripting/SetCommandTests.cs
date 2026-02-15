using System.Threading;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class SetCommandTests
{
    private readonly SetCommand _command = new();

    [Fact]
    public async Task ExecuteAsync_QuotedInterpolatedString_DoesNotKeepOuterQuotes()
    {
        var step = new ScriptStep
        {
            Set = "result_str = \"${hn} | Kernel ${ver}\""
        };

        var context = new ScriptContext();
        context.SetVariable("hn", "chris-NUC7i7DNHE");
        context.SetVariable("ver", "6.8.0-90-generic");

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("result_str").Should().Be("chris-NUC7i7DNHE | Kernel 6.8.0-90-generic");
    }

    [Fact]
    public async Task ExecuteAsync_QuotedLiteral_StoresWithoutQuotes()
    {
        var step = new ScriptStep
        {
            Set = "status = \"QA Complete\""
        };

        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("status").Should().Be("QA Complete");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRightHandSide_SetsEmptyString()
    {
        var step = new ScriptStep
        {
            Set = "empty_src = "
        };

        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.HasVariable("empty_src").Should().BeTrue();
        context.GetVariableString("empty_src").Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PushWithQuotedString_DoesNotStoreLiteralQuoteCharacters()
    {
        var step = new ScriptStep
        {
            Set = "services = push(services, \"sshd\")"
        };

        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var services = context.GetVariable("services").Should().BeOfType<List<string>>().Subject;
        services.Should().ContainSingle().Which.Should().Be("sshd");
    }

    [Fact]
    public async Task ExecuteAsync_PushList_DebugOutputShowsReadableListValues()
    {
        var step = new ScriptStep
        {
            Set = "services = push(services, \"sshd\")"
        };

        var context = new ScriptContext
        {
            DebugMode = true
        };

        string? debugMessage = null;
        context.OutputReceived += (_, args) =>
        {
            if (args.Type == ScriptOutputType.Debug)
                debugMessage = args.Message;
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        debugMessage.Should().Be("Set services = [sshd]");
    }

    [Fact]
    public async Task ExecuteAsync_Unshift_PrependsToList()
    {
        var context = new ScriptContext();

        (await _command.ExecuteAsync(new ScriptStep { Set = "interface_list = split('port1,port2', ',')" }, context, CancellationToken.None))
            .Success.Should().BeTrue();
        (await _command.ExecuteAsync(new ScriptStep { Set = "interface_list = unshift(interface_list, 'any')" }, context, CancellationToken.None))
            .Success.Should().BeTrue();

        var interfaces = context.GetVariable("interface_list").Should().BeOfType<List<string>>().Subject;
        interfaces.Should().Equal("any", "port1", "port2");
    }

    [Fact]
    public async Task ExecuteAsync_ShiftAndPop_MutateWritableListAndReturnRemovedValues()
    {
        var context = new ScriptContext();

        (await _command.ExecuteAsync(new ScriptStep { Set = "values = split('one,two,three', ',')" }, context, CancellationToken.None))
            .Success.Should().BeTrue();
        (await _command.ExecuteAsync(new ScriptStep { Set = "last = pop(values)" }, context, CancellationToken.None))
            .Success.Should().BeTrue();
        (await _command.ExecuteAsync(new ScriptStep { Set = "first = shift(values)" }, context, CancellationToken.None))
            .Success.Should().BeTrue();

        context.GetVariableString("last").Should().Be("three");
        context.GetVariableString("first").Should().Be("one");
        var values = context.GetVariable("values").Should().BeOfType<List<string>>().Subject;
        values.Should().Equal("two");
    }

    [Fact]
    public async Task ExecuteAsync_ListHelpers_FirstLastIndexOfConcat_WorkAsExpected()
    {
        var context = new ScriptContext();

        (await _command.ExecuteAsync(new ScriptStep { Set = "a = split('wan1,wan2', ',')" }, context, CancellationToken.None))
            .Success.Should().BeTrue();
        (await _command.ExecuteAsync(new ScriptStep { Set = "b = split('lan1,lan2', ',')" }, context, CancellationToken.None))
            .Success.Should().BeTrue();

        (await _command.ExecuteAsync(new ScriptStep { Set = "first_value = first(a)" }, context, CancellationToken.None))
            .Success.Should().BeTrue();
        (await _command.ExecuteAsync(new ScriptStep { Set = "last_value = last(a)" }, context, CancellationToken.None))
            .Success.Should().BeTrue();
        (await _command.ExecuteAsync(new ScriptStep { Set = "idx = indexof(a, 'WAN2')" }, context, CancellationToken.None))
            .Success.Should().BeTrue();
        (await _command.ExecuteAsync(new ScriptStep { Set = "combined = concat(a, b)" }, context, CancellationToken.None))
            .Success.Should().BeTrue();

        context.GetVariableString("first_value").Should().Be("wan1");
        context.GetVariableString("last_value").Should().Be("wan2");
        context.GetVariable("idx").Should().Be(1);

        var combined = context.GetVariable("combined").Should().BeOfType<List<string>>().Subject;
        combined.Should().Equal("wan1", "wan2", "lan1", "lan2");

        var originalA = context.GetVariable("a").Should().BeOfType<List<string>>().Subject;
        originalA.Should().Equal("wan1", "wan2");
    }

    [Fact]
    public async Task ExecuteAsync_JsonConstructor_WithNestedJsonExpression_StoresNestedObject()
    {
        var step = new ScriptStep
        {
            Set = "nested = json('server', json('host', '10.0.0.1', 'port', 22), 'client', json('timeout', 30))"
        };

        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();

        var nestedValue = context.GetVariable("nested").Should().BeOfType<string>().Subject;
        var rootNode = JsonNode.Parse(nestedValue);
        rootNode.Should().NotBeNull();
        var root = rootNode!.AsObject();

        root["server"].Should().NotBeNull();
        var server = root["server"]!.AsObject();
        server["host"]!.GetValue<string>().Should().Be("10.0.0.1");
        server["port"]!.GetValue<long>().Should().Be(22);

        root["client"].Should().NotBeNull();
        var client = root["client"]!.AsObject();
        client["timeout"]!.GetValue<long>().Should().Be(30);
    }

    [Fact]
    public async Task ExecuteAsync_JsonKeys_WithNestedPath_ReturnsNestedObjectKeys()
    {
        var context = new ScriptContext();

        var buildNested = new ScriptStep
        {
            Set = "nested = json('server', json('host', '10.0.0.1', 'port', 22), 'client', json('timeout', 30))"
        };

        var keyStep = new ScriptStep
        {
            Set = "server_keys = json.keys(nested, 'server')"
        };

        var buildResult = await _command.ExecuteAsync(buildNested, context, CancellationToken.None);
        var keyResult = await _command.ExecuteAsync(keyStep, context, CancellationToken.None);

        buildResult.Success.Should().BeTrue();
        keyResult.Success.Should().BeTrue();

        var keys = context.GetVariable("server_keys").Should().BeOfType<List<string>>().Subject;
        keys.Should().HaveCount(2);
        keys.Should().BeEquivalentTo(new[] { "host", "port" });
    }

    [Fact]
    public async Task ExecuteAsync_JsonGet_MissingPathWithoutDefault_StoresNullValue()
    {
        var context = new ScriptContext();

        var buildJson = new ScriptStep
        {
            Set = "device = json('name', 'router1')"
        };

        var missingGet = new ScriptStep
        {
            Set = "null_val = json.get(device, 'nonexistent')"
        };

        var buildResult = await _command.ExecuteAsync(buildJson, context, CancellationToken.None);
        var getResult = await _command.ExecuteAsync(missingGet, context, CancellationToken.None);

        buildResult.Success.Should().BeTrue();
        getResult.Success.Should().BeTrue();
        context.HasVariable("null_val").Should().BeTrue();
        context.GetVariable("null_val").Should().BeNull();
        context.GetVariableString("null_val").Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonPop_RemovesLastElementAndReturnsRemovedValue()
    {
        var context = new ScriptContext();
        var buildStep = new ScriptStep { Set = "arr = json([], 'one', 'two', 'three')" };
        var popStep = new ScriptStep { Set = "last = json.pop(arr)" };

        var buildResult = await _command.ExecuteAsync(buildStep, context, CancellationToken.None);
        var popResult = await _command.ExecuteAsync(popStep, context, CancellationToken.None);

        buildResult.Success.Should().BeTrue();
        popResult.Success.Should().BeTrue();
        context.GetVariableString("last").Should().Be("three");

        var arr = GetArrayVariable(context, "arr");
        arr.Should().HaveCount(2);
        arr[0]!.GetValue<string>().Should().Be("one");
        arr[1]!.GetValue<string>().Should().Be("two");
    }

    [Fact]
    public async Task ExecuteAsync_JsonShift_RemovesFirstElementAndReturnsRemovedValue()
    {
        var context = new ScriptContext();
        var buildStep = new ScriptStep { Set = "arr = json([], 'one', 'two', 'three')" };
        var shiftStep = new ScriptStep { Set = "first = json.shift(arr)" };

        var buildResult = await _command.ExecuteAsync(buildStep, context, CancellationToken.None);
        var shiftResult = await _command.ExecuteAsync(shiftStep, context, CancellationToken.None);

        buildResult.Success.Should().BeTrue();
        shiftResult.Success.Should().BeTrue();
        context.GetVariableString("first").Should().Be("one");

        var arr = GetArrayVariable(context, "arr");
        arr.Should().HaveCount(2);
        arr[0]!.GetValue<string>().Should().Be("two");
        arr[1]!.GetValue<string>().Should().Be("three");
    }

    [Fact]
    public async Task ExecuteAsync_JsonLast_IsNonDestructive()
    {
        var context = new ScriptContext();
        var buildStep = new ScriptStep { Set = "arr = json([], 'one', 'two')" };
        var lastStep = new ScriptStep { Set = "last = json.last(arr)" };

        var buildResult = await _command.ExecuteAsync(buildStep, context, CancellationToken.None);
        var lastResult = await _command.ExecuteAsync(lastStep, context, CancellationToken.None);

        buildResult.Success.Should().BeTrue();
        lastResult.Success.Should().BeTrue();
        context.GetVariableString("last").Should().Be("two");

        var arr = GetArrayVariable(context, "arr");
        arr.Should().HaveCount(2);
        arr[0]!.GetValue<string>().Should().Be("one");
        arr[1]!.GetValue<string>().Should().Be("two");
    }

    [Fact]
    public async Task ExecuteAsync_JsonFirst_IsNonDestructive()
    {
        var context = new ScriptContext();
        var buildStep = new ScriptStep { Set = "arr = json([], 'one', 'two')" };
        var firstStep = new ScriptStep { Set = "first = json.first(arr)" };

        var buildResult = await _command.ExecuteAsync(buildStep, context, CancellationToken.None);
        var firstResult = await _command.ExecuteAsync(firstStep, context, CancellationToken.None);

        buildResult.Success.Should().BeTrue();
        firstResult.Success.Should().BeTrue();
        context.GetVariableString("first").Should().Be("one");

        var arr = GetArrayVariable(context, "arr");
        arr.Should().HaveCount(2);
        arr[0]!.GetValue<string>().Should().Be("one");
        arr[1]!.GetValue<string>().Should().Be("two");
    }

    [Fact]
    public async Task ExecuteAsync_JsonPopAndShift_OnEmptyArray_ReturnNullAndLeaveArrayUnchanged()
    {
        var context = new ScriptContext();
        var buildStep = new ScriptStep { Set = "arr = json([])" };
        var popStep = new ScriptStep { Set = "popped = json.pop(arr)" };
        var shiftStep = new ScriptStep { Set = "shifted = json.shift(arr)" };

        var buildResult = await _command.ExecuteAsync(buildStep, context, CancellationToken.None);
        var popResult = await _command.ExecuteAsync(popStep, context, CancellationToken.None);
        var shiftResult = await _command.ExecuteAsync(shiftStep, context, CancellationToken.None);

        buildResult.Success.Should().BeTrue();
        popResult.Success.Should().BeTrue();
        shiftResult.Success.Should().BeTrue();
        context.GetVariable("popped").Should().BeNull();
        context.GetVariable("shifted").Should().BeNull();
        GetArrayVariable(context, "arr").Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonPopAndShift_WithNonWritableExpression_ReturnNullAndEmitWarning()
    {
        var context = new ScriptContext();
        var warnings = new List<string>();
        context.OutputReceived += (_, args) =>
        {
            if (args.Type == ScriptOutputType.Warning)
                warnings.Add(args.Message);
        };

        var buildStep = new ScriptStep { Set = "arr = json([], 'one', 'two', 'three')" };
        var popStep = new ScriptStep { Set = "pop_result = json.pop(json.slice(arr, 0, 2))" };
        var shiftStep = new ScriptStep { Set = "shift_result = json.shift(json.slice(arr, 0, 2))" };

        var buildResult = await _command.ExecuteAsync(buildStep, context, CancellationToken.None);
        var popResult = await _command.ExecuteAsync(popStep, context, CancellationToken.None);
        var shiftResult = await _command.ExecuteAsync(shiftStep, context, CancellationToken.None);

        buildResult.Success.Should().BeTrue();
        popResult.Success.Should().BeTrue();
        shiftResult.Success.Should().BeTrue();
        context.GetVariable("pop_result").Should().BeNull();
        context.GetVariable("shift_result").Should().BeNull();
        warnings.Should().Contain(message => message.Contains("json.pop requires writable top-level array variable reference"));
        warnings.Should().Contain(message => message.Contains("json.shift requires writable top-level array variable reference"));

        var arr = GetArrayVariable(context, "arr");
        arr.Should().HaveCount(3);
        arr[0]!.GetValue<string>().Should().Be("one");
        arr[1]!.GetValue<string>().Should().Be("two");
        arr[2]!.GetValue<string>().Should().Be("three");
    }

    [Fact]
    public async Task ExecuteAsync_JsonPopAndShift_WithDollarBraceArrayReference_MutatesSourceArray()
    {
        var context = new ScriptContext();
        var buildStep = new ScriptStep { Set = "arr = json([], 'one', 'two', 'three')" };
        var popStep = new ScriptStep { Set = "popped = json.pop(${arr})" };
        var shiftStep = new ScriptStep { Set = "shifted = json.shift(${arr})" };

        var buildResult = await _command.ExecuteAsync(buildStep, context, CancellationToken.None);
        var popResult = await _command.ExecuteAsync(popStep, context, CancellationToken.None);
        var shiftResult = await _command.ExecuteAsync(shiftStep, context, CancellationToken.None);

        buildResult.Success.Should().BeTrue();
        popResult.Success.Should().BeTrue();
        shiftResult.Success.Should().BeTrue();
        context.GetVariableString("popped").Should().Be("three");
        context.GetVariableString("shifted").Should().Be("one");

        var arr = GetArrayVariable(context, "arr");
        arr.Should().HaveCount(1);
        arr[0]!.GetValue<string>().Should().Be("two");
    }

    [Fact]
    public async Task ExecuteAsync_ArithmeticHonorsOperatorPrecedence()
    {
        var context = new ScriptContext();
        context.SetVariable("a", 2);
        context.SetVariable("b", 3);
        context.SetVariable("c", 4);

        var step = new ScriptStep { Set = "result = a + b * c" };
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("result").Should().Be(14d);
    }

    [Fact]
    public async Task ExecuteAsync_ArithmeticHonorsParentheses()
    {
        var context = new ScriptContext();
        context.SetVariable("a", 2);
        context.SetVariable("b", 3);
        context.SetVariable("c", 4);

        var step = new ScriptStep { Set = "result = (a + b) * c" };
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("result").Should().Be(20d);
    }

    [Fact]
    public async Task ExecuteAsync_StringFunctions_WorkAsExpected()
    {
        var context = new ScriptContext();

        (await _command.ExecuteAsync(new ScriptStep { Set = "replaced = replace('ssh-helper', 'helper', 'tool')" }, context, CancellationToken.None)).Success.Should().BeTrue();
        context.GetVariableString("replaced").Should().Be("ssh-tool");

        (await _command.ExecuteAsync(new ScriptStep { Set = "parts = split('c,b,a', ',')" }, context, CancellationToken.None)).Success.Should().BeTrue();
        var parts = context.GetVariable("parts").Should().BeOfType<List<string>>().Subject;
        parts.Should().Equal("c", "b", "a");

        (await _command.ExecuteAsync(new ScriptStep { Set = "joined = join(parts, '|')" }, context, CancellationToken.None)).Success.Should().BeTrue();
        context.GetVariableString("joined").Should().Be("c|b|a");

        (await _command.ExecuteAsync(new ScriptStep { Set = "slice = substring('abcdef', 2, 3)" }, context, CancellationToken.None)).Success.Should().BeTrue();
        context.GetVariableString("slice").Should().Be("cde");
    }

    [Fact]
    public async Task ExecuteAsync_TrimWithNestedJsonGetAndPlaceholders_ResolvesNestedFunctionValue()
    {
        var context = new ScriptContext();
        context.SetVariable("admins", "{\"system\":{\"admin\":{\"admin\":{\"trusthost1\":\" 10.0.0.0 255.255.255.0 \"}}}}");
        context.SetVariable("admin_name", "admin");
        context.SetVariable("i", 1);

        var step = new ScriptStep
        {
            Set = "th = trim(json.get(admins, \"system.admin.${admin_name}.trusthost${i}\", \"\"))"
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("th").Should().Be("10.0.0.0 255.255.255.0");
    }

    [Fact]
    public async Task ExecuteAsync_NestedStringFunctions_EvaluatesRecursively()
    {
        var context = new ScriptContext();
        var step = new ScriptStep
        {
            Set = "value = trim(upper('  mixedCase  '))"
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("value").Should().Be("MIXEDCASE");
    }

    [Fact]
    public async Task ExecuteAsync_JsonConstructor_ValueSupportsNestedNonJsonFunctions()
    {
        var context = new ScriptContext();
        context.SetVariable("admins", "{\"system\":{\"admin\":{\"admin\":{\"trusthost1\":\" 10.0.0.0 255.255.255.0 \"}}}}");
        context.SetVariable("admin_name", "admin");
        context.SetVariable("i", 1);
        var step = new ScriptStep
        {
            Set = "row = json('trusthost', trim(json.get(admins, \"system.admin.${admin_name}.trusthost${i}\", \"\")))"
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var rowJson = context.GetVariableString("row");
        var node = JsonNode.Parse(rowJson);
        node.Should().NotBeNull();
        node!["trusthost"]!.GetValue<string>().Should().Be("10.0.0.0 255.255.255.0");
    }

    [Fact]
    public async Task ExecuteAsync_SortFunction_SortsAscendingAndDescending()
    {
        var context = new ScriptContext();

        (await _command.ExecuteAsync(new ScriptStep { Set = "arr = split('b,a,c', ',')" }, context, CancellationToken.None)).Success.Should().BeTrue();
        (await _command.ExecuteAsync(new ScriptStep { Set = "asc = sort(arr)" }, context, CancellationToken.None)).Success.Should().BeTrue();
        (await _command.ExecuteAsync(new ScriptStep { Set = "desc = sort(arr, 'desc')" }, context, CancellationToken.None)).Success.Should().BeTrue();

        var asc = context.GetVariable("asc").Should().BeOfType<List<string>>().Subject;
        var desc = context.GetVariable("desc").Should().BeOfType<List<string>>().Subject;
        asc.Should().Equal("a", "b", "c");
        desc.Should().Equal("c", "b", "a");
    }

    private static JsonArray GetArrayVariable(ScriptContext context, string variableName)
    {
        var value = context.GetVariable(variableName);
        value.Should().NotBeNull();
        if (value is JsonArray jsonArray)
            return jsonArray;

        var parsed = JsonNode.Parse(value!.ToString() ?? string.Empty);
        parsed.Should().NotBeNull();
        return parsed!.AsArray();
    }
}
