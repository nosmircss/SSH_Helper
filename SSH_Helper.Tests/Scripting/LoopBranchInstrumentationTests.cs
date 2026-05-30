using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class LoopBranchInstrumentationTests
{
    // Runs a script and returns the last StepCompleted event arg seen for each canonical StepPath.
    // Asserting via the event arg proves both the command set result.X AND the executor copied it
    // onto StepExecutionEventArgs (the exact data the Flow Canvas consumes).
    private static async Task<Dictionary<string, StepExecutionEventArgs>> RunAndCapture(
        Script script, ScriptContext? context = null)
    {
        var executor = new ScriptExecutor();
        var completed = new Dictionary<string, StepExecutionEventArgs>();
        executor.StepCompleted += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.StepPath))
                completed[e.StepPath!] = e;
        };
        await executor.ExecuteAsync(script, context ?? new ScriptContext());
        return completed;
    }

    [Fact]
    public async Task Foreach_ReportsBodyExecutionCount()
    {
        var context = new ScriptContext();
        context.SetVariable("items", "[\"a\",\"b\",\"c\"]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    Do = new List<ScriptStep> { new() { Set = "last = x" } }
                }
            }
        };

        var completed = await RunAndCapture(script, context);

        completed["steps/0"].IterationCount.Should().Be(3);
    }

    [Fact]
    public async Task Foreach_EmptyCollection_ReportsZero()
    {
        var context = new ScriptContext();
        context.SetVariable("items", "[]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    Do = new List<ScriptStep> { new() { Set = "last = x" } }
                }
            }
        };

        var completed = await RunAndCapture(script, context);

        completed["steps/0"].IterationCount.Should().Be(0);
    }

    [Fact]
    public async Task Foreach_Break_ReportsExecutedCount()
    {
        var context = new ScriptContext();
        context.SetVariable("items", "[\"a\",\"b\",\"c\"]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "last = x" },
                        new() { BreakLoop = true }
                    }
                }
            }
        };

        var completed = await RunAndCapture(script, context);

        completed["steps/0"].IterationCount.Should().Be(1); // body ran once, then broke
    }
}
