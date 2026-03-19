using System;
using System.Collections.Generic;
using System.Globalization;

namespace SSH_Helper.Services.Scripting.Functions
{
    /// <summary>
    /// Mathematical functions for the scripting language.
    /// </summary>
    public class MathFunctions : IFunctionCategory
    {
        private static readonly Random _random = new();

        public void Register(FunctionRegistry registry)
        {
            registry.Register("abs", Abs);
            registry.Register("min", Min);
            registry.Register("max", Max);
            registry.Register("round", Round);
            registry.Register("floor", Floor);
            registry.Register("ceil", Ceil);
            registry.Register("random", RandomInt);
            registry.Register("pow", Pow);
            registry.Register("sqrt", Sqrt);
            registry.Register("clamp", Clamp);
            registry.Register("iif", Iif);
        }

        private static object? Abs(string argsString, ScriptContext context)
        {
            var val = ResolveDouble(argsString.Trim(), context);
            if (val == null) return null;
            var result = Math.Abs(val.Value);
            return IsInteger(result) ? (object)(int)result : result;
        }

        private static object? Min(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0) return null;

            double? min = null;
            foreach (var arg in args)
            {
                var val = ResolveDouble(arg, context);
                if (val == null) continue;
                if (min == null || val.Value < min.Value)
                    min = val.Value;
            }

            if (min == null) return null;
            return IsInteger(min.Value) ? (object)(int)min.Value : min.Value;
        }

        private static object? Max(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0) return null;

            double? max = null;
            foreach (var arg in args)
            {
                var val = ResolveDouble(arg, context);
                if (val == null) continue;
                if (max == null || val.Value > max.Value)
                    max = val.Value;
            }

            if (max == null) return null;
            return IsInteger(max.Value) ? (object)(int)max.Value : max.Value;
        }

        private static object? Round(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0) return null;

            var val = ResolveDouble(args[0], context);
            if (val == null) return null;

            int decimals = 0;
            if (args.Count >= 2)
            {
                var decStr = JsonUtilities.ResolveJsonValue(args[1], context)?.ToString() ?? "0";
                int.TryParse(decStr, out decimals);
            }

            var result = Math.Round(val.Value, Math.Max(0, Math.Min(decimals, 15)), MidpointRounding.AwayFromZero);
            return decimals == 0 && IsInteger(result) ? (object)(int)result : result;
        }

        private static object? Floor(string argsString, ScriptContext context)
        {
            var val = ResolveDouble(argsString.Trim(), context);
            if (val == null) return null;
            return (int)Math.Floor(val.Value);
        }

        private static object? Ceil(string argsString, ScriptContext context)
        {
            var val = ResolveDouble(argsString.Trim(), context);
            if (val == null) return null;
            return (int)Math.Ceiling(val.Value);
        }

        private static object? RandomInt(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);

            int min = 0, max = 100;
            if (args.Count >= 1 && !string.IsNullOrWhiteSpace(args[0]))
            {
                var minStr = JsonUtilities.ResolveJsonValue(args[0], context)?.ToString() ?? "0";
                if (int.TryParse(minStr, out var parsedMin))
                    min = parsedMin;
            }
            if (args.Count >= 2 && !string.IsNullOrWhiteSpace(args[1]))
            {
                var maxStr = JsonUtilities.ResolveJsonValue(args[1], context)?.ToString() ?? "100";
                if (int.TryParse(maxStr, out var parsedMax))
                    max = parsedMax;
            }

            if (min > max) (min, max) = (max, min);
            if (min == max)
                return min;

            lock (_random)
            {
                var value = _random.NextInt64(min, (long)max + 1);
                return (int)value;
            }
        }

        private static object? Pow(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return null;

            var baseVal = ResolveDouble(args[0], context);
            var expVal = ResolveDouble(args[1], context);
            if (baseVal == null || expVal == null) return null;

            var result = Math.Pow(baseVal.Value, expVal.Value);
            return IsInteger(result) ? (object)(int)result : result;
        }

        private static object? Sqrt(string argsString, ScriptContext context)
        {
            var val = ResolveDouble(argsString.Trim(), context);
            if (val == null) return null;
            var result = Math.Sqrt(val.Value);
            return IsInteger(result) ? (object)(int)result : result;
        }

        private static object? Clamp(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 3) return null;

            var val = ResolveDouble(args[0], context);
            var min = ResolveDouble(args[1], context);
            var max = ResolveDouble(args[2], context);
            if (val == null || min == null || max == null) return null;

            var result = Math.Clamp(val.Value, min.Value, max.Value);
            return IsInteger(result) ? (object)(int)result : result;
        }

        /// <summary>
        /// YAML-safe ternary: iif(condition, true_value, false_value).
        /// Condition is evaluated using the same expression evaluator as assert/if,
        /// supporting comparison operators (==, !=, >, <, >=, <=) and logical operators.
        /// Falls back to truthiness check for simple values.
        /// </summary>
        private static object? Iif(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 3) return null;

            var conditionStr = context.SubstituteVariables(args[0].Trim());
            var evaluator = new ExpressionEvaluator(context);
            bool isTruthy;
            try
            {
                isTruthy = evaluator.Evaluate(conditionStr);
            }
            catch
            {
                // If the expression evaluator can't parse it, fall back to truthiness
                var resolvedCondition = JsonUtilities.ResolveJsonValue(args[0].Trim(), context);
                isTruthy = ValueResolver.IsTruthyValue(resolvedCondition);
            }

            var resultExpr = isTruthy ? args[1] : args[2];
            return JsonUtilities.ResolveJsonValue(resultExpr, context);
        }

        // --- Helpers ---

        private static double? ResolveDouble(string expr, ScriptContext context)
        {
            var resolved = JsonUtilities.ResolveJsonValue(expr, context);
            if (resolved == null) return null;

            if (resolved is int i) return i;
            if (resolved is long l) return l;
            if (resolved is double d) return d;
            if (resolved is float f) return f;

            var str = resolved.ToString();
            if (str != null && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                return num;
            return null;
        }

        private static bool IsInteger(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) &&
                   value == Math.Truncate(value) &&
                   value >= int.MinValue && value <= int.MaxValue;
        }
    }
}
