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

        var removed = store.RemoveResults(entryId);

        removed.Should().BeTrue();
        store.HasResults(entryId).Should().BeFalse();
        store.TryGetResults(entryId, out _).Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var store = new HistoryResultStore();
        store.SetResults("one", new List<HostHistoryEntry> { new() { HostAddress = "host1" } });
        store.SetResults("two", new List<HostHistoryEntry> { new() { HostAddress = "host2" } });

        store.Clear();

        store.HasResults("one").Should().BeFalse();
        store.HasResults("two").Should().BeFalse();
    }
}
