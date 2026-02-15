using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Stores per-host results for history entries.
    /// </summary>
    public sealed class HistoryResultStore
    {
        private readonly Dictionary<string, List<HostHistoryEntry>> _results = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ExecutionDetails> _details = new(StringComparer.Ordinal);

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

            var removedResults = _results.Remove(entryId);
            var removedDetails = _details.Remove(entryId);
            return removedResults || removedDetails;
        }

        public void Clear()
        {
            _results.Clear();
            _details.Clear();
        }

        public bool HasResults(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            return _results.ContainsKey(entryId);
        }

        public void SetDetails(string entryId, ExecutionDetails details)
        {
            if (string.IsNullOrWhiteSpace(entryId) || details == null)
                return;

            _details[entryId] = details;
        }

        public bool TryGetDetails(string entryId, out ExecutionDetails? details)
        {
            details = null;
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            return _details.TryGetValue(entryId, out details);
        }

        public bool HasDetails(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            return _details.ContainsKey(entryId);
        }

        public IEnumerable<KeyValuePair<string, List<HostHistoryEntry>>> EnumerateResults()
        {
            return _results;
        }

        public IEnumerable<KeyValuePair<string, ExecutionDetails>> EnumerateDetails()
        {
            return _details;
        }
    }
}
