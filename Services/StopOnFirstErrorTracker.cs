using System.Threading;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Thread-safe tracker for stop-on-first-error behavior.
    /// </summary>
    public sealed class StopOnFirstErrorTracker
    {
        private int _errorFlag;

        public bool HasError => Volatile.Read(ref _errorFlag) != 0;

        /// <summary>
        /// Signals an error; returns true only for the first signal.
        /// </summary>
        public bool TrySignalError()
        {
            return Interlocked.Exchange(ref _errorFlag, 1) == 0;
        }
    }
}
