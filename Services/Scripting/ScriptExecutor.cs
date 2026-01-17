using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Executes parsed scripts by interpreting steps and dispatching to command handlers.
    /// </summary>
    public class ScriptExecutor
    {
        private readonly Dictionary<StepType, IScriptCommand> _commands;

        public ScriptExecutor()
        {
            // Register command handlers
            _commands = new Dictionary<StepType, IScriptCommand>
            {
                { StepType.Send, new SendCommand() },
                { StepType.Print, new PrintCommand() },
                { StepType.Wait, new WaitCommand() },
                { StepType.Set, new SetCommand() },
                { StepType.Exit, new ExitCommand() },
                { StepType.Extract, new ExtractCommand() },
                { StepType.If, new IfCommand(this) },
                { StepType.Foreach, new ForeachCommand(this) },
                { StepType.While, new WhileCommand(this) },
                { StepType.Readfile, new ReadFileCommand() },
                { StepType.Writefile, new WriteFileCommand() },
                { StepType.Input, new InputCommand() },
            };
        }

        /// <summary>
        /// Executes a complete script.
        /// </summary>
        /// <param name="script">The parsed script to execute.</param>
        /// <param name="context">The execution context with session and variables.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of script execution.</returns>
        public async Task<ScriptResult> ExecuteAsync(
            Script script,
            ScriptContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Import script variables (defaults)
                if (script.Vars != null && script.Vars.Count > 0)
                {
                    context.ImportScriptVars(script.Vars);
                }

                // Apply script-level debug setting (overrides service-level if enabled in script)
                if (script.Debug)
                {
                    context.DebugMode = true;
                }

                // Reset debug state
                context.DebugState.Reset();

                // Execute all steps
                var result = await ExecuteStepsAsync(script.Steps, context, cancellationToken);

                // Determine final status
                if (result.ShouldExit)
                {
                    return new ScriptResult
                    {
                        Status = result.ExitStatus,
                        Message = result.Message ?? "Script completed",
                        FullOutput = context.FullOutput
                    };
                }

                if (!result.Success)
                {
                    return new ScriptResult
                    {
                        Status = ScriptExitStatus.Error,
                        Message = result.Message ?? "Script failed",
                        FullOutput = context.FullOutput
                    };
                }

                return new ScriptResult
                {
                    Status = ScriptExitStatus.Success,
                    Message = "Script completed successfully",
                    FullOutput = context.FullOutput
                };
            }
            catch (OperationCanceledException)
            {
                return new ScriptResult
                {
                    Status = ScriptExitStatus.Cancelled,
                    Message = "Script cancelled",
                    FullOutput = context.FullOutput
                };
            }
            catch (Exception ex)
            {
                return new ScriptResult
                {
                    Status = ScriptExitStatus.Error,
                    Message = $"Script error: {ex.Message}",
                    Exception = ex,
                    FullOutput = context.FullOutput
                };
            }
        }

        /// <summary>
        /// Executes a list of steps (used for main script and nested blocks).
        /// </summary>
        public async Task<CommandResult> ExecuteStepsAsync(
            List<ScriptStep> steps,
            ScriptContext context,
            CancellationToken cancellationToken)
        {
            foreach (var step in steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Handle debug pausing
                if (context.DebugState.ShouldPauseAt(step.LineNumber))
                {
                    await HandleDebugPauseAsync(step, context, cancellationToken);
                }

                var result = await ExecuteStepAsync(step, context, cancellationToken);

                // Propagate control flow signals
                if (result.ShouldExit || result.ShouldBreak || result.ShouldContinue)
                    return result;

                // Stop on error (unless on_error: continue)
                if (!result.Success)
                    return result;
            }

            return CommandResult.Ok();
        }

        /// <summary>
        /// Executes a single step by dispatching to the appropriate command handler.
        /// </summary>
        private async Task<CommandResult> ExecuteStepAsync(
            ScriptStep step,
            ScriptContext context,
            CancellationToken cancellationToken)
        {
            var stepType = step.GetStepType();

            if (stepType == StepType.Unknown)
            {
                context.EmitOutput($"Line {step.LineNumber}: Unknown step type, skipping", ScriptOutputType.Warning);
                return CommandResult.Ok();
            }

            if (!_commands.TryGetValue(stepType, out var command))
            {
                return CommandResult.Fail($"No handler for step type: {stepType}");
            }

            try
            {
                return await command.ExecuteAsync(step, context, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error at line {step.LineNumber}: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return CommandResult.Ok(errorMsg);

                return CommandResult.Fail(errorMsg);
            }
        }

        /// <summary>
        /// Handles debug pause at a breakpoint or in step mode.
        /// </summary>
        private async Task HandleDebugPauseAsync(
            ScriptStep step,
            ScriptContext context,
            CancellationToken cancellationToken)
        {
            context.DebugState.IsPaused = true;
            context.DebugState.PausedAtLine = step.LineNumber;

            context.EmitOutput($"[DEBUG] Paused at line {step.LineNumber}", ScriptOutputType.Debug);

            // Wait for continue or step request
            while (context.DebugState.IsPaused &&
                   !context.DebugState.ContinueRequested &&
                   !context.DebugState.StepRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(100, cancellationToken);
            }

            // Handle the request
            if (context.DebugState.ContinueRequested)
            {
                context.DebugState.StepMode = false; // Exit step mode
            }

            // Reset flags
            context.DebugState.IsPaused = false;
            context.DebugState.ContinueRequested = false;
            context.DebugState.StepRequested = false;
        }
    }
}
