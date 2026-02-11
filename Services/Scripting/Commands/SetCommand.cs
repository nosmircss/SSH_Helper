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

            if (TryParseFunctionCall(expression, "length", out var innerLength))
            {
                var resolved = ResolveValue(innerLength, context);
                if (resolved is List<string> list)
                    return list.Count;
                return resolved?.ToString()?.Length ?? 0;
            }

            if (TryParseFunctionCall(expression, "trim", out var innerTrim))
            {
                var resolved = ResolveValue(innerTrim, context);
                return resolved?.ToString()?.Trim() ?? string.Empty;
            }

            if (TryParseFunctionCall(expression, "upper", out var innerUpper))
            {
                var resolved = ResolveValue(innerUpper, context);
                return resolved?.ToString()?.ToUpperInvariant() ?? string.Empty;
            }

            if (TryParseFunctionCall(expression, "lower", out var innerLower))
            {
                var resolved = ResolveValue(innerLower, context);
                return resolved?.ToString()?.ToLowerInvariant() ?? string.Empty;
            }

            if (TryParseFunctionCall(expression, "push", out var innerPush))
            {
                var commaIdx = JsonUtilities.FindTopLevelComma(innerPush);
                if (commaIdx > 0)
                {
                    var arrayName = innerPush.Substring(0, commaIdx).Trim();
                    var valueExpr = innerPush.Substring(commaIdx + 1).Trim();
                    var existing = context.GetVariable(arrayName);
                    var array = existing as List<string> ?? new List<string>();
                    var resolvedValue = EvaluateExpression(valueExpr, context)?.ToString() ?? string.Empty;
                    array.Add(resolvedValue);
                    context.SetVariable(arrayName, array);
                    return array;
                }
            }

            if (TryParseFunctionCall(expression, "replace", out var innerReplace))
            {
                var args = JsonUtilities.SplitTopLevelCommas(innerReplace);
                if (args.Count >= 3)
                {
                    var source = ResolveValue(args[0], context)?.ToString() ?? string.Empty;
                    var oldValue = JsonUtilities.ResolveStringValue(args[1], context);
                    var newValue = JsonUtilities.ResolveStringValue(args[2], context);
                    return source.Replace(oldValue, newValue, StringComparison.Ordinal);
                }
            }

            if (TryParseFunctionCall(expression, "split", out var innerSplit))
            {
                var args = JsonUtilities.SplitTopLevelCommas(innerSplit);
                if (args.Count >= 1)
                {
                    var source = ResolveValue(args[0], context)?.ToString() ?? string.Empty;
                    var delimiter = args.Count > 1 ? JsonUtilities.ResolveStringValue(args[1], context) : ",";
                    if (delimiter.Length == 0)
                    {
                        var chars = new List<string>(source.Length);
                        foreach (var c in source)
                            chars.Add(c.ToString());
                        return chars;
                    }

                    return new List<string>(source.Split(new[] { delimiter }, StringSplitOptions.None));
                }
            }

            if (TryParseFunctionCall(expression, "join", out var innerJoin))
            {
                var args = JsonUtilities.SplitTopLevelCommas(innerJoin);
                if (args.Count >= 1)
                {
                    var value = ResolveValue(args[0], context);
                    var delimiter = args.Count > 1 ? JsonUtilities.ResolveStringValue(args[1], context) : ",";
                    var list = ResolveToStringList(value);
                    return string.Join(delimiter, list);
                }
            }

            if (TryParseFunctionCall(expression, "substring", out var innerSubstring))
            {
                var args = JsonUtilities.SplitTopLevelCommas(innerSubstring);
                if (args.Count >= 2)
                {
                    var source = ResolveValue(args[0], context)?.ToString() ?? string.Empty;
                    var start = (int)ResolveNumeric(args[1], context);
                    if (start < 0)
                        start = 0;
                    if (start >= source.Length)
                        return string.Empty;

                    if (args.Count >= 3)
                    {
                        var length = (int)ResolveNumeric(args[2], context);
                        if (length <= 0)
                            return string.Empty;
                        if (start + length > source.Length)
                            length = source.Length - start;
                        return source.Substring(start, length);
                    }

                    return source.Substring(start);
                }
            }

            if (TryParseFunctionCall(expression, "sort", out var innerSort))
            {
                var args = JsonUtilities.SplitTopLevelCommas(innerSort);
                if (args.Count >= 1)
                {
                    var value = ResolveValue(args[0], context);
                    var order = args.Count > 1 ? JsonUtilities.ResolveStringValue(args[1], context) : "asc";
                    var list = ResolveToStringList(value);
                    list.Sort(StringComparer.OrdinalIgnoreCase);
                    if (order.Equals("desc", StringComparison.OrdinalIgnoreCase))
                        list.Reverse();
                    return list;
                }
            }

            if (JsonUtilities.TryEvaluateJsonExpression(expression, context, out var jsonResult, normalizeStructured: false))
                return jsonResult;

            if (HasArithmeticOperator(expression))
            {
                try
                {
                    return EvaluateArithmetic(expression, context);
                }
                catch (FormatException)
                {
                    return context.SubstituteVariables(expression);
                }
            }

            if ((expression.StartsWith("\"", StringComparison.Ordinal) && expression.EndsWith("\"", StringComparison.Ordinal)) ||
                (expression.StartsWith("'", StringComparison.Ordinal) && expression.EndsWith("'", StringComparison.Ordinal)))
            {
                var literal = expression.Substring(1, expression.Length - 2);
                return context.SubstituteVariables(literal);
            }

            if (expression.Contains("${", StringComparison.Ordinal))
                return context.SubstituteVariables(expression);

            if (int.TryParse(expression, out var intVal))
                return intVal;
            if (double.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleVal))
                return doubleVal;

            var varValue = context.GetVariable(expression);
            if (varValue != null)
                return varValue;

            return context.SubstituteVariables(expression);
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

        private double EvaluateArithmetic(string expression, ScriptContext context)
        {
            var parser = new ArithmeticParser(expression, token => ResolveNumeric(token, context), context);
            return parser.Parse();
        }

        private object? ResolveValue(string expr, ScriptContext context)
        {
            expr = expr.Trim();

            var (handled, length) = ValueResolver.TryResolveLengthExpression(expr, context.GetVariable);
            if (handled)
                return length;

            if (JsonUtilities.TryEvaluateJsonExpression(expr, context, out var jsonResult, normalizeStructured: false))
                return jsonResult;

            if (LooksLikeFunctionCall(expr))
                return EvaluateExpression(expr, context);

            if (expr.Contains("${", StringComparison.Ordinal))
                return context.SubstituteVariables(expr);

            var directValue = context.GetVariable(expr);
            if (directValue != null)
                return directValue;

            if ((expr.StartsWith("\"", StringComparison.Ordinal) && expr.EndsWith("\"", StringComparison.Ordinal)) ||
                (expr.StartsWith("'", StringComparison.Ordinal) && expr.EndsWith("'", StringComparison.Ordinal)))
            {
                return context.SubstituteVariables(expr.Substring(1, expr.Length - 2));
            }

            return expr;
        }

        private static bool LooksLikeFunctionCall(string expr)
        {
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

            return depth == 0;
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

            value = value.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

            if (value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength) + "...";
        }

        private static List<string> ResolveToStringList(object? value)
        {
            if (value == null)
                return new List<string>();

            if (value is List<string> listValue)
                return new List<string>(listValue);

            if (value is JsonArray jsonArray)
            {
                var items = new List<string>(jsonArray.Count);
                foreach (var item in jsonArray)
                    items.Add(JsonUtilities.JsonNodeToStringValue(item));
                return items;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                    items.Add(item?.ToString() ?? string.Empty);
                return items;
            }

            var text = value.ToString() ?? string.Empty;
            var trimmed = text.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                try
                {
                    var parsed = JsonNode.Parse(trimmed) as JsonArray;
                    if (parsed != null)
                    {
                        var items = new List<string>(parsed.Count);
                        foreach (var item in parsed)
                            items.Add(JsonUtilities.JsonNodeToStringValue(item));
                        return items;
                    }
                }
                catch
                {
                    // fall back to string handling
                }
            }

            if (text.Contains('\n') || text.Contains('\r'))
            {
                return new List<string>(text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None));
            }

            return new List<string> { text };
        }

        private double ResolveNumeric(string expr, ScriptContext context)
        {
            expr = expr.Trim();

            var (handled, length) = ValueResolver.TryResolveLengthExpression(expr, context.GetVariable);
            if (handled)
                return length;

            if (double.TryParse(expr, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                return num;
            if (double.TryParse(expr, out num))
                return num;

            var directValue = context.GetVariable(expr);
            if (directValue != null && double.TryParse(directValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var varNum))
                return varNum;
            if (directValue != null && double.TryParse(directValue.ToString(), out varNum))
                return varNum;

            var substituted = context.SubstituteVariables(expr);
            if (double.TryParse(substituted, NumberStyles.Float, CultureInfo.InvariantCulture, out var subNum))
                return subNum;
            if (double.TryParse(substituted, out subNum))
                return subNum;

            throw new FormatException($"Unable to resolve numeric value from '{expr}'");
        }

        private sealed class ArithmeticParser
        {
            private readonly string _expression;
            private readonly Func<string, double> _resolveNumeric;
            private readonly ScriptContext _context;
            private int _position;

            public ArithmeticParser(string expression, Func<string, double> resolveNumeric, ScriptContext context)
            {
                _expression = expression;
                _resolveNumeric = resolveNumeric;
                _context = context;
            }

            public double Parse()
            {
                _position = 0;
                var value = ParseAddSubtract();
                SkipWhitespace();
                if (_position < _expression.Length)
                    throw new FormatException("Unexpected trailing tokens in arithmetic expression");
                return value;
            }

            private double ParseAddSubtract()
            {
                var value = ParseMultiplyDivideModulo();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('+'))
                    {
                        value += ParseMultiplyDivideModulo();
                        continue;
                    }
                    if (Match('-'))
                    {
                        value -= ParseMultiplyDivideModulo();
                        continue;
                    }
                    return value;
                }
            }

            private double ParseMultiplyDivideModulo()
            {
                var value = ParseUnary();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('*'))
                    {
                        value *= ParseUnary();
                        continue;
                    }
                    if (Match('/'))
                    {
                        var rhs = ParseUnary();
                        if (rhs == 0)
                        {
                            _context.EmitOutput("Warning: Division by zero, returning 0", ScriptOutputType.Warning);
                            return 0;
                        }
                        value /= rhs;
                        continue;
                    }
                    if (Match('%'))
                    {
                        var rhs = ParseUnary();
                        if (rhs == 0)
                        {
                            _context.EmitOutput("Warning: Modulo by zero, returning 0", ScriptOutputType.Warning);
                            return 0;
                        }
                        value %= rhs;
                        continue;
                    }

                    return value;
                }
            }

            private double ParseUnary()
            {
                SkipWhitespace();
                if (Match('+'))
                    return ParseUnary();
                if (Match('-'))
                    return -ParseUnary();
                return ParsePrimary();
            }

            private double ParsePrimary()
            {
                SkipWhitespace();
                if (Match('('))
                {
                    var value = ParseAddSubtract();
                    SkipWhitespace();
                    if (!Match(')'))
                        throw new FormatException("Missing closing parenthesis in arithmetic expression");
                    return value;
                }

                var token = ReadToken();
                return _resolveNumeric(token);
            }

            private string ReadToken()
            {
                SkipWhitespace();
                if (_position >= _expression.Length)
                    throw new FormatException("Unexpected end of arithmetic expression");

                var start = _position;
                while (_position < _expression.Length)
                {
                    var c = _expression[_position];
                    if (char.IsWhiteSpace(c) || c == '+' || c == '-' || c == '*' || c == '/' || c == '%' || c == '(' || c == ')')
                        break;
                    _position++;
                }

                if (start == _position)
                    throw new FormatException("Invalid token in arithmetic expression");

                return _expression.Substring(start, _position - start);
            }

            private void SkipWhitespace()
            {
                while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
                    _position++;
            }

            private bool Match(char expected)
            {
                if (_position < _expression.Length && _expression[_position] == expected)
                {
                    _position++;
                    return true;
                }
                return false;
            }
        }
    }
}
