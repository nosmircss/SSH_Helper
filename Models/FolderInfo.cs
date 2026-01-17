namespace SSH_Helper.Models
{
    /// <summary>
    /// Metadata for a preset folder.
    /// </summary>
    public class FolderInfo
    {
        /// <summary>
        /// Whether the folder is expanded in the UI.
        /// </summary>
        public bool IsExpanded { get; set; } = true;

        /// <summary>
        /// Sort order for manual ordering mode.
        /// </summary>
        public int SortOrder { get; set; }

        public FolderInfo Clone()
        {
            return new FolderInfo
            {
                IsExpanded = IsExpanded,
                SortOrder = SortOrder
            };
        }
    }
}
