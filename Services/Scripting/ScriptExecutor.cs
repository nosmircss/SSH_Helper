using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Event args for step execution lifecycle events.
    /// </summary>
    public class StepExecutionEventArgs : EventArgs
    {
        /// <summary>Position of the step within its parent step list.</summary>
        public int StepIndex { get; init; }

        /// <summary>Canonical scope-aware step identity (e.g., steps/2/then/0).</summary>
        public string? StepPath { get; init; }

        /// <summary>The command type being executed.</summary>
        public StepType StepType { get; init; }

        /// <summary>The YAML line number of the step.</summary>
        public int LineNumber { get; init; }

        /// <summary>The step's user-defined name, if any.</summary>
        public string? StepName { get; init; }

        /// <summary>Whether the step completed successfully (only set on StepCompleted).</summary>
        public bool? Success { get; init; }

        /// <summary>The output produced by the step (only set on StepCompleted).</summary>
        public string? Output { get; init; }

        /// <summary>Wall-clock execution time in milliseconds (only set on StepCompleted).</summary>
        public long? DurationMs { get; init; }

        /// <summary>Whether the step was skipped (e.g., disabled node).</summary>
        public bool Skipped { get; init; }

        /// <summary>Loop body execution count (only set on StepCompleted for foreach/while/repeat).</summary>
        public int? IterationCount { get; init; }

        /// <summary>Scope-path key of the branch taken (only set on StepCompleted for if/switch).</summary>
        public string? BranchTaken { get; init; }

        /// <summary>
        /// Live loop-iteration stack at the moment the event fired (outermost first).
        /// Null or empty outside loops. The list is an immutable point-in-time snapshot —
        /// later iterations never mutate it. NOTE: a loop's own StepCompleted fires after
        /// its frame is popped, so a loop's events carry only its ancestors' frames.
        /// </summary>
        public IReadOnlyList<IterationFrame>? IterationStack { get; init; }

        /// <summary>True when the step failed but the error was suppressed (on_error: continue) —
        /// reported as success for control flow, but flagged so debugging surfaces (the canvas
        /// iteration stepper's failure markers) can still find it.</summary>
        public bool SuppressedError { get; init; }
    }

    /// <summary>
    /// Event args for debug pause state transitions.
    /// </summary>
    public class DebugPauseStateChangedEventArgs : EventArgs
    {
        /// <summary>True when entering pause, false when resuming.</summary>
        public bool IsPaused { get; init; }

        /// <summary>Position of the paused/resumed step in the current step list.</summary>
        public int StepIndex { get; init; }

        /// <summary>Canonical scope-aware step identity (e.g., steps/2/then/0).</summary>
        public string? StepPath { get; init; }

        /// <summary>The YAML line number of the paused/resumed step.</summary>
        public int LineNumber { get; init; }

        /// <summary>Resume action used when leaving pause.</summary>
        public DebugResumeAction? ResumeAction { get; init; }
    }

    /// <summary>
    /// Executes parsed scripts by interpreting steps and dispatching to command handlers.
    /// </summary>
    public class ScriptExecutor
    {
        private readonly Dictionary<StepType, IScriptCommand> _commands;

        // Container steps re-enter the executor to run nested steps. After they finish,
        // ScriptContext.LastCommandOutput holds the output of a send nested INSIDE them, so
        // their block must instead report the output carried from the send preceding the
        // container's start. Leaf steps keep LastCommandOutput as-is.
        private static readonly HashSet<StepType> ContainerStepTypes = new()
        {
            StepType.If, StepType.Foreach, StepType.While, StepType.Repeat,
            StepType.Try, StepType.Switch, StepType.Parallel, StepType.Call,
        };

        /// <summary>
        /// Raised before a step begins execution.
        /// </summary>
        public event EventHandler<StepExecutionEventArgs>? StepStarting;

        /// <summary>
        /// Raised after a step finishes execution.
        /// </summary>
        public event EventHandler<StepExecutionEventArgs>? StepCompleted;

        /// <summary>
        /// Raised when debug pause state changes.
        /// </summary>
        public event EventHandler<DebugPauseStateChangedEventArgs>? DebugPauseStateChanged;

        public ScriptExecutor()
            : this(null, null)
        {
        }

        internal ScriptExecutor(
            IBrowserCallbackUiHost? browserCallbackUiHost,
            ILocalCmdConfirmation? localCmdConfirmation = null,
            Func<ScriptContext, SendCommand.ISendCommandSession?>? sendSessionResolver = null)
        {
            browserCallbackUiHost ??= new BrowserCallbackUiHost(BrowserCallbackWebViewProfileManager.Shared);

            // Register command handlers
            _commands = new Dictionary<StepType, IScriptCommand>
            {
                { StepType.Send, sendSessionResolver is null ? new SendCommand() : new SendCommand(sendSessionResolver) },
                { StepType.Print, new PrintCommand() },
                { StepType.Wait, new WaitCommand() },
                { StepType.Set, new SetCommand() },
                { StepType.Exit, new ExitCommand() },
                { StepType.Extract, new ExtractCommand() },
                { StepType.If, new IfCommand(this) },
                { StepType.Foreach, new ForeachCommand(this) },
                { StepType.While, new WhileCommand(this) },
                { StepType.Repeat, new RepeatCommand(this) },
                { StepType.Try, new TryCommand(this) },
                { StepType.Break, new BreakCommand() },
                { StepType.Continue, new ContinueCommand() },
                { StepType.Readfile, new ReadFileCommand() },
                { StepType.Writefile, new WriteFileCommand() },
                { StepType.Exists, new ExistsCommand() },
                { StepType.PlaySound, new PlaySoundCommand() },
                { StepType.Input, new InputCommand() },
                { StepType.UpdateColumn, new UpdateColumnCommand() },
                { StepType.UpdateEnvironment, new UpdateEnvironmentCommand() },
                { StepType.Log, new LogCommand() },
                { StepType.Http, new HttpCommand() },
                { StepType.BrowserCallbackCapture, new BrowserCallbackCaptureCommand(browserCallbackUiHost, BrowserCallbackCaptureCommand.CreateListener) },
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
                { StepType.LocalCmd, new LocalCmdCommand(localCmdConfirmation) },
                { StepType.Vault, new VaultCommand() },
                { StepType.SetHistoryLabel, new SetHistoryLabelCommand() },
                { StepType.Notify, new NotifyCommand() },
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
            var wasCancelled = false;
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
                AssignStepPaths(script.Steps, "steps");

                if (script.Subroutines != null)
                {
                    foreach (var definition in script.Subroutines)
                    {
                        AssignStepPaths(
                            definition.Value.Steps,
                            $"subroutines/{definition.Key}/steps");
                    }
                }

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
                wasCancelled = true;
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
            finally
            {
                if (context.SoftAssertPassed + context.SoftAssertFailed > 0)
                {
                    context.EmitOutput(
                        $"Soft assertions: {context.SoftAssertPassed} passed, {context.SoftAssertFailed} failed",
                        context.SoftAssertFailed > 0 ? ScriptOutputType.Warning : ScriptOutputType.Success);
                }

                LocalCmdCommand.CleanupTrackedBackgroundProcesses(context, wasCancelled);
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
                for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    var step = steps[stepIndex];
                    var stepPath = step.StepPath ?? $"steps/{stepIndex}";
                    // Loop commands read step.StepPath to tag iteration frames, so make the
                    // effective path visible on the step even when it wasn't assigned
                    // (hand-built scripts). Idempotent: same value on every iteration.
                    step.StepPath ??= stepPath;
                    cancellationToken.ThrowIfCancellationRequested();

                    // Handle debug pausing
                    if (context.DebugState.ShouldPauseAtStep(stepPath, step.LineNumber))
                    {
                        await HandleDebugPauseAsync(stepIndex, stepPath, step, context, cancellationToken);
                    }

                    var stepType = step.GetStepType();

                    // Check if node is disabled
                    var nodeId = context.DebugState.GetNodeIdForStepPath(stepPath);
                    if (nodeId != null && context.DebugState.IsNodeDisabled(nodeId))
                    {
                        StepCompleted?.Invoke(this, new StepExecutionEventArgs
                        {
                            StepIndex = stepIndex,
                            StepPath = stepPath,
                            StepType = stepType,
                            LineNumber = step.LineNumber,
                            Success = true,
                            Skipped = true,
                            IterationStack = context.IterationStack
                        });
                        continue;
                    }

                    // Universal `when:` guard: skip non-foreach steps whose guard evaluates false.
                    // (foreach evaluates `when:` per item inside ForeachCommand.)
                    if (stepType != StepType.Foreach && !string.IsNullOrEmpty(step.When))
                    {
                        var guard = new ExpressionEvaluator(context);
                        if (!guard.Evaluate(context.SubstituteVariables(step.When)))
                        {
                            StepCompleted?.Invoke(this, new StepExecutionEventArgs
                            {
                                StepIndex = stepIndex,
                                StepPath = stepPath,
                                StepType = stepType,
                                LineNumber = step.LineNumber,
                                Success = true,
                                Skipped = true,
                                IterationStack = context.IterationStack
                            });
                            continue;
                        }
                    }

                    // Fire step-starting event
                    StepStarting?.Invoke(this, new StepExecutionEventArgs
                    {
                        StepIndex = stepIndex,
                        StepPath = stepPath,
                        StepType = stepType,
                        LineNumber = step.LineNumber,
                        StepName = null,
                        IterationStack = context.IterationStack
                    });

                    // Output carried into this step = whatever the most recent send produced
                    // before the step started. A container overwrites LastCommandOutput via a
                    // nested send, so capture it now to report it instead.
                    var carriedOutput = context.LastCommandOutput;

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var result = await ExecuteStepAsync(step, context, cancellationToken);
                    sw.Stop();

                    // A send block shows its own output (empty when it produced none). A non-send
                    // leaf never changed LastCommandOutput, so it already holds the preceding
                    // send's output. A container reports the send preceding its start, not the
                    // nested send it just ran.
                    var stepOutput = ContainerStepTypes.Contains(stepType)
                        ? carriedOutput
                        : context.LastCommandOutput;

                    // Fire step-completed event
                    StepCompleted?.Invoke(this, new StepExecutionEventArgs
                    {
                        StepIndex = stepIndex,
                        StepPath = stepPath,
                        StepType = stepType,
                        LineNumber = step.LineNumber,
                        StepName = null,
                        Success = result.Success,
                        Output = stepOutput,
                        DurationMs = sw.ElapsedMilliseconds,
                        IterationCount = result.IterationCount,
                        BranchTaken = result.BranchTaken,
                        SuppressedError = result.SuppressedError,
                        IterationStack = context.IterationStack
                    });

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
        /// Uses async signaling for instant response (no polling delay).
        /// </summary>
        private async Task HandleDebugPauseAsync(
            int stepIndex,
            string stepPath,
            ScriptStep step,
            ScriptContext context,
            CancellationToken cancellationToken)
        {
            context.DebugState.IsPaused = true;
            context.DebugState.PausedAtLine = step.LineNumber;
            DebugPauseStateChanged?.Invoke(this, new DebugPauseStateChangedEventArgs
            {
                IsPaused = true,
                StepIndex = stepIndex,
                StepPath = stepPath,
                LineNumber = step.LineNumber
            });

            context.EmitOutput($"[DEBUG] Paused at line {step.LineNumber}", ScriptOutputType.Debug);

            // Wait for resume signal (step or continue) — instant response, no polling
            // WaitForResumeAsync resets request flags atomically before waiting
            var action = await context.DebugState.WaitForResumeAsync(cancellationToken);

            if (action == DebugResumeAction.Step)
            {
                context.DebugState.StepMode = true;
            }
            else if (action == DebugResumeAction.Continue)
            {
                context.DebugState.StepMode = false; // Exit step mode
            }

            context.DebugState.IsPaused = false;
            DebugPauseStateChanged?.Invoke(this, new DebugPauseStateChangedEventArgs
            {
                IsPaused = false,
                StepIndex = stepIndex,
                StepPath = stepPath,
                LineNumber = step.LineNumber,
                ResumeAction = action
            });
        }

        private static void AssignStepPaths(List<ScriptStep> steps, string scopePath)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var stepPath = $"{scopePath}/{i}";
                step.StepPath = stepPath;

                if (step.Then != null && step.Then.Count > 0)
                    AssignStepPaths(step.Then, $"{stepPath}/then");

                if (step.Else != null && step.Else.Count > 0)
                    AssignStepPaths(step.Else, $"{stepPath}/else");

                if (step.Elif != null && step.Elif.Count > 0)
                {
                    for (int elifIndex = 0; elifIndex < step.Elif.Count; elifIndex++)
                    {
                        var branch = step.Elif[elifIndex];
                        if (branch.Then.Count > 0)
                            AssignStepPaths(branch.Then, $"{stepPath}/elif/{elifIndex}/then");
                    }
                }

                if (step.Do != null && step.Do.Count > 0)
                    AssignStepPaths(step.Do, $"{stepPath}/do");

                if (step.Try != null && step.Try.Count > 0)
                    AssignStepPaths(step.Try, $"{stepPath}/try");

                if (step.Catch != null && step.Catch.Count > 0)
                    AssignStepPaths(step.Catch, $"{stepPath}/catch");

                if (step.Finally != null && step.Finally.Count > 0)
                    AssignStepPaths(step.Finally, $"{stepPath}/finally");

                if (step.Cases != null && step.Cases.Count > 0)
                {
                    for (int caseIndex = 0; caseIndex < step.Cases.Count; caseIndex++)
                    {
                        var branch = step.Cases[caseIndex];
                        if (branch.Do.Count > 0)
                            AssignStepPaths(branch.Do, $"{stepPath}/cases/{caseIndex}/do");
                    }
                }

                if (step.Parallel?.Steps != null && step.Parallel.Steps.Count > 0)
                {
                    for (int branchIndex = 0; branchIndex < step.Parallel.Steps.Count; branchIndex++)
                    {
                        var branchStep = step.Parallel.Steps[branchIndex];
                        AssignStepPaths(
                            new List<ScriptStep> { branchStep },
                            $"{stepPath}/parallel/{branchIndex}");
                    }
                }
            }
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
