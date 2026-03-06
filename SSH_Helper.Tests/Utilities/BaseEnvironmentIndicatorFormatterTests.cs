using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class BaseEnvironmentIndicatorFormatterTests
{
    [Fact]
    public void Format_WhenActiveMatchesBase_HidesIndicator()
    {
        var indicator = BaseEnvironmentIndicatorFormatter.Format("Default", "Default");

        indicator.Visible.Should().BeFalse();
        indicator.Text.Should().Be("Base: Default");
    }

    [Fact]
    public void Format_WhenActiveDiffersFromBase_ShowsIndicator()
    {
        var indicator = BaseEnvironmentIndicatorFormatter.Format("prod", "Default");

        indicator.Visible.Should().BeTrue();
        indicator.Text.Should().Be("Base: Default");
    }
}
