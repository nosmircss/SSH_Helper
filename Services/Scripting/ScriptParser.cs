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

            var trimmed = text.TrimStart();

            // Check for YAML document marker
            if (trimmed.StartsWith("---"))
                return true;

            // Check for common script keywords at start of lines
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Take(10)) // Check first 10 lines
            {
                var trimmedLine = line.TrimStart();
                if (trimmedLine.StartsWith("#")) continue; // Skip comments

                if (trimmedLine.StartsWith("name:") ||
                    trimmedLine.StartsWith("description:") ||
                    trimmedLine.StartsWith("vars:") ||
                    trimmedLine.StartsWith("steps:") ||
                    trimmedLine.StartsWith("version:"))
                {
                    return true;
                }

                // Check for step syntax: "- send:", "- print:", etc.
                if (trimmedLine.StartsWith("- send:") ||
                    trimmedLine.StartsWith("- print:") ||
                    trimmedLine.StartsWith("- wait:") ||
                    trimmedLine.StartsWith("- set:") ||
                    trimmedLine.StartsWith("- exit:") ||
                    trimmedLine.StartsWith("- extract:") ||
                    trimmedLine.StartsWith("- if:") ||
                    trimmedLine.StartsWith("- foreach:") ||
                    trimmedLine.StartsWith("- while:"))
                {
                    return true;
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
                            case "vars":
                                script.Vars = ParseVars(parser);
                                break;
                            case "steps":
                                script.Steps = ParseSteps(parser);
                                break;
                            default:
                                // Skip unknown properties
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
                    case "extract":
                        step.Extract = ParseExtractOptions(parser);
                        break;
                    case "then":
                        step.Then = ParseSteps(parser);
                        break;
                    case "else":
                        step.Else = ParseSteps(parser);
                        break;
                    case "do":
                        step.Do = ParseSteps(parser);
                        break;
                    default:
                        // Skip unknown properties
                        SkipValue(parser);
                        break;
                }
            }

            parser.Consume<MappingEnd>();
            return step;
        }

        private ExtractOptions ParseExtractOptions(IParser parser)
        {
            var options = new ExtractOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var key = parser.Consume<Scalar>().Value.ToLowerInvariant();

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
                ValidateSteps(script.Steps, errors, "", lines);
            }

            return errors;
        }

        private void ValidateSteps(List<ScriptStep> steps, List<string> errors, string prefix, string[]? lines)
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
                            ValidateSteps(step.Then, errors, prefix + "  ", lines);
                        if (step.Else != null)
                            ValidateSteps(step.Else, errors, prefix + "  ", lines);
                        break;

                    case StepType.Foreach:
                        if (step.Do == null || step.Do.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Foreach requires 'do' block{lineContent}");
                        }
                        if (step.Do != null)
                            ValidateSteps(step.Do, errors, prefix + "  ", lines);
                        break;

                    case StepType.While:
                        if (step.Do == null || step.Do.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: While requires 'do' block{lineContent}");
                        }
                        if (step.Do != null)
                            ValidateSteps(step.Do, errors, prefix + "  ", lines);
                        break;

                    case StepType.Set:
                        if (string.IsNullOrEmpty(step.Set) || !step.Set.Contains('='))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Set requires 'variable = value' format{lineContent}");
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
