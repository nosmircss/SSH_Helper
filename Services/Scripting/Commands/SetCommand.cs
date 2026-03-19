using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Sets or manipulates a variable value.
    /// Supports: assignment, nested assignment, arithmetic, string helpers, and JSON helpers.
    /// </summary>
    public class SetCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.Set))
                return Task.FromResult(CommandResult.Fail("Set command has no assignment expression"));

            var parts = step.Set.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                return Task.FromResult(CommandResult.Fail($"Invalid set syntax: '{step.Set}'. Expected 'variable = value'"));

            var varName = parts[0].Trim();
            var expression = parts[1].Trim();

            if (string.IsNullOrEmpty(varName))
                return Task.FromResult(CommandResult.Fail("Variable name cannot be empty"));

            if (varName.Contains('.'))
                return HandleNestedAssignment(varName, expression, context);

            var value = EvaluateExpression(expression, context);
            context.SetVariable(varName, value);
            context.EmitOutput($"Set {varName} = {FormatValueForDisplay(value)}", ScriptOutputType.Debug);

            return Task.FromResult(CommandResult.Ok());
        }

        private Task<CommandResult> HandleNestedAssignment(string path, string expression, ScriptContext context)
        {
            var pathParts = path.Split('.');
            var rootName = pathParts[0];

            var existingRoot = context.GetVariable(rootName);
            JsonObject rootObj;

            if (existingRoot is JsonObject existingJsonObj)
            {
                rootObj = existingJsonObj;
            }
            else if (existingRoot is string jsonStr && jsonStr.TrimStart().StartsWith("{", StringComparison.Ordinal))
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

            var finalKey = pathParts[pathParts.Length - 1];
            var value = EvaluateExpression(expression, context);
            current[finalKey] = JsonUtilities.ConvertToJsonNode(value);
            context.SetVariable(rootName, rootObj);

            context.EmitOutput($"Set {path} = {FormatValueForDisplay(value)}", ScriptOutputType.Debug);
            return Task.FromResult(CommandResult.Ok());
        }

        private object? EvaluateExpression(string expression, ScriptContext context)
        {
            expression = expression.Trim();

            if (TryParseFunctionCall(expression, "push", out var innerPush))
            {
                var commaIdx = JsonUtilities.FindTopLevelComma(innerPush);
                if (commaIdx > 0)
                {
                    var arrayExpr = innerPush.Substring(0, commaIdx).Trim();
                    var valueExpr = innerPush.Substring(commaIdx + 1).Trim();
                    var array = ResolveListForExpression(arrayExpr, context, out var listVariableName);
                    var resolvedValue = EvaluateExpression(valueExpr, context)?.ToString() ?? string.Empty;
                    array.Add(resolvedValue);

                    if (!string.IsNullOrEmpty(listVariableName))
                    {
                        context.SetVariable(listVariableName!, array);
                    }

                    return array;
                }
            }

            if (TryParseFunctionCall(expression, "unshift", out var innerUnshift))
            {
                var commaIdx = JsonUtilities.FindTopLevelComma(innerUnshift);
                if (commaIdx > 0)
                {
                    var arrayExpr = innerUnshift.Substring(0, commaIdx).Trim();
                    var valueExpr = innerUnshift.Substring(commaIdx + 1).Trim();
                    var array = ResolveListForExpression(arrayExpr, context, out var listVariableName);
                    var resolvedValue = EvaluateExpression(valueExpr, context)?.ToString() ?? string.Empty;
                    array.Insert(0, resolvedValue);

                    if (!string.IsNullOrEmpty(listVariableName))
                    {
                        context.SetVariable(listVariableName!, array);
                    }

                    return array;
                }
            }

            if (TryParseFunctionCall(expression, "pop", out var innerPop))
            {
                var arrayExpr = innerPop.Trim();
                var array = ResolveListForExpression(arrayExpr, context, out var listVariableName);
                if (array.Count == 0)
                    return null;

                var lastIndex = array.Count - 1;
                var removedValue = array[lastIndex];
                array.RemoveAt(lastIndex);

                if (!string.IsNullOrEmpty(listVariableName))
                {
                    context.SetVariable(listVariableName!, array);
                }

                return removedValue;
            }

            if (TryParseFunctionCall(expression, "shift", out var innerShift))
            {
                var arrayExpr = innerShift.Trim();
                var array = ResolveListForExpression(arrayExpr, context, out var listVariableName);
                if (array.Count == 0)
                    return null;

                var removedValue = array[0];
                array.RemoveAt(0);

                if (!string.IsNullOrEmpty(listVariableName))
                {
                    context.SetVariable(listVariableName!, array);
                }

                return removedValue;
            }

            if (JsonUtilities.TryEvaluateFunctionExpression(expression, context, out var functionValue))
                return functionValue;

            if (JsonUtilities.TryEvaluateJsonExpression(expression, context, out var jsonResult, normalizeStructured: false))
                return jsonResult;

            if (HasArithmeticOperator(expression))
            {
                try
                {
                    var parser = new ExpressionParser(expression, context);
                    return parser.Parse();
                }
                catch (FormatException)
                {
                    return context.SubstituteVariables(expression);
                }
            }

            return ValueResolver.ResolveExpressionValue(expression, context);
        }

        private static bool TryParseFunctionCall(string expression, string functionName, out string inner)
        {
            var prefix = functionName + "(";
            if (expression.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                expression.EndsWith(")", StringComparison.Ordinal))
            {
                inner = expression.Substring(prefix.Length, expression.Length - prefix.Length - 1).Trim();
                return true;
            }

            inner = string.Empty;
            return false;
        }

        private static bool HasArithmeticOperator(string expression)
        {
            var inQuote = false;
            var quoteChar = '\0';

            for (int i = 0; i < expression.Length; i++)
            {
                var c = expression[i];
                if ((c == '"' || c == '\'') && (i == 0 || expression[i - 1] != '\\'))
                {
                    if (!inQuote)
                    {
                        inQuote = true;
                        quoteChar = c;
                    }
                    else if (quoteChar == c)
                    {
                        inQuote = false;
                    }
                    continue;
                }

                if (inQuote)
                    continue;

                if (c == '+' || c == '-' || c == '*' || c == '/' || c == '%' || c == '(' || c == ')')
                    return true;
            }

            return false;
        }

        private object? ResolveValue(string expr, ScriptContext context)
        {
            return ValueResolver.ResolveExpressionValue(expr, context);
        }

        private static bool TryResolveWritableListVariableName(string expr, out string variableName)
        {
            variableName = string.Empty;
            var trimmed = expr.Trim();
            if (trimmed.Length == 0)
                return false;

            if (trimmed.StartsWith("${", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                var candidate = trimmed.Substring(2, trimmed.Length - 3).Trim();
                if (ValueResolver.IsSimpleIdentifier(candidate))
                {
                    variableName = candidate;
                    return true;
                }

                return false;
            }

            if (ValueResolver.IsSimpleIdentifier(trimmed))
            {
                variableName = trimmed;
                return true;
            }

            return false;
        }

        private List<string> ResolveListForExpression(string expr, ScriptContext context, out string? writableVariableName)
        {
            if (TryResolveWritableListVariableName(expr, out var variableName))
            {
                writableVariableName = variableName;
                return ValueResolver.ResolveListValue(context.GetVariable(variableName));
            }

            writableVariableName = null;
            return ValueResolver.ResolveListValue(ResolveValue(expr, context));
        }

        private static string FormatValueForDisplay(object? value)
        {
            return value switch
            {
                null => "",
                List<string> list => FormatListForDisplay(list),
                JsonNode node => ScriptingHelpers.TruncateForDisplay(node.ToJsonString()),
                _ => ScriptingHelpers.TruncateForDisplay(value.ToString() ?? string.Empty)
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
                parts.Add(ScriptingHelpers.TruncateForDisplay(values[i], 30));
            }

            var suffix = values.Count > maxItems ? $", ... ({values.Count} items)" : "";
            return $"[{string.Join(", ", parts)}{suffix}]";
        }

        // ArithmeticParser removed — replaced by ExpressionParser (see Services/Scripting/ExpressionParser.cs)
    }
}
