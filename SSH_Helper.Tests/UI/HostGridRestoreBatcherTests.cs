using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class HostGridRestoreBatcherTests
{
    [Fact]
    public void RestoreScope_CollapsesRepeatedRequestsIntoSingleFlush()
    {
        int scrollbarRefreshes = 0;
        int hostCountRefreshes = 0;
        int dirtyMarks = 0;

        var batcher = new HostGridRestoreBatcher(
            onScrollbarRefresh: () => scrollbarRefreshes++,
            onHostCountRefresh: () => hostCountRefreshes++,
            onMarkDirty: () => dirtyMarks++);

        using (batcher.BeginRestoreScope())
        {
            batcher.RequestScrollbarRefresh();
            batcher.RequestScrollbarRefresh();
            batcher.RequestHostCountRefresh();
            batcher.RequestMarkDirty();
            batcher.RequestMarkDirty();
        }

        Assert.Equal(1, scrollbarRefreshes);
        Assert.Equal(1, hostCountRefreshes);
        Assert.Equal(1, dirtyMarks);
    }

    [Fact]
    public void RestoreScope_NestedScopesFlushOnlyAfterOutermostExit()
    {
        int scrollbarRefreshes = 0;
        int hostCountRefreshes = 0;
        int dirtyMarks = 0;

        var batcher = new HostGridRestoreBatcher(
            onScrollbarRefresh: () => scrollbarRefreshes++,
            onHostCountRefresh: () => hostCountRefreshes++,
            onMarkDirty: () => dirtyMarks++);

        using (batcher.BeginRestoreScope())
        {
            batcher.RequestScrollbarRefresh();

            using (batcher.BeginRestoreScope())
            {
                batcher.RequestHostCountRefresh();
                batcher.RequestMarkDirty();
            }

            Assert.Equal(0, scrollbarRefreshes);
            Assert.Equal(0, hostCountRefreshes);
            Assert.Equal(0, dirtyMarks);
        }

        Assert.Equal(1, scrollbarRefreshes);
        Assert.Equal(1, hostCountRefreshes);
        Assert.Equal(1, dirtyMarks);
    }
}
