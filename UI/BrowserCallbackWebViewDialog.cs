using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using SSH_Helper.Services.Scripting;

namespace SSH_Helper.UI
{
    internal sealed class BrowserCallbackWebViewDialog : Form, IBrowserCallbackOwnedWindow
    {
        private readonly string _startUrl;
        private readonly string _userDataDirectory;
        private readonly bool _darkMode;
        private readonly WebView2 _webView;
        private readonly Label _lblInstructions;
        private readonly Button _btnClose;
        private bool _isCompleted;

        public BrowserCallbackWebViewDialog(string startUrl, string userDataDirectory, bool darkMode)
        {
            _startUrl = startUrl ?? throw new ArgumentNullException(nameof(startUrl));
            _userDataDirectory = userDataDirectory ?? throw new ArgumentNullException(nameof(userDataDirectory));
            _darkMode = darkMode;

            Text = "Browser Callback";
            Size = new Size(1100, 780);
            MinimumSize = new Size(900, 620);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            _lblInstructions = new Label
            {
                Text = "Complete the browser flow below. Close this window to cancel the callback step.",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 4, 0, 0)
            };

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.White
            };

            _btnClose = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                Anchor = AnchorStyles.Right
            };
            _btnClose.Click += (_, _) => Close();

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(12, 8, 12, 0)
            };
            headerPanel.Controls.Add(_lblInstructions);

            var footerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12, 6, 12, 6),
                WrapContents = false
            };
            footerPanel.Controls.Add(_btnClose);

            Controls.Add(_webView);
            Controls.Add(footerPanel);
            Controls.Add(headerPanel);

            ApplyTheme();
        }

        internal void SetCompletionState()
        {
            if (_isCompleted)
            {
                return;
            }

            _isCompleted = true;
            Text = "Browser Callback Complete";
            _lblInstructions.Text = "Browser callback complete. You can review the page below or close this window.";
            _btnClose.Text = "Close";
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(_userDataDirectory);

            if (!IsHandleCreated)
            {
                _ = Handle;
            }

            if (!_webView.IsHandleCreated)
            {
                _webView.CreateControl();
            }

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: _userDataDirectory).ConfigureAwait(true);

            cancellationToken.ThrowIfCancellationRequested();

            await _webView.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            }

            _webView.Source = new Uri(_startUrl, UriKind.Absolute);
        }

        private void ApplyTheme()
        {
            DialogTheme.ApplyTo(this, _darkMode);
            DialogTheme.StyleButton(_btnClose, _darkMode);
            DialogTheme.SetDarkTitleBar(this, _darkMode);

            if (_darkMode)
            {
                _webView.DefaultBackgroundColor = DialogTheme.DarkBackground;
            }
        }
    }
}
