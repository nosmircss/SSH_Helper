using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SSH_Helper.Services.Scripting.Commands;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Shared JSON conversion and utility methods used across the scripting engine.
    /// </summary>
    public static class JsonUtilities
    {
        /// <summary>
        /// Converts a .NET value to a JsonNode.
        /// </summary>
        public static JsonNode? ConvertToJsonNode(object? value)
        {
            if (value == null) return null;

            if (value is JsonNode node) return node.DeepClone();
            if (value is JsonElement element) return JsonNode.Parse(element.GetRawText());
            if (value is string str)
            {
                // Try to parse as JSON first
                if ((str.TrimStart().StartsWith("{") || str.TrimStart().StartsWith("[")) &&
                    (str.TrimEnd().EndsWith("}") || str.TrimEnd().EndsWith("]")))
                {
                    try
                    {
                        return JsonNode.Parse(str);
                    }
                    catch { /* Not valid JSON, fall through to string value */ }
                }
                return JsonValue.Create(str);
            }
            if (value is int i) return JsonValue.Create(i);
            if (value is long l) return JsonValue.Create(l);
            if (value is double d) return JsonValue.Create(d);
            if (value is bool b) return JsonValue.Create(b);
            if (value is List<string> list)
            {
                var arr = new JsonArray();
                foreach (var item in list)
                {
                    arr.Add(ParseJsonValueAsNode(item));
                }
                return arr;
            }

            // Default: serialize to JSON and parse
            try
            {
                var json = JsonSerializer.Serialize(value);
                return JsonNode.Parse(json);
            }
            catch
            {
                return JsonValue.Create(value.ToString());
            }
        }

        /// <summary>
        /// Parses a string value into the appropriate .NET type (number, boolean, object, array, or string).
        /// Used for JSON serialization contexts.
        /// </summary>
        public static object ParseJsonValue(string item)
        {
            if (string.IsNullOrEmpty(item))
                return item;

            var trimmed = item.Trim();

            // Check for boolean values
            if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;

            // Check for null
            if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null!;

            // Check for integer
            if (long.TryParse(trimmed, out var longVal))
                return longVal;

            // Check for floating point
            if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
                return doubleVal;

            // Check if it looks like JSON object or array
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(trimmed);
                }
                catch
                {
                    // Not valid JSON, return as string
                }
            }

            return item;
        }

        /// <summary>
        /// Parses a string value into a JsonNode (for tree manipulation contexts).
        /// </summary>
        public static JsonNode? ParseJsonValueAsNode(string item)
        {
            if (string.IsNullOrEmpty(item))
                return JsonValue.Create(item);

            var trimmed = item.Trim();

            // Check for boolean
            if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
                return JsonValue.Create(true);
            if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
                return JsonValue.Create(false);

            // Check for null
            if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            // Check for integer
            if (long.TryParse(trimmed, out var longVal))
                return JsonValue.Create(longVal);

            // Check for floating point
            if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
                return JsonValue.Create(doubleVal);

            // Check if it looks like JSON
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                try
                {
                    return JsonNode.Parse(trimmed);
                }
                catch { /* Not valid JSON, fall through */ }
            }

            return JsonValue.Create(item);
        }

        /// <summary>
        /// Converts a list to a JSON array string with automatic type detection.
        /// </summary>
        public static string ConvertToJsonArray(object? arrayValue, bool pretty)
        {
            if (arrayValue is not List<string> list)
                return "[]";

            var jsonArray = new List<object>();
            foreach (var item in list)
            {
                jsonArray.Add(ParseJsonValue(item));
            }

            return JsonSerializer.Serialize(jsonArray, new JsonSerializerOptions { WriteIndented = pretty });
        }

        /// <summary>
        /// Deep merges source into target (modifies target in place).
        /// </summary>
        public static void MergeInto(JsonObject target, JsonObject source)
        {
            foreach (var prop in source)
            {
                if (prop.Value is JsonObject sourceChild && target[prop.Key] is JsonObject targetChild)
                {
                    // Recursively merge nested objects
                    MergeInto(targetChild, sourceChild);
                }
                else
                {
                    // Override or add the value
                    target[prop.Key] = prop.Value?.DeepClone();
                }
            }
        }

        /// <summary>
        /// Converts a JsonNode to its appropriate .NET value.
        /// </summary>
        public static object? JsonNodeToValue(JsonNode? node)
        {
            if (node == null)
                return null;

            if (node is JsonValue jsonValue)
            {
                // Try to get the underlying value
                if (jsonValue.TryGetValue<string>(out var str))
                    return str;
                if (jsonValue.TryGetValue<long>(out var lng))
                    return lng;
                if (jsonValue.TryGetValue<double>(out var dbl))
                    return dbl;
                if (jsonValue.TryGetValue<bool>(out var bln))
                    return bln;

                return jsonValue.ToString();
            }

            // For objects and arrays, return as JSON string
            return node.ToJsonString();
        }

        /// <summary>
        /// Converts a JsonNode to a string representation for lists.
        /// </summary>
        public static string JsonNodeToStringValue(JsonNode? node)
        {
            if (node == null)
                return "null";

            if (node is JsonValue jv)
            {
                if (jv.TryGetValue<string>(out var str))
                    return str;
                if (jv.TryGetValue<long>(out var lng))
                    return lng.ToString();
                if (jv.TryGetValue<double>(out var dbl))
                    return dbl.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (jv.TryGetValue<bool>(out var bln))
                    return bln ? "true" : "false";
                return jv.ToString();
            }

            return node.ToJsonString();
        }

        /// <summary>
        /// Resolves a string value, handling quotes and variable substitution.
        /// </summary>
        public static string ResolveStringValue(string expr, ScriptContext context)
        {
            expr = expr.Trim();

            // Handle quoted strings
            if ((expr.StartsWith("\"") && expr.EndsWith("\"")) ||
                (expr.StartsWith("'") && expr.EndsWith("'")))
            {
                expr = expr.Substring(1, expr.Length - 2);
            }

            // Substitute variables
            return context.SubstituteVariables(expr);
        }

        /// <summary>
        /// Resolves a value for JSON, converting to appropriate types.
        /// </summary>
        public static object? ResolveJsonValue(string expr, ScriptContext context)
        {
            expr = expr.Trim();

            // Handle variable reference ${varname}
            if (expr.StartsWith("${") && expr.EndsWith("}"))
            {
                var varName = expr.Substring(2, expr.Length - 3);
                var value = context.GetVariable(varName);
                if (value != null)
                {
                    // If it's already a structured type, return as-is
                    if (value is List<string> list)
                    {
                        // Convert list to proper array with type detection
                        var arr = new List<object>();
                        foreach (var item in list)
                            arr.Add(ParseJsonValue(item));
                        return arr;
                    }
                    if (value is JsonElement || value is JsonObject || value is JsonArray)
                        return value;
                    // If it's a string that looks like JSON, parse it
                    var strVal = value.ToString() ?? "";
                    if (strVal.TrimStart().StartsWith("{") || strVal.TrimStart().StartsWith("["))
                    {
                        try
                        {
                            return JsonSerializer.Deserialize<JsonElement>(strVal);
                        }
                        catch { /* Not valid JSON, fall through */ }
                    }
                    return ParseJsonValue(strVal);
                }
            }

            // Handle quoted strings
            if ((expr.StartsWith("\"") && expr.EndsWith("\"")) ||
                (expr.StartsWith("'") && expr.EndsWith("'")))
            {
                var inner = expr.Substring(1, expr.Length - 2);
                return context.SubstituteVariables(inner);
            }

            // Handle nested JSON expressions used as values, e.g. json("a", json("b", 1))
            if (TryEvaluateJsonExpression(expr, context, out var nestedJsonValue))
            {
                return nestedJsonValue;
            }

            if (TryEvaluateFunctionExpression(expr, context, out var functionValue))
            {
                return functionValue;
            }

            // Handle plain variable name
            var varValue = context.GetVariable(expr);
            if (varValue != null)
            {
                if (varValue is List<string> list)
                {
                    var arr = new List<object>();
                    foreach (var item in list)
                        arr.Add(ParseJsonValue(item));
                    return arr;
                }
                return varValue;
            }

            // Substitute and parse
            var substituted = context.SubstituteVariables(expr);
            return ParseJsonValue(substituted);
        }

        public static bool TryEvaluateFunctionExpression(string expr, ScriptContext context, out object? value)
        {
            value = null;

            if (!TryParseFunctionCall(expr, out var functionName, out var inner))
                return false;

            switch (functionName.ToLowerInvariant())
            {
                case "length":
                {
                    var resolved = ResolveJsonValue(inner, context);
                    value = ValueResolver.ResolveLength(resolved);
                    return true;
                }
                case "list":
                {
                    var args = SplitTopLevelCommas(inner);
                    var items = new List<string>(args.Count);
                    foreach (var arg in args)
                    {
                        items.Add(ResolveJsonValue(arg, context)?.ToString() ?? string.Empty);
                    }

                    value = items;
                    return true;
                }
                case "trim":
                {
                    var resolved = ResolveJsonValue(inner, context);
                    value = resolved?.ToString()?.Trim() ?? string.Empty;
                    return true;
                }
                case "upper":
                {
                    var resolved = ResolveJsonValue(inner, context);
                    value = resolved?.ToString()?.ToUpperInvariant() ?? string.Empty;
                    return true;
                }
                case "lower":
                {
                    var resolved = ResolveJsonValue(inner, context);
                    value = resolved?.ToString()?.ToLowerInvariant() ?? string.Empty;
                    return true;
                }
                case "replace":
                {
                    var args = SplitTopLevelCommas(inner);
                    if (args.Count < 3)
                        return false;

                    var source = ResolveJsonValue(args[0], context)?.ToString() ?? string.Empty;
                    var oldValue = ResolveJsonValue(args[1], context)?.ToString() ?? string.Empty;
                    var newValue = ResolveJsonValue(args[2], context)?.ToString() ?? string.Empty;
                    value = source.Replace(oldValue, newValue, StringComparison.Ordinal);
                    return true;
                }
                case "split":
                {
                    var args = SplitTopLevelCommas(inner);
                    if (args.Count == 0)
                        return false;

                    var source = ResolveJsonValue(args[0], context)?.ToString() ?? string.Empty;
                    var delimiter = args.Count > 1 ? ResolveJsonValue(args[1], context)?.ToString() ?? "," : ",";
                    if (delimiter.Length == 0)
                    {
                        var chars = new List<string>(source.Length);
                        foreach (var c in source)
                            chars.Add(c.ToString());
                        value = chars;
                        return true;
                    }

                    value = new List<string>(source.Split(new[] { delimiter }, StringSplitOptions.None));
                    return true;
                }
                case "join":
                {
                    var args = SplitTopLevelCommas(inner);
                    if (args.Count == 0)
                        return false;

                    var source = ResolveJsonValue(args[0], context);
                    var delimiter = args.Count > 1 ? ResolveJsonValue(args[1], context)?.ToString() ?? "," : ",";
                    value = string.Join(delimiter, ValueResolver.ResolveListValue(source));
                    return true;
                }
                case "substring":
                {
                    var args = SplitTopLevelCommas(inner);
                    if (args.Count < 2)
                        return false;

                    var source = ResolveJsonValue(args[0], context)?.ToString() ?? string.Empty;
                    if (!TryResolveInt(ResolveJsonValue(args[1], context), out var start))
                        start = 0;

                    if (start < 0)
                        start = 0;
                    if (start >= source.Length)
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (args.Count >= 3)
                    {
                        if (!TryResolveInt(ResolveJsonValue(args[2], context), out var length))
                            length = source.Length - start;

                        if (length <= 0)
                        {
                            value = string.Empty;
                            return true;
                        }

                        if (start + length > source.Length)
                            length = source.Length - start;

                        value = source.Substring(start, length);
                        return true;
                    }

                    value = source.Substring(start);
                    return true;
                }
                case "sort":
                {
                    var args = SplitTopLevelCommas(inner);
                    if (args.Count == 0)
                        return false;

                    var list = ValueResolver.ResolveListValue(ResolveJsonValue(args[0], context));
                    list.Sort(StringComparer.OrdinalIgnoreCase);
                    var order = args.Count > 1 ? ResolveJsonValue(args[1], context)?.ToString() ?? "asc" : "asc";
                    if (order.Equals("desc", StringComparison.OrdinalIgnoreCase))
                        list.Reverse();
                    value = list;
                    return true;
                }
                case "compact":
                {
                    var list = ValueResolver.ResolveListValue(ResolveJsonValue(inner, context));
                    value = list.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
                    return true;
                }
                case "trim_all":
                {
                    var list = ValueResolver.ResolveListValue(ResolveJsonValue(inner, context));
                    value = list.Select(item => item?.Trim() ?? string.Empty).ToList();
                    return true;
                }
                case "lower_all":
                {
                    var list = ValueResolver.ResolveListValue(ResolveJsonValue(inner, context));
                    value = list.Select(item => item?.ToLowerInvariant() ?? string.Empty).ToList();
                    return true;
                }
                case "upper_all":
                {
                    var list = ValueResolver.ResolveListValue(ResolveJsonValue(inner, context));
                    value = list.Select(item => item?.ToUpperInvariant() ?? string.Empty).ToList();
                    return true;
                }
                case "distinct":
                {
                    var args = SplitTopLevelCommas(inner);
                    if (args.Count == 0)
                        return false;

                    var list = ValueResolver.ResolveListValue(ResolveJsonValue(args[0], context));
                    var comparer = ValueResolver.ResolveComparisonComparer(args.Count > 1 ? ResolveJsonValue(args[1], context)?.ToString() : null);
                    value = DistinctPreservingOrder(list, comparer);
                    return true;
                }
                case "push_unique":
                {
                    var args = SplitTopLevelCommas(inner);
                    if (args.Count < 2)
                        return false;

                    var list = ValueResolver.ResolveListValue(ResolveJsonValue(args[0], context));
                    var candidate = ResolveJsonValue(args[1], context)?.ToString() ?? string.Empty;
                    var comparer = ValueResolver.ResolveComparisonComparer(args.Count > 2 ? ResolveJsonValue(args[2], context)?.ToString() : null);
                    if (!list.Contains(candidate, comparer))
                        list.Add(candidate);
                    value = list;
                    return true;
                }
                case "first":
                {
                    var list = ValueResolver.ResolveListValue(ResolveJsonValue(inner, context));
                    value = list.Count > 0 ? list[0] : null;
                    return true;
                }
                case "last":
                {
                    var list = ValueResolver.ResolveListValue(ResolveJsonValue(inner, context));
                    value = list.Count > 0 ? list[^1] : null;
                    return true;
                }
                case "indexof":
                {
                    var args = SplitTopLevelCommas(inner);
                    if (args.Count < 2)
                        return false;

                    var list = ValueResolver.ResolveListValue(ResolveJsonValue(args[0], context));
                    var searchValue = ResolveJsonValue(args[1], context)?.ToString() ?? string.Empty;
                    value = list.FindIndex(item => string.Equals(item, searchValue, StringComparison.OrdinalIgnoreCase));
                    return true;
                }
                case "concat":
                {
                    var args = SplitTopLevelCommas(inner);
                    var combined = new List<string>();
                    foreach (var arg in args)
                        combined.AddRange(ValueResolver.ResolveListValue(ResolveJsonValue(arg, context)));
                    value = combined;
                    return true;
                }
                default:
                    return false;
            }
        }

        private static bool TryParseFunctionCall(string expr, out string functionName, out string inner)
        {
            functionName = string.Empty;
            inner = string.Empty;
            if (string.IsNullOrWhiteSpace(expr))
                return false;

            var openIndex = expr.IndexOf('(');
            if (openIndex <= 0 || !expr.EndsWith(")", StringComparison.Ordinal))
                return false;

            var name = expr.Substring(0, openIndex).Trim();
            if (string.IsNullOrEmpty(name))
                return false;

            foreach (var c in name)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.'))
                    return false;
            }

            var depth = 0;
            var inString = false;
            var stringChar = '\0';
            for (int i = openIndex; i < expr.Length; i++)
            {
                var c = expr[i];
                if ((c == '"' || c == '\'') && (i == 0 || expr[i - 1] != '\\'))
                {
                    if (!inString)
                    {
                        inString = true;
                        stringChar = c;
                    }
                    else if (stringChar == c)
                    {
                        inString = false;
                    }
                    continue;
                }

                if (inString)
                    continue;

                if (c == '(')
                    depth++;
                else if (c == ')')
                    depth--;

                if (depth == 0 && i < expr.Length - 1)
                    return false;

                if (depth < 0)
                    return false;
            }

            if (depth != 0)
                return false;

            functionName = name;
            inner = expr.Substring(openIndex + 1, expr.Length - openIndex - 2).Trim();
            return true;
        }

        private static bool TryResolveInt(object? value, out int number)
        {
            number = 0;
            if (value == null)
                return false;

            if (value is int i)
            {
                number = i;
                return true;
            }

            if (value is long l)
            {
                number = (int)l;
                return true;
            }

            var text = value.ToString();
            return int.TryParse(text, out number);
        }

        private static List<string> DistinctPreservingOrder(IEnumerable<string> values, IEqualityComparer<string> comparer)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(comparer);

            foreach (var value in values)
            {
                var safeValue = value ?? string.Empty;
                if (seen.Add(safeValue))
                    result.Add(safeValue);
            }

            return result;
        }

        /// <summary>
        /// Evaluates json(...) and json.*(...) expressions into structured values.
        /// </summary>
        public static bool TryEvaluateJsonExpression(string expr, ScriptContext context, out object? value, bool normalizeStructured = true)
        {
            value = null;

            if (string.IsNullOrWhiteSpace(expr))
                return false;

            // json(...) constructor
            if (expr.StartsWith("json(", StringComparison.OrdinalIgnoreCase) &&
                expr.EndsWith(")", StringComparison.Ordinal))
            {
                var inner = expr.Substring(5, expr.Length - 6).Trim();
                value = MaybeNormalize(JsonFunctions.Constructor(inner, context), normalizeStructured);
                return true;
            }

            // json.*(...) function dispatch
            if (expr.StartsWith("json.", StringComparison.OrdinalIgnoreCase))
            {
                var parenIdx = expr.IndexOf('(');
                if (parenIdx > 5 && expr.EndsWith(")", StringComparison.Ordinal))
                {
                    var funcName = expr.Substring(5, parenIdx - 5);
                    var inner = expr.Substring(parenIdx + 1, expr.Length - parenIdx - 2).Trim();
                    return TryDispatchJsonFunction(funcName, inner, context, out value, normalizeStructured);
                }
            }

            return false;
        }

        private static bool TryDispatchJsonFunction(string funcName, string args, ScriptContext context, out object? value, bool normalizeStructured = true)
        {
            switch (funcName.ToLowerInvariant())
            {
                case "get":
                    value = MaybeNormalize(JsonFunctions.Get(args, context), normalizeStructured);
                    return true;
                case "set":
                    value = MaybeNormalize(JsonFunctions.Set(args, context), normalizeStructured);
                    return true;
                case "delete":
                    value = MaybeNormalize(JsonFunctions.Delete(args, context), normalizeStructured);
                    return true;
                case "merge":
                    value = MaybeNormalize(JsonFunctions.MergeVariadic(args, context), normalizeStructured);
                    return true;
                case "format":
                    value = MaybeNormalize(JsonFunctions.Format(args, context), normalizeStructured);
                    return true;
                case "exists":
                    value = MaybeNormalize(JsonFunctions.Exists(args, context), normalizeStructured);
                    return true;
                case "len":
                    value = MaybeNormalize(JsonFunctions.Len(args, context), normalizeStructured);
                    return true;
                case "type":
                    value = MaybeNormalize(JsonFunctions.Type(args, context), normalizeStructured);
                    return true;
                case "keys":
                    value = MaybeNormalize(JsonFunctions.Keys(args, context), normalizeStructured);
                    return true;
                case "values":
                    value = MaybeNormalize(JsonFunctions.Values(args, context), normalizeStructured);
                    return true;
                case "items":
                    value = MaybeNormalize(JsonFunctions.Items(args, context), normalizeStructured);
                    return true;
                case "push":
                    value = MaybeNormalize(JsonFunctions.Push(args, context), normalizeStructured);
                    return true;
                case "pop":
                    value = MaybeNormalize(JsonFunctions.Pop(args, context), normalizeStructured);
                    return true;
                case "last":
                    value = MaybeNormalize(JsonFunctions.Last(args, context), normalizeStructured);
                    return true;
                case "unshift":
                    value = MaybeNormalize(JsonFunctions.Unshift(args, context), normalizeStructured);
                    return true;
                case "shift":
                    value = MaybeNormalize(JsonFunctions.Shift(args, context), normalizeStructured);
                    return true;
                case "first":
                    value = MaybeNormalize(JsonFunctions.First(args, context), normalizeStructured);
                    return true;
                case "slice":
                    value = MaybeNormalize(JsonFunctions.Slice(args, context), normalizeStructured);
                    return true;
                case "concat":
                    value = MaybeNormalize(JsonFunctions.Concat(args, context), normalizeStructured);
                    return true;
                case "indexof":
                    value = MaybeNormalize(JsonFunctions.IndexOf(args, context), normalizeStructured);
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

        private static object? MaybeNormalize(object? value, bool normalizeStructured)
        {
            return normalizeStructured ? NormalizeStructuredJsonResult(value) : value;
        }

        private static object? NormalizeStructuredJsonResult(object? value)
        {
            if (value is not string strValue)
                return value;

            var trimmed = strValue.Trim();
            if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
            {
                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(trimmed);
                }
                catch
                {
                    // Keep original string if parsing fails.
                }
            }

            return value;
        }

        /// <summary>
        /// Gets a JsonObject from a variable or expression.
        /// </summary>
        public static JsonObject GetJsonObject(string expr, ScriptContext context)
        {
            object? value = null;

            // Check if it's a variable reference
            if (expr.StartsWith("${") && expr.EndsWith("}"))
            {
                var varName = expr.Substring(2, expr.Length - 3);
                value = context.GetVariable(varName);
            }
            else
            {
                value = context.GetVariable(expr);
            }

            // Try to convert to JsonObject
            if (value is JsonObject jsonObj)
                return jsonObj;

            if (value is string strVal && strVal.TrimStart().StartsWith("{"))
            {
                try
                {
                    return JsonNode.Parse(strVal)?.AsObject() ?? new JsonObject();
                }
                catch { /* Not valid JSON */ }
            }

            // Try parsing the expression directly if it looks like JSON
            var substituted = context.SubstituteVariables(expr);
            if (substituted.TrimStart().StartsWith("{"))
            {
                try
                {
                    return JsonNode.Parse(substituted)?.AsObject() ?? new JsonObject();
                }
                catch { /* Not valid JSON */ }
            }

            return new JsonObject();
        }

        /// <summary>
        /// Gets a JsonNode from a variable or expression.
        /// </summary>
        public static JsonNode? GetJsonNode(string expr, ScriptContext context)
        {
            object? value = null;

            // Check if it's a variable reference ${varname}
            if (expr.StartsWith("${") && expr.EndsWith("}"))
            {
                var varName = expr.Substring(2, expr.Length - 3);
                value = context.GetVariable(varName);
            }
            else
            {
                // Try as plain variable name
                value = context.GetVariable(expr);
            }

            // Convert to JsonNode
            if (value is JsonNode node)
                return node;

            if (value is JsonObject jsonObj)
                return jsonObj;

            if (value is JsonArray jsonArr)
                return jsonArr;

            if (value is JsonElement jsonElement)
            {
                try
                {
                    return JsonNode.Parse(jsonElement.GetRawText());
                }
                catch { /* Not valid JSON */ }
            }

            // Handle Dictionary<string, object> (from parse command)
            if (value is IDictionary<string, object> dict)
            {
                try
                {
                    var jsonString = JsonSerializer.Serialize(dict);
                    return JsonNode.Parse(jsonString);
                }
                catch { /* Not valid JSON */ }
            }

            if (value is string strVal)
            {
                var trimmed = strVal.Trim();
                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                {
                    try
                    {
                        return JsonNode.Parse(trimmed);
                    }
                    catch { /* Not valid JSON */ }
                }
            }

            // Try substituting variables and parsing
            var substituted = context.SubstituteVariables(expr);
            var subTrimmed = substituted.Trim();
            if (subTrimmed.StartsWith("{") || subTrimmed.StartsWith("["))
            {
                try
                {
                    return JsonNode.Parse(subTrimmed);
                }
                catch { /* Not valid JSON */ }
            }

            return null;
        }

        /// <summary>
        /// Builds a JSON object from key-value pairs.
        /// </summary>
        public static string BuildJsonObject(string argsString, ScriptContext context, bool pretty)
        {
            var args = SplitTopLevelCommas(argsString);
            var obj = new Dictionary<string, object?>();

            // Process pairs
            for (int i = 0; i + 1 < args.Count; i += 2)
            {
                var key = ResolveStringValue(args[i], context);
                var valueExpr = args[i + 1];
                var value = ResolveJsonValue(valueExpr, context);
                obj[key] = value;
            }

            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = pretty });
        }

        /// <summary>
        /// Finds the first comma at the top level (not inside parentheses, braces, or strings).
        /// </summary>
        public static int FindTopLevelComma(string str)
        {
            int depth = 0;
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];

                if (inString)
                {
                    if (c == stringChar && (i == 0 || str[i - 1] != '\\'))
                        inString = false;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringChar = c;
                    continue;
                }

                if (c == '(' || c == '{' || c == '[')
                    depth++;
                else if (c == ')' || c == '}' || c == ']')
                    depth--;
                else if (c == ',' && depth == 0)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Splits a string by commas at the top level (respecting nested structures and strings).
        /// </summary>
        public static List<string> SplitTopLevelCommas(string str)
        {
            var parts = new List<string>();
            int depth = 0;
            bool inString = false;
            char stringChar = '\0';
            int start = 0;

            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];

                if (inString)
                {
                    if (c == stringChar && (i == 0 || str[i - 1] != '\\'))
                        inString = false;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringChar = c;
                    continue;
                }

                if (c == '(' || c == '{' || c == '[')
                    depth++;
                else if (c == ')' || c == '}' || c == ']')
                    depth--;
                else if (c == ',' && depth == 0)
                {
                    parts.Add(str.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }

            // Add the last part
            if (start < str.Length)
                parts.Add(str.Substring(start).Trim());

            return parts;
        }
    }
}
