using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptContextTests
{
    [Fact]
    public void SubstituteVariables_DynamicArrayIndexUsingNestedInterpolation_ResolvesItem()
    {
        var context = new ScriptContext();
        context.SetVariable("fruits", new List<string> { "apple", "banana", "cherry" });
        context.SetVariable("idx", 1);

        var result = context.SubstituteVariables("Value=${fruits[${idx}]}");

        result.Should().Be("Value=banana");
    }

    [Fact]
    public void SubstituteVariables_DynamicArrayIndexUsingIndexVariable_ResolvesItem()
    {
        var context = new ScriptContext();
        context.SetVariable("fruits", new List<string> { "apple", "banana", "cherry" });
        context.SetVariable("idx", 2);

        var result = context.SubstituteVariables("Value=${fruits[idx]}");

        result.Should().Be("Value=cherry");
    }

    [Fact]
    public void SubstituteVariables_DoubleBraceExpression_ResolvesItem()
    {
        var context = new ScriptContext();
        context.SetVariable("Host_IP", "10.0.0.5");

        var result = context.SubstituteVariables("Host={{Host_IP}}");

        result.Should().Be("Host=10.0.0.5");
    }

    [Fact]
    public void SubstituteVariables_JsonArrayStringLength_UsesElementCount()
    {
        var context = new ScriptContext();
        context.SetVariable("services", "[\"Cloudflare-CDN\",\"Amazon-AWS\"]");

        var result = context.SubstituteVariables("Count=${services.length}");

        result.Should().Be("Count=2");
    }

    [Fact]
    public void SubstituteVariables_JsonArrayStringIndex_ResolvesItem()
    {
        var context = new ScriptContext();
        context.SetVariable("services", "[\"Cloudflare-CDN\",\"Amazon-AWS\"]");

        var result = context.SubstituteVariables("Service=${services[1]}");

        result.Should().Be("Service=Amazon-AWS");
    }

    [Fact]
    public void TimestampVariable_IsAvailableThroughHasVariable()
    {
        var context = new ScriptContext();

        context.HasVariable("_timestamp").Should().BeTrue();
    }

    [Fact]
    public void TimestampVariable_ResolvesDynamically()
    {
        var context = new ScriptContext();

        var first = context.SubstituteVariables("${_timestamp}");
        Thread.Sleep(1100);
        var second = context.SubstituteVariables("${_timestamp}");

        second.Should().NotBe(first);
    }

    [Fact]
    public void InteractiveSessions_AddAndSnapshot_PreservesOrderAndNumbering()
    {
        var context = new ScriptContext();

        context.AddInteractiveSession(new InteractiveTerminalSessionDetails
        {
            HostAddress = "10.0.0.1",
            SessionMode = "separate",
            EmulationMode = "full",
            StartedAtUtc = new DateTime(2026, 2, 14, 10, 0, 0, DateTimeKind.Utc),
            EndedAtUtc = new DateTime(2026, 2, 14, 10, 5, 0, DateTimeKind.Utc),
            CloseReason = "user_closed",
            Completed = true,
            Transcript = "first"
        });
        context.AddInteractiveSession(new InteractiveTerminalSessionDetails
        {
            SessionNumber = 7,
            HostAddress = "10.0.0.2",
            SessionMode = "shared",
            EmulationMode = "full",
            StartedAtUtc = new DateTime(2026, 2, 14, 10, 6, 0, DateTimeKind.Utc),
            EndedAtUtc = new DateTime(2026, 2, 14, 10, 7, 0, DateTimeKind.Utc),
            CloseReason = "disconnected",
            Completed = true,
            Transcript = "second"
        });

        var snapshot = context.GetInteractiveSessionsSnapshot();

        snapshot.Should().HaveCount(2);
        snapshot[0].SessionNumber.Should().Be(1);
        snapshot[0].Transcript.Should().Be("first");
        snapshot[1].SessionNumber.Should().Be(7);
        snapshot[1].Transcript.Should().Be("second");

        snapshot[0].Transcript = "mutated";
        context.GetInteractiveSessionsSnapshot()[0].Transcript.Should().Be("first");
    }

    [Fact]
    public async Task EmitOutput_InvokesSubscribersOutsideStateLock()
    {
        var context = new ScriptContext();
        using var handlerStarted = new ManualResetEventSlim(false);
        using var allowHandlerFinish = new ManualResetEventSlim(false);

        context.OutputReceived += (_, _) =>
        {
            handlerStarted.Set();
            allowHandlerFinish.Wait(TimeSpan.FromSeconds(2));
        };

        var emitTask = Task.Run(() => context.EmitOutput("line-1"));
        handlerStarted.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();

        var setTask = Task.Run(() => context.SetVariable("k", "v"));
        var setCompleted = await Task.WhenAny(setTask, Task.Delay(500)) == setTask;

        try
        {
            setCompleted.Should().BeTrue("EmitOutput should not hold _stateLock while invoking subscribers");
        }
        finally
        {
            allowHandlerFinish.Set();
            await emitTask;
        }
    }
}
