using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Sets or manipulates a variable value.
    /// Supports: "var = value", "var = var + 1", "var = var - 1", "var = length(other)",
    /// "var = push(array, value)", JSON functions with dot notation (json.get, json.set, etc.),
    /// "var.path = value" (nested assignment)
    /// </summary>
    public class SetCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.Set))
                return Task.FromResult(CommandResult.Fail("Set command has no assignment expression"));

            // Parse the assignment: "variable = expression"
            var parts = step.Set.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                return Task.FromResult(CommandResult.Fail($"Invalid set syntax: '{step.Set}'. Expected 'variable = value'"));

            var varName = parts[0].Trim();
            var expression = parts[1].Trim();

            if (string.IsNullOrEmpty(varName))
                return Task.FromResult(CommandResult.Fail("Variable name cannot be empty"));

            // Check for nested assignment (dot notation): "obj.key.subkey = value"
            if (varName.Contains('.'))
            {
                return HandleNestedAssignment(varName, expression, context);
            }

            // Evaluate the expression
            var value = EvaluateExpression(expression, context);

            // Set the variable
            context.SetVariable(varName, value);

            context.EmitOutput($"Set {varName} = {value}", ScriptOutputType.Debug);

            return Task.FromResult(CommandResult.Ok());
        }

        /// <summary>
        /// Handles nested assignment using dot notation (e.g., "obj.key.subkey = value").
        /// Creates intermediate objects as needed.
        /// </summary>
        private Task<CommandResult> HandleNestedAssignment(string path, string expression, ScriptContext context)
        {
            var pathParts = path.Split('.');
            var rootName = pathParts[0];

            // Get or create the root object
            var existingRoot = context.GetVariable(rootName);
            JsonObject rootObj;

            if (existingRoot is JsonObject existingJsonObj)
            {
                rootObj = existingJsonObj;
            }
            else if (existingRoot is string jsonStr && jsonStr.TrimStart().StartsWith("{"))
            {
                try
                {
                    rootObj = JsonNode.Parse(jsonStr)?.AsObject() ?? new JsonObject();
                }
                catch
                {
                    rootObj = new JsonObject();
                }
            }
            else
            {
                rootObj = new JsonObject();
            }

            // Navigate to the parent of the target key, creating intermediate objects
            var current = rootObj;
            for (int i = 1; i < pathParts.Length - 1; i++)
            {
                var key = pathParts[i];
                if (current[key] is JsonObject childObj)
                {
                    current = childObj;
                }
                else
                {
                    var newObj = new JsonObject();
                    current[key] = newObj;
                    current = newObj;
                }
            }

            // Set the final value
            var finalKey = pathParts[pathParts.Length - 1];
            var value = EvaluateExpression(expression, context);

            // Convert value to JsonNode
            current[finalKey] = ConvertToJsonNode(value);

            // Store the updated root object
            context.SetVariable(rootName, rootObj);

            context.EmitOutput($"Set {path} = {value}", ScriptOutputType.Debug);

            return Task.FromResult(CommandResult.Ok());
        }

        /// <summary>
        /// Converts a value to a JsonNode for nested assignment.
        /// </summary>
        private JsonNode? ConvertToJsonNode(object? value)
        {
            if (value == null) return null;

            if (value is JsonNode node) return node.DeepClone();
            if (value is JsonElement element) return JsonNode.Parse(element.GetRawText());
            if (value is string str)
            {
                // Try to parse as JSON first
                if ((str.TrimStart().StartsWith("{") || str.TrimStart().StartsWith("[")) && str.TrimEnd().EndsWith("}") || str.TrimEnd().EndsWith("]"))
                {
                    try
                    {
                        return JsonNode.Parse(str);
                    }
                    catch { }
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
                    arr.Add(ParseJsonValue(item));
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

        private object? EvaluateExpression(string expression, ScriptContext context)
        {
            expression = expression.Trim();

            // Check for function calls: length(var), trim(var), etc.
            if (expression.StartsWith("length(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(7, expression.Length - 8);
                var resolved = ResolveValue(inner, context);
                if (resolved is System.Collections.Generic.List<string> list)
                    return list.Count;
                return resolved?.ToString()?.Length ?? 0;
            }

            if (expression.StartsWith("trim(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(5, expression.Length - 6);
                var resolved = ResolveValue(inner, context);
                return resolved?.ToString()?.Trim() ?? string.Empty;
            }

            if (expression.StartsWith("upper(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(6, expression.Length - 7);
                var resolved = ResolveValue(inner, context);
                return resolved?.ToString()?.ToUpperInvariant() ?? string.Empty;
            }

            if (expression.StartsWith("lower(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(6, expression.Length - 7);
                var resolved = ResolveValue(inner, context);
                return resolved?.ToString()?.ToLowerInvariant() ?? string.Empty;
            }

            // push(array, value) - adds value to array and returns the array
            if (expression.StartsWith("push(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(5, expression.Length - 6);
                var commaIdx = FindTopLevelComma(inner);
                if (commaIdx > 0)
                {
                    var arrayName = inner.Substring(0, commaIdx).Trim();
                    var valueExpr = inner.Substring(commaIdx + 1).Trim();

                    // Get or create the array
                    var existing = context.GetVariable(arrayName);
                    List<string> array;
                    if (existing is List<string> existingList)
                    {
                        array = existingList;
                    }
                    else
                    {
                        array = new List<string>();
                    }

                    // Resolve and add the value
                    var resolvedValue = context.SubstituteVariables(valueExpr);
                    array.Add(resolvedValue);

                    // Update the array variable
                    context.SetVariable(arrayName, array);

                    return array;
                }
            }

            // ============================================
            // JSON Functions with dot notation (json.*)
            // ============================================

            // json(...) - Universal constructor for objects and arrays
            if (expression.StartsWith("json(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(5, expression.Length - 6).Trim();
                return JsonConstructor(inner, context);
            }

            // json.get(json, path, default?) - Extract value with optional default
            if (expression.StartsWith("json.get(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(9, expression.Length - 10).Trim();
                return JsonGet(inner, context);
            }

            // json.set(json, path, value) - Set value at path
            if (expression.StartsWith("json.set(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(9, expression.Length - 10).Trim();
                return JsonSet(inner, context);
            }

            // json.delete(json, path) - Remove key or element at path
            if (expression.StartsWith("json.delete(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(12, expression.Length - 13).Trim();
                return JsonDelete(inner, context);
            }

            // json.merge(obj1, obj2, ...) - Deep merge multiple objects
            if (expression.StartsWith("json.merge(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(11, expression.Length - 12).Trim();
                return JsonMergeVariadic(inner, context);
            }

            // json.format(json, style?) - Format JSON (pretty/compact)
            if (expression.StartsWith("json.format(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(12, expression.Length - 13).Trim();
                return JsonFormat(inner, context);
            }

            // json.exists(json, path) - Check if path exists
            if (expression.StartsWith("json.exists(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(12, expression.Length - 13).Trim();
                return JsonExists(inner, context);
            }

            // json.len(json, path?) - Get array length or object key count
            if (expression.StartsWith("json.len(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(9, expression.Length - 10).Trim();
                return JsonLen(inner, context);
            }

            // json.type(json, path?) - Get value type
            if (expression.StartsWith("json.type(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(10, expression.Length - 11).Trim();
                return JsonType(inner, context);
            }

            // json.keys(json, path?) - Get object keys as list
            if (expression.StartsWith("json.keys(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(10, expression.Length - 11).Trim();
                return JsonKeys(inner, context);
            }

            // json.values(json, path?) - Get object values as list
            if (expression.StartsWith("json.values(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(12, expression.Length - 13).Trim();
                return JsonValues(inner, context);
            }

            // json.items(json, path?) - Extract array elements or object entries
            if (expression.StartsWith("json.items(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(11, expression.Length - 12).Trim();
                return JsonItems(inner, context);
            }

            // json.push(arr, value) - Append to array
            if (expression.StartsWith("json.push(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(10, expression.Length - 11).Trim();
                return JsonPush(inner, context);
            }

            // json.pop(arr) - Remove and return last element
            if (expression.StartsWith("json.pop(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(9, expression.Length - 10).Trim();
                return JsonPop(inner, context);
            }

            // json.unshift(arr, value) - Prepend to array
            if (expression.StartsWith("json.unshift(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(13, expression.Length - 14).Trim();
                return JsonUnshift(inner, context);
            }

            // json.shift(arr) - Remove and return first element
            if (expression.StartsWith("json.shift(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(11, expression.Length - 12).Trim();
                return JsonShift(inner, context);
            }

            // json.slice(arr, start, end?) - Extract subset of array
            if (expression.StartsWith("json.slice(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(11, expression.Length - 12).Trim();
                return JsonSlice(inner, context);
            }

            // json.concat(arr1, arr2, ...) - Concatenate arrays
            if (expression.StartsWith("json.concat(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(12, expression.Length - 13).Trim();
                return JsonConcat(inner, context);
            }

            // json.indexOf(arr, value) - Find index of value
            if (expression.StartsWith("json.indexOf(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(13, expression.Length - 14).Trim();
                return JsonIndexOf(inner, context);
            }

            // Check for arithmetic: var + 1, var - 1, var * 2, var / 3, var % 10
            if (expression.Contains(" + ") || expression.Contains(" - ") ||
                expression.Contains(" * ") || expression.Contains(" / ") ||
                expression.Contains(" % "))
            {
                return EvaluateArithmetic(expression, context);
            }

            // Check for string concatenation with ${var}
            if (expression.Contains("${"))
            {
                return context.SubstituteVariables(expression);
            }

            // Check for quoted string literal
            if ((expression.StartsWith("\"") && expression.EndsWith("\"")) ||
                (expression.StartsWith("'") && expression.EndsWith("'")))
            {
                var literal = expression.Substring(1, expression.Length - 2);
                return context.SubstituteVariables(literal);
            }

            // Try to parse as number
            if (int.TryParse(expression, out var intVal))
                return intVal;
            if (double.TryParse(expression, out var doubleVal))
                return doubleVal;

            // Check if it's a variable reference (without ${})
            var varValue = context.GetVariable(expression);
            if (varValue != null)
                return varValue;

            // Return as literal string
            return context.SubstituteVariables(expression);
        }

        private object? EvaluateArithmetic(string expression, ScriptContext context)
        {
            // Handle simple arithmetic: "var + 1", "var - 1", "var * 2", "var / 3", "var % 10"
            // Note: Only single operator expressions supported. For complex math, chain multiple set commands.

            // Check multiplication first (higher precedence in typical usage)
            string[] mulParts = expression.Split(new[] { " * " }, 2, StringSplitOptions.None);
            if (mulParts.Length == 2)
            {
                var left = ResolveNumeric(mulParts[0].Trim(), context);
                var right = ResolveNumeric(mulParts[1].Trim(), context);
                return left * right;
            }

            // Check division
            string[] divParts = expression.Split(new[] { " / " }, 2, StringSplitOptions.None);
            if (divParts.Length == 2)
            {
                var left = ResolveNumeric(divParts[0].Trim(), context);
                var right = ResolveNumeric(divParts[1].Trim(), context);
                if (right == 0)
                    return 0; // Division by zero returns 0
                return left / right;
            }

            // Check modulo
            string[] modParts = expression.Split(new[] { " % " }, 2, StringSplitOptions.None);
            if (modParts.Length == 2)
            {
                var left = ResolveNumeric(modParts[0].Trim(), context);
                var right = ResolveNumeric(modParts[1].Trim(), context);
                if (right == 0)
                    return 0; // Modulo by zero returns 0
                return left % right;
            }

            // Check addition
            string[] addParts = expression.Split(new[] { " + " }, 2, StringSplitOptions.None);
            if (addParts.Length == 2)
            {
                var left = ResolveNumeric(addParts[0].Trim(), context);
                var right = ResolveNumeric(addParts[1].Trim(), context);
                return left + right;
            }

            // Check subtraction
            string[] subParts = expression.Split(new[] { " - " }, 2, StringSplitOptions.None);
            if (subParts.Length == 2)
            {
                var left = ResolveNumeric(subParts[0].Trim(), context);
                var right = ResolveNumeric(subParts[1].Trim(), context);
                return left - right;
            }

            return 0;
        }

        private object? ResolveValue(string expr, ScriptContext context)
        {
            expr = expr.Trim();

            // Variable substitution
            if (expr.Contains("${"))
            {
                return context.SubstituteVariables(expr);
            }

            // Direct variable reference
            var value = context.GetVariable(expr);
            if (value != null)
                return value;

            return expr;
        }

        private double ResolveNumeric(string expr, ScriptContext context)
        {
            expr = expr.Trim();

            // Try direct numeric parse
            if (double.TryParse(expr, out var num))
                return num;

            // Try variable lookup
            var value = context.GetVariable(expr);
            if (value != null && double.TryParse(value.ToString(), out var varNum))
                return varNum;

            // Try with variable substitution
            var substituted = context.SubstituteVariables(expr);
            if (double.TryParse(substituted, out var subNum))
                return subNum;

            return 0;
        }

        /// <summary>
        /// Finds the first comma at the top level (not inside parentheses or braces).
        /// </summary>
        private int FindTopLevelComma(string str)
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
        private List<string> SplitTopLevelCommas(string str)
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

        /// <summary>
        /// Converts a list to a JSON array string with automatic type detection.
        /// </summary>
        private string ConvertToJsonArray(object? arrayValue, bool pretty)
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
        /// Parses a string value into the appropriate JSON type (number, boolean, object, array, or string).
        /// </summary>
        private object ParseJsonValue(string item)
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
        /// Builds a JSON object from key-value pairs: json_object(key1, value1, key2, value2, ...)
        /// </summary>
        private string BuildJsonObject(string argsString, ScriptContext context, bool pretty)
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
        /// Resolves a string value, handling quotes and variable substitution.
        /// </summary>
        private string ResolveStringValue(string expr, ScriptContext context)
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
        private object? ResolveJsonValue(string expr, ScriptContext context)
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
                        catch { }
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
        private JsonObject GetJsonObject(string expr, ScriptContext context)
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
                catch { }
            }

            // Try parsing the expression directly if it looks like JSON
            var substituted = context.SubstituteVariables(expr);
            if (substituted.TrimStart().StartsWith("{"))
            {
                try
                {
                    return JsonNode.Parse(substituted)?.AsObject() ?? new JsonObject();
                }
                catch { }
            }

            return new JsonObject();
        }

        /// <summary>
        /// Deep merges source into target (modifies target in place).
        /// </summary>
        private void MergeInto(JsonObject target, JsonObject source)
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
        /// Gets a JsonNode from a variable or expression.
        /// </summary>
        private JsonNode? GetJsonNode(string expr, ScriptContext context)
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

            if (value is string strVal)
            {
                var trimmed = strVal.Trim();
                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                {
                    try
                    {
                        return JsonNode.Parse(trimmed);
                    }
                    catch { }
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
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Navigates a JSON path like "data.items[0].name" and returns the value.
        /// </summary>
        private object? NavigateJsonPath(JsonNode? node, string path)
        {
            if (node == null || string.IsNullOrEmpty(path))
                return JsonNodeToValue(node);

            // Split path into segments, handling array indices
            var current = node;
            var segments = ParseJsonPath(path);

            foreach (var segment in segments)
            {
                if (current == null)
                    return null;

                if (segment.IsArrayIndex)
                {
                    // Array index access
                    if (current is JsonArray arr)
                    {
                        if (segment.Index >= 0 && segment.Index < arr.Count)
                        {
                            current = arr[segment.Index];
                        }
                        else
                        {
                            return null; // Index out of bounds
                        }
                    }
                    else
                    {
                        return null; // Not an array
                    }
                }
                else
                {
                    // Object property access
                    if (current is JsonObject obj)
                    {
                        if (obj.TryGetPropertyValue(segment.Key, out var propValue))
                        {
                            current = propValue;
                        }
                        else
                        {
                            return null; // Property not found
                        }
                    }
                    else
                    {
                        return null; // Not an object
                    }
                }
            }

            return JsonNodeToValue(current);
        }

        /// <summary>
        /// Parses a JSON path like "data.items[0].name" into segments.
        /// </summary>
        private List<PathSegment> ParseJsonPath(string path)
        {
            var segments = new List<PathSegment>();
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];

                if (c == '.')
                {
                    // End of property name
                    if (current.Length > 0)
                    {
                        segments.Add(new PathSegment { Key = current.ToString() });
                        current.Clear();
                    }
                }
                else if (c == '[')
                {
                    // Start of array index
                    if (current.Length > 0)
                    {
                        segments.Add(new PathSegment { Key = current.ToString() });
                        current.Clear();
                    }

                    // Find the closing bracket
                    int closeIdx = path.IndexOf(']', i + 1);
                    if (closeIdx > i + 1)
                    {
                        var indexStr = path.Substring(i + 1, closeIdx - i - 1);
                        if (int.TryParse(indexStr, out var index))
                        {
                            segments.Add(new PathSegment { IsArrayIndex = true, Index = index });
                        }
                        i = closeIdx; // Skip past the closing bracket
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            // Add any remaining segment
            if (current.Length > 0)
            {
                segments.Add(new PathSegment { Key = current.ToString() });
            }

            return segments;
        }

        /// <summary>
        /// Represents a segment in a JSON path.
        /// </summary>
        private struct PathSegment
        {
            public string Key;
            public bool IsArrayIndex;
            public int Index;
        }

        /// <summary>
        /// Converts a JsonNode to its appropriate .NET value.
        /// </summary>
        private object? JsonNodeToValue(JsonNode? node)
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

        // ============================================
        // New JSON API Implementation (json.* functions)
        // ============================================

        /// <summary>
        /// Universal JSON constructor: json(...)
        /// - json(list) or json(list, pretty) - convert list to JSON array
        /// - json([], item1, item2, ...) - create array from items
        /// - json("key1", val1, "key2", val2, ...) - create object from key-value pairs
        /// - Add "pretty" anywhere for formatted output
        /// </summary>
        private object JsonConstructor(string argsString, ScriptContext context)
        {
            if (string.IsNullOrWhiteSpace(argsString))
                return "{}";

            var args = SplitTopLevelCommas(argsString);
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
                    var value = ResolveJsonValue(args[i], context);
                    jsonArray.Add(ConvertToJsonNode(value));
                }
                return jsonArray.ToJsonString(new JsonSerializerOptions { WriteIndented = pretty });
            }

            // Check if first arg is a list variable (for array conversion)
            var listValue = context.GetVariable(firstArg);
            if (listValue is List<string> list)
            {
                return ConvertToJsonArray(list, pretty);
            }

            // Check if it's a variable reference to a list
            if (firstArg.StartsWith("${") && firstArg.EndsWith("}"))
            {
                var varName = firstArg.Substring(2, firstArg.Length - 3);
                var varValue = context.GetVariable(varName);
                if (varValue is List<string> varList)
                {
                    return ConvertToJsonArray(varList, pretty);
                }
            }

            // Otherwise, treat as key-value pairs for object creation
            var obj = new Dictionary<string, object?>();
            for (int i = 0; i + 1 < args.Count; i += 2)
            {
                var key = ResolveStringValue(args[i], context);
                var value = ResolveJsonValue(args[i + 1], context);
                obj[key] = value;
            }

            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = pretty });
        }

        /// <summary>
        /// json.get(json, path, default?) - Extract value with optional default
        /// </summary>
        private object? JsonGet(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return null;

            var jsonExpr = args[0].Trim();
            var pathExpr = args[1].Trim();
            object? defaultValue = args.Count >= 3 ? ResolveJsonValue(args[2], context) : null;

            var jsonNode = GetJsonNode(jsonExpr, context);
            if (jsonNode == null)
                return defaultValue;

            var path = ResolveStringValue(pathExpr, context);
            var result = NavigateJsonPath(jsonNode, path);

            // Return default if path doesn't exist (result is null)
            return result ?? defaultValue;
        }

        /// <summary>
        /// json.set(json, path, value) - Set value at path, creating intermediate objects as needed
        /// </summary>
        private object? JsonSet(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count < 3)
                return null;

            var jsonExpr = args[0].Trim();
            var pathExpr = args[1].Trim();
            var valueExpr = args[2].Trim();

            // Get the source JSON (clone it to avoid modifying original)
            var jsonNode = GetJsonNode(jsonExpr, context);
            JsonNode root;
            if (jsonNode == null)
            {
                root = new JsonObject();
            }
            else
            {
                root = jsonNode.DeepClone();
            }

            var path = ResolveStringValue(pathExpr, context);
            var value = ResolveJsonValue(valueExpr, context);
            var valueNode = ConvertToJsonNode(value);

            // Navigate and set the value
            SetJsonPath(root, path, valueNode);

            return root.ToJsonString();
        }

        /// <summary>
        /// Sets a value at a JSON path, creating intermediate objects/arrays as needed.
        /// </summary>
        private void SetJsonPath(JsonNode root, string path, JsonNode? value)
        {
            var segments = ParseJsonPath(path);
            if (segments.Count == 0)
                return;

            JsonNode? current = root;

            // Navigate to parent of target
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var segment = segments[i];
                var nextSegment = segments[i + 1];

                if (segment.IsArrayIndex)
                {
                    if (current is JsonArray arr)
                    {
                        // Extend array if needed
                        while (arr.Count <= segment.Index)
                            arr.Add(nextSegment.IsArrayIndex ? new JsonArray() : new JsonObject());
                        current = arr[segment.Index];
                    }
                    else
                    {
                        return; // Can't navigate
                    }
                }
                else
                {
                    if (current is JsonObject obj)
                    {
                        if (!obj.ContainsKey(segment.Key))
                        {
                            obj[segment.Key] = nextSegment.IsArrayIndex ? new JsonArray() : new JsonObject();
                        }
                        current = obj[segment.Key];
                    }
                    else
                    {
                        return; // Can't navigate
                    }
                }
            }

            // Set the final value
            var lastSegment = segments[segments.Count - 1];
            if (lastSegment.IsArrayIndex)
            {
                if (current is JsonArray finalArr)
                {
                    while (finalArr.Count <= lastSegment.Index)
                        finalArr.Add(null);
                    finalArr[lastSegment.Index] = value;
                }
            }
            else
            {
                if (current is JsonObject finalObj)
                {
                    finalObj[lastSegment.Key] = value;
                }
            }
        }

        /// <summary>
        /// json.delete(json, path) - Remove key or element at path
        /// </summary>
        private object? JsonDelete(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return null;

            var jsonExpr = args[0].Trim();
            var pathExpr = args[1].Trim();

            var jsonNode = GetJsonNode(jsonExpr, context);
            if (jsonNode == null)
                return null;

            var root = jsonNode.DeepClone();
            var path = ResolveStringValue(pathExpr, context);

            DeleteJsonPath(root, path);

            return root.ToJsonString();
        }

        /// <summary>
        /// Deletes a value at a JSON path.
        /// </summary>
        private void DeleteJsonPath(JsonNode root, string path)
        {
            var segments = ParseJsonPath(path);
            if (segments.Count == 0)
                return;

            JsonNode? current = root;

            // Navigate to parent of target
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var segment = segments[i];

                if (segment.IsArrayIndex)
                {
                    if (current is JsonArray arr && segment.Index >= 0 && segment.Index < arr.Count)
                        current = arr[segment.Index];
                    else
                        return;
                }
                else
                {
                    if (current is JsonObject obj && obj.TryGetPropertyValue(segment.Key, out var propValue))
                        current = propValue;
                    else
                        return;
                }
            }

            // Delete the target
            var lastSegment = segments[segments.Count - 1];
            if (lastSegment.IsArrayIndex)
            {
                if (current is JsonArray finalArr && lastSegment.Index >= 0 && lastSegment.Index < finalArr.Count)
                    finalArr.RemoveAt(lastSegment.Index);
            }
            else
            {
                if (current is JsonObject finalObj)
                    finalObj.Remove(lastSegment.Key);
            }
        }

        /// <summary>
        /// json.merge(obj1, obj2, ...) - Deep merge multiple objects (variadic)
        /// </summary>
        private object JsonMergeVariadic(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return "{}";

            JsonObject result = new JsonObject();

            foreach (var arg in args)
            {
                var obj = GetJsonObject(arg.Trim(), context);
                MergeInto(result, obj);
            }

            return result.ToJsonString();
        }

        /// <summary>
        /// json.format(json, style?) - Format JSON (pretty by default, compact if specified)
        /// </summary>
        private string JsonFormat(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return "";

            var jsonExpr = args[0].Trim();
            bool compact = args.Count >= 2 && args[1].Trim().Equals("compact", StringComparison.OrdinalIgnoreCase);

            var jsonNode = GetJsonNode(jsonExpr, context);
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
        private bool JsonExists(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return false;

            var jsonExpr = args[0].Trim();
            var pathExpr = args[1].Trim();

            var jsonNode = GetJsonNode(jsonExpr, context);
            if (jsonNode == null)
                return false;

            var path = ResolveStringValue(pathExpr, context);

            // Try to navigate the path
            return JsonPathExists(jsonNode, path);
        }

        /// <summary>
        /// Checks if a JSON path exists (distinguishes between null value and missing key).
        /// </summary>
        private bool JsonPathExists(JsonNode node, string path)
        {
            var segments = ParseJsonPath(path);
            JsonNode? current = node;

            foreach (var segment in segments)
            {
                if (current == null)
                    return false;

                if (segment.IsArrayIndex)
                {
                    if (current is JsonArray arr)
                    {
                        if (segment.Index < 0 || segment.Index >= arr.Count)
                            return false;
                        current = arr[segment.Index];
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    if (current is JsonObject obj)
                    {
                        if (!obj.ContainsKey(segment.Key))
                            return false;
                        current = obj[segment.Key];
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// json.len(json, path?) - Get array length or object key count
        /// </summary>
        private int JsonLen(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return 0;

            var jsonExpr = args[0].Trim();
            var jsonNode = GetJsonNode(jsonExpr, context);

            if (jsonNode == null)
                return 0;

            // If path is provided, navigate to it first
            if (args.Count >= 2)
            {
                var pathExpr = args[1].Trim();
                var path = ResolveStringValue(pathExpr, context);
                var result = NavigateJsonPath(jsonNode, path);

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
        private string JsonType(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return "null";

            var jsonExpr = args[0].Trim();
            var jsonNode = GetJsonNode(jsonExpr, context);

            if (jsonNode == null)
                return "null";

            // If path is provided, navigate to it first
            if (args.Count >= 2)
            {
                var pathExpr = args[1].Trim();
                var path = ResolveStringValue(pathExpr, context);
                var result = NavigateJsonPath(jsonNode, path);

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

            return GetJsonNodeType(jsonNode);
        }

        private string GetJsonNodeType(JsonNode? node)
        {
            if (node == null)
                return "null";
            if (node is JsonObject)
                return "object";
            if (node is JsonArray)
                return "array";
            if (node is JsonValue jv)
            {
                if (jv.TryGetValue<bool>(out _))
                    return "boolean";
                if (jv.TryGetValue<long>(out _) || jv.TryGetValue<double>(out _))
                    return "number";
                return "string";
            }
            return "null";
        }

        /// <summary>
        /// json.keys(json, path?) - Get object keys as list
        /// </summary>
        private List<string> JsonKeys(string argsString, ScriptContext context)
        {
            var result = new List<string>();
            var args = SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return result;

            var jsonExpr = args[0].Trim();
            var jsonNode = GetJsonNode(jsonExpr, context);

            if (jsonNode == null)
                return result;

            // If path is provided, navigate to it first
            if (args.Count >= 2)
            {
                var pathExpr = args[1].Trim();
                var path = ResolveStringValue(pathExpr, context);
                var navResult = NavigateJsonPath(jsonNode, path);

                if (navResult is string jsonStr && jsonStr.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        jsonNode = JsonNode.Parse(jsonStr);
                    }
                    catch
                    {
                        return result;
                    }
                }
                else if (navResult is JsonNode resultNode)
                {
                    jsonNode = resultNode;
                }
                else
                {
                    return result;
                }
            }

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
        private List<string> JsonValues(string argsString, ScriptContext context)
        {
            var result = new List<string>();
            var args = SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return result;

            var jsonExpr = args[0].Trim();
            var jsonNode = GetJsonNode(jsonExpr, context);

            if (jsonNode == null)
                return result;

            // If path is provided, navigate to it first
            if (args.Count >= 2)
            {
                var pathExpr = args[1].Trim();
                var path = ResolveStringValue(pathExpr, context);
                var navResult = NavigateJsonPath(jsonNode, path);

                if (navResult is string jsonStr && jsonStr.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        jsonNode = JsonNode.Parse(jsonStr);
                    }
                    catch
                    {
                        return result;
                    }
                }
                else if (navResult is JsonNode resultNode)
                {
                    jsonNode = resultNode;
                }
                else
                {
                    return result;
                }
            }

            if (jsonNode is JsonObject obj)
            {
                foreach (var prop in obj)
                {
                    result.Add(JsonNodeToStringValue(prop.Value));
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a JsonNode to a string representation for lists.
        /// </summary>
        private string JsonNodeToStringValue(JsonNode? node)
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
        /// json.items(json, path?) - Extract array elements or object entries
        /// For arrays: returns list of elements
        /// For objects: returns list of {"key": k, "value": v} entries
        /// </summary>
        private List<string> JsonItems(string argsString, ScriptContext context)
        {
            var result = new List<string>();
            var args = SplitTopLevelCommas(argsString);
            if (args.Count == 0)
                return result;

            var jsonExpr = args[0].Trim();
            var jsonNode = GetJsonNode(jsonExpr, context);

            if (jsonNode == null)
                return result;

            // If path is provided, navigate to it first
            if (args.Count >= 2)
            {
                var pathExpr = args[1].Trim();
                var path = ResolveStringValue(pathExpr, context);
                var navResult = NavigateJsonPath(jsonNode, path);

                if (navResult is string jsonStr && (jsonStr.TrimStart().StartsWith("[") || jsonStr.TrimStart().StartsWith("{")))
                {
                    try
                    {
                        jsonNode = JsonNode.Parse(jsonStr);
                    }
                    catch
                    {
                        return result;
                    }
                }
                else if (navResult is JsonNode resultNode)
                {
                    jsonNode = resultNode;
                }
                else
                {
                    return result;
                }
            }

            // Handle arrays (same as before)
            if (jsonNode is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    result.Add(JsonNodeToStringValue(item));
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
        private object? JsonPush(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return null;

            var arrExpr = args[0].Trim();
            var valueExpr = args[1].Trim();

            var arrNode = GetJsonNode(arrExpr, context);
            JsonArray arr;

            if (arrNode is JsonArray existingArr)
            {
                arr = JsonNode.Parse(existingArr.ToJsonString())!.AsArray();
            }
            else
            {
                arr = new JsonArray();
            }

            var value = ResolveJsonValue(valueExpr, context);
            arr.Add(ConvertToJsonNode(value));

            return arr.ToJsonString();
        }

        /// <summary>
        /// json.pop(arr) - Remove and return last element
        /// </summary>
        private object? JsonPop(string argsString, ScriptContext context)
        {
            var arrNode = GetJsonNode(argsString.Trim(), context);
            if (arrNode is not JsonArray arr || arr.Count == 0)
                return null;

            var lastIdx = arr.Count - 1;
            var lastItem = arr[lastIdx];

            // Return just the value (the array modification is not persisted - user must use json.delete or reassign)
            return JsonNodeToValue(lastItem);
        }

        /// <summary>
        /// json.unshift(arr, value) - Prepend value to array
        /// </summary>
        private object? JsonUnshift(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return null;

            var arrExpr = args[0].Trim();
            var valueExpr = args[1].Trim();

            var arrNode = GetJsonNode(arrExpr, context);
            JsonArray arr;

            if (arrNode is JsonArray existingArr)
            {
                arr = JsonNode.Parse(existingArr.ToJsonString())!.AsArray();
            }
            else
            {
                arr = new JsonArray();
            }

            var value = ResolveJsonValue(valueExpr, context);
            var newArr = new JsonArray();
            newArr.Add(ConvertToJsonNode(value));
            foreach (var item in arr)
            {
                newArr.Add(item?.DeepClone());
            }

            return newArr.ToJsonString();
        }

        /// <summary>
        /// json.shift(arr) - Remove and return first element
        /// </summary>
        private object? JsonShift(string argsString, ScriptContext context)
        {
            var arrNode = GetJsonNode(argsString.Trim(), context);
            if (arrNode is not JsonArray arr || arr.Count == 0)
                return null;

            var firstItem = arr[0];
            return JsonNodeToValue(firstItem);
        }

        /// <summary>
        /// json.slice(arr, start, end?) - Extract subset of array
        /// Supports negative indices (from end)
        /// </summary>
        private object? JsonSlice(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return "[]";

            var arrExpr = args[0].Trim();
            var startExpr = args[1].Trim();

            var arrNode = GetJsonNode(arrExpr, context);
            if (arrNode is not JsonArray arr)
                return "[]";

            var startVal = ResolveJsonValue(startExpr, context);
            int start = startVal is int si ? si : (startVal is long sl ? (int)sl : 0);

            int end = arr.Count;
            if (args.Count >= 3)
            {
                var endExpr = args[2].Trim();
                var endVal = ResolveJsonValue(endExpr, context);
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
        private object? JsonConcat(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            var result = new JsonArray();

            foreach (var arg in args)
            {
                var arrNode = GetJsonNode(arg.Trim(), context);
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
        private int JsonIndexOf(string argsString, ScriptContext context)
        {
            var args = SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return -1;

            var arrExpr = args[0].Trim();
            var valueExpr = args[1].Trim();

            var arrNode = GetJsonNode(arrExpr, context);
            if (arrNode is not JsonArray arr)
                return -1;

            var searchValue = ResolveJsonValue(valueExpr, context);
            var searchStr = searchValue?.ToString() ?? "";

            for (int i = 0; i < arr.Count; i++)
            {
                var item = arr[i];
                var itemStr = JsonNodeToStringValue(item);
                if (itemStr == searchStr)
                    return i;
            }

            return -1;
        }
    }
}
