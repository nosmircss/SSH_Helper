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
    /// Modeless window hosting a React Flow visual script editor via WebView2.
    /// Communicates with the React app via PostWebMessage/WebMessageReceived.
    /// </summary>
    internal sealed class FlowCanvasForm : Form
    {
        private readonly WebView2 _webView;
        private readonly Label _statusLabel;
        private readonly bool _darkMode;
        private readonly ConfigurationService? _configService;
        private readonly ConcurrentQueue<string> _pendingMessages = new();
        private bool _reactReady;
        private bool _initStarted;

        // Remember window position across open/close within the same session
        private static Point? _lastLocation;
        private static Size? _lastSize;

        public FlowCanvasForm(bool darkMode, ConfigurationService? configService = null)
        {
            _darkMode = darkMode;
            _configService = configService;
            _webView = new WebView2();

            // Resolve initial size: session cache > persisted config > default
            var initialSize = _lastSize ?? GetPersistedSize() ?? new Size(1200, 800);

            Text = "Flow Canvas";
            Size = initialSize;
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
                AppDataPaths.GetAppFolder(),
                "WebView2",
                "FlowCanvas");
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

            // Find the dist folder and serve via virtual host mapping
            var distResolution = FlowCanvasDistLocator.ResolveDistPath();
            var distPath = distResolution.DistPath;
            System.Diagnostics.Debug.WriteLine($"[FlowCanvas] dist path: {distPath}");

            if (distPath != null)
            {
                // Map the dist folder as a virtual host — this serves all files
                // (HTML, JS, CSS) from the same origin, so type="module" works correctly
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "flowcanvas.local",
                    distPath,
                    CoreWebView2HostResourceAccessKind.Allow);

                _webView.CoreWebView2.Navigate("https://flowcanvas.local/index.html");
                System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Navigating to https://flowcanvas.local/index.html");
            }
            else
            {
                _statusLabel.Text = "Flow Canvas build not found.\n\n" +
                    "Development build: Run `cd FlowCanvas && npm run build`\n" +
                    "Published single-file build: ensure embedded Flow Canvas assets are present.\n\n" +
                    $"Searched:\n{string.Join("\n", distResolution.SearchedPaths)}";
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
                        SendPersistedLayout();
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

                    case "execute-canvas":
                        OnExecuteCanvas?.Invoke(msg);
                        break;

                    case "breakpoint-toggle":
                        OnBreakpointToggle?.Invoke(msg);
                        break;

                    case "run":
                        OnRunRequest?.Invoke(msg);
                        break;

                    case "run-request":
                        System.Diagnostics.Debug.WriteLine("[FlowCanvas] Deprecated outbound message 'run-request' received; use 'run' instead.");
                        OnRunRequest?.Invoke(msg);
                        break;

                    case "disable-block":
                        OnDisableBlock?.Invoke(msg);
                        break;

                    case "test-data-block":
                        OnTestDataBlock?.Invoke(msg);
                        break;

                    case "layout-save":
                        // Pass the whole message so both panel sizes and the heatmap toggle
                        // (which arrives without a panelSizes object) are visible.
                        SavePanelSizes(msg);
                        break;

                    case "pref-save":
                        SaveReducedMotionPref(msg);
                        break;

                    case "layout-autosave":
                        OnLayoutAutosave?.Invoke(msg);
                        break;

                    case "browse-path":
                        OnBrowsePath?.Invoke(msg);
                        break;

                    case "show-error":
                        var errorMsg = msg["message"]?.ToString() ?? "Unknown error";
                        BeginInvoke(() => DialogTheme.Show(this, errorMsg, "Flow Canvas", MessageBoxButtons.OK, MessageBoxIcon.Error));
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Unknown outbound message type '{type}' ignored.");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowCanvas] Message error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends the target host data to the React Host Bar.
        /// Pass null to indicate no valid host is available.
        /// </summary>
        public void SetTargetHost(object? hostData)
        {
            SendMessage(new { type = "set-target-host", host = hostData });
        }

        /// <summary>
        /// Sends a JSON message to the React app. Queues if React isn't ready yet.
        /// </summary>
        public void SendMessage(object message)
        {
            var json = JsonConvert.SerializeObject(message);

            // Always marshal to UI thread first — accessing CoreWebView2
            // from a background thread throws InvalidOperationException
            if (InvokeRequired)
            {
                BeginInvoke(() => PostOrQueue(json));
                return;
            }

            PostOrQueue(json);
        }

        private void PostOrQueue(string json)
        {
            if (_reactReady && !IsDisposed && _webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            else
            {
                _pendingMessages.Enqueue(json);
            }
        }

        /// <summary>
        /// Sends a load-graph message to display nodes and edges.
        /// <paramref name="hasUserLayout"/> tells the canvas whether the positions are a
        /// saved user arrangement (true → keep) or algorithmic defaults (false → the canvas
        /// will run its hierarchical auto-layout).
        /// </summary>
        public void LoadGraph(object nodes, object edges, bool hasUserLayout = false)
        {
            SendMessage(new { type = "load-graph", nodes, edges, hasUserLayout });
        }

        // Events for messages from the React app
        public event Action<JObject>? OnApplyYaml;
        public event Action<JObject>? OnDebugAction;
        public event Action<JObject>? OnTestStep;
        public event Action<JObject>? OnExecuteCanvas;
        public event Action<JObject>? OnBreakpointToggle;
        public event Action<JObject>? OnRunRequest;
        public event Action<JObject>? OnDisableBlock;
        public event Action<JObject>? OnTestDataBlock;
        public event Action<JObject>? OnLayoutAutosave;
        public event Action<JObject>? OnBrowsePath;

        private void ApplyTheme()
        {
            BackColor = _darkMode ? DialogTheme.DarkBackground : SystemColors.Control;
            ForeColor = _darkMode ? Color.FromArgb(212, 212, 212) : SystemColors.ControlText;
            DialogTheme.SetDarkTitleBar(this, _darkMode);
        }

        private void SendPersistedLayout()
        {
            var ws = _configService?.GetCurrent().WindowState;
            if (ws == null) return;

            var panelSizes = new JObject();
            if (ws.FlowCanvasRightPanelWidth > 0)
                panelSizes["rightPanelWidth"] = ws.FlowCanvasRightPanelWidth;
            if (ws.FlowCanvasOutputHeight > 0)
                panelSizes["outputHeight"] = ws.FlowCanvasOutputHeight;

            // React guards each field by type, so sending nulls is harmless. Always send so any
            // persisted display setting is restored, not just panel sizes.
            SendMessage(new
            {
                type = "layout-restore",
                panelSizes,
                heatmapEnabled = ws.FlowCanvasHeatmapEnabled ?? false,
                blockWidth = ws.FlowCanvasBlockWidth,
                textScale = ws.FlowCanvasTextScale,
                density = ws.FlowCanvasDensity,
                defaultBlockExpanded = ws.FlowCanvasDefaultExpanded,
                snapToGrid = ws.FlowCanvasSnapToGrid,
                branchBandsEnabled = ws.FlowCanvasBranchBands,
                compactCommentsEnabled = ws.FlowCanvasCompactComments,
                autoReflowEnabled = ws.FlowCanvasAutoReflow,
            });

            var rm = ws.FlowCanvasReducedMotion;
            if (rm.HasValue) SendMessage(new { type = "pref-restore", reducedMotion = rm.Value });
        }

        private void SavePanelSizes(JObject? msg)
        {
            if (_configService == null || msg == null) return;

            var panelSizes = msg["panelSizes"] as JObject;
            var rightWidth = panelSizes?["rightPanelWidth"]?.Value<int>();
            var outputHeight = panelSizes?["outputHeight"]?.Value<int>();
            // These all reuse the layout-save channel and arrive without a panelSizes object.
            var heatmap = msg["heatmapEnabled"]?.Value<bool>();
            var blockWidth = msg["blockWidth"]?.Value<int>();
            var textScale = msg["textScale"]?.Value<double>();
            var density = msg["density"]?.Value<double>();
            var defaultExpanded = msg["defaultBlockExpanded"]?.Value<bool>();
            var snap = msg["snapToGrid"]?.Value<bool>();
            var bands = msg["branchBandsEnabled"]?.Value<bool>();
            var compact = msg["compactCommentsEnabled"]?.Value<bool>();
            var autoReflow = msg["autoReflowEnabled"]?.Value<bool>();

            if (rightWidth == null && outputHeight == null && heatmap == null && blockWidth == null
                && textScale == null && density == null && defaultExpanded == null && snap == null && bands == null
                && compact == null && autoReflow == null)
                return;

            _configService.Update(c =>
            {
                c.WindowState ??= new Models.WindowState();
                if (rightWidth > 0) c.WindowState.FlowCanvasRightPanelWidth = rightWidth;
                if (outputHeight > 0) c.WindowState.FlowCanvasOutputHeight = outputHeight;
                if (heatmap.HasValue) c.WindowState.FlowCanvasHeatmapEnabled = heatmap.Value;
                if (blockWidth > 0) c.WindowState.FlowCanvasBlockWidth = blockWidth;
                if (textScale.HasValue) c.WindowState.FlowCanvasTextScale = textScale.Value;
                if (density.HasValue) c.WindowState.FlowCanvasDensity = density.Value;
                if (defaultExpanded.HasValue) c.WindowState.FlowCanvasDefaultExpanded = defaultExpanded.Value;
                if (snap.HasValue) c.WindowState.FlowCanvasSnapToGrid = snap.Value;
                if (bands.HasValue) c.WindowState.FlowCanvasBranchBands = bands.Value;
                if (compact.HasValue) c.WindowState.FlowCanvasCompactComments = compact.Value;
                if (autoReflow.HasValue) c.WindowState.FlowCanvasAutoReflow = autoReflow.Value;
            });
        }

        private void SaveReducedMotionPref(JObject msg)
        {
            if (_configService == null) return;
            var v = msg["reducedMotion"]?.Value<bool>();
            if (v == null) return;
            _configService.Update(c =>
            {
                c.WindowState ??= new Models.WindowState();
                c.WindowState.FlowCanvasReducedMotion = v.Value;
            });
        }

        private Size? GetPersistedSize()
        {
            var ws = _configService?.GetCurrent().WindowState;
            if (ws?.FlowCanvasWidth > 0 && ws?.FlowCanvasHeight > 0)
                return new Size(ws.FlowCanvasWidth.Value, ws.FlowCanvasHeight.Value);
            return null;
        }

        private void SavePersistedSize()
        {
            if (_configService == null || _lastSize == null) return;
            var size = _lastSize.Value;
            _configService.Update(c =>
            {
                c.WindowState ??= new Models.WindowState();
                c.WindowState.FlowCanvasWidth = size.Width;
                c.WindowState.FlowCanvasHeight = size.Height;
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SavePersistedSize();
                if (_webView.CoreWebView2 != null)
                    _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _webView.Dispose();
                _statusLabel.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
