using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;
using YamlDotNet.Serialization;

namespace SSH_Helper.Tests.Services;

public class FlowCanvasBridgeTests
{
    [Fact]
    public void TextToGraph_VaultStep_ImportsAsVaultBlock_WithPreviewAndExtractedProps()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            steps:
              - vault:
                  path: ssh/creds/router-a
                  key: password
                  into: secret_password
            """;

        var (nodes, _) = bridge.TextToGraph(yaml);

        var vaultNode = nodes
            .OfType<JObject>()
            .FirstOrDefault(node =>
                string.Equals(
                    node["data"]?["blockType"]?.ToString(),
                    "vault",
                    StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(vaultNode);

        var props = vaultNode!["data"]?["props"] as JObject;
        Assert.NotNull(props);
        Assert.Equal("ssh/creds/router-a", props!["path"]?.ToString());
        Assert.Equal("password", props["key"]?.ToString());
        Assert.Equal("secret_password", props["into"]?.ToString());
        Assert.Equal("ssh/creds/router-a", props["_preview"]?.ToString());
    }

    [Fact]
    public void TextToGraph_SetHistoryLabelScalarStep_ImportsAsSetHistoryLabelBlock_WithPreviewAndExtractedProps()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            steps:
              - sethistorylabel: Core Router
            """;

        var (nodes, _) = bridge.TextToGraph(yaml);

        var labelNode = nodes
            .OfType<JObject>()
            .FirstOrDefault(node =>
                string.Equals(
                    node["data"]?["blockType"]?.ToString(),
                    "sethistorylabel",
                    StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(labelNode);

        var props = labelNode!["data"]?["props"] as JObject;
        Assert.NotNull(props);
        Assert.Equal("Core Router", props!["value"]?.ToString());
        Assert.Equal("Core Router", props["_preview"]?.ToString());
    }

    [Fact]
    public void TextToGraph_NotifyStep_ImportsAsNotifyBlock_WithExtractedProps()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            steps:
              - notify:
                  profile: ops
                  title: Deployment done
                  message: "Build finished"
                  level: success
                  mention:
                    - here
                    - user:123456789
                  attachments:
                    - C:\reports\host-01.txt
                    - C:\reports\summary.csv
                  into: notify_result
            """;

        var (nodes, _) = bridge.TextToGraph(yaml);

        var notifyNode = nodes
            .OfType<JObject>()
            .FirstOrDefault(node =>
                string.Equals(
                    node["data"]?["blockType"]?.ToString(),
                    "notify",
                    StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(notifyNode);

        var props = notifyNode!["data"]?["props"] as JObject;
        Assert.NotNull(props);
        Assert.Equal("ops", props!["profile"]?.ToString());
        Assert.Equal("Deployment done", props["title"]?.ToString());
        Assert.Equal("Build finished", props["message"]?.ToString());
        Assert.Equal("success", props["level"]?.ToString());
        Assert.Equal("notify_result", props["into"]?.ToString());
        Assert.Equal(JTokenType.Array, props["mention"]?.Type);
        Assert.Equal(JTokenType.Array, props["attachments"]?.Type);
        Assert.Equal(@"C:\reports\host-01.txt", props["attachments"]?[0]?.ToString());
        Assert.Equal(@"C:\reports\summary.csv", props["attachments"]?[1]?.ToString());
    }

    [Fact]
    public void ExportGraphToYaml_MixedGeneratedAndContainerSteps_ProducesParsableYaml()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            name: QA Parallel Loop Control
            version: 1
            debug: true
            steps:
              - set: "i = 0"
              - set: "marks = ''"
              - while:
                  condition: "i < 5"
                  max_iterations: 10
                  do:
                    - set:
                        expression: "i = i + 1"
                    - parallel:
                        steps:
                          - if:
                              condition: "i == 2"
                              then:
                                - continue: true
                          - if:
                              condition: "i == 4"
                              then:
                                - break: true
            """;

        var (nodes, edges) = bridge.TextToGraph(yaml);
        var graph = new JObject
        {
            ["nodes"] = nodes,
            ["edges"] = edges
        };

        var export = bridge.ExportGraphToYaml(graph);

        Assert.True(export.Success, string.Join(" | ", export.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(export.Yaml);
        var validationErrors = parser.Validate(script, export.Yaml, enforceCanonicalSyntax: true);

        Assert.Empty(validationErrors);
        Assert.Equal(3, script.Steps.Count);
        Assert.Equal(StepType.While, script.Steps[2].GetStepType());
    }

    [Fact]
    public void ExportGraphToYaml_RepeatUntilContainer_RoundTripsToRepeatStep()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            name: Repeat RoundTrip
            version: 1
            steps:
              - set: "i = 0"
              - repeat:
                  until: "i >= 3"
                  max_iterations: 10
                  do:
                    - set:
                        expression: "i = i + 1"
            """;

        var (nodes, edges) = bridge.TextToGraph(yaml);
        var graph = new JObject
        {
            ["nodes"] = nodes,
            ["edges"] = edges
        };

        var export = bridge.ExportGraphToYaml(graph);

        Assert.True(export.Success, string.Join(" | ", export.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(export.Yaml);
        var validationErrors = parser.Validate(script, export.Yaml, enforceCanonicalSyntax: true);

        Assert.Empty(validationErrors);
        Assert.Equal(2, script.Steps.Count);
        var repeatStep = script.Steps[1];
        Assert.Equal(StepType.Repeat, repeatStep.GetStepType());
        Assert.Equal("i >= 3", repeatStep.Until);
        Assert.NotNull(repeatStep.Do);
        Assert.Single(repeatStep.Do!);
    }

    [Fact]
    public void ExportGraphToYaml_WhenGuardOnGeneratedStep_RoundTrips()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            steps:
              - send:
                  command: systemctl restart nginx
                when: nginx_state != "active"
            """;

        var (nodes, edges) = bridge.TextToGraph(yaml);
        var graph = new JObject { ["nodes"] = nodes, ["edges"] = edges };
        var export = bridge.ExportGraphToYaml(graph);

        Assert.True(export.Success, string.Join(" | ", export.Errors));
        var script = new ScriptParser().Parse(export.Yaml);
        Assert.Single(script.Steps);
        Assert.Equal("nginx_state != \"active\"", script.Steps[0].When);
    }

    [Fact]
    public void ExportGraphToYaml_WhenGuardOnContainerStep_RoundTrips()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            steps:
              - while:
                  condition: "1 == 1"
                  max_iterations: 3
                  do:
                    - print:
                        message: hi
                when: enabled == "yes"
            """;

        var (nodes, edges) = bridge.TextToGraph(yaml);
        var graph = new JObject { ["nodes"] = nodes, ["edges"] = edges };
        var export = bridge.ExportGraphToYaml(graph);

        Assert.True(export.Success, string.Join(" | ", export.Errors));
        var script = new ScriptParser().Parse(export.Yaml);
        Assert.Single(script.Steps);
        Assert.Equal("enabled == \"yes\"", script.Steps[0].When);
    }

    [Fact]
    public void ExportGraphToYaml_UnsupportedBlockType_ReturnsErrorDiagnostic()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                new JObject
                {
                    ["id"] = "__start__",
                    ["type"] = "start",
                    ["position"] = new JObject { ["x"] = 0, ["y"] = 0 },
                    ["data"] = new JObject
                    {
                        ["blockType"] = "_start",
                        ["label"] = "Start",
                        ["props"] = new JObject()
                    }
                },
                new JObject
                {
                    ["id"] = "node-1",
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = 10, ["y"] = 10 },
                    ["data"] = new JObject
                    {
                        ["blockType"] = "not-a-real-block",
                        ["label"] = "Invalid",
                        ["props"] = new JObject()
                    }
                }
            },
            ["edges"] = new JArray
            {
                new JObject
                {
                    ["id"] = "e-start-node-1",
                    ["source"] = "__start__",
                    ["target"] = "node-1"
                }
            }
        };

        var result = bridge.ExportGraphToYaml(graph);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Unsupported block type", System.StringComparison.OrdinalIgnoreCase));

        var errorDiagnostic = Assert.Single(result.Diagnostics, d =>
            d.Severity == FlowCanvasBridge.ExportDiagnosticSeverity.Error &&
            d.Message.Contains("Unsupported block type", System.StringComparison.OrdinalIgnoreCase));
        Assert.Equal("node-1", errorDiagnostic.NodeId);
    }

    [Fact]
    public void ExportGraphToYaml_IncludesChildNodeStepPathMapping()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                new JObject
                {
                    ["id"] = "__start__",
                    ["type"] = "start",
                    ["position"] = new JObject { ["x"] = 40, ["y"] = 20 },
                    ["data"] = new JObject
                    {
                        ["blockType"] = "_start",
                        ["label"] = "Start",
                        ["props"] = new JObject()
                    }
                },
                new JObject
                {
                    ["id"] = "parent",
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = 100, ["y"] = 100 },
                    ["data"] = new JObject
                    {
                        ["blockType"] = "if",
                        ["label"] = "If",
                        ["props"] = new JObject
                        {
                            ["_stepPath"] = "steps/0",
                            ["_yamlSnippet"] = "- if: \"${ok}\"\n  then:\n    - print: \"ok\"\n"
                        }
                    }
                },
                new JObject
                {
                    ["id"] = "child",
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = 140, ["y"] = 220 },
                    ["data"] = new JObject
                    {
                        ["blockType"] = "print",
                        ["label"] = "Print",
                        ["props"] = new JObject
                        {
                            ["_isChildOf"] = "parent",
                            ["_stepPath"] = "steps/0/then/0",
                            ["message"] = "ok"
                        }
                    }
                }
            },
            ["edges"] = new JArray
            {
                new JObject
                {
                    ["id"] = "e-start-parent",
                    ["source"] = "__start__",
                    ["target"] = "parent"
                },
                new JObject
                {
                    ["id"] = "e-parent-child",
                    ["source"] = "parent",
                    ["target"] = "child"
                }
            }
        };

        var result = bridge.ExportGraphToYaml(graph);

        Assert.True(result.Success);
        Assert.True(result.NodeToStepPathMap.TryGetValue("parent", out var parentPath));
        Assert.Equal("steps/0", parentPath);

        Assert.True(result.NodeToStepPathMap.TryGetValue("child", out var childPath));
        Assert.Equal("steps/0/then/0", childPath);
    }

    [Fact]
    public void ExportGraphToYaml_PlainCommentNodes_AreConsumedSilently()
    {
        // A comment node with blockType 'comment' but NO kind/anchor is a plain visual note — must NOT inject any # lines.
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                new JObject
                {
                    ["id"] = "__start__",
                    ["type"] = "start",
                    ["position"] = new JObject { ["x"] = 20, ["y"] = 10 },
                    ["data"] = new JObject
                    {
                        ["blockType"] = "_start",
                        ["label"] = "Start",
                        ["props"] = new JObject()
                    }
                },
                new JObject
                {
                    ["id"] = "comment-1",
                    ["type"] = "comment",
                    ["position"] = new JObject { ["x"] = 10, ["y"] = 10 },
                    ["data"] = new JObject
                    {
                        ["blockType"] = "comment",
                        ["label"] = "Comment",
                        ["props"] = new JObject()
                    }
                },
                new JObject
                {
                    ["id"] = "node-1",
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = 40, ["y"] = 90 },
                    ["data"] = new JObject
                    {
                        ["blockType"] = "print",
                        ["label"] = "Print",
                        ["props"] = new JObject
                        {
                            ["message"] = "hello"
                        }
                    }
                }
            },
            ["edges"] = new JArray
            {
                new JObject
                {
                    ["id"] = "e-start-comment-1",
                    ["source"] = "__start__",
                    ["target"] = "comment-1"
                },
                new JObject
                {
                    ["id"] = "e-comment-1-node-1",
                    ["source"] = "comment-1",
                    ["target"] = "node-1"
                }
            }
        };

        var result = bridge.ExportGraphToYaml(graph);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("Comment nodes are ignored", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("comment-1", result.NodeToStepPathMap.Keys);
        Assert.Contains("node-1", result.NodeToStepPathMap.Keys);
        Assert.DoesNotContain("#", result.Yaml);
    }

    [Fact]
    public void ExportGraphToYaml_IfWithElifAndElse_BranchMetadataProducesCanonicalYaml()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("if-1", "if", new JObject
                {
                    ["condition"] = "${mode} == 'high'"
                }),
                CreateBlockNode("then-1", "print", new JObject { ["message"] = "then-branch" }),
                CreateBlockNode("elif-1", "print", new JObject { ["message"] = "elif-branch" }),
                CreateBlockNode("else-1", "print", new JObject { ["message"] = "else-branch" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "if-1"),
                CreateEdge("if-1", "then-1", branchPath: "then"),
                CreateEdge("if-1", "elif-1", branchPath: "elif/0/then", condition: "${mode} == 'mid'"),
                CreateEdge("if-1", "else-1", sourceHandle: "false", branchPath: "else"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var ifStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.If, ifStep.GetStepType());
        Assert.Single(ifStep.Then ?? new List<ScriptStep>());
        Assert.Single(ifStep.Elif ?? new List<ElifBranch>());
        Assert.Equal("${mode} == 'mid'", ifStep.Elif![0].If);
        Assert.Single(ifStep.Else ?? new List<ScriptStep>());
    }

    [Fact]
    public void ExportGraphToYaml_IfWithStoredSnippetAndBranchEdges_UsesGraphBranchShape()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("if-1", "if", new JObject
                {
                    ["condition"] = "abc == \"true\"",
                    // Simulate stale snippet from a previously loaded/edited block.
                    ["_yamlSnippet"] = "- if:\n    condition: abc == \"true\"\n"
                }),
                CreateBlockNode("then-1", "ping", new JObject
                {
                    ["host"] = "192.168.1.1",
                    ["count"] = 1,
                    ["into"] = "pingresults"
                }),
                CreateBlockNode("else-1", "ping", new JObject
                {
                    ["host"] = "192.168.1.1",
                    ["count"] = 1
                }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "if-1"),
                CreateEdge("if-1", "then-1", branchPath: "then"),
                CreateEdge("if-1", "else-1", sourceHandle: "false", branchPath: "else"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var ifStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.If, ifStep.GetStepType());
        Assert.Single(ifStep.Then ?? new List<ScriptStep>());
        Assert.Single(ifStep.Else ?? new List<ScriptStep>());
    }

    [Fact]
    public void ExportGraphToYaml_TryWithoutSnippet_ExportsDoCatchFinally()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("try-1", "try"),
                CreateBlockNode("do-1", "print", new JObject { ["message"] = "do-branch" }),
                CreateBlockNode("catch-1", "print", new JObject { ["message"] = "catch-branch" }),
                CreateBlockNode("finally-1", "print", new JObject { ["message"] = "finally-branch" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "try-1"),
                CreateEdge("try-1", "do-1", branchPath: "try"),
                CreateEdge("try-1", "catch-1", branchPath: "catch"),
                CreateEdge("try-1", "finally-1", branchPath: "finally"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var tryStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.Try, tryStep.GetStepType());
        Assert.Single(tryStep.Try ?? new List<ScriptStep>());
        Assert.Single(tryStep.Catch ?? new List<ScriptStep>());
        Assert.Single(tryStep.Finally ?? new List<ScriptStep>());
    }

    [Fact]
    public void ExportGraphToYaml_TryWithStoredSnippetAndBranchEdges_UsesGraphBranchShape()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("try-1", "try", new JObject
                {
                    ["_yamlSnippet"] = "- try:\n    do:\n      - print:\n          message: stale\n"
                }),
                CreateBlockNode("do-1", "print", new JObject { ["message"] = "do-branch" }),
                CreateBlockNode("catch-1", "print", new JObject { ["message"] = "catch-branch" }),
                CreateBlockNode("finally-1", "print", new JObject { ["message"] = "finally-branch" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "try-1"),
                CreateEdge("try-1", "do-1", branchPath: "try"),
                CreateEdge("try-1", "catch-1", branchPath: "catch"),
                CreateEdge("try-1", "finally-1", branchPath: "finally"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var tryStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.Try, tryStep.GetStepType());
        Assert.Single(tryStep.Try ?? new List<ScriptStep>());
        Assert.Single(tryStep.Catch ?? new List<ScriptStep>());
        Assert.Single(tryStep.Finally ?? new List<ScriptStep>());
    }

    [Fact]
    public void ExportGraphToYaml_SwitchWithoutSnippet_ExportsCasesAndDefault()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("switch-1", "switch", new JObject { ["value"] = "${region}" }),
                CreateBlockNode("case-1", "print", new JObject { ["message"] = "north-case" }),
                CreateBlockNode("case-2", "print", new JObject { ["message"] = "south-case" }),
                CreateBlockNode("default-1", "print", new JObject { ["message"] = "default-case" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "switch-1"),
                CreateEdge("switch-1", "case-1", branchPath: "cases/0/do", caseValue: "north"),
                CreateEdge("switch-1", "case-2", branchPath: "cases/1/do", caseValue: "south"),
                CreateEdge("switch-1", "default-1", branchPath: "default"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var switchStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.Switch, switchStep.GetStepType());
        Assert.Equal("${region}", switchStep.Switch);
        Assert.NotNull(switchStep.Cases);
        Assert.Equal(2, switchStep.Cases!.Count);
        Assert.Equal("north", switchStep.Cases[0].Value);
        Assert.Equal("south", switchStep.Cases[1].Value);
        Assert.Single(switchStep.Else ?? new List<ScriptStep>());
    }

    [Fact]
    public void ExportGraphToYaml_SwitchWithStoredSnippetAndBranchEdges_UsesGraphBranchShape()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("switch-1", "switch", new JObject
                {
                    ["value"] = "${region}",
                    ["_yamlSnippet"] = "- switch:\n    value: \"${region}\"\n    cases: []\n"
                }),
                CreateBlockNode("case-1", "print", new JObject { ["message"] = "north-case" }),
                CreateBlockNode("case-2", "print", new JObject { ["message"] = "south-case" }),
                CreateBlockNode("default-1", "print", new JObject { ["message"] = "default-case" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "switch-1"),
                CreateEdge("switch-1", "case-1", branchPath: "cases/0/do", caseValue: "north"),
                CreateEdge("switch-1", "case-2", branchPath: "cases/1/do", caseValue: "south"),
                CreateEdge("switch-1", "default-1", branchPath: "default"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var switchStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.Switch, switchStep.GetStepType());
        Assert.NotNull(switchStep.Cases);
        Assert.Equal(2, switchStep.Cases!.Count);
        Assert.Equal("north", switchStep.Cases[0].Value);
        Assert.Equal("south", switchStep.Cases[1].Value);
        Assert.Single(switchStep.Else ?? new List<ScriptStep>());
    }

    [Fact]
    public void ExportGraphToYaml_ParallelWithoutSnippet_ExportsBranchSteps()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("parallel-1", "parallel"),
                CreateBlockNode("branch-1", "print", new JObject { ["message"] = "branch-one" }),
                CreateBlockNode("branch-2", "print", new JObject { ["message"] = "branch-two" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "parallel-1"),
                CreateEdge("parallel-1", "branch-1", branchPath: "parallel/0"),
                CreateEdge("parallel-1", "branch-2", branchPath: "parallel/1"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var parallelStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.Parallel, parallelStep.GetStepType());
        Assert.NotNull(parallelStep.Parallel);
        Assert.Equal(2, parallelStep.Parallel!.Steps.Count);
    }

    [Fact]
    public void ExportGraphToYaml_ParallelWithStoredSnippetAndBranchEdges_UsesGraphBranchShape()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("parallel-1", "parallel", new JObject
                {
                    ["_yamlSnippet"] = "- parallel:\n    steps: []\n"
                }),
                CreateBlockNode("branch-1", "print", new JObject { ["message"] = "branch-one" }),
                CreateBlockNode("branch-2", "print", new JObject { ["message"] = "branch-two" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "parallel-1"),
                CreateEdge("parallel-1", "branch-1", branchPath: "parallel/0"),
                CreateEdge("parallel-1", "branch-2", branchPath: "parallel/1"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var parallelStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.Parallel, parallelStep.GetStepType());
        Assert.NotNull(parallelStep.Parallel);
        Assert.Equal(2, parallelStep.Parallel!.Steps.Count);
    }

    [Fact]
    public void ExportGraphToYaml_IfWithForceGraphExport_UsesEditedVisualChildBranchValues()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("if-1", "if", new JObject
                {
                    ["condition"] = "${should_run}",
                    ["_yamlSnippet"] = "- if:\n    condition: \"${should_run}\"\n    then:\n      - print:\n          message: stale-if\n",
                    ["_forceGraphExport"] = true,
                }),
                CreateVisualChildNode("then-1", "print", "if-1", "then", new JObject { ["message"] = "updated-if-then" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "if-1"),
                CreateEdge("if-1", "then-1", branchPath: "then"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var ifStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.If, ifStep.GetStepType());
        Assert.Equal("updated-if-then", Assert.Single(ifStep.Then ?? new List<ScriptStep>()).Print);
    }

    [Fact]
    public void ExportGraphToYaml_ForeachWithForceGraphExport_UsesEditedVisualChildBranchValues()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("for-1", "foreach", new JObject
                {
                    ["iterator"] = "item in ${items}",
                    ["_yamlSnippet"] = "- foreach:\n    iterator: \"item in ${items}\"\n    do:\n      - print:\n          message: stale-foreach\n",
                    ["_forceGraphExport"] = true,
                }),
                CreateVisualChildNode("do-1", "print", "for-1", "do", new JObject { ["message"] = "updated-foreach-do" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "for-1"),
                CreateEdge("for-1", "do-1", branchPath: "do"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var forStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.Foreach, forStep.GetStepType());
        Assert.Equal("updated-foreach-do", Assert.Single(forStep.Do ?? new List<ScriptStep>()).Print);
    }

    [Fact]
    public void ExportGraphToYaml_WhileWithForceGraphExport_UsesEditedVisualChildBranchValues()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("while-1", "while", new JObject
                {
                    ["condition"] = "${keep_going}",
                    ["_yamlSnippet"] = "- while:\n    condition: \"${keep_going}\"\n    do:\n      - print:\n          message: stale-while\n",
                    ["_forceGraphExport"] = true,
                }),
                CreateVisualChildNode("do-1", "print", "while-1", "do", new JObject { ["message"] = "updated-while-do" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "while-1"),
                CreateEdge("while-1", "do-1", branchPath: "do"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var whileStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.While, whileStep.GetStepType());
        Assert.Equal("updated-while-do", Assert.Single(whileStep.Do ?? new List<ScriptStep>()).Print);
    }

    [Fact]
    public void ExportGraphToYaml_TryWithForceGraphExport_UsesEditedVisualChildBranchValues()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("try-1", "try", new JObject
                {
                    ["_yamlSnippet"] = "- try:\n    do:\n      - print:\n          message: stale-try\n",
                    ["_forceGraphExport"] = true,
                }),
                CreateVisualChildNode("do-1", "print", "try-1", "do", new JObject { ["message"] = "updated-try-do" }),
                CreateVisualChildNode("catch-1", "print", "try-1", "catch", new JObject { ["message"] = "updated-try-catch" }),
                CreateVisualChildNode("finally-1", "print", "try-1", "finally", new JObject { ["message"] = "updated-try-finally" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "try-1"),
                CreateEdge("try-1", "do-1", branchPath: "try"),
                CreateEdge("try-1", "catch-1", branchPath: "catch"),
                CreateEdge("try-1", "finally-1", branchPath: "finally"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var tryStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.Try, tryStep.GetStepType());
        Assert.Equal("updated-try-do", Assert.Single(tryStep.Try ?? new List<ScriptStep>()).Print);
        Assert.Equal("updated-try-catch", Assert.Single(tryStep.Catch ?? new List<ScriptStep>()).Print);
        Assert.Equal("updated-try-finally", Assert.Single(tryStep.Finally ?? new List<ScriptStep>()).Print);
    }

    [Fact]
    public void ExportGraphToYaml_SwitchWithForceGraphExport_UsesEditedVisualChildBranchValues()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("switch-1", "switch", new JObject
                {
                    ["value"] = "${region}",
                    ["_yamlSnippet"] = "- switch:\n    value: \"${region}\"\n    cases: []\n",
                    ["_forceGraphExport"] = true,
                }),
                CreateVisualChildNode("case-1", "print", "switch-1", "case", new JObject { ["message"] = "updated-switch-case" }),
                CreateVisualChildNode("default-1", "print", "switch-1", "default", new JObject { ["message"] = "updated-switch-default" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "switch-1"),
                CreateEdge("switch-1", "case-1", branchPath: "cases/0/do", caseValue: "north"),
                CreateEdge("switch-1", "default-1", branchPath: "default"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var switchStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.Switch, switchStep.GetStepType());
        Assert.Equal("updated-switch-case", Assert.Single(switchStep.Cases![0].Do).Print);
        Assert.Equal("updated-switch-default", Assert.Single(switchStep.Else ?? new List<ScriptStep>()).Print);
    }

    [Fact]
    public void ExportGraphToYaml_ParallelWithForceGraphExport_UsesEditedVisualChildBranchValues()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("parallel-1", "parallel", new JObject
                {
                    ["_yamlSnippet"] = "- parallel:\n    steps: []\n",
                    ["_forceGraphExport"] = true,
                }),
                CreateVisualChildNode("branch-1", "print", "parallel-1", "branch 1", new JObject { ["message"] = "updated-parallel-1" }),
                CreateVisualChildNode("branch-2", "print", "parallel-1", "branch 2", new JObject { ["message"] = "updated-parallel-2" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "parallel-1"),
                CreateEdge("parallel-1", "branch-1", branchPath: "parallel/0"),
                CreateEdge("parallel-1", "branch-2", branchPath: "parallel/1"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var parallelStep = Assert.Single(script.Steps);
        Assert.Equal(StepType.Parallel, parallelStep.GetStepType());
        Assert.NotNull(parallelStep.Parallel);
        Assert.Equal("updated-parallel-1", parallelStep.Parallel!.Steps[0].Print);
        Assert.Equal("updated-parallel-2", parallelStep.Parallel.Steps[1].Print);
    }

    [Fact]
    public void ExportGraphToYaml_StartAdvancedSectionsFromEditors_AreSerializedInPreamble()
    {
        var bridge = new FlowCanvasBridge();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"flowcanvas-parity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var importPath = Path.Combine(tempRoot, "common.yaml");
        File.WriteAllText(importPath, """
            ---
            library: true
            subroutines:
              helper:
                params: [input]
                outputs: [output]
                steps:
                  - print:
                      message: "${input}"
            """);

        var importPathForYaml = importPath.Replace("\\", "/");

        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                new JObject
                {
                    ["id"] = "__start__",
                    ["type"] = "start",
                    ["position"] = new JObject { ["x"] = 0, ["y"] = 0 },
                    ["data"] = new JObject
                    {
                        ["blockType"] = "_start",
                        ["label"] = "Start",
                        ["props"] = new JObject
                        {
                            ["name"] = "Advanced Sections",
                            ["vars_yaml"] = "vars:\n  qa_token: \"abc\"\n  retries: 3",
                            ["imports_yaml"] = $"imports:\n  - path: {importPathForYaml}\n    as: common",
                            ["subroutines_yaml"] = "subroutines:\n  helper:\n    params: [input]\n    outputs: [output]\n    steps:\n      - print:\n          message: \"${input}\"",
                        }
                    }
                },
                CreateBlockNode("print-1", "print", new JObject { ["message"] = "hello" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "print-1")
            }
        };

        try
        {
            var result = bridge.ExportGraphToYaml(graph);
            Assert.True(result.Success, string.Join(" | ", result.Errors));

            var parser = new ScriptParser();
            var script = parser.Parse(result.Yaml);
            var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
            Assert.Empty(errors);

            Assert.Equal("Advanced Sections", script.Name);
            Assert.Equal("abc", script.Vars["qa_token"]?.ToString());
            Assert.Equal("3", script.Vars["retries"]?.ToString());
            Assert.Single(script.Imports);
            Assert.Equal(importPathForYaml, script.Imports[0].Path.Replace("\\", "/"));
            Assert.Equal("common", script.Imports[0].Alias);
            Assert.True(script.Subroutines.ContainsKey("helper"));
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // best effort cleanup for temp test files
            }
        }
    }

    [Fact]
    public void ExportGraphToYaml_PrintWithSingleSpaceMessage_IsAccepted()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                new JObject
                {
                    ["id"] = "print-1",
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = 20, ["y"] = 20 },
                    ["data"] = new JObject
                    {
                        ["blockType"] = "print",
                        ["label"] = "Print",
                        ["props"] = new JObject
                        {
                            ["message"] = " "
                        }
                    }
                }
            },
            ["edges"] = new JArray()
        };

        var result = bridge.ExportGraphToYaml(graph);

        Assert.True(result.Success, string.Join(" | ", result.Errors));
    }

    [Fact]
    public void RoundTrip_SendOptions_PreservesCanonicalOptions()
    {
        var yaml = """
            ---
            name: Send options
            version: 1
            steps:
              - send:
                  command: "echo ready"
                  capture: send_output
                  suppress: true
                  timeout: 12
                  on_error: continue
                  retry: 2
                  retry_delay: 3
                  fail_on_nonzero: true
                  respond:
                    - expect: "Password:"
                      reply: "qa-secret"
            """;

        var export = RoundTripThroughBridge(yaml);
        Assert.True(export.Success, string.Join(" | ", export.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(export.Yaml);
        var errors = parser.Validate(script, export.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var send = Assert.Single(script.Steps);
        Assert.Equal(StepType.Send, send.GetStepType());
        Assert.Equal("echo ready", send.Send);
        Assert.Equal("send_output", send.Capture);
        Assert.True(send.Suppress);
        Assert.Equal(12, send.Timeout);
        Assert.Equal("continue", send.OnError);
        Assert.Equal(2, send.Retry);
        Assert.Equal(3, send.RetryDelay);
        Assert.True(send.FailOnNonZero);
        Assert.NotNull(send.Respond);
        Assert.Single(send.Respond!);
        Assert.Equal("Password:", send.Respond![0].Expect);
        Assert.Equal("qa-secret", send.Respond![0].Reply);
    }

    [Fact]
    public void RoundTrip_LogOptions_PreservesMessageAndLevel()
    {
        var yaml = """
            ---
            name: Log options
            version: 1
            steps:
              - log:
                  message: "structured message"
                  level: success
            """;

        var export = RoundTripThroughBridge(yaml);
        Assert.True(export.Success, string.Join(" | ", export.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(export.Yaml);
        var errors = parser.Validate(script, export.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        var step = Assert.Single(script.Steps);
        Assert.Equal(StepType.Log, step.GetStepType());
        var log = Assert.IsType<LogOptions>(step.Log);
        Assert.Equal("structured message", log.Message);
        Assert.Equal("success", log.Level);
    }

    [Fact]
    public void RoundTrip_SftpPortcheckAndTable_PreserveCanonicalShape()
    {
        var yaml = """
            ---
            name: Shape checks
            version: 1
            steps:
              - sftp:
                  action: upload
                  local_path: "/tmp/a.txt"
                  remote_path: "/tmp/b.txt"
                  timeout: 90
                  into: sftp_result
              - portcheck:
                  host: "127.0.0.1"
                  port: 443
                  timeout: 5
                  into: port_state
              - table:
                  data: "${rows}"
                  into: rendered
                  align: center
                  show_header: false
            """;

        var export = RoundTripThroughBridge(yaml);
        Assert.True(export.Success, string.Join(" | ", export.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(export.Yaml);
        var errors = parser.Validate(script, export.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        Assert.Equal(3, script.Steps.Count);

        var sftp = script.Steps[0];
        Assert.Equal(StepType.Sftp, sftp.GetStepType());
        Assert.NotNull(sftp.Sftp);
        Assert.Equal("/tmp/a.txt", sftp.Sftp!.LocalPath);
        Assert.Equal("/tmp/b.txt", sftp.Sftp.RemotePath);
        Assert.Equal(90, sftp.Sftp.Timeout);
        Assert.Equal("sftp_result", sftp.Sftp.Into);

        var portcheck = script.Steps[1];
        Assert.Equal(StepType.Portcheck, portcheck.GetStepType());
        Assert.NotNull(portcheck.Portcheck);
        Assert.Equal("127.0.0.1", portcheck.Portcheck!.Host);
        Assert.Equal(443, portcheck.Portcheck.Port);
        Assert.Equal(5, portcheck.Portcheck.Timeout);
        Assert.Equal("port_state", portcheck.Portcheck.Into);

        var table = script.Steps[2];
        Assert.Equal(StepType.Table, table.GetStepType());
        Assert.NotNull(table.Table);
        Assert.Equal("${rows}", table.Table!.Data);
        Assert.Equal("rendered", table.Table.Into);
        Assert.Equal("center", table.Table.Align);
        Assert.False(table.Table.ShowHeader);
    }

    [Fact]
    public void RoundTrip_AllQaPresetYamlScripts_MaintainValidationContract()
    {
        var parser = new ScriptParser();
        var failures = new List<string>();

        foreach (var preset in LoadQaYamlPresets())
        {
            Script originalScript;
            try
            {
                originalScript = parser.Parse(preset.Commands);
            }
            catch (Exception ex)
            {
                failures.Add($"{preset.Name}: original parse failed: {ex.Message}");
                continue;
            }

            var originalErrors = parser.Validate(
                originalScript,
                preset.Commands,
                enforceCanonicalSyntax: true,
                allowLibraryDefinitions: originalScript.Library);

            var expectsValidationFailure =
                preset.Name.Contains("[Expected Fail]", StringComparison.Ordinal)
                || ((originalScript.Description ?? string.Empty)
                    .Contains("Expected: intentional validation failure.", StringComparison.Ordinal));

            var export = RoundTripThroughBridge(preset.Commands);
            if (!export.Success)
            {
                if (expectsValidationFailure)
                    continue;

                failures.Add($"{preset.Name}: export failed: {string.Join(" | ", export.Errors)}");
                continue;
            }

            Script rewrittenScript;
            try
            {
                rewrittenScript = parser.Parse(export.Yaml);
            }
            catch (Exception ex)
            {
                failures.Add($"{preset.Name}: rewritten parse failed: {ex.Message}");
                continue;
            }

            var rewrittenErrors = parser.Validate(
                rewrittenScript,
                export.Yaml,
                enforceCanonicalSyntax: true,
                allowLibraryDefinitions: rewrittenScript.Library);

            if (expectsValidationFailure)
            {
                if (rewrittenErrors.Count == 0)
                    failures.Add($"{preset.Name}: expected validation failure after rewrite, but validation passed.");

                continue;
            }

            if (originalErrors.Count > 0)
            {
                failures.Add($"{preset.Name}: original script unexpectedly fails validation: {string.Join(" | ", originalErrors)}");
                continue;
            }

            if (rewrittenErrors.Count > 0)
            {
                failures.Add($"{preset.Name}: rewritten validation failed: {string.Join(" | ", rewrittenErrors)}");
                continue;
            }

            var originalTopLevelTypes = originalScript.Steps.Select(s => s.GetStepType()).ToList();
            var rewrittenTopLevelTypes = rewrittenScript.Steps.Select(s => s.GetStepType()).ToList();
            if (!originalTopLevelTypes.SequenceEqual(rewrittenTopLevelTypes))
            {
                failures.Add(
                    $"{preset.Name}: top-level step types changed. " +
                    $"original=[{string.Join(",", originalTopLevelTypes)}], rewritten=[{string.Join(",", rewrittenTopLevelTypes)}]");
            }
        }

        Assert.True(
            failures.Count == 0,
            string.Join(Environment.NewLine, failures.Take(80)));
    }

    [Fact]
    public void DriftGuard_ExportOptionCatalog_MatchesParserCatalog()
    {
        var parserOptions = ScriptParser.GetKnownStepOptionKeysByCommand();
        var bridgeOptions = FlowCanvasBridge.GetExportOptionKeysByCommand();

        var parserCommands = parserOptions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        var bridgeCommands = bridgeOptions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(
            parserCommands.SequenceEqual(bridgeCommands, StringComparer.OrdinalIgnoreCase),
            $"Command catalogs differ. parser=[{string.Join(", ", parserCommands)}] bridge=[{string.Join(", ", bridgeCommands)}]");

        var mismatches = new List<string>();
        foreach (var command in parserCommands)
        {
            var parserKeys = parserOptions[command];
            var bridgeKeys = bridgeOptions[command];
            var missing = parserKeys.Except(bridgeKeys, StringComparer.OrdinalIgnoreCase).ToList();
            if (missing.Count > 0)
                mismatches.Add($"{command}: missing [{string.Join(", ", missing)}]");
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    [Fact]
    public void DriftGuard_RegistryProperties_MapToRuntimeOptionKeys()
    {
        var parserOptions = ScriptParser.GetKnownStepOptionKeysByCommand();
        var parserRootOptions = ScriptParser.GetKnownStepRootOptionKeysByCommand();
        var blockAliases = FlowCanvasBridge.GetBlockTypeCommandKeyAliases();
        var propertyAliases = FlowCanvasBridge.GetBlockPropertyAliases();

        var registryBlocks = LoadRegistryBlockPropertyKeys(out var registryText);
        var errors = new List<string>();

        foreach (var entry in registryBlocks)
        {
            var blockType = entry.Key;
            var command = blockAliases.TryGetValue(blockType, out var mappedCommand)
                ? mappedCommand
                : blockType;

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (parserOptions.TryGetValue(command, out var commandOptions))
            {
                foreach (var option in commandOptions)
                    allowed.Add(option);
            }

            if (parserRootOptions.TryGetValue(command, out var rootOptions))
            {
                foreach (var option in rootOptions)
                    allowed.Add(option);
            }

            if (allowed.Count == 0 && entry.Value.Count > 0)
            {
                errors.Add($"{blockType}: registry declares properties for command '{command}' with no known runtime option keys.");
                continue;
            }

            foreach (var propertyKey in entry.Value)
            {
                var mappedKey = propertyAliases.TryGetValue(blockType, out var aliasMap) &&
                                aliasMap.TryGetValue(propertyKey, out var alias)
                    ? alias
                    : propertyKey;

                if (!allowed.Contains(mappedKey))
                    errors.Add($"{blockType}.{propertyKey} -> {mappedKey} is not a known runtime option key for '{command}'.");
            }
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));

        Assert.Contains(registryBlocks["send"], key => string.Equals(key, "capture", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(registryBlocks["send"], key => string.Equals(key, "delay", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(registryBlocks["interactive"], key => string.Equals(key, "timeout", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(registryBlocks["return"], key => string.Equals(key, "value", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(registryBlocks["portcheck"], key => string.Equals(key, "host", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(registryBlocks["portcheck"], key => string.Equals(key, "port", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(registryBlocks["table"], key => string.Equals(key, "data", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(registryBlocks["sftp"], key => string.Equals(key, "local_path", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(registryBlocks["sftp"], key => string.Equals(key, "remote_path", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(registryBlocks["playsound"], key => string.Equals(key, "path", StringComparison.OrdinalIgnoreCase));

        var logLevelOptionsMatch = Regex.Match(
            registryText,
            @"type:\s*'log'[\s\S]*?key:\s*'level'[\s\S]*?options:\s*\[(?<values>[^\]]+)\]",
            RegexOptions.Multiline);
        Assert.True(logLevelOptionsMatch.Success, "Unable to find log.level options in registry.ts.");
        var logLevelOptions = Regex.Matches(logLevelOptionsMatch.Groups["values"].Value, @"'([^']+)'")
            .Select(m => m.Groups[1].Value)
            .ToList();
        Assert.Contains(logLevelOptions, value => string.Equals(value, "success", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DriftGuard_RegistryPanelOrder_MatchesBridgePreferredExportOrder_ForAllBlocks()
    {
        var aliases = FlowCanvasBridge.GetBlockTypeCommandKeyAliases();
        var preferredOrderByCommand = FlowCanvasBridge.GetPreferredExportOptionOrderByCommand();
        var registryOrderByBlock = LoadRegistryBlockPropertyOrder(out _);
        var advancedKeys = LoadPropertiesPanelAdvancedKeys(out _);

        var errors = new List<string>();
        foreach (var entry in registryOrderByBlock)
        {
            if (entry.Value.Count == 0)
                continue;

            var blockType = entry.Key;
            var command = aliases.TryGetValue(blockType, out var mappedCommand)
                ? mappedCommand
                : blockType;

            if (!preferredOrderByCommand.TryGetValue(command, out var commandPreferredOrder))
            {
                errors.Add($"{blockType}: missing preferred export order for command '{command}'.");
                continue;
            }

            var expectedPanelOrder = ToPropertiesPanelDisplayOrder(entry.Value, advancedKeys);
            var actualExportOrder = commandPreferredOrder
                .Where(key => expectedPanelOrder.Contains(key, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (!expectedPanelOrder.SequenceEqual(actualExportOrder, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"{blockType} ({command}): expected [{string.Join(", ", expectedPanelOrder)}] " +
                    $"but bridge resolves [{string.Join(", ", actualExportOrder)}].");
            }
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void ExportGraphToYaml_IfWithContinueEdge_ContinuationTargetNotConsumedAsBranch()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("if-1", "if", new JObject { ["condition"] = "true" }),
                CreateBlockNode("then-1", "print", new JObject { ["message"] = "inside-then" }),
                CreateBlockNode("after-1", "print", new JObject { ["message"] = "after-if" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "if-1"),
                CreateEdge("if-1", "then-1", branchPath: "then"),
                CreateEdge("if-1", "after-1", sourceHandle: "continue"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        Assert.Equal(2, script.Steps.Count);
        var ifStep = script.Steps[0];
        Assert.Equal(StepType.If, ifStep.GetStepType());
        Assert.Single(ifStep.Then ?? new List<ScriptStep>());
        Assert.Null(ifStep.Elif);
        Assert.Null(ifStep.Else);

        var afterStep = script.Steps[1];
        Assert.Equal(StepType.Print, afterStep.GetStepType());
    }

    [Fact]
    public void ExportGraphToYaml_ForeachWithContinueEdge_ContinuationTargetNotConsumedAsDo()
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("for-1", "foreach", new JObject { ["iterator"] = "item in ${items}" }),
                CreateBlockNode("do-1", "print", new JObject { ["message"] = "inside-loop" }),
                CreateBlockNode("after-1", "print", new JObject { ["message"] = "after-loop" }),
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "for-1"),
                CreateEdge("for-1", "do-1", branchPath: "do"),
                CreateEdge("for-1", "after-1", sourceHandle: "continue"),
            }
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        Assert.Equal(2, script.Steps.Count);
        var forStep = script.Steps[0];
        Assert.Equal(StepType.Foreach, forStep.GetStepType());
        Assert.Single(forStep.Do ?? new List<ScriptStep>());

        var afterStep = script.Steps[1];
        Assert.Equal(StepType.Print, afterStep.GetStepType());
    }

    [Fact]
    public void ExportGraphToYaml_ExtractMissingFrom_ReturnsRequiredOptionError()
    {
        var result = ExportSingleBlock("extract", new JObject
        {
            ["pattern"] = "Version (.+)",
            ["into"] = "version"
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error =>
            error.Contains("missing required option(s)", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("from", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportGraphToYaml_BrowserCallbackCaptureMissingInto_ReturnsRequiredOptionError()
    {
        var result = ExportSingleBlock("browser_callback", new JObject
        {
            ["start_url"] = "https://idp.example.com/start"
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error =>
            error.Contains("missing required option(s)", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("into", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportGraphToYaml_LocalCmdCustomShellMissingShellPath_ReturnsRequiredOptionError()
    {
        var result = ExportSingleBlock("localcmd", new JObject
        {
            ["command"] = "script.py",
            ["shell"] = "custom"
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error =>
            error.Contains("missing required option(s)", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("shell_path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportGraphToYaml_InputWithoutPrompt_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("input", new JObject
        {
            ["into"] = "answer"
        });

        AssertExportSuccessWithCanonicalValidation(result);
    }

    [Fact]
    public void ExportGraphToYaml_InputWithOnError_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("input", new JObject
        {
            ["into"] = "answer",
            ["on_error"] = "continue"
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        Assert.Single(script.Steps);
        Assert.Equal(StepType.Input, script.Steps[0].GetStepType());
        Assert.Equal("continue", script.Steps[0].OnError);
    }

    [Fact]
    public void ExportGraphToYaml_ScriptPromptFontSizeOptions_AreSerializedInPropertiesPanelOrder()
    {
        var inputResult = ExportSingleBlock("input", new JObject
        {
            ["on_error"] = "continue",
            ["font_size"] = 14.5,
            ["into"] = "answer"
        });

        AssertExportSuccessWithCanonicalValidation(inputResult);
        Assert.Equal(
            new[] { "into", "font_size", "on_error" },
            GetSingleStepOptionOrder(inputResult.Yaml, "input"));

        var chooseResult = ExportSingleBlock("choose", new JObject
        {
            ["on_error"] = "continue",
            ["font_size"] = 16.5,
            ["default"] = "core",
            ["into"] = "selected",
            ["options"] = new JArray("core", "edge"),
            ["prompt"] = "Pick one",
            ["title"] = "Select interface role"
        });

        AssertExportSuccessWithCanonicalValidation(chooseResult);
        Assert.Equal(
            new[] { "title", "prompt", "options", "into", "default", "font_size", "on_error" },
            GetSingleStepOptionOrder(chooseResult.Yaml, "choose"));

        var multiselectResult = ExportSingleBlock("multiselect", new JObject
        {
            ["on_error"] = "continue",
            ["font_size"] = 18,
            ["max"] = 2,
            ["min"] = 1,
            ["into"] = "selected_list",
            ["options"] = new JArray("core", "edge"),
            ["prompt"] = "Pick interfaces",
            ["title"] = "Select interfaces"
        });

        AssertExportSuccessWithCanonicalValidation(multiselectResult);
        Assert.Equal(
            new[] { "title", "prompt", "options", "into", "min", "max", "font_size", "on_error" },
            GetSingleStepOptionOrder(multiselectResult.Yaml, "multiselect"));

        var confirmResult = ExportSingleBlock("confirm", new JObject
        {
            ["on_error"] = "continue",
            ["font_size"] = 20,
            ["default"] = true,
            ["into"] = "confirmed",
            ["prompt"] = "Proceed?",
            ["title"] = "Confirm"
        });

        AssertExportSuccessWithCanonicalValidation(confirmResult);
        Assert.Equal(
            new[] { "title", "prompt", "into", "default", "font_size", "on_error" },
            GetSingleStepOptionOrder(confirmResult.Yaml, "confirm"));
    }

    [Fact]
    public void ExportGraphToYaml_SendOptions_AreSerializedInPropertiesPanelOrder()
    {
        var result = ExportSingleBlock("send", new JObject
        {
            ["on_error"] = "continue",
            ["retry_delay"] = 3,
            ["fail_on_nonzero"] = false,
            ["command"] = "show version",
            ["expect"] = "Version",
            ["retry"] = 2,
            ["capture"] = "out",
            ["timeout"] = 30,
            ["suppress"] = true
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var optionOrder = GetSingleStepOptionOrder(result.Yaml, "send");
        Assert.Equal(
            new[] { "command", "capture", "suppress", "expect", "timeout", "retry", "retry_delay", "fail_on_nonzero", "on_error" },
            optionOrder);
    }

    [Fact]
    public void ExportGraphToYaml_ChooseOptions_AreSerializedInPropertiesPanelOrder()
    {
        var result = ExportSingleBlock("choose", new JObject
        {
            ["on_error"] = "continue",
            ["default"] = "core",
            ["into"] = "selected",
            ["options"] = new JArray("core", "edge"),
            ["prompt"] = "Pick one",
            ["title"] = "Select interface role"
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var optionOrder = GetSingleStepOptionOrder(result.Yaml, "choose");
        Assert.Equal(
            new[] { "title", "prompt", "options", "into", "default", "on_error" },
            optionOrder);
    }

    [Fact]
    public void ExportGraphToYaml_PlaySoundOptions_AreSerializedInPropertiesPanelOrder()
    {
        var result = ExportSingleBlock("playsound", new JObject
        {
            ["into"] = "${fdsaf}",
            ["max_seconds"] = 1,
            ["path"] = @"C:\Windows\Media\Alarm02.wav",
            ["volume"] = 5,
            ["on_error"] = "continue"
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var optionOrder = GetSingleStepOptionOrder(result.Yaml, "playsound");
        Assert.Equal(
            new[] { "path", "max_seconds", "into", "volume", "on_error" },
            optionOrder);
    }

    [Fact]
    public void ExportGraphToYaml_MultiselectOptions_AreSerializedInPropertiesPanelOrder()
    {
        var result = ExportSingleBlock("multiselect", new JObject
        {
            ["on_error"] = "continue",
            ["max"] = 2,
            ["min"] = 1,
            ["into"] = "selected_list",
            ["options"] = new JArray("core", "edge"),
            ["prompt"] = "Pick interfaces",
            ["title"] = "Select interfaces"
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var optionOrder = GetSingleStepOptionOrder(result.Yaml, "multiselect");
        Assert.Equal(
            new[] { "title", "prompt", "options", "into", "min", "max", "on_error" },
            optionOrder);
    }

    [Fact]
    public void ExportGraphToYaml_ConfirmOptions_AreSerializedInPropertiesPanelOrder()
    {
        var result = ExportSingleBlock("confirm", new JObject
        {
            ["on_error"] = "continue",
            ["default"] = true,
            ["into"] = "confirmed",
            ["prompt"] = "Proceed?",
            ["title"] = "Confirm"
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var optionOrder = GetSingleStepOptionOrder(result.Yaml, "confirm");
        Assert.Equal(
            new[] { "title", "prompt", "into", "default", "on_error" },
            optionOrder);
    }

    [Fact]
    public void ExportGraphToYaml_ExtractOptions_AreSerializedInPropertiesPanelOrder()
    {
        var result = ExportSingleBlock("extract", new JObject
        {
            ["required"] = true,
            ["match"] = "all",
            ["from"] = "raw",
            ["into"] = "matches",
            ["pattern"] = @"\\d+"
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var optionOrder = GetSingleStepOptionOrder(result.Yaml, "extract");
        Assert.Equal(
            new[] { "pattern", "into", "from", "match", "required" },
            optionOrder);
    }

    [Fact]
    public void ExportGraphToYaml_ChooseWithoutPrompt_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("choose", new JObject
        {
            ["into"] = "choice",
            ["options"] = new JArray("a", "b")
        });

        AssertExportSuccessWithCanonicalValidation(result);
    }

    [Fact]
    public void ExportGraphToYaml_MultiselectWithoutPrompt_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("multiselect", new JObject
        {
            ["into"] = "choices",
            ["options"] = new JArray("a", "b")
        });

        AssertExportSuccessWithCanonicalValidation(result);
    }

    [Fact]
    public void ExportGraphToYaml_ConfirmWithoutPrompt_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("confirm", new JObject
        {
            ["into"] = "confirmed"
        });

        AssertExportSuccessWithCanonicalValidation(result);
    }

    [Fact]
    public void ExportGraphToYaml_PortcheckWithoutPort_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("portcheck", new JObject
        {
            ["host"] = "127.0.0.1"
        });

        AssertExportSuccessWithCanonicalValidation(result);
    }

    [Fact]
    public void ExportGraphToYaml_WritefileWithoutContent_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("writefile", new JObject
        {
            ["path"] = "C:\\temp\\output.txt"
        });

        AssertExportSuccessWithCanonicalValidation(result);
    }

    [Fact]
    public void ExportGraphToYaml_ReadfileSelectFileWithoutPath_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("readfile", new JObject
        {
            ["select_file"] = true,
            ["into"] = "lines"
        });

        AssertExportSuccessWithCanonicalValidation(result);
    }

    [Fact]
    public void ExportGraphToYaml_ReadfilePathOnlyWithPathInto_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("readfile", new JObject
        {
            ["select_file"] = true,
            ["path_only"] = true,
            ["path_into"] = "picked_path"
        });

        AssertExportSuccessWithCanonicalValidation(result);
        Assert.Contains("path_only: true", result.Yaml, StringComparison.Ordinal);
        Assert.Contains("path_into: picked_path", result.Yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportGraphToYaml_ReadfileAutoBrowse_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("readfile", new JObject
        {
            ["select_file"] = true,
            ["autobrowse"] = true,
            ["path_only"] = true,
            ["path_into"] = "picked_path"
        });

        AssertExportSuccessWithCanonicalValidation(result);
        Assert.Contains("autobrowse: true", result.Yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportGraphToYaml_ReadfileAutoBrowseFalse_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("readfile", new JObject
        {
            ["select_file"] = true,
            ["autobrowse"] = false,
            ["path_only"] = true,
            ["path_into"] = "picked_path"
        });

        AssertExportSuccessWithCanonicalValidation(result);
        Assert.Contains("autobrowse: false", result.Yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportGraphToYaml_HttpBasicAuthWithoutUsername_ReturnsRequiredOptionError()
    {
        var result = ExportSingleBlock("http", new JObject
        {
            ["url"] = "https://api.example.com",
            ["auth"] = "basic",
            ["password"] = "secret"
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error =>
            error.Contains("username", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportGraphToYaml_HttpBearerAuthWithoutToken_ReturnsRequiredOptionError()
    {
        var result = ExportSingleBlock("http", new JObject
        {
            ["url"] = "https://api.example.com",
            ["auth"] = "bearer"
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error =>
            error.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportGraphToYaml_InteractiveHeadlessWithoutCommand_ReturnsRequiredOptionError()
    {
        var result = ExportSingleBlock("interactive", new JObject
        {
            ["show_window"] = false,
            ["max_seconds"] = 30
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error =>
            error.Contains("command", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportGraphToYaml_InteractiveHeadlessWithoutLimiter_ReturnsRequiredOptionError()
    {
        var result = ExportSingleBlock("interactive", new JObject
        {
            ["show_window"] = false,
            ["command"] = "show version"
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error =>
            error.Contains("max_seconds", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("max_lines", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportGraphToYaml_InteractiveHeadlessWithCommandAndLimiter_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("interactive", new JObject
        {
            ["show_window"] = false,
            ["command"] = "show version",
            ["max_seconds"] = 30
        });

        AssertExportSuccessWithCanonicalValidation(result);
    }

    [Fact]
    public void ExportGraphToYaml_LocalCmdArgsJsonArray_ExportsAsSequence()
    {
        var result = ExportSingleBlock("localcmd", new JObject
        {
            ["command"] = "dotnet build",
            ["args"] = "[\"-NoProfile\",\"-ExecutionPolicy\",\"Bypass\"]",
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var localCmd = script.Steps[0].LocalCmd;

        Assert.NotNull(localCmd);
        Assert.Equal(3, localCmd!.Args.Count);
        Assert.Equal("-NoProfile", localCmd.Args[0]);
        Assert.Equal("Bypass", localCmd.Args[2]);
    }

    [Fact]
    public void ExportGraphToYaml_LocalCmdEnvJson_ExportsAsMapping()
    {
        var result = ExportSingleBlock("localcmd", new JObject
        {
            ["command"] = "dotnet build",
            ["env"] = "{\"CONFIGURATION\":\"Release\",\"DOTNET_CLI_TELEMETRY_OPTOUT\":\"1\"}",
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var localCmd = script.Steps[0].LocalCmd;

        Assert.NotNull(localCmd);
        Assert.NotNull(localCmd!.Env);
        Assert.Equal("Release", localCmd.Env!["CONFIGURATION"]);
        Assert.Equal("1", localCmd.Env["DOTNET_CLI_TELEMETRY_OPTOUT"]);
    }

    [Fact]
    public void ExportGraphToYaml_LocalCmdQuietAndSuppress_ExportsAsBooleans()
    {
        var result = ExportSingleBlock("localcmd", new JObject
        {
            ["command"] = "date",
            ["quiet"] = true,
            ["suppress"] = true,
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var localCmd = script.Steps[0].LocalCmd;

        Assert.NotNull(localCmd);
        Assert.True(localCmd!.Quiet);
        Assert.True(localCmd.Suppress);
    }

    [Fact]
    public void ExportGraphToYaml_LocalCmdKeepOpen_ExportsAsBoolean()
    {
        var result = ExportSingleBlock("localcmd", new JObject
        {
            ["command"] = "date",
            ["interactive"] = true,
            ["keep_open"] = true,
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var localCmd = script.Steps[0].LocalCmd;

        Assert.NotNull(localCmd);
        Assert.True(localCmd!.Interactive);
        Assert.True(localCmd.KeepOpen);
    }

    [Fact]
    public void ExportGraphToYaml_LocalCmdCmdShell_ExportsSuccessfully()
    {
        var result = ExportSingleBlock("localcmd", new JObject
        {
            ["command"] = "dir",
            ["shell"] = "cmd",
        });

        AssertExportSuccessWithCanonicalValidation(result);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var localCmd = script.Steps[0].LocalCmd;

        Assert.NotNull(localCmd);
        Assert.Equal("cmd", localCmd!.Shell);
    }

    [Fact]
    public void Registry_LocalCmdShellOptions_ExcludePwshAndCmd()
    {
        _ = LoadRegistryBlockPropertyOrder(out var registryText);

        var blockMatch = Regex.Match(
            registryText,
            @"type:\s*'localcmd'[\s\S]*?\{\s*key:\s*'shell'[\s\S]*?options:\s*\[(?<options>[^\]]+)\]",
            RegexOptions.Multiline);

        Assert.True(blockMatch.Success, "Unable to find localcmd shell options in registry.ts.");
        var optionsText = blockMatch.Groups["options"].Value;
        Assert.Contains("'powershell'", optionsText, StringComparison.Ordinal);
        Assert.DoesNotContain("'pwsh'", optionsText, StringComparison.Ordinal);
        Assert.DoesNotContain("'cmd'", optionsText, StringComparison.Ordinal);
        Assert.Contains("'custom'", optionsText, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_SetHistoryLabelAndNotifyBlocks_ExposeExpectedPropertySurface()
    {
        var registryBlocks = LoadRegistryBlockPropertyOrder(out _);

        Assert.True(
            registryBlocks.ContainsKey("sethistorylabel"),
            "Flow Canvas registry is missing a sethistorylabel block.");
        Assert.Equal(
            new[] { "value", "replace", "mode", "separator" },
            registryBlocks["sethistorylabel"]);
        Assert.Equal(
            new[] { "profile", "channel", "title", "message", "level", "mention", "attachments", "into", "on_error" },
            registryBlocks["notify"]);
    }

    [Fact]
    public void Registry_ScriptPromptBlocks_ExposeFontSizeInAdvancedPanel()
    {
        var registryBlocks = LoadRegistryBlockPropertyOrder(out _);
        var advancedKeys = LoadPropertiesPanelAdvancedKeys(out _);

        Assert.Contains("font_size", advancedKeys);
        Assert.Equal(
            new[] { "title", "prompt", "into", "default", "password", "validate", "validation_error", "font_size", "on_error" },
            ToPropertiesPanelDisplayOrder(registryBlocks["input"], advancedKeys));
        Assert.Equal(
            new[] { "title", "prompt", "options", "into", "default", "font_size", "on_error" },
            ToPropertiesPanelDisplayOrder(registryBlocks["choose"], advancedKeys));
        Assert.Equal(
            new[] { "title", "prompt", "options", "into", "min", "max", "font_size", "on_error" },
            ToPropertiesPanelDisplayOrder(registryBlocks["multiselect"], advancedKeys));
        Assert.Equal(
            new[] { "title", "prompt", "into", "default", "font_size", "on_error" },
            ToPropertiesPanelDisplayOrder(registryBlocks["confirm"], advancedKeys));
    }

    [Fact]
    public void TextToGraph_LocalCmdInteractiveDetached_PreservesExplicitLifetimeProp()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "date"
                  interactive: true
                  lifetime: detached
            """;

        var (nodes, _) = bridge.TextToGraph(yaml);

        var localCmdNode = nodes
            .OfType<JObject>()
            .Single(node => string.Equals(
                node["data"]?["blockType"]?.ToString(),
                "localcmd",
                StringComparison.OrdinalIgnoreCase));

        var props = localCmdNode["data"]?["props"] as JObject;
        Assert.NotNull(props);
        Assert.Equal("detached", props!["lifetime"]?.ToString());
    }

    [Fact]
    public void TextToGraph_ReadfilePathOnly_ImportsPathCaptureProps()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            steps:
              - readfile:
                  select_file: true
                  path_only: true
                  path_into: chosen_path
            """;

        var (nodes, _) = bridge.TextToGraph(yaml);

        var readfileNode = nodes
            .OfType<JObject>()
            .Single(node => string.Equals(
                node["data"]?["blockType"]?.ToString(),
                "readfile",
                StringComparison.OrdinalIgnoreCase));

        var props = readfileNode["data"]?["props"] as JObject;
        Assert.NotNull(props);
        Assert.Equal(true, props!["select_file"]?.Value<bool>());
        Assert.Equal(true, props["path_only"]?.Value<bool>());
        Assert.Equal("chosen_path", props["path_into"]?.ToString());
    }

    [Fact]
    public void TextToGraph_ReadfileAutoBrowse_ImportsAutoBrowseProp()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            steps:
              - readfile:
                  select_file: true
                  autobrowse: true
                  path_only: true
                  path_into: chosen_path
            """;

        var (nodes, _) = bridge.TextToGraph(yaml);

        var readfileNode = nodes
            .OfType<JObject>()
            .Single(node => string.Equals(
                node["data"]?["blockType"]?.ToString(),
                "readfile",
                StringComparison.OrdinalIgnoreCase));

        var props = readfileNode["data"]?["props"] as JObject;
        Assert.NotNull(props);
        Assert.Equal(true, props!["autobrowse"]?.Value<bool>());
    }

    [Fact]
    public void TextToGraph_ReadfileAutoBrowseFalse_ImportsExplicitFalseProp()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            steps:
              - readfile:
                  select_file: true
                  autobrowse: false
                  path_only: true
                  path_into: chosen_path
            """;

        var (nodes, _) = bridge.TextToGraph(yaml);

        var readfileNode = nodes
            .OfType<JObject>()
            .Single(node => string.Equals(
                node["data"]?["blockType"]?.ToString(),
                "readfile",
                StringComparison.OrdinalIgnoreCase));

        var props = readfileNode["data"]?["props"] as JObject;
        Assert.NotNull(props);
        Assert.NotNull(props!["autobrowse"]);
        Assert.Equal(false, props["autobrowse"]?.Value<bool>());
    }

    [Fact]
    public void TextToGraph_ScriptPromptSteps_ImportFontSizeIntoProps()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            ---
            steps:
              - input:
                  into: answer
                  font_size: 14.5
              - choose:
                  into: choice
                  options: [core, edge]
                  font_size: 15
              - multiselect:
                  into: choices
                  options: [core, edge]
                  font_size: 16
              - confirm:
                  into: confirmed
                  font_size: 17.5
            """;

        var (nodes, _) = bridge.TextToGraph(yaml);

        JObject GetProps(string blockType)
        {
            var node = nodes
                .OfType<JObject>()
                .Single(item => string.Equals(
                    item["data"]?["blockType"]?.ToString(),
                    blockType,
                    StringComparison.OrdinalIgnoreCase));

            return Assert.IsType<JObject>(node["data"]?["props"]);
        }

        Assert.Equal("14.5", GetProps("input")["font_size"]?.ToString());
        Assert.Equal("15", GetProps("choose")["font_size"]?.ToString());
        Assert.Equal("16", GetProps("multiselect")["font_size"]?.ToString());
        Assert.Equal("17.5", GetProps("confirm")["font_size"]?.ToString());
    }

    [Fact]
    public void ImportExportRoundTrip_LocalCmdInteractiveDetached_PreservesExplicitLifetime()
    {
        var result = RoundTripThroughBridge(
            """
            ---
            steps:
              - localcmd:
                  command: "date"
                  interactive: true
                  lifetime: detached
            """);

        AssertExportSuccessWithCanonicalValidation(result);
        Assert.Contains("lifetime: detached", result.Yaml, StringComparison.Ordinal);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        Assert.NotNull(script.Steps[0].LocalCmd);
        Assert.True(script.Steps[0].LocalCmd!.LifetimeSpecified);
    }

    [Fact]
    public void ImportExportRoundTrip_SetHistoryLabelScalar_PreservesEditableValue()
    {
        var result = RoundTripThroughBridge(
            """
            ---
            steps:
              - sethistorylabel: Core Router
            """);

        AssertExportSuccessWithCanonicalValidation(result);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        Assert.Single(script.Steps);
        Assert.Equal(StepType.SetHistoryLabel, script.Steps[0].GetStepType());
        switch (script.Steps[0].SetHistoryLabel)
        {
            case string scalarValue:
                Assert.Equal("Core Router", scalarValue);
                break;
            case SetHistoryLabelOptions options:
                Assert.Equal("Core Router", options.Value);
                break;
            default:
                throw new Xunit.Sdk.XunitException("sethistorylabel value was not preserved through the Flow Canvas bridge.");
        }
    }

    [Fact]
    public void ImportExportRoundTrip_ChooseLabelValueOptions_PreservesLabelValuePairs()
    {
        var result = RoundTripThroughBridge(
            """
            steps:
              - choose:
                  prompt: "Pick one"
                  into: selected
                  options:
                    - label: "WAN 1"
                      value: "wan1"
                    - label: "WAN 2"
                      value: "wan2"
            """);

        AssertExportSuccessWithCanonicalValidation(result);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var choose = script.Steps[0].Choose;

        Assert.NotNull(choose);
        Assert.Equal(2, choose!.Options.Count);
        Assert.Equal("WAN 1", choose.Options[0].Label);
        Assert.Equal("wan1", choose.Options[0].Value);
        Assert.Equal("WAN 2", choose.Options[1].Label);
        Assert.Equal("wan2", choose.Options[1].Value);
        Assert.True(string.IsNullOrWhiteSpace(choose.OptionsFrom));
    }

    [Fact]
    public void ImportExportRoundTrip_MultiselectLabelValueOptions_PreservesLabelValuePairs()
    {
        var result = RoundTripThroughBridge(
            """
            steps:
              - multiselect:
                  prompt: "Pick many"
                  into: selected
                  options:
                    - label: "Core"
                      value: "core"
                    - label: "Edge"
                      value: "edge"
            """);

        AssertExportSuccessWithCanonicalValidation(result);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var options = script.Steps[0].Multiselect;

        Assert.NotNull(options);
        Assert.Equal(2, options!.Options.Count);
        Assert.Equal("Core", options.Options[0].Label);
        Assert.Equal("core", options.Options[0].Value);
        Assert.Equal("Edge", options.Options[1].Label);
        Assert.Equal("edge", options.Options[1].Value);
        Assert.True(string.IsNullOrWhiteSpace(options.OptionsFrom));
    }

    [Fact]
    public void ImportExportRoundTrip_ChooseOptionsSourceScalar_PreservesSource()
    {
        var result = RoundTripThroughBridge(
            """
            steps:
              - choose:
                  prompt: "Pick one"
                  into: selected
                  options: ${interface_list}
            """);

        AssertExportSuccessWithCanonicalValidation(result);

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var choose = script.Steps[0].Choose;

        Assert.NotNull(choose);
        Assert.Empty(choose!.Options);
        Assert.Equal("${interface_list}", choose.OptionsFrom);
    }

    [Fact]
    public void RoundTrip_LeadingAndInlineComments_ArePreserved()
    {
        var yaml = "steps:\n  # Create the address object\n  - send:\n      command: cfg  # needs vdom\n";
        var result = RoundTripThroughBridge(yaml);

        Assert.True(result.Success, string.Join(" | ", result.Errors));
        Assert.Contains("# Create the address object", result.Yaml);
        Assert.Contains("# needs vdom", result.Yaml);
        Assert.Equal(1, CountOccurrences(result.Yaml, "# needs vdom"));
    }

    [Fact]
    public void Export_ContainerRegeneration_EmitsLeadingComment()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = "steps:\n  # guard the loop\n  - foreach:\n      iterator: h in ${hosts}\n      do:\n        - print:\n            message: ${h}\n";
        var (nodes, edges) = bridge.TextToGraph(yaml);
        foreach (var n in nodes.OfType<JObject>())
        {
            var props = n["data"]?["props"] as JObject;
            if (props != null && props["_yamlSnippet"] != null) props["_forceGraphExport"] = true;
        }
        var result = bridge.ExportGraphToYaml(new JObject { ["nodes"] = nodes, ["edges"] = edges });
        Assert.True(result.Success, string.Join(" | ", result.Errors));
        Assert.Contains("# guard the loop", result.Yaml);
    }

    [Fact]
    public void RoundTrip_QuotedHashInValue_NotStripped()
    {
        var yaml = "steps:\n  - send:\n      command: \"echo #1\"\n";
        var result = RoundTripThroughBridge(yaml);

        Assert.True(result.Success, string.Join(" | ", result.Errors));
        Assert.Contains("#1", result.Yaml);
    }

    [Fact]
    public void RoundTrip_HashNotPrecededByWhitespace_NotStripped()
    {
        var yaml = "steps:\n  - send:\n      command: v1#2\n";
        var result = RoundTripThroughBridge(yaml);

        Assert.True(result.Success, string.Join(" | ", result.Errors));
        Assert.Contains("v1#2", result.Yaml);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, System.StringComparison.Ordinal)) >= 0)
        { count++; idx += needle.Length; }
        return count;
    }

    private static FlowCanvasBridge.FlowCanvasExportResult RoundTripThroughBridge(string yaml)
    {
        var bridge = new FlowCanvasBridge();
        var (nodes, edges) = bridge.TextToGraph(yaml);
        var graph = new JObject
        {
            ["nodes"] = nodes,
            ["edges"] = edges
        };

        return bridge.ExportGraphToYaml(graph);
    }

    private static FlowCanvasBridge.FlowCanvasExportResult ExportSingleBlock(string blockType, JObject? props = null)
    {
        var bridge = new FlowCanvasBridge();
        var graph = new JObject
        {
            ["nodes"] = new JArray
            {
                CreateStartNode(),
                CreateBlockNode("node-1", blockType, props ?? new JObject())
            },
            ["edges"] = new JArray
            {
                CreateEdge("__start__", "node-1")
            }
        };

        return bridge.ExportGraphToYaml(graph);
    }

    private static void AssertExportSuccessWithCanonicalValidation(FlowCanvasBridge.FlowCanvasExportResult result)
    {
        Assert.True(result.Success, string.Join(" | ", result.Errors));
        Assert.False(string.IsNullOrWhiteSpace(result.Yaml));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);
    }

    private static IReadOnlyList<string> GetSingleStepOptionOrder(string yaml, string commandKey)
    {
        var deserializer = new DeserializerBuilder().Build();
        var root = deserializer.Deserialize<Dictionary<object, object?>>(yaml);

        Assert.True(
            TryGetCaseInsensitiveDictionaryValue(root, "steps", out var stepsObj),
            "YAML does not contain a steps section.");
        var steps = Assert.IsAssignableFrom<IList>(stepsObj);
        Assert.NotEmpty(steps);

        var stepMap = Assert.IsAssignableFrom<IDictionary>(steps[0]);
        object? commandValue = null;
        foreach (DictionaryEntry entry in stepMap)
        {
            if (string.Equals(entry.Key?.ToString(), commandKey, StringComparison.OrdinalIgnoreCase))
            {
                commandValue = entry.Value;
                break;
            }
        }

        Assert.NotNull(commandValue);
        var optionMap = Assert.IsAssignableFrom<IDictionary>(commandValue);
        var keys = new List<string>();
        foreach (DictionaryEntry entry in optionMap)
        {
            if (entry.Key is string key && !string.IsNullOrWhiteSpace(key))
                keys.Add(key);
        }

        return keys;
    }

    private static bool TryGetCaseInsensitiveDictionaryValue(
        IDictionary<object, object?> dictionary,
        string key,
        out object? value)
    {
        foreach (var pair in dictionary)
        {
            if (pair.Key is string pairKey &&
                string.Equals(pairKey, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static IReadOnlyList<(string Name, string Commands)> LoadQaYamlPresets()
    {
        var repoRoot = FindRepositoryRoot();
        var qaPresetsPath = Path.Combine(repoRoot, "qa_presets.json");
        using var document = JsonDocument.Parse(File.ReadAllText(qaPresetsPath));

        var presets = new List<(string Name, string Commands)>();
        if (!document.RootElement.TryGetProperty("presets", out var presetsElement))
            return presets;

        foreach (var property in presetsElement.EnumerateObject())
        {
            if (!property.Value.TryGetProperty("commands", out var commandsElement) ||
                commandsElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var commands = commandsElement.GetString() ?? string.Empty;
            if (!ScriptParser.IsYamlScript(commands))
                continue;

            presets.Add((property.Name, commands));
        }

        return presets;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? search = new(AppContext.BaseDirectory);
        while (search != null && !File.Exists(Path.Combine(search.FullName, "qa_presets.json")))
        {
            search = search.Parent;
        }

        Assert.NotNull(search);
        return search!.FullName;
    }

    private static IReadOnlyDictionary<string, HashSet<string>> LoadRegistryBlockPropertyKeys(out string registryText)
    {
        var orderedProperties = LoadRegistryBlockPropertyOrder(out registryText);
        return orderedProperties.ToDictionary(
            pair => pair.Key,
            pair => new HashSet<string>(pair.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadRegistryBlockPropertyOrder(out string registryText)
    {
        var repoRoot = FindRepositoryRoot();
        var registryPath = Path.Combine(repoRoot, "FlowCanvas", "src", "blockDefs", "registry.ts");
        registryText = File.ReadAllText(registryPath);
        var lines = registryText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var blockProperties = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentBlockType = null;
        var inProperties = false;
        var bracketDepth = 0;
        var inBlockDefs = false;

        foreach (var line in lines)
        {
            if (!inBlockDefs)
            {
                if (line.Contains("export const blockDefs", StringComparison.Ordinal))
                    inBlockDefs = true;
                continue;
            }

            if (line.Trim() == "];")
                break;

            if (!inProperties)
            {
                var typeMatch = Regex.Match(line, @"^\s*(?:\{\s*)?type:\s*'(?<type>[^']+)'\s*,");
                if (typeMatch.Success)
                {
                    currentBlockType = typeMatch.Groups["type"].Value;
                    if (!blockProperties.ContainsKey(currentBlockType))
                        blockProperties[currentBlockType] = new List<string>();
                }

                if (currentBlockType != null && line.Contains("properties:", StringComparison.Ordinal))
                {
                    inProperties = true;
                    bracketDepth = CountChar(line, '[') - CountChar(line, ']');
                    AppendRegistryPropertyKeysFromLine(line, blockProperties[currentBlockType]);
                    if (bracketDepth <= 0)
                        inProperties = false;
                }

                continue;
            }

            AppendRegistryPropertyKeysFromLine(line, blockProperties[currentBlockType!]);

            bracketDepth += CountChar(line, '[') - CountChar(line, ']');
            if (bracketDepth <= 0)
                inProperties = false;
        }

        return blockProperties.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static void AppendRegistryPropertyKeysFromLine(string line, List<string> keys)
    {
        foreach (Match keyMatch in Regex.Matches(line, @"key:\s*'(?<key>[^']+)'"))
        {
            AddPropertyKeyIfMissing(keys, keyMatch.Groups["key"].Value);
        }

        if (line.Contains("timeoutProp", StringComparison.Ordinal))
            AddPropertyKeyIfMissing(keys, "timeout");

        if (line.Contains("onErrorProp", StringComparison.Ordinal))
            AddPropertyKeyIfMissing(keys, "on_error");
    }

    private static void AddPropertyKeyIfMissing(List<string> keys, string key)
    {
        if (!keys.Contains(key, StringComparer.OrdinalIgnoreCase))
            keys.Add(key);
    }

    private static HashSet<string> LoadPropertiesPanelAdvancedKeys(out string propertiesText)
    {
        var repoRoot = FindRepositoryRoot();
        var propertiesPath = Path.Combine(repoRoot, "FlowCanvas", "src", "panels", "Properties.tsx");
        propertiesText = File.ReadAllText(propertiesPath);

        var advancedSetMatch = Regex.Match(
            propertiesText,
            @"const\s+ADVANCED_PROPERTY_KEYS\s*=\s*new Set\(\[(?<values>[\s\S]*?)\]\);",
            RegexOptions.Multiline);
        Assert.True(advancedSetMatch.Success, "Unable to find ADVANCED_PROPERTY_KEYS in Properties.tsx.");

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match keyMatch in Regex.Matches(advancedSetMatch.Groups["values"].Value, @"'([^']+)'"))
            keys.Add(keyMatch.Groups[1].Value);

        return keys;
    }

    private static IReadOnlyList<string> ToPropertiesPanelDisplayOrder(
        IReadOnlyList<string> propertyOrder,
        ISet<string> advancedKeys)
    {
        var core = new List<string>();
        var advanced = new List<string>();
        var onError = new List<string>();

        foreach (var key in propertyOrder)
        {
            if (string.Equals(key, "on_error", StringComparison.OrdinalIgnoreCase))
            {
                onError.Add(key);
                continue;
            }

            if (advancedKeys.Contains(key))
            {
                advanced.Add(key);
                continue;
            }

            core.Add(key);
        }

        var ordered = new List<string>(core.Count + advanced.Count + onError.Count);
        ordered.AddRange(core);
        ordered.AddRange(advanced);
        ordered.AddRange(onError);
        return ordered;
    }

    private static int CountChar(string value, char ch)
    {
        var count = 0;
        foreach (var current in value)
        {
            if (current == ch)
                count++;
        }

        return count;
    }

    private static JObject CreateStartNode()
    {
        return new JObject
        {
            ["id"] = "__start__",
            ["type"] = "start",
            ["position"] = new JObject { ["x"] = 0, ["y"] = 0 },
            ["data"] = new JObject
            {
                ["blockType"] = "_start",
                ["label"] = "Start",
                ["props"] = new JObject()
            }
        };
    }

    private static JObject CreateBlockNode(string id, string blockType, JObject? props = null)
    {
        return new JObject
        {
            ["id"] = id,
            ["type"] = "block",
            ["position"] = new JObject { ["x"] = 0, ["y"] = 0 },
            ["data"] = new JObject
            {
                ["blockType"] = blockType,
                ["label"] = blockType,
                ["props"] = props ?? new JObject()
            }
        };
    }

    private static JObject CreateVisualChildNode(
        string id,
        string blockType,
        string parentId,
        string branchLabel,
        JObject? props = null)
    {
        var visualProps = new JObject
        {
            ["_isChildOf"] = parentId,
            ["_branchLabel"] = branchLabel,
        };

        if (props != null)
        {
            foreach (var property in props.Properties())
                visualProps[property.Name] = property.Value;
        }

        return CreateBlockNode(id, blockType, visualProps);
    }

    [Fact]
    public void TextToGraph_IfWithThenAndElse_CreatesContinuationEdgeFromContainer()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            steps:
              - if:
                  condition: "true"
                  then:
                    - print: "a"
                  else:
                    - print: "b"
              - print: "after"
            """;

        var (nodes, edges) = bridge.TextToGraph(yaml);

        // Find the IF node and the "after" print node
        var ifNode = nodes.Cast<JObject>().First(n => n["data"]?["blockType"]?.ToString() == "if");
        var afterNode = nodes.Cast<JObject>().First(n =>
            n["data"]?["blockType"]?.ToString() == "print" &&
            n["data"]?["props"]?["_isChildOf"] == null &&
            (n["data"]?["props"]?["_preview"]?.ToString() == "after" ||
             n["data"]?["props"]?["message"]?.ToString() == "after"));

        var ifId = ifNode["id"]!.ToString();
        var afterId = afterNode["id"]!.ToString();

        // There should be exactly one edge from IF to after, using sourceHandle="continue"
        var continueEdges = edges.Cast<JObject>().Where(e =>
            e["source"]?.ToString() == ifId &&
            e["target"]?.ToString() == afterId).ToList();

        Assert.Single(continueEdges);
        Assert.Equal("continue", continueEdges[0]["sourceHandle"]?.ToString());

        // The edge should NOT be dashed (no strokeDasharray)
        var style = continueEdges[0]["style"] as JObject;
        Assert.Null(style?["strokeDasharray"]);
    }

    [Fact]
    public void ImportExportRoundTrip_IfWithContinuation_ProducesValidYaml()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            steps:
              - if:
                  condition: "${x} > 0"
                  then:
                    - print: "positive"
                  else:
                    - print: "non-positive"
              - print: "done"
            """;

        // Import
        var (nodes, edges) = bridge.TextToGraph(yaml);
        var graph = new JObject { ["nodes"] = nodes, ["edges"] = edges };

        // Export
        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        // Re-parse and validate
        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        // Should have IF + print at top level
        Assert.Equal(2, script.Steps.Count);
        Assert.Equal(StepType.If, script.Steps[0].GetStepType());
        Assert.Equal(StepType.Print, script.Steps[1].GetStepType());
    }

    [Fact]
    public void ExportGraphToYaml_ImportedIfWithDeletedElseEdge_RegeneratesWithoutElse()
    {
        var bridge = new FlowCanvasBridge();

        // Import a YAML with if/then/else
        var yaml = """
            steps:
              - if:
                  condition: "true"
                  then:
                    - print: "then-branch"
                  else:
                    - print: "else-branch"
              - print: "after"
            """;

        var (nodes, edges) = bridge.TextToGraph(yaml);

        // Find the IF node
        var ifNode = nodes.Cast<JObject>().First(n => n["data"]?["blockType"]?.ToString() == "if");
        var ifId = ifNode["id"]!.ToString();

        // Delete the else edge (the edge from the IF node to the else child)
        var elseChildNode = nodes.Cast<JObject>().FirstOrDefault(n =>
        {
            var props = n["data"]?["props"] as JObject;
            return props?["_isChildOf"]?.ToString() == ifId &&
                   props?["_branchLabel"]?.ToString() == "else";
        });

        Assert.NotNull(elseChildNode);
        var elseChildId = elseChildNode["id"]!.ToString();

        // Remove only the edge connecting IF to the else child (user deletes edge in UI)
        var filteredEdges = new JArray(edges.Cast<JObject>().Where(e =>
            !(e["source"]?.ToString() == ifId && e["target"]?.ToString() == elseChildId)));

        var graph = new JObject { ["nodes"] = nodes, ["edges"] = filteredEdges };

        // Export — should regenerate from graph, NOT use stale snippet with else
        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));

        var parser = new ScriptParser();
        var script = parser.Parse(result.Yaml);
        var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
        Assert.Empty(errors);

        // IF should have then but NO else
        var ifStep = script.Steps[0];
        Assert.Equal(StepType.If, ifStep.GetStepType());
        Assert.NotNull(ifStep.Then);
        Assert.Single(ifStep.Then);
        Assert.Null(ifStep.Else);
    }

    [Fact]
    public void ExportGraphToYaml_ImportedIfWithTwoDigitThenIndex_UsesStoredSnippetWhenFirstChildEdgeExists()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = """
            steps:
              - if:
                  condition: "true"
                  then:
                    - print: "first"
                    - print: "second"
              - print: "after"
            """;

        var (nodes, edges) = bridge.TextToGraph(yaml);

        var ifNode = nodes.Cast<JObject>().First(n => n["data"]?["blockType"]?.ToString() == "if");
        var ifId = ifNode["id"]!.ToString();

        var thenChildren = nodes.Cast<JObject>()
            .Where(n =>
            {
                var props = n["data"]?["props"] as JObject;
                return props?["_isChildOf"]?.ToString() == ifId &&
                       string.Equals(props?["_branchLabel"]?.ToString(), "then", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        Assert.Equal(2, thenChildren.Count);

        var directThenChildIds = edges.Cast<JObject>()
            .Where(e => e["source"]?.ToString() == ifId &&
                        thenChildren.Any(c => c["id"]?.ToString() == e["target"]?.ToString()))
            .Select(e => e["target"]!.ToString())
            .ToList();

        Assert.Single(directThenChildIds);
        var directThenChildId = directThenChildIds[0];

        foreach (var child in thenChildren)
        {
            var childId = child["id"]!.ToString();
            var props = (JObject)child["data"]!["props"]!;
            props["_stepPath"] = childId == directThenChildId ? "steps/0/then/2" : "steps/0/then/10";
        }

        var ifProps = (JObject)ifNode["data"]!["props"]!;
        ifProps["_stepPath"] = "steps/0";
        ifProps["_yamlSnippet"] = """
            - if:
                condition: "true"
                then:
                  # keep-imported-snippet
                  - print: "first"
                  - print: "second"
            """;

        var graph = new JObject
        {
            ["nodes"] = nodes,
            ["edges"] = edges
        };

        var result = bridge.ExportGraphToYaml(graph);
        Assert.True(result.Success, string.Join(" | ", result.Errors));
        Assert.Contains("# keep-imported-snippet", result.Yaml);
    }

    private static JObject CreateEdge(
        string source,
        string target,
        string? sourceHandle = null,
        string? branchPath = null,
        string? condition = null,
        string? caseValue = null)
    {
        var edge = new JObject
        {
            ["id"] = $"e-{source}-{target}-{Guid.NewGuid():N}",
            ["source"] = source,
            ["target"] = target,
        };

        if (!string.IsNullOrWhiteSpace(sourceHandle))
            edge["sourceHandle"] = sourceHandle;

        var data = new JObject();
        if (!string.IsNullOrWhiteSpace(branchPath))
            data["branchPath"] = branchPath;
        if (!string.IsNullOrWhiteSpace(condition))
            data["condition"] = condition;
        if (!string.IsNullOrWhiteSpace(caseValue))
            data["caseValue"] = caseValue;

        if (data.Properties().Any())
            edge["data"] = data;

        return edge;
    }
}

public class FlowCanvasBridgeSplitYamlStepsTests
{
    [Fact]
    public void SplitYamlSteps_LeadingCommentAttachesToNextStep_AndIsStrippedFromSnippet()
    {
        var yaml = "steps:\n  # Get hostname\n  - send:\n      command: hostname\n";
        var steps = FlowCanvasBridge.SplitYamlSteps(yaml);

        Assert.Single(steps);
        Assert.Equal(new[] { "Get hostname" }, steps[0].LeadingComments);
        Assert.DoesNotContain("#", steps[0].Snippet);
        Assert.Contains("- send:", steps[0].Snippet);
    }

    [Fact]
    public void SplitYamlSteps_InlineComment_IsCapturedAndStripped()
    {
        var yaml = "steps:\n  - send:\n      command: cfg  # needs vdom\n";
        var steps = FlowCanvasBridge.SplitYamlSteps(yaml);

        Assert.Single(steps);
        Assert.Equal("needs vdom", steps[0].InlineComment);
        Assert.DoesNotContain("needs vdom", steps[0].Snippet);
    }

    [Fact]
    public void SplitYamlSteps_HashInsideQuotes_IsNotTreatedAsComment()
    {
        var yaml = "steps:\n  - send:\n      command: \"echo #1\"\n";
        var steps = FlowCanvasBridge.SplitYamlSteps(yaml);

        Assert.Single(steps);
        Assert.Null(steps[0].InlineComment);
        Assert.Contains("#1", steps[0].Snippet);
    }

    [Fact]
    public void SplitYamlSteps_BareTrailingHash_IsNotTreatedAsComment()
    {
        var yaml = "steps:\n  - send:\n      command: cfg  #\n";
        var steps = FlowCanvasBridge.SplitYamlSteps(yaml);
        Assert.Single(steps);
        Assert.Null(steps[0].InlineComment);
        Assert.Contains("#", steps[0].Snippet);
    }

    [Fact]
    public void TextToGraph_LeadingComment_EmitsAnchoredCommentNode()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = "steps:\n  # Get hostname\n  - send:\n      command: hostname\n";

        var (nodes, _) = bridge.TextToGraph(yaml);

        var comment = nodes.OfType<JObject>().FirstOrDefault(n =>
            n["data"]?["blockType"]?.ToString() == "comment");
        Assert.NotNull(comment);
        Assert.Equal("comment", comment!["data"]?["kind"]?.ToString());
        Assert.Equal("Get hostname", comment["data"]?["text"]?.ToString());
        Assert.Equal("leading", comment["data"]?["anchor"]?["type"]?.ToString());
        Assert.Equal("steps/0", comment["data"]?["anchor"]?["stepPath"]?.ToString());
    }

    [Fact]
    public void TextToGraph_HeaderComment_EmitsHeaderAnchoredNode()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = "# File header note\nname: demo\nsteps:\n  - send:\n      command: hostname\n";
        var (nodes, _) = bridge.TextToGraph(yaml);
        var header = nodes.OfType<JObject>().FirstOrDefault(n =>
            n["data"]?["anchor"]?["type"]?.ToString() == "header");
        Assert.NotNull(header);
        Assert.Equal("File header note", header!["data"]?["text"]?.ToString());
    }

    [Fact]
    public void TextToGraph_InlineComment_EmitsInlineAnchoredNode()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = "steps:\n  - send:\n      command: hostname  # note\n";
        var (nodes, _) = bridge.TextToGraph(yaml);
        var inline = nodes.OfType<JObject>().FirstOrDefault(n =>
            n["data"]?["anchor"]?["type"]?.ToString() == "inline");
        Assert.NotNull(inline);
        Assert.Equal("note", inline!["data"]?["text"]?.ToString());
        Assert.Equal("steps/0", inline["data"]?["anchor"]?["stepPath"]?.ToString());
    }

    [Fact]
    public void TextToGraph_CommentOnlyPreambleNoSteps_DoesNotWireStartToComment()
    {
        var bridge = new FlowCanvasBridge();
        var yaml = "# just a note\nsteps:\n";
        var (_, edges) = bridge.TextToGraph(yaml);
        foreach (var e in edges.OfType<JObject>())
        {
            var target = e["target"]?.ToString();
            Assert.False(target != null && target.StartsWith("comment-"),
                $"start wired to a comment node: {target}");
        }
    }
}
