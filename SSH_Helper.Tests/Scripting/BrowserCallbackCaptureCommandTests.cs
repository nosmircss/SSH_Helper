using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class BrowserCallbackCaptureCommandTests
{
    [Fact]
    public async Task ExecuteAsync_OpenBrowserWithoutBrowserMode_UsesExternalUiHost()
    {
        var uiHost = new FakeBrowserCallbackUiHost();
        var command = new BrowserCallbackCaptureCommand(uiHost, CreateListener);
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                Into = "callback_data",
                OpenBrowser = true,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        uiHost.ExternalLaunchCount.Should().Be(1);
        uiHost.EmbeddedLaunchCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WebView2Mode_UsesEmbeddedUiHost_AndClosesSessionAfterCompletion()
    {
        var uiHost = new FakeBrowserCallbackUiHost();
        var command = new BrowserCallbackCaptureCommand(uiHost, CreateListener);
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                BrowserMode = "webview2",
                Into = "callback_data",
                OpenBrowser = true,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        uiHost.ExternalLaunchCount.Should().Be(0);
        uiHost.EmbeddedLaunchCount.Should().Be(1);
        uiHost.LastKeepWindowOpenOnSuccess.Should().BeFalse();
        uiHost.LastEmbeddedSession.Should().NotBeNull();
        uiHost.LastEmbeddedSession!.CloseCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_DoesNotCloseSessionAfterCompletion()
    {
        var uiHost = new FakeBrowserCallbackUiHost();
        var command = new BrowserCallbackCaptureCommand(uiHost, CreateListener);
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                BrowserMode = "webview2",
                Into = "callback_data",
                OpenBrowser = true,
                AutoCloseBrowser = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        uiHost.ExternalLaunchCount.Should().Be(0);
        uiHost.EmbeddedLaunchCount.Should().Be(1);
        uiHost.LastKeepWindowOpenOnSuccess.Should().BeTrue();
        uiHost.LastEmbeddedSession.Should().NotBeNull();
        uiHost.LastEmbeddedSession!.CloseCallCount.Should().Be(0);
        uiHost.LastEmbeddedSession.DisposeCallCount.Should().Be(0);
        uiHost.LastEmbeddedSession.MarkCompletedCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WebView2Mode_WithShowAfterSeconds_CompletesBeforeReveal()
    {
        var uiHost = new FakeBrowserCallbackUiHost();
        var command = new BrowserCallbackCaptureCommand(uiHost, CreateListener);
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                BrowserMode = "webview2",
                ShowAfterSeconds = 5,
                Into = "callback_data",
                OpenBrowser = true,
                Timeout = 10
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await uiHost.WaitForEmbeddedLaunchAsync();
        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        uiHost.LastShowAfterSeconds.Should().Be(5);
        uiHost.LastEmbeddedSession.Should().NotBeNull();
        uiHost.LastEmbeddedSession!.WasShown.Should().BeFalse();
        uiHost.LastEmbeddedSession.CloseCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WebView2Mode_WithAutoCloseBrowserFalse_CompletesBeforeReveal_DisposesHiddenSession()
    {
        var uiHost = new FakeBrowserCallbackUiHost();
        var command = new BrowserCallbackCaptureCommand(uiHost, CreateListener);
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                BrowserMode = "webview2",
                ShowAfterSeconds = 5,
                Into = "callback_data",
                OpenBrowser = true,
                AutoCloseBrowser = false,
                Timeout = 10
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await uiHost.WaitForEmbeddedLaunchAsync();
        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        uiHost.LastEmbeddedSession.Should().NotBeNull();
        uiHost.LastEmbeddedSession!.WasShown.Should().BeFalse();
        uiHost.LastEmbeddedSession.CloseCallCount.Should().Be(0);
        uiHost.LastEmbeddedSession.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WebView2Mode_WithShowAfterSeconds_RevealsAfterDelayWhenStillPending()
    {
        var uiHost = new FakeBrowserCallbackUiHost();
        var command = new BrowserCallbackCaptureCommand(uiHost, CreateListener);
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                BrowserMode = "webview2",
                ShowAfterSeconds = 1,
                Into = "callback_data",
                OpenBrowser = true,
                Timeout = 10
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await uiHost.WaitForEmbeddedLaunchAsync();
        await uiHost.LastEmbeddedSession!.WaitForShownAsync();

        uiHost.LastShowAfterSeconds.Should().Be(1);
        uiHost.LastEmbeddedSession.WasShown.Should().BeTrue();

        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        uiHost.LastEmbeddedSession.CloseCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WebView2Mode_WhenDialogClosedBeforeCallback_ReturnsFailure()
    {
        var uiHost = new FakeBrowserCallbackUiHost();
        var command = new BrowserCallbackCaptureCommand(uiHost, CreateListener);
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                BrowserMode = "webview2",
                Into = "callback_data",
                OpenBrowser = true,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await uiHost.WaitForEmbeddedLaunchAsync();
        uiHost.LastEmbeddedSession!.SignalClosedByUser();

        var result = await executeTask;

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("closed");
        uiHost.LastEmbeddedSession.CloseCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_OpenBrowserFalse_IgnoresBrowserMode()
    {
        var uiHost = new FakeBrowserCallbackUiHost();
        var command = new BrowserCallbackCaptureCommand(uiHost, CreateListener);
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                BrowserMode = "webview2",
                Into = "callback_data",
                OpenBrowser = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        uiHost.ExternalLaunchCount.Should().Be(0);
        uiHost.EmbeddedLaunchCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_QueryCapture_PersistsCapturedValues()
    {
        var command = new BrowserCallbackCaptureCommand();
        var context = new ScriptContext();
        var outputs = new List<(ScriptOutputType Type, string Message)>();
        context.OutputReceived += (_, e) => outputs.Add((e.Type, e.Message));
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                Into = "callback_data",
                OpenBrowser = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        context.GetVariableString("callback_data_code").Should().Be("abc123");
        context.GetVariableString("callback_data_state").Should().Be("xyz");
        context.GetVariableString("callback_data_count").Should().Be("2");
        outputs.Should().NotContain(entry =>
            entry.Type == ScriptOutputType.Success &&
            entry.Message.Contains("Captured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_QueryCapture_ReturnsAutoCloseHtmlResponse()
    {
        var command = new BrowserCallbackCaptureCommand();
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                Into = "callback_data",
                OpenBrowser = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        var responseBody = await SendWithRetryForBodyAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);

        await Task.Delay(200);
        executeTask.IsCompleted.Should().BeFalse("the command should wait for the browser completion acknowledgement before returning");

        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback/complete", HttpMethod.Post);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        responseBody.Should().Contain("window.close()", "query-mode callback completion should attempt to close the browser tab");
        responseBody.Should().Contain("Callback captured. You may close this tab.");
    }

    [Fact]
    public async Task ExecuteAsync_QueryCapture_ReturnsThemeAwareHtmlResponse()
    {
        var command = new BrowserCallbackCaptureCommand();
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                Into = "callback_data",
                OpenBrowser = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        var responseBody = await SendWithRetryForBodyAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);

        responseBody.Should().Contain("color-scheme: light dark");
        responseBody.Should().Contain("@media (prefers-color-scheme: dark)");
        responseBody.Should().Contain("background:");
        responseBody.Should().Contain("color:");

        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback/complete", HttpMethod.Post);
        var result = await executeTask;
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_QueryCapture_WithAutoCloseBrowserFalse_ReturnsStayOpenHtmlResponse()
    {
        var command = new BrowserCallbackCaptureCommand();
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                Into = "callback_data",
                AutoCloseBrowser = false,
                OpenBrowser = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        var responseBody = await SendWithRetryForBodyAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);

        await Task.Delay(200);
        executeTask.IsCompleted.Should().BeFalse();

        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback/complete", HttpMethod.Post);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        responseBody.Should().NotContain("window.close()", "auto_close_browser=false should keep the query completion page open");
        responseBody.Should().Contain("Callback captured. You may close this tab.");
    }

    [Fact]
    public async Task ExecuteAsync_FragmentCapture_BridgePostPersistsValues()
    {
        var command = new BrowserCallbackCaptureCommand();
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "fragment",
                Into = "callback_data",
                OpenBrowser = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);

        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback", HttpMethod.Get);

        var formData = new Dictionary<string, string>
        {
            ["h:access_token"] = "token-value",
            ["h:token_type"] = "Bearer",
            ["q:state"] = "ignored-for-fragment"
        };

        await SendWithRetryAsync(
            $"http://127.0.0.1:{port}/oauth_callback/capture",
            HttpMethod.Post,
            new FormUrlEncodedContent(formData));

        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback/complete", HttpMethod.Post);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        context.GetVariableString("callback_data_access_token").Should().Be("token-value");
        context.GetVariableString("callback_data_token_type").Should().Be("Bearer");
        context.GetVariableString("callback_data_count").Should().Be("2");
    }

    [Fact]
    public async Task ExecuteAsync_FragmentCapture_WithAutoCloseBrowserFalse_ReturnsStayOpenBridgeHtml()
    {
        var command = new BrowserCallbackCaptureCommand();
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "fragment",
                Into = "callback_data",
                AutoCloseBrowser = false,
                OpenBrowser = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        var responseBody = await SendWithRetryForBodyAsync($"http://127.0.0.1:{port}/oauth_callback", HttpMethod.Get);

        var formData = new Dictionary<string, string>
        {
            ["h:access_token"] = "token-value",
            ["h:token_type"] = "Bearer"
        };

        await SendWithRetryAsync(
            $"http://127.0.0.1:{port}/oauth_callback/capture",
            HttpMethod.Post,
            new FormUrlEncodedContent(formData));

        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback/complete", HttpMethod.Post);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        responseBody.Should().NotContain("window.close()", "auto_close_browser=false should keep the fragment bridge page open");
        responseBody.Should().Contain("Callback captured. You may close this tab.");
    }

    [Fact]
    public async Task ExecuteAsync_RequiredFieldsMissing_ReturnsFailure()
    {
        var command = new BrowserCallbackCaptureCommand();
        var context = new ScriptContext();
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                Into = "callback_data",
                RequiredFields = new List<string> { "access_token" },
                OpenBrowser = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?state=only", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("missing required field");
    }

    [Fact]
    public async Task ExecuteAsync_WhenQuietFalse_EmitsSuccessCaptureMessage()
    {
        var command = new BrowserCallbackCaptureCommand();
        var context = new ScriptContext();
        var outputs = new List<(ScriptOutputType Type, string Message)>();
        context.OutputReceived += (_, e) => outputs.Add((e.Type, e.Message));
        var port = GetFreePort();

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                Into = "callback_data",
                OpenBrowser = false,
                Quiet = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123&state=xyz", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        outputs.Should().Contain(entry =>
            entry.Type == ScriptOutputType.Success &&
            entry.Message.Contains("Captured 2 callback value(s) into ${callback_data}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_QueryCapture_ClearsStaleSuffixedValues()
    {
        var command = new BrowserCallbackCaptureCommand();
        var context = new ScriptContext();
        var port = GetFreePort();

        context.SetVariable("callback_data", "stale");
        context.SetVariable("callback_data_count", "1");
        context.SetVariable("callback_data_keys", "stale_code");
        context.SetVariable("callback_data_stale_code", "stale-value");

        var step = new ScriptStep
        {
            BrowserCallbackCapture = new BrowserCallbackCaptureOptions
            {
                StartUrl = "https://idp.example.com/start",
                CallbackPath = "/oauth_callback",
                LocalPort = port,
                CaptureMode = "query",
                Into = "callback_data",
                OpenBrowser = false,
                Timeout = 5
            }
        };

        var executeTask = command.ExecuteAsync(step, context, CancellationToken.None);
        await SendWithRetryAsync($"http://127.0.0.1:{port}/oauth_callback?code=abc123", HttpMethod.Get);
        await CompleteCallbackAsync(port);

        var result = await executeTask;

        result.Success.Should().BeTrue();
        context.GetVariableString("callback_data").Should().Be("{\"code\":\"abc123\"}");
        context.GetVariableString("callback_data_stale_code").Should().BeEmpty();
        context.GetVariableString("callback_data_count").Should().Be("1");
        context.GetVariableString("callback_data_code").Should().Be("abc123");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static HttpListener CreateListener(int port)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        return listener;
    }

    private static Task CompleteCallbackAsync(int port, string callbackPath = "/oauth_callback")
    {
        return SendWithRetryAsync($"http://127.0.0.1:{port}{callbackPath}/complete", HttpMethod.Post);
    }

    private static async Task SendWithRetryAsync(string url, HttpMethod method, HttpContent? content = null)
    {
        using var client = new HttpClient();
        byte[]? bodyBytes = null;
        string? mediaType = null;
        if (content != null)
        {
            bodyBytes = await content.ReadAsByteArrayAsync();
            mediaType = content.Headers.ContentType?.ToString();
        }

        Exception? lastException = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(method, url)
                {
                    Content = null
                };

                if (bodyBytes != null)
                {
                    request.Content = new ByteArrayContent(bodyBytes);
                    if (!string.IsNullOrWhiteSpace(mediaType))
                    {
                        request.Content.Headers.TryAddWithoutValidation("Content-Type", mediaType);
                    }
                }

                using var response = await client.SendAsync(request);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(100);
            }
        }

        throw new InvalidOperationException($"Failed to reach callback endpoint after retries: {url}", lastException);
    }

    private static async Task<string> SendWithRetryForBodyAsync(string url, HttpMethod method, HttpContent? content = null)
    {
        using var client = new HttpClient();
        byte[]? bodyBytes = null;
        string? mediaType = null;
        if (content != null)
        {
            bodyBytes = await content.ReadAsByteArrayAsync();
            mediaType = content.Headers.ContentType?.ToString();
        }

        Exception? lastException = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(method, url)
                {
                    Content = null
                };

                if (bodyBytes != null)
                {
                    request.Content = new ByteArrayContent(bodyBytes);
                    if (!string.IsNullOrWhiteSpace(mediaType))
                    {
                        request.Content.Headers.TryAddWithoutValidation("Content-Type", mediaType);
                    }
                }

                using var response = await client.SendAsync(request);
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(100);
            }
        }

        throw new InvalidOperationException($"Failed to reach callback endpoint after retries: {url}", lastException);
    }

    private sealed class FakeBrowserCallbackUiHost : IBrowserCallbackUiHost
    {
        private readonly TaskCompletionSource<bool> _embeddedLaunchTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ExternalLaunchCount { get; private set; }

        public int EmbeddedLaunchCount { get; private set; }

        public int LastShowAfterSeconds { get; private set; }

        public bool LastKeepWindowOpenOnSuccess { get; private set; }

        public FakeEmbeddedBrowserCallbackSession? LastEmbeddedSession { get; private set; }

        public Task WaitForEmbeddedLaunchAsync() => _embeddedLaunchTcs.Task;

        public Task<IBrowserCallbackUiSession> LaunchAsync(BrowserCallbackUiLaunchRequest request, CancellationToken cancellationToken)
        {
            if (request.Mode == BrowserCallbackUiMode.External)
            {
                ExternalLaunchCount++;
                return Task.FromResult<IBrowserCallbackUiSession>(new FakeExternalBrowserCallbackSession());
            }

            EmbeddedLaunchCount++;
            LastShowAfterSeconds = request.ShowAfterSeconds;
            LastKeepWindowOpenOnSuccess = request.KeepWindowOpenOnSuccess;
            LastEmbeddedSession = new FakeEmbeddedBrowserCallbackSession(request.ShowAfterSeconds);
            _embeddedLaunchTcs.TrySetResult(true);
            return Task.FromResult<IBrowserCallbackUiSession>(LastEmbeddedSession);
        }
    }

    private sealed class FakeExternalBrowserCallbackSession : IBrowserCallbackUiSession
    {
        private readonly TaskCompletionSource<bool> _closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BrowserCallbackUiMode Mode => BrowserCallbackUiMode.External;

        public Task ClosedByUser => _closedTcs.Task;

        public bool WasShownToUser => false;

        public int CloseCallCount { get; private set; }

        public int MarkCompletedCallCount { get; private set; }

        public ValueTask MarkCompletedAsync()
        {
            MarkCompletedCallCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseAsync()
        {
            CloseCallCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeEmbeddedBrowserCallbackSession : IBrowserCallbackUiSession
    {
        private readonly TaskCompletionSource<bool> _closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _shownTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _visibilityCts = new();

        public FakeEmbeddedBrowserCallbackSession(int showAfterSeconds)
        {
            if (showAfterSeconds <= 0)
            {
                WasShown = true;
                _shownTcs.TrySetResult(true);
            }
            else
            {
                _ = RevealLaterAsync(showAfterSeconds, _visibilityCts.Token);
            }
        }

        public BrowserCallbackUiMode Mode => BrowserCallbackUiMode.WebView2;

        public Task ClosedByUser => _closedTcs.Task;

        public bool WasShownToUser => WasShown;

        public int CloseCallCount { get; private set; }

        public int MarkCompletedCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public bool WasShown { get; private set; }

        public Task WaitForShownAsync() => _shownTcs.Task;

        public void SignalClosedByUser()
        {
            _visibilityCts.Cancel();
            _closedTcs.TrySetResult(true);
        }

        public ValueTask MarkCompletedAsync()
        {
            MarkCompletedCallCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseAsync()
        {
            CloseCallCount++;
            _visibilityCts.Cancel();
            _closedTcs.TrySetResult(true);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            _visibilityCts.Cancel();
            _visibilityCts.Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task RevealLaterAsync(int showAfterSeconds, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(showAfterSeconds), cancellationToken);
                WasShown = true;
                _shownTcs.TrySetResult(true);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
