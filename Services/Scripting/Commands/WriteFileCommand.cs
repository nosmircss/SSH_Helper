using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Writes content to a text file (append or overwrite).
    /// Supports text, JSON, and CSV formats.
    /// </summary>
    public class WriteFileCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Writefile == null)
                return Task.FromResult(CommandResult.Fail("Writefile command has no options"));

            if (string.IsNullOrEmpty(step.Writefile.Path))
                return Task.FromResult(CommandResult.Fail("Writefile command requires a 'path' property"));

            try
            {
                // Substitute variables in path
                var filePath = context.SubstituteVariables(step.Writefile.Path);

                // Validate path for security
                if (!ScriptFileAccessValidator.ValidateWritePath(filePath, out var pathError))
                {
                    context.EmitOutput(pathError!, ScriptOutputType.Error);

                    if (step.OnError?.ToLowerInvariant() == "continue")
                        return Task.FromResult(CommandResult.Ok(pathError));

                    return Task.FromResult(CommandResult.Fail(pathError!));
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Get content based on format
                var format = step.Writefile.Format?.ToLowerInvariant() ?? "text";
                var mode = step.Writefile.Mode?.ToLowerInvariant() ?? "overwrite";
                string content;

                switch (format)
                {
                    case "json":
                        content = FormatAsJson(step.Writefile, context, filePath, mode);
                        break;
                    case "jsonl":
                        content = FormatAsJsonLine(step.Writefile, context);
                        break;
                    case "csv":
                        content = FormatAsCsv(step.Writefile, context);
                        break;
                    default:
                        content = context.SubstituteVariables(step.Writefile.Content ?? string.Empty);
                        break;
                }

                // Write based on mode (default: overwrite)
                if (mode == "append")
                {
                    // For JSON with append, the merging is handled in FormatAsJson
                    // For JSONL, we always append a line
                    // For text/csv, append with newline
                    if (format == "json")
                    {
                        // JSON append merging writes the full merged content
                        File.WriteAllText(filePath, content);
                        context.EmitOutput($"Merged JSON to '{filePath}'", ScriptOutputType.Debug);
                    }
                    else
                    {
                        var contentToAppend = content;
                        if (!contentToAppend.EndsWith(Environment.NewLine))
                            contentToAppend += Environment.NewLine;

                        File.AppendAllText(filePath, contentToAppend);
                        context.EmitOutput($"Appended to '{filePath}' ({format})", ScriptOutputType.Debug);
                    }
                }
                else
                {
                    // Overwrite mode (default)
                    File.WriteAllText(filePath, content);
                    context.EmitOutput($"Wrote to '{filePath}' (overwrite, {format})", ScriptOutputType.Debug);
                }

                return Task.FromResult(CommandResult.Ok());
            }
            catch (UnauthorizedAccessException ex)
            {
                var errorMsg = $"Access denied writing file: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return Task.FromResult(CommandResult.Ok(errorMsg));

                return Task.FromResult(CommandResult.Fail(errorMsg));
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error writing file: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return Task.FromResult(CommandResult.Ok(errorMsg));

                return Task.FromResult(CommandResult.Fail(errorMsg));
            }
        }

        /// <summary>
        /// Formats content as JSON. In append mode, merges arrays or objects with existing file content.
        /// </summary>
        private string FormatAsJson(WritefileOptions options, ScriptContext context, string filePath, string mode)
        {
            var rawContent = options.Content ?? string.Empty;
            object? newValue = null;

            // Check if content is a variable reference like ${varname}
            if (rawContent.StartsWith("${") && rawContent.EndsWith("}"))
            {
                var varName = rawContent.Substring(2, rawContent.Length - 3);
                newValue = context.GetVariable(varName);
            }
            else
            {
                // Otherwise, substitute variables and try to parse as JSON
                var substituted = context.SubstituteVariables(rawContent);

                try
                {
                    using var doc = JsonDocument.Parse(substituted);
                    newValue = doc.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    // Not valid JSON - emit a debug message explaining why
                    context.EmitOutput($"Content is not valid JSON ({ex.Message}), wrapping as string", ScriptOutputType.Debug);
                    newValue = substituted;
                }
            }

            // Handle append mode - merge with existing file content
            if (mode == "append" && File.Exists(filePath))
            {
                try
                {
                    var existingContent = File.ReadAllText(filePath);
                    if (!string.IsNullOrWhiteSpace(existingContent))
                    {
                        var merged = MergeJsonContent(existingContent, newValue, options.Pretty, context);
                        if (merged != null)
                            return merged;
                    }
                }
                catch (Exception ex)
                {
                    context.EmitOutput($"Could not read existing file for merge: {ex.Message}", ScriptOutputType.Debug);
                }
            }

            return SerializeToJson(newValue, options.Pretty);
        }

        /// <summary>
        /// Merges new JSON content with existing file content.
        /// Arrays are concatenated, objects are deep-merged (new values override existing).
        /// </summary>
        private string? MergeJsonContent(string existingContent, object? newValue, bool pretty, ScriptContext context)
        {
            try
            {
                var existingNode = JsonNode.Parse(existingContent);
                var newNode = ConvertToJsonNode(newValue);

                if (existingNode is JsonArray existingArray)
                {
                    // Merge arrays by concatenation
                    if (newNode is JsonArray newArray)
                    {
                        foreach (var item in newArray)
                        {
                            existingArray.Add(item?.DeepClone());
                        }
                    }
                    else if (newNode != null)
                    {
                        // Add single item to existing array
                        existingArray.Add(newNode.DeepClone());
                    }

                    return SerializeJsonNode(existingArray, pretty);
                }
                else if (existingNode is JsonObject existingObj)
                {
                    // Merge objects
                    if (newNode is JsonObject newObj)
                    {
                        MergeJsonObjects(existingObj, newObj);
                    }
                    else
                    {
                        context.EmitOutput("Cannot merge non-object into existing JSON object", ScriptOutputType.Debug);
                        return null;
                    }

                    return SerializeJsonNode(existingObj, pretty);
                }
            }
            catch (JsonException ex)
            {
                context.EmitOutput($"Could not parse existing JSON for merge: {ex.Message}", ScriptOutputType.Debug);
            }

            return null;
        }

        /// <summary>
        /// Converts a value to a JsonNode for merging operations.
        /// </summary>
        private JsonNode? ConvertToJsonNode(object? value)
        {
            if (value == null) return null;

            if (value is JsonNode node) return node;
            if (value is JsonElement element)
            {
                return JsonNode.Parse(element.GetRawText());
            }
            if (value is JsonObject jsonObj) return jsonObj;
            if (value is string str)
            {
                // Try to parse as JSON
                if (str.TrimStart().StartsWith("{") || str.TrimStart().StartsWith("["))
                {
                    try
                    {
                        return JsonNode.Parse(str);
                    }
                    catch { }
                }
                return JsonValue.Create(str);
            }
            if (value is List<string> list)
            {
                var arr = new JsonArray();
                foreach (var item in list)
                {
                    arr.Add(ParseJsonValue(item));
                }
                return arr;
            }
            if (value is int i) return JsonValue.Create(i);
            if (value is long l) return JsonValue.Create(l);
            if (value is double d) return JsonValue.Create(d);
            if (value is bool b) return JsonValue.Create(b);

            // Fallback: serialize and parse
            try
            {
                var json = JsonSerializer.Serialize(value);
                return JsonNode.Parse(json);
            }
            catch
            {
                return JsonValue.Create(value.ToString());
            }
        }

        /// <summary>
        /// Parses a string value into the appropriate JSON type.
        /// </summary>
        private JsonNode? ParseJsonValue(string item)
        {
            if (string.IsNullOrEmpty(item))
                return JsonValue.Create(item);

            var trimmed = item.Trim();

            // Check for boolean
            if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
                return JsonValue.Create(true);
            if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
                return JsonValue.Create(false);

            // Check for null
            if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            // Check for integer
            if (long.TryParse(trimmed, out var longVal))
                return JsonValue.Create(longVal);

            // Check for floating point
            if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
                return JsonValue.Create(doubleVal);

            // Check if it looks like JSON
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                try
                {
                    return JsonNode.Parse(trimmed);
                }
                catch { }
            }

            return JsonValue.Create(item);
        }

        /// <summary>
        /// Deep merges source object into target object (modifies target in place).
        /// </summary>
        private void MergeJsonObjects(JsonObject target, JsonObject source)
        {
            foreach (var prop in source)
            {
                if (prop.Value is JsonObject sourceChild && target[prop.Key] is JsonObject targetChild)
                {
                    // Recursively merge nested objects
                    MergeJsonObjects(targetChild, sourceChild);
                }
                else
                {
                    // Override or add the value
                    target[prop.Key] = prop.Value?.DeepClone();
                }
            }
        }

        /// <summary>
        /// Serializes a JsonNode to string with optional pretty printing.
        /// </summary>
        private string SerializeJsonNode(JsonNode node, bool pretty)
        {
            var options = new JsonSerializerOptions { WriteIndented = pretty };
            return node.ToJsonString(options);
        }

        /// <summary>
        /// Formats content as a single JSON line (JSONL format).
        /// </summary>
        private string FormatAsJsonLine(WritefileOptions options, ScriptContext context)
        {
            var rawContent = options.Content ?? string.Empty;
            object? value = null;

            // Check if content is a variable reference
            if (rawContent.StartsWith("${") && rawContent.EndsWith("}"))
            {
                var varName = rawContent.Substring(2, rawContent.Length - 3);
                value = context.GetVariable(varName);
            }
            else
            {
                var substituted = context.SubstituteVariables(rawContent);
                try
                {
                    using var doc = JsonDocument.Parse(substituted);
                    value = doc.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    context.EmitOutput($"JSONL content is not valid JSON ({ex.Message}), wrapping as string", ScriptOutputType.Debug);
                    value = substituted;
                }
            }

            // Serialize as compact single line (never pretty for JSONL)
            return SerializeToJson(value, pretty: false);
        }

        /// <summary>
        /// Serializes an object to JSON.
        /// </summary>
        private string SerializeToJson(object? value, bool pretty)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = pretty
            };

            // Handle List<string> specially
            if (value is List<string> stringList)
            {
                return JsonSerializer.Serialize(stringList, jsonOptions);
            }

            // Handle JsonElement (from parsing)
            if (value is JsonElement element)
            {
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = pretty });
                element.WriteTo(writer);
                writer.Flush();
                return Encoding.UTF8.GetString(stream.ToArray());
            }

            // Handle dictionaries
            if (value is IDictionary<string, object?> dict)
            {
                return JsonSerializer.Serialize(dict, jsonOptions);
            }

            // Default serialization
            return JsonSerializer.Serialize(value, jsonOptions);
        }

        /// <summary>
        /// Formats content as CSV.
        /// </summary>
        private string FormatAsCsv(WritefileOptions options, ScriptContext context)
        {
            var sb = new StringBuilder();
            var rawContent = options.Content ?? string.Empty;

            // Write headers if provided
            if (options.Headers != null && options.Headers.Count > 0)
            {
                sb.AppendLine(string.Join(",", options.Headers.ConvertAll(EscapeCsvField)));
            }

            // Check if content is a variable reference like ${varname}
            List<string>? rows = null;
            if (rawContent.StartsWith("${") && rawContent.EndsWith("}"))
            {
                var varName = rawContent.Substring(2, rawContent.Length - 3);
                var varValue = context.GetVariable(varName);

                if (varValue is List<string> list)
                {
                    rows = list;
                }
                else if (varValue != null)
                {
                    rows = new List<string> { varValue.ToString() ?? string.Empty };
                }
            }

            if (rows == null)
            {
                // Substitute variables and split by newlines
                var substituted = context.SubstituteVariables(rawContent);
                rows = new List<string>(substituted.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
            }

            // Write rows
            foreach (var row in rows)
            {
                // If the row contains commas or tabs, treat as already delimited; otherwise, write as single value
                if (row.Contains(',') || row.Contains('\t'))
                {
                    // Split by comma or tab and escape each field
                    var fields = row.Split(new[] { ',', '\t' });
                    sb.AppendLine(string.Join(",", Array.ConvertAll(fields, f => EscapeCsvField(f.Trim()))));
                }
                else
                {
                    sb.AppendLine(EscapeCsvField(row));
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Escapes a field for CSV format (handles quotes and commas).
        /// </summary>
        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }
    }
}
