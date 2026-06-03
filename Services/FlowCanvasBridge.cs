using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using YamlDotNet.Serialization;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Converts between YAML Script models and Flow Canvas graph JSON.
    ///
    /// Strategy: Each graph node stores the original YAML snippet for its step.
    /// On import, steps are split from the YAML text and stored verbatim.
    /// On export, snippets are reassembled into valid YAML.
    /// This preserves all properties, comments, and formatting.
    /// </summary>
    internal sealed class FlowCanvasBridge
    {
        internal enum ExportDiagnosticSeverity
        {
            Warning,
            Error,
        }

        internal sealed class FlowCanvasExportDiagnostic
        {
            public ExportDiagnosticSeverity Severity { get; }
            public string Message { get; }
            public string? NodeId { get; }

            public FlowCanvasExportDiagnostic(ExportDiagnosticSeverity severity, string message, string? nodeId = null)
            {
                Severity = severity;
                Message = message;
                NodeId = nodeId;
            }
        }

        internal sealed class FlowCanvasExportResult
        {
            public string Yaml { get; set; } = string.Empty;
            public Dictionary<string, string> NodeToStepPathMap { get; set; } = new();
            public List<FlowCanvasExportDiagnostic> Diagnostics { get; } = new();
            public bool Success => Diagnostics.All(d => d.Severity != ExportDiagnosticSeverity.Error);

            public IReadOnlyList<string> Errors =>
                Diagnostics.Where(d => d.Severity == ExportDiagnosticSeverity.Error)
                    .Select(d => d.Message)
                    .ToList();

            public IReadOnlyList<string> Warnings =>
                Diagnostics.Where(d => d.Severity == ExportDiagnosticSeverity.Warning)
                    .Select(d => d.Message)
                    .ToList();
        }

        private const double NodeSpacingY = 106;  // ~25% looser than the original 85 for more breathing room
        private const double SingleBranchChildOffset = 70;
        private const double NodeStartX = 250;
        private const double NodeStartY = 40;
        private const double ChildIndentX = 60;
        private const double ChildMinX = 40;
        private const int MaxNestingDepth = 5;

        // Multi-branch horizontal layout constants
        // MinColumnWidth must be >= max child node width + gap to prevent overlap
        private const double ChildNodeMaxWidth = 300;
        private const double ColumnGap = 30;
        private const double MinColumnWidth = ChildNodeMaxWidth + ColumnGap;   // 330 — never narrower than a node

        // Branch edge colors
        private const string ColorThen = "#2ecc71";
        private const string ColorElse = "#e74c3c";
        private const string ColorElif = "#f0c040";
        private const string ColorLoop = "#f0c040";
        private const string ColorTry = "#2ecc71";
        private const string ColorCatch = "#e74c3c";
        private const string ColorFinally = "#4a9eff";
        private const string ColorCase = "#f0c040";
        private const string ColorBranch = "#1abc9c";
        private const string ColorContinue = "#4a9eff";

        private static readonly IReadOnlyDictionary<string, string> BlockTypeToCommandKey =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["browser_callback"] = "browser_callback_capture",
            };

        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BlockPropAliasesByType =
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["wait"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["duration"] = "seconds",
                },
                ["ping"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["target"] = "host",
                },
                ["dns"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["hostname"] = "host",
                },
                ["sftp"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["local"] = "local_path",
                    ["remote"] = "remote_path",
                },
                ["table"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = "data",
                },
                ["browser_callback"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["url"] = "start_url",
                },
                ["updatecolumn"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["expression"] = "value",
                },
                ["updateenvironment"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["expression"] = "value",
                },
            };

        private static readonly HashSet<string> BooleanOptionKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "suppress",
            "fail_on_nonzero",
            "follow_redirects",
            "allow_failure",
            "verify_tls",
            "open_browser",
            "auto_close_browser",
            "quiet",
            "overwrite",
            "show_header",
            "mirror_output",
            "show_window",
            "keep_open",
            "pretty",
            "select_file",
            "autobrowse",
            "path_only",
            "skip_empty_lines",
            "trim_lines",
            "required",
            "wait",
        };

        private static readonly HashSet<string> IntegerOptionKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "seconds",
            "timeout",
            "retry",
            "retry_delay",
            "count",
            "port",
            "local_port",
            "show_after_seconds",
            "max_lines",
            "max_seconds",
            "width",
            "height",
            "columns",
            "rows",
            "min",
            "max",
            "volume",
            "max_seconds",
            "max_output_bytes",
            "version",
        };

        private static readonly HashSet<string> ListOptionKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "required_fields",
            "sections",
            "success_codes",
            "mention",
            "attachments",
        };

        private static readonly HashSet<string> DictionaryOptionKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "headers",
            "args",
            "env",
            "out",
            "keys",
            "write",
            "patch",
        };

        private static readonly HashSet<string> ExitStatusTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "success",
            "failure",
            "fail",
            "error",
        };

        private static readonly IReadOnlyDictionary<string, string[]> RequiredOptionKeysByCommand =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["send"] = ["command"],
                ["print"] = ["message"],
                ["wait"] = ["seconds"],
                ["set"] = ["expression"],
                ["extract"] = ["pattern", "from", "into"],
                ["if"] = ["condition"],
                ["foreach"] = ["iterator"],
                ["while"] = ["condition"],
                ["repeat"] = ["until"],
                ["switch"] = ["value", "cases"],
                ["call"] = ["subroutine"],
                ["assert"] = ["condition"],
                ["parse"] = ["format", "from", "into"],
                ["table"] = ["data"],
                ["readfile"] = ["path", "into"],
                ["writefile"] = ["path"],
                ["exists"] = ["path", "into"],
                ["playsound"] = ["path"],
                ["input"] = ["into"],
                ["choose"] = ["options", "into"],
                ["multiselect"] = ["options", "into"],
                ["confirm"] = ["into"],
                ["ping"] = ["host"],
                ["dns"] = ["host"],
                ["portcheck"] = ["host"],
                ["http"] = ["url"],
                ["webhook"] = ["url"],
                ["browser_callback_capture"] = ["start_url", "into"],
                ["sftp"] = ["action", "local_path", "remote_path"],
                ["updatecolumn"] = ["column", "value"],
                ["updateenvironment"] = ["variable", "value"],
                ["log"] = ["message"],
                ["localcmd"] = ["command"],
                ["vault"] = ["path"],
                ["notify"] = ["message"],
            };

        private static readonly IReadOnlyDictionary<string, string[]> PreferredOptionOrderOverridesByCommand =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // Properties panel order differs from parser option-key catalog for these blocks.
                ["send"] = ["command", "capture", "suppress", "expect", "timeout", "retry", "retry_delay", "fail_on_nonzero", "on_error"],
                ["extract"] = ["pattern", "into", "from", "match", "required"],
                ["readfile"] = ["path", "select_file", "autobrowse", "message", "fileext", "path_only", "path_into", "into", "skip_empty_lines", "trim_lines", "max_lines", "encoding", "on_error"],
                ["choose"] = ["title", "prompt", "options", "into", "default", "font_size", "on_error"],
                ["multiselect"] = ["title", "prompt", "options", "into", "min", "max", "font_size", "on_error"],
                ["playsound"] = ["path", "max_seconds", "into", "wait", "volume", "on_error"],
                ["localcmd"] = ["command", "shell", "shell_path", "args", "env", "working_dir", "interactive", "keep_open", "run_mode", "lifetime", "kill_on_cancel", "success_codes", "max_output_bytes", "confirm", "quiet", "into", "fail_on_nonzero", "suppress", "title", "timeout", "on_error"],
                ["vault"] = ["profile", "path", "key", "keys", "into", "version", "write", "patch", "on_error"],
            };

        private static readonly HashSet<string> AdvancedPanelOptionKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "default",
            "validate",
            "validation_error",
            "font_size",
            "min",
            "max",
            "timeout",
            "retry",
            "retry_delay",
            "fail_on_nonzero",
            "suppress",
            "expect",
            "capture",
            "headers",
            "follow_redirects",
            "allow_failure",
            "verify_tls",
            "auth",
            "username",
            "password",
            "token",
            "content_type",
            "encoding",
            "max_lines",
            "trim_lines",
            "skip_empty_lines",
            "pretty",
            "format",
            "volume",
            "wait",
        };

        internal static IReadOnlyDictionary<string, IReadOnlyList<string>> GetExportOptionKeysByCommand()
        {
            return ScriptParser.GetKnownStepOptionKeysByCommand();
        }

        internal static IReadOnlyDictionary<string, IReadOnlyList<string>> GetPreferredExportOptionOrderByCommand()
        {
            var declaredOptions = ScriptParser.GetDeclaredStepOptionKeysByCommand();
            return declaredOptions.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)ResolvePreferredOptionOrder(pair.Key).ToList(),
                StringComparer.OrdinalIgnoreCase);
        }

        internal static IReadOnlyDictionary<string, string> GetBlockTypeCommandKeyAliases()
        {
            return new Dictionary<string, string>(BlockTypeToCommandKey, StringComparer.OrdinalIgnoreCase);
        }

        internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetBlockPropertyAliases()
        {
            return BlockPropAliasesByType.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(pair.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }

        private int _idCounter;
        private string NextId() => $"node-{_idCounter++}";

        #region YAML → Graph (using raw text splitting)

        /// <summary>
        /// Converts YAML script text into graph JSON by splitting into step snippets.
        /// Each node stores the original YAML for lossless round-tripping.
        /// Container blocks (if, foreach, while, try, switch) are recursively expanded
        /// so their nested children appear as indented visual-only nodes.
        /// </summary>
        public (JArray nodes, JArray edges) TextToGraph(string yamlText)
        {
            var nodes = new JArray();
            var edges = new JArray();
            _idCounter = 0;

            // Parse to get step types/labels, but use raw text for data
            var parser = new ScriptParser();
            var script = parser.Parse(yamlText);

            // Split the YAML text into individual step snippets
            var stepSnippets = SplitYamlSteps(yamlText);

            // Build preamble (everything before "steps:")
            var preamble = ExtractPreamble(yamlText);

            var currentY = NodeStartY + NodeSpacingY; // leave room for Start node at Y=0

            // Tracks nodes that need to connect to the next step.
            // Each entry is (nodeId, sourceHandle, color, label) — sourceHandle is
            // non-null for the false-path skip edge from an if without else.
            var pendingConnections = new List<PendingEdge>();

            for (int i = 0; i < script.Steps.Count && i < stepSnippets.Count; i++)
            {
                var step = script.Steps[i];
                var snippetInfo = stepSnippets[i];
                var snippet = snippetInfo.Snippet;
                var stepType = step.GetStepType();
                var nodeId = NextId();
                var stepPath = BuildStepPath("steps", i);

                // Get a display label from the step
                var (blockType, previewText) = GetStepPreview(step, stepType);

                // Build props: snippet for round-trip + individual fields for the Properties panel
                var stepProps = new JObject { ["_yamlSnippet"] = snippet };
                if (snippetInfo.BlankLinesBefore > 0)
                    stepProps["_blankLinesBefore"] = snippetInfo.BlankLinesBefore;
                stepProps["_stepPath"] = stepPath;
                if (previewText != null)
                    stepProps["_preview"] = previewText;
                ExtractStepProperties(step, stepType, stepProps);

                var node = new JObject
                {
                    ["id"] = nodeId,
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = NodeStartX, ["y"] = currentY },
                    ["data"] = new JObject
                    {
                        ["blockType"] = blockType,
                        ["label"] = GetDisplayLabel(blockType, previewText),
                        ["props"] = stepProps,
                    },
                };

                nodes.Add(node);

                // Connect from all pending nodes to this one
                foreach (var pe in pendingConnections)
                {
                    var edge = new JObject
                    {
                        ["id"] = $"e-{pe.NodeId}-{nodeId}{(pe.SourceHandle != null ? "-" + pe.SourceHandle : "")}",
                        ["source"] = pe.NodeId,
                        ["target"] = nodeId,
                        ["style"] = new JObject { ["stroke"] = pe.Color ?? "#555" },
                    };
                    if (pe.SourceHandle != null)
                        edge["sourceHandle"] = pe.SourceHandle;
                    if (pe.Label != null)
                    {
                        edge["label"] = pe.Label;
                        edge["labelStyle"] = new JObject
                        {
                            ["fill"] = pe.Color ?? "#555",
                            ["fontSize"] = 11,
                            ["fontWeight"] = 600,
                        };
                        edge["type"] = "smoothstep";
                    }
                    if (pe.Dashed)
                    {
                        edge["style"]!["strokeDasharray"] = "5,5";
                    }
                    edges.Add(edge);
                }
                pendingConnections.Clear();

                currentY += NodeSpacingY;

                // Expand container children into visible indented nodes
                if (IsContainerStep(stepType))
                {
                    var branchEnds = ExpandContainerChildren(step, stepType, nodeId, stepPath, ref currentY, 1, NodeStartX, nodes, edges);
                    if (branchEnds.Count > 0)
                    {
                        // Single continuation edge from the container's diamond handle
                        pendingConnections.Add(new PendingEdge(nodeId, "continue", ColorContinue, "next", dashed: false));
                    }
                    else
                    {
                        // No children expanded — this node connects to the next step
                        pendingConnections.Add(new PendingEdge(nodeId));
                    }

                    // If without else: the continuation edge alone is sufficient.
                    // A second dashed "else" skip edge to the same target is confusing.
                }
                else
                {
                    pendingConnections.Add(new PendingEdge(nodeId));
                }
            }

            // Create the Start node (always present, visible)
            var startProps = new JObject();
            if (!string.IsNullOrWhiteSpace(preamble))
            {
                ParsePreambleIntoProps(preamble, script, startProps);
            }

            var startNode = new JObject
            {
                ["id"] = "__start__",
                ["type"] = "start",
                ["position"] = new JObject { ["x"] = NodeStartX, ["y"] = 0 },
                ["data"] = new JObject
                {
                    ["blockType"] = "_start",
                    ["label"] = script.Name ?? "Untitled Script",
                    ["props"] = startProps,
                },
            };
            nodes.Add(startNode);

            // Connect Start to the first step node (if any steps exist)
            string? firstStepId = null;
            for (int n = 0; n < nodes.Count; n++)
            {
                var nid = nodes[n]["id"]?.ToString();
                if (nid != null && nid != "__start__")
                {
                    firstStepId = nid;
                    break;
                }
            }
            if (firstStepId != null)
            {
                edges.Add(new JObject
                {
                    ["id"] = $"edge-start-{firstStepId}",
                    ["source"] = "__start__",
                    ["target"] = firstStepId,
                    ["style"] = new JObject { ["stroke"] = "#666" },
                });
            }

            return (nodes, edges);
        }

        /// <summary>
        /// Determines the branches for a container step and creates child nodes.
        /// Single-branch containers (foreach, while, if-no-else) use left-indent layout.
        /// Multi-branch containers (if/else, parallel, switch, try/catch) use side-by-side horizontal layout.
        /// Returns the list of node IDs at the end of each branch (for merge edges to the next sibling).
        /// </summary>
        private List<string> ExpandContainerChildren(
            ScriptStep parentStep,
            StepType parentType,
            string parentNodeId,
            string parentStepPath,
            ref double currentY,
            int depth,
            double centerX,
            JArray nodes,
            JArray edges)
        {
            var branches = GetBranches(parentStep, parentType);
            var nonEmptyBranches = branches.Where(b => b.Steps != null && b.Steps.Count > 0).ToList();

            if (nonEmptyBranches.Count == 0)
                return new List<string>();

            if (IsMultiBranch(branches))
                return ExpandMultiBranch(nonEmptyBranches, parentNodeId, parentStepPath, ref currentY, depth, centerX, nodes, edges);
            else
                return ExpandSingleBranch(nonEmptyBranches[0], parentNodeId, parentStepPath, ref currentY, depth, centerX, nodes, edges);
        }

        /// <summary>
        /// Single-branch layout: children offset RIGHT of center so the continuation
        /// edge from the container's left-side diamond handle has a clear corridor.
        /// Used for foreach, while, if-without-else.
        /// </summary>
        private List<string> ExpandSingleBranch(
            BranchInfo branch,
            string parentNodeId,
            string parentStepPath,
            ref double currentY,
            int depth,
            double centerX,
            JArray nodes,
            JArray edges)
        {
            // Offset children to the right so the continuation edge (which exits
            // from the container's left-side diamond handle) can route cleanly
            // down the left side without cutting through child blocks.
            var childX = centerX + SingleBranchChildOffset;
            var lastNodeId = PlaceBranchSteps(branch, parentNodeId, parentStepPath, ref currentY, depth, childX, childX, nodes, edges);
            return new List<string> { lastNodeId };
        }

        /// <summary>
        /// Multi-branch layout: branches spread horizontally side-by-side.
        /// All branches start at the same Y. The next sibling starts after the tallest branch.
        /// </summary>
        private List<string> ExpandMultiBranch(
            List<BranchInfo> branches,
            string parentNodeId,
            string parentStepPath,
            ref double currentY,
            int depth,
            double centerX,
            JArray nodes,
            JArray edges)
        {
            // Positions are placeholders only — the canvas recomputes layout on import.
            // Keep branches in distinct columns so a host-only (no-canvas) render is still legible.
            var branchStartY = currentY;
            var maxBranchEndY = currentY;
            var branchEndNodes = new List<string>();
            double columnX = centerX;
            foreach (var branch in branches)
            {
                var branchY = branchStartY;
                var lastNodeId = PlaceBranchSteps(branch, parentNodeId, parentStepPath, ref branchY, depth, columnX, columnX, nodes, edges);
                branchEndNodes.Add(lastNodeId);
                maxBranchEndY = Math.Max(maxBranchEndY, branchY);
                columnX += MinColumnWidth;
            }
            currentY = maxBranchEndY;
            return branchEndNodes;
        }

        /// <summary>
        /// Places a list of steps vertically at the given X position, creating child nodes and edges.
        /// Returns the ID of the last node placed (for merge edges).
        /// </summary>
        private string PlaceBranchSteps(
            BranchInfo branch,
            string parentNodeId,
            string parentStepPath,
            ref double currentY,
            int depth,
            double childX,
            double centerX,
            JArray nodes,
            JArray edges)
        {
            string prevNodeId = parentNodeId;
            string? sourceHandle = branch.SourceHandle;
            string edgeColor = branch.Color;
            bool isFirstInBranch = true;
            bool isContinuation = false;

            for (int childIndex = 0; childIndex < branch.Steps.Count; childIndex++)
            {
                var childStep = branch.Steps[childIndex];
                var childStepType = childStep.GetStepType();
                var childNodeId = NextId();
                var (childBlockType, childPreview) = GetStepPreview(childStep, childStepType);
                var childStepPath = BuildStepPath(BuildScopePath(parentStepPath, branch.ScopePath), childIndex);

                // Build child node props (visual-only, no _yamlSnippet)
                var childProps = new JObject
                {
                    ["_isChildOf"] = parentNodeId,
                    ["_stepPath"] = childStepPath,
                    ["_branchLabel"] = branch.Label,
                    ["_branchColor"] = branch.Color,
                    ["_depth"] = depth,
                };
                if (childPreview != null)
                    childProps["_preview"] = childPreview;
                ExtractStepProperties(childStep, childStepType, childProps);

                var childNode = new JObject
                {
                    ["id"] = childNodeId,
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = childX, ["y"] = currentY },
                    ["data"] = new JObject
                    {
                        ["blockType"] = childBlockType,
                        ["label"] = GetDisplayLabel(childBlockType, childPreview),
                        ["props"] = childProps,
                    },
                };
                nodes.Add(childNode);

                // Create edge from previous node to this child
                var edge = new JObject
                {
                    ["id"] = $"e-{prevNodeId}-{childNodeId}" + (sourceHandle != null ? "-" + sourceHandle : ""),
                    ["source"] = prevNodeId,
                    ["target"] = childNodeId,
                    ["type"] = "smoothstep",
                    ["style"] = new JObject { ["stroke"] = edgeColor },
                };

                if (sourceHandle != null)
                    edge["sourceHandle"] = sourceHandle;

                if (isFirstInBranch)
                {
                    // First edge in the branch gets a label and dashed style
                    edge["label"] = branch.Label;
                    edge["labelStyle"] = new JObject
                    {
                        ["fill"] = edgeColor,
                        ["fontSize"] = 11,
                        ["fontWeight"] = 600,
                    };
                    edge["style"]!["strokeDasharray"] = "5,5";
                }
                else if (isContinuation)
                {
                    // Continuation edge out of a nested container's 'continue' handle.
                    edge["label"] = "next";
                    edge["labelStyle"] = new JObject
                    {
                        ["fill"] = edgeColor,
                        ["fontSize"] = 11,
                        ["fontWeight"] = 600,
                    };
                }

                edges.Add(edge);
                prevNodeId = childNodeId;
                sourceHandle = null;
                edgeColor = branch.Color;
                isFirstInBranch = false;
                isContinuation = false;
                currentY += NodeSpacingY;

                // Recursively expand if this child is also a container
                if (IsContainerStep(childStepType) && depth < MaxNestingDepth)
                {
                    var nestedBranchEnds = ExpandContainerChildren(
                        childStep, childStepType, childNodeId, childStepPath, ref currentY, depth + 1, centerX, nodes, edges);

                    if (nestedBranchEnds.Count > 0)
                    {
                        // The continuation after a nested container flows from the container's own
                        // 'continue' handle — exactly like a top-level container — NOT from its branch
                        // end. This keeps the nested body terminal (e.g. an inner if's `then` ends at
                        // its last step instead of flowing into the next sibling) and routes the spine
                        // straight from the container down to the following step. (issue #45)
                        // prevNodeId intentionally stays = childNodeId (the container node).
                        sourceHandle = "continue";
                        edgeColor = ColorContinue;
                        isContinuation = true;
                    }
                }
            }

            return prevNodeId;
        }

        /// <summary>
        /// Extracts the branch definitions from a container step.
        /// Each branch has a label, color, source handle, and list of child steps.
        /// </summary>
        private static List<BranchInfo> GetBranches(ScriptStep step, StepType stepType)
        {
            var branches = new List<BranchInfo>();

            switch (stepType)
            {
                case StepType.If:
                    if (step.Then != null && step.Then.Count > 0)
                        branches.Add(new BranchInfo("then", "then", ColorThen, null, step.Then));
                    if (step.Elif != null)
                    {
                        for (int elifIndex = 0; elifIndex < step.Elif.Count; elifIndex++)
                        {
                            var elif = step.Elif[elifIndex];
                            var label = elif.If.Length > 20
                                ? "elif: " + elif.If.Substring(0, 17) + "..."
                                : "elif: " + elif.If;
                            branches.Add(new BranchInfo(label, $"elif/{elifIndex}/then", ColorElif, null, elif.Then));
                        }
                    }
                    if (step.Else != null && step.Else.Count > 0)
                        branches.Add(new BranchInfo("else", "else", ColorElse, "false", step.Else));
                    break;

                case StepType.Foreach:
                    if (step.Do != null && step.Do.Count > 0)
                        branches.Add(new BranchInfo("loop", "do", ColorLoop, null, step.Do));
                    break;

                case StepType.While:
                    if (step.Do != null && step.Do.Count > 0)
                        branches.Add(new BranchInfo("loop", "do", ColorLoop, null, step.Do));
                    break;

                case StepType.Repeat:
                    if (step.Do != null && step.Do.Count > 0)
                        branches.Add(new BranchInfo("loop", "do", ColorLoop, null, step.Do));
                    break;

                case StepType.Try:
                    if (step.Try != null && step.Try.Count > 0)
                        branches.Add(new BranchInfo("try", "try", ColorTry, null, step.Try));
                    if (step.Catch != null && step.Catch.Count > 0)
                        branches.Add(new BranchInfo("catch", "catch", ColorCatch, null, step.Catch));
                    if (step.Finally != null && step.Finally.Count > 0)
                        branches.Add(new BranchInfo("finally", "finally", ColorFinally, null, step.Finally));
                    break;

                case StepType.Switch:
                    if (step.Cases != null)
                    {
                        for (int caseIndex = 0; caseIndex < step.Cases.Count; caseIndex++)
                        {
                            var c = step.Cases[caseIndex];
                            var label = c.Value.Length > 20
                                ? "case: " + c.Value.Substring(0, 17) + "..."
                                : "case: " + c.Value;
                            branches.Add(new BranchInfo(label, $"cases/{caseIndex}/do", ColorCase, null, c.Do));
                        }
                    }
                    if (step.Else != null && step.Else.Count > 0)
                        branches.Add(new BranchInfo("default", "default", ColorElse, null, step.Else));
                    break;

                case StepType.Parallel:
                    if (step.Parallel?.Steps != null)
                    {
                        for (int i = 0; i < step.Parallel.Steps.Count; i++)
                        {
                            branches.Add(new BranchInfo(
                                $"branch {i + 1}", $"parallel/{i}", ColorBranch, null,
                                new List<ScriptStep> { step.Parallel.Steps[i] }));
                        }
                    }
                    break;
            }

            return branches;
        }

        private static bool IsContainerStep(StepType stepType)
        {
            return stepType == StepType.If
                || stepType == StepType.Foreach
                || stepType == StepType.While
                || stepType == StepType.Repeat
                || stepType == StepType.Try
                || stepType == StepType.Switch
                || stepType == StepType.Parallel;
        }

        /// <summary>
        /// Holds information about a single branch of a container block.
        /// </summary>
        private sealed class BranchInfo
        {
            public string Label { get; }
            public string ScopePath { get; }
            public string Color { get; }
            public string? SourceHandle { get; }
            public List<ScriptStep> Steps { get; }

            public BranchInfo(string label, string scopePath, string color, string? sourceHandle, List<ScriptStep> steps)
            {
                Label = label;
                ScopePath = scopePath;
                Color = color;
                SourceHandle = sourceHandle;
                Steps = steps;
            }
        }

        /// <summary>
        /// Represents an edge waiting to be connected to the next step in the main flow.
        /// Carries optional source handle, color, and label for styled edges (e.g., if-else skip).
        /// </summary>
        private sealed class PendingEdge
        {
            public string NodeId { get; }
            public string? SourceHandle { get; }
            public string? Color { get; }
            public string? Label { get; }
            public bool Dashed { get; }

            public PendingEdge(string nodeId, string? sourceHandle = null, string? color = null, string? label = null, bool dashed = false)
            {
                NodeId = nodeId;
                SourceHandle = sourceHandle;
                Color = color;
                Label = label;
                Dashed = dashed;
            }
        }

        /// <summary>
        /// Represents an edge in the graph with its source handle information preserved.
        /// Used during YAML export to distinguish branch paths (e.g., if-then vs if-else).
        /// </summary>
        private sealed class EdgeInfo
        {
            public string TargetId { get; }
            public string? SourceHandle { get; }
            public string? BranchPath { get; }
            public string? BranchCondition { get; }
            public string? CaseValue { get; }
            public string? Label { get; }

            public EdgeInfo(
                string targetId,
                string? sourceHandle,
                string? branchPath = null,
                string? branchCondition = null,
                string? caseValue = null,
                string? label = null)
            {
                TargetId = targetId;
                SourceHandle = sourceHandle;
                BranchPath = branchPath;
                BranchCondition = branchCondition;
                CaseValue = caseValue;
                Label = label;
            }
        }

        /// <summary>
        /// Returns true when a container has 2+ non-empty branches (needs side-by-side layout).
        /// </summary>
        private static bool IsMultiBranch(List<BranchInfo> branches)
        {
            return branches.Count(b => b.Steps != null && b.Steps.Count > 0) >= 2;
        }

        /// <summary>
        /// Converts a parsed Script model into graph JSON (fallback when raw text isn't available).
        /// </summary>
        public (JArray nodes, JArray edges) ToGraph(Script script)
        {
            var nodes = new JArray();
            var edges = new JArray();
            var idCounter = 0;

            string NextId() => $"node-{idCounter++}";

            var currentY = NodeStartY;
            string? lastNodeId = null;

            foreach (var step in script.Steps)
            {
                var stepType = step.GetStepType();
                var nodeId = NextId();
                var (blockType, previewText) = GetStepPreview(step, stepType);

                var node = new JObject
                {
                    ["id"] = nodeId,
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = NodeStartX, ["y"] = currentY },
                    ["data"] = new JObject
                    {
                        ["blockType"] = blockType,
                        ["label"] = GetDisplayLabel(blockType, previewText),
                        ["props"] = new JObject
                        {
                            ["_preview"] = previewText ?? blockType,
                        },
                    },
                };
                nodes.Add(node);

                if (lastNodeId != null)
                {
                    edges.Add(new JObject
                    {
                        ["id"] = $"e-{lastNodeId}-{nodeId}",
                        ["source"] = lastNodeId,
                        ["target"] = nodeId,
                        ["style"] = new JObject { ["stroke"] = "#555" },
                    });
                }

                lastNodeId = nodeId;
                currentY += NodeSpacingY;
            }

            return (nodes, edges);
        }

        #endregion

        #region Graph → YAML

        /// <summary>
        /// Converts graph JSON back to YAML and returns structured diagnostics.
        /// </summary>
        public FlowCanvasExportResult ExportGraphToYaml(JObject graphData)
        {
            var result = new FlowCanvasExportResult();
            var nodes = graphData["nodes"] as JArray ?? new JArray();
            var edges = graphData["edges"] as JArray ?? new JArray();

            // Build node map and adjacency
            var nodeMap = new Dictionary<string, JToken>();
            foreach (var n in nodes)
            {
                var id = n["id"]?.ToString();
                if (id != null) nodeMap[id] = n;
            }

            // Find the execution order by following edges from roots
            var outgoing = new Dictionary<string, List<EdgeInfo>>();
            var incomingCount = new Dictionary<string, int>();
            foreach (var edge in edges)
            {
                var src = edge["source"]?.ToString();
                var tgt = edge["target"]?.ToString();
                var sourceHandle = edge["sourceHandle"]?.ToString();
                var edgeLabel = edge["label"]?.ToString();
                var edgeData = edge["data"] as JObject;
                var branchPath = edgeData?["branchPath"]?.ToString();
                var branchCondition = edgeData?["condition"]?.ToString();
                var caseValue = edgeData?["caseValue"]?.ToString();
                if (src == null || tgt == null) continue;
                if (!outgoing.ContainsKey(src)) outgoing[src] = new List<EdgeInfo>();
                outgoing[src].Add(new EdgeInfo(
                    tgt,
                    sourceHandle,
                    branchPath,
                    branchCondition,
                    caseValue,
                    edgeLabel));
                incomingCount[tgt] = incomingCount.TryGetValue(tgt, out var count) ? count + 1 : 1;
            }

            // --- Start node: determine root and build ordered chain ---
            string? startTarget = null;
            if (outgoing.TryGetValue("__start__", out var startTargets) && startTargets.Count > 0)
                startTarget = startTargets[0].TargetId;

            // Build ordered chain from Start's outgoing target
            var orderedIds = new List<string>();
            var visited = new HashSet<string>();
            if (startTarget != null)
            {
                BuildChain(startTarget, outgoing, orderedIds, visited);
            }

            // Warn about disconnected nodes (not reachable from Start, excluded from export)
            foreach (var n in nodes)
            {
                var nid = n["id"]?.ToString();
                if (nid == null || nid == "__start__") continue;
                if (n["hidden"]?.Value<bool>() == true) continue;
                if (!visited.Contains(nid) && !incomingCount.ContainsKey(nid))
                {
                    result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                        ExportDiagnosticSeverity.Warning,
                        $"Node '{nid}' is not reachable from the Start block and will be excluded from the exported YAML.",
                        nid));
                }
            }

            // Build preamble from Start node props
            var sb = new StringBuilder();
            if (nodeMap.TryGetValue("__start__", out var startNode))
            {
                var startProps = startNode["data"]?["props"] as JObject;
                if (startProps != null)
                {
                    sb.Append(SerializeStartPropsToPreamble(startProps));
                }
            }

            // Ensure "steps:" header is present
            var preambleText = sb.ToString();
            if (!HasTopLevelStepsHeader(preambleText))
                sb.AppendLine("steps:");

            // Tracks nodes consumed as branch children by container blocks (if/foreach/while).
            // These are skipped in the main export loop since they're nested inside their parent's YAML.
            var consumedByContainer = new HashSet<string>();

            // Emit each node's YAML
            int topLevelIndex = 0;
            foreach (var nodeId in orderedIds)
            {
                if (!nodeMap.TryGetValue(nodeId, out var node)) continue;
                if (node["hidden"]?.Value<bool>() == true) continue;
                if (nodeId == "__start__") continue;
                if (consumedByContainer.Contains(nodeId)) continue;

                var data = node["data"];
                var props = data?["props"] as JObject;
                var blockType = data?["blockType"]?.ToString() ?? "print";
                var existingStepPath = props?["_stepPath"]?.ToString();
                var isChildNode = props?["_isChildOf"] != null;

                // Skip non-executable node kinds.
                if (string.Equals(blockType, "comment", StringComparison.OrdinalIgnoreCase))
                {
                    result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                        ExportDiagnosticSeverity.Warning,
                        "Comment nodes are ignored during YAML export.",
                        nodeId));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(existingStepPath))
                    result.NodeToStepPathMap[nodeId] = existingStepPath!;

                // Visual-only child nodes do not emit YAML, but keep their path mapping
                // for debug/runtime event correlation.
                if (isChildNode)
                    continue;

                if (!result.NodeToStepPathMap.ContainsKey(nodeId))
                    result.NodeToStepPathMap[nodeId] = BuildStepPath("steps", topLevelIndex);

                topLevelIndex++;

                // Re-emit blank lines the user had between steps to preserve spacing.
                var blankLinesBefore = props?["_blankLinesBefore"]?.Value<int>() ?? 0;
                for (int bl = 0; bl < blankLinesBefore; bl++)
                    sb.AppendLine();

                var yamlSnippet = props?["_yamlSnippet"]?.ToString();
                var forceGraphExport = HasForceGraphExport(props);

                // Container blocks authored visually (branch metadata -> non-child targets)
                // should be regenerated from graph structure even when a stale snippet exists.
                // Also regenerate when the user has modified an imported container's branches
                // (e.g., deleted an else edge) — the stored snippet would be stale.
                if (IsContainerBlockType(blockType) &&
                    (forceGraphExport ||
                     string.IsNullOrWhiteSpace(yamlSnippet) ||
                     HasGraphAuthoredContainerBranches(nodeId, outgoing, nodeMap) ||
                     HasImportedContainerBeenModified(nodeId, outgoing, nodeMap, yamlSnippet)))
                {
                    if (TryGenerateContainerFromGraph(
                            blockType, props, nodeId, outgoing, nodeMap, incomingCount,
                            consumedByContainer, result, out var containerYaml))
                    {
                        sb.AppendLine(containerYaml);
                        continue;
                    }
                }

                // Round-trip path for imported containers whose branches are still represented
                // by visual child nodes and do not require regeneration.
                if (IsContainerBlockType(blockType) && !forceGraphExport && !string.IsNullOrWhiteSpace(yamlSnippet))
                {
                    var normalizedSnippet = NormalizeTopLevelSnippetIndent(yamlSnippet);
                    result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                        ExportDiagnosticSeverity.Warning,
                        $"Container block '{blockType}' is exported from its stored YAML snippet.",
                        nodeId));
                    sb.Append(normalizedSnippet);
                    if (!normalizedSnippet.EndsWith("\n"))
                        sb.AppendLine();
                    continue;
                }

                if (TryGenerateStepYaml(blockType, props, out var generatedYaml, out var error))
                {
                    sb.AppendLine(generatedYaml);
                }
                else
                {
                    result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                        ExportDiagnosticSeverity.Error,
                        string.IsNullOrWhiteSpace(error)
                            ? $"Unable to export block type '{blockType}'."
                            : error!,
                        nodeId));
                }
            }

            if (result.Success)
                result.Yaml = sb.ToString().TrimEnd() + "\n";
            else
                result.Yaml = string.Empty;

            return result;
        }

        public string ToYaml(JObject graphData)
        {
            var exportResult = ExportGraphToYaml(graphData);
            if (!exportResult.Success)
            {
                throw new InvalidOperationException(string.Join(
                    Environment.NewLine,
                    exportResult.Errors));
            }

            return exportResult.Yaml;
        }

        private static bool HasGraphAuthoredContainerBranches(
            string nodeId,
            Dictionary<string, List<EdgeInfo>> outgoing,
            Dictionary<string, JToken> nodeMap)
        {
            if (!outgoing.TryGetValue(nodeId, out var edges) || edges.Count == 0)
                return false;

            foreach (var edge in edges)
            {
                // Continuation edges (sourceHandle="continue") are not branch edges.
                if (string.Equals(edge.SourceHandle, "continue", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Require explicit branch metadata. SourceHandle-only false skip edges
                // (used for imported if-without-else visualization) should not trigger
                // regeneration from graph.
                if (string.IsNullOrWhiteSpace(edge.BranchPath))
                    continue;

                if (!nodeMap.TryGetValue(edge.TargetId, out var targetNode))
                    return true;

                var targetProps = targetNode["data"]?["props"] as JObject;
                var isVisualChild = targetProps?["_isChildOf"] != null;
                if (!isVisualChild)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Detects when an imported container's branch structure has been modified by the user
        /// (e.g., an else edge was deleted). Compares the distinct branch labels among the
        /// container's child nodes with the edges actually connecting them. If a branch's
        /// first child is no longer reachable from the container, the snippet is stale.
        /// </summary>
        private static bool HasImportedContainerBeenModified(
            string nodeId,
            Dictionary<string, List<EdgeInfo>> outgoing,
            Dictionary<string, JToken> nodeMap,
            string? yamlSnippet = null)
        {
            // Collect the set of node IDs directly reachable from the container
            var directTargets = new HashSet<string>(StringComparer.Ordinal);
            if (outgoing.TryGetValue(nodeId, out var edges))
            {
                foreach (var edge in edges)
                    directTargets.Add(edge.TargetId);
            }

            // Collect the distinct YAML branch keys among this container's children.
            // Each branch (then, else, do, catch, etc.) should have its first child
            // directly connected from the container. If a branch's first child is
            // missing from directTargets, the user deleted that branch edge.
            //
            // We derive the branch key from _stepPath (e.g., "steps/3/do/0" → "do")
            // rather than _branchLabel, because _branchLabel is the visual display name
            // ("loop", "case: value") which may not match the YAML keyword ("do", "cases").
            var containerStepPath = nodeMap.TryGetValue(nodeId, out var containerNode)
                ? (containerNode["data"]?["props"] as JObject)?["_stepPath"]?.ToString() ?? ""
                : "";
            var branchFirstChildren = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var branchFirstPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasAnyChildNodes = false;
            foreach (var kvp in nodeMap)
            {
                if (kvp.Key == nodeId) continue;
                var childProps = kvp.Value["data"]?["props"] as JObject;
                if (childProps == null) continue;

                var parentId = childProps["_isChildOf"]?.ToString();
                if (!string.Equals(parentId, nodeId, StringComparison.Ordinal))
                    continue;

                hasAnyChildNodes = true;

                // Extract the YAML branch key from the child's step path.
                // E.g., container="steps/3", child="steps/3/do/0" → key="do"
                //        container="steps/1", child="steps/1/cases/0/do/0" → key="cases"
                var stepPath = childProps["_stepPath"]?.ToString() ?? "";
                var branchKey = ExtractBranchKeyFromStepPath(stepPath, containerStepPath);
                if (string.IsNullOrWhiteSpace(branchKey))
                {
                    // Fallback to _branchLabel if step path doesn't yield a key
                    branchKey = childProps["_branchLabel"]?.ToString();
                }
                if (string.IsNullOrWhiteSpace(branchKey)) continue;

                // Only the first child in each branch (lowest _stepPath index) is directly
                // connected from the container. Compare step paths using numeric segment
                // ordering so ".../10" is treated as later than ".../2".
                if (!branchFirstChildren.ContainsKey(branchKey) ||
                    CompareStepPathSegments(stepPath, branchFirstPaths[branchKey]) < 0)
                {
                    branchFirstChildren[branchKey] = kvp.Key;
                    branchFirstPaths[branchKey] = stepPath;
                }
            }

            if (branchFirstChildren.Count == 0)
            {
                // Legacy/imported child nodes may only include _isChildOf/_stepPath metadata
                // and omit explicit _branchLabel. In that case, keep snippet export behavior.
                if (hasAnyChildNodes)
                    return false;

                // No children remain at all. If the snippet originally defined branches,
                // that means the user deleted all branch children — snippet is stale.
                // Check the snippet for branch keywords to detect this case.
                if (!string.IsNullOrWhiteSpace(yamlSnippet))
                {
                    var snippetBranches = ExtractSnippetBranchKeys(yamlSnippet);
                    if (snippetBranches.Count > 0)
                        return true; // Snippet had branches but no children remain
                }
                return false;
            }

            // Check if any branch's first child is no longer directly reachable
            foreach (var firstChildId in branchFirstChildren.Values)
            {
                if (!directTargets.Contains(firstChildId))
                    return true; // Branch edge was deleted — snippet is stale
            }

            // Check if the snippet originally defined branches that no longer have
            // any child nodes in the graph (e.g., user deleted all nodes in the else branch).
            // The YAML keyword and the internal scope path may differ (e.g., YAML "else:" maps
            // to scope path "default" for switch, or "else" for if). Check both.
            if (!string.IsNullOrWhiteSpace(yamlSnippet))
            {
                var snippetBranches = ExtractSnippetBranchKeys(yamlSnippet);
                foreach (var branch in snippetBranches)
                {
                    if (!branchFirstChildren.ContainsKey(branch))
                    {
                        // Check alias: YAML "else" ↔ scope "default" (switch containers)
                        var alias = string.Equals(branch, "else", StringComparison.OrdinalIgnoreCase) ? "default"
                                  : string.Equals(branch, "default", StringComparison.OrdinalIgnoreCase) ? "else"
                                  : null;
                        if (alias == null || !branchFirstChildren.ContainsKey(alias))
                            return true; // Snippet defines a branch with no remaining children
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts top-level branch keys (then, else, do, catch, finally, cases, default)
        /// from a container's YAML snippet by scanning for indented keywords followed by a colon.
        /// </summary>
        /// <summary>
        /// Extracts the YAML branch key from a child's step path relative to its container.
        /// E.g., container="steps/3", child="steps/3/do/0" → "do"
        ///        container="steps/1", child="steps/1/cases/0/do/0" → "cases"
        /// </summary>
        private static string? ExtractBranchKeyFromStepPath(string childStepPath, string containerStepPath)
        {
            if (string.IsNullOrEmpty(containerStepPath) || string.IsNullOrEmpty(childStepPath))
                return null;

            // Strip the container prefix (e.g., "steps/3/") to get the relative path
            var prefix = containerStepPath.EndsWith("/") ? containerStepPath : containerStepPath + "/";
            if (!childStepPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            var relative = childStepPath.Substring(prefix.Length);
            // First segment is the branch key (e.g., "do/0" → "do", "cases/0/do/0" → "cases")
            var slashIndex = relative.IndexOf('/');
            return slashIndex >= 0 ? relative.Substring(0, slashIndex) : relative;
        }

        private static int CompareStepPathSegments(string leftPath, string rightPath)
        {
            var leftSegments = (leftPath ?? string.Empty).Split('/', StringSplitOptions.None);
            var rightSegments = (rightPath ?? string.Empty).Split('/', StringSplitOptions.None);
            var compareCount = Math.Min(leftSegments.Length, rightSegments.Length);

            for (int i = 0; i < compareCount; i++)
            {
                var leftSegment = leftSegments[i];
                var rightSegment = rightSegments[i];

                if (int.TryParse(leftSegment, out var leftIndex) &&
                    int.TryParse(rightSegment, out var rightIndex))
                {
                    var numericCompare = leftIndex.CompareTo(rightIndex);
                    if (numericCompare != 0)
                        return numericCompare;

                    continue;
                }

                var segmentCompare = string.Compare(leftSegment, rightSegment, StringComparison.OrdinalIgnoreCase);
                if (segmentCompare != 0)
                    return segmentCompare;
            }

            return leftSegments.Length.CompareTo(rightSegments.Length);
        }

        private static HashSet<string> ExtractSnippetBranchKeys(string snippet)
        {
            var branchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] knownBranches = { "then", "else", "elif", "do", "catch", "finally", "cases", "default" };

            // Determine the indent level of the container's direct properties.
            // The snippet starts with "- while:" (or similar). The direct property lines
            // (condition:, do:, then:, etc.) are at the FIRST non-list-item indent level.
            // Only match branch keywords at that exact indent to avoid picking up
            // keywords from nested containers (e.g., "then:" inside a nested "if").
            int propertyIndent = -1;
            var lines = snippet.Split('\n');
            for (int i = 1; i < lines.Length; i++) // skip line 0 (the "- while:" line)
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;
                var indent = line.Length - line.TrimStart().Length;
                if (indent > 0)
                {
                    propertyIndent = indent;
                    break;
                }
            }

            if (propertyIndent < 0) return branchKeys;

            foreach (var line in lines)
            {
                var raw = line.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var indent = raw.Length - raw.TrimStart().Length;
                if (indent != propertyIndent) continue;

                var trimmed = raw.TrimStart();
                foreach (var key in knownBranches)
                {
                    if (trimmed.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith(key + " :", StringComparison.OrdinalIgnoreCase))
                    {
                        branchKeys.Add(key);
                        break;
                    }
                }
            }

            return branchKeys;
        }

        private void BuildChain(string nodeId, Dictionary<string, List<EdgeInfo>> outgoing, List<string> ordered, HashSet<string> visited)
        {
            if (!visited.Add(nodeId)) return;
            ordered.Add(nodeId);
            if (outgoing.TryGetValue(nodeId, out var targets))
            {
                foreach (var ei in targets)
                    BuildChain(ei.TargetId, outgoing, ordered, visited);
            }
        }

        /// <summary>
        /// Collects the ordered list of nodes that make up a single container branch.
        ///
        /// For imported / metadata-tagged graphs every branch child carries an "_isChildOf"
        /// pointer to its owning container and a hierarchical "_stepPath"
        /// (e.g. "steps/1/do/8/then/3"). That metadata is the authoritative, unambiguous branch
        /// definition: the branch is exactly the set of nodes whose _isChildOf is the owning
        /// container and whose _stepPath is an IMMEDIATE child of the branch's scope path,
        /// ordered by step index. Descendants of nested containers (deeper _stepPath) are
        /// excluded here and emitted by their own container's regeneration.
        ///
        /// This replaces reconstructing structure from the linear edge chain, which is ambiguous
        /// across nested-branch boundaries: following plain edges either over-collected (swallowed
        /// the parent branch's following siblings) or, for a nested MULTI-branch container,
        /// dead-ended inside its first branch and silently dropped following siblings (issue #45).
        ///
        /// For purely canvas-authored containers whose children carry no "_isChildOf" metadata
        /// (nesting lives only on the edges) the original edge-following behaviour is preserved:
        /// follow the linear chain and stop at a convergence point (a node with more than one
        /// incoming edge, marking the continuation after the container).
        /// </summary>
        private List<string> CollectBranchChain(
            string startNodeId,
            Dictionary<string, List<EdgeInfo>> outgoing,
            Dictionary<string, int> incomingCount,
            HashSet<string> branchVisited,
            Dictionary<string, JToken> nodeMap)
        {
            var ownerContainer = GetNodeParentId(startNodeId, nodeMap);
            var startPath = GetNodeStepPath(startNodeId, nodeMap);

            // Metadata-driven collection for imported / metadata-tagged branches.
            if (ownerContainer != null && !string.IsNullOrEmpty(startPath))
            {
                var branchScope = GetParentScopePath(startPath!);
                var members = new List<KeyValuePair<int, string>>();
                foreach (var entry in nodeMap)
                {
                    var id = entry.Key;
                    if (branchVisited.Contains(id))
                        continue;
                    if (!string.Equals(GetNodeParentId(id, nodeMap), ownerContainer, StringComparison.Ordinal))
                        continue;
                    if (!TryGetDirectChildIndex(GetNodeStepPath(id, nodeMap), branchScope, out var index))
                        continue;
                    members.Add(new KeyValuePair<int, string>(index, id));
                }

                members.Sort((a, b) => a.Key.CompareTo(b.Key));

                var orderedChain = new List<string>(members.Count);
                foreach (var member in members)
                {
                    branchVisited.Add(member.Value);
                    orderedChain.Add(member.Value);
                }
                return orderedChain;
            }

            // Canvas-authored fallback: structure lives on the edges, not on node metadata.
            // Follow the linear chain and stop at a convergence point (continuation after the
            // container) or when another branch already claimed the node.
            var chain = new List<string>();
            string? currentId = startNodeId;
            while (currentId != null)
            {
                if (branchVisited.Contains(currentId))
                    break;
                if (chain.Count > 0 && incomingCount.TryGetValue(currentId, out var count) && count > 1)
                    break;

                branchVisited.Add(currentId);
                chain.Add(currentId);

                string? nextId = null;
                if (outgoing.TryGetValue(currentId, out var edges))
                {
                    foreach (var ei in edges)
                    {
                        if (string.IsNullOrEmpty(ei.SourceHandle))
                        {
                            nextId = ei.TargetId;
                            break;
                        }
                    }
                }

                currentId = nextId;
            }

            return chain;
        }

        /// <summary>
        /// Reads a node's "_isChildOf" parent-container id, or null for top-level / metadata-less nodes.
        /// </summary>
        private static string? GetNodeParentId(string nodeId, Dictionary<string, JToken> nodeMap)
        {
            if (!nodeMap.TryGetValue(nodeId, out var node))
                return null;
            var parentId = (node["data"]?["props"] as JObject)?["_isChildOf"]?.ToString();
            return string.IsNullOrEmpty(parentId) ? null : parentId;
        }

        /// <summary>Reads a node's hierarchical "_stepPath" (e.g. "steps/1/do/8/then/3"), or null.</summary>
        private static string? GetNodeStepPath(string nodeId, Dictionary<string, JToken> nodeMap)
        {
            if (!nodeMap.TryGetValue(nodeId, out var node))
                return null;
            return (node["data"]?["props"] as JObject)?["_stepPath"]?.ToString();
        }

        /// <summary>Returns a child step path with its final "/index" segment removed (its branch scope).</summary>
        private static string GetParentScopePath(string stepPath)
        {
            var slash = stepPath.LastIndexOf('/');
            return slash > 0 ? stepPath.Substring(0, slash) : stepPath;
        }

        /// <summary>
        /// True when childStepPath is an IMMEDIATE child of branchScope (i.e. "branchScope/&lt;int&gt;"),
        /// yielding that integer index. Deeper descendants ("branchScope/x/...") are rejected so a
        /// nested container's own descendants are not pulled into the parent branch.
        /// </summary>
        private static bool TryGetDirectChildIndex(string? childStepPath, string branchScope, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(childStepPath))
                return false;
            var prefix = branchScope + "/";
            if (!childStepPath!.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            var remainder = childStepPath.Substring(prefix.Length);
            if (remainder.Length == 0 || remainder.IndexOf('/') >= 0)
                return false;
            return int.TryParse(remainder, out index);
        }

        private sealed class BranchExportInfo
        {
            public int Index { get; set; }
            public string TargetId { get; set; } = string.Empty;
            public string? Condition { get; set; }
            public string? CaseValue { get; set; }
        }

        /// <summary>
        /// Generates YAML for a visually-authored container block by deriving branch structure from graph edge metadata.
        /// </summary>
        private bool TryGenerateContainerFromGraph(
            string blockType,
            JObject? props,
            string nodeId,
            Dictionary<string, List<EdgeInfo>> outgoing,
            Dictionary<string, JToken> nodeMap,
            Dictionary<string, int> incomingCount,
            HashSet<string> consumedByContainer,
            FlowCanvasExportResult result,
            out string yaml)
        {
            yaml = string.Empty;

            if (!TryGenerateContainerHeaderYaml(blockType, props, out var headerYaml, out var headerError))
            {
                result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                    ExportDiagnosticSeverity.Error,
                    string.IsNullOrWhiteSpace(headerError)
                        ? $"Unable to export container block '{blockType}'."
                        : headerError!,
                    nodeId));
                return false;
            }

            var nodeEdges = outgoing.TryGetValue(nodeId, out var edgesFromNode)
                ? edgesFromNode.Where(e => !string.Equals(e.SourceHandle, "continue", StringComparison.OrdinalIgnoreCase)).ToList()
                : new List<EdgeInfo>();
            var sb = new StringBuilder();
            sb.Append(headerYaml.TrimEnd());
            sb.AppendLine();

            var branchVisited = new HashSet<string>(StringComparer.Ordinal);

            if (string.Equals(blockType, "if", StringComparison.OrdinalIgnoreCase))
            {
                string? thenTarget = null;
                string? elseTarget = null;
                var elifBranches = new List<BranchExportInfo>();
                var fallbackElifIndex = 0;

                foreach (var edge in nodeEdges)
                {
                    if (string.Equals(edge.SourceHandle, "false", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(edge.BranchPath, "else", StringComparison.OrdinalIgnoreCase))
                    {
                        elseTarget ??= edge.TargetId;
                        continue;
                    }

                    if (TryParseIndexedScope(edge.BranchPath, "elif", out var elifIndex))
                    {
                        elifBranches.Add(new BranchExportInfo
                        {
                            Index = elifIndex,
                            TargetId = edge.TargetId,
                            Condition = edge.BranchCondition ?? ParseConditionFromLabel(edge.Label),
                        });
                        continue;
                    }

                    if (string.Equals(edge.BranchPath, "then", StringComparison.OrdinalIgnoreCase) && thenTarget == null)
                    {
                        thenTarget = edge.TargetId;
                        continue;
                    }

                    if (thenTarget == null)
                    {
                        thenTarget = edge.TargetId;
                        continue;
                    }

                    elifBranches.Add(new BranchExportInfo
                    {
                        Index = fallbackElifIndex++,
                        TargetId = edge.TargetId,
                        Condition = edge.BranchCondition ?? ParseConditionFromLabel(edge.Label),
                    });
                }

                if (thenTarget == null)
                {
                    result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                        ExportDiagnosticSeverity.Error,
                        "If block is missing a 'then' branch connection.",
                        nodeId));
                    return false;
                }

                var thenChain = CollectBranchChain(thenTarget, outgoing, incomingCount, branchVisited, nodeMap);
                if (thenChain.Count == 0)
                {
                    sb.AppendLine("    then: []");
                }
                else
                {
                    sb.AppendLine("    then:");
                    if (!TryGenerateBranchYaml(
                            thenChain, nodeMap, outgoing, incomingCount, consumedByContainer, result, sb, 6))
                    {
                        return false;
                    }
                }

                if (elifBranches.Count > 0)
                {
                    sb.AppendLine("    elif:");
                    foreach (var elif in elifBranches.OrderBy(b => b.Index))
                    {
                        var elifCondition = string.IsNullOrWhiteSpace(elif.Condition) ? "false" : elif.Condition!;
                        sb.AppendLine($"      - condition: {EscapeYamlString(elifCondition)}");
                        var elifChain = CollectBranchChain(elif.TargetId, outgoing, incomingCount, branchVisited, nodeMap);
                        if (elifChain.Count == 0)
                        {
                            sb.AppendLine("        then: []");
                        }
                        else
                        {
                            sb.AppendLine("        then:");
                            if (!TryGenerateBranchYaml(
                                    elifChain, nodeMap, outgoing, incomingCount, consumedByContainer, result, sb, 10))
                            {
                                return false;
                            }
                        }
                    }
                }

                if (elseTarget != null)
                {
                    var elseChain = CollectBranchChain(elseTarget, outgoing, incomingCount, branchVisited, nodeMap);
                    if (elseChain.Count == 0)
                    {
                        sb.AppendLine("    else: []");
                    }
                    else
                    {
                        sb.AppendLine("    else:");
                        if (!TryGenerateBranchYaml(
                                elseChain, nodeMap, outgoing, incomingCount, consumedByContainer, result, sb, 6))
                        {
                            return false;
                        }
                    }
                }
            }
            else if (string.Equals(blockType, "foreach", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(blockType, "while", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(blockType, "repeat", StringComparison.OrdinalIgnoreCase))
            {
                var doEdge = nodeEdges.FirstOrDefault(edge =>
                    string.Equals(edge.BranchPath, "do", StringComparison.OrdinalIgnoreCase))
                    ?? nodeEdges.FirstOrDefault();

                if (doEdge == null)
                {
                    result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                        ExportDiagnosticSeverity.Error,
                        $"Container block '{blockType}' is missing a 'do' branch connection.",
                        nodeId));
                    return false;
                }

                var doChain = CollectBranchChain(doEdge.TargetId, outgoing, incomingCount, branchVisited, nodeMap);
                if (doChain.Count == 0)
                {
                    sb.AppendLine("    do: []");
                }
                else
                {
                    sb.AppendLine("    do:");
                    if (!TryGenerateBranchYaml(
                            doChain, nodeMap, outgoing, incomingCount, consumedByContainer, result, sb, 6))
                    {
                        return false;
                    }
                }
            }
            else if (string.Equals(blockType, "try", StringComparison.OrdinalIgnoreCase))
            {
                EdgeInfo? doEdge = null;
                EdgeInfo? catchEdge = null;
                EdgeInfo? finallyEdge = null;

                foreach (var edge in nodeEdges)
                {
                    if (string.Equals(edge.BranchPath, "catch", StringComparison.OrdinalIgnoreCase))
                    {
                        catchEdge ??= edge;
                        continue;
                    }

                    if (string.Equals(edge.BranchPath, "finally", StringComparison.OrdinalIgnoreCase))
                    {
                        finallyEdge ??= edge;
                        continue;
                    }

                    if (string.Equals(edge.BranchPath, "try", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(edge.BranchPath, "do", StringComparison.OrdinalIgnoreCase))
                    {
                        doEdge ??= edge;
                        continue;
                    }

                    if (doEdge == null) doEdge = edge;
                    else if (catchEdge == null) catchEdge = edge;
                    else if (finallyEdge == null) finallyEdge = edge;
                }

                if (doEdge == null)
                {
                    result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                        ExportDiagnosticSeverity.Error,
                        "Try block is missing a 'do' branch connection.",
                        nodeId));
                    return false;
                }

                var doChain = CollectBranchChain(doEdge.TargetId, outgoing, incomingCount, branchVisited, nodeMap);
                if (doChain.Count == 0)
                {
                    sb.AppendLine("    do: []");
                }
                else
                {
                    sb.AppendLine("    do:");
                    if (!TryGenerateBranchYaml(
                            doChain, nodeMap, outgoing, incomingCount, consumedByContainer, result, sb, 6))
                    {
                        return false;
                    }
                }

                if (catchEdge != null)
                {
                    var catchChain = CollectBranchChain(catchEdge.TargetId, outgoing, incomingCount, branchVisited, nodeMap);
                    if (catchChain.Count == 0)
                    {
                        sb.AppendLine("    catch: []");
                    }
                    else
                    {
                        sb.AppendLine("    catch:");
                        if (!TryGenerateBranchYaml(
                                catchChain, nodeMap, outgoing, incomingCount, consumedByContainer, result, sb, 6))
                        {
                            return false;
                        }
                    }
                }

                if (finallyEdge != null)
                {
                    var finallyChain = CollectBranchChain(finallyEdge.TargetId, outgoing, incomingCount, branchVisited, nodeMap);
                    if (finallyChain.Count == 0)
                    {
                        sb.AppendLine("    finally: []");
                    }
                    else
                    {
                        sb.AppendLine("    finally:");
                        if (!TryGenerateBranchYaml(
                                finallyChain, nodeMap, outgoing, incomingCount, consumedByContainer, result, sb, 6))
                        {
                            return false;
                        }
                    }
                }
            }
            else if (string.Equals(blockType, "switch", StringComparison.OrdinalIgnoreCase))
            {
                var caseBranches = new List<BranchExportInfo>();
                string? defaultTarget = null;
                var fallbackCaseIndex = 0;

                foreach (var edge in nodeEdges)
                {
                    // Imported switch edges carry the branch as a label ("default") with no
                    // branchPath; canvas-authored edges carry data.branchPath. Recognize both so
                    // the default branch is not mistaken for an anonymous fallback case.
                    if (string.Equals(edge.BranchPath, "default", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(edge.BranchPath, "else", StringComparison.OrdinalIgnoreCase) ||
                        (string.IsNullOrEmpty(edge.BranchPath) &&
                         string.Equals(edge.Label?.Trim(), "default", StringComparison.OrdinalIgnoreCase)))
                    {
                        defaultTarget ??= edge.TargetId;
                        continue;
                    }

                    if (TryParseIndexedScope(edge.BranchPath, "cases", out var caseIndex))
                    {
                        caseBranches.Add(new BranchExportInfo
                        {
                            Index = caseIndex,
                            TargetId = edge.TargetId,
                            CaseValue = edge.CaseValue ?? ParseCaseValueFromLabel(edge.Label) ?? $"case_{caseIndex + 1}",
                        });
                        continue;
                    }

                    caseBranches.Add(new BranchExportInfo
                    {
                        Index = fallbackCaseIndex++,
                        TargetId = edge.TargetId,
                        CaseValue = edge.CaseValue ?? ParseCaseValueFromLabel(edge.Label) ?? $"case_{fallbackCaseIndex}",
                    });
                }

                if (caseBranches.Count == 0)
                {
                    result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                        ExportDiagnosticSeverity.Error,
                        "Switch block is missing case branch connections.",
                        nodeId));
                    return false;
                }

                sb.AppendLine("    cases:");
                foreach (var branch in caseBranches.OrderBy(b => b.Index))
                {
                    var caseChain = CollectBranchChain(branch.TargetId, outgoing, incomingCount, branchVisited, nodeMap);
                    sb.AppendLine($"      - value: {EscapeYamlString(branch.CaseValue ?? $"case_{branch.Index + 1}")}");
                    if (caseChain.Count == 0)
                    {
                        sb.AppendLine("        do: []");
                    }
                    else
                    {
                        sb.AppendLine("        do:");
                        if (!TryGenerateBranchYaml(
                                caseChain, nodeMap, outgoing, incomingCount, consumedByContainer, result, sb, 10))
                        {
                            return false;
                        }
                    }
                }

                if (defaultTarget != null)
                {
                    var defaultChain = CollectBranchChain(defaultTarget, outgoing, incomingCount, branchVisited, nodeMap);
                    if (defaultChain.Count == 0)
                    {
                        sb.AppendLine("    default: []");
                    }
                    else
                    {
                        sb.AppendLine("    default:");
                        if (!TryGenerateBranchYaml(
                                defaultChain, nodeMap, outgoing, incomingCount, consumedByContainer, result, sb, 6))
                        {
                            return false;
                        }
                    }
                }
            }
            else if (string.Equals(blockType, "parallel", StringComparison.OrdinalIgnoreCase))
            {
                var branches = new List<BranchExportInfo>();
                var fallbackParallelIndex = 0;

                foreach (var edge in nodeEdges)
                {
                    if (TryParseIndexedScope(edge.BranchPath, "parallel", out var branchIndex))
                    {
                        branches.Add(new BranchExportInfo { Index = branchIndex, TargetId = edge.TargetId });
                    }
                    else
                    {
                        branches.Add(new BranchExportInfo { Index = fallbackParallelIndex++, TargetId = edge.TargetId });
                    }
                }

                if (branches.Count == 0)
                {
                    result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                        ExportDiagnosticSeverity.Error,
                        "Parallel block is missing branch connections.",
                        nodeId));
                    return false;
                }

                sb.AppendLine("    steps:");
                foreach (var branch in branches.OrderBy(b => b.Index))
                {
                    var branchChain = CollectBranchChain(branch.TargetId, outgoing, incomingCount, branchVisited, nodeMap);
                    if (branchChain.Count == 0)
                    {
                        continue;
                    }

                    if (branchChain.Count > 1)
                    {
                        result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                            ExportDiagnosticSeverity.Warning,
                            "Parallel branch contains multiple sequential nodes; exporting the first node only.",
                            nodeId));
                    }

                    if (!TryGenerateSingleNodeYaml(
                            branchChain[0], nodeMap, outgoing, incomingCount, consumedByContainer, result, out var branchYaml))
                    {
                        return false;
                    }

                    sb.AppendLine(IndentYaml(branchYaml.TrimEnd(), 6));
                }
            }

            foreach (var id in branchVisited)
                consumedByContainer.Add(id);

            yaml = sb.ToString().TrimEnd();
            return true;
        }

        private static bool TryParseIndexedScope(string? branchPath, string scopePrefix, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(branchPath))
                return false;

            var parts = branchPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return false;

            if (!string.Equals(parts[0], scopePrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            return int.TryParse(parts[1], out index);
        }

        private static string? ParseConditionFromLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return null;

            var trimmed = label.Trim();
            if (trimmed.StartsWith("elif:", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring("elif:".Length).Trim();

            return null;
        }

        private static string? ParseCaseValueFromLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return null;

            var trimmed = label.Trim();
            if (trimmed.StartsWith("case:", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring("case:".Length).Trim();

            return null;
        }

        private bool TryGenerateSingleNodeYaml(
            string nodeId,
            Dictionary<string, JToken> nodeMap,
            Dictionary<string, List<EdgeInfo>> outgoing,
            Dictionary<string, int> incomingCount,
            HashSet<string> consumedByContainer,
            FlowCanvasExportResult result,
            out string nodeYaml)
        {
            nodeYaml = string.Empty;

            if (!nodeMap.TryGetValue(nodeId, out var node))
                return true;

            var nodeData = node["data"];
            var nodeProps = nodeData?["props"] as JObject;
            var nodeBlockType = nodeData?["blockType"]?.ToString() ?? "print";
            var snippet = nodeProps?["_yamlSnippet"]?.ToString();
            var forceGraphExport = HasForceGraphExport(nodeProps);

            if (IsContainerBlockType(nodeBlockType) &&
                (forceGraphExport ||
                 string.IsNullOrWhiteSpace(snippet) ||
                  HasGraphAuthoredContainerBranches(nodeId, outgoing, nodeMap)))
            {
                if (!TryGenerateContainerFromGraph(
                        nodeBlockType,
                        nodeProps,
                        nodeId,
                        outgoing,
                        nodeMap,
                        incomingCount,
                        consumedByContainer,
                        result,
                        out nodeYaml))
                {
                    return false;
                }

                return true;
            }

            if (IsContainerBlockType(nodeBlockType) && !forceGraphExport && !string.IsNullOrWhiteSpace(snippet))
            {
                nodeYaml = NormalizeTopLevelSnippetIndent(snippet).TrimEnd();
                return true;
            }

            if (!TryGenerateStepYaml(nodeBlockType, nodeProps, out nodeYaml, out var nodeError))
            {
                result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                    ExportDiagnosticSeverity.Error,
                    string.IsNullOrWhiteSpace(nodeError)
                        ? $"Unable to export block type '{nodeBlockType}' inside container."
                        : nodeError!,
                    nodeId));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Generates indented YAML for a list of nodes within a branch (then/else/do/catch/finally).
        /// </summary>
        private bool TryGenerateBranchYaml(
            List<string> nodeChain,
            Dictionary<string, JToken> nodeMap,
            Dictionary<string, List<EdgeInfo>> outgoing,
            Dictionary<string, int> incomingCount,
            HashSet<string> consumedByContainer,
            FlowCanvasExportResult result,
            StringBuilder sb,
            int indent)
        {
            foreach (var childId in nodeChain)
            {
                if (!TryGenerateSingleNodeYaml(
                        childId,
                        nodeMap,
                        outgoing,
                        incomingCount,
                        consumedByContainer,
                        result,
                        out var childYaml))
                {
                    return false;
                }

                sb.AppendLine(IndentYaml(childYaml.TrimEnd(), indent));
            }

            return true;
        }

        private static bool TryGenerateContainerHeaderYaml(
            string blockType,
            JObject? props,
            out string yaml,
            out string? error)
        {
            yaml = string.Empty;
            error = null;

            if (!TryResolveCommandKey(blockType, out var commandKey))
            {
                error = $"Unsupported block type '{blockType}'.";
                return false;
            }

            var options = new JObject();

            void SetScalarOptionIfPresent(string propKey, string optionKey)
            {
                var token = props?[propKey];
                if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                    return;

                var text = token.Type == JTokenType.String ? token.ToString() : token.ToString(Newtonsoft.Json.Formatting.None);
                if (string.IsNullOrWhiteSpace(text))
                    return;

                options[optionKey] = token.Type == JTokenType.String ? new JValue(text) : token.DeepClone();
            }

            if (string.Equals(blockType, "if", StringComparison.OrdinalIgnoreCase))
            {
                SetScalarOptionIfPresent("condition", "condition");
                if (options["condition"] == null)
                {
                    error = "If block is missing required condition.";
                    return false;
                }
            }
            else if (string.Equals(blockType, "foreach", StringComparison.OrdinalIgnoreCase))
            {
                SetScalarOptionIfPresent("iterator", "iterator");
                SetScalarOptionIfPresent("when", "when");
                if (options["iterator"] == null)
                {
                    error = "Foreach block is missing required iterator.";
                    return false;
                }
            }
            else if (string.Equals(blockType, "while", StringComparison.OrdinalIgnoreCase))
            {
                SetScalarOptionIfPresent("condition", "condition");
                if (props?["max_iterations"] != null)
                    options["max_iterations"] = props["max_iterations"]!.DeepClone();
                if (options["condition"] == null)
                {
                    error = "While block is missing required condition.";
                    return false;
                }
            }
            else if (string.Equals(blockType, "repeat", StringComparison.OrdinalIgnoreCase))
            {
                SetScalarOptionIfPresent("until", "until");
                if (props?["max_iterations"] != null)
                    options["max_iterations"] = props["max_iterations"]!.DeepClone();
                if (options["until"] == null)
                {
                    error = "Repeat block is missing required until condition.";
                    return false;
                }
            }
            else if (string.Equals(blockType, "switch", StringComparison.OrdinalIgnoreCase))
            {
                SetScalarOptionIfPresent("value", "value");
                if (options["value"] == null)
                {
                    error = "Switch block is missing required value expression.";
                    return false;
                }
            }

            var commandValue =
                (string.Equals(blockType, "try", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(blockType, "parallel", StringComparison.OrdinalIgnoreCase))
                && options.Count == 0
                    ? JValue.CreateNull()
                    : BuildCommandValueToken(commandKey, options);
            return TrySerializeStepYaml(commandKey, commandValue, out yaml, out error);
        }

        /// <summary>
        /// Indents every line of a YAML string by the specified number of spaces.
        /// </summary>
        private static string IndentYaml(string yaml, int spaces)
        {
            var prefix = new string(' ', spaces);
            var lines = yaml.Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (i > 0) sb.AppendLine();
                if (line.Length > 0)
                    sb.Append(prefix + line);
                else
                    sb.Append(line);
            }
            return sb.ToString();
        }

        private static string BuildStepPath(string scopePath, int index)
        {
            return $"{scopePath}/{index}";
        }

        private static string BuildScopePath(string parentStepPath, string scopePath)
        {
            if (string.IsNullOrWhiteSpace(parentStepPath)) return scopePath;
            if (string.IsNullOrWhiteSpace(scopePath)) return parentStepPath;
            return $"{parentStepPath}/{scopePath}";
        }

        /// <summary>
        /// Creates a meaningful display label from the block type and preview text.
        /// Shows "Get Version" instead of "SEND" when there's useful context.
        /// </summary>
        private static string GetDisplayLabel(string blockType, string? previewText)
        {
            if (string.IsNullOrWhiteSpace(previewText))
                return char.ToUpper(blockType[0]) + blockType.Substring(1);

            // For SET, show the variable name being assigned
            if (blockType == "set" && previewText.Contains('='))
            {
                var varName = previewText.Split('=')[0].Trim();
                return $"set {varName}";
            }

            // Truncate long previews
            if (previewText.Length > 35)
                previewText = previewText.Substring(0, 32) + "...";

            return previewText;
        }

        /// <summary>
        /// Extracts individual property values from a parsed ScriptStep into the props JObject.
        /// These populate the Properties panel fields when a block is clicked.
        /// </summary>
        private static void ExtractStepProperties(ScriptStep step, StepType stepType, JObject props)
        {
            static void SetIfNotNull(JObject target, string key, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    target[key] = value;
            }

            static void SetIfNumber(JObject target, string key, int? value)
            {
                if (value.HasValue)
                    target[key] = value.Value;
            }

            static void SetIfDouble(JObject target, string key, double? value)
            {
                if (value.HasValue)
                    target[key] = value.Value;
            }

            static void SetIfBoolTrue(JObject target, string key, bool value)
            {
                if (value)
                    target[key] = true;
            }

            static void SetIfNullableBool(JObject target, string key, bool? value)
            {
                if (value.HasValue)
                    target[key] = value.Value;
            }

            static JArray SerializeChoiceOptions(IEnumerable<ChoiceOption> options)
            {
                var serialized = new JArray();

                foreach (var option in options)
                {
                    var label = option.Label?.Trim() ?? string.Empty;
                    var value = option.Value?.Trim() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(value))
                        continue;

                    if (string.IsNullOrWhiteSpace(label))
                        label = value;
                    if (string.IsNullOrWhiteSpace(value))
                        value = label;

                    if (string.Equals(label, value, StringComparison.Ordinal))
                    {
                        serialized.Add(value);
                    }
                    else
                    {
                        serialized.Add(new JObject
                        {
                            ["label"] = label,
                            ["value"] = value
                        });
                    }
                }

                return serialized;
            }

            // Common options shared across multiple commands.
            SetIfNumber(props, "timeout", step.Timeout);
            SetIfNotNull(props, "on_error", step.OnError);
            SetIfNotNull(props, "when", step.When);
            SetIfNotNull(props, "capture", step.Capture);
            SetIfNotNull(props, "expect", step.Expect);
            SetIfBoolTrue(props, "suppress", step.Suppress);
            SetIfNumber(props, "retry", step.Retry);
            SetIfNumber(props, "retry_delay", step.RetryDelay);
            SetIfBoolTrue(props, "fail_on_nonzero", step.FailOnNonZero);
            if (step.Respond?.Count > 0)
            {
                props["respond"] = new JArray(step.Respond.Select(pair => new JObject
                {
                    ["expect"] = pair.Expect,
                    ["reply"] = pair.Reply,
                }));
            }

            switch (stepType)
            {
                case StepType.Send:
                    SetIfNotNull(props, "command", step.Send);
                    break;

                case StepType.Print:
                    SetIfNotNull(props, "message", step.Print);
                    break;

                case StepType.Wait:
                    if (step.Wait.HasValue)
                    {
                        props["seconds"] = step.Wait.Value;
                        props["duration"] = step.Wait.Value;
                    }
                    break;

                case StepType.Set:
                    SetIfNotNull(props, "expression", step.Set);
                    break;

                case StepType.Exit:
                {
                    if (TrySplitExitStatusAndMessage(step.Exit, out var status, out var message))
                    {
                        SetIfNotNull(props, "status", status);
                        SetIfNotNull(props, "message", message);
                    }
                    else
                    {
                        SetIfNotNull(props, "status", step.Exit);
                    }
                    break;
                }

                case StepType.Extract:
                    if (step.Extract != null)
                    {
                        SetIfNotNull(props, "pattern", step.Extract.Pattern);
                        if (step.Extract.Into != null)
                            props["into"] = JToken.FromObject(step.Extract.Into);
                        SetIfNotNull(props, "from", step.Extract.From);
                        SetIfNotNull(props, "match", step.Extract.Match);
                        props["required"] = step.Extract.Required;
                    }
                    break;

                case StepType.If:
                    SetIfNotNull(props, "condition", step.If);
                    break;

                case StepType.Foreach:
                    SetIfNotNull(props, "iterator", step.Foreach);
                    break;

                case StepType.While:
                    SetIfNotNull(props, "condition", step.While);
                    SetIfNumber(props, "max_iterations", step.MaxIterations);
                    break;

                case StepType.Repeat:
                    SetIfNotNull(props, "until", step.Until);
                    SetIfNumber(props, "max_iterations", step.MaxIterations);
                    break;

                case StepType.Switch:
                    SetIfNotNull(props, "value", step.Switch);
                    break;

                case StepType.Call:
                    if (step.Call != null)
                    {
                        SetIfNotNull(props, "subroutine", step.Call.Subroutine);
                        if (step.Call.Args.Count > 0) props["args"] = JObject.FromObject(step.Call.Args);
                        if (step.Call.Out.Count > 0) props["out"] = JObject.FromObject(step.Call.Out);
                    }
                    break;

                case StepType.Assert:
                    if (step.Assert != null)
                    {
                        SetIfNotNull(props, "condition", step.Assert.Condition);
                        SetIfNotNull(props, "message", step.Assert.Message);
                        SetIfNotNull(props, "severity", step.Assert.Severity);
                    }
                    break;

                case StepType.Parse:
                    if (step.Parse != null)
                    {
                        SetIfNotNull(props, "format", step.Parse.Format);
                        SetIfNotNull(props, "from", step.Parse.From);
                        SetIfNotNull(props, "into", step.Parse.Into);
                        if (step.Parse.Sections?.Count > 0)
                            props["sections"] = JArray.FromObject(step.Parse.Sections);
                    }
                    break;

                case StepType.Readfile:
                    if (step.Readfile != null)
                    {
                        SetIfNotNull(props, "path", step.Readfile.Path);
                        SetIfNotNull(props, "path_into", step.Readfile.PathInto);
                        SetIfNotNull(props, "into", step.Readfile.Into);
                        SetIfBoolTrue(props, "select_file", step.Readfile.SelectFile);
                        SetIfNullableBool(props, "autobrowse", step.Readfile.AutoBrowse);
                        SetIfBoolTrue(props, "path_only", step.Readfile.PathOnly);
                        SetIfNotNull(props, "message", step.Readfile.Message);
                        SetIfNotNull(props, "fileext", step.Readfile.FileExt);
                        if (!step.Readfile.SkipEmptyLines) props["skip_empty_lines"] = false;
                        if (!step.Readfile.TrimLines) props["trim_lines"] = false;
                        if (step.Readfile.MaxLines != 10000) props["max_lines"] = step.Readfile.MaxLines;
                        if (!string.Equals(step.Readfile.Encoding, "utf-8", StringComparison.OrdinalIgnoreCase))
                            props["encoding"] = step.Readfile.Encoding;
                    }
                    break;

                case StepType.Writefile:
                    if (step.Writefile != null)
                    {
                        SetIfNotNull(props, "path", step.Writefile.Path);
                        SetIfNotNull(props, "content", step.Writefile.Content);
                        SetIfNotNull(props, "mode", step.Writefile.Mode);
                        props["append"] = string.Equals(step.Writefile.Mode, "append", StringComparison.OrdinalIgnoreCase);
                        SetIfNotNull(props, "format", step.Writefile.Format);
                        if (!step.Writefile.Pretty) props["pretty"] = false;
                        if (step.Writefile.Headers != null) props["headers"] = JToken.FromObject(step.Writefile.Headers);
                    }
                    break;

                case StepType.Exists:
                    if (step.Exists != null)
                    {
                        SetIfNotNull(props, "path", step.Exists.Path);
                        SetIfNotNull(props, "into", step.Exists.Into);
                        if (!string.Equals(step.Exists.Type, "any", StringComparison.OrdinalIgnoreCase))
                            props["type"] = step.Exists.Type;
                    }
                    break;

                case StepType.PlaySound:
                    if (step.PlaySound != null)
                    {
                        SetIfNotNull(props, "path", step.PlaySound.Path);
                        if (!step.PlaySound.Wait) props["wait"] = false;
                        if (step.PlaySound.Volume != 100) props["volume"] = step.PlaySound.Volume;
                        SetIfDouble(props, "max_seconds", step.PlaySound.MaxSeconds);
                        SetIfNotNull(props, "into", step.PlaySound.Into);
                    }
                    break;

                case StepType.Input:
                    if (step.Input != null)
                    {
                        SetIfNotNull(props, "title", step.Input.Title);
                        SetIfNotNull(props, "prompt", step.Input.Prompt);
                        SetIfNotNull(props, "into", step.Input.Into);
                        SetIfNotNull(props, "default", step.Input.Default);
                        SetIfBoolTrue(props, "password", step.Input.Password);
                        SetIfNotNull(props, "validate", step.Input.Validate);
                        SetIfNotNull(props, "validation_error", step.Input.ValidationError);
                        SetIfDouble(props, "font_size", step.Input.FontSize);
                    }
                    break;

                case StepType.Choose:
                    if (step.Choose != null)
                    {
                        SetIfNotNull(props, "title", step.Choose.Title);
                        SetIfNotNull(props, "prompt", step.Choose.Prompt);
                        SetIfNotNull(props, "into", step.Choose.Into);
                        SetIfNotNull(props, "default", step.Choose.Default);
                        SetIfDouble(props, "font_size", step.Choose.FontSize);
                        if (!string.IsNullOrWhiteSpace(step.Choose.OptionsFrom))
                        {
                            props["options"] = step.Choose.OptionsFrom;
                        }
                        else if (step.Choose.Options.Count > 0)
                        {
                            props["options"] = SerializeChoiceOptions(step.Choose.Options);
                        }
                    }
                    break;

                case StepType.Multiselect:
                    if (step.Multiselect != null)
                    {
                        SetIfNotNull(props, "title", step.Multiselect.Title);
                        SetIfNotNull(props, "prompt", step.Multiselect.Prompt);
                        SetIfNotNull(props, "into", step.Multiselect.Into);
                        SetIfNumber(props, "min", step.Multiselect.Min);
                        SetIfNumber(props, "max", step.Multiselect.Max);
                        SetIfDouble(props, "font_size", step.Multiselect.FontSize);
                        if (!string.IsNullOrWhiteSpace(step.Multiselect.OptionsFrom))
                        {
                            props["options"] = step.Multiselect.OptionsFrom;
                        }
                        else if (step.Multiselect.Options.Count > 0)
                        {
                            props["options"] = SerializeChoiceOptions(step.Multiselect.Options);
                        }
                    }
                    break;

                case StepType.Confirm:
                    if (step.Confirm != null)
                    {
                        SetIfNotNull(props, "title", step.Confirm.Title);
                        SetIfNotNull(props, "prompt", step.Confirm.Prompt);
                        SetIfNotNull(props, "into", step.Confirm.Into);
                        if (step.Confirm.Default) props["default"] = true;
                        SetIfDouble(props, "font_size", step.Confirm.FontSize);
                    }
                    break;

                case StepType.Ping:
                    if (step.Ping != null)
                    {
                        SetIfNotNull(props, "host", step.Ping.Host);
                        SetIfNotNull(props, "target", step.Ping.Host); // legacy UI field support
                        if (step.Ping.Count != 4) props["count"] = step.Ping.Count;
                        if (step.Ping.Timeout != 3000) props["timeout"] = step.Ping.Timeout;
                        SetIfNotNull(props, "into", step.Ping.Into);
                    }
                    break;

                case StepType.Dns:
                    if (step.Dns != null)
                    {
                        SetIfNotNull(props, "host", step.Dns.Host);
                        SetIfNotNull(props, "hostname", step.Dns.Host); // legacy UI field support
                        SetIfNotNull(props, "type", step.Dns.Type);
                        if (step.Dns.Timeout != 10) props["timeout"] = step.Dns.Timeout;
                        SetIfNotNull(props, "into", step.Dns.Into);
                    }
                    break;

                case StepType.Portcheck:
                    if (step.Portcheck != null)
                    {
                        SetIfNotNull(props, "host", step.Portcheck.Host);
                        props["port"] = step.Portcheck.Port;
                        props["target"] = $"{step.Portcheck.Host}:{step.Portcheck.Port}"; // legacy UI field support
                        if (step.Portcheck.Timeout != 5) props["timeout"] = step.Portcheck.Timeout;
                        SetIfNotNull(props, "into", step.Portcheck.Into);
                    }
                    break;

                case StepType.Http:
                    if (step.Http != null)
                    {
                        SetIfNotNull(props, "url", step.Http.Url);
                        SetIfNotNull(props, "method", step.Http.Method);
                        SetIfNotNull(props, "body", step.Http.Body);
                        SetIfNotNull(props, "into", step.Http.Into);
                        if (step.Http.Headers?.Count > 0) props["headers"] = JObject.FromObject(step.Http.Headers);
                        if (step.Http.Timeout != 30) props["timeout"] = step.Http.Timeout;
                        if (!step.Http.FollowRedirects) props["follow_redirects"] = false;
                        if (step.Http.AllowFailure) props["allow_failure"] = true;
                        if (!step.Http.VerifyTls) props["verify_tls"] = false;
                        SetIfNotNull(props, "auth", step.Http.Auth);
                        SetIfNotNull(props, "username", step.Http.Username);
                        SetIfNotNull(props, "password", step.Http.Password);
                        SetIfNotNull(props, "token", step.Http.Token);
                        SetIfNotNull(props, "content_type", step.Http.ContentType);
                    }
                    break;

                case StepType.Webhook:
                    if (step.Webhook != null)
                    {
                        SetIfNotNull(props, "url", step.Webhook.Url);
                        SetIfNotNull(props, "method", step.Webhook.Method);
                        SetIfNotNull(props, "body", step.Webhook.Body);
                        SetIfNotNull(props, "into", step.Webhook.Into);
                        if (step.Webhook.Headers?.Count > 0) props["headers"] = JObject.FromObject(step.Webhook.Headers);
                        if (step.Webhook.Timeout != 30) props["timeout"] = step.Webhook.Timeout;
                    }
                    break;

                case StepType.Notify:
                    if (step.Notify != null)
                    {
                        SetIfNotNull(props, "profile", step.Notify.Profile);
                        SetIfNotNull(props, "channel", step.Notify.Channel);
                        SetIfNotNull(props, "title", step.Notify.Title);
                        SetIfNotNull(props, "message", step.Notify.Message);
                        if (!string.Equals(step.Notify.Level, "info", StringComparison.OrdinalIgnoreCase))
                            props["level"] = step.Notify.Level;
                        if (step.Notify.Mention != null && step.Notify.Mention.Count > 0)
                            props["mention"] = JArray.FromObject(step.Notify.Mention);
                        if (step.Notify.Attachments != null && step.Notify.Attachments.Count > 0)
                            props["attachments"] = JArray.FromObject(step.Notify.Attachments);
                        SetIfNotNull(props, "into", step.Notify.Into);
                    }
                    break;

                case StepType.Sftp:
                    if (step.Sftp != null)
                    {
                        SetIfNotNull(props, "action", step.Sftp.Action);
                        SetIfNotNull(props, "local_path", step.Sftp.LocalPath);
                        SetIfNotNull(props, "remote_path", step.Sftp.RemotePath);
                        SetIfNotNull(props, "local", step.Sftp.LocalPath);   // legacy UI field support
                        SetIfNotNull(props, "remote", step.Sftp.RemotePath); // legacy UI field support
                        SetIfNotNull(props, "host", step.Sftp.Host);
                        if (step.Sftp.Port.HasValue) props["port"] = step.Sftp.Port.Value;
                        SetIfNotNull(props, "username", step.Sftp.Username);
                        SetIfNotNull(props, "password", step.Sftp.Password);
                        if (!step.Sftp.Overwrite) props["overwrite"] = false;
                        if (step.Sftp.Timeout != 120) props["timeout"] = step.Sftp.Timeout;
                        SetIfNotNull(props, "into", step.Sftp.Into);
                    }
                    break;

                case StepType.Interactive:
                    if (step.Interactive != null)
                    {
                        if (step.Interactive.Session == InteractiveSessionMode.Shared)
                            props["session"] = "shared";
                        SetIfNotNull(props, "title", step.Interactive.Title);
                        SetIfNotNull(props, "command", step.Interactive.Command);
                        SetIfNotNull(props, "capture", step.Interactive.Capture);
                        SetIfNumber(props, "max_seconds", step.Interactive.MaxSeconds);
                        SetIfNumber(props, "max_lines", step.Interactive.MaxLines);
                        SetIfNumber(props, "width", step.Interactive.Width);
                        SetIfNumber(props, "height", step.Interactive.Height);
                        SetIfNumber(props, "columns", step.Interactive.Columns);
                        SetIfNumber(props, "rows", step.Interactive.Rows);
                        if (step.Interactive.MirrorOutput) props["mirror_output"] = true;
                        if (!step.Interactive.ShowWindow) props["show_window"] = false;
                    }
                    break;

                case StepType.Table:
                    if (step.Table != null)
                    {
                        SetIfNotNull(props, "data", step.Table.Data);
                        SetIfNotNull(props, "source", step.Table.Data); // legacy UI field support
                        if (step.Table.Columns != null && step.Table.Columns.Count > 0)
                            props["columns"] = JArray.FromObject(step.Table.Columns);
                        SetIfNotNull(props, "into", step.Table.Into);
                        if (!string.Equals(step.Table.Align, "left", StringComparison.OrdinalIgnoreCase))
                            props["align"] = step.Table.Align;
                        if (!step.Table.ShowHeader)
                            props["show_header"] = false;
                    }
                    break;

                case StepType.UpdateColumn:
                    if (step.UpdateColumn != null)
                    {
                        SetIfNotNull(props, "column", step.UpdateColumn.Column);
                        SetIfNotNull(props, "value", step.UpdateColumn.Value);
                        SetIfNotNull(props, "expression", step.UpdateColumn.Value); // legacy UI field support
                    }
                    break;

                case StepType.UpdateEnvironment:
                    if (step.UpdateEnvironment != null)
                    {
                        SetIfNotNull(props, "variable", step.UpdateEnvironment.Variable);
                        SetIfNotNull(props, "value", step.UpdateEnvironment.Value);
                        SetIfNotNull(props, "expression", step.UpdateEnvironment.Value); // legacy UI field support
                    }
                    break;

                case StepType.BrowserCallbackCapture:
                    if (step.BrowserCallbackCapture != null)
                    {
                        SetIfNotNull(props, "start_url", step.BrowserCallbackCapture.StartUrl);
                        SetIfNotNull(props, "url", step.BrowserCallbackCapture.StartUrl); // legacy UI field support
                        SetIfNotNull(props, "callback_path", step.BrowserCallbackCapture.CallbackPath);
                        if (step.BrowserCallbackCapture.LocalPort != 8086) props["local_port"] = step.BrowserCallbackCapture.LocalPort;
                        if (!string.Equals(step.BrowserCallbackCapture.CaptureMode, "auto", StringComparison.OrdinalIgnoreCase))
                            props["capture_mode"] = step.BrowserCallbackCapture.CaptureMode;
                        if (!string.Equals(step.BrowserCallbackCapture.BrowserMode, "external", StringComparison.OrdinalIgnoreCase))
                            props["browser_mode"] = step.BrowserCallbackCapture.BrowserMode;
                        if (step.BrowserCallbackCapture.ShowAfterSeconds > 0)
                            props["show_after_seconds"] = step.BrowserCallbackCapture.ShowAfterSeconds;
                        SetIfNotNull(props, "into", step.BrowserCallbackCapture.Into);
                        if (step.BrowserCallbackCapture.RequiredFields?.Count > 0)
                            props["required_fields"] = JArray.FromObject(step.BrowserCallbackCapture.RequiredFields);
                        if (step.BrowserCallbackCapture.Timeout != 300) props["timeout"] = step.BrowserCallbackCapture.Timeout;
                        if (!step.BrowserCallbackCapture.OpenBrowser) props["open_browser"] = false;
                        if (!step.BrowserCallbackCapture.AutoCloseBrowser) props["auto_close_browser"] = false;
                        SetIfNotNull(props, "completion_message", step.BrowserCallbackCapture.CompletionMessage);
                        SetIfNotNull(props, "failure_message", step.BrowserCallbackCapture.FailureMessage);
                        if (!step.BrowserCallbackCapture.Quiet) props["quiet"] = false;
                    }
                    break;

                case StepType.LocalCmd:
                    if (step.LocalCmd != null)
                    {
                        SetIfNotNull(props, "command", step.LocalCmd.Command);
                        if (!string.Equals(step.LocalCmd.Shell, "powershell", StringComparison.OrdinalIgnoreCase))
                            props["shell"] = step.LocalCmd.Shell;
                        SetIfNotNull(props, "shell_path", step.LocalCmd.ShellPath);
                        if (step.LocalCmd.Args.Count > 0)
                            props["args"] = JArray.FromObject(step.LocalCmd.Args);
                        if (step.LocalCmd.Env?.Count > 0)
                            props["env"] = JObject.FromObject(step.LocalCmd.Env);
                        SetIfNotNull(props, "working_dir", step.LocalCmd.WorkingDir);
                        if (step.LocalCmd.Interactive) props["interactive"] = true;
                        if (step.LocalCmd.KeepOpen) props["keep_open"] = true;
                        if (!string.Equals(step.LocalCmd.RunMode, "foreground", StringComparison.OrdinalIgnoreCase))
                            props["run_mode"] = step.LocalCmd.RunMode;
                        if (step.LocalCmd.LifetimeSpecified ||
                            !string.Equals(step.LocalCmd.Lifetime, "detached", StringComparison.OrdinalIgnoreCase))
                            props["lifetime"] = step.LocalCmd.Lifetime;
                        if (step.LocalCmd.KillOnCancel) props["kill_on_cancel"] = true;
                        if (!step.LocalCmd.FailOnNonZero) props["fail_on_nonzero"] = false;
                        if (step.LocalCmd.SuccessCodes.Count != 1 || step.LocalCmd.SuccessCodes[0] != 0)
                            props["success_codes"] = JArray.FromObject(step.LocalCmd.SuccessCodes);
                        if (step.LocalCmd.MaxOutputBytes != 1024 * 1024)
                            props["max_output_bytes"] = step.LocalCmd.MaxOutputBytes;
                        if (!string.Equals(step.LocalCmd.Confirm, "always", StringComparison.OrdinalIgnoreCase))
                            props["confirm"] = step.LocalCmd.Confirm;
                        if (step.LocalCmd.Quiet) props["quiet"] = true;
                        if (step.LocalCmd.Suppress) props["suppress"] = true;
                        SetIfNotNull(props, "title", step.LocalCmd.Title);
                        SetIfNotNull(props, "into", step.LocalCmd.Into);
                    }
                    break;

                case StepType.Vault:
                    if (step.Vault != null)
                    {
                        SetIfNotNull(props, "path", step.Vault.Path);
                        SetIfNotNull(props, "profile", step.Vault.Profile);
                        SetIfNotNull(props, "key", step.Vault.Key);
                        SetIfNotNull(props, "into", step.Vault.Into);
                        SetIfNumber(props, "version", step.Vault.Version);
                        if (step.Vault.Keys?.Count > 0)
                            props["keys"] = JObject.FromObject(step.Vault.Keys);
                        if (step.Vault.Write?.Count > 0)
                            props["write"] = JObject.FromObject(step.Vault.Write);
                        if (step.Vault.Patch?.Count > 0)
                            props["patch"] = JObject.FromObject(step.Vault.Patch);
                        SetIfNotNull(props, "on_error", step.Vault.OnError);
                    }
                    break;

                case StepType.SetHistoryLabel:
                    switch (step.SetHistoryLabel)
                    {
                        case string label:
                            SetIfNotNull(props, "value", label);
                            break;
                        case SetHistoryLabelOptions options:
                            SetIfNotNull(props, "value", options.Value);
                            if (options.Replace.HasValue)
                                props["replace"] = options.Replace.Value ? "true" : "false";
                            if (!string.Equals(options.Mode, HistoryLabelOperation.ReplaceMode, StringComparison.OrdinalIgnoreCase))
                                props["mode"] = HistoryLabelOperation.NormalizeMode(options.Mode);
                            SetIfNotNull(props, "separator", options.Separator);
                            break;
                    }
                    break;

                case StepType.Log:
                    switch (step.Log)
                    {
                        case LogOptions logOptions:
                            SetIfNotNull(props, "message", logOptions.Message);
                            SetIfNotNull(props, "level", logOptions.Level);
                            break;
                        case string message:
                            SetIfNotNull(props, "message", message);
                            break;
                    }
                    break;
            }
        }

        private static bool TryGenerateStepYaml(string blockType, JObject? props, out string yaml, out string? error)
        {
            error = null;
            yaml = string.Empty;

            if (!TryResolveCommandKey(blockType, out var commandKey))
            {
                error = $"Unsupported block type '{blockType}'.";
                return false;
            }

            var supportedOptionsByCommand = ScriptParser.GetKnownStepOptionKeysByCommand();
            var supportedRootOptionsByCommand = ScriptParser.GetKnownStepRootOptionKeysByCommand();
            var allowedOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (supportedOptionsByCommand.TryGetValue(commandKey, out var commandOptions))
            {
                foreach (var option in commandOptions)
                    allowedOptions.Add(option);
            }

            if (supportedRootOptionsByCommand.TryGetValue(commandKey, out var rootOptions))
            {
                foreach (var option in rootOptions)
                    allowedOptions.Add(option);
            }

            var yamlSnippet = props?["_yamlSnippet"]?.ToString();
            if (!TryParseSnippetOptions(yamlSnippet, commandKey, out var options, out error))
                return false;

            var unsupportedProps = new List<string>();
            var stepRootOptions = new JObject();
            if (props != null)
            {
                foreach (var property in props.Properties())
                {
                    if (IsMetadataProperty(property.Name))
                        continue;

                    // `when:` is a step-level guard (a sibling of the command), not a command-map option.
                    if (string.Equals(property.Name, "when", StringComparison.OrdinalIgnoreCase))
                    {
                        var whenText = property.Value.Type == JTokenType.String
                            ? property.Value.ToString()
                            : property.Value.ToString(Newtonsoft.Json.Formatting.None);
                        if (!string.IsNullOrWhiteSpace(whenText))
                            stepRootOptions["when"] = new JValue(whenText);
                        continue;
                    }

                    if (TryHandleSpecialLegacyProp(commandKey, blockType, property.Name, property.Value, options, out var specialError))
                    {
                        if (!string.IsNullOrWhiteSpace(specialError))
                            unsupportedProps.Add($"{property.Name} ({specialError})");
                        continue;
                    }

                    var optionKey = ResolveOptionKey(blockType, property.Name);
                    if (!allowedOptions.Contains(optionKey))
                    {
                        unsupportedProps.Add(property.Name);
                        continue;
                    }

                    if (!TryNormalizeOptionValue(commandKey, optionKey, property.Value, out var normalizedValue, out var normalizeError))
                    {
                        unsupportedProps.Add($"{property.Name} ({normalizeError})");
                        continue;
                    }

                    if (normalizedValue.Type == JTokenType.Null)
                        options.Remove(optionKey);
                    else
                        options[optionKey] = normalizedValue;
                }
            }

            if (unsupportedProps.Count > 0)
            {
                error =
                    $"Block '{blockType}' contains unsupported or invalid properties: {string.Join(", ", unsupportedProps)}. " +
                    "Adjust Flow Canvas properties to canonical runtime options before export.";
                return false;
            }

            if (!TryEnsureRequiredOptions(commandKey, options, out error))
                return false;

            var orderedOptions = ReorderOptionsForSerialization(commandKey, options);
            var commandValue = BuildCommandValueToken(commandKey, orderedOptions);
            return TrySerializeStepYaml(commandKey, commandValue, stepRootOptions, out yaml, out error);
        }

        private static bool TryResolveCommandKey(string blockType, out string commandKey)
        {
            if (BlockTypeToCommandKey.TryGetValue(blockType, out var mapped))
            {
                commandKey = mapped;
                return true;
            }

            if (ScriptParser.GetKnownStepCommands().Contains(blockType, StringComparer.OrdinalIgnoreCase))
            {
                commandKey = blockType;
                return true;
            }

            commandKey = string.Empty;
            return false;
        }

        private static bool IsMetadataProperty(string propertyName)
        {
            return propertyName.StartsWith("_", StringComparison.Ordinal);
        }

        private static bool HasForceGraphExport(JObject? props)
        {
            var token = props?["_forceGraphExport"];
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return false;

            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();

            if (token.Type == JTokenType.Integer)
                return token.Value<int>() != 0;

            if (token.Type == JTokenType.String &&
                bool.TryParse(token.ToString(), out var parsed))
            {
                return parsed;
            }

            return true;
        }

        private static string ResolveOptionKey(string blockType, string propName)
        {
            if (BlockPropAliasesByType.TryGetValue(blockType, out var aliases) &&
                aliases.TryGetValue(propName, out var mapped))
            {
                return mapped;
            }

            return propName;
        }

        private static bool TryHandleSpecialLegacyProp(
            string commandKey,
            string blockType,
            string propName,
            JToken propValue,
            JObject options,
            out string? error)
        {
            error = null;

            if (string.Equals(commandKey, "send", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propName, "delay", StringComparison.OrdinalIgnoreCase))
            {
                error = "use send.retry_delay (send.delay is not a runtime option)";
                return true;
            }

            if (string.Equals(commandKey, "interactive", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propName, "timeout", StringComparison.OrdinalIgnoreCase))
            {
                error = "use interactive.max_seconds (interactive.timeout is not a runtime option)";
                return true;
            }

            if (string.Equals(commandKey, "return", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propName, "value", StringComparison.OrdinalIgnoreCase))
            {
                error = "return.value is not supported by runtime syntax";
                return true;
            }

            if (string.Equals(commandKey, "portcheck", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propName, "target", StringComparison.OrdinalIgnoreCase))
            {
                var text = propValue.Type == JTokenType.Null ? null : propValue.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    options.Remove("host");
                    options.Remove("port");
                    return true;
                }

                if (!TryParseTargetHostPort(text!, out var host, out var port))
                {
                    error = "target must be in 'host:port' form";
                    return true;
                }

                options["host"] = host;
                options["port"] = port;
                return true;
            }

            if (string.Equals(commandKey, "writefile", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propName, "append", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryNormalizeBoolean(propValue, out var append))
                {
                    error = "append must be true or false";
                    return true;
                }

                options["mode"] = append ? "append" : "overwrite";
                return true;
            }

            if (string.Equals(commandKey, "foreach", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propName, "expression", StringComparison.OrdinalIgnoreCase))
            {
                // Legacy panel modeled foreach as variable + expression. Runtime expects "iterator".
                var iterator = options["iterator"]?.ToString();
                if (string.IsNullOrWhiteSpace(iterator))
                    return false;

                var expression = propValue.ToString();
                if (string.IsNullOrWhiteSpace(expression))
                {
                    options.Remove("iterator");
                }
                else
                {
                    var variable = iterator;
                    var inIndex = iterator.IndexOf(" in ", StringComparison.Ordinal);
                    if (inIndex > 0)
                        variable = iterator[..inIndex];

                    options["iterator"] = $"{variable} in {expression}";
                }

                return true;
            }

            if (string.Equals(commandKey, "foreach", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propName, "variable", StringComparison.OrdinalIgnoreCase))
            {
                var iterator = options["iterator"]?.ToString();
                var variable = propValue.ToString();

                if (string.IsNullOrWhiteSpace(variable))
                {
                    options.Remove("iterator");
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(iterator))
                {
                    var inIndex = iterator.IndexOf(" in ", StringComparison.Ordinal);
                    var expression = inIndex > 0 ? iterator[(inIndex + 4)..] : string.Empty;
                    options["iterator"] = string.IsNullOrWhiteSpace(expression)
                        ? variable
                        : $"{variable} in {expression}";
                }
                else
                {
                    options["iterator"] = variable;
                }

                return true;
            }

            return false;
        }

        private static bool TryNormalizeOptionValue(
            string commandKey,
            string optionKey,
            JToken value,
            out JToken normalized,
            out string? error)
        {
            error = null;
            normalized = JValue.CreateNull();

            if (value.Type == JTokenType.Null || value.Type == JTokenType.Undefined)
            {
                normalized = JValue.CreateNull();
                return true;
            }

            if (string.Equals(optionKey, "default", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(commandKey, "confirm", StringComparison.OrdinalIgnoreCase))
            {
                if (TryNormalizeBoolean(value, out var confirmDefault))
                {
                    normalized = new JValue(confirmDefault);
                    return true;
                }

                error = "must be true or false";
                return false;
            }

            if (string.Equals(optionKey, "password", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(commandKey, "input", StringComparison.OrdinalIgnoreCase))
            {
                if (TryNormalizeBoolean(value, out var passwordFlag))
                {
                    normalized = new JValue(passwordFlag);
                    return true;
                }

                error = "must be true or false";
                return false;
            }

            if (string.Equals(commandKey, "writefile", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(optionKey, "headers", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Type == JTokenType.Array)
                {
                    normalized = value.DeepClone();
                    return true;
                }

                var headerText = value.ToString();
                if (string.IsNullOrWhiteSpace(headerText))
                {
                    normalized = JValue.CreateNull();
                    return true;
                }

                normalized = new JArray(SplitCommaSeparated(headerText).Select(s => (JToken)new JValue(s)));
                return true;
            }

            if (string.Equals(commandKey, "localcmd", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(optionKey, "args", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Type == JTokenType.Array)
                {
                    normalized = value.DeepClone();
                    return true;
                }

                if (value.Type == JTokenType.String)
                {
                    var raw = value.ToString();
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        normalized = JValue.CreateNull();
                        return true;
                    }

                    try
                    {
                        var parsed = JToken.Parse(raw);
                        if (parsed.Type == JTokenType.Array)
                        {
                            normalized = parsed;
                            return true;
                        }
                    }
                    catch
                    {
                        // Scalar string form is valid for localcmd.args.
                    }

                    normalized = new JValue(raw);
                    return true;
                }

                normalized = value.DeepClone();
                return true;
            }

            if (string.Equals(commandKey, "localcmd", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(optionKey, "env", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Type == JTokenType.Object)
                {
                    normalized = value.DeepClone();
                    return true;
                }

                var raw = value.ToString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    normalized = JValue.CreateNull();
                    return true;
                }

                try
                {
                    var parsed = JToken.Parse(raw);
                    if (parsed.Type == JTokenType.Object)
                    {
                        normalized = parsed;
                        return true;
                    }
                }
                catch
                {
                    // fall through
                }

                error = "must be a JSON object mapping";
                return false;
            }

            if (DictionaryOptionKeys.Contains(optionKey))
            {
                if (value.Type == JTokenType.Object)
                {
                    normalized = value.DeepClone();
                    return true;
                }

                var raw = value.ToString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    normalized = JValue.CreateNull();
                    return true;
                }

                try
                {
                    var parsed = JToken.Parse(raw);
                    if (parsed.Type == JTokenType.Object)
                    {
                        normalized = parsed;
                        return true;
                    }
                }
                catch
                {
                    // fall through
                }

                error = "must be an object mapping";
                return false;
            }

            if (string.Equals(optionKey, "respond", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Type == JTokenType.Array)
                {
                    normalized = value.DeepClone();
                    return true;
                }

                error = "send.respond must be a list of { expect, reply } items";
                return false;
            }

            if (string.Equals(optionKey, "columns", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(commandKey, "table", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Type == JTokenType.Array)
                {
                    normalized = NormalizeTableColumns(value as JArray ?? new JArray());
                    return true;
                }

                var columnText = value.ToString();
                if (string.IsNullOrWhiteSpace(columnText))
                {
                    normalized = JValue.CreateNull();
                    return true;
                }

                normalized = new JArray(
                    SplitCommaSeparated(columnText).Select(column => new JObject
                    {
                        ["header"] = column,
                        ["field"] = column,
                    }));
                return true;
            }

            if (ListOptionKeys.Contains(optionKey))
            {
                if (value.Type == JTokenType.Array)
                {
                    normalized = value.DeepClone();
                    return true;
                }

                var rawList = value.ToString();
                if (string.IsNullOrWhiteSpace(rawList))
                {
                    normalized = JValue.CreateNull();
                    return true;
                }

                normalized = new JArray(SplitCommaSeparated(rawList).Select(s => (JToken)new JValue(s)));
                return true;
            }

            if (string.Equals(optionKey, "options", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(commandKey, "choose", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(commandKey, "multiselect", StringComparison.OrdinalIgnoreCase)))
            {
                if (value.Type == JTokenType.Array)
                {
                    normalized = value.DeepClone();
                    return true;
                }

                var rawOptions = value.ToString();
                if (string.IsNullOrWhiteSpace(rawOptions))
                {
                    normalized = JValue.CreateNull();
                    return true;
                }

                if (rawOptions.Contains(",", StringComparison.Ordinal))
                {
                    normalized = new JArray(SplitCommaSeparated(rawOptions).Select(s => (JToken)new JValue(s)));
                    return true;
                }

                normalized = new JValue(rawOptions);
                return true;
            }

            if (BooleanOptionKeys.Contains(optionKey))
            {
                if (TryNormalizeBoolean(value, out var boolValue))
                {
                    normalized = new JValue(boolValue);
                    return true;
                }

                error = "must be true or false";
                return false;
            }

            if (IntegerOptionKeys.Contains(optionKey))
            {
                if (TryNormalizeInteger(value, out var intValue))
                {
                    normalized = new JValue(intValue);
                    return true;
                }

                error = "must be an integer";
                return false;
            }

            if (value.Type == JTokenType.String)
            {
                var text = value.ToString();
                if (AllowsWhitespaceOnlyRequiredValue(commandKey, optionKey))
                {
                    normalized = text.Length == 0 ? JValue.CreateNull() : new JValue(text);
                    return true;
                }

                normalized = string.IsNullOrWhiteSpace(text) ? JValue.CreateNull() : new JValue(text);
                return true;
            }

            normalized = value.DeepClone();
            return true;
        }

        private static bool TryNormalizeBoolean(JToken value, out bool normalized)
        {
            normalized = false;
            if (value.Type == JTokenType.Boolean)
            {
                normalized = value.Value<bool>();
                return true;
            }

            if (value.Type == JTokenType.Integer)
            {
                normalized = value.Value<int>() != 0;
                return true;
            }

            var text = value.ToString().Trim();
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "1", StringComparison.OrdinalIgnoreCase))
            {
                normalized = true;
                return true;
            }

            if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "0", StringComparison.OrdinalIgnoreCase))
            {
                normalized = false;
                return true;
            }

            return false;
        }

        private static bool TryNormalizeInteger(JToken value, out int normalized)
        {
            normalized = 0;
            if (value.Type == JTokenType.Integer)
            {
                normalized = value.Value<int>();
                return true;
            }

            return int.TryParse(value.ToString(), out normalized);
        }

        private static JArray NormalizeTableColumns(JArray columns)
        {
            var normalized = new JArray();
            foreach (var column in columns)
            {
                if (column.Type == JTokenType.Object)
                {
                    normalized.Add(column.DeepClone());
                }
                else if (column.Type == JTokenType.String)
                {
                    var text = column.ToString();
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    normalized.Add(new JObject
                    {
                        ["header"] = text,
                        ["field"] = text,
                    });
                }
            }

            return normalized;
        }

        private static IReadOnlyList<string> SplitCommaSeparated(string value)
        {
            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToList();
        }

        private static bool TryParseSnippetOptions(string? yamlSnippet, string expectedCommandKey, out JObject options, out string? error)
        {
            options = new JObject();
            error = null;

            if (string.IsNullOrWhiteSpace(yamlSnippet))
                return true;

            var parseText = ScriptParser.PreprocessYaml(BuildSnippetParseText(yamlSnippet!));
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var root = deserializer.Deserialize<Dictionary<object, object?>>(parseText);
                if (!TryGetCaseInsensitiveDictionaryValue(root, "steps", out var stepsObj) || stepsObj is not IList stepsList || stepsList.Count == 0)
                    return true;

                if (stepsList[0] is not IDictionary stepMap || stepMap.Count == 0)
                    return true;

                var enumerator = stepMap.GetEnumerator();
                if (!enumerator.MoveNext())
                    return true;

                var commandKey = enumerator.Key?.ToString() ?? string.Empty;
                if (!string.Equals(commandKey, expectedCommandKey, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Block expected '{expectedCommandKey}' snippet but found '{commandKey}'.";
                    return false;
                }

                var commandValue = ConvertYamlValueToJToken(enumerator.Value);
                if (!TryConvertCommandValueToOptions(expectedCommandKey, commandValue, out options, out error))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to parse existing YAML snippet for '{expectedCommandKey}': {ex.Message}";
                return false;
            }
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

        private static string BuildSnippetParseText(string yamlSnippet)
        {
            var normalized = yamlSnippet
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);
            var lines = normalized.Split('\n');
            var sb = new StringBuilder();
            sb.AppendLine("steps:");
            foreach (var line in lines)
            {
                if (line.Length == 0)
                {
                    sb.AppendLine();
                    continue;
                }

                sb.Append("  ");
                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        private static JToken ConvertYamlValueToJToken(object? value)
        {
            switch (value)
            {
                case null:
                    return JValue.CreateNull();
                case IDictionary dictionary:
                {
                    var obj = new JObject();
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        var key = entry.Key?.ToString();
                        if (string.IsNullOrWhiteSpace(key))
                            continue;
                        obj[key] = ConvertYamlValueToJToken(entry.Value);
                    }

                    return obj;
                }
                case IEnumerable enumerable when value is not string:
                {
                    var array = new JArray();
                    foreach (var item in enumerable)
                        array.Add(ConvertYamlValueToJToken(item));
                    return array;
                }
                case bool b:
                    return new JValue(b);
                case int i:
                    return new JValue(i);
                case long l:
                    return new JValue(l);
                case float f:
                    return new JValue(f);
                case double d:
                    return new JValue(d);
                case decimal m:
                    return new JValue(m);
                default:
                    return new JValue(value.ToString());
            }
        }

        private static bool TryConvertCommandValueToOptions(
            string commandKey,
            JToken commandValue,
            out JObject options,
            out string? error)
        {
            error = null;

            if (commandValue.Type == JTokenType.Null || commandValue.Type == JTokenType.Undefined)
            {
                options = new JObject();
                return true;
            }

            if (commandValue is JObject map)
            {
                options = (JObject)map.DeepClone();
                return true;
            }

            if (commandValue.Type == JTokenType.Array)
            {
                error = $"Unsupported inline YAML shape for '{commandKey}'.";
                options = new JObject();
                return false;
            }

            options = new JObject();
            switch (commandKey.ToLowerInvariant())
            {
                case "send":
                    options["command"] = commandValue;
                    return true;
                case "print":
                    options["message"] = commandValue;
                    return true;
                case "wait":
                    options["seconds"] = commandValue;
                    return true;
                case "set":
                    options["expression"] = commandValue;
                    return true;
                case "if":
                    options["condition"] = commandValue;
                    return true;
                case "foreach":
                    options["iterator"] = commandValue;
                    return true;
                case "while":
                    options["condition"] = commandValue;
                    return true;
                case "repeat":
                    options["until"] = commandValue;
                    return true;
                case "switch":
                    options["value"] = commandValue;
                    return true;
                case "call":
                    options["subroutine"] = commandValue;
                    return true;
                case "ping":
                    options["host"] = commandValue;
                    return true;
                case "http":
                    options["url"] = commandValue;
                    return true;
                case "browser_callback_capture":
                    options["start_url"] = commandValue;
                    return true;
                case "assert":
                    options["condition"] = commandValue;
                    return true;
                case "log":
                    options["message"] = commandValue;
                    return true;
                case "sethistorylabel":
                    options["value"] = commandValue;
                    return true;
                case "exit":
                {
                    if (TrySplitExitStatusAndMessage(commandValue.ToString(), out var status, out var message))
                    {
                        if (!string.IsNullOrWhiteSpace(status)) options["status"] = status;
                        if (!string.IsNullOrWhiteSpace(message)) options["message"] = message;
                    }
                    else
                    {
                        options["status"] = commandValue.ToString();
                    }
                    return true;
                }
                case "localcmd":
                    options["command"] = commandValue;
                    return true;
                case "break":
                case "continue":
                case "return":
                    return true;
                default:
                    error = $"Unsupported inline YAML shape for '{commandKey}'.";
                    return false;
            }
        }

        private static bool TrySplitExitStatusAndMessage(string? exitValue, out string? status, out string? message)
        {
            status = null;
            message = null;

            if (string.IsNullOrWhiteSpace(exitValue))
                return false;

            var trimmed = exitValue.Trim();
            var firstSpace = trimmed.IndexOf(' ');
            if (firstSpace > 0)
            {
                var statusToken = trimmed[..firstSpace];
                if (ExitStatusTokens.Contains(statusToken))
                {
                    status = statusToken;
                    var remainder = trimmed[(firstSpace + 1)..].Trim();
                    if (remainder.Length > 0)
                        message = remainder;
                    return true;
                }
            }

            if (ExitStatusTokens.Contains(trimmed))
            {
                status = trimmed;
                return true;
            }

            status = "success";
            message = trimmed;
            return true;
        }

        private static bool TryEnsureRequiredOptions(string commandKey, JObject options, out string? error)
        {
            error = null;
            if (!RequiredOptionKeysByCommand.TryGetValue(commandKey, out var requiredKeys))
                requiredKeys = [];

            var missing = new List<string>();
            bool HasPresentOption(string optionKey)
            {
                return options.TryGetValue(optionKey, StringComparison.OrdinalIgnoreCase, out var optionToken)
                       && !IsMissingRequiredOptionToken(commandKey, optionKey, optionToken);
            }

            foreach (var key in requiredKeys)
            {
                var readfileSelectFile = string.Equals(commandKey, "readfile", StringComparison.OrdinalIgnoreCase) &&
                    options.TryGetValue("select_file", StringComparison.OrdinalIgnoreCase, out var selectFileToken) &&
                    selectFileToken.Type == JTokenType.Boolean &&
                    selectFileToken.Value<bool>();

                var readfilePathOnly = string.Equals(commandKey, "readfile", StringComparison.OrdinalIgnoreCase) &&
                    options.TryGetValue("path_only", StringComparison.OrdinalIgnoreCase, out var pathOnlyToken) &&
                    pathOnlyToken.Type == JTokenType.Boolean &&
                    pathOnlyToken.Value<bool>();

                if (string.Equals(commandKey, "readfile", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(key, "path", StringComparison.OrdinalIgnoreCase) &&
                    readfileSelectFile)
                {
                    continue;
                }

                if (string.Equals(commandKey, "readfile", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(key, "into", StringComparison.OrdinalIgnoreCase) &&
                    readfilePathOnly)
                {
                    continue;
                }

                if (!options.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token) ||
                    IsMissingRequiredOptionToken(commandKey, key, token))
                {
                    missing.Add(key);
                }
            }

            if (string.Equals(commandKey, "readfile", StringComparison.OrdinalIgnoreCase))
            {
                var readfilePathOnly = options.TryGetValue("path_only", StringComparison.OrdinalIgnoreCase, out var pathOnlyToken) &&
                    pathOnlyToken.Type == JTokenType.Boolean &&
                    pathOnlyToken.Value<bool>();

                if (readfilePathOnly && !HasPresentOption("path_into"))
                {
                    missing.Add("path_into");
                }
            }

            if (string.Equals(commandKey, "http", StringComparison.OrdinalIgnoreCase) &&
                options.TryGetValue("auth", StringComparison.OrdinalIgnoreCase, out var authToken))
            {
                var auth = authToken.ToString().Trim();
                if (!IsDynamicRuntimeValue(auth))
                {
                    if (string.Equals(auth, "basic", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!HasPresentOption("username"))
                            missing.Add("username");
                        if (!HasPresentOption("password"))
                            missing.Add("password");
                    }
                    else if (string.Equals(auth, "bearer", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!HasPresentOption("token"))
                            missing.Add("token");
                    }
                }
            }

            if (string.Equals(commandKey, "interactive", StringComparison.OrdinalIgnoreCase))
            {
                var showWindow = true;
                if (options.TryGetValue("show_window", StringComparison.OrdinalIgnoreCase, out var showWindowToken) &&
                    TryNormalizeBoolean(showWindowToken, out var normalizedShowWindow))
                {
                    showWindow = normalizedShowWindow;
                }

                if (!showWindow)
                {
                    if (!HasPresentOption("command"))
                        missing.Add("command");

                    var hasMaxSeconds = HasPresentOption("max_seconds");
                    var hasMaxLines = HasPresentOption("max_lines");
                    if (!hasMaxSeconds && !hasMaxLines)
                    {
                        missing.Add("max_seconds|max_lines");
                    }
                }
            }

            if (string.Equals(commandKey, "localcmd", StringComparison.OrdinalIgnoreCase) &&
                options.TryGetValue("shell", StringComparison.OrdinalIgnoreCase, out var shellToken))
            {
                var shell = shellToken.ToString().Trim();
                if (!IsDynamicRuntimeValue(shell) &&
                    string.Equals(shell, "custom", StringComparison.OrdinalIgnoreCase) &&
                    !HasPresentOption("shell_path"))
                {
                    missing.Add("shell_path");
                }
            }

            if (missing.Count == 0)
                return true;

            error = $"Block '{commandKey}' is missing required option(s): {string.Join(", ", missing)}.";
            return false;
        }

        private static bool IsDynamicRuntimeValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            return trimmed.Contains("${", StringComparison.Ordinal) && trimmed.Contains("}", StringComparison.Ordinal);
        }

        private static bool IsMissingRequiredOptionToken(string commandKey, string optionKey, JToken token)
        {
            if (token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return true;

            if (token.Type == JTokenType.Array)
                return !token.HasValues;

            if (token.Type != JTokenType.String)
                return false;

            var value = token.ToString();
            if (AllowsWhitespaceOnlyRequiredValue(commandKey, optionKey))
                return string.IsNullOrEmpty(value);

            return string.IsNullOrWhiteSpace(value);
        }

        private static JObject ReorderOptionsForSerialization(string commandKey, JObject options)
        {
            if (options.Count <= 1)
                return options;

            var preferredOrder = ResolvePreferredOptionOrder(commandKey);
            if (preferredOrder.Count == 0)
                return options;

            var ordered = new JObject();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in preferredOrder)
            {
                if (seen.Contains(key))
                    continue;

                if (!options.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var value))
                    continue;

                ordered[key] = value.DeepClone();
                seen.Add(key);
            }

            // Preserve any non-panel/unknown keys by appending them in their current order.
            foreach (var property in options.Properties())
            {
                if (seen.Contains(property.Name))
                    continue;

                ordered[property.Name] = property.Value.DeepClone();
                seen.Add(property.Name);
            }

            return ordered;
        }

        private static IReadOnlyList<string> ResolvePreferredOptionOrder(string commandKey)
        {
            if (PreferredOptionOrderOverridesByCommand.TryGetValue(commandKey, out var overrideOrder))
                return overrideOrder;

            if (!ScriptParser.GetDeclaredStepOptionKeysByCommand().TryGetValue(commandKey, out var parserOrder))
                return Array.Empty<string>();

            // Mirror Properties panel grouping and order: Core -> Advanced -> On Error.
            var core = new List<string>();
            var advanced = new List<string>();
            var onError = new List<string>();
            foreach (var optionKey in parserOrder)
            {
                if (string.Equals(optionKey, "on_error", StringComparison.OrdinalIgnoreCase))
                {
                    onError.Add(optionKey);
                }
                else if (AdvancedPanelOptionKeys.Contains(optionKey))
                {
                    advanced.Add(optionKey);
                }
                else
                {
                    core.Add(optionKey);
                }
            }

            var ordered = new List<string>(core.Count + advanced.Count + onError.Count);
            ordered.AddRange(core);
            ordered.AddRange(advanced);
            ordered.AddRange(onError);

            return ordered;
        }

        private static bool AllowsWhitespaceOnlyRequiredValue(string commandKey, string optionKey)
        {
            return string.Equals(commandKey, "print", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(optionKey, "message", StringComparison.OrdinalIgnoreCase);
        }

        private static JToken BuildCommandValueToken(string commandKey, JObject options)
        {
            if (string.Equals(commandKey, "break", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(commandKey, "continue", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(commandKey, "return", StringComparison.OrdinalIgnoreCase))
            {
                return JValue.CreateNull();
            }

            return options;
        }

        private static bool TrySerializeStepYaml(string commandKey, JToken commandValue, out string yaml, out string? error)
            => TrySerializeStepYaml(commandKey, commandValue, null, out yaml, out error);

        private static bool TrySerializeStepYaml(string commandKey, JToken commandValue, JObject? rootOptions, out string yaml, out string? error)
        {
            error = null;
            yaml = string.Empty;

            var hasRoot = rootOptions != null && rootOptions.Count > 0;
            var isNullCommand = commandValue.Type == JTokenType.Null || commandValue.Type == JTokenType.Undefined;

            try
            {
                if (isNullCommand && !hasRoot)
                {
                    yaml = $"- {commandKey}:";
                    return true;
                }

                var stepMap = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [commandKey] = isNullCommand ? null : ConvertJTokenToYamlValue(commandValue),
                };

                if (hasRoot)
                {
                    foreach (var rootProp in rootOptions!.Properties())
                        stepMap[rootProp.Name] = ConvertJTokenToYamlValue(rootProp.Value);
                }

                var serializer = new SerializerBuilder().Build();
                var yamlObject = new List<object?> { stepMap };
                yaml = serializer.Serialize(yamlObject).TrimEnd('\r', '\n');
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to serialize block '{commandKey}': {ex.Message}";
                return false;
            }
        }

        private static object? ConvertJTokenToYamlValue(JToken token)
        {
            return token.Type switch
            {
                JTokenType.Object => ((JObject)token).Properties()
                    .ToDictionary(
                        property => property.Name,
                        property => ConvertJTokenToYamlValue(property.Value),
                        StringComparer.Ordinal),
                JTokenType.Array => ((JArray)token)
                    .Select(ConvertJTokenToYamlValue)
                    .ToList(),
                JTokenType.Integer => token.Value<long>(),
                JTokenType.Float => token.Value<double>(),
                JTokenType.Boolean => token.Value<bool>(),
                JTokenType.Null => null,
                JTokenType.Undefined => null,
                _ => token.ToString(),
            };
        }

        private static bool TryParseTargetHostPort(string value, out string host, out int port)
        {
            host = string.Empty;
            port = 0;
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
                return false;

            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                var closeBracket = trimmed.IndexOf(']');
                if (closeBracket <= 1 || closeBracket + 2 > trimmed.Length || trimmed[closeBracket + 1] != ':')
                    return false;

                host = trimmed.Substring(1, closeBracket - 1).Trim();
                var portText = trimmed[(closeBracket + 2)..].Trim();
                return host.Length > 0 && int.TryParse(portText, out port);
            }

            var separatorIndex = trimmed.LastIndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
                return false;

            host = trimmed[..separatorIndex].Trim();
            var rawPort = trimmed[(separatorIndex + 1)..].Trim();
            return host.Length > 0 && int.TryParse(rawPort, out port);
        }

        private static bool IsContainerBlockType(string blockType)
        {
            return blockType == "if"
                || blockType == "foreach"
                || blockType == "while"
                || blockType == "repeat"
                || blockType == "switch"
                || blockType == "parallel"
                || blockType == "try";
        }

        private static string NormalizeTopLevelSnippetIndent(string snippet)
        {
            if (string.IsNullOrWhiteSpace(snippet))
                return snippet;

            var usesWindowsLineEndings = snippet.Contains("\r\n", StringComparison.Ordinal);
            var normalized = snippet
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);
            var lines = normalized.Split('\n');

            var dedent = 0;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                dedent = CountLeadingSpaces(line);
                break;
            }

            if (dedent <= 0)
                return snippet;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var removable = 0;
                while (removable < dedent && removable < line.Length && line[removable] == ' ')
                    removable++;

                lines[i] = line.Substring(removable);
            }

            var result = string.Join("\n", lines);
            return usesWindowsLineEndings
                ? result.Replace("\n", "\r\n", StringComparison.Ordinal)
                : result;
        }

        private static int CountLeadingSpaces(string line)
        {
            var count = 0;
            while (count < line.Length && line[count] == ' ')
                count++;

            return count;
        }

        private static string QuoteYaml(string value)
        {
            var escaped = value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            return $"\"{escaped}\"";
        }

        #endregion

        #region Helpers

        private static string? GetLogPreview(object? logValue)
        {
            return logValue switch
            {
                LogOptions options => options.Message,
                string message => message,
                _ => logValue?.ToString(),
            };
        }

        private static string? GetSetHistoryLabelPreview(object? setHistoryLabelValue)
        {
            return setHistoryLabelValue switch
            {
                SetHistoryLabelOptions options => options.Value,
                string value => value,
                _ => setHistoryLabelValue?.ToString(),
            };
        }

        private static (string blockType, string? preview) GetStepPreview(ScriptStep step, StepType stepType)
        {
            return stepType switch
            {
                StepType.Send => ("send", step.Send),
                StepType.Print => ("print", step.Print),
                StepType.Wait => ("wait", step.Wait?.ToString()),
                StepType.Set => ("set", step.Set),
                StepType.Exit => ("exit", step.Exit),
                StepType.Extract => ("extract", step.Extract?.Pattern),
                StepType.If => ("if", step.If),
                StepType.Foreach => ("foreach", step.Foreach),
                StepType.While => ("while", step.While),
                StepType.Repeat => ("repeat", step.Until),
                StepType.Switch => ("switch", step.Switch),
                StepType.Try => ("try", null),
                StepType.Break => ("break", null),
                StepType.Continue => ("continue", null),
                StepType.Call => ("call", step.Call?.Subroutine),
                StepType.Return => ("return", null),
                StepType.Parallel => ("parallel", null),
                StepType.Ping => ("ping", step.Ping?.Host),
                StepType.Dns => ("dns", step.Dns?.Host),
                StepType.Portcheck => ("portcheck", step.Portcheck?.Host),
                StepType.Http => ("http", step.Http?.Url),
                StepType.Webhook => ("webhook", step.Webhook?.Url),
                StepType.Readfile => ("readfile", step.Readfile?.Path),
                StepType.Writefile => ("writefile", step.Writefile?.Path),
                StepType.Exists => ("exists", step.Exists?.Path),
                StepType.PlaySound => ("playsound", step.PlaySound?.Path),
                StepType.Log => ("log", GetLogPreview(step.Log)),
                StepType.Input => ("input", step.Input?.Prompt),
                StepType.Choose => ("choose", step.Choose?.Prompt),
                StepType.Multiselect => ("multiselect", step.Multiselect?.Prompt),
                StepType.Confirm => ("confirm", step.Confirm?.Prompt),
                StepType.Interactive => ("interactive", null),
                StepType.Assert => ("assert", step.Assert?.Condition),
                StepType.Sftp => ("sftp", step.Sftp?.Action),
                StepType.Table => ("table", step.Table?.Data),
                StepType.Parse => ("parse", step.Parse?.Format),
                StepType.BrowserCallbackCapture => ("browser_callback", step.BrowserCallbackCapture?.StartUrl),
                StepType.UpdateColumn => ("updatecolumn", step.UpdateColumn?.Column),
                StepType.UpdateEnvironment => ("updateenvironment", step.UpdateEnvironment?.Variable),
                StepType.LocalCmd => ("localcmd", step.LocalCmd?.Command),
                StepType.Vault => ("vault", step.Vault?.Path),
                StepType.SetHistoryLabel => ("sethistorylabel", GetSetHistoryLabelPreview(step.SetHistoryLabel)),
                StepType.Notify => ("notify", step.Notify?.Title ?? step.Notify?.Message),
                _ => ("unknown", null),
            };
        }

        /// <summary>
        /// A step snippet together with the number of blank lines that preceded it.
        /// </summary>
        private readonly record struct StepSnippetInfo(string Snippet, int BlankLinesBefore);

        /// <summary>
        /// Splits YAML text into individual top-level step snippets.
        /// Each snippet is the complete YAML text for one step (including nested blocks).
        /// Also records the number of blank lines between steps so the exporter can
        /// reproduce the user's original spacing.
        /// </summary>
        private static List<StepSnippetInfo> SplitYamlSteps(string yamlText)
        {
            var steps = new List<StepSnippetInfo>();
            var lines = yamlText.Split('\n');

            // Find where "steps:" starts
            int stepsLineIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimEnd('\r');
                if (trimmed == "steps:" || trimmed == "steps: ")
                {
                    stepsLineIndex = i;
                    break;
                }
            }

            if (stepsLineIndex < 0) return steps;

            // Determine the indent level of step items (first "- " after "steps:")
            int stepIndent = -1;
            var currentStep = new StringBuilder();
            bool inStep = false;
            int blankLinesBefore = 0;   // blank lines accumulated before next step
            int currentBlankLines = 0;  // blank lines for the step being built

            for (int i = stepsLineIndex + 1; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (inStep)
                    {
                        // Blank line while inside a step — tentatively include it.
                        // We'll track separately in case it turns out to be inter-step spacing.
                        currentStep.AppendLine(line);
                    }
                    blankLinesBefore++;
                    continue;
                }

                var indent = line.Length - line.TrimStart().Length;
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("- ") || trimmed == "-")
                {
                    if (stepIndent < 0) stepIndent = indent;

                    if (indent == stepIndent)
                    {
                        // New top-level step — finalize the previous one
                        if (inStep && currentStep.Length > 0)
                        {
                            steps.Add(new StepSnippetInfo(
                                currentStep.ToString().TrimEnd('\r', '\n') + "\n",
                                currentBlankLines));
                        }
                        currentStep.Clear();
                        currentStep.AppendLine(line);
                        currentBlankLines = inStep ? blankLinesBefore : 0;
                        blankLinesBefore = 0;
                        inStep = true;
                        continue;
                    }
                }

                // Non-blank line that is part of the current step — reset blank counter.
                blankLinesBefore = 0;

                // Continuation of current step (deeper indent or non-list line)
                if (inStep && (indent > stepIndent || string.IsNullOrWhiteSpace(line)))
                {
                    currentStep.AppendLine(line);
                }
                else if (inStep && indent <= stepIndent && !trimmed.StartsWith("- "))
                {
                    // Back to step indent but not a new step — belongs to previous step
                    currentStep.AppendLine(line);
                }
            }

            // Last step
            if (inStep && currentStep.Length > 0)
            {
                steps.Add(new StepSnippetInfo(
                    currentStep.ToString().TrimEnd('\r', '\n') + "\n",
                    currentBlankLines));
            }

            return steps;
        }

        /// <summary>
        /// Extracts everything before "steps:" as the preamble.
        /// </summary>
        private static string ExtractPreamble(string yamlText)
        {
            var lines = yamlText.Split('\n');
            var sb = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimEnd('\r');
                sb.AppendLine(lines[i].TrimEnd('\r'));
                if (trimmed == "steps:" || trimmed == "steps: ")
                    break;
            }

            return sb.ToString();
        }

        private static bool HasTopLevelStepsHeader(string yamlText)
        {
            var lines = yamlText.Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                if (line.Length == 0)
                    continue;

                var isIndented = line.StartsWith(" ", StringComparison.Ordinal)
                    || line.StartsWith("\t", StringComparison.Ordinal);
                if (isIndented)
                    continue;

                var normalized = line.TrimEnd();
                if (string.Equals(normalized, "steps:", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Parses known preamble fields from the Script model into Start node props.
        /// Unknown preamble content is stored in _yamlSnippet for round-trip safety.
        /// </summary>
        private static void ParsePreambleIntoProps(string preamble, Script script, JObject props)
        {
            if (!string.IsNullOrEmpty(script.Name))
                props["name"] = script.Name;
            if (!string.IsNullOrEmpty(script.Description))
                props["description"] = script.Description;
            if (script.Version != 1)
                props["version"] = script.Version;
            if (!string.IsNullOrEmpty(script.Environment))
                props["environment"] = script.Environment;
            if (script.Debug)
                props["debug"] = true;
            if (script.NoBanner)
                props["nobanner"] = true;
            if (script.CompactErrors)
                props["compact_errors"] = true;
            if (script.SuppressMissingColumnWarning)
                props["suppress_missing_column_warning"] = true;
            if (script.Library)
                props["library"] = true;

            // Store vars as JObject for read-only display
            if (script.Vars.Count > 0)
            {
                var varsObj = new JObject();
                foreach (var kv in script.Vars)
                    varsObj[kv.Key] = kv.Value != null ? JToken.FromObject(kv.Value) : JValue.CreateNull();
                props["vars"] = varsObj;
            }

            // Store imports as JArray for read-only display
            if (script.Imports.Count > 0)
            {
                var importsArr = new JArray();
                foreach (var imp in script.Imports)
                {
                    importsArr.Add(new JObject
                    {
                        ["path"] = imp.Path,
                        ["as"] = imp.Alias,
                    });
                }
                props["imports"] = importsArr;
            }

            var varsSection = ExtractYamlSection(preamble, "vars:");
            if (!string.IsNullOrWhiteSpace(varsSection))
                props["vars_yaml"] = varsSection.TrimEnd('\r', '\n');

            var importsSection = ExtractYamlSection(preamble, "imports:");
            if (!string.IsNullOrWhiteSpace(importsSection))
                props["imports_yaml"] = importsSection.TrimEnd('\r', '\n');

            var subroutinesSection = ExtractYamlSection(preamble, "subroutines:");
            if (!string.IsNullOrWhiteSpace(subroutinesSection))
                props["subroutines_yaml"] = subroutinesSection.TrimEnd('\r', '\n');

            // Store full preamble as fallback for unrecognized keys
            props["_yamlSnippet"] = preamble;
        }

        /// <summary>
        /// Serializes Start node props back to YAML preamble text.
        /// </summary>
        private static string SerializeStartPropsToPreamble(JObject props)
        {
            var sb = new StringBuilder();

            var name = props["name"]?.ToString();
            var description = props["description"]?.ToString();
            var version = props["version"]?.Value<int?>() ?? 0;
            var environment = props["environment"]?.ToString();
            var debug = props["debug"]?.Value<bool>() == true;
            var nobanner = props["nobanner"]?.Value<bool>() == true;
            var compactErrors = props["compact_errors"]?.Value<bool>() == true;
            var suppressWarning = props["suppress_missing_column_warning"]?.Value<bool>() == true;
            var library = props["library"]?.Value<bool>() == true;
            var vars = props["vars"] as JObject;
            var imports = props["imports"] as JArray;
            var subroutines = props["subroutines"] as JObject;
            var varsYaml = props["vars_yaml"]?.ToString();
            var importsYaml = props["imports_yaml"]?.ToString();
            var subroutinesYaml = props["subroutines_yaml"]?.ToString();
            var snippet = props["_yamlSnippet"]?.ToString();

            if (!string.IsNullOrEmpty(name))
                sb.AppendLine($"name: {name}");
            if (!string.IsNullOrEmpty(description))
                sb.AppendLine($"description: {EscapeYamlString(description)}");
            if (version > 1)
                sb.AppendLine($"version: {version}");
            if (!string.IsNullOrEmpty(environment))
                sb.AppendLine($"environment: {environment}");
            if (debug)
                sb.AppendLine("debug: true");
            if (nobanner)
                sb.AppendLine("nobanner: true");
            if (compactErrors)
                sb.AppendLine("compact_errors: true");
            if (suppressWarning)
                sb.AppendLine("suppress_missing_column_warning: true");
            if (library)
                sb.AppendLine("library: true");

            if (AppendRawSection(sb, "vars:", varsYaml))
            {
            }
            else if (vars != null && vars.Count > 0)
            {
                AppendSerializedSection(sb, "vars", vars);
            }
            else if (snippet != null)
            {
                var varsSection = ExtractYamlSection(snippet, "vars:");
                if (!string.IsNullOrWhiteSpace(varsSection))
                {
                    sb.Append(varsSection);
                }
            }

            if (AppendRawSection(sb, "imports:", importsYaml))
            {
            }
            else if (imports != null && imports.Count > 0)
            {
                var structuredImports = NormalizeImportsForSerialization(imports);
                AppendSerializedSection(sb, "imports", structuredImports);
            }
            else if (snippet != null)
            {
                var importsSection = ExtractYamlSection(snippet, "imports:");
                if (!string.IsNullOrWhiteSpace(importsSection))
                {
                    sb.Append(importsSection);
                }
            }

            if (AppendRawSection(sb, "subroutines:", subroutinesYaml))
            {
                // explicit editor content wins
            }
            else if (subroutines != null && subroutines.Count > 0)
            {
                AppendSerializedSection(sb, "subroutines", subroutines);
            }
            else if (snippet != null)
            {
                var subroutinesSection = ExtractYamlSection(snippet, "subroutines:");
                if (!string.IsNullOrEmpty(subroutinesSection))
                    sb.Append(subroutinesSection);
            }

            // Append any unrecognized sections from the original snippet
            if (snippet != null)
            {
                var unrecognized = ExtractUnrecognizedSections(snippet);
                if (!string.IsNullOrEmpty(unrecognized))
                    sb.Append(unrecognized);
            }

            return sb.ToString();
        }

        private static bool AppendRawSection(StringBuilder sb, string header, string? rawSection)
        {
            if (string.IsNullOrWhiteSpace(rawSection))
                return false;

            var section = rawSection!.Trim('\r', '\n');
            if (!section.TrimStart().StartsWith(header, StringComparison.OrdinalIgnoreCase))
            {
                section = $"{header}\n{section}";
            }

            sb.AppendLine(section.TrimEnd('\r', '\n'));
            return true;
        }

        private static void AppendSerializedSection(StringBuilder sb, string sectionName, JToken sectionValue)
        {
            var serializer = new SerializerBuilder().Build();
            var yamlObject = new Dictionary<string, object?>
            {
                [sectionName] = ConvertJTokenToYamlValue(sectionValue),
            };

            var sectionYaml = serializer.Serialize(yamlObject).TrimEnd('\r', '\n');
            sb.AppendLine(sectionYaml);
        }

        private static JToken NormalizeImportsForSerialization(JArray imports)
        {
            var normalized = new JArray();
            foreach (var item in imports)
            {
                if (item is JObject map)
                {
                    var path = map["path"]?.ToString();
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    var alias = map["as"]?.ToString();
                    if (string.IsNullOrWhiteSpace(alias))
                        alias = DeriveImportAlias(path!);

                    normalized.Add(new JObject
                    {
                        ["path"] = path,
                        ["as"] = alias,
                    });
                    continue;
                }

                var legacyPath = item?.ToString();
                if (string.IsNullOrWhiteSpace(legacyPath))
                    continue;

                normalized.Add(new JObject
                {
                    ["path"] = legacyPath,
                    ["as"] = DeriveImportAlias(legacyPath),
                });
            }

            return normalized;
        }

        private static string DeriveImportAlias(string path)
        {
            var normalized = path.Replace('\\', '/');
            var fileName = normalized.Split('/').LastOrDefault() ?? "import";
            var dotIndex = fileName.IndexOf('.');
            var baseName = dotIndex > 0 ? fileName[..dotIndex] : fileName;
            return string.IsNullOrWhiteSpace(baseName) ? "import" : baseName;
        }

        private static string EscapeYamlString(string value)
        {
            if (value.Contains('\n') || value.Contains(':') || value.Contains('#') ||
                value.StartsWith(' ') || value.EndsWith(' '))
                return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
            return value;
        }

        private static string ExtractYamlSection(string yaml, string sectionKey)
        {
            var lines = yaml.Split('\n');
            var sb = new StringBuilder();
            bool inSection = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimEnd('\r');
                if (trimmed.TrimStart() == sectionKey || trimmed.TrimStart().StartsWith(sectionKey))
                {
                    inSection = true;
                    sb.AppendLine(trimmed);
                    continue;
                }
                if (inSection)
                {
                    if (trimmed.Length > 0 && (trimmed[0] == ' ' || trimmed[0] == '\t' || trimmed.TrimStart().StartsWith("-")))
                    {
                        sb.AppendLine(trimmed);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return sb.ToString();
        }

        private static string ExtractUnrecognizedSections(string snippet)
        {
            var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "name:", "description:", "version:", "environment:",
                "debug:", "nobanner:", "compact_errors:", "suppress_missing_column_warning:", "library:",
                "vars:", "imports:", "subroutines:", "steps:",
            };

            var lines = snippet.Split('\n');
            var sb = new StringBuilder();
            bool inUnrecognized = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var trimmed = line.TrimStart();
                var isIndented = line.StartsWith(" ", StringComparison.Ordinal)
                    || line.StartsWith("\t", StringComparison.Ordinal);
                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                {
                    if (inUnrecognized)
                        sb.AppendLine(line);
                    continue;
                }

                bool isKnown = false;
                foreach (var key in knownKeys)
                {
                    if (trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    {
                        isKnown = true;
                        break;
                    }
                }

                if (!isKnown && !isIndented && !trimmed.StartsWith("-"))
                {
                    inUnrecognized = true;
                    sb.AppendLine(line);
                }
                else if (inUnrecognized && (isIndented || trimmed.StartsWith("-")))
                {
                    sb.AppendLine(line);
                }
                else
                {
                    inUnrecognized = false;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the block type property name for display in the block preview.
        /// </summary>
        private static string? GetPreviewKey(string blockType)
        {
            return blockType switch
            {
                "send" => "command",
                "print" => "message",
                "extract" => "pattern",
                "if" => "condition",
                "foreach" => "iterator",
                "while" => "condition",
                "repeat" => "until",
                "set" => "expression",
                "wait" => "seconds",
                "vault" => "path",
                _ => null,
            };
        }

        #endregion

        #region Node-ID mapping

        /// <summary>
        /// Builds a mapping from canvas node IDs to scoped step paths for
        /// correlating execution/debug events with canvas blocks.
        /// </summary>
        public Dictionary<string, string> BuildNodeIdToStepPathMap(string yamlText)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(yamlText))
                return map;

            try
            {
                var (nodes, _) = TextToGraph(yamlText);
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    var id = node["id"]?.ToString();
                    if (id == null)
                        continue;

                    // Skip metadata nodes (preamble, etc.)
                    if (id.StartsWith("__"))
                        continue;

                    // Skip child nodes (they are visual-only, not top-level steps)
                    if (node["data"]?["props"]?["_isChildOf"] != null)
                    {
                        var childPath = node["data"]?["props"]?["_stepPath"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(childPath))
                            map[id] = childPath!;
                        continue;
                    }

                    var stepPath = node["data"]?["props"]?["_stepPath"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(stepPath))
                        map[id] = stepPath!;
                }
            }
            catch
            {
                // If parsing fails, return empty map
            }

            return map;
        }

        /// <summary>
        /// Compatibility mapping for legacy step-index consumers.
        /// </summary>
        public Dictionary<string, int> BuildNodeIdToStepIndexMap(string yamlText)
        {
            var stepPathMap = BuildNodeIdToStepPathMap(yamlText);
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in stepPathMap)
            {
                if (TryGetTopLevelStepIndex(pair.Value, out var idx))
                    result[pair.Key] = idx;
            }

            return result;
        }

        public static bool TryGetTopLevelStepIndex(string stepPath, out int stepIndex)
        {
            stepIndex = -1;
            if (string.IsNullOrWhiteSpace(stepPath))
                return false;

            var parts = stepPath.Split('/');
            if (parts.Length < 2 || !string.Equals(parts[0], "steps", StringComparison.Ordinal))
                return false;

            return int.TryParse(parts[1], out stepIndex);
        }

        #endregion

        #region Canvas Layout Persistence

        /// <summary>
        /// Computes a structure hash from the graph nodes' block types and step paths.
        /// The hash is insensitive to value changes (e.g., editing a command argument)
        /// but changes when blocks are added, removed, reordered, or change type.
        /// </summary>
        public static string ComputeStructureHash(JArray nodes)
        {
            var tuples = new List<string>();
            foreach (var node in nodes)
            {
                var id = node["id"]?.ToString();
                if (id == "__start__") continue;
                if (node["type"]?.ToString() == "comment") continue;

                var blockType = node["data"]?["blockType"]?.ToString() ?? "";
                var stepPath = node["data"]?["props"]?["_stepPath"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(stepPath))
                    tuples.Add($"{stepPath}:{blockType}");
            }

            tuples.Sort(StringComparer.Ordinal);
            var structure = string.Join("|", tuples);

            var bytes = System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(structure));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Computes a structure hash directly from YAML text without generating graph nodes.
        /// Used by the editor to detect structural changes cheaply.
        /// Returns null if the YAML is invalid or not a script.
        /// </summary>
        public static string? ComputeStructureHashFromYaml(string yamlText)
        {
            try
            {
                if (!Scripting.ScriptParser.IsYamlScript(yamlText))
                    return null;

                var parser = new Scripting.ScriptParser();
                var script = parser.Parse(yamlText);
                if (script.Steps.Count == 0)
                    return null;

                var tuples = new List<string>();
                CollectStructureTuples(script.Steps, "steps", tuples);

                tuples.Sort(StringComparer.Ordinal);
                var structure = string.Join("|", tuples);

                var bytes = System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes(structure));
                return Convert.ToHexString(bytes).ToLowerInvariant();
            }
            catch
            {
                return null;
            }
        }

        private static void CollectStructureTuples(List<Scripting.Models.ScriptStep> steps, string scopePath, List<string> tuples)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var stepType = step.GetStepType();
                var (blockType, _) = GetStepPreview(step, stepType);
                var stepPath = BuildStepPath(scopePath, i);
                tuples.Add($"{stepPath}:{blockType}");

                if (IsContainerStep(stepType))
                {
                    var branches = GetBranches(step, stepType);
                    foreach (var branch in branches)
                    {
                        var branchScope = BuildScopePath(stepPath, branch.ScopePath);
                        CollectStructureTuples(branch.Steps, branchScope, tuples);
                    }
                }
            }
        }

        /// <summary>
        /// Merges stored layout data into algorithmically-positioned graph nodes.
        /// Overrides positions, appends comment nodes, and marks disabled blocks.
        /// </summary>
        public static void MergeLayout(JArray nodes, CanvasLayoutData layout)
        {
            // Override positions for existing nodes
            foreach (var node in nodes)
            {
                var id = node["id"]?.ToString();
                if (id != null && layout.Positions.TryGetValue(id, out var pos))
                {
                    node["position"] = new JObject { ["x"] = pos.X, ["y"] = pos.Y };
                }

                // Mark disabled blocks
                if (id != null && layout.DisabledBlockIds.Contains(id))
                {
                    var data = node["data"] as JObject;
                    if (data != null)
                        data["disabled"] = true;
                }

                // Mark expanded blocks (presentation only — never read by YAML export)
                if (id != null && layout.ExpandedNodeIds.Contains(id))
                {
                    var dataExp = node["data"] as JObject;
                    if (dataExp != null) dataExp["expanded"] = true;
                }
            }

            // Append comment nodes
            foreach (var comment in layout.Comments)
            {
                var commentNode = new JObject
                {
                    ["id"] = comment.Id,
                    ["type"] = "comment",
                    ["position"] = new JObject { ["x"] = comment.X, ["y"] = comment.Y },
                    ["style"] = new JObject { ["width"] = comment.Width, ["height"] = comment.Height },
                    ["data"] = new JObject
                    {
                        ["commentId"] = comment.Id,
                        ["text"] = comment.Text,
                        ["color"] = comment.Color,
                    },
                };
                if (comment.AttachedToNodeId != null)
                    ((JObject)commentNode["data"]!)["attachedToNodeId"] = comment.AttachedToNodeId;

                nodes.Add(commentNode);
            }
        }

        /// <summary>
        /// Extracts layout data (positions, comments, disabled blocks) from a graph payload.
        /// Used when capturing layout on "Apply YAML".
        /// </summary>
        public static CanvasLayoutData ExtractLayout(JArray nodes, JArray? commentNodes, IEnumerable<string>? disabledBlockIds, IEnumerable<string>? expandedNodeIds = null)
        {
            var layout = new CanvasLayoutData
            {
                StructureHash = ComputeStructureHash(nodes),
            };

            // Extract positions from executable nodes
            foreach (var node in nodes)
            {
                var id = node["id"]?.ToString();
                if (id == null) continue;
                if (node["type"]?.ToString() == "comment") continue;

                var pos = node["position"];
                if (pos != null)
                {
                    layout.Positions[id] = new NodePosition
                    {
                        X = pos["x"]?.Value<double>() ?? 0,
                        Y = pos["y"]?.Value<double>() ?? 0,
                    };
                }
            }

            // Extract comments
            if (commentNodes != null)
            {
                foreach (var c in commentNodes)
                {
                    var comment = new CanvasComment
                    {
                        Id = c["id"]?.ToString() ?? "",
                        Text = c["text"]?.ToString() ?? "",
                        Color = c["color"]?.ToString() ?? "#e0c040",
                        X = c["x"]?.Value<double>() ?? 0,
                        Y = c["y"]?.Value<double>() ?? 0,
                        Width = c["width"]?.Value<double>() ?? 200,
                        Height = c["height"]?.Value<double>() ?? 100,
                        AttachedToNodeId = c["attachedToNodeId"]?.ToString(),
                    };
                    layout.Comments.Add(comment);
                }
            }

            // Disabled blocks
            if (disabledBlockIds != null)
            {
                layout.DisabledBlockIds.AddRange(disabledBlockIds);
            }

            // Expanded nodes (presentation only)
            if (expandedNodeIds != null)
            {
                layout.ExpandedNodeIds.AddRange(expandedNodeIds);
            }

            return layout;
        }

        #endregion
    }
}

