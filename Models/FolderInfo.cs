namespace SSH_Helper.Models
{
    /// <summary>
    /// Metadata for a preset folder.
    /// </summary>
    public class FolderInfo
    {
        /// <summary>
        /// Optional folder-specific base environment override.
        /// Null means inherit from the nearest ancestor folder or the global base environment.
        /// </summary>
        public string? BaseEnvironment { get; set; }

        /// <summary>
        /// Whether the folder is expanded in the UI.
        /// </summary>
        public bool IsExpanded { get; set; } = true;

        /// <summary>
        /// Sort order for manual ordering mode.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Whether the folder is marked as a favorite.
        /// </summary>
        public bool IsFavorite { get; set; }

        public FolderInfo Clone()
        {
            return new FolderInfo
            {
                BaseEnvironment = BaseEnvironment,
                IsExpanded = IsExpanded,
                SortOrder = SortOrder,
                IsFavorite = IsFavorite
            };
        }
    }
}
