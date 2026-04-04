namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Coordinates single-owner scheduler execution across multiple SSH Helper instances.
    /// Uses a named local-session mutex to allow exactly one active scheduler owner.
    /// </summary>
    internal sealed class SchedulerInstanceLock : IDisposable
    {
        internal const string DefaultMutexName = @"Local\SSH_Helper_Scheduler_v1";
        private static readonly object InProcessOwnershipGate = new();
        private static readonly HashSet<string> InProcessOwnedMutexNames = new(StringComparer.Ordinal);

        private readonly Mutex _mutex;
        private readonly string _mutexName;
        private bool _disposed;

        public SchedulerInstanceLock(string? mutexName = null)
        {
            var name = string.IsNullOrWhiteSpace(mutexName) ? DefaultMutexName : mutexName!;
            _mutexName = name;
            _mutex = new Mutex(initiallyOwned: false, name);
        }

        public bool IsAcquired { get; private set; }

        public bool TryAcquire()
        {
            ThrowIfDisposed();

            if (IsAcquired)
                return true;

            lock (InProcessOwnershipGate)
            {
                if (InProcessOwnedMutexNames.Contains(_mutexName))
                    return false;

                try
                {
                    IsAcquired = _mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    IsAcquired = true;
                }

                if (IsAcquired)
                {
                    InProcessOwnedMutexNames.Add(_mutexName);
                }

                return IsAcquired;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (IsAcquired)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Best effort; mutex was no longer owned.
                }
                finally
                {
                    lock (InProcessOwnershipGate)
                    {
                        InProcessOwnedMutexNames.Remove(_mutexName);
                    }
                    IsAcquired = false;
                }
            }

            _mutex.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SchedulerInstanceLock));
        }
    }
}
