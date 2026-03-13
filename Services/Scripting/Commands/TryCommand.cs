using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Executes structured try/catch/finally blocks.
    /// </summary>
    public class TryCommand : IScriptCommand
    {
        private readonly ScriptExecutor _executor;

        public TryCommand(ScriptExecutor executor)
        {
            _executor = executor;
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Try == null || step.Try.Count == 0)
                return CommandResult.Fail("try requires a non-empty try block");

            var result = await _executor.ExecuteStepsAsync(step.Try, context, cancellationToken, context.LoopDepth);

            var shouldHandleFailure = !result.Success && !result.ShouldExit && !result.ShouldBreak && !result.ShouldContinue && !result.ShouldReturn;
            if (shouldHandleFailure && step.Catch != null && step.Catch.Count > 0)
            {
                result = await _executor.ExecuteStepsAsync(step.Catch, context, cancellationToken, context.LoopDepth);
            }

            if (step.Finally != null && step.Finally.Count > 0)
            {
                var finallyResult = await _executor.ExecuteStepsAsync(step.Finally, context, cancellationToken, context.LoopDepth);
                if (finallyResult.ShouldExit || finallyResult.ShouldBreak || finallyResult.ShouldContinue || finallyResult.ShouldReturn || !finallyResult.Success)
                    return finallyResult;
            }

            return result;
        }
    }
}
