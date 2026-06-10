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

    // Two sibling ifs: `end` starts in IF-A's then (with a survivor after it) and y in IF-B's then.
    private const string TwoBandsYaml = """
        steps:
        - if:
            condition: a == "1"
            then:
              - send:
                  command: end
              - send:
                  command: survivor
        - if:
            condition: b == "2"
            then:
              - send:
                  command: y
        """;

    /// <summary>
    /// Export-level pin for the band-to-band MOVE (the re-home fix): after the React store moves
    /// `end` from IF-A's then to IF-B's then, BOTH containers carry _forceGraphExport. Export must
    /// emit `end` exactly once — nested under IF-B — and IF-A's then must hold only the survivor.
    /// Without the _forceGraphExport flag on the VACATED container, IF-A would re-emit `end` from
    /// its stale snippet while IF-B also emits it from the graph: a SILENT duplicate (the export
    /// succeeds). (If the user leaves band A with no then entry wire at all, the exporter instead
    /// raises a loud "missing 'then' branch connection" diagnostic — not this test's concern.)
    /// </summary>
    [Fact]
    public void BandToBandMove_EmitsMovedBlockOnceUnderNewContainer_VacatedBranchDropsIt()
    {
        var bridge = new FlowCanvasBridge();
        var (nodes, edges) = bridge.TextToGraph(TwoBandsYaml);

        JObject NodeByStepPath(string sp) => nodes.OfType<JObject>()
            .First(n => (string?)n["data"]?["props"]?["_stepPath"] == sp);

        var ifA = NodeByStepPath("steps/0");
        var endNode = NodeByStepPath("steps/0/then/0");
        var survivor = NodeByStepPath("steps/0/then/1");
        var ifB = NodeByStepPath("steps/1");
        var yNode = NodeByStepPath("steps/1/then/0");
        var ifAId = (string)ifA["id"]!;
        var endId = (string)endNode["id"]!;
        var survivorId = (string)survivor["id"]!;
        var ifBId = (string)ifB["id"]!;
        var yId = (string)yNode["id"]!;

        // Post-move state, exactly as the React store leaves it (deriveChildMembership re-home +
        // applyChildMembership + renumberStepPaths): end re-homed under IF-B (cosmetics cleared,
        // wire-authored), y bumped, the vacated branch's survivor compacted to then/0 — itself a
        // re-home, because rewiring IF-A's then entry to the orphaned survivor goes through the
        // same gesture — and BOTH containers flagged for graph re-export.
        var endProps = (JObject)endNode["data"]!["props"]!;
        endProps["_isChildOf"] = ifBId;
        endProps["_stepPath"] = "steps/1/then/0";
        endProps["_membershipFromConnect"] = true;
        endProps.Remove("_branchLabel");
        endProps.Remove("_branchColor");
        endProps.Remove("_depth");
        ((JObject)yNode["data"]!["props"]!)["_stepPath"] = "steps/1/then/1";
        var survivorProps = (JObject)survivor["data"]!["props"]!;
        survivorProps["_stepPath"] = "steps/0/then/0";
        survivorProps["_membershipFromConnect"] = true;
        survivorProps.Remove("_branchLabel");
        survivorProps.Remove("_branchColor");
        survivorProps.Remove("_depth");
        ((JObject)ifA["data"]!["props"]!)["_forceGraphExport"] = true;
        ((JObject)ifB["data"]!["props"]!)["_forceGraphExport"] = true;

        // Faithful edges: the user deleted IF-A→end, end→survivor and IF-B→y, then wired
        // IF-A.then→survivor (band A's new then entry), IF-B.then→end, and end→y. Canvas-authored
        // branch edges carry data.branchPath (imported ones only had labels).
        var dropPairs = new[] { (ifAId, endId), (endId, survivorId), (ifBId, yId) };
        foreach (var edge in edges.OfType<JObject>().ToList())
        {
            var src = (string?)edge["source"];
            var tgt = (string?)edge["target"];
            if (dropPairs.Any(p => p.Item1 == src && p.Item2 == tgt))
                edge.Remove();
        }
        edges.Add(new JObject
        {
            ["id"] = $"e-{ifAId}-{survivorId}",
            ["source"] = ifAId,
            ["target"] = survivorId,
            ["label"] = "then",
            ["data"] = new JObject { ["branchPath"] = "then" },
        });
        edges.Add(new JObject
        {
            ["id"] = $"e-{ifBId}-{endId}",
            ["source"] = ifBId,
            ["target"] = endId,
            ["label"] = "then",
            ["data"] = new JObject { ["branchPath"] = "then" },
        });
        edges.Add(new JObject
        {
            ["id"] = $"e-{endId}-{yId}",
            ["source"] = endId,
            ["target"] = yId,
        });

        var result = bridge.ExportGraphToYaml(new JObject { ["nodes"] = nodes, ["edges"] = edges });
        Assert.True(result.Success, "export errors: " + string.Join("; ", result.Errors));

        var steps = new ScriptParser().Parse(result.Yaml ?? "").Steps;
        Assert.Equal(2, steps.Count);
        Assert.Equal(StepType.If, steps[0].GetStepType());
        Assert.Equal(StepType.If, steps[1].GetStepType());

        // Vacated branch: ONLY the survivor (the stale snippet must not re-emit `end`).
        Assert.Single(steps[0].Then!);
        Assert.Equal("survivor", steps[0].Then![0].Send);

        // New branch: end then y, in _stepPath order.
        Assert.Equal(2, steps[1].Then!.Count);
        Assert.Equal("end", steps[1].Then![0].Send);
        Assert.Equal("y", steps[1].Then![1].Send);

        // And globally: the moved block is emitted EXACTLY once across the whole script.
        var sends = new List<string>();
        CollectSends(steps, sends);
        Assert.Equal(1, sends.Count(s => s == "end"));
    }

    private static void CollectSends(List<ScriptStep> steps, List<string> acc)
    {
        foreach (var step in steps)
        {
            if (step.Send != null) acc.Add(step.Send);
            if (step.Then is { Count: > 0 }) CollectSends(step.Then, acc);
            if (step.Else is { Count: > 0 }) CollectSends(step.Else, acc);
            if (step.Do is { Count: > 0 }) CollectSends(step.Do, acc);
            if (step.Elif != null)
                foreach (var elif in step.Elif)
                    if (elif.Then is { Count: > 0 }) CollectSends(elif.Then, acc);
        }
    }
}
