using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Makes HTTP requests with optional auth, body, and response capture.
    /// </summary>
    public class HttpCommand : IScriptCommand
    {
        private readonly Func<HttpOptions, HttpMessageHandler> _handlerFactory;

        private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
        };

        private static readonly HashSet<string> AllowedAuthModes = new(StringComparer.OrdinalIgnoreCase)
        {
            "none", "basic", "bearer"
        };

        private static readonly Dictionary<string, string> ContentTypeMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["json"] = "application/json",
            ["form"] = "application/x-www-form-urlencoded",
            ["text"] = "text/plain",
            ["xml"] = "application/xml"
        };

        public HttpCommand()
            : this(CreateDefaultHandler)
        {
        }

        internal HttpCommand(Func<HttpOptions, HttpMessageHandler> handlerFactory)
        {
            _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Http == null)
                return CommandResult.Fail("Http command has no options");

            var options = step.Http;
            var into = options.Into;
            var totalSw = Stopwatch.StartNew();

            // Clear capture variables at the start of each execution to prevent stale values.
            ClearCapture(into, context);

            if (string.IsNullOrWhiteSpace(options.Url))
                return ApplyOnError(step, "Http requires 'url'");

            var url = context.SubstituteVariables(options.Url);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return ApplyOnError(step, $"Invalid URL: {url}");

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return ApplyOnError(step, $"URL must use http or https scheme: {url}");
            }

            var method = context.SubstituteVariables(options.Method ?? "GET").Trim().ToUpperInvariant();
            if (!AllowedMethods.Contains(method))
                return ApplyOnError(step, $"Http method '{method}' is not supported");

            var authMode = context.SubstituteVariables(options.Auth ?? "none").Trim().ToLowerInvariant();
            if (!AllowedAuthModes.Contains(authMode))
                return ApplyOnError(step, $"Http auth '{authMode}' is not supported");

            var contentTypeToken = string.IsNullOrWhiteSpace(options.ContentType)
                ? null
                : context.SubstituteVariables(options.ContentType).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(contentTypeToken) && !ContentTypeMappings.ContainsKey(contentTypeToken))
                return ApplyOnError(step, $"Http content_type '{contentTypeToken}' is not supported");

            var body = options.Body == null ? null : context.SubstituteVariables(options.Body);
            var timeoutSeconds = options.Timeout > 0 ? options.Timeout : 30;

            using var handler = _handlerFactory(options);
            using var client = new HttpClient(handler, disposeHandler: true);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var request = new HttpRequestMessage(new HttpMethod(method), uri);
            var authSummary = "none";

            if (string.Equals(authMode, "basic", StringComparison.OrdinalIgnoreCase))
            {
                var username = context.SubstituteVariables(options.Username ?? string.Empty);
                var password = context.SubstituteVariables(options.Password ?? string.Empty);
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return ApplyOnError(step, "Http auth 'basic' requires non-empty username and password");

                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
                authSummary = $"basic username={username} password_length={password.Length}";
            }
            else if (string.Equals(authMode, "bearer", StringComparison.OrdinalIgnoreCase))
            {
                var token = context.SubstituteVariables(options.Token ?? string.Empty);
                if (string.IsNullOrWhiteSpace(token))
                    return ApplyOnError(step, "Http auth 'bearer' requires non-empty token");

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                authSummary = $"bearer token_length={token.Length}";
            }

            var headers = BuildResolvedHeaders(options.Headers, context);
            headers.TryGetValue("Content-Type", out var explicitContentTypeHeader);
            var mappedContentType = string.IsNullOrWhiteSpace(contentTypeToken)
                ? null
                : ContentTypeMappings[contentTypeToken];
            var effectiveContentType = !string.IsNullOrWhiteSpace(explicitContentTypeHeader)
                ? explicitContentTypeHeader
                : mappedContentType;

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(effectiveContentType))
                {
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(effectiveContentType);
                }
            }

            ApplyHeaders(request, headers);

            context.EmitOutput($"Http: {method} {url}", ScriptOutputType.Debug);
            context.EmitOutput(
                $"Http: Request options auth={authSummary} timeout={timeoutSeconds}s follow_redirects={options.FollowRedirects} verify_tls={options.VerifyTls}",
                ScriptOutputType.Debug);
            context.EmitOutput($"Http: Request headers {SerializeHeaders(request)}", ScriptOutputType.Debug);
            if (body != null)
            {
                context.EmitOutput($"Http: Request body {ScriptingHelpers.FormatForDisplay(body)}", ScriptOutputType.Debug);
            }

            try
            {
                var apiSw = Stopwatch.StartNew();
                using var response = await client.SendAsync(request, cts.Token);
                apiSw.Stop();
                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
                totalSw.Stop();

                var apiMs = apiSw.ElapsedMilliseconds;
                var totalMs = totalSw.ElapsedMilliseconds;

                CaptureHttpResponse(
                    into,
                    context,
                    responseBody,
                    (int)response.StatusCode,
                    SerializeHeaders(response),
                    apiMs,
                    totalMs);

                context.EmitOutput(
                    $"Http: Timing endpoint={SummarizeEndpoint(uri)} status={(int)response.StatusCode} api_ms={apiMs} total_ms={totalMs}",
                    ScriptOutputType.Debug);
                context.EmitOutput($"Http: Response status {(int)response.StatusCode} {response.ReasonPhrase}", ScriptOutputType.Debug);
                context.EmitOutput($"Http: Response headers {SerializeHeaders(response)}", ScriptOutputType.Debug);
                context.EmitOutput($"Http: Response body {ScriptingHelpers.FormatForDisplay(responseBody)}", ScriptOutputType.Debug);

                if (!response.IsSuccessStatusCode)
                {
                    var error = $"Http failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                    if (options.AllowFailure)
                        return CommandResult.Ok(error);

                    context.EmitOutput(error, ScriptOutputType.Warning);

                    return ApplyOnError(step, error);
                }

                context.EmitOutput($"Http: Success ({(int)response.StatusCode})", ScriptOutputType.Debug);
                return CommandResult.Ok();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                totalSw.Stop();
                return ApplyOnError(step, $"Http timed out after {timeoutSeconds} seconds");
            }
            catch (HttpRequestException ex)
            {
                totalSw.Stop();
                return ApplyOnError(step, $"Http transport error: {ex.Message}");
            }
            catch (Exception ex)
            {
                totalSw.Stop();
                return ApplyOnError(step, $"Http error: {ex.Message}");
            }
        }

        private static Dictionary<string, string> BuildResolvedHeaders(
            Dictionary<string, string>? headers,
            ScriptContext context)
        {
            var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null || headers.Count == 0)
                return resolved;

            foreach (var kvp in headers)
            {
                resolved[kvp.Key] = context.SubstituteVariables(kvp.Value ?? string.Empty);
            }

            return resolved;
        }

        private static void ApplyHeaders(HttpRequestMessage request, Dictionary<string, string> headers)
        {
            foreach (var kvp in headers)
            {
                if (string.Equals(kvp.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    if (request.Content != null)
                    {
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(kvp.Value);
                    }
                    continue;
                }

                if (!request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value) && request.Content != null)
                {
                    request.Content.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }
        }

        private static void ClearCapture(string? into, ScriptContext context)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, string.Empty);
            context.SetVariable(into + "_status", string.Empty);
            context.SetVariable(into + "_headers", string.Empty);
            context.SetVariable(into + "_api_ms", string.Empty);
            context.SetVariable(into + "_total_ms", string.Empty);
        }

        private static void CaptureHttpResponse(
            string? into,
            ScriptContext context,
            string body,
            int status,
            string headersJson,
            long apiMs,
            long totalMs)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, body);
            context.SetVariable(into + "_status", status);
            context.SetVariable(into + "_headers", headersJson);
            context.SetVariable(into + "_api_ms", apiMs);
            context.SetVariable(into + "_total_ms", totalMs);
        }

        private static string SummarizeEndpoint(Uri uri)
        {
            var path = string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
            return uri.Host + path;
        }

        private static string SerializeHeaders(HttpResponseMessage response)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            foreach (var header in response.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            return JsonSerializer.Serialize(headers);
        }

        private static string SerializeHeaders(HttpRequestMessage request)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key] = string.Join(", ", header.Value);
                }
            }

            return JsonSerializer.Serialize(headers);
        }

        private static CommandResult ApplyOnError(ScriptStep step, string message)
            => CommandResult.ApplyOnError(step, message);

        private static HttpMessageHandler CreateDefaultHandler(HttpOptions options)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = options.FollowRedirects
            };

            if (!options.VerifyTls)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
        }
    }
}
