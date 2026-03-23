using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class SshExecutionServiceFlowCanvasDebugBootstrapTests
{
    [Fact]
    public async Task ExecutePresetAsync_WithConfiguredFlowCanvasBreakpoint_PausesBeforeFirstStep()
    {
        using var service = new SshExecutionService();

        var configureMethod = typeof(SshExecutionService).GetMethod(
            "ConfigureFlowCanvasDebugStateForRun");
        configureMethod.Should().NotBeNull(
            "SshExecutionService must expose a deterministic Flow Canvas debug bootstrap entrypoint.");

        configureMethod!.Invoke(
            service,
            new object?[]
            {
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["node-first"] = "steps/0"
                },
                new[] { "node-first" },
                Array.Empty<string>()
            });

        var pauseObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DebugPauseStateChanged += (_, args) =>
        {
            if (!args.IsPaused)
                return;

            pauseObserved.TrySetResult(true);
        };

        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - set: "i = 0"
                  - set: "i = i + 1"
                """
        };

        var resultsTask = service.ExecutePresetAsync(
            new[] { HostConnection.Parse("127.0.0.1") },
            preset,
            string.Empty,
            string.Empty,
            SshTimeoutOptions.FromSeconds(5));

        var pauseTask = await Task.WhenAny(pauseObserved.Task, Task.Delay(2000));
        var pauseHit = pauseTask == pauseObserved.Task && await pauseObserved.Task;

        pauseHit.Should().BeTrue("the first configured Flow Canvas breakpoint should pause execution before step 0 runs.");
        service.ActiveScriptContext.Should().NotBeNull();
        service.ActiveScriptContext!.DebugState.ContinueRequested = true;

        var results = await resultsTask;
        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();
    }
}
