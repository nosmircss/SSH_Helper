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

    [Fact]
    public async Task While_ReportsIterationCount()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "n = 0" },
                new()
                {
                    While = "n < 3",
                    Do = new List<ScriptStep> { new() { Set = "n = n + 1" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/1"].IterationCount.Should().Be(3);
    }

    [Fact]
    public async Task Repeat_ReportsIterationCount()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "n = 0" },
                new()
                {
                    Until = "n >= 3",
                    Do = new List<ScriptStep> { new() { Set = "n = n + 1" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/1"].IterationCount.Should().Be(3);
    }

    [Fact]
    public async Task If_Then_ReportsThen()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    If = "1 == 1",
                    Then = new List<ScriptStep> { new() { Set = "x = 1" } },
                    Else = new List<ScriptStep> { new() { Set = "x = 2" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().Be("then");
    }

    [Fact]
    public async Task If_Else_ReportsElse()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    If = "1 == 0",
                    Then = new List<ScriptStep> { new() { Set = "x = 1" } },
                    Else = new List<ScriptStep> { new() { Set = "x = 2" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().Be("else");
    }

    [Fact]
    public async Task If_Elif_ReportsElifKey()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    If = "1 == 0",
                    Then = new List<ScriptStep> { new() { Set = "x = 1" } },
                    Elif = new List<ElifBranch>
                    {
                        new() { If = "1 == 1", Then = new List<ScriptStep> { new() { Set = "x = 3" } } }
                    }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().Be("elif/0/then");
    }

    [Fact]
    public async Task If_NoBranch_ReportsNull()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    If = "1 == 0",
                    Then = new List<ScriptStep> { new() { Set = "x = 1" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().BeNull();
    }

    [Fact]
    public async Task Switch_Case_ReportsCaseKey()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Switch = "alpha",
                    Cases = new List<SwitchCase>
                    {
                        new() { Value = "beta", Do = new List<ScriptStep> { new() { Set = "x = 1" } } },
                        new() { Value = "alpha", Do = new List<ScriptStep> { new() { Set = "x = 2" } } }
                    }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().Be("cases/1/do");
    }

    [Fact]
    public async Task Switch_Default_ReportsDefault()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Switch = "zzz",
                    Cases = new List<SwitchCase>
                    {
                        new() { Value = "alpha", Do = new List<ScriptStep> { new() { Set = "x = 1" } } }
                    },
                    Else = new List<ScriptStep> { new() { Set = "x = 9" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().Be("default");
    }

    [Fact]
    public async Task Switch_NoMatch_ReportsNull()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Switch = "zzz",
                    Cases = new List<SwitchCase>
                    {
                        new() { Value = "alpha", Do = new List<ScriptStep> { new() { Set = "x = 1" } } }
                    }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().BeNull();
    }
}
