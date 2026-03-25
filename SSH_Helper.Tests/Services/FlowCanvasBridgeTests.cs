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
}
