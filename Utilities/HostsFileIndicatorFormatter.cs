namespace SSH_Helper.Utilities
{
    internal static class HostsFileIndicatorFormatter
    {
        private const string UnsavedLabel = "Unsaved";

        public static string Format(string? loadedFilePath, bool isDirty)
        {
            if (string.IsNullOrWhiteSpace(loadedFilePath))
                return UnsavedLabel;

            var fileName = Path.GetFileName(loadedFilePath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = loadedFilePath;

            return isDirty
                ? $"{fileName} (unsaved)"
                : fileName;
        }
    }
}
