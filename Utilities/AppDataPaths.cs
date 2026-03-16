namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Centralized helper for resolving the application's %LocalAppData% folder.
    /// </summary>
    public static class AppDataPaths
    {
        private const string AppFolderName = "SSH_Helper";

        /// <summary>
        /// Returns the application data folder (%LocalAppData%\SSH_Helper), creating it if needed.
        /// </summary>
        public static string GetAppFolder()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return folder;
        }
    }
}
