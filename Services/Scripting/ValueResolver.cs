using System;
using System.Collections.Generic;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Centralized value resolution utilities shared across the scripting engine.
    /// Handles property resolution (.length) and common patterns.
    /// </summary>
    public static class ValueResolver
    {
        /// <summary>
        /// Resolves the .length property on a variable value.
        /// </summary>
        public static int ResolveLength(object? value)
        {
            return value switch
            {
                List<string> list => list.Count,
                string str => str.Length,
                System.Collections.ICollection collection => collection.Count,
                _ => 0
            };
        }

        /// <summary>
        /// If expr ends with ".length", extracts the base name and resolves the length.
        /// Returns (true, length) if handled, (false, 0) otherwise.
        /// </summary>
        public static (bool handled, int length) TryResolveLengthExpression(
            string expr, Func<string, object?> getVariable)
        {
            if (!expr.EndsWith(".length", StringComparison.OrdinalIgnoreCase))
                return (false, 0);

            var baseName = expr.Substring(0, expr.Length - 7);
            var value = getVariable(baseName);
            return (true, ResolveLength(value));
        }
    }
}
