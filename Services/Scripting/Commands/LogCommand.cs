using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Outputs a message with a specific log level.
    /// Supports both simple string form and options object with level.
    /// </summary>
    public class LogCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            string message;
            string level = "info";

            // Handle both simple string and options object
            if (step.Log is string simpleMessage)
            {
                message = simpleMessage;
            }
            else if (step.Log is LogOptions options)
            {
                message = options.Message;
                level = options.Level?.ToLowerInvariant() ?? "info";
            }
            else
            {
                // Try to extract from dictionary (YAML parsing may produce this)
                if (step.Log is System.Collections.Generic.IDictionary<object, object> dict)
                {
                    message = dict.TryGetValue("message", out var msgObj) ? msgObj?.ToString() ?? string.Empty : string.Empty;
                    level = dict.TryGetValue("level", out var lvlObj) ? lvlObj?.ToString()?.ToLowerInvariant() ?? "info" : "info";
                }
                else
                {
                    return Task.FromResult(CommandResult.Fail("Log command requires a message"));
                }
            }

            if (string.IsNullOrEmpty(message))
                return Task.FromResult(CommandResult.Ok());

            // Substitute variables in the message
            var substituted = context.SubstituteVariables(message);
            var outputType = MapLevelToOutputType(level);

            context.EmitOutput(substituted, outputType);

            return Task.FromResult(CommandResult.Ok());
        }

        private static ScriptOutputType MapLevelToOutputType(string level)
        {
            return level switch
            {
                "debug" => ScriptOutputType.Debug,
                "warning" or "warn" => ScriptOutputType.Warning,
                "error" or "err" => ScriptOutputType.Error,
                "success" => ScriptOutputType.Success,
                _ => ScriptOutputType.Info
            };
        }
    }
}
