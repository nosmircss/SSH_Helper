using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Executes multiple steps concurrently with optional concurrency limits.
    /// </summary>
    public class ParallelCommand : IScriptCommand
    {
        private readonly ScriptExecutor _executor;

        public ParallelCommand(ScriptExecutor executor)
        {
            _executor = executor;
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Parallel == null || step.Parallel.Steps.Count == 0)
                return CommandResult.Fail("Parallel command has no steps");

            var maxConcurrent = step.Parallel.MaxConcurrent > 0
                ? step.Parallel.MaxConcurrent
                : step.Parallel.Steps.Count;

            context.EmitOutput(
                $"Executing {step.Parallel.Steps.Count} step(s) in parallel (max concurrent: {maxConcurrent})",
                ScriptOutputType.Debug);

            var semaphore = new SemaphoreSlim(maxConcurrent);
            var loopDepth = context.LoopDepth;
            var results = new List<(int index, CommandResult result)>();
            var lockObj = new object();

            var tasks = step.Parallel.Steps.Select((childStep, index) => Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    // Steps share one context; script context/session enforce internal synchronization.
                    var result = await _executor.ExecuteStepsAsync(
                        new List<ScriptStep> { childStep },
                        context,
                        cancellationToken,
                        loopDepth);

                    lock (lockObj)
                    {
                        results.Add((index, result));
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken)).ToArray();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return CommandResult.Fail($"Parallel execution error: {ex.Message}");
            }
            finally
            {
                semaphore.Dispose();
            }

            // Check results in order
            var orderedResults = results.OrderBy(r => r.index).ToList();
            foreach (var (index, result) in orderedResults)
            {
                if (result.ShouldExit || result.ShouldBreak || result.ShouldContinue)
                    return result;

                if (!result.Success && !result.SuppressedError)
                {
                    return CommandResult.Fail(
                        result.Message ?? $"Parallel step {index + 1} failed");
                }
            }

            context.EmitOutput($"All {step.Parallel.Steps.Count} parallel step(s) completed", ScriptOutputType.Debug);
            return CommandResult.Ok();
        }
    }
}
