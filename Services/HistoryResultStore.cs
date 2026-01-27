using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Stores per-host results for history entries.
    /// </summary>
    public sealed class HistoryResultStore
    {
        private readonly Dictionary<string, List<HostHistoryEntry>> _results = new(StringComparer.Ordinal);

        public void SetResults(string entryId, List<HostHistoryEntry> hostResults)
        {
            if (string.IsNullOrWhiteSpace(entryId) || hostResults == null)
                return;

            _results[entryId] = hostResults;
        }

        public bool TryGetResults(string entryId, out List<HostHistoryEntry>? hostResults)
        {
            hostResults = null;
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            return _results.TryGetValue(entryId, out hostResults);
        }

        public bool RemoveResults(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            return _results.Remove(entryId);
        }

        public void Clear()
        {
            _results.Clear();
        }

        public bool HasResults(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            return _results.ContainsKey(entryId);
        }
    }
}
