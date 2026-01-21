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
    /// "var = push(array, value)", "var = json_array(array)", "var = json_object(...)",
    /// "var = json_merge(obj1, obj2)", "var = json_get(json, path)", "var = json_pretty(json)",
    /// "var = json_items(json, path)" (extract array items for foreach), "var.path = value" (nested assignment)
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

            // json_array(array) - converts array to JSON array string
            if (expression.StartsWith("json_array(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(11, expression.Length - 12).Trim();
                var arrayValue = context.GetVariable(inner);
                return ConvertToJsonArray(arrayValue, pretty: false);
            }

            // json_array_pretty(array) - converts array to pretty-printed JSON array string
            if (expression.StartsWith("json_array_pretty(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(18, expression.Length - 19).Trim();
                var arrayValue = context.GetVariable(inner);
                return ConvertToJsonArray(arrayValue, pretty: true);
            }

            // json_object(key1, value1, key2, value2, ...) - creates a JSON object from key-value pairs
            if (expression.StartsWith("json_object(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(12, expression.Length - 13).Trim();
                return BuildJsonObject(inner, context, pretty: false);
            }

            // json_object_pretty(key1, value1, key2, value2, ...) - creates a pretty-printed JSON object
            if (expression.StartsWith("json_object_pretty(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(19, expression.Length - 20).Trim();
                return BuildJsonObject(inner, context, pretty: true);
            }

            // json_merge(obj1, obj2) - merges two JSON objects (obj2 values override obj1)
            if (expression.StartsWith("json_merge(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(11, expression.Length - 12).Trim();
                return MergeJsonObjects(inner, context);
            }

            // json_get(json, "path.to.key") - extracts a value from JSON using dot notation
            if (expression.StartsWith("json_get(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(9, expression.Length - 10).Trim();
                return JsonGetValue(inner, context);
            }

            // json_pretty(json) - formats JSON with indentation
            if (expression.StartsWith("json_pretty(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(12, expression.Length - 13).Trim();
                return JsonPrettyFormat(inner, context);
            }

            // json_items(json) or json_items(json, "path") - extracts array items as List<string> for foreach
            if (expression.StartsWith("json_items(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(11, expression.Length - 12).Trim();
                return JsonItemsToList(inner, context);
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
        /// Merges two JSON objects. Values from obj2 override values in obj1.
        /// </summary>
        private object MergeJsonObjects(string argsString, ScriptContext context)
        {
            var commaIdx = FindTopLevelComma(argsString);
            if (commaIdx < 0)
                return "{}";

            var obj1Expr = argsString.Substring(0, commaIdx).Trim();
            var obj2Expr = argsString.Substring(commaIdx + 1).Trim();

            var obj1 = GetJsonObject(obj1Expr, context);
            var obj2 = GetJsonObject(obj2Expr, context);

            // Deep merge obj2 into obj1
            MergeInto(obj1, obj2);

            return obj1;
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
        /// Extracts a value from JSON using dot notation path: json_get(json, "path.to.key")
        /// Supports array indexing with [n] syntax: "items[0].name"
        /// </summary>
        private object? JsonGetValue(string argsString, ScriptContext context)
        {
            var commaIdx = FindTopLevelComma(argsString);
            if (commaIdx < 0)
                return null;

            var jsonExpr = argsString.Substring(0, commaIdx).Trim();
            var pathExpr = argsString.Substring(commaIdx + 1).Trim();

            // Get the JSON source
            var jsonNode = GetJsonNode(jsonExpr, context);
            if (jsonNode == null)
                return null;

            // Resolve the path (remove quotes if present)
            var path = ResolveStringValue(pathExpr, context);

            // Navigate the path
            return NavigateJsonPath(jsonNode, path);
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

        /// <summary>
        /// Formats JSON with indentation: json_pretty(json)
        /// </summary>
        private string JsonPrettyFormat(string expr, ScriptContext context)
        {
            var jsonNode = GetJsonNode(expr, context);
            if (jsonNode == null)
            {
                // Try to parse the expression directly after substitution
                var substituted = context.SubstituteVariables(expr);
                var trimmed = substituted.Trim();

                // Remove quotes if it's a quoted string containing JSON
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
                        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
                    }
                    catch
                    {
                        return trimmed; // Return as-is if not valid JSON
                    }
                }

                return substituted;
            }

            return jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Extracts array items as a List&lt;string&gt; for use with foreach.
        /// Syntax: json_items(json) or json_items(json, "path.to.array")
        /// Each array element is serialized as a JSON string if it's an object/array.
        /// </summary>
        private List<string> JsonItemsToList(string argsString, ScriptContext context)
        {
            var result = new List<string>();

            // Check if we have a path argument
            var commaIdx = FindTopLevelComma(argsString);
            JsonNode? arrayNode;

            if (commaIdx > 0)
            {
                // json_items(json, "path")
                var jsonExpr = argsString.Substring(0, commaIdx).Trim();
                var pathExpr = argsString.Substring(commaIdx + 1).Trim();

                var jsonNode = GetJsonNode(jsonExpr, context);
                if (jsonNode == null)
                    return result;

                var path = ResolveStringValue(pathExpr, context);

                // Navigate to the array using the path
                var navResult = NavigateJsonPath(jsonNode, path);
                if (navResult is string jsonStr && (jsonStr.TrimStart().StartsWith("[")))
                {
                    try
                    {
                        arrayNode = JsonNode.Parse(jsonStr);
                    }
                    catch
                    {
                        return result;
                    }
                }
                else if (navResult is JsonNode node)
                {
                    arrayNode = node;
                }
                else
                {
                    return result;
                }
            }
            else
            {
                // json_items(json) - no path, the expression itself should be an array
                arrayNode = GetJsonNode(argsString, context);
            }

            // Extract items from the array
            if (arrayNode is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item == null)
                    {
                        result.Add("null");
                    }
                    else if (item is JsonValue jv)
                    {
                        // Get the raw value for primitives
                        if (jv.TryGetValue<string>(out var str))
                            result.Add(str);
                        else if (jv.TryGetValue<long>(out var lng))
                            result.Add(lng.ToString());
                        else if (jv.TryGetValue<double>(out var dbl))
                            result.Add(dbl.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        else if (jv.TryGetValue<bool>(out var bln))
                            result.Add(bln ? "true" : "false");
                        else
                            result.Add(jv.ToString());
                    }
                    else
                    {
                        // For objects and arrays, serialize to compact JSON
                        result.Add(item.ToJsonString());
                    }
                }
            }

            return result;
        }
    }
}
