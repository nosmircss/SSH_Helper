using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Executes a block repeatedly until a condition becomes true (do-while).
    /// The body always runs at least once; the `until` condition is tested at the bottom of each iteration.
    /// Format: "repeat:" with nested "until"/"do" (or scalar "repeat: &lt;until&gt;" with a sibling "do").
    /// </summary>
    public class RepeatCommand : IScriptCommand
    {
        private readonly ScriptExecutor _executor;
        private const int DefaultMaxIterations = 10000; // Safety limit to prevent infinite loops

        public RepeatCommand(ScriptExecutor executor)
        {
            _executor = executor;
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.Until))
                return CommandResult.Fail("Repeat command has no 'until' condition");

            if (step.Do == null || step.Do.Count == 0)
                return CommandResult.Fail("Repeat requires 'do' block");

            var evaluator = new ExpressionEvaluator(context);
            var maxIterations = step.MaxIterations.GetValueOrDefault(DefaultMaxIterations);
            if (maxIterations <= 0)
                maxIterations = DefaultMaxIterations;

            int iteration = 0;
            int executed = 0;

            context.PushIterationFrame(step.StepPath ?? string.Empty, -1);
            try
            {
                while (iteration < maxIterations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    context.SetVariable("_iteration", iteration);
                    context.SetCurrentIterationFrame(iteration);

                    var execResult = await _executor.ExecuteStepsAsync(step.Do, context, cancellationToken, context.LoopDepth + 1);
                    executed++;

                    if (execResult.ShouldExit || execResult.ShouldReturn)
                    {
                        execResult.IterationCount = executed;
                        return execResult;
                    }

                    if (execResult.ShouldBreak)
                    {
                        context.EmitOutput($"Repeat: break after {iteration + 1} iteration(s)", ScriptOutputType.Debug);
                        break;
                    }

                    // `continue` falls through to the bottom condition check; a genuine failure stops the loop.
                    if (!execResult.ShouldContinue && !execResult.Success)
                    {
                        execResult.IterationCount = executed;
                        return execResult;
                    }

                    iteration++;

                    // Bottom-tested: exit once the until condition becomes true.
                    if (evaluator.Evaluate(step.Until))
                    {
                        context.EmitOutput($"Repeat: until condition true after {iteration} iteration(s)", ScriptOutputType.Debug);
                        break;
                    }
                }
            }
            finally
            {
                context.PopIterationFrame();
            }

            if (iteration >= maxIterations)
            {
                context.EmitOutput($"Repeat: reached maximum iterations ({maxIterations}), stopping", ScriptOutputType.Warning);
            }

            return new CommandResult { Success = true, IterationCount = executed };
        }
    }
}
