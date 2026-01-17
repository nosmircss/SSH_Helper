using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Writes content to a text file (append or overwrite).
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
                // Substitute variables in path and content
                var filePath = context.SubstituteVariables(step.Writefile.Path);
                var content = context.SubstituteVariables(step.Writefile.Content ?? string.Empty);

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

                // Write based on mode
                var mode = step.Writefile.Mode?.ToLowerInvariant() ?? "append";

                if (mode == "overwrite")
                {
                    File.WriteAllText(filePath, content);
                    context.EmitOutput($"Wrote to '{filePath}' (overwrite)", ScriptOutputType.Debug);
                }
                else
                {
                    // Append mode (default) - add newline if content doesn't end with one
                    var contentToAppend = content;
                    if (!contentToAppend.EndsWith(Environment.NewLine))
                        contentToAppend += Environment.NewLine;

                    File.AppendAllText(filePath, contentToAppend);
                    context.EmitOutput($"Appended to '{filePath}'", ScriptOutputType.Debug);
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
    }
}
