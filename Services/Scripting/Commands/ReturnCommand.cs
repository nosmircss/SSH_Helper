using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Exits the current subroutine early.
    /// </summary>
    public sealed class ReturnCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(step.ReturnFromSubroutine ? CommandResult.Return() : CommandResult.Ok());
        }
    }
}
