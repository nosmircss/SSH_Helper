namespace SSH_Helper.UI;

internal sealed class HostGridRestoreBatcher
{
    private readonly Action _onScrollbarRefresh;
    private readonly Action _onHostCountRefresh;
    private readonly Action _onMarkDirty;
    private int _restoreScopeDepth;
    private bool _scrollbarRefreshRequested;
    private bool _hostCountRefreshRequested;
    private bool _markDirtyRequested;

    public HostGridRestoreBatcher(
        Action onScrollbarRefresh,
        Action onHostCountRefresh,
        Action onMarkDirty)
    {
        _onScrollbarRefresh = onScrollbarRefresh ?? throw new ArgumentNullException(nameof(onScrollbarRefresh));
        _onHostCountRefresh = onHostCountRefresh ?? throw new ArgumentNullException(nameof(onHostCountRefresh));
        _onMarkDirty = onMarkDirty ?? throw new ArgumentNullException(nameof(onMarkDirty));
    }

    public IDisposable BeginRestoreScope()
    {
        _restoreScopeDepth++;
        return new RestoreScope(this);
    }

    public void RequestScrollbarRefresh()
    {
        if (_restoreScopeDepth == 0)
        {
            _onScrollbarRefresh();
            return;
        }

        _scrollbarRefreshRequested = true;
    }

    public void RequestHostCountRefresh()
    {
        if (_restoreScopeDepth == 0)
        {
            _onHostCountRefresh();
            return;
        }

        _hostCountRefreshRequested = true;
    }

    public void RequestMarkDirty()
    {
        if (_restoreScopeDepth == 0)
        {
            _onMarkDirty();
            return;
        }

        _markDirtyRequested = true;
    }

    private void EndRestoreScope()
    {
        if (_restoreScopeDepth == 0)
        {
            throw new InvalidOperationException("Restore scope ended without a matching begin.");
        }

        _restoreScopeDepth--;
        if (_restoreScopeDepth != 0)
        {
            return;
        }

        FlushPendingRequests();
    }

    private void FlushPendingRequests()
    {
        if (_markDirtyRequested)
        {
            _markDirtyRequested = false;
            _onMarkDirty();
        }

        if (_hostCountRefreshRequested)
        {
            _hostCountRefreshRequested = false;
            _onHostCountRefresh();
        }

        if (_scrollbarRefreshRequested)
        {
            _scrollbarRefreshRequested = false;
            _onScrollbarRefresh();
        }
    }

    private sealed class RestoreScope : IDisposable
    {
        private HostGridRestoreBatcher? _owner;

        public RestoreScope(HostGridRestoreBatcher owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            var owner = _owner;
            if (owner == null)
            {
                return;
            }

            _owner = null;
            owner.EndRestoreScope();
        }
    }
}
