using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Pauses script execution for a specified number of seconds.
    /// </summary>
    public class WaitCommand : IScriptCommand
    {
        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (!step.Wait.HasValue || step.Wait.Value <= 0)
                return CommandResult.Ok();

            var seconds = step.Wait.Value;

            context.EmitOutput($"Waiting {seconds} second(s)...", ScriptOutputType.Debug);

            await Task.Delay(seconds * 1000, cancellationToken);

            return CommandResult.Ok();
        }
    }
}
