using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SSH_Helper.Services.Scripting.Functions
{
    /// <summary>
    /// String manipulation functions for the scripting language.
    /// Includes both new functions and migrated existing ones.
    /// </summary>
    public class StringFunctions : IFunctionCategory
    {
        public void Register(FunctionRegistry registry)
        {
            // New string functions
            registry.Register("contains", Contains);
            registry.Register("startswith", StartsWith);
            registry.Register("endswith", EndsWith);
            registry.Register("pad_left", PadLeft);
            registry.Register("pad_right", PadRight);
            registry.Register("repeat", Repeat);
            registry.Register("reverse", Reverse);
            registry.Register("regex_replace", RegexReplace);
            registry.Register("format", Format);
            registry.Register("char_at", CharAt);
            registry.Register("index_of", IndexOf);
        }

        private static object? Contains(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return false;

            var source = Resolve(args[0], context);
            var sub = Resolve(args[1], context);
            return source.Contains(sub, StringComparison.OrdinalIgnoreCase);
        }

        private static object? StartsWith(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return false;

            var source = Resolve(args[0], context);
            var prefix = Resolve(args[1], context);
            return source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static object? EndsWith(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return false;

            var source = Resolve(args[0], context);
            var suffix = Resolve(args[1], context);
            return source.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        private static object? PadLeft(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return null;

            var source = Resolve(args[0], context);
            if (!int.TryParse(Resolve(args[1], context), out var width))
                return source;

            var padChar = args.Count >= 3 ? Resolve(args[2], context) : " ";
            if (padChar.Length == 0) padChar = " ";

            return source.PadLeft(width, padChar[0]);
        }

        private static object? PadRight(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return null;

            var source = Resolve(args[0], context);
            if (!int.TryParse(Resolve(args[1], context), out var width))
                return source;

            var padChar = args.Count >= 3 ? Resolve(args[2], context) : " ";
            if (padChar.Length == 0) padChar = " ";

            return source.PadRight(width, padChar[0]);
        }

        private static object? Repeat(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return null;

            var source = Resolve(args[0], context);
            if (!int.TryParse(Resolve(args[1], context), out var count) || count < 0)
                return source;

            if (count == 0) return string.Empty;
            if (count > 10000) count = 10000; // safety cap

            return string.Concat(System.Linq.Enumerable.Repeat(source, count));
        }

        private static object? Reverse(string argsString, ScriptContext context)
        {
            // Get the raw variable to preserve list type identity
            var argExpr = argsString.Trim();
            var rawVar = ResolveRawVariable(argExpr, context);

            if (rawVar is List<string> strList)
            {
                var reversed = new List<string>(strList);
                reversed.Reverse();
                return reversed;
            }

            var resolved = JsonUtilities.ResolveJsonValue(argExpr, context);

            if (resolved is List<object> objList)
            {
                var items = objList.Select(o => o?.ToString() ?? string.Empty).ToList();
                items.Reverse();
                return items;
            }

            var str = resolved?.ToString() ?? string.Empty;
            var chars = str.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        private static object? ResolveRawVariable(string expr, ScriptContext context)
        {
            if (expr.StartsWith("${") && expr.EndsWith("}"))
                return context.GetVariable(expr.Substring(2, expr.Length - 3).Trim());

            // Simple identifier — try direct variable lookup first
            if (ValueResolver.IsSimpleIdentifier(expr))
                return context.GetVariable(expr);

            return null;
        }

        private static object? RegexReplace(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 3) return null;

            var source = Resolve(args[0], context);
            var pattern = StripDelimiters(Resolve(args[1], context));
            var replacement = Resolve(args[2], context);

            try
            {
                return Regex.Replace(source, pattern, replacement, RegexOptions.None, TimeSpan.FromSeconds(5));
            }
            catch (RegexMatchTimeoutException)
            {
                return source;
            }
            catch (ArgumentException)
            {
                return source;
            }
        }

        private static object? Format(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0) return null;

            var template = Resolve(args[0], context);
            var formatArgs = new object[args.Count - 1];
            for (int i = 1; i < args.Count; i++)
                formatArgs[i - 1] = Resolve(args[i], context);

            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, formatArgs);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        private static object? CharAt(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return null;

            var source = Resolve(args[0], context);
            if (!int.TryParse(Resolve(args[1], context), out var index))
                return null;

            if (index < 0 || index >= source.Length) return null;
            return source[index].ToString();
        }

        private static object? IndexOf(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return -1;

            var source = Resolve(args[0], context);
            var sub = Resolve(args[1], context);
            return source.IndexOf(sub, StringComparison.OrdinalIgnoreCase);
        }

        // --- Helpers ---

        private static string Resolve(string expr, ScriptContext context)
        {
            return JsonUtilities.ResolveJsonValue(expr, context)?.ToString() ?? string.Empty;
        }

        private static string StripDelimiters(string pattern)
        {
            if (pattern.Length >= 2)
            {
                if ((pattern.StartsWith("/") && pattern.EndsWith("/")) ||
                    (pattern.StartsWith("'") && pattern.EndsWith("'")) ||
                    (pattern.StartsWith("\"") && pattern.EndsWith("\"")))
                {
                    return pattern.Substring(1, pattern.Length - 2);
                }
            }
            return pattern;
        }
    }
}
