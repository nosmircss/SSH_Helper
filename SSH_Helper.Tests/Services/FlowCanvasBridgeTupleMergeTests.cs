using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class FlowCanvasBridgeTupleMergeTests
{
    private static JObject Block(string id, string stepPath, string blockType, double x = 0, double y = 0) => new()
    {
        ["id"] = id,
        ["type"] = "block",
        ["position"] = new JObject { ["x"] = x, ["y"] = y },
        ["data"] = new JObject
        {
            ["blockType"] = blockType,
            ["props"] = new JObject { ["_stepPath"] = stepPath },
        },
    };

    private static CanvasLayoutData LayoutWith(params (string id, string sp, string bt, double x, double y)[] entries)
    {
        var l = new CanvasLayoutData();
        foreach (var e in entries)
            l.Positions[e.id] = new NodePosition { X = e.x, Y = e.y, StepPath = e.sp, BlockType = e.bt };
        return l;
    }

    [Fact]
    public void Append_at_end_is_safe_keeps_existing_returns_new_id()
    {
        var nodes = new JArray { Block("node-0", "steps/0", "send"), Block("node-1", "steps/1", "print"), Block("node-2", "steps/2", "print") };
        var layout = LayoutWith(("node-0", "steps/0", "send", 100, 200), ("node-1", "steps/1", "print", 150, 400));

        var (safe, newIds) = FlowCanvasBridge.TryMergeLayoutByTuple(nodes, layout);

        safe.Should().BeTrue();
        newIds.Should().BeEquivalentTo(new[] { "node-2" });
        nodes[0]["position"]!["x"]!.Value<double>().Should().Be(100);
        nodes[1]["position"]!["y"]!.Value<double>().Should().Be(400);
    }

    [Fact]
    public void Mid_body_insert_is_unsafe()
    {
        var nodes = new JArray
        {
            Block("node-0", "steps/0", "send"),
            Block("node-1", "steps/1", "wait"),
            Block("node-2", "steps/2", "print"),
            Block("node-3", "steps/3", "print"),
        };
        var layout = LayoutWith(
            ("node-0", "steps/0", "send", 1, 1),
            ("node-1", "steps/1", "print", 2, 2),
            ("node-2", "steps/2", "print", 3, 3));

        var (safe, _) = FlowCanvasBridge.TryMergeLayoutByTuple(nodes, layout);

        safe.Should().BeFalse("a shifted step path breaks the saved-tuple subset; clean reflow instead of mis-mapping");
    }

    [Fact]
    public void Pure_move_no_structural_change_is_safe_no_new_ids()
    {
        var nodes = new JArray { Block("node-0", "steps/0", "send"), Block("node-1", "steps/1", "print") };
        var layout = LayoutWith(("node-0", "steps/0", "send", 10, 10), ("node-1", "steps/1", "print", 20, 20));

        var (safe, newIds) = FlowCanvasBridge.TryMergeLayoutByTuple(nodes, layout);

        safe.Should().BeTrue();
        newIds.Should().BeEmpty();
        nodes[1]["position"]!["x"]!.Value<double>().Should().Be(20);
    }

    [Fact]
    public void PreMigration_layout_without_stepPath_is_unsafe()
    {
        var nodes = new JArray { Block("node-0", "steps/0", "send") };
        var layout = new CanvasLayoutData();
        layout.Positions["node-0"] = new NodePosition { X = 5, Y = 5 };

        var (safe, _) = FlowCanvasBridge.TryMergeLayoutByTuple(nodes, layout);

        safe.Should().BeFalse();
    }
}
