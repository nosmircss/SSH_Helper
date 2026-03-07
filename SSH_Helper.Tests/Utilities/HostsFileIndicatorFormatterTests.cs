using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class HostsFileIndicatorFormatterTests
{
    [Fact]
    public void Format_WhenFilePathIsMissing_ReturnsUnsaved()
    {
        var indicator = HostsFileIndicatorFormatter.Format(null, isDirty: false, CsvFileSyncStatus.NotTracked);

        indicator.Should().Be("Unsaved");
    }

    [Fact]
    public void Format_WhenFilePathExistsAndGridIsClean_ReturnsFileName()
    {
        var indicator = HostsFileIndicatorFormatter.Format(@"C:\temp\hosts.csv", isDirty: false, CsvFileSyncStatus.Current);

        indicator.Should().Be("hosts.csv");
    }

    [Fact]
    public void Format_WhenFilePathExistsAndGridIsDirty_AppendsUnsavedMarker()
    {
        var indicator = HostsFileIndicatorFormatter.Format(@"C:\temp\hosts.csv", isDirty: true, CsvFileSyncStatus.Current);

        indicator.Should().Be("hosts.csv (unsaved)");
    }

    [Fact]
    public void Format_WhenDiskChanged_AppendsDiskChangedMarker()
    {
        var indicator = HostsFileIndicatorFormatter.Format(@"C:\temp\hosts.csv", isDirty: false, CsvFileSyncStatus.ChangedOnDisk);

        indicator.Should().Be("hosts.csv (disk changed)");
    }

    [Fact]
    public void Format_WhenDirtyAndMissingOnDisk_AppendsBothMarkers()
    {
        var indicator = HostsFileIndicatorFormatter.Format(@"C:\temp\hosts.csv", isDirty: true, CsvFileSyncStatus.MissingOnDisk);

        indicator.Should().Be("hosts.csv (unsaved, missing on disk)");
    }
}
