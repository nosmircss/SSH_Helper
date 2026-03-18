using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;
using System.Windows.Forms;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Captures localhost callback values from a browser-driven flow.
    /// </summary>
    public class BrowserCallbackCaptureCommand : IScriptCommand
    {
        private readonly Func<string, bool> _browserLauncher;
        private readonly Func<int, HttpListener> _listenerFactory;

        public BrowserCallbackCaptureCommand()
            : this(LaunchBrowser, CreateListener)
        {
        }

        internal BrowserCallbackCaptureCommand(
            Func<string, bool> browserLauncher,
            Func<int, HttpListener> listenerFactory)
        {
            _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            _listenerFactory = listenerFactory ?? throw new ArgumentNullException(nameof(listenerFactory));
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.BrowserCallbackCapture == null)
                return CommandResult.Fail("browser_callback_capture has no options");

            var options = step.BrowserCallbackCapture;
            var into = context.SubstituteVariables(options.Into ?? string.Empty).Trim();
            ClearCapture(into, context);

            var startUrl = context.SubstituteVariables(options.StartUrl ?? string.Empty).Trim();
            var callbackPath = context.SubstituteVariables(options.CallbackPath ?? string.Empty).Trim();
            var captureMode = ParseCaptureMode(context.SubstituteVariables(options.CaptureMode ?? "auto"));

            if (string.IsNullOrWhiteSpace(startUrl))
                return CommandResult.ApplyOnError(step, "browser_callback_capture requires 'start_url'");
            if (string.IsNullOrWhiteSpace(callbackPath))
                return CommandResult.ApplyOnError(step, "browser_callback_capture requires 'callback_path'");
            if (string.IsNullOrWhiteSpace(into))
                return CommandResult.ApplyOnError(step, "browser_callback_capture requires 'into'");

            callbackPath = NormalizePath(callbackPath);
            var postPath = NormalizePath(callbackPath + "/capture");
            var timeoutSeconds = options.Timeout > 0 ? options.Timeout : 300;

            using var listener = _listenerFactory(options.LocalPort);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                return CommandResult.ApplyOnError(step, $"browser_callback_capture failed to start local listener: {ex.Message}");
            }

            var callbackUrl = $"http://127.0.0.1:{options.LocalPort}{callbackPath}";
            context.EmitOutput($"Browser callback listener started at {callbackUrl}", ScriptOutputType.Debug);

            if (options.OpenBrowser)
            {
                if (!_browserLauncher(startUrl))
                {
                    return CommandResult.ApplyOnError(step, "browser_callback_capture could not launch default browser");
                }

                context.EmitOutput($"Opened browser to {startUrl}", ScriptOutputType.Debug);
            }
            else
            {
                context.EmitOutput($"Open this URL manually to continue: {startUrl}", ScriptOutputType.Info);
            }

            try
            {
                var captured = await CaptureAsync(
                    listener,
                    callbackPath,
                    postPath,
                    captureMode,
                    options.CompletionMessage,
                    options.FailureMessage,
                    timeoutCts.Token).ConfigureAwait(false);

                RequestApplicationFocusRestore();

                var requiredFields = (options.RequiredFields ?? new List<string>())
                    .Select(field => context.SubstituteVariables(field ?? string.Empty).Trim())
                    .Where(field => !string.IsNullOrWhiteSpace(field))
                    .ToList();

                if (requiredFields.Count > 0)
                {
                    var missing = requiredFields
                        .Where(field => !captured.ContainsKey(field) || string.IsNullOrWhiteSpace(captured[field]))
                        .ToList();

                    if (missing.Count > 0)
                    {
                        return CommandResult.ApplyOnError(step,
                            $"browser_callback_capture missing required field(s): {string.Join(", ", missing)}");
                    }
                }

                PersistCapture(into, captured, context);
                if (!options.Quiet)
                {
                    context.EmitOutput($"Captured {captured.Count} callback value(s) into ${{{into}}}", ScriptOutputType.Success);
                }
                return CommandResult.Ok();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CommandResult.ApplyOnError(step, $"browser_callback_capture timed out after {timeoutSeconds} seconds");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return CommandResult.ApplyOnError(step, $"browser_callback_capture failed: {ex.Message}");
            }
            finally
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                }
            }
        }

        private static HttpListener CreateListener(int localPort)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{localPort}/");
            return listener;
        }

        private static bool LaunchBrowser(string startUrl)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = startUrl,
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<Dictionary<string, string>> CaptureAsync(
            HttpListener listener,
            string callbackPath,
            string postPath,
            CaptureMode captureMode,
            string? completionMessage,
            string? failureMessage,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var context = await WaitForContextAsync(listener, cancellationToken).ConfigureAwait(false);
                var path = NormalizePath(context.Request.Url?.AbsolutePath ?? "/");

                if (PathEquals(path, callbackPath))
                {
                    if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        var queryValues = ReadQueryValues(context.Request);
                        if (captureMode == CaptureMode.Query && queryValues.Count > 0)
                        {
                            await WriteTextResponseAsync(context.Response, 200, "Callback captured. You may close this tab.")
                                .ConfigureAwait(false);
                            return queryValues;
                        }

                        var html = BuildCaptureHtml(postPath, completionMessage, failureMessage);
                        await WriteHtmlResponseAsync(context.Response, 200, html).ConfigureAwait(false);
                        continue;
                    }

                    if (string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        var bodyValues = await ReadBodyValuesAsync(context.Request, cancellationToken).ConfigureAwait(false);
                        var payload = BuildPayload(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), bodyValues, captureMode);
                        await WriteTextResponseAsync(context.Response, 200, "OK").ConfigureAwait(false);
                        if (payload.Count > 0)
                            return payload;
                        continue;
                    }
                }
                else if (PathEquals(path, postPath) && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    var bodyValues = await ReadBodyValuesAsync(context.Request, cancellationToken).ConfigureAwait(false);
                    var queryValues = ReadQueryValues(context.Request);
                    var payload = BuildPayload(queryValues, bodyValues, captureMode);
                    await WriteTextResponseAsync(context.Response, 200, "OK").ConfigureAwait(false);

                    if (payload.Count > 0)
                        return payload;

                    continue;
                }

                await WriteTextResponseAsync(context.Response, 404, "Not Found").ConfigureAwait(false);
            }
        }

        private static async Task<HttpListenerContext> WaitForContextAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var contextTask = listener.GetContextAsync();
                var completed = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
                if (completed == contextTask)
                {
                    return await contextTask.ConfigureAwait(false);
                }
            }
        }

        private static Dictionary<string, string> ReadQueryValues(HttpListenerRequest request)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in request.QueryString.AllKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                result[key] = request.QueryString[key] ?? string.Empty;
            }

            return result;
        }

        private static async Task<Dictionary<string, string>> ReadBodyValuesAsync(HttpListenerRequest request, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8, true, 4096, leaveOpen: true);
            var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return ParseFormEncoded(body);
        }

        private static Dictionary<string, string> ParseFormEncoded(string body)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(body))
                return result;

            var pairs = body.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pair in pairs)
            {
                var separatorIndex = pair.IndexOf('=');
                string rawKey;
                string rawValue;
                if (separatorIndex < 0)
                {
                    rawKey = pair;
                    rawValue = string.Empty;
                }
                else
                {
                    rawKey = pair.Substring(0, separatorIndex);
                    rawValue = pair.Substring(separatorIndex + 1);
                }

                var key = WebUtility.UrlDecode(rawKey.Replace('+', ' ')) ?? string.Empty;
                var value = WebUtility.UrlDecode(rawValue.Replace('+', ' ')) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                result[key] = value;
            }

            return result;
        }

        private static Dictionary<string, string> BuildPayload(
            Dictionary<string, string> queryValues,
            Dictionary<string, string> bodyValues,
            CaptureMode captureMode)
        {
            var query = ExtractPrefixed(bodyValues, "q:");
            var fragment = ExtractPrefixed(bodyValues, "h:");
            var rawBody = bodyValues
                .Where(kvp => !kvp.Key.StartsWith("q:", StringComparison.OrdinalIgnoreCase) && !kvp.Key.StartsWith("h:", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            if (query.Count == 0 && queryValues.Count > 0)
            {
                foreach (var kvp in queryValues)
                    query[kvp.Key] = kvp.Value;
            }

            return captureMode switch
            {
                CaptureMode.Query => query,
                CaptureMode.Fragment => fragment,
                CaptureMode.PostBody => rawBody,
                _ => Merge(mergeOrder: new[] { query, fragment, rawBody })
            };
        }

        private static Dictionary<string, string> ExtractPrefixed(Dictionary<string, string> values, string prefix)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in values)
            {
                if (!kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var key = kvp.Key.Substring(prefix.Length);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                result[key] = kvp.Value;
            }

            return result;
        }

        private static Dictionary<string, string> Merge(IEnumerable<Dictionary<string, string>> mergeOrder)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in mergeOrder)
            {
                foreach (var kvp in source)
                    result[kvp.Key] = kvp.Value;
            }

            return result;
        }

        private static void PersistCapture(string into, Dictionary<string, string> captured, ScriptContext context)
        {
            var sorted = captured
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            context.SetVariable(into, JsonSerializer.Serialize(sorted));
            context.SetVariable(into + "_count", sorted.Count);
            context.SetVariable(into + "_keys", string.Join(",", sorted.Keys));

            foreach (var kvp in sorted)
            {
                var suffix = NormalizeVariableSuffix(kvp.Key);
                context.SetVariable($"{into}_{suffix}", kvp.Value);
            }
        }

        private static void ClearCapture(string into, ScriptContext context)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, string.Empty);
            context.SetVariable(into + "_count", string.Empty);
            context.SetVariable(into + "_keys", string.Empty);
        }

        private static string NormalizeVariableSuffix(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "value";

            var chars = key.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
            var normalized = new string(chars).Trim('_');
            if (string.IsNullOrWhiteSpace(normalized))
                normalized = "value";
            if (char.IsDigit(normalized[0]))
                normalized = "_" + normalized;
            return normalized;
        }

        private static CaptureMode ParseCaptureMode(string mode)
        {
            if (string.Equals(mode, "fragment", StringComparison.OrdinalIgnoreCase))
                return CaptureMode.Fragment;
            if (string.Equals(mode, "query", StringComparison.OrdinalIgnoreCase))
                return CaptureMode.Query;
            if (string.Equals(mode, "post_body", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "postbody", StringComparison.OrdinalIgnoreCase))
                return CaptureMode.PostBody;
            return CaptureMode.Auto;
        }

        private static async Task WriteTextResponseAsync(HttpListenerResponse response, int statusCode, string message)
        {
            var payload = Encoding.UTF8.GetBytes(message);
            response.StatusCode = statusCode;
            response.ContentType = "text/plain; charset=utf-8";
            response.ContentLength64 = payload.LongLength;
            await response.OutputStream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            response.Close();
        }

        private static async Task WriteHtmlResponseAsync(HttpListenerResponse response, int statusCode, string html)
        {
            var payload = Encoding.UTF8.GetBytes(html);
            response.StatusCode = statusCode;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = payload.LongLength;
            await response.OutputStream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            response.Close();
        }

        private static string BuildCaptureHtml(string postPath, string? completionMessage, string? failureMessage)
        {
            var completion = EscapeForJavaScriptString(string.IsNullOrWhiteSpace(completionMessage)
                ? "Callback captured. You may close this tab."
                : completionMessage);
            var failure = EscapeForJavaScriptString(string.IsNullOrWhiteSpace(failureMessage)
                ? "Failed to send callback values to local listener."
                : failureMessage);

            var encodedPostPath = EscapeForJavaScriptString(postPath);

            return $"<!DOCTYPE html>\n" +
                   "<html><head><meta charset=\"utf-8\"><title>Processing callback...</title></head>\n" +
                   "<body style=\"font-family:Segoe UI,Arial,sans-serif;margin:40px;\">\n" +
                   "<h3>Processing browser callback...</h3>\n" +
                   "<p id=\"status\">Collecting callback values...</p>\n" +
                   "<script>\n" +
                   "(function(){\n" +
                   "  var payload = new URLSearchParams();\n" +
                   "  var query = new URLSearchParams(window.location.search.substring(1));\n" +
                   "  query.forEach(function(v,k){ payload.append('q:' + k, v); });\n" +
                   "  var hash = new URLSearchParams(window.location.hash.substring(1));\n" +
                   "  hash.forEach(function(v,k){ payload.append('h:' + k, v); });\n" +
                   $"  fetch('{encodedPostPath}', {{ method:'POST', headers:{{'Content-Type':'application/x-www-form-urlencoded'}}, body: payload.toString() }})\n" +
                   "    .then(function(){\n" +
                   "      document.getElementById('status').textContent='" + completion + "';\n" +
                   "      setTimeout(function(){ window.close(); }, 200);\n" +
                   "    })\n" +
                   "    .catch(function(){ document.getElementById('status').textContent='" + failure + "'; });\n" +
                   "})();\n" +
                   "</script>\n" +
                   "</body></html>";
        }

        private static void RequestApplicationFocusRestore()
        {
            try
            {
                var targetForm = GetTargetForm();
                if (targetForm != null)
                {
                    if (targetForm.InvokeRequired)
                    {
                        targetForm.BeginInvoke((Action)(() => ScheduleUiActivationAttempts(targetForm)));
                        return;
                    }

                    ScheduleUiActivationAttempts(targetForm);
                    return;
                }
            }
            catch
            {
                // Best-effort only. Focus behavior can be blocked by OS foreground lock rules.
            }
            // No open form available; do a single best-effort fallback.
            var handle = Process.GetCurrentProcess().MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(handle);
            }
        }

        private static Form? GetTargetForm()
        {
            if (Application.OpenForms.Count <= 0)
                return null;

            foreach (Form form in Application.OpenForms)
            {
                if (!form.IsDisposed && form.Visible)
                    return form;
            }

            var fallback = Application.OpenForms[0] as Form;
            if (fallback == null)
                return null;

            return fallback.IsDisposed ? null : fallback;
        }

        private static void ScheduleUiActivationAttempts(Form form)
        {
            if (form.IsDisposed || !form.IsHandleCreated)
                return;

            var attempts = 0;
            var timer = new System.Windows.Forms.Timer
            {
                Interval = 220
            };

            timer.Tick += (_, _) =>
            {
                try
                {
                    if (form.IsDisposed)
                    {
                        timer.Stop();
                        timer.Dispose();
                        return;
                    }

                    TryActivateForm(form);
                    attempts++;

                    // First attempt is near browser-close timing, later attempts catch delayed focus release.
                    if (attempts >= 3)
                    {
                        timer.Stop();
                        timer.Dispose();
                        return;
                    }

                    timer.Interval = 320;
                }
                catch
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };

            timer.Start();
        }

        private static void TryActivateForm(Form form)
        {
            var handle = form.Handle;
            if (handle == IntPtr.Zero)
                return;

            var isMinimized = form.WindowState == FormWindowState.Minimized || NativeMethods.IsIconic(handle);
            if (isMinimized)
            {
                // Restore only when minimized so maximized/fullscreen state is preserved.
                NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
            }

            form.Show();
            form.TopMost = true;
            form.BringToFront();
            form.Activate();
            form.TopMost = false;

            NativeMethods.SetForegroundWindow(handle);
        }

        private static string EscapeForJavaScriptString(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal)
                .Replace("\r", string.Empty, StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }

        private static bool PathEquals(string left, string right)
        {
            return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "/";

            var normalized = path.Trim();
            if (!normalized.StartsWith("/", StringComparison.Ordinal))
                normalized = "/" + normalized;
            while (normalized.Contains("//", StringComparison.Ordinal))
                normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
            return normalized;
        }

        private enum CaptureMode
        {
            Auto,
            Fragment,
            Query,
            PostBody
        }

        private static class NativeMethods
        {
            internal const int SW_RESTORE = 9;

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool IsIconic(IntPtr hWnd);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        }
    }
}
