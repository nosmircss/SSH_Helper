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
                { StepType.Try, new TryCommand(this) },
                { StepType.Break, new BreakCommand() },
                { StepType.Continue, new ContinueCommand() },
                { StepType.Readfile, new ReadFileCommand() },
                { StepType.Writefile, new WriteFileCommand() },
                { StepType.Input, new InputCommand() },
                { StepType.UpdateColumn, new UpdateColumnCommand() },
                { StepType.UpdateEnvironment, new UpdateEnvironmentCommand() },
                { StepType.Log, new LogCommand() },
                { StepType.Http, new HttpCommand() },
                { StepType.BrowserCallbackCapture, new BrowserCallbackCaptureCommand() },
                { StepType.Ping, new PingCommand() },
                { StepType.Dns, new DnsCommand() },
                { StepType.Portcheck, new PortcheckCommand() },
                { StepType.Sftp, new SftpCommand() },
                { StepType.Webhook, new WebhookCommand() },
                { StepType.Parse, new ParseCommand() },
                { StepType.Choose, new ChooseCommand() },
                { StepType.Multiselect, new MultiselectCommand() },
                { StepType.Confirm, new ConfirmCommand() },
                { StepType.Interactive, new InteractiveCommand() },
                { StepType.Assert, new AssertCommand() },
                { StepType.Switch, new SwitchCommand(this) },
                { StepType.Parallel, new ParallelCommand(this) },
                { StepType.Call, new CallCommand(this) },
                { StepType.Return, new ReturnCommand() },
                { StepType.Table, new TableCommand() },
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
                context.ActiveScript = script;
                context.SubroutineRegistry = script.SubroutineRegistry;

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
                context.RemoveVariable("_last_error");

                // Execute all steps
                var result = await ExecuteStepsAsync(script.Steps, context, cancellationToken, 0);

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
            return await ExecuteStepsAsync(steps, context, cancellationToken, context.LoopDepth);
        }

        /// <summary>
        /// Executes a list of steps at a specific loop depth.
        /// </summary>
        public async Task<CommandResult> ExecuteStepsAsync(
            List<ScriptStep> steps,
            ScriptContext context,
            CancellationToken cancellationToken,
            int loopDepth,
            bool preserveLastErrorOnSuccess = false)
        {
            var previousDepth = context.LoopDepth;
            context.LoopDepth = loopDepth;

            try
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

                    if (result.SuppressedError)
                    {
                        context.SetVariable("_last_error", result.Message ?? string.Empty);
                    }
                    else if (result.Success && !preserveLastErrorOnSuccess)
                    {
                        context.RemoveVariable("_last_error");
                    }

                    // Propagate control flow signals
                    if (result.IsControlFlow)
                        return result;

                    // Stop on error (unless on_error: continue)
                    if (!result.Success)
                    {
                        if (!string.IsNullOrEmpty(result.Message))
                            context.SetVariable("_last_error", result.Message);
                        return result;
                    }
                }

                return CommandResult.Ok();
            }
            finally
            {
                context.LoopDepth = previousDepth;
            }
        }

        /// <summary>
        /// Executes a single step with optional retry logic.
        /// </summary>
        private async Task<CommandResult> ExecuteStepAsync(
            ScriptStep step,
            ScriptContext context,
            CancellationToken cancellationToken)
        {
            var maxRetries = step.Retry.HasValue && step.Retry.Value > 0 ? step.Retry.Value : 0;

            if (maxRetries == 0)
                return await ExecuteStepCoreAsync(step, context, cancellationToken);

            // Retry loop
            var originalOnError = step.OnError;
            CommandResult result = CommandResult.Fail("No attempts");

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                // On non-final attempts, force failures to surface (override on_error)
                var onErrorForAttempt = attempt < maxRetries ? "stop" : originalOnError;
                step.OnError = onErrorForAttempt;
                try
                {
                    result = await ExecuteStepCoreAsync(step, context, cancellationToken);
                }
                finally
                {
                    step.OnError = originalOnError;
                }

                // Don't retry on: success, exit, break, continue, suppressed
                if (result.Success || result.ShouldExit || result.ShouldBreak ||
                    result.ShouldContinue || result.ShouldReturn || result.SuppressedError)
                {
                    if (attempt > 0)
                        context.EmitOutput($"Step succeeded on attempt {attempt + 1}", ScriptOutputType.Debug);
                    return result;
                }

                // Failed - retry if not the last attempt
                if (attempt < maxRetries)
                {
                    var delay = step.RetryDelay.HasValue && step.RetryDelay.Value > 0 ? step.RetryDelay.Value : 1;
                    context.EmitOutput(
                        $"Step failed (attempt {attempt + 1}/{maxRetries + 1}), retrying in {delay}s...",
                        ScriptOutputType.Warning);
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                }
            }

            return result;
        }

        /// <summary>
        /// Executes a single step by dispatching to the appropriate command handler.
        /// </summary>
        private async Task<CommandResult> ExecuteStepCoreAsync(
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

                return CommandResult.ApplyOnError(step, errorMsg);
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

        /// <summary>
        /// Executes one resolved subroutine in an isolated child variable scope.
        /// </summary>
        public async Task<CommandResult> ExecuteSubroutineAsync(
            ScriptSubroutineDefinition definition,
            ScriptContext callerContext,
            IReadOnlyDictionary<string, object?> args,
            IReadOnlyDictionary<string, string> outputBindings,
            CancellationToken cancellationToken)
        {
            var childContext = callerContext.CreateChildScope(args, definition);
            if (childContext.CallDepth > 32)
            {
                return CommandResult.Fail($"Subroutine call depth exceeded the maximum of 32 at '{definition.QualifiedName}'");
            }

            var result = await ExecuteStepsAsync(definition.Subroutine.Steps, childContext, cancellationToken, loopDepth: 0);

            if ((result.Success || result.ShouldReturn) &&
                !result.ShouldExit &&
                !result.ShouldBreak &&
                !result.ShouldContinue)
            {
                callerContext.CopyOutputsFromChild(childContext, outputBindings, definition.Subroutine.Outputs);
                return CommandResult.Ok(result.Message);
            }

            return result;
        }
    }
}
