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

    [Fact]
    public async Task Foreach_TagsNestedEvents_PerIteration_WithItemLabels()
    {
        var context = new ScriptContext();
        context.SetVariable("items", "[\"alpha\",\"beta\",\"gamma\"]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    StepPath = "steps/0",
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "last = x", StepPath = "steps/0/do/0" }
                    }
                }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        var bodyEvents = events.Where(e => e.StepPath == "steps/0/do/0").ToList();
        bodyEvents.Should().HaveCount(3);
        for (int i = 0; i < 3; i++)
        {
            bodyEvents[i].IterationStack.Should().NotBeNull();
            bodyEvents[i].IterationStack!.Should().HaveCount(1);
            bodyEvents[i].IterationStack![0].LoopStepPath.Should().Be("steps/0");
            bodyEvents[i].IterationStack![0].Index.Should().Be(i);
        }
        bodyEvents.Select(e => e.IterationStack![0].Label)
            .Should().Equal("alpha", "beta", "gamma");

        // The loop's own completion fires AFTER its frame pops → ancestors only (none here).
        var loopEvent = events.Single(e => e.StepPath == "steps/0");
        (loopEvent.IterationStack ?? new List<IterationFrame>()).Should().BeEmpty();
    }

    [Fact]
    public async Task Foreach_DictForm_UsesKeyAsLabel()
    {
        var context = new ScriptContext();
        context.SetVariable("map", "{\"k1\":\"v1\",\"k2\":\"v2\"}");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "key, value in map",
                    StepPath = "steps/0",
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "last = value", StepPath = "steps/0/do/0" }
                    }
                }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        events.Where(e => e.StepPath == "steps/0/do/0")
            .Select(e => e.IterationStack![0].Label)
            .Should().Equal("k1", "k2");
    }

    [Fact]
    public async Task Foreach_LongItemValue_IsTruncatedTo48Chars()
    {
        var longItem = new string('z', 60);
        var context = new ScriptContext();
        context.SetVariable("items", $"[\"{longItem}\"]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    StepPath = "steps/0",
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "last = x", StepPath = "steps/0/do/0" }
                    }
                }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        var label = events.Single(e => e.StepPath == "steps/0/do/0").IterationStack![0].Label;
        label.Should().HaveLength(48);
        label.Should().Be(new string('z', 47) + "…");
    }

    [Fact]
    public async Task Foreach_TruncationDoesNotSplitSurrogatePairs()
    {
        // 46 z's then an emoji (surrogate pair at positions 46-47): a naive cut at 47
        // would strand the high surrogate. The guard trims one extra char instead.
        var item = new string('z', 46) + "\U0001F600" + new string('z', 10);
        var context = new ScriptContext();
        context.SetVariable("items", $"[\"{item}\"]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    StepPath = "steps/0",
                    Do = new List<ScriptStep> { new() { Set = "last = x", StepPath = "steps/0/do/0" } }
                }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        var label = events.Single(e => e.StepPath == "steps/0/do/0").IterationStack![0].Label!;
        char.IsHighSurrogate(label[label.Length - 2]).Should().BeFalse(); // char before '…' is not a stranded high surrogate
        label.Should().EndWith("…");
    }

    [Fact]
    public async Task Foreach_StackIsPopped_AfterBreak()
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
                    StepPath = "steps/0",
                    Do = new List<ScriptStep>
                    {
                        new() { BreakLoop = true, StepPath = "steps/0/do/0" }
                    }
                },
                new() { Set = "after = 1", StepPath = "steps/1" }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        // Only iteration 0 ran; the trailing top-level step must carry an EMPTY stack.
        events.Where(e => e.StepPath == "steps/0/do/0").Should().HaveCount(1);
        var after = events.Single(e => e.StepPath == "steps/1");
        (after.IterationStack ?? new List<IterationFrame>()).Should().BeEmpty();
    }
}
