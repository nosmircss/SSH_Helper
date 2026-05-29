using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptExecutorWhenGuardTests
{
    [Fact]
    public async Task ExecuteAsync_StepWithFalseWhen_IsSkipped()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var completed = new List<StepExecutionEventArgs>();
        executor.StepCompleted += (_, e) => completed.Add(e);

        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "ran = yes", When = "1 == 2" }
            }
        };

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.HasVariable("ran").Should().BeFalse();
        completed.Should().ContainSingle(e => e.Skipped);
    }

    [Fact]
    public async Task ExecuteAsync_StepWithTrueWhen_Runs()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();

        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "ran = yes", When = "1 == 1" }
            }
        };

        await executor.ExecuteAsync(script, context);

        context.GetVariableString("ran").Should().Be("yes");
    }

    [Fact]
    public async Task ExecuteAsync_ForeachWhen_StillFiltersPerItem()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        context.SetVariable("items", new List<string> { "a", "b", "c" });

        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    When = "{{x_index}} != 1",
                    Do = new List<ScriptStep> { new() { Set = "last = {{x}}" } }
                }
            }
        };

        await executor.ExecuteAsync(script, context);

        // Body ran for a (idx 0) and c (idx 2); b (idx 1) filtered. Foreach was NOT skipped wholesale.
        context.GetVariableString("last").Should().Be("c");
    }
}
