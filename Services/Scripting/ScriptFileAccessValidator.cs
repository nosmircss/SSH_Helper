using System;
using System.Collections.Generic;
using System.IO;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Validates file paths for script file operations to prevent path traversal attacks.
    /// </summary>
    public static class ScriptFileAccessValidator
    {
        private static readonly HashSet<string> BlockedPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Windows",
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"C:\ProgramData",
            @"C:\$Recycle.Bin",
            @"C:\System Volume Information",
        };

        private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".sys", ".drv", ".com", ".bat", ".cmd", ".ps1",
            ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".msc", ".msi",
            ".scr", ".pif", ".hta", ".cpl", ".msp", ".gadget"
        };

        /// <summary>
        /// Validates a file path for read operations.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <param name="error">Error message if validation fails.</param>
        /// <returns>True if path is safe to read, false otherwise.</returns>
        public static bool ValidateReadPath(string filePath, out string? error)
        {
            error = null;

            try
            {
                var fullPath = Path.GetFullPath(filePath);

                // Check for blocked system directories
                foreach (var blocked in BlockedPaths)
                {
                    if (fullPath.StartsWith(blocked, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"Access denied: Cannot read from protected system directory '{blocked}'";
                        return false;
                    }
                }

                // Block reading from other users' directories
                var usersDir = @"C:\Users";
                var currentUserDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (fullPath.StartsWith(usersDir, StringComparison.OrdinalIgnoreCase) &&
                    !fullPath.StartsWith(currentUserDir, StringComparison.OrdinalIgnoreCase) &&
                    !fullPath.StartsWith(Path.Combine(usersDir, "Public"), StringComparison.OrdinalIgnoreCase))
                {
                    error = "Access denied: Cannot read from other users' directories";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Invalid path: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Validates a file path for write operations (more restrictive than read).
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <param name="error">Error message if validation fails.</param>
        /// <returns>True if path is safe to write, false otherwise.</returns>
        public static bool ValidateWritePath(string filePath, out string? error)
        {
            error = null;

            try
            {
                var fullPath = Path.GetFullPath(filePath);

                // First check read restrictions (they apply to write too)
                if (!ValidateReadPath(filePath, out error))
                    return false;

                // Block writing executable extensions
                var extension = Path.GetExtension(fullPath);
                if (BlockedExtensions.Contains(extension))
                {
                    error = $"Access denied: Cannot write files with extension '{extension}'";
                    return false;
                }

                // Only allow writing to specific safe locations
                var allowedBasePaths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
                };

                bool isAllowed = false;
                foreach (var basePath in allowedBasePaths)
                {
                    if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                    {
                        isAllowed = true;
                        break;
                    }
                }

                if (!isAllowed)
                {
                    error = "Access denied: Can only write to user directories (Documents, Desktop, AppData, Temp)";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Invalid path: {ex.Message}";
                return false;
            }
        }
    }
}
