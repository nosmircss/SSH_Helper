namespace SSH_Helper.Utilities
{
    internal static class FolderBaseEnvironmentSummaryFormatter
    {
        public static string FormatSummaryLine(string? explicitBaseEnvironment, PresetBaseEnvironmentResolution resolution)
        {
            if (!string.IsNullOrWhiteSpace(explicitBaseEnvironment))
            {
                return $"  Folder Base Environment: {explicitBaseEnvironment.Trim()}";
            }

            if (resolution.SourceKind == PresetBaseEnvironmentSourceKind.FolderBase &&
                !string.IsNullOrWhiteSpace(resolution.SourceFolderPath))
            {
                return $"  Inherited Folder Base: {resolution.EnvironmentName} (from {resolution.SourceFolderPath})";
            }

            return $"  Base Environment: {resolution.EnvironmentName} (global)";
        }

        public static string FormatInheritChoiceLabel(PresetBaseEnvironmentResolution resolution)
        {
            if (resolution.SourceKind == PresetBaseEnvironmentSourceKind.FolderBase &&
                !string.IsNullOrWhiteSpace(resolution.SourceFolderPath))
            {
                return $"(Inherit: {resolution.EnvironmentName} from {resolution.SourceFolderPath})";
            }

            return $"(Inherit Global: {resolution.EnvironmentName})";
        }
    }
}
