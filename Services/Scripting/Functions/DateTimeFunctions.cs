using System;
using System.Globalization;

namespace SSH_Helper.Services.Scripting.Functions
{
    /// <summary>
    /// Date and time functions for the scripting language.
    /// </summary>
    public class DateTimeFunctions : IFunctionCategory
    {
        private static readonly string[] ParseFormats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd",
            "MM/dd/yyyy HH:mm:ss",
            "MM/dd/yyyy",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy"
        };

        public void Register(FunctionRegistry registry)
        {
            registry.Register("now", Now);
            registry.Register("now_local", NowLocal);
            registry.Register("now_utc", NowUtc);
            registry.Register("epoch", Epoch);
            registry.Register("epoch_to_date", EpochToDate);
            registry.Register("date_add", DateAdd);
            registry.Register("date_diff", DateDiff);
            registry.Register("date_format", DateFormat);
            registry.Register("parse_date", ParseDate);
        }

        private static object? Now(string argsString, ScriptContext context)
        {
            return DateTime.Now.ToString(ResolveFormat(argsString, context), CultureInfo.InvariantCulture);
        }

        private static object? NowLocal(string argsString, ScriptContext context)
        {
            return DateTime.Now.ToString(ResolveFormat(argsString, context), CultureInfo.InvariantCulture);
        }

        private static object? NowUtc(string argsString, ScriptContext context)
        {
            return DateTime.UtcNow.ToString(ResolveFormat(argsString, context), CultureInfo.InvariantCulture);
        }

        private static object? Epoch(string argsString, ScriptContext context)
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static object? EpochToDate(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0) return null;

            var epochStr = JsonUtilities.ResolveJsonValue(args[0], context)?.ToString() ?? "0";
            if (!long.TryParse(epochStr, out var epoch))
                return null;

            var dt = DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime;
            var format = "yyyy-MM-dd HH:mm:ss";
            if (args.Count >= 2)
            {
                var resolved = JsonUtilities.ResolveJsonValue(args[1], context)?.ToString();
                if (!string.IsNullOrEmpty(resolved))
                    format = resolved;
            }

            return dt.ToString(format, CultureInfo.InvariantCulture);
        }

        private static object? DateAdd(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 3) return null;

            var tsStr = JsonUtilities.ResolveJsonValue(args[0], context)?.ToString() ?? string.Empty;
            if (!TryParseDateTime(tsStr, out var dt))
                return null;

            var amountStr = JsonUtilities.ResolveJsonValue(args[1], context)?.ToString() ?? "0";
            if (!double.TryParse(amountStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
                return null;

            var unit = JsonUtilities.ResolveJsonValue(args[2], context)?.ToString()?.ToLowerInvariant() ?? "seconds";

            var result = unit switch
            {
                "seconds" or "second" or "s" => dt.AddSeconds(amount),
                "minutes" or "minute" or "m" => dt.AddMinutes(amount),
                "hours" or "hour" or "h" => dt.AddHours(amount),
                "days" or "day" or "d" => dt.AddDays(amount),
                "weeks" or "week" or "w" => dt.AddDays(amount * 7),
                "months" or "month" or "mo" => dt.AddMonths((int)amount),
                "years" or "year" or "y" => dt.AddYears((int)amount),
                _ => dt
            };

            return result.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static object? DateDiff(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 3) return null;

            var aStr = JsonUtilities.ResolveJsonValue(args[0], context)?.ToString() ?? string.Empty;
            var bStr = JsonUtilities.ResolveJsonValue(args[1], context)?.ToString() ?? string.Empty;

            if (!TryParseDateTime(aStr, out var a) || !TryParseDateTime(bStr, out var b))
                return null;

            var diff = a - b;
            var unit = JsonUtilities.ResolveJsonValue(args[2], context)?.ToString()?.ToLowerInvariant() ?? "seconds";

            double result = unit switch
            {
                "seconds" or "second" or "s" => diff.TotalSeconds,
                "minutes" or "minute" or "m" => diff.TotalMinutes,
                "hours" or "hour" or "h" => diff.TotalHours,
                "days" or "day" or "d" => diff.TotalDays,
                "weeks" or "week" or "w" => diff.TotalDays / 7.0,
                _ => diff.TotalSeconds
            };

            return Math.Round(result, 2);
        }

        private static object? DateFormat(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return null;

            var tsStr = JsonUtilities.ResolveJsonValue(args[0], context)?.ToString() ?? string.Empty;
            if (!TryParseDateTime(tsStr, out var dt))
                return null;

            var format = JsonUtilities.ResolveJsonValue(args[1], context)?.ToString() ?? "yyyy-MM-dd HH:mm:ss";
            return dt.ToString(format, CultureInfo.InvariantCulture);
        }

        private static object? ParseDate(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return null;

            var input = JsonUtilities.ResolveJsonValue(args[0], context)?.ToString() ?? string.Empty;
            var format = JsonUtilities.ResolveJsonValue(args[1], context)?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(format) ||
                !DateTime.TryParseExact(input, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return null;
            }

            var outFormat = "yyyy-MM-dd HH:mm:ss";
            if (args.Count >= 3)
            {
                var resolved = JsonUtilities.ResolveJsonValue(args[2], context)?.ToString();
                if (!string.IsNullOrEmpty(resolved))
                    outFormat = resolved;
            }

            return dt.ToString(outFormat, CultureInfo.InvariantCulture);
        }

        private static string ResolveFormat(string argsString, ScriptContext context)
        {
            var format = "yyyy-MM-dd HH:mm:ss";
            if (!string.IsNullOrWhiteSpace(argsString))
            {
                var resolved = JsonUtilities.ResolveJsonValue(argsString.Trim(), context)?.ToString();
                if (!string.IsNullOrEmpty(resolved))
                    format = resolved;
            }
            return format;
        }

        private static bool TryParseDateTime(string input, out DateTime result)
        {
            return DateTime.TryParseExact(input, ParseFormats,
                       CultureInfo.InvariantCulture, DateTimeStyles.None, out result) ||
                   DateTime.TryParse(input, CultureInfo.InvariantCulture,
                       DateTimeStyles.None, out result);
        }
    }
}
