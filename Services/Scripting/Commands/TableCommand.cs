using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Formats data into aligned columns for display.
    /// </summary>
    public class TableCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Table == null || string.IsNullOrWhiteSpace(step.Table.Data))
                return Task.FromResult(CommandResult.Fail("Table command has no data source"));

            // Resolve data variable
            var dataRef = step.Table.Data.Trim();
            if (dataRef.StartsWith("${", StringComparison.Ordinal) && dataRef.EndsWith("}", StringComparison.Ordinal))
                dataRef = dataRef.Substring(2, dataRef.Length - 3);

            var rawData = context.GetVariable(dataRef);
            if (rawData == null)
            {
                context.EmitOutput($"Table: variable '{dataRef}' not found or empty", ScriptOutputType.Warning);
                return Task.FromResult(CommandResult.Ok());
            }

            // Convert data to list of rows (each row is a dictionary of field->value)
            var rows = ConvertToRows(rawData);
            if (rows.Count == 0)
            {
                context.EmitOutput("Table: no data rows", ScriptOutputType.Debug);
                return Task.FromResult(CommandResult.Ok());
            }

            // Determine columns
            List<TableColumnInfo> columns;
            if (step.Table.Columns != null && step.Table.Columns.Count > 0)
            {
                columns = step.Table.Columns.Select(c => new TableColumnInfo
                {
                    Header = c.Header,
                    Field = c.Field ?? c.Header,
                    Align = c.Align ?? step.Table.Align,
                    FixedWidth = c.Width
                }).ToList();
            }
            else
            {
                // Auto-detect columns from data keys
                var allKeys = rows.SelectMany(r => r.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                columns = allKeys.Select(k => new TableColumnInfo
                {
                    Header = k,
                    Field = k,
                    Align = step.Table.Align,
                    FixedWidth = null
                }).ToList();

                // For single-column list-like data, show a meaningful header when sourced from a variable.
                if (columns.Count == 1 &&
                    columns[0].Field.Equals("Value", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(dataRef))
                {
                    columns[0].Header = dataRef;
                }
            }

            // Calculate column widths
            foreach (var col in columns)
            {
                if (col.FixedWidth.HasValue && col.FixedWidth.Value > 0)
                {
                    col.Width = col.FixedWidth.Value;
                }
                else
                {
                    var maxDataWidth = rows.Max(r =>
                        r.TryGetValue(col.Field, out var val) ? (val?.Length ?? 0) : 0);
                    col.Width = Math.Max(col.Header.Length, maxDataWidth);
                }
            }

            // Build table output
            var sb = new StringBuilder();

            if (step.Table.ShowHeader)
            {
                // Header row
                var headerParts = columns.Select(c => AlignText(c.Header, c.Width, c.Align));
                sb.AppendLine(string.Join("  ", headerParts));

                // Separator
                var separatorParts = columns.Select(c => new string('-', c.Width));
                sb.AppendLine(string.Join("  ", separatorParts));
            }

            // Data rows
            foreach (var row in rows)
            {
                var cellParts = columns.Select(c =>
                {
                    var value = row.TryGetValue(c.Field, out var val) ? (val ?? "-") : "-";
                    return AlignText(value, c.Width, c.Align);
                });
                sb.AppendLine(string.Join("  ", cellParts));
            }

            var tableText = sb.ToString().TrimEnd();

            context.EmitOutput(tableText, ScriptOutputType.Info);

            if (!string.IsNullOrWhiteSpace(step.Table.Into))
            {
                context.SetVariable(step.Table.Into, tableText);
            }

            return Task.FromResult(CommandResult.Ok());
        }

        private static List<Dictionary<string, string>> ConvertToRows(object? data)
        {
            var rows = new List<Dictionary<string, string>>();

            if (data == null)
                return rows;

            if (data is List<string> stringList)
            {
                // Simple list -> single-column table with "Value" header
                foreach (var item in stringList)
                {
                    rows.Add(CreateValueRow(item ?? string.Empty));
                }
                return rows;
            }

            if (data is JsonElement jsonElement)
            {
                AddRowsFromJsonElement(jsonElement, rows);
                return rows;
            }

            if (data is JsonNode jsonNode)
            {
                AddRowsFromJsonNode(jsonNode, rows);
                return rows;
            }

            if (TryConvertDictionaryLikeObject(data, out var objectRow))
            {
                rows.Add(objectRow);
                return rows;
            }

            if (data is string text)
            {
                if (TryParseJsonElement(text, out var parsedElement))
                {
                    AddRowsFromJsonElement(parsedElement, rows);
                    return rows;
                }

                // Not JSON - treat as newline-delimited
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    rows.Add(CreateValueRow(line));
                }
                return rows;
            }

            if (data is IEnumerable enumerable && data is not string)
            {
                foreach (var item in enumerable)
                {
                    AddRowsFromValue(item, rows);
                }
                return rows;
            }

            AddRowsFromValue(data, rows);
            return rows;
        }

        private static Dictionary<string, string> CreateValueRow(string value)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Value"] = value
            };
        }

        private static bool TryParseJsonElement(string value, out JsonElement element)
        {
            try
            {
                using var document = JsonDocument.Parse(value);
                element = document.RootElement.Clone();
                return true;
            }
            catch
            {
                element = default;
                return false;
            }
        }

        private static void AddRowsFromJsonElement(JsonElement element, List<Dictionary<string, string>> rows)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        AddRowsFromJsonElementValue(item, rows);
                    }
                    break;
                case JsonValueKind.Object:
                    rows.Add(ConvertJsonObjectElementToRow(element));
                    break;
                default:
                    rows.Add(CreateValueRow(ConvertJsonElementToCellValue(element)));
                    break;
            }
        }

        private static void AddRowsFromJsonElementValue(JsonElement value, List<Dictionary<string, string>> rows)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                rows.Add(ConvertJsonObjectElementToRow(value));
                return;
            }

            rows.Add(CreateValueRow(ConvertJsonElementToCellValue(value)));
        }

        private static Dictionary<string, string> ConvertJsonObjectElementToRow(JsonElement element)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                row[property.Name] = ConvertJsonElementToCellValue(property.Value);
            }
            return row;
        }

        private static string ConvertJsonElementToCellValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.Undefined => string.Empty,
                _ => value.ToString()
            };
        }

        private static void AddRowsFromJsonNode(JsonNode? node, List<Dictionary<string, string>> rows)
        {
            if (node == null)
                return;

            switch (node)
            {
                case JsonArray array:
                    foreach (var item in array)
                    {
                        AddRowsFromJsonNodeValue(item, rows);
                    }
                    break;
                case JsonObject obj:
                    rows.Add(ConvertJsonObjectNodeToRow(obj));
                    break;
                default:
                    rows.Add(CreateValueRow(ConvertJsonNodeToCellValue(node)));
                    break;
            }
        }

        private static void AddRowsFromJsonNodeValue(JsonNode? node, List<Dictionary<string, string>> rows)
        {
            if (node is JsonObject jsonObject)
            {
                rows.Add(ConvertJsonObjectNodeToRow(jsonObject));
                return;
            }

            rows.Add(CreateValueRow(ConvertJsonNodeToCellValue(node)));
        }

        private static Dictionary<string, string> ConvertJsonObjectNodeToRow(JsonObject node)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in node)
            {
                row[property.Key] = ConvertJsonNodeToCellValue(property.Value);
            }
            return row;
        }

        private static string ConvertJsonNodeToCellValue(JsonNode? node)
        {
            if (node == null)
                return string.Empty;

            if (node is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue<string>(out var stringValue))
                    return stringValue;
                if (jsonValue.TryGetValue<long>(out var longValue))
                    return longValue.ToString();
                if (jsonValue.TryGetValue<double>(out var doubleValue))
                    return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (jsonValue.TryGetValue<bool>(out var boolValue))
                    return boolValue ? "true" : "false";
            }

            return node.ToJsonString();
        }

        private static void AddRowsFromValue(object? value, List<Dictionary<string, string>> rows)
        {
            if (value == null)
            {
                rows.Add(CreateValueRow(string.Empty));
                return;
            }

            if (value is JsonElement jsonElement)
            {
                AddRowsFromJsonElementValue(jsonElement, rows);
                return;
            }

            if (value is JsonNode jsonNode)
            {
                AddRowsFromJsonNodeValue(jsonNode, rows);
                return;
            }

            if (TryConvertDictionaryLikeObject(value, out var row))
            {
                rows.Add(row);
                return;
            }

            rows.Add(CreateValueRow(value.ToString() ?? string.Empty));
        }

        private static bool TryConvertDictionaryLikeObject(object value, out Dictionary<string, string> row)
        {
            row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = entry.Key?.ToString();
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    row[key] = entry.Value?.ToString() ?? string.Empty;
                }

                return row.Count > 0;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                var sawEntry = false;
                foreach (var entry in enumerable)
                {
                    if (entry == null)
                        continue;

                    var entryType = entry.GetType();
                    var keyProperty = entryType.GetProperty("Key");
                    var valueProperty = entryType.GetProperty("Value");
                    if (keyProperty == null || valueProperty == null)
                    {
                        row.Clear();
                        return false;
                    }

                    var key = keyProperty.GetValue(entry)?.ToString();
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    var rawValue = valueProperty.GetValue(entry);
                    row[key] = rawValue?.ToString() ?? string.Empty;
                    sawEntry = true;
                }

                return sawEntry;
            }

            return false;
        }

        private static string AlignText(string text, int width, string align)
        {
            if (text.Length >= width)
                return text.Substring(0, width);

            return align.ToLowerInvariant() switch
            {
                "right" => text.PadLeft(width),
                "center" => text.PadLeft((width + text.Length) / 2).PadRight(width),
                _ => text.PadRight(width)
            };
        }

        private class TableColumnInfo
        {
            public string Header { get; set; } = string.Empty;
            public string Field { get; set; } = string.Empty;
            public string Align { get; set; } = "left";
            public int? FixedWidth { get; set; }
            public int Width { get; set; }
        }
    }
}
