using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class HistoryListCollectionUpdaterTests
{
    [Fact]
    public void InsertNewest_AddsEntryAtTop_AndUpdatesIndexMap()
    {
        var items = new List<HistoryListItem>
        {
            new("older", "Older")
        };
        var indexEntries = new Dictionary<string, HistoryIndexEntry>(StringComparer.Ordinal)
        {
            ["older"] = new() { Id = "older", Label = "Older" }
        };
        var newEntry = new HistoryIndexEntry
        {
            Id = "newer",
            Label = "Newer",
            HasHostResults = true,
            HasDetails = true
        };

        var (addedItem, removedIds) = HistoryListCollectionUpdater.InsertNewest(items, indexEntries, newEntry, maxEntries: 10);

        addedItem.Id.Should().Be("newer");
        items.Select(item => item.Id).Should().ContainInOrder("newer", "older");
        indexEntries.Should().ContainKey("newer");
        indexEntries["newer"].Label.Should().Be("Newer");
        removedIds.Should().BeEmpty();
    }

    [Fact]
    public void InsertNewest_WhenRetentionExceeded_TrimsTailAndReturnsRemovedIds()
    {
        var items = new List<HistoryListItem>
        {
            new("current", "Current"),
            new("older", "Older")
        };
        var indexEntries = new Dictionary<string, HistoryIndexEntry>(StringComparer.Ordinal)
        {
            ["current"] = new() { Id = "current", Label = "Current" },
            ["older"] = new() { Id = "older", Label = "Older" }
        };
        var newEntry = new HistoryIndexEntry
        {
            Id = "newest",
            Label = "Newest"
        };

        var (_, removedIds) = HistoryListCollectionUpdater.InsertNewest(items, indexEntries, newEntry, maxEntries: 2);

        items.Select(item => item.Id).Should().ContainInOrder("newest", "current");
        removedIds.Should().ContainSingle().Which.Should().Be("older");
        indexEntries.Should().ContainKey("newest");
        indexEntries.Should().ContainKey("current");
        indexEntries.Should().NotContainKey("older");
    }

    [Fact]
    public void InsertNewest_WhenEntryAlreadyExists_ReplacesExistingItemWithoutDuplication()
    {
        var items = new List<HistoryListItem>
        {
            new("same", "Old Label"),
            new("older", "Older")
        };
        var indexEntries = new Dictionary<string, HistoryIndexEntry>(StringComparer.Ordinal)
        {
            ["same"] = new() { Id = "same", Label = "Old Label" },
            ["older"] = new() { Id = "older", Label = "Older" }
        };
        var replacementEntry = new HistoryIndexEntry
        {
            Id = "same",
            Label = "Updated Label",
            HasHostResults = true
        };

        HistoryListCollectionUpdater.InsertNewest(items, indexEntries, replacementEntry, maxEntries: 10);

        items.Should().HaveCount(2);
        items[0].Id.Should().Be("same");
        items[0].Label.Should().Be("Updated Label");
        items[0].HasHostResults.Should().BeTrue();
        items[1].Id.Should().Be("older");
    }
}
