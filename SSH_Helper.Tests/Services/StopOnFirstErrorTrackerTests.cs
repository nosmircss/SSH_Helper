using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class StopOnFirstErrorTrackerTests
{
    [Fact]
    public void TrySignalError_FirstCallWins()
    {
        var tracker = new StopOnFirstErrorTracker();

        tracker.TrySignalError().Should().BeTrue();
        tracker.TrySignalError().Should().BeFalse();
        tracker.HasError.Should().BeTrue();
    }

    [Fact]
    public async Task TrySignalError_OnlyOneWinnerAcrossTasks()
    {
        var tracker = new StopOnFirstErrorTracker();
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => tracker.TrySignalError()))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Count(r => r).Should().Be(1);
        tracker.HasError.Should().BeTrue();
    }
}
