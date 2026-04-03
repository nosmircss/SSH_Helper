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

        public static string FormatCommandSectionTitle(bool isDirty, CanvasLayoutState canvasState = CanvasLayoutState.None)
        {
            var title = isDirty
                ? $"{CommandSectionLabel} (unsaved)"
                : CommandSectionLabel;

            return canvasState switch
            {
                CanvasLayoutState.Saved => $"{title}  \u2022  Layout saved",
                CanvasLayoutState.WillReset => $"{title}  \u2022  \u26A0 Layout will reset",
                _ => title,
            };
        }

        public enum CanvasLayoutState
        {
            None,
            Saved,
            WillReset,
        }

        public static string FormatSaveButtonLabel(bool isDirty)
        {
            return isDirty
                ? $"{SaveButtonLabel}*"
                : SaveButtonLabel;
        }
    }
}
