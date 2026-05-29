using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            "repeat",
            "updatecolumn",
            "updateenvironment",
            "readfile",
            "writefile",
            "exists",
            "playsound",
            "input",
            "log",
            "http",
            "browser_callback_capture",
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
            "try",
            "assert",
            "switch",
            "parallel",
            "call",
            "return",
            "table",
            "localcmd",
            "vault",
            "sethistorylabel",
            "notify"
        };
        private static readonly string[] KnownTopLevelKeys =
        {
            "name",
            "description",
            "version",
            "environment",
            "debug",
            "nobanner",
            "compact_errors",
            "suppress_missing_column_warning",
            "library",
            "preconnect",
            "vars",
            "imports",
            "subroutines",
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
            "finally",
            "retry",
            "retry_delay",
            "cases"
        };
        private static readonly IReadOnlyDictionary<string, string[]> CommandOptionKeys =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["send"] = ["command", "capture", "suppress", "expect", "timeout", "on_error", "retry", "retry_delay", "fail_on_nonzero", "respond"],
                ["print"] = ["message"],
                ["wait"] = ["seconds"],
                ["set"] = ["expression"],
                ["exit"] = ["status", "message"],
                ["extract"] = ["from", "pattern", "into", "match", "required"],
                ["if"] = ["condition", "then", "elif", "else"],
                ["foreach"] = ["iterator", "when", "do"],
                ["while"] = ["condition", "max_iterations", "do"],
                ["repeat"] = ["until", "max_iterations", "do"],
                ["try"] = ["do", "catch", "finally"],
                ["readfile"] = ["path", "select_file", "message", "fileext", "autobrowse", "path_into", "path_only", "into", "skip_empty_lines", "trim_lines", "max_lines", "encoding", "on_error"],
                ["writefile"] = ["path", "content", "mode", "format", "pretty", "headers", "on_error"],
                ["exists"] = ["path", "into", "type", "on_error"],
                ["playsound"] = ["path", "wait", "volume", "max_seconds", "into", "on_error"],
                ["input"] = ["title", "prompt", "into", "default", "password", "validate", "validation_error", "font_size", "on_error"],
                ["updatecolumn"] = ["column", "value"],
                ["updateenvironment"] = ["variable", "value"],
                ["log"] = ["message", "level"],
                ["http"] = ["url", "method", "body", "headers", "into", "timeout", "follow_redirects", "allow_failure", "verify_tls", "auth", "username", "password", "token", "content_type", "on_error"],
                ["browser_callback_capture"] = ["start_url", "callback_path", "local_port", "capture_mode", "browser_mode", "show_after_seconds", "into", "required_fields", "timeout", "open_browser", "auto_close_browser", "completion_message", "failure_message", "quiet", "on_error"],
                ["ping"] = ["host", "count", "timeout", "into", "on_error"],
                ["dns"] = ["host", "type", "timeout", "into", "on_error"],
                ["portcheck"] = ["host", "port", "timeout", "into", "on_error"],
                ["sftp"] = ["action", "local_path", "remote_path", "host", "port", "username", "password", "overwrite", "timeout", "into", "on_error"],
                ["webhook"] = ["url", "method", "body", "headers", "into", "timeout", "on_error"],
                ["parse"] = ["format", "from", "into", "sections"],
                ["choose"] = ["title", "prompt", "into", "options", "default", "font_size", "on_error"],
                ["multiselect"] = ["title", "prompt", "into", "options", "min", "max", "font_size", "on_error"],
                ["confirm"] = ["title", "prompt", "into", "default", "font_size", "on_error"],
                ["interactive"] = ["session", "title", "command", "capture", "max_seconds", "max_lines", "width", "height", "mirror_output", "show_window", "on_error"],
                ["assert"] = ["condition", "message", "severity"],
                ["switch"] = ["value", "cases", "default"],
                ["parallel"] = ["steps", "max_concurrent"],
                ["call"] = ["subroutine", "args", "out", "on_error"],
                ["table"] = ["data", "columns", "into", "align", "show_header"],
                ["localcmd"] = ["command", "shell", "shell_path", "args", "env", "working_dir", "interactive", "keep_open", "run_mode", "lifetime", "kill_on_cancel", "fail_on_nonzero", "success_codes", "max_output_bytes", "confirm", "quiet", "suppress", "title", "into", "timeout", "on_error"],
                ["vault"] = ["path", "key", "keys", "into", "write", "patch", "profile", "version", "on_error"],
                ["sethistorylabel"] = ["value", "replace", "mode", "separator"],
                ["notify"] = ["profile", "channel", "title", "message", "level", "mention", "attachments", "into", "on_error"]
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
                ["repeat"] = [],
                ["updatecolumn"] = [],
                ["updateenvironment"] = [],
                ["readfile"] = [],
                ["writefile"] = [],
                ["exists"] = [],
                ["playsound"] = [],
                ["input"] = [],
                ["log"] = [],
                ["http"] = [],
                ["browser_callback_capture"] = [],
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
                ["try"] = [],
                ["assert"] = [],
                ["switch"] = ["cases", "else"],
                ["parallel"] = [],
                ["call"] = [],
                ["return"] = [],
                ["table"] = [],
                ["localcmd"] = [],
                ["vault"] = [],
                ["sethistorylabel"] = [],
                ["notify"] = []
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
            "repeat",
            "try",
            "call",
            "switch",
            "assert"
        };
        private static readonly HashSet<StepType> CommandMapOnErrorStepTypes =
        [
            StepType.Send,
            StepType.Readfile,
            StepType.Writefile,
            StepType.Exists,
            StepType.PlaySound,
            StepType.Input,
            StepType.Http,
            StepType.BrowserCallbackCapture,
            StepType.Ping,
            StepType.Dns,
            StepType.Portcheck,
            StepType.Sftp,
            StepType.Webhook,
            StepType.Choose,
            StepType.Multiselect,
            StepType.Confirm,
            StepType.Interactive,
            StepType.Call,
            StepType.Assert,
            StepType.Notify
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
                ["action"] = ["upload", "download"],
                ["mode"] = ["overwrite", "append"],
                ["format"] = ["text", "json", "jsonl", "csv"],
                ["level"] = ["info", "debug", "warning", "error", "success"],
                ["encoding"] = ["utf-8", "ascii", "utf-16", "utf-32"],
                ["required"] = ["true", "false"],
                ["select_file"] = ["true", "false"],
                ["skip_empty_lines"] = ["true", "false"],
                ["trim_lines"] = ["true", "false"],
                ["pretty"] = ["true", "false"],
                ["fail_on_nonzero"] = ["true", "false"],
                ["suppress"] = ["true", "false"],
                ["overwrite"] = ["true", "false"],
                ["follow_redirects"] = ["true", "false"],
                ["allow_failure"] = ["true", "false"],
                ["verify_tls"] = ["true", "false"],
                ["capture_mode"] = ["auto", "fragment", "query", "post_body"],
                ["browser_mode"] = ["external", "webview2"],
                ["open_browser"] = ["true", "false"],
                ["auto_close_browser"] = ["true", "false"],
                ["quiet"] = ["true", "false"],
                ["session"] = ["separate", "shared"],
                ["mirror_output"] = ["true", "false"],
                ["show_window"] = ["true", "false"],
                ["wait"] = ["true", "false"],
                ["debug"] = ["true", "false"],
                ["nobanner"] = ["true", "false"],
                ["compact_errors"] = ["true", "false"],
                ["suppress_missing_column_warning"] = ["true", "false"],
                ["library"] = ["true", "false"],
                ["severity"] = ["error", "warning"],
                ["align"] = ["left", "right", "center"],
                ["show_header"] = ["true", "false"]
            };

        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string[]>> EnumLikeOptionValuesByCommand =
            new Dictionary<string, IReadOnlyDictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase)
            {
                ["dns"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["type"] = ["A", "AAAA", "PTR"]
                },
                ["exists"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["type"] = ["any", "file", "directory"]
                },
                ["input"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["password"] = ["true", "false"]
                },
                ["confirm"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["default"] = ["true", "false"]
                },
                ["localcmd"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["shell"] = ["powershell", "custom"],
                    ["interactive"] = ["true", "false"],
                    ["keep_open"] = ["true", "false"],
                    ["run_mode"] = ["foreground", "background"],
                    ["lifetime"] = ["detached", "script", "app"],
                    ["kill_on_cancel"] = ["true", "false"],
                    ["confirm"] = ["always", "once", "never"]
                },
                ["sethistorylabel"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mode"] = HistoryLabelOperation.KnownModes
                },
                ["notify"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["channel"] = ["slack", "teams", "discord", "toast", "smtp"],
                    ["level"] = ["info", "warn", "error", "success"]
                }
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

        public static IReadOnlyDictionary<string, IReadOnlyList<string>> GetDeclaredStepOptionKeysByCommand()
        {
            return CommandOptionKeys.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value
                    .Distinct(StringComparer.OrdinalIgnoreCase)
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

        public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> GetEnumLikeOptionValuesByCommand()
        {
            return EnumLikeOptionValuesByCommand.ToDictionary(
                commandPair => commandPair.Key,
                commandPair => (IReadOnlyDictionary<string, IReadOnlyList<string>>)commandPair.Value.ToDictionary(
                    keyPair => keyPair.Key,
                    keyPair => (IReadOnlyList<string>)keyPair.Value,
                    StringComparer.OrdinalIgnoreCase),
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
                    trimmedLine.StartsWith("preconnect:", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("vars:", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("imports:", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("subroutines:", StringComparison.OrdinalIgnoreCase))
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

                yamlText = PreprocessYaml(yamlText);

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
                        if (!script.DeclaredTopLevelKeys.Add(keyName))
                        {
                            AddScriptParseError(script, $"Line {(int)key.Start.Line}: Duplicate top-level key '{key.Value}'");
                        }

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
                            case "environment":
                                script.Environment = parser.Consume<Scalar>().Value;
                                break;
                            case "debug":
                                script.Debug = ParseBooleanOrDefault(parser, script.Debug);
                                break;
                            case "nobanner":
                                script.NoBanner = ParseBooleanOrDefault(parser, script.NoBanner);
                                break;
                            case "compact_errors":
                                script.CompactErrors = ParseBooleanOrDefault(parser, script.CompactErrors);
                                break;
                            case "suppress_missing_column_warning":
                                script.SuppressMissingColumnWarning = ParseBooleanOrDefault(parser, script.SuppressMissingColumnWarning);
                                break;
                            case "library":
                                script.Library = ParseBooleanOrDefault(parser, script.Library);
                                break;
                            case "preconnect":
                                if (parser.Accept<SequenceStart>(out _))
                                {
                                    script.Preconnect = ParseSteps(parser);
                                }
                                else
                                {
                                    AddScriptParseError(script, "preconnect must be a sequence of steps");
                                    SkipValue(parser);
                                }
                                break;
                            case "vars":
                                script.Vars = ParseVars(parser);
                                break;
                            case "imports":
                                script.Imports = ParseImports(parser, script);
                                break;
                            case "subroutines":
                                script.Subroutines = ParseSubroutines(parser, script);
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

        // Regex matches lines like "  - set: value" or "    condition: value" where value is unquoted.
        // Group 1 = leading whitespace + optional "- ", Group 2 = key name, Group 3 = the value.
        private static readonly Regex PlainScalarLineRegex = new(
            @"^(\s*(?:-\s+)?)(\w+):\s+(.+)$",
            RegexOptions.Compiled);

        // Keys whose plain scalar values commonly contain colons (ternary, time formats, etc.)
        private static readonly HashSet<string> ScalarValueKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            // Step keys that accept inline expression/string scalars
            "set", "print", "send", "exit", "if", "while", "repeat", "until", "when", "assert",
            "call", "return", "foreach", "sethistorylabel",
            // Expanded-form sub-keys that accept expression scalars
            "expression", "condition", "message", "command", "expect",
            "format", "value", "source", "pattern", "url", "body", "path"
        };

        /// <summary>
        /// Pre-processes YAML text to auto-quote plain scalar values that contain
        /// unquoted colons (e.g. ternary operators like <c>x ? "a" : "b"</c>),
        /// which would otherwise cause YAML mapping parse errors.
        /// </summary>
        internal static string PreprocessYaml(string yamlText)
        {
            var lines = yamlText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var modified = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Skip blank lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                var match = PlainScalarLineRegex.Match(line);
                if (!match.Success)
                    continue;

                var key = match.Groups[2].Value;
                var value = match.Groups[3].Value;

                // Only process known scalar-value keys
                if (!ScalarValueKeys.Contains(key))
                    continue;

                // Value starts a block scalar (| or >) — leave it alone
                if (value.StartsWith("|") || value.StartsWith(">"))
                    continue;

                // Check if the value would confuse the YAML parser
                if (!NeedsYamlQuoting(value))
                    continue;

                // Wrap in single quotes, escaping any existing single quotes by doubling them
                var escaped = value.Replace("'", "''");
                lines[i] = $"{match.Groups[1].Value}{key}: '{escaped}'";
                modified = true;
            }

            return modified ? string.Join("\n", lines) : yamlText;
        }

        /// <summary>
        /// Determines whether a plain YAML scalar value needs to be wrapped in quotes
        /// to prevent YAML parsing ambiguity. Detects:
        /// <list type="bullet">
        ///   <item>Colons (<c>x ? "a" : "b"</c>, <c>"Error 404: Not Found"</c>) — YAML sees a nested mapping.
        ///         In YAML plain scalars, quotes are NOT special, so <c>": "</c> inside DSL-level
        ///         "quoted strings" still breaks YAML.</item>
        ///   <item>Embedded quoted substrings with trailing text (<c>"alice" in names</c>) — YAML
        ///         parses only the first quoted scalar and chokes on the rest</item>
        ///   <item><c>#</c> preceded by space — YAML treats it as a comment</item>
        /// </list>
        /// </summary>
        private static bool NeedsYamlQuoting(string value)
        {
            // If the entire value is a single quoted string, YAML handles it fine
            if (value.Length >= 2)
            {
                if ((value[0] == '"' && value[^1] == '"') ||
                    (value[0] == '\'' && value[^1] == '\''))
                {
                    // But verify there's no premature close — e.g. "alice" in names
                    // has a closing quote at position 6 but the value continues
                    if (!HasTrailingContentAfterQuotedString(value))
                        return false;
                }
            }

            // Value starts with a quote but isn't fully quoted — YAML will misparse
            if (value[0] == '"' || value[0] == '\'')
                return true;

            // In a YAML plain scalar, quotes are NOT special characters — they are just
            // regular characters. So we must NOT track quote state here. Any ": " or
            // " #" anywhere in the value is a problem regardless of DSL-level quoting.
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                // Colon followed by space or at end — YAML mapping indicator
                if (c == ':' && (i + 1 >= value.Length || value[i + 1] == ' '))
                    return true;

                // Hash preceded by space — YAML comment
                if (c == '#' && i > 0 && value[i - 1] == ' ')
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a value that starts and ends with matching quotes actually has
        /// content after the first closing quote (e.g. <c>"alice" in names</c>).
        /// </summary>
        private static bool HasTrailingContentAfterQuotedString(string value)
        {
            char quote = value[0];
            // Find the first closing quote after position 0
            for (int i = 1; i < value.Length - 1; i++)
            {
                if (value[i] == quote)
                {
                    // For double quotes, skip escaped quotes
                    if (quote == '"' && i > 0 && value[i - 1] == '\\')
                        continue;

                    // Found a closing quote before the end — there's trailing content
                    return true;
                }
            }
            return false;
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

        private List<ScriptImport> ParseImports(IParser parser, Script script)
        {
            var imports = new List<ScriptImport>();

            if (!parser.Accept<SequenceStart>(out _))
            {
                AddScriptParseError(script, "imports must be a sequence of mappings with required keys 'path' and 'as'");
                SkipValue(parser);
                return imports;
            }

            parser.Consume<SequenceStart>();
            while (!parser.Accept<SequenceEnd>(out _))
            {
                if (!parser.Accept<MappingStart>(out _))
                {
                    AddScriptParseError(
                        script,
                        $"Line {(int)(parser.Current?.Start.Line ?? 0)}: import entry must be a mapping with required keys 'path' and 'as'");
                    SkipValue(parser);
                    continue;
                }

                var mappingStart = parser.Consume<MappingStart>();
                var import = new ScriptImport { LineNumber = (int)mappingStart.Start.Line };

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "path":
                            import.Path = parser.Consume<Scalar>().Value;
                            break;
                        case "as":
                        case "alias":
                            import.Alias = parser.Consume<Scalar>().Value;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown import key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                imports.Add(import);
            }

            parser.Consume<SequenceEnd>();
            return imports;
        }

        private Dictionary<string, ScriptSubroutine> ParseSubroutines(IParser parser, Script script)
        {
            var subroutines = new Dictionary<string, ScriptSubroutine>(StringComparer.OrdinalIgnoreCase);

            if (!parser.Accept<MappingStart>(out _))
            {
                AddScriptParseError(script, "subroutines must be a mapping of names to definitions");
                SkipValue(parser);
                return subroutines;
            }

            parser.Consume<MappingStart>();
            while (!parser.Accept<MappingEnd>(out _))
            {
                var nameScalar = parser.Consume<Scalar>();
                var name = nameScalar.Value;
                var subroutine = new ScriptSubroutine
                {
                    Name = name,
                    LineNumber = (int)nameScalar.Start.Line
                };

                if (subroutines.ContainsKey(name))
                {
                    AddScriptParseError(script, $"Line {(int)nameScalar.Start.Line}: Duplicate subroutine name '{name}'");
                }

                if (!parser.Accept<MappingStart>(out _))
                {
                    subroutine.ParseErrors.Add($"Line {subroutine.LineNumber}: subroutine '{name}' must be a mapping");
                    SkipValue(parser);
                    subroutines[name] = subroutine;
                    continue;
                }

                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "params":
                            subroutine.Params = ParseStringList(parser);
                            break;
                        case "outputs":
                            subroutine.Outputs = ParseStringList(parser);
                            break;
                        case "steps":
                            subroutine.Steps = ParseSteps(parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown subroutine key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                subroutines[name] = subroutine;
            }

            parser.Consume<MappingEnd>();
            return subroutines;
        }

        private List<string> ParseStringList(IParser parser)
        {
            var value = ParseScalarOrSequence(parser);
            return value switch
            {
                List<string> list => list.Where(item => !string.IsNullOrWhiteSpace(item)).ToList(),
                string str when !string.IsNullOrWhiteSpace(str) => new List<string> { str },
                _ => new List<string>()
            };
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
                    case "repeat":
                        step.DeclaredStepType = StepType.Repeat;
                        ParseRepeatStep(parser, step);
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
                    case "retry":
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var retry))
                            step.Retry = retry;
                        break;
                    case "retry_delay":
                    case "retrydelay":
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var retryDelay))
                            step.RetryDelay = retryDelay;
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
                        step.Readfile = ParseReadfileOptions(parser, step);
                        break;
                    case "writefile":
                        step.DeclaredStepType = StepType.Writefile;
                        step.Writefile = ParseWritefileOptions(parser, step);
                        break;
                    case "exists":
                        step.DeclaredStepType = StepType.Exists;
                        step.Exists = ParseExistsOptions(parser, step);
                        break;
                    case "playsound":
                        step.DeclaredStepType = StepType.PlaySound;
                        step.PlaySound = ParsePlaySoundOptions(parser, step);
                        break;
                    case "input":
                        step.DeclaredStepType = StepType.Input;
                        step.Input = ParseInputOptions(parser, step);
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
                    case "browser_callback_capture":
                        step.DeclaredStepType = StepType.BrowserCallbackCapture;
                        step.BrowserCallbackCapture = ParseBrowserCallbackCaptureOptions(parser, step);
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
                    case "notify":
                        step.DeclaredStepType = StepType.Notify;
                        step.Notify = ParseNotifyOptions(parser, step);
                        break;
                    case "parse":
                        step.DeclaredStepType = StepType.Parse;
                        step.Parse = ParseParseOptions(parser);
                        break;
                    case "choose":
                        step.DeclaredStepType = StepType.Choose;
                        step.Choose = ParseChooseOptions(parser, step);
                        break;
                    case "multiselect":
                        step.DeclaredStepType = StepType.Multiselect;
                        step.Multiselect = ParseMultiselectOptions(parser, step);
                        break;
                    case "confirm":
                        step.DeclaredStepType = StepType.Confirm;
                        step.Confirm = ParseConfirmOptions(parser, step);
                        break;
                    case "interactive":
                        step.DeclaredStepType = StepType.Interactive;
                        step.Interactive = ParseInteractiveOptions(parser, step);
                        break;
                    case "assert":
                        step.DeclaredStepType = StepType.Assert;
                        step.Assert = ParseAssertOptions(parser, step);
                        break;
                    case "switch":
                        step.DeclaredStepType = StepType.Switch;
                        ParseSwitchStep(parser, step);
                        break;
                    case "parallel":
                        step.DeclaredStepType = StepType.Parallel;
                        step.Parallel = ParseParallelOptions(parser, step);
                        break;
                    case "call":
                        step.DeclaredStepType = StepType.Call;
                        step.Call = ParseCallOptions(parser, step);
                        break;
                    case "return":
                        step.DeclaredStepType = StepType.Return;
                        step.ReturnFromSubroutine = ParseBooleanish(parser);
                        break;
                    case "table":
                        step.DeclaredStepType = StepType.Table;
                        step.Table = ParseTableOptions(parser, step);
                        break;
                    case "localcmd":
                        step.DeclaredStepType = StepType.LocalCmd;
                        step.LocalCmd = ParseLocalCmdOptions(parser, step);
                        break;
                    case "vault":
                        step.DeclaredStepType = StepType.Vault;
                        step.Vault = ParseVaultOptions(parser, step);
                        break;
                    case "sethistorylabel":
                        step.DeclaredStepType = StepType.SetHistoryLabel;
                        step.SetHistoryLabel = ParseSetHistoryLabelValue(parser);
                        break;
                    case "cases":
                        step.Cases = ParseSwitchCases(parser);
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
                        case "retry":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var retry))
                                step.Retry = retry;
                            break;
                        case "retry_delay":
                        case "retrydelay":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var retryDelay))
                                step.RetryDelay = retryDelay;
                            break;
                        case "fail_on_nonzero":
                        case "failonnonzero":
                            step.FailOnNonZero = ParseBooleanOrDefault(parser, step.FailOnNonZero);
                            break;
                        case "respond":
                            step.Respond = ParseRespondPairs(parser, step);
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

        private void ParseRepeatStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                var hasUntil = false;
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();
                    switch (key)
                    {
                        case "until":
                            step.Until = parser.Consume<Scalar>().Value;
                            hasUntil = !string.IsNullOrWhiteSpace(step.Until);
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
                            AddUnknownKeyWarning($"Unknown repeat key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                if (!hasUntil)
                {
                    AddStepParseError(step, "repeat.until is required");
                }
                return;
            }

            if (parser.Accept<Scalar>(out _))
            {
                step.Until = parser.Consume<Scalar>().Value;
                if (string.IsNullOrWhiteSpace(step.Until))
                {
                    AddStepParseError(step, "repeat.until is required");
                }
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "repeat must be a mapping with required key 'until'");
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

        private CallOptions? ParseCallOptions(IParser parser, ScriptStep step)
        {
            if (!parser.Accept<MappingStart>(out _))
            {
                SkipValue(parser);
                AddStepParseError(step, "call must be a mapping with required key 'subroutine'");
                return null;
            }

            var options = new CallOptions();
            var hasSubroutine = false;

            parser.Consume<MappingStart>();
            while (!parser.Accept<MappingEnd>(out _))
            {
                var keyScalar = parser.Consume<Scalar>();
                var key = keyScalar.Value.ToLowerInvariant();

                switch (key)
                {
                    case "subroutine":
                        options.Subroutine = parser.Consume<Scalar>().Value;
                        hasSubroutine = !string.IsNullOrWhiteSpace(options.Subroutine);
                        break;
                    case "args":
                        options.Args = ParseStringDictionary(parser, "call.args", step);
                        break;
                    case "out":
                        options.Out = ParseStringDictionary(parser, "call.out", step);
                        break;
                    case "on_error":
                    case "onerror":
                        step.OnError = parser.Consume<Scalar>().Value;
                        break;
                    default:
                        AddUnknownKeyWarning($"Unknown call key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                        SkipValue(parser);
                        break;
                }
            }

            parser.Consume<MappingEnd>();
            if (!hasSubroutine)
            {
                AddStepParseError(step, "call.subroutine is required");
            }

            return options;
        }

        private Dictionary<string, string> ParseStringDictionary(IParser parser, string contextName, ScriptStep step)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!parser.Accept<MappingStart>(out _))
            {
                AddStepParseError(step, $"{contextName} must be a mapping");
                SkipValue(parser);
                return values;
            }

            parser.Consume<MappingStart>();
            while (!parser.Accept<MappingEnd>(out _))
            {
                var keyScalar = parser.Consume<Scalar>();
                var key = keyScalar.Value;
                if (!parser.Accept<Scalar>(out _))
                {
                    AddStepParseError(step, $"{contextName}.{key} must be a scalar value");
                    SkipValue(parser);
                    continue;
                }

                values[key] = parser.Consume<Scalar>().Value;
            }

            parser.Consume<MappingEnd>();
            return values;
        }

        private static void AddScriptParseError(Script script, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (!script.ParseErrors.Contains(message, StringComparer.Ordinal))
            {
                script.ParseErrors.Add(message);
            }
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
                        case "required":
                            var requiredVal = parser.Consume<Scalar>().Value;
                            options.Required = !string.Equals(requiredVal, "false", StringComparison.OrdinalIgnoreCase);
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

        private ReadfileOptions ParseReadfileOptions(IParser parser, ScriptStep step)
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
                        case "select_file":
                        case "selectfile":
                            var selectFileValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                            options.SelectFile = selectFileValue == "true" || selectFileValue == "yes" || selectFileValue == "1";
                            break;
                        case "message":
                            options.Message = parser.Consume<Scalar>().Value;
                            break;
                        case "fileext":
                        case "file_ext":
                        case "file_extensions":
                        case "fileextensions":
                            options.FileExt = parser.Consume<Scalar>().Value;
                            break;
                        case "autobrowse":
                        case "auto_browse":
                            var autoBrowseValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                            options.AutoBrowse = autoBrowseValue == "true" || autoBrowseValue == "yes" || autoBrowseValue == "1";
                            break;
                        case "path_into":
                        case "pathinto":
                            options.PathInto = parser.Consume<Scalar>().Value;
                            break;
                        case "path_only":
                        case "pathonly":
                            var pathOnlyValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                            options.PathOnly = pathOnlyValue == "true" || pathOnlyValue == "yes" || pathOnlyValue == "1";
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
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
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

        private WritefileOptions ParseWritefileOptions(IParser parser, ScriptStep step)
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
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
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

        private ExistsOptions ParseExistsOptions(IParser parser, ScriptStep step)
        {
            var options = new ExistsOptions();

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
                        case "type":
                            options.Type = parser.Consume<Scalar>().Value;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown exists key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
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

        private PlaySoundOptions ParsePlaySoundOptions(IParser parser, ScriptStep step)
        {
            var options = new PlaySoundOptions();

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
                        case "wait":
                            var waitValue = parser.Consume<Scalar>().Value.ToLowerInvariant();
                            if (waitValue == "true" || waitValue == "yes" || waitValue == "1")
                            {
                                options.Wait = true;
                            }
                            else if (waitValue == "false" || waitValue == "no" || waitValue == "0")
                            {
                                options.Wait = false;
                            }
                            else
                            {
                                AddStepParseError(step, "playsound.wait must be true/false");
                            }
                            break;
                        case "volume":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var volume))
                                options.Volume = volume;
                            else
                                AddStepParseError(step, "playsound.volume must be an integer between 0 and 100");
                            break;
                        case "max_seconds":
                        case "maxseconds":
                            if (double.TryParse(
                                parser.Consume<Scalar>().Value,
                                NumberStyles.Float | NumberStyles.AllowThousands,
                                CultureInfo.InvariantCulture,
                                out var maxSeconds))
                                options.MaxSeconds = maxSeconds;
                            else
                                AddStepParseError(step, "playsound.max_seconds must be a positive number");
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown playsound key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
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

        private InputOptions ParseInputOptions(IParser parser, ScriptStep step)
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
                        case "title":
                            options.Title = parser.Consume<Scalar>().Value;
                            break;
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
                        case "font_size":
                        case "fontsize":
                            if (float.TryParse(parser.Consume<Scalar>().Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var inputFont))
                                options.FontSize = inputFont;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
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

        private ChooseOptions ParseChooseOptions(IParser parser, ScriptStep step)
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
                        case "title":
                            options.Title = parser.Consume<Scalar>().Value;
                            break;
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
                        case "font_size":
                        case "fontsize":
                            if (float.TryParse(parser.Consume<Scalar>().Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var chooseFont))
                                options.FontSize = chooseFont;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
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

        private MultiselectOptions ParseMultiselectOptions(IParser parser, ScriptStep step)
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
                        case "title":
                            options.Title = parser.Consume<Scalar>().Value;
                            break;
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
                        case "font_size":
                        case "fontsize":
                            if (float.TryParse(parser.Consume<Scalar>().Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var multiFont))
                                options.FontSize = multiFont;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
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

        private ConfirmOptions ParseConfirmOptions(IParser parser, ScriptStep step)
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
                        case "title":
                            options.Title = parser.Consume<Scalar>().Value;
                            break;
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
                        case "font_size":
                        case "fontsize":
                            if (float.TryParse(parser.Consume<Scalar>().Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var confirmFont))
                                options.FontSize = confirmFont;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
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
                AddStepParseError(step, "interactive must be a mapping with optional keys 'session', 'title', 'command', 'capture', 'max_seconds', 'max_lines', 'width', 'height', 'mirror_output', 'show_window', and 'on_error'");
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

                    case "title":
                        options.Title = parser.Consume<Scalar>().Value;
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

                    case "max_lines":
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var maxLines))
                        {
                            options.MaxLines = maxLines;
                        }
                        else
                        {
                            AddStepParseError(step, "interactive.max_lines must be a positive integer");
                        }
                        break;

                    case "width":
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var width))
                        {
                            options.Width = width;
                        }
                        else
                        {
                            AddStepParseError(step, "interactive.width must be a positive integer");
                        }
                        break;

                    case "height":
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var height))
                        {
                            options.Height = height;
                        }
                        else
                        {
                            AddStepParseError(step, "interactive.height must be a positive integer");
                        }
                        break;

                    case "columns":
                        AddUnknownKeyWarning("interactive.columns is deprecated; use interactive.width/interactive.height (pixels)", (int)keyScalar.Start.Line);
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var legacyColumns))
                        {
                            options.Columns = legacyColumns;
                        }
                        else
                        {
                            AddStepParseError(step, "interactive.columns must be a positive integer");
                        }
                        break;

                    case "rows":
                        AddUnknownKeyWarning("interactive.rows is deprecated; use interactive.width/interactive.height (pixels)", (int)keyScalar.Start.Line);
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var legacyRows))
                        {
                            options.Rows = legacyRows;
                        }
                        else
                        {
                            AddStepParseError(step, "interactive.rows must be a positive integer");
                        }
                        break;

                    case "mirror_output":
                        options.MirrorOutput = ParseBooleanOrDefault(parser, options.MirrorOutput);
                        break;

                    case "show_window":
                        options.ShowWindow = ParseBooleanOrDefault(parser, options.ShowWindow);
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

        private object? ParseSetHistoryLabelValue(IParser parser)
        {
            if (parser.Accept<Scalar>(out _))
            {
                return parser.Consume<Scalar>().Value;
            }

            if (parser.Accept<MappingStart>(out _))
            {
                var options = new SetHistoryLabelOptions();
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "value":
                            if (parser.Accept<Scalar>(out _))
                            {
                                options.Value = parser.Consume<Scalar>().Value;
                            }
                            else
                            {
                                SkipValue(parser);
                            }
                            break;
                        case "replace":
                            if (TryParseBooleanStrict(parser, out var replace))
                            {
                                options.Replace = replace;
                            }
                            else
                            {
                                options.Replace = null;
                            }
                            break;
                        case "mode":
                            if (parser.Accept<Scalar>(out _))
                            {
                                options.Mode = parser.Consume<Scalar>().Value;
                            }
                            else
                            {
                                SkipValue(parser);
                            }
                            break;
                        case "separator":
                            if (parser.Accept<Scalar>(out _))
                            {
                                options.Separator = parser.Consume<Scalar>().Value;
                            }
                            else
                            {
                                SkipValue(parser);
                            }
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown sethistorylabel key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                return options;
            }

            SkipValue(parser);
            return null;
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

        private BrowserCallbackCaptureOptions ParseBrowserCallbackCaptureOptions(IParser parser, ScriptStep step)
        {
            var options = new BrowserCallbackCaptureOptions();

            if (parser.Accept<Scalar>(out _))
            {
                // Shorthand: - browser_callback_capture: "https://example.com/start"
                options.StartUrl = parser.Consume<Scalar>().Value;
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
                        case "start_url":
                        case "starturl":
                            options.StartUrl = parser.Consume<Scalar>().Value;
                            break;
                        case "callback_path":
                        case "callbackpath":
                            options.CallbackPath = parser.Consume<Scalar>().Value;
                            break;
                        case "local_port":
                        case "localport":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var localPort))
                                options.LocalPort = localPort;
                            break;
                        case "capture_mode":
                        case "capturemode":
                            options.CaptureMode = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "browser_mode":
                        case "browsermode":
                            options.BrowserMode = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "show_after_seconds":
                        case "showafterseconds":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var showAfterSeconds))
                                options.ShowAfterSeconds = showAfterSeconds;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "required_fields":
                        case "requiredfields":
                            options.RequiredFields = ParseStringList(parser);
                            break;
                        case "timeout":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var timeout))
                                options.Timeout = timeout;
                            break;
                        case "open_browser":
                        case "openbrowser":
                            options.OpenBrowser = ParseBooleanOrDefault(parser, options.OpenBrowser);
                            break;
                        case "auto_close_browser":
                        case "autoclosebrowser":
                            options.AutoCloseBrowser = ParseBooleanOrDefault(parser, options.AutoCloseBrowser);
                            break;
                        case "completion_message":
                        case "completionmessage":
                            options.CompletionMessage = parser.Consume<Scalar>().Value;
                            break;
                        case "failure_message":
                        case "failuremessage":
                            options.FailureMessage = parser.Consume<Scalar>().Value;
                            break;
                        case "quiet":
                            options.Quiet = ParseBooleanOrDefault(parser, options.Quiet);
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown browser_callback_capture key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
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

        private NotifyOptions ParseNotifyOptions(IParser parser, ScriptStep step)
        {
            var options = new NotifyOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "profile":
                            options.Profile = parser.Consume<Scalar>().Value;
                            break;
                        case "channel":
                            options.Channel = parser.Consume<Scalar>().Value;
                            break;
                        case "title":
                            options.Title = parser.Consume<Scalar>().Value;
                            break;
                        case "message":
                            options.Message = parser.Consume<Scalar>().Value;
                            break;
                        case "level":
                            options.Level = parser.Consume<Scalar>().Value;
                            break;
                        case "mention":
                            options.Mention = ParseStringList(parser);
                            break;
                        case "attachments":
                            options.Attachments = ParseStringList(parser);
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown notify key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
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

        private AssertOptions ParseAssertOptions(IParser parser, ScriptStep step)
        {
            var options = new AssertOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "condition":
                        case "that":
                            options.Condition = parser.Consume<Scalar>().Value;
                            break;
                        case "message":
                            options.Message = parser.Consume<Scalar>().Value;
                            break;
                        case "severity":
                            options.Severity = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown assert key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else if (parser.Accept<Scalar>(out _))
            {
                // Shorthand: assert: "condition expression"
                options.Condition = parser.Consume<Scalar>().Value;
            }
            else
            {
                SkipValue(parser);
            }

            return options;
        }

        private void ParseSwitchStep(IParser parser, ScriptStep step)
        {
            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "value":
                            step.Switch = parser.Consume<Scalar>().Value;
                            break;
                        case "cases":
                            step.Cases = ParseSwitchCases(parser);
                            break;
                        case "default":
                            step.Else = ParseSteps(parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown switch key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                return;
            }

            if (parser.Accept<Scalar>(out _))
            {
                // Shorthand: switch: "${var}"
                step.Switch = parser.Consume<Scalar>().Value;
                return;
            }

            SkipValue(parser);
            AddStepParseError(step, "switch must be a mapping with 'value' and 'cases'");
        }

        private List<SwitchCase> ParseSwitchCases(IParser parser)
        {
            var cases = new List<SwitchCase>();

            if (!parser.Accept<SequenceStart>(out _))
            {
                SkipValue(parser);
                return cases;
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
                var switchCase = new SwitchCase { LineNumber = (int)mapStart.Start.Line };

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "value":
                        case "case":
                            switchCase.Value = parser.Consume<Scalar>().Value;
                            break;
                        case "do":
                            switchCase.Do = ParseSteps(parser) ?? new List<ScriptStep>();
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown switch case key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                cases.Add(switchCase);
            }

            parser.Consume<SequenceEnd>();
            return cases;
        }

        private Models.ParallelOptions ParseParallelOptions(IParser parser, ScriptStep step)
        {
            var options = new Models.ParallelOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "steps":
                            options.Steps = ParseSteps(parser) ?? new List<ScriptStep>();
                            break;
                        case "max_concurrent":
                        case "maxconcurrent":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var maxConcurrent))
                                options.MaxConcurrent = maxConcurrent;
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown parallel key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
                AddStepParseError(step, "parallel must be a mapping with required key 'steps'");
            }

            return options;
        }

        private TableOptions ParseTableOptions(IParser parser, ScriptStep step)
        {
            var options = new TableOptions();

            if (parser.Accept<MappingStart>(out _))
            {
                parser.Consume<MappingStart>();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "data":
                            options.Data = parser.Consume<Scalar>().Value;
                            break;
                        case "columns":
                            options.Columns = ParseTableColumns(parser);
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "align":
                            options.Align = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "show_header":
                        case "showheader":
                            options.ShowHeader = ParseBooleanOrDefault(parser, options.ShowHeader);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown table key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
            }
            else
            {
                SkipValue(parser);
                AddStepParseError(step, "table must be a mapping with required key 'data'");
            }

            return options;
        }

        private List<TableColumn> ParseTableColumns(IParser parser)
        {
            var columns = new List<TableColumn>();

            if (!parser.Accept<SequenceStart>(out _))
            {
                SkipValue(parser);
                return columns;
            }

            parser.Consume<SequenceStart>();

            while (!parser.Accept<SequenceEnd>(out _))
            {
                if (!parser.Accept<MappingStart>(out _))
                {
                    SkipValue(parser);
                    continue;
                }

                parser.Consume<MappingStart>();
                var column = new TableColumn();

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "header":
                            column.Header = parser.Consume<Scalar>().Value;
                            break;
                        case "field":
                            column.Field = parser.Consume<Scalar>().Value;
                            break;
                        case "align":
                            column.Align = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "width":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var width))
                                column.Width = width;
                            break;
                        default:
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();
                columns.Add(column);
            }

            parser.Consume<SequenceEnd>();
            return columns;
        }

        private LocalCmdOptions ParseLocalCmdOptions(IParser parser, ScriptStep step)
        {
            var options = new LocalCmdOptions();

            if (parser.Accept<Scalar>(out _))
            {
                options.Command = parser.Consume<Scalar>().Value;
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
                        case "command":
                            options.Command = parser.Consume<Scalar>().Value;
                            break;
                        case "shell":
                            options.Shell = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "shell_path":
                            options.ShellPath = parser.Consume<Scalar>().Value;
                            break;
                        case "args":
                            options.Args = ParseStringList(parser);
                            break;
                        case "env":
                            options.Env = ParseStringDictionary(parser);
                            break;
                        case "working_dir":
                            options.WorkingDir = parser.Consume<Scalar>().Value;
                            break;
                        case "interactive":
                            options.Interactive = ParseBooleanOrDefault(parser, false);
                            break;
                        case "keep_open":
                            options.KeepOpen = ParseBooleanOrDefault(parser, false);
                            break;
                        case "run_mode":
                            options.RunMode = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "lifetime":
                            options.Lifetime = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            options.LifetimeSpecified = true;
                            break;
                        case "kill_on_cancel":
                            options.KillOnCancel = ParseBooleanOrDefault(parser, false);
                            break;
                        case "fail_on_nonzero":
                            options.FailOnNonZero = ParseBooleanOrDefault(parser, true);
                            break;
                        case "success_codes":
                            options.SuccessCodes = ParseIntList(parser);
                            break;
                        case "max_output_bytes":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var maxBytes))
                                options.MaxOutputBytes = maxBytes;
                            break;
                        case "confirm":
                            options.Confirm = NormalizeLowerLiteralEnum(parser.Consume<Scalar>().Value);
                            break;
                        case "quiet":
                            options.Quiet = ParseBooleanOrDefault(parser, false);
                            break;
                        case "suppress":
                            options.Suppress = ParseBooleanOrDefault(parser, false);
                            break;
                        case "title":
                            options.Title = parser.Consume<Scalar>().Value;
                            break;
                        case "into":
                            options.Into = parser.Consume<Scalar>().Value;
                            break;
                        case "timeout":
                            if (int.TryParse(parser.Consume<Scalar>().Value, out var timeout))
                                step.Timeout = timeout;
                            break;
                        case "on_error":
                        case "onerror":
                            ApplyNestedOnErrorAlias(step, parser);
                            break;
                        default:
                            AddUnknownKeyWarning($"Unknown localcmd key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
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

        private VaultStepOptions ParseVaultOptions(IParser parser, ScriptStep step)
        {
            var options = new VaultStepOptions();

            if (!parser.Accept<MappingStart>(out _))
            {
                SkipValue(parser);
                return options;
            }

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
                    case "profile":
                        options.Profile = parser.Consume<Scalar>().Value;
                        break;
                    case "key":
                        options.Key = parser.Consume<Scalar>().Value;
                        break;
                    case "keys":
                        options.Keys = ParseStringDictionary(parser);
                        break;
                    case "into":
                        options.Into = parser.Consume<Scalar>().Value;
                        break;
                    case "version":
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var version))
                            options.Version = version;
                        break;
                    case "write":
                        options.Write = ParseStringDictionary(parser);
                        break;
                    case "patch":
                        options.Patch = ParseStringDictionary(parser);
                        break;
                    case "on_error":
                    case "onerror":
                        ApplyNestedOnErrorAlias(step, parser);
                        break;
                    default:
                        AddUnknownKeyWarning($"Unknown vault key '{keyScalar.Value}'", (int)keyScalar.Start.Line);
                        SkipValue(parser);
                        break;
                }
            }

            parser.Consume<MappingEnd>();

            return options;
        }

        private List<int> ParseIntList(IParser parser)
        {
            var list = new List<int>();

            if (parser.Accept<Scalar>(out _))
            {
                var value = parser.Consume<Scalar>().Value;
                foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(part, out var n))
                        list.Add(n);
                }
                return list;
            }

            if (parser.Accept<SequenceStart>(out _))
            {
                parser.Consume<SequenceStart>();
                while (!parser.Accept<SequenceEnd>(out _))
                {
                    if (parser.Accept<Scalar>(out _))
                    {
                        if (int.TryParse(parser.Consume<Scalar>().Value, out var n))
                            list.Add(n);
                    }
                    else
                    {
                        SkipValue(parser);
                    }
                }
                parser.Consume<SequenceEnd>();
                return list;
            }

            SkipValue(parser);
            return list;
        }

        private List<RespondPair> ParseRespondPairs(IParser parser, ScriptStep step)
        {
            var pairs = new List<RespondPair>();

            if (!parser.Accept<SequenceStart>(out _))
            {
                SkipValue(parser);
                AddStepParseError(step, "send.respond must be a sequence of expect/reply pairs");
                return pairs;
            }

            parser.Consume<SequenceStart>();

            while (!parser.Accept<SequenceEnd>(out _))
            {
                if (!parser.Accept<MappingStart>(out _))
                {
                    SkipValue(parser);
                    continue;
                }

                var mappingStart = parser.Consume<MappingStart>();
                var pairLine = (int)mappingStart.Start.Line;
                var pair = new RespondPair();
                var hasExpect = false;
                var hasReply = false;

                while (!parser.Accept<MappingEnd>(out _))
                {
                    var keyScalar = parser.Consume<Scalar>();
                    var key = keyScalar.Value.ToLowerInvariant();

                    switch (key)
                    {
                        case "expect":
                            pair.Expect = parser.Consume<Scalar>().Value;
                            hasExpect = !string.IsNullOrWhiteSpace(pair.Expect);
                            break;
                        case "reply":
                        case "send":
                            pair.Reply = parser.Consume<Scalar>().Value;
                            hasReply = !string.IsNullOrWhiteSpace(pair.Reply);
                            break;
                        default:
                            SkipValue(parser);
                            break;
                    }
                }

                parser.Consume<MappingEnd>();

                if (!hasExpect || !hasReply)
                {
                    AddStepParseError(step, $"send.respond entry at line {pairLine} requires both 'expect' and 'reply'");
                    continue;
                }

                pairs.Add(pair);
            }

            parser.Consume<SequenceEnd>();

            if (pairs.Count == 0)
            {
                AddStepParseError(step, "send.respond must contain at least one valid expect/reply pair");
            }

            return pairs;
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
        public List<string> Validate(
            Script script,
            string? originalYaml = null,
            bool enforceCanonicalSyntax = false,
            bool allowLibraryDefinitions = false)
        {
            var errors = new List<string>();
            var lines = originalYaml?.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            script.SubroutineRegistry = null;

            foreach (var parseError in script.ParseErrors)
            {
                errors.Add(parseError);
            }

            if (script.Library)
            {
                ValidateLibraryTopLevel(script, errors);

                if (!allowLibraryDefinitions)
                {
                    errors.Add("Library scripts cannot be executed directly");
                }
            }
            else if (script.Steps == null || script.Steps.Count == 0)
            {
                errors.Add("Script has no steps defined");
            }
            else
            {
                if (script.Preconnect != null && script.Preconnect.Count > 0)
                {
                    ValidateSteps(
                        script.Preconnect,
                        errors,
                        "Preconnect: ",
                        lines,
                        loopDepth: 0,
                        enforceCanonicalSyntax,
                        insideSubroutine: false,
                        insidePreconnect: true);
                }

                ValidateSteps(script.Steps, errors, "", lines, 0, enforceCanonicalSyntax, insideSubroutine: false);
            }

            foreach (var subroutine in script.Subroutines.Values)
            {
                ValidateSubroutine(subroutine, errors, lines, enforceCanonicalSyntax);
            }

            if (allowLibraryDefinitions || !script.Library)
            {
                var builder = new ScriptSubroutineRegistryBuilder();
                script.SubroutineRegistry = builder.Build(script, errors, enforceCanonicalSyntax);
            }

            return errors;
        }

        private void ValidateLibraryTopLevel(Script script, List<string> errors)
        {
            ValidateForbiddenLibraryKey(script, errors, "steps");
            ValidateForbiddenLibraryKey(script, errors, "preconnect");
            ValidateForbiddenLibraryKey(script, errors, "vars");
            ValidateForbiddenLibraryKey(script, errors, "imports");
            ValidateForbiddenLibraryKey(script, errors, "environment");
            ValidateForbiddenLibraryKey(script, errors, "debug");
            ValidateForbiddenLibraryKey(script, errors, "nobanner");
            ValidateForbiddenLibraryKey(script, errors, "compact_errors");
            ValidateForbiddenLibraryKey(script, errors, "suppress_missing_column_warning");
        }

        private static void ValidateForbiddenLibraryKey(Script script, List<string> errors, string key)
        {
            if (script.DeclaredTopLevelKeys.Contains(key))
            {
                errors.Add($"Library files may not declare '{key}'");
            }
        }

        private void ValidateSubroutine(
            ScriptSubroutine subroutine,
            List<string> errors,
            string[]? lines,
            bool enforceCanonicalSyntax)
        {
            foreach (var parseError in subroutine.ParseErrors)
            {
                errors.Add(parseError);
            }

            if (string.IsNullOrWhiteSpace(subroutine.Name))
            {
                errors.Add($"Line {subroutine.LineNumber}: subroutine name is required");
            }

            ValidateUniqueNames(
                subroutine.Params,
                $"Line {subroutine.LineNumber}: subroutine '{subroutine.Name}' has duplicate param '{{0}}'",
                errors);

            ValidateUniqueNames(
                subroutine.Outputs,
                $"Line {subroutine.LineNumber}: subroutine '{subroutine.Name}' has duplicate output '{{0}}'",
                errors);

            if (subroutine.Steps == null || subroutine.Steps.Count == 0)
            {
                errors.Add($"Line {subroutine.LineNumber}: subroutine '{subroutine.Name}' requires 'steps'");
                return;
            }

            ValidateSteps(
                subroutine.Steps,
                errors,
                $"Subroutine '{subroutine.Name}': ",
                lines,
                loopDepth: 0,
                enforceCanonicalSyntax,
                insideSubroutine: true);
        }

        private static void ValidateUniqueNames(
            IEnumerable<string> names,
            string messageTemplate,
            List<string> errors)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(messageTemplate.Replace("{0}", "(blank)", StringComparison.Ordinal));
                    continue;
                }

                if (!seen.Add(name))
                {
                    errors.Add(string.Format(messageTemplate, name));
                }
            }
        }

        private void ValidateSteps(
            List<ScriptStep> steps,
            List<string> errors,
            string prefix,
            string[]? lines,
            int loopDepth,
            bool enforceCanonicalSyntax,
            bool insideSubroutine,
            bool insidePreconnect = false)
        {
            foreach (var step in steps)
            {
                var stepType = step.GetStepType();

                if (insidePreconnect && RequiresSshShellSession(stepType))
                {
                    var lineContent = GetLineContent(lines, step.LineNumber);
                    errors.Add(
                        $"{prefix}Line {step.LineNumber}: {stepType.ToString().ToLowerInvariant()} is not allowed in preconnect because it requires an active SSH session{lineContent}");
                }

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
                    case StepType.Send:
                        if (step.FailOnNonZero && !string.IsNullOrWhiteSpace(step.Expect))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: send.fail_on_nonzero is not supported with send.expect{lineContent}");
                        }
                        break;

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
                            ValidateSteps(step.Then, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
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
                                    ValidateSteps(branch.Then, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
                                }
                            }
                        }
                        if (step.Else != null)
                            ValidateSteps(step.Else, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
                        break;

                    case StepType.Foreach:
                        if (step.Do == null || step.Do.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Foreach requires 'do' block{lineContent}");
                        }
                        if (step.Do != null)
                            ValidateSteps(step.Do, errors, prefix + "  ", lines, loopDepth + 1, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
                        break;

                    case StepType.Repeat:
                        if (string.IsNullOrWhiteSpace(step.Until))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Repeat requires 'until' condition{lineContent}");
                        }
                        if (step.Do == null || step.Do.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Repeat requires 'do' block{lineContent}");
                        }
                        if (step.MaxIterations.HasValue && step.MaxIterations.Value <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: max_iterations must be greater than 0{lineContent}");
                        }
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
                            ValidateSteps(step.Do, errors, prefix + "  ", lines, loopDepth + 1, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
                        break;

                    case StepType.Try:
                        if (step.Try == null || step.Try.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Try requires 'do' block{lineContent}");
                        }
                        if (step.Try != null)
                            ValidateSteps(step.Try, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
                        if (step.Catch != null)
                            ValidateSteps(step.Catch, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
                        if (step.Finally != null)
                            ValidateSteps(step.Finally, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
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

                    case StepType.Return:
                        if (!insideSubroutine)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: return can only be used inside subroutines{lineContent}");
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
                        if (step.Readfile == null || (!step.Readfile.SelectFile && string.IsNullOrEmpty(step.Readfile.Path)))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Readfile requires 'path'{lineContent}");
                        }

                        if (step.Readfile == null)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Readfile requires 'into' variable{lineContent}");
                            break;
                        }

                        if (step.Readfile.PathOnly)
                        {
                            if (string.IsNullOrWhiteSpace(step.Readfile.PathInto))
                            {
                                var lineContent = GetLineContent(lines, step.LineNumber);
                                errors.Add($"{prefix}Line {step.LineNumber}: Readfile requires 'path_into' when 'path_only' is true{lineContent}");
                            }
                        }
                        else if (string.IsNullOrEmpty(step.Readfile.Into))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Readfile requires 'into' variable{lineContent}");
                        }

                        if (!step.Readfile.PathOnly &&
                            !string.IsNullOrWhiteSpace(step.Readfile.PathInto) &&
                            string.Equals(step.Readfile.PathInto, step.Readfile.Into, StringComparison.OrdinalIgnoreCase))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Readfile 'path_into' must differ from 'into' unless 'path_only' is true{lineContent}");
                        }
                        break;

                    case StepType.Writefile:
                        if (step.Writefile == null || string.IsNullOrEmpty(step.Writefile.Path))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Writefile requires 'path'{lineContent}");
                        }
                        break;

                    case StepType.Exists:
                        if (step.Exists == null || string.IsNullOrWhiteSpace(step.Exists.Path))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Exists requires 'path'{lineContent}");
                        }

                        if (step.Exists == null || string.IsNullOrWhiteSpace(step.Exists.Into))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Exists requires 'into' variable{lineContent}");
                        }

                        if (step.Exists != null &&
                            !IsDynamicValue(step.Exists.Type) &&
                            !string.Equals(step.Exists.Type, "any", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(step.Exists.Type, "file", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(step.Exists.Type, "directory", StringComparison.OrdinalIgnoreCase))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Exists 'type' must be one of any, file, directory{lineContent}");
                        }
                        break;

                    case StepType.PlaySound:
                        if (step.PlaySound == null || string.IsNullOrWhiteSpace(step.PlaySound.Path))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Playsound requires 'path'{lineContent}");
                        }

                        if (step.PlaySound != null && (step.PlaySound.Volume < 0 || step.PlaySound.Volume > 100))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Playsound 'volume' must be between 0 and 100{lineContent}");
                        }

                        if (step.PlaySound?.MaxSeconds.HasValue == true && step.PlaySound.MaxSeconds.Value <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: playsound.max_seconds must be greater than 0{lineContent}");
                        }
                        break;

                    case StepType.Input:
                        if (step.Input == null || string.IsNullOrEmpty(step.Input.Into))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Input requires 'into' variable{lineContent}");
                        }
                        break;

                    case StepType.Choose:
                        if (step.Choose == null || string.IsNullOrWhiteSpace(step.Choose.Into))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Choose requires 'into' variable{lineContent}");
                        }

                        if (step.Choose == null ||
                            ((step.Choose.Options == null || step.Choose.Options.Count == 0) &&
                             string.IsNullOrWhiteSpace(step.Choose.OptionsFrom)))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Choose requires 'options'{lineContent}");
                        }
                        break;

                    case StepType.Multiselect:
                        if (step.Multiselect == null || string.IsNullOrWhiteSpace(step.Multiselect.Into))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Multiselect requires 'into' variable{lineContent}");
                        }

                        if (step.Multiselect == null ||
                            ((step.Multiselect.Options == null || step.Multiselect.Options.Count == 0) &&
                             string.IsNullOrWhiteSpace(step.Multiselect.OptionsFrom)))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Multiselect requires 'options'{lineContent}");
                        }
                        break;

                    case StepType.Confirm:
                        if (step.Confirm == null || string.IsNullOrWhiteSpace(step.Confirm.Into))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Confirm requires 'into' variable{lineContent}");
                        }
                        break;

                    case StepType.Webhook:
                        if (step.Webhook == null || string.IsNullOrWhiteSpace(step.Webhook.Url))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Webhook requires 'url'{lineContent}");
                        }
                        break;

                    case StepType.Log:
                        switch (step.Log)
                        {
                            case LogOptions logOptions when string.IsNullOrWhiteSpace(logOptions.Message):
                            {
                                var lineContent = GetLineContent(lines, step.LineNumber);
                                errors.Add($"{prefix}Line {step.LineNumber}: Log requires 'message'{lineContent}");
                                break;
                            }
                            case string message when string.IsNullOrWhiteSpace(message):
                            {
                                var lineContent = GetLineContent(lines, step.LineNumber);
                                errors.Add($"{prefix}Line {step.LineNumber}: Log requires 'message'{lineContent}");
                                break;
                            }
                            case null:
                            {
                                var lineContent = GetLineContent(lines, step.LineNumber);
                                errors.Add($"{prefix}Line {step.LineNumber}: Log requires 'message'{lineContent}");
                                break;
                            }
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

                    case StepType.BrowserCallbackCapture:
                        if (step.BrowserCallbackCapture == null)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: browser_callback_capture requires options{lineContent}");
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(step.BrowserCallbackCapture.StartUrl))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: browser_callback_capture requires 'start_url'{lineContent}");
                        }

                        if (string.IsNullOrWhiteSpace(step.BrowserCallbackCapture.CallbackPath))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: browser_callback_capture requires 'callback_path'{lineContent}");
                        }
                        else if (!IsDynamicValue(step.BrowserCallbackCapture.CallbackPath) &&
                                 !step.BrowserCallbackCapture.CallbackPath.StartsWith("/", StringComparison.Ordinal))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: browser_callback_capture.callback_path must start with '/'{lineContent}");
                        }

                        if (string.IsNullOrWhiteSpace(step.BrowserCallbackCapture.Into))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: browser_callback_capture requires 'into'{lineContent}");
                        }

                        if (step.BrowserCallbackCapture.LocalPort < 1 || step.BrowserCallbackCapture.LocalPort > 65535)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: browser_callback_capture.local_port must be between 1 and 65535{lineContent}");
                        }

                        if (step.BrowserCallbackCapture.Timeout <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: browser_callback_capture.timeout must be greater than 0{lineContent}");
                        }

                        if (!IsDynamicValue(step.BrowserCallbackCapture.CaptureMode) &&
                            !string.Equals(step.BrowserCallbackCapture.CaptureMode, "auto", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(step.BrowserCallbackCapture.CaptureMode, "fragment", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(step.BrowserCallbackCapture.CaptureMode, "query", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(step.BrowserCallbackCapture.CaptureMode, "post_body", StringComparison.OrdinalIgnoreCase))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: browser_callback_capture.capture_mode must be one of auto, fragment, query, post_body{lineContent}");
                        }

                        if (!IsDynamicValue(step.BrowserCallbackCapture.BrowserMode) &&
                            !string.Equals(step.BrowserCallbackCapture.BrowserMode, "external", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(step.BrowserCallbackCapture.BrowserMode, "webview2", StringComparison.OrdinalIgnoreCase))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: browser_callback_capture.browser_mode must be one of external, webview2{lineContent}");
                        }

                        if (step.BrowserCallbackCapture.ShowAfterSeconds < 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: browser_callback_capture.show_after_seconds must be greater than or equal to 0{lineContent}");
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

                    case StepType.SetHistoryLabel:
                        if (step.SetHistoryLabel is SetHistoryLabelOptions setHistoryLabelOptions &&
                            !IsDynamicValue(setHistoryLabelOptions.Mode) &&
                            !HistoryLabelOperation.IsValidMode(setHistoryLabelOptions.Mode))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: sethistorylabel 'mode' must be one of replace, append, prepend, clear{lineContent}");
                        }
                        break;

                    case StepType.LocalCmd:
                        if (step.LocalCmd == null)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: LocalCmd requires options{lineContent}");
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(step.LocalCmd.Command))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: LocalCmd requires 'command'{lineContent}");
                        }

                        if (!IsDynamicValue(step.LocalCmd.Shell) && !IsValidLocalCmdShell(step.LocalCmd.Shell))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: localcmd 'shell' must be one of powershell, cmd, custom{lineContent}");
                        }

                        if (!IsDynamicValue(step.LocalCmd.Shell) &&
                            string.Equals(step.LocalCmd.Shell, "custom", StringComparison.OrdinalIgnoreCase) &&
                            string.IsNullOrWhiteSpace(step.LocalCmd.ShellPath))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: localcmd 'shell_path' is required when shell is custom{lineContent}");
                        }

                        if (!IsDynamicValue(step.LocalCmd.RunMode) && !IsValidLocalCmdRunMode(step.LocalCmd.RunMode))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: localcmd 'run_mode' must be one of foreground, background{lineContent}");
                        }

                        if (step.LocalCmd.Interactive &&
                            string.Equals(step.LocalCmd.RunMode, "background", StringComparison.OrdinalIgnoreCase))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive: true and run_mode: background are mutually exclusive{lineContent}");
                        }

                        if (step.LocalCmd.KeepOpen && !step.LocalCmd.Interactive)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: localcmd.keep_open requires interactive: true{lineContent}");
                        }

                        if (!IsDynamicValue(step.LocalCmd.Lifetime) && !IsValidLocalCmdLifetime(step.LocalCmd.Lifetime))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: localcmd 'lifetime' must be one of detached, script, app{lineContent}");
                        }

                        if (!IsDynamicValue(step.LocalCmd.Confirm) && !IsValidLocalCmdConfirm(step.LocalCmd.Confirm))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: localcmd 'confirm' must be one of always, once, never{lineContent}");
                        }

                        if (step.LocalCmd.MaxOutputBytes <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: localcmd.max_output_bytes must be greater than 0{lineContent}");
                        }
                        break;

                    case StepType.Interactive:
                        if (step.Interactive == null)
                        {
                            // ParseStep already records this mapping-shape error for interactive.
                            // Avoid duplicate reporting when parse errors are surfaced above.
                            if (step.ParseErrors.Count == 0)
                            {
                                var lineContent = GetLineContent(lines, step.LineNumber);
                                errors.Add($"{prefix}Line {step.LineNumber}: interactive must be a mapping with optional keys 'session', 'title', 'command', 'capture', 'max_seconds', 'max_lines', 'width', 'height', 'mirror_output', 'show_window', and 'on_error'{lineContent}");
                            }
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

                        if (step.Interactive.MaxLines.HasValue &&
                            step.Interactive.MaxLines.Value <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive.max_lines must be greater than 0{lineContent}");
                        }

                        if (step.Interactive.Width.HasValue &&
                            step.Interactive.Width.Value <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive.width must be greater than 0{lineContent}");
                        }

                        if (step.Interactive.Height.HasValue &&
                            step.Interactive.Height.Value <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive.height must be greater than 0{lineContent}");
                        }

                        if (step.Interactive.Columns.HasValue &&
                            step.Interactive.Columns.Value <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive.columns must be greater than 0{lineContent}");
                        }

                        if (step.Interactive.Rows.HasValue &&
                            step.Interactive.Rows.Value <= 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive.rows must be greater than 0{lineContent}");
                        }

                        if (!step.Interactive.ShowWindow &&
                            string.IsNullOrWhiteSpace(step.Interactive.Command))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive.show_window=false requires interactive.command{lineContent}");
                        }

                        if (!step.Interactive.ShowWindow &&
                            !step.Interactive.MaxSeconds.HasValue &&
                            !step.Interactive.MaxLines.HasValue)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: interactive.show_window=false requires interactive.max_seconds or interactive.max_lines{lineContent}");
                        }
                        break;

                    case StepType.Assert:
                        if (step.Assert == null || string.IsNullOrWhiteSpace(step.Assert.Condition))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Assert requires 'condition'{lineContent}");
                        }
                        if (step.Assert != null && !IsDynamicValue(step.Assert.Severity) &&
                            !string.Equals(step.Assert.Severity, "error", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(step.Assert.Severity, "warning", StringComparison.OrdinalIgnoreCase))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Assert 'severity' must be 'error' or 'warning'{lineContent}");
                        }
                        break;

                    case StepType.Switch:
                        if (string.IsNullOrWhiteSpace(step.Switch))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Switch requires a value expression{lineContent}");
                        }
                        if (step.Cases == null || step.Cases.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Switch requires at least one 'case'{lineContent}");
                        }
                        if (step.Cases != null)
                        {
                            foreach (var switchCase in step.Cases)
                            {
                                if (switchCase.Do != null)
                                    ValidateSteps(switchCase.Do, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
                            }
                        }
                        if (step.Else != null)
                            ValidateSteps(step.Else, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
                        break;

                    case StepType.Parallel:
                        if (step.Parallel == null || step.Parallel.Steps.Count == 0)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Parallel requires at least one step{lineContent}");
                        }
                        if (step.Parallel?.Steps != null)
                            ValidateSteps(step.Parallel.Steps, errors, prefix + "  ", lines, loopDepth, enforceCanonicalSyntax, insideSubroutine, insidePreconnect);
                        break;

                    case StepType.Call:
                        if (step.Call == null)
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Call requires 'subroutine'{lineContent}");
                        }
                        break;

                    case StepType.Table:
                        if (step.Table == null || string.IsNullOrWhiteSpace(step.Table.Data))
                        {
                            var lineContent = GetLineContent(lines, step.LineNumber);
                            errors.Add($"{prefix}Line {step.LineNumber}: Table requires 'data'{lineContent}");
                        }
                        break;
                }

                // Validate retry options
                if (step.Retry.HasValue && step.Retry.Value < 0)
                {
                    var lineContent = GetLineContent(lines, step.LineNumber);
                    errors.Add($"{prefix}Line {step.LineNumber}: retry must be a non-negative integer{lineContent}");
                }
                if (step.RetryDelay.HasValue && step.RetryDelay.Value < 0)
                {
                    var lineContent = GetLineContent(lines, step.LineNumber);
                    errors.Add($"{prefix}Line {step.LineNumber}: retry_delay must be a non-negative integer{lineContent}");
                }
            }
        }

        private static bool IsValidOnErrorValue(string value)
        {
            return string.Equals(value, "continue", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "stop", StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiresSshShellSession(StepType stepType)
        {
            return stepType == StepType.Send || stepType == StepType.Interactive;
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

        private static bool IsValidLocalCmdShell(string shell)
        {
            if (string.IsNullOrWhiteSpace(shell))
                return false;

            var normalized = shell.Trim();
            return string.Equals(normalized, "powershell", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "powershell.exe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "cmd", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("\\cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("\\powershell.exe", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/powershell.exe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "custom", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidLocalCmdRunMode(string runMode)
        {
            return string.Equals(runMode, "foreground", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(runMode, "background", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidLocalCmdLifetime(string lifetime)
        {
            return string.Equals(lifetime, "detached", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(lifetime, "script", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(lifetime, "app", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidLocalCmdConfirm(string confirm)
        {
            return string.Equals(confirm, "always", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(confirm, "once", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(confirm, "never", StringComparison.OrdinalIgnoreCase);
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
