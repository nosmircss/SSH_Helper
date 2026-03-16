namespace SSH_Helper.UI;

internal static class PresetDeletionSelectionResolver
{
    internal static string? GetAdjacentPresetName(IReadOnlyList<PresetNodeTag> displayOrderedNodes, string deletedPresetName)
    {
        if (displayOrderedNodes == null || string.IsNullOrWhiteSpace(deletedPresetName))
        {
            return null;
        }

        var visiblePresetNames = displayOrderedNodes
            .Where(node => !node.IsFolder)
            .Select(node => node.Name)
            .ToList();

        var deletedIndex = visiblePresetNames.FindIndex(name =>
            string.Equals(name, deletedPresetName, StringComparison.Ordinal));

        if (deletedIndex < 0)
        {
            return null;
        }

        if (deletedIndex > 0)
        {
            return visiblePresetNames[deletedIndex - 1];
        }

        return deletedIndex + 1 < visiblePresetNames.Count
            ? visiblePresetNames[deletedIndex + 1]
            : null;
    }
}
