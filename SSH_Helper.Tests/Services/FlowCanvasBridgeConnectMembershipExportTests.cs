using System.Linq;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Services;

/// <summary>
/// Phase 3 of the band-membership fix (Flow Canvas): a block the user wired into a nested
/// container's branch on the canvas carries connect-authored _isChildOf/_stepPath metadata —
/// exactly what childMembership.ts (deriveChildMembership/applyChildMembership) writes in the React
/// store. This verifies the C# exporter treats that block as a real branch member: CollectBranchChain's
/// metadata path pulls it into the branch body in _stepPath order, it serialises from its props
/// (the import-only `_` metadata is ignored), and the result round-trips back through import.
/// </summary>
public class FlowCanvasBridgeConnectMembershipExportTests
{
    // The nested `if` is LAST in the foreach do-branch, so its `continue` handle is free — the exact
    // shape that makes the reported "wire a print onto the inner if" gesture reachable.
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
        """;

    [Fact]
    public void ContinueWiredChild_IsExportedIntoBranchInOrder_AndRoundTrips()
    {
        var bridge = new FlowCanvasBridge();
        var (nodes, edges) = bridge.TextToGraph(Yaml);

        JObject NodeByStepPath(string sp) => nodes.OfType<JObject>()
            .First(n => (string?)n["data"]?["props"]?["_stepPath"] == sp);

        var foreachNode = NodeByStepPath("steps/0");
        var ifNode = NodeByStepPath("steps/0/do/1");
        var foreachId = (string)foreachNode["id"]!;
        var ifId = (string)ifNode["id"]!;

        // Mimic onConnect → applyChildMembership: the new send becomes the next do-sibling after the
        // nested if (do/2), tagged as a child of the foreach, with the import-only metadata absent
        // (no _yamlSnippet) so it serialises from its `command` prop — just like a palette drop.
        var appended = new JObject
        {
            ["id"] = "appended-1",
            ["type"] = "block",
            ["position"] = new JObject { ["x"] = 0, ["y"] = 0 },
            ["data"] = new JObject
            {
                ["blockType"] = "send",
                ["label"] = "Send",
                ["props"] = new JObject
                {
                    ["command"] = "appended",
                    ["_isChildOf"] = foreachId,
                    ["_stepPath"] = "steps/0/do/2",
                    ["_membershipFromConnect"] = true,
                },
            },
        };
        nodes.Add(appended);
        // applyChildMembership flags the ancestor container so export regenerates it from the graph.
        (foreachNode["data"]!["props"] as JObject)!["_forceGraphExport"] = true;

        // The continuation edge onConnect adds: if → appended via the if's own 'continue' handle.
        edges.Add(new JObject
        {
            ["id"] = $"e-{ifId}-appended-1-continue",
            ["source"] = ifId,
            ["target"] = "appended-1",
            ["sourceHandle"] = "continue",
        });

        var result = bridge.ExportGraphToYaml(new JObject { ["nodes"] = nodes, ["edges"] = edges });
        Assert.True(result.Success, "export errors: " + string.Join("; ", result.Errors));

        // The appended send lands INSIDE the foreach do-branch, AFTER the nested if (not orphaned out).
        var foreachStep = new ScriptParser().Parse(result.Yaml ?? "").Steps.Single();
        Assert.Equal(StepType.Foreach, foreachStep.GetStepType());
        var doBranch = foreachStep.Do!;
        Assert.Equal(3, doBranch.Count);
        Assert.Equal(StepType.Send, doBranch[0].GetStepType());
        Assert.Equal(StepType.If, doBranch[1].GetStepType());
        Assert.Equal(StepType.Send, doBranch[2].GetStepType());
        Assert.Equal("appended", doBranch[2].Send);

        // Round-trip: re-importing the exported YAML reproduces the appended step in the same slot.
        var (nodes2, _) = bridge.TextToGraph(result.Yaml!);
        var appendedAgain = nodes2.OfType<JObject>()
            .First(n => (string?)n["data"]?["props"]?["_stepPath"] == "steps/0/do/2");
        Assert.Equal("send", (string?)appendedAgain["data"]?["blockType"]);
    }
}
