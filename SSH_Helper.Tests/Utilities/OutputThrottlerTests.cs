using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class OutputThrottlerTests
{
    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            d(state);
        }
    }

    [Fact]
    public void EnqueueThenFlush_EmitsBufferedOutput()
    {
        var outputs = new List<string>();
        using var throttler = new OutputThrottler(
            TimeSpan.FromMinutes(5),
            outputs.Add,
            new ImmediateSynchronizationContext());

        throttler.Enqueue("hello");
        throttler.Enqueue(" ");
        throttler.Enqueue("world");
        throttler.Flush();

        outputs.Should().ContainSingle();
        outputs[0].Should().Be("hello world");
    }

    [Fact]
    public void Clear_DropsPendingOutput()
    {
        var outputs = new List<string>();
        using var throttler = new OutputThrottler(
            TimeSpan.FromMinutes(5),
            outputs.Add,
            new ImmediateSynchronizationContext());

        throttler.Enqueue("pending");
        throttler.Clear();
        throttler.Flush();

        outputs.Should().BeEmpty();
    }
}
