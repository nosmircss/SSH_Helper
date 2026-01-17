using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Outputs a message to the script output.
    /// </summary>
    public class PrintCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.Print))
                return Task.FromResult(CommandResult.Ok());

            // Substitute variables in the message
            var message = context.SubstituteVariables(step.Print);

            context.EmitOutput(message, ScriptOutputType.Info);

            return Task.FromResult(CommandResult.Ok());
        }
    }
}
