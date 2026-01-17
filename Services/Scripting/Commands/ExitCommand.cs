using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Terminates script execution with a status and message.
    /// Format: "exit: success message" or "exit: failure message" or just "exit: message"
    /// </summary>
    public class ExitCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            var exitText = step.Exit ?? string.Empty;

            // Substitute variables in the exit message
            exitText = context.SubstituteVariables(exitText);

            // Parse status and message
            var status = ScriptExitStatus.Success;
            var message = exitText;

            var trimmed = exitText.TrimStart();
            if (trimmed.StartsWith("success ", System.StringComparison.OrdinalIgnoreCase))
            {
                status = ScriptExitStatus.Success;
                message = trimmed.Substring(8).Trim();
                message = TrimQuotes(message);
            }
            else if (trimmed.StartsWith("failure ", System.StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("fail ", System.StringComparison.OrdinalIgnoreCase))
            {
                status = ScriptExitStatus.Failure;
                var spaceIndex = trimmed.IndexOf(' ');
                message = trimmed.Substring(spaceIndex + 1).Trim();
                message = TrimQuotes(message);
            }
            else if (trimmed.StartsWith("error ", System.StringComparison.OrdinalIgnoreCase))
            {
                status = ScriptExitStatus.Error;
                message = trimmed.Substring(6).Trim();
                message = TrimQuotes(message);
            }
            else
            {
                message = TrimQuotes(message);
            }

            // Emit the exit message with appropriate type
            var outputType = status == ScriptExitStatus.Success
                ? ScriptOutputType.Success
                : ScriptOutputType.Error;

            context.EmitOutput($"[EXIT {status}] {message}", outputType);

            return Task.FromResult(CommandResult.Exit(status, message));
        }

        private static string TrimQuotes(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            if ((s.StartsWith("\"") && s.EndsWith("\"")) ||
                (s.StartsWith("'") && s.EndsWith("'")))
            {
                return s.Substring(1, s.Length - 2);
            }

            return s;
        }
    }
}
