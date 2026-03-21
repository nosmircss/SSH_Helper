using System;
using System.Collections.Generic;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Represents a parsed lambda expression: x => body or (acc, x) => body.
    /// </summary>
    public sealed class LambdaExpression
    {
        /// <summary>
        /// Parameter names (e.g., ["x"] or ["acc", "x"]).
        /// </summary>
        public List<string> Parameters { get; }

        /// <summary>
        /// The body expression string.
        /// </summary>
        public string Body { get; }

        private LambdaExpression(List<string> parameters, string body)
        {
            Parameters = parameters;
            Body = body;
        }

        /// <summary>
        /// Tries to parse a lambda expression from a string.
        /// Supports: x => expr, (acc, x) => expr
        /// </summary>
        public static bool TryParse(string input, out LambdaExpression? lambda)
        {
            lambda = null;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Find the arrow '=>' at the top level (not inside quotes or parens)
            var arrowIndex = FindTopLevelArrow(input);
            if (arrowIndex < 0)
                return false;

            var paramsPart = input.Substring(0, arrowIndex).Trim();
            var body = input.Substring(arrowIndex + 2).Trim();

            if (string.IsNullOrEmpty(paramsPart) || string.IsNullOrEmpty(body))
                return false;

            var parameters = ParseParameters(paramsPart);
            if (parameters == null || parameters.Count == 0)
                return false;

            lambda = new LambdaExpression(parameters, body);
            return true;
        }

        /// <summary>
        /// Evaluates this lambda with the given argument values in the provided context.
        /// Saves and restores any existing variables that conflict with parameter names.
        /// </summary>
        public object? Evaluate(ScriptContext context, params object?[] args)
        {
            if (args.Length != Parameters.Count)
                throw new ArgumentException($"Lambda expects {Parameters.Count} argument(s) but got {args.Length}");

            // Save existing variable values
            var savedValues = new Dictionary<string, (bool existed, object? value)>();
            for (int i = 0; i < Parameters.Count; i++)
            {
                var param = Parameters[i];
                savedValues[param] = (context.HasVariable(param), context.GetVariable(param));
                context.SetVariable(param, args[i]);
            }

            try
            {
                var parser = new ExpressionParser(Body, context);
                return parser.Parse();
            }
            finally
            {
                // Restore original variable values
                foreach (var (param, (existed, value)) in savedValues)
                {
                    if (existed)
                        context.SetVariable(param, value);
                    else
                        context.RemoveVariable(param);
                }
            }
        }

        private static int FindTopLevelArrow(string input)
        {
            int depth = 0;
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < input.Length - 1; i++)
            {
                char c = input[i];

                if (inString)
                {
                    if (c == stringChar && (i == 0 || input[i - 1] != '\\'))
                        inString = false;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringChar = c;
                    continue;
                }

                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;

                if (depth == 0 && c == '=' && input[i + 1] == '>')
                    return i;
            }

            return -1;
        }

        private static List<string>? ParseParameters(string paramsPart)
        {
            // Strip surrounding parentheses if present: (a, b) -> a, b
            if (paramsPart.StartsWith("(") && paramsPart.EndsWith(")"))
                paramsPart = paramsPart.Substring(1, paramsPart.Length - 2).Trim();

            if (string.IsNullOrEmpty(paramsPart))
                return null;

            var parts = paramsPart.Split(',');
            var parameters = new List<string>(parts.Length);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed) || !IsValidParameterName(trimmed))
                    return null;
                parameters.Add(trimmed);
            }

            return parameters;
        }

        private static bool IsValidParameterName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!char.IsLetter(name[0]) && name[0] != '_') return false;
            for (int i = 1; i < name.Length; i++)
            {
                if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                    return false;
            }
            return true;
        }
    }
}
