using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

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
