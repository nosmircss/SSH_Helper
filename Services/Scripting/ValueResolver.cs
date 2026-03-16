using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Centralized value resolution utilities shared across the scripting engine.
    /// Handles property resolution, collection semantics, and common expression patterns.
    /// </summary>
    public static class ValueResolver
    {
        private static readonly Regex TopLevelIndexExpressionPattern = new(
            @"^(?<name>[A-Za-z_]\w*)\[(?<index>[^\]]+)\]$",
            RegexOptions.Compiled);

        /// <summary>
        /// Resolves the .length property on a variable value.
        /// </summary>
        public static int ResolveLength(object? value)
        {
            return value switch
            {
                null => 0,
                List<string> list => list.Count,
                JsonArray array => array.Count,
                JsonObject obj => obj.Count,
                JsonElement element => ResolveJsonElementLength(element),
                string str => TryResolveStructuredLengthFromString(str, out var structuredLength) ? structuredLength : str.Length,
                ICollection collection => collection.Count,
                IEnumerable enumerable => CountEnumerable(enumerable),
                _ => 0
            };
        }

        /// <summary>
        /// Resolves a collection-oriented list view of a value.
        /// JSON arrays are expanded, newline-delimited strings become line items, and
        /// scalar strings remain a single-item list.
        /// </summary>
        public static List<string> ResolveCollectionItems(object? value)
        {
            if (value == null)
                return new List<string>();

            if (value is List<string> list)
                return new List<string>(list);

            if (value is JsonArray array)
            {
                var items = new List<string>(array.Count);
                foreach (var item in array)
                    items.Add(JsonUtilities.JsonNodeToStringValue(item));
                return items;
            }

            if (value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Array:
                    {
                        var items = new List<string>();
                        foreach (var item in element.EnumerateArray())
                            items.Add(JsonElementToStringValue(item));
                        return items;
                    }
                    case JsonValueKind.String:
                        return ResolveCollectionItems(element.GetString());
                }
            }

            if (value is string text)
            {
                if (TryParseJsonNode(text, out var node))
                {
                    if (node is JsonArray jsonArray)
                        return ResolveCollectionItems(jsonArray);

                    if (node is JsonObject)
                        return new List<string>();
                }

                var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                var items = new List<string>();
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        items.Add(line);
                }

                return items;
            }

            if (value is IEnumerable enumerable)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                    items.Add(item?.ToString() ?? string.Empty);
                return items;
            }

            return new List<string> { value.ToString() ?? string.Empty };
        }

        public static List<string> ResolveCollectionExpression(string expr, ScriptContext context)
        {
            expr = expr.Trim();
            if (IsSimpleIdentifier(expr) && !context.HasVariable(expr))
                return new List<string>();

            return ResolveCollectionItems(ResolveExpressionValue(expr, context));
        }

        /// <summary>
        /// Resolves a list-friendly view of a value for expression helpers.
        /// Unlike ResolveCollectionItems, scalar strings remain single values unless they are JSON arrays.
        /// </summary>
        public static List<string> ResolveListValue(object? value)
        {
            if (value == null)
                return new List<string>();

            if (value is List<string> list)
                return new List<string>(list);

            if (value is JsonArray array)
            {
                var items = new List<string>(array.Count);
                foreach (var item in array)
                    items.Add(JsonUtilities.JsonNodeToStringValue(item));
                return items;
            }

            if (value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Array:
                    {
                        var items = new List<string>();
                        foreach (var item in element.EnumerateArray())
                            items.Add(JsonElementToStringValue(item));
                        return items;
                    }
                    case JsonValueKind.String:
                        return ResolveListValue(element.GetString());
                }
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                    items.Add(item?.ToString() ?? string.Empty);
                return items;
            }

            var text = value.ToString() ?? string.Empty;
            if (TryParseJsonNode(text, out var node) && node is JsonArray jsonArray)
                return ResolveListValue(jsonArray);

            if (text.Contains('\n') || text.Contains('\r'))
                return new List<string>(text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None));

            return new List<string> { text };
        }

        /// <summary>
        /// Resolves basic expression values shared across condition evaluation and collection handling.
        /// </summary>
        public static object? ResolveExpressionValue(string expr, ScriptContext context)
        {
            expr = expr.Trim();

            var (handled, length) = TryResolveLengthExpression(expr, context.GetVariable);
            if (handled)
                return length;

            if (JsonUtilities.TryEvaluateJsonExpression(expr, context, out var jsonResult, normalizeStructured: false))
                return jsonResult;

            if (JsonUtilities.TryEvaluateFunctionExpression(expr, context, out var functionValue))
                return functionValue;

            if ((expr.StartsWith("\"", StringComparison.Ordinal) && expr.EndsWith("\"", StringComparison.Ordinal)) ||
                (expr.StartsWith("'", StringComparison.Ordinal) && expr.EndsWith("'", StringComparison.Ordinal)))
            {
                return context.SubstituteVariables(expr.Substring(1, expr.Length - 2));
            }

            if (expr.Contains("${", StringComparison.Ordinal) || expr.Contains("{{", StringComparison.Ordinal))
                return context.SubstituteVariables(expr);

            var directValue = context.GetVariable(expr);
            if (directValue != null || context.HasVariable(expr))
                return directValue;

            if (TryResolveIndexedExpressionValue(expr, context, out var indexedValue))
                return indexedValue;

            if (int.TryParse(expr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                return intValue;

            if (double.TryParse(expr, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                return doubleValue;

            return expr;
        }

        private static bool TryResolveIndexedExpressionValue(string expr, ScriptContext context, out object? value)
        {
            value = null;

            var match = TopLevelIndexExpressionPattern.Match(expr);
            if (!match.Success)
                return false;

            var variableName = match.Groups["name"].Value;
            var indexExpression = match.Groups["index"].Value.Trim();
            if (string.IsNullOrEmpty(variableName) || string.IsNullOrEmpty(indexExpression))
                return false;

            if (!TryResolveCollectionIndex(indexExpression, context, out var index))
                return false;

            var items = context.GetVariableList(variableName);
            if (index < 0 || index >= items.Count)
            {
                value = null;
                return true;
            }

            value = items[index];
            return true;
        }

        private static bool TryResolveCollectionIndex(string expr, ScriptContext context, out int index)
        {
            index = 0;
            if (int.TryParse(expr, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                return true;

            var rawValue = context.GetVariable(expr);
            if (rawValue == null && !context.HasVariable(expr))
                return false;

            return int.TryParse(rawValue?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
        }

        public static bool IsEmptyValue(object? value)
        {
            return value switch
            {
                null => true,
                string str => TryResolveStructuredEmptinessFromString(str, out var isStructuredEmpty) ? isStructuredEmpty : string.IsNullOrEmpty(str),
                List<string> list => list.Count == 0,
                JsonArray array => array.Count == 0,
                JsonObject obj => obj.Count == 0,
                JsonElement element => IsJsonElementEmpty(element),
                ICollection collection => collection.Count == 0,
                IEnumerable enumerable => !HasAny(enumerable),
                _ => false
            };
        }

        public static bool IsTruthyValue(object? value)
        {
            return value switch
            {
                null => false,
                bool boolValue => boolValue,
                int intValue => intValue != 0,
                long longValue => longValue != 0,
                double doubleValue => Math.Abs(doubleValue) > double.Epsilon,
                float floatValue => Math.Abs(floatValue) > float.Epsilon,
                string str => TryResolveStructuredEmptinessFromString(str, out var structuredEmpty)
                    ? !structuredEmpty
                    : !string.IsNullOrEmpty(str) && !str.Equals("false", StringComparison.OrdinalIgnoreCase),
                List<string> list => list.Count > 0,
                JsonArray array => array.Count > 0,
                JsonObject obj => obj.Count > 0,
                JsonElement element => IsJsonElementTruthy(element),
                ICollection collection => collection.Count > 0,
                IEnumerable enumerable => HasAny(enumerable),
                _ => true
            };
        }

        public static StringComparer ResolveComparisonComparer(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
                return StringComparer.OrdinalIgnoreCase;

            return mode.Trim().ToLowerInvariant() switch
            {
                "ordinal" => StringComparer.Ordinal,
                "ignore_case" => StringComparer.OrdinalIgnoreCase,
                "ignorecase" => StringComparer.OrdinalIgnoreCase,
                _ => StringComparer.OrdinalIgnoreCase
            };
        }

        public static bool CollectionContains(IEnumerable<string> values, string candidate, string? mode = null)
        {
            var comparer = ResolveComparisonComparer(mode);
            foreach (var value in values)
            {
                if (comparer.Equals(value ?? string.Empty, candidate))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// If expr ends with ".length", extracts the base name and resolves the length.
        /// Returns (true, length) if handled, (false, 0) otherwise.
        /// </summary>
        public static (bool handled, int length) TryResolveLengthExpression(
            string expr, Func<string, object?> getVariable)
        {
            if (!expr.EndsWith(".length", StringComparison.OrdinalIgnoreCase))
                return (false, 0);

            var baseName = expr.Substring(0, expr.Length - 7);
            var value = getVariable(baseName);
            return (true, ResolveLength(value));
        }

        private static int ResolveJsonElementLength(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => element.GetArrayLength(),
                JsonValueKind.Object => CountJsonObjectProperties(element),
                JsonValueKind.String => ResolveLength(element.GetString()),
                _ => 0
            };
        }

        private static bool IsJsonElementEmpty(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => true,
                JsonValueKind.Array => element.GetArrayLength() == 0,
                JsonValueKind.Object => CountJsonObjectProperties(element) == 0,
                JsonValueKind.String => IsEmptyValue(element.GetString()),
                _ => false
            };
        }

        private static bool IsJsonElementTruthy(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => false,
                JsonValueKind.False => false,
                JsonValueKind.True => true,
                JsonValueKind.Number => TryGetJsonElementNumber(element, out var number) && Math.Abs(number) > double.Epsilon,
                JsonValueKind.String => IsTruthyValue(element.GetString()),
                JsonValueKind.Array => element.GetArrayLength() > 0,
                JsonValueKind.Object => CountJsonObjectProperties(element) > 0,
                _ => true
            };
        }

        private static bool TryGetJsonElementNumber(JsonElement element, out double number)
        {
            if (element.TryGetDouble(out number))
                return true;

            number = 0;
            return false;
        }

        private static int CountJsonObjectProperties(JsonElement element)
        {
            var count = 0;
            foreach (var _ in element.EnumerateObject())
                count++;
            return count;
        }

        private static int CountEnumerable(IEnumerable enumerable)
        {
            var count = 0;
            foreach (var _ in enumerable)
                count++;
            return count;
        }

        private static bool HasAny(IEnumerable enumerable)
        {
            foreach (var _ in enumerable)
                return true;

            return false;
        }

        private static bool TryResolveStructuredLengthFromString(string value, out int length)
        {
            length = 0;
            if (!TryParseJsonNode(value, out var node))
                return false;

            length = node switch
            {
                JsonArray array => array.Count,
                JsonObject obj => obj.Count,
                _ => 0
            };
            return node is JsonArray or JsonObject;
        }

        private static bool TryResolveStructuredEmptinessFromString(string value, out bool isEmpty)
        {
            isEmpty = false;
            if (!TryParseJsonNode(value, out var node))
                return false;

            isEmpty = node switch
            {
                JsonArray array => array.Count == 0,
                JsonObject obj => obj.Count == 0,
                _ => false
            };
            return node is JsonArray or JsonObject;
        }

        private static bool TryParseJsonNode(string value, out JsonNode? node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            if (!(trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal)))
                return false;

            try
            {
                node = JsonNode.Parse(trimmed);
                return node != null;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsSimpleIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (!(char.IsLetter(value[0]) || value[0] == '_'))
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                var c = value[i];
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                    return false;
            }

            return true;
        }

        private static string JsonElementToStringValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
                _ => element.ToString()
            };
        }
    }
}
