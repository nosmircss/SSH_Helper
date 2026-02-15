using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Resolves choose/multiselect options from inline lists or runtime variables.
    /// </summary>
    internal static class ChoiceOptionResolver
    {
        private static readonly Regex SimpleVariableNameRegex = new(@"^[A-Za-z_]\w*$", RegexOptions.Compiled);

        public static List<ChoiceOption> Resolve(
            IEnumerable<ChoiceOption>? inlineOptions,
            string? optionsSource,
            ScriptContext context,
            out string? error)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!string.IsNullOrWhiteSpace(optionsSource))
            {
                return ResolveFromSource(optionsSource, context, out error);
            }

            var resolvedInline = ResolveInlineOptions(inlineOptions, context);
            error = resolvedInline.Count > 0 ? null : "no options were provided";
            return resolvedInline;
        }

        private static List<ChoiceOption> ResolveFromSource(string optionsSource, ScriptContext context, out string? error)
        {
            var rawValue = ResolveSourceValue(optionsSource, context);
            if (rawValue == null)
            {
                error = $"options source '{optionsSource}' did not resolve to a value";
                return new List<ChoiceOption>();
            }

            var resolved = ConvertRawValueToOptions(rawValue, context);
            if (resolved.Count == 0)
            {
                error = $"options source '{optionsSource}' resolved to an empty list";
                return resolved;
            }

            error = null;
            return resolved;
        }

        private static object? ResolveSourceValue(string optionsSource, ScriptContext context)
        {
            var source = optionsSource.Trim();
            if (source.Length == 0)
                return null;

            if (TryExtractWholeVariableToken(source, out var tokenVarName) && context.HasVariable(tokenVarName))
            {
                return context.GetVariable(tokenVarName);
            }

            if (SimpleVariableNameRegex.IsMatch(source) && context.HasVariable(source))
            {
                return context.GetVariable(source);
            }

            if (SimpleVariableNameRegex.IsMatch(source))
            {
                return null;
            }

            var substituted = context.SubstituteVariables(source).Trim();
            if (substituted.Length == 0)
                return null;

            if (SimpleVariableNameRegex.IsMatch(substituted) && context.HasVariable(substituted))
            {
                return context.GetVariable(substituted);
            }

            if (SimpleVariableNameRegex.IsMatch(substituted))
            {
                return null;
            }

            return substituted;
        }

        private static bool TryExtractWholeVariableToken(string text, out string variableName)
        {
            variableName = string.Empty;

            if (text.Length >= 4 &&
                text.StartsWith("${", StringComparison.Ordinal) &&
                text.EndsWith("}", StringComparison.Ordinal))
            {
                var name = text.Substring(2, text.Length - 3).Trim();
                if (name.Length > 0)
                {
                    variableName = name;
                    return true;
                }
            }

            if (text.Length >= 4 &&
                text.StartsWith("{{", StringComparison.Ordinal) &&
                text.EndsWith("}}", StringComparison.Ordinal))
            {
                var name = text.Substring(2, text.Length - 4).Trim();
                if (name.Length > 0)
                {
                    variableName = name;
                    return true;
                }
            }

            return false;
        }

        private static List<ChoiceOption> ConvertRawValueToOptions(object rawValue, ScriptContext context)
        {
            return rawValue switch
            {
                List<ChoiceOption> choiceOptions => ResolveInlineOptions(choiceOptions, context),
                IEnumerable<ChoiceOption> choiceOptions => ResolveInlineOptions(choiceOptions, context),
                string text => BuildOptionsFromStrings(ParseStringItems(text), context),
                IEnumerable enumerable => BuildOptionsFromEnumerable(enumerable, context),
                _ => BuildOptionsFromStrings([rawValue.ToString() ?? string.Empty], context)
            };
        }

        private static List<ChoiceOption> ResolveInlineOptions(IEnumerable<ChoiceOption>? inlineOptions, ScriptContext context)
        {
            var resolved = new List<ChoiceOption>();
            if (inlineOptions == null)
                return resolved;

            foreach (var option in inlineOptions)
            {
                if (option == null)
                    continue;

                var label = context.SubstituteVariables(option.Label ?? string.Empty).Trim();
                var value = context.SubstituteVariables(option.Value ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(value))
                    continue;

                if (string.IsNullOrWhiteSpace(label))
                    label = value;
                if (string.IsNullOrWhiteSpace(value))
                    value = label;

                resolved.Add(new ChoiceOption
                {
                    Label = label,
                    Value = value
                });
            }

            return resolved;
        }

        private static List<ChoiceOption> BuildOptionsFromEnumerable(IEnumerable values, ScriptContext context)
        {
            var options = new List<ChoiceOption>();

            foreach (var item in values)
            {
                if (item == null)
                    continue;

                if (item is ChoiceOption option)
                {
                    options.AddRange(ResolveInlineOptions([option], context));
                    continue;
                }

                options.AddRange(BuildOptionsFromStrings([item.ToString() ?? string.Empty], context));
            }

            return options;
        }

        private static List<ChoiceOption> BuildOptionsFromStrings(IEnumerable<string> values, ScriptContext context)
        {
            var options = new List<ChoiceOption>();

            foreach (var value in values)
            {
                var resolved = context.SubstituteVariables(value ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(resolved))
                    continue;

                options.Add(new ChoiceOption
                {
                    Label = resolved,
                    Value = resolved
                });
            }

            return options;
        }

        private static List<string> ParseStringItems(string value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
                return new List<string>();

            if (trimmed.StartsWith("[", StringComparison.Ordinal) &&
                trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                try
                {
                    using var document = JsonDocument.Parse(trimmed);
                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var jsonItems = new List<string>();
                        foreach (var element in document.RootElement.EnumerateArray())
                        {
                            var text = element.ValueKind switch
                            {
                                JsonValueKind.String => element.GetString(),
                                JsonValueKind.Null => null,
                                _ => element.ToString()
                            };

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                jsonItems.Add(text);
                            }
                        }

                        if (jsonItems.Count > 0)
                            return jsonItems;
                    }
                }
                catch (JsonException)
                {
                    // Fall back to delimited parsing.
                }
            }

            var split = trimmed
                .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToList();

            if (split.Count == 0)
                split.Add(trimmed);

            return split;
        }
    }
}
