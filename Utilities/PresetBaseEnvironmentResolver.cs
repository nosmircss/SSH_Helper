using SSH_Helper.Models;

namespace SSH_Helper.Utilities
{
    internal enum PresetBaseEnvironmentSourceKind
    {
        GlobalBase,
        FolderBase
    }

    internal readonly record struct PresetBaseEnvironmentResolution(
        string EnvironmentName,
        PresetBaseEnvironmentSourceKind SourceKind,
        string? SourceFolderPath);

    internal static class PresetBaseEnvironmentResolver
    {
        public static PresetBaseEnvironmentResolution Resolve(
            string globalBaseEnvironment,
            string? folderPath,
            IReadOnlyDictionary<string, FolderInfo> folders)
        {
            if (string.IsNullOrWhiteSpace(globalBaseEnvironment))
                throw new ArgumentException("Global base environment is required.", nameof(globalBaseEnvironment));

            var currentPath = string.IsNullOrWhiteSpace(folderPath) ? null : folderPath.Trim();
            while (!string.IsNullOrWhiteSpace(currentPath))
            {
                if (folders.TryGetValue(currentPath, out var folderInfo) &&
                    !string.IsNullOrWhiteSpace(folderInfo.BaseEnvironment))
                {
                    return new PresetBaseEnvironmentResolution(
                        folderInfo.BaseEnvironment.Trim(),
                        PresetBaseEnvironmentSourceKind.FolderBase,
                        currentPath);
                }

                currentPath = FolderPathUtility.GetParentPath(currentPath);
            }

            return new PresetBaseEnvironmentResolution(
                globalBaseEnvironment.Trim(),
                PresetBaseEnvironmentSourceKind.GlobalBase,
                null);
        }
    }
}
