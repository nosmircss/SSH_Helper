using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using Xunit;

namespace SSH_Helper.Tests.Services.Scripting;

[Collection(SSH_Helper.Tests.UI.CallbackUiSerialCollection.Name)]
public sealed class BrowserCallbackUiHostTests
{
    [WinFormsFact]
    public async Task LaunchAsync_WebView2KeepOpenRequest_ShowsDialogModeless()
    {
        using var owner = new Form();
        try
        {
            owner.Show();
            owner.Activate();
            Application.DoEvents();

            var profileManager = new RecordingProfileManager();
            var dialogFactory = new RecordingDialogFactory();
            var host = new BrowserCallbackUiHost(profileManager, dialogFactory);

            var session = await host.LaunchAsync(
                new BrowserCallbackUiLaunchRequest(
                    BrowserCallbackUiMode.WebView2,
                    "https://example.com/start",
                    darkMode: false,
                    showAfterSeconds: 0,
                    keepWindowOpenOnSuccess: true),
                CancellationToken.None);

            WaitForUi(() => dialogFactory.LastDialog?.ShowCallCount == 1 || dialogFactory.LastDialog?.ShowDialogCallCount == 1);

            dialogFactory.LastDialog.Should().NotBeNull();
            dialogFactory.LastDialog!.ShowCallCount.Should().Be(1);
            dialogFactory.LastDialog.ShowDialogCallCount.Should().Be(0);
            dialogFactory.LastDialog.LastOwner.Should().NotBeNull();
            dialogFactory.LastDialog.LastOwner.Should().BeAssignableTo<Form>();

            await session.DisposeAsync();
            Application.DoEvents();
        }
        finally
        {
            CloseShownForms(owner);
        }
    }

    [WinFormsFact]
    public async Task LaunchAsync_WebView2KeepOpenRequest_LeavesOwnerEnabled()
    {
        using var owner = new Form();
        try
        {
            owner.Show();
            owner.Activate();
            Application.DoEvents();

            var profileManager = new RecordingProfileManager();
            var dialogFactory = new RecordingDialogFactory();
            var host = new BrowserCallbackUiHost(profileManager, dialogFactory);

            var session = await host.LaunchAsync(
                new BrowserCallbackUiLaunchRequest(
                    BrowserCallbackUiMode.WebView2,
                    "https://example.com/start",
                    darkMode: false,
                    showAfterSeconds: 0,
                    keepWindowOpenOnSuccess: true),
                CancellationToken.None);

            WaitForUi(() => dialogFactory.LastDialog?.ShowCallCount == 1);

            owner.Enabled.Should().BeTrue("keep-open callback launch should not force a whole-form disabled repaint");

            await session.DisposeAsync();
            Application.DoEvents();
        }
        finally
        {
            CloseShownForms(owner);
        }
    }

    [WinFormsFact]
    public async Task LaunchAsync_WebView2_IgnoresActiveBrowserCallbackWindow_WhenSelectingOwner()
    {
        using var mainForm = new Form();
        using var callbackWindow = new FakeBrowserCallbackOwnerWindow();
        try
        {
            mainForm.Show();
            callbackWindow.Show(mainForm);
            callbackWindow.Activate();
            Application.DoEvents();

            var profileManager = new RecordingProfileManager();
            var dialogFactory = new RecordingDialogFactory();
            var host = new BrowserCallbackUiHost(profileManager, dialogFactory);

            var session = await host.LaunchAsync(
                new BrowserCallbackUiLaunchRequest(
                    BrowserCallbackUiMode.WebView2,
                    "https://example.com/start",
                    darkMode: false,
                    showAfterSeconds: 0,
                    keepWindowOpenOnSuccess: true),
                CancellationToken.None);

            WaitForUi(() => dialogFactory.LastDialog?.LastOwner != null);

            dialogFactory.LastDialog.Should().NotBeNull();
            dialogFactory.LastDialog!.LastOwner.Should().BeSameAs(mainForm);

            await session.DisposeAsync();
            Application.DoEvents();
        }
        finally
        {
            CloseShownForms(callbackWindow, mainForm);
        }
    }

    [WinFormsFact]
    public async Task ClosingKeepOpenBrowserCallbackWindow_RequestsMainFormActivation()
    {
        using var mainForm = new Form();
        try
        {
            mainForm.Show();
            mainForm.Activate();
            Application.DoEvents();

            var restoreProperty = typeof(BrowserCallbackFocusRestorer).GetProperty(
                "ScheduleUiActivationAttemptsOverrideForTests",
                BindingFlags.NonPublic | BindingFlags.Static);
            restoreProperty.Should().NotBeNull("the modeless callback close path should be able to override focus restoration in tests");
            if (restoreProperty == null)
            {
                return;
            }

            var originalOverride = restoreProperty.GetValue(null);
            var restoredForms = new List<Form>();

            try
            {
                restoreProperty.SetValue(null, (Action<Form>)(form => restoredForms.Add(form)));

                var profileManager = new RecordingProfileManager();
                var dialogFactory = new RecordingDialogFactory();
                var host = new BrowserCallbackUiHost(profileManager, dialogFactory);

                var sessionOne = await host.LaunchAsync(
                    new BrowserCallbackUiLaunchRequest(
                        BrowserCallbackUiMode.WebView2,
                        "https://example.com/start/one",
                        darkMode: false,
                        showAfterSeconds: 0,
                        keepWindowOpenOnSuccess: true),
                    CancellationToken.None);

                WaitForUi(() => dialogFactory.CreatedDialogs.Count >= 1 && dialogFactory.CreatedDialogs[0].ShowCallCount == 1);

                var sessionTwo = await host.LaunchAsync(
                    new BrowserCallbackUiLaunchRequest(
                        BrowserCallbackUiMode.WebView2,
                        "https://example.com/start/two",
                        darkMode: false,
                        showAfterSeconds: 0,
                        keepWindowOpenOnSuccess: true),
                    CancellationToken.None);

                WaitForUi(() => dialogFactory.CreatedDialogs.Count >= 2 && dialogFactory.CreatedDialogs[1].ShowCallCount == 1);

                dialogFactory.CreatedDialogs[0].Close();
                WaitForUi(() => restoredForms.Count > 0);

                restoredForms.Should().ContainSingle();
                restoredForms[0].Should().BeSameAs(mainForm);

                await sessionOne.DisposeAsync();
                await sessionTwo.DisposeAsync();
                Application.DoEvents();
            }
            finally
            {
                restoreProperty.SetValue(null, originalOverride);
            }
        }
        finally
        {
            CloseShownForms(mainForm);
        }
    }

    private sealed class RecordingProfileManager : IBrowserCallbackWebViewProfileManager
    {
        public string UserDataDirectory { get; } = Path.Combine(Path.GetTempPath(), $"BrowserCallbackUiHostTests_{Guid.NewGuid():N}");

        public EmbeddedBrowserDataClearResult ClearEmbeddedBrowserData() => EmbeddedBrowserDataClearResult.Cleared;

        public IDisposable RegisterActiveSession() => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class RecordingDialogFactory : IBrowserCallbackWebViewDialogFactory
    {
        public List<RecordingDialogAdapter> CreatedDialogs { get; } = new();

        public RecordingDialogAdapter? LastDialog { get; private set; }

        public IBrowserCallbackWebViewDialogAdapter Create(string startUrl, string userDataDirectory, bool darkMode)
        {
            LastDialog = new RecordingDialogAdapter();
            CreatedDialogs.Add(LastDialog);
            return LastDialog;
        }
    }

    private sealed class RecordingDialogAdapter : IBrowserCallbackWebViewDialogAdapter
    {
        public event FormClosedEventHandler? FormClosed;

        public bool IsDisposed { get; private set; }

        public bool Visible { get; private set; }

        public int ShowCallCount { get; private set; }

        public int ShowDialogCallCount { get; private set; }

        public IWin32Window? LastOwner { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void SetCompletionState()
        {
        }

        public void Show(IWin32Window owner)
        {
            ShowCallCount++;
            LastOwner = owner;
            Visible = true;
        }

        public void ShowDialog(IWin32Window owner)
        {
            ShowDialogCallCount++;
            LastOwner = owner;
            Visible = true;
        }

        public void BringToFront()
        {
        }

        public void Activate()
        {
        }

        public void Close()
        {
            Visible = false;
            FormClosed?.Invoke(this, new FormClosedEventArgs(CloseReason.UserClosing));
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class FakeBrowserCallbackOwnerWindow : Form, IBrowserCallbackOwnedWindow
    {
    }

    private static void WaitForUi(Func<bool> condition, int timeoutMs = 1000)
    {
        var started = Environment.TickCount64;
        while (!condition())
        {
            Application.DoEvents();
            Thread.Sleep(10);

            if (Environment.TickCount64 - started > timeoutMs)
            {
                break;
            }
        }
    }

    private static void CloseShownForms(params Form[] forms)
    {
        foreach (var form in forms.Reverse())
        {
            if (form.IsDisposed)
            {
                continue;
            }

            if (form.Visible)
            {
                form.Close();
            }
            else
            {
                form.Dispose();
            }
        }

        Application.DoEvents();
    }
}
