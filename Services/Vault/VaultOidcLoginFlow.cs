using System.Diagnostics;
using System.IO;
using System.Net;

namespace SSH_Helper.Services.Vault
{
    internal sealed class VaultOidcLoginResult
    {
        public string State { get; init; } = string.Empty;
        public string? Code { get; init; }
        public string? Error { get; init; }
        public string? ErrorDescription { get; init; }
    }

    internal interface IVaultOidcLoginFlow
    {
        Task<VaultOidcLoginResult> ExecuteAsync(
            string authUrl,
            string callbackHost,
            int callbackPort,
            string callbackPath,
            int timeoutSeconds,
            CancellationToken cancellationToken);
    }

    internal sealed class VaultOidcLoginFlow : IVaultOidcLoginFlow
    {
        public async Task<VaultOidcLoginResult> ExecuteAsync(
            string authUrl,
            string callbackHost,
            int callbackPort,
            string callbackPath,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(authUrl))
                throw new VaultException("Vault OIDC login failed — auth URL was empty");

            var normalizedPath = NormalizePath(callbackPath);
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://{callbackHost}:{callbackPort}/");

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                throw new VaultException(
                    $"Vault OIDC login failed — cannot start local callback listener on {callbackHost}:{callbackPort}: {ex.Message}",
                    ex);
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                throw new VaultException($"Vault OIDC login failed — cannot open browser: {ex.Message}", ex);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(15, timeoutSeconds)));

            try
            {
                while (true)
                {
                    var context = await WaitForContextAsync(listener, timeoutCts.Token).ConfigureAwait(false);
                    var path = NormalizePath(context.Request.Url?.AbsolutePath ?? "/");

                    if (!string.Equals(path, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteTextAsync(context.Response, 404, "Not Found").ConfigureAwait(false);
                        continue;
                    }

                    if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteTextAsync(context.Response, 405, "Method Not Allowed").ConfigureAwait(false);
                        continue;
                    }

                    var state = context.Request.QueryString["state"] ?? string.Empty;
                    var code = context.Request.QueryString["code"];
                    var error = context.Request.QueryString["error"];
                    var errorDescription = context.Request.QueryString["error_description"];

                    var html = string.IsNullOrEmpty(error)
                        ? BuildCompletionHtml("Vault OIDC login complete. You can close this browser tab.")
                        : BuildCompletionHtml("Vault OIDC login failed. You can close this browser tab.");

                    await WriteHtmlAsync(context.Response, 200, html).ConfigureAwait(false);

                    return new VaultOidcLoginResult
                    {
                        State = state,
                        Code = code,
                        Error = error,
                        ErrorDescription = errorDescription
                    };
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new VaultException($"Vault OIDC login timed out after {Math.Max(15, timeoutSeconds)} seconds");
            }
            finally
            {
                if (listener.IsListening)
                    listener.Stop();
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
                    return await contextTask.ConfigureAwait(false);
            }
        }

        private static async Task WriteTextAsync(HttpListenerResponse response, int statusCode, string content)
        {
            response.StatusCode = statusCode;
            using var writer = new StreamWriter(response.OutputStream);
            await writer.WriteAsync(content).ConfigureAwait(false);
            response.Close();
        }

        private static async Task WriteHtmlAsync(HttpListenerResponse response, int statusCode, string html)
        {
            response.StatusCode = statusCode;
            response.ContentType = "text/html; charset=utf-8";
            using var writer = new StreamWriter(response.OutputStream);
            await writer.WriteAsync(html).ConfigureAwait(false);
            response.Close();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "/";

            var normalized = path.Trim();
            if (!normalized.StartsWith('/'))
                normalized = "/" + normalized;

            return normalized;
        }

        private static string BuildCompletionHtml(string message)
        {
            var encoded = WebUtility.HtmlEncode(message);
            return $"<!doctype html><html><head><meta charset=\"utf-8\"><title>Vault OIDC</title></head><body><p>{encoded}</p></body></html>";
        }
    }
}
