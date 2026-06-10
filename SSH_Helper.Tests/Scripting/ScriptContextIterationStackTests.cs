using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptContextIterationStackTests
{
    [Fact]
    public void IterationStack_IsEmptyByDefault()
    {
        var context = new ScriptContext();
        context.IterationStack.Should().BeEmpty();
    }

    [Fact]
    public void PushSetPop_RoundTrips()
    {
        var context = new ScriptContext();

        context.PushIterationFrame("steps/2", -1);
        context.SetCurrentIterationFrame(0, "web-01");

        context.IterationStack.Should().HaveCount(1);
        context.IterationStack[0].Should().Be(new IterationFrame("steps/2", 0, "web-01"));

        context.PushIterationFrame("steps/2/do/1", -1);
        context.SetCurrentIterationFrame(4);
        context.IterationStack.Should().HaveCount(2);
        context.IterationStack[1].Should().Be(new IterationFrame("steps/2/do/1", 4, null));

        context.PopIterationFrame();
        context.IterationStack.Should().HaveCount(1);
        context.IterationStack[0].LoopStepPath.Should().Be("steps/2");

        context.PopIterationFrame();
        context.IterationStack.Should().BeEmpty();

        // Popping an empty stack must be a no-op, not a throw.
        context.PopIterationFrame();
        context.IterationStack.Should().BeEmpty();
    }

    [Fact]
    public void Snapshots_AreImmutable_AcrossSetCurrentIterationFrame()
    {
        var context = new ScriptContext();
        context.PushIterationFrame("steps/0", -1);
        context.SetCurrentIterationFrame(0, "a");

        var snapshot = context.IterationStack;

        context.SetCurrentIterationFrame(1, "b");

        snapshot[0].Index.Should().Be(0);
        snapshot[0].Label.Should().Be("a");
        context.IterationStack[0].Index.Should().Be(1);
    }

    [Fact]
    public async Task Stack_IsIsolated_AcrossParallelTasks()
    {
        // Mirrors ParallelCommand: one shared context, arms on Task.Run. AsyncLocal must
        // keep each arm's pushes invisible to the other.
        var context = new ScriptContext();

        var t1 = Task.Run(async () =>
        {
            context.PushIterationFrame("steps/0/parallel/0", -1);
            context.SetCurrentIterationFrame(0);
            await Task.Delay(50);
            return context.IterationStack.Select(f => f.LoopStepPath).ToArray();
        });
        var t2 = Task.Run(async () =>
        {
            context.PushIterationFrame("steps/0/parallel/1", -1);
            context.SetCurrentIterationFrame(0);
            await Task.Delay(50);
            return context.IterationStack.Select(f => f.LoopStepPath).ToArray();
        });

        var results = await Task.WhenAll(t1, t2);

        results[0].Should().Equal("steps/0/parallel/0");
        results[1].Should().Equal("steps/0/parallel/1");
        context.IterationStack.Should().BeEmpty(); // parent never saw either push
    }
}
