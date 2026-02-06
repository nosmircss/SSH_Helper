using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Implements all json.* function calls used by the set command.
    /// </summary>
    internal static class JsonFunctions
    {
        /// <summary>
        /// Universal JSON constructor: json(...)
        /// - json(list) or json(list, pretty) - convert list to JSON array
        /// - json([], item1, item2, ...) - create array from items
        /// - json("key1", val1, "key2", val2, ...) - create object from key-value pairs
        /// </summary>
        public static object Constructor(string argsString, ScriptContext context)
        {
            if (string.IsNullOrWhiteSpace(argsString))
                return "{}";

            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            bool pretty = args.RemoveAll(a => a.Trim().Equals("pretty", StringComparison.OrdinalIgnoreCase)) > 0;

            if (args.Count == 0)
                return "{}";

            var firstArg = args[0].Trim();

            // Check if first arg is [] for explicit array creation
            if (firstArg == "[]")
            {
                var jsonArray = new JsonArray();
                for (int i = 1; i < args.Count; i++)
                {
                    var value = JsonUtilities.ResolveJsonValue(args[i], context);
                    jsonArray.Add(JsonUtilities.ConvertToJsonNode(value));
                }
                return jsonArray.ToJsonString(new JsonSerializerOptions { WriteIndented = pretty });
            }

            // Check if first arg is a list variable (for array conversion)
            var listValue = context.GetVariable(firstArg);
            if (listValue is List<string> list)
            {
                return JsonUtilities.ConvertToJsonArray(list, pretty);
            }

            // Check if it's a variable reference to a list
            if (firstArg.StartsWith("${") && firstArg.EndsWith("}"))
            {
                var varName = firstArg.Substring(2, firstArg.Length - 3);
                var varValue = context.GetVariable(varName);
                if (varValue is List<string> varList)
                {
                    return JsonUtilities.ConvertToJsonArray(varList, pretty);
                }
            }

            // Otherwise, treat as key-value pairs for object creation
            var obj = new Dictionary<string, object?>();
            for (int i = 0; i + 1 < args.Count; i += 2)
            {
                var key = JsonUtilities.ResolveStringValue(args[i], context);
                var value = JsonUtilities.ResolveJsonValue(args[i + 1], context);
                obj[key] = value;
            }

            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = pretty });
        }

        /// <summary>
        /// json.get(json, path, default?) - Extract value with optional default
        /// </summary>
        public static object? Get(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return null;

            var jsonExpr = args[0].Trim();
            var pathExpr = args[1].Trim();
            object? defaultValue = args.Count >= 3 ? JsonUtilities.ResolveJsonValue(args[2], context) : null;

            var jsonNode = JsonUtilities.GetJsonNode(jsonExpr, context);
            if (jsonNode == null)
                return defaultValue;

            var path = JsonUtilities.ResolveStringValue(pathExpr, context);
            var result = JsonPathNavigator.Navigate(jsonNode, path);

            // Return default if path doesn't exist (result is null)
            return result ?? defaultValue;
        }

        /// <summary>
        /// json.set(json, path, value) - Set value at path, creating intermediate objects as needed
        /// </summary>
        public static object? Set(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 3)
                return null;

            var jsonExpr = args[0].Trim();
            var pathExpr = args[1].Trim();
            var valueExpr = args[2].Trim();

            // Get the source JSON (clone it to avoid modifying original)
            var jsonNode = JsonUtilities.GetJsonNode(jsonExpr, context);
            JsonNode root;
            if (jsonNode == null)
            {
                root = new JsonObject();
            }
            else
            {
                root = jsonNode.DeepClone();
            }

            var path = JsonUtilities.ResolveStringValue(pathExpr, context);
            var value = JsonUtilities.ResolveJsonValue(valueExpr, context);
            var valueNode = JsonUtilities.ConvertToJsonNode(value);

            // Navigate and set the value
            JsonPathNavigator.SetAtPath(root, path, valueNode);

            return root.ToJsonString();
        }

        /// <summary>
        /// json.delete(json, path) - Remove key or element at path
        /// </summary>
        public static object? Delete(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return null;

            var jsonExpr = args[0].Trim();
            var pathExpr = args[1].Trim();

            var jsonNode = JsonUtilities.GetJsonNode(jsonExpr, context);
            if (jsonNode == null)
                return null;

            var root = jsonNode.DeepClone();
            var path = JsonUtilities.ResolveStringValue(pathExpr, context);

            JsonPathNavigator.DeleteAtPath(root, path);

            return root.ToJsonString();
        }

        /// <summary>
        /// json.merge(obj1, obj2, ...) - Deep merge multiple objects (variadic)
        /// </summary>
        public static object MergeVariadic(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return "{}";

            JsonObject result = new JsonObject();

            foreach (var arg in args)
            {
                var obj = JsonUtilities.GetJsonObject(arg.Trim(), context);
                JsonUtilities.MergeInto(result, obj);
            }

            return result.ToJsonString();
        }

        /// <summary>
        /// json.format(json, style?) - Format JSON (pretty by default, compact if specified)
        /// </summary>
        public static string Format(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return "";

            var jsonExpr = args[0].Trim();
            bool compact = args.Count >= 2 && args[1].Trim().Equals("compact", StringComparison.OrdinalIgnoreCase);

            var jsonNode = JsonUtilities.GetJsonNode(jsonExpr, context);
            if (jsonNode == null)
            {
                var substituted = context.SubstituteVariables(jsonExpr);
                var trimmed = substituted.Trim();

                if ((trimmed.StartsWith("\"") && trimmed.EndsWith("\"")) ||
                    (trimmed.StartsWith("'") && trimmed.EndsWith("'")))
                {
                    trimmed = trimmed.Substring(1, trimmed.Length - 2);
                }

                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(trimmed);
                        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = !compact });
                    }
                    catch
                    {
                        return trimmed;
                    }
                }
                return substituted;
            }

            return jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = !compact });
        }

        /// <summary>
        /// json.exists(json, path) - Check if path exists (returns true/false)
        /// </summary>
        public static bool Exists(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return false;

            var jsonExpr = args[0].Trim();
            var pathExpr = args[1].Trim();

            var jsonNode = JsonUtilities.GetJsonNode(jsonExpr, context);
            if (jsonNode == null)
                return false;

            var path = JsonUtilities.ResolveStringValue(pathExpr, context);

            return JsonPathNavigator.PathExists(jsonNode, path);
        }

        /// <summary>
        /// json.len(json, path?) - Get array length or object key count
        /// </summary>
        public static int Len(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return 0;

            var jsonExpr = args[0].Trim();
            var jsonNode = JsonUtilities.GetJsonNode(jsonExpr, context);

            if (jsonNode == null)
                return 0;

            // If path is provided, navigate to it first
            if (args.Count >= 2)
            {
                var pathExpr = args[1].Trim();
                var path = JsonUtilities.ResolveStringValue(pathExpr, context);
                var result = JsonPathNavigator.Navigate(jsonNode, path);

                if (result is string jsonStr && (jsonStr.TrimStart().StartsWith("[") || jsonStr.TrimStart().StartsWith("{")))
                {
                    try
                    {
                        jsonNode = JsonNode.Parse(jsonStr);
                    }
                    catch
                    {
                        return 0;
                    }
                }
                else if (result is JsonNode resultNode)
                {
                    jsonNode = resultNode;
                }
                else
                {
                    return 0;
                }
            }

            if (jsonNode is JsonArray arr)
                return arr.Count;
            if (jsonNode is JsonObject obj)
                return obj.Count;

            return 0;
        }

        /// <summary>
        /// json.type(json, path?) - Get value type
        /// Returns: "object", "array", "string", "number", "boolean", "null"
        /// </summary>
        public static string Type(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return "null";

            var jsonExpr = args[0].Trim();
            var jsonNode = JsonUtilities.GetJsonNode(jsonExpr, context);

            if (jsonNode == null)
                return "null";

            // If path is provided, navigate to it first
            if (args.Count >= 2)
            {
                var pathExpr = args[1].Trim();
                var path = JsonUtilities.ResolveStringValue(pathExpr, context);
                var result = JsonPathNavigator.Navigate(jsonNode, path);

                if (result == null)
                    return "null";

                if (result is string jsonStr)
                {
                    var trimmed = jsonStr.TrimStart();
                    if (trimmed.StartsWith("{"))
                        return "object";
                    if (trimmed.StartsWith("["))
                        return "array";
                    return "string";
                }
                if (result is JsonNode resultNode)
                    jsonNode = resultNode;
                else if (result is long || result is int || result is double)
                    return "number";
                else if (result is bool)
                    return "boolean";
                else
                    return "string";
            }

            return JsonPathNavigator.GetNodeType(jsonNode);
        }

        /// <summary>
        /// json.keys(json, path?) - Get object keys as list
        /// </summary>
        public static List<string> Keys(string argsString, ScriptContext context)
        {
            var result = new List<string>();
            var jsonNode = NavigateToTarget(argsString, context, expectObject: true);

            if (jsonNode is JsonObject obj)
            {
                foreach (var prop in obj)
                {
                    result.Add(prop.Key);
                }
            }

            return result;
        }

        /// <summary>
        /// json.values(json, path?) - Get object values as list
        /// </summary>
        public static List<string> Values(string argsString, ScriptContext context)
        {
            var result = new List<string>();
            var jsonNode = NavigateToTarget(argsString, context, expectObject: true);

            if (jsonNode is JsonObject obj)
            {
                foreach (var prop in obj)
                {
                    result.Add(JsonUtilities.JsonNodeToStringValue(prop.Value));
                }
            }

            return result;
        }

        /// <summary>
        /// json.items(json, path?) - Extract array elements or object entries
        /// For arrays: returns list of elements
        /// For objects: returns list of {"key": k, "value": v} entries
        /// </summary>
        public static List<string> Items(string argsString, ScriptContext context)
        {
            var result = new List<string>();
            var jsonNode = NavigateToTarget(argsString, context, expectObject: false);

            // Handle arrays
            if (jsonNode is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    result.Add(JsonUtilities.JsonNodeToStringValue(item));
                }
            }
            // Handle objects - return key/value entries
            else if (jsonNode is JsonObject obj)
            {
                foreach (var prop in obj)
                {
                    var entry = new JsonObject
                    {
                        ["key"] = prop.Key,
                        ["value"] = prop.Value?.DeepClone()
                    };
                    result.Add(entry.ToJsonString());
                }
            }

            return result;
        }

        /// <summary>
        /// json.push(arr, value) - Append value to array
        /// </summary>
        public static object? Push(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return null;

            var arrExpr = args[0].Trim();
            var valueExpr = args[1].Trim();

            var arrNode = JsonUtilities.GetJsonNode(arrExpr, context);
            JsonArray arr;

            if (arrNode is JsonArray existingArr)
            {
                arr = JsonNode.Parse(existingArr.ToJsonString())!.AsArray();
            }
            else
            {
                arr = new JsonArray();
            }

            var value = JsonUtilities.ResolveJsonValue(valueExpr, context);
            arr.Add(JsonUtilities.ConvertToJsonNode(value));

            return arr.ToJsonString();
        }

        /// <summary>
        /// json.pop(arr) - Remove and return last element
        /// </summary>
        public static object? Pop(string argsString, ScriptContext context)
        {
            var arrNode = JsonUtilities.GetJsonNode(argsString.Trim(), context);
            if (arrNode is not JsonArray arr || arr.Count == 0)
                return null;

            var lastIdx = arr.Count - 1;
            var lastItem = arr[lastIdx];

            // Return just the value (the array modification is not persisted - user must use json.delete or reassign)
            return JsonUtilities.JsonNodeToValue(lastItem);
        }

        /// <summary>
        /// json.unshift(arr, value) - Prepend value to array
        /// </summary>
        public static object? Unshift(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return null;

            var arrExpr = args[0].Trim();
            var valueExpr = args[1].Trim();

            var arrNode = JsonUtilities.GetJsonNode(arrExpr, context);
            JsonArray arr;

            if (arrNode is JsonArray existingArr)
            {
                arr = JsonNode.Parse(existingArr.ToJsonString())!.AsArray();
            }
            else
            {
                arr = new JsonArray();
            }

            var value = JsonUtilities.ResolveJsonValue(valueExpr, context);
            var newArr = new JsonArray();
            newArr.Add(JsonUtilities.ConvertToJsonNode(value));
            foreach (var item in arr)
            {
                newArr.Add(item?.DeepClone());
            }

            return newArr.ToJsonString();
        }

        /// <summary>
        /// json.shift(arr) - Remove and return first element
        /// </summary>
        public static object? Shift(string argsString, ScriptContext context)
        {
            var arrNode = JsonUtilities.GetJsonNode(argsString.Trim(), context);
            if (arrNode is not JsonArray arr || arr.Count == 0)
                return null;

            var firstItem = arr[0];
            return JsonUtilities.JsonNodeToValue(firstItem);
        }

        /// <summary>
        /// json.slice(arr, start, end?) - Extract subset of array
        /// Supports negative indices (from end)
        /// </summary>
        public static object? Slice(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return "[]";

            var arrExpr = args[0].Trim();
            var startExpr = args[1].Trim();

            var arrNode = JsonUtilities.GetJsonNode(arrExpr, context);
            if (arrNode is not JsonArray arr)
                return "[]";

            var startVal = JsonUtilities.ResolveJsonValue(startExpr, context);
            int start = startVal is int si ? si : (startVal is long sl ? (int)sl : 0);

            int end = arr.Count;
            if (args.Count >= 3)
            {
                var endExpr = args[2].Trim();
                var endVal = JsonUtilities.ResolveJsonValue(endExpr, context);
                end = endVal is int ei ? ei : (endVal is long el ? (int)el : arr.Count);
            }

            // Handle negative indices
            if (start < 0)
                start = Math.Max(0, arr.Count + start);
            if (end < 0)
                end = Math.Max(0, arr.Count + end);

            // Clamp to bounds
            start = Math.Max(0, Math.Min(start, arr.Count));
            end = Math.Max(0, Math.Min(end, arr.Count));

            var result = new JsonArray();
            for (int i = start; i < end; i++)
            {
                result.Add(arr[i]?.DeepClone());
            }

            return result.ToJsonString();
        }

        /// <summary>
        /// json.concat(arr1, arr2, ...) - Concatenate multiple arrays
        /// </summary>
        public static object? Concat(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            var result = new JsonArray();

            foreach (var arg in args)
            {
                var arrNode = JsonUtilities.GetJsonNode(arg.Trim(), context);
                if (arrNode is JsonArray arr)
                {
                    foreach (var item in arr)
                    {
                        result.Add(item?.DeepClone());
                    }
                }
            }

            return result.ToJsonString();
        }

        /// <summary>
        /// json.indexOf(arr, value) - Find index of value (-1 if not found)
        /// </summary>
        public static int IndexOf(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return -1;

            var arrExpr = args[0].Trim();
            var valueExpr = args[1].Trim();

            var arrNode = JsonUtilities.GetJsonNode(arrExpr, context);
            if (arrNode is not JsonArray arr)
                return -1;

            var searchValue = JsonUtilities.ResolveJsonValue(valueExpr, context);
            var searchStr = searchValue?.ToString() ?? "";

            for (int i = 0; i < arr.Count; i++)
            {
                var item = arr[i];
                var itemStr = JsonUtilities.JsonNodeToStringValue(item);
                if (itemStr == searchStr)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Helper: Navigate to a target JSON node with optional path.
        /// Used by keys, values, items functions.
        /// </summary>
        private static JsonNode? NavigateToTarget(string argsString, ScriptContext context, bool expectObject)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return null;

            var jsonExpr = args[0].Trim();
            var jsonNode = JsonUtilities.GetJsonNode(jsonExpr, context);

            if (jsonNode == null)
                return null;

            // If path is provided, navigate to it first
            if (args.Count >= 2)
            {
                var pathExpr = args[1].Trim();
                var path = JsonUtilities.ResolveStringValue(pathExpr, context);
                var navResult = JsonPathNavigator.Navigate(jsonNode, path);

                var startChar = expectObject ? "{" : "[";
                if (navResult is string jsonStr)
                {
                    var trimmed = jsonStr.TrimStart();
                    if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                    {
                        try
                        {
                            return JsonNode.Parse(jsonStr);
                        }
                        catch
                        {
                            return null;
                        }
                    }
                    return null;
                }
                else if (navResult is JsonNode resultNode)
                {
                    return resultNode;
                }
                else
                {
                    return null;
                }
            }

            return jsonNode;
        }
    }
}
