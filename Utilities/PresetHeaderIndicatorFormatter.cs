namespace SSH_Helper.Utilities
{
    internal static class PresetHeaderIndicatorFormatter
    {
        private const string DefaultLabel = "Presets";
        private const string CommandSectionLabel = "Commands";
        private const string SaveButtonLabel = "Save";

        public static string Format(string? selectedFolderName, string? presetName, bool isDirty)
        {
            if (!string.IsNullOrWhiteSpace(selectedFolderName))
            {
                return $"Folder: {selectedFolderName}";
            }

            var effectivePresetName = string.IsNullOrWhiteSpace(presetName)
                ? null
                : presetName.Trim();

            if (string.IsNullOrWhiteSpace(effectivePresetName))
            {
                return isDirty
                    ? $"{DefaultLabel} (unsaved)"
                    : DefaultLabel;
            }

            return isDirty
                ? $"Preset: {effectivePresetName} (unsaved)"
                : $"Preset: {effectivePresetName}";
        }

        public static string FormatCommandSectionTitle(bool isDirty)
        {
            return isDirty
                ? $"{CommandSectionLabel} (unsaved)"
                : CommandSectionLabel;
        }

        public static string FormatSaveButtonLabel(bool isDirty)
        {
            return isDirty
                ? $"{SaveButtonLabel}*"
                : SaveButtonLabel;
        }
    }
}
