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

        public HistoryListItem(string id, string label, string output)
        {
            Id = id;
            Label = label;
            Output = output;
        }

        public override string ToString() => Label;
    }
}
