namespace SSH_Helper.Utilities
{
    internal static class PresetEnvironmentStatusFormatter
    {
        public static string FormatRestoreMessage(string presetName, PresetBaseEnvironmentResolution resolution)
        {
            if (string.IsNullOrWhiteSpace(presetName))
                throw new ArgumentException("Preset name is required.", nameof(presetName));

            if (string.IsNullOrWhiteSpace(resolution.EnvironmentName))
                throw new ArgumentException("Resolved environment name is required.", nameof(resolution));

            var environmentName = resolution.EnvironmentName.Trim();
            var baseLabel = resolution.SourceKind == PresetBaseEnvironmentSourceKind.FolderBase
                ? "folder base environment"
                : "base environment";

            return $"Preset '{presetName.Trim()}' restored {baseLabel} '{environmentName}'.";
        }

        public static string FormatSwitchMessage(string presetName, string targetEnvironment)
        {
            if (string.IsNullOrWhiteSpace(presetName))
                throw new ArgumentException("Preset name is required.", nameof(presetName));

            if (string.IsNullOrWhiteSpace(targetEnvironment))
                throw new ArgumentException("Target environment is required.", nameof(targetEnvironment));

            return $"Preset '{presetName.Trim()}' switched to environment '{targetEnvironment.Trim()}'.";
        }

        public static string FormatMissingEnvironmentMessage(string presetName, string targetEnvironment)
        {
            if (string.IsNullOrWhiteSpace(presetName))
                throw new ArgumentException("Preset name is required.", nameof(presetName));

            if (string.IsNullOrWhiteSpace(targetEnvironment))
                throw new ArgumentException("Target environment is required.", nameof(targetEnvironment));

            return $"Preset '{presetName.Trim()}' requested environment '{targetEnvironment.Trim()}', but it was not found.";
        }
    }
}
