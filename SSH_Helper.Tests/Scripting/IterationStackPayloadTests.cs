using System.Collections.Generic;
using FluentAssertions;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class IterationStackPayloadTests
{
    private static readonly Dictionary<string, string> Map = new(System.StringComparer.Ordinal)
    {
        ["steps/0"] = "node-F",
        ["steps/0/do/1"] = "node-W",
    };

    [Fact]
    public void NullOrEmptyStack_ReturnsNull()
    {
        FlowCanvasBridge.BuildIterationStackPayload(null, Map).Should().BeNull();
        FlowCanvasBridge.BuildIterationStackPayload(new List<IterationFrame>(), Map).Should().BeNull();
    }

    [Fact]
    public void ResolvableFrames_MapToLoopNodeIds_InOrder()
    {
        var stack = new List<IterationFrame>
        {
            new("steps/0", 2, "web-02"),
            new("steps/0/do/1", 4, null),
        };

        var payload = FlowCanvasBridge.BuildIterationStackPayload(stack, Map);

        payload.Should().HaveCount(2);
        payload![0]["loopId"].Should().Be("node-F");
        payload[0]["i"].Should().Be(2);
        payload[0]["label"].Should().Be("web-02");
        payload[1]["loopId"].Should().Be("node-W");
        payload[1]["i"].Should().Be(4);
        payload[1]["label"].Should().BeNull();
    }

    [Fact]
    public void UnresolvableOrNotStartedFrames_AreSkipped_Gracefully()
    {
        var stack = new List<IterationFrame>
        {
            new("steps/0", 1, "a"),
            new("subroutines/x/steps/2", 0, null), // no canvas node — skipped
            new("steps/0/do/1", -1, null),         // pushed, no iteration yet — skipped
            new("", 3, null),                      // no path — skipped
        };

        var payload = FlowCanvasBridge.BuildIterationStackPayload(stack, Map);

        payload.Should().HaveCount(1);
        payload![0]["loopId"].Should().Be("node-F");
    }

    [Fact]
    public void NullMap_OrNothingResolvable_ReturnsNull()
    {
        var stack = new List<IterationFrame> { new("steps/9", 0, null) };
        FlowCanvasBridge.BuildIterationStackPayload(stack, null).Should().BeNull();
        FlowCanvasBridge.BuildIterationStackPayload(stack, Map).Should().BeNull();
    }
}
