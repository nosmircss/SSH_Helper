using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptExecutorLoopScopingTests
{
    private static Script ForeachScript(string foreachExpr, params ScriptStep[] body)
        => new()
        {
            Steps = new List<ScriptStep>
            {
                new() { Foreach = foreachExpr, Do = new List<ScriptStep>(body) }
            }
        };

    [Fact]
    public async Task Foreach_RestoresOuterVariableWithSameNameAsIterator()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        context.SetVariable("x", "outer");
        context.SetVariable("items", new List<string> { "a", "b" });

        await executor.ExecuteAsync(ForeachScript("x in items", new ScriptStep { Set = "last = {{x}}" }), context);

        context.GetVariableString("last").Should().Be("b");
        context.GetVariableString("x").Should().Be("outer"); // restored, not clobbered
    }

    [Fact]
    public async Task Foreach_RemovesIteratorAndIndexWhenNoOuterValue()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        context.SetVariable("items", new List<string> { "a", "b" });

        await executor.ExecuteAsync(ForeachScript("y in items", new ScriptStep { Set = "last = {{y}}" }), context);

        context.HasVariable("y").Should().BeFalse();
        context.HasVariable("y_index").Should().BeFalse();
    }

    [Fact]
    public async Task Foreach_RestoresScopeOnBreak()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        context.SetVariable("x", "outer");
        context.SetVariable("items", new List<string> { "a", "b", "c" });

        await executor.ExecuteAsync(
            ForeachScript("x in items",
                new ScriptStep { Set = "seen = {{x}}" },
                new ScriptStep { BreakLoop = true }),
            context);

        context.GetVariableString("seen").Should().Be("a");
        context.GetVariableString("x").Should().Be("outer"); // restored despite early break
    }

    [Fact]
    public async Task Foreach_ExposesMetadataScalars()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        context.SetVariable("items", new List<string> { "a", "b", "c" });

        await executor.ExecuteAsync(
            ForeachScript("x in items",
                new ScriptStep { Set = "cap_number = {{x_number}}" },
                new ScriptStep { Set = "cap_count = {{x_count}}" },
                new ScriptStep { Set = "cap_last = {{x_last}}" }),
            context);

        context.GetVariableString("cap_number").Should().Be("3"); // 1-based, final iteration
        context.GetVariableString("cap_count").Should().Be("3");
        context.GetVariableString("cap_last").Should().Be("True");
    }

    [Fact]
    public async Task Foreach_FirstAndIndexOnSingleItem()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        context.SetVariable("items", new List<string> { "only" });

        await executor.ExecuteAsync(
            ForeachScript("x in items",
                new ScriptStep { Set = "cap_first = {{x_first}}" },
                new ScriptStep { Set = "cap_idx = {{x_index}}" }),
            context);

        context.GetVariableString("cap_first").Should().Be("True");
        context.GetVariableString("cap_idx").Should().Be("0");
    }

    [Fact]
    public async Task Foreach_DictIteration_SetsKeyAndValue()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        context.SetVariable("m", "{\"host\":\"server1\"}");

        await executor.ExecuteAsync(
            ForeachScript("key, val in m",
                new ScriptStep { Set = "ck = {{key}}" },
                new ScriptStep { Set = "cv = {{val}}" }),
            context);

        context.GetVariableString("ck").Should().Be("host");
        context.GetVariableString("cv").Should().Be("server1");
    }

    [Fact]
    public async Task Foreach_DictIteration_IteratesPastFirstEntry()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        context.SetVariable("m", "{\"k1\":\"v1\",\"k2\":\"v2\"}");

        await executor.ExecuteAsync(
            ForeachScript("key, val in m",
                new ScriptStep { Set = "ck = {{key}}" },
                new ScriptStep { Set = "cv = {{val}}" }),
            context);

        // ck/cv reflect the last entry, proving iteration reached the second pair
        context.GetVariableString("ck").Should().Be("k2");
        context.GetVariableString("cv").Should().Be("v2");
    }
}
