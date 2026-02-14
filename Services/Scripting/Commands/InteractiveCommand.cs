using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Services.Terminal;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Opens an in-app terminal session and blocks script execution until it closes.
    /// </summary>
    public sealed class InteractiveCommand : IScriptCommand
    {
        private readonly IInteractiveTerminalService _terminalService;

        public InteractiveCommand(IInteractiveTerminalService? terminalService = null)
        {
            _terminalService = terminalService ?? new InteractiveTerminalService();
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Interactive == null)
            {
                return CommandResult.Fail("Interactive command requires a mapping with options.");
            }

            try
            {
                var runResult = await _terminalService.RunAsync(context, step.Interactive, cancellationToken);
                if (runResult.Success)
                {
                    return CommandResult.Ok();
                }

                var errorMessage = string.IsNullOrWhiteSpace(runResult.ErrorMessage)
                    ? "Interactive terminal step failed."
                    : runResult.ErrorMessage;

                context.EmitOutput(errorMessage, ScriptOutputType.Error);

                if (string.Equals(step.OnError, "continue", StringComparison.OrdinalIgnoreCase))
                {
                    return CommandResult.Suppressed(errorMessage);
                }

                return CommandResult.Fail(errorMessage);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }
    }
}
