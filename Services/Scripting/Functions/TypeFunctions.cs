using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SSH_Helper.Services.Scripting.Functions
{
    /// <summary>
    /// Type conversion and inspection functions.
    /// </summary>
    public class TypeFunctions : IFunctionCategory
    {
        public void Register(FunctionRegistry registry)
        {
            registry.Register("int", ToInt);
            registry.Register("float", ToFloat);
            registry.Register("str", ToStr);
            registry.Register("bool", ToBool);
            registry.Register("typeof", TypeOf);
            registry.Register("is_number", IsNumber);
            registry.Register("is_list", IsList);
            registry.Register("is_json", IsJson);
            registry.Register("is_empty", IsEmpty);
        }

        private static object? ToInt(string argsString, ScriptContext context)
        {
            var resolved = JsonUtilities.ResolveJsonValue(argsString.Trim(), context);
            if (resolved == null) return 0;
            if (resolved is int i) return i;
            if (resolved is long l) return (int)l;
            if (resolved is double d) return (int)d;
            if (resolved is float f) return (int)f;
            if (resolved is bool b) return b ? 1 : 0;

            var str = resolved.ToString();
            if (str != null)
            {
                if (str.Equals("true", StringComparison.OrdinalIgnoreCase)) return 1;
                if (str.Equals("false", StringComparison.OrdinalIgnoreCase)) return 0;
                if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                    return (int)num;
            }
            return 0;
        }

        private static object? ToFloat(string argsString, ScriptContext context)
        {
            var resolved = JsonUtilities.ResolveJsonValue(argsString.Trim(), context);
            if (resolved == null) return 0.0;
            if (resolved is double d) return d;
            if (resolved is int i) return (double)i;
            if (resolved is long l) return (double)l;
            if (resolved is float f) return (double)f;
            if (resolved is bool b) return b ? 1.0 : 0.0;

            var str = resolved.ToString();
            if (str != null && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                return num;
            return 0.0;
        }

        private static object? ToStr(string argsString, ScriptContext context)
        {
            var resolved = JsonUtilities.ResolveJsonValue(argsString.Trim(), context);
            if (resolved == null) return string.Empty;
            if (resolved is JsonNode node) return node.ToJsonString();
            if (resolved is JsonElement elem) return elem.GetRawText();
            return resolved.ToString() ?? string.Empty;
        }

        private static object? ToBool(string argsString, ScriptContext context)
        {
            var resolved = JsonUtilities.ResolveJsonValue(argsString.Trim(), context);
            return ValueResolver.IsTruthyValue(resolved);
        }

        private static object? TypeOf(string argsString, ScriptContext context)
        {
            var expr = argsString.Trim();
            var rawVar = GetRawVariable(expr, context);

            // If expr is a simple identifier that doesn't exist as a variable, it's null
            bool isSimpleIdent = ValueResolver.IsSimpleIdentifier(expr) ||
                                 (expr.StartsWith("${") && expr.EndsWith("}"));
            if (rawVar == null && isSimpleIdent && !HasVariable(expr, context))
                return "null";

            var value = rawVar ?? JsonUtilities.ResolveJsonValue(expr, context);

            return value switch
            {
                null => "null",
                bool => "bool",
                int or long or double or float => "number",
                List<string> => "list",
                List<object> => "list",
                JsonArray => "list",
                JsonObject => "json",
                JsonElement elem => elem.ValueKind switch
                {
                    JsonValueKind.Array => "list",
                    JsonValueKind.Object => "json",
                    JsonValueKind.Number => "number",
                    JsonValueKind.True or JsonValueKind.False => "bool",
                    JsonValueKind.Null or JsonValueKind.Undefined => "null",
                    _ => "string"
                },
                // Infer type from string content (e.g. "42" → number, "true" → bool)
                string s => InferStringType(s),
                _ => "string"
            };
        }

        private static object? IsNumber(string argsString, ScriptContext context)
        {
            var resolved = JsonUtilities.ResolveJsonValue(argsString.Trim(), context);
            if (resolved is int or long or double or float) return true;
            if (resolved is string s)
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            return false;
        }

        private static object? IsList(string argsString, ScriptContext context)
        {
            var expr = argsString.Trim();
            var rawVar = GetRawVariable(expr, context);
            if (rawVar is List<string> or JsonArray) return true;

            var resolved = rawVar ?? JsonUtilities.ResolveJsonValue(expr, context);
            return resolved is List<string> or List<object> or JsonArray ||
                   (resolved is JsonElement elem && elem.ValueKind == JsonValueKind.Array) ||
                   (resolved is string s && s.TrimStart().StartsWith("[") && IsValidJson(s));
        }

        private static object? IsJson(string argsString, ScriptContext context)
        {
            var expr = argsString.Trim();
            var rawVar = GetRawVariable(expr, context);
            if (rawVar is JsonObject or JsonArray) return true;

            var resolved = rawVar ?? JsonUtilities.ResolveJsonValue(expr, context);
            return resolved is JsonObject or JsonArray ||
                   (resolved is JsonElement elem && (elem.ValueKind == JsonValueKind.Object || elem.ValueKind == JsonValueKind.Array)) ||
                   (resolved is string s && (s.TrimStart().StartsWith("{") || s.TrimStart().StartsWith("[")) && IsValidJson(s));
        }

        private static object? IsEmpty(string argsString, ScriptContext context)
        {
            var resolved = JsonUtilities.ResolveJsonValue(argsString.Trim(), context);
            return ValueResolver.IsEmptyValue(resolved);
        }

        // --- Helpers ---

        private static object? GetRawVariable(string expr, ScriptContext context)
        {
            if (expr.StartsWith("${") && expr.EndsWith("}"))
                return context.GetVariable(expr.Substring(2, expr.Length - 3).Trim());
            if (ValueResolver.IsSimpleIdentifier(expr))
                return context.GetVariable(expr);
            return null;
        }

        private static bool HasVariable(string expr, ScriptContext context)
        {
            if (expr.StartsWith("${") && expr.EndsWith("}"))
                return context.HasVariable(expr.Substring(2, expr.Length - 3).Trim());
            if (ValueResolver.IsSimpleIdentifier(expr))
                return context.HasVariable(expr);
            return false;
        }

        private static string InferStringType(string s)
        {
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return "number";
            if (s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("false", StringComparison.OrdinalIgnoreCase))
                return "bool";
            if ((s.TrimStart().StartsWith("{") || s.TrimStart().StartsWith("[")) && IsValidJson(s))
                return "json";
            return "string";
        }

        private static bool IsValidJson(string s)
        {
            try
            {
                JsonDocument.Parse(s);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
