using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Evaluates conditional expressions for if/while statements.
    /// Supports: ==, !=, >, >=, less than, less-or-equal, matches, contains, startswith, endswith, is empty, is defined, and, or, not
    /// </summary>
    public class ExpressionEvaluator
    {
        private readonly ScriptContext _context;

        public ExpressionEvaluator(ScriptContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Evaluates a condition expression and returns true or false.
        /// </summary>
        public bool Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            expression = expression.Trim();

            // Handle logical operators (lowest precedence)
            // Check for " and " or " or " (with spaces to avoid matching within words)
            var andIndex = FindLogicalOperator(expression, " and ");
            if (andIndex > 0)
            {
                var left = expression.Substring(0, andIndex);
                var right = expression.Substring(andIndex + 5);
                return Evaluate(left) && Evaluate(right);
            }

            var orIndex = FindLogicalOperator(expression, " or ");
            if (orIndex > 0)
            {
                var left = expression.Substring(0, orIndex);
                var right = expression.Substring(orIndex + 4);
                return Evaluate(left) || Evaluate(right);
            }

            // Handle "not " prefix
            if (expression.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
            {
                return !Evaluate(expression.Substring(4));
            }

            // Handle parentheses
            if (expression.StartsWith("(") && expression.EndsWith(")"))
            {
                return Evaluate(expression.Substring(1, expression.Length - 2));
            }

            // Now evaluate comparison operators
            return EvaluateComparison(expression);
        }

        private bool EvaluateComparison(string expression)
        {
            // Check for "is empty" / "is not empty"
            if (expression.EndsWith(" is empty", StringComparison.OrdinalIgnoreCase))
            {
                var varName = expression.Substring(0, expression.Length - 9).Trim();
                var emptyValue = ResolveValue(varName);
                return string.IsNullOrEmpty(emptyValue?.ToString());
            }

            if (expression.EndsWith(" is not empty", StringComparison.OrdinalIgnoreCase))
            {
                var varName = expression.Substring(0, expression.Length - 13).Trim();
                var notEmptyValue = ResolveValue(varName);
                return !string.IsNullOrEmpty(notEmptyValue?.ToString());
            }

            // Check for "is defined" / "is not defined"
            if (expression.EndsWith(" is defined", StringComparison.OrdinalIgnoreCase))
            {
                var varName = expression.Substring(0, expression.Length - 11).Trim();
                return _context.HasVariable(varName);
            }

            if (expression.EndsWith(" is not defined", StringComparison.OrdinalIgnoreCase))
            {
                var varName = expression.Substring(0, expression.Length - 15).Trim();
                return !_context.HasVariable(varName);
            }

            // Check for comparison operators
            // Order matters: check longer operators first (>=, <=, !=, ==) before shorter ones (>, <)

            // matches (regex)
            var matchesIndex = FindOperator(expression, " matches ");
            if (matchesIndex > 0)
            {
                var left = ResolveValue(expression.Substring(0, matchesIndex))?.ToString() ?? "";
                var pattern = ExtractPattern(expression.Substring(matchesIndex + 9).Trim());
                try
                {
                    return Regex.IsMatch(left, pattern, RegexOptions.IgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            // contains
            var containsIndex = FindOperator(expression, " contains ");
            if (containsIndex > 0)
            {
                var left = ResolveValue(expression.Substring(0, containsIndex))?.ToString() ?? "";
                var right = ResolveStringValue(expression.Substring(containsIndex + 10).Trim());
                return left.Contains(right, StringComparison.OrdinalIgnoreCase);
            }

            // startswith
            var startsWithIndex = FindOperator(expression, " startswith ");
            if (startsWithIndex > 0)
            {
                var left = ResolveValue(expression.Substring(0, startsWithIndex))?.ToString() ?? "";
                var right = ResolveStringValue(expression.Substring(startsWithIndex + 12).Trim());
                return left.StartsWith(right, StringComparison.OrdinalIgnoreCase);
            }

            // endswith
            var endsWithIndex = FindOperator(expression, " endswith ");
            if (endsWithIndex > 0)
            {
                var left = ResolveValue(expression.Substring(0, endsWithIndex))?.ToString() ?? "";
                var right = ResolveStringValue(expression.Substring(endsWithIndex + 10).Trim());
                return left.EndsWith(right, StringComparison.OrdinalIgnoreCase);
            }

            // != (not equals)
            var neIndex = FindOperator(expression, " != ");
            if (neIndex > 0)
            {
                var left = ResolveValue(expression.Substring(0, neIndex));
                var right = ResolveValue(expression.Substring(neIndex + 4));
                return !AreEqual(left, right);
            }

            // == (equals)
            var eqIndex = FindOperator(expression, " == ");
            if (eqIndex > 0)
            {
                var left = ResolveValue(expression.Substring(0, eqIndex));
                var right = ResolveValue(expression.Substring(eqIndex + 4));
                return AreEqual(left, right);
            }

            // >=
            var gteIndex = FindOperator(expression, " >= ");
            if (gteIndex > 0)
            {
                var left = ResolveNumeric(expression.Substring(0, gteIndex));
                var right = ResolveNumeric(expression.Substring(gteIndex + 4));
                return left >= right;
            }

            // <=
            var lteIndex = FindOperator(expression, " <= ");
            if (lteIndex > 0)
            {
                var left = ResolveNumeric(expression.Substring(0, lteIndex));
                var right = ResolveNumeric(expression.Substring(lteIndex + 4));
                return left <= right;
            }

            // >
            var gtIndex = FindOperator(expression, " > ");
            if (gtIndex > 0)
            {
                var left = ResolveNumeric(expression.Substring(0, gtIndex));
                var right = ResolveNumeric(expression.Substring(gtIndex + 3));
                return left > right;
            }

            // <
            var ltIndex = FindOperator(expression, " < ");
            if (ltIndex > 0)
            {
                var left = ResolveNumeric(expression.Substring(0, ltIndex));
                var right = ResolveNumeric(expression.Substring(ltIndex + 3));
                return left < right;
            }

            // If no operator found, treat as truthy check
            var value = ResolveValue(expression);
            return IsTruthy(value);
        }

        private int FindLogicalOperator(string expression, string op)
        {
            // Find operator outside of quotes
            var inQuote = false;
            var quoteChar = '\0';

            for (int i = 0; i < expression.Length - op.Length; i++)
            {
                var c = expression[i];

                if ((c == '"' || c == '\'') && (i == 0 || expression[i - 1] != '\\'))
                {
                    if (!inQuote)
                    {
                        inQuote = true;
                        quoteChar = c;
                    }
                    else if (c == quoteChar)
                    {
                        inQuote = false;
                    }
                }

                if (!inQuote && expression.Substring(i).StartsWith(op, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindOperator(string expression, string op)
        {
            return FindLogicalOperator(expression, op);
        }

        private object? ResolveValue(string expr)
        {
            expr = expr.Trim();

            // Handle quoted strings
            if ((expr.StartsWith("\"") && expr.EndsWith("\"")) ||
                (expr.StartsWith("'") && expr.EndsWith("'")))
            {
                return expr.Substring(1, expr.Length - 2);
            }

            // Handle variable substitution
            if (expr.Contains("${"))
            {
                return _context.SubstituteVariables(expr);
            }

            // Try variable lookup
            if (_context.HasVariable(expr))
            {
                return _context.GetVariable(expr);
            }

            // Try numeric
            if (double.TryParse(expr, out var num))
            {
                return num;
            }

            // Return as literal
            return expr;
        }

        private string ResolveStringValue(string expr)
        {
            var value = ResolveValue(expr);
            return value?.ToString() ?? string.Empty;
        }

        private double ResolveNumeric(string expr)
        {
            var value = ResolveValue(expr);
            if (value is double d) return d;
            if (value is int i) return i;
            if (double.TryParse(value?.ToString(), out var num))
                return num;
            return 0;
        }

        private string ExtractPattern(string expr)
        {
            expr = expr.Trim();

            // Handle 'pattern' or "pattern" syntax
            if ((expr.StartsWith("\"") && expr.EndsWith("\"")) ||
                (expr.StartsWith("'") && expr.EndsWith("'")))
            {
                return expr.Substring(1, expr.Length - 2);
            }

            // Handle /pattern/ syntax (traditional regex delimiters)
            if (expr.StartsWith("/") && expr.EndsWith("/"))
            {
                return expr.Substring(1, expr.Length - 2);
            }

            // Return as-is
            return expr;
        }

        private bool AreEqual(object? left, object? right)
        {
            if (left == null && right == null) return true;
            if (left == null || right == null) return false;

            var leftStr = left.ToString();
            var rightStr = right.ToString();

            // Try numeric comparison
            if (double.TryParse(leftStr, out var leftNum) &&
                double.TryParse(rightStr, out var rightNum))
            {
                return Math.Abs(leftNum - rightNum) < 0.0001;
            }

            // String comparison (case-insensitive)
            return string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTruthy(object? value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            if (value is int i) return i != 0;
            if (value is double d) return d != 0;
            if (value is string s) return !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase);
            if (value is List<string> list) return list.Count > 0;
            return true;
        }
    }
}
