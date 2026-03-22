using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;

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
        private const double NodeSpacingY = 120;
        private const double NodeStartX = 250;
        private const double NodeStartY = 40;
        private const double ChildIndentX = 60;
        private const double ChildMinX = 40;
        private const int MaxNestingDepth = 5;

        // Multi-branch horizontal layout constants
        // MinColumnWidth must be >= max child node width + gap to prevent overlap
        private const double ChildNodeMaxWidth = 260;
        private const double ColumnGap = 30;
        private const double BaseColumnWidth = ChildNodeMaxWidth + ColumnGap;  // 290
        private const double ColumnWidthDecay = 0.92;
        private const double MinColumnWidth = ChildNodeMaxWidth + ColumnGap;   // 290 — never narrower than a node
        private const double MaxSpreadWidth = 1400;

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

            var currentY = NodeStartY;

            // Tracks nodes that need to connect to the next step.
            // Each entry is (nodeId, sourceHandle, color, label) — sourceHandle is
            // non-null for the false-path skip edge from an if without else.
            var pendingConnections = new List<PendingEdge>();

            for (int i = 0; i < script.Steps.Count && i < stepSnippets.Count; i++)
            {
                var step = script.Steps[i];
                var snippet = stepSnippets[i];
                var stepType = step.GetStepType();
                var nodeId = NextId();

                // Get a display label from the step
                var (blockType, previewText) = GetStepPreview(step, stepType);

                // Build props: snippet for round-trip + individual fields for the Properties panel
                var stepProps = new JObject { ["_yamlSnippet"] = snippet };
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
                        edge["style"]!["strokeDasharray"] = "5,5";
                    }
                    edges.Add(edge);
                }
                pendingConnections.Clear();

                currentY += NodeSpacingY;

                // Expand container children into visible indented nodes
                if (IsContainerStep(stepType))
                {
                    var branchEnds = ExpandContainerChildren(step, stepType, nodeId, ref currentY, 1, NodeStartX, nodes, edges);
                    if (branchEnds.Count > 0)
                    {
                        foreach (var be in branchEnds)
                            pendingConnections.Add(new PendingEdge(be));
                    }
                    else
                    {
                        // No children expanded — this node connects to the next step
                        pendingConnections.Add(new PendingEdge(nodeId));
                    }

                    // For IF without else: add a skip edge from the false handle
                    // so the user sees both "then" and "skip" paths converging
                    if (stepType == StepType.If
                        && (step.Else == null || step.Else.Count == 0)
                        && (step.Elif == null || step.Elif.Count == 0))
                    {
                        pendingConnections.Add(new PendingEdge(nodeId, "false", ColorElse, "else"));
                    }
                }
                else
                {
                    pendingConnections.Add(new PendingEdge(nodeId));
                }
            }

            // Store preamble in a special metadata node (hidden, used for export)
            if (!string.IsNullOrWhiteSpace(preamble))
            {
                var metaNode = new JObject
                {
                    ["id"] = "__preamble__",
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = -9999, ["y"] = -9999 },
                    ["hidden"] = true,
                    ["data"] = new JObject
                    {
                        ["blockType"] = "_preamble",
                        ["props"] = new JObject { ["_yamlSnippet"] = preamble },
                    },
                };
                nodes.Add(metaNode);
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
                return ExpandMultiBranch(nonEmptyBranches, parentNodeId, ref currentY, depth, centerX, nodes, edges);
            else
                return ExpandSingleBranch(nonEmptyBranches[0], parentNodeId, ref currentY, depth, centerX, nodes, edges);
        }

        /// <summary>
        /// Single-branch layout: children go LEFT of the center (existing behavior).
        /// Used for foreach, while, if-without-else.
        /// </summary>
        private List<string> ExpandSingleBranch(
            BranchInfo branch,
            string parentNodeId,
            ref double currentY,
            int depth,
            double centerX,
            JArray nodes,
            JArray edges)
        {
            // Children stay at the branch's own centerX — no further indentation.
            // The indent was for the main-flow case; inside a branch column the
            // children should just flow straight down within their allocated space.
            var lastNodeId = PlaceBranchSteps(branch, parentNodeId, ref currentY, depth, centerX, centerX, nodes, edges);
            return new List<string> { lastNodeId };
        }

        /// <summary>
        /// Multi-branch layout: branches spread horizontally side-by-side.
        /// All branches start at the same Y. The next sibling starts after the tallest branch.
        /// </summary>
        private List<string> ExpandMultiBranch(
            List<BranchInfo> branches,
            string parentNodeId,
            ref double currentY,
            int depth,
            double centerX,
            JArray nodes,
            JArray edges)
        {
            // Pass 1: Measure each branch width
            var branchSizes = new List<SubtreeSize>();
            foreach (var branch in branches)
                branchSizes.Add(MeasureSteps(branch.Steps));

            int totalColumns = branchSizes.Sum(s => s.Columns);
            double colWidth = GetColumnWidth(depth);
            double totalPixelWidth = totalColumns * colWidth;

            // Cap total spread
            if (totalPixelWidth > MaxSpreadWidth)
            {
                colWidth = MaxSpreadWidth / totalColumns;
                totalPixelWidth = MaxSpreadWidth;
            }

            // Calculate X positions for each branch, centered around centerX
            double leftEdge = centerX - totalPixelWidth / 2.0;
            var branchStartY = currentY;
            var maxBranchEndY = currentY;
            var branchEndNodes = new List<string>();

            for (int i = 0; i < branches.Count; i++)
            {
                var branch = branches[i];
                var branchSize = branchSizes[i];

                // Each branch gets its proportional horizontal share
                double branchPixelWidth = branchSize.Columns * colWidth;
                double branchCenterX = leftEdge + branchPixelWidth / 2.0;

                // Each branch starts at the same Y (independent tracking)
                var branchY = branchStartY;
                var lastNodeId = PlaceBranchSteps(branch, parentNodeId, ref branchY, depth, branchCenterX, branchCenterX, nodes, edges);

                branchEndNodes.Add(lastNodeId);
                maxBranchEndY = Math.Max(maxBranchEndY, branchY);
                leftEdge += branchPixelWidth;
            }

            // Advance past the tallest branch
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
            ref double currentY,
            int depth,
            double childX,
            double centerX,
            JArray nodes,
            JArray edges)
        {
            string prevNodeId = parentNodeId;
            string? sourceHandle = branch.SourceHandle;
            bool isFirstInBranch = true;

            foreach (var childStep in branch.Steps)
            {
                var childStepType = childStep.GetStepType();
                var childNodeId = NextId();
                var (childBlockType, childPreview) = GetStepPreview(childStep, childStepType);

                // Build child node props (visual-only, no _yamlSnippet)
                var childProps = new JObject
                {
                    ["_isChildOf"] = parentNodeId,
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
                    ["id"] = $"e-{prevNodeId}-{childNodeId}",
                    ["source"] = prevNodeId,
                    ["target"] = childNodeId,
                    ["type"] = "smoothstep",
                    ["style"] = new JObject { ["stroke"] = branch.Color },
                };

                if (sourceHandle != null)
                    edge["sourceHandle"] = sourceHandle;

                if (isFirstInBranch)
                {
                    // First edge in the branch gets a label and dashed style
                    edge["label"] = branch.Label;
                    edge["labelStyle"] = new JObject
                    {
                        ["fill"] = branch.Color,
                        ["fontSize"] = 11,
                        ["fontWeight"] = 600,
                    };
                    edge["style"]!["strokeDasharray"] = "5,5";
                }

                edges.Add(edge);
                prevNodeId = childNodeId;
                sourceHandle = null;
                isFirstInBranch = false;
                currentY += NodeSpacingY;

                // Recursively expand if this child is also a container
                if (IsContainerStep(childStepType) && depth < MaxNestingDepth)
                {
                    var nestedBranchEnds = ExpandContainerChildren(
                        childStep, childStepType, childNodeId, ref currentY, depth + 1, centerX, nodes, edges);

                    if (nestedBranchEnds.Count > 0)
                    {
                        prevNodeId = nestedBranchEnds[nestedBranchEnds.Count - 1];
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
                        branches.Add(new BranchInfo("then", ColorThen, null, step.Then));
                    if (step.Elif != null)
                    {
                        foreach (var elif in step.Elif)
                        {
                            var label = elif.If.Length > 20
                                ? "elif: " + elif.If.Substring(0, 17) + "..."
                                : "elif: " + elif.If;
                            branches.Add(new BranchInfo(label, ColorElif, null, elif.Then));
                        }
                    }
                    if (step.Else != null && step.Else.Count > 0)
                        branches.Add(new BranchInfo("else", ColorElse, "false", step.Else));
                    break;

                case StepType.Foreach:
                    if (step.Do != null && step.Do.Count > 0)
                        branches.Add(new BranchInfo("loop", ColorLoop, null, step.Do));
                    break;

                case StepType.While:
                    if (step.Do != null && step.Do.Count > 0)
                        branches.Add(new BranchInfo("loop", ColorLoop, null, step.Do));
                    break;

                case StepType.Try:
                    if (step.Try != null && step.Try.Count > 0)
                        branches.Add(new BranchInfo("try", ColorTry, null, step.Try));
                    if (step.Catch != null && step.Catch.Count > 0)
                        branches.Add(new BranchInfo("catch", ColorCatch, null, step.Catch));
                    if (step.Finally != null && step.Finally.Count > 0)
                        branches.Add(new BranchInfo("finally", ColorFinally, null, step.Finally));
                    break;

                case StepType.Switch:
                    if (step.Cases != null)
                    {
                        foreach (var c in step.Cases)
                        {
                            var label = c.Value.Length > 20
                                ? "case: " + c.Value.Substring(0, 17) + "..."
                                : "case: " + c.Value;
                            branches.Add(new BranchInfo(label, ColorCase, null, c.Do));
                        }
                    }
                    break;

                case StepType.Parallel:
                    if (step.Parallel?.Steps != null)
                    {
                        for (int i = 0; i < step.Parallel.Steps.Count; i++)
                        {
                            branches.Add(new BranchInfo(
                                $"branch {i + 1}", ColorBranch, null,
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
            public string Color { get; }
            public string? SourceHandle { get; }
            public List<ScriptStep> Steps { get; }

            public BranchInfo(string label, string color, string? sourceHandle, List<ScriptStep> steps)
            {
                Label = label;
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

            public PendingEdge(string nodeId, string? sourceHandle = null, string? color = null, string? label = null)
            {
                NodeId = nodeId;
                SourceHandle = sourceHandle;
                Color = color;
                Label = label;
            }
        }

        #region Subtree Measurement (Pass 1)

        /// <summary>
        /// Measures the dimensions of a list of steps for layout purposes.
        /// Width = number of columns needed, Height = number of rows needed.
        /// </summary>
        private sealed class SubtreeSize
        {
            public int Columns { get; set; } = 1;
            public int Rows { get; set; }
        }

        /// <summary>
        /// Measures the subtree size for a list of steps.
        /// </summary>
        private SubtreeSize MeasureSteps(List<ScriptStep> steps)
        {
            var size = new SubtreeSize();

            foreach (var step in steps)
            {
                var stepType = step.GetStepType();
                size.Rows += 1; // the step itself

                if (IsContainerStep(stepType))
                {
                    var branches = GetBranches(step, stepType);
                    if (IsMultiBranch(branches))
                    {
                        int totalBranchCols = 0;
                        int maxBranchRows = 0;
                        foreach (var branch in branches)
                        {
                            if (branch.Steps == null || branch.Steps.Count == 0) continue;
                            var branchSize = MeasureSteps(branch.Steps);
                            totalBranchCols += branchSize.Columns;
                            maxBranchRows = Math.Max(maxBranchRows, branchSize.Rows);
                        }
                        size.Columns = Math.Max(size.Columns, Math.Max(2, totalBranchCols));
                        size.Rows += maxBranchRows;
                    }
                    else
                    {
                        // Single branch — takes 1 column, height adds to parent
                        foreach (var branch in branches)
                        {
                            if (branch.Steps == null || branch.Steps.Count == 0) continue;
                            var branchSize = MeasureSteps(branch.Steps);
                            size.Columns = Math.Max(size.Columns, branchSize.Columns);
                            size.Rows += branchSize.Rows;
                        }
                    }
                }
            }

            return size;
        }

        /// <summary>
        /// Returns true when a container has 2+ non-empty branches (needs side-by-side layout).
        /// </summary>
        private static bool IsMultiBranch(List<BranchInfo> branches)
        {
            return branches.Count(b => b.Steps != null && b.Steps.Count > 0) >= 2;
        }

        /// <summary>
        /// Calculates the column width in pixels for a given nesting depth.
        /// Deeper nesting = narrower columns to prevent extreme horizontal spread.
        /// </summary>
        private static double GetColumnWidth(int depth)
        {
            return Math.Max(MinColumnWidth, BaseColumnWidth * Math.Pow(ColumnWidthDecay, depth));
        }

        #endregion

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
        /// Converts graph JSON back to YAML by reassembling stored snippets.
        /// Nodes that have _yamlSnippet are emitted verbatim.
        /// Nodes created visually (no snippet) are generated from properties.
        /// </summary>
        public string ToYaml(JObject graphData)
        {
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
            var outgoing = new Dictionary<string, List<string>>();
            var hasIncoming = new HashSet<string>();
            foreach (var edge in edges)
            {
                var src = edge["source"]?.ToString();
                var tgt = edge["target"]?.ToString();
                if (src == null || tgt == null) continue;
                if (!outgoing.ContainsKey(src)) outgoing[src] = new List<string>();
                outgoing[src].Add(tgt);
                hasIncoming.Add(tgt);
            }

            // Root nodes = no incoming edges, not hidden
            var roots = nodes
                .Where(n => n["hidden"]?.Value<bool>() != true)
                .Select(n => n["id"]?.ToString())
                .Where(id => id != null && !hasIncoming.Contains(id))
                .ToList();

            // Build ordered chain following edges
            var orderedIds = new List<string>();
            var visited = new HashSet<string>();
            foreach (var rootId in roots!)
            {
                BuildChain(rootId!, outgoing, orderedIds, visited);
            }

            // Check for preamble
            var sb = new StringBuilder();
            if (nodeMap.TryGetValue("__preamble__", out var preambleNode))
            {
                var preambleSnippet = preambleNode["data"]?["props"]?["_yamlSnippet"]?.ToString();
                if (!string.IsNullOrWhiteSpace(preambleSnippet))
                {
                    sb.Append(preambleSnippet);
                    if (!preambleSnippet.EndsWith("\n"))
                        sb.AppendLine();
                }
            }

            // Check if we need to add "steps:" header
            var preambleText = sb.ToString();
            if (!preambleText.Contains("steps:"))
                sb.AppendLine("steps:");

            // Emit each node's YAML
            foreach (var nodeId in orderedIds)
            {
                if (!nodeMap.TryGetValue(nodeId, out var node)) continue;
                if (node["hidden"]?.Value<bool>() == true) continue;

                var data = node["data"];
                var props = data?["props"] as JObject;

                // Skip visual-only child nodes — their YAML is inside the parent's snippet
                if (props?["_isChildOf"] != null) continue;

                var yamlSnippet = props?["_yamlSnippet"]?.ToString();

                if (!string.IsNullOrWhiteSpace(yamlSnippet))
                {
                    // Emit the original YAML verbatim
                    sb.Append(yamlSnippet);
                    if (!yamlSnippet.EndsWith("\n"))
                        sb.AppendLine();
                }
                else
                {
                    // Visually created node — generate minimal YAML
                    var blockType = data?["blockType"]?.ToString() ?? "print";
                    sb.AppendLine(GenerateStepYaml(blockType, props));
                }
            }

            return sb.ToString().TrimEnd() + "\n";
        }

        private void BuildChain(string nodeId, Dictionary<string, List<string>> outgoing, List<string> ordered, HashSet<string> visited)
        {
            if (!visited.Add(nodeId)) return;
            ordered.Add(nodeId);
            if (outgoing.TryGetValue(nodeId, out var targets))
            {
                foreach (var t in targets)
                    BuildChain(t, outgoing, ordered, visited);
            }
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
            // Common properties
            if (step.Timeout.HasValue) props["timeout"] = step.Timeout.Value;
            if (step.OnError != null) props["on_error"] = step.OnError;
            if (step.Capture != null) props["capture"] = step.Capture;
            if (step.Suppress) props["suppress"] = true;

            switch (stepType)
            {
                case StepType.Send:
                    if (step.Send != null) props["command"] = step.Send;
                    if (step.Expect != null) props["expect"] = step.Expect;
                    break;

                case StepType.Print:
                    if (step.Print != null) props["message"] = step.Print;
                    break;

                case StepType.Wait:
                    if (step.Wait.HasValue) props["duration"] = step.Wait.Value;
                    break;

                case StepType.Set:
                    if (step.Set != null) props["expression"] = step.Set;
                    break;

                case StepType.Exit:
                    if (step.Exit != null) props["status"] = step.Exit;
                    break;

                case StepType.Extract:
                    if (step.Extract != null)
                    {
                        props["pattern"] = step.Extract.Pattern;
                        if (step.Extract.Into != null) props["into"] = JToken.FromObject(step.Extract.Into);
                        if (!string.IsNullOrEmpty(step.Extract.From)) props["source"] = step.Extract.From;
                        props["match"] = step.Extract.Match;
                    }
                    break;

                case StepType.If:
                    if (step.If != null) props["condition"] = step.If;
                    break;

                case StepType.Foreach:
                    if (step.Foreach != null) props["expression"] = step.Foreach;
                    break;

                case StepType.While:
                    if (step.While != null) props["condition"] = step.While;
                    break;

                case StepType.Switch:
                    if (step.Switch != null) props["expression"] = step.Switch;
                    break;

                case StepType.Call:
                    if (step.Call != null) props["subroutine"] = step.Call.Subroutine;
                    break;

                case StepType.Assert:
                    if (step.Assert != null)
                    {
                        props["condition"] = step.Assert.Condition;
                        if (step.Assert.Message != null) props["message"] = step.Assert.Message;
                    }
                    break;

                case StepType.Parse:
                    if (step.Parse != null)
                    {
                        props["format"] = step.Parse.Format;
                        props["from"] = step.Parse.From;
                        props["into"] = step.Parse.Into;
                    }
                    break;

                case StepType.Readfile:
                    if (step.Readfile != null) props["path"] = step.Readfile.Path;
                    break;

                case StepType.Writefile:
                    if (step.Writefile != null)
                    {
                        props["path"] = step.Writefile.Path;
                        props["content"] = step.Writefile.Content;
                        props["mode"] = step.Writefile.Mode;
                        if (step.Writefile.Format != null) props["format"] = step.Writefile.Format;
                        if (step.Writefile.Headers != null) props["headers"] = JToken.FromObject(step.Writefile.Headers);
                    }
                    break;

                case StepType.Input:
                    if (step.Input != null)
                    {
                        props["prompt"] = step.Input.Prompt;
                        props["into"] = step.Input.Into;
                    }
                    break;

                case StepType.Choose:
                    if (step.Choose != null) props["prompt"] = step.Choose.Prompt;
                    break;

                case StepType.Multiselect:
                    if (step.Multiselect != null) props["prompt"] = step.Multiselect.Prompt;
                    break;

                case StepType.Confirm:
                    if (step.Confirm != null) props["prompt"] = step.Confirm.Prompt;
                    break;

                case StepType.Ping:
                    if (step.Ping != null) props["target"] = step.Ping.Host;
                    break;

                case StepType.Dns:
                    if (step.Dns != null) props["hostname"] = step.Dns.Host;
                    break;

                case StepType.Portcheck:
                    if (step.Portcheck != null) props["target"] = $"{step.Portcheck.Host}:{step.Portcheck.Port}";
                    break;

                case StepType.Http:
                    if (step.Http != null)
                    {
                        props["url"] = step.Http.Url;
                        if (step.Http.Method != null) props["method"] = step.Http.Method;
                    }
                    break;

                case StepType.Webhook:
                    if (step.Webhook != null) props["url"] = step.Webhook.Url;
                    break;

                case StepType.Sftp:
                    if (step.Sftp != null)
                    {
                        props["action"] = step.Sftp.Action;
                        props["local"] = step.Sftp.LocalPath;
                        props["remote"] = step.Sftp.RemotePath;
                    }
                    break;

                case StepType.UpdateColumn:
                    if (step.UpdateColumn != null)
                    {
                        props["column"] = step.UpdateColumn.Column;
                        props["expression"] = step.UpdateColumn.Value;
                    }
                    break;

                case StepType.UpdateEnvironment:
                    if (step.UpdateEnvironment != null)
                    {
                        props["variable"] = step.UpdateEnvironment.Variable;
                        props["expression"] = step.UpdateEnvironment.Value;
                    }
                    break;

                case StepType.BrowserCallbackCapture:
                    if (step.BrowserCallbackCapture != null) props["url"] = step.BrowserCallbackCapture.StartUrl;
                    break;

                case StepType.Log:
                    if (step.Log != null) props["message"] = step.Log.ToString();
                    break;
            }
        }

        private static string GenerateStepYaml(string blockType, JObject? props)
        {
            // Generate YAML for visually-created blocks (no original snippet)
            var preview = props?["_preview"]?.ToString();
            switch (blockType)
            {
                case "send":
                    var cmd = props?["command"]?.ToString() ?? preview ?? "echo hello";
                    return $"- send: {cmd}";
                case "print":
                    return $"- print: \"{props?["message"]?.ToString() ?? preview ?? ""}\"";
                case "wait":
                    return $"- wait: {props?["duration"] ?? 1000}";
                case "set":
                    return $"- set: {props?["expression"]?.ToString() ?? preview ?? "x = 1"}";
                case "extract":
                    return $"- extract:\n    pattern: \"{props?["pattern"] ?? ""}\"\n    into: {props?["into"] ?? "result"}";
                case "if":
                    return $"- if: {props?["condition"]?.ToString() ?? "true"}\n  then:\n    - print: \"(condition met)\"";
                case "foreach":
                    return $"- foreach: {props?["variable"] ?? "item"} in {props?["expression"] ?? "[]"}\n  do:\n    - print: \"${{item}}\"";
                case "while":
                    return $"- while: {props?["condition"] ?? "false"}\n  do:\n    - print: \"loop\"";
                case "exit":
                    return $"- exit: {props?["status"] ?? "success"}";
                case "break":
                    return "- break:";
                case "continue":
                    return "- continue:";
                case "ping":
                    return $"- ping: {props?["target"] ?? "127.0.0.1"}";
                case "dns":
                    return $"- dns: {props?["hostname"] ?? "example.com"}";
                case "log":
                    return $"- log: {props?["message"]?.ToString() ?? ""}";
                default:
                    return $"- {blockType}: # added from Flow Canvas";
            }
        }

        #endregion

        #region Helpers

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
                StepType.Log => ("log", step.Log?.ToString()),
                StepType.Input => ("input", step.Input?.Prompt),
                StepType.Choose => ("choose", step.Choose?.Prompt),
                StepType.Multiselect => ("multiselect", step.Multiselect?.Prompt),
                StepType.Confirm => ("confirm", step.Confirm?.Prompt),
                StepType.Interactive => ("interactive", null),
                StepType.Assert => ("assert", step.Assert?.Condition),
                StepType.Sftp => ("sftp", step.Sftp?.Action),
                StepType.Table => ("table", null),
                StepType.Parse => ("parse", step.Parse?.Format),
                StepType.BrowserCallbackCapture => ("browser_callback", step.BrowserCallbackCapture?.StartUrl),
                StepType.UpdateColumn => ("updatecolumn", step.UpdateColumn?.Column),
                StepType.UpdateEnvironment => ("updateenvironment", step.UpdateEnvironment?.Variable),
                _ => ("unknown", null),
            };
        }

        /// <summary>
        /// Splits YAML text into individual top-level step snippets.
        /// Each snippet is the complete YAML text for one step (including nested blocks).
        /// </summary>
        private static List<string> SplitYamlSteps(string yamlText)
        {
            var steps = new List<string>();
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

            for (int i = stepsLineIndex + 1; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (inStep) currentStep.AppendLine(line);
                    continue;
                }

                var indent = line.Length - line.TrimStart().Length;
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("- ") || trimmed == "-")
                {
                    if (stepIndent < 0) stepIndent = indent;

                    if (indent == stepIndent)
                    {
                        // New top-level step
                        if (inStep && currentStep.Length > 0)
                        {
                            steps.Add(currentStep.ToString().TrimEnd('\r', '\n') + "\n");
                        }
                        currentStep.Clear();
                        currentStep.AppendLine(line);
                        inStep = true;
                        continue;
                    }
                }

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
                steps.Add(currentStep.ToString().TrimEnd('\r', '\n') + "\n");
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
                "foreach" => "expression",
                "while" => "condition",
                "set" => "expression",
                "wait" => "duration",
                _ => null,
            };
        }

        #endregion
    }
}
