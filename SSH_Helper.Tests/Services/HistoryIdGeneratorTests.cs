using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class HistoryIdGeneratorTests
{
    [Fact]
    public void NewId_GeneratesUniqueId()
    {
        var first = HistoryIdGenerator.NewId();
        var second = HistoryIdGenerator.NewId();

        first.Should().NotBeNullOrWhiteSpace();
        second.Should().NotBeNullOrWhiteSpace();
        first.Should().NotBe(second);
    }

    [Fact]
    public void NewId_UsesCompactGuidFormat()
    {
        var id = HistoryIdGenerator.NewId();

        id.Length.Should().Be(32);
    }
}
