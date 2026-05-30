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

            string? branchTaken = null;

            if (result)
            {
                branchTaken = "then";
                // Execute 'then' block
                if (step.Then != null && step.Then.Count > 0)
                {
                    var thenResult = await _executor.ExecuteStepsAsync(step.Then, context, cancellationToken, context.LoopDepth);
                    if (thenResult.IsControlFlow || !thenResult.Success)
                    {
                        thenResult.BranchTaken = branchTaken;
                        return thenResult;
                    }
                }
            }
            else
            {
                if (step.Elif != null)
                {
                    for (int i = 0; i < step.Elif.Count; i++)
                    {
                        var elif = step.Elif[i];
                        if (string.IsNullOrWhiteSpace(elif.If))
                            continue;

                        var elifResult = evaluator.Evaluate(elif.If);
                        context.EmitOutput($"Elif '{elif.If}' => {elifResult}", ScriptOutputType.Debug);
                        if (!elifResult)
                            continue;

                        branchTaken = $"elif/{i}/then";

                        if (elif.Then.Count > 0)
                        {
                            var branchResult = await _executor.ExecuteStepsAsync(elif.Then, context, cancellationToken, context.LoopDepth);
                            if (branchResult.IsControlFlow || !branchResult.Success)
                            {
                                branchResult.BranchTaken = branchTaken;
                                return branchResult;
                            }
                        }

                        return new CommandResult { Success = true, BranchTaken = branchTaken };
                    }
                }

                // Execute 'else' block
                if (step.Else != null && step.Else.Count > 0)
                {
                    branchTaken = "else";
                    var elseResult = await _executor.ExecuteStepsAsync(step.Else, context, cancellationToken, context.LoopDepth);
                    if (elseResult.IsControlFlow || !elseResult.Success)
                    {
                        elseResult.BranchTaken = branchTaken;
                        return elseResult;
                    }
                }
            }

            return new CommandResult { Success = true, BranchTaken = branchTaken };
        }
    }
}
