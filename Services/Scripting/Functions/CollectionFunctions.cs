using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SSH_Helper.Services.Scripting.Functions
{
    /// <summary>
    /// Higher-order collection functions with arrow-style lambda support.
    /// </summary>
    public class CollectionFunctions : IFunctionCategory
    {
        public void Register(FunctionRegistry registry)
        {
            registry.Register("map", Map);
            registry.Register("filter", Filter);
            registry.Register("reduce", Reduce);
            registry.Register("find", Find);
            registry.Register("any", Any);
            registry.Register("all", All);
            registry.Register("count", Count);
            registry.Register("range", Range);
            registry.Register("slice", Slice);
            registry.Register("flatten", Flatten);
            registry.Register("zip", Zip);
        }

        private static object? Map(string argsString, ScriptContext context)
        {
            if (!SplitCollectionAndLambda(argsString, context, out var items, out var lambda))
                return null;

            var result = new List<string>(items.Count);
            foreach (var item in items)
            {
                var value = lambda!.Evaluate(context, item);
                result.Add(value?.ToString() ?? string.Empty);
            }
            return result;
        }

        private static object? Filter(string argsString, ScriptContext context)
        {
            if (!SplitCollectionAndLambda(argsString, context, out var items, out var lambda))
                return null;

            var result = new List<string>();
            foreach (var item in items)
            {
                var value = lambda!.Evaluate(context, item);
                if (ValueResolver.IsTruthyValue(value))
                    result.Add(item);
            }
            return result;
        }

        private static object? Reduce(string argsString, ScriptContext context)
        {
            var parts = JsonUtilities.SplitTopLevelCommas(argsString);
            if (parts.Count < 3) return null;

            var items = ResolveList(parts[0], context);
            if (!LambdaExpression.TryParse(parts[1].Trim(), out var lambda))
                return null;

            if (lambda!.Parameters.Count != 2)
                return null;

            var accumulator = ResolveValue(parts[2], context);

            foreach (var item in items)
            {
                accumulator = lambda.Evaluate(context, accumulator, item);
            }

            return accumulator;
        }

        private static object? Find(string argsString, ScriptContext context)
        {
            if (!SplitCollectionAndLambda(argsString, context, out var items, out var lambda))
                return null;

            foreach (var item in items)
            {
                var value = lambda!.Evaluate(context, item);
                if (ValueResolver.IsTruthyValue(value))
                    return item;
            }
            return null;
        }

        private static object? Any(string argsString, ScriptContext context)
        {
            if (!SplitCollectionAndLambda(argsString, context, out var items, out var lambda))
                return false;

            foreach (var item in items)
            {
                var value = lambda!.Evaluate(context, item);
                if (ValueResolver.IsTruthyValue(value))
                    return true;
            }
            return false;
        }

        private static object? All(string argsString, ScriptContext context)
        {
            if (!SplitCollectionAndLambda(argsString, context, out var items, out var lambda))
                return false;

            foreach (var item in items)
            {
                var value = lambda!.Evaluate(context, item);
                if (!ValueResolver.IsTruthyValue(value))
                    return false;
            }
            return true;
        }

        private static object? Count(string argsString, ScriptContext context)
        {
            var parts = JsonUtilities.SplitTopLevelCommas(argsString);
            if (parts.Count == 0) return 0;

            var items = ResolveList(parts[0], context);

            // No lambda — just count
            if (parts.Count < 2 || !LambdaExpression.TryParse(parts[1].Trim(), out var lambda))
                return items.Count;

            int count = 0;
            foreach (var item in items)
            {
                var value = lambda!.Evaluate(context, item);
                if (ValueResolver.IsTruthyValue(value))
                    count++;
            }
            return count;
        }

        private static object? Range(string argsString, ScriptContext context)
        {
            var parts = JsonUtilities.SplitTopLevelCommas(argsString);
            if (parts.Count < 2) return null;

            if (!TryResolveInt(parts[0], context, out var start)) return null;
            if (!TryResolveInt(parts[1], context, out var end)) return null;

            var step = 1;
            if (parts.Count >= 3)
                TryResolveInt(parts[2], context, out step);

            if (step == 0) return null;

            var result = new List<string>();
            const int maxItems = 100000;

            if (step > 0)
            {
                for (int i = start; i < end && result.Count < maxItems; i += step)
                    result.Add(i.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                for (int i = start; i > end && result.Count < maxItems; i += step)
                    result.Add(i.ToString(CultureInfo.InvariantCulture));
            }

            return result;
        }

        private static object? Slice(string argsString, ScriptContext context)
        {
            var parts = JsonUtilities.SplitTopLevelCommas(argsString);
            if (parts.Count < 2) return null;

            var items = ResolveList(parts[0], context);
            if (!TryResolveInt(parts[1], context, out var start)) return null;

            // Normalize negative index
            if (start < 0) start = Math.Max(0, items.Count + start);
            if (start > items.Count) start = items.Count;

            var end = items.Count;
            if (parts.Count >= 3 && TryResolveInt(parts[2], context, out var endVal))
            {
                end = endVal < 0 ? Math.Max(0, items.Count + endVal) : Math.Min(endVal, items.Count);
            }

            if (end <= start) return new List<string>();
            return items.GetRange(start, end - start);
        }

        private static object? Flatten(string argsString, ScriptContext context)
        {
            var resolved = JsonUtilities.ResolveJsonValue(argsString.Trim(), context);
            var result = new List<string>();

            if (resolved is JsonArray jsonArray)
            {
                foreach (var item in jsonArray)
                {
                    if (item is JsonArray innerArray)
                    {
                        foreach (var inner in innerArray)
                            result.Add(JsonUtilities.JsonNodeToStringValue(inner));
                    }
                    else
                    {
                        result.Add(JsonUtilities.JsonNodeToStringValue(item));
                    }
                }
                return result;
            }

            var items = ValueResolver.ResolveListValue(resolved);
            foreach (var item in items)
            {
                // Try parsing each item as a JSON array
                var trimmed = item.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    try
                    {
                        var innerArray = JsonSerializer.Deserialize<JsonElement>(trimmed);
                        if (innerArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var inner in innerArray.EnumerateArray())
                                result.Add(inner.ValueKind == JsonValueKind.String ? inner.GetString()! : inner.GetRawText());
                            continue;
                        }
                    }
                    catch { /* Not JSON array, add as-is */ }
                }
                result.Add(item);
            }

            return result;
        }

        private static object? Zip(string argsString, ScriptContext context)
        {
            var parts = JsonUtilities.SplitTopLevelCommas(argsString);
            if (parts.Count < 2) return null;

            var list1 = ResolveList(parts[0], context);
            var list2 = ResolveList(parts[1], context);
            var minLen = Math.Min(list1.Count, list2.Count);

            var result = new List<string>(minLen);
            for (int i = 0; i < minLen; i++)
            {
                // Encode each pair as a JSON array string
                result.Add($"[\"{EscapeJson(list1[i])}\",\"{EscapeJson(list2[i])}\"]");
            }
            return result;
        }

        // --- Helpers ---

        /// <summary>
        /// Splits "collection, lambda" arguments for most collection functions.
        /// </summary>
        private static bool SplitCollectionAndLambda(
            string argsString, ScriptContext context,
            out List<string> items, out LambdaExpression? lambda)
        {
            items = new List<string>();
            lambda = null;

            var parts = JsonUtilities.SplitTopLevelCommas(argsString);
            if (parts.Count < 2) return false;

            items = ResolveList(parts[0], context);
            return LambdaExpression.TryParse(parts[1].Trim(), out lambda);
        }

        private static List<string> ResolveList(string expr, ScriptContext context)
        {
            var resolved = JsonUtilities.ResolveJsonValue(expr.Trim(), context);
            return ValueResolver.ResolveListValue(resolved);
        }

        private static object? ResolveValue(string expr, ScriptContext context)
        {
            return JsonUtilities.ResolveJsonValue(expr.Trim(), context);
        }

        private static bool TryResolveInt(string expr, ScriptContext context, out int result)
        {
            result = 0;
            var resolved = JsonUtilities.ResolveJsonValue(expr.Trim(), context);
            if (resolved == null) return false;
            return int.TryParse(resolved.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
