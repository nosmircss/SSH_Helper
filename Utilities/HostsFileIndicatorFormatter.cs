namespace SSH_Helper.Utilities
{
    internal static class HostsFileIndicatorFormatter
    {
        private const string UnsavedLabel = "Unsaved";

        public static string Format(string? loadedFilePath, bool isDirty, CsvFileSyncStatus syncStatus)
        {
            if (string.IsNullOrWhiteSpace(loadedFilePath))
                return UnsavedLabel;

            var fileName = Path.GetFileName(loadedFilePath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = loadedFilePath;

            var suffixes = new List<string>();
            if (isDirty)
            {
                suffixes.Add("unsaved");
            }

            if (syncStatus == CsvFileSyncStatus.ChangedOnDisk)
            {
                suffixes.Add("disk changed");
            }
            else if (syncStatus == CsvFileSyncStatus.MissingOnDisk)
            {
                suffixes.Add("missing on disk");
            }

            return suffixes.Count == 0
                ? fileName
                : $"{fileName} ({string.Join(", ", suffixes)})";
        }
    }
}
