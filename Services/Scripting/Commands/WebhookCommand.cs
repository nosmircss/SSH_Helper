using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Makes an HTTP request to a URL and optionally captures the response.
    /// </summary>
    public class WebhookCommand : IScriptCommand
    {
        // Reuse HttpClient for performance (recommended by Microsoft)
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly Func<HttpMessageHandler>? _handlerFactory;

        public WebhookCommand()
        {
        }

        internal WebhookCommand(Func<HttpMessageHandler> handlerFactory)
        {
            _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Webhook == null)
                return CommandResult.Fail("Webhook command has no options");

            var options = step.Webhook;
            var into = options.Into;

            // Clear capture variables up front so failure paths do not leave them undefined or stale.
            ClearCapture(into, context);

            if (string.IsNullOrEmpty(options.Url))
                return CommandResult.Fail("Webhook requires 'url'");

            try
            {
                // Substitute variables in URL and body
                var url = context.SubstituteVariables(options.Url);
                var method = options.Method?.ToUpperInvariant() ?? "POST";

                // Validate URL format
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    return CommandResult.Fail($"Invalid URL: {url}");

                if (uri.Scheme != "http" && uri.Scheme != "https")
                    return CommandResult.Fail($"URL must use http or https scheme: {url}");

                // SECURITY NOTE:
                // No private/internal destination filtering is applied here by design.
                // Scripts can target localhost and RFC1918 ranges for infrastructure automation,
                // so untrusted scripts may create SSRF-style internal probing behavior.

                // Create cancellation with timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(options.Timeout > 0 ? options.Timeout : 30));

                // Build the request
                var request = new HttpRequestMessage(new HttpMethod(method), uri);

                // Add headers
                if (options.Headers != null)
                {
                    foreach (var header in options.Headers)
                    {
                        var headerValue = context.SubstituteVariables(header.Value);
                        request.Headers.TryAddWithoutValidation(header.Key, headerValue);
                    }
                }

                // Add body for methods that support it
                if (!string.IsNullOrEmpty(options.Body) &&
                    (method == "POST" || method == "PUT" || method == "PATCH"))
                {
                    var body = context.SubstituteVariables(options.Body);

                    // Determine content type from headers or default to application/json
                    var contentType = "application/json";
                    if (options.Headers != null)
                    {
                        foreach (var header in options.Headers)
                        {
                            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                            {
                                contentType = context.SubstituteVariables(header.Value);
                                break;
                            }
                        }
                    }

                    request.Content = new StringContent(body, Encoding.UTF8, contentType);
                }

                context.EmitOutput($"Webhook: {method} {url}", ScriptOutputType.Debug);

                // Send the request
                using var ownedClient = CreateOwnedClient();
                var client = ownedClient ?? _httpClient;
                var response = await client.SendAsync(request, cts.Token);
                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

                // Capture response if requested
                if (!string.IsNullOrEmpty(options.Into))
                {
                    context.SetVariable(options.Into, responseBody);
                    context.SetVariable(options.Into + "_status", (int)response.StatusCode);
                    context.EmitOutput($"Webhook: Response captured to ${{{options.Into}}} (status: {(int)response.StatusCode})", ScriptOutputType.Debug);
                }

                // Check for success
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = $"Webhook failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                    context.EmitOutput(errorMsg, ScriptOutputType.Warning);

                    if (step.OnError?.ToLowerInvariant() == "continue")
                        return CommandResult.Suppressed(errorMsg);
                    return CommandResult.Fail(errorMsg);
                }

                context.EmitOutput($"Webhook: Success ({(int)response.StatusCode})", ScriptOutputType.Debug);
                return CommandResult.Ok();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred
                var errorMsg = $"Webhook timed out after {options.Timeout} seconds";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return CommandResult.Suppressed(errorMsg);
                return CommandResult.Fail(errorMsg);
            }
            catch (HttpRequestException ex)
            {
                var errorMsg = $"Webhook error: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return CommandResult.Suppressed(errorMsg);
                return CommandResult.Fail(errorMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Webhook error: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (step.OnError?.ToLowerInvariant() == "continue")
                    return CommandResult.Suppressed(errorMsg);
                return CommandResult.Fail(errorMsg);
            }
        }

        private HttpClient? CreateOwnedClient()
        {
            return _handlerFactory == null
                ? null
                : new HttpClient(_handlerFactory(), disposeHandler: true);
        }

        private static void ClearCapture(string? into, ScriptContext context)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, string.Empty);
            context.SetVariable(into + "_status", string.Empty);
        }
    }
}
