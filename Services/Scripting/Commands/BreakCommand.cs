using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Exits the current loop.
    /// </summary>
    public class BreakCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (context.LoopDepth <= 0)
                return Task.FromResult(CommandResult.Fail("break can only be used inside a loop"));

            context.EmitOutput("Break: exiting loop", ScriptOutputType.Debug);
            return Task.FromResult(CommandResult.Break());
        }
    }
}
