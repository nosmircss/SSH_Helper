using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Skips to the next iteration of the current loop.
    /// </summary>
    public class ContinueCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (context.LoopDepth <= 0)
                return Task.FromResult(CommandResult.Fail("continue can only be used inside a loop"));

            context.EmitOutput("Continue: next loop iteration", ScriptOutputType.Debug);
            return Task.FromResult(CommandResult.Continue());
        }
    }
}
