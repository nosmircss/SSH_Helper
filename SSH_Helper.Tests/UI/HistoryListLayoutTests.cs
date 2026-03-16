using System.Drawing;
using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class HistoryListLayoutTests
{
    [WinFormsFact]
    public void CalculateItemHeight_ShortLabel_UsesMinimumHeight()
    {
        using var font = new Font("Segoe UI", 9f);

        var height = HistoryListLayout.CalculateItemHeight(
            "2026-03-13 09:55:59 - QA WriteFile JSON [Windows]",
            font,
            clientWidth: 360);

        height.Should().Be(HistoryListLayout.GetMinimumItemHeight(font));
    }

    [WinFormsFact]
    public void CalculateItemHeight_WrappedLabel_GrowsBeyondMinimumHeight()
    {
        using var font = new Font("Segoe UI", 9f);

        var height = HistoryListLayout.CalculateItemHeight(
            "2026-03-13 09:55:59 - QA WriteFile JSON [Windows] with a long descriptive suffix that should wrap to another line",
            font,
            clientWidth: 150);

        height.Should().BeGreaterThan(HistoryListLayout.GetMinimumItemHeight(font));
        height.Should().BeLessThanOrEqualTo(HistoryListLayout.GetMaximumItemHeight(font));
    }

    [WinFormsFact]
    public void CalculateItemHeight_VeryLongLabel_CapsAtThreeLines()
    {
        using var font = new Font("Segoe UI", 9f);
        var veryLongLabel = string.Join(" ", Enumerable.Repeat(
            "2026-03-13 09:55:59 - QA WriteFile JSON [Windows] with extremely long wrapped history content",
            6));

        var height = HistoryListLayout.CalculateItemHeight(
            veryLongLabel,
            font,
            clientWidth: 130);

        height.Should().Be(HistoryListLayout.GetMaximumItemHeight(font));
    }
}
