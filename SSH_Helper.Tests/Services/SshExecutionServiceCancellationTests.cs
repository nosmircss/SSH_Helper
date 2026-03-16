using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class SshExecutionServiceCancellationTests
{
    [Fact]
    public async Task ExecutePresetAsync_LocalScript_StopMarksResultCancelled()
    {
        using var service = new SshExecutionService();
        var preset = new PresetInfo { Commands = CreateLongWaitScript() };
        preset.IsScript.Should().BeTrue();

        var resultsTask = service.ExecutePresetAsync(
            new[] { HostConnection.Parse("127.0.0.1") },
            preset,
            string.Empty,
            string.Empty,
            SshTimeoutOptions.FromSeconds(30));

        await Task.Delay(250);
        service.Stop();

        var results = await resultsTask;

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].WasCancelled.Should().BeTrue();
        results[0].ErrorMessage.Should().Be("Operation cancelled");
        results[0].Output.Should().Contain("CANCELLED");
    }

    [Fact]
    public async Task ExecuteFolderAsync_Sequential_StopMarksHostResultCancelled()
    {
        using var service = new SshExecutionService();
        var presets = new Dictionary<string, PresetInfo>
        {
            ["One"] = new() { Commands = CreateLongWaitScript() },
            ["Two"] = new() { Commands = CreateLongWaitScript() }
        };

        var resultsTask = service.ExecuteFolderAsync(
            new[] { HostConnection.Parse("127.0.0.1") },
            presets,
            string.Empty,
            string.Empty,
            SshTimeoutOptions.FromSeconds(30),
            new FolderExecutionOptions
            {
                SelectedPresets = presets.Keys.ToList(),
                RunPresetsInParallel = false,
                ParallelHostCount = 1
            });

        await Task.Delay(250);
        service.Stop();

        var results = await resultsTask;

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].WasCancelled.Should().BeTrue();
        results[0].ErrorMessage.Should().Be("Operation cancelled");
    }

    [Fact]
    public async Task ExecuteFolderAsync_Parallel_StopMarksHostResultCancelled()
    {
        using var service = new SshExecutionService();
        var presets = new Dictionary<string, PresetInfo>
        {
            ["One"] = new() { Commands = CreateLongWaitScript() },
            ["Two"] = new() { Commands = CreateLongWaitScript() }
        };

        var resultsTask = service.ExecuteFolderAsync(
            new[] { HostConnection.Parse("127.0.0.1") },
            presets,
            string.Empty,
            string.Empty,
            SshTimeoutOptions.FromSeconds(30),
            new FolderExecutionOptions
            {
                SelectedPresets = presets.Keys.ToList(),
                RunPresetsInParallel = true,
                ParallelHostCount = 1
            });

        await Task.Delay(250);
        service.Stop();

        var results = await resultsTask;

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].WasCancelled.Should().BeTrue();
        results[0].ErrorMessage.Should().Be("Operation cancelled");
    }

    private static string CreateLongWaitScript()
    {
        return "---\nsteps:\n  - wait: 10\n";
    }
}
