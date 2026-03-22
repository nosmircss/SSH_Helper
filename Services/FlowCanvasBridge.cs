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

        #region YAML → Graph (using raw text splitting)

        /// <summary>
        /// Converts YAML script text into graph JSON by splitting into step snippets.
        /// Each node stores the original YAML for lossless round-tripping.
        /// </summary>
        public (JArray nodes, JArray edges) TextToGraph(string yamlText)
        {
            var nodes = new JArray();
            var edges = new JArray();
            var idCounter = 0;

            string NextId() => $"node-{idCounter++}";

            // Parse to get step types/labels, but use raw text for data
            var parser = new ScriptParser();
            var script = parser.Parse(yamlText);

            // Split the YAML text into individual step snippets
            var stepSnippets = SplitYamlSteps(yamlText);

            // Build preamble (everything before "steps:")
            var preamble = ExtractPreamble(yamlText);

            var currentY = NodeStartY;
            string? lastNodeId = null;

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
                        ["label"] = blockType.ToUpperInvariant(),
                        ["props"] = stepProps,
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
                        ["label"] = blockType.ToUpperInvariant(),
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
