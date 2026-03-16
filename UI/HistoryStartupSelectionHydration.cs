namespace SSH_Helper.UI;

internal static class HistoryStartupSelectionHydration
{
    internal static bool ShouldHydrateSelectedEntry(bool historySelectionHandlingEnabled, bool hasSelectedEntry)
    {
        return !historySelectionHandlingEnabled && hasSelectedEntry;
    }
}
