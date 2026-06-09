using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class IterationStackEventTests
{
    private static async Task<List<StepExecutionEventArgs>> RunAndCaptureAll(
        Script script, ScriptContext? context = null)
    {
        var executor = new ScriptExecutor();
        var events = new List<StepExecutionEventArgs>();
        executor.StepCompleted += (_, e) => { lock (events) events.Add(e); };
        await executor.ExecuteAsync(script, context ?? new ScriptContext());
        return events;
    }

    [Fact]
    public async Task TopLevelStep_HasEmptyIterationStack()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep> { new() { Set = "x = 1" } }
        };

        var events = await RunAndCaptureAll(script);

        events.Should().HaveCount(1);
        (events[0].IterationStack ?? new List<IterationFrame>()).Should().BeEmpty();
    }
}
