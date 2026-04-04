using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public sealed class SchedulerInstanceLockTests
{
    [Fact]
    public void TryAcquire_SameNameSecondLock_FailsWhileFirstHeld()
    {
        var lockName = $"Local\\SSH_Helper_Scheduler_Test_{Guid.NewGuid():N}";

        using var first = new SchedulerInstanceLock(lockName);
        using var second = new SchedulerInstanceLock(lockName);

        first.TryAcquire().Should().BeTrue();
        second.TryAcquire().Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_AfterFirstDisposed_SecondCanAcquire()
    {
        var lockName = $"Local\\SSH_Helper_Scheduler_Test_{Guid.NewGuid():N}";
        var second = new SchedulerInstanceLock(lockName);

        using (var first = new SchedulerInstanceLock(lockName))
        {
            first.TryAcquire().Should().BeTrue();
        }

        second.TryAcquire().Should().BeTrue();
        second.Dispose();
    }
}
