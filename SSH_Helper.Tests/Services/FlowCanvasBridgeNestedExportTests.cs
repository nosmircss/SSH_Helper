using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Services;

/// <summary>
/// Regression tests for issue #45: exporting a Flow Canvas graph whose containers must be
/// regenerated from the graph (the user edited a property, which sets _forceGraphExport on every
/// ancestor container) used to corrupt nested-container branches. The original bug over-consumed
/// (swallowing/duplicating following siblings); a first fix attempt then under-consumed for nested
/// MULTI-branch containers (silently dropping following siblings). The committed fix reconstructs
/// each branch from the authoritative _isChildOf/_stepPath metadata.
///
/// The critical cases are the *mid-branch* multi-branch nested containers (a nested if/else,
/// if/elif/else, try/catch/finally, or parallel that is NOT the last child of its parent branch),
/// because those are exactly what the edge-following reconstruction got wrong.
/// </summary>
public class FlowCanvasBridgeNestedExportTests
{
    // The original issue #45 shape: a nested if/else as the terminal child, with single-branch
    // nested ifs inside its then-branch.
    private const string DeepNestedYaml = """
        steps:
        - foreach: syslogd in syslog_instance
          do:
            - print: "Processing {{syslogd}}"
            - set:
                expression: port = json.get(current_parsed, "log.{{syslogd}}.setting.port", "514")
            - if:
                condition: syslog_status == "enable"
                then:
                  - send:
                      command: set status disable
                  - send:
                      command: end
                      expect: 'Do you want to continue\? \(y/n\)'
                  - if:
                      condition: port != "514"
                      then:
                        - send:
                            command: y
                  - send:
                      command: set status enable
                  - if:
                      condition: interface_select_method == "specify"
                      then:
                        - send:
                            command: set interface {{interface}}
                  - print:
                      message: "{{syslogd}} Restarted"
                else:
                  - print:
                      message: "Nothing to do"
        """;

    // A nested if/else placed BEFORE following siblings in the same branch — the case the
    // first fix attempt silently truncated.
    private const string MidBranchIfElseYaml = """
        steps:
        - foreach: x in items
          do:
            - print: before
            - if:
                condition: x == "a"
                then:
                  - send:
                      command: then1
                else:
                  - send:
                      command: else1
            - print: after1
            - send:
                command: after2
        """;

    private const string MidBranchIfElifElseYaml = """
        steps:
        - foreach: x in items
          do:
            - print: before
            - if:
                condition: x == "a"
                then:
                  - send:
                      command: then1
                elif:
                  - condition: x == "b"
                    then:
                      - send:
                          command: elif1
                else:
                  - send:
                      command: else1
            - print: after1
            - send:
                command: after2
        """;

    private const string MidBranchTryYaml = """
        steps:
        - foreach: x in items
          do:
            - print: before
            - try:
                do:
                  - send:
                      command: try1
                catch:
                  - send:
                      command: catch1
                finally:
                  - send:
                      command: finally1
            - print: after1
            - send:
                command: after2
        """;

    private const string MidBranchParallelYaml = """
        steps:
        - foreach: x in items
          do:
            - print: before
            - parallel:
                steps:
                  - send:
                      command: p1
                  - send:
                      command: p2
            - print: after1
            - send:
                command: after2
        """;

    private const string MidBranchSwitchYaml = """
        steps:
        - foreach: x in items
          do:
            - print: before
            - switch:
                value: x
                cases:
                  - value: a
                    do:
                      - send:
                          command: s1
                  - value: b
                    do:
                      - send:
                          command: s2
                default:
                  - send:
                      command: sd
            - print: after1
            - send:
                command: after2
        """;

    public static IEnumerable<object[]> Fixtures() => new[]
    {
        new object[] { "deep-nested", DeepNestedYaml },
        new object[] { "mid-branch-if-else", MidBranchIfElseYaml },
        new object[] { "mid-branch-if-elif-else", MidBranchIfElifElseYaml },
        new object[] { "mid-branch-try", MidBranchTryYaml },
        new object[] { "mid-branch-parallel", MidBranchParallelYaml },
        new object[] { "mid-branch-switch", MidBranchSwitchYaml },
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Export_WithForceGraphExport_PreservesNestedStructure(string name, string yaml)
    {
        AssertGraphRegenerationPreservesStructure(yaml, forceFlag: true, stripSnippets: false, name);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Export_WithSnippetsStripped_PreservesNestedStructure(string name, string yaml)
    {
        AssertGraphRegenerationPreservesStructure(yaml, forceFlag: false, stripSnippets: true, name);
    }

    private static void AssertGraphRegenerationPreservesStructure(string yaml, bool forceFlag, bool stripSnippets, string name)
    {
        var bridge = new FlowCanvasBridge();
        var expected = new ScriptParser().Parse(yaml);

        var (nodes, edges) = bridge.TextToGraph(yaml);

        foreach (var n in nodes.OfType<JObject>())
        {
            var props = n["data"]?["props"] as JObject;
            if (props == null) continue;
            if (stripSnippets) props.Remove("_yamlSnippet");
            if (forceFlag && IsContainer(n["data"]?["blockType"]?.ToString() ?? ""))
                props["_forceGraphExport"] = true;
        }

        var result = bridge.ExportGraphToYaml(new JObject { ["nodes"] = nodes, ["edges"] = edges });
        Assert.True(result.Success, $"[{name}] export errors: " + string.Join("; ", result.Errors));

        var actual = new ScriptParser().Parse(result.Yaml ?? "");

        var expectedCanon = Canonicalize(expected.Steps);
        var actualCanon = Canonicalize(actual.Steps);
        Assert.True(expectedCanon == actualCanon,
            $"[{name}] structure changed on graph regeneration.\n--- EXPECTED ---\n{expectedCanon}\n--- ACTUAL ---\n{actualCanon}\n--- YAML ---\n{result.Yaml}");
    }

    private static bool IsContainer(string blockType) =>
        blockType is "if" or "foreach" or "while" or "repeat" or "try" or "switch" or "parallel";

    /// <summary>
    /// Formatting-independent canonical signature of a step tree: step type + primary value +
    /// recursively nested branches. Two scripts with the same signature are semantically equivalent.
    /// </summary>
    private static string Canonicalize(List<ScriptStep> steps)
    {
        var sb = new StringBuilder();
        Walk(steps, 0, sb);
        return sb.ToString();
    }

    private static void Walk(List<ScriptStep>? steps, int depth, StringBuilder sb)
    {
        if (steps == null) return;
        var indent = new string(' ', depth * 2);
        foreach (var s in steps)
        {
            var type = s.GetStepType();
            sb.Append(indent).Append(type).Append('(').Append(Primary(s, type)).Append(')').Append('\n');
            WalkBranch(s.Do, depth + 1, "do", sb);
            WalkBranch(s.Then, depth + 1, "then", sb);
            if (s.Elif != null)
                for (int i = 0; i < s.Elif.Count; i++)
                {
                    sb.Append(indent).Append("  elif(").Append(s.Elif[i].If).Append(")\n");
                    WalkBranch(s.Elif[i].Then, depth + 2, "then", sb);
                }
            WalkBranch(s.Else, depth + 1, "else", sb);
            WalkBranch(s.Try, depth + 1, "try", sb);
            WalkBranch(s.Catch, depth + 1, "catch", sb);
            WalkBranch(s.Finally, depth + 1, "finally", sb);
            if (s.Cases != null)
                for (int i = 0; i < s.Cases.Count; i++)
                {
                    sb.Append(indent).Append("  case(").Append(s.Cases[i].Value).Append(")\n");
                    WalkBranch(s.Cases[i].Do, depth + 2, "do", sb);
                }
            if (s.Parallel?.Steps != null)
                WalkBranch(s.Parallel.Steps, depth + 1, "parallel", sb);
        }
    }

    private static void WalkBranch(List<ScriptStep>? branch, int depth, string label, StringBuilder sb)
    {
        if (branch == null || branch.Count == 0) return;
        sb.Append(new string(' ', depth * 2)).Append('.').Append(label).Append('\n');
        Walk(branch, depth + 1, sb);
    }

    private static string Primary(ScriptStep s, StepType type) => type switch
    {
        StepType.Send => $"{s.Send}|expect={s.Expect}|cap={s.Capture}|sup={s.Suppress}",
        StepType.Print => s.Print ?? "",
        StepType.Set => s.Set ?? "",
        StepType.If => s.If ?? "",
        StepType.Foreach => s.Foreach ?? "",
        StepType.Switch => s.Switch ?? "",
        _ => "",
    };
}
