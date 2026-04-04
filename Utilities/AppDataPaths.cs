namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Centralized helper for resolving the application's runtime storage folder.
    /// </summary>
    public static class AppDataPaths
    {
        private const string AppFolderName = "SSH_Helper";

        /// <summary>
        /// True when this build was compiled in portable mode.
        /// </summary>
        public static bool IsPortableBuild
        {
            get
            {
#if PORTABLE_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Returns the application storage folder, creating it if needed.
        /// Standard build: %LocalAppData%\SSH_Helper
        /// Portable build: executable directory
        /// </summary>
        public static string GetAppFolder()
        {
            var folder = ResolveAppFolder(
                IsPortableBuild,
                AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return folder;
        }

        /// <summary>
        /// Resolves the application storage folder for a given build mode.
        /// </summary>
        internal static string ResolveAppFolder(bool portableBuild, string baseDirectory, string localAppDataDirectory)
        {
            if (portableBuild)
            {
                if (string.IsNullOrWhiteSpace(baseDirectory))
                    throw new ArgumentException("Base directory is required for portable mode.", nameof(baseDirectory));

                return Path.GetFullPath(baseDirectory);
            }

            if (string.IsNullOrWhiteSpace(localAppDataDirectory))
                throw new ArgumentException("LocalAppData directory is required for standard mode.", nameof(localAppDataDirectory));

            return Path.Combine(localAppDataDirectory, AppFolderName);
        }

        /// <summary>
        /// Verifies that the target folder can be created and written to.
        /// </summary>
        internal static bool TryEnsureFolderWritable(string folderPath, out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                error = "Storage folder path is empty.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(folderPath);

                var probeFile = Path.Combine(folderPath, $".write-probe-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probeFile, "probe");
                File.Delete(probeFile);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Validates writable app storage requirements for startup.
        /// Portable mode requires executable-directory write access.
        /// </summary>
        public static bool ValidateStartupStorageWritable(out string? error)
        {
            error = null;

            if (!IsPortableBuild)
                return true;

            var folder = GetAppFolder();
            if (TryEnsureFolderWritable(folder, out var details))
                return true;

            error = $"Portable mode requires write access to '{folder}'. Move SSH_Helper_Portable.exe to a writable folder and try again. Details: {details}";
            return false;
        }
    }
}
