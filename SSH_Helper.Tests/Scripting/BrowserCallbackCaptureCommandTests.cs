using System;
using System.Collections.Generic;
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

        var result = await executeTask;

        result.Success.Should().BeTrue();
        context.GetVariableString("callback_data_access_token").Should().Be("token-value");
        context.GetVariableString("callback_data_token_type").Should().Be("Bearer");
        context.GetVariableString("callback_data_count").Should().Be("2");
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
}
