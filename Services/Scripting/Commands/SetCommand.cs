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

            context.EmitOutput($"Set {varName} = {FormatValueForDisplay(value)}", ScriptOutputType.Debug);

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
            current[finalKey] = JsonUtilities.ConvertToJsonNode(value);

            // Store the updated root object
            context.SetVariable(rootName, rootObj);

            context.EmitOutput($"Set {path} = {FormatValueForDisplay(value)}", ScriptOutputType.Debug);

            return Task.FromResult(CommandResult.Ok());
        }

        private object? EvaluateExpression(string expression, ScriptContext context)
        {
            expression = expression.Trim();

            // Check for function calls: length(var), trim(var), etc.
            if (expression.StartsWith("length(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(7, expression.Length - 8);
                var resolved = ResolveValue(inner, context);
                if (resolved is List<string> list)
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
                var commaIdx = JsonUtilities.FindTopLevelComma(inner);
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

                    // Resolve and add the value (supports quoted strings, numbers, vars, etc.)
                    var resolvedValue = EvaluateExpression(valueExpr, context)?.ToString() ?? string.Empty;
                    array.Add(resolvedValue);

                    // Update the array variable
                    context.SetVariable(arrayName, array);

                    return array;
                }
            }

            // json(...) constructor
            if (expression.StartsWith("json(") && expression.EndsWith(")"))
            {
                var inner = expression.Substring(5, expression.Length - 6).Trim();
                return JsonFunctions.Constructor(inner, context);
            }

            // json.* function dispatch
            if (expression.StartsWith("json."))
            {
                var parenIdx = expression.IndexOf('(');
                if (parenIdx > 5 && expression.EndsWith(")"))
                {
                    var funcName = expression.Substring(5, parenIdx - 5);
                    var inner = expression.Substring(parenIdx + 1, expression.Length - parenIdx - 2).Trim();
                    if (TryDispatchJsonFunction(funcName, inner, context, out var result))
                        return result;
                }
            }

            // Check for arithmetic: var + 1, var - 1, var * 2, var / 3, var % 10
            if (expression.Contains(" + ") || expression.Contains(" - ") ||
                expression.Contains(" * ") || expression.Contains(" / ") ||
                expression.Contains(" % "))
            {
                return EvaluateArithmetic(expression, context);
            }

            // Check for quoted string literal
            if ((expression.StartsWith("\"") && expression.EndsWith("\"")) ||
                (expression.StartsWith("'") && expression.EndsWith("'")))
            {
                var literal = expression.Substring(1, expression.Length - 2);
                return context.SubstituteVariables(literal);
            }

            // Check for string concatenation with ${var}
            if (expression.Contains("${"))
            {
                return context.SubstituteVariables(expression);
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

        private static bool TryDispatchJsonFunction(string funcName, string args, ScriptContext context, out object? result)
        {
            switch (funcName.ToLowerInvariant())
            {
                case "get":
                    result = JsonFunctions.Get(args, context);
                    return true;
                case "set":
                    result = JsonFunctions.Set(args, context);
                    return true;
                case "delete":
                    result = JsonFunctions.Delete(args, context);
                    return true;
                case "merge":
                    result = JsonFunctions.MergeVariadic(args, context);
                    return true;
                case "format":
                    result = JsonFunctions.Format(args, context);
                    return true;
                case "exists":
                    result = JsonFunctions.Exists(args, context);
                    return true;
                case "len":
                    result = JsonFunctions.Len(args, context);
                    return true;
                case "type":
                    result = JsonFunctions.Type(args, context);
                    return true;
                case "keys":
                    result = JsonFunctions.Keys(args, context);
                    return true;
                case "values":
                    result = JsonFunctions.Values(args, context);
                    return true;
                case "items":
                    result = JsonFunctions.Items(args, context);
                    return true;
                case "push":
                    result = JsonFunctions.Push(args, context);
                    return true;
                case "pop":
                    result = JsonFunctions.Pop(args, context);
                    return true;
                case "unshift":
                    result = JsonFunctions.Unshift(args, context);
                    return true;
                case "shift":
                    result = JsonFunctions.Shift(args, context);
                    return true;
                case "slice":
                    result = JsonFunctions.Slice(args, context);
                    return true;
                case "concat":
                    result = JsonFunctions.Concat(args, context);
                    return true;
                case "indexof":
                    result = JsonFunctions.IndexOf(args, context);
                    return true;
                default:
                    result = null;
                    return false;
            }
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
                {
                    context.EmitOutput("Warning: Division by zero, returning 0", ScriptOutputType.Warning);
                    return 0;
                }
                return left / right;
            }

            // Check modulo
            string[] modParts = expression.Split(new[] { " % " }, 2, StringSplitOptions.None);
            if (modParts.Length == 2)
            {
                var left = ResolveNumeric(modParts[0].Trim(), context);
                var right = ResolveNumeric(modParts[1].Trim(), context);
                if (right == 0)
                {
                    context.EmitOutput("Warning: Modulo by zero, returning 0", ScriptOutputType.Warning);
                    return 0;
                }
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

            var (handled, length) = ValueResolver.TryResolveLengthExpression(expr, context.GetVariable);
            if (handled)
                return length;

            // Variable substitution
            if (expr.Contains("${"))
            {
                return context.SubstituteVariables(expr);
            }

            // Direct variable reference
            var directValue = context.GetVariable(expr);
            if (directValue != null)
                return directValue;

            return expr;
        }

        private static string FormatValueForDisplay(object? value)
        {
            return value switch
            {
                null => "",
                List<string> list => FormatListForDisplay(list),
                JsonNode node => TruncateForDisplay(node.ToJsonString()),
                _ => TruncateForDisplay(value.ToString() ?? string.Empty)
            };
        }

        private static string FormatListForDisplay(List<string> values, int maxItems = 10)
        {
            if (values.Count == 0)
                return "[]";

            var displayCount = Math.Min(values.Count, maxItems);
            var parts = new List<string>(displayCount);

            for (int i = 0; i < displayCount; i++)
            {
                parts.Add(TruncateForDisplay(values[i], 30));
            }

            var suffix = values.Count > maxItems ? $", ... ({values.Count} items)" : "";
            return $"[{string.Join(", ", parts)}{suffix}]";
        }

        private static string TruncateForDisplay(string value, int maxLength = 100)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            value = value.Replace("\r", "").Replace("\n", "\\n");

            if (value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength) + "...";
        }

        private double ResolveNumeric(string expr, ScriptContext context)
        {
            expr = expr.Trim();

            var (handled, length) = ValueResolver.TryResolveLengthExpression(expr, context.GetVariable);
            if (handled)
                return length;

            // Try direct numeric parse
            if (double.TryParse(expr, out var num))
                return num;

            // Try variable lookup
            var directValue = context.GetVariable(expr);
            if (directValue != null && double.TryParse(directValue.ToString(), out var varNum))
                return varNum;

            // Try with variable substitution
            var substituted = context.SubstituteVariables(expr);
            if (double.TryParse(substituted, out var subNum))
                return subNum;

            return 0;
        }
    }
}
