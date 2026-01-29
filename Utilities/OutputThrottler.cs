using System;
using System.Text;
using System.Threading;

namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Buffers output and flushes it on a fixed interval using a target synchronization context.
    /// </summary>
    public sealed class OutputThrottler : IDisposable
    {
        private readonly object _lock = new();
        private readonly StringBuilder _pending = new();
        private readonly SynchronizationContext _syncContext;
        private readonly System.Threading.Timer _timer;
        private readonly Action<string> _flushAction;
        private int _flushQueued;
        private bool _disposed;

        public OutputThrottler(TimeSpan interval, Action<string> flushAction, SynchronizationContext syncContext)
        {
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");

            _flushAction = flushAction ?? throw new ArgumentNullException(nameof(flushAction));
            _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));
            _timer = new System.Threading.Timer(OnTimerTick, null, interval, interval);
        }

        public void Enqueue(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            lock (_lock)
            {
                _pending.Append(text);
            }
        }

        public void Flush()
        {
            ScheduleFlush();
        }

        public void Clear()
        {
            lock (_lock)
            {
                _pending.Clear();
            }
        }

        private void OnTimerTick(object? state)
        {
            ScheduleFlush();
        }

        private void ScheduleFlush()
        {
            if (_disposed)
                return;

            if (Interlocked.Exchange(ref _flushQueued, 1) == 1)
                return;

            _syncContext.Post(_ => FlushOnContext(), null);
        }

        private void FlushOnContext()
        {
            Interlocked.Exchange(ref _flushQueued, 0);

            string? output = null;
            lock (_lock)
            {
                if (_pending.Length == 0)
                    return;

                output = _pending.ToString();
                _pending.Clear();
            }

            if (!string.IsNullOrEmpty(output))
            {
                _flushAction(output);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _timer.Dispose();
        }
    }
}
