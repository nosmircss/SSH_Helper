using System.Text;

namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Shared utility for atomic JSON file writes with optional backup.
    /// Uses temp-file + File.Replace pattern to prevent partial writes on crash.
    /// </summary>
    public static class JsonFileWriter
    {
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        /// <summary>
        /// Atomically writes JSON content to a file using a temp-file swap pattern.
        /// If <paramref name="createBackup"/> is true, the previous file is retained as {path}.bak.
        /// </summary>
        /// <param name="path">Target file path.</param>
        /// <param name="json">JSON content to write.</param>
        /// <param name="createBackup">Whether to keep a .bak copy of the previous file.</param>
        public static void WriteJsonAtomic(string path, string json, bool createBackup)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = path + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json, Utf8NoBom);

                if (File.Exists(path))
                {
                    if (createBackup)
                    {
                        try
                        {
                            File.Replace(tempPath, path, path + ".bak", ignoreMetadataErrors: true);
                            return;
                        }
                        catch
                        {
                            // Fall back to copy + move path replacement.
                            try
                            {
                                File.Copy(path, path + ".bak", overwrite: true);
                            }
                            catch
                            {
                                // Best effort backup.
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                            return;
                        }
                        catch
                        {
                            // Fall back to delete + move below.
                        }
                    }

                    File.Delete(path);
                }

                File.Move(tempPath, path);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // Best effort cleanup.
                    }
                }
            }
        }
    }
}
