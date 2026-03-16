using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class HistoryStartupSelectionHydrationTests
{
    [Fact]
    public void ShouldHydrateSelectedEntry_WhenSelectionExistsAndHandlingIsDisabled_ReturnsTrue()
    {
        var shouldHydrate = HistoryStartupSelectionHydration.ShouldHydrateSelectedEntry(
            historySelectionHandlingEnabled: false,
            hasSelectedEntry: true);

        shouldHydrate.Should().BeTrue();
    }

    [Fact]
    public void ShouldHydrateSelectedEntry_WhenNoSelectionExists_ReturnsFalse()
    {
        var shouldHydrate = HistoryStartupSelectionHydration.ShouldHydrateSelectedEntry(
            historySelectionHandlingEnabled: false,
            hasSelectedEntry: false);

        shouldHydrate.Should().BeFalse();
    }

    [Fact]
    public void ShouldHydrateSelectedEntry_WhenHandlingAlreadyEnabled_ReturnsFalse()
    {
        var shouldHydrate = HistoryStartupSelectionHydration.ShouldHydrateSelectedEntry(
            historySelectionHandlingEnabled: true,
            hasSelectedEntry: true);

        shouldHydrate.Should().BeFalse();
    }
}
