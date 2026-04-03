using System;
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

namespace SSH_Helper.Tests.Services;

public class FlowCanvasBridgeTests
{
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
    public void ExportGraphToYaml_CommentNodes_AreIgnoredWithWarning()
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
        Assert.Contains(result.Warnings, w => w.Contains("Comment nodes are ignored", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("comment-1", result.NodeToStepPathMap.Keys);
        Assert.Contains("node-1", result.NodeToStepPathMap.Keys);
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
        var repoRoot = FindRepositoryRoot();
        var registryPath = Path.Combine(repoRoot, "FlowCanvas", "src", "blockDefs", "registry.ts");
        registryText = File.ReadAllText(registryPath);
        var lines = registryText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var blockProperties = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
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

            var typeMatch = Regex.Match(line, @"^\s*(?:\{\s*)?type:\s*'(?<type>[^']+)'\s*,");
            if (typeMatch.Success)
            {
                currentBlockType = typeMatch.Groups["type"].Value;
                if (!blockProperties.ContainsKey(currentBlockType))
                    blockProperties[currentBlockType] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (!inProperties)
            {
                if (currentBlockType != null && line.Contains("properties:", StringComparison.Ordinal))
                {
                    inProperties = true;
                    bracketDepth = CountChar(line, '[') - CountChar(line, ']');
                }

                continue;
            }

            foreach (Match keyMatch in Regex.Matches(line, @"key:\s*'(?<key>[^']+)'"))
            {
                blockProperties[currentBlockType!].Add(keyMatch.Groups["key"].Value);
            }

            bracketDepth += CountChar(line, '[') - CountChar(line, ']');
            if (bracketDepth <= 0)
                inProperties = false;
        }

        return blockProperties;
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
