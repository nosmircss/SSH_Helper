using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public sealed class ScriptExecutorDebugStepTests
{
    [Fact]
    public async Task ExecuteAsync_StepResumeFromBreakpoint_PausesAtNextStep()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        context.DebugState.SetNodeToStepPathMap(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["node-first"] = "steps/0"
        });
        context.DebugState.ToggleNodeBreakpoint("node-first");

        var pausePaths = new List<string>();
        var firstPause = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPause = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResume = new TaskCompletionSource<DebugResumeAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResume = new TaskCompletionSource<DebugResumeAction>(TaskCreationOptions.RunContinuationsAsynchronously);

        executor.DebugPauseStateChanged += (_, args) =>
        {
            if (args.IsPaused && !string.IsNullOrWhiteSpace(args.StepPath))
            {
                lock (pausePaths)
                {
                    pausePaths.Add(args.StepPath!);
                    if (pausePaths.Count == 1)
                    {
                        firstPause.TrySetResult(args.StepPath!);
                    }
                    else if (pausePaths.Count == 2)
                    {
                        secondPause.TrySetResult(args.StepPath!);
                    }
                }

                return;
            }

            if (!args.ResumeAction.HasValue)
            {
                return;
            }

            if (!firstResume.Task.IsCompleted)
            {
                firstResume.TrySetResult(args.ResumeAction.Value);
            }
            else if (!secondResume.Task.IsCompleted)
            {
                secondResume.TrySetResult(args.ResumeAction.Value);
            }
        };

        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "i = 0" },
                new() { Set = "i = i + 1" },
                new() { Set = "i = i + 1" }
            }
        };

        var resultTask = executor.ExecuteAsync(script, context);

        var firstPausePath = await WaitForSignalAsync(firstPause.Task, "first debug pause");
        firstPausePath.Should().Be("steps/0");

        await SignalUntilResumedAsync(context.DebugState, step: true, resumeSignal: firstResume.Task);
        var firstResumeAction = await firstResume.Task;
        firstResumeAction.Should().Be(DebugResumeAction.Step);

        var secondPausePath = await WaitForSignalAsync(secondPause.Task, "second debug pause after step");
        secondPausePath.Should().Be("steps/1");

        await SignalUntilResumedAsync(context.DebugState, step: false, resumeSignal: secondResume.Task);
        var secondResumeAction = await secondResume.Task;
        secondResumeAction.Should().Be(DebugResumeAction.Continue);

        var result = await resultTask;
        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariable("i").Should().Be(2d);
    }

    [Fact]
    public async Task ExecuteAsync_StepIntoParallel_ContinueReleasesAllPausedBranches()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext
        {
            DebugMode = true
        };
        context.DebugState.StepMode = true;

        var firstPause = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResume = new TaskCompletionSource<DebugResumeAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parallelBranchesPaused = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parallelPauseCount = 0;

        executor.DebugPauseStateChanged += (_, args) =>
        {
            if (args.IsPaused && !string.IsNullOrWhiteSpace(args.StepPath))
            {
                if (!firstPause.Task.IsCompleted)
                {
                    firstPause.TrySetResult(args.StepPath!);
                }

                if (args.StepPath!.Contains("/parallel/", StringComparison.Ordinal))
                {
                    if (Interlocked.Increment(ref parallelPauseCount) >= 2)
                    {
                        parallelBranchesPaused.TrySetResult(true);
                    }
                }

                return;
            }

            if (args.ResumeAction.HasValue && !firstResume.Task.IsCompleted)
            {
                firstResume.TrySetResult(args.ResumeAction.Value);
            }
        };

        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Parallel = new SSH_Helper.Services.Scripting.Models.ParallelOptions
                    {
                        Steps = new List<ScriptStep>
                        {
                            new() { Set = "a = 1" },
                            new() { Set = "b = 2" }
                        }
                    }
                }
            }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var resultTask = executor.ExecuteAsync(script, context, cts.Token);

        var firstPausePath = await WaitForSignalAsync(firstPause.Task, "initial pause before stepping into parallel");
        firstPausePath.Should().Be("steps/0");

        await SignalUntilResumedAsync(context.DebugState, step: true, resumeSignal: firstResume.Task);
        await WaitForSignalAsync(parallelBranchesPaused.Task, "both parallel branches should pause under step mode");

        context.DebugState.ContinueRequested = true;

        var completion = await Task.WhenAny(resultTask, Task.Delay(2000));
        if (completion != resultTask)
        {
            cts.Cancel();
            await resultTask;
        }

        completion.Should().Be(resultTask, "continue should release all paused parallel branches");
        var result = await resultTask;
        result.Status.Should().Be(ScriptExitStatus.Success);
    }

    private static async Task<T> WaitForSignalAsync<T>(
        Task<T> signalTask,
        string description,
        int timeoutMs = 2000)
    {
        var completed = await Task.WhenAny(signalTask, Task.Delay(timeoutMs));
        completed.Should().Be(signalTask, $"{description} should be observed within {timeoutMs}ms.");
        return await signalTask;
    }

    private static async Task SignalUntilResumedAsync(
        DebugState state,
        bool step,
        Task<DebugResumeAction> resumeSignal,
        int timeoutMs = 2000)
    {
        var startTicks = Environment.TickCount64;

        while (!resumeSignal.IsCompleted && Environment.TickCount64 - startTicks < timeoutMs)
        {
            if (step)
            {
                state.StepRequested = true;
            }
            else
            {
                state.ContinueRequested = true;
            }

            await Task.Delay(10);
        }

        await WaitForSignalAsync(
            resumeSignal,
            step ? "step resume" : "continue resume",
            timeoutMs);
    }
}
