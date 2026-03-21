using SSH_Helper.Utilities;

namespace SSH_Helper.Services.Scripting
{
    internal enum EmbeddedBrowserDataClearResult
    {
        Cleared,
        ActiveSessionBlocked
    }

    internal interface IBrowserCallbackWebViewProfileManager
    {
        string UserDataDirectory { get; }

        IDisposable RegisterActiveSession();

        EmbeddedBrowserDataClearResult ClearEmbeddedBrowserData();
    }

    internal sealed class BrowserCallbackWebViewProfileManager : IBrowserCallbackWebViewProfileManager
    {
        private readonly object _syncRoot = new();
        private int _activeSessionCount;

        internal static BrowserCallbackWebViewProfileManager Shared { get; } =
            new(Path.Combine(AppDataPaths.GetAppFolder(), "WebView2", "BrowserCallback"));

        internal BrowserCallbackWebViewProfileManager(string userDataDirectory)
        {
            UserDataDirectory = userDataDirectory ?? throw new ArgumentNullException(nameof(userDataDirectory));
        }

        public string UserDataDirectory { get; }

        public IDisposable RegisterActiveSession()
        {
            lock (_syncRoot)
            {
                Directory.CreateDirectory(UserDataDirectory);
                _activeSessionCount++;
            }

            return new ActiveSessionRegistration(this);
        }

        public EmbeddedBrowserDataClearResult ClearEmbeddedBrowserData()
        {
            lock (_syncRoot)
            {
                if (_activeSessionCount > 0)
                {
                    return EmbeddedBrowserDataClearResult.ActiveSessionBlocked;
                }

                if (Directory.Exists(UserDataDirectory))
                {
                    Directory.Delete(UserDataDirectory, recursive: true);
                }

                return EmbeddedBrowserDataClearResult.Cleared;
            }
        }

        private void ReleaseActiveSession()
        {
            lock (_syncRoot)
            {
                if (_activeSessionCount > 0)
                {
                    _activeSessionCount--;
                }
            }
        }

        private sealed class ActiveSessionRegistration : IDisposable
        {
            private readonly BrowserCallbackWebViewProfileManager _owner;
            private int _disposed;

            public ActiveSessionRegistration(BrowserCallbackWebViewProfileManager owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                _owner.ReleaseActiveSession();
            }
        }
    }
}
