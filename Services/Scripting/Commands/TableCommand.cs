using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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

            if (data is List<string> stringList)
            {
                // Simple list -> single-column table with "Value" header
                foreach (var item in stringList)
                {
                    rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Value"] = item ?? ""
                    });
                }
                return rows;
            }

            if (data is string jsonStr)
            {
                // Try parsing as JSON array
                try
                {
                    var token = JToken.Parse(jsonStr);
                    if (token is JArray array)
                    {
                        foreach (var item in array)
                        {
                            if (item is JObject obj)
                            {
                                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                foreach (var prop in obj.Properties())
                                {
                                    row[prop.Name] = prop.Value?.ToString() ?? "";
                                }
                                rows.Add(row);
                            }
                            else
                            {
                                rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["Value"] = item.ToString()
                                });
                            }
                        }
                        return rows;
                    }
                }
                catch
                {
                    // Not JSON - treat as newline-delimited
                    var lines = jsonStr.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Value"] = line
                        });
                    }
                    return rows;
                }
            }

            if (data is List<object> objList)
            {
                foreach (var item in objList)
                {
                    rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Value"] = item?.ToString() ?? ""
                    });
                }
                return rows;
            }

            return rows;
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
