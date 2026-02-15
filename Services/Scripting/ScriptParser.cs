using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Parses YAML script text into a Script object.
    /// </summary>
    public class ScriptParser
    {
        private readonly IDeserializer _deserializer;
        private readonly List<string> _warnings = new();
        private static readonly string[] KnownStepKeys =
        {
            "send",
            "print",
            "wait",
            "set",
            "exit",
            "extract",
            "if",
            "foreach",
            "while",
            "updatecolumn",
            "updateenvironment",
            "readfile",
            "writefile",
            "input",
            "log",
            "http",
            "ping",
            "dns",
            "portcheck",
            "sftp",
            "webhook",
            "parse",
            "choose",
            "multiselect",
            "confirm",
            "interactive",
            "break",
            "continue",
            "try"
        };
        private static readonly string[] KnownTopLevelKeys =
        {
            "name",
            "description",
            "version",
            "debug",
            "nobanner",
            "vars",
            "steps"
        };
        private static readonly string[] CommonStepOptionKeys =
        {
            "capture",
            "suppress",
            "expect",
            "timeout",
            "on_error",
            "when",
            "max_iterations",
            "then",
            "elif",
            "else",
            "do",
            "catch",
            "finally"
        };
        private static readonly IReadOnlyDictionary<string, string[]> CommandOptionKeys =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["send"] = ["command", "capture", "suppress", "expect", "timeout", "on_error"],
                ["print"] = ["message"],
                ["wait"] = ["seconds"],
                ["set"] = ["expression"],
                ["exit"] = ["status", "message"],
                ["extract"] = ["from", "pattern", "into", "match"],
                ["if"] = ["condition", "then", "elif", "else"],
                ["foreach"] = ["iterator", "when", "do"],
                ["while"] = ["condition", "max_iterations", "do"],
                ["try"] = ["do", "catch", "finally"],
                ["readfile"] = ["path", "into", "skip_empty_lines", "trim_lines", "max_lines", "encoding"],
                ["writefile"] = ["path", "content", "mode", "format", "pretty", "headers"],
                ["input"] = ["prompt", "into", "default", "password", "validate", "validation_error"],
                ["updatecolumn"] = ["column", "value"],
                ["updateenvironment"] = ["variable", "value"],
                ["log"] = ["message", "level"],
                ["http"] = ["url", "method", "body", "headers", "into", "timeout", "follow_redirects", "allow_failure", "verify_tls", "auth", "username", "password", "token", "content_type", "on_error"],
                ["ping"] = ["host", "count", "timeout", "into", "on_error"],
                ["dns"] = ["host", "type", "timeout", "into", "on_error"],
                ["portcheck"] = ["host", "port", "timeout", "into", "on_error"],
                ["sftp"] = ["action", "local_path", "remote_path", "host", "port", "username", "password", "overwrite", "timeout", "into", "on_error"],
                ["webhook"] = ["url", "method", "body", "headers", "into", "timeout", "on_error"],
                ["parse"] = ["format", "from", "into", "sections"],
                ["choose"] = ["prompt", "into", "options", "default"],
                ["multiselect"] = ["prompt", "into", "options", "min", "max"],
                ["confirm"] = ["prompt", "into", "default"],
                ["interactive"] = ["session", "command", "capture", "max_seconds", "mirror_output", "on_error"]
            };
        private static readonly IReadOnlyDictionary<string, string[]> StepRootOptionKeysByCommand =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["send"] = [],
                ["print"] = [],
                ["wait"] = [],
                ["set"] = [],
                ["exit"] = [],
                ["extract"] = [],
                ["if"] = [],
                ["foreach"] = [],
                ["while"] = [],
                ["updatecolumn"] = [],
                ["updateenvironment"] = [],
                ["readfile"] = [],
                ["writefile"] = [],
                ["input"] = [],
                ["log"] = [],
                ["http"] = [],
                ["ping"] = [],
                ["dns"] = [],
                ["portcheck"] = [],
                ["sftp"] = [],
                ["webhook"] = [],
                ["parse"] = [],
                ["choose"] = [],
                ["multiselect"] = [],
                ["confirm"] = [],
                ["interactive"] = [],
                ["break"] = [],
                ["continue"] = [],
                ["try"] = []
            };
        private static readonly HashSet<string> CanonicalMapCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "send",
            "print",
            "wait",
            "set",
            "exit",
            "if",
            "foreach",
            "while",
            "try"
        };
        private static readonly HashSet<StepType> CommandMapOnErrorStepTypes =
        [
            StepType.Send,
            StepType.Readfile,
            StepType.Writefile,
            StepType.Input,
            StepType.Http,
            StepType.Ping,
            StepType.Dns,
            StepType.Portcheck,
            StepType.Sftp,
            StepType.Webhook,
            StepType.Choose,
            StepType.Multiselect,
            StepType.Confirm,
            StepType.Interactive
        ];
        private static readonly HashSet<string> ExitStatusTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "success",
            "failure",
            "fail",
            "error"
        };
        private static readonly string[] KnownStepOptionKeys = CommonStepOptionKeys
            .Concat(StepRootOptionKeysByCommand.Values.SelectMany(values => values))
            .Concat(CommandOptionKeys.Values.SelectMany(values => values))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        private static readonly IReadOnlyDictionary<string, string[]> EnumLikeOptionValues =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["on_error"] = ["continue", "stop"],
                ["method"] = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"],
                ["auth"] = ["none", "basic", "bearer"],
                ["content_type"] = ["json", "form", "text", "xml"],
                ["type"] = ["A", "AAAA", "PTR"],
                ["action"] = ["upload", "download"],
                ["mode"] = ["overwrite", "append"],
                ["format"] = ["text", "json", "jsonl", "csv"],
                ["level"] = ["info", "debug", "warning", "error", "success"],
                ["encoding"] = ["utf-8", "ascii", "utf-16", "utf-32"],
                ["follow_redirects"] = ["true", "false"],
                ["allow_failure"] = ["true", "false"],
                ["verify_tls"] = ["true", "false"],
                ["session"] = ["separate", "shared"],
                ["mirror_output"] = ["true", "false"]
            };

        /// <summary>
        /// Parser warnings captured during the most recent parse operation.
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings;

        public static IReadOnlyList<string> GetKnownTopLevelKeys() => KnownTopLevelKeys;

        public static IReadOnlyList<string> GetKnownStepCommands() => KnownStepKeys;

        public static IReadOnlyList<string> GetKnownCommonStepOptionKeys()
        {
            return CommonStepOptionKeys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IReadOnlyDictionary<string, IReadOnlyList<string>> GetKnownStepOptionKeysByCommand()
        {
            return CommandOptionKeys.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
        }

        public static IReadOnlyDictionary<string, IReadOnlyList<string>> GetKnownStepRootOptionKeysByCommand()
        {
            return StepRootOptionKeysByCommand.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
        }

        public static IReadOnlyList<string> GetKnownStepOptionKeys() => KnownStepOptionKeys;

        public static IReadOnlyDictionary<string, IReadOnlyList<string>> GetEnumLikeOptionValues()
        {
            return EnumLikeOptionValues.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        public ScriptParser()
        {
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        /// <summary>
        /// Detects if the given text is a YAML script (vs plain commands).
        /// </summary>
        public static bool IsYamlScript(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Use strong script indicators only. Metadata keys like name:/description:
            // are intentionally excluded because many plain CLI commands use them.
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                // YAML document marker
                if (trimmedLine.StartsWith("---", StringComparison.Ordinal))
                {
                    return true;
                }

                // Distinctive top-level script sections
                if (trimmedLine.StartsWith("steps:", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("vars:", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Step syntax: "- send:", "- print:", etc.
                if (trimmedLine.StartsWith("- ", StringComparison.Ordinal))
                {
                    var stepContent = trimmedLine.Substring(2);
                    foreach (var stepKey in KnownStepKeys)
                    {
                        if (stepContent.StartsWith($"{stepKey}:", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Parses YAML text into a Script object.
        /// </summary>
        /// <param name="yamlText">The YAML script text.</param>
        /// <returns>A parsed Script object.</returns>
        /// <exception cref="ScriptParseException">If parsing fails.</exception>
        public Script Parse(string yamlText)
        {
            try
            {
                _warnings.Clear();

                // Use a custom approach to handle the flexible step format
                using var reader = new StringReader(yamlText);
                var parser = new Parser(reader);

                var script = new Script();
                parser.Consume<StreamStart>();

                if (parser.Accept<DocumentStart>(out _))
                {
                    parser.Consume<DocumentStart>();
                }

                if (parser.Accept<MappingStart>(out _))
                {
                    parser.Consume<MappingStart>();

                    while (!parser.Accept<MappingEnd>(out _))
                    {
                        var key = parser.Consume<Scalar>();
                        var keyName = key.Value.ToLowerInvariant();

                        switch (keyName)
                        {
                            case "name":
                                script.Name = parser.Consume<Scalar>().Value;
                                break;
                            case "description":
                                script.Description = parser.Consume<Scalar>().Value;
                                break;
                            case "version":
                                if (int.TryParse(parser.Consume<Scalar>().Value, out var ver))
                                    script.Version = ver;
                                break;
                            case "debug":
                                var debugValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                                script.Debug = debugValue == "true" || debugValue == "yes" || debugValue == "1";
                                break;
                            case "nobanner":
                                var nobannerValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                                script.NoBanner = nobannerValue == "true" || nobannerValue == "yes" || nobannerValue == "1";
                                break;
                            case "vars":
                                script.Vars = ParseVars(parser);
                                break;
                            case "steps":
                                script.Steps = ParseSteps(parser);
                                break;
                            default:
                                AddUnknownKeyWarning($"Unknown top-level key '{key.Value}'", (int)key.Start.Line);
                                SkipValue(parser);
                                break;
                        }
                    }

                    parser.Consume<MappingEnd>();
                }

                return script;
            }
            catch (YamlException ex)
            {
                throw new ScriptParseException($"YAML parsing error at line {ex.Start.Line}: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not ScriptParseException)
            {
                throw new ScriptParseException($"Failed to parse script: {ex.Message}", ex);
            }
        }

        private Dictionary<string, object?> ParseVars(IParser parser)
        {
            var vars = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var key = parser.Consume<Scalar>().Value;
                    var value = ParseScalarOrSequence(parser);
                    vars[key] = value;
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                // Skip if not a mapping
                SkipValue(parser);
            }

            return vars;
        }

        private List<ScriptStep> ParseSteps(IParser parser)
        {
            var steps = new List<ScriptStep>();

            if (parser.Accept<SequenceStart>(out _))
            {
                parser.Consume<SequenceStart>();

                while (!parser.Accept<SequenceEnd>(out _))
                {
                    var step = ParseStep(parser);
                    if (step != null)
                        steps.Add(step);
                }

                parser.Consume<SequenceEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return steps;
        }

        private ScriptStep? ParseStep(IParser parser)
        {
            if (!parser.Accept<MappingStart>(out _))
            {
                var invalidStep = new ScriptStep
                {
                    LineNumber = (int)(parser.Current?.Start.Line ?? 0)
                };
                AddStepParseError(invalidStep, "step must be a mapping (for example '- print: message')");
                SkipValue(parser);
                return invalidStep;
            }

            var step = new ScriptStep();

            // Get line number for error reporting
            var mappingStart = parser.Consume<MappingStart>();
            step.LineNumber = (int)mappingStart.Start.Line;

            while (!parser.Accept<MappingEnd>(out _))
            {
                var key = parser.Consume<Scalar>();
                var keyName = key.Value.ToLowerInvariant();

                switch (keyName)
                {
                    case "send":
                        step.DeclaredStepType = StepType.Send;
                        ParseSendStep(parser, step);
                        break;
                    case "print":
                        step.DeclaredStepType = StepType.Print;
                        ParsePrintStep(parser, step);
                        break;
                    case "wait":
                        step.DeclaredStepType = StepType.Wait;
                        ParseWaitStep(parser, step);
                        break;
                    case "set":
                        step.DeclaredStepType = StepType.Set;
                        ParseSetStep(parser, step);
                        break;
                    case "exit":
                        step.DeclaredStepType = StepType.Exit;
                        ParseExitStep(parser, step);
                        break;
                    case "if":
                        step.DeclaredStepType = StepType.If;
                        ParseIfStep(parser, step);
                        break;
                    case "foreach":
                        step.DeclaredStepType = StepType.Foreach;
                        ParseForeachStep(parser, step);
                        break;
                    case "while":
                        step.DeclaredStepType = StepType.While;
                        ParseWhileStep(parser, step);
                        break;
                    case "break":
                        step.DeclaredStepType = StepType.Break;
                        step.BreakLoop = ParseBooleanish(parser);
                        break;
                    case "continue":
                        step.DeclaredStepType = StepType.Continue;
                        step.ContinueLoop = ParseBooleanish(parser);
                        break;
                    case "try":
                        step.DeclaredStepType = StepType.Try;
                        ParseTryStep(parser, step);
                        break;
                    case "capture":
                        step.Capture = parser.Consume<Scalar>().Value;
                        break;
                    case "suppress":
                        var suppressValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                        step.Suppress = suppressValue == "true" || suppressValue == "yes" || suppressValue == "1";
                        break;
                    case "expect":
                        step.Expect = parser.Consume<Scalar>().Value;
                        break;
                    case "timeout":
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var timeout))
                            step.Timeout = timeout;
                        break;
                    case "on_error":
                    case "onerror":
                        step.UsesStepRootOnError = true;
                        step.OnError = parser.Consume<Scalar>().Value;
                        break;
                    case "when":
                        step.When = parser.Consume<Scalar>().Value;
                        break;
                    case "max_iterations":
                    case "maxiterations":
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var maxIterations))
                            step.MaxIterations = maxIterations;
                        break;
                    case "extract":
                        step.DeclaredStepType = StepType.Extract;
                        step.Extract = ParseExtractOptions(parser);
                        break;
                    case "readfile":
                        step.DeclaredStepType = StepType.Readfile;
                        step.Readfile = ParseReadfileOptions(parser);
                        break;
                    case "writefile":
                        step.DeclaredStepType = StepType.Writefile;
                        step.Writefile = ParseWritefileOptions(parser);
                        break;
                    case "input":
                        step.DeclaredStepType = StepType.Input;
                        step.Input = ParseInputOptions(parser);
                        break;
                    case "updatecolumn":
                        step.DeclaredStepType = StepType.UpdateColumn;
                        step.UpdateColumn = ParseUpdateColumnOptions(parser);
                        break;
                    case "updateenvironment":
                        step.DeclaredStepType = StepType.UpdateEnvironment;
                        step.UpdateEnvironment = ParseUpdateEnvironmentOptions(parser);
                        break;
                    case "log":
                        step.DeclaredStepType = StepType.Log;
                        step.Log = ParseLogValue(parser);
                        break;
                    case "http":
                        step.DeclaredStepType = StepType.Http;
                        step.Http = ParseHttpOptions(parser, step);
                        break;
                    case "ping":
                        step.DeclaredStepType = StepType.Ping;
                        step.Ping = ParsePingOptions(parser, step);
                        break;
                    case "dns":
                        step.DeclaredStepType = StepType.Dns;
                        step.Dns = ParseDnsOptions(parser, step);
                        break;
                    case "portcheck":
                        step.DeclaredStepType = StepType.Portcheck;
                        step.Portcheck = ParsePortcheckOptions(parser, step);
                        break;
                    case "sftp":
                        step.DeclaredStepType = StepType.Sftp;
                        step.Sftp = ParseSftpOptions(parser, step);
                        break;
                    case "webhook":
                        step.DeclaredStepType = StepType.Webhook;
                        step.Webhook = ParseWebhookOptions(parser, step);
                        break;
                    case "parse":
                        step.DeclaredStepType = StepType.Parse;
                        step.Parse = ParseParseOptions(parser);
                        break;
                    case "choose":
                        step.DeclaredStepType = StepType.Choose;
                        step.Choose = ParseChooseOptions(parser);
                        break;
                    case "multiselect":
                        step.DeclaredStepType = StepType.Multiselect;
                        step.Multiselect = ParseMultiselectOptions(parser);
                        break;
                    case "confirm":
                        step.DeclaredStepType = StepType.Confirm;
                        step.Confirm = ParseConfirmOptions(parser);
                        break;
                    case "interactive":
                        step.DeclaredStepType = StepType.Interactive;
                        step.Interactive = ParseInteractiveOptions(parser, step);
                        break;
                    case "then":
                        step.Then = ParseSteps(parser);
                        break;
                    case "elif":
                        step.Elif = ParseElifBranches(parser);
                        break;
                    case "else":
                        step.Else = ParseSteps(parser);
                        break;
                    case "do":
                        step.Do = ParseSteps(parser);
                        break;
                    case "catch":
                        step.Catch = ParseSteps(parser);
                        break;
                    case "finally":
                        step.Finally = ParseSteps(parser);
                        break;
                    default:
                        AddUnknownKeyWarning($"Unknown step key '{key.Value}'", (int)key.Start.Line);
                        SkipValue(parser);
                        break;
                }
            }

            parser.Consume<MappingEnd>();
            return step;
        }

        private bool ParseBooleanish(IParser parser)
        {
            if (parser.Accept<Scalar>(out _))
            {
                var value = parser.Consume<Scalar>().Value;
                if (string.IsNullOrWhiteSpace(value))
                    return true;

                var lower = value.ToLowerInvariant();
                return lower == "true" || lower == "yes" || lower == "1";
            }

            SkipValue(parser);
            return true;
        }

        private void ParseSendStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                var hasCommand = false;
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "command":
                            step.Send = parser.Consume<Scalar>().Value;
                            hasCommand = !string.IsNullOrWhiteSpace(step.Send);
                            break;
                        case "capture":
                            step.Capture = parser.Consume<Scalar>().Value;
                            break;
                        case "suppress":
                            step.Suppress = ParseBooleanOrDefault(parser, step.Suppress);
                            break;
                        case "expect":
                            step.Expect = parser.Consume<Scalar>().Value;
                            break;
                        case "timeout":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var timeout))
                                step.Timeout = timeout;
                            break;
                        case "on_error":
                        case "onerror":
                            step.OnError = parser.Consume<Scalar>().Value;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown send key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                if (!hasCommand)
                {
                    AddStepParseError(step, "send.command is required");
                }
                return;
            }

            if (parser.Accept<Scalar>(out _))
            {
                step.Send = parser.Consume<Scalar>().Value;
                if (string.IsNullOrWhiteSpace(step.Send))
                {
                    AddStepParseError(step, "send.command is required");
                }
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "send must be a mapping with required key 'command'");
        }

        private void ParsePrintStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                var hasMessage = false;
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "message":
                            step.Print = parser.Consume<Scalar>().Value;
                            hasMessage = true;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown print key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                if (!hasMessage)
                {
                    AddStepParseError(step, "print.message is required");
                }
                return;
            }

            if (parser.Accept<Scalar>(out _))
            {
                step.Print = parser.Consume<Scalar>().Value;
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "print must be a mapping with required key 'message'");
        }

        private void ParseWaitStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                var hasSeconds = false;
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();
                    switch (key)
                    {
                        case "seconds":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var seconds))
                            {
                                step.Wait = seconds;
                                hasSeconds = true;
                            }
                            else
                            {
                                AddStepParseError(step, "wait.seconds must be an integer");
                            }
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown wait key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                if (!hasSeconds)
                {
                    AddStepParseError(step, "wait.seconds is required");
                }
                return;
            }

            if (parser.Accept<Scalar>(out _))
            {
                if (int.TryParse(parser.Consume<Scalar>().Value, out var seconds))
                {
                    step.Wait = seconds;
                }
                else
                {
                    AddStepParseError(step, "wait.seconds must be an integer");
                }
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "wait must be a mapping with required key 'seconds'");
        }

        private void ParseSetStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                var hasExpression = false;
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();
                    switch (key)
                    {
                        case "expression":
                            step.Set = parser.Consume<Scalar>().Value;
                            hasExpression = !string.IsNullOrWhiteSpace(step.Set);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown set key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                if (!hasExpression)
                {
                    AddStepParseError(step, "set.expression is required");
                }
                return;
            }

            if (parser.Accept<Scalar>(out _))
            {
                step.Set = parser.Consume<Scalar>().Value;
                if (string.IsNullOrWhiteSpace(step.Set))
                {
                    AddStepParseError(step, "set.expression is required");
                }
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "set must be a mapping with required key 'expression'");
        }

        private void ParseExitStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                string? status = null;
                string? message = null;
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();
                    switch (key)
                    {
                        case "status":
                            status = parser.Consume<Scalar>().Value;
                            break;
                        case "message":
                            message = parser.Consume<Scalar>().Value;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown exit key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                if (string.IsNullOrWhiteSpace(status) && string.IsNullOrWhiteSpace(message))
                {
                    AddStepParseError(step, "exit requires at least one of 'status' or 'message'");
                    return;
                }

                var parts = new[] { status, message }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim());
                step.Exit = string.Join(" ", parts);
                return;
            }

            if (parser.Accept<Scalar>(out _))
            {
                var scalar = parser.Consume<Scalar>().Value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(scalar))
                {
                    AddStepParseError(step, "exit requires a message, status, or both");
                    return;
                }

                var firstSpace = scalar.IndexOf(' ');
                if (firstSpace > 0)
                {
                    var statusToken = scalar[..firstSpace];
                    var remainder = scalar[(firstSpace + 1)..].Trim();
                    if (ExitStatusTokens.Contains(statusToken))
                    {
                        step.Exit = string.IsNullOrWhiteSpace(remainder)
                            ? statusToken
                            : $"{statusToken} {remainder}";
                        return;
                    }
                }

                if (ExitStatusTokens.Contains(scalar))
                {
                    step.Exit = scalar;
                    return;
                }

                // Shorthand form defaults to success status.
                step.Exit = $"success {scalar}";
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "exit must be a mapping with key 'status' and/or 'message'");
        }

        private void ParseIfStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                var hasCondition = false;
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();
                    switch (key)
                    {
                        case "condition":
                            step.If = parser.Consume<Scalar>().Value;
                            hasCondition = !string.IsNullOrWhiteSpace(step.If);
                            break;
                        case "then":
                            step.Then = ParseSteps(parser);
                            break;
                        case "elif":
                            step.Elif = ParseElifBranches(parser);
                            break;
                        case "else":
                            step.Else = ParseSteps(parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown if key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                if (!hasCondition)
                {
                    AddStepParseError(step, "if.condition is required");
                }
                return;
            }

            if (parser.Accept<Scalar>(out _))
            {
                step.If = parser.Consume<Scalar>().Value;
                if (string.IsNullOrWhiteSpace(step.If))
                {
                    AddStepParseError(step, "if.condition is required");
                }
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "if must be a mapping with required key 'condition'");
        }

        private void ParseForeachStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                var hasIterator = false;
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();
                    switch (key)
                    {
                        case "iterator":
                            step.Foreach = parser.Consume<Scalar>().Value;
                            hasIterator = !string.IsNullOrWhiteSpace(step.Foreach);
                            break;
                        case "when":
                            step.When = parser.Consume<Scalar>().Value;
                            break;
                        case "do":
                            step.Do = ParseSteps(parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown foreach key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                if (!hasIterator)
                {
                    AddStepParseError(step, "foreach.iterator is required");
                }
                return;
            }

            if (parser.Accept<Scalar>(out _))
            {
                step.Foreach = parser.Consume<Scalar>().Value;
                if (string.IsNullOrWhiteSpace(step.Foreach))
                {
                    AddStepParseError(step, "foreach.iterator is required");
                }
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "foreach must be a mapping with required key 'iterator'");
        }

        private void ParseWhileStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                var hasCondition = false;
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();
                    switch (key)
                    {
                        case "condition":
                            step.While = parser.Consume<Scalar>().Value;
                            hasCondition = !string.IsNullOrWhiteSpace(step.While);
                            break;
                        case "do":
                            step.Do = ParseSteps(parser);
                            break;
                        case "max_iterations":
                        case "maxiterations":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var maxIterations))
                                step.MaxIterations = maxIterations;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown while key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                if (!hasCondition)
                {
                    AddStepParseError(step, "while.condition is required");
                }
                return;
            }

            if (parser.Accept<Scalar>(out _))
            {
                step.While = parser.Consume<Scalar>().Value;
                if (string.IsNullOrWhiteSpace(step.While))
                {
                    AddStepParseError(step, "while.condition is required");
                }
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "while must be a mapping with required key 'condition'");
        }

        private void ParseTryStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                var hasDo = false;
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();
                    switch (key)
                    {
                        case "do":
                            step.Try = ParseSteps(parser);
                            hasDo = step.Try != null;
                            break;
                        case "catch":
                            step.Catch = ParseSteps(parser);
                            break;
                        case "finally":
                            step.Finally = ParseSteps(parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown try key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                if (!hasDo)
                {
                    AddStepParseError(step, "try.do is required");
                }
                return;
            }

            if (parser.Accept<SequenceStart>(out _))
            {
                step.Try = ParseSteps(parser);
                AddLegacyInlineError(step, "try", "do");
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "try must be a mapping with required key 'do'");
        }

        private static void AddStepParseError(ScriptStep step, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (!step.ParseErrors.Contains(message, StringComparer.Ordinal))
            {
                step.ParseErrors.Add(message);
            }
        }

        private static void AddLegacyInlineError(ScriptStep step, string command, string key)
        {
            AddStepParseError(
                step,
                $"Legacy inline syntax '{command}: ...' is not supported; use nested '{command}.{key}' format");
        }

        private List<ElifBranch> ParseElifBranches(IParser parser)
        {
            var branches = new List<ElifBranch>();
            if (!parser.Accept<SequenceStart>(out _))
            {
                SkipValue(parser);
                return branches;
            }

            parser.Consume<SequenceStart>();
            while (!parser.Accept<SequenceEnd>(out _))
            {
                if (!parser.Accept<MappingStart>(out _))
                {
                    SkipValue(parser);
                    continue;
                }

                var mapStart = parser.Consume<MappingStart>();
                var branch = new ElifBranch { LineNumber = (int)mapStart.Start.Line };

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var key = parser.Consume<Scalar>();
                    var keyName = key.Value.ToLowerInvariant();
                    switch (keyName)
                    {
                        case "condition":
                        case "if":
                            branch.If = parser.Consume<Scalar>().Value;
                            break;
                        case "then":
                            branch.Then = ParseSteps(parser) ?? new List<ScriptStep>();
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown elif key '{key.Value}'", (int)key.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                branches.Add(branch);
            }

            parser.Consume<SequenceEnd>();
            return branches;
        }

        private ExtractOptions ParseExtractOptions(IParser parser)
        {
            var options = new ExtractOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "from":
                            options.From = parser.Consume<Scalar>().Value;
                            break;
                        case "pattern":
                            options.Pattern = parser.Consume<Scalar>().Value;
                            break;
                        case "into":
                            options.Into = ParseScalarOrSequence(parser);
                            break;
                        case "match":
                            options.Match = parser.Consume<Scalar>().Value;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown extract key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private UpdateColumnOptions ParseUpdateColumnOptions(IParser parser)
        {
            var options = new UpdateColumnOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "column":
                            options.Column = parser.Consume<Scalar>().Value;
                            break;
                        case "value":
                            options.Value = parser.Consume<Scalar>().Value;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown updatecolumn key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private UpdateEnvironmentOptions ParseUpdateEnvironmentOptions(IParser parser)
        {
            var options = new UpdateEnvironmentOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "variable":
                        case "name":
                            options.Variable = parser.Consume<Scalar>().Value;
                            break;
                        case "value":
                            options.Value = parser.Consume<Scalar>().Value;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown updateenvironment key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private ReadfileOptions ParseReadfileOptions(IParser parser)
        {
            var options = new ReadfileOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "path":
                            options.Path = parser.Consume<Scalar>().Value;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "skip_empty_lines":
                        case "skipemptylines":
                            var skipValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                            options.SkipEmptyLines = skipValue == "true" || skipValue == "yes" || skipValue == "1";
                            break;
                        case "trim_lines":
                        case "trimlines":
                            var trimValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                            options.TrimLines = trimValue == "true" || trimValue == "yes" || trimValue == "1";
                            break;
                        case "max_lines":
                        case "maxlines":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var maxLines))
                                options.MaxLines = maxLines;
                            break;
                        case "encoding":
                            options.Encoding = parser.Consume<Scalar>().Value;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown readfile key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private WritefileOptions ParseWritefileOptions(IParser parser)
        {
            var options = new WritefileOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "path":
                            options.Path = parser.Consume<Scalar>().Value;
                            break;
                        case "content":
                            options.Content = parser.Consume<Scalar>().Value;
                            break;
                        case "mode":
                            options.Mode = parser.Consume<Scalar>().Value;
                            break;
                        case "format":
                            options.Format = parser.Consume<Scalar>().Value;
                            break;
                        case "pretty":
                            var prettyValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                            options.Pretty = prettyValue == "true" || prettyValue == "yes" || prettyValue == "1";
                            break;
                        case "headers":
                            var headersList = ParseScalarOrSequence(parser);
                            if (headersList is List<string> list)
                                options.Headers = list;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown writefile key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private InputOptions ParseInputOptions(IParser parser)
        {
            var options = new InputOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "prompt":
                            options.Prompt = parser.Consume<Scalar>().Value;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "default":
                            options.Default = parser.Consume<Scalar>().Value;
                            break;
                        case "password":
                            var pwdValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                            options.Password = pwdValue == "true" || pwdValue == "yes" || pwdValue == "1";
                            break;
                        case "validate":
                            options.Validate = parser.Consume<Scalar>().Value;
                            break;
                        case "validation_error":
                        case "validationerror":
                            options.ValidationError = parser.Consume<Scalar>().Value;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown input key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private ChooseOptions ParseChooseOptions(IParser parser)
        {
            var options = new ChooseOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "prompt":
                            options.Prompt = parser.Consume<Scalar>().Value;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "options":
                            (options.Options, options.OptionsFrom) = ParseChoiceOptions(parser);
                            break;
                        case "default":
                            options.Default = parser.Consume<Scalar>().Value;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown choose key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private MultiselectOptions ParseMultiselectOptions(IParser parser)
        {
            var options = new MultiselectOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "prompt":
                            options.Prompt = parser.Consume<Scalar>().Value;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "options":
                            (options.Options, options.OptionsFrom) = ParseChoiceOptions(parser);
                            break;
                        case "min":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var min))
                                options.Min = min;
                            break;
                        case "max":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var max))
                                options.Max = max;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown multiselect key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private ConfirmOptions ParseConfirmOptions(IParser parser)
        {
            var options = new ConfirmOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "prompt":
                            options.Prompt = parser.Consume<Scalar>().Value;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "default":
                            var defVal = parser.Consume<Scalar>().Value.ToLowerInvariant();
                            options.Default = defVal == "true" || defVal == "yes" || defVal == "1";
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown confirm key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private InteractiveOptions? ParseInteractiveOptions(IParser parser, ScriptStep step)
        {
            if (!parser.Accept<MappingStart>(out _))
            {
                SkipValue(parser);
                AddStepParseError(step, "interactive must be a mapping with optional keys 'session', 'command', 'capture', 'max_seconds', 'mirror_output', and 'on_error'");
                return null;
            }

            var options = new InteractiveOptions();
            parser.Consume<MappingStart>();

            while (!parser.Accept<MappingEnd>(out _))
            {
                var keyScalar = parser.Consume<Scalar>();
                var key = keyScalar.Value.ToLowerInvariant();

                switch (key)
                {
                    case "session":
                        if (!parser.Accept<Scalar>(out _))
                        {
                            SkipValue(parser);
                            AddStepParseError(step, "interactive.session must be 'separate' or 'shared'");
                            break;
                        }

                        var sessionValue = parser.Consume<Scalar>().Value;
                        if (string.Equals(sessionValue, "separate", StringComparison.OrdinalIgnoreCase))
                        {
                            options.Session = InteractiveSessionMode.Separate;
                        }
                        else if (string.Equals(sessionValue, "shared", StringComparison.OrdinalIgnoreCase))
                        {
                            options.Session = InteractiveSessionMode.Shared;
                        }
                        else
                        {
                            AddStepParseError(step, "interactive.session must be 'separate' or 'shared'");
                        }
                        break;

                    case "command":
                        options.Command = parser.Consume<Scalar>().Value;
                        break;

                    case "capture":
                        options.Capture = parser.Consume<Scalar>().Value;
                        break;

                    case "max_seconds":
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var maxSeconds))
                        {
                            options.MaxSeconds = maxSeconds;
                        }
                        else
                        {
                            AddStepParseError(step, "interactive.max_seconds must be a positive integer");
                        }
                        break;

                    case "mirror_output":
                        options.MirrorOutput = ParseBooleanOrDefault(parser, options.MirrorOutput);
                        break;

                    case "emulation":
                        AddUnknownKeyWarning("interactive.emulation is deprecated and ignored", (int)keyScalar.Start.Line);
                        SkipValue(parser);
                        break;

                    case "on_error":
                    case "onerror":
                        ApplyNestedOnErrorAlias(step, parser);
                        break;

                    default:
                        AddStepParseError(step, $"interactive.{keyScalar.Value} is not supported");
                        SkipValue(parser);
                        break;
                }
            }

            parser.Consume<MappingEnd>();
            return options;
        }

        /// <summary>
        /// Parses choose/multiselect options from either:
        /// - sequence syntax (inline options list), or
        /// - scalar syntax (variable/expression source for options).
        /// </summary>
        private (List<ChoiceOption> options, string? optionsFrom) ParseChoiceOptions(IParser parser)
        {
            if (parser.Accept<SequenceStart>(out _))
            {
                return (ParseChoiceOptionList(parser), null);
            }

            if (parser.Accept<Scalar>(out _))
            {
                return (new List<ChoiceOption>(), parser.Consume<Scalar>().Value);
            }

            SkipValue(parser);
            return (new List<ChoiceOption>(), null);
        }

        /// <summary>
        /// Parses a YAML sequence where each item is either a scalar string (label=value)
        /// or a mapping with "label" and "value" keys.
        /// </summary>
        private List<ChoiceOption> ParseChoiceOptionList(IParser parser)
        {
            var result = new List<ChoiceOption>();

            parser.Consume<SequenceStart>();

            while (!parser.Accept<SequenceEnd>(out _))
            {
                if (parser.Accept<Scalar>(out _))
                {
                    var val = parser.Consume<Scalar>().Value;
                    result.Add(new ChoiceOption { Label = val, Value = val });
                }
                else if (parser.Accept<MappingStart>(out _))
                {
                    parser.Consume<MappingStart>();
                    var opt = new ChoiceOption();

                    while (!parser.Accept<MappingEnd>(out _))
                    {
                        var k = parser.Consume<Scalar>().Value.ToLowerInvariant();
                        switch (k)
                        {
                            case "label":
                                opt.Label = parser.Consume<Scalar>().Value;
                                break;
                            case "value":
                                opt.Value = parser.Consume<Scalar>().Value;
                                break;
                            default:
                                SkipValue(parser);
                                break;
                        }
                    }

                    parser.Consume<MappingEnd>();

                    // Default value to label or label to value when only one is specified
                    if (string.IsNullOrEmpty(opt.Value) && !string.IsNullOrEmpty(opt.Label))
                        opt.Value = opt.Label;
                    if (string.IsNullOrEmpty(opt.Label) && !string.IsNullOrEmpty(opt.Value))
                        opt.Label = opt.Value;

                    result.Add(opt);
                }
                else
                {
                    SkipValue(parser);
                }
            }

            parser.Consume<SequenceEnd>();
            return result;
        }

        private object? ParseScalarOrSequence(IParser parser)
        {
            if (parser.Accept<Scalar>(out _))
            {
                return parser.Consume<Scalar>().Value;
            }
            else if (parser.Accept<SequenceStart>(out _))
            {
                var list = new List<string>();
                parser.Consume<SequenceStart>();

                while (!parser.Accept<SequenceEnd>(out _))
                {
                    if (parser.Accept<Scalar>(out _))
                    {
                        list.Add(parser.Consume<Scalar>().Value);
                    }
                    else
                    {
                        SkipValue(parser);
                    }
                }

                parser.Consume<SequenceEnd>();
                return list;
            }
            else
            {
                SkipValue(parser);
                return null;
            }
        }

        private object? ParseLogValue(IParser parser)
        {
            // Log can be a simple string or an options object
            if (parser.Accept<Scalar>(out _))
            {
                return parser.Consume<Scalar>().Value;
            }
            else if (parser.Accept<MappingStart>(out _))
            {
                var options = new LogOptions();
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "message":
                            options.Message = parser.Consume<Scalar>().Value;
                            break;
                        case "level":
                            options.Level = parser.Consume<Scalar>().Value;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown log key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                return options;
            }
            else
            {
                SkipValue(parser);
                return null;
            }
        }

        private HttpOptions ParseHttpOptions(IParser parser, ScriptStep step)
        {
            var options = new HttpOptions();

            if (parser.Accept<Scalar>(out _))
            {
                // Shorthand: - http: "https://example.com"
                options.Url = parser.Consume<Scalar>().Value;
                return options;
            }

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "url":
                            options.Url = parser.Consume<Scalar>().Value;
                            break;
                        case "method":
                            options.Method = NormalizeUpperLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "body":
                            options.Body = parser.Consume<Scalar>().Value;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "timeout":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var timeout))
                                options.Timeout = timeout;
                            break;
                        case "follow_redirects":
                        case "followredirects":
                            options.FollowRedirects = ParseBooleanOrDefault(parser, options.FollowRedirects);
                            break;
                        case "allow_failure":
                        case "allowfailure":
                            options.AllowFailure = ParseBooleanOrDefault(parser, options.AllowFailure);
                            break;
                        case "verify_tls":
                        case "verifytls":
                            if (!TryParseBooleanStrict(parser, out var verifyTls))
                            {
                                options.VerifyTlsTypeValid = false;
                            }
                            else
                            {
                                options.VerifyTls = verifyTls;
                            }
                            break;
                        case "auth":
                            options.Auth = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "username":
                            options.Username = parser.Consume<Scalar>().Value;
                            break;
                        case "password":
                            options.Password = parser.Consume<Scalar>().Value;
                            break;
                        case "token":
                            options.Token = parser.Consume<Scalar>().Value;
                            break;
                        case "content_type":
                        case "contenttype":
                            options.ContentType = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "headers":
                            options.Headers = ParseStringDictionary(parser);
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown http key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private PingOptions ParsePingOptions(IParser parser, ScriptStep step)
        {
            var options = new PingOptions();

            if (parser.Accept<Scalar>(out _))
            {
                // Shorthand: - ping: "host-or-ip"
                options.Host = parser.Consume<Scalar>().Value;
                return options;
            }

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "host":
                            options.Host = parser.Consume<Scalar>().Value;
                            break;
                        case "count":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var count))
                                options.Count = count;
                            break;
                        case "timeout":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var timeout))
                                options.Timeout = timeout;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown ping key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private DnsOptions ParseDnsOptions(IParser parser, ScriptStep step)
        {
            var options = new DnsOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "host":
                            options.Host = parser.Consume<Scalar>().Value;
                            break;
                        case "type":
                            options.Type = NormalizeUpperLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "timeout":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var timeout))
                                options.Timeout = timeout;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown dns key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private PortcheckOptions ParsePortcheckOptions(IParser parser, ScriptStep step)
        {
            var options = new PortcheckOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "host":
                            options.Host = parser.Consume<Scalar>().Value;
                            break;
                        case "port":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var port))
                                options.Port = port;
                            break;
                        case "timeout":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var timeout))
                                options.Timeout = timeout;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown portcheck key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private SftpOptions ParseSftpOptions(IParser parser, ScriptStep step)
        {
            var options = new SftpOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "action":
                            options.Action = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "local_path":
                        case "localpath":
                            options.LocalPath = parser.Consume<Scalar>().Value;
                            break;
                        case "remote_path":
                        case "remotepath":
                            options.RemotePath = parser.Consume<Scalar>().Value;
                            break;
                        case "host":
                            options.Host = parser.Consume<Scalar>().Value;
                            break;
                        case "port":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var port))
                                options.Port = port;
                            break;
                        case "username":
                            options.Username = parser.Consume<Scalar>().Value;
                            break;
                        case "password":
                            options.Password = parser.Consume<Scalar>().Value;
                            break;
                        case "overwrite":
                            options.Overwrite = ParseBooleanOrDefault(parser, options.Overwrite);
                            break;
                        case "timeout":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var timeout))
                                options.Timeout = timeout;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown sftp key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private WebhookOptions ParseWebhookOptions(IParser parser, ScriptStep step)
        {
            var options = new WebhookOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "url":
                            options.Url = parser.Consume<Scalar>().Value;
                            break;
                        case "method":
                            options.Method = parser.Consume<Scalar>().Value;
                            break;
                        case "body":
                            options.Body = parser.Consume<Scalar>().Value;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "timeout":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var timeout))
                                options.Timeout = timeout;
                            break;
                        case "headers":
                            options.Headers = ParseStringDictionary(parser);
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown webhook key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private ParseOptions ParseParseOptions(IParser parser)
        {
            var options = new ParseOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "format":
                            options.Format = parser.Consume<Scalar>().Value;
                            break;
                        case "from":
                            options.From = parser.Consume<Scalar>().Value;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "sections":
                            var sectionsList = ParseScalarOrSequence(parser);
                            if (sectionsList is List<string> list)
                                options.Sections = list;
                            else if (sectionsList is string singleSection)
                                options.Sections = new List<string> { singleSection };
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown parse key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private static void ApplyNestedOnErrorAlias(ScriptStep step, IParser parser)
        {
            var nestedOnError = parser.Consume<Scalar>().Value;
            if (string.IsNullOrWhiteSpace(step.OnError))
            {
                step.OnError = nestedOnError;
            }
        }

        private Dictionary<string, string> ParseStringDictionary(IParser parser)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var key = parser.Consume<Scalar>().Value;
                    var value = parser.Consume<Scalar>().Value;
                    dict[key] = value;
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
            }

            return dict;
        }

        private bool ParseBooleanOrDefault(IParser parser, bool defaultValue)
        {
            if (TryParseBooleanStrict(parser, out var value))
                return value;

            return defaultValue;
        }

        private bool TryParseBooleanStrict(IParser parser, out bool value)
        {
            value = false;

            if (!parser.Accept<Scalar>(out _))
            {
                SkipValue(parser);
                return false;
            }

            var scalarValue = parser.Consume<Scalar>().Value;
            if (TryParseBooleanToken(scalarValue, out value))
                return true;

            return false;
        }

        private static bool TryParseBooleanToken(string? raw, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "1":
                    value = true;
                    return true;
                case "false":
                case "no":
                case "0":
                    value = false;
                    return true;
                default:
                    return false;
            }
        }

        private static string NormalizeUpperLiteralEnum(string value)
        {
            if (ContainsVariableToken(value))
                return value;

            return value.ToUpperInvariant();
        }

        private static string NormalizeLowerLiteralEnum(string value)
        {
            if (ContainsVariableToken(value))
                return value;

            return value.ToLowerInvariant();
        }

        private void SkipValue(IParser parser)
        {
            var depth = 0;

            do
            {
                if (parser.Accept<MappingStart>(out _) || parser.Accept<SequenceStart>(out _))
                {
                    depth++;
                    parser.MoveNext();
                }
                else if (parser.Accept<MappingEnd>(out _) || parser.Accept<SequenceEnd>(out _))
                {
                    depth--;
                    parser.MoveNext();
                }
                else
                {
                    parser.MoveNext();
                }
            } while (depth > 0);
        }

        /// <summary>
        /// Validates a script and returns any errors found.
        /// </summary>
        /// <param name="script">The parsed script to validate.</param>
        /// <param name="originalYaml">Optional original YAML text for including line content in errors.</param>
        /// <param name="enforceCanonicalSyntax">Whether to enforce strict command-map requirements and command-map placement rules.</param>
        public List<string> Validate(Script script, string? originalYaml = null, bool enforceCanonicalSyntax = false)
        {
            var errors = new List<string>();
            var lines = originalYaml?.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            if (script.Steps == null || script.Steps.Count == 0)
            {
                errors.Add("Script has no steps defined");
            }
            else
            {
                ValidateSteps(script.Steps, errors, "", lines, 0, enforceCanonicalSyntax);
            }

            return errors;
        }

        private void ValidateSteps(
            List<ScriptStep> steps,
            List<string> errors,
            string prefix,
            string[]? lines,
            int loopDepth,
            bool enforceCanonicalSyntax)
        {
            foreach (var step in steps)
            {
                var stepType = step.GetStepType();

                if ((enforceCanonicalSyntax || stepType == StepType.Interactive) && step.ParseErrors.Count > 0)
                {
                    var lineContent = GetLineContent(lines, step.LineNumber);
                    foreach (var parseError in step.ParseErrors)
                    {
                        errors.Add($"{prefix}Line {step.LineNumber}: {parseError}{lineContent}");
                    }
                }

                if (stepType == StepType.Unknown)
                {
                    if (!enforceCanonicalSyntax || step.ParseErrors.Count == 0)
                    {
                        var lineContent = GetLineContent(lines, step.LineNumber);
                        errors.Add($"{prefix}Line {step.LineNumber}: Step has no recognized command{lineContent}");
                    }
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(step.OnError) && !IsValidOnErrorValue(step.OnError))
                {
                    var lineContent = GetLineContent(lines, step.LineNumber);
                    errors.Add($"{prefix}Line {step.LineNumber}: on_error must be 'continue' or 'stop'{lineContent}");
                }

                if (enforceCanonicalSyntax &&
                    step.UsesStepRootOnError &&
                    !string.IsNullOrWhiteSpace(step.OnError) &&
                    CommandMapOnErrorStepTypes.Contains(stepType))
                {
                    var lineContent = GetLineContent(lines, step.LineNumber);
                    var commandName = stepType.ToString().ToLowerInvariant();
                    errors.Add(
                        $"{prefix}Line {step.LineNumber}: step-level on_error is not supported for '{commandName}'; use '{commandName}.on_error' inside the command map{lineContent}");
                }

                // Validate specific step types
                switch (stepType)
                {
                    case StepType.Extract:
                        if (step.Extract == null || string.IsNullOrEmpty(step.Extract.From))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Extract requires 'from' variable{lineContent}");
                        }
                        if (step.Extract == null || string.IsNullOrEmpty(step.Extract.Pattern))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Extract requires 'pattern'{lineContent}");
                        }
                        if (step.Extract?.Into == null)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Extract requires 'into' variable{lineContent}");
                        }
                        break;

                    case StepType.If:
                        if (step.Then == null || step.Then.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: If requires 'then' block{lineContent}");
                        }
                        if (step.Then != null)
                            ValidateSteps(step.Then, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax);
                        if (step.Elif != null)
                        {
                            foreach (var branch in step.Elif)
                            {
                                if (string.IsNullOrWhiteSpace(branch.If))
                                {
                                    var lineContent = GetLineContent(lines, branch.LineNumber);
                                    errors.Add($"{prefix}Line {branch.LineNumber}: Elif requires 'condition'{lineContent}");
                                }
                                if (branch.Then == null || branch.Then.Count == 0)
                                {
                                    var lineContent = GetLineContent(lines, branch.LineNumber);
                                    errors.Add($"{prefix}Line {branch.LineNumber}: Elif requires 'then' block{lineContent}");
                                }
                                else
                                {
                                    ValidateSteps(branch.Then, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax);
                                }
                            }
                        }
                        if (step.Else != null)
                            ValidateSteps(step.Else, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax);
                        break;

                    case StepType.Foreach:
                        if (step.Do == null || step.Do.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Foreach requires 'do' block{lineContent}");
                        }
                        if (step.Do != null)
                            ValidateSteps(step.Do, errors, prefix + "  ", lines, loopDepth + 1, enforceCanonicalSyntax);
                        break;

                    case StepType.While:
                        if (step.Do == null || step.Do.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: While requires 'do' block{lineContent}");
                        }
                        if (step.MaxIterations.HasValue && step.MaxIterations.Value <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: max_iterations must be greater than 0{lineContent}");
                        }
                        if (step.Do != null)
                            ValidateSteps(step.Do, errors, prefix + "  ", lines, loopDepth + 1, enforceCanonicalSyntax);
                        break;

                    case StepType.Try:
                        if (step.Try == null || step.Try.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Try requires 'do' block{lineContent}");
                        }
                        if (step.Try != null)
                            ValidateSteps(step.Try, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax);
                        if (step.Catch != null)
                            ValidateSteps(step.Catch, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax);
                        if (step.Finally != null)
                            ValidateSteps(step.Finally, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax);
                        break;

                    case StepType.Break:
                        if (loopDepth <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: break can only be used inside foreach/while blocks{lineContent}");
                        }
                        break;

                    case StepType.Continue:
                        if (loopDepth <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: continue can only be used inside foreach/while blocks{lineContent}");
                        }
                        break;

                    case StepType.Set:
                        if (string.IsNullOrEmpty(step.Set) || !step.Set.Contains('='))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Set requires 'variable = value' format{lineContent}");
                        }
                        break;

                    case StepType.UpdateColumn:
                        if (step.UpdateColumn != null)
                        {
                            if (string.IsNullOrEmpty(step.UpdateColumn.Column))
                            {
                                var lineContent = GetLineContent(lines, step.LineNumber);
                                errors.Add($"{prefix}Line {step.LineNumber}: UpdateColumn requires 'column' name{lineContent}");
                            }
                            if (step.UpdateColumn.Value == null)
                            {
                                var lineContent = GetLineContent(lines, step.LineNumber);
                                errors.Add($"{prefix}Line {step.LineNumber}: UpdateColumn requires 'value'{lineContent}");
                            }
                        }
                        break;

                    case StepType.UpdateEnvironment:
                        if (step.UpdateEnvironment != null)
                        {
                            if (string.IsNullOrWhiteSpace(step.UpdateEnvironment.Variable))
                            {
                                var lineContent = GetLineContent(lines, step.LineNumber);
                                errors.Add($"{prefix}Line {step.LineNumber}: UpdateEnvironment requires 'variable' name{lineContent}");
                            }
                            if (step.UpdateEnvironment.Value == null)
                            {
                                var lineContent = GetLineContent(lines, step.LineNumber);
                                errors.Add($"{prefix}Line {step.LineNumber}: UpdateEnvironment requires 'value'{lineContent}");
                            }
                        }
                        break;

                    case StepType.Readfile:
                        if (step.Readfile == null || string.IsNullOrEmpty(step.Readfile.Path))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Readfile requires 'path'{lineContent}");
                        }
                        if (step.Readfile == null || string.IsNullOrEmpty(step.Readfile.Into))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Readfile requires 'into' variable{lineContent}");
                        }
                        break;

                    case StepType.Writefile:
                        if (step.Writefile == null || string.IsNullOrEmpty(step.Writefile.Path))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Writefile requires 'path'{lineContent}");
                        }
                        break;

                    case StepType.Input:
                        if (step.Input == null || string.IsNullOrEmpty(step.Input.Into))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Input requires 'into' variable{lineContent}");
                        }
                        break;

                    case StepType.Http:
                        if (step.Http == null || string.IsNullOrWhiteSpace(step.Http.Url))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Http requires 'url'{lineContent}");
                        }

                        if (step.Http != null && !IsDynamicValue(step.Http.Method) && !IsValidHttpMethod(step.Http.Method))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Http 'method' must be one of GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS{lineContent}");
                        }

                        if (step.Http != null && !IsDynamicValue(step.Http.Auth) && !IsValidHttpAuth(step.Http.Auth))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Http 'auth' must be one of none, basic, bearer{lineContent}");
                        }

                        if (step.Http != null && !IsDynamicValue(step.Http.ContentType) && !IsValidHttpContentType(step.Http.ContentType))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Http 'content_type' must be one of json, form, text, xml{lineContent}");
                        }

                        if (step.Http != null && !step.Http.VerifyTlsTypeValid)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Http 'verify_tls' must be a boolean value{lineContent}");
                        }

                        if (step.Http != null && !IsDynamicValue(step.Http.Auth))
                        {
                            if (string.Equals(step.Http.Auth, "basic", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrWhiteSpace(step.Http.Username))
                                {
                                    var lineContent = GetLineContent(lines, step.LineNumber);
                                    errors.Add($"{prefix}Line {step.LineNumber}: Http 'auth: basic' requires 'username'{lineContent}");
                                }

                                if (string.IsNullOrWhiteSpace(step.Http.Password))
                                {
                                    var lineContent = GetLineContent(lines, step.LineNumber);
                                    errors.Add($"{prefix}Line {step.LineNumber}: Http 'auth: basic' requires 'password'{lineContent}");
                                }
                            }
                            else if (string.Equals(step.Http.Auth, "bearer", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrWhiteSpace(step.Http.Token))
                                {
                                    var lineContent = GetLineContent(lines, step.LineNumber);
                                    errors.Add($"{prefix}Line {step.LineNumber}: Http 'auth: bearer' requires 'token'{lineContent}");
                                }
                            }
                        }
                        break;

                    case StepType.Ping:
                        if (step.Ping == null || string.IsNullOrWhiteSpace(step.Ping.Host))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Ping requires 'host'{lineContent}");
                        }
                        break;

                    case StepType.Dns:
                        if (step.Dns == null || string.IsNullOrWhiteSpace(step.Dns.Host))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Dns requires 'host'{lineContent}");
                        }

                        if (step.Dns != null && !IsDynamicValue(step.Dns.Type) && !IsValidDnsType(step.Dns.Type))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Dns 'type' must be one of A, AAAA, PTR{lineContent}");
                        }
                        break;

                    case StepType.Portcheck:
                        if (step.Portcheck == null || string.IsNullOrWhiteSpace(step.Portcheck.Host))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Portcheck requires 'host'{lineContent}");
                        }
                        break;

                    case StepType.Sftp:
                        if (step.Sftp == null || string.IsNullOrWhiteSpace(step.Sftp.Action))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Sftp requires 'action'{lineContent}");
                        }
                        else if (!IsDynamicValue(step.Sftp.Action) && !IsValidSftpAction(step.Sftp.Action))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Sftp 'action' must be 'upload' or 'download'{lineContent}");
                        }

                        if (step.Sftp == null || string.IsNullOrWhiteSpace(step.Sftp.LocalPath))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Sftp requires 'local_path'{lineContent}");
                        }

                        if (step.Sftp == null || string.IsNullOrWhiteSpace(step.Sftp.RemotePath))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Sftp requires 'remote_path'{lineContent}");
                        }
                        break;

                    case StepType.Parse:
                        if (step.Parse == null || string.IsNullOrEmpty(step.Parse.Format))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Parse requires 'format' (e.g., 'fortigate'){lineContent}");
                        }
                        if (step.Parse == null || string.IsNullOrEmpty(step.Parse.From))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Parse requires 'from' variable{lineContent}");
                        }
                        if (step.Parse == null || string.IsNullOrEmpty(step.Parse.Into))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Parse requires 'into' variable{lineContent}");
                        }
                        break;

                    case StepType.Interactive:
                        if (step.Interactive == null)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive must be a mapping with optional keys 'session', 'command', 'capture', 'max_seconds', 'mirror_output', and 'on_error'{lineContent}");
                            break;
                        }

                        if (!string.IsNullOrWhiteSpace(step.Interactive.Command) &&
                            step.Interactive.Session != InteractiveSessionMode.Separate)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive.session must be 'separate' when interactive.command is set{lineContent}");
                        }

                        if (step.Interactive.MaxSeconds.HasValue &&
                            step.Interactive.MaxSeconds.Value <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive.max_seconds must be greater than 0{lineContent}");
                        }
                        break;
                }
            }
        }

        private static bool IsValidOnErrorValue(string value)
        {
            return string.Equals(value, "continue", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "stop", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDynamicValue(string? value)
        {
            return ContainsVariableToken(value);
        }

        private static bool IsValidHttpMethod(string method)
        {
            return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidHttpAuth(string auth)
        {
            return string.Equals(auth, "none", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(auth, "basic", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(auth, "bearer", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidHttpContentType(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return true;

            return string.Equals(contentType, "json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "form", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "text", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "xml", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidDnsType(string dnsType)
        {
            return string.Equals(dnsType, "A", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(dnsType, "AAAA", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(dnsType, "PTR", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidSftpAction(string action)
        {
            return string.Equals(action, "upload", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, "download", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsVariableToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Contains("${", StringComparison.Ordinal) ||
                   value.Contains("{{", StringComparison.Ordinal);
        }

        private static string GetLineContent(string[]? lines, int lineNumber)
        {
            if (lines == null || lineNumber < 1 || lineNumber > lines.Length)
                return "";

            var content = lines[lineNumber - 1].Trim();
            if (string.IsNullOrEmpty(content))
                return "";

            return $"\n  > {content}";
        }

        private void AddUnknownKeyWarning(string message, int lineNumber)
        {
            _warnings.Add($"Line {lineNumber}: {message}");
        }
    }

    /// <summary>
    /// Exception thrown when script parsing fails.
    /// </summary>
    public class ScriptParseException : Exception
    {
        public ScriptParseException(string message) : base(message) { }
        public ScriptParseException(string message, Exception inner) : base(message, inner) { }
    }
}
