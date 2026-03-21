using System.Diagnostics;
using System.Windows.Forms;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.UI;

namespace SSH_Helper.Services.Scripting
{
    internal enum BrowserCallbackUiMode
    {
        External,
        WebView2
    }

    internal sealed class BrowserCallbackUiLaunchRequest
    {
        public BrowserCallbackUiLaunchRequest(
            BrowserCallbackUiMode mode,
            string startUrl,
            bool darkMode,
            int showAfterSeconds = 0,
            bool keepWindowOpenOnSuccess = false)
        {
            Mode = mode;
            StartUrl = startUrl ?? throw new ArgumentNullException(nameof(startUrl));
            DarkMode = darkMode;
            ShowAfterSeconds = Math.Max(0, showAfterSeconds);
            KeepWindowOpenOnSuccess = keepWindowOpenOnSuccess;
        }

        public BrowserCallbackUiMode Mode { get; }

        public string StartUrl { get; }

        public bool DarkMode { get; }

        public int ShowAfterSeconds { get; }

        public bool KeepWindowOpenOnSuccess { get; }
    }

    internal interface IBrowserCallbackOwnedWindow
    {
    }

    internal interface IBrowserCallbackUiSession : IAsyncDisposable
    {
        BrowserCallbackUiMode Mode { get; }

        Task ClosedByUser { get; }

        bool WasShownToUser { get; }

        ValueTask MarkCompletedAsync();

        ValueTask CloseAsync();
    }

    internal interface IBrowserCallbackUiHost
    {
        Task<IBrowserCallbackUiSession> LaunchAsync(BrowserCallbackUiLaunchRequest request, CancellationToken cancellationToken);
    }

    internal interface IBrowserCallbackWebViewDialogAdapter : IDisposable
    {
        event FormClosedEventHandler? FormClosed;

        bool IsDisposed { get; }

        bool Visible { get; }

        Task InitializeAsync(CancellationToken cancellationToken);

        void SetCompletionState();

        void Show(IWin32Window owner);

        void ShowDialog(IWin32Window owner);

        void BringToFront();

        void Activate();

        void Close();
    }

    internal interface IBrowserCallbackWebViewDialogFactory
    {
        IBrowserCallbackWebViewDialogAdapter Create(string startUrl, string userDataDirectory, bool darkMode);
    }

    internal sealed class BrowserCallbackUiHost : IBrowserCallbackUiHost
    {
        private readonly IBrowserCallbackWebViewProfileManager _profileManager;
        private readonly IBrowserCallbackWebViewDialogFactory _dialogFactory;

        internal BrowserCallbackUiHost(IBrowserCallbackWebViewProfileManager profileManager)
            : this(profileManager, new BrowserCallbackWebViewDialogFactory())
        {
        }

        internal BrowserCallbackUiHost(
            IBrowserCallbackWebViewProfileManager profileManager,
            IBrowserCallbackWebViewDialogFactory dialogFactory)
        {
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
            _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
        }

        public async Task<IBrowserCallbackUiSession> LaunchAsync(BrowserCallbackUiLaunchRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            switch (request.Mode)
            {
                case BrowserCallbackUiMode.External:
                    LaunchExternalBrowser(request.StartUrl);
                    return new ExternalBrowserCallbackUiSession();

                case BrowserCallbackUiMode.WebView2:
                    var owner = ResolveOwnerForm();
                    if (owner == null)
                    {
                        throw new InvalidOperationException("browser_callback_capture webview2 mode requires an active application window");
                    }

                    var session = new BrowserCallbackWebViewDialogSession(owner, request, _profileManager, _dialogFactory);
                    await session.StartAsync(cancellationToken).ConfigureAwait(false);
                    return session;

                default:
                    throw new InvalidOperationException($"Unsupported browser callback UI mode: {request.Mode}");
            }
        }

        private static void LaunchExternalBrowser(string startUrl)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = startUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("browser_callback_capture could not launch default browser", ex);
            }
        }

        private static Form? ResolveOwnerForm()
        {
            if (IsEligibleOwnerForm(Form.ActiveForm))
            {
                return Form.ActiveForm;
            }

            foreach (Form form in Application.OpenForms)
            {
                if (IsEligibleOwnerForm(form))
                {
                    return form;
                }
            }

            foreach (Form form in Application.OpenForms)
            {
                if (!form.IsDisposed)
                {
                    return form;
                }
            }

            return null;
        }

        private static bool IsEligibleOwnerForm(Form? form)
        {
            return form != null &&
                !form.IsDisposed &&
                form.Visible &&
                form is not IBrowserCallbackOwnedWindow;
        }

        private sealed class ExternalBrowserCallbackUiSession : IBrowserCallbackUiSession
        {
            private static readonly Task NeverClosedTask = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously).Task;

            public BrowserCallbackUiMode Mode => BrowserCallbackUiMode.External;

            public Task ClosedByUser => NeverClosedTask;

            public bool WasShownToUser => false;

            public ValueTask MarkCompletedAsync()
            {
                return ValueTask.CompletedTask;
            }

            public ValueTask CloseAsync()
            {
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }

        private sealed class BrowserCallbackWebViewDialogSession : IBrowserCallbackUiSession
        {
            private readonly Form _owner;
            private readonly BrowserCallbackUiLaunchRequest _request;
            private readonly IBrowserCallbackWebViewProfileManager _profileManager;
            private readonly IBrowserCallbackWebViewDialogFactory _dialogFactory;
            private readonly TaskCompletionSource<bool> _closedByUserTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _dialogClosedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly CancellationTokenSource _sessionLifetimeCts = new();
            private IBrowserCallbackWebViewDialogAdapter? _dialog;
            private IDisposable? _activeSessionRegistration;
            private int _closeRequested;
            private int _showQueued;
            private int _showStarted;
            private int _dialogPresented;
            private int _completed;

            public BrowserCallbackWebViewDialogSession(
                Form owner,
                BrowserCallbackUiLaunchRequest request,
                IBrowserCallbackWebViewProfileManager profileManager,
                IBrowserCallbackWebViewDialogFactory dialogFactory)
            {
                _owner = owner;
                _request = request;
                _profileManager = profileManager;
                _dialogFactory = dialogFactory;
            }

            public BrowserCallbackUiMode Mode => BrowserCallbackUiMode.WebView2;

            public Task ClosedByUser => _closedByUserTcs.Task;

            public bool WasShownToUser => Interlocked.CompareExchange(ref _dialogPresented, 0, 0) != 0;

            public async ValueTask MarkCompletedAsync()
            {
                Interlocked.Exchange(ref _completed, 1);

                if (_dialogClosedTcs.Task.IsCompleted)
                {
                    return;
                }

                var dialog = _dialog;
                if (dialog == null || dialog.IsDisposed)
                {
                    return;
                }

                await InvokeOnUiThreadAsync(_owner, () =>
                {
                    if (_dialog == null || _dialog.IsDisposed || _dialogClosedTcs.Task.IsCompleted)
                    {
                        return Task.CompletedTask;
                    }

                    _dialog.SetCompletionState();
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                if (_owner.IsDisposed)
                {
                    throw new InvalidOperationException("browser_callback_capture webview2 mode requires an active application window");
                }

                await InvokeOnUiThreadAsync(_owner, async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    _activeSessionRegistration = _profileManager.RegisterActiveSession();
                    _dialog = _dialogFactory.Create(
                        _request.StartUrl,
                        _profileManager.UserDataDirectory,
                        _request.DarkMode);
                    _dialog.FormClosed += Dialog_FormClosed;

                    try
                    {
                        await _dialog.InitializeAsync(cancellationToken).ConfigureAwait(true);
                    }
                    catch
                    {
                        CleanupDialogState();
                        throw;
                    }

                    QueueDialogShow(cancellationToken);
                }).ConfigureAwait(false);
            }

            public async ValueTask CloseAsync()
            {
                if (_dialogClosedTcs.Task.IsCompleted)
                {
                    return;
                }

                var dialog = _dialog;
                if (dialog == null)
                {
                    return;
                }

                if (Interlocked.Exchange(ref _closeRequested, 1) == 0)
                {
                    _sessionLifetimeCts.Cancel();

                    await InvokeOnUiThreadAsync(_owner, () =>
                    {
                        if (_dialogClosedTcs.Task.IsCompleted)
                        {
                            return Task.CompletedTask;
                        }

                        if (dialog.IsDisposed)
                        {
                            return Task.CompletedTask;
                        }

                        if (Interlocked.CompareExchange(ref _showStarted, 0, 0) == 0 && !dialog.Visible)
                        {
                            CleanupDialogState();
                            _dialogClosedTcs.TrySetResult(true);
                            return Task.CompletedTask;
                        }

                        dialog.Close();
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                }

                await _dialogClosedTcs.Task.ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync()
            {
                await CloseAsync().ConfigureAwait(false);
            }

            private void Dialog_FormClosed(object? sender, FormClosedEventArgs e)
            {
                if (Interlocked.CompareExchange(ref _closeRequested, 0, 0) == 0 &&
                    Interlocked.CompareExchange(ref _completed, 0, 0) == 0)
                {
                    _closedByUserTcs.TrySetResult(true);
                }

                CleanupDialogState();
                RestoreOwnerFocusAfterModelessClose();
                _dialogClosedTcs.TrySetResult(true);
            }

            private void CleanupDialogState()
            {
                _sessionLifetimeCts.Cancel();

                if (_dialog != null)
                {
                    _dialog.FormClosed -= Dialog_FormClosed;
                    _dialog.Dispose();
                    _dialog = null;
                }

                _activeSessionRegistration?.Dispose();
                _activeSessionRegistration = null;
            }

            private void QueueDialogShow(CancellationToken cancellationToken)
            {
                if (_request.ShowAfterSeconds <= 0)
                {
                    BeginShowDialog();
                    return;
                }

                if (Interlocked.Exchange(ref _showQueued, 1) != 0)
                {
                    return;
                }

                _ = DelayAndShowDialogAsync(cancellationToken);
            }

            private async Task DelayAndShowDialogAsync(CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sessionLifetimeCts.Token);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_request.ShowAfterSeconds), linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                BeginShowDialog();
            }

            private void BeginShowDialog()
            {
                if (_owner.IsDisposed || _dialogClosedTcs.Task.IsCompleted)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref _closeRequested, 0, 0) != 0)
                {
                    return;
                }

                if (Interlocked.Exchange(ref _showStarted, 1) != 0)
                {
                    return;
                }

                _owner.BeginInvoke((Action)(() =>
                {
                    if (_dialog == null || _dialog.IsDisposed || _dialogClosedTcs.Task.IsCompleted)
                    {
                        _dialogClosedTcs.TrySetResult(true);
                        return;
                    }

                    if (Interlocked.CompareExchange(ref _closeRequested, 0, 0) != 0)
                    {
                        return;
                    }

                    Interlocked.Exchange(ref _dialogPresented, 1);
                    if (_request.KeepWindowOpenOnSuccess)
                    {
                        _dialog.Show(_owner);
                        _dialog.BringToFront();
                        _dialog.Activate();
                        return;
                    }

                    _dialog.ShowDialog(_owner);
                }));
            }

            private void RestoreOwnerFocusAfterModelessClose()
            {
                if (!_request.KeepWindowOpenOnSuccess || _owner.IsDisposed || !_owner.Visible)
                {
                    return;
                }

                BrowserCallbackFocusRestorer.ScheduleUiActivationAttempts(_owner);
            }

            private static Task InvokeOnUiThreadAsync(Control control, Func<Task> callback)
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                void Begin()
                {
                    _ = RunAsync();

                    async Task RunAsync()
                    {
                        try
                        {
                            await callback().ConfigureAwait(true);
                            tcs.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    }
                }

                if (control.IsDisposed)
                {
                    tcs.TrySetException(new ObjectDisposedException(control.Name));
                    return tcs.Task;
                }

                if (control.InvokeRequired)
                {
                    control.BeginInvoke((Action)Begin);
                }
                else
                {
                    Begin();
                }

                return tcs.Task;
            }
        }

        private sealed class BrowserCallbackWebViewDialogFactory : IBrowserCallbackWebViewDialogFactory
        {
            public IBrowserCallbackWebViewDialogAdapter Create(string startUrl, string userDataDirectory, bool darkMode)
            {
                return new BrowserCallbackWebViewDialogAdapter(
                    new BrowserCallbackWebViewDialog(startUrl, userDataDirectory, darkMode));
            }
        }

        private sealed class BrowserCallbackWebViewDialogAdapter : IBrowserCallbackWebViewDialogAdapter
        {
            private readonly BrowserCallbackWebViewDialog _dialog;

            public BrowserCallbackWebViewDialogAdapter(BrowserCallbackWebViewDialog dialog)
            {
                _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            }

            public event FormClosedEventHandler? FormClosed
            {
                add => _dialog.FormClosed += value;
                remove => _dialog.FormClosed -= value;
            }

            public bool IsDisposed => _dialog.IsDisposed;

            public bool Visible => _dialog.Visible;

            public Task InitializeAsync(CancellationToken cancellationToken) => _dialog.InitializeAsync(cancellationToken);

            public void SetCompletionState() => _dialog.SetCompletionState();

            public void Show(IWin32Window owner) => _dialog.Show(owner);

            public void ShowDialog(IWin32Window owner) => _dialog.ShowDialog(owner);

            public void BringToFront() => _dialog.BringToFront();

            public void Activate() => _dialog.Activate();

            public void Close() => _dialog.Close();

            public void Dispose() => _dialog.Dispose();
        }
    }
}
