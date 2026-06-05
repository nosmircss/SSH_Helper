// SSH_Helper.Tests/Services/FlowCanvasBridgeLayoutPersistenceTests.cs
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class FlowCanvasBridgeLayoutPersistenceTests
{
    private static double Const(string name) =>
        (double)typeof(FlowCanvasBridge).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Fact]
    public void ChildNodeMaxWidth_matches_typescript()
    {
        Const("ChildNodeMaxWidth").Should().Be(300);
        Const("MinColumnWidth").Should().Be(330);
    }

    [Fact]
    public void CanvasLayoutData_clones_expanded_ids()
    {
        var data = new SSH_Helper.Models.CanvasLayoutData();
        data.ExpandedNodeIds.Add("node-3");
        var clone = data.Clone();
        clone.ExpandedNodeIds.Should().ContainSingle().Which.Should().Be("node-3");
        clone.ExpandedNodeIds.Add("node-4"); // independence
        data.ExpandedNodeIds.Should().HaveCount(1);
    }

    // -------------------------------------------------------------------------
    // R3: kind + anchor survive ExtractLayout -> MergeLayout; no duplicate ids
    // -------------------------------------------------------------------------

    /// <summary>
    /// Exercises the real layout side-channel boundary:
    ///   ExtractLayout (from flat comments[] array, the shape React sends)
    ///   -> CanvasComment.Clone (persistence round-trip)
    ///   -> MergeLayout (back into a nodes JArray)
    /// Asserts:
    ///   1. kind:'comment' and anchor fields are preserved end-to-end.
    ///   2. When the TextToGraph-emitted node already has the same id, MergeLayout
    ///      reconciles (updates position) instead of appending a duplicate.
    /// </summary>
    [Fact]
    public void MergeLayout_preserves_kind_and_anchor_and_deduplicates_comment_nodes()
    {
        // Arrange — flat comments[] array exactly as React buildExecutableGraphPayload emits
        var flatComments = new JArray
        {
            new JObject
            {
                ["id"] = "comment-c0",
                ["text"] = "Review this step",
                ["color"] = "#e0c040",
                ["x"] = 10.0,
                ["y"] = 20.0,
                ["width"] = 220.0,
                ["height"] = 110.0,
                ["attachedToNodeId"] = "node-0",
                ["kind"] = "comment",
                ["anchor"] = new JObject
                {
                    ["type"] = "leading",
                    ["stepPath"] = "steps/0",
                    ["lineOffset"] = 2,
                },
            },
        };

        // Act — ExtractLayout reads the flat comments[] (second arg)
        var layout = FlowCanvasBridge.ExtractLayout(
            nodes: new JArray(),          // no executable nodes needed for this sub-test
            commentNodes: flatComments,
            disabledBlockIds: null);

        // Assert — kind and anchor survived ExtractLayout
        layout.Comments.Should().ContainSingle();
        var saved = layout.Comments[0];
        saved.Kind.Should().Be("comment");
        saved.Anchor.Should().NotBeNull();
        saved.Anchor!.Type.Should().Be("leading");
        saved.Anchor.StepPath.Should().Be("steps/0");
        saved.Anchor.LineOffset.Should().Be(2);

        // Assert — Clone preserves them too (persistence path)
        var cloned = layout.Clone();
        var clonedAnchor = cloned.Comments[0].Anchor;
        cloned.Comments[0].Kind.Should().Be("comment");
        clonedAnchor.Should().NotBeNull();
        clonedAnchor!.Type.Should().Be("leading");
        clonedAnchor.StepPath.Should().Be("steps/0");
        clonedAnchor.LineOffset.Should().Be(2);

        // Arrange — TextToGraph already emitted a node with the same id (the comment node
        // that BuildCommentNode produces on import). MergeLayout must NOT append a duplicate.
        var nodes = new JArray
        {
            new JObject
            {
                ["id"] = "comment-c0",
                ["type"] = "comment",
                ["position"] = new JObject { ["x"] = 0.0, ["y"] = 0.0 },
                ["style"] = new JObject { ["width"] = 200.0, ["height"] = 100.0 },
                ["data"] = new JObject
                {
                    ["commentId"] = "comment-c0",
                    ["text"] = "Review this step",
                    ["color"] = "#e0c040",
                    ["kind"] = "comment",
                    ["anchor"] = new JObject { ["type"] = "leading", ["stepPath"] = "steps/0", ["lineOffset"] = 2 },
                },
            },
        };

        // Act — MergeLayout with saved layout that has the same comment id
        FlowCanvasBridge.MergeLayout(nodes, layout);

        // Assert — no duplicate: exactly one node with id comment-c0
        var commentNodes = nodes.Where(n => n["id"]?.ToString() == "comment-c0").ToList();
        commentNodes.Should().ContainSingle("MergeLayout must reconcile, not append a duplicate");

        // Assert — position was updated from saved layout
        var merged = commentNodes[0];
        merged["position"]!["x"]!.Value<double>().Should().Be(10.0);
        merged["position"]!["y"]!.Value<double>().Should().Be(20.0);

        // Assert — kind and anchor are present in the merged node data
        var mergedData = merged["data"] as JObject;
        mergedData.Should().NotBeNull();
        mergedData!["kind"]?.ToString().Should().Be("comment");
        var mergedAnchor = mergedData["anchor"] as JObject;
        mergedAnchor.Should().NotBeNull();
        mergedAnchor!["type"]?.ToString().Should().Be("leading");
        mergedAnchor["stepPath"]?.ToString().Should().Be("steps/0");
        mergedAnchor["lineOffset"]?.Value<int>().Should().Be(2);
    }

    /// <summary>
    /// When the saved comment id does NOT exist among the current nodes (e.g. a purely
    /// canvas-authored sticky note that TextToGraph never emits), MergeLayout appends it.
    /// </summary>
    [Fact]
    public void MergeLayout_appends_new_comment_node_when_id_not_present()
    {
        var flatComments = new JArray
        {
            new JObject
            {
                ["id"] = "comment-c1",
                ["text"] = "Canvas note",
                ["color"] = "#e0c040",
                ["x"] = 5.0,
                ["y"] = 15.0,
                ["width"] = 200.0,
                ["height"] = 100.0,
                ["kind"] = "sticky",
            },
        };

        var layout = FlowCanvasBridge.ExtractLayout(
            nodes: new JArray(),
            commentNodes: flatComments,
            disabledBlockIds: null);

        // Nodes array has no comment-c1 yet
        var nodes = new JArray();
        FlowCanvasBridge.MergeLayout(nodes, layout);

        nodes.Should().ContainSingle();
        var appended = (JObject)nodes[0];
        appended["id"]?.ToString().Should().Be("comment-c1");
        var data = appended["data"] as JObject;
        data.Should().NotBeNull();
        data!["kind"]?.ToString().Should().Be("sticky");
    }

    [Fact]
    public void Export_ignores_expanded_flag()
    {
        JArray BuildGraph(bool expanded)
        {
            var print = new JObject
            {
                ["id"] = "node-0",
                ["type"] = "block",
                ["position"] = new JObject { ["x"] = 0, ["y"] = 0 },
                ["data"] = new JObject
                {
                    ["blockType"] = "print",
                    ["label"] = "Print",
                    ["props"] = new JObject { ["message"] = "hi" },
                },
            };
            if (expanded) ((JObject)print["data"]!)["expanded"] = true;
            return new JArray
            {
                new JObject
                {
                    ["id"] = "__start__", ["type"] = "start",
                    ["position"] = new JObject { ["x"] = 0, ["y"] = 0 },
                    ["data"] = new JObject { ["blockType"] = "_start", ["props"] = new JObject() },
                },
                print,
            };
        }
        var edges = new JArray
        {
            new JObject { ["id"] = "e0", ["source"] = "__start__", ["target"] = "node-0" },
        };
        var bridge = new SSH_Helper.Services.FlowCanvasBridge();
        var withExpanded = bridge.ExportGraphToYaml(new JObject { ["nodes"] = BuildGraph(true), ["edges"] = edges });
        var without = bridge.ExportGraphToYaml(new JObject { ["nodes"] = BuildGraph(false), ["edges"] = edges });
        without.Yaml.Should().NotBeNullOrWhiteSpace();           // sanity: baseline actually exports something
        withExpanded.Yaml.Should().Be(without.Yaml);             // expanded flag changes nothing
    }
}
