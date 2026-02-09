using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

            if (string.Equals(authMode, "basic", StringComparison.OrdinalIgnoreCase))
            {
                var username = context.SubstituteVariables(options.Username ?? string.Empty);
                var password = context.SubstituteVariables(options.Password ?? string.Empty);
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return ApplyOnError(step, "Http auth 'basic' requires non-empty username and password");

                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            }
            else if (string.Equals(authMode, "bearer", StringComparison.OrdinalIgnoreCase))
            {
                var token = context.SubstituteVariables(options.Token ?? string.Empty);
                if (string.IsNullOrWhiteSpace(token))
                    return ApplyOnError(step, "Http auth 'bearer' requires non-empty token");

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

            try
            {
                using var response = await client.SendAsync(request, cts.Token);
                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

                CaptureHttpResponse(into, context, responseBody, (int)response.StatusCode, SerializeHeaders(response));

                if (!response.IsSuccessStatusCode)
                {
                    var error = $"Http failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                    context.EmitOutput(error, ScriptOutputType.Warning);

                    if (options.AllowFailure)
                        return CommandResult.Ok(error);

                    return ApplyOnError(step, error);
                }

                context.EmitOutput($"Http: Success ({(int)response.StatusCode})", ScriptOutputType.Debug);
                return CommandResult.Ok();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ApplyOnError(step, $"Http timed out after {timeoutSeconds} seconds");
            }
            catch (HttpRequestException ex)
            {
                return ApplyOnError(step, $"Http transport error: {ex.Message}");
            }
            catch (Exception ex)
            {
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
        }

        private static void CaptureHttpResponse(string? into, ScriptContext context, string body, int status, string headersJson)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, body);
            context.SetVariable(into + "_status", status);
            context.SetVariable(into + "_headers", headersJson);
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

        private static CommandResult ApplyOnError(ScriptStep step, string message)
        {
            if (string.Equals(step.OnError, "continue", StringComparison.OrdinalIgnoreCase))
                return CommandResult.Suppressed(message);

            return CommandResult.Fail(message);
        }

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
