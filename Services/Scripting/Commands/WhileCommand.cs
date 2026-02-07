using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Executes a block repeatedly while a condition is true.
    /// Format: "while: condition"
    /// </summary>
    public class WhileCommand : IScriptCommand
    {
        private readonly ScriptExecutor _executor;
        private const int DefaultMaxIterations = 10000; // Safety limit to prevent infinite loops

        public WhileCommand(ScriptExecutor executor)
        {
            _executor = executor;
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.While))
                return CommandResult.Fail("While command has no condition");

            if (step.Do == null || step.Do.Count == 0)
                return CommandResult.Fail("While requires 'do' block");

            var evaluator = new ExpressionEvaluator(context);
            var maxIterations = step.MaxIterations.GetValueOrDefault(DefaultMaxIterations);
            if (maxIterations <= 0)
                maxIterations = DefaultMaxIterations;
            int iteration = 0;

            while (iteration < maxIterations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Evaluate expression directly; ExpressionEvaluator resolves variables.
                var condition = step.While;
                var result = evaluator.Evaluate(condition);

                if (!result)
                {
                    context.EmitOutput($"While: condition false after {iteration} iteration(s)", ScriptOutputType.Debug);
                    break;
                }

                if (iteration == 0)
                {
                    context.EmitOutput($"While: entering loop", ScriptOutputType.Debug);
                }

                // Set iteration variable
                context.SetVariable("_iteration", iteration);

                // Execute the 'do' block
                var execResult = await _executor.ExecuteStepsAsync(step.Do, context, cancellationToken, context.LoopDepth + 1);

                // Handle control flow
                if (execResult.ShouldExit)
                    return execResult;

                if (execResult.ShouldBreak)
                {
                    context.EmitOutput($"While: break after {iteration + 1} iteration(s)", ScriptOutputType.Debug);
                    break;
                }

                if (execResult.ShouldContinue)
                {
                    iteration++;
                    continue;
                }

                if (!execResult.Success)
                    return execResult;

                iteration++;
            }

            if (iteration >= maxIterations)
            {
                context.EmitOutput($"While: reached maximum iterations ({maxIterations}), stopping", ScriptOutputType.Warning);
            }

            return CommandResult.Ok();
        }
    }
}
