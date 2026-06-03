using System.Linq;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

/// <summary>
/// Issue #45 (import edge wiring): a container nested inside another container's branch must
/// continue to its next sibling from the container's own 'continue' handle — exactly like a
/// top-level container — leaving its branch body terminal. Previously the next sibling was wired
/// from the nested container's branch end, so the body (e.g. an inner if's `then`) visually flowed
/// into the following step instead of ending.
/// </summary>
public class FlowCanvasBridgeNestedEdgeWiringTests
{
    private const string Yaml = """
        steps:
        - foreach: x in items
          do:
            - send:
                command: before
            - if:
                condition: port != "514"
                then:
                  - send:
                      command: y
            - send:
                command: after
        """;

    [Fact]
    public void NestedContainer_ContinuesFromContainerHandle_AndBodyIsTerminal()
    {
        var bridge = new FlowCanvasBridge();
        var (nodes, edges) = bridge.TextToGraph(Yaml);

        string IdByStepPath(string sp) => nodes.OfType<JObject>()
            .First(n => (string?)n["data"]?["props"]?["_stepPath"] == sp)["id"]!.ToString();

        var ifPort = IdByStepPath("steps/0/do/1");          // the nested if
        var body = IdByStepPath("steps/0/do/1/then/0");     // send y (the if's only then child)
        var after = IdByStepPath("steps/0/do/2");           // the sibling AFTER the nested if

        bool EdgeExists(string src, string tgt, string? handle = null) => edges.OfType<JObject>().Any(e =>
            (string?)e["source"] == src && (string?)e["target"] == tgt &&
            (handle == null || (string?)e["sourceHandle"] == handle));

        // The if's then-body is wired from the if (dashed branch edge).
        Assert.True(EdgeExists(ifPort, body), "if → then-body edge missing");

        // The continuation goes from the if's own 'continue' handle to the next sibling...
        Assert.True(EdgeExists(ifPort, after, "continue"), "if → next-sibling 'continue' edge missing");

        // ...and the body (send y) is terminal: it must NOT flow into the next sibling.
        Assert.False(EdgeExists(body, after), "body should be terminal, but it connects to the next sibling");
        Assert.DoesNotContain(edges.OfType<JObject>(), e => (string?)e["source"] == body);
    }
}
