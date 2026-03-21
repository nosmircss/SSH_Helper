using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Unified recursive-descent expression parser that replaces ArithmeticParser.
    /// Returns object? to support mixed numeric/string/bool/list values.
    ///
    /// Grammar (precedence low to high):
    ///   Expression     := Ternary
    ///   Ternary        := NullCoalesce ('?' Expression ':' Expression)?         [Phase 3]
    ///   NullCoalesce   := Addition ('??' NullCoalesce)?                         [Phase 3]
    ///   Addition       := Multiplication (('+' | '-') Multiplication)*
    ///   Multiplication := Unary (('*' | '/' | '%') Unary)*
    ///   Unary          := ('-' | '+') Unary | Primary
    ///   Primary        := '(' Expression ')' | FunctionCall | StringLiteral | NumberLiteral | VariableRef
    /// </summary>
    public sealed class ExpressionParser
    {
        private readonly string _expression;
        private readonly ScriptContext _context;
        private int _position;

        public ExpressionParser(string expression, ScriptContext context)
        {
            _expression = expression ?? throw new ArgumentNullException(nameof(expression));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Parses and evaluates the expression, returning the result.
        /// </summary>
        public object? Parse()
        {
            _position = 0;
            var value = ParseExpression();
            SkipWhitespace();
            if (_position < _expression.Length)
                throw new FormatException($"Unexpected trailing tokens in expression at position {_position}: '{_expression.Substring(_position)}'");
            return value;
        }

        private object? ParseExpression()
        {
            return ParseTernary();
        }

        private object? ParseTernary()
        {
            var left = ParseNullCoalesce();
            SkipWhitespace();

            // Check for '?' but NOT '??' (null coalescing is handled by ParseNullCoalesce)
            if (_position < _expression.Length && _expression[_position] == '?' &&
                (_position + 1 >= _expression.Length || _expression[_position + 1] != '?'))
            {
                _position++; // consume '?'
                var trueValue = ParseExpression();
                SkipWhitespace();
                if (!Match(':'))
                    throw new FormatException("Expected ':' in ternary expression");
                var falseValue = ParseExpression();

                return IsTruthy(left) ? trueValue : falseValue;
            }

            return left;
        }

        private object? ParseNullCoalesce()
        {
            var left = ParseComparison();
            SkipWhitespace();

            if (_position + 1 < _expression.Length &&
                _expression[_position] == '?' && _expression[_position + 1] == '?')
            {
                _position += 2; // consume '??'
                var right = ParseNullCoalesce(); // right-associative
                return IsNullOrEmpty(left) ? right : left;
            }

            return left;
        }

        private object? ParseComparison()
        {
            var left = ParseAddSubtract();
            SkipWhitespace();

            if (_position >= _expression.Length)
                return left;

            // Check for two-character operators first: ==, !=, >=, <=
            if (_position + 1 < _expression.Length)
            {
                var twoChar = _expression.Substring(_position, 2);
                switch (twoChar)
                {
                    case "==":
                        _position += 2;
                        var rEq = ParseAddSubtract();
                        return CompareValues(left, rEq) == 0;
                    case "!=":
                        _position += 2;
                        var rNe = ParseAddSubtract();
                        return CompareValues(left, rNe) != 0;
                    case ">=":
                        _position += 2;
                        var rGe = ParseAddSubtract();
                        return CompareNumeric(left, rGe) >= 0;
                    case "<=":
                        _position += 2;
                        var rLe = ParseAddSubtract();
                        return CompareNumeric(left, rLe) <= 0;
                }
            }

            // Single-character: > and < (but not >= or <=, already handled)
            var c = _expression[_position];
            if (c == '>' && (_position + 1 >= _expression.Length || _expression[_position + 1] != '='))
            {
                _position++;
                var rGt = ParseAddSubtract();
                return CompareNumeric(left, rGt) > 0;
            }
            if (c == '<' && (_position + 1 >= _expression.Length || _expression[_position + 1] != '='))
            {
                _position++;
                var rLt = ParseAddSubtract();
                return CompareNumeric(left, rLt) < 0;
            }

            return left;
        }

        private static int CompareValues(object? left, object? right)
        {
            var ls = left?.ToString() ?? string.Empty;
            var rs = right?.ToString() ?? string.Empty;

            // Try numeric comparison first
            if (TryAsDouble(left, out var ld) && TryAsDouble(right, out var rd))
                return ld.CompareTo(rd);

            return string.Compare(ls, rs, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareNumeric(object? left, object? right)
        {
            if (TryAsDouble(left, out var ld) && TryAsDouble(right, out var rd))
                return ld.CompareTo(rd);

            // Fall back to string comparison
            var ls = left?.ToString() ?? string.Empty;
            var rs = right?.ToString() ?? string.Empty;
            return string.Compare(ls, rs, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTruthy(object? value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            if (value is double d) return d != 0;
            if (value is int i) return i != 0;
            if (value is string s) return !string.IsNullOrEmpty(s) &&
                                          !s.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                                          !s.Equals("0", StringComparison.Ordinal);
            return true;
        }

        private static bool IsNullOrEmpty(object? value)
        {
            if (value == null) return true;
            if (value is string s) return string.IsNullOrEmpty(s);
            return false;
        }

        private object? ParseAddSubtract()
        {
            var left = ParseMultiplyDivideModulo();

            while (true)
            {
                SkipWhitespace();
                if (Match('+'))
                {
                    var right = ParseMultiplyDivideModulo();

                    // Polymorphic +: if both sides are numeric, add. Otherwise concatenate as strings.
                    if (TryAsDouble(left, out var leftNum) && TryAsDouble(right, out var rightNum))
                    {
                        left = leftNum + rightNum;
                    }
                    else
                    {
                        // String concatenation: at least one side is non-numeric.
                        var ls = left?.ToString() ?? string.Empty;
                        var rs = right?.ToString() ?? string.Empty;
                        left = ls + rs;
                    }
                    continue;
                }
                if (Match('-'))
                {
                    var right = ParseMultiplyDivideModulo();
                    left = CoerceToDouble(left) - CoerceToDouble(right);
                    continue;
                }
                return left;
            }
        }

        private object? ParseMultiplyDivideModulo()
        {
            var left = ParseUnary();

            while (true)
            {
                SkipWhitespace();
                if (Match('*'))
                {
                    var right = ParseUnary();
                    left = CoerceToDouble(left) * CoerceToDouble(right);
                    continue;
                }
                if (Match('/'))
                {
                    var right = ParseUnary();
                    var rhs = CoerceToDouble(right);
                    if (rhs == 0)
                    {
                        _context.EmitOutput("Warning: Division by zero, returning 0", ScriptOutputType.Warning);
                        left = 0.0;
                    }
                    else
                    {
                        left = CoerceToDouble(left) / rhs;
                    }
                    continue;
                }
                if (Match('%'))
                {
                    var right = ParseUnary();
                    var rhs = CoerceToDouble(right);
                    if (rhs == 0)
                    {
                        _context.EmitOutput("Warning: Modulo by zero, returning 0", ScriptOutputType.Warning);
                        left = 0.0;
                    }
                    else
                    {
                        left = CoerceToDouble(left) % rhs;
                    }
                    continue;
                }
                return left;
            }
        }

        private object? ParseUnary()
        {
            SkipWhitespace();
            if (Match('+'))
                return ParseUnary();
            if (Match('-'))
            {
                var operand = ParseUnary();
                return -CoerceToDouble(operand);
            }
            return ParsePrimary();
        }

        private object? ParsePrimary()
        {
            SkipWhitespace();

            if (_position >= _expression.Length)
                throw new FormatException("Unexpected end of expression");

            // Parenthesized sub-expression
            if (_expression[_position] == '(')
            {
                _position++;
                var value = ParseExpression();
                SkipWhitespace();
                if (!Match(')'))
                    throw new FormatException("Missing closing parenthesis in expression");
                return value;
            }

            // String literal (double or single quotes)
            if (_expression[_position] == '"' || _expression[_position] == '\'')
            {
                return ReadStringLiteral();
            }

            // Read a token (variable name, number, or function call start)
            var token = ReadToken();

            // Check if this is a function call: token followed by '('
            SkipWhitespace();
            if (_position < _expression.Length && _expression[_position] == '(')
            {
                var argsString = ReadBalancedParens();
                return ResolveFunction(token, argsString);
            }

            // Resolve as a value (number literal, variable, etc.)
            return ResolveTokenValue(token);
        }

        private string ReadStringLiteral()
        {
            var quoteChar = _expression[_position];
            _position++;
            var start = _position;

            while (_position < _expression.Length)
            {
                if (_expression[_position] == '\\' && _position + 1 < _expression.Length)
                {
                    _position += 2;
                    continue;
                }
                if (_expression[_position] == quoteChar)
                {
                    var value = _expression.Substring(start, _position - start);
                    _position++;
                    // Process escape sequences
                    value = value.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
                    value = value.Replace($"\\{quoteChar}", quoteChar.ToString());
                    // Resolve variables inside the string
                    return _context.SubstituteVariables(value);
                }
                _position++;
            }

            // Unterminated string — return what we have
            return _expression.Substring(start);
        }

        private string ReadToken()
        {
            SkipWhitespace();
            if (_position >= _expression.Length)
                throw new FormatException("Unexpected end of expression");

            var start = _position;

            // Read variable name, function name (including dotted: json.get), or number
            while (_position < _expression.Length)
            {
                var c = _expression[_position];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '$' || c == '{' || c == '}')
                {
                    _position++;
                    continue;
                }
                break;
            }

            if (start == _position)
                throw new FormatException($"Invalid token in expression at position {_position}");

            return _expression.Substring(start, _position - start);
        }

        /// <summary>
        /// Reads the content between balanced parentheses, advancing past the closing ')'.
        /// The opening '(' must be at _position when called.
        /// </summary>
        private string ReadBalancedParens()
        {
            if (_expression[_position] != '(')
                throw new FormatException("Expected '(' at start of function arguments");

            _position++; // skip opening '('
            int depth = 1;
            var start = _position;
            bool inString = false;
            char stringChar = '\0';

            while (_position < _expression.Length && depth > 0)
            {
                var c = _expression[_position];

                if (inString)
                {
                    if (c == stringChar && (_position == 0 || _expression[_position - 1] != '\\'))
                        inString = false;
                    _position++;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringChar = c;
                    _position++;
                    continue;
                }

                if (c == '(') depth++;
                else if (c == ')') depth--;

                if (depth > 0)
                    _position++;
            }

            if (depth != 0)
                throw new FormatException("Unmatched parenthesis in function call");

            var argsString = _expression.Substring(start, _position - start);
            _position++; // skip closing ')'
            return argsString;
        }

        private object? ResolveFunction(string name, string argsString)
        {
            // Try the registry first
            if (FunctionRegistry.Instance.TryEvaluate(name, argsString, _context, out var registryValue))
                return registryValue;

            // Fall back to existing function dispatch
            var fullExpr = $"{name}({argsString})";
            if (JsonUtilities.TryEvaluateFunctionExpression(fullExpr, _context, out var funcValue))
                return funcValue;

            if (JsonUtilities.TryEvaluateJsonExpression(fullExpr, _context, out var jsonValue, normalizeStructured: false))
                return jsonValue;

            throw new FormatException($"Unknown function: {name}");
        }

        private object? ResolveTokenValue(string token)
        {
            // Boolean literals
            if (token.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (token.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;
            if (token.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            // Numeric literal
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                return num;

            // .length property
            var (handled, length) = ValueResolver.TryResolveLengthExpression(token, _context.GetVariable);
            if (handled)
                return (double)length;

            // Variable lookup — return the raw value, preserving type
            var value = _context.GetVariable(token);
            if (value != null || _context.HasVariable(token))
            {
                if (value is int i) return (double)i;
                if (value is long l) return (double)l;
                if (value is double d) return d;
                if (value is float f) return (double)f;
                if (value is bool) return value;
                if (value is List<string>) return value;

                // Try numeric coercion for string values
                var strVal = value?.ToString();
                if (strVal != null && double.TryParse(strVal, NumberStyles.Float, CultureInfo.InvariantCulture, out var varNum))
                    return varNum;
                if (strVal != null && double.TryParse(strVal, out varNum))
                    return varNum;

                // Return the string value as-is (enables string concatenation)
                return strVal ?? string.Empty;
            }

            // Token contains ${...} substitution markers — process them
            if (token.Contains("${") || token.Contains("{{"))
            {
                var substituted = _context.SubstituteVariables(token);
                if (double.TryParse(substituted, NumberStyles.Float, CultureInfo.InvariantCulture, out var subNum))
                    return subNum;
                if (double.TryParse(substituted, out subNum))
                    return subNum;
                return substituted;
            }

            // Undefined identifier — return null (enables ?? null coalescing)
            return null;
        }

        // --- Type coercion helpers ---

        private static bool TryAsDouble(object? value, out double result)
        {
            result = 0;
            if (value == null) return false;
            if (value is double d) { result = d; return true; }
            if (value is int i) { result = i; return true; }
            if (value is long l) { result = l; return true; }
            if (value is float f) { result = f; return true; }
            if (value is string s)
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            return false;
        }

        private static double CoerceToDouble(object? value)
        {
            if (value == null)
                return 0;
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is float f) return f;
            if (value is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                return num;
            throw new FormatException($"Unable to coerce '{value}' to a numeric value");
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
