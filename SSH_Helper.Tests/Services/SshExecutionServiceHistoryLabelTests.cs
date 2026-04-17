using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class SshExecutionServiceHistoryLabelTests
{
    [Fact]
    public async Task ExecuteFolderAsync_SequentialLaterPresetCanClearEarlierHistoryLabel()
    {
        using var service = new SshExecutionService();
        var presets = new Dictionary<string, PresetInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Set"] = new() { Commands = CreateSetHistoryLabelScript("Core Router", replace: true) },
            ["Clear"] = new() { Commands = CreateSetHistoryLabelScript(string.Empty) }
        };

        var results = await service.ExecuteFolderAsync(
            new[] { HostConnection.Parse("127.0.0.1") },
            presets,
            string.Empty,
            string.Empty,
            SshTimeoutOptions.FromSeconds(5),
            new FolderExecutionOptions
            {
                SelectedPresets = new List<string> { "Set", "Clear" },
                RunPresetsInParallel = false,
                ParallelHostCount = 1
            });

        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();
        results[0].HistoryLabelTouched.Should().BeTrue();
        results[0].HistoryLabel.Should().BeNull();
        results[0].HistoryLabelReplacesAddress.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteFolderAsync_ParallelPresetsResolveHistoryLabelBySelectedOrder()
    {
        using var service = new SshExecutionService();
        var presets = new Dictionary<string, PresetInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["SlowFirst"] = new() { Commands = CreateDelayedSetHistoryLabelScript(delaySeconds: 1, value: "First Finished Last") },
            ["FastSecond"] = new() { Commands = CreateSetHistoryLabelScript("Second Should Win", replace: true) }
        };

        var results = await service.ExecuteFolderAsync(
            new[] { HostConnection.Parse("127.0.0.1") },
            presets,
            string.Empty,
            string.Empty,
            SshTimeoutOptions.FromSeconds(5),
            new FolderExecutionOptions
            {
                SelectedPresets = new List<string> { "SlowFirst", "FastSecond" },
                RunPresetsInParallel = true,
                ParallelHostCount = 1
            });

        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();
        results[0].HistoryLabelTouched.Should().BeTrue();
        results[0].HistoryLabel.Should().Be("Second Should Win");
        results[0].HistoryLabelReplacesAddress.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteFolderAsync_SequentialLaterPresetCanAppendEarlierHistoryLabel()
    {
        using var service = new SshExecutionService();
        var presets = new Dictionary<string, PresetInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Set"] = new() { Commands = CreateSetHistoryLabelScript("Core", replace: true) },
            ["Append"] = new() { Commands = CreateSetHistoryLabelScript("Router", mode: "append", separator: " ") }
        };

        var results = await service.ExecuteFolderAsync(
            new[] { HostConnection.Parse("127.0.0.1") },
            presets,
            string.Empty,
            string.Empty,
            SshTimeoutOptions.FromSeconds(5),
            new FolderExecutionOptions
            {
                SelectedPresets = new List<string> { "Set", "Append" },
                RunPresetsInParallel = false,
                ParallelHostCount = 1
            });

        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();
        results[0].HistoryLabelTouched.Should().BeTrue();
        results[0].HistoryLabel.Should().Be("Core Router");
        results[0].HistoryLabelReplacesAddress.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteFolderAsync_ParallelPresetsAppendHistoryLabelBySelectedOrder()
    {
        using var service = new SshExecutionService();
        var presets = new Dictionary<string, PresetInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["SlowFirst"] = new() { Commands = CreateDelayedSetHistoryLabelScript(delaySeconds: 1, value: "Core", replace: true) },
            ["FastSecond"] = new() { Commands = CreateSetHistoryLabelScript("Router", mode: "append", separator: " ") }
        };

        var results = await service.ExecuteFolderAsync(
            new[] { HostConnection.Parse("127.0.0.1") },
            presets,
            string.Empty,
            string.Empty,
            SshTimeoutOptions.FromSeconds(5),
            new FolderExecutionOptions
            {
                SelectedPresets = new List<string> { "SlowFirst", "FastSecond" },
                RunPresetsInParallel = true,
                ParallelHostCount = 1
            });

        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();
        results[0].HistoryLabelTouched.Should().BeTrue();
        results[0].HistoryLabel.Should().Be("Core Router");
        results[0].HistoryLabelReplacesAddress.Should().BeTrue();
    }

    private static string CreateSetHistoryLabelScript(
        string value,
        bool replace = false,
        string? mode = null,
        string? separator = null)
    {
        var newline = Environment.NewLine;
        var replaceLine = replace
            ? $"      replace: true{newline}"
            : string.Empty;
        var modeLine = string.IsNullOrWhiteSpace(mode)
            ? string.Empty
            : $"      mode: {mode}{newline}";
        var separatorLine = separator == null
            ? string.Empty
            : $"      separator: {QuoteYaml(separator)}{newline}";

        return
            $"---{newline}" +
            $"steps:{newline}" +
            $"  - sethistorylabel:{newline}" +
            $"      value: {QuoteYaml(value)}{newline}" +
            modeLine +
            separatorLine +
            replaceLine;
    }

    private static string CreateDelayedSetHistoryLabelScript(
        int delaySeconds,
        string value,
        bool replace = false,
        string? mode = null,
        string? separator = null)
    {
        var newline = Environment.NewLine;
        return
            $"---{newline}" +
            $"steps:{newline}" +
            $"  - wait: {delaySeconds}{newline}" +
            $"  - sethistorylabel:{newline}" +
            $"      value: {QuoteYaml(value)}{newline}" +
            (string.IsNullOrWhiteSpace(mode) ? string.Empty : $"      mode: {mode}{newline}") +
            (separator == null ? string.Empty : $"      separator: {QuoteYaml(separator)}{newline}") +
            (replace ? $"      replace: true{newline}" : string.Empty);
    }

    private static string QuoteYaml(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }
}
