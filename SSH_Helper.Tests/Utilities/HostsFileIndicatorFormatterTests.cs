using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class HostsFileIndicatorFormatterTests
{
    [Fact]
    public void Format_WhenFilePathIsMissing_ReturnsUnsaved()
    {
        var indicator = HostsFileIndicatorFormatter.Format(null, isDirty: false);

        indicator.Should().Be("Unsaved");
    }

    [Fact]
    public void Format_WhenFilePathExistsAndGridIsClean_ReturnsFileName()
    {
        var indicator = HostsFileIndicatorFormatter.Format(@"C:\temp\hosts.csv", isDirty: false);

        indicator.Should().Be("hosts.csv");
    }

    [Fact]
    public void Format_WhenFilePathExistsAndGridIsDirty_AppendsUnsavedMarker()
    {
        var indicator = HostsFileIndicatorFormatter.Format(@"C:\temp\hosts.csv", isDirty: true);

        indicator.Should().Be("hosts.csv (unsaved)");
    }
}
