using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Executes conditional logic based on an expression.
    /// </summary>
    public class IfCommand : IScriptCommand
    {
        private readonly ScriptExecutor _executor;

        public IfCommand(ScriptExecutor executor)
        {
            _executor = executor;
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.If))
                return CommandResult.Fail("If command has no condition");

            var condition = step.If;

            // Evaluate the condition
            var evaluator = new ExpressionEvaluator(context);
            var result = evaluator.Evaluate(condition);

            context.EmitOutput($"If '{condition}' => {result}", ScriptOutputType.Debug);

            if (result)
            {
                // Execute 'then' block
                if (step.Then != null && step.Then.Count > 0)
                {
                    var thenResult = await _executor.ExecuteStepsAsync(step.Then, context, cancellationToken, context.LoopDepth);
                    if (thenResult.ShouldExit || thenResult.ShouldBreak || thenResult.ShouldContinue || thenResult.ShouldReturn || !thenResult.Success)
                        return thenResult;
                }
            }
            else
            {
                if (step.Elif != null)
                {
                    foreach (var elif in step.Elif)
                    {
                        if (string.IsNullOrWhiteSpace(elif.If))
                            continue;

                        var elifResult = evaluator.Evaluate(elif.If);
                        context.EmitOutput($"Elif '{elif.If}' => {elifResult}", ScriptOutputType.Debug);
                        if (!elifResult)
                            continue;

                        if (elif.Then.Count > 0)
                        {
                            var branchResult = await _executor.ExecuteStepsAsync(elif.Then, context, cancellationToken, context.LoopDepth);
                            if (branchResult.ShouldExit || branchResult.ShouldBreak || branchResult.ShouldContinue || branchResult.ShouldReturn || !branchResult.Success)
                                return branchResult;
                        }

                        return CommandResult.Ok();
                    }
                }

                // Execute 'else' block
                if (step.Else != null && step.Else.Count > 0)
                {
                    var elseResult = await _executor.ExecuteStepsAsync(step.Else, context, cancellationToken, context.LoopDepth);
                    if (elseResult.ShouldExit || elseResult.ShouldBreak || elseResult.ShouldContinue || elseResult.ShouldReturn || !elseResult.Success)
                        return elseResult;
                }
            }

            return CommandResult.Ok();
        }
    }
}
