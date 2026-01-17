using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Reads a text file line by line into a list variable.
    /// </summary>
    public class ReadFileCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Readfile == null)
                return Task.FromResult(CommandResult.Fail("Readfile command has no options"));

            if (string.IsNullOrEmpty(step.Readfile.Path))
                return Task.FromResult(CommandResult.Fail("Readfile command requires a 'path' property"));

            if (string.IsNullOrEmpty(step.Readfile.Into))
                return Task.FromResult(CommandResult.Fail("Readfile command requires an 'into' property"));

            try
            {
                // Substitute variables in the path
                var filePath = context.SubstituteVariables(step.Readfile.Path);

                // Validate path for security
                if (!ScriptFileAccessValidator.ValidateReadPath(filePath, out var pathError))
                {
                    context.EmitOutput(pathError!, ScriptOutputType.Error);

                    if (step.OnError?.ToLowerInvariant() == "continue")
                        return Task.FromResult(CommandResult.Ok(pathError));

                    return Task.FromResult(CommandResult.Fail(pathError!));
                }

                // Check if file exists
                if (!File.Exists(filePath))
                {
                    // Set variable to empty list and emit warning
                    context.SetVariable(step.Readfile.Into, new List<string>());
                    context.EmitOutput($"File not found: {filePath} - variable '{step.Readfile.Into}' set to empty list", ScriptOutputType.Warning);

                    if (step.OnError?.ToLowerInvariant() == "continue")
                        return Task.FromResult(CommandResult.Ok());

                    return Task.FromResult(CommandResult.Fail($"File not found: {filePath}"));
                }

                // Get encoding
                var encoding = GetEncoding(step.Readfile.Encoding);

                // Read lines with max limit
                var maxLines = step.Readfile.MaxLines > 0 ? step.Readfile.MaxLines : int.MaxValue;
                var lines = new List<string>();
                var lineCount = 0;
                var truncated = false;

                using (var reader = new StreamReader(filePath, encoding))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var processedLine = step.Readfile.TrimLines ? line.Trim() : line;

                        // Skip empty lines if configured
                        if (step.Readfile.SkipEmptyLines && string.IsNullOrEmpty(processedLine))
                            continue;

                        lines.Add(processedLine);
                        lineCount++;

                        // Check max lines limit
                        if (lineCount >= maxLines)
                        {
                            truncated = true;
                            break;
                        }
                    }
                }

                // Store the lines in the variable
                context.SetVariable(step.Readfile.Into, lines);

                var message = $"Read {lines.Count} lines from '{filePath}' into '{step.Readfile.Into}'";
                if (truncated)
                    message += $" (truncated at {maxLines} lines)";

                context.EmitOutput(message, ScriptOutputType.Debug);

                return Task.FromResult(CommandResult.Ok());
            }
            catch (UnauthorizedAccessException ex)
            {
                var errorMsg = $"Access denied reading file: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return Task.FromResult(CommandResult.Ok(errorMsg));

                return Task.FromResult(CommandResult.Fail(errorMsg));
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error reading file: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return Task.FromResult(CommandResult.Ok(errorMsg));

                return Task.FromResult(CommandResult.Fail(errorMsg));
            }
        }

        private static Encoding GetEncoding(string? encodingName)
        {
            return encodingName?.ToLowerInvariant() switch
            {
                "ascii" => Encoding.ASCII,
                "utf-16" or "unicode" => Encoding.Unicode,
                "utf-16be" => Encoding.BigEndianUnicode,
                "utf-32" => Encoding.UTF32,
                "latin1" or "iso-8859-1" => Encoding.Latin1,
                _ => Encoding.UTF8 // Default to UTF-8
            };
        }
    }
}
