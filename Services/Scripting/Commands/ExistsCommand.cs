using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Checks whether a local path exists as a file, directory, or either.
    /// </summary>
    public class ExistsCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Exists == null)
                return Task.FromResult(CommandResult.Fail("Exists command has no options"));

            cancellationToken.ThrowIfCancellationRequested();

            var options = step.Exists;
            var into = options.Into?.Trim() ?? string.Empty;
            var rawType = Environment.ExpandEnvironmentVariables(context.SubstituteVariables(options.Type ?? string.Empty));
            var type = NormalizeType(rawType);
            var rawPath = options.Path ?? string.Empty;
            var expandedPath = Environment.ExpandEnvironmentVariables(context.SubstituteVariables(rawPath)).Trim();

            if (string.IsNullOrWhiteSpace(expandedPath))
            {
                return Task.FromResult(FailWithCapture(step, context, into, expandedPath, type, "Exists requires 'path'"));
            }

            try
            {
                var fullPath = Path.GetFullPath(expandedPath);
                var isFile = File.Exists(fullPath);
                var isDirectory = Directory.Exists(fullPath);
                var exists = type switch
                {
                    "file" => isFile,
                    "directory" => isDirectory,
                    _ => isFile || isDirectory,
                };

                Capture(into, context, fullPath, type, exists, isFile, isDirectory);
                context.EmitOutput($"Exists: {fullPath} => {exists} (type={type})", ScriptOutputType.Debug);
                return Task.FromResult(CommandResult.Ok());
            }
            catch (Exception ex)
            {
                return Task.FromResult(FailWithCapture(step, context, into, expandedPath, type, $"Exists error: {ex.Message}"));
            }
        }

        private static string NormalizeType(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "file" => "file",
                "directory" => "directory",
                _ => "any",
            };
        }

        private static void Capture(
            string into,
            ScriptContext context,
            string path,
            string type,
            bool exists,
            bool isFile,
            bool isDirectory,
            string? error = null)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, exists);

            var meta = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["exists"] = exists,
                ["is_file"] = isFile,
                ["is_directory"] = isDirectory,
                ["path"] = path,
                ["type"] = type,
            };

            if (!string.IsNullOrWhiteSpace(error))
                meta["error"] = error;

            context.SetVariable(into + "_meta", meta);
        }

        private static CommandResult FailWithCapture(
            ScriptStep step,
            ScriptContext context,
            string into,
            string path,
            string type,
            string message)
        {
            Capture(
                into,
                context,
                path,
                type,
                exists: false,
                isFile: false,
                isDirectory: false,
                error: message);

            return CommandResult.ApplyOnError(step, message);
        }
    }
}
