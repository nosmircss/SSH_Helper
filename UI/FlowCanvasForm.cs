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
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            if (_darkMode)
                _webView.DefaultBackgroundColor = DialogTheme.DarkBackground;

            // Listen for messages from React
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Map the dist folder as a virtual host so React app loads correctly
            var distPath = GetDistPath();
            if (distPath != null && Directory.Exists(distPath))
            {
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "flowcanvas.local",
                    distPath,
                    CoreWebView2HostResourceAccessKind.Allow);

                _webView.CoreWebView2.Navigate("https://flowcanvas.local/index.html");
            }
            else
            {
                // Fallback: show error message
                _webView.CoreWebView2.NavigateToString(
                    "<html><body style='background:#1a1a2e;color:#e74c3c;font-family:sans-serif;padding:40px;'>" +
                    "<h2>Flow Canvas build not found</h2>" +
                    "<p>Run <code>npm run build</code> in the FlowCanvas/ directory.</p>" +
                    $"<p>Expected: {distPath ?? "unknown"}</p></body></html>");
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
                    BeginInvoke(() => _webView.CoreWebView2.PostWebMessageAsJson(json));
                else
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

        private static string? GetDistPath()
        {
            // Try relative to executable first (production)
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var distFromExe = Path.Combine(exeDir, "FlowCanvas", "dist");
            if (Directory.Exists(distFromExe))
                return distFromExe;

            // Try relative to project root (development)
            var projectRoot = FindProjectRoot(exeDir);
            if (projectRoot != null)
            {
                var distFromProject = Path.Combine(projectRoot, "FlowCanvas", "dist");
                if (Directory.Exists(distFromProject))
                    return distFromProject;
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
