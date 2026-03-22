using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Converts between YAML Script models and Flow Canvas graph JSON.
    /// Handles YAML → Graph (for import) and Graph → YAML (for export).
    /// </summary>
    internal sealed class FlowCanvasBridge
    {
        private const double NodeSpacingY = 120;
        private const double NodeStartX = 250;
        private const double NodeStartY = 40;

        #region YAML → Graph

        /// <summary>
        /// Converts a parsed Script model into graph JSON (nodes + edges) for React Flow.
        /// </summary>
        public (JArray nodes, JArray edges) ToGraph(Script script)
        {
            var nodes = new JArray();
            var edges = new JArray();
            var idCounter = 0;

            string NextId() => $"node-{idCounter++}";

            ConvertSteps(script.Steps, nodes, edges, NextId, null, NodeStartX, NodeStartY);

            return (nodes, edges);
        }

        private string? ConvertSteps(
            List<ScriptStep> steps,
            JArray nodes,
            JArray edges,
            Func<string> nextId,
            string? previousNodeId,
            double startX,
            double startY,
            string? sourceHandle = null)
        {
            var currentY = startY;
            var lastNodeId = previousNodeId;

            foreach (var step in steps)
            {
                var nodeId = nextId();
                var stepType = step.GetStepType();
                var (blockType, props, label) = ExtractStepData(step, stepType);

                var node = new JObject
                {
                    ["id"] = nodeId,
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = startX, ["y"] = currentY },
                    ["data"] = new JObject
                    {
                        ["blockType"] = blockType,
                        ["label"] = label,
                        ["props"] = props,
                    },
                };
                nodes.Add(node);

                // Connect from previous node
                if (lastNodeId != null)
                {
                    var edge = new JObject
                    {
                        ["id"] = $"e-{lastNodeId}-{nodeId}",
                        ["source"] = lastNodeId,
                        ["target"] = nodeId,
                        ["style"] = new JObject { ["stroke"] = "#555" },
                    };
                    if (sourceHandle != null)
                    {
                        edge["sourceHandle"] = sourceHandle;
                        sourceHandle = null; // Only use for the first connection
                    }
                    edges.Add(edge);
                }

                // Handle container blocks with child steps
                if (stepType == StepType.If)
                {
                    // Then branch
                    if (step.Then != null && step.Then.Count > 0)
                    {
                        ConvertSteps(step.Then, nodes, edges, nextId, nodeId, startX - 120, currentY + NodeSpacingY);
                    }
                    // Else branch
                    if (step.Else != null && step.Else.Count > 0)
                    {
                        ConvertSteps(step.Else, nodes, edges, nextId, nodeId, startX + 120, currentY + NodeSpacingY, "false");
                    }
                }
                else if (stepType == StepType.Foreach || stepType == StepType.While)
                {
                    var bodySteps = stepType == StepType.Foreach ? step.Do : step.Do;
                    if (bodySteps != null && bodySteps.Count > 0)
                    {
                        ConvertSteps(bodySteps, nodes, edges, nextId, nodeId, startX + 50, currentY + NodeSpacingY);
                    }
                }
                else if (stepType == StepType.Try)
                {
                    if (step.Try != null && step.Try.Count > 0)
                    {
                        ConvertSteps(step.Try, nodes, edges, nextId, nodeId, startX - 80, currentY + NodeSpacingY);
                    }
                    if (step.Catch != null && step.Catch.Count > 0)
                    {
                        ConvertSteps(step.Catch, nodes, edges, nextId, nodeId, startX + 160, currentY + NodeSpacingY);
                    }
                }

                lastNodeId = nodeId;
                currentY += NodeSpacingY;
            }

            return lastNodeId;
        }

        private static (string blockType, JObject props, string? label) ExtractStepData(ScriptStep step, StepType stepType)
        {
            var props = new JObject();
            string? label = null;

            switch (stepType)
            {
                case StepType.Send:
                    props["command"] = step.Send;
                    if (step.Timeout.HasValue) props["timeout"] = step.Timeout.Value;
                    if (step.Expect != null) props["expect"] = step.Expect;
                    if (step.OnError != null) props["on_error"] = step.OnError;
                    return ("send", props, label);

                case StepType.Print:
                    props["message"] = step.Print;
                    return ("print", props, label);

                case StepType.Wait:
                    props["duration"] = step.Wait ?? 1000;
                    return ("wait", props, label);

                case StepType.Set:
                    props["expression"] = step.Set;
                    return ("set", props, label);

                case StepType.Exit:
                    props["status"] = step.Exit;
                    return ("exit", props, label);

                case StepType.Extract:
                    if (step.Extract != null)
                    {
                        props["pattern"] = step.Extract.Pattern;
                        if (step.Extract.Into != null) props["into"] = JToken.FromObject(step.Extract.Into);
                        if (!string.IsNullOrEmpty(step.Extract.From)) props["source"] = step.Extract.From;
                    }
                    return ("extract", props, label);

                case StepType.If:
                    props["condition"] = step.If;
                    return ("if", props, label);

                case StepType.Foreach:
                    props["expression"] = step.Foreach;
                    return ("foreach", props, label);

                case StepType.While:
                    props["condition"] = step.While;
                    return ("while", props, label);

                case StepType.Switch:
                    props["expression"] = step.Switch;
                    return ("switch", props, label);

                case StepType.Try:
                    return ("try", props, label);

                case StepType.Break:
                    return ("break", props, label);

                case StepType.Continue:
                    return ("continue", props, label);

                case StepType.Call:
                    if (step.Call != null) props["subroutine"] = step.Call.Subroutine;
                    return ("call", props, label);

                case StepType.Return:
                    return ("return", props, label);

                case StepType.Parallel:
                    return ("parallel", props, label);

                case StepType.UpdateColumn:
                    if (step.UpdateColumn != null)
                    {
                        props["column"] = step.UpdateColumn.Column;
                        props["expression"] = step.UpdateColumn.Value;
                    }
                    return ("updatecolumn", props, label);

                case StepType.UpdateEnvironment:
                    if (step.UpdateEnvironment != null)
                    {
                        props["variable"] = step.UpdateEnvironment.Variable;
                        props["expression"] = step.UpdateEnvironment.Value;
                    }
                    return ("updateenvironment", props, label);

                case StepType.Ping:
                    if (step.Ping != null) props["target"] = step.Ping.Host;
                    return ("ping", props, label);

                case StepType.Dns:
                    if (step.Dns != null) props["hostname"] = step.Dns.Host;
                    return ("dns", props, label);

                case StepType.Portcheck:
                    if (step.Portcheck != null) props["target"] = $"{step.Portcheck.Host}:{step.Portcheck.Port}";
                    return ("portcheck", props, label);

                case StepType.Http:
                    if (step.Http != null)
                    {
                        props["url"] = step.Http.Url;
                        if (step.Http.Method != null) props["method"] = step.Http.Method;
                    }
                    return ("http", props, label);

                case StepType.Webhook:
                    if (step.Webhook != null) props["url"] = step.Webhook.Url;
                    return ("webhook", props, label);

                case StepType.Readfile:
                    if (step.Readfile != null) props["path"] = step.Readfile.Path;
                    return ("readfile", props, label);

                case StepType.Writefile:
                    if (step.Writefile != null) props["path"] = step.Writefile.Path;
                    return ("writefile", props, label);

                case StepType.Log:
                    props["message"] = step.Log?.ToString();
                    return ("log", props, label);

                case StepType.Input:
                    if (step.Input != null)
                    {
                        props["prompt"] = step.Input.Prompt;
                        props["into"] = step.Input.Into;
                    }
                    return ("input", props, label);

                case StepType.Choose:
                    if (step.Choose != null) props["prompt"] = step.Choose.Prompt;
                    return ("choose", props, label);

                case StepType.Multiselect:
                    if (step.Multiselect != null) props["prompt"] = step.Multiselect.Prompt;
                    return ("multiselect", props, label);

                case StepType.Confirm:
                    if (step.Confirm != null) props["prompt"] = step.Confirm.Prompt;
                    return ("confirm", props, label);

                case StepType.Interactive:
                    return ("interactive", props, label);

                case StepType.Assert:
                    if (step.Assert != null) props["condition"] = step.Assert.Condition;
                    return ("assert", props, label);

                case StepType.Sftp:
                    if (step.Sftp != null)
                    {
                        props["action"] = step.Sftp.Action;
                        props["local"] = step.Sftp.LocalPath;
                        props["remote"] = step.Sftp.RemotePath;
                    }
                    return ("sftp", props, label);

                case StepType.Table:
                    return ("table", props, label);

                case StepType.Parse:
                    return ("parse", props, label);

                case StepType.BrowserCallbackCapture:
                    if (step.BrowserCallbackCapture != null) props["url"] = step.BrowserCallbackCapture.StartUrl;
                    return ("browser_callback", props, label);

                default:
                    return ("send", props, $"Unknown: {stepType}");
            }
        }

        #endregion

        #region Graph → YAML

        /// <summary>
        /// Converts graph JSON from React Flow back to YAML script text.
        /// This is a simplified conversion — generates clean, readable YAML.
        /// </summary>
        public string ToYaml(JObject graphData)
        {
            var nodes = graphData["nodes"] as JArray ?? new JArray();
            var edges = graphData["edges"] as JArray ?? new JArray();

            // Build adjacency: source → targets
            var outgoing = new Dictionary<string, List<(string targetId, string? sourceHandle)>>();
            foreach (var edge in edges)
            {
                var src = edge["source"]?.ToString();
                var tgt = edge["target"]?.ToString();
                if (src == null || tgt == null) continue;

                if (!outgoing.ContainsKey(src))
                    outgoing[src] = new List<(string, string?)>();
                outgoing[src].Add((tgt, edge["sourceHandle"]?.ToString()));
            }

            // Find roots (nodes with no incoming edges)
            var hasIncoming = new HashSet<string>();
            foreach (var edge in edges)
            {
                var tgt = edge["target"]?.ToString();
                if (tgt != null) hasIncoming.Add(tgt);
            }

            var nodeMap = new Dictionary<string, JToken>();
            foreach (var n in nodes)
            {
                var id = n["id"]?.ToString();
                if (id != null) nodeMap[id] = n;
            }

            var roots = nodes
                .Select(n => n["id"]?.ToString())
                .Where(id => id != null && !hasIncoming.Contains(id))
                .ToList();

            var lines = new List<string>();
            lines.Add("steps:");

            var visited = new HashSet<string>();
            foreach (var rootId in roots!)
            {
                EmitChain(rootId!, nodeMap, outgoing, lines, visited, indent: 2);
            }

            return string.Join("\n", lines);
        }

        private void EmitChain(
            string nodeId,
            Dictionary<string, JToken> nodeMap,
            Dictionary<string, List<(string targetId, string? sourceHandle)>> outgoing,
            List<string> lines,
            HashSet<string> visited,
            int indent)
        {
            if (!visited.Add(nodeId)) return;
            if (!nodeMap.TryGetValue(nodeId, out var node)) return;

            var data = node["data"];
            var blockType = data?["blockType"]?.ToString() ?? "send";
            var props = data?["props"] as JObject ?? new JObject();
            var pad = new string(' ', indent);

            EmitStep(blockType, props, lines, pad);

            // Follow outgoing edges (skip "false" handle — those are else branches)
            if (outgoing.TryGetValue(nodeId, out var targets))
            {
                foreach (var (targetId, handle) in targets)
                {
                    if (handle != "false")
                    {
                        EmitChain(targetId, nodeMap, outgoing, lines, visited, indent);
                    }
                }
            }
        }

        private static void EmitStep(string blockType, JObject props, List<string> lines, string pad)
        {
            switch (blockType)
            {
                case "send":
                    lines.Add($"{pad}- send: {props["command"] ?? ""}");
                    if (props["timeout"] != null) lines.Add($"{pad}  timeout: {props["timeout"]}");
                    if (props["on_error"] != null && props["on_error"]!.ToString() != "stop")
                        lines.Add($"{pad}  on_error: {props["on_error"]}");
                    break;

                case "print":
                    lines.Add($"{pad}- print: {props["message"] ?? ""}");
                    break;

                case "wait":
                    lines.Add($"{pad}- wait: {props["duration"] ?? 1000}");
                    break;

                case "set":
                    lines.Add($"{pad}- set: {props["expression"] ?? ""}");
                    break;

                case "exit":
                    lines.Add($"{pad}- exit: {props["status"] ?? "success"}");
                    break;

                case "extract":
                    lines.Add($"{pad}- extract:");
                    lines.Add($"{pad}    pattern: \"{props["pattern"] ?? ""}\"");
                    if (props["into"] != null) lines.Add($"{pad}    into: {props["into"]}");
                    break;

                case "if":
                    lines.Add($"{pad}- if: {props["condition"] ?? "true"}");
                    lines.Add($"{pad}  then:");
                    lines.Add($"{pad}    - print: \"(then branch)\"");
                    break;

                case "foreach":
                    lines.Add($"{pad}- foreach: {props["variable"] ?? "item"} in {props["expression"] ?? "[]"}");
                    lines.Add($"{pad}  do:");
                    lines.Add($"{pad}    - print: \"(loop body)\"");
                    break;

                case "while":
                    lines.Add($"{pad}- while: {props["condition"] ?? "false"}");
                    lines.Add($"{pad}  do:");
                    lines.Add($"{pad}    - print: \"(loop body)\"");
                    break;

                case "break":
                    lines.Add($"{pad}- break:");
                    break;

                case "continue":
                    lines.Add($"{pad}- continue:");
                    break;

                case "call":
                    lines.Add($"{pad}- call: {props["subroutine"] ?? ""}");
                    break;

                case "return":
                    lines.Add($"{pad}- return:");
                    break;

                case "updatecolumn":
                    lines.Add($"{pad}- updatecolumn:");
                    lines.Add($"{pad}    column: {props["column"] ?? ""}");
                    lines.Add($"{pad}    value: {props["expression"] ?? ""}");
                    break;

                case "updateenvironment":
                    lines.Add($"{pad}- updateenvironment:");
                    lines.Add($"{pad}    variable: {props["variable"] ?? ""}");
                    lines.Add($"{pad}    value: {props["expression"] ?? ""}");
                    break;

                case "ping":
                    lines.Add($"{pad}- ping: {props["target"] ?? ""}");
                    break;

                case "dns":
                    lines.Add($"{pad}- dns: {props["hostname"] ?? ""}");
                    break;

                case "portcheck":
                    lines.Add($"{pad}- portcheck: {props["target"] ?? ""}");
                    break;

                case "http":
                    lines.Add($"{pad}- http:");
                    lines.Add($"{pad}    url: {props["url"] ?? ""}");
                    if (props["method"] != null) lines.Add($"{pad}    method: {props["method"]}");
                    break;

                case "webhook":
                    lines.Add($"{pad}- webhook: {props["url"] ?? ""}");
                    break;

                case "log":
                    lines.Add($"{pad}- log: {props["message"] ?? ""}");
                    break;

                case "assert":
                    lines.Add($"{pad}- assert: {props["condition"] ?? "true"}");
                    break;

                default:
                    lines.Add($"{pad}- {blockType}: # unsupported in visual export");
                    break;
            }
        }

        #endregion
    }
}
