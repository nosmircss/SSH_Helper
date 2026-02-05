namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Utility methods for working with nested folder paths using forward-slash separator.
    /// </summary>
    public static class FolderPathUtility
    {
        /// <summary>
        /// Path separator for nested folders.
        /// </summary>
        public const char Separator = '/';

        /// <summary>
        /// Gets the parent path of a folder path.
        /// </summary>
        /// <param name="path">The folder path (e.g., "A/B/C").</param>
        /// <returns>The parent path (e.g., "A/B"), or null if path is a root-level folder.</returns>
        public static string? GetParentPath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var lastSeparator = path.LastIndexOf(Separator);
            return lastSeparator > 0 ? path[..lastSeparator] : null;
        }

        /// <summary>
        /// Gets the folder name (last segment) from a path.
        /// </summary>
        /// <param name="path">The folder path (e.g., "A/B/C").</param>
        /// <returns>The folder name (e.g., "C"), or the path itself if no separator.</returns>
        public static string GetFolderName(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            var lastSeparator = path.LastIndexOf(Separator);
            return lastSeparator >= 0 ? path[(lastSeparator + 1)..] : path;
        }

        /// <summary>
        /// Splits a path into its segments.
        /// </summary>
        /// <param name="path">The folder path (e.g., "A/B/C").</param>
        /// <returns>Array of segments (e.g., ["A", "B", "C"]).</returns>
        public static string[] GetPathSegments(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return Array.Empty<string>();

            return path.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Gets all ancestor paths for a given path, from shallowest to deepest.
        /// </summary>
        /// <param name="path">The folder path (e.g., "A/B/C").</param>
        /// <returns>Ancestor paths in order (e.g., ["A", "A/B"] for "A/B/C").</returns>
        public static IEnumerable<string> GetAncestorPaths(string? path)
        {
            if (string.IsNullOrEmpty(path))
                yield break;

            var segments = path.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < segments.Length; i++)
            {
                yield return string.Join(Separator, segments.Take(i));
            }
        }

        /// <summary>
        /// Gets all paths in the hierarchy including ancestors and the path itself.
        /// </summary>
        /// <param name="path">The folder path (e.g., "A/B/C").</param>
        /// <returns>All paths including self (e.g., ["A", "A/B", "A/B/C"] for "A/B/C").</returns>
        public static IEnumerable<string> GetAllPathsInHierarchy(string? path)
        {
            if (string.IsNullOrEmpty(path))
                yield break;

            var segments = path.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i <= segments.Length; i++)
            {
                yield return string.Join(Separator, segments.Take(i));
            }
        }

        /// <summary>
        /// Checks if a path is a descendant of another path.
        /// </summary>
        /// <param name="path">The potential descendant path.</param>
        /// <param name="ancestorPath">The potential ancestor path.</param>
        /// <returns>True if path is a descendant of ancestorPath.</returns>
        public static bool IsDescendantOf(string? path, string? ancestorPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(ancestorPath))
                return false;

            return path.StartsWith(ancestorPath + Separator, StringComparison.Ordinal);
        }

        /// <summary>
        /// Checks if a path is an immediate child of another path.
        /// </summary>
        /// <param name="path">The potential child path.</param>
        /// <param name="parentPath">The potential parent path.</param>
        /// <returns>True if path is an immediate child of parentPath.</returns>
        public static bool IsImmediateChildOf(string? path, string? parentPath)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (string.IsNullOrEmpty(parentPath))
            {
                // Check if path is a root-level folder (no separator)
                return !path.Contains(Separator);
            }

            // Must start with parent path + separator
            if (!path.StartsWith(parentPath + Separator, StringComparison.Ordinal))
                return false;

            // The remaining part after parent + separator should have no more separators
            var remainder = path[(parentPath.Length + 1)..];
            return !remainder.Contains(Separator);
        }

        /// <summary>
        /// Combines a parent path with a child name to form a full path.
        /// </summary>
        /// <param name="parentPath">The parent path (can be null or empty for root).</param>
        /// <param name="childName">The child folder name.</param>
        /// <returns>The combined path.</returns>
        public static string CombinePath(string? parentPath, string childName)
        {
            if (string.IsNullOrEmpty(parentPath))
                return childName;

            return parentPath + Separator + childName;
        }

        /// <summary>
        /// Renames a path by replacing the old prefix with a new prefix.
        /// Used when renaming parent folders.
        /// </summary>
        /// <param name="path">The path to update.</param>
        /// <param name="oldPrefix">The old prefix to replace.</param>
        /// <param name="newPrefix">The new prefix.</param>
        /// <returns>The updated path.</returns>
        public static string RenamePath(string path, string oldPrefix, string newPrefix)
        {
            if (path == oldPrefix)
                return newPrefix;

            if (path.StartsWith(oldPrefix + Separator, StringComparison.Ordinal))
                return newPrefix + path[oldPrefix.Length..];

            return path;
        }

        /// <summary>
        /// Gets the depth of a path (number of segments).
        /// </summary>
        /// <param name="path">The folder path.</param>
        /// <returns>The depth (1 for root folders, 2 for first-level nested, etc.).</returns>
        public static int GetDepth(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;

            return path.Count(c => c == Separator) + 1;
        }
    }
}
