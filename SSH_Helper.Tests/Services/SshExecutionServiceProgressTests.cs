using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class SshExecutionServiceProgressTests
{
    [Fact]
    public async Task ExecuteFolderAsync_MultiHostSinglePreset_ReportsCompletedOperations()
    {
        using var service = new SshExecutionService();
        var progress = new CollectingProgress();
        var presets = new Dictionary<string, PresetInfo>
        {
            ["Single"] = new() { Commands = CreateLocalProgressScript("single") }
        };

        var results = await service.ExecuteFolderAsync(
            new[]
            {
                HostConnection.Parse("127.0.0.1"),
                HostConnection.Parse("127.0.0.2"),
                HostConnection.Parse("127.0.0.3")
            },
            presets,
            string.Empty,
            string.Empty,
            SshTimeoutOptions.FromSeconds(5),
            new FolderExecutionOptions
            {
                SelectedPresets = new List<string> { "Single" },
                RunPresetsInParallel = false,
                ParallelHostCount = 3
            },
            progress);

        results.Should().HaveCount(3);
        progress.Reports.Should().NotBeEmpty();
        progress.Reports.Should().Contain(r => r.CompletedOperations > 0 && r.CompletedOperations < 3);
        progress.Reports.Max(r => r.CompletedOperations).Should().Be(3);
        progress.Reports.Should().OnlyContain(r => r.TotalOperations == 3);
    }

    [Fact]
    public async Task ExecuteFolderAsync_MultiHostMultiPreset_ReportsTotalHostTaskOperations()
    {
        using var service = new SshExecutionService();
        var progress = new CollectingProgress();
        var presets = new Dictionary<string, PresetInfo>
        {
            ["One"] = new() { Commands = CreateLocalProgressScript("one") },
            ["Two"] = new() { Commands = CreateLocalProgressScript("two") }
        };

        var results = await service.ExecuteFolderAsync(
            new[]
            {
                HostConnection.Parse("127.0.0.1"),
                HostConnection.Parse("127.0.0.2")
            },
            presets,
            string.Empty,
            string.Empty,
            SshTimeoutOptions.FromSeconds(5),
            new FolderExecutionOptions
            {
                SelectedPresets = new List<string> { "One", "Two" },
                RunPresetsInParallel = true,
                ParallelHostCount = 2
            },
            progress);

        results.Should().HaveCount(2);
        progress.Reports.Should().NotBeEmpty();
        progress.Reports.Should().Contain(r => r.CompletedOperations > 0 && r.CompletedOperations < 4);
        progress.Reports.Max(r => r.CompletedOperations).Should().Be(4);
        progress.Reports.Should().OnlyContain(r => r.TotalOperations == 4);
    }

    private static string CreateLocalProgressScript(string label)
    {
        return
            $"---{Environment.NewLine}" +
            "steps:" + Environment.NewLine +
            "  - print:" + Environment.NewLine +
            $"      message: \"{label}\"" + Environment.NewLine;
    }

    private sealed class CollectingProgress : IProgress<FolderExecutionProgress>
    {
        private readonly object _sync = new();

        public List<FolderExecutionProgress> Reports { get; } = new();

        public void Report(FolderExecutionProgress value)
        {
            lock (_sync)
            {
                Reports.Add(new FolderExecutionProgress
                {
                    CompletedOperations = value.CompletedOperations,
                    TotalOperations = value.TotalOperations,
                    CurrentHost = value.CurrentHost,
                    CurrentPreset = value.CurrentPreset,
                    CompletedPresets = value.CompletedPresets,
                    TotalPresets = value.TotalPresets,
                    CompletedHosts = value.CompletedHosts,
                    TotalHosts = value.TotalHosts
                });
            }
        }
    }
}
