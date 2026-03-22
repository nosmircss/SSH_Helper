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
        private readonly bool _darkMode;
        private readonly ConcurrentQueue<string> _pendingMessages = new();
        private bool _reactReady;

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

            // Layout: WebView2 fills the entire form
            ((System.ComponentModel.ISupportInitialize)_webView).BeginInit();
            _webView.Dock = DockStyle.Fill;
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

            // Initialize WebView2 after handle creation
            HandleCreated += async (_, _) =>
            {
                try
                {
                    await InitializeWebView2Async();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to initialize Flow Canvas:\n{ex.Message}",
                        "Flow Canvas Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Close();
                }
            };
        }

        private async System.Threading.Tasks.Task InitializeWebView2Async()
        {
            // Ensure handles exist (required for EnsureCoreWebView2Async)
            if (!_webView.IsHandleCreated)
                _webView.CreateControl();

            // Use a dedicated user data folder
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SSH_Helper", "WebView2", "FlowCanvas");
            Directory.CreateDirectory(userDataDir);

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataDir).ConfigureAwait(true);

            await _webView.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

            // Configure settings
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true; // Allow right-click for DevTools
            _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true; // Allow F12 for DevTools
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            if (_darkMode)
                _webView.DefaultBackgroundColor = DialogTheme.DarkBackground;

            // Log navigation events and JS console errors
            _webView.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Navigation completed: success={e.IsSuccess}, status={e.WebErrorStatus}");
                if (!e.IsSuccess)
                {
                    _webView.CoreWebView2.NavigateToString(
                        $"<html><body style='background:#1a1a2e;color:#e74c3c;font-family:sans-serif;padding:40px;'>" +
                        $"<h2>Navigation Error</h2>" +
                        $"<p>WebErrorStatus: {e.WebErrorStatus}</p></body></html>");
                }
            };


            // Listen for messages from React
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Load the React app — single-file HTML with all JS/CSS inlined
            var indexPath = GetIndexHtmlPath();
            System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Resolved index.html: {indexPath}");

            if (indexPath != null && File.Exists(indexPath))
            {
                var html = File.ReadAllText(indexPath);
                _webView.CoreWebView2.NavigateToString(html);
                System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Loaded {html.Length} chars via NavigateToString");
            }
            else
            {
                // Fallback: show diagnostic error
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var projectRoot = FindProjectRoot(exeDir);
                _webView.CoreWebView2.NavigateToString(
                    "<html><body style='background:#1a1a2e;color:#e74c3c;font-family:sans-serif;padding:40px;'>" +
                    "<h2>Flow Canvas build not found</h2>" +
                    "<p>Run <code>cd FlowCanvas && npm run build</code> in the project directory.</p>" +
                    $"<p>Exe dir: {exeDir}</p>" +
                    $"<p>Project root: {projectRoot ?? "not found"}</p>" +
                    $"<p>Looked for: {indexPath ?? "null"}</p></body></html>");
            }
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
                        // Flush any queued messages
                        while (_pendingMessages.TryDequeue(out var pending))
                        {
                            _webView.CoreWebView2.PostWebMessageAsJson(pending);
                        }
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
                System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Message parse error: {ex.Message}");
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
            // Try relative to executable first (production)
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
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
            }
            base.Dispose(disposing);
        }
    }
}
