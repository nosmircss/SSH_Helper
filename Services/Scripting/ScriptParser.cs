using System;
using System.Collections.Generic;
using System.IO;
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
            "readfile",
            "writefile",
            "input",
            "log",
            "webhook",
            "parse",
            "break",
            "continue",
            "try"
        };

        /// <summary>
        /// Parser warnings captured during the most recent parse operation.
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings;

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
                SkipValue(parser);
                return null;
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
                        step.Send = parser.Consume<Scalar>().Value;
                        break;
                    case "print":
                        step.Print = parser.Consume<Scalar>().Value;
                        break;
                    case "wait":
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var wait))
                            step.Wait = wait;
                        break;
                    case "set":
                        step.Set = parser.Consume<Scalar>().Value;
                        break;
                    case "exit":
                        step.Exit = parser.Consume<Scalar>().Value;
                        break;
                    case "if":
                        step.If = parser.Consume<Scalar>().Value;
                        break;
                    case "foreach":
                        step.Foreach = parser.Consume<Scalar>().Value;
                        break;
                    case "while":
                        step.While = parser.Consume<Scalar>().Value;
                        break;
                    case "break":
                        step.BreakLoop = ParseBooleanish(parser);
                        break;
                    case "continue":
                        step.ContinueLoop = ParseBooleanish(parser);
                        break;
                    case "try":
                        step.Try = ParseSteps(parser);
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
                        step.Extract = ParseExtractOptions(parser);
                        break;
                    case "readfile":
                        step.Readfile = ParseReadfileOptions(parser);
                        break;
                    case "writefile":
                        step.Writefile = ParseWritefileOptions(parser);
                        break;
                    case "input":
                        step.Input = ParseInputOptions(parser);
                        break;
                    case "updatecolumn":
                        step.UpdateColumn = ParseUpdateColumnOptions(parser);
                        break;
                    case "log":
                        step.Log = ParseLogValue(parser);
                        break;
                    case "webhook":
                        step.Webhook = ParseWebhookOptions(parser);
                        break;
                    case "parse":
                        step.Parse = ParseParseOptions(parser);
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

        private WebhookOptions ParseWebhookOptions(IParser parser)
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
        public List<string> Validate(Script script, string? originalYaml = null)
        {
            var errors = new List<string>();
            var lines = originalYaml?.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            if (script.Steps == null || script.Steps.Count == 0)
            {
                errors.Add("Script has no steps defined");
            }
            else
            {
                ValidateSteps(script.Steps, errors, "", lines, 0);
            }

            return errors;
        }

        private void ValidateSteps(List<ScriptStep> steps, List<string> errors, string prefix, string[]? lines, int loopDepth)
        {
            foreach (var step in steps)
            {
                var stepType = step.GetStepType();

                if (stepType == StepType.Unknown)
                {
                    var lineContent = GetLineContent(lines, step.LineNumber);
                    errors.Add($"{prefix}Line {step.LineNumber}: Step has no recognized command{lineContent}");
                    continue;
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
                            ValidateSteps(step.Then, errors, prefix + "  ", lines, loopDepth);
                        if (step.Elif != null)
                        {
                            foreach (var branch in step.Elif)
                            {
                                if (string.IsNullOrWhiteSpace(branch.If))
                                {
                                    var lineContent = GetLineContent(lines, branch.LineNumber);
                                    errors.Add($"{prefix}Line {branch.LineNumber}: Elif requires 'if' condition{lineContent}");
                                }
                                if (branch.Then == null || branch.Then.Count == 0)
                                {
                                    var lineContent = GetLineContent(lines, branch.LineNumber);
                                    errors.Add($"{prefix}Line {branch.LineNumber}: Elif requires 'then' block{lineContent}");
                                }
                                else
                                {
                                    ValidateSteps(branch.Then, errors, prefix + "  ", lines, loopDepth);
                                }
                            }
                        }
                        if (step.Else != null)
                            ValidateSteps(step.Else, errors, prefix + "  ", lines, loopDepth);
                        break;

                    case StepType.Foreach:
                        if (step.Do == null || step.Do.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Foreach requires 'do' block{lineContent}");
                        }
                        if (step.Do != null)
                            ValidateSteps(step.Do, errors, prefix + "  ", lines, loopDepth + 1);
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
                            ValidateSteps(step.Do, errors, prefix + "  ", lines, loopDepth + 1);
                        break;

                    case StepType.Try:
                        if (step.Try == null || step.Try.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Try requires 'try' block{lineContent}");
                        }
                        if (step.Try != null)
                            ValidateSteps(step.Try, errors, prefix + "  ", lines, loopDepth);
                        if (step.Catch != null)
                            ValidateSteps(step.Catch, errors, prefix + "  ", lines, loopDepth);
                        if (step.Finally != null)
                            ValidateSteps(step.Finally, errors, prefix + "  ", lines, loopDepth);
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
                }
            }
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
