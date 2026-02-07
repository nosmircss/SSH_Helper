using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class HistoryResultStoreTests
{
    [Fact]
    public void SetAndGetResults_StoresAndReturnsResults()
    {
        var store = new HistoryResultStore();
        var entryId = "entry-1";
        var results = new List<HostHistoryEntry>
        {
            new()
            {
                HostAddress = "host1",
                Output = "ok",
                Success = true,
                Timestamp = DateTime.UtcNow
            }
        };

        store.SetResults(entryId, results);

        store.HasResults(entryId).Should().BeTrue();
        store.TryGetResults(entryId, out var loaded).Should().BeTrue();
        loaded.Should().BeEquivalentTo(results);
    }

    [Fact]
    public void RemoveResults_CleansUpStoredEntry()
    {
        var store = new HistoryResultStore();
        var entryId = "entry-2";
        store.SetResults(entryId, new List<HostHistoryEntry> { new() { HostAddress = "host2" } });
        store.SetDetails(entryId, CreateDetails("Preset A"));

        var removed = store.RemoveResults(entryId);

        removed.Should().BeTrue();
        store.HasResults(entryId).Should().BeFalse();
        store.TryGetResults(entryId, out _).Should().BeFalse();
        store.HasDetails(entryId).Should().BeFalse();
        store.TryGetDetails(entryId, out _).Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var store = new HistoryResultStore();
        store.SetResults("one", new List<HostHistoryEntry> { new() { HostAddress = "host1" } });
        store.SetResults("two", new List<HostHistoryEntry> { new() { HostAddress = "host2" } });
        store.SetDetails("one", CreateDetails("Preset One"));
        store.SetDetails("two", CreateDetails("Preset Two"));

        store.Clear();

        store.HasResults("one").Should().BeFalse();
        store.HasResults("two").Should().BeFalse();
        store.HasDetails("one").Should().BeFalse();
        store.HasDetails("two").Should().BeFalse();
    }

    [Fact]
    public void SetAndGetDetails_StoresAndReturnsDetails()
    {
        var store = new HistoryResultStore();
        var entryId = "entry-3";
        var details = CreateDetails("Folder Run");

        store.SetDetails(entryId, details);

        store.HasDetails(entryId).Should().BeTrue();
        store.TryGetDetails(entryId, out var loaded).Should().BeTrue();
        loaded.Should().NotBeNull();
        loaded!.PresetName.Should().Be("Folder Run");
        loaded.RunMode.Should().Be("Sequential presets");
    }

    private static ExecutionDetails CreateDetails(string presetName)
    {
        return new ExecutionDetails
        {
            PresetName = presetName,
            Commands = "show version",
            PresetType = "Simple",
            StartTimeUtc = DateTime.UtcNow.AddSeconds(-2),
            EndTimeUtc = DateTime.UtcNow,
            Username = "admin",
            CommandTimeoutSeconds = 30,
            ConnectionTimeoutSeconds = 15,
            UseConnectionPooling = true,
            RunMode = "Sequential presets"
        };
    }
}
