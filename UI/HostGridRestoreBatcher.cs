namespace SSH_Helper.UI;

internal sealed class HostGridRestoreBatcher
{
    private readonly Action _onScrollbarRefresh;
    private readonly Action _onHostCountRefresh;
    private readonly Action _onMarkDirty;
    private int _mutationScopeDepth;
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
        return new Scope(this, ScopeKind.Restore);
    }

    public IDisposable BeginMutationScope()
    {
        _mutationScopeDepth++;
        return new Scope(this, ScopeKind.Mutation);
    }

    public void RequestScrollbarRefresh()
    {
        if (!IsRepaintBatchingActive)
        {
            _onScrollbarRefresh();
            return;
        }

        _scrollbarRefreshRequested = true;
    }

    public void RequestHostCountRefresh()
    {
        if (!IsRepaintBatchingActive)
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
        if (IsRepaintBatchingActive)
        {
            return;
        }

        FlushPendingRequests();
    }

    private void EndMutationScope()
    {
        if (_mutationScopeDepth == 0)
        {
            throw new InvalidOperationException("Mutation scope ended without a matching begin.");
        }

        _mutationScopeDepth--;
        if (IsRepaintBatchingActive)
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

    private bool IsRepaintBatchingActive => _restoreScopeDepth != 0 || _mutationScopeDepth != 0;

    private enum ScopeKind
    {
        Restore,
        Mutation
    }

    private sealed class Scope : IDisposable
    {
        private HostGridRestoreBatcher? _owner;
        private readonly ScopeKind _kind;

        public Scope(HostGridRestoreBatcher owner, ScopeKind kind)
        {
            _owner = owner;
            _kind = kind;
        }

        public void Dispose()
        {
            var owner = _owner;
            if (owner == null)
            {
                return;
            }

            _owner = null;
            switch (_kind)
            {
                case ScopeKind.Restore:
                    owner.EndRestoreScope();
                    break;
                case ScopeKind.Mutation:
                    owner.EndMutationScope();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
