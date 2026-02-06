using System;
using System.Collections.Generic;
using System.Globalization;
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
                // Substitute variables in path (supports both script variables and Windows env vars like %HOMEPATH%)
                var filePath = Environment.ExpandEnvironmentVariables(
                    context.SubstituteVariables(step.Writefile.Path));

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
                    // For JSONL, append one normalized line at a time
                    // For text/csv, append with newline
                    if (format == "json")
                    {
                        // JSON append merging writes the full merged content
                        File.WriteAllText(filePath, content);
                        context.EmitOutput($"Merged JSON to '{filePath}'", ScriptOutputType.Debug);
                    }
                    else if (format == "jsonl")
                    {
                        AppendJsonLine(filePath, content);
                        context.EmitOutput($"Appended to '{filePath}' ({format})", ScriptOutputType.Debug);
                    }
                    else
                    {
                        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0 && !FileEndsWithLineBreak(filePath))
                        {
                            File.AppendAllText(filePath, Environment.NewLine);
                        }

                        var contentToAppend = EnsureTrailingNewLine(content);

                        File.AppendAllText(filePath, contentToAppend);
                        context.EmitOutput($"Appended to '{filePath}' ({format})", ScriptOutputType.Debug);
                    }
                }
                else
                {
                    // Overwrite mode (default)
                    if (format == "jsonl")
                    {
                        File.WriteAllText(filePath, EnsureTrailingNewLine(content));
                    }
                    else
                    {
                        File.WriteAllText(filePath, content);
                    }
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
                newValue = NormalizeJsonLikeString(context.GetVariable(varName), context, "Content is not valid JSON", emitDebugOnInvalid: false);
            }
            else
            {
                // Otherwise, substitute variables and try to parse as JSON
                var substituted = context.SubstituteVariables(rawContent);
                newValue = NormalizeJsonLikeString(substituted, context, "Content is not valid JSON", emitDebugOnInvalid: true);
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
                var newNode = JsonUtilities.ConvertToJsonNode(newValue);

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
                        JsonUtilities.MergeInto(existingObj, newObj);
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
                value = NormalizeJsonLikeString(context.GetVariable(varName), context, "JSONL content is not valid JSON", emitDebugOnInvalid: false);
            }
            else
            {
                var substituted = context.SubstituteVariables(rawContent);
                value = NormalizeJsonLikeString(substituted, context, "JSONL content is not valid JSON", emitDebugOnInvalid: true);
            }

            // Serialize as compact single line (never pretty for JSONL)
            return SerializeToJson(value, pretty: false);
        }

        /// <summary>
        /// Parses JSON-like strings into JsonElement so structured JSON is not serialized as a quoted string.
        /// </summary>
        private object? NormalizeJsonLikeString(object? value, ScriptContext context, string debugPrefix, bool emitDebugOnInvalid)
        {
            if (value is not string strValue)
                return value;

            var trimmed = strValue.Trim();
            if (trimmed.Length == 0)
                return strValue;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                return doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                if (emitDebugOnInvalid)
                {
                    context.EmitOutput($"{debugPrefix} ({ex.Message}), wrapping as string", ScriptOutputType.Debug);
                }
            }

            return strValue;
        }

        /// <summary>
        /// Ensures text ends with a single platform newline.
        /// </summary>
        private static string EnsureTrailingNewLine(string content)
        {
            var normalized = content.TrimEnd('\r', '\n');
            return normalized + Environment.NewLine;
        }

        /// <summary>
        /// Appends a single JSONL record while preserving line boundaries in existing files.
        /// </summary>
        private static void AppendJsonLine(string filePath, string content)
        {
            if (File.Exists(filePath) && new FileInfo(filePath).Length > 0 && !FileEndsWithLineBreak(filePath))
            {
                File.AppendAllText(filePath, Environment.NewLine);
            }

            var line = EnsureTrailingNewLine(content);
            File.AppendAllText(filePath, line);
        }

        /// <summary>
        /// Returns true when the file's final byte is a newline terminator.
        /// </summary>
        private static bool FileEndsWithLineBreak(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length == 0)
                return false;

            stream.Seek(-1, SeekOrigin.End);
            int lastByte = stream.ReadByte();
            return lastByte == '\n' || lastByte == '\r';
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
            List<string>? headers = options.Headers;
            if (rawContent.StartsWith("${") && rawContent.EndsWith("}"))
            {
                var varName = rawContent.Substring(2, rawContent.Length - 3);
                var varValue = context.GetVariable(varName);

                if (varValue is List<string> list)
                {
                    rows = list;
                }
                else if (varValue is string strValue)
                {
                    // Check if it's a JSON array of objects
                    var trimmed = strValue.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        try
                        {
                            var jsonArray = JsonNode.Parse(trimmed)?.AsArray();
                            if (jsonArray != null && jsonArray.Count > 0)
                            {
                                rows = new List<string>();
                                foreach (var element in jsonArray)
                                {
                                    if (element is JsonObject obj)
                                    {
                                        // If headers provided, use them to extract values in order
                                        if (headers != null && headers.Count > 0)
                                        {
                                            var values = new List<string>();
                                            foreach (var header in headers)
                                            {
                                                var val = GetCsvNodeValue(obj[header]);
                                                values.Add(val);
                                            }
                                            rows.Add(string.Join(",", values.ConvertAll(EscapeCsvField)));
                                        }
                                        else
                                        {
                                            // No headers, just serialize each object
                                            rows.Add(element.ToJsonString());
                                        }
                                    }
                                    else if (element != null)
                                    {
                                        rows.Add(GetCsvNodeValue(element));
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Not valid JSON, treat as regular string
                            rows = new List<string> { strValue };
                        }
                    }
                    else
                    {
                        rows = new List<string> { strValue };
                    }
                }
                else if (varValue != null)
                {
                    rows = new List<string> { varValue.ToString() ?? string.Empty };
                }
            }

            // Track if rows were pre-formatted from JSON (already escaped)
            bool rowsPreFormatted = rows != null && rawContent.StartsWith("${");

            if (rows == null)
            {
                // Substitute variables and split by newlines
                var substituted = context.SubstituteVariables(rawContent);
                rows = new List<string>(substituted.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
                rowsPreFormatted = false;
            }

            // Write rows
            foreach (var row in rows)
            {
                if (rowsPreFormatted)
                {
                    // Rows from JSON parsing are already properly formatted
                    sb.AppendLine(row);
                }
                else if (row.Contains(',') || row.Contains('\t'))
                {
                    // If the row contains commas or tabs, treat as already delimited
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
        /// Converts a JSON node to a compact, CSV-safe string value.
        /// Scalars are converted to plain strings; objects/arrays stay compact JSON.
        /// </summary>
        private static string GetCsvNodeValue(JsonNode? node)
        {
            if (node == null)
                return string.Empty;

            if (node is JsonArray array)
            {
                return FlattenCsvArray(array);
            }

            if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var str))
                {
                    if (TryParseJsonArray(str, out var parsedArray))
                    {
                        return FlattenCsvArray(parsedArray);
                    }

                    return str;
                }
                if (value.TryGetValue<long>(out var lng))
                    return lng.ToString(CultureInfo.InvariantCulture);
                if (value.TryGetValue<double>(out var dbl))
                    return dbl.ToString(CultureInfo.InvariantCulture);
                if (value.TryGetValue<decimal>(out var dec))
                    return dec.ToString(CultureInfo.InvariantCulture);
                if (value.TryGetValue<bool>(out var bln))
                    return bln ? "true" : "false";

                return value.ToString();
            }

            // For arrays/objects, emit compact JSON (not pretty-printed).
            return node.ToJsonString();
        }

        /// <summary>
        /// Flattens JSON arrays for CSV cells using comma-space separators.
        /// </summary>
        private static string FlattenCsvArray(JsonArray array)
        {
            var items = new List<string>(array.Count);
            foreach (var item in array)
            {
                if (item is JsonArray nestedArray)
                {
                    items.Add(nestedArray.ToJsonString());
                }
                else if (item is JsonObject nestedObject)
                {
                    items.Add(nestedObject.ToJsonString());
                }
                else
                {
                    items.Add(GetCsvNodeValue(item));
                }
            }

            return string.Join(", ", items);
        }

        /// <summary>
        /// Parses JSON array text.
        /// </summary>
        private static bool TryParseJsonArray(string value, out JsonArray array)
        {
            array = new JsonArray();
            var trimmed = value.Trim();
            if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
                return false;

            try
            {
                if (JsonNode.Parse(trimmed) is JsonArray parsed)
                {
                    array = parsed;
                    return true;
                }
            }
            catch
            {
                // Not valid JSON array text.
            }

            return false;
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
