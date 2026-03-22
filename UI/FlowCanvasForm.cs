using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SSH_Helper.UI
{
    /// <summary>
    /// Modeless window hosting a React Flow visual script editor via WebView2.
    /// Communicates with the React app via PostWebMessage/WebMessageReceived.
    /// </summary>
    internal sealed class FlowCanvasForm : Form
    {
        private readonly WebView2 _webView;
        private readonly Label _statusLabel;
        private readonly bool _darkMode;
        private readonly ConcurrentQueue<string> _pendingMessages = new();
        private bool _reactReady;
        private bool _initStarted;

        // Remember window position across open/close within the same session
        private static Point? _lastLocation;
        private static Size? _lastSize;

        public FlowCanvasForm(bool darkMode)
        {
            _darkMode = darkMode;
            _webView = new WebView2();

            Text = "Flow Canvas";
            Size = _lastSize ?? new Size(1200, 800);
            MinimumSize = new Size(800, 600);
            StartPosition = _lastLocation.HasValue
                ? FormStartPosition.Manual
                : FormStartPosition.CenterParent;
            if (_lastLocation.HasValue)
                Location = _lastLocation.Value;
            ShowInTaskbar = true;
            KeyPreview = true;

            // Status label — visible until WebView2 loads content
            _statusLabel = new Label
            {
                Text = "Initializing Flow Canvas...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12F),
                ForeColor = Color.FromArgb(136, 136, 136),
                BackColor = _darkMode ? DialogTheme.DarkBackground : SystemColors.Control,
            };
            Controls.Add(_statusLabel);

            // WebView2 control — hidden initially, shown when ready
            ((System.ComponentModel.ISupportInitialize)_webView).BeginInit();
            _webView.Dock = DockStyle.Fill;
            _webView.Visible = false;
            Controls.Add(_webView);
            ((System.ComponentModel.ISupportInitialize)_webView).EndInit();

            // Theme
            ApplyTheme();

            // Track position
            LocationChanged += (_, _) =>
            {
                if (WindowState == FormWindowState.Normal)
                    _lastLocation = Location;
            };
            SizeChanged += (_, _) =>
            {
                if (WindowState == FormWindowState.Normal)
                    _lastSize = Size;
            };

            // Use Shown event — fires after form is fully visible and message loop is running
            Shown += OnFormShown;
        }

        private async void OnFormShown(object? sender, EventArgs e)
        {
            if (_initStarted) return;
            _initStarted = true;

            try
            {
                await InitializeWebView2Async();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
                _statusLabel.ForeColor = Color.FromArgb(231, 76, 60);
                System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Init error: {ex}");
            }
        }

        private async System.Threading.Tasks.Task InitializeWebView2Async()
        {
            UpdateStatus("Creating WebView2 environment...");

            // Ensure WebView2 control handle exists
            if (!_webView.IsHandleCreated)
                _webView.CreateControl();

            // Use a dedicated user data folder
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SSH_Helper", "WebView2", "FlowCanvas");
            Directory.CreateDirectory(userDataDir);

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataDir);

            UpdateStatus("Initializing WebView2 runtime...");

            await _webView.EnsureCoreWebView2Async(environment);

            UpdateStatus("Loading Flow Canvas app...");

            // Configure settings
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            if (_darkMode)
                _webView.DefaultBackgroundColor = DialogTheme.DarkBackground;

            // Navigation events
            _webView.CoreWebView2.NavigationCompleted += (s, ev) =>
            {
                System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Nav done: success={ev.IsSuccess} status={ev.WebErrorStatus}");
                if (ev.IsSuccess)
                {
                    // Show WebView2, hide status label
                    _webView.Visible = true;
                    _statusLabel.Visible = false;
                }
                else
                {
                    UpdateStatus($"Navigation error: {ev.WebErrorStatus}");
                    _statusLabel.ForeColor = Color.FromArgb(231, 76, 60);
                }
            };

            // Listen for messages from React
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Find and load the single-file HTML
            var indexPath = GetIndexHtmlPath();
            System.Diagnostics.Debug.WriteLine($"[FlowCanvas] index.html path: {indexPath}");

            if (indexPath != null && File.Exists(indexPath))
            {
                var html = File.ReadAllText(indexPath);

                // Strip 'type="module"' and 'crossorigin' — these block execution
                // when loaded via NavigateToString (about:blank origin).
                // Wrap in DOMContentLoaded since removing type="module" makes it
                // synchronous (runs before <body> is parsed).
                html = html.Replace("<script type=\"module\" crossorigin>",
                    "<script>document.addEventListener('DOMContentLoaded',function(){");
                html = html.Replace("</script>\n  </head>",
                    "});</script>\n  </head>");
                html = html.Replace(" crossorigin ", " ");

                System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Loading {html.Length} chars");
                _webView.CoreWebView2.NavigateToString(html);
            }
            else
            {
                // Show diagnostic info directly in the status label
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var projectRoot = FindProjectRoot(exeDir);
                var searchedPaths = new[]
                {
                    Path.Combine(exeDir, "FlowCanvas", "dist", "index.html"),
                    projectRoot != null ? Path.Combine(projectRoot, "FlowCanvas", "dist", "index.html") : "(no project root found)"
                };

                _statusLabel.Text = "Flow Canvas build not found.\n\n" +
                    "Run: cd FlowCanvas && npm run build\n\n" +
                    $"Searched:\n{string.Join("\n", searchedPaths)}";
                _statusLabel.ForeColor = Color.FromArgb(231, 76, 60);
                _statusLabel.Font = new Font("Consolas", 10F);
                _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
                _statusLabel.Padding = new Padding(40);
            }
        }

        private void UpdateStatus(string text)
        {
            if (!IsDisposed && _statusLabel.Visible)
                _statusLabel.Text = text;
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.WebMessageAsJson;
                var msg = JObject.Parse(json);
                var type = msg["type"]?.ToString();

                switch (type)
                {
                    case "ready":
                        _reactReady = true;
                        while (_pendingMessages.TryDequeue(out var pending))
                            _webView.CoreWebView2.PostWebMessageAsJson(pending);
                        break;

                    case "apply-yaml":
                        OnApplyYaml?.Invoke(msg);
                        break;

                    case "debug-action":
                        OnDebugAction?.Invoke(msg);
                        break;

                    case "test-step":
                        OnTestStep?.Invoke(msg);
                        break;

                    case "breakpoint-toggle":
                        OnBreakpointToggle?.Invoke(msg);
                        break;

                    case "run-request":
                        OnRunRequest?.Invoke(msg);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Message error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a JSON message to the React app. Queues if React isn't ready yet.
        /// </summary>
        public void SendMessage(object message)
        {
            var json = JsonConvert.SerializeObject(message);

            if (_reactReady && _webView.CoreWebView2 != null)
            {
                if (InvokeRequired)
                    BeginInvoke(() =>
                    {
                        if (!IsDisposed && _webView.CoreWebView2 != null)
                            _webView.CoreWebView2.PostWebMessageAsJson(json);
                    });
                else if (!IsDisposed)
                    _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            else
            {
                _pendingMessages.Enqueue(json);
            }
        }

        /// <summary>
        /// Sends a load-graph message to display nodes and edges.
        /// </summary>
        public void LoadGraph(object nodes, object edges)
        {
            SendMessage(new { type = "load-graph", nodes, edges });
        }

        // Events for messages from the React app
        public event Action<JObject>? OnApplyYaml;
        public event Action<JObject>? OnDebugAction;
        public event Action<JObject>? OnTestStep;
        public event Action<JObject>? OnBreakpointToggle;
        public event Action<JObject>? OnRunRequest;

        private void ApplyTheme()
        {
            BackColor = _darkMode ? DialogTheme.DarkBackground : SystemColors.Control;
            ForeColor = _darkMode ? Color.FromArgb(212, 212, 212) : SystemColors.ControlText;
            DialogTheme.SetDarkTitleBar(this, _darkMode);
        }

        private static string? GetIndexHtmlPath()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;

            // Try relative to executable (production)
            var fromExe = Path.Combine(exeDir, "FlowCanvas", "dist", "index.html");
            if (File.Exists(fromExe))
                return fromExe;

            // Try relative to project root (development)
            var projectRoot = FindProjectRoot(exeDir);
            if (projectRoot != null)
            {
                var fromProject = Path.Combine(projectRoot, "FlowCanvas", "dist", "index.html");
                if (File.Exists(fromProject))
                    return fromProject;
            }

            return null;
        }

        private static string? FindProjectRoot(string startDir)
        {
            var dir = startDir;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "SSH_Helper.csproj")) ||
                    File.Exists(Path.Combine(dir, "SSH_Helper.sln")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_webView.CoreWebView2 != null)
                    _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _webView.Dispose();
                _statusLabel.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
