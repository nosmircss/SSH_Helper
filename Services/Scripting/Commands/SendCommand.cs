using System;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Utilities;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Executes an SSH command via the shell session.
    /// </summary>
    public class SendCommand : IScriptCommand
    {
        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.Send))
                return CommandResult.Fail("Send command has no command text");

            var session = context.Session;
            if (session == null)
                return CommandResult.Fail("No SSH session available");

            try
            {
                // Substitute variables in the command
                var command = context.SubstituteVariables(step.Send);

                // Only show command if not suppressed
                if (!step.Suppress)
                {
                    var prompt = session.CurrentPrompt ?? ">>>";
                    context.EmitOutput($"{prompt} {command}", ScriptOutputType.Command);
                }

                // Execute the command
                var timeoutSeconds = step.Timeout.HasValue && step.Timeout.Value > 0 ? step.Timeout.Value : (int?)null;
                var output = await session.ExecuteAsync(command, step.Expect, timeoutSeconds, cancellationToken);

                // Strip the echoed command from output
                output = TerminalOutputProcessor.StripCommandEcho(output, command);
                output = TerminalOutputProcessor.StripTrailingPrompt(output, session.CurrentPrompt);

                // Record the output
                context.RecordCommandOutput(output, step.Capture);

                // Only emit output if not suppressed
                if (!step.Suppress)
                {
                    context.EmitOutput(output, ScriptOutputType.CommandOutput);
                }

                return CommandResult.Ok();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Command failed: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return CommandResult.Ok(errorMsg);

                return CommandResult.Fail(errorMsg);
            }
        }
    }
}
