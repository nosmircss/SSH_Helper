using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;

internal static class Program
{
    private static readonly HashSet<string> IgnoredSemanticProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "DeclaredTopLevelKeys",
        "ParseErrors",
        "SubroutineRegistry",
        "LineNumber",
        "StepPath",
        "DeclaredStepType",
        "UsesStepRootOnError",
    };

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: FlowCanvasParityCli <prepare-cases|evaluate-cases> [args]");
            return 1;
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "prepare-cases":
                    return PrepareCases(args.Skip(1).ToArray());
                case "evaluate-cases":
                    return EvaluateCases();
                default:
                    Console.Error.WriteLine($"Unknown command '{args[0]}'.");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int PrepareCases(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("prepare-cases requires a path to qa_presets.json");
            return 1;
        }

        var qaPresetsPath = args[0];
        var includeSyntheticBrowserCallback = args.Any(arg =>
            string.Equals(arg, "--include-synthetic-browser-callback", StringComparison.OrdinalIgnoreCase));

        if (!File.Exists(qaPresetsPath))
        {
            Console.Error.WriteLine($"qa_presets.json not found: {qaPresetsPath}");
            return 1;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(qaPresetsPath));
        if (!document.RootElement.TryGetProperty("presets", out var presetsElement) ||
            presetsElement.ValueKind != JsonValueKind.Object)
        {
            Console.Error.WriteLine($"Invalid qa_presets.json format: missing object 'presets' in {qaPresetsPath}");
            return 1;
        }

        var bridge = new FlowCanvasBridge();
        var parser = new ScriptParser();
        var preparedCases = new JArray();

        foreach (var presetProperty in presetsElement.EnumerateObject())
        {
            if (!presetProperty.Value.TryGetProperty("commands", out var commandsElement) ||
                commandsElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var commands = commandsElement.GetString() ?? string.Empty;
            if (!ScriptParser.IsYamlScript(commands))
            {
                continue;
            }

            preparedCases.Add(BuildPreparedCase(
                presetProperty.Name,
                commands,
                isSynthetic: false,
                parser,
                bridge));
        }

        if (includeSyntheticBrowserCallback)
        {
            const string syntheticName = "Synthetic Browser Callback";
            preparedCases.Add(BuildPreparedCase(
                syntheticName,
                BuildSyntheticBrowserCallbackYaml(),
                isSynthetic: true,
                parser,
                bridge));
        }

        var payload = new JObject
        {
            ["cases"] = preparedCases
        };

        Console.Out.Write(payload.ToString(Formatting.None));
        return 0;
    }

    private static int EvaluateCases()
    {
        var stdin = Console.In.ReadToEnd();
        if (string.IsNullOrWhiteSpace(stdin))
        {
            Console.Error.WriteLine("evaluate-cases expects JSON payload on stdin.");
            return 1;
        }

        JObject input;
        try
        {
            input = JObject.Parse(stdin);
        }
        catch (Newtonsoft.Json.JsonReaderException ex)
        {
            Console.Error.WriteLine($"Invalid input JSON: {ex.Message}");
            return 1;
        }

        if (input["cases"] is not JArray caseArray)
        {
            Console.Error.WriteLine("Input payload must contain a 'cases' array.");
            return 1;
        }

        var bridge = new FlowCanvasBridge();
        var parser = new ScriptParser();
        var results = new JArray();

        foreach (var token in caseArray)
        {
            if (token is not JObject caseObject)
            {
                continue;
            }

            var name = caseObject.Value<string>("name") ?? "unnamed-case";
            var sourceYaml = caseObject.Value<string>("sourceYaml") ?? string.Empty;
            var nodes = caseObject["nodes"] as JArray ?? new JArray();
            var edges = caseObject["edges"] as JArray ?? new JArray();

            var sourceAnalysis = AnalyzeYaml(parser, sourceYaml);

            var graph = new JObject
            {
                ["nodes"] = nodes,
                ["edges"] = edges,
            };

            var export = bridge.ExportGraphToYaml(graph);
            var exportYaml = export.Yaml ?? string.Empty;
            var exportAnalysis = AnalyzeYaml(parser, exportYaml);

            bool? semanticEquivalent = null;
            string? semanticDiff = null;
            if (sourceAnalysis.Script != null && exportAnalysis.Script != null)
            {
                var sourceCanonical = CanonicalizeScript(sourceAnalysis.Script);
                var exportCanonical = CanonicalizeScript(exportAnalysis.Script);
                semanticEquivalent = JToken.DeepEquals(sourceCanonical, exportCanonical);
                if (semanticEquivalent == false)
                {
                    semanticDiff = DescribeFirstDifference(sourceCanonical, exportCanonical, "$");
                }
            }

            results.Add(new JObject
            {
                ["name"] = name,
                ["sourceParseError"] = sourceAnalysis.ParseError,
                ["sourceValidationErrors"] = new JArray(sourceAnalysis.ValidationErrors),
                ["exportSuccess"] = export.Success,
                ["exportErrors"] = new JArray(export.Errors),
                ["exportWarnings"] = new JArray(export.Warnings),
                ["exportYaml"] = exportYaml,
                ["exportParseError"] = exportAnalysis.ParseError,
                ["exportValidationErrors"] = new JArray(exportAnalysis.ValidationErrors),
                ["semanticEquivalent"] = semanticEquivalent is null ? JValue.CreateNull() : semanticEquivalent.Value,
                ["semanticDiff"] = semanticDiff,
            });
        }

        var payload = new JObject
        {
            ["results"] = results
        };

        Console.Out.Write(payload.ToString(Formatting.None));
        return 0;
    }

    private static JObject BuildPreparedCase(
        string name,
        string commands,
        bool isSynthetic,
        ScriptParser parser,
        FlowCanvasBridge bridge)
    {
        var sourceAnalysis = AnalyzeYaml(parser, commands);

        JToken nodes = new JArray();
        JToken edges = new JArray();
        string? graphBuildError = null;

        if (sourceAnalysis.Script != null)
        {
            try
            {
                var (graphNodes, graphEdges) = bridge.TextToGraph(commands);
                nodes = graphNodes;
                edges = graphEdges;
            }
            catch (Exception ex)
            {
                graphBuildError = ex.Message;
            }
        }

        return new JObject
        {
            ["name"] = name,
            ["classification"] = IsIntentionalInvalidCase(name, commands) ? "intentional-invalid" : "valid",
            ["isSynthetic"] = isSynthetic,
            ["sourceYaml"] = commands,
            ["sourceParseError"] = sourceAnalysis.ParseError,
            ["sourceValidationErrors"] = new JArray(sourceAnalysis.ValidationErrors),
            ["graphBuildError"] = graphBuildError,
            ["nodes"] = nodes,
            ["edges"] = edges,
        };
    }

    private static bool IsIntentionalInvalidCase(string name, string sourceYaml)
    {
        if (name.Contains("[Expected Fail]", StringComparison.OrdinalIgnoreCase))
            return true;

        return sourceYaml.Contains("Expected: intentional validation failure.", StringComparison.Ordinal);
    }

    private static YamlAnalysis AnalyzeYaml(ScriptParser parser, string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new YamlAnalysis(
                Script: null,
                ParseError: "YAML input is empty.",
                ValidationErrors: new List<string>());
        }

        try
        {
            var script = parser.Parse(yaml);
            var validationErrors = parser.Validate(script, yaml, enforceCanonicalSyntax: true);
            return new YamlAnalysis(script, ParseError: null, ValidationErrors: validationErrors);
        }
        catch (ScriptParseException ex)
        {
            return new YamlAnalysis(
                Script: null,
                ParseError: ex.Message,
                ValidationErrors: new List<string>());
        }
    }

    private static JToken CanonicalizeScript(Script script)
    {
        var serializer = Newtonsoft.Json.JsonSerializer.Create(new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        });
        var token = JToken.FromObject(script, serializer);
        return NormalizeToken(token);
    }

    private static JToken NormalizeToken(JToken token)
    {
        if (token.Type == JTokenType.Object)
        {
            var obj = new JObject();
            foreach (var property in ((JObject)token).Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (IgnoredSemanticProperties.Contains(property.Name))
                    continue;

                var normalized = property.Name.Equals("Log", StringComparison.OrdinalIgnoreCase)
                    ? NormalizeLogToken(property.Value)
                    : NormalizeToken(property.Value);
                if (normalized.Type == JTokenType.Null)
                    continue;

                obj[property.Name] = normalized;
            }

            // For choose/multiselect options, label differences are presentation-only in parity checks.
            var labelProperty = obj.Property("Label", StringComparison.OrdinalIgnoreCase);
            var valueProperty = obj.Property("Value", StringComparison.OrdinalIgnoreCase);
            if (labelProperty != null && valueProperty != null)
            {
                labelProperty.Value = valueProperty.Value.DeepClone();
            }

            return obj;
        }

        if (token.Type == JTokenType.Array)
        {
            var array = new JArray();
            foreach (var child in (JArray)token)
            {
                array.Add(NormalizeToken(child));
            }

            return array;
        }

        return token;
    }

    private static JToken NormalizeLogToken(JToken logToken)
    {
        if (logToken.Type == JTokenType.String)
        {
            return new JObject
            {
                ["Message"] = logToken.Value<string>() ?? string.Empty,
                ["Level"] = "info",
            };
        }

        if (logToken.Type == JTokenType.Object)
        {
            var logObject = (JObject)NormalizeToken(logToken);
            if (logObject.Property("Level", StringComparison.OrdinalIgnoreCase) == null)
            {
                logObject["Level"] = "info";
            }

            return logObject;
        }

        return NormalizeToken(logToken);
    }

    private static string DescribeFirstDifference(JToken left, JToken right, string path)
    {
        if (left.Type != right.Type)
        {
            return $"{path}: token type mismatch ({left.Type} != {right.Type}).";
        }

        if (left is JObject leftObject && right is JObject rightObject)
        {
            var leftNames = leftObject.Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var rightNames = rightObject.Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

            if (!leftNames.SequenceEqual(rightNames, StringComparer.Ordinal))
            {
                return $"{path}: property set mismatch ({string.Join(", ", leftNames)} != {string.Join(", ", rightNames)}).";
            }

            foreach (var propertyName in leftNames)
            {
                var nestedPath = $"{path}.{propertyName}";
                var nested = DescribeFirstDifference(leftObject[propertyName]!, rightObject[propertyName]!, nestedPath);
                if (!string.IsNullOrEmpty(nested))
                {
                    return nested;
                }
            }

            return string.Empty;
        }

        if (left is JArray leftArray && right is JArray rightArray)
        {
            if (leftArray.Count != rightArray.Count)
            {
                return $"{path}: array length mismatch ({leftArray.Count} != {rightArray.Count}).";
            }

            for (var i = 0; i < leftArray.Count; i++)
            {
                var nested = DescribeFirstDifference(leftArray[i]!, rightArray[i]!, $"{path}[{i}]");
                if (!string.IsNullOrEmpty(nested))
                {
                    return nested;
                }
            }

            return string.Empty;
        }

        if (JToken.DeepEquals(left, right))
            return string.Empty;

        var leftValue = left.ToString(Formatting.None);
        var rightValue = right.ToString(Formatting.None);
        if (leftValue.Length > 200) leftValue = $"{leftValue[..200]}...";
        if (rightValue.Length > 200) rightValue = $"{rightValue[..200]}...";
        return $"{path}: value mismatch ({leftValue} != {rightValue}).";
    }

    private static string BuildSyntheticBrowserCallbackYaml()
    {
        return """
            ---
            name: Synthetic Browser Callback
            description: "Requires: internet access for OAuth callback simulation. Expected: pass."
            steps:
              - browser_callback_capture:
                  start_url: "https://example.com/oauth"
                  callback_path: "/callback"
                  local_port: 8086
                  capture_mode: auto
                  browser_mode: external
                  into: oauth_result
              - exit: "success PASS - synthetic browser callback"
            """;
    }

    private sealed record YamlAnalysis(
        Script? Script,
        string? ParseError,
        List<string> ValidationErrors);
}
