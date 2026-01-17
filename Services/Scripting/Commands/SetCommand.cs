using System;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Sets or manipulates a variable value.
    /// Supports: "var = value", "var = var + 1", "var = var - 1", "var = length(other)"
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

            // Evaluate the expression
            var value = EvaluateExpression(expression, context);

            // Set the variable
            context.SetVariable(varName, value);

            context.EmitOutput($"Set {varName} = {value}", ScriptOutputType.Debug);

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

            // Check for arithmetic: var + 1, var - 1
            if (expression.Contains(" + ") || expression.Contains(" - "))
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
            // Handle simple arithmetic: "var + 1" or "var - 1" or "5 + 3"
            string[] addParts = expression.Split(new[] { " + " }, 2, StringSplitOptions.None);
            if (addParts.Length == 2)
            {
                var left = ResolveNumeric(addParts[0].Trim(), context);
                var right = ResolveNumeric(addParts[1].Trim(), context);
                return left + right;
            }

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
    }
}
