using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.Utilities;

namespace SSH_Helper.UI
{
    /// <summary>
    /// Detachable top-level window mirroring the run output. Hosts its own WebView2 loading the
    /// same dist with ?panel=runoutput (renders only RunOutputView). Owned and fed by Form1;
    /// independent of the Flow Canvas window. Dark-only (matches the console).
    /// </summary>
    internal sealed class RunOutputWindowForm : Form
    {
        private readonly WebView2 _webView;
        private readonly Label _statusLabel;
        private readonly bool _darkMode;
        private readonly ConfigurationService? _configService;
        private readonly ConcurrentQueue<string> _pendingMessages = new();
        private bool _reactReady;
        private bool _initStarted;

        private static Point? _lastLocation;
        private static Size? _lastSize;

        public RunOutputWindowForm(bool darkMode, ConfigurationService? configService = null)
        {
            _darkMode = darkMode;
            _configService = configService;
            _webView = new WebView2();

            var initialSize = _lastSize ?? GetPersistedSize() ?? new Size(900, 520);

            Text = "Run Output";
            Size = initialSize;
            MinimumSize = new Size(420, 260);

            var persistedLocation = _lastLocation ?? GetPersistedLocation();
            StartPosition = persistedLocation.HasValue ? FormStartPosition.Manual : FormStartPosition.CenterParent;
            if (persistedLocation.HasValue) Location = persistedLocation.Value;
            ShowInTaskbar = true;
            KeyPreview = true;

            _statusLabel = new Label
            {
                Text = "Initializing Run Output...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12F),
                ForeColor = Color.FromArgb(136, 136, 136),
                BackColor = _darkMode ? DialogTheme.DarkBackground : SystemColors.Control,
            };
            Controls.Add(_statusLabel);

            ((System.ComponentModel.ISupportInitialize)_webView).BeginInit();
            _webView.Dock = DockStyle.Fill;
            _webView.Visible = false;
            Controls.Add(_webView);
            ((System.ComponentModel.ISupportInitialize)_webView).EndInit();

            if (_darkMode) BackColor = DialogTheme.DarkBackground;
            DialogTheme.SetDarkTitleBar(this, _darkMode);

            LocationChanged += (_, _) => { if (WindowState == FormWindowState.Normal) _lastLocation = Location; };
            SizeChanged += (_, _) => { if (WindowState == FormWindowState.Normal) _lastSize = Size; };
            FormClosing += (_, _) => SavePersistedGeometry();

            Shown += OnFormShown;
        }

        private async void OnFormShown(object? sender, EventArgs e)
        {
            if (_initStarted) return;
            _initStarted = true;
            try { await InitializeWebView2Async(); }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
                _statusLabel.ForeColor = Color.FromArgb(231, 76, 60);
                System.Diagnostics.Debug.WriteLine($"[RunOutputWindow] Init error: {ex}");
            }
        }

        private async System.Threading.Tasks.Task InitializeWebView2Async()
        {
            _statusLabel.Text = "Loading Run Output...";
            if (!_webView.IsHandleCreated) _webView.CreateControl();

            // Dedicated user-data folder: a separate browser process from the Flow Canvas WebView2,
            // which avoids any user-data-folder lock contention between the two windows.
            var userDataDir = Path.Combine(AppDataPaths.GetAppFolder(), "WebView2", "RunOutputWindow");
            Directory.CreateDirectory(userDataDir);

            var environment = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: userDataDir);
            await _webView.EnsureCoreWebView2Async(environment);

            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            if (_darkMode) _webView.DefaultBackgroundColor = DialogTheme.DarkBackground;

            _webView.CoreWebView2.NavigationCompleted += (s, ev) =>
            {
                if (ev.IsSuccess) { _webView.Visible = true; _statusLabel.Visible = false; }
                else { _statusLabel.Text = $"Navigation error: {ev.WebErrorStatus}"; _statusLabel.ForeColor = Color.FromArgb(231, 76, 60); }
            };
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            var distPath = FlowCanvasDistLocator.ResolveDistPath().DistPath;
            if (distPath != null)
            {
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping("flowcanvas.local", distPath, CoreWebView2HostResourceAccessKind.Allow);
                _webView.CoreWebView2.Navigate("https://flowcanvas.local/index.html?panel=runoutput");
            }
            else
            {
                _statusLabel.Text = "Flow Canvas assets not found.";
                _statusLabel.ForeColor = Color.FromArgb(231, 76, 60);
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try { HandleHostMessage(JObject.Parse(e.WebMessageAsJson)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RunOutputWindow] Message error: {ex.Message}"); }
        }

        internal void HandleHostMessage(JObject msg)
        {
            switch (msg["type"]?.ToString())
            {
                case "ready":
                    _reactReady = true;
                    while (_pendingMessages.TryDequeue(out var pending))
                        _webView.CoreWebView2.PostWebMessageAsJson(pending);
                    SendPersistedPrefs();
                    break;
                case "layout-save":
                    SaveRunOutputPrefs(msg);
                    break;
            }
        }

        public void SendMessage(object message)
        {
            var json = JsonConvert.SerializeObject(message);
            if (InvokeRequired) { BeginInvoke(() => PostOrQueue(json)); return; }
            PostOrQueue(json);
        }

        private void PostOrQueue(string json)
        {
            if (_reactReady && !IsDisposed && _webView.CoreWebView2 != null)
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            else
                _pendingMessages.Enqueue(json);
        }

        public void SendRunOutputAppend(string chunk)
        {
            if (string.IsNullOrEmpty(chunk)) return;
            SendMessage(new { type = "run-output", chunk });
        }

        public void SendRunOutputClear() => SendMessage(new { type = "run-output-clear" });

        /// <summary>Drives the console's LIVE indicator (reuses the canvas run-lifecycle messages).</summary>
        public void SendRunState(bool running) => SendMessage(new { type = running ? "execution-started" : "execution-finished" });

        private void SendPersistedPrefs()
        {
            var ws = _configService?.GetCurrent().WindowState;
            if (ws == null) return;
            SendMessage(new
            {
                type = "layout-restore",
                runOutputColor = ws.FlowCanvasRunOutputColor,
                runOutputWrap = ws.FlowCanvasRunOutputWrap,
                runOutputFollow = ws.FlowCanvasRunOutputFollow,
            });
        }

        private void SaveRunOutputPrefs(JObject msg)
        {
            if (_configService == null) return;
            var color = msg["runOutputColor"]?.Value<bool>();
            var wrap = msg["runOutputWrap"]?.Value<bool>();
            var follow = msg["runOutputFollow"]?.Value<bool>();
            if (color == null && wrap == null && follow == null) return;
            _configService.Update(c =>
            {
                c.WindowState ??= new Models.WindowState();
                if (color.HasValue) c.WindowState.FlowCanvasRunOutputColor = color.Value;
                if (wrap.HasValue) c.WindowState.FlowCanvasRunOutputWrap = wrap.Value;
                if (follow.HasValue) c.WindowState.FlowCanvasRunOutputFollow = follow.Value;
            });
        }

        private Size? GetPersistedSize()
        {
            var ws = _configService?.GetCurrent().WindowState;
            if (ws?.FlowCanvasRunOutputWindowWidth > 0 && ws?.FlowCanvasRunOutputWindowHeight > 0)
                return new Size(ws.FlowCanvasRunOutputWindowWidth.Value, ws.FlowCanvasRunOutputWindowHeight.Value);
            return null;
        }

        private Point? GetPersistedLocation()
        {
            var ws = _configService?.GetCurrent().WindowState;
            if (ws?.FlowCanvasRunOutputWindowLeft is int l && ws?.FlowCanvasRunOutputWindowTop is int t)
                return new Point(l, t);
            return null;
        }

        private void SavePersistedGeometry()
        {
            if (_configService == null || WindowState != FormWindowState.Normal) return;
            _configService.Update(c =>
            {
                c.WindowState ??= new Models.WindowState();
                c.WindowState.FlowCanvasRunOutputWindowLeft = Location.X;
                c.WindowState.FlowCanvasRunOutputWindowTop = Location.Y;
                c.WindowState.FlowCanvasRunOutputWindowWidth = Size.Width;
                c.WindowState.FlowCanvasRunOutputWindowHeight = Size.Height;
            });
        }
    }
}
