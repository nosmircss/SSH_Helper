// SSH_Helper.Tests/Services/FlowCanvasBridgeLayoutPersistenceTests.cs
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
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
