using SSH_Helper.Models;

namespace SSH_Helper.UI;

internal static class HistoryListCollectionUpdater
{
    internal static HistoryListItem CreateListItem(HistoryIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new HistoryListItem(
            entry.Id,
            entry.Label,
            output: string.Empty,
            hasHostResults: entry.HasHostResults,
            hasDetails: entry.HasDetails);
    }

    internal static (HistoryListItem AddedItem, IReadOnlyList<string> RemovedIds) InsertNewest(
        IList<HistoryListItem> items,
        IDictionary<string, HistoryIndexEntry> indexEntries,
        HistoryIndexEntry entry,
        int maxEntries)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(indexEntries);
        ArgumentNullException.ThrowIfNull(entry);

        var entryId = entry.Id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(entryId))
            throw new ArgumentException("History index entry id is required.", nameof(entry));

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (string.Equals(items[i].Id, entryId, StringComparison.Ordinal))
            {
                items.RemoveAt(i);
                break;
            }
        }

        indexEntries[entryId] = entry;

        var addedItem = CreateListItem(entry);
        items.Insert(0, addedItem);

        var removedIds = new List<string>();
        if (maxEntries > 0)
        {
            while (items.Count > maxEntries)
            {
                var removedItem = items[^1];
                removedIds.Add(removedItem.Id);
                items.RemoveAt(items.Count - 1);
                indexEntries.Remove(removedItem.Id);
            }
        }

        return (addedItem, removedIds);
    }
}
