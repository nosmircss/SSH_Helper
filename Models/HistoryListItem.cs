namespace SSH_Helper.Models
{
    /// <summary>
    /// UI list item for execution history entries.
    /// </summary>
    public sealed class HistoryListItem
    {
        public string Id { get; }
        public string Label { get; set; }
        public string Output { get; set; }
        public bool HasHostResults { get; set; }
        public bool HasDetails { get; set; }

        public HistoryListItem(
            string id,
            string label,
            string output = "",
            bool hasHostResults = false,
            bool hasDetails = false)
        {
            Id = id;
            Label = label;
            Output = output;
            HasHostResults = hasHostResults;
            HasDetails = hasDetails;
        }

        public override string ToString() => Label;
    }
}
